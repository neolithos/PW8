﻿#region -- copyright --
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
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Neo.PerfectWorking.Data;
using Neo.PerfectWorking.Win32;

namespace Neo.PerfectWorking.UI
{
	internal partial class DashBoardWindow : Window, IPwWidgetWindow
	{
		private readonly IPwGlobal global;

		private Rect rcCurrentCursor;
		private readonly DispatcherTimer mousePositionTimer;

		private readonly Storyboard hideAnimation;
		private readonly Storyboard showAnimation;

		private Color foregroundColor;
		private Color backgroundColor;
		private Color borderColor;

		public DashBoardWindow(PwGlobal global)
		{
			this.global = global ?? throw new ArgumentNullException(nameof(global));

			InitializeComponent();

			showAnimation = (Storyboard)FindResource(nameof(showAnimation));
			hideAnimation = (Storyboard)FindResource(nameof(hideAnimation));
			hideAnimation.Completed += (sender, e) => Hide();

			mousePositionTimer = new DispatcherTimer(DispatcherPriority.Normal)
			{
				IsEnabled = false,
				Interval = TimeSpan.FromMilliseconds(200)
			};
			mousePositionTimer.Tick += (sender, e) => CheckMousePosition();

			RecalcPosition();

			Visibility = Visibility.Hidden;
		} // ctor

		public object SetDashboard(UIElement dash)
		{
			this.dash.Child = dash ?? throw new ArgumentNullException(nameof(dash));
			return dash;
		} // proc SetDashboard

		public void RecalcPosition()
		{
			var rc = SystemParameters.WorkArea;

			Left = rc.Right - ActualWidth;
			Top = rc.Bottom - ActualHeight;
		} // proc RecalcPosition

		protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
			=> RecalcPosition();

		public void BeginShow(int x, int y)
		{
			if (Visibility != Visibility.Visible)
			{
				BeginStoryboard(showAnimation);
				Show();
			}

			// update drag size
			mousePositionTimer.IsEnabled = false;
			var dragSizeX = SystemParameters.MinimumHorizontalDragDistance;
			var dragSizeY = SystemParameters.MinimumVerticalDragDistance;
			rcCurrentCursor = new Rect(
				x - dragSizeX / 2,
				y - dragSizeY / 2,
				dragSizeX,
				dragSizeY
			);
			mousePositionTimer.IsEnabled = true;
		} // proc BeginShow

		public void BeginHide(bool noFade)
		{
			if (!IsVisible)
				return;

			rcCurrentCursor = Rect.Empty;
			mousePositionTimer.IsEnabled = false;
			if (noFade)
				Hide();
			else
				BeginStoryboard(hideAnimation);
		} // proc BeginHide

		private static Point GetCursorPosition()
		{
			var pt = new System.Drawing.Point();
			NativeMethods.GetCursorPos(ref pt);
			return new Point(pt.X, pt.Y);
		} // func GetCursorPosition

		private void CheckMousePosition()
		{
			if (IsVisible && !rcCurrentCursor.Contains(GetCursorPosition()))
				BeginHide(false);
		} // proc CheckMousePosition

		public Color BackgroundColor
		{
			get => backgroundColor;
			internal set
			{
				if (backgroundColor != value)
				{
					backgroundColor = value;
					Background = new SolidColorBrush(backgroundColor);
				}
			}
		} // prop BackgroundColor

		Brush IPwWidgetWindow.BackgroundBrush => Background;

		public Color ForegroundColor
		{
			get => foregroundColor;
			internal set
			{
				if (foregroundColor != value)
				{
					foregroundColor = value;
					Foreground = new SolidColorBrush(foregroundColor);
				}
			}
		} // prop ForegroundColor

		Brush IPwWidgetWindow.ForegroundBrush => Foreground;

		public Color BorderColor
		{
			get => borderColor;
			internal set
			{
				if (borderColor != value)
				{
					borderColor = value;
					BorderBrush = new SolidColorBrush(borderColor);
				}
			}
		} // prop BorderColor

		public IPwGlobal Global => global;
	} // class DashBoardWindow
}
