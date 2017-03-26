using System;
using System.IO;
using System.Security.Cryptography;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.IronLua;
using Neo.PerfectWorking.Data;
using Neo.PerfectWorking.Stuff;

namespace PW.Core.Tests
{
	[TestClass]
	public class ConfigTests
	{
		[TestMethod]
		public void ParseConfig01()
		{
			var cfg = new PwConfigTable(Path.GetFullPath(@"..\..\Configs\Parse\Config01.xml"));
			Assert.AreEqual(false, cfg.IsModified);
			Assert.AreEqual(23, cfg["vint"]);
			Assert.AreEqual("Hallo Welt", cfg["vstr"]);

			var t1 = (LuaTable)cfg["t1"];
			Assert.AreEqual(42, t1["subv"]);
			Assert.AreEqual(new DateTime(1982, 5, 26), t1["subdt"]);

			var t2 = (LuaTable)t1["subsubt"];
			Assert.AreEqual("Hallo\n\tWelt", t2["v"]);
			Assert.AreEqual("<text>", t2["v2"]);
			Assert.AreEqual("inline", ((XElement)t2["xv"]).Name.LocalName);
			Assert.AreEqual(1, t2[1]);
			Assert.AreEqual(2, t2[2]);
			Assert.AreEqual(10, t2[10]);

			var t3 = (LuaTable)t1["subsube"];
			Assert.AreEqual(0, t3.Values.Count);

			t3["neu"] = 1;

			Assert.AreEqual(true, cfg.IsModified);
		}

		[TestMethod]
		public void ParseSkip01()
		{
			var cfg = new PwConfigTable(Path.GetFullPath(@"..\..\Configs\Parse\Skip01.xml"));
			Assert.AreEqual(false, cfg.IsModified);
			Assert.AreEqual(23, cfg["vint"]);
			Assert.AreEqual("Hallo Welt", cfg["vstr"]);
			Assert.AreEqual(2, cfg.Members.Count);
		}

		[TestMethod]
		public void SaveConfig01()
		{
			var fileName = Path.GetFullPath(@"..\..\Configs\Saved\Config01.xml");
			File.Delete(fileName);
			var cfg = new PwConfigTable(fileName)
			{
				["vint"] = 23,
				["vstr"] = "Hallo Welt"
			};
			var t1 = new LuaTable();
			cfg["t1"] = t1;
			t1["subv"] = 42;
			t1["subdt"] = new DateTime(1982, 5, 26);

			var t2 = new LuaTable
			{
				["v"] = "Hallo\n\tWelt",
				["v2"] = "<text>",
				["xv"] = new XElement("inline", new XElement("test", "wert")),
				[1] = 1,
				[2] = 2,
				[10] = 10
			};
			t1["subsubt"] = t2;


			var t3= new LuaTable();
			t1["subsube"] = t3;

			Assert.AreEqual(true, cfg.IsModified);

			cfg.Save();

			Assert.AreEqual(false, cfg.IsModified);
			Assert.AreEqual(fileName, cfg.FileName);

			var md5 = MD5.Create();
			Assert.AreEqual("0CB38A7CEFDFC7BA596E3BF19A6DC782", md5.ComputeHash(File.ReadAllBytes(fileName)).ToHexString());
		}

		[TestMethod]
		public void CheckAgents01()
		{
			var fileName = Path.GetFullPath(@"..\..\Configs\Saved\Agents01.xml");
			File.Delete(fileName);

			var cfg = new PwConfigTable(fileName);
			Assert.AreEqual(false, cfg.IsModified);
			cfg["t"] = new LuaTable
			{
				["s"] = new LuaTable()
			};
			dynamic c = cfg;
			c.t.s.test = 23;

			Assert.AreEqual(23, c.t.s.test);
			Assert.AreEqual(true, cfg.IsModified);
		}

	}

}
