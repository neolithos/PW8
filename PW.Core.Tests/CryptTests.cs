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
		public void TestCrypteSimple()
		{
			var p = new XorEncryptProtector("XorSimple", "AA", "Perfect Working 8 @ 2017".CreateSecureString());
			EncryptDecrypt(p);
		}

		[TestMethod]
		public void TestCrypteDESstatic()
		{
			var p = new DesEncryptProtectorStatic("DES Static", "AA", new byte[] { 0x03, 0x01, 0x20, 0x17, 0x56, 0x38, 0x22, 0x38, 0xAF, 0xFE, 0x56, 0x18, 0x55, 0x71, 0xE4, 0xF4 });
			EncryptDecrypt(p, "AADbXAmqadEY+xhmzjC269Ow==");
		}

		[TestMethod]
		public void TestCrypteDESstring()
		{
			var p = new DesEncryptProtectorString("DES Static", "Perfect Working 8 @ 2017".CreateSecureString());
			EncryptDecrypt(p);
		}
	}
}
