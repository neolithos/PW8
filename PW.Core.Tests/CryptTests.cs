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
using Neo.PerfectWorking.Stuff;

namespace PW.Core.Tests
{
	[TestClass]
	public class CryptTests
	{
		private static void EncryptDecrypt(ICredentialProtector p, string cryptedData = null)
		{
			var crypt = p.Encrypt(PasswordHelper.CreateSecureString("géh€im"));
			if (cryptedData != null)
				Assert.AreEqual(cryptedData, crypt);
			if (p.TryDecrypt(crypt, out var ss))
				Assert.AreEqual("géh€im", ss.GetPassword());
			else
				Assert.Fail();
		}

		[TestMethod]
		public void TestCryptNone()
		{
			var p = Protector.NoProtector;
			EncryptDecrypt(p, "0géh€im");
		}

		[TestMethod]
		public void TestCryptSimple()
		{
			var p = new XorEncryptProtector("XorSimple", "AA", "Perfect Working 8 @ 2017".CreateSecureString());
			EncryptDecrypt(p);
		}

		[TestMethod]
		public void TestCryptDESstatic()
		{
			var p = new DesEncryptProtectorStatic("DES Static", "AA", new byte[] { 0x03, 0x01, 0x20, 0x17, 0x56, 0x38, 0x22, 0x38, 0xAF, 0xFE, 0x56, 0x18, 0x55, 0x71, 0xE4, 0xF4 });
			EncryptDecrypt(p, "AADbXAmqadEY+xhmzjC269Ow==");
		}

		[TestMethod]
		public void TestCryptDESstring()
		{
			var p = new DesEncryptProtectorString("DES Static", "Perfect Working 8 @ 2017".CreateSecureString());
			EncryptDecrypt(p);
		}

		[TestMethod]
		public void TestWindowsWithIV()
		{
			var p = new WindowsCryptProtector("Windows With IV", false, false, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 });
			EncryptDecrypt(p);
		}

		[TestMethod]
		public void TestWindowsNoIV()
		{
			var p = new WindowsCryptProtector("Windows No IV");
			EncryptDecrypt(p);
		}

		[TestMethod]
		public void TestPowerShellNoKey()
		{
			var p = new PowerShellProtector("PowerShell NoKey");
			EncryptDecrypt(p);
		}

		[TestMethod]
		public void TestPowerShellWithKey()
		{
			var p = new PowerShellProtector("PowerShell WithKey", "testtesttesttest".CreateSecureString());
			EncryptDecrypt(p);
		}

		[TestMethod]
		public void TestPowerShellDecrypt()
		{
			var p = new PowerShellProtector("PowerShell NoKey", "testtesttesttest".CreateSecureString());
			if (p.TryDecrypt("76492d1116743f0423413b16050a5345MgB8AGMAeAArAGsAbwBNAHUAWAA3AHEAbgB0ADYATABSADcASABoAEgAWgBlAFEAPQA9AHwAOQAwAGYAZABkADgAZQAwADIAYQAyADIANgA3AGEAZQBhAGUAYQBkADgANQA3AGYANwBmADQANAA4AGUANQA1AA==", out var ss))
				Assert.AreEqual("géh€im", ss.GetPassword());
			else
				Assert.Fail();
		}
	}
}
