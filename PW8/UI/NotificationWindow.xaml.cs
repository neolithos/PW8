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
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Neo.PerfectWorking.UI
{
	public partial class NotificationWindow : Window
	{
		#region -- class TextImageContent ---------------------------------------------

		private sealed class TextImageContent
		{
			public TextImageContent(string text, object image)
			{
				Text = text;
				Image = image;
			} // ctor

			public string Text { get; }
			public object Image { get; }
		} // class TextImageContent

		#endregion

		private readonly Storyboard windowStoryboard;
		private readonly FrameworkElement textTemplate;

		public NotificationWindow()
		{
			InitializeComponent();

			windowStoryboard = (Storyboard)FindResource(nameof(windowStoryboard));
			textTemplate = (FrameworkElement)FindResource(nameof(textTemplate));
		} // ctor

		protected override void OnClosing(CancelEventArgs e)
		{
			base.OnClosing(e);
			e.Cancel = true;
		} // proc OnClosing

		protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);
			if (e.Property == ActualWidthProperty
				|| e.Property == ActualHeightProperty)
				RecalcPosition(true);
		} // proc OnPropertyChanged

		public void RecalcPosition(bool force = false)
		{
			if (force || IsVisible)
			{
				var rc = SystemParameters.WorkArea;

				Left = rc.Left + (rc.Width - ActualWidth) / 2;
				Top = rc.Top + (rc.Height - ActualHeight) * 4 / 5;
			}
		} // proc RecalcPosition

		private void SetContent(FrameworkElement control)
		{
			this.DataContext = control;
			RecalcPosition(true);
		} // proc SetContent

		private void SetTextContent(string text, object image)
		{
			textTemplate.DataContext = new TextImageContent(text, image);
			SetContent(textTemplate);
		} // proc SetTextContent

		private void SetImageContent(ImageSource source)
		{
			SetTextContent("not impl", null);
		} // proc SetImageContent

		private void SetUriContent(Uri uri)
		{
			SetTextContent("not impl", null);
		} // proc SetUriContent

		public void Show(object message, object image)
		{
			switch (message)
			{
				case string text:
					SetTextContent(text, image);
					break;
				case ImageSource img:
					SetImageContent(img);
					break;
				case Uri uri:
					SetUriContent(uri);
					break;
				case FrameworkElement control:
					SetContent(control);
					break;
				case null:
					break;
				default:
					SetTextContent(message.ToString(), null);
					return;
			}

			if (Visibility == Visibility.Visible) // reset storyboard
			{
				windowStoryboard.Seek(TimeSpan.FromMilliseconds(500), TimeSeekOrigin.BeginTime);
			}
			else // start storyboard
			{
				Opacity = 0.0;
				Show();
				windowStoryboard.Begin();
			}
		} // proc Show

		private void WindowStoryboard_Completed(object sender, EventArgs e)
			=> Hide();
	} // class NotificationWindow
}
