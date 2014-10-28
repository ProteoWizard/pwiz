/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Web;
using pwiz.Skyline.Model.Results.RemoteApi;

namespace pwiz.Skyline.Model.Results
{
    public abstract class MsDataFileUri : IComparable
    {
        public abstract string GetFileName();
        public abstract string GetFileNameWithoutExtension();
        public abstract override string ToString();
        public abstract int CompareTo(object obj);
        public abstract DateTime GetFileLastWriteTime();
        public abstract string GetSampleName();
        public abstract int GetSampleIndex();
        public abstract string GetExtension();
        public abstract MsDataFileUri ToLower();
        public abstract MsDataFileUri Normalize();


        public string GetSampleOrFileName()
        {
            return GetSampleName() ?? GetFileNameWithoutExtension();
        }

        public static MsDataFileUri Parse(string url)
        {
            if (url.StartsWith(ChorusUrl.ChorusUrlPrefix))
            {
                return new ChorusUrl(url);
            }
            return new MsDataFilePath(SampleHelp.GetPathFilePart(url), 
                SampleHelp.GetPathSampleNamePart(url), 
                SampleHelp.GetPathSampleIndexPart(url));
        }
    }

    public class MsDataFilePath : MsDataFileUri
    {
        public static readonly MsDataFilePath EMPTY = new MsDataFilePath(string.Empty);
        public MsDataFilePath(string filePath) : this(filePath, null, -1)
        {
        }
        public MsDataFilePath(string filePath, string sampleName, int sampleIndex)
        {
            FilePath = filePath;
            SampleName = sampleName;
            SampleIndex = sampleIndex;
        }

        protected MsDataFilePath(MsDataFilePath msDataFilePath)
        {
            FilePath = msDataFilePath.FilePath;
            SampleName = msDataFilePath.SampleName;
            SampleIndex = msDataFilePath.SampleIndex;
        }

        public string FilePath { get; private set; }

        public MsDataFilePath SetFilePath(string filePath)
        {
            return new MsDataFilePath(this){FilePath = filePath};
        }
        public string SampleName { get; private set; }
        public int SampleIndex { get; private set; }
        public override string GetFileNameWithoutExtension()
        {
            return Path.GetFileNameWithoutExtension(FilePath);
        }

        public override string GetExtension()
        {
            return Path.GetExtension(FilePath);
        }

        public override string GetFileName()
        {
            return Path.GetFileName(FilePath);
        }

        public override int GetSampleIndex()
        {
            return SampleIndex;
        }

        public override string ToString()
        {
            if (string.IsNullOrEmpty(SampleName) && -1 == SampleIndex)
            {
                return FilePath;
            }
            return SampleHelp.EncodePath(FilePath, SampleName, SampleIndex);
        }

        public override DateTime GetFileLastWriteTime()
        {
            return File.GetLastWriteTime(FilePath);
        }

        public override string GetSampleName()
        {
            return SampleName;
        }

        public override MsDataFileUri ToLower()
        {
            return SetFilePath(FilePath.ToLower());
        }

        public override MsDataFileUri Normalize()
        {
            return SetFilePath(Path.GetFullPath(FilePath));
        }

        protected bool Equals(MsDataFilePath other)
        {
            return string.Equals(FilePath, other.FilePath) &&
                string.Equals(SampleName, other.SampleName) &&
                SampleIndex == other.SampleIndex;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((MsDataFilePath) obj);
        }

        protected int CompareTo(MsDataFilePath other)
        {
            // Culture specific sorting desirable in file paths
// ReSharper disable StringCompareToIsCultureSpecific
            int result = FilePath.CompareTo(other.FilePath);
            if (result != 0)
                return result;
            result = SampleName.CompareTo(other.SampleName);
            if (result != 0)
                return result;
            return SampleIndex.CompareTo(other.SampleIndex);
// ReSharper restore StringCompareToIsCultureSpecific
        }

        public override int CompareTo(object obj)
        {
            if (ReferenceEquals(null, obj)) return -1;
            if (ReferenceEquals(this, obj)) return 0;
            if (obj.GetType() != GetType()) return -1;
            return CompareTo((MsDataFilePath)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = FilePath.GetHashCode();
                hashCode = (hashCode*397) ^ (SampleName != null ? SampleName.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ SampleIndex;
                return hashCode;
            }
        }
    }

    public class ChorusUrl : MsDataFileUri
    {
        public static readonly ChorusUrl EMPTY = new ChorusUrl(ChorusUrlPrefix);
        public const string ChorusUrlPrefix = "chorus:";    // Not L10N

        public ChorusUrl(string chorusUrl)
        {
            if (!chorusUrl.StartsWith(ChorusUrlPrefix))
            {
                throw new ArgumentException("URL must start with " + ChorusUrlPrefix); // Not L10N
            }
            NameValueCollection nameValueCollection =
                HttpUtility.ParseQueryString(chorusUrl.Substring(ChorusUrlPrefix.Length));
            ServerUrl = nameValueCollection.Get("server");    // Not L10N
            Username = nameValueCollection.Get("username");    // Not L10N
            ProjectId = ParseLong(nameValueCollection.Get("projectId")); // Not L10N
            ExperimentId = ParseLong(nameValueCollection.Get("experimentId")); // Not L10N
            FileId = ParseLong(nameValueCollection.Get("fileId"));    // Not L10N
            Path = nameValueCollection.Get("path");    // Not L10N
            FileWriteTime = ParseDate(nameValueCollection.Get("fileWriteTime")); // Not L10N
            RunStartTime = ParseDate(nameValueCollection.Get("runStartTime")); // Not L10N

        }

        protected ChorusUrl(ChorusUrl chorusUrl)
        {
            ServerUrl = chorusUrl.ServerUrl;
            Username = chorusUrl.Username;
            ProjectId = chorusUrl.ProjectId;
            ExperimentId = chorusUrl.ExperimentId;
            FileId = chorusUrl.FileId;
            Path = chorusUrl.Path;
            RunStartTime = chorusUrl.RunStartTime;
            FileWriteTime = chorusUrl.FileWriteTime;
        }

        public string ServerUrl { get; private set; }

        public ChorusUrl SetServerUrl(string serverUrl)
        {
            return new ChorusUrl(this) {ServerUrl = serverUrl};
        }

        public string Username { get; private set; }

        public ChorusUrl SetUsername(string username)
        {
            return new ChorusUrl(this){Username = username};
        }

        public string Path { get; private set; }

        public long? ProjectId { get; private set; }

        public ChorusUrl SetProjectId(long? projectId)
        {
            return new ChorusUrl(this) { ProjectId = projectId };
        }

        public long? ExperimentId { get; private set; }

        public ChorusUrl SetExperimentId(long? experimentId)
        {
            return new ChorusUrl(this) {ExperimentId = experimentId};
        }

        public long? FileId { get; private set; }

        public ChorusUrl SetFileId(long? fileId)
        {
            return new ChorusUrl(this) { FileId = fileId };
        }

        public ChorusUrl SetPath(string path)
        {
            return new ChorusUrl(this){Path = path};
        }

        public override string GetFileName()
        {
            return GetPathParts().LastOrDefault() ?? ServerUrl;
        }

        public DateTime? RunStartTime { get; private set; }

        public ChorusUrl SetRunStartTime(DateTime? runStartTime)
        {
            return new ChorusUrl(this) {RunStartTime = runStartTime};
        }

        public DateTime? FileWriteTime { get; private set; }

        public ChorusUrl SetFileWriteTime(DateTime? fileWriteTime)
        {
            return new ChorusUrl(this) {FileWriteTime = fileWriteTime};
        }

        public override string GetFileNameWithoutExtension()
        {
            string fileName = GetFileName();
            int ichDot = fileName.IndexOf('.');
            if (ichDot < 0)
            {
                return fileName;
            }
            return fileName.Substring(0, ichDot);
        }
        
        public override string GetExtension()
        {
            string fileName = GetFileName();
            int ichDot = fileName.IndexOf('.');
            if (ichDot < 0)
            {
                return string.Empty;
            }
            return fileName.Substring(ichDot);
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
            return EMPTY.SetServerUrl(ServerUrl).SetUsername(Username);
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

        public override int CompareTo(object obj)
        {
            if (ReferenceEquals(null, obj)) return -1;
            if (ReferenceEquals(this, obj)) return 0;
            if (obj.GetType() != GetType()) return -1;
            return CompareTo((ChorusUrl)obj);
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

        public ChorusAccount FindChorusAccount(IEnumerable<ChorusAccount> chorusAccounts)
        {
            return chorusAccounts.FirstOrDefault(chorusAccount =>
                Equals(ServerUrl, chorusAccount.ServerUrl) && Equals(Username, chorusAccount.Username));
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
    }
}
