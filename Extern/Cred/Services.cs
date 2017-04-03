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

	public interface ICredentialProtector
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

	public interface ICredentialProtectorUI : ICredentialProtector
	{
		/// <summary>Human readable name for the protector.</summary>
		string Name { get; }
	} // interface ICredentialProtectorUI
}
