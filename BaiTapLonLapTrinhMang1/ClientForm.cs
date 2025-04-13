using BaiTapLopLapTrinhMang.Helpers;
using System;
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

		public ClientForm()
		{
			InitializeComponent();
			_client = new TcpClient();
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

				string macAddress = GetMacAddress();
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
							Disconnect(); // Use the public method to handle UI updates
							break;
						}
						else if (message == "PING") // Server is checking if we are alive
						{
							byte[] response = Encoding.ASCII.GetBytes("PONG");
							await _stream.WriteAsync(response, 0, response.Length, token);
							UpdateStatus("Sent: PONG");
						}
						// REMOVED: HELLO/MAC response logic - server doesn't send HELLO anymore for keep-alive
						// else if (message == "HELLO") { ... }

						// Keep PONG response logic if server explicitly sends PONG (unlikely now)
						// else if (message == "PONG") { UpdateStatus("Received: PONG"); }

						else if (message == "SHUTDOWN")
						{
							UpdateStatus("Server requested computer shutdown.");
							ShutdownComputer(); // This will also call Disconnect
							break;
						}
						// Potentially handle other messages if needed
					}
					else
					{
						// Read returned 0 bytes, usually means graceful remote closure
						if (_isConnected) // Check if disconnect wasn't initiated locally
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
				if (_isConnected) // Only log if we didn't initiate the disconnect
				{
					UpdateStatus($"Connection lost: {se.SocketErrorCode}");
					Disconnect();
				}
			}
			catch (Exception ex) when (_isConnected) // Catch other exceptions only if we thought we were connected
			{
				UpdateStatus($"Connection error: {ex.Message}");
				Disconnect();
			}
			finally
			{
				// Ensure cleanup happens even if loop exits unexpectedly,
				// but only if Disconnect wasn't already called.
				if (_isConnected)
				{
					Disconnect();
				}
			}
		}

		// REMOVED: KeepAliveAsync method is no longer needed.
		// private async Task KeepAliveAsync(CancellationToken token) { ... }

		private void Disconnect()
		{
			if (!_isConnected && _client == null) return; // Already disconnected or not connected yet

			_isConnected = false; // Set state first
			DisconnectInternal(); // Perform actual cleanup

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
			// Cancel ongoing operations
			_cts?.Cancel();
			_cts?.Dispose(); // Dispose the CTS
			_cts = new CancellationTokenSource(); // Create a new one for potential reuse (though ConnectAsync also does this)

			// Close stream and client safely
			try { _stream?.Close(); } catch { /* Ignore */ }
			try { _client?.Close(); } catch { /* Ignore */ }
			_stream = null;
			_client = null; // Set to null after closing
		}


		private string GetMacAddress()
		{
			// Ensure client and its socket are valid before accessing endpoints
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
					return macAddress ?? "Unknown MAC"; // Return "Unknown MAC" if helper returns null
				}
			}
			else if (_isConnected) // If we think we are connected but can't get endpoint
			{
				UpdateStatus("Warning: Could not determine local endpoint to find MAC address.");
			}
			return "Unknown MAC";
		}


		private void UpdateStatus(string message)
		{
			if (IsDisposed || !IsHandleCreated) return; // Prevent invoking on disposed form

			if (InvokeRequired)
			{
				try
				{
					Invoke((Action)(() => UpdateStatus(message)));
				}
				catch (ObjectDisposedException) { /* Form closing, ignore */ }
				return;
			}
			thongBaoRtbx.AppendText($"{DateTime.Now}: {message}\n");
			thongBaoRtbx.SelectionStart = thongBaoRtbx.Text.Length;
			thongBaoRtbx.ScrollToCaret();
		}


		private bool CheckForm()
		{
			errorProvider1.Clear(); // Clear previous errors
			if (string.IsNullOrWhiteSpace(serverIpAddressTbx.Text) || !NetworkHelper.IsValidIP(serverIpAddressTbx.Text))
			{
				errorProvider1.SetError(serverIpAddressTbx, "Invalid IP address");
				return false;
			}
			// No need to set error provider to "" explicitly, Clear() does it.
			return true;
		}

		private void ClientForm_FormClosing(object sender, FormClosingEventArgs e)
		{
			// Check _isConnected state before calling Disconnect
			if (_isConnected)
			{
				Disconnect();
			}
			else
			{
				// Ensure CTS is cancelled even if not fully connected
				_cts?.Cancel();
				_cts?.Dispose();
			}
		}


		private void ShutdownComputer()
		{
			try
			{
				// Use /f to force closing applications
				System.Diagnostics.Process.Start("shutdown", "/s /f /t 0");
				UpdateStatus("Attempting to shut down computer...");
			}
			catch (Exception ex)
			{
				UpdateStatus($"Failed to initiate shutdown: {ex.Message}");
			}
			finally
			{
				// Disconnect regardless of shutdown success/failure
				Disconnect();
			}
		}

		private void thongBaoRtbx_TextChanged(object sender, EventArgs e)
		{
			// Optional: Limit the amount of text in the RichTextBox
			const int MAX_LINES = 500;
			if (thongBaoRtbx.Lines.Length > MAX_LINES)
			{
				thongBaoRtbx.Select(0, thongBaoRtbx.GetFirstCharIndexFromLine(thongBaoRtbx.Lines.Length - MAX_LINES));
				thongBaoRtbx.SelectedText = "";
			}
		}
	}
}