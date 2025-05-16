using pwiz.Common.PeakFinding;
using pwiz.Skyline.Model.Lib;

namespace pwiz.Skyline.Model.RetentionTimes.PeakImputation
{
    public class ExemplaryPeak
    {
        public ExemplaryPeak(Library library, string spectrumSourceFile, PeakBounds peakBounds)
        {
            Library = library;
            PeakBounds = peakBounds;
            SpectrumSourceFile = spectrumSourceFile;
        }

        public Library Library { get; }
        public PeakBounds PeakBounds { get; }
        public string SpectrumSourceFile { get; }

        protected bool Equals(ExemplaryPeak other)
        {
            return Equals(Library, other.Library) && Equals(PeakBounds, other.PeakBounds) && SpectrumSourceFile == other.SpectrumSourceFile;
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ExemplaryPeak)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Library != null ? Library.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (PeakBounds != null ? PeakBounds.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (SpectrumSourceFile != null ? SpectrumSourceFile.GetHashCode() : 0);
                return hashCode;
            }
        }
    }
}
