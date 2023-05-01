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
using Neo.PerfectWorking.Stuff;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.PerfectWorking.Backup
{
	#region -- class SyncItems --------------------------------------------------------

	public sealed class SyncItems
	{
		#region -- enum DirtyProperty -------------------------------------------------

		[Flags]
		private enum DirtyProperty
		{
			None = 0,

			CacheSize = 1,
			TotalLength = 2,
			TaskCount = 4,

			TotalScannedFilesAndBytes = 8,
			WrittenBytes = 16,
			ExecutedTasks = 32
		} // enum DirtyProperty

		#endregion

		private readonly int maxCacheSize = 1 << 20;
		private readonly int cacheBorder = 2048;

		private readonly Queue<TaskCompletionSource<bool>> openTasksChangedWaiter = new Queue<TaskCompletionSource<bool>>();
		private readonly List<SyncTask> openTasks = new List<SyncTask>();

		private int currentCacheSize = 0;
		private long currentTotalLength = 0L;
		private int currentTaskCount = 0;

		private int totalScannedFiles = 0;
		private long totalWriteBytes = 0L;
		private long totalEqualBytes = 0L;

		private int totalExecutedTasks = 0;
		private long writtenBytes = 0L;

		private void EnqueuePropertyChanged(DirtyProperty properties)
		{
		} // proc EnqueuePropertyChanged

		public void NotifyBytesWritten(SyncTask task, long bytesWritten)
		{
			writtenBytes += bytesWritten;
			NotifyTaskChange(DirtyProperty.WrittenBytes);
		} // proc NotifyBytesWritten

		public void NotifyTaskExecuted(SyncTask task)
		{
			totalExecutedTasks++;
			NotifyTaskChange(DirtyProperty.ExecutedTasks);
		} // proc NotifyTaskExecuted

		#region -- Task Queue Management ----------------------------------------------

		private Task WaitTaskChangeAsync(CancellationToken cancellationToken)
		{
			// test for cancellation
			cancellationToken.ThrowIfCancellationRequested();

			// wait for task cache
			var wait = new TaskCompletionSource<bool>();
			lock (openTasksChangedWaiter)
				openTasksChangedWaiter.Enqueue(wait);
			return wait.Task;
		} // proc WaitTaskChangeAsync

		private void NotifyTaskChange(DirtyProperty properties)
		{
			// finish awaiter
			lock (openTasksChangedWaiter)
			{
				while (openTasksChangedWaiter.Count > 0)
					openTasksChangedWaiter.Dequeue().TrySetResult(true);
			}

			EnqueuePropertyChanged(properties);
		} // proc NotifyTaskChange

		private async Task<SyncTask> EnqueueTaskAsync(SyncTask task, CancellationToken cancellationToken)
		{
			// test if cache is full
			while (currentCacheSize + task.CacheSize > maxCacheSize)
				await WaitTaskChangeAsync(cancellationToken);

			// append task
			lock (openTasks)
			{
				var dirtyProperties = DirtyProperty.TaskCount;

				// update statistik
				if (task.CacheSize > 0)
				{
					currentCacheSize += task.CacheSize;
					dirtyProperties |= DirtyProperty.CacheSize;
				}
				if (task.Length > 0)
				{
					currentTotalLength += task.Length;
					dirtyProperties |= DirtyProperty.TotalLength;
				}

				// add task
				currentTaskCount++;
				var idx = openTasks.BinarySearch(task);
				if (idx > 0)
					throw new InvalidOperationException();
				openTasks.Insert(~idx, task);

				NotifyTaskChange(dirtyProperties);
			}

			return task;
		} // proc EnqueueTaskAsync

		private async Task<SyncTask> DequeueTaskAsync(int relativeIndex, CancellationToken cancellationToken)
		{
			int DequeueTaskUnSafe()
			{
				if (relativeIndex < 0)
				{
					var index = openTasks.Count + relativeIndex;
					return index >= 0 ? index : -1;
				}
				else
					return relativeIndex < openTasks.Count ? relativeIndex : -1;
			} // func DequeueTaskUnSafe

			while (true)
			{
				lock (openTasks)
				{
					var taskIndex = DequeueTaskUnSafe();

					if (taskIndex >= 0)
					{
						var dirtyProperties = DirtyProperty.TaskCount;
						var task = openTasks[taskIndex];

						// check if parent task is finished
						var parentTaskIndex = openTasks.BinarySearch(task.Parent);
						if (parentTaskIndex >= 0)
						{
							task = task.Parent; // first task to do before this task can be done
							taskIndex = parentTaskIndex;
						}

						// update statistik
						if (task.CacheSize > 0)
						{
							currentCacheSize -= task.CacheSize;
							dirtyProperties |= DirtyProperty.CacheSize;
						}
						if (task.Length > 0)
						{
							currentTotalLength -= task.Length;
							dirtyProperties |= DirtyProperty.TotalLength;
						}
						currentTaskCount--;

						// remove task
						openTasks.RemoveAt(taskIndex);

						NotifyTaskChange(dirtyProperties);
					}
				}

				// wait for list change
				await WaitTaskChangeAsync(cancellationToken);
			}
		} // proc DequeueTaskAsync

		#endregion

		#region -- AppendSyncDirectory ------------------------------------------------

		public const FileAttributes CheckAttributeSet = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReadOnly;

		private bool EqualFileTime(string info, DateTime sourceTime, DateTime targetTime, out string changeType)
		{
			var diff = (sourceTime - targetTime);
			if (diff.Ticks >= 10000000)
			{
				changeType = $"{info} diff {diff:G}";
				return false;
			}
			else
			{
				changeType = null;
				return true;
			}
		} // proc EqualFileTime

		private bool EqualAttributes(FileAttributes sourceAttributes, FileAttributes targetAttributes, out string changeType)
		{
			var sourceAttr = sourceAttributes & CheckAttributeSet;
			var targetAttr = targetAttributes & CheckAttributeSet;
			if (sourceAttr != targetAttr)
			{
				changeType = $"attributes '{sourceAttr}' != '{targetAttr}''";
				return false;
			}
			else
			{
				changeType = null;
				return true;
			}
		} // func EqualAttributes

		private bool EqualFileSystemInfo(FileSystemInfo fsiSource, FileSystemInfo fsiTarget, out string changeType)
		{
			if (fsiTarget == null || !fsiTarget.Exists)
			{
				changeType = "missing";
				return false;
			}
			else if (!EqualFileTime("creation", fsiSource.CreationTimeUtc, fsiTarget.CreationTimeUtc, out changeType))
				return false;
			else if (!EqualFileTime("lastwrite", fsiSource.LastWriteTimeUtc, fsiTarget.LastWriteTimeUtc, out changeType))
				return false;
			else if (!EqualAttributes(fsiSource.Attributes, fsiTarget.Attributes, out changeType))
				return false;
			else
				return true;
		} // proc EqualFileSystemInfo

		private bool EqualFileInfo(FileInfo fiSource, FileInfo fiTarget, out string changeType)
		{
			if (!EqualFileSystemInfo(fiSource, fiTarget, out changeType))
				return false;
			else if (fiSource.Length != fiTarget.Length)
			{
				changeType = $"Source != Target ({fiSource.Length:N0} != {fiTarget.Length:N0})";
				return false;
			}
			else
				return true;
		} // proc EqualFileInfo

		private Task WriteLogEntyAsync(SyncLogWriter logFile, string action, string relativePath, string changeType)
		{
			if (String.IsNullOrEmpty(changeType))
				throw new ArgumentNullException(nameof(changeType), "ChangeType not set.");

			return logFile.WriteEntryAsync(action, relativePath, changeType);
		} // proc WriteLogEntry


		private void EnqueueStatisticChange(FileSystemItem fsiEqual, FileSystemItem fsiCopy)
		{
			totalScannedFiles++;

			if (fsiEqual != null && fsiEqual.Info is FileInfo fi)
				totalEqualBytes += fi.Length;
			if (fsiCopy != null && fsiCopy.Info is FileInfo fi2)
				totalWriteBytes += fi2.Length;

			EnqueuePropertyChanged(DirtyProperty.TotalScannedFilesAndBytes);
		} // proc EnqueueStatisticChange

		private async Task EnqueueNothingAsync(SyncLogWriter logFile, FileSystemItem fsiSource, string changeType, CancellationToken cancellationToken)
		{
			if (String.IsNullOrEmpty(changeType))
			{
				await logFile.WriteEntryAsync("Nothing", fsiSource.RelativePath);
				EnqueueStatisticChange(fsiSource, null);
			}
			else
				throw new ArgumentException("ChangeType is not empty.", nameof(changeType));
		} // proc EnqueueNothingAsync

		private async Task<SyncTask> EnqueueEnsureDirectoryAsync(SyncLogWriter logFile, SyncTask parentTask, FileSystemItem fsiSource, FileSystemItem fsiTarget, string changeType, CancellationToken cancellationToken)
		{
			await WriteLogEntyAsync(logFile, "Ensure", fsiSource.RelativePath, changeType);
			EnqueueStatisticChange(null, null);
			return await EnqueueTaskAsync(SyncTask.EnsureAttributes(parentTask, fsiSource.RelativePath, fsiSource.Info, fsiTarget.Info), cancellationToken);
		} // func EnqueueEnsureDirectoryAsync

		private async Task<SyncTask> EnqueueCopyItemTaskAsync(SyncLogWriter logFile, SyncTask parentTask, FileSystemItem fsiSource, FileInfo fiTarget, string changeType, CancellationToken cancellationToken)
		{
			await WriteLogEntyAsync(logFile, "Copy", fsiSource.RelativePath, changeType);
			EnqueueStatisticChange(null, fsiSource);
			return await EnqueueTaskAsync(await SyncTask.CopyItemAsync(parentTask, fsiSource.RelativePath, (FileInfo)fsiSource.Info, fiTarget, cacheBorder), cancellationToken);
		} // func EnqueueCopyItemTaskAsync

		private async Task<SyncTask> EnqueueRemoveItemAsync(SyncLogWriter logFile, SyncTask parentTask, string relativeParentPath, FileSystemInfo removeInfo, CancellationToken cancellationToken)
		{
			await logFile.WriteEntryAsync("Remove", Path.Combine(relativeParentPath, removeInfo.Name));
			return await EnqueueTaskAsync(SyncTask.RemoveItem(parentTask, relativeParentPath, removeInfo), cancellationToken);
		} // func EnqueueRemoveItemAsync

		public async Task AppendSyncDirectoryAsync(SyncLogWriter logFile, string source, string target, string[] excludes, CancellationToken cancellationToken)
		{
			var warnings = logFile == null ? null : new Action<string>(m => logFile.WriteLineAsync(m).Wait());

			// build filter
			var excludeFilter = FileSystemItem.CreateExcludeFilter(excludes);

			// create root
			var recursiveDirectory = new Stack<Tuple<FileSystemItem, SyncTask>>();
			recursiveDirectory.Push(new Tuple<FileSystemItem, SyncTask>(new FileSystemItem(String.Empty, new DirectoryInfo(source)), null));

			// enumerate all directories recursive
			while (recursiveDirectory.Count > 0)
			{
				var (cur, parentTask) = recursiveDirectory.Pop();

				// analyse source and target directory
				var targetDirectory = new DirectoryInfo(Path.Combine(target, cur.RelativePath));
				var sourceItems = await Task.Run(() => cur.Enumerate(cancellationToken, excludeFilter: excludeFilter, warnings: warnings).ToArray(), cancellationToken);
				var targetItems = await Task.Run(() => FileSystemItem.EnumerateFileSystemInfo(targetDirectory).ToArray(), cancellationToken);

				// compare directories
				for (var i = 0; i < sourceItems.Length; i++)
				{
					var fsiSource = sourceItems[i];

					// compare the file items
					var targetIndex = Array.FindIndex(targetItems, c => c != null && String.Compare(c.Name, fsiSource.Name, StringComparison.OrdinalIgnoreCase) == 0);

					if (fsiSource.IsDirectory)
					{
						var fsiTarget = new FileSystemItem(fsiSource.RelativePath, targetIndex == -1 ? new DirectoryInfo(Path.Combine(targetDirectory.FullName, fsiSource.Name)) : targetItems[targetIndex]);
						// enforce directory creation
						if (!EqualFileSystemInfo(fsiSource.Info, fsiTarget.Info, out var changeType))
						{
							var newTask = await EnqueueEnsureDirectoryAsync(logFile, parentTask, fsiSource, fsiTarget, changeType, cancellationToken);
							recursiveDirectory.Push(new Tuple<FileSystemItem, SyncTask>(fsiSource, newTask));
						}
						else
						{
							await EnqueueNothingAsync(logFile, fsiSource, changeType, cancellationToken);
							recursiveDirectory.Push(new Tuple<FileSystemItem, SyncTask>(fsiSource, null));
						}
					}
					else if (fsiSource.IsFile)
					{
						if (targetIndex < 0) // target does not exist, copy to create
						{
							await EnqueueCopyItemTaskAsync(logFile, parentTask, fsiSource, new FileInfo(Path.Combine(targetDirectory.FullName, fsiSource.Name)), "missing", cancellationToken);
						}
						else if (targetItems[targetIndex] is DirectoryInfo di)
						{
							var removeDirectoryTask = await EnqueueRemoveItemAsync(logFile, parentTask, cur.RelativePath, di, cancellationToken);
							await EnqueueCopyItemTaskAsync(logFile, removeDirectoryTask, fsiSource, new FileInfo(Path.Combine(targetDirectory.FullName, fsiSource.Name)), "directory", cancellationToken);
						}
						else if (targetItems[targetIndex] is FileInfo fiTarget)
						{
							var fiSource = (FileInfo)fsiSource.Info;

							if (!EqualFileInfo(fiSource, fiTarget, out var changeType))
								await EnqueueCopyItemTaskAsync(logFile, parentTask, fsiSource, fiTarget, changeType, cancellationToken);
							else
								await EnqueueNothingAsync(logFile, fsiSource, changeType, cancellationToken);
						}
						else
							throw new InvalidCastException($"Only {nameof(DirectoryInfo)} or {nameof(FileInfo)} is allowed (target).");
					}
					else
						throw new InvalidCastException($"Only {nameof(DirectoryInfo)} or {nameof(FileInfo)} is allowed (source).");

					// mark as found and processed
					if (targetIndex != -1)
						targetItems[targetIndex] = null;

					if (cancellationToken.IsCancellationRequested)
						return;
				}

				// remove items
				for (var j = 0; j < targetItems.Length; j++)
				{
					var fsi = targetItems[j];
					if (fsi != null)
						await EnqueueRemoveItemAsync(logFile, parentTask, cur.RelativePath, fsi, cancellationToken);
				}
			}
		} // proc AppendSyncDirectoryAsync

		#endregion

		//public Task AppendCleanDirectoryAsync(string directory, TimeSpan age, string[] excludes)
		//{
		//	foreach (var fsi in FileSystemItem.Enumerate(directory, excludes, filesOnly: false, recursive: true))
		//	{

		//	}
		//} // proc AppendCleanDirectory

		/// <summary>Total bytes to copy.</summary>
		public long TotalLength => currentTotalLength;
		/// <summary>Currently cached bytes to write.</summary>
		public int CacheSize => currentCacheSize;
		/// <summary>Number of active tasks</summary>
		public int TaskCount => currentTaskCount;

		/// <summary>Total scanned files.</summary>
		public int TotalScannedFiles => totalScannedFiles;

		/// <summary>Total executed tasks.</summary>
		public int TotalExecutedTasks => totalExecutedTasks;
		/// <summary>Bytes to copy to the target.</summary>
		public long TotalWriteBytes => totalWriteBytes;
		/// <summary>Bytes they not need to copy.</summary>
		public long TotalEqualBytes => totalEqualBytes;
		/// <summary>Total written bytes.</summary>
		public long WrittenBytes => writtenBytes;
	} // class SyncItems

	#endregion
}
