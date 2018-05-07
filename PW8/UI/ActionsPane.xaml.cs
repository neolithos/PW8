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
using System.Windows.Data;
using System.Windows.Input;
using Neo.PerfectWorking.Data;

namespace Neo.PerfectWorking.UI
{
	public partial class ActionsPane : PwWindowPane
	{
		private static DependencyPropertyKey actionsPropertyKey = DependencyProperty.RegisterReadOnly("Actions", typeof(ICollectionView), typeof(ActionsPane), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty ActionsProperty = actionsPropertyKey.DependencyProperty;

		private readonly IPwGlobal global;
		
		internal ActionsPane(IPwGlobal global)
		{
			InitializeComponent();

			this.global = global;
			var actions = CollectionViewSource.GetDefaultView(global.GetCollection<PwAction>());

			actions.Filter = OnFilter;
			actions.SortDescriptions.Add(new SortDescription("Title", ListSortDirection.Ascending));

			SetValue(actionsPropertyKey, actions);

			this.DataContext = this;
		} // ctor

		private bool OnFilter(object item)
		{
			var c = item as PwAction;
			if (c == null)
				return false;
			else
			{
				var currentFilter = filterListBox.CurrentFilter;
				if (String.IsNullOrEmpty(currentFilter))
					return true;
				else
					return c.Title.IndexOf(currentFilter, StringComparison.OrdinalIgnoreCase) >= 0
						|| (c.Label != null && c.Label.IndexOf(currentFilter, StringComparison.OrdinalIgnoreCase) >= 0);
			}
		} // func OnFilter

		public ICollectionView Actions => (ICollectionView)GetValue(ActionsProperty);
	} // class ActionsPane
}
