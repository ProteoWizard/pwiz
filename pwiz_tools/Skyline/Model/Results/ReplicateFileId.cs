using System;
using pwiz.Skyline.Util.Extensions;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace pwiz.Skyline.Model.Results
{
    public class ReplicateFileId
    {
        public ReplicateFileId(ChromatogramSetId chromatogramSetId, ChromFileInfoId fileId)
        {
            ChromatogramSetId = chromatogramSetId;
            FileId = fileId;
        }

        public ChromatogramSetId ChromatogramSetId { get; }
        public ChromFileInfoId FileId { get; }

        protected bool Equals(ReplicateFileId other)
        {
            return ReferenceEquals(ChromatogramSetId, other.ChromatogramSetId) && ReferenceEquals(FileId, other.FileId);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ReplicateFileId)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = RuntimeHelpers.GetHashCode(ChromatogramSetId);
                result = (result * 397) ^ (FileId == null ? 0 : RuntimeHelpers.GetHashCode(FileId));
                return result;
            }
        }

        public static bool operator==(ReplicateFileId a, ReplicateFileId b)
        {
            return Equals(a, b);
        }

        public static bool operator !=(ReplicateFileId a, ReplicateFileId b)
        {
            return !Equals(a, b);
        }
        public static ReplicateFileId Find(SrmDocument document, MsDataFileUri msDataFileUri)
        {
            var measuredResults = document.MeasuredResults;
            if (measuredResults == null)
            {
                return null;
            }

            foreach (var chromatogramSet in measuredResults.Chromatograms)
            {
                var chromFileInfoId = chromatogramSet.FindFile(msDataFileUri);
                if (chromFileInfoId != null)
                {
                    return new ReplicateFileId((ChromatogramSetId) chromatogramSet.Id, chromFileInfoId);
                }
            }

            return null;
        }

        public ReplicateFileInfo FindInfo(MeasuredResults measuredResults)
        {
            if (!measuredResults.TryGetChromatogramSet(ChromatogramSetId.GlobalIndex, out var chromatogramSet,
                    out var replicateIndex))
            {
                return null;
            }

            int fileIndex = chromatogramSet.IndexOfId(FileId);
            if (fileIndex < 0)
            {
                return null;
            }

            return new ReplicateFileInfo(replicateIndex, chromatogramSet, chromatogramSet.MSDataFileInfos[fileIndex]);
        }
    }

    public class ReplicateFileInfo
    {
        public ReplicateFileInfo(string display, int replicateIndex, string replicateName,
            MsDataFileUri msDataFileUri, ReplicateFileId replicateFileId)
        {
            Display = display;
            ReplicateIndex = replicateIndex;
            ReplicateName = replicateName;
            MsDataFileUri = msDataFileUri;
            ReplicateFileId = replicateFileId;
        }

        public ReplicateFileInfo(int replicateIndex, ChromatogramSet chromatogramSet, ChromFileInfo chromFileInfo)
        {
            if (chromatogramSet.MSDataFileInfos.Count == 1 || chromFileInfo == null)
            {
                Display = chromatogramSet.Name;
            }
            else
            {
                Display = TextUtil.ColonSeparate(chromatogramSet.Name,
                    chromFileInfo.FilePath.GetFileNameWithoutExtension());
            }

            ReplicateIndex = replicateIndex;
            ReplicateName = chromatogramSet.Name;
            MsDataFileUri = chromFileInfo?.FilePath;
            ReplicateFileId = new ReplicateFileId((ChromatogramSetId)chromatogramSet.Id, chromFileInfo?.FileId);
        }

        public string Display { get; }
        public override string ToString()
        {
            return Display;
        }

        public int ReplicateIndex { get; }
        public string ReplicateName { get; }

        public MsDataFileUri MsDataFileUri { get; }
        public ReplicateFileId ReplicateFileId { get; }

        public static IEnumerable<ReplicateFileInfo> List(MeasuredResults measuredResults)
        {
            if (measuredResults == null)
            {
                yield break;
            }

            for (int replicateIndex = 0; replicateIndex < measuredResults.Chromatograms.Count; replicateIndex++)
            {
                var chromatogramSet = measuredResults.Chromatograms[replicateIndex];
                foreach (var msDataFileInfo in chromatogramSet.MSDataFileInfos)
                {
                    yield return new ReplicateFileInfo(replicateIndex, chromatogramSet, msDataFileInfo);
                }
            }
        }

        public static ReplicateFileInfo ForReplicateIndex(MeasuredResults measuredResults, int replicateIndex)
        {
            if (replicateIndex < 0 || measuredResults == null)
            {
                return null;
            }

            var chromatogramSet = measuredResults.Chromatograms[replicateIndex];
            return new ReplicateFileInfo(replicateIndex, chromatogramSet, null);
        }

        public IEnumerable<TChromInfo> GetChromInfos<TChromInfo>(Results<TChromInfo> results)
            where TChromInfo : ChromInfo
        {
            if (results == null || results.Count <= ReplicateIndex)
            {
                return Array.Empty<TChromInfo>();
            }

            if (ReplicateFileId.FileId == null)
            {
                return results[ReplicateIndex];
            }

            return results[ReplicateIndex]
                .Where(chromInfo => ReferenceEquals(ReplicateFileId.FileId, chromInfo.FileId));
        }

        public static ReplicateFileInfo Consensus
        {
            get
            {
                return new ReplicateFileInfo("Consensus", -1, null, null, null);
            }
        }

        public static ReplicateFileInfo All
        {
            get
            {
                return new ReplicateFileInfo("All", -1, null, null, null);
            }
        }

        protected bool Equals(ReplicateFileInfo other)
        {
            return Display == other.Display && ReplicateIndex == other.ReplicateIndex && ReplicateName == other.ReplicateName && Equals(MsDataFileUri, other.MsDataFileUri);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ReplicateFileInfo)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Display != null ? Display.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ ReplicateIndex;
                hashCode = (hashCode * 397) ^ (ReplicateName != null ? ReplicateName.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (MsDataFileUri != null ? MsDataFileUri.GetHashCode() : 0);
                return hashCode;
            }
        }
    }
}
