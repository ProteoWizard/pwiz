using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results.RemoteApi.Chorus
{
    public class ChorusUrl : RemoteUrl
    {
        public static readonly ChorusUrl EMPTY = new ChorusUrl(ChorusUrlPrefix);
        public const string ChorusUrlPrefix = "chorus:";    // Not L10N

        public ChorusUrl(string chorusUrl)
        {
            if (!chorusUrl.StartsWith(ChorusUrlPrefix))
            {
                throw new ArgumentException("URL must start with " + ChorusUrlPrefix); // Not L10N
            }
            var nameValueParameters = NameValueParameters.Parse(chorusUrl.Substring(ChorusUrlPrefix.Length));
            Init(nameValueParameters);
        }

        protected override void Init(NameValueParameters nameValueParameters)
        {
            base.Init(nameValueParameters);
            ProjectId = nameValueParameters.GetLongValue("projectId");
            ExperimentId = nameValueParameters.GetLongValue("experimentId");
            FileId = nameValueParameters.GetLongValue("fileId");
            Path = nameValueParameters.GetValue("path");
            FileWriteTime = nameValueParameters.GetDateValue("fileWriteTime");
            RunStartTime = nameValueParameters.GetDateValue("runStartTime");
        }

        public string Path { get; private set; }

        public long? ProjectId { get; private set; }

        public ChorusUrl SetProjectId(long? projectId)
        {
            return ChangeProp(ImClone(this), im => im.ProjectId = projectId);
        }

        public long? ExperimentId { get; private set; }

        public ChorusUrl SetExperimentId(long? experimentId)
        {
            return ChangeProp(ImClone(this), im => im.ExperimentId = experimentId);
        }

        public long? FileId { get; private set; }

        public ChorusUrl SetFileId(long? fileId)
        {
            return ChangeProp(ImClone(this), im => im.FileId = fileId);
        }

        public ChorusUrl SetPath(string path)
        {
            return ChangeProp(ImClone(this), im => im.Path = path);
        }

        public override string GetFileName()
        {
            return GetPathParts().LastOrDefault() ?? ServerUrl;
        }

        public DateTime? RunStartTime { get; private set; }

        public ChorusUrl SetRunStartTime(DateTime? runStartTime)
        {
            return ChangeProp(ImClone(this), im=>im.RunStartTime = runStartTime);
        }

        public DateTime? FileWriteTime { get; private set; }

        public ChorusUrl SetFileWriteTime(DateTime? fileWriteTime)
        {
            return ChangeProp(ImClone(this), im=>im.FileWriteTime = fileWriteTime);
        }

        public override string GetFilePath()
        {
            return Uri.UnescapeDataString(Path);
        }

        public IEnumerable<string> GetPathParts()
        {
            if (string.IsNullOrEmpty(Path))
            {
                return new String[0];
            }
            return Path.Split(new[] {'/'}).Select(Uri.UnescapeDataString);
        }

        public ChorusUrl SetPathParts(IEnumerable<string> parts)
        {
            return SetPath(string.Join("/", parts.Select(Uri.EscapeDataString)));    // Not L10N
        }

        public ChorusUrl AddPathPart(string part)
        {
            return SetPathParts(GetPathParts().Concat(new[]{part}));
        }

        public override bool IsWatersLockmassCorrectionCandidate()
        {
            return false;  // Chorus will have already performed lockmass correction
        }

        public override LockMassParameters GetLockMassParameters()
        {
            return LockMassParameters.EMPTY;  // Chorus will have already performed lockmass correction
        }

        public override MsDataFileUri ChangeLockMassParameters(LockMassParameters lockMassParameters)
        {
            return this;  // Chorus will have already performed lockmass correction
        }

        public override bool GetCentroidMs1()
        {
            return false;  // Chorus will have already centroided
        }

        public override bool GetCentroidMs2()
        {
            return false; // Chorus will have already centroided
        }

        public override MsDataFileUri ChangeCentroiding(bool centroidMS1, bool centroidMS2)
        {
            return this; // Chorus will have already centroided
        }

        public override string ToString()
        {
            List<string> parts = new List<string>();
            if (!string.IsNullOrEmpty(ServerUrl))
            {
                parts.Add("server=" + Uri.EscapeDataString(ServerUrl));    // Not L10N
            }
            if (ProjectId.HasValue)
            {
                parts.Add("projectId=" + Uri.EscapeDataString(LongToString(ProjectId.Value))); // Not L10N
            }
            if (ExperimentId.HasValue)
            {
                parts.Add("experimentId=" + Uri.EscapeDataString(LongToString(ExperimentId.Value))); // Not L10N
            }
            if (FileId.HasValue)
            {
                parts.Add("fileId=" + Uri.EscapeDataString(LongToString(FileId.Value)));    // Not L10N
            }
            if (!string.IsNullOrEmpty(Path))
            {
                parts.Add("path=" + Uri.EscapeDataString(Path));    // Not L10N
            }
            if (!string.IsNullOrEmpty(Username))
            {
                parts.Add("username=" + Uri.EscapeDataString(Username));    // Not L10N
            }
            if (FileWriteTime.HasValue)
            {
                parts.Add("fileWriteTime=" + Uri.EscapeDataString(DateToString(FileWriteTime.Value))); // Not L10N
            }
            if (RunStartTime.HasValue)
            {
                parts.Add("runStartTime=" + Uri.EscapeDataString(DateToString(RunStartTime.Value))); // Not L10N
            }
            return ChorusUrlPrefix + string.Join("&", parts); // Not L10N
        }

        public override DateTime GetFileLastWriteTime()
        {
            return default(DateTime);
        }

        public ChorusUrl GetRootChorusUrl()
        {
            return (ChorusUrl) EMPTY.ChangeServerUrl(ServerUrl).ChangeUsername(Username);
        }

        public Uri GetChromExtractionUri()
        {
            return new Uri(ServerUrl + "/skyline/api/chroextract/file/" + LongToString(FileId.Value));    // Not L10N
        }

        public override string GetSampleName()
        {
            return null;
        }

        public override int GetSampleIndex()
        {
            return -1;
        }

        public override MsDataFileUri ToLower()
        {
            return this;
        }

        public override MsDataFileUri Normalize()
        {
            return this;
        }

        protected bool Equals(ChorusUrl other)
        {
            return string.Equals(ServerUrl, other.ServerUrl) &&
                   Equals(ExperimentId, other.ExperimentId) &&
                   Equals(ProjectId, other.ProjectId) &&
                   Equals(FileId, other.FileId) &&
                   Equals(RunStartTime, other.RunStartTime) &&
                   string.Equals(Path, other.Path);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ChorusUrl) obj);
        }

        protected int CompareTo(ChorusUrl other)
        {
            // Culture specific sorting desirable in file paths
// ReSharper disable StringCompareToIsCultureSpecific
            int result = ServerUrl.CompareTo(other.ServerUrl);
            if (result != 0)
                return result;
            if (FileId.HasValue)
            {
                result = FileId.Value.CompareTo(other.FileId);
                if (result != 0)
                {
                    return result;
                }
            }
            else if (other.FileId.HasValue)
            {
                return -1;
            }
            return Path.CompareTo(other.Path);
// ReSharper restore StringCompareToIsCultureSpecific
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (ServerUrl != null ? ServerUrl.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ ProjectId.GetHashCode();
                hashCode = (hashCode*397) ^ ExperimentId.GetHashCode();
                hashCode = (hashCode*397) ^ FileId.GetHashCode();
                hashCode = (hashCode*397) ^ (Path != null ? Path.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (Username != null ? Username.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ RunStartTime.GetHashCode();
                return hashCode;
            }
        }

        public ChorusUrl GetParent()
        {
            var pathParts = GetPathParts().ToArray();
            if (pathParts.Length == 0)
            {
                return EMPTY;
            }
            var result = GetRootChorusUrl();
            for (int i = 0; i < pathParts.Length - 1; i++)
            {
                result = result.AddPathPart(pathParts[i]);
            }
            if (FileId.HasValue)
            {
                result = result.SetExperimentId(ExperimentId);
            }
            if (ExperimentId.HasValue)
            {
                result = result.SetProjectId(ProjectId);
            }
            return result;
        }

        public ChorusAccount FindChorusAccount(IEnumerable<RemoteAccount> chorusAccounts)
        {
            return (ChorusAccount) chorusAccounts.FirstOrDefault(chorusAccount => chorusAccount.CanHandleUrl(this));
        }

        private static string LongToString(long value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static long? ParseLong(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return null;
            }
            return long.Parse(str, CultureInfo.InvariantCulture);
        }

        private static string DateToString(DateTime dateTime)
        {
            return dateTime.ToString(CultureInfo.InvariantCulture);
        }

        private DateTime? ParseDate(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return null;
            }
            return DateTime.Parse(str, CultureInfo.InvariantCulture);
        }

        public override RemoteAccountType AccountType
        {
            get { return RemoteAccountType.CHORUS; }
        }
    }
}