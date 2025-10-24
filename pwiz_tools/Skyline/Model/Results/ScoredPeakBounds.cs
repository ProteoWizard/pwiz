using pwiz.Common.PeakFinding;
using pwiz.Common.SystemUtil;
using pwiz.CommonMsData;
using pwiz.Skyline.Model.Lib;

namespace pwiz.Skyline.Model.Results
{
    public class ScoredPeakBounds : Immutable
    {
        public ScoredPeakBounds(float apexTime, float startTime, float endTime, float score)
        {
            ApexTime = apexTime;
            StartTime = startTime;
            EndTime = endTime;
            Score = score;
        }

        public float ApexTime { get; }
        public float StartTime { get; }
        public float EndTime { get; }
        public float Score { get; }

        public PeakBounds PeakBounds
        {
            get
            {
                return new PeakBounds(StartTime, EndTime);
            }
        }

        protected bool Equals(ScoredPeakBounds other)
        {
            return StartTime.Equals(other.StartTime) && EndTime.Equals(other.EndTime) && Score.Equals(other.Score);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ScoredPeakBounds)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = StartTime.GetHashCode();
                hashCode = (hashCode * 397) ^ EndTime.GetHashCode();
                hashCode = (hashCode * 397) ^ Score.GetHashCode();
                return hashCode;
            }
        }
    }

    public class PeakSource : Immutable
    {
        public PeakSource(string filePath)
        {
            FilePath = filePath;
        }

        public string FilePath { get;  }
        public string LibraryName { get; private set; }

        public PeakSource ChangeLibraryName(string value)
        {
            return ChangeProp(ImClone(this), im => im.LibraryName = value);
        }

        public string ReplicateName { get; private set; }

        public PeakSource ChangeReplicateName(string value)
        {
            return ChangeProp(ImClone(this), im => im.ReplicateName = value);
        }

        public static PeakSource FromLibrary(Library library, string spectrumSourceFile)
        {
            return new PeakSource(spectrumSourceFile).ChangeLibraryName(library.Name);
        }

        public static PeakSource FromChromFile(ChromatogramSet chromatogramSet, MsDataFileUri file)
        {
            return new PeakSource(file.ToString()).ChangeReplicateName(chromatogramSet.Name);
        }
    }

    public class SourcedPeak
    {
        public SourcedPeak(PeakSource peakSource, ScoredPeakBounds scoredPeak)
        {
            Source = peakSource;
            Peak = scoredPeak;
        }

        public PeakSource Source { get; }
        public ScoredPeakBounds Peak { get; }
    }
}
