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
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.PerfectWorking.Cred;

namespace PW.Core.Tests
{
	[TestClass]
	public class WinCred
	{
		[TestMethod]
		public void CredPlainEnum()
		{
			//var p = new WindowsCredentialProviderReadOnly(GlobalMock.Instance, "CredTest");
			//foreach (var c in p)
			//	Console.WriteLine($"Cred: {c.TargetName}, {c.UserName}, {c.Comment}, {c.LastWritten}");
		}

		[TestMethod]
		public void CredWriteTest()
		{
			//var b = Marshal.StringToCoTaskMemUni("geheim");
			//var l = 6;
			////var b = Marshal.AllocCoTaskMem(1024);
			////var l = 1024;
			////if (!NativeMethods.CredProtect(false, "geheim", 6, b, ref l, out var t))
			////	throw new Win32Exception();
			//Console.WriteLine(Marshal.PtrToStringUni(b, l));

			//var l2 = 1024;
			//var b2 = Marshal.AllocCoTaskMem(l2);
			//if (!NativeMethods.CredUnprotect(false, b, l, b2, ref l2))
			//	throw new Win32Exception();
			//Console.WriteLine(Marshal.PtrToStringUni(b2, l2));

			//Marshal.FreeCoTaskMem(b);
			//Marshal.FreeCoTaskMem(b2);
			////var p = new WindowsCredentialProvider(GlobalMock.Instance, "CredTest", "pw_test:*", null, false);
		}
	}
}
