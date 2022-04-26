using pwiz.Skyline.Model.Databinding.Entities;

namespace pwiz.Skyline.Model.Results
{
    public class CandidatePeakGroupData
    {
        public CandidatePeakGroupData(int? peakIndex, double retentionTime, double minStartTime, double maxEndTime,
            bool chosen, PeakGroupScore score, bool originallyBestPeak)
        {
            PeakIndex = peakIndex;
            RetentionTime = retentionTime;
            MinStartTime = minStartTime;
            MaxEndTime = maxEndTime;
            Chosen = chosen;
            Score = score;
            OriginallyBestPeak = originallyBestPeak;
        }

        public int? PeakIndex { get; }
        public double RetentionTime { get; }
        public double MinStartTime { get; }
        public double MaxEndTime { get; }
        public bool Chosen { get; }
        public PeakGroupScore Score { get; }
        public bool OriginallyBestPeak { get; }

        public static CandidatePeakGroupData CustomPeak(double retentionTime, double minStartTime, double maxEndTime,
            PeakGroupScore score)
        {
            return new CandidatePeakGroupData(null, retentionTime, minStartTime, maxEndTime, true, score, false);
        }

        public static CandidatePeakGroupData FoundPeak(int peakIndex, double retentionTime, double minStartTime,
            double maxEndTime, bool chosen, PeakGroupScore score, bool originallyBestPeak)
        {
            return new CandidatePeakGroupData(peakIndex, retentionTime, minStartTime, maxEndTime, chosen, score,
                originallyBestPeak);
        }
    }
}
