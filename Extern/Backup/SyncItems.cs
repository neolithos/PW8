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
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TecWare.DE.Stuff;

namespace Neo.PerfectWorking.Backup
{
	#region -- class SyncItems --------------------------------------------------------

	public sealed class SyncItems : INotifyPropertyChanged, ISyncSafeIO
	{
		#region -- enum DirtyProperty -------------------------------------------------

		private static readonly PropertyChangedEventArgs[] propertyEventArgs = new PropertyChangedEventArgs[]
		{
			new PropertyChangedEventArgs(nameof(CacheSize)),
			new PropertyChangedEventArgs(nameof(TotalLength)),
			new PropertyChangedEventArgs(nameof(TaskCount)),

			new PropertyChangedEventArgs(nameof(TotalScannedFiles)),
			new PropertyChangedEventArgs(nameof(TotalEqualBytes)),
			new PropertyChangedEventArgs(nameof(TotalWriteBytes)),
			new PropertyChangedEventArgs(nameof(WrittenBytes)),
			new PropertyChangedEventArgs(nameof(TotalExecutedTasks))
		};

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

		public event PropertyChangedEventHandler PropertyChanged;

		private const int maxCacheSize = 0x20000000;
		private const int cacheBorder = maxCacheSize / 16;

		private readonly ISyncTaskUI ui;
		private readonly SequenceTimer propertyChangedTimer;
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

		private DirtyProperty dirtyProperties = DirtyProperty.None;

		public SyncItems(ISyncTaskUI ui)
		{
			this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
			propertyChangedTimer = new SequenceTimer(FirePropertiesChanged);
		} // ctor

		private void FirePropertiesChanged()
		{
			var notifyProperties = new PropertyChangedEventArgs[propertyEventArgs.Length];

			lock (propertyChangedTimer)
			{
				if (dirtyProperties == DirtyProperty.None)
					return;

				if ((dirtyProperties & DirtyProperty.CacheSize) != 0)
					notifyProperties[0] = propertyEventArgs[0];
				if ((dirtyProperties & DirtyProperty.TotalLength) != 0)
					notifyProperties[1] = propertyEventArgs[1];
				if ((dirtyProperties & DirtyProperty.TaskCount) != 0)
					notifyProperties[2] = propertyEventArgs[2];

				if ((dirtyProperties & DirtyProperty.TotalScannedFilesAndBytes) != 0)
				{
					notifyProperties[3] = propertyEventArgs[3];
					notifyProperties[4] = propertyEventArgs[4];
					notifyProperties[5] = propertyEventArgs[5];
				}
				if ((dirtyProperties & DirtyProperty.WrittenBytes) != 0)
					notifyProperties[6] = propertyEventArgs[6];
				if ((dirtyProperties & DirtyProperty.ExecutedTasks) != 0)
					notifyProperties[7] = propertyEventArgs[7];

				dirtyProperties = DirtyProperty.None;
			}

			// invoke property changed
			if (ui.Context == null)
				FirePropertiesChanged(notifyProperties);
			else
				ui.Context.Post(FirePropertiesChanged, notifyProperties);
		} // proc FirePropertiesChanged

		private void FirePropertiesChanged(object state)
		{
			var notifyProperties = (PropertyChangedEventArgs[])state;
			var propertyChanged = PropertyChanged;
			if (propertyChanged != null)
			{
				for (var i = 0; i < notifyProperties.Length; i++)
				{
					if (notifyProperties[i] != null)
						propertyChanged(this, notifyProperties[i]);
				}
			}
		} // proc FirePropertiesChanged

		private void EnqueuePropertyChanged(DirtyProperty properties)
		{
			lock (propertyChangedTimer)
			{
				dirtyProperties |= properties;
				if (!propertyChangedTimer.IsEnabled)
					propertyChangedTimer.Start(500);
			}
		} // proc EnqueuePropertyChanged

		public void NotifyBytesWritten(SyncTask task, long bytesWritten)
		{
			writtenBytes += bytesWritten;
			NotifyTaskChange(DirtyProperty.WrittenBytes);
		} // proc NotifyBytesWritten

		public void NotifyTaskExecuted(SyncTask task)
		{
			writtenBytes += task.Length;
			totalExecutedTasks++;
			NotifyTaskChange(DirtyProperty.ExecutedTasks | DirtyProperty.WrittenBytes);
		} // proc NotifyTaskExecuted

		#region -- Task Queue Management ----------------------------------------------

		private Task WaitTaskChangeAsync(CancellationToken cancellationToken)
		{
			// test for cancellation
			cancellationToken.ThrowIfCancellationRequested();

			// wait for task cache
			var wait = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
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
				{
					var resultIndex = -1;

					while (relativeIndex < openTasks.Count)
					{
						resultIndex = relativeIndex;
						if (!openTasks[relativeIndex].HasChildren) // skip ensure items
							break;
						relativeIndex++;
					}

					return resultIndex;
				}
			} // func DequeueTaskUnSafe

			while (true)
			{
				SyncTask task = null;
				lock (openTasks)
				{
					var taskIndex = DequeueTaskUnSafe();

					if (taskIndex >= 0)
					{
						var dirtyProperties = DirtyProperty.TaskCount;
						task = openTasks[taskIndex];

						// check if parent task is finished
						while (task.Parent != null)
						{
							var parentTaskIndex = openTasks.BinarySearch(task.Parent);
							if (parentTaskIndex >= 0)
							{
								task = task.Parent; // first task to do before this task can be done
								taskIndex = parentTaskIndex;
							}
							else
								break;
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
						task.IsStarted = true;

						NotifyTaskChange(dirtyProperties);
					}
				}

				// return task
				if (task != null)
					return task;

				// wait for list change
				await WaitTaskChangeAsync(cancellationToken);
			}
		} // proc DequeueTaskAsync

		#endregion

		#region -- SafeIO -------------------------------------------------------------

		private (T, Exception) SafeIOcore<T>(Func<T> func)
		{
			const int max = 6;
			var tries = 0;
			while (true)
			{
				try
				{
					return (func(), null);
				}
				catch (UnauthorizedAccessException e)
				{
					if (tries++ >= max)
						return (default, e);
					Thread.Sleep(1000 * tries);
				}
				catch (IOException e)
				{
					if (tries++ >= max)
						return (default, e);
					Thread.Sleep(1000 * tries);
				}
			}
		} // proc SafeIO

		public void SafeIO(Action action, string actionDescription)
		{
			while (true)
			{
				var (_, e) = SafeIOcore(() => { action(); return true; });
				if (e == null)
					return;
				else if (!RetryAction(actionDescription, e))
					throw new Exception($"Action failed: {actionDescription}", e);
			}
		} // proc SafeIO

		public T SafeIO<T>(Func<T> func, string actionDescription)
		{
			while (true)
			{
				var (r, e) = SafeIOcore(func);
				if (e == null)
					return r;
				else if (!RetryAction(actionDescription, e))
					throw new Exception($"Action failed: {actionDescription}", e);
			}
		} // proc SafeIO

		public Stream SafeOpen(Func<FileInfo, Stream> func, FileInfo file, string actionDescription)
		{
			while (true)
			{
				var (s, e) = SafeIOcore(() => func(file));
				if (e == null)
					return s;
				else
				{
					throw new NotImplementedException();
				}
			}
		} // func SafeOpen

		private bool RetryAction(string actionDescription, Exception e)
		{
			if(actionDescription != null)
			throw new NotImplementedException();
			else
			return true;
		} // func RetryAction

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
			else if (!EqualFileTime("creation", fsiSource.CreationTimeUtc, fsiTarget.CreationTimeUtc, out changeType)) // on directories only the creation time is usefull, lastwritetime is when the last file was created
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
			else if (!EqualFileTime("lastwrite", fiSource.LastWriteTimeUtc, fiTarget.LastWriteTimeUtc, out changeType))
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
			return await EnqueueTaskAsync(await SyncTask.CopyItemAsync(this, parentTask, fsiSource.RelativePath, (FileInfo)fsiSource.Info, fiTarget, cacheBorder, UseSourceCache(fsiSource)), cancellationToken);
		} // func EnqueueCopyItemTaskAsync

		private bool UseSourceCache(FileSystemItem fsiSource)
			=> fsiSource.RelativePath.IndexOf("Mozilla", StringComparison.OrdinalIgnoreCase) >= 0;

		private async Task<SyncTask> EnqueueRemoveItemAsync(SyncLogWriter logFile, SyncTask parentTask, string relativeParentPath, FileSystemInfo removeInfo, CancellationToken cancellationToken)
		{
			await logFile.WriteEntryAsync("Remove", Path.Combine(relativeParentPath, removeInfo.Name));
			return await EnqueueTaskAsync(SyncTask.RemoveItem(parentTask, relativeParentPath, removeInfo), cancellationToken);
		} // func EnqueueRemoveItemAsync

		public async Task AppendSyncDirectoryAsync(SyncLogWriter logFile, IProgress<string> progress, string source, string target, string[] excludes, CancellationToken cancellationToken)
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
				progress?.Report(cur.RelativePath);
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

		public async Task ProcessTaskAsync(CancellationToken cancellationToken, bool smallPrefered = true)
		{
			var t = await DequeueTaskAsync(smallPrefered ? 0 : -1, cancellationToken);
			await t.ExecuteAsync(this);
		} // proc ProcessTaskAsync

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

		/// <summary>UI access</summary>
		public ISyncTaskUI UI => ui;
	} // class SyncItems

	#endregion
}
