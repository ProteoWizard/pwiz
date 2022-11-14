/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using pwiz.Common.PeakFinding;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{

    public class PeakIntegrator
    {
        public PeakIntegrator(TimeIntensities interpolatedTimeIntensities)
            : this(FullScanAcquisitionMethod.None, null, ChromSource.unknown, null, interpolatedTimeIntensities, null)
        {
        }

        public PeakIntegrator(FullScanAcquisitionMethod acquisitionMethod, TimeIntervals timeIntervals, ChromSource chromSource, TimeIntensities rawTimeIntensities, TimeIntensities interpolatedTimeIntensities, IPeakFinder peakFinder)
        {
            FullScanAcquisitionMethod = acquisitionMethod;
            TimeIntervals = timeIntervals;
            ChromSource = chromSource;
            RawTimeIntensities = rawTimeIntensities;
            InterpolatedTimeIntensities = interpolatedTimeIntensities;
            PeakFinder = peakFinder;
        }

        public FullScanAcquisitionMethod FullScanAcquisitionMethod { get; }
        public ChromSource ChromSource { get; }

        public IPeakFinder PeakFinder { get; private set; }
        public TimeIntensities InterpolatedTimeIntensities { get; }
        public TimeIntensities RawTimeIntensities { get; }
        public TimeIntervals TimeIntervals { get; }

        /// <summary>
        /// Return the ChromPeak with the specified start and end times chosen by a user.
        /// </summary>
        public ChromPeak IntegratePeak(float startTime, float endTime, ChromPeak.FlagValues flags)
        {
            if (!BackgroundSubtraction)
            {
                // For a triggered acquisition, we just use the start and end time supplied by the
                // user and Crawdad is not involved with the peak integration.
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

        /// <summary>
        /// Returns a ChromPeak and IFoundPeak that match the start and end times a particular other IFoundPeak
        /// that was found by Crawdad.
        /// </summary>
        public Tuple<ChromPeak, IFoundPeak> IntegrateFoundPeak(IFoundPeak peakMax, ChromPeak.FlagValues flags)
        {
            Assume.IsNotNull(PeakFinder);
            var interpolatedPeak = PeakFinder.GetPeak(peakMax.StartIndex, peakMax.EndIndex);
            if ((flags & ChromPeak.FlagValues.forced_integration) != 0 && ChromData.AreCoeluting(peakMax, interpolatedPeak))
                flags &= ~ChromPeak.FlagValues.forced_integration;

            var chromPeak = new ChromPeak(PeakFinder, interpolatedPeak, flags, InterpolatedTimeIntensities, RawTimeIntensities?.Times);
            if (!BackgroundSubtraction)
            {
                chromPeak = IntegratePeakWithoutBackground(InterpolatedTimeIntensities.Times[peakMax.StartIndex], InterpolatedTimeIntensities.Times[peakMax.EndIndex], flags);
            }

            return Tuple.Create(chromPeak, interpolatedPeak);
        }

        /// <summary>
        /// Returns a ChromPeak with the specified start and end times and no background subtraction.
        /// </summary>
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
            return ChromPeak.IntegrateWithoutBackground(RawTimeIntensities ?? InterpolatedTimeIntensities, startTime,
                endTime, flags);
        }

        public bool BackgroundSubtraction
        {
            get
            {
                return HasBackgroundSubtraction(FullScanAcquisitionMethod, TimeIntervals, ChromSource);
            }
        }

        public static bool HasBackgroundSubtraction(FullScanAcquisitionMethod acquisitionMethod,
            TimeIntervals timeIntervals, ChromSource chromSource)
        {
            if (timeIntervals != null)
            {
                return false;
            }

            if (FullScanAcquisitionMethod.DDA.Equals(acquisitionMethod) && chromSource == ChromSource.fragment)
            {
                return false;
            }

            return true;
        }

        public static IPeakFinder CreatePeakFinder(TimeIntensities interpolatedTimeIntensities)
        {
            var peakFinder = PeakFinders.NewDefaultPeakFinder();
            peakFinder.SetChromatogram(interpolatedTimeIntensities.Times, interpolatedTimeIntensities.Intensities);
            return peakFinder;
        }
    }
}
