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
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using Neo.PerfectWorking.QuickConnect;
using TecWare.DE.Stuff;

namespace Neo.PerfectWorking.OpenVpn
{
	#region -- class OpenVpnInfo ------------------------------------------------------

	/// <summary>Represents a running vpn.</summary>
	/// <remarks>HKLM\Software\WOW6432Node\Perfect Working\RunningVPN right to QueryValues, SetValue for all users</remarks>
	public sealed class OpenVpnInfo : INotifyPropertyChanged, IDisposable
	{
		public event PropertyChangedEventHandler PropertyChanged;
		public event EventHandler<OpenVpnNeedPasswordArgs> NeedPassword;

		private readonly string configFile;
		private readonly string exitEventName;

		#region -- Ctor/Dtor ----------------------------------------------------------

		private OpenVpnInfo(string exitEventName, string configFile)
		{
			this.exitEventName = exitEventName ?? throw new ArgumentNullException(nameof(exitEventName));
			this.configFile = configFile ?? throw new ArgumentNullException(nameof(configFile));

			Name = Path.GetFileNameWithoutExtension(configFile);

			Refresh();
		} // ctor

		public void Dispose()
		{
			memoryMap?.Dispose();
			connection?.Dispose();
			exitEvent?.Dispose();
		} // proc Dispose

		private void OnPropertyChanged(string propertyName)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		private void Set<T>(ref T value, T newValue, string propertyName)
		{
			if (!Equals(value, newValue))
			{
				value = newValue;
				OnPropertyChanged(propertyName);
			}
		} // proc Set

		private void SetLong(ref long value, long newValue, string propertyName)
		{
			if (value != newValue)
			{
				value = newValue;
				OnPropertyChanged(propertyName);
			}
		} // proc SetLong

		#endregion

		#region -- Start, Close -------------------------------------------------------

		private static byte[] GetServiceStartupMessage(string workingDir, string cmdLine, string managementPassword)
		{
			// build startup message
			var buf = new byte[(workingDir.Length + 1 + cmdLine.Length + 1 + managementPassword.Length + 1) << 1];
			var ofs = Encoding.Unicode.GetBytes(workingDir, 0, workingDir.Length, buf, 0);
			buf[ofs++] = 0;
			buf[ofs++] = 0;
			ofs += Encoding.Unicode.GetBytes(cmdLine, 0, cmdLine.Length, buf, ofs);
			buf[ofs++] = 0;
			buf[ofs++] = 0;
			ofs += Encoding.Unicode.GetBytes(managementPassword, 0, managementPassword.Length, buf, ofs);
			buf[ofs++] = 0;
			buf[ofs++] = 0;
			if (buf.Length != ofs)
				throw new ArgumentException();
			return buf;
		} // func GetServiceStartupMessage

		private static string BuildCommandLine(string configFile, bool holdLog, string exitEventName, IPEndPoint managementEndPoint)
		{
			var cmdLine = new StringBuilder();

			// append log info
			cmdLine.Append(" --log ")
				.Append('"').Append(GetDefaultLogPath(configFile)).Append('"');

			// append config
			cmdLine.Append(" --config ")
				.Append('"').Append(configFile).Append('"');

			// append exit event
			cmdLine.Append(" --service ")
				.Append(exitEventName)
				.Append(" 0");

			// append management listener
			cmdLine.Append(" --management ")
				.Append(managementEndPoint.Address.ToString())
				.Append(' ').Append(managementEndPoint.Port)
				.Append(" stdin");

			if (holdLog)
				cmdLine.Append(" --management-hold"); // wait for connect
			cmdLine.Append(" --management-query-passwords"); // ask for passwords

			cmdLine.Append(" --auth-retry interact");

			return cmdLine.ToString();
		} // func BuildCommandLine

		private static bool TryParseCode(string value, out int code)
		{
			if (!String.IsNullOrEmpty(value)
				&& value.StartsWith("0x")
				&& Int32.TryParse(value.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
				return true;
			else
			{
				code = 0;
				return false;
			}
		} // func TryParseCode

		private async Task StartWithServiceCoreAsync(bool holdLog)
		{
			// create parameter
			var workingDir = Path.GetDirectoryName(configFile);
			var managementEndPoint = GetFreeManagementEndPoint();
			CreateExitEvent();
			var cmdLine = BuildCommandLine(configFile, holdLog, GetGlobalEventName(), managementEndPoint);

			// Save port parameter
			using (var r = OpenRunningVpnKey(true))
				r.SetValue(exitEventName + "_Port", managementEndPoint.Port, RegistryValueKind.DWord);

			try
			{
				// start vpn process
				var pipe = new NamedPipeClientStream(".", @"openvpn\service", PipeDirection.InOut, PipeOptions.Asynchronous);
				
				await pipe.ConnectAsync();
				// message mode
				pipe.ReadMode = PipeTransmissionMode.Message;

				// start openvpn
				var managementPassword = GetPasswordFromEventName();
				var buf = GetServiceStartupMessage(workingDir, cmdLine, managementPassword);
				Log.Default.VpnStartConfigViaPipe(Name, cmdLine);
				await pipe.WriteAsync(buf, 0, buf.Length);

				// wait for startup
				var recv = new byte[1024];
				var recvCount = await pipe.ReadAsync(recv, 0, recv.Length);
				if (recvCount > 0)
				{
					var msg = Encoding.Unicode.GetString(recv, 0, recvCount).Split('\n');

					if (msg.Length == 0)
						throw new ArgumentException("Missing return value.");
					if (!TryParseCode(msg[0], out var errorCode))
						throw new ArgumentException($"Unexpected openvpn result: {msg[0]}");

					if (errorCode == 0) // success
					{
						if (msg.Length < 3)
							throw new ArgumentOutOfRangeException("Missing arguments");
						if (!TryParseCode(msg[1], out var pid))
							throw new ArgumentException($"Could not parse pid '{msg[1]}'.");
					}
					else // error
					{
						if (msg.Length < 3)
							throw new ArgumentOutOfRangeException("Missing arguments");

						throw new Exception($"OpenVPN error: {msg[0]}, {msg[1]}, {msg[2]}");
						//switch(errorCode)
						//{
						//	case 0x20000000:
						//		msg = "Error on openvpn startup.";
						//		break;
						//	case 0x20000001:
						//		msg = "Error on openvpn startup.";
						//		break;
						//	case 0x20000002:
						//		msg = "Error on openvpn startup.";
						//		break;
						//		case 0x20000003:
						//		msg = "Error on openvpn startup.";
						//		break;
						// GetLastError
						//}
					}
				}
				else
					throw new Exception("OpenVPN startup failed.");
			}
			catch
			{
				exitEvent.Dispose();
				exitEvent = null;
				throw;
			}
		} // proc StartWithServiceCoreAsync

		private async Task StartCoreAsync(bool holdLog)
		{
			// create parameter
			var workingDir = Path.GetDirectoryName(configFile);
			var managementEndPoint = GetFreeManagementEndPoint();
			CreateExitEvent();
			var cmdLine = BuildCommandLine(configFile, holdLog, GetGlobalEventName(), managementEndPoint);

			using (var r = OpenRunningVpnKey(true))
				r.SetValue(exitEventName + "_Port", managementEndPoint.Port, RegistryValueKind.DWord);

			try
			{
				var exePath = GetServiceConfiguration("exe_path");
				var managementPassword = GetPasswordFromEventName();
				Log.Default.VpnStartConfigViaExe(Name, exePath, cmdLine);
				var process = Process.Start(new ProcessStartInfo(exePath, cmdLine) { WorkingDirectory = workingDir, RedirectStandardInput = true, RedirectStandardOutput = true, UseShellExecute = false });
				await process.StandardInput.WriteLineAsync(managementPassword);
			}
			catch
			{
				exitEvent.Dispose();
				exitEvent = null;
				throw;
			}
		} // proc StartCoreAsync
		
		public async Task StartAsync(bool withService)
		{
			if (WaitForActive(0))
				return;

			if (withService)
				await StartWithServiceCoreAsync(true);
			else
				await StartCoreAsync(true);

			await StartConnectAsync(0, 5, true);
		} // proc StartAsync

		public Task CloseAsync()
		{
			if (TrySet())
				return Task.Run(() => WaitForActive(-1));
			else
				return Task.CompletedTask;
		} // proc CloseAsync

		#endregion

		#region -- Shared State -------------------------------------------------------

		private const int maxLogLines = 1024;
		private const int sharedStateSize = 32 + 1024 * maxLogLines;
		private MemoryMappedFile memoryMap = null;

		private OpenVpnState currentState = OpenVpnState.Disconnected;
		private long inBytes = 0L;
		private long outBytes = 0L;

		#region -- struct SharedStateHeander ------------------------------------------

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		private struct SharedStateHeander
		{
			// size = 32 byte
			public byte Sign;
			public byte Version;
			public short State;
			public long FileTimeUtc;
			public long InBytes;
			public long OutBytes;
			public short LastLogIndex;
			public short Reserverd1;
		} // struct SharedStateHeander

		#endregion

		#region -- struct SharedLogLine -----------------------------------------------

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		private struct SharedLogLine
		{
			// size = 1024
			public long FileTimeUtc;
			public short Flag;
			[MarshalAs(UnmanagedType.LPWStr, SizeConst = 507)]
			public string Text;
		} // struct SharedLogLine

		#endregion

		#region -- class LogLineEnumerable --------------------------------------------

		private sealed class LogLineEnumerable : IEnumerable<OpenVpnLogLine>
		{
			private readonly OpenVpnInfo vpnInfo;

			public LogLineEnumerable(OpenVpnInfo vpnInfo)
			{
				this.vpnInfo = vpnInfo ?? throw new ArgumentNullException(nameof(vpnInfo));
			} // ctor

			private static bool ReadLogLine(MemoryMappedViewAccessor mem, int top, ref int cur, out SharedLogLine sharedLogLine)
			{
				mem.Read(32 + cur * 1024, out sharedLogLine);

				cur++;
				if (cur >= maxLogLines)
					cur = 0;

				return top == cur;
			} // func NextLogLine

			private static void WriteLogLine(MemoryMappedViewAccessor mem, DateTime time, OpenVpnLineTyp type, string text)
			{
				var top = mem.ReadInt16(28);

				var textBytes = Encoding.Unicode.GetBytes(text);
				
				var textOfs = 0;
				int ofs;
				while (textOfs < textBytes.Length)
				{
					// write parts
					ofs = top * 1024 + 32;
					if (textOfs == 0)
					{
						mem.Write(ofs, time.ToFileTimeUtc());
						mem.Write(ofs + 8, (ushort)type);
					}
					else
					{
						mem.Write(ofs, 0L);
						mem.Write(ofs + 8, (ushort)type);
					}
					var l = Math.Max(507, textBytes.Length - textOfs);
					mem.WriteArray(ofs + 10, textBytes, 0, l);
					textOfs += l;
					if (++top >= maxLogLines)
						top = 0;
				}

				// empty broken lines
				ofs = top * 1024 + 32;
				if (mem.ReadInt64(ofs) == 0 && mem.ReadInt16(ofs + 8) != 0)
					mem.Write(ofs + 8, (short)0);

				// commit line
				mem.Write(28, top);
			} // proc WriteLogLine

			public IEnumerator<OpenVpnLogLine> GetEnumerator()
			{
				using var mem = vpnInfo.EnsureSharedState().CreateViewAccessor(0, sharedStateSize, MemoryMappedFileAccess.Read);
				
				var top = (int)mem.ReadInt16(28); // log index
				var cur = top;

				// loop until we read the whole memory block
				while (ReadLogLine(mem, top, ref cur, out var sharedLogLine))
				{
					if (sharedLogLine.FileTimeUtc != 0)
					{
						nextLine:
						var sb = new StringBuilder();
						var time = DateTime.FromFileTimeUtc(sharedLogLine.FileTimeUtc);
						var type = (OpenVpnLineTyp)(sharedLogLine.Flag & 0xFF);

						sb.Append(sharedLogLine.Text);

						while (ReadLogLine(mem, top, ref cur, out sharedLogLine))
						{
							if (sharedLogLine.FileTimeUtc == 0) // no time, if flag is not 0, line is extented
							{
								if (sharedLogLine.Flag != 0)
									sb.Append(sharedLogLine.Text);
								else
								{
									yield return new OpenVpnLogLine(time, type, sb.ToString());
									goto nextLine;
								}
							}
							else
							{
								yield return new OpenVpnLogLine(time, type, sb.ToString());
								goto nextLine;
							}
						}
					}
				}
			} // func GetEnumerator

			IEnumerator IEnumerable.GetEnumerator()
				=> GetEnumerator();
		} // class LogLineEnumerable

		#endregion

		private MemoryMappedFile EnsureSharedState()
		{
			if (memoryMap == null)
			{
				memoryMap = MemoryMappedFile.CreateOrOpen(exitEventName, sharedStateSize, MemoryMappedFileAccess.ReadWrite, MemoryMappedFileOptions.None, HandleInheritability.Inheritable);

				// add access to all users
				var security = memoryMap.GetAccessControl();
				var users = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
				security.AddAccessRule(new AccessRule<MemoryMappedFileRights>(
					users,
					MemoryMappedFileRights.FullControl,
					AccessControlType.Allow
				));
				memoryMap.SetAccessControl(security);

				using var m = memoryMap.CreateViewAccessor(0, 4, MemoryMappedFileAccess.ReadWrite);
				if (m.ReadByte(0) == 0) // signature is empty
				{
					m.Write(0, (byte)0xA5);
					m.Write(1, (byte)1);
				}
				return memoryMap;
			}
			else
				return memoryMap;
		} // func EnsureSharedState

		private static void GetHeader(MemoryMappedViewAccessor memory, out SharedStateHeander stateHeader)
			=> memory.Read(0, out stateHeader);

		private void GetHeader(out SharedStateHeander stateHeader)
		{
			using var m = EnsureSharedState().CreateViewAccessor(0, 32, MemoryMappedFileAccess.Read);
			GetHeader(m, out stateHeader);
		} // func GetHeader

		private bool IsLastWriteUpToDate(long lastWrite)
			=> lastWrite > 0 && (lastWrite - DateTime.Now.ToFileTimeUtc()) / 10000 < 5000;

		private void RefreshState()
		{
			if (isConnecting) // currently, connection, there will be no new state.
				return;

			GetHeader(out var state); // check for state
			if (connection != null || IsLastWriteUpToDate(state.FileTimeUtc)) // state exists and is up to date
				UpdateState(state.State, state.InBytes, state.OutBytes); // update state
			else // state is out dated -> try connect
			{
				StartConnectAsync(new Random().Next(100, 2000), 0, false).Silent(
					ex => Debug.Print(ex.ToString())
				);
			}
		} // proc RefreshState

		private async Task StartConnectAsync(int sleep, int retry, bool releaseHold)
		{
			isConnecting = true;
			try
			{
				await Task.Delay(sleep);

				// recheck header
				GetHeader(out var state);
				if (releaseHold || !IsLastWriteUpToDate(state.FileTimeUtc))
				{
					// start connection
					Log.Default.VpnConnectionOpen(Name);
					connection = await OpenVpnConnection.ConnectAsync(GetManagementPort(), GetPasswordFromEventName(), retry);
					connection.ByteCountChanged += Connection_ByteCountChanged;
					connection.StateChanged += Connection_StateChanged;
					connection.NeedPassword += Connection_NeedPassword;
					connection.LogLine += Connection_LogLine;
					connection.BackgroundException += Connection_BackgroundException;
					connection.Closed += Connection_Closed;
					if (releaseHold)
						await connection.ReleaseHoldAsync();

					RefreshState();
				}
			}
			finally
			{
				isConnecting = false;
			}
		} // func StartConnect

		private static OpenVpnState TranslateState(short state)
		{
			return state > 0 && state <= 9
				? (OpenVpnState)state
				: OpenVpnState.Active;
		} // func TranslateState

		private void UpdateState(short state, long inBytes, long outBytes)
		{
			Set(ref currentState, TranslateState(state), nameof(State));
			SetLong(ref this.inBytes, inBytes, nameof(InBytes));
			SetLong(ref this.outBytes, outBytes, nameof(OutBytes));
		} // proc UpdateState

		public IEnumerable<OpenVpnLogLine> LogLines
			=> new LogLineEnumerable(this);

		#endregion

		#region -- Connect ------------------------------------------------------------

		private bool isConnecting = false;
		private OpenVpnConnection connection = null;

		private static int GetFreePort()
		{
			using var s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			s.Bind(defaultLoopbackEndPoint);
			return ((IPEndPoint)s.LocalEndPoint).Port;
		} // func GetFreePort

		private static IPEndPoint GetFreeManagementEndPoint()
			=> new IPEndPoint(IPAddress.Loopback, GetFreePort());

		private string GetPasswordFromEventName()
			=> $"pwd{exitEventName.GetHashCode():x}";

		private int GetManagementPort()
		{
			using var r = OpenRunningVpnKey(false);
			return Convert.ToInt32(r.GetValue(exitEventName + "_Port"));
		} // func GetManagementPort

		private void Connection_Closed(object sender, EventArgs e)
		{
			connection = null;
			Log.Default.VpnConnectionClosed(Name);
		} // event Connection_Closed

		private void Connection_StateChanged(DateTime stamp, OpenVpnState state)
		{
			using var mem = EnsureSharedState().CreateViewAccessor(0, 32, MemoryMappedFileAccess.ReadWrite);
			GetHeader(mem, out var h);
			h.State = (short)state;
			h.FileTimeUtc = DateTime.Now.ToFileTimeUtc();
			mem.Write(0, ref h);
		} // event Connection_StateChanged

		private void Connection_ByteCountChanged(int cid, long inBytes, long outBytes)
		{
			using var mem = EnsureSharedState().CreateViewAccessor(0, 32, MemoryMappedFileAccess.ReadWrite);
			GetHeader(mem, out var h);
			h.InBytes = inBytes;
			h.OutBytes = outBytes;
			h.FileTimeUtc = DateTime.Now.ToFileTimeUtc();
			mem.Write(0, ref h);
		} // event Connection_ByteCountChanged

		private void Connection_LogLine(OpenVpnLogLine logLine)
		{
			switch (logLine.Type)
			{
				case OpenVpnLineTyp.Debug:
					Log.Default.VpnLogLineDebug(Name, logLine.Text);
					break;
				case OpenVpnLineTyp.FatalError:
				case OpenVpnLineTyp.NoneFatalError:
					Log.Default.VpnLogLineError(Name, logLine.Text);
					break;
				case OpenVpnLineTyp.Warning:
					Log.Default.VpnLogLineWarning(Name, logLine.Text);
					break;
				default:
					Log.Default.VpnLogLineInfo(Name, logLine.Text);
					break;
			}
		} // event Connection_LogLine

		private void Connection_NeedPassword(object sender, OpenVpnNeedPasswordArgs e)
		{
			NeedPassword?.Invoke(this, e);
			Log.Default.VpnPasswordNeeded(Name, e.Name, e.NeedUsername, e.UserName, e.NeedPassword, e.Handled);
		} // event Connection_NeedPassword

		private void Connection_BackgroundException(object sender, OpenVpnExceptionArgs e)
			=> Log.Default.VpnBackgroundException(Name, e.Exception.Message, e.Exception.ToString());

		#endregion

		#region -- Close, Wait --------------------------------------------------------

		private EventWaitHandle exitEvent = null;

		private string GetGlobalEventName()
			=> "Global\\" + exitEventName;

		private void UpdateExitEvent(EventWaitHandle newExitEvent)
		{
			Owner = GetEventOwner(newExitEvent);
			exitEvent = newExitEvent;
		} // proc UpdateExitEvent

		private void CreateExitEvent()
		{
			var newExitEvent = new EventWaitHandle(false, EventResetMode.ManualReset, GetGlobalEventName());

			// add access to all users
			var security = newExitEvent.GetAccessControl();
			var users = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
			security.AddAccessRule(new EventWaitHandleAccessRule(
				users,
				EventWaitHandleRights.FullControl,
				AccessControlType.Allow
			));
			newExitEvent.SetAccessControl(security);

			Log.Default.VpnCreateExitEvent(Name, exitEventName);

			// update event
			UpdateExitEvent(newExitEvent);
		} // func CreateExitEvent

		private static string GetEventOwner(EventWaitHandle eventHandle)
		{
			try
			{
				var accessControl = eventHandle.GetAccessControl();
				var nt = (NTAccount)accessControl.GetOwner(typeof(NTAccount));
				return nt.Value;
			}
			catch (UnauthorizedAccessException)
			{
				return null;
			}
		} // func GetEventOwner

		private bool TrySet()
		{
			if (WaitForActive(0))
			{
				exitEvent.Set();
				return true;
			}
			else
				return false;
		} // func Close

		public bool WaitForActive(int milliseconds)
		{
			// try open event
			if (exitEvent == null)
			{
				if (EventWaitHandle.TryOpenExisting(GetGlobalEventName(), EventWaitHandleRights.FullControl, out var ev))
				{
					Log.Default.VpnUseExistingEvent(Name, exitEventName);
					UpdateExitEvent(ev);
				}
				else
					return false;
			}

			// check state of the event
			if (exitEvent.WaitOne(milliseconds))
			{
				exitEvent.Dispose();
				exitEvent = null;
				return false;
			}
			else
				return true;
		} // func WaitForActive

		public bool Refresh()
		{
			var lastWasActive = exitEvent != null;
			if (WaitForActive(0)) // connection is active
			{
				RefreshState();
				return lastWasActive != true;
			}
			else
				return lastWasActive != false;
		} // proc Refresh

		#endregion

		/// <summary>Name of the name connection.</summary>
		public string Name { get; }
		/// <summary>Unique key of the running vpn.</summary>
		public string EventName => exitEventName;
		/// <summary>Initial creator of the vpn.</summary>
		public string Owner { get; private set; }
		/// <summary>Configuration file of the von</summary>
		public string ConfigFile => configFile;

		/// <summary>Current state of the connection</summary>
		public OpenVpnState State => currentState;
		/// <summary>Incoming bytes of this connection</summary>
		public long InBytes => inBytes;
		/// <summary>Outgoing bytes of this connection.</summary>
		public long OutBytes => outBytes;

		/// <summary>Is the vpn running (checks the exit event)</summary>
		public bool IsActive => WaitForActive(0);

		#region -- Configuration ------------------------------------------------------

		private const string openVpnRegistryPath = @"SOFTWARE\OpenVPN";
		private static readonly IPEndPoint defaultLoopbackEndPoint = new IPEndPoint(IPAddress.Loopback, 0);

		private static string GetServiceConfiguration(string valueName)
		{
			using var reg = Registry.LocalMachine.OpenSubKey(openVpnRegistryPath, false);
			return reg?.GetValue(valueName) as string;
		} // func GetConfiguration

		private static string GetDefaultLogPath(string configFile)
		{
			if (configFile == null)
				throw new ArgumentNullException(nameof(configFile));

			var logPath = GetServiceConfiguration("log_dir");
			if (logPath == null)
				throw new ArgumentNullException("log_dir");

			return Path.Combine(logPath, Path.GetFileNameWithoutExtension(configFile) + ".log");
		} // func GetDefaultLogPath

		private static string GetDefaultConfigPath()
			=> GetServiceConfiguration("config_dir");

		#endregion

		#region -- Get ----------------------------------------------------------------

		private const string knownVpnRegistryPath = @"Perfect Working\RunningVPN";
		private const RegistryRights registryPathWritePermissions = RegistryRights.QueryValues | RegistryRights.SetValue;
		private const RegistryRights registryPathReadPermissions = RegistryRights.QueryValues;

		private static RegistryKey OpenRunningVpnKey(bool write)
		{
			var registryPathPermissions = write ? registryPathWritePermissions : registryPathReadPermissions;
			var permissionCheck = write ? RegistryKeyPermissionCheck.ReadWriteSubTree : RegistryKeyPermissionCheck.ReadSubTree;
			var reg = Registry.LocalMachine;
			return reg.OpenSubKey(@"Software\WOW6432Node\" + knownVpnRegistryPath, permissionCheck, registryPathPermissions)
				?? reg.OpenSubKey(@"Software\" + knownVpnRegistryPath, permissionCheck, registryPathPermissions);
		} // func OpenRunningVpnKey

		private static string RegisterEventName(string configFile)
		{
			var exitEventName = Path.GetFileNameWithoutExtension(configFile) + "_" + configFile.GetHashCode().ToString("X");
			using var r = OpenRunningVpnKey(true);
			r.SetValue(exitEventName, configFile);
			return exitEventName;
		} // proc RegisterEventName

		private static void RemoveConfigFileFromRegistry(string eventName)
		{
			using var k2 = OpenRunningVpnKey(true);
			k2.DeleteValue(eventName);
			k2.DeleteValue(eventName + "_Port", false);
		} // proc RemoveConfigFileFromRegistry

		private static bool TryGetConfigFileFromRegistry(RegistryKey key, string eventName, out string openVpnConfigFile)
		{
			openVpnConfigFile = key.GetValue(eventName) as string;
			if (String.IsNullOrEmpty(openVpnConfigFile)) // no config
			{
				RemoveConfigFileFromRegistry(eventName);
				return false;
			}
			try
			{
				openVpnConfigFile = Path.GetFullPath(openVpnConfigFile);
				if (!File.Exists(openVpnConfigFile)) // missing file
				{
					Log.Default.VpnRemoveConfigFileFromRegistry(eventName, openVpnConfigFile);
					RemoveConfigFileFromRegistry(eventName);
					return false;
				}
			}
			catch
			{
				return false;
			}

			return true;
		} // func TryGetConfigFileFromRegistry

		public static IEnumerable<OpenVpnInfo> Get()
		{
			var returned = new List<string>();

			bool IsReturned(string cfg)
			{
				var r = returned.BinarySearch(cfg, StringComparer.OrdinalIgnoreCase);
				if (r >= 0)
					return true;

				returned.Insert(~r, cfg);
				return false;
			} // func IsReturned

			// enumerate all configurations, that are known from registry
			using (var r = OpenRunningVpnKey(false))
			{
				if (r != null)
				{
					foreach (var eventName in r.GetValueNames())
					{
						if (!eventName.EndsWith("_Port")
							&& TryGetConfigFileFromRegistry(r, eventName, out var openVpnConfigFile)
							&& !IsReturned(openVpnConfigFile))
						{
							yield return new OpenVpnInfo(eventName, openVpnConfigFile);
						}
					}
				}
			}

			// enumerate configurations from disk
			var configPath = GetDefaultConfigPath();
			if (!String.IsNullOrEmpty(configPath))
			{
				var cfgDirectory = new DirectoryInfo(configPath);
				if (cfgDirectory.Exists)
				{
					foreach (var di in cfgDirectory.EnumerateDirectories())
					{
						foreach(var fi in di.GetFiles("*.ovpn", SearchOption.TopDirectoryOnly))
						{
							if (!IsReturned(fi.FullName))
							{
								var openVpnConfigFile = fi.FullName;
								var eventName = RegisterEventName(openVpnConfigFile);
								yield return new OpenVpnInfo(eventName, openVpnConfigFile);
							}
						}
					}
				}
			}

			if (returned.Count > 0)
				Log.Default.VpnConfigFound(String.Join("\n", returned));
			else
				Log.Default.VpnSearchConfigs(configPath);
		} // func GetActive

		public static OpenVpnInfo Get(string configFile)
		{
			configFile = Path.GetFullPath(configFile);

			if (!File.Exists(configFile))
				throw new FileNotFoundException("File not found.", configFile);

			// search existing running configs
			using (var r = OpenRunningVpnKey(false))
			{
				foreach (var eventName in r.GetValueNames())
				{
					if (TryGetConfigFileFromRegistry(r, eventName, out var openVpnConfigFile)
						&& String.Compare(openVpnConfigFile, configFile, StringComparison.OrdinalIgnoreCase) == 0)
						return new OpenVpnInfo(eventName, openVpnConfigFile);
				}
			}

			return new OpenVpnInfo(RegisterEventName(configFile), configFile);
		} // func GetActive

		#endregion

		public static string EscaseString(string value)
		{
			return value.Replace("\\", "\\\\").Replace("\"","\\\"");
		} // proc EscaseString
	} // class OpenVpnInfo

	#endregion
}
