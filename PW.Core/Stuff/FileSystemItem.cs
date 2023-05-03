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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using TecWare.DE.Stuff;

namespace Neo.PerfectWorking.Stuff
{
	/// <summary>File item that was found.</summary>
	public sealed class FileSystemItem
	{
		private readonly string relativePath;
		private readonly FileSystemInfo fileSystemInfo;

		public FileSystemItem(string relativePath, FileSystemInfo fileSystemInfo)
		{
			this.relativePath = relativePath ?? throw new ArgumentNullException(nameof(relativePath));
			this.fileSystemInfo = fileSystemInfo ?? throw new ArgumentNullException(nameof(fileSystemInfo));
		} // ctor

		public override string ToString()
			=> relativePath;

		public IEnumerable<FileSystemItem> Enumerate(Predicate<string> excludeFilter = null, Action<string> warnings = null)
			=> Enumerate(this, excludeFilter ?? StaticAlways, warnings, CancellationToken.None);

		public IEnumerable<FileSystemItem> Enumerate(CancellationToken cancellationToken, Predicate<string> excludeFilter = null, Action<string> warnings = null)
			=> Enumerate(this, excludeFilter ?? StaticAlways, warnings, cancellationToken);

		/// <summary>Name of the file.</summary>
		public string Name => fileSystemInfo.Name;
		/// <summary>Relative path</summary>
		public string RelativePath => relativePath;
		/// <summary>Full path information</summary>
		public FileSystemInfo Info => fileSystemInfo;
		/// <summary>Is this a file.</summary>
		public bool IsFile => fileSystemInfo is FileInfo;
		/// <summary>Is this a directory.</summary>
		public bool IsDirectory => fileSystemInfo is DirectoryInfo;

		#region -- Enumerate ----------------------------------------------------------

		#region -- class EnumHelper ---------------------------------------------------

		private sealed class EnumHelper : IEnumerable<FileSystemItem>
		{
			private readonly Action<string> warnings;
			private readonly Stack<FileSystemItem> recursiveDirectories = new Stack<FileSystemItem>();
			private readonly Predicate<string> excludeFilter;
			private readonly bool filesOnly;
			private readonly bool recursive;

			public EnumHelper(Action<string> warnings, DirectoryInfo baseDirectory, Predicate<string> excludeFilter, bool filesOnly, bool recursive)
			{
				this.warnings = warnings;
				recursiveDirectories.Push(new FileSystemItem(String.Empty, baseDirectory));
				this.excludeFilter = excludeFilter ?? throw new ArgumentNullException(nameof(excludeFilter));
				this.filesOnly = filesOnly;
				this.recursive = recursive;
			} // ctor

			public IEnumerator<FileSystemItem> GetEnumerator()
			{
				var isFirstPop = true;
				while (recursiveDirectories.Count > 0)
				{
					var cur = recursiveDirectories.Pop();

					if (isFirstPop)
						isFirstPop = false;
					else if (!filesOnly)
						yield return cur;

					// enumerate file system information
					foreach (var fi in Enumerate(cur, excludeFilter, warnings, CancellationToken.None))
					{
						if (fi.IsFile)
							yield return fi;
						else if (recursive) // push for later use, and return
							recursiveDirectories.Push(fi);
						else if (!filesOnly) // returns also directory
							yield return fi;
					}
				}
			} // func GetEnumerator

			IEnumerator IEnumerable.GetEnumerator()
				=> GetEnumerator();
		} // class EnumHelper

		#endregion

		private static bool IsExcludedFromList(IReadOnlyCollection<Predicate<string>> excludeFilter, string expression)
		{
			foreach (var c in excludeFilter)
				if (c(expression))
					return true;
			return false;
		} // func IsExcludedFromList

		public static Predicate<string> CreateExcludeFilter(string[] excludes)
		{
			if (excludes == null || excludes.Length == 0)
				return Procs.GetFilterFunction(null, false);

			var excludeFilters = excludes.Select(f => Procs.GetFilterFunction(f)).Where(c => c != null).ToArray();
			return new Predicate<string>(arg => IsExcludedFromList(excludeFilters, arg));
		} // func CreateExcludeFilter

		private static IEnumerable<FileSystemInfo> EnumerateFileSystemInfo(DirectoryInfo currentDirectory, string relativePath, Action<string> warnings)
		{
			string GetPathInfo()
				=> relativePath ?? currentDirectory.FullName;

			if (!currentDirectory.Exists)
				return Array.Empty<FileSystemInfo>();
			if ((currentDirectory.Attributes & FileAttributes.ReparsePoint) != 0)
			{
				if (warnings != null)
				{
					warnings($"ReparsePoint: {GetPathInfo()}");
					return null;
				}
				else
					throw new IOException("ReparsePoint not allowed.");
			}

			var retry = 0;
			retry:
			try
			{
				return currentDirectory.EnumerateFileSystemInfos();
			}
			catch (IOException e)
			{
				if (retry++ < 10)
				{
					warnings?.Invoke($"{e.Message}: {GetPathInfo()}");
					Thread.Sleep(retry * 100);
					goto retry;
				}
				else
					throw; // unknown exception
			}
			catch (UnauthorizedAccessException)
			{
				if (warnings != null)
				{
					warnings($"Zugriff verweigert: {GetPathInfo()}");
					return null;
				}
				else
					throw;
			}
		} // func EnumerateFileSystemInfo

		private static IEnumerable<FileSystemItem> Enumerate(FileSystemItem directoryItem, Predicate<string> excludeFilter, Action<string> warnings, CancellationToken cancellationToken)
		{
			var eFiles = EnumerateFileSystemInfo((DirectoryInfo)directoryItem.Info, directoryItem.RelativePath, warnings);
			if (eFiles != null)
			{
				foreach (var fsi in eFiles)
				{
					var relativePath = Path.Combine(directoryItem.RelativePath, fsi.Name);
					if ((fsi.Attributes & FileAttributes.ReparsePoint) != 0)
					{
						warnings?.Invoke($"ReparsePoint: {relativePath}");
						continue;
					}

					// is the item filtered
					if (excludeFilter(fsi is DirectoryInfo ? relativePath + Path.DirectorySeparatorChar : relativePath))
					{
						warnings?.Invoke($"Gefiltert: {relativePath}");
						continue;
					}

					// return file information
					yield return new FileSystemItem(relativePath, fsi);

					if (cancellationToken.IsCancellationRequested)
						yield break;
				}
			}
		} // func Enumerate

		public static IEnumerable<FileSystemInfo> EnumerateFileSystemInfo(DirectoryInfo directoryInfo, bool throwException = true)
			=> EnumerateFileSystemInfo(directoryInfo, null, throwException ? null : EmptyLog);

		public static IEnumerable<FileSystemItem> Enumerate(string directory, string[] excludes, Action<string> warnings = null, bool filesOnly = true, bool recursive = true)
			=> new EnumHelper(warnings, new DirectoryInfo(directory), CreateExcludeFilter(excludes), filesOnly, recursive);

		public static IEnumerable<FileSystemItem> Enumerate(DirectoryInfo directoryInfo, Predicate<string> excludeFilter = null, Action<string> warnings = null)
			=> Enumerate(new FileSystemItem(String.Empty, directoryInfo), excludeFilter ?? StaticAlways, warnings, CancellationToken.None);

		#endregion

		private static Predicate<string> StaticAlways { get; } = Procs.GetFilterFunction(null, false);
		public static Action<string> EmptyLog { get; } = new Action<string>(c => { });
	} // class FileSystemItem
}
