/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results.RemoteApi
{
    public abstract class RemoteUrl : MsDataFileUri
    {
        public static readonly RemoteUrl EMPTY = new Empty();
        private enum Attr
        {
            centroid_ms1,   // Legacy
            centroid_ms2,   // Legacy
            lockmass_pos,
            lockmass_neg,
            lockmass_tol,
            path,
            server,
            username,
            modified_time,
        }

        public abstract RemoteAccountType AccountType { get; }

        private RemoteUrl()
        {
        }

        // ReSharper disable VirtualMemberCallInConstructor
        protected RemoteUrl(string url)
        {
            string prefix = AccountType.Name + @":";
            if (!url.StartsWith(prefix))
            {
                throw new ArgumentException(@"URL must start with " + prefix);
            }
            var nameValueParameters = NameValueParameters.Parse(url.Substring(prefix.Length));
            Init(nameValueParameters);
        }
        // ReSharper restore VirtualMemberCallInConstructor

        protected virtual void Init(NameValueParameters nameValueParameters)
        {
            LegacyCentroidMs1 = nameValueParameters.GetBoolValue(Attr.centroid_ms1.ToString());
            LegacyCentroidMs2 = nameValueParameters.GetBoolValue(Attr.centroid_ms2.ToString());
            LockMassParameters = new LockMassParameters(
                nameValueParameters.GetDoubleValue(Attr.lockmass_pos.ToString()),
                nameValueParameters.GetDoubleValue(Attr.lockmass_neg.ToString()),
                nameValueParameters.GetDoubleValue(Attr.lockmass_tol.ToString()));
            ServerUrl = nameValueParameters.GetValue(Attr.server.ToString());
            Username = nameValueParameters.GetValue(Attr.username.ToString());
            EncodedPath = nameValueParameters.GetValue(Attr.path.ToString());
            ModifiedTime = nameValueParameters.GetDateValue(Attr.modified_time.ToString());
        }

        public override MsDataFileUri ChangeLockMassParameters(LockMassParameters lockMassParameters)
        {
            return ChangeProp(ImClone(this), im => im.LockMassParameters = lockMassParameters);
        }

        public bool LegacyCentroidMs1 { get; private set; }
        public bool LegacyCentroidMs2 { get; private set; }
        public LockMassParameters LockMassParameters { get; private set; }
        public string ServerUrl { get; private set; }
        public string Username { get; private set; }
        public DateTime? ModifiedTime { get; private set; }

        public RemoteUrl ChangeModifiedTime(DateTime? modifiedTime)
        {
            return ChangeProp(ImClone(this), im => im.ModifiedTime = modifiedTime);
        }

        public RemoteUrl ChangeServerUrl(string serverUrl)
        {
            return ChangeProp(ImClone(this), im => im.ServerUrl = serverUrl);
        }

        public RemoteUrl ChangeUsername(string username)
        {
            return ChangeProp(ImClone(this), im => im.Username = username);
        }

        public override bool LegacyGetCentroidMs1()
        {
            return LegacyCentroidMs1;
        }

        public override bool LegacyGetCentroidMs2()
        {
            return LegacyCentroidMs2;
        }

        public override string GetExtension()
        {
            return Path.GetExtension(GetFilePath());
        }

        public override MsDataFileUri GetLocation()
        {
            return this;
        }

        public override LockMassParameters GetLockMassParameters()
        {
            return LockMassParameters;
        }

        public override MsDataFileUri Normalize()
        {
            return this;
        }

        public override MsDataFileUri ToLower()
        {
            return this;
        }

        protected virtual NameValueParameters GetParameters()
        {
            var nameValuePairs = new NameValueParameters();
            nameValuePairs.SetBoolValue(Attr.centroid_ms1.ToString(), LegacyCentroidMs1);
            nameValuePairs.SetBoolValue(Attr.centroid_ms2.ToString(), LegacyCentroidMs2);
            nameValuePairs.SetDoubleValue(Attr.lockmass_neg.ToString(), LockMassParameters.LockmassNegative);
            nameValuePairs.SetDoubleValue(Attr.lockmass_pos.ToString(), LockMassParameters.LockmassPositive);
            nameValuePairs.SetDoubleValue(Attr.lockmass_tol.ToString(), LockMassParameters.LockmassTolerance);
            nameValuePairs.SetValue(Attr.path.ToString(), EncodedPath);
            nameValuePairs.SetValue(Attr.server.ToString(), ServerUrl);
            nameValuePairs.SetValue(Attr.username.ToString(), Username);
            nameValuePairs.SetDateValue(Attr.modified_time.ToString(), ModifiedTime);
            return nameValuePairs;
        }

        public override string ToString()
        {
            return AccountType.Name + @":" + GetParameters();
        }

        public override int GetSampleIndex()
        {
            return -1;
        }

        public override string GetSampleName()
        {
            return null;
        }

        public override string GetFileName()
        {
            return Path.GetFileName(LastPathPart);
        }

        public override string GetFileNameWithoutExtension()
        {
            return Path.GetFileNameWithoutExtension(LastPathPart);
        }

        public string EncodedPath { get; private set; }

        public RemoteUrl ChangePathParts(IEnumerable<string> parts)
        {
            return ChangeProp(ImClone(this),
                im => im.EncodedPath = string.Join(@"/", parts.Select(Uri.EscapeDataString)));
        }

        public IEnumerable<string> GetPathParts()
        {
            if (EncodedPath == null)
            {
                return new string[0];
            }
            return EncodedPath.Split('/').Select(Uri.UnescapeDataString);
        }

        public override DateTime GetFileLastWriteTime()
        {
            return ModifiedTime.GetValueOrDefault();
        }

        public override string GetFilePath()
        {
            return Uri.UnescapeDataString(EncodedPath);
        }

        public string LastPathPart
        {
            get { return GetPathParts().LastOrDefault(); }
        }

        public override MsDataFileUri RemoveLegacyParameters()
        {
            return ChangeProp(ImClone(this), im =>
            {
                im.LegacyCentroidMs1 = false;
                im.LegacyCentroidMs2 = false;
            });
        }

        private class Empty : RemoteUrl
        {
            protected override object ImmutableClone()
            {
                throw new InvalidOperationException();
            }

            public override string GetFilePath()
            {
                return string.Empty;
            }

            public override bool IsWatersLockmassCorrectionCandidate()
            {
                return false;
            }

            public override RemoteAccountType AccountType
            {
                get { return null; }
            }

            public override string ToString()
            {
                return string.Empty;
            }

            public override MsDataFileImpl OpenMsDataFile(bool simAsSpectra, bool preferOnlyMs1,
                bool centroidMs1, bool centroidMs2, bool ignoreZeroIntensityPoints)
            {
                throw new InvalidOperationException();
            }
        }

        protected NameValueParameters ParseNameValueParameters(string url)
        {
            string prefix = AccountType.Name + @":";
            if (!url.StartsWith(prefix))
            {
                throw new ArgumentException(string.Format(@"URL must start with '{0}'", prefix));
            }
            return NameValueParameters.Parse(url.Substring(prefix.Length));
        }

        protected static T GetValue<T>(NameValueCollection nameValueCollection, object key, T defaultValue)
        {
            string strValue = nameValueCollection.Get(key.ToString());
            if (strValue == null)
            {
                return defaultValue;
            }
            return (T) Convert.ChangeType(strValue, typeof(T), CultureInfo.InvariantCulture);
        }

        protected bool Equals(RemoteUrl other)
        {
            return LegacyCentroidMs1 == other.LegacyCentroidMs1 && LegacyCentroidMs2 == other.LegacyCentroidMs2 &&
                   Equals(LockMassParameters, other.LockMassParameters) && string.Equals(ServerUrl, other.ServerUrl) &&
                   string.Equals(Username, other.Username) && ModifiedTime.Equals(other.ModifiedTime) &&
                   string.Equals(EncodedPath, other.EncodedPath);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((RemoteUrl) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = LegacyCentroidMs1.GetHashCode();
                hashCode = (hashCode * 397) ^ LegacyCentroidMs2.GetHashCode();
                hashCode = (hashCode * 397) ^ (LockMassParameters != null ? LockMassParameters.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (ServerUrl != null ? ServerUrl.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Username != null ? Username.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ ModifiedTime.GetHashCode();
                hashCode = (hashCode * 397) ^ (EncodedPath != null ? EncodedPath.GetHashCode() : 0);
                return hashCode;
            }
        }

        public virtual RemoteAccount FindMatchingAccount(IEnumerable<RemoteAccount> accounts)
        {
            var sameServerAccounts = accounts.Where(account => account.CanHandleUrl(this)).ToArray();
            var matchingUsername = sameServerAccounts.FirstOrDefault(account => account.Username == Username);
            if (matchingUsername != null)
            {
                return matchingUsername;
            }
            return sameServerAccounts.FirstOrDefault();
        }
    }
}
