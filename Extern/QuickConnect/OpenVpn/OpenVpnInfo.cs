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
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace Neo.PerfectWorking.OpenVpn
{
	#region -- class OpenVpnInfo ------------------------------------------------------

	/// <summary>Represents a running vpn.</summary>
	/// <remarks>HKLM\Software\WOW6432Node\Perfect Working\RunningVPN right to QueryValues, SetValue for all users</remarks>
	public sealed class OpenVpnInfo : IDisposable
	{
		private readonly string configFile;
		private readonly string exitEventName;

		#region -- Ctor/Dtor ----------------------------------------------------------

		private OpenVpnInfo(string eventName, string configFile)
		{
			this.exitEventName = eventName ?? throw new ArgumentNullException(nameof(eventName));
			this.configFile = configFile ?? throw new ArgumentNullException(nameof(configFile));

			Refresh();
		} // ctor

		public void Dispose()
		{
			exitEvent?.Dispose();
		} // proc Dispose

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
			try
			{
				var exePath = GetServiceConfiguration("exe_path");
				var managementPassword = GetPasswordFromEventName();
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
		
		public Task StartAsync(bool withService , bool holdLog = false)
		{
			if (WaitForActive(0))
				return Task.CompletedTask;

			return withService
				? StartWithServiceCoreAsync(holdLog)
				: StartCoreAsync(holdLog);
		} // proc StartAsync

		public Task CloseAsync()
		{
			if (TrySet())
				return Task.Run(() => WaitForActive(-1));
			else
				return Task.CompletedTask;
		} // proc CloseAsync

		#endregion

		#region -- Connect ------------------------------------------------------------

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

		//private readonly IPEndPoint managementEndPoint;
		//private readonly string managementPassword;

		//this.managementEndPoint = managementEndPoint ?? throw new ArgumentNullException(nameof(managementEndPoint));
		//managementPassword = GetPasswordFromEventName(eventName);

		//public async Task<OpenVpnConnection> ConnectAsync()
		//{
		//	// connect to socket
		//	var managementSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
		//	try
		//	{
		//		await managementSocket.ConnectAsync(managementEndPoint);
		//	}
		//	catch
		//	{
		//		managementSocket.Dispose();
		//		throw;
		//	}

		//	// init streams
		//	var managementStream = new NetworkStream(managementSocket, true);
		//	var connection = new OpenVpnConnection(managementStream);
		//	try
		//	{
		//		return await connection.InitAsync(managementPassword);
		//	}
		//	catch
		//	{
		//		connection.Dispose();
		//		throw;
		//	}
		//} // func ConnectAsync

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
					UpdateExitEvent(ev);
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
			return lastWasActive != WaitForActive(0); // connection is active
		} // proc Refresh

		#endregion

		/// <summary>Unique key of the running vpn.</summary>
		public string EventName => exitEventName;
		/// <summary>Initial creator of the vpn.</summary>
		public string Owner { get; private set; }
		/// <summary>Configuration file of the von</summary>
		public string ConfigFile => configFile;

		/// <summary>Is the vpn running</summary>
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
			var r = OpenRunningVpnKey(true);
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
				foreach (var eventName in r.GetValueNames())
				{
					if (TryGetConfigFileFromRegistry(r, eventName, out var openVpnConfigFile)
						&& !IsReturned(openVpnConfigFile))
						yield return new OpenVpnInfo(eventName, openVpnConfigFile);
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
								yield return new OpenVpnInfo(RegisterEventName(fi.FullName), fi.FullName);
						}
					}
				}
			}
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
	} // class OpenVpnInfo

	#endregion
}
