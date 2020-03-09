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
using System.Diagnostics.Tracing;

// wevtutil im PW8.PerfectWorking-Main-Log.etwManifest.man /rf:"C:\Projects\PW8\PW8\bin\Debug\PW8.PerfectWorking-Main-Log.etwManifest.dll" /mf:"C:\Projects\PW8\PW8\bin\Debug\PW8.PerfectWorking-Main-Log.etwManifest.dll"
// wevtutil um PW8.PerfectWorking-Main-Log.etwManifest.man
namespace Neo.PerfectWorking
{
	[EventSource(Name = "PerfectWorking-Main-Log")]
	internal sealed class Log : EventSource
	{
		public class Tasks
		{
			public const EventTask RefreshConfiguration = (EventTask)1;
		} // class Tasks

		private Log()
			: base(EventSourceSettings.EtwManifestEventFormat)
		{
		} // ctor

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

		[Event(10, Channel = EventChannel.Operational, Level = EventLevel.Informational, Keywords = EventKeywords.None,
			Message = "Register HotKey '{0}' with Id {1}: Successful")]
		public void RegisterHotKeySuccess(string hotKey, int keyId)
			=> WriteEvent(10, hotKey, keyId);

		[Event(11, Channel = EventChannel.Operational, Level = EventLevel.Error, Keywords = EventKeywords.None,
			Message = "Register HotKey '{0}' with Id {1}: Failed with {2}")]
		public void RegisterHotKeyFailed(string hotKey, int keyId, int errorCode)
			=> WriteEvent(11, hotKey, keyId, errorCode);

		public static Log Default { get; } = new Log();
	} // class Log
}
