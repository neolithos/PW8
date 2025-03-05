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
using TecWare.DE.Stuff;

namespace Neo.PerfectWorking.Cred
{
	[EventSource(Name = "PerfectWorking-Cred-Log")]
	internal sealed class Log : EventSource
	{
		#region -- class Tags ---------------------------------------------------------

		public sealed class Tags
		{
			public const EventTags FileProvider = (EventTags)1;
		} // class Tags

		#endregion

		private Log()
			: base(EventSourceSettings.EtwManifestEventFormat)
		{
		} // ctor

		#region -- FileProvider -------------------------------------------------------

		[Event(1000, Channel = EventChannel.Operational, Tags = Tags.FileProvider, Level = EventLevel.Informational, Message = "[{0}] Quelldatei ist nicht verfügbar.\n{1}")]
		public void SourceFileIsOffline(string provider, string fileName)
			=> WriteEvent(1000, provider, fileName);

		[Event(1001, Channel = EventChannel.Operational, Tags = Tags.FileProvider, Level = EventLevel.Informational, Message = "[{0}] Hole neu Daten ab.\n{1}")]
		public void SourceFileCopy(string provider, string fileName)
			=> WriteEvent(1001, provider, fileName);

		[Event(1002, Channel = EventChannel.Operational, Tags = Tags.FileProvider, Level = EventLevel.Warning, Message = "[{0}] Quelldatei konnte nicht kopiert werden.\n{1}\nFehler: {2}")]
		public void SourceFileCopyFailed(string provider, string fileName, string message)
			=> WriteEvent(1002, provider, fileName, message);

		public static void FileProviderLoadFailed(string provider, Exception e)
			=> Default.FileProviderLoadFailed(provider, e.GetInnerException().Message, e.GetMessageString());

		[Event(1003, Channel = EventChannel.Operational, Tags = Tags.FileProvider, Level = EventLevel.Error, Message = "[{0}] Laden der Daten fehlgeschlagen: {1}")]
		public void FileProviderLoadFailed(string provider, string message, string exception)
			=> WriteEvent(1003, provider, message, exception);

		public static void FileProviderSaveFailed(string provider, Exception e)
			=> Default.FileProviderSaveFailed(provider, e.GetInnerException().Message, e.GetMessageString());

		[Event(1004, Channel = EventChannel.Operational, Tags = Tags.FileProvider, Level = EventLevel.Error, Message = "[{0}] Speichern der Daten fehlgeschlagen: {1}")]
		public void FileProviderSaveFailed(string provider, string message, string exception)
			=> WriteEvent(1004, provider, message, exception);

		#endregion

		public static Log Default { get; } = new Log();
	} // class Log
}
