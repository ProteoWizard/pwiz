using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pwiz.Skyline.Model.Results
{
    public struct PeakShapeValues
    {
        public PeakShapeValues(float stdDev, float skewness, float kurtosis)
        {
            StdDev = stdDev;
            Skewness = skewness;
            Kurtosis = kurtosis;
        }

        public float StdDev { get; }
        public float Skewness { get; }
        public float Kurtosis { get; }
    }
}
