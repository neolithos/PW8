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
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Neo.PerfectWorking.Data;

namespace Neo.PerfectWorking.UI
{
	#region -- interface IPwProgress --------------------------------------------------

	public interface IPwProgress : IProgress<string>, IDisposable
	{
	} // interface IPwProgress

	#endregion

	#region -- class PwContentPane ----------------------------------------------------

	/// <summary>IPwWindowPane implementation for wpf, that also supports progress handling for async methods.</summary>
	public class PwContentPane : ContentControl, IPwWindowPane, INotifyPropertyChanged
	{
		#region -- class PwPaneProgress -----------------------------------------------

		private sealed class PwPaneProgress : IPwProgress, INotifyPropertyChanged
		{
			public event PropertyChangedEventHandler PropertyChanged;

			private readonly PwContentPane pane;
			private readonly string defaultLabel;
			private string currentLabel = null;

			public PwPaneProgress(PwContentPane pane, string defaultLabel)
			{
				this.pane = pane;
				this.defaultLabel = defaultLabel;
			} // ctor

			void IDisposable.Dispose()
				=> Dispose(true);

			private void Dispose(bool disposing)
				=> pane.RemoveProgress(this);
			
			private void OnPropertyChanged(string propertyName)
			{
				if (PropertyChanged != null)
					pane.Dispatcher.Invoke(new Action<string>(OnPropertyChangedUI), propertyName);
			} // proc OnPropertyChanged

			private void OnPropertyChangedUI(string propertyName)
				=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

			public void Report(string value)
			{
				if (String.IsNullOrEmpty(value))
					value = null;

				if(value != currentLabel)
				{
					currentLabel = value;
					OnPropertyChanged(nameof(Label));
				}
			} // proc Report

			public string Label => currentLabel ?? defaultLabel;
		} // class PwPaneProgress

		#endregion

		public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(PwContentPane));
		public static readonly DependencyProperty ImageProperty = DependencyProperty.Register(nameof(Image), typeof(ImageSource), typeof(PwContentPane));
		
		private static readonly DependencyPropertyKey isProgressActivePropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsProgressActive), typeof(bool), typeof(PwContentPane), new FrameworkPropertyMetadata(false));
		public static readonly DependencyProperty IsProgressActiveProperty = isProgressActivePropertyKey.DependencyProperty;

		private static readonly DependencyPropertyKey currentProgressPropertyKey = DependencyProperty.RegisterReadOnly(nameof(CurrentProgress), typeof(object), typeof(PwContentPane), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty CurrentProgressProperty = currentProgressPropertyKey.DependencyProperty;

		public event PropertyChangedEventHandler PropertyChanged;

		#region -- Progress Service ---------------------------------------------------

		private readonly List<PwPaneProgress> progresses = new List<PwPaneProgress>();

		public IPwProgress CreateProgress(string defaultLabel)
		{
			var progress = new PwPaneProgress(this, defaultLabel);
			progresses.Add(progress);
			UpdateProgress();
			return progress;
		} // func CreateProgress

		private void RemoveProgress(PwPaneProgress progress)
		{
			if (progresses.Remove(progress))
				UpdateProgress();
		} // proc RemoveProgress

		private void UpdateProgress()
		{
			if (progresses.Count > 0)
			{
				CurrentProgress = progresses[progresses.Count - 1];
				IsProgressActive = true;
			}
			else
			{
				CurrentProgress = null;
				IsProgressActive = false;
			}
		} // proc UpdateProgress

		#endregion

		protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);
			if (e.Property == TitleProperty
				|| e.Property == ImageProperty
				|| e.Property == IsEnabledProperty)
				OnNotifyPropertyChanged(e.Property.Name);
		} // proc OnPropertyChanged

		protected void OnNotifyPropertyChanged(string name)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

		public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
		public object Control => this;
		public object Image { get => GetValue(ImageProperty); set => SetValue(ImageProperty, value); }

		public bool IsProgressActive { get => (bool)GetValue(IsProgressActiveProperty); private set => SetValue(isProgressActivePropertyKey, value); }
		public object CurrentProgress { get => GetValue(CurrentProgressProperty); private set => SetValue(currentProgressPropertyKey, value); }

		static PwContentPane()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PwContentPane), new FrameworkPropertyMetadata(typeof(PwContentPane)));
			FocusableProperty.OverrideMetadata(typeof(PwContentPane), new FrameworkPropertyMetadata(false));
			KeyboardNavigation.IsTabStopProperty.OverrideMetadata(typeof(PwContentPane), new FrameworkPropertyMetadata(false));
		}
	} // class PwContentPane

	#endregion
}
