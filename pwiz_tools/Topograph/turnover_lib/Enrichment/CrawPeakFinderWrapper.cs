using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using pwiz.Crawdad;

namespace pwiz.Topograph.Enrichment
{
    public class CrawPeakFinderWrapper
    {
        private readonly CrawdadPeakFinder _crawdadPeakFinder;
        private const int MIN_TOLERANCE_LEN = 4;
        private const int MIN_TOLERANCE_SMOOTH_FWHM = 3;
        private const float FRACTION_FWHM_LEN = 0.5F;
        private const float DESCENT_TOL = 0.005f;
        private const float ASCENT_TOL = 0.50f;
        private IList<double> _times;
        private IList<double> _intensities;

        public CrawPeakFinderWrapper()
        {
            _crawdadPeakFinder = new CrawdadPeakFinder();
        }

        public void SetChromatogram(IList<double> times, IList<double> intensities)
        {
            _times = times;
            _intensities = intensities;
            _crawdadPeakFinder.SetChromatogram(times, intensities);
        }

        public List<CrawdadPeak> CalcPeaks(int maxPeaks)
        {
            var result = new List<CrawdadPeak>();
            foreach (var crawdadPeak in _crawdadPeakFinder.CalcPeaks(maxPeaks))
            {
                Extend(crawdadPeak);
                result.Add(crawdadPeak);
            }
            return result;
        }

        private void Extend(CrawdadPeak crawdadPeak)
        {
            // Look a number of steps dependent on the width of the peak, since interval width
            // may vary.
            int toleranceLen = Math.Max(MIN_TOLERANCE_LEN, (int)Math.Round(crawdadPeak.Fwhm * FRACTION_FWHM_LEN));

            crawdadPeak.StartIndex = ExtendBoundary(crawdadPeak, crawdadPeak.StartIndex, -1, toleranceLen);
            crawdadPeak.EndIndex = ExtendBoundary(crawdadPeak, crawdadPeak.EndIndex, 1, toleranceLen);
        }

        private int ExtendBoundary(CrawdadPeak peakPrimary, int indexBoundary, int increment, int toleranceLen)
        {
            if (peakPrimary.Fwhm >= MIN_TOLERANCE_SMOOTH_FWHM)
            {
                indexBoundary = ExtendBoundary(peakPrimary, false, indexBoundary, increment, toleranceLen);
            }
            // TODO:
            // Because smoothed data can have a tendency to reach baseline one
            // interval sooner than the raw data, do a final check to choose the
            // boundary correctly for the raw data.
            //indexBoundary = RetractBoundary(peakPrimary, true, indexBoundary, -increment);
            //indexBoundary = ExtendBoundary(peakPrimary, true, indexBoundary, increment, toleranceLen);
            return indexBoundary;
        }

        private int ExtendBoundary(CrawdadPeak peakPrimary, bool useRaw, int indexBoundary, int increment, int toleranceLen)
        {
            var intensities = _intensities;
            int lenIntensities = intensities.Count;
            var boundaryIntensity = intensities[indexBoundary];
            var maxIntensity = boundaryIntensity;
            // Look for a descent proportional to the height of the peak.  Because, SRM data is
            // so low noise, just looking for any descent can lead to boundaries very far away from
            // the peak.
            float height = peakPrimary.Height;
            double minDescent = height * DESCENT_TOL;
            // Put a limit on how high intensity can go before the search is terminated
            double maxHeight = ((height - boundaryIntensity) * ASCENT_TOL) + boundaryIntensity;

            // Extend the index in the direction of the increment
            for (int i = indexBoundary + increment;
                 i > 0 && i < lenIntensities - 1 && Math.Abs(indexBoundary - i) < toleranceLen;
                 i += increment)
            {
                double maxIntensityCurrent = intensities[i];

                // If intensity goes above the maximum, stop looking
                if (maxIntensityCurrent > maxHeight)
                    break;

                // If descent greater than tolerance, step until it no longer is
                while (maxIntensity - maxIntensityCurrent > minDescent)
                {
                    indexBoundary += increment;
                    if (indexBoundary == i)
                        maxIntensity = maxIntensityCurrent;
                    else
                        maxIntensityCurrent = intensities[indexBoundary];
                }
            }

            return indexBoundary;
        }

    }
}
