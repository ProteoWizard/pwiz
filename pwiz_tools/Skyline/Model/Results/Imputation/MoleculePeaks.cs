using System.Collections.Generic;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.Results.Imputation
{
    public class MoleculePeaks : Immutable
    {
        public MoleculePeaks(IdentityPath identityPath, IEnumerable<RatedPeak> peaks)
        {
            PeptideIdentityPath = identityPath;
            Peaks = ImmutableList.ValueOf(peaks);
        }

        public IdentityPath PeptideIdentityPath { get; }
        public ImmutableList<RatedPeak> Peaks { get; private set; }

        public MoleculePeaks ChangePeaks(IEnumerable<RatedPeak> peaks, RatedPeak bestPeak,
            RatedPeak.PeakBounds exemplaryPeakBounds)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im.Peaks = ImmutableList.ValueOf(peaks);
                im.BestPeak = bestPeak;
                im.ExemplaryPeakBounds = exemplaryPeakBounds;
            });
        }
        public RatedPeak BestPeak { get; private set; }
        public double? AlignmentStandardTime { get; private set; }

        public MoleculePeaks ChangeAlignmentStandardTime(double? value)
        {
            return ChangeProp(ImClone(this), im => im.AlignmentStandardTime = value);
        }
        
        public RatedPeak.PeakBounds ExemplaryPeakBounds { get; private set; }
    }
}