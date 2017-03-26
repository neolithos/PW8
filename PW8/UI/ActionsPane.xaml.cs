using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Neo.PerfectWorking.Data;

namespace Neo.PerfectWorking.UI
{
	public partial class ActionsPane : PwWindowPane
	{
		public static DependencyProperty ActionsProperty = DependencyProperty.Register("Actions", typeof(ICollectionView), typeof(ActionsPane));

		private readonly IPwGlobal global;
		
		internal ActionsPane(IPwGlobal global)
		{
			InitializeComponent();

			this.global = global;
			var actions = CollectionViewSource.GetDefaultView(global.GetCollection<PwAction>());

			actions.Filter = OnFilter;
			actions.SortDescriptions.Add(new SortDescription("Title", ListSortDirection.Ascending));

			SetValue(ActionsProperty, actions);

			this.DataContext = this;
		} // ctor

		protected override void OnKeyUp(KeyEventArgs e) 
			=> base.OnKeyUp(e);

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
	} // class ActionsPane
}
