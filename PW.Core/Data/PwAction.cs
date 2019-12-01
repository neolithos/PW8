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
using System.Threading.Tasks;
using System.Windows.Input;
using Neo.PerfectWorking.Stuff;
using TecWare.DE.Data;

namespace Neo.PerfectWorking.Data
{
	#region -- class PwAction ---------------------------------------------------------

	public sealed class PwAction : ObservableObject, IPwUIHotKey, ICommand
	{
		public event EventHandler CanExecuteChanged;

		private readonly IPwGlobal global;
		private readonly string originalLabel;
		private string currentLabel = null;
		private PwKey hotKey = PwKey.None;
		private object originalImage;
		private object currentImage = null;
		private readonly List<Func<PwAction, Task>> isRunning = new List<Func<PwAction, Task>>();

		private Func<PwAction, Task> currentExecute = null;
		private Func<PwAction, bool> canExecute = null;

		public PwAction(IServiceProvider sp, string title, string label, PwKey key, object image)
		{
			global = sp.GetService<IPwGlobal>(true);

			Title = title;
			hotKey = key;
			originalLabel = label;
			originalImage = global.ConvertImage(image);
		} // ctor

		/// <summary>Refresh CanExecute state</summary>
		public void Refresh()
			=> CanExecuteChanged?.Invoke(this, EventArgs.Empty);

		bool ICommand.CanExecute(object parameter)
			=> currentExecute != null && (canExecute?.Invoke(this) ?? true);

		void ICommand.Execute(object parameter)
		{
			var action = currentExecute;
			if (action != null)
				Task.Run(() => ExecuteBackground(action));
		} // proc ICommand.Execute

		private async Task ExecuteBackground(Func<PwAction, Task> action)
		{
			try
			{
				lock (isRunning)
				{
					if (isRunning.Contains(action))
						return;

					isRunning.Add(action);
					if (isRunning.Count == 1)
					{
						Label = "Is running...";
						OnPropertyChanged(nameof(IsProgressVisible));
						OnPropertyChanged(nameof(IsRunning));
					}
				}

				await action.Invoke(this);
			}
			catch (Exception e)
			{
				var ex = UnpackException(e);
				if (ex is TaskCanceledException)
					global.UI.ShowNotification($"{Title} wurde abgebrochen.");
				else
					await global.UI.ShowExceptionAsync($"Command '{Title}' failed.", e);
			}
			finally
			{
				lock (isRunning)
				{
					isRunning.Remove(action);
					if (isRunning.Count == 0)
					{
						Label = null;
						Image = null;
						UpdateProgress(-1, -1);
						OnPropertyChanged(nameof(IsProgressVisible));
						OnPropertyChanged(nameof(IsRunning));
					}
				}
			}
		} // proc ExecuteBackground

		private static Exception UnpackException(Exception e)
			=> e is AggregateException a ? UnpackException(a.InnerException) : e;

		public void UpdateProgress(long pos, long max)
		{
			void SetProgress(int value)
			{
				if (ProgressValue != value)
				{
					ProgressValue = value;
					OnPropertyChanged(nameof(ProgressValue));
				}
			} // proc SetProgress

			if (pos < 0 || max < 0)
				SetProgress(-1);
			else
			{
				var newProgress = unchecked((int)(pos * 100 / max));
				if (newProgress > 100)
					newProgress = 100;
				SetProgress(newProgress);
			}
		} // proc UpdateProgress

		/// <summary>Current image of the button.</summary>
		public object Image
		{
			get => originalImage ?? currentImage;
			set
			{
				if (currentImage != value)
				{
					currentImage = global.ConvertImage(value);
					OnPropertyChanged(nameof(Image));
				}
			}
		} // prop Image

		public object OriginalImage
		{
			get => originalImage;
			set
			{
				if (originalImage != value)
				{
					originalImage = global.ConvertImage(value);
					if (currentImage == null)
						OnPropertyChanged(nameof(Image));
				}
			}
		} // prop OriginalImage

		/// <summary>Title of the button</summary>
		public string Title { get; }
		/// <summary>Shortcut for the command.</summary>
		public PwKey Key { get => hotKey; set => Set(ref hotKey, value, nameof(Key)); }

		/// <summary>Label of the button</summary>
		public string Label
		{
			get => currentLabel ?? originalLabel;
			set => Set(ref currentLabel, value, nameof(Label));
		} // prop Label

		/// <summary>Is the progress bar active</summary>
		public bool IsProgressVisible => ProgressValue < 0 && IsRunning;
		/// <summary>The current progress value, Normalized to 100, &lt;0 hidden</summary>
		public int ProgressValue { get; private set; } = -1;

		/// <summary>Is the current command in action.</summary>
		public bool IsRunning { get { lock (isRunning) return isRunning.Count > 0; } }

		/// <summary>Function to execute</summary>
		public Func<PwAction, Task> Execute
		{
			get => currentExecute;
			set
			{
				currentExecute = value;
				Refresh();
			}
		} // prop Execute

		/// <summary>Can this function executed.</summary>
		public Func<PwAction, bool> CanExecute
		{
			get => canExecute;
			set
			{
				canExecute = value;
				Refresh();
			}
		} // prop Execute
	} // class PwAction

	#endregion
}
