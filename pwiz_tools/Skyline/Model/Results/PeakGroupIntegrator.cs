using System.Collections.Generic;
using System.Linq;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Results
{
    public class PeakGroupIntegrator
    {
        private List<PeakIntegrator> _peakIntegrators;
        public PeakGroupIntegrator(FullScanAcquisitionMethod acquisitionMethod, TimeIntervals timeIntervals)
        {
            FullScanAcquisitionMethod = acquisitionMethod;
            TimeIntervals = timeIntervals;
            _peakIntegrators = new List<PeakIntegrator>();
        }

        public FullScanAcquisitionMethod FullScanAcquisitionMethod { get; }

        public TimeIntervals TimeIntervals { get; }

        public IEnumerable<PeakIntegrator> PeakIntegrators
        {
            get { return _peakIntegrators.AsEnumerable(); }
        }

        public void AddPeakIntegrator(PeakIntegrator peakIntegrator)
        {
            _peakIntegrators.Add(peakIntegrator);
        }

        public double GetTotalDdaIntensityAtTime(float time, int timeIndexHint)
        {
            double totalIntensity = 0;
            foreach (var peakIntegrator in PeakIntegrators)
            {
                if (peakIntegrator.ChromSource != ChromSource.fragment)
                {
                    continue;
                }

                var timeIntensities = peakIntegrator.RawTimeIntensities ?? peakIntegrator.InterpolatedTimeIntensities;
                float intensity;
                if (timeIndexHint >= 0 && timeIndexHint < timeIntensities.NumPoints && timeIntensities.Times[timeIndexHint] == time)
                {
                    intensity = timeIntensities.Intensities[timeIndexHint];
                }
                else
                {
                    intensity = timeIntensities.Intensities[timeIntensities.IndexOfNearestTime(time)];
                }

                totalIntensity += intensity;
            }

            return totalIntensity;
        }
    }
}
