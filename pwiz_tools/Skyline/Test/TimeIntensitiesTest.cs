/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using MathNet.Numerics.Random;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class TimeIntensitiesTest : AbstractUnitTest
    {
        private TimeIntensitiesTester _tester = new TimeIntensitiesTester();
        [TestMethod]
        public void TestMergeTimesAndAddIntensities()
        {
            TimeIntensities oddTimes = _tester.Verify(new TimeIntensities(new []{1f,3,5,7,9}, new []{3f,6,9,12,15}, null, null));
            TimeIntensities evenTimes = _tester.Verify(new TimeIntensities(new[] { 2f, 4, 6, 8 }, new[] { 10f, 9, 8, 7 }, null, null));
            var expectedMergedTimes = ImmutableList.ValueOf(new[] {1f, 2, 3, 4, 5, 6, 7, 8, 9});
            var mergedAndAdded = oddTimes.MergeTimesAndAddIntensities(evenTimes);
            var mergedAndAdded2 = evenTimes.MergeTimesAndAddIntensities(oddTimes);
            Assert.AreEqual(expectedMergedTimes, mergedAndAdded.Times);
            Assert.AreEqual(expectedMergedTimes, mergedAndAdded2.Times);
            Assert.AreEqual(mergedAndAdded.Intensities, mergedAndAdded2.Intensities);

            var oddInterpolated = _tester.ReplaceTimes(oddTimes, expectedMergedTimes, false, true);
            var evenInterpolated = _tester.ReplaceTimes(evenTimes, expectedMergedTimes, false, true);
            var sumOfInterpolatedIntensities = oddInterpolated.Intensities
                .Zip(evenInterpolated.Intensities, (f1, f2) => f1 + f2)
                .ToArray();
            AssertListEqual(sumOfInterpolatedIntensities, mergedAndAdded.Intensities);

            var addedToOdds = oddTimes.AddIntensities(evenTimes);
            Assert.AreEqual(oddTimes.Times, addedToOdds.Times);
            var addedToEvens = evenTimes.AddIntensities(oddTimes);
            Assert.AreEqual(evenTimes.Times, addedToEvens.Times);
        }

        [TestMethod]
        public void TestInterpolateShift()
        {
            TimeIntensities timeIntensities = new TimeIntensities(new []{1f,2,3,4}, new[]{1f,2,3,4}, null, null);
            var shifted = timeIntensities.Interpolate(new [] {1.1f, 2.1f, 3.1f, 4.1f}, false);
            Assert.AreEqual(1.1f, shifted.Times.First());
        }

        [TestMethod]
        public void TestRemovePoint()
        {
            TimeIntensities timeIntensities = new TimeIntensities(new []{1f,2,3,4,5}, new []{1f,2,3,2,1}, null, null);
            var pointList = TimeIntensityPoint.RemovePointsAt(timeIntensities.ToPoints().ToArray(), new[]{2});
            AssertListEqual(new[]{1.0,2,4,5}, pointList.Select(p=>p.Time).ToArray());
            Assert.AreEqual(1.0, pointList[0].Intensity);
            Assert.AreEqual(1.0, pointList[3].Intensity);
            Assert.AreEqual(7.0 / 3, pointList[1].Intensity, 1E-7);
            Assert.AreEqual(7.0 / 3, pointList[2].Intensity, 1E-7);
        }

        [TestMethod]
        public void TestRemovePoint2()
        {
            var originalPoints = new[]
            {
                new TimeIntensityPoint(1, 1, 0, 0),
                new TimeIntensityPoint(2, 2, 0, 0),
                new TimeIntensityPoint(2.9, 10, 0, 0),
                new TimeIntensityPoint(4, 3, 0, 0),
                new TimeIntensityPoint(5, 2, 0, 0)
            };
            var originalTotalArea = TimeIntensityPoint.Integrate(1, 5, originalPoints).Intensity;
            var newPoints = TimeIntensityPoint.RemovePointsAt(originalPoints, new[]{2});
            var newTotalArea = TimeIntensityPoint.Integrate(1, 5, newPoints.ToArray()).Intensity;
            Assert.AreEqual(originalTotalArea, newTotalArea);
            var timeIntensities = new TimeIntensities(new []{1,2,2.9f,4,5}, new[]{1f,2,10,3,2}, null, null);
            var interpolated = _tester.Interpolate(timeIntensities, new[] {1f, 2, 4, 5});
            Assert.IsNotNull(interpolated);
        }

        [TestMethod]
        public void TestInterpolateStraightLine()
        {
            var slopeIntercepts = new[] {Tuple.Create(1f, 0f), Tuple.Create(3f, 2f)};
            foreach (var slopeIntercept in slopeIntercepts)
            {
                float slope = slopeIntercept.Item1;
                float intercept = slopeIntercept.Item2;
                var interpolatedTimes = new[] { 0f, 0.2f, 0.45f, 0.5f, 0.6f, 0.75f, 0.9f };
                TimeIntensities timeIntensities = new TimeIntensities(new[] { 0f, 1f }, new[] { intercept, intercept + slope }, null, null);
                var interpolated = timeIntensities.Interpolate(interpolatedTimes, false);
                for (int i = 0; i < interpolated.NumPoints; i++)
                {
                    var expected = slope * interpolated.Times[i] + intercept;
                    if (expected != interpolated.Intensities[i])
                    {
                        Assert.AreEqual(expected, interpolated.Intensities[i]);
                    }
                }
                _tester.Verify(interpolated);
            }
        }

        [TestMethod]
        public void TestTimeIntensitiesAsymmetricIsolationTestValues()
        {
            var timeIntensities = new TimeIntensities(new[]
            {
                99.4438f, 99.5009155f, 99.558f, 99.6151f, 99.6722f, 99.72929f, 99.78637f, 99.84347f,
                99.90055f, 99.9576645f, 100.014748f, 100.071831f, 100.128937f, 100.186035f, 100.243118f, 100.300217f, 
                100.3573f, 100.4144f, 100.471481f, 100.52858f, 100.58567f, 100.642769f, 100.6999f, 100.757f, 
                100.8141f, 100.8712f, 100.9283f, 100.985382f, 101.042465f, 101.099564f, 101.15667f,
            }, new[]
            {
                403f, 291f, 422f, 412f, 342f, 413f, 353f, 545f, 
                861f, 1417f, 031f, 3134f, 3965f, 5313f, 6787f, 6314f,
                6466f, 5428f, 4674f, 3602f, 3058f, 2307f, 1427f, 1243f,
                1111f, 810f, 660f, 668f, 639f, 752f, 625f,
            }, new[]
            {
                -18.847065f, -26.8238564f, -1.94144917f, -7.206021f, -19.1371536f, 17.8624325f, -6.79865456f, -15.6344109f, 
                -6.10711575f, -16.8071232f, -18.229414f, -15.9091911f, -10.1931458f, -16.0022926f, -12.6276522f, -13.7935066f,
                -14.0671473f, -14.4990721f, -9.203463f, -15.0001087f, -9.346336f, -10.6277065f, -8.416262f, -7.35153866f,
                -1.45148933f, -11.6552305f, -25.0683174f, -19.510561f, -24.40466f, -26.9654217f, -15.690608f,
            }, null);
            var interpolatedTimes = new[]
            {
                99.4438f, 99.5009f, 99.558f, 99.6151047f, 99.6722f, 99.7293f, 99.7864f, 99.8435f,
                99.9006042f, 99.9577f, 100.0148f, 100.0719f, 100.129005f, 100.186104f, 100.2432f, 100.3003f,
                100.3574f, 100.414505f, 100.4716f, 100.5287f, 100.5858f, 100.6429f, 100.700005f, 100.7571f,
                100.8142f, 100.8713f, 100.9284f, 100.985504f, 101.0426f, 101.0997f,
            };
            var oldInterpolated = timeIntensities.OldInterpolate(interpolatedTimes, false);
            var newInterpolated = timeIntensities.Interpolate(interpolatedTimes, false);
            AssertListEqual(interpolatedTimes, oldInterpolated.Times.ToArray());
            AssertListEqual(interpolatedTimes, newInterpolated.Times.ToArray());
        }

        [TestMethod]
        public void TestCompareInterpolation()
        {
            var timeIntensities = new TimeIntensities(
                new[] { 99.4438f, 99.5009155f, 99.558f, 99.6151f, 99.6722f, 99.72929f, },
                new[] { 403f, 291f, 422f, 412f, 342f, 413f, }, null, null);
            var interpolatedTimes = new[] { 99.4438f, 99.5009f, 99.558f, 99.6151047f, 99.6722f, 99.7293f };
            var newInterpolated = timeIntensities.Interpolate(interpolatedTimes, false);
            var oldInterpolated = timeIntensities.OldInterpolate(interpolatedTimes, false);
            AssertListEqual(interpolatedTimes, oldInterpolated.Times.ToArray());
            AssertListEqual(interpolatedTimes, newInterpolated.Times.ToArray());
        }

        [TestMethod]
        public void TestInterpolationOnRandomNumbers()
        {
            var tester = new TimeIntensitiesTester {Epsilon = 1e-7};
            foreach (int seed in new[] {0, (int) DateTime.Now.Ticks})
            {
                foreach (int nPoints in new[] {0, 1, 2, 3, 10, 100, 1000})
                {
                    try
                    {
                        var random = new Random(seed);
                        TestRandomPoints(tester, random, nPoints);
                    }
                    catch (Exception exception)
                    {
                        throw new Exception("Failure running scenario with seed " + seed + " and num points " + nPoints, exception);
                    }
                }
            }
        }

        private void TestRandomPoints(TimeIntensitiesTester tester, Random random, int nPoints)
        {
            var originalTimes = new[] { 0f, 1f }
                .Concat(random.NextDoubles(nPoints).Select(t => (float)t))
                .Distinct()
                .OrderBy(t => t).ToArray();
            Assert.AreEqual(0f, originalTimes[0]);
            Assert.AreEqual(1f, originalTimes[originalTimes.Length - 1]);
            var originalIntensities = random.NextDoubles(originalTimes.Length)
                .Select(intensity => (float)intensity)
                .ToArray();
            var originalMassErrors = random.NextDoubles(originalTimes.Length)
                .Select(massError => (float)massError)
                .ToArray();
            var timeIntensities = tester.Verify(new TimeIntensities(originalTimes, originalIntensities, originalMassErrors, null));
            double originalArea, originalMassError;
            timeIntensities.Integrate(0, 1, out originalArea, out originalMassError);
            Assert.IsTrue(originalArea >= 0);
            Assert.IsTrue(originalArea <= 1);
            var interpolatedTimes = originalTimes
                .Concat(random.NextDoubles(nPoints).Select(t => (float)t))
                .OrderBy(t => random.NextDouble())
                .Take(nPoints)
                .Concat(new[] { 0f, 1f })
                .Distinct()
                .OrderBy(t => t)
                .ToArray();
            Assert.AreEqual(0f, interpolatedTimes[0]);
            Assert.AreEqual(1f, interpolatedTimes[interpolatedTimes.Length - 1]);
            var interpolated = tester.Interpolate(timeIntensities, interpolatedTimes);
            double interpolatedArea, interpolatedMassError;
            interpolated.Integrate(0, 1, out interpolatedArea, out interpolatedMassError);
            Assert.AreEqual(originalArea, interpolatedArea, 1e-7);
            Assert.AreEqual(originalMassError, interpolatedMassError, 1e-7);
            // Do the interpolation in multiple steps and make sure that the result is the same.
            // And make sure that the result is the same regardless of whether we remove
            // points from the beginning or end.
            var interpolatedDoubleTimes = interpolatedTimes.Select(v => (double) v).ToArray();
            var interpolatedDoubleTimesSet = new HashSet<double>(interpolatedDoubleTimes);
            var mergedPoints = TimeIntensityPoint.AddTimes(timeIntensities.ToPointsArray(), interpolatedDoubleTimes, false, false);
            var pointsRemovedFromLeft = new List<TimeIntensityPoint>(mergedPoints);
            for (int i = 0; i < pointsRemovedFromLeft.Count; i++)
            {
                if (!interpolatedDoubleTimesSet.Contains(pointsRemovedFromLeft[i].Time))
                {
                    pointsRemovedFromLeft = TimeIntensityPoint.RemovePointsAt(pointsRemovedFromLeft, new[] {i});
                    i--;
                }
                var integral = TimeIntensityPoint.Integrate(0, 1, pointsRemovedFromLeft);
                Assert.AreEqual(originalArea, integral.Intensity, 1e-7);
                Assert.AreEqual(originalMassError, integral.MassError, 1e-7);
            }
            var pointsRemovedFromRight = new List<TimeIntensityPoint>(mergedPoints);
            for (int i = pointsRemovedFromRight.Count - 1; i >= 0; i--)
            {
                if (!interpolatedDoubleTimesSet.Contains(pointsRemovedFromRight[i].Time))
                {
                    pointsRemovedFromRight = TimeIntensityPoint.RemovePointsAt(pointsRemovedFromRight, new[] {i});
                }
                var integral = TimeIntensityPoint.Integrate(0, 1, pointsRemovedFromRight);
                Assert.AreEqual(originalArea, integral.Intensity, 1e-7);
                Assert.AreEqual(originalMassError, integral.MassError, 1e-7);
            }
            Assert.AreEqual(interpolated.NumPoints, pointsRemovedFromLeft.Count);
            Assert.AreEqual(interpolated.NumPoints, pointsRemovedFromRight.Count);
            AssertListEqual(interpolated.Intensities.ToArray(),
                pointsRemovedFromLeft.Select(p => (float) p.Intensity).ToArray());
            AssertListEqual(interpolated.Intensities.ToArray(),
                pointsRemovedFromRight.Select(p => (float) p.Intensity).ToArray());
        }

        [TestMethod]
        public void TestWeightedAverageTwoPoints()
        {
            foreach (int seed in new[] {0, (int) DateTime.UtcNow.Ticks})
            {
                try
                {
                    double epsilon = 1e-7;
                    var random = new Random(seed);
                    var point1 = new TimeIntensityPoint(random.NextDouble() * 100, random.NextDouble() * 1000, (random.NextDouble() -.5) * 100, random.Next());
                    var weight1 = random.NextDouble();
                    var point2 = new TimeIntensityPoint(random.NextDouble() * 100, random.NextDouble() * 1000, (random.NextDouble() -.5) * 100, random.Next());
                    var generalResult = TimeIntensityPoint.WeightedAverage(new[]
                        {Tuple.Create(point1, weight1), Tuple.Create(point2, 1 - weight1)});
                    var twoPointsResult = TimeIntensityPoint.WeightedAverageTwoPoints(point1.Time * weight1 + point2.Time * (1-weight1), point1, weight1, point2);
                    Assert.AreEqual(generalResult.Time, twoPointsResult.Time, epsilon);
                    Assert.AreEqual(generalResult.Intensity, twoPointsResult.Intensity, epsilon);
                    Assert.AreEqual(generalResult.MassError, twoPointsResult.MassError, epsilon);
                    Assert.AreEqual(generalResult.ScanIndex, twoPointsResult.ScanIndex, epsilon);
                }
                catch (Exception e)
                {
                    throw new Exception("Test failed using seed " + seed, e);
                }
            }
        }

        private void AssertListEqual<T>(IList<T> expected, IList<T> actual)
        {
            Assert.AreEqual(expected.Count, actual.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.AreEqual(expected[i], actual[i], "Elements at position {0} differ", i);
            }
        }
    }
}
