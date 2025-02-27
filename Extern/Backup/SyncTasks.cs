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
using Microsoft.SqlServer.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Contexts;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.PerfectWorking.Backup
{
	#region -- interface ISyncIaskUI --------------------------------------------------

	public interface ISyncTaskUI
	{
		IProgress<int> StartTask(string taskName);

		SynchronizationContext Context { get; }
	} // interface ISyncTaskUI

	#endregion

	#region -- interface ISyncSafeIO --------------------------------------------------

	public interface ISyncSafeIO
	{
		void SafeIO(Action action, string actionDescription = null);
		T SafeIO<T>(Func<T> action, string actionDescription = null);

		Stream SafeOpen(Func<FileInfo, Stream> func, FileInfo file, string actionDescription = null);
	} // interface ISyncSafeIO

	#endregion

	#region -- class SyncTask ---------------------------------------------------------

	/// <summary>Synchronization item.</summary>
	public abstract class SyncTask : IComparable<SyncTask>
	{
		#region -- class CopyFileTask -------------------------------------------------

		private sealed class CopyFileTask : SyncTask
		{
			#region -- Win32 CopyFile -------------------------------------------------

			[Flags]
			private enum CopyFileFlags : int
			{
				FailIfExists = 0x00000001,
				Restartable = 0x00000002,
				OpenSourceForWrite = 0x00000004,
				AllowDecryptedDestination = 0x00000008,
				CopySymlink = 0x00000800,
				NoBuffering = 0x00001000,
				RequestCompressedTraffic = 0x10000000
			} // enum CopyFileFlags

			private enum CopyFileCallbackAction : int
			{
				Continue = 0,
				Cancel = 1,
				Stop = 2,
				Quiet = 3
			} // enum CopyFileCallbackAction

			private delegate CopyFileCallbackAction CopyProgressRoutineDelegate(ulong totalFileSize, ulong totalBytesTransferred, ulong streamSize, ulong streamBytesTransferred, uint dwStreamNumber, uint dwCallbackReason, IntPtr hSourceFile, IntPtr hDestinationFile, IntPtr lpData);

			[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
			private static extern bool CopyFileEx(string sourceFileName, string destinationFileName, CopyProgressRoutineDelegate progressRoutine, IntPtr lpData, IntPtr lpCancel, CopyFileFlags copyFlags);

			#endregion

			#region -- class PercentProgress ------------------------------------------

			private sealed class PercentProgress
			{
				private readonly IProgress<int> progress;
				private readonly CancellationToken cancellationToken;
				private readonly bool isRestartable;

				private int currentProgress; // progress in based on 1000

				public PercentProgress(IProgress<int> progress, CancellationToken cancellationToken, bool isRestartable)
				{
					this.progress = progress ?? throw new ArgumentNullException(nameof(progress));
					this.cancellationToken = cancellationToken;
					this.isRestartable = isRestartable;
				} // ctor

				public void Update(ulong transfered, ulong total)
				{
					var newProgress = total > 0 ? unchecked((int)(transfered * 1000 / total)) : -1;
					if (newProgress != currentProgress)
					{
						currentProgress = newProgress;
						progress.Report(newProgress);
					}
				} // proc Update

				public bool IsCanceled => cancellationToken.IsCancellationRequested;
				public bool IsRestartable => isRestartable;
			} // class PercentProgress

			#endregion

			private const FileAttributes notAllowedDestinationAttributes = FileAttributes.Hidden | FileAttributes.ReadOnly;

			private readonly FileInfo source;
			private readonly FileInfo destination;

			public CopyFileTask(SyncTask parentTask, string relativeName, FileInfo source, FileInfo destination)
				: base(parentTask, relativeName)
			{
				this.source = source ?? throw new ArgumentNullException(nameof(source));
				this.destination = destination ?? throw new ArgumentNullException(nameof(destination));
			} // ctor

			private CopyFileCallbackAction CopyProgress(ulong totalFileSize, ulong totalBytesTransferred, ulong streamSize, ulong streamBytesTransferred, uint dwStreamNumber, uint dwCallbackReason, IntPtr hSourceFile, IntPtr hDestinationFile, IntPtr lpData)
			{
				var hProgress = GCHandle.FromIntPtr(lpData);
				if (hProgress.Target is PercentProgress pp)
				{
					//if (dwCallbackReason == 1) {} // next stream is copyied
					
					pp.Update(totalBytesTransferred, totalFileSize);

					return pp.IsCanceled ? (pp.IsRestartable ? CopyFileCallbackAction.Stop : CopyFileCallbackAction.Cancel) : CopyFileCallbackAction.Continue;
				}
				else
					return CopyFileCallbackAction.Continue;
			} // proc CopyProgress

			private void CopyCore(IProgress<int> progress, CopyFileFlags flags)
			{
				// start copy operation with progress
				var percentProgress = new PercentProgress(progress, CancellationToken.None, (flags & CopyFileFlags.Restartable) != 0);
				var hProgress = GCHandle.Alloc(percentProgress, GCHandleType.Normal);
				try
				{
					// start copy file
					if (!CopyFileEx(source.FullName, destination.FullName, CopyProgress, GCHandle.ToIntPtr(hProgress), IntPtr.Zero, flags))
						throw new Win32Exception();
				}
				finally
				{
					hProgress.Free();
				}
			} // proc CopyCore

			private void Copy(IProgress<int> progress)
			{
				// create
				var flags = CopyFileFlags.AllowDecryptedDestination | CopyFileFlags.RequestCompressedTraffic;
				if (source.Length > 1 << 19)
					flags |= CopyFileFlags.Restartable;

				// remove hidden,readonly for destination
				if (destination.Exists && (destination.Attributes & notAllowedDestinationAttributes) != 0)
					IO.SafeIO(() => destination.Attributes &= ~notAllowedDestinationAttributes, $"Attribute setzen: {destination.FullName}");

				progress.Report(0);
				IO.SafeIO(() => CopyCore(progress, flags), $"Kopieren: {destination.FullName}");

				// check attributes
				progress.Report(-2);
				IO.SafeIO(() =>
				{
					destination.Refresh();
					CopyAttributes(source, destination);
				}, $"Attribute setzen: {destination.FullName}");
			} // proc Copy

			protected override Task ExecuteAsync(IProgress<int> progress)
				=> Task.Run(() => Copy(progress));

			protected override string Action => "Kopiere {0}";
			public override long Length => source.Length;
			public override DateTime Stamp => source.LastWriteTimeUtc;
		} // class CopyFileTask

		#endregion

		#region -- class WriteFileTask ------------------------------------------------

		private sealed class WriteFileTask : SyncTask
		{
			private readonly DateTime sourceLastWwriteUtc;
			private readonly DateTime sourceCreationTimeUtc;
			private readonly FileAttributes sourceFileAttributes;
			private readonly byte[] data;
			private readonly FileInfo destination;

			public WriteFileTask(ISyncSafeIO safeIO, SyncTask parentTask, string relativeName, FileInfo source, FileInfo destination)
				: base(parentTask, relativeName)
			{
				sourceCreationTimeUtc = source.CreationTimeUtc;
				sourceLastWwriteUtc = source.LastWriteTimeUtc;
				sourceFileAttributes = source.Attributes;
				
				using (var src = safeIO.SafeOpen(fi => fi.OpenRead(), source, $"Lese: {relativeName}"))
				{
					if (src.Length == 0)
						data = Array.Empty<byte>();
					else
					{
						data = new byte[src.Length];
						src.Read(data, 0, data.Length);
					}
				}

				this.destination = destination ?? throw new ArgumentNullException(nameof(destination));
			} // ctor

			private void Write(IProgress<int> progress)
			{
				using (var dst = IO.SafeOpen(fi => fi.Create(), destination, $"Datei nicht angelegt: {destination.FullName}"))
				{
					var offset = 0;
					while (offset < data.Length)
					{
						// write content
						var w = Math.Min(data.Length - offset, 0x10000);
						dst.Write(data, offset, w);
						offset += w;

						// report change
						progress.Report(offset * 1000 / data.Length);
					}
				}

				progress.Report(-2);
				IO.SafeIO(() => CopyAttributes(sourceCreationTimeUtc, sourceLastWwriteUtc, sourceFileAttributes, destination), $"Attribute setzen: {destination.FullName}");
			} // proc Write

			protected override Task ExecuteAsync(IProgress<int> progress)
				=> Task.Run(() => Write(progress));

			protected override string Action => "Schreibe {0}";
			public override long Length => data.Length;
			public override DateTime Stamp => sourceLastWwriteUtc;
			public override int CacheSize => data.Length;
		} // class WriteFileTask

		#endregion

		#region -- class EnsureDirectoryTask ------------------------------------------

		private sealed class EnsureDirectoryTask : SyncTask
		{
			private readonly DirectoryInfo source;
			private readonly DirectoryInfo destination;

			public EnsureDirectoryTask(SyncTask parentTask, string relativeName, DirectoryInfo source, DirectoryInfo destination)
				: base(parentTask, relativeName)
			{
				this.source = source ?? throw new ArgumentNullException(nameof(source));
				this.destination = destination ?? throw new ArgumentNullException(nameof(destination));
			} // ctor

			private void EnsureDirectory()
			{
				if (!destination.Exists)
					IO.SafeIO(destination.Create, $"Verzeichnis konnte nicht erzeugt werden: {destination.FullName}");

				IO.SafeIO(() =>
				{
					destination.Refresh();
					CopyAttributes(source, destination);
				}, $"Attribute konnten nicht gesetzt werden: {destination.FullName}");
			} // proc EnsureDirectory

			protected override Task ExecuteAsync(IProgress<int> progress)
				=> Task.Run(() => EnsureDirectory());

			protected override string Action => "Erzeuge {0}";
			public override long Length => 0;
			public override DateTime Stamp => source.LastWriteTimeUtc;
		} // class EnsureDirectoryTask

		#endregion

		#region -- class DeleteFileTask -----------------------------------------------

		private sealed class DeleteFileTask : SyncTask
		{
			private readonly FileInfo destination;

			public DeleteFileTask(SyncTask parentTask, string relativeName, FileInfo destination)
				: base(parentTask, relativeName)
			{
				this.destination = destination ?? throw new ArgumentNullException(nameof(destination));
			} // ctor

			private void DeleteCore()
			{
				destination.Refresh();
				if (destination.Exists)
				{
					if (destination.Attributes != FileAttributes.Normal)
						destination.Attributes = FileAttributes.Normal;
					destination.Delete();
				}
			} // proc DeleteCore

			protected override Task ExecuteAsync(IProgress<int> progress)
				=> Task.Run(() => IO.SafeIO(DeleteCore, $"Löschen der Datei: {destination.FullName}"));

			protected override string Action => "Lösche {0}";
			public override long Length => 0;
			public override DateTime Stamp => destination.LastWriteTimeUtc;
		} // class DeleteFileTask

		#endregion

		#region -- class DeleteDirectoryTask ------------------------------------------

		private sealed class DeleteDirectoryTask : SyncTask
		{
			private readonly DirectoryInfo destination;

			public DeleteDirectoryTask(SyncTask parentTask, string relativeName, DirectoryInfo destination)
				: base(parentTask, relativeName)
			{
				this.destination = destination ?? throw new ArgumentNullException(nameof(destination));
			} // ctor

			protected override Task ExecuteAsync(IProgress<int> progress)
				=> Task.Run(() => IO.SafeIO(() => destination.Delete(true), $"Löschen des Verzeichnisses: {destination.FullName}"));

			protected override string Action => "Lösche Verzeichnis {0}";
			public override long Length => 0;
			public override DateTime Stamp => destination.LastWriteTimeUtc;
		} // class DeleteDirectoryTask

		#endregion

		private readonly SyncTask parentTask;
		private int childTasks = 0;
		private readonly string relativeName;

		private SyncItems syncService;
		private bool isStarted = false;
		private bool isFinished = false;
		private readonly Queue<TaskCompletionSource<bool>> waitTasks = new Queue<TaskCompletionSource<bool>>();

		protected SyncTask(SyncTask parentTask, string relativeName)
		{
			this.parentTask = parentTask;
			if (parentTask != null)
				parentTask.childTasks++;

			this.relativeName = relativeName ?? throw new ArgumentNullException(nameof(relativeName));
		} // ctor

		public override bool Equals(object obj)
			=> obj is SyncTask t && CompareTo(t) == 0;

		public override int GetHashCode()
			=> relativeName.GetHashCode();

		public int CompareTo(SyncTask other)
		{
			if (other is null)
				return -1;
			else
			{
				var n = String.Compare(relativeName, other.relativeName, StringComparison.OrdinalIgnoreCase);
				if (n == 0)
					return 0;
				else
				{
					var lengthCompare = Length - other.Length;
					if (lengthCompare == 0)
					{
						var ticksCompare = Stamp.Ticks - other.Stamp.Ticks;
						if (ticksCompare == 0)
							return n;
						else
							return ticksCompare <= 0 ? -1 : 1;
					}
					else
						return lengthCompare < 0 ? -1 : 1;
				}
			}
		} // proc CompareTo

		private static FileSystemInfo UnsetReadonly(FileSystemInfo fsi)
		{
			if (fsi.Exists && (fsi.Attributes & FileAttributes.ReadOnly) != 0)
				fsi.Attributes &= ~FileAttributes.ReadOnly;
			return fsi;
		} // func UnsetReadonly

		protected static void CopyAttributes(DateTime sourceCreationTimeUtc, DateTime sourceLastWriteTimeUtc, FileAttributes sourceAttributes, FileSystemInfo destination)
		{
			if (destination.CreationTimeUtc != sourceCreationTimeUtc)
				UnsetReadonly(destination).CreationTimeUtc = sourceCreationTimeUtc;
			if (destination is FileInfo && destination.LastWriteTimeUtc != sourceLastWriteTimeUtc)
				UnsetReadonly(destination).LastWriteTimeUtc = sourceLastWriteTimeUtc;

			var newAttributes = (destination.Attributes & ~SyncItems.CheckAttributeSet) | sourceAttributes & SyncItems.CheckAttributeSet;
			if (destination.Attributes != newAttributes)
				destination.Attributes = newAttributes;
		} // proc CopyAttributes

		protected static void CopyAttributes(FileSystemInfo source, FileSystemInfo destination)
			=> CopyAttributes(source.CreationTimeUtc, source.LastWriteTimeUtc, source.Attributes, destination);

		protected abstract Task ExecuteAsync(IProgress<int> progress);

		private Task WaitAsync()
		{
			lock (waitTasks)
			{
				if (isFinished)
					return Task.CompletedTask;

				var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
				waitTasks.Enqueue(tcs);
				return tcs.Task;
			}
		} // func WaitAsync

		private void ExecuteFinished(Exception e)
		{
			// mark task finished successful
			lock (waitTasks)
			{
				isFinished = true;
				while (waitTasks.Count > 0)
				{
					var w = waitTasks.Dequeue();
					if (e != null)
						w.TrySetException(e);
					else
						w.TrySetResult(true);
				}
			}
		} // proc ExecuteFinished

		public async Task ExecuteAsync(SyncItems syncService)
		{
			if (parentTask != null)
				await parentTask.WaitAsync();

			this.syncService = syncService;
			var progress = syncService.UI.StartTask(Name);
			try
			{
				await ExecuteAsync(progress);

				ExecuteFinished(null);
			}
			catch (Exception e)
			{
				ExecuteFinished(e);
				throw;
			}
			finally
			{
				if (progress is IDisposable d)
					d.Dispose();

				syncService.NotifyTaskExecuted(this);
				this.syncService = null;
			}
		} // proc ExecuteAsync

		public SyncTask Parent => parentTask;
		public bool HasChildren => childTasks > 0;

		protected abstract string Action { get; }
		public string Name => String.Format(Action, relativeName);
		public abstract long Length { get; }
		public abstract DateTime Stamp { get; }
		public virtual int CacheSize => 0;

		public bool IsStarted { get => isStarted; set => isStarted = value; }
		public bool IsFinished => isFinished;
		protected ISyncSafeIO IO => syncService;

		public static SyncTask RemoveItem(SyncTask parentTask, string relativeParentPath, FileSystemInfo fsiToRemove)
		{
			var relativeName = Path.Combine(relativeParentPath, fsiToRemove.Name);
			if (fsiToRemove is DirectoryInfo di)
				return new DeleteDirectoryTask(parentTask, relativeName, di);
			else if (fsiToRemove is FileInfo fi)
				return new DeleteFileTask(parentTask, relativeName, fi);
			else
				throw new InvalidOperationException();
		} // func RemoveItem

		public static SyncTask EnsureAttributes(SyncTask parentTask, string relativeName, FileSystemInfo fsiSource, FileSystemInfo fsiTarget)
		{
			if (fsiSource is DirectoryInfo diSource && fsiTarget is DirectoryInfo diTarget)
				return new EnsureDirectoryTask(parentTask, relativeName, diSource, diTarget);
			else
				throw new InvalidOperationException();
		} // func EnsureAttributes

		public static Task<SyncTask> CopyItemAsync(ISyncSafeIO safeIO, SyncTask parentTask, string relativeName, FileInfo fiSource, FileInfo fiTarget, int cacheBorder, bool useCopyCache)
		{
			if (useCopyCache && fiSource.Length < cacheBorder)
				return Task.Run<SyncTask>(() => new WriteFileTask(safeIO, parentTask, relativeName, fiSource, fiTarget));
			else
				return Task.FromResult<SyncTask>(new CopyFileTask(parentTask, relativeName, fiSource, fiTarget));
		} // func CopyItemAsync
	} // class SyncIask

	#endregion
}
