
using BaiTapLopLapTrinhMang.Helpers;
using BaiTapLopLapTrinhMang1.Models; 
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

namespace BaiTapLopLapTrinhMang
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
		private const int UDP_BROADCAST_PORT = 8888;

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

				// Chạy trong nền
				_ = Task.Run(() => AcceptClientsAsync(_cts.Token), _cts.Token);
			}
			catch (SocketException sockEx)
			{
				UpdateStatus($"Error starting server (Socket): {sockEx.Message} (Error Code: {sockEx.SocketErrorCode})");
				ShutdownServer();
			}
			catch (Exception ex)
			{
				UpdateStatus($"Error starting server: {ex.Message}");
				ShutdownServer();
			}
		}

		private async Task AcceptClientsAsync(CancellationToken token)
		{
			UpdateStatus("Accept loop started.");
			while (!token.IsCancellationRequested)
			{
				try
				{
					TcpClient client = await _listener.AcceptTcpClientAsync();

					if (token.IsCancellationRequested)
					{
						client.Close();
						break;
					}

					IPEndPoint endpoint = client.Client.RemoteEndPoint as IPEndPoint;
					if (endpoint == null)
					{
						UpdateStatus("Failed to get client endpoint. Closing connection.");
						client.Close();
						continue;
					}
					string clientId = endpoint.ToString();

					UpdateStatus($"Client attempting connection from {clientId}");

					var clientInfo = new ClientInfo(endpoint.Address.ToString(), endpoint.Port, "Pending MAC...");
					clientInfo.LastActivity = DateTime.UtcNow;

					if (_clients.TryAdd(clientId, (client, clientInfo)))
					{

						BeginInvoke((Action)(() =>
						{
							if (!_listClient.Any(c => c.ClientId == clientId))
							{
								_listClient.Add(clientInfo);
							}
						}));
						UpdateStatus($"Client connected: {clientId}");

						_ = Task.Run(() => HandleClientAsync(client, clientId, token), token);
					}
					else
					{
						UpdateStatus($"Failed to add client {clientId} to dictionary (already exists?). Closing connection.");
						client.Close();
					}
				}
				catch (ObjectDisposedException) when (token.IsCancellationRequested || _listener == null)
				{
					UpdateStatus("Listener stopped, exiting accept loop.");
					break;
				}
				catch (SocketException se) when (token.IsCancellationRequested)
				{
					UpdateStatus($"Socket exception during accept (likely shutdown): {se.Message}");
					break;
				}
				catch (SocketException se)
				{
					UpdateStatus($"Socket error accepting client: {se.Message}");
					await Task.Delay(100, CancellationToken.None);
				}
				catch (Exception ex) when (!token.IsCancellationRequested)
				{
					UpdateStatus($"Error accepting client: {ex.GetType().Name} - {ex.Message}");
					await Task.Delay(100, CancellationToken.None);
				}
			}
			UpdateStatus("Accept loop finished.");
		}


		private async Task HandleClientAsync(TcpClient client, string clientId, CancellationToken token)
		{
			NetworkStream stream = null;
			string clientDesc = clientId;

			try
			{
				clientDesc = client.Client.RemoteEndPoint?.ToString() ?? clientId;
				stream = client.GetStream();
				byte[] buffer = new byte[1024];

				while (!token.IsCancellationRequested && client.Connected)
				{
					var readCts = CancellationTokenSource.CreateLinkedTokenSource(token);


					int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, readCts.Token);
					if (bytesRead > 0)
					{

						if (_clients.TryGetValue(clientId, out var clientTuple))
						{
							clientTuple.info.LastActivity = DateTime.UtcNow;
						}
						else
						{
							UpdateStatus($"Warning: Client {clientDesc} sent data but not found in dictionary.");
						}

						string message = Encoding.ASCII.GetString(buffer, 0, bytesRead).Trim();
						UpdateStatus($"Received from {clientDesc}: {message}");


						if (message.Equals("PONG", StringComparison.OrdinalIgnoreCase))
						{

						}
						/*						else if (message.Equals("PING", StringComparison.OrdinalIgnoreCase))
												{
													byte[] response = Encoding.ASCII.GetBytes("PONG");
													await stream.WriteAsync(response, 0, response.Length, CancellationToken.None);
													UpdateStatus($"Sent PONG to {clientDesc} (in response to unexpected PING)");
												}*/
						else
						{
							UpdateClientMac(clientId, message);
						}
					}
					else
					{
						UpdateStatus($"Client {clientDesc} disconnected gracefully (read 0 bytes).");
						break;
					}
				}
			}
			catch (OperationCanceledException) when (token.IsCancellationRequested)
			{
				UpdateStatus($"Handling canceled for client {clientDesc} (Server shutdown?).");
			}
			catch (OperationCanceledException)
			{
				UpdateStatus($"Read timeout for client {clientDesc}.");
			}
			catch (IOException ioEx)
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
			catch (Exception ex) when (!token.IsCancellationRequested)
			{
				UpdateStatus($"Unhandled error handling client {clientDesc}: {ex.GetType().Name} - {ex.Message}");
			}
			finally
			{
				UpdateStatus($"Initiating cleanup for client {clientDesc}.");
				RemoveClient(clientId);


				try { stream?.Close(); } catch (Exception ex) { UpdateStatus($"Error closing stream for {clientDesc}: {ex.Message}"); }
				try { client?.Close(); } catch (Exception ex) { UpdateStatus($"Error closing client {clientDesc}: {ex.Message}"); }
				client?.Dispose();

				UpdateStatus($"Finished handling and cleanup for client {clientDesc}.");
			}
		}


		private void ShutdownServer()
		{
			if (!_isRunning && _cts.IsCancellationRequested)
			{
				UpdateStatus("Shutdown already in progress or server stopped.");
				return;
			}

			_isRunning = false;
			UpdateStatus("Shutting down server...");


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


			try
			{
				_listener?.Stop();
				UpdateStatus("Listener stopped.");
			}
			catch (ObjectDisposedException)
			{
				UpdateStatus("Listener already stopped/disposed (likely due to cancellation).");
			}
			catch (Exception ex)
			{
				UpdateStatus($"Error stopping listener: {ex.Message}");
			}
			_listener = null;

			UpdateStatus("Disconnecting clients...");

			var clientsToDisconnect = _clients.ToList();
			_clients.Clear();

			foreach (var clientEntry in clientsToDisconnect)
			{
				string clientId = clientEntry.Key;
				TcpClient client = clientEntry.Value.client;
				try
				{
					UpdateStatus($"Disconnecting client: {clientId}");
					if (client.Connected)
					{
						NetworkStream stream = client.GetStream();
						byte[] disconnectMsg = Encoding.ASCII.GetBytes("DISCONNECT");

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

					try { client.Close(); } catch { }
					try { client.Dispose(); } catch { }
					UpdateStatus($"Closed and disposed client: {clientId}");
				}
			}

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
				BeginInvoke(clearBindingListAction);
			}
			else
			{
				clearBindingListAction();
			}

			UpdateStatus("Server shut down complete.");
			Console.WriteLine("Server shut down.");

			_cts?.Dispose();
			_cts = new CancellationTokenSource();

			// Cập nhật lại trạng thái UI trên UI thread
			Action updateUiAction = () =>
			{
				StartStopBtn.Text = "Start";
				ipAddressCbx.Enabled = true;
				portNup.Enabled = true;
				timer1.Stop();
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
		}


		private void LogToFile(string message)
		{
			try
			{
				// Tạo thư mục Logs nếu chưa có
				string logDirectory = Path.Combine(Application.StartupPath, "Logs");
				Directory.CreateDirectory(logDirectory);
				string logPath = Path.Combine(logDirectory, $"server_log_{DateTime.Now:yyyyMMdd}.txt");


				lock (this)
				{
					using (StreamWriter writer = File.AppendText(logPath))
					{
						writer.WriteLine(message);
					}
				}
			}
			catch (Exception ex)
			{
			}
		}


		private void UpdateClientMac(string clientId, string macAddress)
		{
			// Bỏ qua nếu MAC không hợp lệ hoặc là giá trị mặc định
			if (string.IsNullOrWhiteSpace(macAddress) || macAddress.Length < 12 || macAddress.Equals("Pending MAC...", StringComparison.OrdinalIgnoreCase))
			{
				return;
			}

			if (_clients.TryGetValue(clientId, out var clientTuple))
			{
				ClientInfo info = clientTuple.info;
				if (info.MacAddress != macAddress)
				{
					info.MacAddress = macAddress;
					UpdateStatus($"Updated MAC for {clientId} to {macAddress}");

					ResetBindingListItem(info);
				}
			}
			else
			{
				UpdateStatus($"Warning: Cannot update MAC for non-existent client ID: {clientId}");
			}
		}


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
				ClientInfoDgv.BeginInvoke(resetAction);
			}
			else
			{
				resetAction();
			}
		}


		private void RefreshDataGridView()
		{
			if (ClientInfoDgv.InvokeRequired)
			{
				ClientInfoDgv.BeginInvoke((Action)(() => ClientInfoDgv.Refresh()));
			}
			else
			{
				ClientInfoDgv.Refresh();
			}
		}

		private void RemoveClient(string clientId)
		{
			UpdateStatus($"Attempting to remove client: {clientId}");

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

				if (this.IsHandleCreated && !this.IsDisposed)
				{
					if (InvokeRequired)
					{
						BeginInvoke(removeListAction);
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

			}
			else
			{
				UpdateStatus($"Client {clientId} already removed or not found in dictionary.");
			}
		}


		private bool CheckForm()
		{
			errorProvider1.Clear();

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


		private void ServerForm_FormClosing(object sender, FormClosingEventArgs e)
		{
			UpdateStatus("Form closing event triggered.");
			if (_isRunning)
			{
				UpdateStatus("Server is running, initiating shutdown...");
				ShutdownServer();
			}
			else
			{
				try
				{
					_cts?.Cancel();
					_cts?.Dispose();
				}
				catch (ObjectDisposedException) { } // Bỏ qua nếu đã disposed
				UpdateStatus("Server was not running. Disposed CancellationTokenSource.");
			}
		}


		private void KeepAliveTimer_Tick(object sender, EventArgs e)
		{

			if (!_isRunning || (_cts != null && _cts.IsCancellationRequested)) // Kiểm tra _cts null
			{
				return;
			}


			_ = CheckClientsAsync();
		}

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


			var clientIds = _clients.Keys.ToList();
			Console.WriteLine($"DEBUG: Checking {clientIds.Count} clients.");

			foreach (var clientId in clientIds)
			{
				Console.WriteLine($"DEBUG: Checking client {clientId}...");
				if (!_clients.TryGetValue(clientId, out var clientTuple))
				{
					Console.WriteLine($"DEBUG: Client {clientId} vanished before check.");
					continue;
				}

				TcpClient client = clientTuple.client;
				ClientInfo info = clientTuple.info;


				TimeSpan timeSinceLastActivity = utcNow - info.LastActivity;
				Console.WriteLine($"DEBUG: Client {clientId} - Time since last activity: {timeSinceLastActivity.TotalSeconds:F1}s / Timeout: {_clientTimeout.TotalSeconds}s");
				if (timeSinceLastActivity > _clientTimeout)
				{
					UpdateStatus($"Client {clientId} timed out (Last activity: {info.LastActivity:HH:mm:ss UTC}). Marking for removal.");
					clientsToRemove.Add(clientId);
					Console.WriteLine($"DEBUG: Client {clientId} marked for removal due to timeout.");
					continue;
				}


				if (client.Connected)
				{
					Console.WriteLine($"DEBUG: Client {clientId} is connected. Attempting PING.");
					try
					{
						NetworkStream stream = client.GetStream();
						byte[] pingMessage = Encoding.ASCII.GetBytes("PING");

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
						break;
					}
					catch (OperationCanceledException ex)
					{
						UpdateStatus($"PING write operation to {clientId} timed out. Marking for removal. {ex.Message}");
						Console.WriteLine($"DEBUG: PING write timeout for {clientId}. Marking for removal.");
						clientsToRemove.Add(clientId);
					}
					catch (IOException ioEx)
					{
						UpdateStatus($"IO Error sending PING to {clientId}: {ioEx.Message}. Marking for removal.");
						Console.WriteLine($"DEBUG: IO Error sending PING to {clientId}. Marking for removal.");
						clientsToRemove.Add(clientId);
					}
					catch (Exception ex)
					{
						UpdateStatus($"Error sending PING to {clientId}: {ex.GetType().Name} - {ex.Message}. Marking for removal.");
						Console.WriteLine($"DEBUG: Generic error sending PING to {clientId}. Marking for removal.");
						clientsToRemove.Add(clientId);
					}
				}
				else
				{
					UpdateStatus($"Client {clientId} reported as disconnected by TcpClient. Marking for removal.");
					Console.WriteLine($"DEBUG: Client {clientId} reported disconnected. Marking for removal.");
					clientsToRemove.Add(clientId);
				}
			}


			Console.WriteLine($"DEBUG: Removing {clientsToRemove.Count} clients.");
			foreach (var clientIdToRemove in clientsToRemove)
			{
				RemoveClient(clientIdToRemove);
			}
			Console.WriteLine("DEBUG: CheckClientsAsync finished.");
		}


		private async void tatMayTinhBtn_Click(object sender, EventArgs e)
		{
			if (ClientInfoDgv.SelectedRows.Count == 0)
			{
				MessageBox.Show("Please select a client from the table.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}
			var selectedRow = ClientInfoDgv.SelectedRows[0];
			if (selectedRow.DataBoundItem == null)
			{
				MessageBox.Show("Selected row does not contain client data.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}
			var selectedClientInfo = (ClientInfo)selectedRow.DataBoundItem;


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


			if (_clients.TryGetValue(clientId, out var clientTuple) && clientTuple.client.Connected)
			{
				try
				{
					NetworkStream stream = clientTuple.client.GetStream();
					byte[] shutdownMessage = Encoding.ASCII.GetBytes("SHUTDOWN");
					UpdateStatus($"Attempting to send SHUTDOWN command to {clientId}...");

					using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
					{
						await stream.WriteAsync(shutdownMessage, 0, shutdownMessage.Length, cts.Token);
						UpdateStatus($"Sent SHUTDOWN command to {clientId}. Client should disconnect or shut down.");

					}
				}
				catch (OperationCanceledException)
				{
					UpdateStatus($"Timeout sending SHUTDOWN to {clientId}. Removing client.");
					RemoveClient(clientId);
				}
				catch (IOException ioEx)
				{
					UpdateStatus($"IO Error sending SHUTDOWN to {clientId}: {ioEx.Message}. Removing client.");
					RemoveClient(clientId);
				}
				catch (Exception ex)
				{
					UpdateStatus($"Error sending SHUTDOWN to {clientId}: {ex.GetType().Name} - {ex.Message}. Removing client.");
					RemoveClient(clientId);
				}
			}
			else
			{
				MessageBox.Show($"Client {clientId} is no longer connected or available.", "Client Unavailable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				// Đảm bảo client không còn tồn tại trong danh sách
				UpdateStatus($"Client {clientId} not found or disconnected when trying to send SHUTDOWN.");
				RemoveClient(clientId); // Gọi Remove để dọn dẹp nếu còn sót
			}
		}

		private async void button1_Click(object sender, EventArgs e) // disconnect
		{
			if (!_isRunning)
			{
				MessageBox.Show("Server is not running.", "Server Offline", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}

			if (ClientInfoDgv.SelectedRows.Count == 0)
			{
				MessageBox.Show("Please select a client from the table to disconnect.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}

			var selectedRow = ClientInfoDgv.SelectedRows[0];
			if (selectedRow.DataBoundItem == null)
			{
				MessageBox.Show("Selected row does not contain valid client data.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}
			var selectedClientInfo = (ClientInfo)selectedRow.DataBoundItem;
			string clientId = selectedClientInfo.ClientId;

			var confirmResult = MessageBox.Show($"Are you sure you want to disconnect client {clientId}?",
												 "Confirm Disconnection",
												 MessageBoxButtons.YesNo,
												 MessageBoxIcon.Question);

			if (confirmResult == DialogResult.No)
				return;


			if (_clients.TryGetValue(clientId, out var clientTuple))
			{
				TcpClient clientToDisconnect = clientTuple.client;
				UpdateStatus($"Attempting to disconnect client: {clientId}");

				try
				{
					if (clientToDisconnect.Connected)
					{
						try
						{
							NetworkStream stream = clientToDisconnect.GetStream();
							byte[] disconnectMsg = Encoding.ASCII.GetBytes("DISCONNECT");
							using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
							{
								await stream.WriteAsync(disconnectMsg, 0, disconnectMsg.Length, cts.Token);
								UpdateStatus($"Sent DISCONNECT notification to {clientId}.");
							}
						}
						catch (Exception writeEx)
						{

							UpdateStatus($"Could not send DISCONNECT notification to {clientId} (may already be disconnected): {writeEx.Message}");
						}
					}


					clientToDisconnect.Close();
					UpdateStatus($"Closed connection for client {clientId} from server side.");


				}
				catch (Exception ex)
				{
					UpdateStatus($"Error during disconnection process for {clientId}: {ex.Message}");

				}
				finally
				{
					RemoveClient(clientId);
				}
			}
			else
			{
				MessageBox.Show($"Client {clientId} is no longer connected or could not be found.", "Client Unavailable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				UpdateStatus($"Client {clientId} not found in dictionary when attempting to disconnect.");
				RemoveClient(clientId);
			}
		}

		private void ServerForm_Load_1(object sender, EventArgs e)
		{

		}



		private void button2_Click(object sender, EventArgs e)
		{
			try
			{
				if (_listener == null)
				{
					MessageBox.Show("Can not start find client, server does not start.");
					UpdateStatus("Cand not send boardcast. Server does not start");
					return;
				}

				IPEndPoint endPoint = (IPEndPoint)_listener.LocalEndpoint;
				string serverIp = endPoint.Address.ToString();
				int serverPort = endPoint.Port;

				string broadcastMessage = $"CONNECT:{serverIp}:{serverPort}";
				byte[] data = Encoding.ASCII.GetBytes(broadcastMessage);


				using (UdpClient udpClient = new UdpClient())
				{
					udpClient.EnableBroadcast = true;

					IPEndPoint broadcastEndPoint = new IPEndPoint(IPAddress.Broadcast, UDP_BROADCAST_PORT);

					udpClient.Send(data, data.Length, broadcastEndPoint);

					UpdateStatus($"Sent boardcast: {broadcastMessage}");
				}
			}
			catch (Exception ex)
			{
				UpdateStatus($"Error when sent broadcast: {ex.Message}");
			}
		}
	}
}