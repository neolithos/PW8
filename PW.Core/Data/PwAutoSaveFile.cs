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
	#region -- interface IPwAutoPersist -----------------------------------------------

	public interface IPwAutoPersist
	{
		/// <summary>Modification time, the file was loaded</summary>
		DateTime LastModificationTime { get; }
		/// <summary>Is the file modificated and ready to save.</summary>
		bool IsModified { get; }

		/// <summary>Filename, only for debug and visualisation.</summary>
		string FileName { get; }
	} // interface IPwAutoPersist

	#endregion

	#region -- interface IPwAutoPersistFile -------------------------------------------

	public interface IPwAutoPersistFile : IPwAutoPersist
	{
		/// <summary>Save file</summary>
		/// <param name="force"></param>
		void Save(bool force = false);
		/// <summary>Reload file from disk.</summary>
		void Reload();

		/// <summary>Reads the last write time of the monitored file from disk.</summary>
		DateTime LastWriteTime { get; }
	} // interface IPwAutoPersistFile

	#endregion

	#region -- interface IPwAutoPersistFileAsync --------------------------------------

	/// <summary>Extension to reload or save files asynchron.</summary>
	public interface IPwAutoPersistFileAsync : IPwAutoPersist
	{
		/// <summary></summary>
		/// <param name="force"></param>
		/// <returns></returns>
		Task SaveAsync(bool force = false);
		/// <summary></summary>
		/// <returns></returns>
		Task ReloadAsync();

		/// <summary>Return the last reload.</summary>
		DateTime LastSuccessfulReload { get; }
		/// <summary>Defines the time between reloads.</summary>
		TimeSpan ReloadIntervall { get; }
	} // interface IPwAutoPersistFileAsync

	#endregion
}
