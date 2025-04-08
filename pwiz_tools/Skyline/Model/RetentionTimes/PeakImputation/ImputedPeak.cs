using pwiz.Common.PeakFinding;

namespace pwiz.Skyline.Model.RetentionTimes.PeakImputation
{
    public class ImputedPeak
    {
        public ImputedPeak(PeakBounds peakBounds, ExemplaryPeak exemplaryPeak)
        {
            PeakBounds = peakBounds;
            ExemplaryPeak = exemplaryPeak;
        }

        public PeakBounds PeakBounds
        {
            get;
        }

        public ExemplaryPeak ExemplaryPeak { get; }
    }


}
