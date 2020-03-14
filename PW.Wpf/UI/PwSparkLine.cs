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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace Neo.PerfectWorking.UI
{
	#region -- interface IPwSparkLineSource ----------------------------------------------

	public interface IPwSparkLineSource
	{
		event EventHandler Changed;
		
		double GetPoint(int index, double scaleY);

		int Count { get; }
	} // interface IPwSparkLineSource

	#endregion

	#region -- class PwSparkLine ---------------------------------------------------------

	public class PwSparkLine : FrameworkElement
	{
		protected override void OnRender(DrawingContext dc)
		{
			var lineSource = LineSource;
			if (lineSource == null || lineSource.Count <= 0)
				return;

			var width = ActualWidth;
			var height = ActualHeight;

			// collect segments
			var segments = new List<LineSegment>();
			var firstPoint = new Point(0.0, 0.0);
			var count = lineSource.Count;
			for (var i = 0; i < count; i++)
			{
				var x = width * i / (count - 1);
				var y = height - lineSource.GetPoint(i, height);
				if (y < 0.0)
					y = 0.0;
				if (y > height)
					y = height;

				if (i == 0)
					firstPoint = new Point(x, y);
				else
					segments.Add(new LineSegment(new Point(x, y), true));	
			}

			// draw background
			if (Fill != null && !Fill.Equals(Brushes.Transparent))
			{
				var figure = new PathFigure
				{
					StartPoint = new Point(0, height),
					IsClosed = true
				};
				figure.Segments.Add(new LineSegment(firstPoint, true));
				foreach (var s in segments)
					figure.Segments.Add(s);
				figure.Segments.Add(new LineSegment(new Point(width, height), true));

				dc.DrawGeometry(Fill, null, new PathGeometry(new PathFigure[] { figure }));
			}
			
			// draw line
			if (Foreground != null)
			{
				dc.DrawGeometry(null, Foreground, new PathGeometry(new PathFigure[] { new PathFigure(firstPoint, segments, false) }));
			}
		} // proc OnRender

		#region -- LineSource - Property ----------------------------------------------------

		public static readonly DependencyProperty LineSourceProperty = DependencyProperty.Register(nameof(LineSource), typeof(IPwSparkLineSource), typeof(PwSparkLine), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, new PropertyChangedCallback(OnLineSourceChanged)));

		private static void OnLineSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PwSparkLine)d).OnLineSourceChanged((IPwSparkLineSource)e.NewValue, (IPwSparkLineSource)e.OldValue);

		private void LineSource_Changed(object sender, EventArgs e)
			=> InvalidateVisual();


		private void OnLineSourceChanged(IPwSparkLineSource newValue, IPwSparkLineSource oldValue)
		{
			if (oldValue != null)
				oldValue.Changed -= LineSource_Changed;
			if (newValue != null)
				newValue.Changed += LineSource_Changed;
		} // proc OnLineSourceChanged

		public IPwSparkLineSource LineSource { get => (IPwSparkLineSource)GetValue(LineSourceProperty); set => SetValue(LineSourceProperty, value); }

		#endregion

		#region -- Foreground - Property ----------------------------------------------------

		public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register(nameof(Foreground), typeof(Pen), typeof(PwSparkLine), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

		public Pen Foreground { get => (Pen)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

		#endregion

		#region -- Fill - Property ----------------------------------------------------------

		public static readonly DependencyProperty FillProperty = DependencyProperty.Register(nameof(Fill), typeof(Brush), typeof(PwSparkLine), new FrameworkPropertyMetadata(Brushes.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));

		public Brush Fill { get => (Brush)GetValue(FillProperty); set => SetValue(FillProperty, value); }

		#endregion
	} // class PwSparkLine

	#endregion
}
