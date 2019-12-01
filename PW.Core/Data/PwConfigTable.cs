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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Neo.IronLua;
using Neo.PerfectWorking.Stuff;
using TecWare.DE.Stuff;

namespace Neo.PerfectWorking.Data
{
	public sealed class PwConfigTable : LuaTable, IPwAutoSaveFile, IDisposable
	{
		const string rootNodeName = "config";
		const string xElementName = "x";
		const string valueElementName = "v";
		const string tableElementName = "t";
		const string indexKeyName = "i";
		const string memberKeyName = "m";
		const string typeAttributeName = "t";

		#region -- class TableAgent ---------------------------------------------------

		private class TableAgent
		{
			private readonly TableAgent parent;
			private readonly string propertyName;
			private readonly LuaTable table;
			private readonly PropertyChangedEventHandler propertyChanged;

			private readonly Dictionary<string, TableAgent> childAgents = new Dictionary<string, TableAgent>();

			public TableAgent(TableAgent parent, string propertyName, LuaTable table)
			{
				this.parent = parent;
				this.table = table;
				this.propertyName = propertyName;

				// initialize events
				propertyChanged = (sender, e) =>
				{
					CheckValue(e.PropertyName, table.GetMemberValue(e.PropertyName, rawGet: true));
					SetModified();
				};
				table.PropertyChanged += propertyChanged;
			} // ctor

			private void SetModified()
			{
				if (parent == null)
					((PwConfigTable)table).SetModified();
				else
					parent.SetModified();
			} // proc SetModified

			public void Remove()
			{
				// remove watcher
				while (childAgents.Count > 0)
					childAgents.Values.First().Remove();

				// remove event
				table.PropertyChanged -= propertyChanged;

				// remove self
				if (parent != null)
					parent.childAgents.Remove(propertyName);
			} // proc Remove

			private void CheckValues()
			{
				foreach (var m in table.Members)
					CheckValue(m.Key, m.Value);
			} // proc CheckValues

			private void CheckValue(string propertyName, object value)
			{
				void CheckValueInternal()
				{
					// check value type
					var valueType = value.GetType();
					if (valueType == typeof(LuaTable))
					{
						var newAgent = new TableAgent(this, propertyName, (LuaTable)value);
						try
						{
							newAgent.CheckValues();
							childAgents.Add(propertyName, newAgent);
						}
						catch
						{
							newAgent.Remove();
							throw;
						}
					}
					else // filter types
					{
						switch (Type.GetTypeCode(valueType))
						{
							case TypeCode.DBNull:
							case TypeCode.Empty:
								throw new ArgumentException($"Type of '{propertyName}' can not be persist in the configuration system.");
							case TypeCode.Object:
								if (valueType == typeof(XElement))
									break;
								throw new ArgumentException($"Type '{valueType.Name}' of '{propertyName}' can not be persist in the configuration system.");
						}
					}
				} // proc CheckValueInternal

				if (childAgents.TryGetValue(propertyName, out var child)) // this property is watched
				{
					if (!ReferenceEquals(value, child.table))
					{
						// remove watcher
						child.Remove();
						// add new watcher
						if (!(value is null))
							CheckValueInternal();
					}
				}
				else if (!(value is null)) // this property is new
					CheckValueInternal();
			} // proc CheckValue
		} // class TableAgent

		#endregion

		private readonly TableAgent tableAgent;
		private readonly FileInfo fileInfo;
		private DateTime lastFileModification;
		private DateTime lastDataModification;
		private bool isLoading = false;

		#region -- Ctor/Dtor ----------------------------------------------------------

		public PwConfigTable(string fileName)
		{
			this.fileInfo = new FileInfo(Path.GetFullPath(fileName));

			// start table agent
			this.tableAgent = new TableAgent(null, String.Empty, this);

			this.lastFileModification = DateTime.MinValue;
			this.lastDataModification = DateTime.MinValue;

			Load();
		} // ctor

		public void Dispose()
		{
			tableAgent.Remove();
		} // proc Dispose

		public void SetModified()
		{
			if (!IsLoading)
				lastDataModification = DateTime.Now;
		} // proc SetModified

		#endregion

		#region -- ReadProperties -----------------------------------------------------

		private static XmlReader ReadNode(XmlReader xml)
		{
			if (!xml.Read())
				throw new IOException("Unexpected EOF.");
			return xml;
		} // proc ReadNode

		private static LuaTable ReadProperties(XmlReader xml, LuaTable table)
		{
			object GetKey()
			{
				var idx = xml.GetAttribute(indexKeyName);
				if (Int32.TryParse(idx, out var i))
					return i;
				else
				{
					var m = xml.GetAttribute(memberKeyName);
					if (!String.IsNullOrEmpty(m))
						return m;
					else
						return null;
				}
			} // func GetKey

			void Skip()
				=> xml.Skip();

			var isEmptyTable = false;
			if (table == null)
			{
				table = new LuaTable();
				isEmptyTable = true;
			}
			else
				isEmptyTable = table.Members.Count == 0 && table.ArrayList.Count == 0;
			
			var deleteKeys = isEmptyTable ? null : new List<object>(table.Values.Keys); // processed keys

			while (xml.NodeType == XmlNodeType.Element)
			{
				var key = GetKey();
				if (key == null)
					Skip();
				else
				{
					deleteKeys?.Remove(key); // set key as readed

					switch (xml.LocalName)
					{
						case valueElementName:
							if (xml.IsEmptyElement)
							{
								if (!isEmptyTable)
									table[key] = null;

								ReadNode(xml);
							}
							else
							{
								var t = xml.GetAttribute("t");
								var valueType = LuaType.GetType(t)?.Type ?? typeof(string);
								switch (ReadNode(xml).NodeType)
								{
									case XmlNodeType.Text:
									case XmlNodeType.CDATA:
										table[key] = Procs.ChangeType(xml.ReadContentAsString(), valueType);
										break;
									default:
										Skip();
										break;
								}
								xml.ReadEndElement();
							}
							break;
						case xElementName:
							if (xml.IsEmptyElement)
							{
								if (!isEmptyTable)
									table[key] = null;

								ReadNode(xml);
							}
							else
							{
								table[key] = XNode.ReadFrom(ReadNode(xml));
								xml.ReadEndElement();
							}
							break;
						case tableElementName:
							if (xml.IsEmptyElement)
							{
								ReadNode(xml);
								table[key] = new LuaTable();
							}
							else
							{
								table[key] = ReadProperties(ReadNode(xml), table[key] as LuaTable);
								xml.ReadEndElement();
							}
							break;
						default:
							Skip();
							break;
					}
				}
			}

			if (deleteKeys != null)
			{
				foreach (var key in deleteKeys)
					table[key] = null;
			}

			return table;
		} // func ReadProperties

		#endregion

		#region -- WriteProperties ----------------------------------------------------

		private static void WriteProperties(XmlWriter xml, LuaTable table)
		{
			Tuple<string, string> GetMemberAttribute(object key)
			{
				if (key is int idx)
					return new Tuple<string, string>(indexKeyName, idx.ChangeType<string>());
				else if (key is string propertyName)
					return new Tuple<string, string>(memberKeyName, propertyName.ChangeType<string>());
				else
					return null;
			} // func WriteMemberAttribute

			void WriteValue(string elementName, Tuple<string, string> attr, Action writeValue)
			{
				xml.WriteStartElement(elementName);
				xml.WriteAttributeString(attr.Item1, attr.Item2);
				writeValue();
				xml.WriteEndElement();
			}

			foreach (var m in table) // we save all values, and ignore unpersistable types
			{
				var attr = GetMemberAttribute(m.Key); // get the member
				if (attr != null)
				{
					var valueType = m.Value.GetType();
					switch (TypeInfo.GetTypeCode(valueType))
					{
						case TypeCode.Empty:
						case TypeCode.DBNull:
							break; // ignore
						case TypeCode.Object:
							if (valueType == typeof(XElement))
								WriteValue(xElementName, attr, () => ((XElement)m.Value).WriteTo(xml));
							else if (valueType == typeof(LuaTable))
								WriteValue(tableElementName, attr, () => WriteProperties(xml, (LuaTable)m.Value));
							break; // ignore
						default:
							WriteValue(valueElementName, attr, () =>
							{
								xml.WriteAttributeString(typeAttributeName, LuaType.GetType(valueType).AliasOrFullName);
								var v = m.Value.ChangeType<string>();
								if (v.IndexOfAny(new char[] { '<', '>', '\n' }) >= 0) // check for invalid chars
									xml.WriteCData(v);
								else
									xml.WriteValue(v);

							});
							break;
					}
				}
			}
		} // proc WriteProperties

		#endregion

		#region -- Load/Save ----------------------------------------------------------

		private void Load()
		{
			if (IsLoading)
				throw new InvalidOperationException();

			isLoading = true;
			try
			{
				fileInfo.Refresh();
				if (fileInfo.Exists)
				{
					try
					{
						using (var xml = XmlReader.Create(fileInfo.FullName, new XmlReaderSettings() { IgnoreComments = true, IgnoreProcessingInstructions = true, IgnoreWhitespace = true }))
						{
							// parse content
							xml.ReadStartElement(rootNodeName);
							if (!xml.IsEmptyElement)
								ReadProperties(xml, this);
						}

						lastFileModification = fileInfo.LastWriteTime;
					}
					catch
					{
						lastFileModification = DateTime.MinValue;
						throw;
					}
				}
				else
					lastFileModification = DateTime.MinValue;
			}
			finally
			{
				isLoading = false;
			}
		} // proc Load

		public void Save(bool force = false)
		{
			if (!force && !IsModified)
				return;

			// todo: write in bak and replace content
			using (var xml = XmlWriter.Create(fileInfo.FullName, new XmlWriterSettings() { Indent = true, IndentChars = "\t", Encoding = Encoding.UTF8, NewLineHandling = NewLineHandling.Entitize, NewLineChars = Environment.NewLine, NewLineOnAttributes = false }))
			{
				xml.WriteStartDocument();
				xml.WriteStartElement(rootNodeName);
				WriteProperties(xml, this);
				xml.WriteEndElement();
				xml.WriteEndDocument();
			}

			fileInfo.Refresh();
			lastFileModification = fileInfo.LastWriteTime;
		} // proc Save

		void IPwAutoSaveFile.Reload()
			=> Load();

		#endregion

		public bool IsModified => lastFileModification < lastDataModification;
		public bool IsLoading => isLoading;
		public string FileName => fileInfo.FullName;

		DateTime IPwAutoSaveFile.LastModificationTime => lastFileModification;
	} // class PwConfigTable
}
