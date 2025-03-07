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
using Neo.PerfectWorking.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Diagnostics.Eventing.Reader;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml;
using TecWare.DE.Stuff;

namespace Neo.PerfectWorking.Cred.Provider
{
	#region -- class FileCredentialProviderBase ---------------------------------------

	internal abstract class FileCredentialProviderBase : ICredentialProvider, IPwAutoSaveFile
	{
		#region -- class CredentialItemBase -------------------------------------------

		protected abstract class CredentialItemBase : ICredentialInfo, IXmlCredentialItem
		{
			[Flags]
			protected enum PropertyName
			{
				None = 0,

				UserName = 2,
				Comment = 4,
				EncryptedPassword = 8,
				LastWritten = 16,

				All = UserName | Comment | LastWritten | EncryptedPassword,

				IsVisible = 32
			} // enum PropertyName

			public event PropertyChangedEventHandler PropertyChanged;

			private readonly FileCredentialProviderBase provider;
			private bool isTouched = false;

			private PropertyName propertyChanged = PropertyName.None;

			public CredentialItemBase(FileCredentialProviderBase provider)
			{
				this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
			} // ctor

			public bool GetTouched()
			{
				var s = isTouched;
				isTouched = false;
				return s;
			} // func GetTouched

			protected abstract bool UpdateCore(IXmlCredentialItem other, object data);

			public bool Update(IXmlCredentialItem other, object data)
			{
				isTouched = true;
				return UpdateCore(other, data);
			} // func Update

			protected abstract bool RemoveCore();

			public bool Remove()
				=> RemoveCore();

			public void NotifyPropertyChanged()
			{
				OnPropertyChanged(propertyChanged, false);
				propertyChanged = PropertyName.None;
			} // proc NotifyPropertyChanged

			protected void OnPropertyChanged(PropertyName property, bool late)
			{
				if (late)
					propertyChanged |= (property & PropertyName.All);
				else
				{
					for (var i = 0; i < 4; i++)
					{
						var n = (PropertyName)(1 << (i + 1));
						if ((property & n) != 0)
							PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n.ToString()));
					}
				}
			} // proc OnPropertyChanged

			protected void OnPropertyChanged(XmlCredentialProperty props, bool late)
				=> OnPropertyChanged((PropertyName)props, late);

			public ICredentialProvider Provider => provider;

			public abstract object Image { get; }
			public abstract string TargetName { get; }
			public abstract string UserName { get; set; }
			public abstract string Comment { get; set; }
			public abstract DateTime LastWritten { get; }

			public SecureString GetPassword()
				=> provider.package.DecryptPassword(EncryptedPassword, provider.protector);

			public void SetPassword(SecureString password)
				=> EncryptedPassword = provider.package.EncryptPassword(password, provider.protector);

			public abstract object EncryptedPassword { get; set; }

			public abstract bool IsVisible { get; }
		} // class CredentialItemBase

		#endregion

		public event NotifyCollectionChangedEventHandler CollectionChanged;

		private readonly ICredPackage package;
		private readonly ICredentialProtector protector;
		private readonly string fileName;
		private readonly string shadowFileName; // Local copy for network files

		private DateTime lastModification;
		private readonly List<CredentialItemBase> items = new List<CredentialItemBase>();

		#region -- Ctor/Dtor ----------------------------------------------------------

		protected FileCredentialProviderBase(ICredPackage package, string fileName, ICredentialProtector protector, string shadowFileName)
		{
			this.package = package ?? throw new ArgumentNullException(nameof(package));
			this.fileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
			this.protector = protector ?? throw new ArgumentNullException(nameof(protector));
			this.shadowFileName = shadowFileName;

			lastModification = DateTime.MinValue;

			// Start load
			Load(true, true);
		} // ctor

		#endregion

		#region -- ICredentialProvider - members --------------------------------------

		protected void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
			=> CollectionChanged?.Invoke(this, e);

		protected abstract CredentialItemBase CreateNew(ICredentialInfo item);

		protected virtual void OnItemAdded(CredentialItemBase item)
			=> OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, new ICredentialInfo[] { item }));

		protected virtual void OnItemRemoved(CredentialItemBase item)
			=> OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, new ICredentialInfo[] { item }));

		ICredentialInfo ICredentialProvider.Append(ICredentialInfo newItem)
		{
			var item = Find(newItem.TargetName);
			if (item == null)
			{
				item = CreateNew(newItem);
				items.Add(item);
				OnItemAdded(item);

				return item;
			}
			else
			{
				item.Update(null, newItem);
				return item;
			}
		} // proc Append

		bool ICredentialProvider.Remove(string targetName)
		{
			var item = Find(targetName);
			if (item == null)
				return false;

			if (item.Remove())
			{
				OnItemRemoved(item);
				return true;
			}
			else
				return false;
		} // func Remove

		public NetworkCredential GetCredential(Uri uri, string authType)
			=> CredPackage.FindCredentials(Name, uri, this);

		protected IEnumerable<CredentialItemBase> GetItems()
			=> items;

		public IEnumerator<ICredentialInfo> GetEnumerator()
			=> items.Where(c => c.IsVisible).GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumerator();

		#endregion

		#region -- IPwAutoSaveFile - members ------------------------------------------

		#region -- Update Shadow File -------------------------------------------------

		protected string CopyFileSafe(FileInfo src, FileInfo dst, bool move)
		{
			var tries = 0;
			var lastException = (string)null;
			while (tries < 3)
			{
				try
				{
					src.CopyTo(dst.FullName, true);
					dst.LastWriteTimeUtc = src.LastWriteTimeUtc;
					if (move)
						src.Delete();
					return null;
				}
				catch (UnauthorizedAccessException e)
				{
					return e.Message;
				}
				catch (IOException e)
				{
					lastException = e.Message;
				}
				tries++;
				Thread.Sleep(tries * 1000);
			}
			return lastException;
		} // proc CopyFile

		private async Task<bool> CopyFileAsync(FileInfo src, FileInfo dst)
		{
			Log.Default.SourceFileCopy(Name, src.FullName);
			if (dst.Exists)
				dst.Attributes = dst.Attributes & ~FileAttributes.ReadOnly;
			try
			{
				var r = await Task.Run(() => CopyFileSafe(src, dst, false));
				if (r == null)
					return true;
				else
				{
					Log.Default.SourceFileCopyFailed(Name, src.FullName, r);
					return false;
				}
			}
			finally
			{
				dst.Attributes = dst.Attributes | FileAttributes.ReadOnly;
			}
		} // proc CopyFileSync

		private async Task<bool> UpdateShadowFileAsync()
		{
			if (shadowFileName == null)
				return false;

			var shadowFileInfo = new FileInfo(shadowFileName);
			var sourceFileInfo = new FileInfo(fileName);

			// run refresh in background
			await Task.Run(new Action(sourceFileInfo.Refresh));
			// check download needed
			if (sourceFileInfo.Exists)
			{
				if (!shadowFileInfo.Exists || sourceFileInfo.LastWriteTimeUtc > shadowFileInfo.LastWriteTimeUtc)
					return await CopyFileAsync(sourceFileInfo, shadowFileInfo);
			}
			else
				Log.Default.SourceFileIsOffline(Name, sourceFileInfo.FullName);

			return false;
		} // func PullShadowFileAsync

		#endregion

		#region -- Load ---------------------------------------------------------------

		protected virtual DateTime GetLastWriteTimeSafe()
		{
			try
			{
				var f = shadowFileName ?? fileName;
				return File.Exists(f) ? File.GetLastWriteTime(f) : DateTime.MinValue;
			}
			catch (IOException)
			{
				return DateTime.MinValue;
			}
		} // func GetLastWriteTimeSafe

		protected bool IsNewDataOnDisk()
			=> lastModification < GetLastWriteTimeSafe();

		protected virtual IEnumerable<IXmlCredentialItem> LoadItemsCore()
		{
			var loadFileName = shadowFileName ?? fileName;
			return File.Exists(loadFileName) ? XmlCredentialItem.Load(loadFileName) : Array.Empty<IXmlCredentialItem>();
		} // func LoadItemsCore

		protected abstract CredentialItemBase CreateItem(IXmlCredentialItem item);

		private void LoadItemsSync()
		{
			var itemsAdded = new List<ICredentialInfo>();
			var itemsRemoved = new List<ICredentialInfo>();

			var itemsCount = items.Count;

			// update items
			var lm = GetLastWriteTimeSafe();
			foreach (var cur in LoadItemsCore())
			{
				if (cur is CredentialItemBase item)
				{
					if (!items.Contains(item))
					{
						items.Add(item);
						itemsAdded.Add(item);
					}
				}
				else
				{
					item = Find(cur.TargetName);
					if (item == null) // new item
					{
						item = CreateItem(cur);
						items.Add(item);
						itemsAdded.Add(item);
					}
					else
					{
						var isVisibleOld = item.IsVisible;
						if (item.Update(cur, null)) // update current item
						{
							if (isVisibleOld)
								item.NotifyPropertyChanged();
							else
								itemsAdded.Add(item);
						}
					}
				}
			}

			// collect removed items
			for (var i = itemsCount - 1; i >= 0; i--)
			{
				if (!items[i].GetTouched())
				{
					itemsRemoved.Add(items[i]);
					items.RemoveAt(i);
				}
			}

			// notify changes
			if (itemsAdded.Count > 0)
				OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, itemsAdded));
			if (itemsRemoved.Count > 0)
				OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, itemsRemoved));

			lastModification = lm;
		} // proc LoadItemsSync

		protected void Load(bool force, bool allowSyncData)
		{
			Task<bool> shadowFileSynced = null;

			// try sync shadow file
			if (allowSyncData)
			{
				shadowFileSynced = UpdateShadowFileAsync();
				if (shadowFileSynced.Wait(200))
				{
					if (shadowFileSynced.Result)
						force = true;
					shadowFileSynced = null;
				}
			}

			// update items
			if (force || IsNewDataOnDisk())
				LoadItemsSync();

			// wait for shadow copy
			shadowFileSynced?.ContinueWith(
				t =>
				{
					if (t.Result) // reload new version
						Load(false, false);
				}
				, TaskContinuationOptions.ExecuteSynchronously
			);
		} // proc Load

		#endregion

		protected abstract void Save(bool force);

		void IPwAutoSaveFile.Reload()
			=> Load(false, true);

		void IPwAutoSaveFile.Save(bool force)
			=> Save(force);

		string IPwAutoSaveFile.FileName => fileName;

		#endregion

		protected CredentialItemBase Find(string targetName)
			=> items.FirstOrDefault(c => String.Compare(c.TargetName, targetName, StringComparison.OrdinalIgnoreCase) == 0);

		public ICredPackage Package => package;
		public ICredentialProtector Protector => protector;

		public string Name => Path.GetFileNameWithoutExtension(fileName);
		public abstract bool IsReadOnly { get; }
		public abstract bool IsModified { get; }
		public DateTime LastModificationTime => lastModification;

		public int Count => items.Count;

		protected string FileName => fileName;
		protected string ShadowFileName => shadowFileName;
	} // class FileCredentialProviderBase

	#endregion

	#region -- class FileReadOnlyCredentialProvider -----------------------------------

	internal sealed class FileReadOnlyCredentialProvider : FileCredentialProviderBase
	{
		#region -- class CredentialItem -----------------------------------------------

		private sealed class CredentialItem : CredentialItemBase
		{
			private IXmlCredentialItem item;

			#region -- Ctor/Dtor ------------------------------------------------------

			public CredentialItem(FileReadOnlyCredentialProvider provider, IXmlCredentialItem item)
				: base(provider)
			{
				this.item = item ?? throw new ArgumentNullException(nameof(item));
			} // ctor

			#endregion

			#region -- Update/Remove --------------------------------------------------

			protected override bool UpdateCore(IXmlCredentialItem other, object _)
			{
				if (other == null)
					throw new ArgumentNullException(nameof(other));

				var props = XmlCredentialItem.Compare(item, other, false);
				if (props == XmlCredentialProperty.None)
					return false;

				item = other;

				OnPropertyChanged(props, true);

				return true;
			} // func Update

			protected override bool RemoveCore()
				=> false;

			#endregion

			public override object Image => providerImage;
			public override string TargetName => item.TargetName;

			public override string UserName { get => item.UserName; set => throw GetReadOnlyException(); }
			public override string Comment { get => item.Comment; set => throw GetReadOnlyException(); }
			public override object EncryptedPassword { get => item.EncryptedPassword; set => throw GetReadOnlyException(); }
			public override DateTime LastWritten => item.LastWritten;

			public override bool IsVisible => true;
		} // class CredentialItem

		#endregion

		#region -- Ctor ---------------------------------------------------------------

		public FileReadOnlyCredentialProvider(ICredPackage package, string fileName, ICredentialProtector protector, string shadowFileName)
			: base(package, fileName, protector, shadowFileName)
		{
		} // ctor

		private static Exception GetReadOnlyException()
			=> new InvalidOperationException("This credential provider is readonly.");

		#endregion

		#region -- Load/Save ----------------------------------------------------------

		protected override CredentialItemBase CreateItem(IXmlCredentialItem item)
			=> new CredentialItem(this, item);

		protected override CredentialItemBase CreateNew(ICredentialInfo item)
			=> throw GetReadOnlyException();

		protected override void OnItemAdded(CredentialItemBase item)
			=> throw GetReadOnlyException();

		protected override void OnItemRemoved(CredentialItemBase item)
			=> throw GetReadOnlyException();

		protected override void Save(bool force)
			=> throw GetReadOnlyException();

		#endregion

		public override bool IsReadOnly => true;
		public override bool IsModified => false;

		// -- Static ------------------------------------------------------

		private static readonly DrawingImage providerImage;

		static FileReadOnlyCredentialProvider()
		{
			providerImage = new DrawingImage(
				new GeometryDrawing(
					new SolidColorBrush(Colors.DarkRed), null, Geometry.Parse("M7 14C5.9 14 5 13.1 5 12S5.9 10 7 10 9 10.9 9 12 8.1 14 7 14M12.6 10C11.8 7.7 9.6 6 7 6C3.7 6 1 8.7 1 12S3.7 18 7 18C9.6 18 11.8 16.3 12.6 14H16V18H20V14H23V10H12.6Z")
				)
			);
			providerImage.Freeze();
		} // sctor
	} // class FileReadOnlyCredentialProvider

	#endregion

	#region -- class FileCredentialProvider -------------------------------------------

	internal sealed class FileCredentialProvider : FileCredentialProviderBase
	{
		#region -- enum CredentialItemState -------------------------------------------

		private enum CredentialItemState
		{
			Original,
			New,
			Modified,
			Deleted
		} // enum CredentialItemState

		#endregion

		#region -- class CredentialItem -----------------------------------------------

		private sealed class CredentialItem : CredentialItemBase
		{
			private delegate T GetOriginalValueDelegate<T>(IXmlCredentialItem item);

			private IXmlCredentialItem original;
			private CredentialItemState state;

			// modified values
			private string newTargetName = null;
			private string userName = null;
			private string comment = null;
			private DateTime lastWritten = DateTime.MinValue;
			private object encryptedPassword = null;

			#region -- Ctor/Dtor ------------------------------------------------------

			public CredentialItem(FileCredentialProvider provider, CredentialItemState state, IXmlCredentialItem item)
				: base(provider)
			{
				if (state == CredentialItemState.Original)
				{
					original = item;
					this.state = CredentialItemState.Original;
				}
				else
				{
					this.state = state == CredentialItemState.Deleted ? CredentialItemState.Deleted : CredentialItemState.New;
					original = null;
					newTargetName = item.TargetName;
					userName = item.UserName;
					comment = item.Comment;
					lastWritten = item.LastWritten;
					encryptedPassword = item.EncryptedPassword;
				}
			} // ctor

			public CredentialItem(FileCredentialProvider provider, ICredentialInfo item)
				: base(provider)
			{
				original = null;
				state = CredentialItemState.New;

				newTargetName = item.TargetName ?? throw new ArgumentNullException(nameof(TargetName));
				UpdateCore(null, item);
			} // ctor

			#endregion

			#region -- Update/Remove --------------------------------------------------

			private bool UpdateOriginal(IXmlCredentialItem item)
			{
				if (state == CredentialItemState.Original) // original update
				{
					var props = XmlCredentialItem.Compare(original, item, false);
					if (props == XmlCredentialProperty.None)
						return false;

					original = item;
					OnPropertyChanged(props, true);
					return true;
				}
				else if (item.LastWritten > lastWritten) // incoming data is newer, clear changes
				{
					var props = XmlCredentialItem.Compare(item, this);
					original = item;
					OnPropertyChanged(props, true);
					SetState(CredentialItemState.Original);
					return props != XmlCredentialProperty.None;
				}
				else if (item.LastWritten < lastWritten) // incoming data is older, just set
				{
					original = item;
					if (state == CredentialItemState.New)
						SetState(CredentialItemState.Modified);
					return false;
				}
				else // data is equal, might become orignal
				{
					var props = XmlCredentialItem.Compare(item, this, false);
					original = item;
					SetState(props == XmlCredentialProperty.None ? CredentialItemState.Original : CredentialItemState.Modified);
					return false;
				}
			} // proc UpdateOriginal

			protected override bool UpdateCore(IXmlCredentialItem item, object data)
			{
				if (item is null && data is ICredentialInfo credInfo) // new item
				{
					userName = credInfo.UserName;
					comment = credInfo.Comment;
					lastWritten = credInfo.LastWritten;
					var provider = (FileCredentialProvider)Provider;
					encryptedPassword = provider.Package.EncryptPassword(credInfo.GetPassword(), provider.Protector);

					SetState(original is null ? CredentialItemState.New : CredentialItemState.Modified);
					OnPropertyChanged(PropertyName.All, true);
					return true;
				}
				else if (data is CredentialItemState s) // update item data with the specified state
				{
					if (s == CredentialItemState.Original)
						return UpdateOriginal(item);
					else// check for new state
					{
						PropertyName props;
						if (!(original is null))
						{
							if (original.LastWritten > item.LastWritten) // orignal is newer, nothing todo
								return false;
							props = (PropertyName)XmlCredentialItem.Compare(original, item);
						}
						else
							props = PropertyName.All;

						userName = item.UserName;
						comment = item.Comment;
						encryptedPassword = item.EncryptedPassword;
						lastWritten = item.LastWritten;

						OnPropertyChanged(props, true);
						SetState(original is null ? CredentialItemState.New : CredentialItemState.Modified);

						return props != PropertyName.None;
					}
				}
				else if (data is null) // original item
					return UpdateOriginal(item);
				else
					throw new ArgumentException();
			} // func UpdateCore

			protected override bool RemoveCore()
			{
				SetLastWritten();
				SetState(CredentialItemState.Deleted);
				return true;
			} // proc SetState

			#endregion

			#region -- SetState -------------------------------------------------------

			private void SetState(CredentialItemState state)
			{
				if (this.state != state)
				{
					this.state = state;

					switch (state)
					{
						case CredentialItemState.Original:
							if (original == null)
								throw new InvalidOperationException();
							newTargetName = null;
							userName = null;
							comment = null;
							lastWritten = DateTime.MinValue;
							encryptedPassword = null;
							break;

						case CredentialItemState.New:
							if (!(original is null))
								throw new InvalidOperationException();
							if (newTargetName is null)
								throw new InvalidOperationException();
							break;

						case CredentialItemState.Modified:
							if (original == null)
								throw new InvalidOperationException();

							newTargetName = null;
							break;

						case CredentialItemState.Deleted:
							userName = null;
							comment = null;
							lastWritten = DateTime.MinValue;
							encryptedPassword = null;
							break;
					}
					OnPropertyChanged(PropertyName.IsVisible, false);
				}
			} // proc SetState

			#endregion

			public CredentialItemState State => state;

			#region -- Properties -----------------------------------------------------

			private T GetValue<T>(ref T localValue, GetOriginalValueDelegate<T> originalValue)
			{
				return state == CredentialItemState.New || state == CredentialItemState.Modified
					? localValue
					: originalValue(original);
			} // func GetValue

			private void SetValueCore<T>(ref T localValue, T value, PropertyName propertyName)
			{
				localValue = value;
				OnPropertyChanged(propertyName, false);
				((FileCredentialProvider)Provider).SetModified();
			} // proc SetValueCore

			private static bool EqualsProperty(PropertyName propertyName, object a, object b)
				=> propertyName == PropertyName.EncryptedPassword ? Cred.Provider.Protector.EqualValue(a, b) : Equals(a, b);

			private bool SetValue<T>(ref T localValue, GetOriginalValueDelegate<T> originalValue, T value, PropertyName propertyName)
			{
				if (state == CredentialItemState.New)
				{
					if (EqualsProperty(propertyName, localValue, value))
						return false;

					// update local copy
					SetValueCore(ref localValue, value, propertyName);
					return true;
				}
				else if (state == CredentialItemState.Original)
				{
					if (EqualsProperty(propertyName, originalValue(original), value))
						return false;

					// switch state to copy
					SetState(CredentialItemState.Modified);
					// update local copy
					SetValueCore(ref localValue, value, propertyName);
					return true;
				}
				else if (state == CredentialItemState.Modified)
				{
					if (EqualsProperty(propertyName, localValue, value))
						return false;

					// update local copy
					localValue = value;

					// switch state back to original
					if (EqualsProperty(PropertyName.UserName, original.UserName, userName)
						&& EqualsProperty(PropertyName.Comment, original.Comment, comment)
						&& EqualsProperty(PropertyName.EncryptedPassword, original.EncryptedPassword, encryptedPassword))
					{
						SetState(CredentialItemState.Original);
					}

					OnPropertyChanged(propertyName, false);
					return true;
				}
				else
					throw new InvalidOperationException();
			} // proc SetValue

			private static string GetUserName(IXmlCredentialItem original)
				=> original.UserName;

			private static string GetComment(IXmlCredentialItem original)
				=> original.Comment;

			private static DateTime GetLastWritten(IXmlCredentialItem original)
				=> original.LastWritten;

			private static object GetEncryptedPassword(IXmlCredentialItem original)
				=> original.EncryptedPassword;

			private void SetLastWritten()
				=> SetValueCore(ref lastWritten, DateTime.UtcNow, PropertyName.LastWritten);

			public override string TargetName => state == CredentialItemState.New ? newTargetName : original.TargetName;
			public override object Image => providerImage;

			public override string UserName
			{
				get => GetValue(ref userName, GetUserName);
				set
				{
					if (SetValue(ref userName, GetUserName, value, PropertyName.UserName))
						SetLastWritten();
				}
			} // prop UserName

			public override string Comment
			{
				get => GetValue(ref comment, GetComment);
				set
				{
					if (SetValue(ref comment, GetComment, value, PropertyName.Comment))
						SetLastWritten();
				}
			} // prop Comment

			public override DateTime LastWritten => GetValue(ref lastWritten, GetLastWritten);

			public override object EncryptedPassword
			{
				get => GetValue(ref encryptedPassword, GetEncryptedPassword);
				set
				{
					if (SetValue(ref encryptedPassword, GetEncryptedPassword, value, PropertyName.EncryptedPassword))
						SetLastWritten();
				}
			} // prop EncryptedPassword

			#endregion

			public override bool IsVisible => state != CredentialItemState.Deleted;
		} // class CredentialItem

		#endregion

		private readonly string changeFileName;
		private bool isModified = false;

		#region -- Ctor/Dtor ----------------------------------------------------------

		public FileCredentialProvider(CredPackage package, string fileName, ICredentialProtector protector, string shadowFileName)
			: base(package, fileName, protector, shadowFileName)
		{
			if (shadowFileName == null)
				changeFileName = null;
			else
			{
				var i = shadowFileName.LastIndexOf("-shadow");
				changeFileName = shadowFileName.Substring(0, i) + "-changes" + shadowFileName.Substring(i + 7);
			}
		} // ctor

		#endregion

		#region -- Load/Save ----------------------------------------------------------

		private void SetModified()
			=> isModified = true;

		protected override DateTime GetLastWriteTimeSafe()
		{
			if (changeFileName != null)
			{
				try
				{
					var dtChangeFile = DateTime.MinValue;
					var dtShadowFile = DateTime.MinValue;

					if (File.Exists(changeFileName))
						dtChangeFile = File.GetLastWriteTime(changeFileName);
					else if (File.Exists(ShadowFileName))
						dtShadowFile = File.GetLastWriteTime(ShadowFileName);

					return dtShadowFile < dtChangeFile ? dtChangeFile : dtShadowFile;
				}
				catch (IOException)
				{
					return DateTime.MinValue;
				}
			}
			else
				return base.GetLastWriteTimeSafe();
		} // func GetLastWriteTimeSafe

		private void StartSync()
		{

		}

		protected override IEnumerable<IXmlCredentialItem> LoadItemsCore()
		{
			var hasLocalChanges = false;

			// load shadow content
			foreach (var cur in base.LoadItemsCore())
				yield return cur;

			// load local copy
			if (changeFileName != null)
			{
				var lastModification = File.GetLastWriteTimeUtc(changeFileName);
				using var xml = XmlReader.Create(changeFileName, Procs.XmlReaderSettings);

				xml.MoveToContent();
				if (xml.Name != "changes")
					throw new ArgumentException();

				xml.Read();

				while (xml.NodeType == XmlNodeType.Element)
				{
					var state = GetStateFromName(xml.Name);
					var data = XmlCredentialItem.Read(xml, xml.GetAttribute("uri"), lastModification);

					var item = Find(data.TargetName);
					if (item == null)
						yield return new CredentialItem(this, state, data);
					else
						item.Update(data, state);
					hasLocalChanges = true;
				}
			}

			if (hasLocalChanges)
				StartSync();
		} // proc LoadItemsCore

		protected override CredentialItemBase CreateItem(IXmlCredentialItem item)
			=> new CredentialItem(this, CredentialItemState.Original, item);

		protected override CredentialItemBase CreateNew(ICredentialInfo item)
			=> new CredentialItem(this, item);

		protected override void OnItemAdded(CredentialItemBase item)
		{
			SetModified();
			base.OnItemAdded(item);
		} // proc OnItemAdded

		protected override void OnItemRemoved(CredentialItemBase item)
		{
			SetModified();
			base.OnItemRemoved(item);
		} // proc OnItemRemoved

		private string GetElementName(CredentialItemState state)
		{
			return state switch
			{
				CredentialItemState.New => "n",
				CredentialItemState.Modified => "m",
				CredentialItemState.Deleted => "d",
				_ => throw new ArgumentOutOfRangeException(nameof(state), state, "State is not persistable."),
			};
		} // func GetElementName

		private CredentialItemState GetStateFromName(string name)
		{
			if (String.IsNullOrEmpty(name))
				throw new ArgumentNullException(nameof(name));

			return name[1] switch
			{
				'n' => CredentialItemState.New,
				'm' => CredentialItemState.Modified,
				'd' => CredentialItemState.Deleted,
				_ => throw new ArgumentOutOfRangeException(nameof(name), name, "Can not read state."),
			};
		} // func GetStateFromName

		private void SaveChangeMode()
		{
			var hasChanges = false;

			// Persist changes
			var tmpFileName = changeFileName + "~";
			using var xml = XmlWriter.Create(tmpFileName, Procs.XmlWriterSettings);
			{
				xml.WriteStartDocument();
				xml.WriteStartElement("changes");
				foreach (var cur in GetItems().Cast<CredentialItem>().Where(c => c.State != CredentialItemState.Original))
				{
					XmlCredentialItem.Write(xml, cur, GetElementName(cur.State));
					hasChanges = true;
				}
				xml.WriteEndElement();
				xml.WriteEndDocument();
			}

			// Update original file
			var m = CopyFileSafe(new FileInfo(tmpFileName), new FileInfo(changeFileName), true);
			if (!(m is null))
				Log.Default.FileProviderSaveFailed(Name, changeFileName, m);

			if (hasChanges)
				StartSync();
		} // proc SaveChangeMode

		private void SaveDirectMode()
		{
			var fileName = FileName;

			// write content in file
			var tmpFileName = fileName + "~";
			XmlCredentialItem.Save(tmpFileName, this.Cast<IXmlCredentialItem>());

			// Update original file
			var m = CopyFileSafe(new FileInfo(tmpFileName), new FileInfo(fileName), true);
			if (m is null)
			{
				// reload data
				Load(true, false);
			}
			else
				Log.Default.FileProviderSaveFailed(Name, fileName, m);

			// mark unmodified
			isModified = false;
		} // proc SaveDirectMode

		protected override void Save(bool force)
		{
			if (changeFileName != null)
				SaveChangeMode();
			else
				SaveDirectMode();
		} // proc Save

		#endregion

		public override bool IsReadOnly => false;
		public override bool IsModified => isModified;

		// -- Static ------------------------------------------------------

		private static readonly DrawingImage providerImage;

		static FileCredentialProvider()
		{
			providerImage = new DrawingImage(
				new GeometryDrawing(
					new SolidColorBrush(Colors.DarkGreen), null, Geometry.Parse("M7 14C5.9 14 5 13.1 5 12S5.9 10 7 10 9 10.9 9 12 8.1 14 7 14M12.6 10C11.8 7.7 9.6 6 7 6C3.7 6 1 8.7 1 12S3.7 18 7 18C9.6 18 11.8 16.3 12.6 14H16V18H20V14H23V10H12.6Z")
				)
			);
			providerImage.Freeze();
		} // sctor
	}

	#endregion
}
