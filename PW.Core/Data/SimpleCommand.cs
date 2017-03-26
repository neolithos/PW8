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
