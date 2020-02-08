#region -- copyright --
//
// Licensed under the EUPL, Version 1.1 or - as soon they will be approved by the
// European Commission - subsequent versions of the EUPL(the "Licence"); You may
// not use this work except in compliance with the Licence.
//
// You may obtain a copy of the Licence at:
// http://ec.europa.eu/idabc/eupl
//
// Unless required by applicable law or agreed to in writing, software distributed
// under the Licence is distributed on an "AS IS" basis, WITHOUT WARRANTIES OR
// CONDITIONS OF ANY KIND, either express or implied. See the Licence for the
// specific language governing permissions and limitations under the Licence.
//
#endregion
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// use OpenVPN-Service
// https://community.openvpn.net/openvpn/wiki/OpenVPNInteractiveService
// vpn options
// https://openvpn.net/index.php/open-source/documentation/manuals/65-openvpn-20x-manpage.html
// openvpn management interface-> tcp
// https://openvpn.net/index.php/open-source/documentation/miscellaneous/79-management-interface.html


namespace Neo.PerfectWorking.OpenVpn
{
	#region -- enum OpenVpnState ------------------------------------------------------

	public enum OpenVpnState
	{
		Unknown = -2,
		/// <summary>Disconnected</summary>
		Disconnected = -1,
		/// <summary>Active</summary>
		Active = 0,
		/// <summary>OpenVPN's initial state. (CONNECTING)</summary>
		Connecting = 1,
		/// <summary>Waiting for initial response from server. (WAIT)</summary>
		Wait = 2,
		/// <summary>Authenticating with server. (AUTH)</summary>
		Authentifaction = 3,
		/// <summary>Downloading configuration options from server. (GET_CONFIG)</summary>
		GetConfiguration = 4,
		/// <summary>Assigning IP address to virtual network interface. (ASSIGN_IP)</summary>
		AssignIP = 5,
		/// <summary>Adding routes to system. (ADD_ROUTES)</summary>
		AddingRoutes = 6,
		/// <summary>Initialization Sequence Completed. (CONNECTED)</summary>
		Connected = 7,
		/// <summary>A restart has occurred. (RECONNECTING)</summary>
		Reconnecting = 8,
		/// <summary>A graceful exit is in progress. (EXITING)</summary>
		Exiting = 9
	} // enum OpenVpnState

	#endregion

	#region -- enum OpenVpnLineTyp ----------------------------------------------------

	public enum OpenVpnLineTyp
	{
		None = 0,
		Info = 1,
		FatalError,
		NoneFatalError,
		Warning,
		Debug
	} // enum OpenVpnLineTyp

	#endregion

	#region -- class OpenVpnLogLine ---------------------------------------------------

	public sealed class OpenVpnLogLine
	{
		private readonly DateTime time;
		private readonly OpenVpnLineTyp type;
		private readonly string text;

		public OpenVpnLogLine(DateTime time, OpenVpnLineTyp type, string text)
		{
			this.time = time;
			this.type = type;
			this.text = text;
		} // ctor

		public DateTime Time => time;
		public OpenVpnLineTyp Type => type;
		public string Text => text;

		public static OpenVpnLineTyp ToType(char c)
		{
			return c switch
			{
				'I' => OpenVpnLineTyp.Info,
				'F' => OpenVpnLineTyp.FatalError,
				'N' => OpenVpnLineTyp.NoneFatalError,
				'W' => OpenVpnLineTyp.Warning,
				'D' => OpenVpnLineTyp.Debug,
				_ => OpenVpnLineTyp.None,
			};
		} // func ToType
	} // class OpenVpnLogLine

	#endregion
}
