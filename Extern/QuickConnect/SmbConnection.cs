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
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Neo.PerfectWorking.Data;
using Neo.PerfectWorking.Stuff;
using static Neo.PerfectWorking.QuickConnect.NativeMethods;

namespace Neo.PerfectWorking.QuickConnect
{
	#region -- class SmbConnection ----------------------------------------------------

	internal sealed class SmbConnection
	{
		private readonly IPwGlobal global;
		private readonly WeakReference<PwAction> actionReference;
		private readonly string remotePath;
		private readonly string localPath;
		private readonly string credentialTarget;

		private bool isConnected = false;
		private CancellationTokenSource currentActionCancellation = null;

		private readonly Func<PwAction, Task> executeTask;
		private readonly Func<PwAction, Task> cancelTask;

		public SmbConnection(IPwGlobal global, PwAction action, string remotePath, string localPath, string credentialTarget)
		{
			this.global = global;
			this.actionReference = new WeakReference<PwAction>(action);
			this.remotePath = remotePath;
			this.localPath = localPath;
			this.credentialTarget = credentialTarget;

			this.executeTask = Execute;
			this.cancelTask = Cancel;

			action.OriginalImage = imageDisconnected;
			action.Execute = executeTask;

			RegisterConnection(this);
		} // ctor

		private Task Execute(PwAction action)
		{
			if (currentActionCancellation != null)
				throw new InvalidOperationException();

			currentActionCancellation = new CancellationTokenSource();
			action.Execute = cancelTask;
			return Task.Run(
				() => isConnected
					? ExecuteDisconnect(action, currentActionCancellation.Token)
					: ExecuteConnect(action, currentActionCancellation.Token),
				currentActionCancellation.Token
			).ContinueWith(
				t =>
				{
					try
					{
						t.Wait();
					}
					finally
					{
						currentActionCancellation = null;
						action.Execute = executeTask;
					}
				}
			);
		} // proc Execute

		private Task Cancel(PwAction action)
		{
			if (!currentActionCancellation.IsCancellationRequested)
			{
				action.Label = "Abbrechen...";
				currentActionCancellation.Cancel();
			}
			return Task.CompletedTask;
		} // proc Cancel

		private string GetCaption(bool connect)
			=> (connect
				? "Verbinden mit "
				: "Trennen von ") + remotePath;

		private Task ErrBoxAsync(bool connect, string text)
			=> global.UI.MsgBoxAsync(text, GetCaption(connect), "i", "o");

		private async Task ExecuteConnect(PwAction action, CancellationToken cancellationToken)
		{
			action.Label = "Verbinden...";
			Log.Default.SmbConnect(remotePath);

			// get user name and password
			var networkCredential = global.GetCredential(credentialTarget ?? remotePath);
			using (var password = new InteropSecurePassword(networkCredential?.SecurePassword))
			{
				var userName = networkCredential?.GetUserName();

				// prepare connection
				var nr = new NETRESOURCE()
				{
					dwScope = 0,
					dwType = 1, // Disk
					dwDisplayType = 0,
					dwUsage = 0,
					lpLocalName = localPath,
					lpRemoteName = remotePath,
					lpComment = null,
					lpProvider = null
				};

				var hr = WNetAddConnection3(IntPtr.Zero, ref nr, password, userName, 8); // CONNECT_INTERACTIVE 8
				var reCheckReturn = false;
				ReCheckReturn:
				if (cancellationToken.IsCancellationRequested)
					return;

				action.Label = "Warte...";
				if (hr == 0)
					UpdateState(action, true);
				else if (hr == 5) // ERROR_ACCESS_DENIED
					await ErrBoxAsync(true, "Zugriff verweigert.");
				else if (hr == 85) // ERROR_ALREADY_ASSIGNED
					await ErrBoxAsync(true, "Laufwerksbuchstabe wird schon verwendet.");
				else if (hr == 86) // ERROR_INVALID_PASSWORD
					await ErrBoxAsync(true, "Passwort ist ungültig.");
				else if (hr == 53) // ERROR_BAD_NETPATH
					await ErrBoxAsync(true, "Netzwerk nicht erreichbar.");
				else if (hr == 67) // ERROR_BAD_NET_NAME
					await ErrBoxAsync(true, "Remote ist ungültig.");
				else if (hr == 1203) // ERROR_NO_NET_OR_BAD_PATH
					await ErrBoxAsync(true, "Netzwerk nicht verfügbar oder der Netzwerkpfad wurde falsch angegeben.");
				else if (hr == 1219) // ERROR_SESSION_CREDENTIAL_CONFLICT
					if (reCheckReturn)
						await ErrBoxAsync(true, "Multiple connections to a server or shared resource by the same user, using more than one user name, are not allowed. Disconnect all previous connections to the server or shared resource and try again.");
					else
					{
						action.Label = "Verbinden...";
						hr = WNetAddConnection3(IntPtr.Zero, ref nr, password, userName, 8);
						reCheckReturn = true;
						goto ReCheckReturn;
					}
				else if (hr == 1222) // ERROR_NO_NETWORK
					await ErrBoxAsync(true, "Kein Netzwerk.");
				else if (hr == 1208)
				{
					var errorCode = 0;
					var errorBuffer = new StringBuilder(1024);
					var nameBuffer = new StringBuilder(1024);
					WNetGetLastError(ref errorCode, errorBuffer, errorBuffer.Capacity, nameBuffer, nameBuffer.Capacity);

					await ErrBoxAsync(true, "Verbindung konnte nicht hergestelt werden. Ein erweiterter Fehler ist aufgetreten.\n\n" +
						$"Fehler: {errorBuffer}\n" +
						$"Name: {nameBuffer}"
					);
				}
				else if (hr == 1223 || hr == 1202)// ERROR_CANCELLED, ERROR_DEVICE_ALREADY_REMEMBERED
				{ }
				else
					await ErrBoxAsync(true, String.Format("Verbindung konnte nicht hergestellt werden. (Fehler: {0})", hr));
			}
		} // proc ExecuteConnect

		private async Task ExecuteDisconnect(PwAction action, CancellationToken cancellationToken)
		{
			var forceDisconnect = false;
			Redo:
			action.Label = "Trennen...";

			if (cancellationToken.IsCancellationRequested)
				return;

			var hr = WNetCancelConnection2(localPath ?? remotePath, 0, false);
			action.Label = "Warte...";
			if (hr == 0)
				UpdateState(action, false);
			else if (hr == 2401) // ERROR_OPEN_FILES
			{
				var messageText = forceDisconnect
					? "Es gibt offene Dateien auf diesem Laufwerk.\nErneut versuchen?"
					: "Es gibt offene Dateien auf diesem Laufwerk.\nTrotzdem trennen?";

				if (await global.UI.MsgBoxAsync(messageText, GetCaption(false), "q", "yesno") == "y")
				{
					forceDisconnect = true;
					goto Redo;
				}
			}
			else if (hr == 2404) // ERROR_DEVICE_IN_USE 
				await ErrBoxAsync(false, "Netzwerklaufwerk wird zur Zeit verwendet und kann nicht freigegeben werden.");
			else if (hr != 2250) // ERROR_NOT_CONNECTED 
				await ErrBoxAsync(false, $"Netzwerklaufwerk konnte nicht getrennt werden. (Fehler: {hr})");
		} // proc ExecuteConnect

		private void UpdateState(PwAction action, bool isConnected)
		{
			this.isConnected = isConnected;
			action.OriginalImage = isConnected ? imageConnected : imageDisconnected;
		} // proc UpdateState

		public void UpdateState(bool isConnected)
		{
			if (actionReference.TryGetTarget(out var action))
				UpdateState(action, isConnected);
		} // proc UpdateSate

		public string LocalPath => localPath;
		public string RemotePath => remotePath;

		private static readonly ImageSource imageConnected = new BitmapImage(new Uri("pack://application:,,,/PW.QuickConnect;component/Resources/NetDriveConnected.png", UriKind.Absolute));
		private static readonly ImageSource imageDisconnected = new BitmapImage(new Uri("pack://application:,,,/PW.QuickConnect;component/Resources/NetDriveDisconnected.png", UriKind.Absolute));

		private static readonly List<WeakReference<SmbConnection>> connections = new List<WeakReference<SmbConnection>>();

		private static void RegisterConnection(SmbConnection connection)
			=> connections.Add(new WeakReference<SmbConnection>(connection));

		public static void RefreshConnectionState()
		{
			//Log.Default.SmbConnect("remotePath");
			IntPtr dataPtr;
			var count = 100;
			var dataSize = count * Marshal.SizeOf(typeof(NETRESOURCE));

			dataPtr = Marshal.AllocHGlobal(dataSize);
			try
			{
				// RESOURCE_CONNECTED 1
				// RESOURCETYPE_DISK  1 
				// RESOURCE_CONNECTED
				if (WNetOpenEnum(1, 1, 1, IntPtr.Zero, out var enumPtr) != 0)
					throw new Win32Exception();
				WNetEnumResource(enumPtr, ref count, dataPtr, ref dataSize);
				WNetCloseEnum(enumPtr);

				// extract information
				var currentConnections = new Tuple<string, string>[count];
				for (var i = 0; i < count; i++)
				{
					var nr = (NETRESOURCE)Marshal.PtrToStructure(new IntPtr(dataPtr.ToInt64() + (i * Marshal.SizeOf(typeof(NETRESOURCE)))), typeof(NETRESOURCE));
					currentConnections[i] = new Tuple<string, string>(nr.lpLocalName, nr.lpRemoteName);
				}

				// check the connections
				for (var i = connections.Count - 1; i >= 0; i--)
				{
					if (connections[i].TryGetTarget(out var con))
						con.UpdateState(Array.Exists(currentConnections,
							c => String.Compare(c.Item1, con.LocalPath, StringComparison.OrdinalIgnoreCase) == 0
								&& String.Compare(c.Item2, con.RemotePath, StringComparison.OrdinalIgnoreCase) == 0
						));
					else
						connections.RemoveAt(i);
				}
			}
			finally
			{
				Marshal.FreeHGlobal(dataPtr);
			}
		} // proc RefreshConnectionState
	} // class SmbConnection

	#endregion
}
