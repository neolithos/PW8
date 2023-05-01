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

// wevtutil im PW8.PerfectWorking-Main-Log.etwManifest.man /rf:"C:\Projects\PW8\PW8\bin\Debug\PW8.PerfectWorking-Main-Log.etwManifest.dll" /mf:"C:\Projects\PW8\PW8\bin\Debug\PW8.PerfectWorking-Main-Log.etwManifest.dll"
// wevtutil um PW8.PerfectWorking-Main-Log.etwManifest.man
namespace Neo.PerfectWorking
{
	[EventSource(Name = "PerfectWorking-Main-Log")]
	internal sealed class Log : EventSource
	{
		public sealed class Tasks
		{
			public const EventTask RefreshConfiguration = (EventTask)1;
		} // class Tasks

		public sealed class Tags
		{
			public const EventTags HotKey = (EventTags)1;
		} // class Tags

		private Log()
			: base(EventSourceSettings.EtwManifestEventFormat)
		{
		} // ctor

		#region -- Global -------------------------------------------------------------

		[Event(1, Channel = EventChannel.Operational, Task = Tasks.RefreshConfiguration, Opcode = EventOpcode.Start, Level = EventLevel.Informational, Keywords = EventKeywords.None, 
			Message = "Loading configuration file: {0}")]
		public void StartRefreshCongifuration(string configurationFile)
			=> WriteEvent(1, configurationFile);

		[Event(2, Channel = EventChannel.Operational, Task = Tasks.RefreshConfiguration, Opcode = EventOpcode.Stop, Level = EventLevel.Informational, Keywords = EventKeywords.None, 
			Message = "Configuration loaded: {0} (compile:{1}ms, run: {2}ms, clean: {3}ms)")]
		public void FinishRefreshCongifuration(string configurationFile, long compileTime, long runTime, long cleanTime)
			=> WriteEvent(2, configurationFile, compileTime, runTime, cleanTime);

		[Event(3, Channel = EventChannel.Operational, Level = EventLevel.Error, Keywords = EventKeywords.None, 
			Message = "Exception '{0}': {1}")]
		public void Exception(string text, string exceptionFull)
			=> WriteEvent(3, text, exceptionFull);

		[Event(4, Channel = EventChannel.Operational, Level = EventLevel.Verbose, Keywords = EventKeywords.None,
			Message = "{0}")]
		public void LuaPrint(string text)
			=> WriteEvent(4, text);

		#endregion

		#region -- HotKey -------------------------------------------------------------

		[Event(10, Channel = EventChannel.Operational, Level = EventLevel.Informational, Keywords = EventKeywords.None, Tags = Tags.HotKey,
			Message = "Register HotKey '{0}' with Id {1}: Successful")]
		public void RegisterHotKeySuccess(string hotKey, int keyId)
			=> WriteEvent(10, hotKey, keyId);

		[Event(11, Channel = EventChannel.Operational, Level = EventLevel.Error, Keywords = EventKeywords.None, Tags = Tags.HotKey,
			Message = "Register HotKey '{0}' with Id {1}: Failed with {2}")]
		public void RegisterHotKeyFailed(string hotKey, int keyId, int errorCode)
			=> WriteEvent(11, hotKey, keyId, errorCode);

		[Event(12, Channel = EventChannel.Operational, Level = EventLevel.Informational, Keywords = EventKeywords.None, Tags = Tags.HotKey,
			Message = "HotKey processed (id={0},key={1})")]
		public void HotKeyProcessed(int hotKeyId, string keyString)
			=> WriteEvent(12, hotKeyId, keyString);

		[Event(13, Channel = EventChannel.Operational, Level = EventLevel.Error, Keywords = EventKeywords.None, Tags = Tags.HotKey,
			Message = "HotKey not processed (id={0})")]
		public void HotKeyUnprocessed(int hotKeyId)
			=> WriteEvent(13, hotKeyId);

		[Event(14, Channel = EventChannel.Operational, Level = EventLevel.Informational, Keywords = EventKeywords.None, Tags = Tags.HotKey,
			Message = "Send key strokes: {0} chars")]
		public void SendKeyData(int chars)
			=> WriteEvent(14, chars);

		[Event(15, Channel = EventChannel.Operational, Level = EventLevel.Informational, Keywords = EventKeywords.None, Tags = Tags.HotKey,
			Message = "Send KeyDown: {0}")]
		public void SendKeyDown(string key)
			=> WriteEvent(15, key);

		[Event(16, Channel = EventChannel.Operational, Level = EventLevel.Informational, Keywords = EventKeywords.None, Tags = Tags.HotKey,
			Message = "Send KeyUp: {0}")]
		public void SendKeyUp(string key)
			=> WriteEvent(16, key);

		[Event(17, Channel = EventChannel.Operational, Level = EventLevel.Informational, Keywords = EventKeywords.None, Tags = Tags.HotKey,
			Message = "Send KeyDown/Up: {0}")]
		public void SendKey(string key)
			=> WriteEvent(17, key);

		#endregion

		#region -- Credential ---------------------------------------------------------

		[Event(20, Channel = EventChannel.Operational, Level = EventLevel.Informational, Keywords = EventKeywords.None,
			Message = "Credential returned: {0}\nName: {1}/{2}")]
		public void CredentialReturned(string targetName, string domain, string userName)
			=> WriteEvent(20, targetName, domain, userName);

		[Event(21, Channel = EventChannel.Operational, Level = EventLevel.Error, Keywords = EventKeywords.None,
			Message = "Credential missing: {0}")]
		public void CredentialNotReturned(string targetName)
			=> WriteEvent(21, targetName);

		#endregion

		public static Log Default { get; } = new Log();
	} // class Log
}
