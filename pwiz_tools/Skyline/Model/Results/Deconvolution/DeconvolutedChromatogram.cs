using System;
using System.Collections.Generic;
using pwiz.Common.Collections;

namespace pwiz.Skyline.Model.Results.Deconvolution
{
    // public class DeconvolutedChromatogram
    // {
    //     public DeconvolutedChromatogram(DeconvolutionKey key, TimeIntensities timeIntensities)
    //     {
    //         Key = key;
    //         TimeIntensities = timeIntensities;
    //     }
    //
    //     public DeconvolutionKey Key { get; private set; }
    //     public TimeIntensities TimeIntensities { get; private set; }
    // }
    //
    public class DeconvolutedChromatograms
    {
        public DeconvolutedChromatograms(IEnumerable<Tuple<DeconvolutionKey, TimeIntensities>> chromatograms,
            IEnumerable<double> scores)
        {
            Chromatograms = ImmutableList.ValueOf(chromatograms);
            Scores = ImmutableList.ValueOf(scores);
        }

        public ImmutableList<Tuple<DeconvolutionKey, TimeIntensities>> Chromatograms { get; private set; }
        public ImmutableList<double> Scores { get; private set; }
    }
}
