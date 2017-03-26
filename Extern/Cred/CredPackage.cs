using Neo.PerfectWorking.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using System.IO;
using System.Xml;
using System.Globalization;
using System.ComponentModel;
using System.Security;
using System.Collections.Specialized;
using System.Xml.Linq;
using Neo.PerfectWorking.Cred.Data;
using System.Windows.Media;

namespace Neo.PerfectWorking.Cred
{
	public sealed class CredPackage : PwPackageBase, ICredentials
	{
		private readonly IPwCollection<ICredentialProvider> credentialProviders;

		public CredPackage(IPwGlobal global) 
			: base(global, nameof(CredPackage))
		{
			this.credentialProviders = global.RegisterCollection<ICredentialProvider>(this);

			global.RegisterObject(this, nameof(CredPackagePane), new CredPackagePane(this));
		} // ctor

		public ICredentialProvider CreateCredentialProvider(string fileName, bool readOnly = false)
		{
			if (!Path.IsPathRooted(fileName))
			{
				if (fileName.IndexOf('\\') >= 0)
					throw new Exception("Relative paths are not allowed.");

				fileName = Path.Combine(Global.UI.ApplicationLocalDirectory.FullName, fileName + ".xcred");
			}
			throw new NotImplementedException(); //return new FileCredProvider(fileName, readOnly);
		} // func RegisterFileProvider

		public NetworkCredential GetCredential(Uri uri, string authType)
		{
			NetworkCredential found = null;
			lock (credentialProviders.SyncRoot)
			{
				foreach (var cp in credentialProviders)
					found = cp.GetCredential(uri, authType);
			}
			return found;
		} // func GetCredential

		public IPwCollection<ICredentialProvider> CredentialProviders => credentialProviders;
	} // class CredPackage
}
