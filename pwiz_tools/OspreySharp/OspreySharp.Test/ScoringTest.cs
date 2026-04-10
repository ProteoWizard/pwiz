using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.OspreySharp.Core;
using pwiz.OspreySharp.Scoring;

namespace pwiz.OspreySharp.Test
{
    /// <summary>
    /// Tests for OspreySharp.Scoring, ported from osprey-scoring Rust tests.
    /// </summary>
    [TestClass]
    public class ScoringTest
    {
        private const double TOLERANCE = 1e-6;

        #region DecoyGenerator Tests

        [TestMethod]
        public void TestDecoyReverseTrypsin()
        {
            var generator = new DecoyGenerator(Enzyme.Trypsin);
            var target = new LibraryEntry(1, "PEPTIDEK", "PEPTIDEK", 2, 500.0, 10.0);
            var decoy = generator.Generate(target);

            Assert.IsTrue(decoy.IsDecoy);
            // PEPTIDE reversed = EDITPEP, K stays at C-terminus
            Assert.AreEqual("EDITPEPK", decoy.Sequence);
            Assert.IsTrue(decoy.ModifiedSequence.StartsWith("DECOY_"));
            Assert.AreEqual(1u | 0x80000000u, decoy.Id);
        }

        [TestMethod]
        public void TestDecoyReverseLysN()
        {
            var generator = new DecoyGenerator(Enzyme.LysN);
            var target = new LibraryEntry(1, "KPEPTIDE", "KPEPTIDE", 2, 500.0, 10.0);
            var decoy = generator.Generate(target);

            Assert.IsTrue(decoy.IsDecoy);
            // K stays at N-terminus, PEPTIDE reversed = EDITPEP
            Assert.AreEqual("KEDITPEP", decoy.Sequence);
        }

        [TestMethod]
        public void TestDecoyWithModification()
        {
            var generator = new DecoyGenerator(Enzyme.Trypsin);
            var target = new LibraryEntry(1, "PEPTIDEK", "PEPTIDEK", 2, 500.0, 10.0);
            target.Modifications.Add(new Modification
            {
                Position = 3, // T in PEPTIDEK
                UnimodId = 35,
                MassDelta = 15.994915,
                Name = "Oxidation"
            });

            var decoy = generator.Generate(target);

            // Reversed: EDITPEPK
            // Position mapping: [6, 5, 4, 3, 2, 1, 0, 7]
            // T at original pos 3 -> new pos 3 (symmetric in this case)
            Assert.AreEqual(1, decoy.Modifications.Count);
            Assert.AreEqual(3, decoy.Modifications[0].Position);
        }

        [TestMethod]
        public void TestDecoyPreservesPrecursorMz()
        {
            var generator = new DecoyGenerator();
            var target = new LibraryEntry(1, "PEPTIDEK", "PEPTIDEK", 2, 500.123, 10.0);

            var decoy = generator.Generate(target);

            // Same amino acids -> same precursor mass
            Assert.AreEqual(500.123, decoy.PrecursorMz, TOLERANCE);
        }

        [TestMethod]
        public void TestDecoyProteinIdPrefix()
        {
            var generator = new DecoyGenerator();
            var target = new LibraryEntry(1, "PEPTIDEK", "PEPTIDEK", 2, 500.0, 10.0);
            target.ProteinIds = new List<string> { "P12345", "Q67890" };

            var decoy = generator.Generate(target);

            Assert.AreEqual(2, decoy.ProteinIds.Count);
            Assert.IsTrue(decoy.ProteinIds[0].StartsWith("DECOY_"));
            Assert.IsTrue(decoy.ProteinIds[1].StartsWith("DECOY_"));
            Assert.AreEqual("DECOY_P12345", decoy.ProteinIds[0]);
            Assert.AreEqual("DECOY_Q67890", decoy.ProteinIds[1]);
        }

        [TestMethod]
        public void TestDecoyCollisionDetection()
        {
            // If reversed == original (palindromic), should use cycle fallback
            var generator = new DecoyGenerator(Enzyme.Trypsin);

            // "ABAAR" reversed internal: ABAA + R = ABAAR (same as original for this case)
            // Actually let's use a real palindrome: ABBAR -> reverse internal = ABBAR
            // A simpler case: "AK" is too short.
            // Let's test: ABAK reversed -> BAAK (different, so no collision)
            // For collision, we need palindromic internal: e.g., "ABAR" -> reverse "BAA" + R = "BAAR" (different)
            // Better: test via CycleSequence directly
            var target = new LibraryEntry(1, "PK", "PK", 2, 200.0, 5.0);
            var decoy = generator.Generate(target);

            // Too short to reverse meaningfully, stays same, so cycle fallback
            Assert.IsTrue(decoy.IsDecoy);
            Assert.AreEqual("PK", decoy.Sequence); // Both reverse and cycle give PK for 2-char
        }

        [TestMethod]
        public void TestCycleSequence()
        {
            var generator = new DecoyGenerator(Enzyme.Trypsin);
            int[] mapping;

            // PEPTIDEK with cycle=1: shift internal [PEPTIDE] by 1 -> EPTIDEP, keep K
            string cycled = generator.CycleSequence("PEPTIDEK", 1, out mapping);
            Assert.AreEqual("EPTIDEPK", cycled);
        }

        [TestMethod]
        public void TestEnzymeDetection()
        {
            // C-terminal K/R -> Trypsin
            Assert.AreEqual(Enzyme.Trypsin, DecoyGenerator.DetectEnzyme("PEPTIDEK"));
            Assert.AreEqual(Enzyme.Trypsin, DecoyGenerator.DetectEnzyme("PEPTIDER"));

            // N-terminal K -> LysN
            Assert.AreEqual(Enzyme.LysN, DecoyGenerator.DetectEnzyme("KPEPTIDE"));

            // N-terminal D -> AspN
            Assert.AreEqual(Enzyme.AspN, DecoyGenerator.DetectEnzyme("DPEPTIDE"));

            // No recognizable enzyme
            Assert.AreEqual(Enzyme.Unspecific, DecoyGenerator.DetectEnzyme("PEPTIDE"));

            // Trypsin preserves C-terminus
            Assert.IsTrue(Enzyme.Trypsin.PreservesCTerminus());
            Assert.IsTrue(Enzyme.LysC.PreservesCTerminus());
            Assert.IsFalse(Enzyme.LysN.PreservesCTerminus());
            Assert.IsFalse(Enzyme.AspN.PreservesCTerminus());
        }

        #endregion

        #region PearsonCorrelation Tests

        [TestMethod]
        public void TestPearsonIdenticalVectors()
        {
            double[] x = { 1.0, 2.0, 3.0, 4.0, 5.0 };
            double[] y = { 1.0, 2.0, 3.0, 4.0, 5.0 };
            double r = PearsonCorrelation.Pearson(x, y);
            Assert.AreEqual(1.0, r, 1e-10);
        }

        [TestMethod]
        public void TestPearsonPerfectNegative()
        {
            double[] x = { 1.0, 2.0, 3.0, 4.0, 5.0 };
            double[] y = { 5.0, 4.0, 3.0, 2.0, 1.0 };
            double r = PearsonCorrelation.Pearson(x, y);
            Assert.AreEqual(-1.0, r, 1e-10);
        }

        [TestMethod]
        public void TestPearsonUncorrelated()
        {
            // x = [1,0,-1,0], y = [0,1,0,-1] -> orthogonal
            double[] x = { 1.0, 0.0, -1.0, 0.0 };
            double[] y = { 0.0, 1.0, 0.0, -1.0 };
            double r = PearsonCorrelation.Pearson(x, y);
            Assert.AreEqual(0.0, r, 1e-10);
        }

        [TestMethod]
        public void TestPearsonConstantInput()
        {
            // Constant input has zero variance -> returns 0
            double[] x = { 5.0, 5.0, 5.0, 5.0 };
            double[] y = { 1.0, 2.0, 3.0, 4.0 };
            double r = PearsonCorrelation.Pearson(x, y);
            Assert.AreEqual(0.0, r, 1e-10);
        }

        [TestMethod]
        public void TestPearsonLinearTransform()
        {
            // Scaling and offset preserve correlation
            double[] x = { 1.0, 2.0, 3.0, 4.0, 5.0 };
            double[] y = new double[5];
            for (int i = 0; i < 5; i++)
                y[i] = 3.0 * x[i] + 7.0; // y = 3x + 7

            double r = PearsonCorrelation.Pearson(x, y);
            Assert.AreEqual(1.0, r, 1e-10);
        }

        #endregion

        #region SpectralScorer Tests

        [TestMethod]
        public void TestXcorrPerfectMatch()
        {
            var scorer = new SpectralScorer();
            int nBins = scorer.BinConfig.NBins;

            // Create identical observed and library bins at several positions
            double[] observed = new double[nBins];
            double[] library = new double[nBins];

            // Place peaks at bins 300, 400, 500
            int[] peakBins = { 300, 400, 500 };
            foreach (int b in peakBins)
            {
                observed[b] = 100.0;
                library[b] = 1.0; // unit intensity for library (Comet-style)
            }

            double score = scorer.XCorr(observed, library);

            // Perfect match of identical spectra should give a high positive score
            Assert.IsTrue(score > 0.0,
                string.Format("XCorr should be positive for matching spectra, got {0}", score));
        }

        [TestMethod]
        public void TestXcorrNoMatch()
        {
            var scorer = new SpectralScorer();
            int nBins = scorer.BinConfig.NBins;

            // Observed peaks at bins 100-102, library at 900-902 (disjoint)
            double[] observed = new double[nBins];
            double[] library = new double[nBins];

            observed[100] = 100.0;
            observed[101] = 50.0;
            observed[102] = 75.0;

            library[900] = 1.0;
            library[901] = 1.0;
            library[902] = 1.0;

            double score = scorer.XCorr(observed, library);

            // Disjoint spectra should give a score near zero or slightly negative
            Assert.IsTrue(score <= 0.01,
                string.Format("XCorr should be near zero for disjoint spectra, got {0}", score));
        }

        [TestMethod]
        public void TestLibCosineScorer()
        {
            var tolerance = FragmentToleranceConfig.Hram(10.0);
            var scorer = new SpectralScorer();

            var entry = new LibraryEntry(1, "PEPTIDE", "PEPTIDE", 2, 500.0, 10.0);
            entry.Fragments = new List<LibraryFragment>
            {
                new LibraryFragment { Mz = 300.0, RelativeIntensity = 100.0f, Annotation = new FragmentAnnotation() },
                new LibraryFragment { Mz = 400.0, RelativeIntensity = 50.0f, Annotation = new FragmentAnnotation() },
                new LibraryFragment { Mz = 500.0, RelativeIntensity = 75.0f, Annotation = new FragmentAnnotation() }
            };

            var spectrum = new Spectrum
            {
                ScanNumber = 1,
                RetentionTime = 10.0,
                PrecursorMz = 500.0,
                IsolationWindow = IsolationWindow.Symmetric(500.0, 12.5),
                Mzs = new double[] { 300.0, 400.0, 500.0 },
                Intensities = new float[] { 100.0f, 50.0f, 75.0f }
            };

            double score = scorer.LibCosine(spectrum, entry, tolerance);

            // Perfect match should give cosine close to 1.0
            Assert.IsTrue(score > 0.99,
                string.Format("LibCosine should be ~1.0 for perfect match, got {0}", score));
        }

        #endregion

        #region CalibrationScorer Tests

        [TestMethod]
        public void TestCompeteCalibrationPairs()
        {
            // Setup: 3 pairs of target/decoy
            // Pair 1 (baseId=1): target wins
            // Pair 2 (baseId=2): decoy wins
            // Pair 3 (baseId=3): tie -> decoy wins
            double[] scores = { 0.9, 0.5, 0.3, 0.7, 0.6, 0.6 };
            uint[] entryIds = {
                1,              // target for pair 1
                1 | 0x80000000, // decoy for pair 1
                2,              // target for pair 2
                2 | 0x80000000, // decoy for pair 2
                3,              // target for pair 3
                3 | 0x80000000  // decoy for pair 3
            };
            bool[] isDecoy = { false, true, false, true, false, true };
            int[] allIndices = { 0, 1, 2, 3, 4, 5 };

            int[] winners = CalibrationScorer.CompeteCalibrationPairs(
                scores, entryIds, isDecoy, allIndices);

            // Should have 3 winners (one per pair)
            Assert.AreEqual(3, winners.Length);

            // Verify winners are sorted by score descending
            for (int i = 0; i < winners.Length - 1; i++)
            {
                Assert.IsTrue(scores[winners[i]] >= scores[winners[i + 1]],
                    string.Format("Winners should be sorted by score descending at position {0}", i));
            }

            // Find each pair's winner
            bool foundTargetWin = false;
            bool foundDecoyWin = false;
            bool foundTieDecoyWin = false;

            foreach (int w in winners)
            {
                uint baseId = entryIds[w] & 0x7FFFFFFF;
                if (baseId == 1 && !isDecoy[w])
                    foundTargetWin = true; // target score 0.9 > decoy score 0.5
                if (baseId == 2 && isDecoy[w])
                    foundDecoyWin = true; // decoy score 0.7 > target score 0.3
                if (baseId == 3 && isDecoy[w])
                    foundTieDecoyWin = true; // tie 0.6 == 0.6 -> decoy wins
            }

            Assert.IsTrue(foundTargetWin, "Pair 1: target should win (0.9 > 0.5)");
            Assert.IsTrue(foundDecoyWin, "Pair 2: decoy should win (0.7 > 0.3)");
            Assert.IsTrue(foundTieDecoyWin, "Pair 3: decoy should win on tie (0.6 == 0.6)");
        }

        #endregion

        #region HasMatch Tests

        [TestMethod]
        public void TestHasMatch()
        {
            var tolerance = FragmentToleranceConfig.UnitResolution(0.5);
            double[] spectrumMzs = { 200.0, 300.0, 400.0, 500.0, 600.0 };

            // Exact match
            Assert.IsTrue(SpectralScorer.HasMatch(300.0, spectrumMzs, tolerance));

            // Within tolerance
            Assert.IsTrue(SpectralScorer.HasMatch(300.3, spectrumMzs, tolerance));
            Assert.IsTrue(SpectralScorer.HasMatch(299.7, spectrumMzs, tolerance));

            // Outside tolerance
            Assert.IsFalse(SpectralScorer.HasMatch(300.6, spectrumMzs, tolerance));
            Assert.IsFalse(SpectralScorer.HasMatch(250.0, spectrumMzs, tolerance));

            // Edge: empty spectrum
            Assert.IsFalse(SpectralScorer.HasMatch(300.0, new double[0], tolerance));

            // PPM tolerance
            var ppmTolerance = FragmentToleranceConfig.Hram(10.0);
            // 10 ppm of 500 = 0.005 Da
            Assert.IsTrue(SpectralScorer.HasMatch(500.002, spectrumMzs, ppmTolerance));
            Assert.IsFalse(SpectralScorer.HasMatch(500.01, spectrumMzs, ppmTolerance));
        }

        #endregion
    }
}
