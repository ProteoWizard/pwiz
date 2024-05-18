using System.Collections.Generic;
using pwiz.Common.Collections;

namespace pwiz.Skyline.Model.Results.Imputation
{
    public class MoleculePeaks
    {
        public MoleculePeaks(IdentityPath identityPath, IEnumerable<RatedPeak> peaks)
        {
            PeptideIdentityPath = identityPath;
            Peaks = ImmutableList.ValueOf(peaks);
        }

        public IdentityPath PeptideIdentityPath { get; }

        public ImmutableList<RatedPeak> Peaks { get; }
    }
}