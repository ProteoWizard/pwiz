/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.Results
{
    public class TimeIntensities : Immutable
    {
        public static readonly TimeIntensities EMPTY = new TimeIntensities(ImmutableList<float>.EMPTY, ImmutableList<float>.EMPTY, null, null);
        public TimeIntensities(IEnumerable<float> times, IEnumerable<float> intensities, IEnumerable<float> massErrors, IEnumerable<int> scanIds)
        {
            Times = ImmutableList<float>.ValueOf(times);
            Intensities = ImmutableList.ValueOf(intensities);
            MassErrors = ImmutableList<float>.ValueOf(massErrors);
            ScanIds = ImmutableList<int>.ValueOf(scanIds);
        }

        public ImmutableList<float> Times { get; private set; }
        public ImmutableList<float> Intensities { get; private set; }
        public ImmutableList<float> MassErrors { get; private set; }
        public ImmutableList<int> ScanIds { get; private set; }
        public int NumPoints { get { return Times.Count; } }

        public TimeIntensities ChangeMassErrors(IEnumerable<float> massErrors)
        {
            return ChangeProp(ImClone(this), im => im.MassErrors = ImmutableList.ValueOf(massErrors));
        }

        public TimeIntensities ChangeScanIds(IEnumerable<int> scanIds)
        {
            return ChangeProp(ImClone(this), im => im.ScanIds = ImmutableList.ValueOf(scanIds));
        }

        public TimeIntensities ChangeIntensities(IEnumerable<float> intensities)
        {
            return ChangeProp(ImClone(this), im => im.Intensities = ImmutableList.ValueOf(intensities));
        }

        public TimeIntensities Interpolate(IList<float> timesNew, bool inferZeros)
        {
            if (timesNew.Count == 0)
                return this;
            double intervalDelta = 0;
            if (timesNew.Count > 1)
            {
                intervalDelta = (timesNew[timesNew.Count - 1] - timesNew[0]) / (timesNew.Count - 1);
            }

            var timesMeasured = Times;
            var intensMeasured = Intensities;
            IList<float> massErrorsMeasured = MassErrors;

            var intensNew = new List<float>();
            var massErrorsNew = massErrorsMeasured != null ? new List<float>() : null;

            int iTime = 0;
            double timeLast = timesNew[0];
            double intenLast = 0;
            double massErrorLast = 0;
            if (!inferZeros && intensMeasured.Count != 0)
            {
                intenLast = intensMeasured[0];
                if (massErrorsMeasured != null)
                    massErrorLast = massErrorsMeasured[0];
            }
            for (int i = 0; i < timesMeasured.Count && iTime < timesNew.Count; i++)
            {
                double intenNext;
                float time = timesMeasured[i];
                float inten = intensMeasured[i];
                double totalInten = inten;
                double massError = 0;
                if (massErrorsMeasured != null)
                    massError = massErrorsMeasured[i];

                // Continue enumerating points until one is encountered
                // that has a greater time value than the point being assigned.
                while (i < timesMeasured.Count - 1 && time < timesNew[iTime])
                {
                    i++;
                    time = timesMeasured[i];
                    inten = intensMeasured[i];

                    if (massErrorsMeasured != null)
                    {
                        // Average the mass error in these points weigthed by intensity
                        // into the next mass error value
                        totalInten += inten;
                        // TODO: Figure out whether this is an appropriate estimation method
                        massError += (massErrorsMeasured[i] - massError) * inten / totalInten;
                    }
                }

                if (i >= timesMeasured.Count)
                    break;

                // If the next measured intensity is more than the new delta
                // away from the intensity being assigned, then interpolate
                // the next point toward zero, and set the last intensity to
                // zero.
                if (inferZeros && intenLast > 0 && timesNew[iTime] + intervalDelta < time)
                {
                    intenNext = intenLast +
                                (timesNew[iTime] - timeLast) * (0 - intenLast) /
                                (timesNew[iTime] + intervalDelta - timeLast);
                    intensNew.Add((float)intenNext);
                    AddMassError(massErrorsNew, massError);
                    timeLast = timesNew[iTime++];
                    intenLast = 0;
                }

                if (inferZeros)
                {
                    // If the last intensity was zero, and the next measured time
                    // is more than a delta away, assign zeros until within a
                    // delta of the measured intensity.
                    while (intenLast == 0 && iTime < timesNew.Count && timesNew[iTime] + intervalDelta < time)
                    {
                        intensNew.Add(0);
                        AddMassError(massErrorsNew, massError);
                        timeLast = timesNew[iTime++];
                    }
                }
                else
                {
                    // Up to just before the current point, project the line from the
                    // last point to the current point at each interval.
                    while (iTime < timesNew.Count && timesNew[iTime] + intervalDelta < time)
                    {
                        intenNext = intenLast + (timesNew[iTime] - timeLast) * (inten - intenLast) / (time - timeLast);
                        intensNew.Add((float)intenNext);
                        AddMassError(massErrorsNew, massError);
                        iTime++;
                    }
                }

                if (iTime >= timesNew.Count)
                    break;

                // Interpolate from the last intensity toward the measured
                // intenisty now within a delta of the point being assigned.
                if (time == timeLast)
                    intenNext = intenLast;
                else
                    intenNext = intenLast + (timesNew[iTime] - timeLast) * (inten - intenLast) / (time - timeLast);
                intensNew.Add((float)intenNext);
                massErrorLast = AddMassError(massErrorsNew, massError);
                iTime++;
                intenLast = inten;
                timeLast = time;
            }

            // Fill any unassigned intensities with zeros.
            while (intensNew.Count < timesNew.Count)
            {
                intensNew.Add(0);
                AddMassError(massErrorsNew, massErrorLast);
            }
            int[] scanIndexesNew = null;
            // Replicate scan ids to match new times.
            if (ScanIds != null)
            {
                scanIndexesNew = new int[timesNew.Count];
                int rawIndex = 0;
                for (int i = 0; i < timesNew.Count; i++)
                {
                    // Choose the RawScanId corresponding to the closest RawTime to the new time.
                    float newTime = timesNew[i];
                    while (rawIndex < Times.Count && Times[rawIndex] <= newTime)
                        rawIndex++;
                    if (rawIndex >= Times.Count)
                        rawIndex--;
                    if (rawIndex > 0 && newTime - Times[rawIndex - 1] < Times[rawIndex] - newTime)
                        rawIndex--;
                    scanIndexesNew[i] = ScanIds[rawIndex];
                }
            }

            IEnumerable<float> massErrorsNewTruncated = null;
            if (massErrorsNew != null)
            {
                // Round off all the mass errors, since that is what will be persisted in the .skyd file
                massErrorsNewTruncated = massErrorsNew.Select(error => ChromPeak.To10x(error)/10f);
            }
            return new TimeIntensities(timesNew, intensNew, massErrorsNewTruncated, scanIndexesNew);
        }
        private static float AddMassError(ICollection<float> massErrors, double massError)
        {
            if (massErrors != null)
            {
                massErrors.Add((float) massError);
                return (float) massError;
            }
            return 0;
        }

        /// <summary>
        /// Adds the intensities from the other TimeIntensities to the intensities in this.
        /// The returned TimeIntensities will have the same set of times as this.
        /// </summary>
        public TimeIntensities AddIntensities(TimeIntensities other)
        {
            if (!Times.Equals(other.Times))
            {
                other = other.Interpolate(Times, false);
            }
            float[] newIntensities = new float[Times.Count];
            for (int i = 0; i < Times.Count; i++)
            {
                // Avoid arithmetic overflow
                double intensitySum = Intensities[i] + other.Intensities[i];
                newIntensities[i] = intensitySum < float.MaxValue ? (float)intensitySum : float.MaxValue;
            }
            return new TimeIntensities(Times, newIntensities, null, null);
        }

        /// <summary>
        /// Adds the intensities from the other TimeIntensities to the intensities in this.
        /// The returned TimeIntensities whill have a set of times which is the union of the 
        /// times in this and <paramref name="other"/>.
        /// </summary>
        public TimeIntensities MergeTimesAndAddIntensities(TimeIntensities other)
        {
            if (Times.Equals(other.Times))
            {
                return AddIntensities(other);
            }
            var mergedTimes = ImmutableList.ValueOf(Times.Concat(other.Times).Distinct().OrderBy(time => time));
            if (mergedTimes.Equals(Times))
            {
                return AddIntensities(other);
            }
            return Interpolate(mergedTimes, false).AddIntensities(other);
        }

        public TimeIntensities Truncate(double minRetentionTime, double maxRetentionTime)
        {
            int firstIndex = CollectionUtil.BinarySearch(Times, (float) minRetentionTime);
            if (firstIndex < 0)
            {
                firstIndex = ~firstIndex - 1;
            }
            firstIndex = Math.Max(firstIndex, 0);
            int lastIndex = CollectionUtil.BinarySearch(Times, (float) maxRetentionTime);
            if (lastIndex < 0)
            {
                lastIndex = ~lastIndex;
            }
            lastIndex = Math.Min(lastIndex, NumPoints - 1);
            if (firstIndex == 0 && lastIndex == NumPoints - 1)
            {
                return this;
            }
            return new TimeIntensities(
                SubList(Times, firstIndex, lastIndex),
                SubList(Intensities, firstIndex, lastIndex),
                SubList(MassErrors, firstIndex, lastIndex),
                SubList(ScanIds, firstIndex, lastIndex));
        }

        private static IEnumerable<T> SubList<T>(IList<T> list, int firstIndex, int lastIndex)
        {
            if (list == null)
            {
                return null;
            }
            return list.Skip(firstIndex).Take(lastIndex - firstIndex + 1);
        }

        public double Integral(int startIndex, int endIndex)
        {
            if (startIndex >= endIndex)
            {
                return 0;
            }

            double total = 0;
            
            for (int i = startIndex + 1; i < endIndex; i++)
            {
                total += Intensities[i] * (Times[i + 1] - Times[i - 1]) / 2;
            }
            total += Intensities[startIndex] * (Times[startIndex + 1] - Times[startIndex]) / 2;
            total += Intensities[endIndex] * (Times[endIndex] - Times[endIndex - 1]) / 2;
            return total;
        }

        public int IndexOfNearestTime(float time)
        {
            int iTime = CollectionUtil.BinarySearch(Times, time);
            if (iTime < 0)
            {
                // Get index of first time greater than time argument
                iTime = ~iTime;
                // If the value before it was closer, then use that time
                if (iTime == Times.Count || (iTime > 0 && Times[iTime] - time > time - Times[iTime - 1]))
                    iTime--;
            }
            return iTime;
        }

        /// <summary>
        /// Return a new TimeIntensities which includes the specified time point.
        /// The intensities and mass errors will be interpolated using the two values on either side
        /// of the inserted time.
        /// </summary>
        public TimeIntensities InterpolateTime(float newTime)
        {
            int index = CollectionUtil.BinarySearch(Times, newTime);
            if (index >= 0)
            {
                return this;
            }

            index = ~index;
            double newIntensity;
            double newMassError = 0;
            int newScanId = 0;
            if (index == 0)
            {
                newIntensity = Intensities[0];
                newMassError = MassErrors?[0] ?? 0;
                newScanId = ScanIds?[0] ?? 0;
            }
            else if (index >= Times.Count)
            {
                newIntensity = Intensities[NumPoints - 1];
                newMassError = MassErrors?[NumPoints - 1] ?? 0;
                newScanId = ScanIds?[NumPoints - 1] ?? 0;
            }
            else
            {
                double intensity1 = Intensities[index - 1];
                double intensity2 = Intensities[index];
                double time1 = Times[index - 1];
                double time2 = Times[index];
                double width = time2 - time1;
                newIntensity = (intensity2 * (newTime - time1) + intensity1 * (time2 - newTime)) / width;
                if (MassErrors != null)
                {
                    double massError1 = MassErrors[index - 1];
                    double massError2 = MassErrors[index];
                    double weight1 = intensity1 * (time2 - newTime);
                    double weight2 = intensity2 * (newTime - time1);
                    if (weight1 + weight2 > 0)
                    {
                        newMassError = (weight1 * massError1 + weight2 * massError2) / (weight1 + weight2);
                    }
                }

                if (ScanIds != null)
                {
                    if (newTime - time1 < time2 - newTime)
                    {
                        newScanId = ScanIds[index - 1];
                    }
                    else
                    {
                        newScanId = ScanIds[index];
                    }
                }
            }

            var newTimes = Times.ToList();
            newTimes.Insert(index, newTime);
            var newIntensities = Intensities.ToList();
            newIntensities.Insert(index, (float) newIntensity);
            IList<float> newMassErrors = null;
            if (MassErrors != null)
            {
                newMassErrors = MassErrors.ToList();
                newMassErrors.Insert(index, (float) newMassError);
            }

            IList<int> newScanIds = null;
            if (ScanIds != null)
            {
                newScanIds = ScanIds.ToList();
                newScanIds.Insert(index, newScanId);
            }

            return new TimeIntensities(newTimes, newIntensities, newMassErrors, newScanIds);
        }
    }
}