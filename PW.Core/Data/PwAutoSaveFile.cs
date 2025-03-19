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
using System.Threading;
using System.Threading.Tasks;

namespace Neo.PerfectWorking.Data
{
	#region -- interface IPwAutoSaveFile ----------------------------------------------

	public interface IPwAutoSaveFile
	{
		/// <summary>Save file</summary>
		/// <param name="force"></param>
		void Save(bool force = false);
		/// <summary>Reload file from disk.</summary>
		void Reload();

		/// <summary>Modification time, the file was loaded</summary>
		DateTime LastModificationTime { get; }
		/// <summary>Is the file modificated.</summary>
		bool IsModified { get; }
		/// <summary>Filename</summary>
		string FileName { get; }
	} // interface IPwAutoSaveFile

	#endregion

	#region -- interface IPwAutoSaveFile ----------------------------------------------

	/// <summary>Extension to reload or save files asynchron.</summary>
	public interface IPwAutoSaveFile2 : IPwAutoSaveFile
	{
		/// <summary></summary>
		/// <param name="force"></param>
		/// <returns></returns>
		Task SaveAsync(bool force = false);
		/// <summary></summary>
		/// <returns></returns>
		Task ReloadAsync();
	} // interface IPwAutoSaveFile

	#endregion
}
