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
        public static readonly string ChorusUrlPrefix = RemoteAccountType.CHORUS.Name + ":";    // Not L10N
        public static readonly ChorusUrl Empty = new ChorusUrl(ChorusUrlPrefix);

        public ChorusUrl(string chorusUrl) : base(chorusUrl)
        {
        }

        protected override void Init(NameValueParameters nameValueParameters)
        {
            base.Init(nameValueParameters);
            // ReSharper disable NonLocalizedString
            ProjectId = nameValueParameters.GetLongValue("projectId");
            ExperimentId = nameValueParameters.GetLongValue("experimentId");
            FileId = nameValueParameters.GetLongValue("fileId");
            RunStartTime = nameValueParameters.GetDateValue("runStartTime");
            // ReSharper restore NonLocalizedString
        }

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

        public DateTime? RunStartTime { get; private set; }

        public ChorusUrl ChangeRunStartTime(DateTime? runStartTime)
        {
            return ChangeProp(ImClone(this), im=>im.RunStartTime = runStartTime);
        }

        public ChorusUrl AddPathPart(string part)
        {
            return (ChorusUrl) ChangePathParts(GetPathParts().Concat(new[]{part}));
        }

        public override bool IsWatersLockmassCorrectionCandidate()
        {
            return false;  // Chorus will have already performed lockmass correction
        }

        protected override NameValueParameters GetParameters()
        {
            var result = base.GetParameters();
            // ReSharper disable NonLocalizedString
            result.SetLongValue("projectId", ProjectId);
            result.SetLongValue("experimentId", ExperimentId);
            result.SetLongValue("fileId", FileId);
            result.SetDateValue("runStartTime", RunStartTime);
            // ReSharper restore NonLocalizedString
            return result;
        }

        public ChorusUrl GetRootChorusUrl()
        {
            return (ChorusUrl) Empty.ChangeServerUrl(ServerUrl).ChangeUsername(Username);
        }

        public Uri GetChromExtractionUri()
        {
            return new Uri(ServerUrl + "/skyline/api/chroextract/file/" + LongToString(FileId.Value)); // Not L10N
        }

        protected bool Equals(ChorusUrl other)
        {
            return base.Equals(other) && ProjectId == other.ProjectId && ExperimentId == other.ExperimentId &&
                   FileId == other.FileId && RunStartTime.Equals(other.RunStartTime);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ChorusUrl) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = base.GetHashCode();
                hashCode = (hashCode * 397) ^ ProjectId.GetHashCode();
                hashCode = (hashCode * 397) ^ ExperimentId.GetHashCode();
                hashCode = (hashCode * 397) ^ FileId.GetHashCode();
                hashCode = (hashCode * 397) ^ RunStartTime.GetHashCode();
                return hashCode;
            }
        }

        public ChorusUrl GetParent()
        {
            var pathParts = GetPathParts().ToArray();
            if (pathParts.Length == 0)
            {
                return Empty;
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

        public override RemoteAccountType AccountType
        {
            get { return RemoteAccountType.CHORUS; }
        }

        public override MsDataFileImpl OpenMsDataFile(bool simAsSpectra, int preferOnlyMsLevel)
        {
            throw new InvalidOperationException();
        }
    }
}