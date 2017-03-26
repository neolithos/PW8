using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neo.PerfectWorking.Data
{
	#region -- interface IPwAutoSaveFile ------------------------------------------------

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
}
