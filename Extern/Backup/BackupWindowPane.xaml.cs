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
using Neo.PerfectWorking.UI;
using System;

namespace Neo.PerfectWorking.Backup
{
	/// <summary>
	/// Interaction logic for BackupWindowPane.xaml
	/// </summary>
	public partial class BackupWindowPane : PwContentPane
	{
		private readonly BackupPackage package;

		public BackupWindowPane(BackupPackage package)
		{
			InitializeComponent();

			this.package = package ?? throw new ArgumentNullException(nameof(package));

			this.DataContext = package;
		} // ctor
	} //class BackupWindowPane
}
