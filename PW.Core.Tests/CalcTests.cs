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
using Neo.PerfectWorking.Calc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PW.Core.Tests
{
	[TestClass]
	public class CalcTests
	{
		private void ScanToken(string formular, params Formular.TokenType[] type)
		{
			var f = new Formular(new FormularEnvironment(null), formular);
			var i = 0;
			foreach (var t in f.GetTokens())
			{
				Console.WriteLine(t.ToString());
				Assert.AreEqual(type[i], t.Type);
				i++;
			}
			Assert.AreEqual(i, type.Length);
		} // func ScanToken

		private void ScanNums(string formular, params object[] values)
		{
			var f = new Formular(new FormularEnvironment(null), formular);
			var i = 0;
			foreach (var t in f.GetTokens())
			{
				Console.WriteLine(t.ToString());
				Assert.AreEqual(values[i], t.Value);
				i++;
			}
			Assert.AreEqual(i, values.Length);
		} // func ScanToken

		[TestMethod]
		public void ScanTest01()
		{
			ScanToken("+ - * ** / \\ % & | ^ ~ ! // << >> ( ) = ; # : abs",
				Formular.TokenType.Plus,
				Formular.TokenType.Minus,
				Formular.TokenType.Star,
				Formular.TokenType.Power,
				Formular.TokenType.Slash,
				Formular.TokenType.Backshlash,
				Formular.TokenType.Percent,
				Formular.TokenType.BitAnd,
				Formular.TokenType.BitOr,
				Formular.TokenType.BitXOr,
				Formular.TokenType.BitNot,
				Formular.TokenType.Faculty,
				Formular.TokenType.Root,
				Formular.TokenType.ShiftLeft,
				Formular.TokenType.ShiftRight,
				Formular.TokenType.BracketOpen,
				Formular.TokenType.BracketClose,
				Formular.TokenType.Equal,
				Formular.TokenType.Semi,
				Formular.TokenType.Raute,
				Formular.TokenType.Colon,
				Formular.TokenType.Identifier
			);
		}

		[TestMethod]
		public void ScanTest02()
		{
			ScanNums("0 123 1,24 4e2",
				0L,
				123L,
				1.24,
				400.0
			);
		}

		[TestMethod]
		public void ScanTest03()
		{
			var f = new Formular(new FormularEnvironment(null), "1111111111111111111111111111111111111111111111111111111111111111111111111111111");
			var t = f.GetTokens().FirstOrDefault();
			Assert.AreEqual(typeof(double), t.Value.GetType());
			f = new Formular(new FormularEnvironment(null), "1111111111111111111111111111111111111111111111111111111111111111111", true);
			t = f.GetTokens().FirstOrDefault();
			Assert.AreEqual(typeof(string), t.Value.GetType()); // überlauf
		}

		private void TestCalc(object expected, string formular)
		{
			Console.WriteLine("=== " + formular + " ===");
			var f = new Formular(new FormularEnvironment(null), formular);
			f.DebugOut(Console.Out);
			Assert.AreEqual(expected, f.GetResult());
			Console.WriteLine("===> ({0}){1}", expected.GetType().Name, expected);
			Console.WriteLine();
		}

		[TestMethod]
		public void ParseTest01()
			=> TestCalc(65.2, "2 + 20 + 21,1 * 2 + 1");

		[TestMethod]
		public void ParseTest02()
			=> TestCalc(5L, "10 / 2");

		[TestMethod]
		public void ParseTest03()
			=> TestCalc(1023L, "2 ** 10 -1");

		[TestMethod]
		public void ParseTest04()
			=> TestCalc(23L, "abs(23*-1)");

		[TestMethod]
		public void ParseTest05()
			=> TestCalc(1.2, "a = 1,2");

	}
}
