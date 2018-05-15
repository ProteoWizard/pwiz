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
            RunStartTime = nameValueParameters.GetDateValue("runStartTime");
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

        protected override NameValueParameters GetParameters()
        {
            var result = base.GetParameters();
            result.SetLongValue("projectId", ProjectId);
            result.SetLongValue("experimentId", ExperimentId);
            result.SetLongValue("fileId", FileId);
            result.SetDateValue("runStartTime", RunStartTime);
            return result;
        }

        public ChorusUrl GetRootChorusUrl()
        {
            return (ChorusUrl) EMPTY.ChangeServerUrl(ServerUrl).ChangeUsername(Username);
        }

        public Uri GetChromExtractionUri()
        {
            return new Uri(ServerUrl + "/skyline/api/chroextract/file/" + LongToString(FileId.Value));    // Not L10N
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

        public override RemoteAccountType AccountType
        {
            get { return RemoteAccountType.CHORUS; }
        }

        public override MsDataFileImpl OpenMsDataFile(bool simAsSpectra)
        {
            throw new InvalidOperationException();
        }
    }
}