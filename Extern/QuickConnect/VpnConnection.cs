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
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Neo.PerfectWorking.Data;
using Neo.PerfectWorking.OpenVpn;
using TecWare.DE.Data;
using TecWare.DE.Stuff;

namespace Neo.PerfectWorking.QuickConnect
{
	internal class VpnConnection : ObservableObject, IPwAction
	{
		public event EventHandler CanExecuteChanged;

		private readonly IPwGlobal global;
		private readonly string name;
		private readonly OpenVpnInfo vpnInfo;

		private bool isRunning = false;

		public VpnConnection(IPwGlobal global, OpenVpnInfo vpnInfo, string name = null)
		{
			this.global = global ?? throw new ArgumentNullException(nameof(global));
			this.vpnInfo = vpnInfo ?? throw new ArgumentNullException(nameof(vpnInfo));
			this.name = name ?? Path.GetFileNameWithoutExtension(vpnInfo.ConfigFile);

			vpnInfo.NeedPassword += VpnInfo_NeedPassword;
			vpnInfo.PropertyChanged += VpnInfo_PropertyChanged;
			connections.Add(new WeakReference<VpnConnection>(this));
		} // ctor

		private void VpnInfo_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(OpenVpnInfo.InBytes):
				case nameof(OpenVpnInfo.OutBytes):
					OnPropertyChanged(nameof(Label));
					break;
				case nameof(OpenVpnInfo.State):
					OnPropertyChanged(nameof(Label));
					OnPropertyChanged(nameof(Image));
					break;
			}
		} // event VpnInfo_PropertyChanged

		private void RefreshCore(bool enforceNotify)
		{
			if (vpnInfo.Refresh() || enforceNotify)
			{
				OnPropertyChanged(nameof(Label));
				OnPropertyChanged(nameof(Image));
			}

			CanExecuteChanged?.Invoke(this, EventArgs.Empty);
		} // proc RefreshCore

		public void Refresh()
			=> RefreshCore(false);

		private string GetLabelText()
		{
			if (vpnInfo.IsActive)
			{
				switch (vpnInfo.State)
				{
					case OpenVpnState.Active:
					case OpenVpnState.Connected:
						return $"Läuft ({new FileSize(vpnInfo.InBytes + vpnInfo.OutBytes).ToString("XiB")}, {vpnInfo.Owner})";
					case OpenVpnState.Connecting:
						return "Verbinden...";

					case OpenVpnState.Disconnected:
					case OpenVpnState.Exiting:
						return "Schließen...";
					case OpenVpnState.AddingRoutes:
						return "Routen setzen...";
					case OpenVpnState.AssignIP:
						return "IP abholen...";
					case OpenVpnState.Authentifaction:
						return "Authentifizierung...";
					case OpenVpnState.GetConfiguration:
						return "Konfigurieren...";
					case OpenVpnState.Reconnecting:
						return "Neu verbinden...";
					case OpenVpnState.Wait:
						return "Warten...";
					case OpenVpnState.Unknown:
					default:
						return "Unbekannt...";
				}
			}
			else
				return String.Empty;
		} // func GetLabelText

		private object GetImage()
		{
			if (vpnInfo.IsActive)
			{
				switch (vpnInfo.State)
				{
					case OpenVpnState.Connected:
					case OpenVpnState.Active:
						return imageConnected;

					case OpenVpnState.GetConfiguration:
					case OpenVpnState.Reconnecting:
					case OpenVpnState.Wait:
					case OpenVpnState.AddingRoutes:
					case OpenVpnState.AssignIP:
					case OpenVpnState.Authentifaction:
					case OpenVpnState.Connecting:
						return imageConnecting;

					case OpenVpnState.Exiting:
					case OpenVpnState.Disconnected:
						return imageDisconnecting;
					case OpenVpnState.Unknown:
					default:
						return imageDisconnected;
				}
			}
			else
				return imageDisconnected;
		} // func GetImage

		public bool CanExecute(object parameter)
			=> true;

		private async Task ExecuteAsync()
		{
			IsRunning = true;
			try
			{
				if (IsActive)
					await vpnInfo.CloseAsync();
				else
					await vpnInfo.StartAsync(true);
				RefreshCore(true);
			}
			finally
			{
				IsRunning = false;
			}
		} // proc ExecuteAsync

		public void Execute(object parameter)
			=> ExecuteAsync().Silent(ShowException);

		private void ShowException(Exception e)
			=> global.UI.ShowException(e);

		private void VpnInfo_NeedPassword(object sender, OpenVpnNeedPasswordArgs e)
		{
			var nc = global.GetCredential("vpn://" + name);
			if (e.NeedUsername)
				e.UserName = nc?.UserName ?? "anonymous";
			if (e.NeedPassword)
				e.Password = nc?.Password ?? "none";
			e.Handled = true;
		} // event VpnInfo_NeedPassword

		public string Title => name;
		public string Label => GetLabelText();
		public object Image => GetImage();

		public bool IsProgressVisible => IsRunning;
		public int ProgressValue => -1;

		public bool IsRunning
		{
			get => isRunning;
			set
			{
				if (Set(ref isRunning, value, nameof(IsRunning)))
					OnPropertyChanged(nameof(IsProgressVisible));
			}
		} // prop IsRunning

		public string EventName => vpnInfo.EventName;
		public string ConfigFile => vpnInfo.ConfigFile;

		private bool IsActive => vpnInfo != null && vpnInfo.IsActive;

		private static readonly ImageSource imageConnected = new BitmapImage(new Uri("pack://application:,,,/PW.QuickConnect;component/Resources/VpnConnected.png", UriKind.Absolute));
		private static readonly ImageSource imageConnecting = new BitmapImage(new Uri("pack://application:,,,/PW.QuickConnect;component/Resources/VpnConnecting.png", UriKind.Absolute));
		private static readonly ImageSource imageDisconnecting = new BitmapImage(new Uri("pack://application:,,,/PW.QuickConnect;component/Resources/VpnDisconnecting.png", UriKind.Absolute));
		private static readonly ImageSource imageDisconnected = new BitmapImage(new Uri("pack://application:,,,/PW.QuickConnect;component/Resources/VpnDisconnected.png", UriKind.Absolute));

		private static readonly List<WeakReference<VpnConnection>> connections = new List<WeakReference<VpnConnection>>();

		public static void RefreshConnectionState()
		{
			for (var i = connections.Count - 1; i >= 0; i--)
			{
				if (connections[i].TryGetTarget(out var vpn))
					vpn.Refresh();
				else
					connections.RemoveAt(i);
			}
		} // proc RefreshConnectionState
	} // class VpnConnection
}
