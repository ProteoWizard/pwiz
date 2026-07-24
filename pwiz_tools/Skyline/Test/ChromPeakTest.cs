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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class ChromPeakTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestMobilogramPeakMetrics()
        {
            // Well-formed peak over a baseline of 5, sampled finely enough that >= 3 bins clear
            // half-max.  IM: 0..10   int: 5 10 25 60 90 100 90 60 25 10 5
            var peak = new[]
            {
                new KeyValuePair<double, double>(0, 5),
                new KeyValuePair<double, double>(1, 10),
                new KeyValuePair<double, double>(2, 25),
                new KeyValuePair<double, double>(3, 60),
                new KeyValuePair<double, double>(4, 90),
                new KeyValuePair<double, double>(5, 100),
                new KeyValuePair<double, double>(6, 90),
                new KeyValuePair<double, double>(7, 60),
                new KeyValuePair<double, double>(8, 25),
                new KeyValuePair<double, double>(9, 10),
                new KeyValuePair<double, double>(10, 5),
            };
            var m = MobilogramPeakMetrics.Compute(peak);
            Assert.IsTrue(m.HasValue);
            Assert.AreEqual(95, m.Value.Height, 1e-9);  // apex 100 - baseline 5
            Assert.AreEqual(425, m.Value.Area, 1e-9);   // baseline-subtracted trapezoids
            Assert.IsTrue(m.Value.FullWidthHalfMax.HasValue);
            // Half-max level = 5 + 95/2 = 52.5; crossings on the 25->60 rise and 60->25 fall.
            var left = 2 + (52.5 - 25) / (60 - 25.0);
            var right = 7 + (52.5 - 60) / (25 - 60.0);
            Assert.AreEqual(right - left, m.Value.FullWidthHalfMax.Value, 1e-9);

            // Ill-formed: a single spike over a noise floor - only one bin clears half-max, so FWHM
            // is withheld, while baseline-subtracted area/height still report.
            var spike = new[]
            {
                new KeyValuePair<double, double>(0, 2),
                new KeyValuePair<double, double>(1, 3),
                new KeyValuePair<double, double>(2, 100),
                new KeyValuePair<double, double>(3, 3),
                new KeyValuePair<double, double>(4, 2),
            };
            var ms = MobilogramPeakMetrics.Compute(spike);
            Assert.IsTrue(ms.HasValue);
            Assert.AreEqual(98, ms.Value.Height, 1e-9); // apex 100 - baseline 2
            Assert.AreEqual(100, ms.Value.Area, 1e-9);  // trapezoids of (v - 2)
            Assert.IsFalse(ms.Value.FullWidthHalfMax.HasValue);

            // Degenerate inputs return null.
            Assert.IsFalse(MobilogramPeakMetrics.Compute(null).HasValue);
            Assert.IsFalse(MobilogramPeakMetrics.Compute(new KeyValuePair<double, double>[0]).HasValue);
            Assert.IsFalse(MobilogramPeakMetrics.Compute(
                new[] { new KeyValuePair<double, double>(1, 100) }).HasValue);
        }

        [TestMethod]
        public void TestObservedIonMobilityCcsErrorPrecision()
        {
            // Regression: the observed-vs-target IM/CCS percent error was formatted with the
            // ppm-grade Formats.MASS_ERROR ("0.#"), which rounds sub-0.05% differences to "0" -
            // so a real gap (observed IM 33.79 vs target 33.805) read as "0% error" even though
            // the observed and target values plainly differ. It now uses Formats.PercentError
            // ("0.##") so small differences stay visible.
            var culture = CultureInfo.CurrentCulture;
            string zeroPct = (0.0).ToString(Formats.PercentError, culture);

            const double observedIm = 33.79, targetIm = 33.805;
            double imPct = 100.0 * (observedIm - targetIm) / targetIm; // ~ -0.044%
            string imPctText = imPct.ToString(Formats.PercentError, culture);
            Assert.AreNotEqual(zeroPct, imPctText); // the format itself must not round this away

            // The shared formatter (observed-line tooltip + properties pane) must surface it.
            string imFormatted = ObservedValueFormatter.FormatWithPercentError(observedIm, targetIm, Formats.IonMobility);
            StringAssert.Contains(imFormatted, imPctText);

            // CCS gap (260.88 vs 261) likewise renders a nonzero error.
            const double observedCcs = 260.88, targetCcs = 261;
            double ccsPct = 100.0 * (observedCcs - targetCcs) / targetCcs;
            string ccsFormatted = ObservedValueFormatter.FormatWithPercentError(observedCcs, targetCcs, Formats.CCS);
            StringAssert.Contains(ccsFormatted, ccsPct.ToString(Formats.PercentError, culture));

            // A zero target (IM-only data, no CCS ground truth) renders the value alone, no error.
            string noTarget = ObservedValueFormatter.FormatWithPercentError(observedIm, 0, Formats.IonMobility);
            Assert.AreEqual(observedIm.ToString(Formats.IonMobility, culture), noTarget);
        }

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
        public void TestChromPeakV19CacheCompat()
        {
            // A v19 cache wrote ChromPeak as 52 bytes - the observed IM/CCS fields
            // and the observed_ion_mobility_known / observed_ccs_known flag bits
            // did not exist. When the v20 reader encounters those records, the
            // trailing 8 bytes (where the new floats now live) are zero-padded by
            // StructSerializer and the unset flag bits make the getters return
            // null. This locks in that contract by round-tripping through a
            // 52-byte StructSerializer, which truncates on write and zero-pads on
            // read - the same shape as a real v19-on-disk record.
            Assert.AreEqual(52, ChromPeak.GetStructSize(CacheFormatVersion.Nineteen));

            // Build a peak with mass error set but no observed IM/CCS - the
            // constructor explicitly clears observed_ion_mobility_known /
            // observed_ccs_known, matching what a v19-written record looks like.
            var peak = new ChromPeak(1f, 0.5f, 1.5f, 100f, 5f, 50f, 0.5f,
                ChromPeak.FlagValues.time_normalized, 0.0, 1, null);
            Assert.IsNull(peak.ObservedIonMobility);
            Assert.IsNull(peak.ObservedCcs);

            var v19Serializer = ChromPeak.StructSerializer(52);
            using (var stream = new MemoryStream())
            {
                v19Serializer.WriteItems(stream, new[] { peak });
                Assert.AreEqual(52, stream.Length);
                stream.Position = 0;
                var roundTripped = v19Serializer.ReadArray(stream, 1)[0];
                Assert.IsNull(roundTripped.ObservedIonMobility);
                Assert.IsNull(roundTripped.ObservedCcs);
                // Sanity: data that did fit in 52 bytes round-trips intact.
                Assert.AreEqual(peak.RetentionTime, roundTripped.RetentionTime);
                Assert.AreEqual(peak.Area, roundTripped.Area);
                Assert.AreEqual(peak.MassError, roundTripped.MassError);
            }
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

        [TestMethod]
        public void TestObservedIonMobilityScaleHelpers()
        {
            // Strict variant: returns the scale for tracked units, throws otherwise.
            Assert.AreEqual(10000, RawTimeIntensities.GetObservedIonMobilityScale(eIonMobilityUnits.inverse_K0_Vsec_per_cm2));
            Assert.AreEqual(100, RawTimeIntensities.GetObservedIonMobilityScale(eIonMobilityUnits.drift_time_msec));
            AssertEx.ThrowsException<ArgumentOutOfRangeException>(() =>
                RawTimeIntensities.GetObservedIonMobilityScale(eIonMobilityUnits.compensation_V));
            AssertEx.ThrowsException<ArgumentOutOfRangeException>(() =>
                RawTimeIntensities.GetObservedIonMobilityScale(eIonMobilityUnits.waters_sonar));
            AssertEx.ThrowsException<ArgumentOutOfRangeException>(() =>
                RawTimeIntensities.GetObservedIonMobilityScale(eIonMobilityUnits.none));
            AssertEx.ThrowsException<ArgumentOutOfRangeException>(() =>
                RawTimeIntensities.GetObservedIonMobilityScale(eIonMobilityUnits.unknown));

            // Gate-friendly variant: returns scale for tracked units, zero for others.
            Assert.AreEqual(10000, RawTimeIntensities.GetObservedIonMobilityScaleOrZero(eIonMobilityUnits.inverse_K0_Vsec_per_cm2));
            Assert.AreEqual(100, RawTimeIntensities.GetObservedIonMobilityScaleOrZero(eIonMobilityUnits.drift_time_msec));
            Assert.AreEqual(0, RawTimeIntensities.GetObservedIonMobilityScaleOrZero(eIonMobilityUnits.compensation_V));
            Assert.AreEqual(0, RawTimeIntensities.GetObservedIonMobilityScaleOrZero(eIonMobilityUnits.waters_sonar));
            Assert.AreEqual(0, RawTimeIntensities.GetObservedIonMobilityScaleOrZero(eIonMobilityUnits.none));
            Assert.AreEqual(0, RawTimeIntensities.GetObservedIonMobilityScaleOrZero(eIonMobilityUnits.unknown));

            // Tracked-unit predicate gates extraction; it must agree with the scale (true iff
            // there is a non-zero scale). FAIMS and Waters SONAR are not ion mobility, so false.
            Assert.IsTrue(RawTimeIntensities.IsTrackedObservedIonMobilityUnit(eIonMobilityUnits.inverse_K0_Vsec_per_cm2));
            Assert.IsTrue(RawTimeIntensities.IsTrackedObservedIonMobilityUnit(eIonMobilityUnits.drift_time_msec));
            Assert.IsFalse(RawTimeIntensities.IsTrackedObservedIonMobilityUnit(eIonMobilityUnits.compensation_V));
            Assert.IsFalse(RawTimeIntensities.IsTrackedObservedIonMobilityUnit(eIonMobilityUnits.waters_sonar));
            Assert.IsFalse(RawTimeIntensities.IsTrackedObservedIonMobilityUnit(eIonMobilityUnits.none));
            Assert.IsFalse(RawTimeIntensities.IsTrackedObservedIonMobilityUnit(eIonMobilityUnits.unknown));
        }

        [TestMethod]
        public void TestRawTimeIntensitiesObservedIonMobilityRoundTrip()
        {
            // 1/K0 (scale 10000): instrument resolution is 0.001-0.01, so 0.0001-step
            // encoding is well sub-resolution. Test values span the proteomics range.
            AssertObservedIonMobilityRoundTrip(eIonMobilityUnits.inverse_K0_Vsec_per_cm2,
                new[] { 0.7234f, 1.1567f, 1.8901f, 2.3456f });

            // Drift time ms (scale 100): instrument resolution is 0.01-0.1 ms, so
            // 0.01-step encoding is well sub-resolution.
            AssertObservedIonMobilityRoundTrip(eIonMobilityUnits.drift_time_msec,
                new[] { 4.23f, 17.45f, 58.91f, 142.07f });
        }

        [TestMethod]
        public void TestRawTimeIntensitiesNoObservedIonMobility()
        {
            // A RawTimeIntensities with no observed IM data should round-trip cleanly
            // with scale=0 and no ObservedIonMobilities array emitted.
            var ti = new TimeIntensities(new[] { 0f, 1f }, new[] { 10f, 20f }, null, null);
            var raw = new RawTimeIntensities(new[] { ti }, null, 0);

            var encoded = raw.ToChromatogramGroupData();
            Assert.AreEqual(0, encoded.ObservedIonMobilityScale);
            Assert.AreEqual(0, encoded.Chromatograms[0].ObservedIonMobilitiesScaled.Count);

            var decoded = RawTimeIntensities.FromChromatogramGroupData(encoded);
            Assert.AreEqual(0, decoded.ObservedIonMobilityScale);
            Assert.IsNull(decoded.TransitionTimeIntensities[0].ObservedIonMobilities);
        }

        [TestMethod]
        public void TestObservedIonMobilityZeroScaleDecodesToNull()
        {
            // Defensive guard: a cache that carries an encoded observed-IM array but a zero
            // group scale (e.g. a malformed write where the scale was sourced from a missing
            // CCS converter rather than the data reader's IM units) must decode to "no observed
            // IM", not divide by a zero scale into NaN/Infinity.
            var times = new[] { 0f, 1f, 2f };
            var intensities = new[] { 100f, 100f, 100f };
            var observedIms = new[] { 4.23f, 4.50f, 4.80f };
            var ti = new TimeIntensities(times, intensities, null, null, observedIms);
            var scale = RawTimeIntensities.GetObservedIonMobilityScale(eIonMobilityUnits.drift_time_msec);
            var raw = new RawTimeIntensities(new[] { ti }, null, scale);

            var encoded = raw.ToChromatogramGroupData();
            Assert.AreNotEqual(0, encoded.Chromatograms[0].ObservedIonMobilitiesScaled.Count);
            // Corrupt the group scale to zero, leaving the encoded scaled array in place.
            encoded.ObservedIonMobilityScale = 0;

            var decoded = RawTimeIntensities.FromChromatogramGroupData(encoded);
            Assert.IsNull(decoded.TransitionTimeIntensities[0].ObservedIonMobilities,
                @"Zero observed-IM scale must decode to null, not NaN/Infinity");
        }

        [TestMethod]
        public void TestApexObservedIonMobilityOfValid()
        {
            // Observed IM for a peak is read at the apex - but at the highest-intensity scan
            // that carries a VALID observed IM, so a gap (0/NaN) at the literal max-intensity
            // scan can't suppress an otherwise-measurable value.
            var intensities = new[] { 10f, 30f, 100f, 90f, 5f }; // apex at index 2, runner-up index 3

            // Apex scan (index 2) has a valid IM -> read it directly.
            var imAllValid = new[] { 4.1f, 4.2f, 4.3f, 4.4f, 4.5f };
            Assert.AreEqual(4.3f, ChromPeak.ApexObservedIonMobility(intensities, imAllValid, 0, 4));

            // Apex scan is a gap (0): fall back to the next-highest-intensity valid scan
            // (index 3), not to null and not to a weaker valid scan.
            var imGapAtMax = new[] { 4.1f, 4.2f, 0f, 4.4f, 4.5f };
            Assert.AreEqual(4.4f, ChromPeak.ApexObservedIonMobility(intensities, imGapAtMax, 0, 4));

            // Apex scan is NaN: same fallback to the strongest valid scan.
            var imNanAtMax = new[] { 4.1f, 4.2f, float.NaN, 4.4f, 4.5f };
            Assert.AreEqual(4.4f, ChromPeak.ApexObservedIonMobility(intensities, imNanAtMax, 0, 4));

            // No valid IM anywhere in the window -> null (don't fabricate a value).
            var imNone = new[] { 0f, 0f, float.NaN, 0f, 0f };
            Assert.IsNull(ChromPeak.ApexObservedIonMobility(intensities, imNone, 0, 4));

            // Windowing: a stronger valid scan outside [startIndex, endIndex] is ignored.
            // Restricted to indices 0..1, the apex-of-valid is index 1 (30), not index 2 (100).
            Assert.AreEqual(4.2f, ChromPeak.ApexObservedIonMobility(intensities, imAllValid, 0, 1));
        }

        [TestMethod]
        public void TestInterpolatedTimeIntensitiesMixedGroupStreamRoundTrip()
        {
            // A chromatogram group can carry the has_mass_errors / has_observed_ion_mobilities flag
            // at the group level while some transitions lack the array (e.g. after merging a cache
            // that has the data with one that doesn't). WriteToStream skips those transitions, so
            // ReadFromStream must honor the per-transition MissingMassErrors / MissingObservedIonMobility
            // flags - otherwise it over-reads and corrupts every transition after the gap.
            var times = new[] { 0f, 1f, 2f, 3f };
            int numPoints = times.Length;
            // Transitions 0 and 2 carry mass errors and observed IM; transition 1 carries neither.
            var ti0 = new TimeIntensities(times, new[] { 1f, 2f, 3f, 4f },
                new[] { 0.1f, 0.2f, 0.3f, 0.4f }, null, new[] { 1.1f, 1.2f, 1.3f, 1.4f });
            var ti1 = new TimeIntensities(times, new[] { 5f, 6f, 7f, 8f }, null, null);
            var ti2 = new TimeIntensities(times, new[] { 9f, 10f, 11f, 12f },
                new[] { 0.5f, 0.6f, 0.7f, 0.8f }, null, new[] { 2.1f, 2.2f, 2.3f, 2.4f });
            var group = new InterpolatedTimeIntensities(new[] { ti0, ti1, ti2 },
                new[] { ChromSource.fragment, ChromSource.fragment, ChromSource.fragment });

            var stream = new MemoryStream();
            group.WriteToStream(stream);
            stream.Position = 0;

            var header = new ChromGroupHeaderInfo(new SignedMz(500.0), 3, 0, 0, null, 0, 0, numPoints,
                ChromGroupHeaderInfo.FlagValues.has_mass_errors | ChromGroupHeaderInfo.FlagValues.has_observed_ion_mobilities,
                null, null, null, eIonMobilityUnits.none);
            var t1Missing = new ChromTransition(0, 0, 0, 0, ChromSource.fragment, 0)
            {
                MissingMassErrors = true,
                MissingObservedIonMobility = true
            };
            var chromTransitions = new[]
            {
                new ChromTransition(0, 0, 0, 0, ChromSource.fragment, 0),
                t1Missing,
                new ChromTransition(0, 0, 0, 0, ChromSource.fragment, 0)
            };

            var result = InterpolatedTimeIntensities.ReadFromStream(stream, header, chromTransitions)
                .TransitionTimeIntensities;
            Assert.AreEqual(3, result.Count);
            // Transition 1's missing arrays come back null...
            Assert.IsNull(result[1].MassErrors);
            Assert.IsNull(result[1].ObservedIonMobilities);
            // ...and transitions 0 and 2 round-trip intact (2 would be corrupt if 1 had consumed bytes).
            AssertFloatsEqual(ti0.MassErrors, result[0].MassErrors);
            AssertFloatsEqual(ti0.ObservedIonMobilities, result[0].ObservedIonMobilities);
            AssertFloatsEqual(ti2.MassErrors, result[2].MassErrors);
            AssertFloatsEqual(ti2.ObservedIonMobilities, result[2].ObservedIonMobilities);
        }

        private static void AssertFloatsEqual(IReadOnlyList<float> expected, IReadOnlyList<float> actual)
        {
            Assert.IsNotNull(actual);
            Assert.AreEqual(expected.Count, actual.Count);
            for (int i = 0; i < expected.Count; i++)
                Assert.AreEqual(expected[i], actual[i], 1e-4f);
        }

        private static void AssertObservedIonMobilityRoundTrip(eIonMobilityUnits units, float[] observedIms)
        {
            var times = Enumerable.Range(0, observedIms.Length).Select(i => (float)i).ToArray();
            var intensities = observedIms.Select(_ => 100f).ToArray();
            var ti = new TimeIntensities(times, intensities, null, null, observedIms);
            var scale = RawTimeIntensities.GetObservedIonMobilityScale(units);
            var raw = new RawTimeIntensities(new[] { ti }, null, scale);

            var encoded = raw.ToChromatogramGroupData();
            Assert.AreEqual(scale, encoded.ObservedIonMobilityScale);
            Assert.AreEqual(observedIms.Length, encoded.Chromatograms[0].ObservedIonMobilitiesScaled.Count);

            var decoded = RawTimeIntensities.FromChromatogramGroupData(encoded);
            Assert.AreEqual(scale, decoded.ObservedIonMobilityScale);
            var decodedIms = decoded.TransitionTimeIntensities[0].ObservedIonMobilities;
            Assert.IsNotNull(decodedIms);
            Assert.AreEqual(observedIms.Length, decodedIms.Count);

            // Maximum quantization error from rounding is 0.5/scale.
            double tolerance = 0.5 / scale;
            for (int i = 0; i < observedIms.Length; i++)
            {
                Assert.AreEqual(observedIms[i], decodedIms[i], tolerance,
                    $@"IM at index {i} ({observedIms[i]}) decoded to {decodedIms[i]}, tolerance {tolerance}");
            }
        }
    }
}
