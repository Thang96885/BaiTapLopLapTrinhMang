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


			try { _stream?.Close(); } catch {}
			try { _client?.Close(); } catch {  }
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
				catch (ObjectDisposedException) {  }
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