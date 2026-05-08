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

using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class ChromPeakTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestChromPeakIntegration()
        {
            var times = Enumerable.Range(0, 12).Select(i => (float) i).ToArray();
            var timeIntensities = new TimeIntensities(times, times.Select(t => 36 - (t - 6) * (t - 6)), null, null);
            var chromPeak = ChromPeak.IntegrateWithoutBackground(timeIntensities, 1, 11, 0, null);
            Assert.AreEqual(36f, chromPeak.Height);
            Assert.AreEqual(11, timeIntensities.Intensities[1]);
            Assert.AreEqual(20, timeIntensities.Intensities[2]);
            var fwhmStart = (7 * 2 + 2 * 1) / 9.0;
            Assert.AreEqual(20, timeIntensities.Intensities[10]);
            Assert.AreEqual(11, timeIntensities.Intensities[11]);
            var fwhmEnd = (7 * 10 + 2 * 11) / 9.0;
            Assert.AreEqual(fwhmEnd-fwhmStart, chromPeak.Fwhm, .00001);
            Assert.AreEqual(false, chromPeak.IsFwhmDegenerate);
            var chromPeak2 = ChromPeak.IntegrateWithoutBackground(timeIntensities, 2, 10, 0, null);
            Assert.AreEqual(8, chromPeak2.Fwhm);
            Assert.AreEqual(true, chromPeak2.IsFwhmDegenerate);
        }

        [TestMethod]
        public void TestPeakIntegrator()
        {
            var times = Enumerable.Range(0, 12).Select(i => i / 7f).ToList();
            var timeIntensities = new TimeIntensities(times, Enumerable.Range(0, 12).Select(t => 36f - (t - 6) * (t - 6)), null, null);
            var peakIntegrator = new PeakIntegrator(timeIntensities);
            var flagValues = ChromPeak.FlagValues.time_normalized;
            var peakStartTime = times[1];
            var peakEndTime = times[times.Count - 2];
            var peakWithBackground = peakIntegrator.IntegratePeak(peakStartTime, peakEndTime, flagValues);
            Assert.AreNotEqual(0, peakWithBackground.BackgroundArea);

            // Set the TimeIntervals so the peakIntegrator will use "IntegratePeakWithoutBackground" 
            var peakIntegratorWithTimeIntervals = new PeakIntegrator(
                new PeakGroupIntegrator(FullScanAcquisitionMethod.None, TimeIntervals.EMPTY),
                ChromSource.unknown, null, timeIntensities, null);
            var peakWithoutBackground = peakIntegratorWithTimeIntervals.IntegratePeak(peakStartTime, peakEndTime, flagValues);
            Assert.AreEqual(0, peakWithoutBackground.BackgroundArea);
            var expectedArea = peakWithBackground.Area + peakWithBackground.BackgroundArea;
            Assert.AreEqual(expectedArea, peakWithoutBackground.Area, .01);
        }

        [TestMethod]
        public void TestFlatChromatogramIntegration()
        {
            const float constantIntensity = 8;
            var times = new [] {0, 1.5f, 2, 3};
            var intensities = Enumerable.Repeat(constantIntensity, times.Length);
            var timeIntensities = new TimeIntensities(times, intensities, null, null);
            var peakIntegrator = new PeakIntegrator(
                new PeakGroupIntegrator(FullScanAcquisitionMethod.None, TimeIntervals.EMPTY),
                ChromSource.unknown, null, timeIntensities, null);
            var flagValues = ChromPeak.FlagValues.time_normalized;
            for (float peakStartTime = 0; peakStartTime < 3; peakStartTime += .1f)
            {
                var zeroLengthPeak = peakIntegrator.IntegratePeak(peakStartTime, peakStartTime, flagValues);
                Assert.AreEqual(0, zeroLengthPeak.Area);
                for (float peakEndTime = peakStartTime + .1f; peakEndTime < 3; peakEndTime += .1f)
                {
                    var peak = peakIntegrator.IntegratePeak(peakStartTime, peakEndTime, flagValues);
                    AssertEx.AreEqual(60 * constantIntensity * (peakEndTime - peakStartTime), peak.Area, .001);
                }
            }            
        }

        [TestMethod]
        public void TestConstantSlopePeakIntegration()
        {
            const double slope = 8;
            var times = new[] { 0, 1.5f, 2, 3 };
            var intensities = times.Select(time => (float) (time * slope)).ToList();
            var timeIntensities = new TimeIntensities(times, intensities, null, null);
            var peakIntegrator = new PeakIntegrator(new PeakGroupIntegrator(FullScanAcquisitionMethod.None, TimeIntervals.EMPTY), 
                ChromSource.unknown, null, timeIntensities, null);
            var flagValues = ChromPeak.FlagValues.time_normalized;
            for (float peakStartTime = 0; peakStartTime < 3; peakStartTime += .1f)
            {
                var zeroLengthPeak = peakIntegrator.IntegratePeak(peakStartTime, peakStartTime, flagValues);
                Assert.AreEqual(0, zeroLengthPeak.Area);
                for (float peakEndTime = peakStartTime + .1f; peakEndTime < 3; peakEndTime += .1f)
                {
                    var peak = peakIntegrator.IntegratePeak(peakStartTime, peakEndTime, flagValues);
                    AssertEx.AreEqual(60 * slope * (peakStartTime + peakEndTime) / 2 * (peakEndTime - peakStartTime), peak.Area, .001);
                }
            }
        }

        [TestMethod]
        public unsafe void TestChromPeakStructSize()
        {
            // v20 added ObservedIonMobility and ObservedCcs as floats. Locking in
            // the struct size guards against accidental layout drift, since the
            // cache writes peaks via Marshal.SizeOf<ChromPeak>().
            Assert.AreEqual(52, ChromPeak.GetStructSize(CacheFormatVersion.Nineteen));
            Assert.AreEqual(60, ChromPeak.GetStructSize(CacheFormatVersion.Twenty));
            Assert.AreEqual(60, ChromPeak.GetStructSize(CacheFormatVersion.CURRENT));
            Assert.AreEqual(60, sizeof(ChromPeak));
            Assert.AreEqual(60, Marshal.SizeOf<ChromPeak>());
        }

        [TestMethod]
        public void TestChromPeakObservedCcsRoundTrip()
        {
            // ObservedCcs is set via WithObservedCcs (called by ApplyObservedCcs in
            // the cache builder while the source file is open). Empty peak with no
            // CCS should report null; round-trip through WithObservedCcs sets the
            // flag and the value, and clearing with null clears both.
            ChromPeak peak = ChromPeak.EMPTY;
            Assert.IsNull(peak.ObservedCcs);

            peak = peak.WithObservedCcs(345.67);
            Assert.IsNotNull(peak.ObservedCcs);
            Assert.AreEqual(345.67f, peak.ObservedCcs.Value, .001);

            peak = peak.WithObservedCcs(null);
            Assert.IsNull(peak.ObservedCcs);
        }

        [TestMethod]
        public void TestTimeIntensitiesInterpolatePreservesObservedIonMobilities()
        {
            // Regression: ChromData.Interpolate used to drop ObservedIonMobilities
            // by reconstructing a TimeIntensities from individual fields without it,
            // which silently zeroed observed IM through the entire cache build.
            // The fix is to interpolate directly off RawTimeIntensities, but this
            // also exercises that TimeIntensities.Interpolate itself preserves the
            // ObservedIonMobilities array (interpolating values along with intensities).
            var times = new[] { 1f, 2f, 3f };
            var intensities = new[] { 10f, 20f, 30f };
            var observedIms = new[] { 1.10f, 1.20f, 1.30f };
            var ti = new TimeIntensities(times, intensities, null, null, observedIms);
            Assert.IsNotNull(ti.ObservedIonMobilities);
            Assert.AreEqual(3, ti.ObservedIonMobilities.Count);

            // Interpolating onto the same time grid should preserve all 3 values.
            var same = ti.Interpolate(times, false);
            Assert.IsNotNull(same.ObservedIonMobilities);
            Assert.AreEqual(3, same.ObservedIonMobilities.Count);
            for (int i = 0; i < 3; i++)
                Assert.AreEqual(observedIms[i], same.ObservedIonMobilities[i], .0001);

            // Interpolating onto a finer grid also preserves the array (size grows).
            var finer = ti.Interpolate(new[] { 1f, 1.5f, 2f, 2.5f, 3f }, false);
            Assert.IsNotNull(finer.ObservedIonMobilities);
            Assert.AreEqual(5, finer.ObservedIonMobilities.Count);
        }

        [TestMethod]
        public void TestTimeIntensitiesTruncatePreservesObservedIonMobilities()
        {
            var times = new[] { 1f, 2f, 3f, 4f, 5f };
            var intensities = new[] { 10f, 20f, 30f, 40f, 50f };
            var observedIms = new[] { 1.1f, 1.2f, 1.3f, 1.4f, 1.5f };
            var ti = new TimeIntensities(times, intensities, null, null, observedIms);

            var truncated = ti.Truncate(2.0, 4.0);
            Assert.IsNotNull(truncated.ObservedIonMobilities);
            Assert.AreEqual(truncated.NumPoints, truncated.ObservedIonMobilities.Count);
            // Slice should be {1.2, 1.3, 1.4}
            Assert.AreEqual(1.2f, truncated.ObservedIonMobilities[0], .0001);
            Assert.AreEqual(1.4f, truncated.ObservedIonMobilities[truncated.NumPoints - 1], .0001);
        }

        [TestMethod]
        public void TestTimeIntensitiesInterpolateTimePreservesObservedIonMobilities()
        {
            var times = new[] { 1f, 2f };
            var intensities = new[] { 10f, 20f };
            var observedIms = new[] { 1.0f, 2.0f };
            var ti = new TimeIntensities(times, intensities, null, null, observedIms);

            // Insert a midpoint via InterpolateTime; the new point should get an
            // intensity-weighted observed IM (the single-point variant Skyline
            // already uses elsewhere in the constructor).
            var withMidpoint = ti.InterpolateTime(1.5f);
            Assert.AreEqual(3, withMidpoint.NumPoints);
            Assert.IsNotNull(withMidpoint.ObservedIonMobilities);
            Assert.AreEqual(3, withMidpoint.ObservedIonMobilities.Count);
            Assert.AreEqual(1.0f, withMidpoint.ObservedIonMobilities[0]);
            Assert.AreEqual(2.0f, withMidpoint.ObservedIonMobilities[2]);
            // Midpoint value falls between the bracketing values (the precise number
            // depends on the intensity-weighted formula in TimeIntensities, but it
            // must be in [1.0, 2.0]).
            float mid = withMidpoint.ObservedIonMobilities[1];
            Assert.IsTrue(mid >= 1.0f && mid <= 2.0f, $@"midpoint observed IM {mid} not bracketed");
        }

        [TestMethod]
        public void TestTimeIntensitiesInterpolateTime()
        {
            var timeIntensities = new TimeIntensities(new[] {1f, 2f}, new[] {15f, 25f}, new[] {5, -5f}, new[] {1, 2});

            var timeIntensities2 = timeIntensities.InterpolateTime(1.5f);
            Assert.AreEqual(3, timeIntensities2.NumPoints);
            Assert.AreEqual(1.5f, timeIntensities2.Times[1]);
            Assert.AreEqual(20f, timeIntensities2.Intensities[1]);
            Assert.AreEqual(-1.25f, timeIntensities2.MassErrors[1]);

            var timeIntensities3 = timeIntensities.InterpolateTime(1.25f);
            Assert.AreEqual(3, timeIntensities3.NumPoints);
            Assert.AreEqual(1.25f, timeIntensities3.Times[1]);
            Assert.AreEqual(17.5, timeIntensities3.Intensities[1]);
            Assert.AreEqual((float) (10.0/7), timeIntensities3.MassErrors[1]);
            Assert.AreEqual(1, timeIntensities3.ScanIds[1]);

            var timeIntensities4 = timeIntensities.InterpolateTime(1.75f);
            Assert.AreEqual(3, timeIntensities4.NumPoints);
            Assert.AreEqual(1.75f, timeIntensities4.Times[1]);
            Assert.AreEqual(22.5f, timeIntensities4.Intensities[1]);
            Assert.AreEqual((float) (-10.0 / 3), timeIntensities4.MassErrors[1]);
            Assert.AreEqual(2, timeIntensities4.ScanIds[1]);

            var timeIntensities5 = timeIntensities.InterpolateTime(1.00001f);
            Assert.AreEqual(1.00001f, timeIntensities5.Times[1]);
            Assert.AreEqual(15f, timeIntensities5.Intensities[1], .01);
            Assert.AreEqual(5f, timeIntensities5.MassErrors[1], .01);

            var timeIntensities6 = timeIntensities.InterpolateTime(1.99999f);
            Assert.AreEqual(1.99999f, timeIntensities6.Times[1]);
            Assert.AreEqual(25f, timeIntensities6.Intensities[1], .01);
            Assert.AreEqual(-5f, timeIntensities6.MassErrors[1], .01);

            var timeIntensities7 = timeIntensities.InterpolateTime(-1f);
            Assert.AreEqual(-1f, timeIntensities7.Times[0]);
            Assert.AreEqual(15f, timeIntensities7.Intensities[0]);
            Assert.AreEqual(5f, timeIntensities7.MassErrors[0]);
            Assert.AreEqual(1, timeIntensities7.ScanIds[0]);

            var timeIntensities8 = timeIntensities.InterpolateTime(4f);
            Assert.AreEqual(4f, timeIntensities8.Times[2]);
            Assert.AreEqual(25f, timeIntensities8.Intensities[2]);
            Assert.AreEqual(-5f, timeIntensities8.MassErrors[2]);
            Assert.AreEqual(2, timeIntensities8.ScanIds[2]);
        }
    }
}
