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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Neo.PerfectWorking.Data
{
	public abstract class SimpleCommandBase : ICommand
	{
		public event EventHandler CanExecuteChanged;

		public void Refresh()
			=> CanExecuteChanged?.Invoke(this, EventArgs.Empty);

		public virtual bool CanExecute(object parameter)
			=> true;

		public abstract void Execute(object parameter);

		bool ICommand.CanExecute(object parameter)
			=> CanExecute(parameter);

		void ICommand.Execute(object parameter)
			=> Execute(parameter);
	} // class SimpleCommandBase

	public sealed class SimpleCommand : SimpleCommandBase
	{
		private readonly Action<object> execute;
		private readonly Func<object, bool> canExecute;

		public SimpleCommand(Action<object> execute, Func<object, bool> canExecute = null)
		{
			this.execute = execute;
			this.canExecute = canExecute;
		} // ctor

		public override void Execute(object parameter)
			=> execute?.Invoke(parameter);

		public override bool CanExecute(object parameter)
			=> canExecute?.Invoke(parameter) ?? true;
	} // class SimpleCommand
}
