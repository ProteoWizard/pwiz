using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    public class MedianPeakShape
    {
        public MedianPeakShape(IEnumerable<float> times, IEnumerable<float> intensities)
        {
            Times = ImmutableList.ValueOf(times);
            Intensities = ImmutableList.ValueOf(intensities);
        }
        public ImmutableList<float> Times { get; }
        public ImmutableList<float> Intensities { get; }

        public static MedianPeakShape GetMedianPeakShape(float startTime, float endTime,
            IList<TimeIntensities> chromatograms)
        {
            const int numPoints = 100;
            var normalizationFactors = chromatograms.Select(c => c.MaxIntensityInRange(startTime, endTime)).ToList();
            var times = Enumerable.Range(0, numPoints)
                .Select(i => startTime + (endTime - startTime) * i / (numPoints - 1)).ToList();
            var intensities = new List<float>(times.Count);
            foreach (var time in times)
            {
                var values = new List<double>();
                for (int iChromatogram = 0; iChromatogram < chromatograms.Count; iChromatogram++)
                {
                    float normalizationFactor = normalizationFactors[iChromatogram];
                    if (normalizationFactor != 0)
                    {
                        values.Add(chromatograms[iChromatogram].GetInterpolatedIntensity(time) / normalizationFactor);
                    }
                }

                if (values.Count == 0)
                {
                    intensities.Add(0);
                }
                else
                {
                    intensities.Add((float)new Statistics(values).Median());
                }
            }

            return new MedianPeakShape(times, intensities);
        }

        public double GetCorrelation(TimeIntensities chromatogram)
        {
            var intensities = Times.Select(t => (double) chromatogram.GetInterpolatedIntensity(t)).ToList();

            return MathNet.Numerics.Statistics.Correlation.Pearson(
                Intensities.Select(i => (double) i),
                intensities);
        }
    }
}
