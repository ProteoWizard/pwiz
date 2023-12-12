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
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.PeakFinding;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{

    public class PeakIntegrator
    {
        public PeakIntegrator(TimeIntensities interpolatedTimeIntensities)
            : this(new PeakGroupIntegrator(FullScanAcquisitionMethod.None, null), ChromSource.unknown, null, interpolatedTimeIntensities, null)
        {
        }

        public PeakIntegrator(PeakGroupIntegrator peakGroupIntegrator, ChromSource chromSource, TimeIntensities rawTimeIntensities, TimeIntensities interpolatedTimeIntensities, IPeakFinder peakFinder)
        {
            PeakGroupIntegrator = peakGroupIntegrator;
            ChromSource = chromSource;
            RawTimeIntensities = rawTimeIntensities;
            InterpolatedTimeIntensities = interpolatedTimeIntensities;
            PeakFinder = peakFinder;
        }

        public PeakGroupIntegrator PeakGroupIntegrator { get; }
        public FullScanAcquisitionMethod FullScanAcquisitionMethod
        {
            get { return PeakGroupIntegrator.FullScanAcquisitionMethod; }
        }
        public ChromSource ChromSource { get; }

        public IPeakFinder PeakFinder { get; private set; }
        public TimeIntensities InterpolatedTimeIntensities { get; }
        public TimeIntensities RawTimeIntensities { get; }
        public TimeIntervals TimeIntervals
        {
            get { return PeakGroupIntegrator.TimeIntervals; }
        }

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
            var chromPeak = new ChromPeak(PeakFinder, foundPeak, flags, InterpolatedTimeIntensities, RawTimeIntensities?.Times, PeakGroupIntegrator.GetMedianChromatogram(startTime, endTime));
            return chromPeak;
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

            var startTime = InterpolatedTimeIntensities.Times[peakMax.StartIndex];
            var endTime = InterpolatedTimeIntensities.Times[peakMax.EndIndex];
            var chromPeak = new ChromPeak(PeakFinder, interpolatedPeak, flags, InterpolatedTimeIntensities, RawTimeIntensities?.Times, PeakGroupIntegrator.GetMedianChromatogram(startTime, endTime));
            if (!BackgroundSubtraction)
            {
                chromPeak = IntegratePeakWithoutBackground(startTime, endTime, flags);
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
                    endTime = Math.Max(startTime, Math.Min(endTime, TimeIntervals.Ends[intervalIndex]));
                }
            }
            return ChromPeak.IntegrateWithoutBackground(RawTimeIntensities ?? InterpolatedTimeIntensities, startTime,
                endTime, flags, PeakGroupIntegrator.GetMedianChromatogram(startTime, endTime));
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

        public TimeIntensities GetChromatogramNormalizedToOne(float startTime, float endTime)
        {
            var timeIntensities = RawTimeIntensities ?? InterpolatedTimeIntensities;
            var times = new List<float>();
            var intensities = new List<float>();
            int index = CollectionUtil.BinarySearch(timeIntensities.Times, startTime);
            if (index < 0)
            {
                times.Add(startTime);
                intensities.Add(timeIntensities.GetInterpolatedIntensity(startTime));
                index = ~index;
            }

            for (; index < timeIntensities.NumPoints; index++)
            {
                var time = timeIntensities.Times[index];
                if (time > endTime)
                {
                    break;
                }
                times.Add(time);
                intensities.Add(timeIntensities.Intensities[index]);
            }

            if (times[times.Count - 1] < endTime)
            {
                times.Add(endTime);
                intensities.Add(timeIntensities.GetInterpolatedIntensity(endTime));
            }

            var max = intensities.Max();
            if (max == 0)
            {
                return new TimeIntensities(times, intensities);
            }

            return new TimeIntensities(times, intensities.Select(i => i / max));
        }
    }
}
