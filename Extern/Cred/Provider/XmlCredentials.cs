﻿#region -- copyright --
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
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using TecWare.DE.Stuff;

namespace Neo.PerfectWorking.Cred.Provider
{
	#region -- enum XmlCredentialProperty -----------------------------------------

	[Flags]
	internal enum XmlCredentialProperty
	{
		None = 0,
		TargetName = 1,
		UserName = 2,
		Comment = 4,
		Password = 8,
		LastWritten = 16,
		Lesser = 0x10000,
		Greater = 0x20000,

		PropertyMask = TargetName | UserName | Comment | Password | LastWritten,
		DirectionMask = Lesser | Greater
	} // enum XmlCredentialProperty

	#endregion

	#region -- interface IXmlCredentialItem -------------------------------------------

	internal interface IXmlCredentialItem : IComparable<IXmlCredentialItem>, IEquatable<IXmlCredentialItem>
	{
		string TargetName { get; }
		string UserName { get; }
		string Comment { get; }
		DateTime LastWritten { get; }
		object EncryptedPassword { get; }
	} // interface IXmlCredentialItem

	#endregion

	#region -- class XmlCredentialItem ------------------------------------------------

	[DebuggerDisplay("(XmlCredentialItem[{targetName}]")]
	internal sealed class XmlCredentialItem : IXmlCredentialItem, IEquatable<IXmlCredentialItem>, IComparable<IXmlCredentialItem>
	{
		private static readonly XName rootNodeName = "passwords";
		private static readonly XName entryName = "entry";

		private readonly string targetName;
		private readonly string userName;
		private readonly string comment;
		private readonly DateTime lastWritten;
		private readonly object encryptedPassword;

		#region -- Ctor/Dtor ----------------------------------------------------------

		private XmlCredentialItem(string targetName, string userName, string comment, DateTime lastWritten, object encryptedPassword)
		{
			this.targetName = targetName ?? throw new ArgumentNullException(nameof(targetName));
			this.userName = userName;
			this.comment = comment;
			this.lastWritten = lastWritten;
			this.encryptedPassword = encryptedPassword;
		} // ctor

		public override int GetHashCode()
		{
			return targetName.GetHashCode() ^
				(userName?.GetHashCode() ?? 0) ^
				(comment?.GetHashCode() ?? 0) ^
				lastWritten.GetHashCode() ^
				(encryptedPassword?.GetHashCode() ?? 0);
		} // func GetHashCode

		public override bool Equals(object obj)
			=> obj is XmlCredentialItem other && CompareTo(other) == 0;

		public bool Equals(IXmlCredentialItem other)
			=> CompareTo(other) == 0;

		public static XmlCredentialProperty Compare(IXmlCredentialItem x, IXmlCredentialItem y, bool testFirst = false)
		{
			var changed = XmlCredentialProperty.None;

			bool Set(int cmp, XmlCredentialProperty property)
			{
				if (cmp == 0)
					return false;
				else
				{
					if ((changed & XmlCredentialProperty.DirectionMask) == 0)
					{
						if (cmp < 0)
							changed |= XmlCredentialProperty.Lesser;
						else if (cmp > 0)
							changed |= XmlCredentialProperty.Greater;
					}

					changed |= property;
					return testFirst;
				}
			} // func Set

			if (Set(String.Compare(x.TargetName, y.TargetName, StringComparison.OrdinalIgnoreCase), XmlCredentialProperty.TargetName))
				return changed;
			if (Set(String.Compare(x.UserName, y.UserName, StringComparison.Ordinal), XmlCredentialProperty.UserName))
				return changed;
			if (Set(String.Compare(x.Comment, y.Comment, StringComparison.Ordinal), XmlCredentialProperty.Comment))
				return changed;
			if (Set(x.LastWritten.CompareTo(y.LastWritten), XmlCredentialProperty.LastWritten))
				return changed;
			if (Set(Equals(x.EncryptedPassword, y.EncryptedPassword) ? 0 : -1, XmlCredentialProperty.Password))
				return changed;

#if DEBUG
			if (changed != XmlCredentialProperty.None && (changed & XmlCredentialProperty.DirectionMask) == 0)
				throw new InvalidOperationException("Changed is set with property, with other direction.");
#endif

			return changed;
		} // func Compare

		public int CompareTo(IXmlCredentialItem other)
		{
			var cmp = Compare(this, other, true);
			if ((cmp & XmlCredentialProperty.Lesser) != 0)
				return -1;
			else if ((cmp & XmlCredentialProperty.Greater) != 0)
				return 1;
			else
				return 0;
		} // func CompareTo

		#endregion

		public string TargetName => targetName;
		public string UserName => userName;
		public string Comment => comment;
		public DateTime LastWritten => lastWritten;
		public object EncryptedPassword => encryptedPassword;

		#region -- Read ---------------------------------------------------------------

		private static bool IsCompress(string fileName)
			=> fileName.EndsWith(".gz", StringComparison.OrdinalIgnoreCase);

		private static Stream OpenFileStream(string fileName)
		{
			// open file name
			var src = (Stream)new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
			try
			{
				// is the stream compressed
				if (IsCompress(fileName))
					src = new GZipStream(src, CompressionMode.Decompress, false);
				return src;
			}
			catch
			{
				src.Dispose();
				throw;
			}
		} // func OpenFileStream

		public static XmlCredentialItem Read(XmlReader xml, string targetName)
		{
			string GetAttr(string name)
			{
				var r = xml.GetAttribute(name);
				return String.IsNullOrEmpty(r) ? null : r;
			} // func GetAttr

			DateTime ConvertDateTime(string value)
				=> value != null && Int64.TryParse(value, out var dt) ? DateTime.FromFileTimeUtc(dt) : DateTime.MinValue;

			object ReadEncryptedPassword(string fmt)
			{
				var contentString = xml.ReadContentAsString();
				if (fmt == "base64")
					return Convert.FromBase64String(contentString);
				else
					return contentString;
			} // func ReadEncryptedPassword

			var userName = GetAttr("uname");
			var comment = GetAttr("comment");
			var lastWritten = ConvertDateTime(GetAttr("written"));

			// read password
			object encryptedPassword = null;
			if (xml.IsEmptyElement)
				xml.Read();
			else
			{
				var fmt = GetAttr("fmt");

				xml.Read();
				switch (xml.NodeType)
				{
					case XmlNodeType.CDATA:
					case XmlNodeType.Text:
						encryptedPassword = ReadEncryptedPassword(fmt);
						break;
					default:
						xml.Skip();
						break;
				}
				xml.ReadEndElement();
			}

			return new XmlCredentialItem(targetName, userName, comment, lastWritten, encryptedPassword);
		} // func Read

		public static IEnumerable<XmlCredentialItem> Load(XmlReader xml)
		{
			xml.ReadStartElement(rootNodeName.LocalName);
			while (xml.NodeType == XmlNodeType.Element)
			{
				var targetName = xml.GetAttribute("uri");
				if (targetName == null)
					xml.Skip();
				else
					yield return Read(xml, targetName);
			}
		} // func ReadAsync

		public static async Task<IEnumerable<XmlCredentialItem>> LoadAsync(Stream stream, XmlReaderSettings settings = null)
		{
			using (var xml = XmlReader.Create(stream, settings ?? Procs.XmlReaderSettings))
				return await Load(xml).ToAsync();
		} // func LoadAsync

		public static async Task<IEnumerable<XmlCredentialItem>> LoadAsync(string fileName)
		{
			if (String.IsNullOrEmpty(fileName))
				throw new ArgumentNullException(nameof(fileName));

			// parse content
			using (var src = await Task.Run(() => OpenFileStream(fileName)))
				return await LoadAsync(src);
		} // func LoadAsync

		#endregion

		#region -- Write --------------------------------------------------------------

		private static Stream CreateFileStream(string fileName)
		{
			// create file name
			var src = (Stream)new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None);
			try
			{
				// is the stream compressed
				if (IsCompress(fileName))
					src = new GZipStream(src, CompressionLevel.Optimal, false);
				return src;
			}
			catch
			{
				src.Dispose();
				throw;
			}
		} // func CreateFileStream

		public void Write(XmlWriter xml)
		{
			void WriteAttr(string name, string value)
			{
				if (!String.IsNullOrEmpty(value))
					xml.WriteAttributeString(name, value);
			} // proc WriteAttr

			xml.WriteStartElement(entryName.LocalName);
			WriteAttr("uri", targetName);
			WriteAttr("uname", userName);
			WriteAttr("comment", comment);

			if (lastWritten != DateTime.MinValue)
				xml.WriteAttributeString("written", lastWritten.ToFileTimeUtc().ToString());

			if (Protector.HasValue(encryptedPassword))
			{
				if (encryptedPassword is byte[] passwordBytes)
				{
					WriteAttr("fmt", "base64");
					xml.WriteValue(Convert.ToBase64String(passwordBytes));
				}
				else
					xml.WriteValue(encryptedPassword.ToString());
			}

			xml.WriteEndElement();
		} // proc Write

		public static void Save(XmlWriter xml, IEnumerable<XmlCredentialItem> items)
		{
			xml.WriteStartDocument();
			xml.WriteStartElement(rootNodeName.LocalName);

			foreach (var c in items)
				c.Write(xml);

			xml.WriteEndElement();
			xml.WriteEndDocument();
		} // proc Save

		public static async Task SaveAsync(Stream stream, IEnumerable<XmlCredentialItem> items, XmlWriterSettings settings = null)
		{
			using (var xml = XmlWriter.Create(stream, settings ?? Procs.XmlWriterSettings))
				await Task.Run(() => Save(xml, items));
		} // proc SaveAsync

		public static async Task SaveAsync(string fileName, IEnumerable<XmlCredentialItem> items)
		{
			using (var dst = CreateFileStream(fileName))
				await SaveAsync(dst, items);
		} // proc SaveAsync

		#endregion
	} // class XmlCredentialItem

	#endregion
}
