using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Neo.PerfectWorking.Win32;

namespace Neo.PerfectWorking.UI
{
	public partial class DashBoardWindow : Window
	{
		private Rect rcCurrentCursor;
		private readonly DispatcherTimer mousePositionTimer;

		private readonly Storyboard hideAnimation;
		private readonly Storyboard showAnimation;

		public DashBoardWindow()
		{
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
		} // ctor

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
			if (!IsVisible)
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
	} // class DashBoardWindow
}
