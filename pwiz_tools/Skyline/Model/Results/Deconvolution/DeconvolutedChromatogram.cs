using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pwiz.Skyline.Model.Results.Deconvolution
{
    public class DeconvolutedChromatogram
    {
        public DeconvolutedChromatogram(DeconvolutionKey key, TimeIntensities timeIntensities)
        {
            Key = key;
            TimeIntensities = timeIntensities;
        }

        public DeconvolutionKey Key { get; private set; }
        public TimeIntensities TimeIntensities { get; private set; }
    }
}
