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
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Security;
using System.Windows.Media;
using System.Xml;
using System.Xml.Linq;
using Neo.PerfectWorking.Data;
using Neo.PerfectWorking.Stuff;

namespace Neo.PerfectWorking.Cred.Provider
{
	#region -- class FileCredentialProvider ---------------------------------------------

	internal sealed class FileCredentialProvider : ICredentialProvider, IPwAutoSaveFile
	{
		private static readonly XName rootNodeName = "passwords";
		private static readonly XName entryName = "entry";

		#region -- class FileCredentialInfo ---------------------------------------------

		internal sealed class FileCredentialInfo : ICredentialInfo
		{
			public event PropertyChangedEventHandler PropertyChanged;

			private readonly FileCredentialProvider provider;
			private readonly string targetName;
			private string userName;
			private string comment;
			private DateTime lastWritten;
			private object encryptedPassword;

			public FileCredentialInfo(FileCredentialProvider provider, string targetName)
			{
				this.provider = provider;
				this.targetName = targetName;
			} // ctor

			public void Read(XmlReader xml)
			{
				string GetAttr(string name)
				{
					var r = xml.GetAttribute(name);
					return String.IsNullOrEmpty(r) ? null : r;
				}

				DateTime ConvertDateTime(string value)
					=> value != null && Int64.TryParse(value, out var dt) ?
						DateTime.FromFileTimeUtc(dt) :
						DateTime.MinValue;

				UserName = GetAttr("uname");
				Comment = GetAttr("comment");

				var tmpLastWritten = ConvertDateTime(GetAttr("written"));
				if (lastWritten != tmpLastWritten)
				{
					lastWritten = tmpLastWritten;
					OnPropertyChanged(nameof(LastWritten));
				}

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
							var contentString = xml.ReadContentAsString();
							if (fmt == "base64")
								encryptedPassword = Convert.FromBase64String(contentString);
							else
								encryptedPassword = contentString;
							break;
						default:
							xml.Skip();
							break;
					}
					xml.ReadEndElement();
				}
			} // proc Read

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
				if (Protector.HasValue(encryptedPassword))
				{
					if (lastWritten != DateTime.MinValue)
						xml.WriteAttributeString("written", lastWritten.ToFileTimeUtc().ToString());
					if (encryptedPassword is byte[])
					{
						WriteAttr("fmt", "base64");
						xml.WriteValue(Convert.ToBase64String((byte[])encryptedPassword));
					}
					else
						xml.WriteValue(encryptedPassword.ToString());
				}
				xml.WriteEndElement();
			} // proc Write

			private void OnPropertyChanged(string propertyName)
				=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

			public void SetPassword(SecureString password)
			{
				if (password == null || password.Length == 0)
					encryptedPassword = null;
				else
				{
					var tmp = provider.package.EncryptPassword(password, provider.protector);
					if (!Protector.EqualValue(tmp, encryptedPassword))
					{
						encryptedPassword = tmp;
						provider.SetModified();
					}
				}
			} // proc SetPassword

			public SecureString GetPassword()
				=> provider.package.DecryptPassword(encryptedPassword, provider.protector);

			private void SetProperty(string propertyName, ref string value, string newValue)
			{
				if (value != newValue)
				{
					value = newValue;
					lastWritten = DateTime.UtcNow;
					provider.SetModified();
					OnPropertyChanged(propertyName);
					OnPropertyChanged(nameof(LastWritten));
				}
			} // proc SetProperty

			public string TargetName => targetName;

			public string UserName
			{
				get => userName;
				set => SetProperty(nameof(UserName), ref userName, value);
			} // prop TargetName

			public string Comment
			{
				get => comment;
				set => SetProperty(nameof(Comment), ref comment, value);
			} // prop TargetName

			public DateTime LastWritten => lastWritten;

			public object Image => providerImage;

			public ICredentialProvider Provider => provider;
		} // class FileCredentialInfo

		#endregion

		public event NotifyCollectionChangedEventHandler CollectionChanged;

		private readonly CredPackage package;
		private readonly string fileName;
		private readonly bool isReadOnly;
		private readonly ICredentialProtector protector;
		private readonly List<FileCredentialInfo> items = new List<FileCredentialInfo>();

		private DateTime lastModification;
		private bool isModified = false;
		private bool isLoading = false;

		#region -- Ctor/Dtor ------------------------------------------------------------

		public FileCredentialProvider(CredPackage package, string fileName, bool readOnly, ICredentialProtector protector)
		{
			this.package = package;
			this.fileName = fileName;
			this.isReadOnly = readOnly;
			this.protector = protector ?? Protector.NoProtector;

			Load();
		} // ctor

		#endregion

		#region -- Load/Save ------------------------------------------------------------

		private void CheckReadOnly()
		{
			if (isReadOnly)
				throw new InvalidOperationException("This credential provider is readonly.");
		} // proc CheckReadOnly

		private void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
			=> CollectionChanged?.Invoke(this, e);

		void IPwAutoSaveFile.Reload()
			=> Load();

		void IPwAutoSaveFile.Save(bool force)
		{
			if (!isReadOnly && (isModified || force))
				Save();
		} // proc IPwAutoSaveFile.Save

		private void Load()
		{
			if (File.Exists(fileName))
			{
				lastModification = File.GetLastWriteTime(fileName);
				isLoading = true;
				try
				{
					using (var xml = XmlReader.Create(fileName, new XmlReaderSettings() { IgnoreComments = true, IgnoreWhitespace = true }))
					{
						xml.ReadStartElement(rootNodeName.LocalName);
						while (xml.NodeType == XmlNodeType.Element)
						{
							var targetName = xml.GetAttribute("uri");
							if (targetName == null)
								xml.Skip();
							else
							{
								var item = FindItemByUri(targetName);
								if (item != null)
									item.Read(xml);
								else
								{
									var idx = items.Count;
									item = new FileCredentialInfo(this, targetName);
									item.Read(xml);
									items.Add(item);
									OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, idx));
								}
							}
						}
					}
				}
				finally
				{
					isLoading = false;
				}
			}

			isModified = false;
		} // proc Load

		private void Save()
		{
			using (var xml = XmlWriter.Create(fileName, new XmlWriterSettings() { Indent = true, NewLineHandling = NewLineHandling.Entitize }))
			{
				xml.WriteStartDocument();
				xml.WriteStartElement(rootNodeName.LocalName);

				foreach (var c in items)
					c.Write(xml);

				xml.WriteEndElement();
				xml.WriteEndDocument();
			}

			isModified = false;
			lastModification = File.GetLastWriteTime(fileName);
		} // proc Save

		private void SetModified()
		{
			if (isLoading)
				return;

			CheckReadOnly();
			isModified = true;
		} // proc SetModified

		#endregion

		#region -- Append, Remove -------------------------------------------------------

		public ICredentialInfo Append(ICredentialInfo newItem)
		{
			CheckReadOnly();

			// find existing item
			var targetName = newItem.TargetName;
			var baseTargetName = targetName;
			var targetNameCounter = 0;
			while (true)
			{
				if (FindItemByUri(targetName) == null)
					break;
				else
					targetName = baseTargetName + "/" + (++targetNameCounter).ToString();
			}


			// create item
			var currentItem = new FileCredentialInfo(this, targetName)
			{
				Comment = newItem.Comment,
				UserName = newItem.UserName
			};
			currentItem.SetPassword(newItem.GetPassword());
			
			// add
			var index = items.Count;
			items.Add(currentItem);
			OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, currentItem, index));

			return currentItem;
		} // proc Append

		public bool Remove(string targetName)
		{
			CheckReadOnly();

			var index = FindItemIndexByUri(targetName);
			if (index >= 0)
			{
				var oldItem = items[index];
				items.RemoveAt(index);
				OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, oldItem, index));
				SetModified();
				return true;
			}
			else
				return false;
		} // func Remove

		private FileCredentialInfo FindItemByUri(string targetName)
		{
			var idx = FindItemIndexByUri(targetName);
			return idx >= 0 ? items[idx] : null;
		} // func FindItemByUri

		private int FindItemIndexByUri(string targetName)
			=> items.FindIndex(c => String.Compare(c.TargetName, targetName, StringComparison.OrdinalIgnoreCase) == 0);

		#endregion

		public NetworkCredential GetCredential(Uri uri, string authType)
			=> CredPackage.FindCredentials(Name, uri, items);

		public IEnumerator<ICredentialInfo> GetEnumerator()
			=> items.GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumerator();

		DateTime IPwAutoSaveFile.LastModificationTime => lastModification;
		bool IPwAutoSaveFile.IsModified => isModified;
		string IPwAutoSaveFile.FileName => fileName;

		public string Name => Path.GetFileNameWithoutExtension(fileName);
		public bool IsReadOnly => isReadOnly;

		// -- Static ------------------------------------------------------

		private static DrawingImage providerImage;

		static FileCredentialProvider()
		{
			providerImage = new DrawingImage(
				new GeometryDrawing(
					new SolidColorBrush(Colors.Gray), null, Geometry.Parse("M13,9V3.5L18.5,9M6,2C4.89,2 4,2.89 4,4V20A2,2 0 0,0 6,22H18A2,2 0 0,0 20,20V8L14,2H6Z")
				)
			);
			providerImage.Freeze();
		} // sctor
	} // class FileCredentialProvider

	#endregion
}
