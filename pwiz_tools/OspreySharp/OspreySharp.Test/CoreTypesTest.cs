/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4) <noreply .at. anthropic.com>
 *
 * Based on osprey (https://github.com/MacCossLab/osprey)
 *   by Michael J. MacCoss, MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.Test
{
    /// <summary>
    /// Tests for core Osprey types, ported from osprey-core Rust tests.
    /// </summary>
    [TestClass]
    public class CoreTypesTest
    {
        private const double TOLERANCE = 1e-6;

        #region IonType Tests

        [TestMethod]
        public void TestIonTypeFromChar()
        {
            // Lowercase
            Assert.AreEqual(IonType.B, IonTypeExtensions.FromChar('b'));
            Assert.AreEqual(IonType.Y, IonTypeExtensions.FromChar('y'));
            Assert.AreEqual(IonType.Z, IonTypeExtensions.FromChar('z'));
            Assert.AreEqual(IonType.A, IonTypeExtensions.FromChar('a'));
            Assert.AreEqual(IonType.C, IonTypeExtensions.FromChar('c'));
            Assert.AreEqual(IonType.X, IonTypeExtensions.FromChar('x'));

            // Uppercase
            Assert.AreEqual(IonType.B, IonTypeExtensions.FromChar('B'));
            Assert.AreEqual(IonType.Y, IonTypeExtensions.FromChar('Y'));
            Assert.AreEqual(IonType.Z, IonTypeExtensions.FromChar('Z'));
            Assert.AreEqual(IonType.A, IonTypeExtensions.FromChar('A'));
            Assert.AreEqual(IonType.C, IonTypeExtensions.FromChar('C'));
            Assert.AreEqual(IonType.X, IonTypeExtensions.FromChar('X'));

            // Unknown characters
            Assert.AreEqual(IonType.Unknown, IonTypeExtensions.FromChar('?'));
            Assert.AreEqual(IonType.Unknown, IonTypeExtensions.FromChar('q'));
            Assert.AreEqual(IonType.Unknown, IonTypeExtensions.FromChar('1'));
        }

        #endregion

        #region NeutralLoss Tests

        [TestMethod]
        public void TestNeutralLossMass()
        {
            Assert.AreEqual(18.010565, NeutralLoss.H2O.Mass, TOLERANCE);
            Assert.AreEqual(17.026549, NeutralLoss.NH3.Mass, TOLERANCE);
            Assert.AreEqual(97.976896, NeutralLoss.H3PO4.Mass, TOLERANCE);
        }

        [TestMethod]
        public void TestNeutralLossParse()
        {
            // Named losses
            AssertNeutralLossEqual(NeutralLoss.H2O, NeutralLoss.Parse("H2O"));
            AssertNeutralLossEqual(NeutralLoss.H2O, NeutralLoss.Parse("WATER"));
            AssertNeutralLossEqual(NeutralLoss.NH3, NeutralLoss.Parse("NH3"));
            AssertNeutralLossEqual(NeutralLoss.NH3, NeutralLoss.Parse("AMMONIA"));
            AssertNeutralLossEqual(NeutralLoss.H3PO4, NeutralLoss.Parse("H3PO4"));
            AssertNeutralLossEqual(NeutralLoss.H3PO4, NeutralLoss.Parse("PHOSPHO"));

            // Null returns
            Assert.IsNull(NeutralLoss.Parse(""));
            Assert.IsNull(NeutralLoss.Parse("NOLOSS"));
            Assert.IsNull(NeutralLoss.Parse(null));

            // Custom numeric
            var custom = NeutralLoss.Parse("18.5");
            Assert.IsNotNull(custom);
            Assert.AreEqual(18.5, custom.Mass, TOLERANCE);
        }

        #endregion

        #region IsolationWindow Tests

        [TestMethod]
        public void TestIsolationWindowContains()
        {
            var window = IsolationWindow.Symmetric(500.0, 12.5);

            // Verify bounds
            Assert.AreEqual(487.5, window.LowerBound, TOLERANCE);
            Assert.AreEqual(512.5, window.UpperBound, TOLERANCE);
            Assert.AreEqual(25.0, window.Width, TOLERANCE);

            // Half-open interval: [lower, upper)
            Assert.IsTrue(window.Contains(487.5));     // lower bound inclusive
            Assert.IsTrue(window.Contains(500.0));     // center
            Assert.IsTrue(window.Contains(512.4999));  // just below upper bound
            Assert.IsFalse(window.Contains(512.5));    // upper bound exclusive
            Assert.IsFalse(window.Contains(400.0));    // well below
            Assert.IsFalse(window.Contains(600.0));    // well above
        }

        #endregion

        #region IsotopeEnvelope Tests

        [TestMethod]
        public void TestIsotopeMzCalculation()
        {
            int charge = 2;
            double precursorMz = 500.0;
            double expectedGap = IsotopeEnvelope.NEUTRON_MASS / charge;

            double[] isotopeMzs = IsotopeEnvelope.CalculateIsotopeMzs(precursorMz, charge);
            Assert.AreEqual(5, isotopeMzs.Length);

            // Verify gap is approximately NEUTRON_MASS / charge ~ 0.501434
            Assert.AreEqual(0.501434, expectedGap, 1e-4);

            // Verify the 5 m/z values: [M-1, M+0, M+1, M+2, M+3]
            Assert.AreEqual(precursorMz - expectedGap, isotopeMzs[0], 1e-4);
            Assert.AreEqual(precursorMz, isotopeMzs[1], 1e-4);
            Assert.AreEqual(precursorMz + expectedGap, isotopeMzs[2], 1e-4);
            Assert.AreEqual(precursorMz + 2 * expectedGap, isotopeMzs[3], 1e-4);
            Assert.AreEqual(precursorMz + 3 * expectedGap, isotopeMzs[4], 1e-4);
        }

        [TestMethod]
        public void TestIsotopeEnvelopeExtraction()
        {
            int charge = 2;
            double precursorMz = 500.0;
            double gap = IsotopeEnvelope.NEUTRON_MASS / charge;
            float[] expectedIntensities = { 10f, 100f, 80f, 40f, 15f };

            // Create MS1 with peaks at isotope positions
            var ms1 = CreateMs1WithIsotopePeaks(precursorMz, gap, expectedIntensities);

            var envelope = IsotopeEnvelope.Extract(ms1, precursorMz, charge, 20.0);

            // Verify intensities match [M-1, M+0, M+1, M+2, M+3]
            for (int i = 0; i < 5; i++)
            {
                Assert.AreEqual(expectedIntensities[i], envelope.Intensities[i], TOLERANCE);
            }

            // M+0 observed
            Assert.IsTrue(envelope.HasM0);
            Assert.IsNotNull(envelope.M0ObservedMz);
            Assert.AreEqual(precursorMz, envelope.M0ObservedMz.Value, 1e-4);
            Assert.AreEqual(100.0, envelope.M0Intensity, TOLERANCE);
        }

        [TestMethod]
        public void TestIsotopeEnvelopeMissingPeaks()
        {
            int charge = 2;
            double precursorMz = 500.0;
            double gap = IsotopeEnvelope.NEUTRON_MASS / charge;

            // Only M+0 and M+1 peaks present
            var ms1 = new MS1Spectrum
            {
                ScanNumber = 1,
                RetentionTime = 1.0,
                Mzs = new[] { precursorMz, precursorMz + gap },
                Intensities = new[] { 100f, 80f }
            };

            var envelope = IsotopeEnvelope.Extract(ms1, precursorMz, charge, 20.0);

            // M-1, M+2, M+3 should be zero (missing)
            Assert.AreEqual(0.0, envelope.Intensities[0], TOLERANCE); // M-1
            Assert.AreEqual(100.0, envelope.Intensities[1], TOLERANCE); // M+0
            Assert.AreEqual(80.0, envelope.Intensities[2], TOLERANCE);  // M+1
            Assert.AreEqual(0.0, envelope.Intensities[3], TOLERANCE); // M+2
            Assert.AreEqual(0.0, envelope.Intensities[4], TOLERANCE); // M+3

            Assert.IsTrue(envelope.HasM0);
        }

        #endregion

        #region MS1Spectrum Tests

        [TestMethod]
        public void TestMs1FindPeak()
        {
            var ms1 = new MS1Spectrum
            {
                ScanNumber = 1,
                RetentionTime = 1.0,
                Mzs = new[] { 499.995, 500.0, 500.005, 501.0 },
                Intensities = new[] { 50f, 100f, 75f, 25f }
            };

            // Find peak near 500.0 with 20 ppm tolerance
            var peak = ms1.FindPeakPpm(500.0, 20.0);
            Assert.IsNotNull(peak);
            Assert.AreEqual(500.0, peak.Value.Mz, TOLERANCE);
            Assert.AreEqual(100f, peak.Value.Intensity, TOLERANCE);

            // No peak near 600.0
            var noPeak = ms1.FindPeakPpm(600.0, 20.0);
            Assert.IsNull(noPeak);
        }

        #endregion

        #region BinConfig Tests

        [TestMethod]
        public void TestBinConfigUnitResolution()
        {
            var config = BinConfig.UnitResolution();

            // Bin width should be ~1.0005079
            Assert.AreEqual(1.0005079, config.BinWidth, TOLERANCE);

            // Round-trip: mz -> bin -> mz should be within one bin width
            double testMz = 500.0;
            int bin = config.MzToBin(testMz);
            double roundTripped = config.BinToMz(bin);
            Assert.IsTrue(Math.Abs(roundTripped - testMz) < config.BinWidth);
        }

        [TestMethod]
        public void TestBinConfigHram()
        {
            var config = BinConfig.HRAM();

            // HRAM bin width is 0.02
            Assert.AreEqual(0.02, config.BinWidth, TOLERANCE);

            // Round-trip
            double testMz = 500.0;
            int bin = config.MzToBin(testMz);
            double roundTripped = config.BinToMz(bin);
            Assert.IsTrue(Math.Abs(roundTripped - testMz) < config.BinWidth);
        }

        #endregion

        #region FdrEntry Tests

        [TestMethod]
        public void TestFdrEntryEffectiveQvalue()
        {
            var entry = new FdrEntry
            {
                RunPrecursorQvalue = 0.005,
                RunPeptideQvalue = 0.015
            };

            Assert.AreEqual(0.005, entry.EffectiveRunQvalue(FdrLevel.Precursor), TOLERANCE);
            Assert.AreEqual(0.015, entry.EffectiveRunQvalue(FdrLevel.Peptide), TOLERANCE);
            // Both = max(precursor, peptide) = 0.015
            Assert.AreEqual(0.015, entry.EffectiveRunQvalue(FdrLevel.Both), TOLERANCE);
        }

        #endregion

        #region LibraryEntry Tests

        [TestMethod]
        public void TestLibraryEntryCreation()
        {
            var entry = new LibraryEntry(1, "PEPTIDE", "PEPTIDE", 2, 400.215, 12.5);

            Assert.IsNotNull(entry);
            Assert.AreEqual(1u, entry.Id);
            Assert.AreEqual("PEPTIDE", entry.Sequence);
            Assert.AreEqual("PEPTIDE", entry.ModifiedSequence);
            Assert.AreEqual((byte)2, entry.Charge);
            Assert.AreEqual(400.215, entry.PrecursorMz, TOLERANCE);
            Assert.AreEqual(12.5, entry.RetentionTime, TOLERANCE);
            Assert.IsFalse(entry.IsDecoy);
            Assert.IsFalse(entry.RtCalibrated);
            Assert.AreEqual(0, entry.Fragments.Count);
            Assert.AreEqual(0, entry.Modifications.Count);
            Assert.AreEqual(0, entry.ProteinIds.Count);
            Assert.AreEqual(0, entry.GeneNames.Count);
        }

        [TestMethod]
        public void TestLibraryEntryWithFragments()
        {
            var entry = new LibraryEntry(2, "PEPTIDER", "PEP[+80]TIDER", 3, 350.5, 15.0);

            entry.Fragments.Add(new LibraryFragment
            {
                Mz = 175.119,
                RelativeIntensity = 1.0f,
                Annotation = new FragmentAnnotation
                {
                    IonType = IonType.Y,
                    Ordinal = 3,
                    Charge = 1
                }
            });
            entry.Fragments.Add(new LibraryFragment
            {
                Mz = 274.187,
                RelativeIntensity = 0.8f,
                Annotation = new FragmentAnnotation
                {
                    IonType = IonType.B,
                    Ordinal = 4,
                    Charge = 1
                }
            });

            entry.Modifications.Add(new Modification
            {
                Position = 2,
                UnimodId = 21,
                MassDelta = 79.966331
            });

            Assert.AreEqual(2, entry.Fragments.Count);
            Assert.AreEqual(1, entry.Modifications.Count);
            Assert.AreEqual(79.966331, entry.Modifications[0].MassDelta, TOLERANCE);
            Assert.AreEqual(IonType.Y, entry.Fragments[0].Annotation.IonType);
            Assert.AreEqual(IonType.B, entry.Fragments[1].Annotation.IonType);
        }

        #endregion

        #region CoelutionFeatureSet Tests

        [TestMethod]
        public void TestCoelutionFeatureSetDefaults()
        {
            var features = new CoelutionFeatureSet();

            Assert.AreEqual(0.0, features.CoelutionSum, TOLERANCE);
            Assert.AreEqual(0.0, features.CoelutionMin, TOLERANCE);
            Assert.AreEqual(0.0, features.CoelutionMax, TOLERANCE);
            Assert.AreEqual((byte)0, features.NCoelutingFragments);
            Assert.AreEqual((byte)0, features.NFragmentPairs);
            Assert.AreEqual(0.0, features.PeakApex, TOLERANCE);
            Assert.AreEqual(0.0, features.PeakArea, TOLERANCE);
            Assert.AreEqual(0.0, features.PeakWidth, TOLERANCE);
            Assert.AreEqual(0.0, features.Hyperscore, TOLERANCE);
            Assert.AreEqual(0.0, features.Xcorr, TOLERANCE);
            Assert.AreEqual(0.0, features.DotProduct, TOLERANCE);
            Assert.AreEqual(0.0, features.RtDeviation, TOLERANCE);

            // FragmentCorr should be length 6, all zeros
            Assert.IsNotNull(features.FragmentCorr);
            Assert.AreEqual(6, features.FragmentCorr.Length);
            for (int i = 0; i < 6; i++)
            {
                Assert.AreEqual(0.0, features.FragmentCorr[i], TOLERANCE);
            }
        }

        #endregion

        #region OspreyConfig Tests

        [TestMethod]
        public void TestDefaultConfig()
        {
            var config = new OspreyConfig();

            Assert.AreEqual(0.01, config.RunFdr, TOLERANCE);
            Assert.IsTrue(config.RtCalibration.Enabled);
            Assert.AreEqual(10.0, config.FragmentTolerance.Tolerance, TOLERANCE);
            Assert.AreEqual(ToleranceUnit.Ppm, config.FragmentTolerance.Unit);
            Assert.AreEqual(ResolutionMode.Auto, config.ResolutionMode);
            Assert.AreEqual(DecoyMethod.Reverse, config.DecoyMethod);
            Assert.IsTrue(config.PrefilterEnabled);
            Assert.AreEqual(0.01, config.ExperimentFdr, TOLERANCE);
            Assert.AreEqual(FdrMethod.Percolator, config.FdrMethod);
            Assert.AreEqual(FdrLevel.Both, config.FdrLevel);
            Assert.AreEqual(SharedPeptideMode.All, config.SharedPeptides);
        }

        [TestMethod]
        public void TestFragmentTolerancePpm()
        {
            var config = FragmentToleranceConfig.Hram(10.0);

            Assert.AreEqual(10.0, config.Tolerance, TOLERANCE);
            Assert.AreEqual(ToleranceUnit.Ppm, config.Unit);

            // 10 ppm of 500 = 0.005 Da
            Assert.AreEqual(0.005, config.ToleranceDa(500.0), TOLERANCE);

            // At exactly 10 ppm boundary
            Assert.IsTrue(config.WithinTolerance(500.0, 500.005));

            // Mass error: (500.005 - 500.0) / 500.0 * 1e6 = 10.0 ppm
            Assert.AreEqual(10.0, config.MassError(500.0, 500.005), 0.1);
        }

        [TestMethod]
        public void TestFragmentToleranceDa()
        {
            var config = FragmentToleranceConfig.UnitResolution(0.3);

            Assert.AreEqual(0.3, config.Tolerance, TOLERANCE);
            Assert.AreEqual(ToleranceUnit.Mz, config.Unit);

            // Da tolerance is constant across m/z
            Assert.AreEqual(0.3, config.ToleranceDa(500.0), TOLERANCE);
            Assert.AreEqual(0.3, config.ToleranceDa(1000.0), TOLERANCE);

            // Within tolerance
            Assert.IsTrue(config.WithinTolerance(500.0, 500.29));
            Assert.IsFalse(config.WithinTolerance(500.0, 500.31));
        }

        [TestMethod]
        public void TestSearchHashDeterministic()
        {
            var config = new OspreyConfig();
            string hash1 = config.SearchParameterHash();
            string hash2 = config.SearchParameterHash();

            Assert.AreEqual(hash1, hash2);
            Assert.AreEqual(64, hash1.Length); // SHA-256 hex is 64 chars
        }

        [TestMethod]
        public void TestSearchHashChangesWithTolerance()
        {
            var config = new OspreyConfig();
            string hash1 = config.SearchParameterHash();
            config.FragmentTolerance.Tolerance = 20.0;
            string hash2 = config.SearchParameterHash();

            Assert.AreNotEqual(hash1, hash2);
        }

        [TestMethod]
        public void TestLibrarySourceFromPath()
        {
            var tsv = LibrarySource.FromPath("library.tsv");
            Assert.AreEqual(LibraryFormat.DiannTsv, tsv.Format);

            var blib = LibrarySource.FromPath("library.blib");
            Assert.AreEqual(LibraryFormat.Blib, blib.Format);

            var elib = LibrarySource.FromPath("library.elib");
            Assert.AreEqual(LibraryFormat.Elib, elib.Format);
        }

        [TestMethod]
        public void TestRtCalibrationConfig()
        {
            var config = new RTCalibrationConfig();
            Assert.IsTrue(config.Enabled);
            Assert.AreEqual(0.3, config.LoessBandwidth, TOLERANCE);
            Assert.AreEqual(200, config.MinCalibrationPoints);
            Assert.AreEqual(3.0, config.RtToleranceFactor, TOLERANCE);
            Assert.AreEqual(2.0, config.FallbackRtTolerance, TOLERANCE);
            Assert.AreEqual(0.5, config.MinRtTolerance, TOLERANCE);
            Assert.AreEqual(100000, config.CalibrationSampleSize);
            Assert.AreEqual(2.0, config.CalibrationRetryFactor, TOLERANCE);
            Assert.AreEqual(3.0, config.MaxRtTolerance, TOLERANCE);

            var disabled = RTCalibrationConfig.CreateDisabled();
            Assert.IsFalse(disabled.Enabled);
        }

        #endregion

        #region Helpers

        private static MS1Spectrum CreateMs1WithIsotopePeaks(double precursorMz, double gap,
            float[] intensities)
        {
            // Build peaks at [M-1, M+0, M+1, M+2, M+3]
            var mzs = new double[5];
            for (int i = 0; i < 5; i++)
            {
                mzs[i] = precursorMz + (i - 1) * gap;
            }

            return new MS1Spectrum
            {
                ScanNumber = 1,
                RetentionTime = 1.0,
                Mzs = mzs,
                Intensities = intensities
            };
        }

        private static void AssertNeutralLossEqual(NeutralLoss expected, NeutralLoss actual)
        {
            Assert.IsNotNull(actual);
            Assert.AreEqual(expected.Mass, actual.Mass, TOLERANCE);
        }

        #endregion
    }
}
