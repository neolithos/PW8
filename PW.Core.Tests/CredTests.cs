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
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TecWare.DE.Stuff;

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
				=> protector.Encrypt(password);

			public SecureString DecryptPassword(object encryptedPassword, ICredentialProtector protector = null)
				=> protector.TryDecrypt(encryptedPassword, out var pwd) ? pwd : null;

			public string Name => "Cred";
		} // class CredPackageMock

		private sealed class CredentialInfo : ICredentialInfo
		{
			public event PropertyChangedEventHandler PropertyChanged;

			private readonly ICredentialProvider provider;
			private readonly string targetName;
			private readonly string userName;
			private readonly string comment;
			private readonly string password;
			private readonly DateTime lastWritten;

			public CredentialInfo(ICredentialProvider provider, string targetName, string userName, string comment, string password, DateTime lastWritten)
			{
				this.provider = provider;
				this.targetName = targetName;
				this.userName = userName;
				this.comment = comment;
				this.password = password;
				this.lastWritten = lastWritten;
			}

			public SecureString GetPassword()
				=> Protector.NoProtector.TryDecrypt("0" + password, out var pwd) ? pwd : null;

			public void SetPassword(SecureString password)
			{
				throw new NotImplementedException();
			}

			public ICredentialProvider Provider => provider;
			public object Image => null;

			public string TargetName => targetName;
			public string UserName { get => userName; set => throw new NotImplementedException(); }
			public string Comment { get => comment; set => throw new NotImplementedException(); }
			public DateTime LastWritten => lastWritten;
		}

		private static readonly ICredPackage package = new CredPackageMock();

		private static void TestCredItem(string uri, string usr, string pwd, string cmt, IXmlCredentialItem item)
		{
			Assert.AreEqual(uri, item.TargetName);
			Assert.AreEqual(usr, item.UserName);
			Assert.AreEqual(pwd, item.EncryptedPassword as string);
			Assert.AreEqual(cmt, item.Comment);
		}

		private static Task[] GetActiveTasks()
		{
			var mi = typeof(Task).GetMethod("GetActiveTasks", BindingFlags.NonPublic | BindingFlags.Static);
			return (Task[])mi.Invoke(null, Array.Empty<object>());
		}

		private static void DeleteFile(string fileName)
		{
			var fi = new FileInfo(fileName);
			if(fi.Exists)
			{
				if ((fi.Attributes & FileAttributes.ReadOnly) != 0)
					fi.Attributes &= ~FileAttributes.ReadOnly;
				fi.Delete();
			}
		}

		private static void CopyFile(string srcName, string dstName)
		{	
			File.Copy(srcName, dstName, true);
			File.SetLastWriteTimeUtc(dstName, DateTime.UtcNow);
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
			CopyFile(@"Cred\XmlParseTest.xml", @"Cred\XmlReadOnly.xml");
			var ro = new FileReadOnlyCredentialProvider(package, @"Cred\XmlReadOnly.xml", Protector.NoProtector, null);
			Assert.AreEqual(2, ro.Count);

			CopyFile(@"Cred\XmlParseChg.xml", @"Cred\XmlReadOnly.xml");
			((IPwAutoSaveFile)ro).Reload();
			Assert.AreEqual(2, ro.Count);
			TestCredItem("ftp://test1", "user3", "pwd1", "ftp test3", (IXmlCredentialItem)ro.FirstOrDefault());
			TestCredItem("ftp://test4", "user4", "pwd4", "ftp test4", (IXmlCredentialItem)ro.Skip(1).FirstOrDefault());
		}

		[TestMethod]
		public void XmlReadOnlyTestShadow()
		{
			DeleteFile(@"Cred\XmlReadOnly.Shadow.xml");
			DeleteFile(@"Cred\XmlReadOnly.xml");

			var ro = new FileReadOnlyCredentialProvider(package, @"Cred\XmlReadOnly.xml", Protector.NoProtector, @"Cred\XmlReadOnly.Shadow.xml");

			Assert.AreEqual(0, ro.Count);

			CopyFile(@"Cred\XmlParseTest.xml", @"Cred\XmlReadOnly.xml");
			((IPwAutoSaveFile)ro).Reload();
			Task.WaitAll(GetActiveTasks());
			Assert.AreEqual(2, ro.Count);

			CopyFile(@"Cred\XmlParseChg.xml", @"Cred\XmlReadOnly.xml");
			((IPwAutoSaveFile)ro).Reload();
			Task.WaitAll(GetActiveTasks());
			Assert.AreEqual(2, ro.Count);
			TestCredItem("ftp://test1", "user3", "pwd1", "ftp test3", (IXmlCredentialItem)ro.FirstOrDefault());
			TestCredItem("ftp://test4", "user4", "pwd4", "ftp test4", (IXmlCredentialItem)ro.Skip(1).FirstOrDefault());
		}

		[TestMethod]
		public void XmlDirectSaveTest()
		{
			DeleteFile(@"Cred\Save.xml");
			var f = new FileCredentialProvider(package, @"Cred\Save.xml", Protector.NoProtector, null);
			Assert.AreEqual(0,f.Count);

			// test new entry
			f.Append(new CredentialInfo(f, "http://neu1", "user1", "comment1", "pwd1", DateTime.UtcNow));

			// load new stuff from disk
			CopyFile(@"Cred\XmlParseTest.xml", @"Cred\Save.xml");

			f.Append(new CredentialInfo(f, "http://test2", "usr2", "c2", "pwd2", DateTime.UtcNow));

			((IPwAutoSaveFile)f).Reload();
			Assert.IsTrue(f.IsModified);

			// save combination
			((IPwAutoSaveFile)f).Save();
			Assert.IsFalse(f.IsModified);

			// test
			var data = XmlCredentialItem.Load(@"Cred\Save.xml").ToArray();
			TestCredItem("http://neu1", "user1", "0pwd1", "comment1", data[0]);
			TestCredItem("http://test2", "usr2", "0pwd2", "c2", data[1]);
			TestCredItem("ftp://test1", "user1", "pwd1", "ftp test1", data[2]);

			CopyFile(@"Cred\XmlParseTest.xml", @"Cred\Save.xml");
			Thread.Sleep(100);

			f.Remove("ftp://test1");
			((IPwAutoSaveFile)f).Reload();
			Assert.IsTrue(f.IsModified);

			((IPwAutoSaveFile)f).Save();
			Assert.IsFalse(f.IsModified);

			data = XmlCredentialItem.Load(@"Cred\Save.xml").ToArray();
			TestCredItem("http://test2", "user2", "pwd2", "http test2", data[0]);

			Assert.AreEqual(1, f.Count);
		}

		[TestMethod]
		public void XmlShadowSaveTest()
		{
			DeleteFile(@"Cred\Save.xml");
			DeleteFile(@"Cred\Save-shadow.xml");
			DeleteFile(@"Cred\Save-changes.xml");
			var f = new FileCredentialProvider(package, @"Cred\Save.xml", Protector.NoProtector, @"Cred\Save-shadow.xml");
			Assert.AreEqual(0, f.Count);

			// test new entry
			f.Append(new CredentialInfo(f, "http://neu1", "user1", "comment1", "pwd1", DateTime.UtcNow));
			((IPwAutoSaveFile)f).Save(false);

			// load new stuff from disk
			CopyFile(@"Cred\XmlParseTest.xml", @"Cred\Save.xml");

			f.Append(new CredentialInfo(f, "http://test2", "usr2", "c2", "pwd2", DateTime.UtcNow));

			((IPwAutoSaveFile)f).Reload();
			Assert.IsTrue(f.IsModified);

			// save combination
			((IPwAutoSaveFile)f).Save();
			Assert.IsFalse(f.IsModified);

			// test
			var data = XmlCredentialItem.Load(@"Cred\Save-changes.xml").ToArray();
			TestCredItem("http://neu1", "user1", "0pwd1", "comment1", data[0]);
			TestCredItem("http://test2", "usr2", "0pwd2", "c2", data[1]);
			
			CopyFile(@"Cred\XmlParseTest.xml", @"Cred\Save.xml");
			Thread.Sleep(100);

			f.Remove("ftp://test1");
			((IPwAutoSaveFile)f).Reload();
			Assert.IsTrue(f.IsModified);

			((IPwAutoSaveFile)f).Save();
			Assert.IsFalse(f.IsModified);

			Assert.AreEqual(2, f.Count);


			f = new FileCredentialProvider(package, @"Cred\Save.xml", Protector.NoProtector, @"Cred\Save-shadow.xml");
			Assert.AreEqual(2, f.Count);
		}
	} // class CredTests
}
