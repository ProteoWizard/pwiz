using System;
using pwiz.Common.PeakFinding;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    public class PeakIntegrator
    {
        public PeakIntegrator(TimeIntensities interpolatedTimeIntensities)
            : this(interpolatedTimeIntensities, CreatePeakFinder(interpolatedTimeIntensities))
        {
        }

        public PeakIntegrator(TimeIntensities interpolatedTimeIntensities, IPeakFinder peakFinder)
        {
            InterpolatedTimeIntensities = interpolatedTimeIntensities;
            PeakFinder = peakFinder;
        }

        public IPeakFinder PeakFinder { get; private set; }
        public TimeIntensities InterpolatedTimeIntensities { get; private set; }
        public TimeIntensities RawTimeIntensities { get; set; }
        public TimeIntervals TimeIntervals { get; set; }

        public ChromPeak IntegratePeak(float startTime, float endTime, ChromPeak.FlagValues flags)
        {
            if (TimeIntervals != null)
            {
                return IntegratePeakWithoutBackground(startTime, endTime, flags);
            }
            if (PeakFinder == null)
            {
                PeakFinder = CreatePeakFinder(InterpolatedTimeIntensities);
            }

            int startIndex = InterpolatedTimeIntensities.IndexOfNearestTime(startTime);
            int endIndex = InterpolatedTimeIntensities.IndexOfNearestTime(endTime);
            if (startIndex == endIndex)
            {
                return ChromPeak.EMPTY;
            }
            var foundPeak = PeakFinder.GetPeak(startIndex, endIndex);
            return new ChromPeak(PeakFinder, foundPeak, flags, InterpolatedTimeIntensities, RawTimeIntensities?.Times);
        }

        public Tuple<ChromPeak, IFoundPeak> IntegrateFoundPeak(IFoundPeak peakMax, ChromPeak.FlagValues flags)
        {
            Assume.IsNotNull(PeakFinder);
            var interpolatedPeak = PeakFinder.GetPeak(peakMax.StartIndex, peakMax.EndIndex);
            if ((flags & ChromPeak.FlagValues.forced_integration) != 0 && ChromData.AreCoeluting(peakMax, interpolatedPeak))
                flags &= ~ChromPeak.FlagValues.forced_integration;

            var chromPeak = new ChromPeak(PeakFinder, interpolatedPeak, flags, InterpolatedTimeIntensities, RawTimeIntensities?.Times);
            if (TimeIntervals != null)
            {
                chromPeak = IntegratePeakWithoutBackground(InterpolatedTimeIntensities.Times[peakMax.StartIndex], InterpolatedTimeIntensities.Times[peakMax.EndIndex], flags);
            }

            return Tuple.Create(chromPeak, interpolatedPeak);
        }

        private ChromPeak IntegratePeakWithoutBackground(float startTime, float endTime, ChromPeak.FlagValues flags)
        {
            if (TimeIntervals != null)
            {
                var intervalIndex = TimeIntervals.IndexOfIntervalEndingAfter(startTime);
                if (intervalIndex >= 0 && intervalIndex < TimeIntervals.Count)
                {
                    startTime = Math.Max(startTime, TimeIntervals.Starts[intervalIndex]);
                    endTime = Math.Min(endTime, TimeIntervals.Ends[intervalIndex]);
                }
            }
            return new ChromPeak(RawTimeIntensities ?? InterpolatedTimeIntensities, startTime, endTime, flags);
        }

        public static IPeakFinder CreatePeakFinder(TimeIntensities interpolatedTimeIntensities)
        {
            var peakFinder = PeakFinders.NewDefaultPeakFinder();
            peakFinder.SetChromatogram(interpolatedTimeIntensities.Times, interpolatedTimeIntensities.Intensities);
            return peakFinder;
        }
    }
}
