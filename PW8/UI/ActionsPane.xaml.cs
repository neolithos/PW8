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
	public partial class ActionsPane : PwContentPane
	{
		private static readonly DependencyPropertyKey actionsPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Actions), typeof(ICollectionView), typeof(ActionsPane), new FrameworkPropertyMetadata(null));
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

		private void ActionListDoubleClick(object sender, MouseButtonEventArgs e)
		{
			if (e.LeftButton == MouseButtonState.Pressed)
			{
				var ctx = (e.Source as FrameworkElement)?.DataContext;
				if (ctx is ICommand cmd && cmd.CanExecute(null))
					cmd.Execute(null);
			}
		} // event actionListDoubleClick

		private void ActionListRightClick(object sender, MouseButtonEventArgs e)
		{
			if((e.Source as FrameworkElement)?.DataContext is IPwContextMenuFactory f)
				;
		} // event actionListRightClick

		private bool OnFilter(object item)
		{
			if (item is PwAction c)
			{
				var currentFilter = filterListBox.CurrentFilter;
				if (String.IsNullOrEmpty(currentFilter))
					return true;
				else
					return c.Title.IndexOf(currentFilter, StringComparison.OrdinalIgnoreCase) >= 0
						|| (c.Label != null && c.Label.IndexOf(currentFilter, StringComparison.OrdinalIgnoreCase) >= 0);
			}
			else
				return false;
		} // func OnFilter

		public ICollectionView Actions => (ICollectionView)GetValue(ActionsProperty);
	} // class ActionsPane
}
