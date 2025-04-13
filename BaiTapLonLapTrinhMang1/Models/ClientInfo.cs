using System;
using System.ComponentModel;
using System.Runtime.CompilerServices; // Cần cho CallerMemberName (nếu dùng)

// Đảm bảo namespace phù hợp với cấu trúc thư mục của bạn
namespace BaiTapLopLapTrinhMang1.Models
{
	public class ClientInfo : INotifyPropertyChanged
	{
		private string _ipAddress;
		private int _port;
		private string _macAddress;
		private DateTime _lastActivity;

		public string IpAddress
		{
			get => _ipAddress;
			set { _ipAddress = value; OnPropertyChanged(); }
		}
		public int Port
		{
			get => _port;
			set { _port = value; OnPropertyChanged(); }
		}
		public string MacAddress
		{
			get => _macAddress;
			set { _macAddress = value; OnPropertyChanged(); }
		}
		public DateTime LastActivity
		{
			get => _lastActivity;
			set { _lastActivity = value; OnPropertyChanged(); }
		}

		public ClientInfo(string ipAddress, int port, string macAddress)
		{
			IpAddress = ipAddress;
			Port = port;
			MacAddress = macAddress;
			LastActivity = DateTime.UtcNow;
		}

		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
		public string ClientId => $"{IpAddress}:{Port}";
	}
}