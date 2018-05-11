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
            centroid_ms1,
            centroid_ms2,
            lockmass_pos,
            lockmass_neg,
            lockmass_tol,
            path,
            server,
            username        }

        public abstract RemoteAccountType AccountType { get; }

        protected virtual void Init(NameValueParameters nameValueParameters)
        {
            CentroidMs1 = nameValueParameters.GetBoolValue(Attr.centroid_ms1.ToString());
            CentroidMs2 = nameValueParameters.GetBoolValue(Attr.centroid_ms2.ToString());
            LockMassParameters = new LockMassParameters(
                nameValueParameters.GetDoubleValue(Attr.lockmass_pos.ToString()),
                nameValueParameters.GetDoubleValue(Attr.lockmass_neg.ToString()),
                nameValueParameters.GetDoubleValue(Attr.lockmass_tol.ToString()));
            ServerUrl = nameValueParameters.GetValue(Attr.server.ToString());
            Username = nameValueParameters.GetValue(Attr.username.ToString());
            EncodedPath = nameValueParameters.GetValue(Attr.path.ToString());

        }

        public override MsDataFileUri ChangeCentroiding(bool centroidMS1, bool centroidMS2)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im.CentroidMs1 = centroidMS1;
                im.CentroidMs2 = centroidMS2;
            });
        }

        public override MsDataFileUri ChangeLockMassParameters(LockMassParameters lockMassParameters)
        {
            return ChangeProp(ImClone(this), im => im.LockMassParameters = lockMassParameters);
        }

        public bool CentroidMs1 { get; private set; }
        public bool CentroidMs2 { get; private set; }
        public LockMassParameters LockMassParameters { get; private set; }
        public string ServerUrl { get; private set; }
        public string Username { get; private set; }

        public RemoteUrl ChangeServerUrl(string serverUrl)
        {
            return ChangeProp(ImClone(this), im => im.ServerUrl = serverUrl);
        }

        public RemoteUrl ChangeUsername(string username)
        {
            return ChangeProp(ImClone(this), im => im.Username = username);
        }

        public override bool GetCentroidMs1()
        {
            return CentroidMs1;
        }

        public override bool GetCentroidMs2()
        {
            return CentroidMs2;
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
            nameValuePairs.SetBoolValue(Attr.centroid_ms1.ToString(), CentroidMs1);
            nameValuePairs.SetBoolValue(Attr.centroid_ms2.ToString(), CentroidMs2);
            nameValuePairs.SetDoubleValue(Attr.lockmass_neg.ToString(), LockMassParameters.LockmassNegative);
            nameValuePairs.SetDoubleValue(Attr.lockmass_pos.ToString(), LockMassParameters.LockmassPositive);
            nameValuePairs.SetDoubleValue(Attr.lockmass_tol.ToString(), LockMassParameters.LockmassTolerance);
            nameValuePairs.SetValue(Attr.path.ToString(), EncodedPath);
            nameValuePairs.SetValue(Attr.server.ToString(), ServerUrl);
            nameValuePairs.SetValue(Attr.username.ToString(), Username);
            return nameValuePairs;
        }

        public override string ToString()
        {
            return AccountType.Name + ":" + GetParameters();
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
                im => im.EncodedPath = string.Join("/", parts.Select(Uri.EscapeDataString)));
        }

        public IEnumerable<string> GetPathParts()
        {
            return EncodedPath.Split('/').Select(Uri.UnescapeDataString);
        }

        public override string GetFilePath()
        {
            return Uri.UnescapeDataString(EncodedPath);
        }

        public string LastPathPart
        {
            get { return GetPathParts().LastOrDefault(); }
        }

        private class Empty : RemoteUrl
        {
            public override DateTime GetFileLastWriteTime()
            {
                return default(DateTime);
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
        }

        protected NameValueParameters ParseNameValueParameters(string url)
        {
            string prefix = AccountType.Name + ":";
            if (!url.StartsWith(prefix))
            {
                throw new ArgumentException(string.Format("URL must start with '{0}'", prefix)); // Not L10N
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
    }
}
