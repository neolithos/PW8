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
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using TecWare.DE.Stuff;

namespace Neo.PerfectWorking.UI
{
	public static class UIHelper
	{
		#region -- class EnumeratorBefore ---------------------------------------------

		private class EnumeratorBefore : IEnumerator
		{
			private readonly IEnumerator elements;
			private readonly object firstElement;
			private int state = -1;

			public EnumeratorBefore(object firstElement, IEnumerator elements)
			{
				this.elements = elements;
				this.firstElement = firstElement;
			}

			public bool MoveNext()
			{
				state++;
				return state == 0 ? true : elements.MoveNext();
			} // func MoveNext

			public void Reset()
			{
				state = -1;
				elements.Reset();
			} // proc Reset

			public object Current => state == 0 ? firstElement : elements.Current;
		} // class EnumeratorBefore

		#endregion

		public static IEnumerator GetChildrenEnumerator(IEnumerator logicalChildren, object header)
		{
			if (header is FrameworkElement)
				return new EnumeratorBefore(header, logicalChildren);
			else
				return logicalChildren;
		}

		public static void ToClipboard(this string text)
		{
			if (String.IsNullOrEmpty(text))
				return;
			try
			{
				Clipboard.SetText(text);
			}
			catch (Exception) { }
		} // func ToClipboard

		public static MenuItem FindMenuItem(this ItemsControl menu, string name)
		{
			if (menu is MenuItem mi && mi.Name == name)
				return mi;

			foreach (var m in menu.Items.OfType<ItemsControl>())
			{
				var r = FindMenuItem(m, name);
				if (r != null)
					return r;
			}

			return null;
		} // func FindMenuItem

		public static object ConvertValue(Type toType, object value, object @default)
		{
			if (value == null)
				return @default;

			if (value is string fromString)
			{
				var conv = TypeDescriptor.GetConverter(toType);
				return conv.ConvertFromInvariantString(null, fromString);
			}
			else
				return Procs.ChangeType(value, toType);
		} // func ConvertValue

		public static T ConvertValue<T>(object value, T @default)
			=> (T)ConvertValue(typeof(T), value, @default);

		public static Color GetMixedColor(Color color1, Color color2, float f)
		{
			return Color.FromScRgb(1.0f,
				color1.ScR * f + color2.ScR * (1.0f - f),
				color1.ScG * f + color2.ScG * (1.0f - f),
				color1.ScB * f + color2.ScB * (1.0f - f)
			);
		} // func GetMixedColor
	} // class UIHelper
}
