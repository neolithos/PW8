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
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace Neo.PerfectWorking.Cred
{
	#region -- interface ICredentialInfo ------------------------------------------------

	/// <summary>Information of one credential</summary>
	public interface ICredentialInfo : INotifyPropertyChanged
	{
		void SetPassword(SecureString password);
		SecureString GetPassword();

		/// <summary>Targetname or uri of the credential</summary>
		string TargetName { get; }
		/// <summary>User name</summary>
		string UserName { get; set; }
		/// <summary>Comment</summary>
		string Comment { get; set; }
		/// <summary>Last time the credential was changed.</summary>
		DateTime LastWritten { get; }
		/// <summary>Credential image</summary>
		object Image { get; }

		/// <summary>Credentialprovider </summary>
		ICredentialProvider Provider { get; }
	} // interface ICredentialInfo

	#endregion

	#region -- interface ICredentialProvider --------------------------------------------

	public interface ICredentialProvider : ICredentials, IEnumerable<ICredentialInfo>, INotifyCollectionChanged
	{
		/// <summary>Adds the new credential item.</summary>
		/// <param name="newItem"></param>
		/// <returns></returns>
		ICredentialInfo Append(ICredentialInfo newItem);
		/// <summary></summary>
		/// <param name="targetName"></param>
		/// <returns></returns>
		bool Remove(string targetName);

		string Name { get; }
		/// <summary>Is this credentialinfo editable</summary>
		bool IsReadOnly { get; }
	} // interface ICredentialProvider

	#endregion

	#region -- interface ICredentialProtector -------------------------------------------

	public interface ICredentialProtector : IDisposable
	{
		/// <summary>Decrypts the data to an password.</summary>
		/// <param name="encrypt">Encrypted data.</param>
		/// <param name="password">Decrypted password.</param>
		/// <returns><c>true</c>, if the decrypt was successful</returns>
		bool TryDecrypt(object encrypted, out SecureString password);
		/// <summary>Encrypts the data.</summary>
		/// <param name="password">Password to encrypt.</param>
		/// <returns>Encrypted password</returns>
		object Encrypt(SecureString password);
		/// <summary>Checks the header of the current encrypted data.</summary>
		/// <param name="encrypted">Encrypted data.</param>
		/// <returns><c>true</c>, if decrypt can encrypt the password.</returns>
		bool CanDecryptPrefix(object encrypted);
	} // interface ICredentialProtector

	#endregion
}
