using BaiTapLopLapTrinhMang.Helpers;
using IWshRuntimeLibrary;
using System;
using System.Management;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BaiTapLopLapTrinhMang
{
	public partial class ClientForm : Form
	{
		private volatile bool _isConnected = false;
		private TcpClient _client;
		private NetworkStream _stream;
		private CancellationTokenSource _cts = new CancellationTokenSource();


		private UdpClient _udpListener;
		private const int UDP_PORT = 8888;
		private IPEndPoint _broadcastEndPoint;
		private CancellationTokenSource _udpCts = new CancellationTokenSource();

		public ClientForm()
		{
			InitializeComponent();
			_client = new TcpClient();

			InitializeUdpListener();

			//AddShortcutToStartup();

		}

		private void AddShortcutToStartup()
		{
			string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
			string shortcutPath = Path.Combine(startupFolder, "MyApp.lnk");

			if (!System.IO.File.Exists(shortcutPath))
			{
				WshShell shell = new WshShell();
				IWshShortcut shortcut = (IWshShortcut)shell.CreateShortcut(shortcutPath);
				shortcut.TargetPath = Application.ExecutablePath;
				shortcut.WorkingDirectory = Application.StartupPath;
				shortcut.WindowStyle = 1;
				shortcut.Description = "Khởi động MyApp cùng Windows";
				shortcut.Save();
			}
		}

		private void InitializeUdpListener()
		{
			try
			{
				_udpListener = new UdpClient();
				_udpListener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
				_broadcastEndPoint = new IPEndPoint(IPAddress.Any, UDP_PORT);
				_udpListener.Client.Bind(_broadcastEndPoint);

				UpdateStatus("UDP listener initialized on port " + UDP_PORT);

				// Start listening for broadcast messages
				_ = Task.Run(() => ListenForBroadcastsAsync(_udpCts.Token), _udpCts.Token);
			}
			catch (Exception ex)
			{
				UpdateStatus($"Failed to initialize UDP listener: {ex.Message}");
			}
		}

		private async Task ListenForBroadcastsAsync(CancellationToken token)
		{
			try
			{
				UpdateStatus("Starting to listen for server broadcasts...");

				while (!token.IsCancellationRequested)
				{
					UdpReceiveResult result = await _udpListener.ReceiveAsync();
					string message = Encoding.ASCII.GetString(result.Buffer);
					UpdateStatus($"Received broadcast: {message}");

					ProcessBroadcastMessage(message, result.RemoteEndPoint);
				}
			}
			catch (OperationCanceledException)
			{
				UpdateStatus("UDP listening canceled.");
			}
			catch (Exception ex)
			{
				UpdateStatus($"UDP listening error: {ex.Message}");

				await Task.Delay(5000, CancellationToken.None);
				if (!token.IsCancellationRequested)
				{
					_ = Task.Run(() => ListenForBroadcastsAsync(token), token);
				}
			}
		}

		private void ProcessBroadcastMessage(string message, IPEndPoint remoteEndPoint)
		{
			try
			{
				// chuỗi gửi đến sẽ như sau "CONNECT:192.168.1.100:9000"
				if (message.StartsWith("CONNECT:"))
				{
					string[] parts = message.Substring(8).Split(':');
					if (parts.Length == 2 && IPAddress.TryParse(parts[0], out IPAddress serverIp) && int.TryParse(parts[1], out int serverPort))
					{
						UpdateStatus($"Server {serverIp}:{serverPort} is requesting connection");

						if (InvokeRequired)
						{
							Invoke((Action)(() => AskToConnect(serverIp.ToString(), serverPort)));
						}
						else
						{
							AskToConnect(serverIp.ToString(), serverPort);
						}
					}
				}
			}
			catch (Exception ex)
			{
				UpdateStatus($"Error processing broadcast message: {ex.Message}");
			}
		}

		private void AskToConnect(string serverIp, int serverPort)
		{
			if (_isConnected)
			{
				UpdateStatus("Already connected to a server. Ignoring connection request.");
				return;
			}

			DialogResult result = MessageBox.Show(
				$"Server at {serverIp}:{serverPort} is requesting connection. Do you want to connect?",
				"Connection Request",
				MessageBoxButtons.YesNo,
				MessageBoxIcon.Question);

			if (result == DialogResult.Yes)
			{
				serverIpAddressTbx.Text = serverIp;
				serverPortNud.Value = serverPort;

				ConnectBtn_Click(this, EventArgs.Empty);
			}
			else
			{
				UpdateStatus("Connection request declined by user.");
			}
		}

		private async void ConnectBtn_Click(object sender, EventArgs e)
		{
			bool currentlyConnected = _isConnected;

			if (!currentlyConnected && !CheckForm())
			{
				return;
			}

			_isConnected = !currentlyConnected;

			if (_isConnected)
			{
				ConnectBtn.Text = "Disconnect";
				_cts = new CancellationTokenSource();

				if (_client == null || !_client.Connected)
				{
					_client = new TcpClient();
				}
				await ConnectAsync(serverIpAddressTbx.Text, (int)serverPortNud.Value);
			}
			else
			{
				Disconnect();
				ConnectBtn.Text = "Connect";
			}
		}

		private async Task ConnectAsync(string serverIp, int port)
		{
			try
			{
				IPAddress ipAddress = IPAddress.Parse(serverIp);
				await _client.ConnectAsync(ipAddress, port);
				_stream = _client.GetStream();
				_isConnected = true;

				UpdateStatus("Connected to server.");
				Console.WriteLine("Connected to server.");

				string macAddress = GetAllInformation();

				byte[] data = Encoding.ASCII.GetBytes(macAddress);

				await _stream.WriteAsync(data, 0, data.Length, _cts.Token);
				UpdateStatus($"Sent MAC: {macAddress}");


				_ = Task.Run(() => ListenAsync(_cts.Token), _cts.Token);
			}
			catch (OperationCanceledException)
			{
				UpdateStatus("Connection attempt canceled.");
				DisconnectInternal();
			}
			catch (Exception ex)
			{
				UpdateStatus($"Error connecting to server: {ex.Message}");
				DisconnectInternal();

				if (InvokeRequired) Invoke((Action)(() => ConnectBtn.Text = "Connect"));
				else ConnectBtn.Text = "Connect";
				_isConnected = false;
			}
		}

		private async Task ListenAsync(CancellationToken token)
		{
			byte[] buffer = new byte[1024];

			try
			{
				while (_isConnected && _client.Connected && !token.IsCancellationRequested)
				{
					int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, token);
					if (bytesRead > 0)
					{
						string message = Encoding.ASCII.GetString(buffer, 0, bytesRead).Trim();
						UpdateStatus($"Received: {message}");

						if (message == "DISCONNECT")
						{
							UpdateStatus("Server requested disconnection.");
							Disconnect();
							break;
						}
						else if (message == "PING")
						{
							byte[] response = Encoding.ASCII.GetBytes("PONG");
							await _stream.WriteAsync(response, 0, response.Length, token);
							UpdateStatus("Sent: PONG");
						}

						else if (message == "SHUTDOWN")
						{
							UpdateStatus("Server requested computer shutdown.");
							ShutdownComputer();
							break;
						}

					}
					else
					{

						if (_isConnected)
						{
							UpdateStatus("Server disconnected gracefully.");
							Disconnect();
						}
						break;
					}
				}
			}
			catch (OperationCanceledException)
			{
				UpdateStatus("Listening canceled.");
			}
			catch (System.IO.IOException ioEx) when (ioEx.InnerException is SocketException se && (se.SocketErrorCode == SocketError.ConnectionReset || se.SocketErrorCode == SocketError.ConnectionAborted))
			{
				if (_isConnected)
				{
					UpdateStatus($"Connection lost: {se.SocketErrorCode}");
					Disconnect();
				}
			}
			catch (Exception ex) when (_isConnected)
			{
				UpdateStatus($"Connection error: {ex.Message}");
				Disconnect();
			}
			finally
			{
				if (_isConnected)
				{
					Disconnect();
				}
			}
		}

		private void Disconnect()
		{
			if (!_isConnected && _client == null) return;

			_isConnected = false;
			DisconnectInternal();

			// Update UI on the correct thread
			if (InvokeRequired)
			{
				Invoke((Action)(() =>
				{
					ConnectBtn.Text = "Connect";
					UpdateStatus("Client disconnected.");
				}));
			}
			else
			{
				ConnectBtn.Text = "Connect";
				UpdateStatus("Client disconnected.");
			}
			Console.WriteLine("Client disconnected.");
		}

		private void DisconnectInternal()
		{

			_cts?.Cancel();
			_cts?.Dispose();
			_cts = new CancellationTokenSource();


			try { _stream?.Close(); } catch { }
			try { _client?.Close(); } catch { }
			_stream = null;
			_client = null;
		}


		private string GetMacAddress()
		{
			if (_client?.Client != null && _client.Client.LocalEndPoint != null)
			{
				var localEndPoint = _client.Client.LocalEndPoint as IPEndPoint;
				if (localEndPoint != null)
				{
					IPAddress localIP = localEndPoint.Address;
					if (localIP.IsIPv4MappedToIPv6)
					{
						localIP = localIP.MapToIPv4();
					}

					string macAddress = NetworkHelper.GetMacAddress(localIP);
					Console.WriteLine($"MAC Address for {localIP}: {macAddress}");
					if (macAddress == "")
						return "Unknown MAC";
					return macAddress ?? "Unknown MAC";
				}
			}
			else if (_isConnected)
			{
				UpdateStatus("Warning: Could not determine local endpoint to find MAC address.");
			}
			return "Unknown MAC";
		}


		private void UpdateStatus(string message)
		{
			if (IsDisposed || !IsHandleCreated) return;

			if (InvokeRequired)
			{
				try
				{
					Invoke((Action)(() => UpdateStatus(message)));
				}
				catch (ObjectDisposedException) { }
				return;
			}
			thongBaoRtbx.AppendText($"{DateTime.Now}: {message}\n");
			thongBaoRtbx.SelectionStart = thongBaoRtbx.Text.Length;
			thongBaoRtbx.ScrollToCaret();
		}


		private bool CheckForm()
		{
			errorProvider1.Clear();
			if (string.IsNullOrWhiteSpace(serverIpAddressTbx.Text) || !NetworkHelper.IsValidIP(serverIpAddressTbx.Text))
			{
				errorProvider1.SetError(serverIpAddressTbx, "Invalid IP address");
				return false;
			}

			return true;
		}

		private void ClientForm_FormClosing(object sender, FormClosingEventArgs e)
		{
			// Clean up UDP resources
			_udpCts?.Cancel();
			_udpCts?.Dispose();
			_udpListener?.Close();
			_udpListener?.Dispose();

			if (_isConnected)
			{
				Disconnect();
			}
			else
			{
				_cts?.Cancel();
				_cts?.Dispose();
			}
		}


		private void ShutdownComputer()
		{
			try
			{
				System.Diagnostics.Process.Start("shutdown", "/s /f /t 0");
				UpdateStatus("Attempting to shut down computer...");
			}
			catch (Exception ex)
			{
				UpdateStatus($"Failed to initiate shutdown: {ex.Message}");
			}
			finally
			{
				Disconnect();
			}
		}


		private string GetAllInformation()
		{
			var Mac = GetMacAddress();
			var clientName = tenMayTbx.Text;
			if (clientName == "")
				clientName = "Default client";
			var systemInfo = GetSystemInformation();

			return Mac + Environment.NewLine + clientName + Environment.NewLine + systemInfo;
		}

		private string GetSystemInformation()
		{
			try
			{
				StringBuilder systemInfo = new StringBuilder();

				// Get OS information
				ManagementObjectSearcher osSearcher = new ManagementObjectSearcher("SELECT Caption, Version FROM Win32_OperatingSystem");
				foreach (ManagementObject os in osSearcher.Get())
				{
					systemInfo.AppendLine($"Operating System: {os["Caption"]}");
					systemInfo.AppendLine($"Version: {os["Version"]}");
				}

				// Get Computer information
				ManagementObjectSearcher computerSearcher = new ManagementObjectSearcher("SELECT Manufacturer, Model, TotalPhysicalMemory FROM Win32_ComputerSystem");
				foreach (ManagementObject computer in computerSearcher.Get())
				{
					systemInfo.AppendLine($"Manufacturer: {computer["Manufacturer"]}");
					systemInfo.AppendLine($"Model: {computer["Model"]}");

					// Convert bytes to GB with 2 decimal places
					double ramGB = Math.Round(Convert.ToDouble(computer["TotalPhysicalMemory"]) / 1073741824, 2);
					systemInfo.AppendLine($"RAM: {ramGB} GB");
				}

				// Get CPU information
				ManagementObjectSearcher cpuSearcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
				foreach (ManagementObject cpu in cpuSearcher.Get())
				{
					systemInfo.AppendLine($"CPU: {cpu["Name"]}");
				}

				// Get BIOS information
				ManagementObjectSearcher biosSearcher = new ManagementObjectSearcher("SELECT SMBIOSBIOSVersion FROM Win32_BIOS");
				foreach (ManagementObject bios in biosSearcher.Get())
				{
					systemInfo.AppendLine($"BIOS: {bios["SMBIOSBIOSVersion"]}");
				}

				return systemInfo.ToString();
			}
			catch (Exception ex)
			{
				return $"Error retrieving system information: {ex.Message}";
			}
		}

		private void thongBaoRtbx_TextChanged(object sender, EventArgs e)
		{
			const int MAX_LINES = 500;
			if (thongBaoRtbx.Lines.Length > MAX_LINES)
			{
				thongBaoRtbx.Select(0, thongBaoRtbx.GetFirstCharIndexFromLine(thongBaoRtbx.Lines.Length - MAX_LINES));
				thongBaoRtbx.SelectedText = "";
			}
		}
	}
}