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

namespace Neo.PerfectWorking.QuickConnect
{
	[EventSource(Name = "PerfectWorking-QuickConnect-Log")]
	internal sealed class Log : EventSource
	{
		private Log()
			: base(EventSourceSettings.EtwManifestEventFormat)
		{
			//System.Diagnostics.Tracing.EventListener a = new EventListener();
			
		} // ctor

		[Event(1000, Channel = EventChannel.Operational, Level = EventLevel.Informational, Message = "Smb Verbindung to {0}")]
		public void SmbConnect(string remotePath)
			=> WriteEvent(1000, remotePath);

		public static Log Default { get; } = new Log();
	} // class Log
}
