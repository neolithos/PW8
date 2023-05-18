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
using Neo.PerfectWorking.Data;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using TecWare.DE.Data;

[assembly: PwPackage(typeof(Neo.PerfectWorking.Backup.BackupPackage))]
[assembly: InternalsVisibleTo("PW.Core.Tests")]

namespace Neo.PerfectWorking.Backup
{
	#region -- class BackupPackage ----------------------------------------------------

	public sealed class BackupPackage : PwPackageBase, IPwIdleAction
	{
		private readonly IPwCollection<BackupModel> backups;

		public BackupPackage(IPwGlobal global)
			: base(global, nameof(BackupPackage))
		{
			backups = global.RegisterCollection<BackupModel>(this);

			global.RegisterObject(this, "Log", Log.Default);
			global.RegisterObject(this, nameof(BackupWindowPane), new BackupWindowPane(this));
		} // ctor

		public BackupModel CreateTarget(string name, string target)
			=> new BackupModel(name, target);

		public bool OnIdle(int elapsed)
		{
			return false;
		}

		public IReadOnlyList<BackupModel> Backups => backups;
	} // class BackupPackage

	#endregion

	#region -- enum BackupState -------------------------------------------------------

	public enum BackupState
	{
		None,
		Missing,
		Ready,
		Running,
		Done
	} // enum BackupState

	#endregion

	#region -- class BackupSource -----------------------------------------------------

	internal sealed class BackupSource
	{
		private readonly string source;
		private readonly string target;
		private readonly Predicate<string>[] excludes;

	} // class BackupSource

	#endregion

	#region -- class BackupModel ------------------------------------------------------

	public sealed class BackupModel : ObservableObject, ICommand
	{
		public event EventHandler CanExecuteChanged;

		private readonly string name;
		private readonly string targetDirectory;
		private readonly BackupSource[] backupSource;

		private BackupState state = BackupState.None;
		// private readonly SyncItems sync;

		public BackupModel(string name, string targetDirectory)
		{
			this.name = name ?? throw new ArgumentNullException(nameof(name));
			this.targetDirectory = targetDirectory ?? throw new ArgumentNullException(nameof(targetDirectory));

			State = BackupState.Ready;
		} // ctor

		bool ICommand.CanExecute(object parameter)
		{
			return true;
		}

		void ICommand.Execute(object parameter)
		{
			if (state == BackupState.Done)
				State = BackupState.Ready;
			else
			{

			Log.Default.StartScan("src", "trc");
			State = BackupState.Running;
			Task.Delay(2000).ContinueWith(t =>
			{
				t.Wait();
				State = BackupState.Done;
			});

			}
		}

		public void AppendSource(string source, string target, object excludes)
		{

		} // proc AppendSource

		public string Name => name;

		public BackupState State { get => state; private set => Set(ref state, value, nameof(State)); }
	} // class BackupModel

	#endregion
}
