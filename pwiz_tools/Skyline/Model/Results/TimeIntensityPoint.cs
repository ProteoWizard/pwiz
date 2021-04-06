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
using System.Diagnostics;
using System.Linq;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    public sealed class TimeIntensityPoint
    {
        public static readonly TimeIntensityPoint ZERO = new TimeIntensityPoint(0, 0, 0, 0);
        public TimeIntensityPoint(double time, double intensity, double massError, int scanIndex)
        {
            Time = time;
            Intensity = intensity;
            MassError = massError;
            ScanIndex = scanIndex;
        }
        public double Time { get; private set; }
        public double Intensity { get; private set; }
        public double MassError { get; private set; }
        public int ScanIndex { get; private set; }

        public TimeIntensityPoint ChangeIntensity(double newIntensity)
        {
            return new TimeIntensityPoint(Time, newIntensity, MassError, ScanIndex);
        }

        public TimeIntensityPoint ChangeTime(double newTime)
        {
            return new TimeIntensityPoint(newTime, Intensity, MassError, ScanIndex);
        }

        public override string ToString()
        {
            string str = String.Format("Time: {0:R}, Intensity: {1:R}", Time, Intensity);
            if (MassError != 0)
            {
                str += ", MassError: " + MassError;
            }
            if (ScanIndex != 0)
            {
                str += ", ScanIndex: " + ScanIndex;
            }
            return str;
        }

        public static TimeIntensityPoint Zero(double time)
        {
            return new TimeIntensityPoint(time, 0, 0, 0);
        }

        public static TimeIntensityPoint Interpolate(TimeIntensityPoint prevPoint, TimeIntensityPoint nextPoint, double time)
        {
            double prevWeight = (nextPoint.Time - time) / (nextPoint.Time - prevPoint.Time);
            return WeightedAverageTwoPoints(time, prevPoint, prevWeight, nextPoint);
        }

        public static TimeIntensityPoint Integrate(double startTime, double endTime, IList<TimeIntensityPoint> points)
        {
            Assume.IsTrue(startTime >= points[0].Time);
            Assume.IsTrue(endTime <= points[points.Count - 1].Time);
            List<Tuple<TimeIntensityPoint, double>> pointWeights = new List<Tuple<TimeIntensityPoint, double>>();
            for (int i = 0; i < points.Count; i++)
            {
                if (i > 0)
                {
                    Assume.IsTrue(points[i].Time >= points[i - 1].Time);
                }
                var point = points[i];
                if (point.Time < startTime || point.Time > endTime)
                {
                    continue;
                }

                double startInterval;
                if (i == 0)
                {
                    startInterval = startTime;
                }
                else
                {
                    startInterval = Math.Max(startTime, (points[i - 1].Time + points[i].Time) / 2);
                }
                double endInterval;
                if (i == points.Count - 1)
                {
                    endInterval = endTime;
                }
                else
                {
                    endInterval = Math.Min(endTime, (points[i].Time + points[i + 1].Time) / 2);
                }

                if (endInterval > startInterval)
                {
                    pointWeights.Add(Tuple.Create(points[i], endInterval - startInterval));
                }
            }
            var averagePoint = WeightedAverage(pointWeights);
            return new TimeIntensityPoint(averagePoint.Time, averagePoint.Intensity * (endTime - startTime), averagePoint.MassError, averagePoint.ScanIndex);
        }

        public static TimeIntensityPoint WeightedAverage(IEnumerable<Tuple<TimeIntensityPoint, double>> pointWeights)
        {
            double totalWeight = 0;
            double totalTime = 0;
            double totalIntensity = 0;
            double totalMassError = 0;
            int bestScanIndex = 0;
            double maxWeight = double.MinValue;

            foreach (var pointWeight in pointWeights)
            {
                totalWeight += pointWeight.Item2;
                totalTime += pointWeight.Item1.Time * pointWeight.Item2;
                totalIntensity += pointWeight.Item1.Intensity * pointWeight.Item2;
                totalMassError += pointWeight.Item1.MassError * pointWeight.Item1.Intensity * pointWeight.Item2;
                if (pointWeight.Item2 > maxWeight)
                {
                    maxWeight = pointWeight.Item2;
                    bestScanIndex = pointWeight.Item1.ScanIndex;
                }
            }
            return new TimeIntensityPoint(totalTime / totalWeight, 
                totalIntensity / totalWeight, 
                totalIntensity == 0 ? 0 : totalMassError / totalIntensity, 
                bestScanIndex);
        }

        /// <summary>
        /// Just like <see cref="WeightedAverage"/> but optimized for the case where you want
        /// to take the average of two points, and the total weight is exactly 1.0.
        /// </summary>
        public static TimeIntensityPoint WeightedAverageTwoPoints(double targetTime, TimeIntensityPoint point1, double weight1,
            TimeIntensityPoint point2)
        {
            double weight2 = 1 - weight1;
            double intensity1 = point1.Intensity * weight1;
            double intensity2 = point2.Intensity * weight2;
            double totalIntensity = intensity1 + intensity2;
            return new TimeIntensityPoint(targetTime,
                totalIntensity,
                (point1.MassError * intensity1 + point2.MassError * intensity2) / totalIntensity,
                weight1 >= 0.5 ? point1.ScanIndex : point2.ScanIndex);
        }

        /// <summary>
        /// Returns a list of points with the specified indexes removed.
        /// The returned list of points will have the intensities adjusted so that the 
        /// integral over the entire range is preserved if possible.
        /// Note that if indexesToRemove contains either the first or last index in list,
        /// then it is not possible to preserve the integral.
        /// </summary>
        public static List<TimeIntensityPoint> RemovePointsAt(IList<TimeIntensityPoint> list, IList<int> indexesToRemove)
        {
            if (indexesToRemove.Count == 0)
            {
                return new List<TimeIntensityPoint>(list);
            }
            var result = new List<TimeIntensityPoint>(list.Count - indexesToRemove.Count);
            int removeIndex = 0;
            TimeIntensityPoint nextPoint = null;
            for (int iSrc = 0; iSrc < list.Count; iSrc++)
            {
                var pointToRemove = nextPoint ?? list[iSrc];
                if (removeIndex >= indexesToRemove.Count || indexesToRemove[removeIndex] != iSrc)
                {
                    result.Add(pointToRemove);
                    nextPoint = null;
                    continue;
                }
                removeIndex = removeIndex + 1;
                if (iSrc >= list.Count - 1 || result.Count == 0)
                {
                    continue;
                }
                var prevPoint = result[result.Count - 1];
                nextPoint = list[iSrc + 1];
                double previousPreviousTime = result.Count < 2 ? prevPoint.Time : result[result.Count - 2].Time;
                double nextNextTime = iSrc >= list.Count - 2 ? nextPoint.Time : list[iSrc + 2].Time;
                var previousIntervalStart = MidPoint(previousPreviousTime, prevPoint.Time);
                var nextIntervalEnd = MidPoint(nextPoint.Time, nextNextTime);
                var newPoints = RemovePointBetween(previousIntervalStart, prevPoint, pointToRemove, nextPoint, nextIntervalEnd);
                result[result.Count - 1] = newPoints.Item1;
                nextPoint = newPoints.Item2;
            }
            return result;
        }

        /// <summary>
        /// Figures out what the neighbors of a removed point need to be changed to in order 
        /// to preserve the integral between the previous and next neighbors.
        /// </summary>
        public static Tuple<TimeIntensityPoint, TimeIntensityPoint> RemovePointBetween(
            double prevTimeBegin, TimeIntensityPoint prevPoint, 
            TimeIntensityPoint pointToRemove, 
            TimeIntensityPoint nextPoint, double nextTimeEnd)
        {
            double newMidPoint = MidPoint(prevPoint.Time, nextPoint.Time);
            double oldPrevInterval = MidPoint(prevPoint.Time, pointToRemove.Time) - prevTimeBegin;
            double newPrevInterval = newMidPoint - prevTimeBegin;
            double oldNextInterval = nextTimeEnd - MidPoint(pointToRemove.Time, nextPoint.Time);
            double newNextInterval = nextTimeEnd - newMidPoint;
            double prevWeight = oldPrevInterval / newPrevInterval;
            double nextWeight = oldNextInterval / newNextInterval;
            var newPrevious = WeightedAverageTwoPoints(prevPoint.Time, prevPoint, prevWeight, pointToRemove);
            var newNext = WeightedAverageTwoPoints(nextPoint.Time, nextPoint, nextWeight, pointToRemove);
            return Tuple.Create(newPrevious, newNext);
        }

        private static double MidPoint(double time1, double time2)
        {
            return (time1 + time2) / 2;
        }

        /// <summary>
        /// Returns a list of points which includes all of the first list of points, plus interpolated
        /// points at all of the new times specified.
        /// </summary>
        public static List<TimeIntensityPoint> AddTimes(IList<TimeIntensityPoint> points, IList<double> timesToAdd, bool inferZeroes, bool extrapolateZeroes)
        {
            List<TimeIntensityPoint> list = new List<TimeIntensityPoint>(points.Count + timesToAdd.Count);
            int addIndex = 0;
            int myIndex = 0;
            while (true)
            {
                int compare;
                if (myIndex < points.Count)
                {
                    if (addIndex < timesToAdd.Count)
                    {
                        compare = points[myIndex].Time.CompareTo(timesToAdd[addIndex]);
                    }
                    else
                    {
                        compare = -1;
                    }
                }
                else
                {
                    if (addIndex < timesToAdd.Count)
                    {
                        compare = 1;
                    }
                    else
                    {
                        break;
                    }
                }
                if (compare <= 0)
                {
                    list.Add(points[myIndex]);
                    myIndex++;
                    if (compare == 0)
                    {
                        addIndex++;
                    }
                }
                else
                {
                    double time = timesToAdd[addIndex];
                    TimeIntensityPoint prevPoint = null;
                    TimeIntensityPoint nextPoint = null;
                    if (myIndex > 0)
                    {
                        prevPoint = points[myIndex - 1];
                    }
                    if (myIndex < points.Count)
                    {
                        nextPoint = points[myIndex];
                    }
                    bool isZero = extrapolateZeroes && (prevPoint == null || nextPoint == null);
                    if (inferZeroes && prevPoint != null && nextPoint != null)
                    {
                        if (addIndex > 0 && addIndex + 1 < timesToAdd.Count)
                        {
                            double prevTime = timesToAdd[addIndex - 1];
                            double nextTime = timesToAdd[addIndex + 1];
                            if (prevTime > prevPoint.Time && nextTime < nextPoint.Time)
                            {
                                isZero = true;
                            }
                        }
                    }
                    TimeIntensityPoint pointToAdd;
                    if (isZero)
                    {
                        pointToAdd = Zero(time);
                    }
                    else
                    {
                        if (prevPoint != null && nextPoint != null)
                        {
                            pointToAdd = Interpolate(prevPoint, nextPoint, time);
                        }
                        else
                        {
                            pointToAdd = (prevPoint ?? nextPoint ?? ZERO).ChangeTime(time);
                        }
                    }
                    list.Add(pointToAdd);
                    addIndex++;
                }
            }
            VerifyAddTimesResult(points, timesToAdd, inferZeroes, extrapolateZeroes, list);
            return list;
        }
        [Conditional("DEBUG")]
        private static void VerifyAddTimesResult(IList<TimeIntensityPoint> points, IList<double> timesToAdd, bool inferZeroes, bool extrapolateZeroes, ICollection<TimeIntensityPoint> result)
        {
            Assume.IsFalse(timesToAdd.Except(result.Select(p => p.Time)).Any());
            if (points.Count == 0)
            {
                Assume.IsTrue(result.All(p => 0 == p.Intensity));
                return;
            }
            foreach (var point in result)
            {
                if (point.Time < points[0].Time)
                {
                    if (extrapolateZeroes)
                    {
                        Assume.IsTrue(point.Intensity == 0);
                    }
                    else
                    {
                        Assume.IsTrue(point.Intensity == points[0].Intensity);
                    }
                }
                else if (point.Time > points[points.Count - 1].Time)
                {
                    if (extrapolateZeroes)
                    {
                        Assume.IsTrue(point.Intensity == 0);
                    }
                    else
                    {
                        Assume.IsTrue(point.Intensity == points[points.Count- 1].Intensity);
                    }
                }
            }
        }

    }


}