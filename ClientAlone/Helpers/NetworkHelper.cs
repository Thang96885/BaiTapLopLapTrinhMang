using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace BaiTapLopLapTrinhMang.Helpers
{
	public static class NetworkHelper
	{
		public static List<string> GetAllIPAddresses()
		{
			List<string> ipAddresses = new List<string>();

			// Get a list of all network interfaces
			NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();

			foreach (NetworkInterface adapter in interfaces)
			{
				// Only consider interfaces that are up and not loopback or tunnel
				if (adapter.OperationalStatus == OperationalStatus.Up &&
					adapter.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
					adapter.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
				{
					// Get the IP addresses associated with the adapter
					IPInterfaceProperties properties = adapter.GetIPProperties();

					// Get unicast addresses (IPv4 and IPv6)
					foreach (UnicastIPAddressInformation unicastAddress in properties.UnicastAddresses)
					{
						// Add the IP address to the list, excluding link-local addresses
						if (unicastAddress.Address.AddressFamily == AddressFamily.InterNetwork || // IPv4
							(unicastAddress.Address.AddressFamily == AddressFamily.InterNetworkV6 && !unicastAddress.Address.IsIPv6LinkLocal)) // IPv6 (not link-local)
						{
							ipAddresses.Add(unicastAddress.Address.ToString());
						}
					}
				}
			}

			return ipAddresses;
		}

		public static string GetMacAddress(IPAddress ipAddress)
		{
			var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();

			foreach (var ni in networkInterfaces)
			{
				var ipProps = ni.GetIPProperties();
				foreach (var addr in ipProps.UnicastAddresses)
				{
					if (addr.Address.Equals(ipAddress))
					{
						return string.Join(":", ni.GetPhysicalAddress()
												 .GetAddressBytes()
												 .Select(b => b.ToString("X2")));
					}
				}
			}
			return "Không tìm thấy MAC Address";
		}

		public static bool IsValidIP(string ipAddress)
		{
			if (string.IsNullOrWhiteSpace(ipAddress))
			{
				return false;
			}

			try
			{
				// Use IPAddress.Parse to attempt parsing the string.  It handles IPv4 and IPv6.
				IPAddress.Parse(ipAddress);
				return true;
			}
			catch (FormatException)
			{
				// Parsing failed, invalid IP
				return false;
			}
		}
	}
}
