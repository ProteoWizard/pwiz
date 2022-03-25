using pwiz.Skyline.Model.Databinding.Entities;

namespace pwiz.Skyline.Model.Results
{
    public class CandidatePeakGroupData
    {
        public CandidatePeakGroupData(int? peakIndex, double retentionTime, double minStartTime, double maxEndTime,
            bool chosen, PeakGroupScore score)
        {
            PeakIndex = peakIndex;
            RetentionTime = retentionTime;
            MinStartTime = minStartTime;
            MaxEndTime = maxEndTime;
            Chosen = chosen;
            Score = score;
        }

        public int? PeakIndex { get; }
        public double RetentionTime { get; }
        public double MinStartTime { get; }
        public double MaxEndTime { get; }
        public bool Chosen { get; }
        public PeakGroupScore Score { get; }
    }
}
