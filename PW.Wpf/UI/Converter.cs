using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Neo.PerfectWorking.UI
{
	#region -- class ImageDefaultConverter ----------------------------------------------

	[ValueConversion(typeof(ImageSource), typeof(ImageSource))]
	public sealed class ImageDefaultConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
			=> value ?? DefaultImage;

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) 
			=> throw new NotSupportedException();

		public ImageSource DefaultImage { get; set; }
	} // class ImageDefaultConverter

	#endregion

	#region -- class VisibilityBooleanConverter -----------------------------------------

	[ValueConversion(typeof(bool), typeof(Visibility))]
	public sealed class VisibilityBooleanConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var isVisible = (bool)value;
			if (Negate)
				isVisible = !isVisible;

			return isVisible ? Visibility.Visible : (Collapse ? Visibility.Collapsed : Visibility.Hidden);
		} // func Convert

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			=> throw new NotSupportedException();

		public bool Collapse { get; set; } = false;
		public bool Negate { get; set; } = false;
	} // class VisibilityConverter

	#endregion

	#region -- class NotBooleanConverter ------------------------------------------------

	[ValueConversion(typeof(bool), typeof(bool))]
	public sealed class NotBooleanConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value != null && value.GetType() != typeof(bool) 
				|| targetType != typeof(bool))
				throw new ArgumentException("Only boolean values allowed.", nameof(targetType));

			return value is bool v ? !v : true;
		} // func Convert

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			=> Convert(value, targetType, parameter, culture);
	} // class NotBooleanConverter

	#endregion
}
