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

namespace Neo.PerfectWorking.Stuff
{
	public class CredCompareValue
	{
		private readonly string credentialProvider;
		private readonly string targetName;
		private readonly int targetNameServerLength;
		private readonly string userName;

		public CredCompareValue(Uri uri)
		{
			if (uri.IsAbsoluteUri)
			{
				credentialProvider = uri.Scheme;
				targetName = uri.Host + uri.AbsolutePath;
				targetNameServerLength = uri.Host.Length;
				userName = String.IsNullOrWhiteSpace(uri.UserInfo) ? null : uri.UserInfo;
			}
			else
			{
				credentialProvider = null;

				var path = uri.OriginalString;

				// user name
				var p = path.IndexOf('@');
				if (p >= 0)
				{
					userName = path.Substring(0, p);
					path = path.Substring(p + 1);
				}

				// targetname
				p = path.IndexOf('/');
				targetName = path;
				targetNameServerLength = p == -1 ? targetName.Length : p;
			}
		} // ctor

		public bool IsSameProvider(string otherProvider)
		{
			return otherProvider == null
				? false
				: credentialProvider == null
					? true
					: String.Compare(credentialProvider, otherProvider, StringComparison.OrdinalIgnoreCase) == 0;
		} // func IsSameProvider

		public int TestTargetName(string otherTargetName)
		{
			if (targetName == null)
				return -1;

			var p = otherTargetName.IndexOf('/');
			if (p == -1) // compare 'server' part only
			{
				if (targetNameServerLength == otherTargetName.Length
					&& String.Compare(targetName, 0, otherTargetName, 0, targetNameServerLength, StringComparison.OrdinalIgnoreCase) == 0)
					return targetNameServerLength == targetName.Length ? Int32.MaxValue : 1;
				else
					return -1;
			}
			else
			{
				if (targetNameServerLength == p
					&& String.Compare(targetName, 0, otherTargetName, 0, p, StringComparison.OrdinalIgnoreCase) == 0) // 'server' is equal
				{
					if (otherTargetName.Length == targetName.Length
						&& String.Compare(targetName, p, otherTargetName, p, otherTargetName.Length - p, StringComparison.OrdinalIgnoreCase) == 0)
						return Int32.MaxValue;
					else if (otherTargetName.StartsWith(targetName, StringComparison.OrdinalIgnoreCase)) // check starts with
						return 1;
					else
						return -1;
				}
				else
					return -1;
			}
		} // func TestTargetName

		public bool IsUserName(string otherUserName)
			=> userName == null || String.Compare(userName, otherUserName, StringComparison.OrdinalIgnoreCase) == 0;

		public bool HasContent => targetName != null;
	} // class CredCompare
}
