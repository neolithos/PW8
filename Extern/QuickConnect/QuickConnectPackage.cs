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
using System.Diagnostics;
using System.Linq;
using Neo.PerfectWorking.Data;
using Neo.PerfectWorking.OpenVpn;
using Neo.PerfectWorking.Stuff;

namespace Neo.PerfectWorking.QuickConnect
{
	public sealed class QuickConnectPackage : PwPackageBase, IPwIdleAction, IDisposable
	{
		private bool isDisposed = false;
		private int lastConnectionEnum;
		
		public QuickConnectPackage(IPwGlobal global) 
			: base(global, nameof(QuickConnectPackage))
		{
			this.lastConnectionEnum = Environment.TickCount;

			global.UI.AddIdleAction(this);

			Global.RegisterObject(this, "Log", Log.Default);
		} // ctor

		public void Dispose()
		{
			if (isDisposed)
				return;

			isDisposed = true;
			Global.UI.RemoveIdleAction(this);
		} // proc Dispose

		[Obsolete]
		public IPwAction CreateConnection(string displayName, string remotePath, string localPath = null, string credentialTarget = null)
			=> CreateSmbConnection(displayName, remotePath, localPath, credentialTarget);

		public IPwAction CreateVpnConnection(string displayName, string configFile) 
			=> new VpnConnection(Global, OpenVpnInfo.Get(configFile), displayName);

		public IPwAction CreateSmbConnection(string displayName, string remotePath, string localPath = null, string credentialTarget = null)
		{
			var a = new PwAction(Global, displayName, remotePath, PwKey.None, null);
			new SmbConnection(Global, a, remotePath, localPath, credentialTarget);
			return a;
		} // func CreateSmbConnection

		public IEnumerable<IPwAction> GetVpnConfigurations()
			=> OpenVpnInfo.Get().Select(c => new VpnConnection(Global, c));

		public bool OnIdle(int elapsed)
		{
			if (unchecked(Environment.TickCount - lastConnectionEnum) > 1000)
			{
				try
				{
					var sw = Stopwatch.StartNew();
					SmbConnection.RefreshConnectionState();
					Debug.Print("Refresh (Smb): {0}ms", sw.ElapsedMilliseconds);
					VpnConnection.RefreshConnectionState();
					Debug.Print("Refresh (Vpn): {0}ms", sw.ElapsedMilliseconds);
				}
				finally
				{
					lastConnectionEnum = Environment.TickCount;
				}
			}

			return false;
		} // proc OnIdle
	} // class QuickConnectPackage
}
