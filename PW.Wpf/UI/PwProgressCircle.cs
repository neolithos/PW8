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
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace Neo.PerfectWorking.UI
{
	public class PwProgressCircle : RangeBase
	{
		public static readonly DependencyProperty IsIndeterminateProperty = ProgressBar.IsIndeterminateProperty.AddOwner(typeof(PwProgressCircle),
			new FrameworkPropertyMetadata(false, IsIndeterminateChanged)
		);

		private readonly static DependencyPropertyKey progressTextKey = DependencyProperty.RegisterReadOnly(nameof(ProgressText), typeof(string), typeof(PwProgressCircle), new PropertyMetadata());
		public readonly static DependencyProperty ProgressTextProperty = progressTextKey.DependencyProperty;

		public readonly static DependencyProperty NumberOfPointsProperty = DependencyProperty.Register(nameof(NumberOfPoints), typeof(int), typeof(PwProgressCircle),
			new FrameworkPropertyMetadata(30, NumberOfPointsPropertyChanged)
		);
		public readonly static DependencyProperty TailPointsProperty = DependencyProperty.Register(nameof(TailPoints), typeof(int), typeof(PwProgressCircle),
			new FrameworkPropertyMetadata(20, TailPointsPropertyChanged)
		);
		public readonly static DependencyProperty TailColorProperty = DependencyProperty.Register(nameof(TailColor), typeof(Color), typeof(PwProgressCircle),
			new FrameworkPropertyMetadata(SystemColors.HighlightColor, TailColorPropertyChanged)
		);
		public readonly static DependencyProperty TailSpeedProperty = DependencyProperty.Register(nameof(TailSpeed), typeof(TimeSpan), typeof(PwProgressCircle),
			new FrameworkPropertyMetadata(TimeSpan.FromMilliseconds(50), TailSpeedPropertyChanged)
		);
		public readonly static DependencyProperty ProgressTextVisibleProperty = DependencyProperty.Register(nameof(ProgressTextVisible), typeof(Visibility), typeof(PwProgressCircle),
			new FrameworkPropertyMetadata(Visibility.Visible)
		);

		private readonly DispatcherTimer timer;

		// -- tail --
		private Brush[] currentTailColors = null;
		private int currentRadiusPoint = 0;
		private int currentNumberOfPoints = 0;
		// -- arc --
		private StreamGeometry arcGeometry = null;
		private Pen arcPen = null;

		public PwProgressCircle()
		{
			timer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher)
			{
				Interval = TimeSpan.FromMilliseconds(50),
				IsEnabled = true
			};
			timer.Tick += (sender, e) =>
			{
				currentRadiusPoint++;
				if (currentRadiusPoint > currentNumberOfPoints)
					currentRadiusPoint = 0;

				InvalidateVisual();
			};

			PrepareArcPen();
		} // ctor

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static Point GetCirclePoint(double w, double h, double rx, double ry, double r)
			=> new Point(
				Math.Cos(2 * Math.PI * r) * (w / 2 - rx) + w / 2,
				Math.Sin(2 * Math.PI * r) * (h / 2 - ry) + h / 2
			);

		protected override void OnRender(DrawingContext dc)
		{
			if (currentTailColors != null)
			{
				var w = ActualWidth;
				var h = ActualHeight;
				var rx = w / currentNumberOfPoints;
				var ry = h / currentNumberOfPoints;

				for (var i = 0; i < currentTailColors.Length; i++)
					dc.DrawEllipse(currentTailColors[i], null, GetCirclePoint(w, h, rx, ry, (double)(i + currentRadiusPoint) / currentNumberOfPoints), rx, ry);
			}

			if (arcGeometry != null && arcPen != null)
				dc.DrawGeometry(null, arcPen, arcGeometry);
		} // proc OnReader

		private void PrepareArcPen()
		{
			var color = TailColor;
			arcPen = new Pen(new SolidColorBrush(Color.FromArgb(192, color.R, color.G, color.B)), (ActualWidth / NumberOfPoints) * 2);
			arcPen.Freeze();
		} // proc PrepareArcPen

		private void PrepareIntermediateCircle()
		{
			timer.IsEnabled = false;

			if (IsIndeterminate)
			{
				var color = TailColor;
				currentTailColors = new Brush[TailPoints];
				for (var i = 0; i < currentTailColors.Length; i++)
				{
					currentTailColors[i] = new SolidColorBrush(
						Color.FromArgb((byte)(255 * (i + 1) / currentTailColors.Length), color.R, color.G, color.B)
					);
					currentTailColors[i].Freeze();
				}

				currentRadiusPoint = 0;
				currentNumberOfPoints = NumberOfPoints;
				if (currentNumberOfPoints <= 0)
					currentNumberOfPoints = 1;

				timer.Interval = TailSpeed;
				timer.IsEnabled = true;
			}
			else
			{
				currentTailColors = null;
				InvalidateVisual();
			}
		} // proc PrepareIntermediateCircle

		private void UpdateProgressCircle()
		{
			if (Value > Minimum)
			{
				var w = ActualWidth;
				var h = ActualHeight;
				var c = currentNumberOfPoints > 0 ? currentNumberOfPoints : Convert.ToInt32((w + h) / 2);
				var rx = w / c;
				var ry = h / c;

				var arcGeo = new StreamGeometry();
				using (var ctx = arcGeo.Open())
				{
					var r = Value == Maximum ? 0.9999999 : (Value - Minimum) / (Maximum - Minimum);
					var pt = GetCirclePoint(w, h, rx, ry, r - 0.25);

					ctx.BeginFigure(new Point(w / 2, ry), false, false);
					ctx.ArcTo(pt, new Size(w / 2 - rx, h / 2 - ry), (r - 0.25) * 180, r > 0.5, SweepDirection.Clockwise, true, true);

					SetValue(progressTextKey, String.Format("{0:N0}%", r * 100));
				}

				arcGeo.Freeze();
				arcGeometry = arcGeo;
			}
			else
				arcGeometry = null;

			InvalidateVisual();
		} // proc UpdateProgressCircle

		private static void NumberOfPointsPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PwProgressCircle)d).OnNumberOfPointsPropertyChanged((int)e.OldValue, (int)e.NewValue);

		private static void TailPointsPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PwProgressCircle)d).OnTailPointsPropertyChanged((int)e.OldValue, (int)e.NewValue);

		private static void TailColorPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PwProgressCircle)d).OnTailColorPropertyChanged((Color)e.OldValue, (Color)e.NewValue);

		private static void IsIndeterminateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PwProgressCircle)d).OnIsIndeterminateChanged((bool)e.NewValue, (bool)e.OldValue);

		private static void TailSpeedPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PwProgressCircle)d).OnTailSpeedPropertyChanged((TimeSpan)e.OldValue, (TimeSpan)e.NewValue);

		protected void OnIsIndeterminateChanged(bool newValue, bool oldValue)
			=> PrepareIntermediateCircle();

		protected virtual void OnNumberOfPointsPropertyChanged(int oldValue, int newValue)
			=> PrepareIntermediateCircle();

		protected virtual void OnTailPointsPropertyChanged(int oldValue, int newValue)
			=> PrepareIntermediateCircle();

		protected virtual void OnTailSpeedPropertyChanged(TimeSpan oldValue, TimeSpan newValue)
			=> PrepareIntermediateCircle();

		protected virtual void OnTailColorPropertyChanged(Color oldValue, Color newValue)
		{
			PrepareIntermediateCircle();
			PrepareArcPen();
		} // proc OnTailColorPropertyChanged

		protected override void OnMinimumChanged(double oldMinimum, double newMinimum)
		{
			base.OnMinimumChanged(oldMinimum, newMinimum);
			UpdateProgressCircle();
		} // proc OnMinimumChanged

		protected override void OnMaximumChanged(double oldMaximum, double newMaximum)
		{
			base.OnMaximumChanged(oldMaximum, newMaximum);
			UpdateProgressCircle();
		} // proc OnMaximumChanged

		protected override void OnValueChanged(double oldValue, double newValue)
		{
			base.OnValueChanged(oldValue, newValue);
			UpdateProgressCircle();
		} // proc OnValueChanged

		protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
		{
			base.OnRenderSizeChanged(sizeInfo);
			PrepareArcPen();
			PrepareIntermediateCircle();
			UpdateProgressCircle();
		} // proc OnRenderSizeChanged

		/// <summary>Text in the middle of the circle.</summary>
		public string ProgressText => (string)GetValue(ProgressTextProperty);
		public bool IsIndeterminate { get => (bool)GetValue(IsIndeterminateProperty); set => SetValue(IsIndeterminateProperty, value); }

		public int NumberOfPoints { get => (int)GetValue(NumberOfPointsProperty); set => SetValue(NumberOfPointsProperty, value); }
		public int TailPoints { get => (int)GetValue(TailPointsProperty); set => SetValue(TailPointsProperty, value); }
		public Color TailColor { get => (Color)GetValue(TailColorProperty); set => SetValue(TailColorProperty, value); }
		public TimeSpan TailSpeed { get => (TimeSpan)GetValue(TailSpeedProperty); set => SetValue(TailSpeedProperty, value); }

		public Visibility ProgressTextVisible { get => (Visibility)GetValue(ProgressTextVisibleProperty); set => SetValue(ProgressTextVisibleProperty, value); }

		static PwProgressCircle()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PwProgressCircle), new FrameworkPropertyMetadata(typeof(PwProgressCircle)));
			FocusableProperty.OverrideMetadata(typeof(PwProgressBar), new FrameworkPropertyMetadata(false));
		}
	} // class PwProgressCircle
}
