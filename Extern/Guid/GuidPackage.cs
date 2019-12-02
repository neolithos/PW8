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
using System.ComponentModel;
using Neo.IronLua;
using Neo.PerfectWorking.Data;
using Neo.PerfectWorking.Stuff;

namespace Neo.PerfectWorking
{
	public sealed class GuidPackage : PwPackageBase, INotifyPropertyChanged
	{
		public const string CurrentFormatPropertyName = "Format";

		public event PropertyChangedEventHandler PropertyChanged;

		private Guid currentGuid;
		private string currentFormattedGuid = null;

		private readonly LuaTable guidProperties;

		public GuidPackage(IPwGlobal global)
			: base(global, nameof(GuidPackage))
		{
			guidProperties = global.UserLocal.GetLuaTable("Guid");
			guidProperties.PropertyChanged += GuidProperties_PropertyChanged;

			CurrentGuid = Guid.NewGuid();

			global.RegisterObject(this, nameof(GuidPackagePane), new GuidPackagePane(this));
		} // ctor
		
		private void GuidProperties_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == CurrentFormatPropertyName)
				OnPropertyChanged(nameof(CurrentFormat));
		} // event GuidProperties_PropertyChanged

		private void OnPropertyChanged(string propertyName)
		{
			switch (propertyName)
			{
				case nameof(CurrentGuid):
				case nameof(CurrentFormat):
					RefreshCurrentFormattedGuid();
					break;
			}
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		} // proc OnPropertyChanged

		public string NewGuid()
		{
			CurrentGuid = Guid.NewGuid();
			return CurrentFormattedGuid;
		} // func NewGuid

		public string GuidToString(Guid guid, char format)
		{
			switch (format)
			{
				case 'b':
				case 'd':
				case 'n':
				case 'p':
					return guid.ToString(new string(format, 1)).ToLower();
				case 'B':
				case 'D':
				case 'N':
				case 'P':
					return guid.ToString(new string(format, 1)).ToUpper();
				case 'c':
					return GuidToStringCStyle(guid, false);
				case 'C':
					return GuidToStringCStyle(guid, true);
				default:
					return "";
			}
		} // func GuidToString

		private string GuidToStringCStyle(Guid guid, bool upper)
		{
			var bytes = guid.ToByteArray();
			return String.Format(
				upper
					? "{{ 0x{3:X2}{2:X2}{1:X2}{0:X2}, 0x{5:X2}{4:X2}, 0x{7:X2}{6:X2}, {{ 0x{8:X2}, 0x{9:X2}, 0x{10:X2}, 0x{11:X2}, 0x{12:X2}, 0x{13:X2}, 0x{14:X2}, 0x{15:X2} }} }}"
					: "{{ 0x{3:x2}{2:x2}{1:x2}{0:x2}, 0x{5:x2}{4:x2}, 0x{7:x2}{6:x2}, {{ 0x{8:x2}, 0x{9:x2}, 0x{10:x2}, 0x{11:x2}, 0x{12:x2}, 0x{13:x2}, 0x{14:x2}, 0x{15:x2} }} }}",
				bytes[0], bytes[1], bytes[2], bytes[3], bytes[4], bytes[5], bytes[6], bytes[7], bytes[8], bytes[9], bytes[10], bytes[11], bytes[12], bytes[13], bytes[14], bytes[15]
			);
		} // func GuidToStringCStyle

		private void RefreshCurrentFormattedGuid()
		{
			currentFormattedGuid = GuidToString(currentGuid, CurrentFormat);
			OnPropertyChanged(nameof(CurrentFormattedGuid));
		} // proc RefreshCurrentFormattedGuid

		public Guid CurrentGuid
		{
			get => currentGuid;
			set
			{
				if (currentGuid != value)
				{
					currentGuid = value;
					OnPropertyChanged(nameof(CurrentGuid));
				}
			}
		} // prop CurrentGuid

		public string CurrentFormattedGuid => currentFormattedGuid;

		public char CurrentFormat
		{
			get => guidProperties.GetOptionalValue(CurrentFormatPropertyName, "D", rawGet: true)[0];
			set => guidProperties.SetMemberValue(CurrentFormatPropertyName, new string(value, 1), rawSet: true);
		} // prop CurrentFormat
	} // class GuidPackage
}
