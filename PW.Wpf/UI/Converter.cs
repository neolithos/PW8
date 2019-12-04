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
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using TecWare.DE.Stuff;

namespace Neo.PerfectWorking.UI
{
	#region -- class PwConverter ------------------------------------------------------

	public static class PwConverter
	{
		public static IValueConverter NotBoolean { get; } = new NotBooleanConverter();
		public static IValueConverter BooleanToVisible { get; } = new VisibilityBooleanConverter();
		public static IValueConverter BooleanToNotVisible { get; } = new VisibilityBooleanConverter() { Negate = true };
		public static IValueConverter BooleanToCollapsed { get; } = new VisibilityBooleanConverter() { Collapse = true };
	} // class PwConverter

	#endregion

	#region -- class InvariantConverter -----------------------------------------------

	public class InvariantConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
			=> Procs.ChangeType(value ?? parameter, targetType);

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			=> Procs.ChangeType(value, targetType);

		public static InvariantConverter Default { get; } = new InvariantConverter();
	} // class InvariantConverter

	#endregion

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
