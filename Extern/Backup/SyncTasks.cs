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
using System.IO;
using System.Threading.Tasks;

namespace Neo.PerfectWorking.Backup
{
	#region -- interface ISyncIaskUI --------------------------------------------------

	public interface ISyncTaskUI
	{
	} // interface ISyncTaskUI

	#endregion

	#region -- class SyncTask ---------------------------------------------------------

	/// <summary>Synchronization item.</summary>
	public abstract class SyncTask : IComparable<SyncTask>
	{
		#region -- class CopyFileTask -------------------------------------------------

		private sealed class CopyFileTask : SyncTask
		{
			private readonly FileInfo source;
			private readonly FileInfo destination;

			public CopyFileTask(SyncTask parentTask, string relativeName, FileInfo source, FileInfo destination)
				: base(parentTask, relativeName)
			{
				this.source = source ?? throw new ArgumentNullException(nameof(source));
				this.destination = destination ?? throw new ArgumentNullException(nameof(destination));
			} // ctor

			private void Copy()
			{
				source.CopyTo(destination.FullName, true);
				CopyAttributes(source, destination);
			} // proc Copy

			protected override Task ExecuteAsync()
				=> Task.Run(() => Copy());

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

			public WriteFileTask(SyncTask parentTask, string relativeName, FileInfo source, FileInfo destination)
				: base(parentTask, relativeName)
			{
				sourceCreationTimeUtc = source.CreationTimeUtc;
				sourceLastWwriteUtc = source.LastWriteTimeUtc;
				sourceFileAttributes = source.Attributes;
				data = File.ReadAllBytes(source.FullName);

				this.destination = destination ?? throw new ArgumentNullException(nameof(destination));
			} // ctor

			private void Write()
			{
				File.WriteAllBytes(destination.FullName, data);
				CopyAttributes(sourceCreationTimeUtc, sourceLastWwriteUtc, sourceFileAttributes, destination);
			} // proc Write

			protected override Task ExecuteAsync()
				=> Task.Run(() => Write());

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
					destination.Create();

				CopyAttributes(source, destination);
			} // proc EnsureDirectory

			protected override Task ExecuteAsync()
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

			protected override Task ExecuteAsync()
				=> Task.Run(() => destination.Delete());

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

			protected override Task ExecuteAsync()
				=> Task.Run(() => destination.Delete(true));

			protected override string Action => "Lösche Verzeichnis {0}";
			public override long Length => 0;
			public override DateTime Stamp => destination.LastWriteTimeUtc;
		} // class DeleteDirectoryTask

		#endregion

		private readonly SyncTask parentTask;
		private readonly string relativeName;

		private bool isFinished = false;
		private readonly Queue<TaskCompletionSource<bool>> waitTasks = new Queue<TaskCompletionSource<bool>>();

		protected SyncTask(SyncTask parentTask, string relativeName)
		{
			this.parentTask = parentTask;
			this.relativeName = relativeName ?? throw new ArgumentNullException(nameof(relativeName));
		} // ctor

		public override bool Equals(object obj)
			=> obj is SyncTask t && CompareTo(t) == 0;

		public override int GetHashCode()
			=> relativeName.GetHashCode();

		public int CompareTo(SyncTask other)
		{
			if (String.Compare(relativeName, other.relativeName, StringComparison.OrdinalIgnoreCase) == 0)
				return 0;
			else
			{
				var lengtCompare = Length - other.Length;
				if (lengtCompare == 0)
					return Stamp.Ticks - other.Stamp.Ticks <= 0 ? -1 : 1;
				else
					return lengtCompare < 0 ? -1 : 1;
			}
		} // proc CompareTo

		protected static void CopyAttributes(DateTime sourceCreationTimeUtc, DateTime sourceLastWriteTimeUtc, FileAttributes sourceAttributes, FileSystemInfo destination)
		{
			destination.CreationTimeUtc = sourceCreationTimeUtc;
			destination.LastWriteTimeUtc = sourceLastWriteTimeUtc;

			destination.Attributes = (destination.Attributes & ~SyncItems.CheckAttributeSet) | sourceAttributes & SyncItems.CheckAttributeSet;
		} // proc CopyAttributes

		protected static void CopyAttributes(FileSystemInfo source, FileSystemInfo destination)
			=> CopyAttributes(source.CreationTimeUtc, source.LastWriteTimeUtc, source.Attributes, destination);

		protected abstract Task ExecuteAsync();

		private Task WaitAsync()
		{
			lock (waitTasks)
			{
				if (isFinished)
					return Task.CompletedTask;

				var tcs = new TaskCompletionSource<bool>();
				waitTasks.Enqueue(tcs);
				return tcs.Task;
			}
		} // func WaitAsync

		public async Task ExecuteAsync(SyncItems syncService)
		{
			if (parentTask != null)
				await parentTask.WaitAsync();

			await ExecuteAsync();

			// mark task finished
			lock (waitTasks)
			{
				isFinished = true;
				while (waitTasks.Count > 0)
					waitTasks.Dequeue().TrySetResult(true);
				syncService.NotifyTaskExecuted(this);
			}
		} // proc ExecuteAsync

		public SyncTask Parent => parentTask;

		protected abstract string Action { get; }
		public string Name => String.Format(Action, relativeName);
		public abstract long Length { get; }
		public abstract DateTime Stamp { get; }
		public virtual int CacheSize => 0;

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

		public static Task<SyncTask> CopyItemAsync(SyncTask parentTask, string relativeName, FileInfo fiSource, FileInfo fiTarget, int cacheBorder)
		{
			if (fiSource.Length < cacheBorder)
				return Task.Run<SyncTask>(() => new WriteFileTask(parentTask, relativeName, fiSource, fiTarget));
			else
				return Task.FromResult<SyncTask>(new CopyFileTask(parentTask, relativeName, fiSource, fiTarget));
		} // func CopyItemAsync
	} // class SyncIask

	#endregion
}
