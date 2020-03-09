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
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

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
	} // class UIHelper
}
