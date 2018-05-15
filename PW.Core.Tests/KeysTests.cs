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
using Neo.PerfectWorking.Stuff;

namespace PW.Core.Tests
{
	[TestClass]
	public class KeyTests
	{
		[TestMethod]
		public void TestKeyNames()
		{
			Assert.AreEqual(13, PwKey.GetVirtualKeyFromString("Return", false));
			Assert.AreEqual(65, PwKey.GetVirtualKeyFromString("A", false));
			Assert.AreEqual(90, PwKey.GetVirtualKeyFromString("Z", false));
			Assert.AreEqual(112, PwKey.GetVirtualKeyFromString("F1", false));
			Assert.AreEqual(135, PwKey.GetVirtualKeyFromString("F24", false));
			Assert.AreEqual(160, PwKey.GetVirtualKeyFromString("LeftShift", false));
			Assert.AreEqual(220, PwKey.GetVirtualKeyFromString("Oem5", false));
			Assert.AreEqual(254, PwKey.GetVirtualKeyFromString("OemClear", false));
		}

		[TestMethod]
		public void KeyConvert()
		{
			Assert.AreEqual(PwKey.None, PwKey.Parse("None"));
			Assert.AreEqual(new PwKey(PwKeyModifiers.None, 65), PwKey.Parse("A"));
			Assert.AreEqual(new PwKey(PwKeyModifiers.Win, 65), PwKey.Parse("Win+A"));
			Assert.AreEqual(new PwKey(PwKeyModifiers.Control| PwKeyModifiers.Alt, 65), PwKey.Parse("Ctrl+Alt+A"));
		}
	}
}
