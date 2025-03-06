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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.PerfectWorking.Cred;
using Neo.PerfectWorking.Cred.Provider;
using Neo.PerfectWorking.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace PW.Core.Tests
{
	[TestClass]
	public class CredTests
    {
		private sealed class CredPackageMock : ICredPackage
		{
			public NetworkCredential GetCredential(Uri uri, string authType)
			{
				throw new NotImplementedException();
			}

			public object EncryptPassword(SecureString password, ICredentialProtector protector = null)
			{
				throw new NotImplementedException();
			}

			public SecureString DecryptPassword(object encryptedPassword, ICredentialProtector protector = null)
			{
				throw new NotImplementedException();
			}

			public string Name => "Cred";
		} // class CredPackageMock

		private static readonly ICredPackage package = new CredPackageMock();

		private static void TestCredItem(string uri, string usr, string pwd, string cmt, IXmlCredentialItem item)
		{
			Assert.AreEqual(uri, item.TargetName);
			Assert.AreEqual(usr, item.UserName);
			Assert.AreEqual(pwd, item.EncryptedPassword as string);
			Assert.AreEqual(cmt, item.Comment);
		}

		[TestMethod]
		public void XmlParseTest()
		{
			var data = XmlCredentialItem.Load(@"Cred\XmlParseTest.xml").ToArray();

			Assert.AreEqual(2, data.Length);
			TestCredItem("http://test2", "user2", "pwd2", "http test2", data[0]);
			TestCredItem("ftp://test1", "user1", "pwd1", "ftp test1", data[1]);

			XmlCredentialItem.Save(@"Cred\XmlParseTest-2.xml", data);
			var data2 = XmlCredentialItem.Load(@"Cred\XmlParseTest-2.xml").ToArray();

			for (var i = 0; i < data.Length; i++)
				Assert.AreEqual(XmlCredentialProperty.None, XmlCredentialItem.Compare(data[i], data2[i], true), $"index[{i}]");
		} // proc XmlParseTest

		[TestMethod]
		public void XmlReadOnlyTest()
		{
			File.Copy(@"Cred\XmlParseTest.xml", @"Cred\XmlReadOnly.xml", true);
			var ro = new FileReadOnlyCredentialProvider(package, @"Cred\XmlReadOnly.xml", Protector.NoProtector, null);
			Assert.AreEqual(2, ro.Count);

			File.Copy(@"Cred\XmlParseChg.xml", @"Cred\XmlReadOnly.xml", true);
			((IPwAutoSaveFile)ro).Reload();
			Assert.AreEqual(2, ro.Count);
			TestCredItem("ftp://test1", "user3", "pwd1", "ftp test3", (IXmlCredentialItem)ro.FirstOrDefault());
			TestCredItem("ftp://test4", "user4", "pwd4", "ftp test4", (IXmlCredentialItem)ro.Skip(1).FirstOrDefault());
		}
	} // class CredTests
}
