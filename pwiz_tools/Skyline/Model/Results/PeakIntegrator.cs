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
            if (TimeIntervals != null)
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
            var chromPeak = new ChromPeak(PeakFinder, foundPeak, flags, InterpolatedTimeIntensities, RawTimeIntensities?.Times);
            chromPeak = FixDdaPeakArea(chromPeak);
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

            var chromPeak = new ChromPeak(PeakFinder, interpolatedPeak, flags, InterpolatedTimeIntensities, RawTimeIntensities?.Times);
            if (TimeIntervals != null)
            {
                chromPeak = IntegratePeakWithoutBackground(InterpolatedTimeIntensities.Times[peakMax.StartIndex], InterpolatedTimeIntensities.Times[peakMax.EndIndex], flags);
            }

            chromPeak = FixDdaPeakArea(chromPeak);
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
            return new ChromPeak(RawTimeIntensities ?? InterpolatedTimeIntensities, startTime, endTime, flags);
        }

        /// <summary>
        /// If the Acquisition Method is DDA, then change the Area of the peak to
        /// be the intensity at the time point with the highest total MS1 intensity
        /// </summary>
        public ChromPeak FixDdaPeakArea(ChromPeak chromPeak)
        {
            if (ChromSource != ChromSource.fragment ||
                !FullScanAcquisitionMethod.DDA.Equals(PeakGroupIntegrator.FullScanAcquisitionMethod))
            {
                return chromPeak;
            }

            var timeIntensities = RawTimeIntensities ?? InterpolatedTimeIntensities;
            int? bestIndex = GetIndexOfBestDdaRetentionTime(timeIntensities, chromPeak.StartTime, chromPeak.EndTime);
            var flags = chromPeak.Flags;
            // Fwhm is always degenerate and peak is never truncated
            flags |= ChromPeak.FlagValues.degenerate_fwhm | ChromPeak.FlagValues.peak_truncation_known;
            flags &= ~(ChromPeak.FlagValues.forced_integration | ChromPeak.FlagValues.peak_truncated);
            if (!bestIndex.HasValue)
            {
                return new ChromPeak(chromPeak.RetentionTime, chromPeak.StartTime, chromPeak.EndTime, 0, 0, 0, 0,
                    flags, null, chromPeak.PointsAcross);
            }

            float retentionTime = timeIntensities.Times[bestIndex.Value];
            float height = timeIntensities.Intensities[bestIndex.Value];
            float? massError = timeIntensities.MassErrors?[bestIndex.Value];

            return new ChromPeak(retentionTime, chromPeak.StartTime, chromPeak.EndTime, height, 0, height, 0,
                flags, massError, chromPeak.PointsAcross);
        }

        /// <summary>
        /// Return the point in the range where the total MS2 intensity is the largest.
        /// </summary>
        public int? GetIndexOfBestDdaRetentionTime(TimeIntensities timeIntensities, float startTime, float endTime)
        {
            int? bestIndex = null;
            double bestTotalIntensity = 0;
            int index = CollectionUtil.BinarySearch(timeIntensities.Times, startTime);
            if (index < 0)
            {
                index = ~index;
            }

            for (; index < timeIntensities.NumPoints; index++)
            {
                var time = timeIntensities.Times[index];
                if (time > endTime)
                {
                    break;
                }

                var totalIntensity = PeakGroupIntegrator.GetTotalMs2IntensityAtTime(time, index);
                if (bestIndex == null || totalIntensity > bestTotalIntensity)
                {
                    bestIndex = index;
                    bestTotalIntensity = totalIntensity;
                }
            }

            return bestIndex;
        }

        public static IPeakFinder CreatePeakFinder(TimeIntensities interpolatedTimeIntensities)
        {
            var peakFinder = PeakFinders.NewDefaultPeakFinder();
            peakFinder.SetChromatogram(interpolatedTimeIntensities.Times, interpolatedTimeIntensities.Intensities);
            return peakFinder;
        }
    }
}
