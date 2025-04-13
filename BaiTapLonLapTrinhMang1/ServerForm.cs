// Thêm các using cần thiết ở đầu file
using BaiTapLopLapTrinhMang.Helpers;
using BaiTapLopLapTrinhMang1.Models; // Thêm using cho Models
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

// Đảm bảo namespace phù hợp (có thể là BaiTapLopLapTrinhMang.Forms)
namespace BaiTapLopLapTrinhMang // Hoặc namespace gốc nếu không dùng thư mục Forms
{
	public partial class ServerForm : Form
	{
		private volatile bool _isRunning = false;
		private BindingList<ClientInfo> _listClient = new BindingList<ClientInfo>();
		private TcpListener _listener;
		private ConcurrentDictionary<string, (TcpClient client, ClientInfo info)> _clients = new ConcurrentDictionary<string, (TcpClient, ClientInfo)>();
		private CancellationTokenSource _cts = new CancellationTokenSource();

		private readonly TimeSpan _clientTimeout = TimeSpan.FromSeconds(30);
		private readonly TimeSpan _pingInterval = TimeSpan.FromSeconds(10);

		public ServerForm()
		{
			InitializeComponent();
			try
			{
				ipAddressCbx.DataSource = NetworkHelper.GetAllIPAddresses();
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Design-time/Runtime error getting IP Addresses: {ex.Message}");
				ipAddressCbx.Items.Add("127.0.0.1");
				ipAddressCbx.Items.Add("Error loading IPs");
				ipAddressCbx.SelectedIndex = 0;
			}

			_listClient.AllowNew = true;
			_listClient.AllowRemove = true;
			_listClient.AllowEdit = true;
			ClientInfoDgv.DataSource = _listClient;
		}
		private void ServerForm_Load(object sender, EventArgs e)
		{
			ConfigureDataGridViewColumns();
		}

		private void ConfigureDataGridViewColumns()
		{
			try
			{
				if (ClientInfoDgv.Columns["IpAddress"] != null)
					ClientInfoDgv.Columns["IpAddress"].HeaderText = "IP Address";
				if (ClientInfoDgv.Columns["Port"] != null)
					ClientInfoDgv.Columns["Port"].HeaderText = "Port";
				if (ClientInfoDgv.Columns["MacAddress"] != null)
					ClientInfoDgv.Columns["MacAddress"].HeaderText = "MAC Address";
				if (ClientInfoDgv.Columns["LastActivity"] != null)
				{
					ClientInfoDgv.Columns["LastActivity"].HeaderText = "Last Activity (UTC)";
					ClientInfoDgv.Columns["LastActivity"].DefaultCellStyle.Format = "yyyy-MM-dd HH:mm:ss";
				}
				if (ClientInfoDgv.Columns["ClientId"] != null)
					ClientInfoDgv.Columns["ClientId"].Visible = false;

				ClientInfoDgv.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error configuring DataGridView columns on Load: {ex.Message}");
				MessageBox.Show($"Error configuring DataGridView columns: {ex.Message}", "Configuration Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			}
		}


		private async void StartStopBtn_Click(object sender, EventArgs e)
		{
			if (!_isRunning)
			{
				if (!CheckForm()) return;
			}

			_isRunning = !_isRunning;

			if (_isRunning)
			{
				StartStopBtn.Text = "Stop";
				ipAddressCbx.Enabled = false;
				portNup.Enabled = false;

				_cts = new CancellationTokenSource();

				string selectedIp = ipAddressCbx.SelectedItem?.ToString() ?? ipAddressCbx.Text;
				int selectedPort = (int)portNup.Value;
				await StartServerAsync(new ServerSetting(selectedIp, selectedPort));

				timer1.Interval = (int)_pingInterval.TotalMilliseconds;
				timer1.Start();
			}
			else
			{
				StartStopBtn.Text = "Start";
				ipAddressCbx.Enabled = true;
				portNup.Enabled = true;

				timer1.Stop();
				ShutdownServer();
			}
		}

		private async Task StartServerAsync(ServerSetting settings)
		{

			if (string.IsNullOrWhiteSpace(settings.IpAddress))
			{
				UpdateStatus("Error: Invalid IP address selected for server.");

				Invoke((Action)(() =>
				{
					StartStopBtn.Text = "Start";
					ipAddressCbx.Enabled = true;
					portNup.Enabled = true;
					_isRunning = false;
				}));
				return;
			}

			IPAddress ipAddress;
			if (!IPAddress.TryParse(settings.IpAddress, out ipAddress))
			{
				UpdateStatus($"Error: Cannot parse IP address '{settings.IpAddress}'.");
				Invoke((Action)(() =>
				{
					StartStopBtn.Text = "Start";
					ipAddressCbx.Enabled = true;
					portNup.Enabled = true;
					_isRunning = false;
				}));
				return;
			}

			_listener = new TcpListener(ipAddress, settings.PortNumber);

			try
			{
				_listener.Start();
				UpdateStatus($"Server started on {ipAddress}:{settings.PortNumber}. Listening...");
				Console.WriteLine("Server started. Listening for connections...");

				// Run client acceptance in the background
				_ = Task.Run(() => AcceptClientsAsync(_cts.Token), _cts.Token);
			}
			catch (SocketException sockEx) // Bắt lỗi cụ thể hơn
			{
				UpdateStatus($"Error starting server (Socket): {sockEx.Message} (Error Code: {sockEx.SocketErrorCode})");
				ShutdownServer(); // Ensure cleanup on error
			}
			catch (Exception ex)
			{
				UpdateStatus($"Error starting server: {ex.Message}");
				ShutdownServer(); // Ensure cleanup on error
			}
		}

		private async Task AcceptClientsAsync(CancellationToken token)
		{
			UpdateStatus("Accept loop started.");
			while (!token.IsCancellationRequested) // Check token primarily
			{
				try
				{
					TcpClient client = await _listener.AcceptTcpClientAsync();
					// Check token again after await
					if (token.IsCancellationRequested)
					{
						client.Close();
						break;
					}

					IPEndPoint endpoint = client.Client.RemoteEndPoint as IPEndPoint;
					if (endpoint == null) // Kiểm tra phòng trường hợp RemoteEndPoint không phải IPEndPoint
					{
						UpdateStatus("Failed to get client endpoint. Closing connection.");
						client.Close();
						continue;
					}
					string clientId = endpoint.ToString();

					UpdateStatus($"Client attempting connection from {clientId}");

					var clientInfo = new ClientInfo(endpoint.Address.ToString(), endpoint.Port, "Pending MAC...");
					clientInfo.LastActivity = DateTime.UtcNow; // Set initial activity time

					if (_clients.TryAdd(clientId, (client, clientInfo)))
					{
						// Add to BindingList on UI thread
						// Sử dụng BeginInvoke để không chặn luồng Accept nếu UI đang bận
						BeginInvoke((Action)(() =>
						{
							if (!_listClient.Any(c => c.ClientId == clientId)) // Kiểm tra trùng lặp trước khi thêm vào UI list
							{
								_listClient.Add(clientInfo);
							}
						}));
						UpdateStatus($"Client connected: {clientId}");

						// Handle client communication in a separate task
						_ = Task.Run(() => HandleClientAsync(client, clientId, token), token);
					}
					else
					{
						UpdateStatus($"Failed to add client {clientId} to dictionary (already exists?). Closing connection.");
						client.Close(); // Close the duplicate connection attempt
					}
				}
				catch (ObjectDisposedException) when (token.IsCancellationRequested || _listener == null)
				{
					// Listener was stopped, expected during shutdown
					UpdateStatus("Listener stopped, exiting accept loop.");
					break;
				}
				catch (SocketException se) when (token.IsCancellationRequested)
				{
					UpdateStatus($"Socket exception during accept (likely shutdown): {se.Message}");
					break;
				}
				catch (SocketException se) // Lỗi socket khác khi đang chạy
				{
					UpdateStatus($"Socket error accepting client: {se.Message}");
					await Task.Delay(100, CancellationToken.None); // Delay trước khi thử lại
				}
				catch (Exception ex) when (!token.IsCancellationRequested)
				{
					UpdateStatus($"Error accepting client: {ex.GetType().Name} - {ex.Message}");
					// Optional: Delay slightly before retrying to prevent tight loop on persistent errors
					await Task.Delay(100, CancellationToken.None);
				}
			}
			UpdateStatus("Accept loop finished.");
		}


		private async Task HandleClientAsync(TcpClient client, string clientId, CancellationToken token)
		{
			NetworkStream stream = null;
			string clientDesc = clientId; // Mô tả client để dùng trong log/lỗi

			try
			{
				clientDesc = client.Client.RemoteEndPoint?.ToString() ?? clientId; // Lấy thông tin mới nhất nếu có
				stream = client.GetStream();
				byte[] buffer = new byte[1024]; // 1KB buffer

				while (!token.IsCancellationRequested && client.Connected)
				{
					// Set read timeout carefully
					// stream.ReadTimeout = (int)(_clientTimeout.TotalMilliseconds * 1.5); // Đặt timeout có thể gây IOException nếu không có dữ liệu

					// Sử dụng ReadAsync với CancellationToken để có thể hủy bỏ
					var readCts = CancellationTokenSource.CreateLinkedTokenSource(token);
					// readCts.CancelAfter(_clientTimeout + TimeSpan.FromSeconds(15)); // Timeout cho việc đọc (hơi phức tạp hơn chỉ ReadTimeout)

					int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, readCts.Token /*token*/); // Sử dụng token gốc hoặc linked token
					if (bytesRead > 0)
					{
						// Update last activity time whenever data is received
						if (_clients.TryGetValue(clientId, out var clientTuple))
						{
							// Cập nhật trên đối tượng info trực tiếp
							clientTuple.info.LastActivity = DateTime.UtcNow;
						}
						else
						{
							UpdateStatus($"Warning: Client {clientDesc} sent data but not found in dictionary.");
							// Có thể break hoặc bỏ qua tùy logic
						}

						string message = Encoding.ASCII.GetString(buffer, 0, bytesRead).Trim();
						UpdateStatus($"Received from {clientDesc}: {message}");

						// Xử lý tin nhắn
						if (message.Equals("PONG", StringComparison.OrdinalIgnoreCase))
						{
							// PONG received, activity time updated. Log if needed.
							// UpdateStatus($"PONG received from {clientDesc}");
						}
						else if (message.Equals("PING", StringComparison.OrdinalIgnoreCase))
						{
							// Client không nên gửi PING, nhưng ta phản hồi nếu có
							byte[] response = Encoding.ASCII.GetBytes("PONG");
							// Gửi phản hồi PONG không cần chờ lâu hoặc token phức tạp
							await stream.WriteAsync(response, 0, response.Length, CancellationToken.None);
							UpdateStatus($"Sent PONG to {clientDesc} (in response to unexpected PING)");
						}
						else
						{
							// Mặc định là MAC address hoặc tin nhắn khác
							UpdateClientMac(clientId, message); // Cập nhật MAC
						}
					}
					else
					{
						// Read 0 bytes: Client đóng kết nối một cách bình thường
						UpdateStatus($"Client {clientDesc} disconnected gracefully (read 0 bytes).");
						break; // Exit loop
					}
				}
			}
			catch (OperationCanceledException) when (token.IsCancellationRequested)
			{
				// Bị hủy bỏ do server shutdown hoặc lý do khác từ token gốc
				UpdateStatus($"Handling canceled for client {clientDesc} (Server shutdown?).");
			}
			catch (OperationCanceledException)
			{
				// Bị hủy bỏ do timeout đọc (nếu dùng CancelAfter trên readCts)
				UpdateStatus($"Read timeout for client {clientDesc}.");
			}
			catch (IOException ioEx) // Lỗi IO thường gặp khi mất kết nối
			{
				var socketEx = ioEx.InnerException as SocketException;
				if (socketEx != null)
				{
					UpdateStatus($"IO/Socket Error for client {clientDesc}: {socketEx.SocketErrorCode} - {socketEx.Message}");
				}
				else
				{
					UpdateStatus($"IO Error for client {clientDesc}: {ioEx.Message}");
				}
			}
			catch (Exception ex) when (!token.IsCancellationRequested) // Các lỗi khác khi server vẫn đang chạy
			{
				UpdateStatus($"Unhandled error handling client {clientDesc}: {ex.GetType().Name} - {ex.Message}");
				// Ghi chi tiết lỗi vào log nếu cần: LogToFile(ex.ToString());
			}
			// Khối finally luôn được thực thi để dọn dẹp
			finally
			{
				UpdateStatus($"Initiating cleanup for client {clientDesc}.");
				// Đảm bảo client được xóa khỏi danh sách và tài nguyên được giải phóng
				RemoveClient(clientId);

				// Đóng stream và client một cách an toàn
				try { stream?.Close(); } catch (Exception ex) { UpdateStatus($"Error closing stream for {clientDesc}: {ex.Message}"); }
				try { client?.Close(); } catch (Exception ex) { UpdateStatus($"Error closing client {clientDesc}: {ex.Message}"); }
				client?.Dispose(); // Gọi Dispose để giải phóng hoàn toàn

				UpdateStatus($"Finished handling and cleanup for client {clientDesc}.");
			}
		}

		// ---- SHUTDOWN SERVER ----
		private void ShutdownServer()
		{
			if (!_isRunning && _cts.IsCancellationRequested)
			{
				UpdateStatus("Shutdown already in progress or server stopped.");
				return; // Đã shutdown hoặc đang shutdown
			}

			_isRunning = false; // Đặt trạng thái ngưng chạy
			UpdateStatus("Shutting down server...");

			// Hủy các tác vụ đang chạy (Accept, HandleClient, CheckClients)
			try
			{
				_cts.Cancel();
				UpdateStatus("Cancellation requested for all tasks.");
			}
			catch (ObjectDisposedException)
			{
				UpdateStatus("CancellationTokenSource already disposed.");
			}
			catch (Exception ex)
			{
				UpdateStatus($"Error cancelling tasks: {ex.Message}");
			}

			// Dừng Listener để không nhận kết nối mới
			try
			{
				// Kiểm tra listener có tồn tại và đang lắng nghe không
				// if (_listener != null && _listener.Server != null && _listener.Server.IsBound)
				// {
				_listener?.Stop(); // Có thể ném ObjectDisposedException nếu CTS đã cancel AcceptTcpClientAsync
				UpdateStatus("Listener stopped.");
				// }
			}
			catch (ObjectDisposedException)
			{
				UpdateStatus("Listener already stopped/disposed (likely due to cancellation).");
			}
			catch (Exception ex)
			{
				UpdateStatus($"Error stopping listener: {ex.Message}");
			}
			_listener = null; // Đặt listener thành null

			UpdateStatus("Disconnecting clients...");
			// Ngắt kết nối tất cả client hiện tại một cách an toàn
			var clientsToDisconnect = _clients.ToList(); // Tạo bản sao để tránh lỗi thay đổi collection khi lặp
			_clients.Clear(); // Xóa ngay lập tức để ngăn HandleClient cố gắng truy cập lại

			foreach (var clientEntry in clientsToDisconnect)
			{
				string clientId = clientEntry.Key;
				TcpClient client = clientEntry.Value.client;
				try
				{
					UpdateStatus($"Disconnecting client: {clientId}");
					if (client.Connected) // Chỉ cố gửi nếu còn connected
					{
						NetworkStream stream = client.GetStream();
						byte[] disconnectMsg = Encoding.ASCII.GetBytes("DISCONNECT");
						// Gửi không đồng bộ với timeout ngắn, không cần đợi hoàn thành tuyệt đối
						using (var disconnectCts = new CancellationTokenSource(TimeSpan.FromSeconds(1)))
						{
							stream.WriteAsync(disconnectMsg, 0, disconnectMsg.Length, disconnectCts.Token).ContinueWith(t =>
							{
								if (t.IsFaulted) UpdateStatus($"Error sending DISCONNECT to {clientId}: {t.Exception?.InnerException?.Message}");
							});
						}
					}
				}
				catch (Exception ex)
				{
					UpdateStatus($"Exception during client disconnect preparation for {clientId}: {ex.Message}");
				}
				finally
				{
					// Luôn đóng và giải phóng client
					try { client.Close(); } catch { /* Ignored */ }
					try { client.Dispose(); } catch { /* Ignored */}
					UpdateStatus($"Closed and disposed client: {clientId}");
				}
			}

			// Xóa danh sách trên UI thread
			Action clearBindingListAction = () =>
			{
				if (_listClient != null)
				{
					_listClient.Clear();
					UpdateStatus("Client list cleared from UI.");
				}
			};

			if (InvokeRequired)
			{
				BeginInvoke(clearBindingListAction); // Dùng BeginInvoke để không bị block nếu UI thread bận
			}
			else
			{
				clearBindingListAction();
			}

			UpdateStatus("Server shut down complete.");
			Console.WriteLine("Server shut down.");

			// Giải phóng CTS cũ và tạo mới nếu cần khởi động lại
			_cts?.Dispose();
			_cts = new CancellationTokenSource();

			// Cập nhật lại trạng thái UI trên UI thread
			Action updateUiAction = () =>
			{
				StartStopBtn.Text = "Start";
				ipAddressCbx.Enabled = true;
				portNup.Enabled = true;
				timer1.Stop(); // Đảm bảo timer dừng
				UpdateStatus("UI controls re-enabled.");
			};
			if (InvokeRequired)
			{
				BeginInvoke(updateUiAction);
			}
			else
			{
				updateUiAction();
			}
		}

		private void UpdateStatus(string message)
		{
			// Log vào Console và File như cũ
			string logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}: {message}";
			Console.WriteLine(logMessage);
			LogToFile(logMessage);

			// Cập nhật StatusStrip (nếu có) trên UI Thread
			// if (statusStrip1 != null && statusStripLabel1 != null) // Kiểm tra control tồn tại
			// {
			//      Action updateLabelAction = () => statusStripLabel1.Text = message;
			//      if (statusStrip1.InvokeRequired)
			//      {
			//           statusStrip1.BeginInvoke(updateLabelAction);
			//      }
			//      else
			//      {
			//           updateLabelAction();
			//      }
			// }
		}


		private void LogToFile(string message)
		{
			try
			{
				// Tạo thư mục Logs nếu chưa có
				string logDirectory = Path.Combine(Application.StartupPath, "Logs");
				Directory.CreateDirectory(logDirectory);
				string logPath = Path.Combine(logDirectory, $"server_log_{DateTime.Now:yyyyMMdd}.txt");

				// Sử dụng lock để tránh ghi file đồng thời từ nhiều luồng (mặc dù UpdateStatus thường gọi tuần tự)
				lock (this) // Lock trên đối tượng Form là đủ đơn giản ở đây
				{
					using (StreamWriter writer = File.AppendText(logPath))
					{
						writer.WriteLine(message);
					}
				}
			}
			catch (Exception ex)
			{
				// Ghi lỗi ra Console vì không thể ghi file log
				Console.WriteLine($"CRITICAL - Error writing to log file: {ex.Message}");
			}
		}

		// ---- UPDATE CLIENT MAC ----
		private void UpdateClientMac(string clientId, string macAddress)
		{
			// Bỏ qua nếu MAC không hợp lệ hoặc là giá trị mặc định
			if (string.IsNullOrWhiteSpace(macAddress) || macAddress.Length < 12 || macAddress.Equals("Pending MAC...", StringComparison.OrdinalIgnoreCase))
			{
				return;
			}

			// Chuẩn hóa MAC address (ví dụ: thành chữ hoa, bỏ dấu phân cách nếu cần)
			// macAddress = macAddress.Replace("-","").Replace(":","").ToUpper();

			if (_clients.TryGetValue(clientId, out var clientTuple))
			{
				ClientInfo info = clientTuple.info;
				// Chỉ cập nhật và refresh nếu MAC thực sự thay đổi
				if (info.MacAddress != macAddress)
				{
					info.MacAddress = macAddress; // Thuộc tính này sẽ gọi OnPropertyChanged
					UpdateStatus($"Updated MAC for {clientId} to {macAddress}");

					// Refresh DataGridView trên UI thread
					// RefreshDataGridView(); // Cách này refresh toàn bộ grid, có thể không hiệu quả

					// Tìm index và reset item để BindingList tự cập nhật row đó (hiệu quả hơn)
					ResetBindingListItem(info);
				}
			}
			else
			{
				UpdateStatus($"Warning: Cannot update MAC for non-existent client ID: {clientId}");
			}
		}

		// Helper để refresh một item cụ thể trong BindingList
		private void ResetBindingListItem(ClientInfo itemToReset)
		{
			Action resetAction = () =>
			{
				int index = _listClient.IndexOf(itemToReset);
				if (index >= 0)
				{
					_listClient.ResetItem(index);
				}
			};

			if (ClientInfoDgv.InvokeRequired)
			{
				ClientInfoDgv.BeginInvoke(resetAction); // Dùng BeginInvoke
			}
			else
			{
				resetAction();
			}
		}


		// Helper to refresh the DGV on the UI thread (ít dùng hơn nếu dùng ResetItem)
		private void RefreshDataGridView()
		{
			if (ClientInfoDgv.InvokeRequired)
			{
				// Dùng BeginInvoke thay vì Invoke để tránh block
				ClientInfoDgv.BeginInvoke((Action)(() => ClientInfoDgv.Refresh()));
			}
			else
			{
				ClientInfoDgv.Refresh();
			}
		}

		// ---- REMOVE CLIENT ----
		private void RemoveClient(string clientId)
		{
			UpdateStatus($"Attempting to remove client: {clientId}");
			// Xóa khỏi dictionary trước, lấy ra tuple đã xóa nếu thành công
			if (_clients.TryRemove(clientId, out var removedTuple))
			{
				UpdateStatus($"Removed client {clientId} from dictionary.");
				// Xóa khỏi BindingList trên UI thread
				Action removeListAction = () =>
				{
					var clientInfoToRemove = _listClient.FirstOrDefault(c => c.ClientId == clientId);
					if (clientInfoToRemove != null)
					{
						bool removed = _listClient.Remove(clientInfoToRemove);
						if (removed)
						{
							UpdateStatus($"Client {clientId} removed from UI list.");
						}
						else
						{
							UpdateStatus($"Failed to remove client {clientId} from UI list (already removed?).");
						}
					}
					else
					{
						UpdateStatus($"Client {clientId} not found in UI list for removal.");
					}
				};

				if (this.IsHandleCreated && !this.IsDisposed) // Chỉ Invoke/BeginInvoke nếu Form còn tồn tại
				{
					if (InvokeRequired)
					{
						BeginInvoke(removeListAction); // Sử dụng BeginInvoke
					}
					else
					{
						removeListAction();
					}
				}
				else
				{
					UpdateStatus($"Form disposed or handle not created, skipping UI list removal for {clientId}.");
				}

				// Đóng và giải phóng TcpClient một cách an toàn trong finally của HandleClientAsync đã đủ
				// Không cần đóng lại ở đây nữa trừ khi có logic đặc biệt
				// try { removedTuple.client?.Close(); } catch { /* Ignore */ }
				// try { removedTuple.client?.Dispose(); } catch { /* Ignore */ }
			}
			else
			{
				UpdateStatus($"Client {clientId} already removed or not found in dictionary.");
			}
		}

		// ---- CHECK FORM ----
		private bool CheckForm()
		{
			errorProvider1.Clear();
			// Kiểm tra xem có item nào được chọn không hoặc text có hợp lệ không
			if (ipAddressCbx.SelectedItem == null && string.IsNullOrWhiteSpace(ipAddressCbx.Text))
			{
				errorProvider1.SetError(ipAddressCbx, "Please choose or enter a valid IP address for the server");
				return false;
			}
			string selectedIp = ipAddressCbx.SelectedItem?.ToString() ?? ipAddressCbx.Text;
			if (!IPAddress.TryParse(selectedIp, out _))
			{
				errorProvider1.SetError(ipAddressCbx, "Invalid IP address format.");
				return false;
			}
			return true;
		}

		// ---- FORM CLOSING ----
		private void ServerForm_FormClosing(object sender, FormClosingEventArgs e)
		{
			UpdateStatus("Form closing event triggered.");
			if (_isRunning)
			{
				UpdateStatus("Server is running, initiating shutdown...");
				ShutdownServer();
				// Cân nhắc không nên đợi ở đây vì có thể làm treo UI khi đóng form
				// Task.Delay(500).Wait();
			}
			else
			{
				// Đảm bảo CTS được hủy và giải phóng ngay cả khi server không chạy
				try
				{
					_cts?.Cancel();
					_cts?.Dispose();
				}
				catch (ObjectDisposedException) { } // Bỏ qua nếu đã disposed
				UpdateStatus("Server was not running. Disposed CancellationTokenSource.");
			}
		}

		// ---- KEEP ALIVE TIMER TICK ----
		private void KeepAliveTimer_Tick(object sender, EventArgs e)
		{
			// Log khi timer tick để debug
			// Console.WriteLine($"DEBUG: KeepAliveTimer_Tick - isRunning: {_isRunning}, isCancellationRequested: {_cts.IsCancellationRequested}");

			// Không chạy nếu server đang dừng hoặc đang trong quá trình shutdown
			if (!_isRunning || (_cts != null && _cts.IsCancellationRequested)) // Kiểm tra _cts null
			{
				// Không cần stop timer ở đây, Start/Stop button và Shutdown quản lý việc này
				// timer1.Stop();
				return;
			}

			// Chạy kiểm tra client bất đồng bộ
			_ = CheckClientsAsync();
		}

		// ---- CHECK CLIENTS ASYNC ----
		private async Task CheckClientsAsync()
		{
			// Log khi bắt đầu kiểm tra
			Console.WriteLine("DEBUG: CheckClientsAsync started.");
			if (!_isRunning || (_cts != null && _cts.IsCancellationRequested))
			{
				Console.WriteLine("DEBUG: CheckClientsAsync skipped (not running or cancellation requested).");
				return;
			}

			DateTime utcNow = DateTime.UtcNow;
			List<string> clientsToRemove = new List<string>();

			// Tạo bản sao key an toàn để lặp
			var clientIds = _clients.Keys.ToList();
			Console.WriteLine($"DEBUG: Checking {clientIds.Count} clients.");

			foreach (var clientId in clientIds)
			{
				Console.WriteLine($"DEBUG: Checking client {clientId}...");
				if (!_clients.TryGetValue(clientId, out var clientTuple))
				{
					Console.WriteLine($"DEBUG: Client {clientId} vanished before check.");
					continue; // Client đã bị xóa bởi luồng khác
				}

				TcpClient client = clientTuple.client;
				ClientInfo info = clientTuple.info;

				// 1. Kiểm tra Timeout trước
				TimeSpan timeSinceLastActivity = utcNow - info.LastActivity;
				Console.WriteLine($"DEBUG: Client {clientId} - Time since last activity: {timeSinceLastActivity.TotalSeconds:F1}s / Timeout: {_clientTimeout.TotalSeconds}s");
				if (timeSinceLastActivity > _clientTimeout)
				{
					UpdateStatus($"Client {clientId} timed out (Last activity: {info.LastActivity:HH:mm:ss UTC}). Marking for removal.");
					clientsToRemove.Add(clientId);
					Console.WriteLine($"DEBUG: Client {clientId} marked for removal due to timeout.");
					continue; // Chuyển sang client tiếp theo
				}

				// 2. Nếu không timeout, gửi PING
				if (client.Connected) // Kiểm tra trạng thái Connected của TcpClient
				{
					Console.WriteLine($"DEBUG: Client {clientId} is connected. Attempting PING.");
					try
					{
						NetworkStream stream = client.GetStream();
						byte[] pingMessage = Encoding.ASCII.GetBytes("PING");

						// Gửi PING với timeout ngắn và liên kết với CancellationToken chính
						using (var writeCts = new CancellationTokenSource(_pingInterval.Subtract(TimeSpan.FromSeconds(1)))) // Timeout gửi < ping interval
						using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, writeCts.Token))
						{
							Console.WriteLine($"DEBUG: Sending PING to {clientId}...");
							await stream.WriteAsync(pingMessage, 0, pingMessage.Length, linkedCts.Token);
							Console.WriteLine($"DEBUG: PING sent to {clientId}.");
						}
					}
					catch (OperationCanceledException) when (_cts.IsCancellationRequested)
					{
						UpdateStatus($"PING sending cancelled for {clientId} (server shutting down).");
						Console.WriteLine($"DEBUG: PING cancelled for {clientId} (server shutdown).");
						break; // Dừng gửi PING nếu server shutdown
					}
					catch (OperationCanceledException ex) // Write timed out
					{
						UpdateStatus($"PING write operation to {clientId} timed out. Marking for removal. {ex.Message}");
						Console.WriteLine($"DEBUG: PING write timeout for {clientId}. Marking for removal.");
						clientsToRemove.Add(clientId);
					}
					catch (IOException ioEx) // Lỗi IO khi gửi (vd: kết nối đã đóng)
					{
						UpdateStatus($"IO Error sending PING to {clientId}: {ioEx.Message}. Marking for removal.");
						Console.WriteLine($"DEBUG: IO Error sending PING to {clientId}. Marking for removal.");
						clientsToRemove.Add(clientId);
					}
					catch (Exception ex) // Lỗi khác
					{
						UpdateStatus($"Error sending PING to {clientId}: {ex.GetType().Name} - {ex.Message}. Marking for removal.");
						Console.WriteLine($"DEBUG: Generic error sending PING to {clientId}. Marking for removal.");
						clientsToRemove.Add(clientId);
					}
				}
				else // Nếu client.Connected là false
				{
					UpdateStatus($"Client {clientId} reported as disconnected by TcpClient. Marking for removal.");
					Console.WriteLine($"DEBUG: Client {clientId} reported disconnected. Marking for removal.");
					clientsToRemove.Add(clientId);
				}
			}

			// 3. Xóa các client đã đánh dấu
			Console.WriteLine($"DEBUG: Removing {clientsToRemove.Count} clients.");
			foreach (var clientIdToRemove in clientsToRemove)
			{
				RemoveClient(clientIdToRemove); // Phương thức này đã xử lý log và dọn dẹp
			}
			Console.WriteLine("DEBUG: CheckClientsAsync finished.");
		}

		// ---- SHUTDOWN BUTTON CLICK ----
		private async void tatMayTinhBtn_Click(object sender, EventArgs e)
		{
			// Lấy dòng được chọn
			if (ClientInfoDgv.SelectedRows.Count == 0)
			{
				MessageBox.Show("Please select a client from the table.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}
			var selectedRow = ClientInfoDgv.SelectedRows[0];
			if (selectedRow.DataBoundItem == null) // Kiểm tra nếu dòng không có dữ liệu (hiếm gặp)
			{
				MessageBox.Show("Selected row does not contain client data.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}
			var selectedClientInfo = (ClientInfo)selectedRow.DataBoundItem;

			// Xác nhận trước khi gửi lệnh
			var confirmResult = MessageBox.Show($"Are you sure you want to send a shutdown command to client {selectedClientInfo.ClientId} ({selectedClientInfo.MacAddress})?",
												 "Confirm Shutdown Command",
												 MessageBoxButtons.YesNo,
												 MessageBoxIcon.Question);

			if (confirmResult == DialogResult.No)
				return;


			if (!_isRunning)
			{
				MessageBox.Show("Server is not running. Cannot send command.", "Server Offline", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			string clientId = selectedClientInfo.ClientId;

			// Kiểm tra client còn tồn tại và kết nối không
			if (_clients.TryGetValue(clientId, out var clientTuple) && clientTuple.client.Connected)
			{
				try
				{
					NetworkStream stream = clientTuple.client.GetStream();
					byte[] shutdownMessage = Encoding.ASCII.GetBytes("SHUTDOWN");
					UpdateStatus($"Attempting to send SHUTDOWN command to {clientId}...");
					// Gửi với timeout
					using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
					{
						await stream.WriteAsync(shutdownMessage, 0, shutdownMessage.Length, cts.Token);
						UpdateStatus($"Sent SHUTDOWN command to {clientId}. Client should disconnect or shut down.");
						// Không tự động xóa client ở đây, để cơ chế PING/PONG hoặc client tự ngắt kết nối xử lý
					}
				}
				catch (OperationCanceledException) // Gửi bị timeout
				{
					UpdateStatus($"Timeout sending SHUTDOWN to {clientId}. Removing client.");
					RemoveClient(clientId); // Xóa client nếu không gửi được lệnh
				}
				catch (IOException ioEx) // Lỗi IO (vd: client đã ngắt kết nối trước khi gửi)
				{
					UpdateStatus($"IO Error sending SHUTDOWN to {clientId}: {ioEx.Message}. Removing client.");
					RemoveClient(clientId);
				}
				catch (Exception ex) // Lỗi khác
				{
					UpdateStatus($"Error sending SHUTDOWN to {clientId}: {ex.GetType().Name} - {ex.Message}. Removing client.");
					RemoveClient(clientId); // Xóa client nếu có lỗi nghiêm trọng
				}
			}
			else // Client không tìm thấy trong dictionary hoặc không connected
			{
				MessageBox.Show($"Client {clientId} is no longer connected or available.", "Client Unavailable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				// Đảm bảo client không còn tồn tại trong danh sách
				UpdateStatus($"Client {clientId} not found or disconnected when trying to send SHUTDOWN.");
				RemoveClient(clientId); // Gọi Remove để dọn dẹp nếu còn sót
			}
		}

		private async void button1_Click(object sender, EventArgs e)
		{
			if (!_isRunning)
			{
				MessageBox.Show("Server is not running.", "Server Offline", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}

			// 2. Kiểm tra xem có client nào được chọn không
			if (ClientInfoDgv.SelectedRows.Count == 0)
			{
				MessageBox.Show("Please select a client from the table to disconnect.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}

			// 3. Lấy thông tin client được chọn
			var selectedRow = ClientInfoDgv.SelectedRows[0];
			if (selectedRow.DataBoundItem == null)
			{
				MessageBox.Show("Selected row does not contain valid client data.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}
			var selectedClientInfo = (ClientInfo)selectedRow.DataBoundItem;
			string clientId = selectedClientInfo.ClientId;

			// 4. Xác nhận hành động (Tùy chọn nhưng nên có)
			var confirmResult = MessageBox.Show($"Are you sure you want to disconnect client {clientId}?",
												 "Confirm Disconnection",
												 MessageBoxButtons.YesNo,
												 MessageBoxIcon.Question);

			if (confirmResult == DialogResult.No)
				return;

			// 5. Tìm client trong dictionary và thực hiện ngắt kết nối
			if (_clients.TryGetValue(clientId, out var clientTuple))
			{
				TcpClient clientToDisconnect = clientTuple.client;
				UpdateStatus($"Attempting to disconnect client: {clientId}");

				try
				{
					// 5a. (Optional but recommended) Gửi tin nhắn DISCONNECT cho client biết lý do
					if (clientToDisconnect.Connected)
					{
						try
						{
							NetworkStream stream = clientToDisconnect.GetStream();
							byte[] disconnectMsg = Encoding.ASCII.GetBytes("DISCONNECT");
							using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2))) // Timeout ngắn để gửi
							{
								await stream.WriteAsync(disconnectMsg, 0, disconnectMsg.Length, cts.Token);
								UpdateStatus($"Sent DISCONNECT notification to {clientId}.");
							}
						}
						catch (Exception writeEx)
						{
							// Lỗi khi gửi tin nhắn (có thể client đã ngắt kết nối) - vẫn tiếp tục đóng phía server
							UpdateStatus($"Could not send DISCONNECT notification to {clientId} (may already be disconnected): {writeEx.Message}");
						}
					}

					// 5b. Đóng kết nối từ phía Server (Quan trọng)
					clientToDisconnect.Close(); // Đóng socket
					UpdateStatus($"Closed connection for client {clientId} from server side.");

					// 5c. Xóa client khỏi quản lý (Tự động gọi trong bước tiếp theo)
					// Không cần gọi clientToDisconnect.Dispose() ở đây vì RemoveClient sẽ xử lý

				}
				catch (Exception ex)
				{
					UpdateStatus($"Error during disconnection process for {clientId}: {ex.Message}");
					// Dù có lỗi, vẫn cố gắng xóa client khỏi danh sách
				}
				finally
				{
					// 5d. Gọi RemoveClient để xóa khỏi dictionary và UI list, và Dispose client
					RemoveClient(clientId);
				}
			}
			else // Không tìm thấy client trong dictionary (có thể đã bị xóa bởi cơ chế timeout)
			{
				MessageBox.Show($"Client {clientId} is no longer connected or could not be found.", "Client Unavailable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				UpdateStatus($"Client {clientId} not found in dictionary when attempting to disconnect.");
				// Gọi RemoveClient để đảm bảo dọn dẹp nếu còn sót trong UI list
				RemoveClient(clientId);
			}
		}

		private void ServerForm_Load_1(object sender, EventArgs e)
		{

		}
	}
}