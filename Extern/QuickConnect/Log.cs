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
using System.Diagnostics.Tracing;

namespace Neo.PerfectWorking.QuickConnect
{
	[EventSource(Name = "PerfectWorking-QuickConnect-Log")]
	internal sealed class Log : EventSource
	{
		#region -- class Tags ---------------------------------------------------------

		public sealed class Tags
		{
			public const EventTags Smb = (EventTags)1;
			public const EventTags Vpn = (EventTags)2;
		} // class Tags

		#endregion

		private Log()
			: base(EventSourceSettings.EtwManifestEventFormat)
		{
		} // ctor

		#region -- Smb ----------------------------------------------------------------

		[Event(1000, Channel = EventChannel.Operational, Tags = Tags.Smb, Level = EventLevel.Informational, Message = "Smb Verbindung to {0}")]
		public void SmbConnect(string remotePath)
			=> WriteEvent(1000, remotePath);

		[Event(1001, Channel = EventChannel.Debug, Tags = Tags.Smb, Level = EventLevel.Verbose, Message = "Smb Refresh {0}ms")]
		public void SmbRefreshState(long duration)
			=>  WriteEvent(1001, duration);

		#endregion

		#region -- Vpn ----------------------------------------------------------------

		[Event(2000, Channel = EventChannel.Operational, Tags = Tags.Vpn, Level = EventLevel.Informational, Message = "Vpn Konfigurationen gefunden.\n{0}")]
		public void VpnConfigFound(string configurations)
			=> WriteEvent(2000, configurations);
		
		[Event(2001, Channel = EventChannel.Operational, Tags = Tags.Vpn, Level = EventLevel.Informational, Message = "Keine Vpn Konfiguration gefunden in {0}")]
		public void VpnSearchConfigs(string fullName)
			=> WriteEvent(2001, fullName);

		[Event(2002, Channel = EventChannel.Operational, Tags = Tags.Vpn, Level = EventLevel.Informational, Message = "Vpn Konfiguration wird aus der Registrierung entfernt {0}\nKonfiguration: {1})")]
		public void VpnRemoveConfigFileFromRegistry(string eventName, string openVpnConfigFile)
			=> WriteEvent(2002, eventName, openVpnConfigFile);

		[Event(2003, Channel = EventChannel.Operational, Tags = Tags.Vpn, Level = EventLevel.Informational, Message = "{0}: Create event {1}")]
		public void VpnCreateExitEvent(string name, string exitEventName) 
			=> WriteEvent(2003, name, exitEventName);
		
		[Event(2004, Channel = EventChannel.Operational, Tags = Tags.Vpn, Level = EventLevel.Informational, Message = "{0}: Use event {1}")]
		public void VpnUseExistingEvent(string name, string exitEventName)
			=> WriteEvent(2004, name, exitEventName);

		[Event(2005, Channel = EventChannel.Operational, Tags = Tags.Vpn, Level = EventLevel.Error, Message = "{0}: {1}\n{2}")]
		public void VpnBackgroundException(string name, string message, string exception)
			=> WriteEvent(2005, name, message, exception);

		[Event(2010, Channel = EventChannel.Operational, Tags = Tags.Vpn, Level = EventLevel.Informational, Message = "{0}: Started (pipe)\n{1}")]
		public void VpnStartConfigViaPipe(string name, string cmdLine)
			=> WriteEvent(2010, name, cmdLine);

		[Event(2011, Channel = EventChannel.Operational, Tags = Tags.Vpn, Level = EventLevel.Informational, Message = "{0}: Started (direct)\n{1} {2}")]
		public void VpnStartConfigViaExe(string name, string exePath, string cmdLine)
			=> WriteEvent(2011, name, exePath, cmdLine);

		[Event(2012, Channel = EventChannel.Operational, Tags = Tags.Vpn, Level = EventLevel.Informational, Message = "{0}: Open management interface")]
		public void VpnConnectionOpen(string name)
			=> WriteEvent(2012, name);

		[Event(2013, Channel = EventChannel.Operational, Tags = Tags.Vpn, Level = EventLevel.Informational, Message = "{0}: Close management interface")]
		public void VpnConnectionClosed(string name)
			=> WriteEvent(2013, name);

		[Event(2014, Channel = EventChannel.Operational, Tags = Tags.Vpn, Level = EventLevel.Verbose, Message = "{0}: {1}")]
		public void VpnLogLineDebug(string name, string text)
			=> WriteEvent(2014, name, text);

		[Event(2015, Channel = EventChannel.Operational, Tags = Tags.Vpn, Level = EventLevel.Error, Message = "{0}: {1}")]
		public void VpnLogLineError(string name, string text)
			=> WriteEvent(2015, name, text);

		[Event(2016, Channel = EventChannel.Operational, Tags = Tags.Vpn, Level = EventLevel.Warning, Message = "{0}: {1}")]
		public void VpnLogLineWarning(string name, string text) 
			=> WriteEvent(2016, name, text);

		[Event(2017, Channel = EventChannel.Operational, Tags = Tags.Vpn, Level = EventLevel.Informational, Message = "{0}: {1}")]
		public void VpnLogLineInfo(string name, string text)
			=> WriteEvent(2017, name, text);

		[Event(2018, Channel = EventChannel.Operational, Tags = Tags.Vpn, Level = EventLevel.Informational, Message = "{0}: Authentification requested\nName: {1}\nNeed username: {2} '{3}'\nNeed password: {4}\nHandled: {5}")]
		public void VpnPasswordNeeded(string name, string authName, bool needUsername, string userName, bool needPassword, bool handled)
			=> WriteEvent(2018, name, authName, needUsername, userName, needPassword, handled);

		#endregion

		public static Log Default { get; } = new Log();
	} // class Log
}
