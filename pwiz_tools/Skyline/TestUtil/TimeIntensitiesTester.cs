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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.Results;

namespace pwiz.SkylineTestUtil
{
    public class TimeIntensitiesTester
    {
        public TimeIntensitiesTester()
        {
            Epsilon = 1E-3;
            MaxNestedLoopInteration = 10;
        }
        public double Epsilon { get; set; }
        public int MaxNestedLoopInteration { get; set; }
        public TimeIntensities Verify(TimeIntensities timeIntensities)
        {
            VerifyIntegrationOnAllSubIntervals(timeIntensities);
            return timeIntensities;
        }

        public TimeIntensities Interpolate(TimeIntensities timeIntensities, IList<float> times)
        {
            return ReplaceTimes(timeIntensities, times, false, false);
        }

        public TimeIntensities ReplaceTimes(TimeIntensities timeIntensities, IList<float> times, bool inferZeroes, bool extrapolateZeroes)
        {
            var interpolated = timeIntensities.ReplaceTimes(times, inferZeroes, extrapolateZeroes);
            AssertListsEqual(times.ToArray(), interpolated.Times.ToArray());
            if (!inferZeroes)
            {
                VerifyInterpolationPreservedArea(timeIntensities, interpolated);
            }
            return interpolated;
        }

        /// <summary>
        /// Verifies that interpolating a TimeIntensities preserves the integral across the points that 
        /// were unchanged along with both of their neighbors.
        /// </summary>
        public void VerifyInterpolationPreservedArea(TimeIntensities original, TimeIntensities interpolated)
        {
            var times = interpolated.Times;
            var originalIndexes = times.Select(t => CollectionUtil.BinarySearch(original.Times, t)).ToArray();
            // Verify the area preserving quality of interpolation
            for (int index1 = 0; index1 < times.Count - 3; index1++)
            {
                if (originalIndexes[index1] < 0)
                {
                    continue;
                }
                if (originalIndexes[index1] + 1 != originalIndexes[index1 + 1] || originalIndexes[index1] + 2 != originalIndexes[index1 + 2])
                {
                    continue;
                }
                // If both neighbors of a point are still the neighbor, then the middle point should not have moved.
                Assert.AreEqual(original.Intensities[originalIndexes[index1] + 1], interpolated.Intensities[index1 + 1]);
                if (original.MassErrors != null)
                {
                    Assert.AreEqual(original.MassErrors[originalIndexes[index1] + 1], interpolated.MassErrors[index1 + 1]);
                }
                if (original.ScanIds != null)
                {
                    Assert.AreEqual(original.ScanIds[originalIndexes[index1] + 1], interpolated.ScanIds[index1 + 1]);
                }
                for (int index2 = index1 + 2; index2 < times.Count - 2; index2++)
                {
                    if (originalIndexes[index2] < 0)
                    {
                        continue;
                    }
                    if (originalIndexes[index2] + 1 != originalIndexes[index2 + 1] || originalIndexes[index2 + 2] != originalIndexes[index2] + 2)
                    {
                        continue;
                    }

                    float startTime = times[index1 + 1];
                    float endTime = times[index2 + 1];
                    double originalArea, originalMassError;
                    original.Integrate(startTime, endTime, out originalArea, out originalMassError);
                    double interpolatedArea, interpolatedMassError;
                    interpolated.Integrate(startTime, endTime, out interpolatedArea, out interpolatedMassError);
                    Assert.AreEqual(originalArea, interpolatedArea, Epsilon);
                    Assert.AreEqual(originalMassError, interpolatedMassError, Epsilon);
                }
            }
        }


        /// <summary>
        /// Verifies that for all values a, b, c, integral(a,c) = integral(a,b) + integral(b,c).
        /// </summary>
        public void VerifyIntegrationOnAllSubIntervals(TimeIntensities timeIntensities)
        {
            int stepSize = Math.Max(1, timeIntensities.NumPoints / MaxNestedLoopInteration);
            for (int index1 = 0; index1 < timeIntensities.NumPoints; index1+= stepSize)
            {
                double t1 = timeIntensities.Times[index1];
                for (int index2 = index1 + 1; index2 < timeIntensities.NumPoints; index2 += stepSize)
                {
                    double t2 = timeIntensities.Times[index2];
                    VerifyIntegrationOnSubInterval(timeIntensities, t1, (t1 + t2) / 2, t2);
                    for (int index3 = index2 + 1; index3 < timeIntensities.NumPoints; index3+=stepSize)
                    {
                        double t3 = timeIntensities.Times[index3];
                        VerifyIntegrationOnSubInterval(timeIntensities, t1, t2, t3);
                    }
                }
            }
        }

        public void VerifyIntegrationOnSubInterval(TimeIntensities timeIntensities, double t1, double t2, double t3)
        {
            double fullArea, fullMassError;
            timeIntensities.Integrate(t1, t3, out fullArea, out fullMassError);
            double firstArea, firstMassError;
            timeIntensities.Integrate(t1, t2, out firstArea, out firstMassError);
            double secondArea, secondMassError;
            timeIntensities.Integrate(t2, t3, out secondArea, out secondMassError);
            AssertEqual(fullArea, firstArea + secondArea);
            if (fullArea > 0)
            {
                var subMassError = (firstMassError * firstArea + secondMassError * secondArea) / (firstArea + secondArea);
                AssertEqual(fullMassError, subMassError);
            }
        }

        public void AssertEqual(double expected, double actual)
        {
            var allowableDifference = Math.Max(Epsilon,
                Math.Min(Epsilon * Math.Abs(expected), Epsilon * Math.Abs(actual)));
            if (Math.Abs(expected - actual) > allowableDifference)
            {
                Assert.AreEqual(expected, actual, allowableDifference);
            }
        }
        public void AssertListsEqual<T>(IList<T> expected, IList<T> actual)
        {
            Assert.AreEqual(expected.Count, actual.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.AreEqual(expected[i], actual[i], "Elements at position {0} differ", i);
            }
        }
    }
}
