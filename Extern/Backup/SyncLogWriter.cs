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
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Neo.PerfectWorking.Backup
{
	#region -- class SyncLogWriter ----------------------------------------------------

	public sealed class SyncLogWriter : IDisposable
	{
		private readonly TextWriter writer;
		private readonly DateTime logStarted = DateTime.Now;
		private bool isDisposed = false;
		private DateTime lastFlushed = DateTime.Now;

		private SyncLogWriter(TextWriter writer)
		{
			this.writer = writer ?? throw new ArgumentNullException(nameof(writer));

			writer.WriteLine($"Started: {logStarted:G}");
		} // ctor

		public void Dispose()
		{
			if (isDisposed)
				throw new ObjectDisposedException(nameof(SyncLogWriter));
			isDisposed = true;

			writer.WriteLine($"Closed: {DateTime.Now - logStarted:c}");
			writer.Dispose();
		} // proc Dispose

		private string GetLogLine(string action, string path, string app)
		{
			var diff = DateTime.Now - logStarted;
			return $"{diff:hh\\:mm\\:ss\\,fff};{action,-20};{path};{app}";
		} // func GetLogLine

		private string GetLogLineSimple(string m)
		{
			var p = m.IndexOf(':');
			return p == -1
				? GetLogLine(null, null, m)
				: GetLogLine(m.Substring(0, p).Trim(), m.Substring(p + 1).Trim(), null);
		} // func GetLogLine

		private async Task FlushLogAsync()
		{
			if ((DateTime.Now - lastFlushed).Ticks > 10000)
			{
				await writer.FlushAsync();
				lastFlushed = DateTime.Now;
			}
		} // proc FlushLogAsync

		public async Task WriteEntryAsync(string action, string path, string app = null)
		{
			await writer.WriteLineAsync(GetLogLine(action, path, app));
			await FlushLogAsync();
		} // proc WriteEntry

		public async Task WriteLineAsync(string m)
		{
			await writer.WriteLineAsync(GetLogLineSimple(m));
			await FlushLogAsync();
		} // proc WriteEntry

		public static SyncLogWriter Create(string fileName)
		{
			var fi = new FileInfo(fileName);
			if (!fi.Exists)
				File.WriteAllBytes(fileName, Array.Empty<byte>());

			var dst = new FileStream(fileName, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
			if (dst.Length > 0)
				dst.SetLength(0);

			return new SyncLogWriter(new StreamWriter(dst, Encoding.UTF8, 1 << 20, false));
		} // func Create
	} // class SyncLogWriter

	#endregion
}
