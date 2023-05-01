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
using Neo.PerfectWorking.Data;
using System.Runtime.CompilerServices;

[assembly: PwPackage(typeof(Neo.PerfectWorking.Backup.BackupPackage))]
[assembly: InternalsVisibleTo("PW.Core.Tests")]

namespace Neo.PerfectWorking.Backup
{
	public sealed class BackupPackage : PwPackageBase
	{
		public BackupPackage(IPwGlobal global)
			: base(global, nameof(BackupPackage))
		{
		} // ctor
	} // class CredPackage
}
