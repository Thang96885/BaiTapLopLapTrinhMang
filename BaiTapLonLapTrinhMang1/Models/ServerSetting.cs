using System;

// Đảm bảo namespace phù hợp với cấu trúc thư mục của bạn
namespace BaiTapLopLapTrinhMang1.Models
{
	public class ServerSetting
	{
		public string IpAddress { get; set; }
		public int PortNumber { get; set; }

		public ServerSetting(string ipAddress, int portNumber)
		{
			IpAddress = ipAddress;
			PortNumber = portNumber;
		}
	}
}