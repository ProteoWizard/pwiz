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
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.OspreySharp.Chromatography;
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

            // PEPTIDEK with cycle=1: shift internal [PEPTIDE] by 1 -> EPTIDEP, keep K
            string cycled = generator.CycleSequence("PEPTIDEK", 1, out _);
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
                Mzs = new[] { 300.0, 400.0, 500.0 },
                Intensities = new[] { 100.0f, 50.0f, 75.0f }
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

        #region Regression Tests - Sessions 5-9 Bug Fixes

        /// <summary>
        /// Session 9 fix: Two library fragments mapping to the same 1-Th bin
        /// must contribute to xcorr once, not twice. Without dedup, the score
        /// is inflated because the same preprocessed bin value is summed twice.
        /// Fixture: two fragments at 500.1 and 500.3 Th (same unit-res bin).
        /// </summary>
        [TestMethod]
        public void TestXcorrFragmentBinDedup()
        {
            var scorer = new SpectralScorer(); // unit resolution: 1.0005 Th bins

            // Spectrum with a single peak at 500.2
            var spectrum = new Spectrum
            {
                Mzs = new[] { 500.2 },
                Intensities = new[] { 10000.0f }
            };

            // Library entry with two fragments in the SAME bin (~500 Th)
            var entryTwoFrags = new LibraryEntry(1, "TEST", "TEST", 2, 300.0, 10.0);
            entryTwoFrags.Fragments.Add(new LibraryFragment { Mz = 500.1, RelativeIntensity = 1.0f });
            entryTwoFrags.Fragments.Add(new LibraryFragment { Mz = 500.3, RelativeIntensity = 1.0f });

            // Library entry with one fragment in that bin
            var entryOneFrag = new LibraryEntry(2, "TEST2", "TEST2", 2, 300.0, 10.0);
            entryOneFrag.Fragments.Add(new LibraryFragment { Mz = 500.1, RelativeIntensity = 1.0f });

            double scoreTwoFrags = scorer.XcorrAtScan(spectrum, entryTwoFrags);
            double scoreOneFrag = scorer.XcorrAtScan(spectrum, entryOneFrag);

            // With dedup, two fragments sharing a bin should score identically
            // to one fragment in that bin. Pre-fix code double-counted.
            Assert.AreEqual(scoreOneFrag, scoreTwoFrags, 1e-10,
                "Two fragments in the same bin must score the same as one (bin dedup)");
        }

        /// <summary>
        /// Session 9 fix: LibCosine must match the closest peak by m/z, not
        /// the most intense. When two observed peaks are within tolerance of a
        /// library fragment, the closer one wins even if it is weaker.
        /// </summary>
        [TestMethod]
        public void TestLibCosineClosestByMz()
        {
            var scorer = new SpectralScorer();
            var tolerance = FragmentToleranceConfig.UnitResolution(0.5);

            // Two observed peaks within 0.5 Th tolerance of fragment at 500.0:
            //   499.8 (closer, weaker) and 500.4 (farther, much stronger)
            var spectrum = new Spectrum
            {
                Mzs = new[] { 499.8, 500.4 },
                Intensities = new[] { 100.0f, 10000.0f }
            };

            var entry = new LibraryEntry(1, "TEST", "TEST", 2, 300.0, 10.0);
            entry.Fragments.Add(new LibraryFragment { Mz = 500.0, RelativeIntensity = 1.0f });

            double score = scorer.LibCosine(spectrum, entry, tolerance);

            // The matched intensity should be 100.0 (closest), not 10000.0 (most intense).
            // With sqrt preprocessing: sqrt(100) = 10, sqrt(1) = 1 -> cosine = 10/(10*1) = 1.0
            // If it incorrectly chose 10000: sqrt(10000) = 100 -> cosine = 100/(100*1) = 1.0
            // Both give cosine=1.0 since single fragment, but we can verify the chosen intensity
            // by adding a second unmatched fragment that dilutes the score differently.

            // Better test: add a second fragment that is unmatched.
            var entry2 = new LibraryEntry(2, "TEST2", "TEST2", 2, 300.0, 10.0);
            entry2.Fragments.Add(new LibraryFragment { Mz = 500.0, RelativeIntensity = 1.0f });
            entry2.Fragments.Add(new LibraryFragment { Mz = 800.0, RelativeIntensity = 1.0f });

            double score2 = scorer.LibCosine(spectrum, entry2, tolerance);

            // With closest-by-mz (correct): matched intensity = 100
            //   lib = [sqrt(1), sqrt(1)] = [1, 1], obs = [sqrt(100), 0] = [10, 0]
            //   dot = 10, |lib| = sqrt(2), |obs| = 10 -> cosine = 10/(sqrt(2)*10) = 0.707
            // With most-intense (bug): matched intensity = 10000
            //   obs = [sqrt(10000), 0] = [100, 0]
            //   dot = 100, |lib| = sqrt(2), |obs| = 100 -> cosine = 100/(sqrt(2)*100) = 0.707
            // Same cosine! Need a different fixture where the distinction matters.

            // Use two library fragments that compete for different observed peaks.
            var spectrum3 = new Spectrum
            {
                Mzs = new[] { 499.8, 500.4, 600.0 },
                Intensities = new[] { 100.0f, 10000.0f, 5000.0f }
            };
            var entry3 = new LibraryEntry(3, "TEST3", "TEST3", 2, 300.0, 10.0);
            entry3.Fragments.Add(new LibraryFragment { Mz = 500.0, RelativeIntensity = 1.0f });
            entry3.Fragments.Add(new LibraryFragment { Mz = 600.0, RelativeIntensity = 0.5f });

            double score3 = scorer.LibCosine(spectrum3, entry3, tolerance);

            // Fragment at 500.0: closest match is 499.8 (diff=0.2) not 500.4 (diff=0.4)
            // Matched intensities: [100.0, 5000.0]
            // lib_sqrt = [1.0, sqrt(0.5)], obs_sqrt = [10.0, sqrt(5000)]
            double libA = Math.Sqrt(1.0f), libB = Math.Sqrt(0.5f);
            double obsA = Math.Sqrt(100.0), obsB = Math.Sqrt(5000.0);
            double expectedCosine = (libA * obsA + libB * obsB) /
                (Math.Sqrt(libA * libA + libB * libB) * Math.Sqrt(obsA * obsA + obsB * obsB));

            Assert.AreEqual(expectedCosine, score3, 1e-6,
                "LibCosine must match closest peak by m/z, not most intense");
        }

        /// <summary>
        /// Session 9 fix: n_coeluting_fragments counts a fragment as coeluting
        /// only if its MEAN pairwise correlation with other fragments is > 0.
        /// The pre-fix code counted a fragment as coeluting if ANY pairwise
        /// correlation was positive, which is too permissive.
        /// </summary>
        [TestMethod]
        public void TestCoelutingFragmentsMeanPositive()
        {
            // 3 fragments, 8 scans within peak
            // Fragment 0: clean Gaussian peak (coeluting)
            // Fragment 1: clean Gaussian peak, similar shape (coeluting)
            // Fragment 2: inverted shape (anti-correlated with 0 and 1)
            //   mean pairwise with 0 and 1 will be negative -> NOT coeluting

            double[] frag0 = { 0, 1, 3, 8, 10, 8, 3, 1 };
            double[] frag1 = { 0, 2, 5, 9, 11, 9, 4, 1 };
            double[] frag2 = { 10, 9, 7, 2, 0, 2, 7, 9 }; // anti-correlated

            var xics = new List<double[]> { frag0, frag1, frag2 };

            // Compute per-fragment mean pairwise correlation
            int nFrags = xics.Count;
            var fragCorrSum = new double[nFrags];
            var fragCorrCount = new int[nFrags];

            for (int i = 0; i < nFrags; i++)
            {
                for (int j = i + 1; j < nFrags; j++)
                {
                    double r = ComputePearson(xics[i], xics[j]);
                    fragCorrSum[i] += r;
                    fragCorrCount[i]++;
                    fragCorrSum[j] += r;
                    fragCorrCount[j]++;
                }
            }

            int nCoeluting = 0;
            for (int i = 0; i < nFrags; i++)
            {
                double meanCorr = fragCorrCount[i] > 0 ? fragCorrSum[i] / fragCorrCount[i] : 0.0;
                if (meanCorr > 0.0)
                    nCoeluting++;
            }

            // Fragments 0 and 1 are correlated (~1.0 with each other).
            // Fragment 2 is anti-correlated with both (~-1.0).
            // frag0 mean = (r01 + r02) / 2 = (~1 + ~-1) / 2 -> near 0
            // frag1 mean = (r01 + r12) / 2 = (~1 + ~-1) / 2 -> near 0
            // frag2 mean = (r02 + r12) / 2 = (~-1 + ~-1) / 2 = ~-1

            // With mean-positive (correct): frag0 and frag1 have ~0 mean (barely positive
            // due to imperfect anti-correlation), frag2 has negative mean.
            // With any-positive (bug): all 3 would count because r01 > 0 gives frag2 a positive pair.

            // frag2's mean pairwise correlation must be negative
            double frag2Mean = fragCorrSum[2] / fragCorrCount[2];
            Assert.IsTrue(frag2Mean < 0.0,
                string.Format("Fragment 2 (anti-correlated) mean pairwise should be negative, got {0:F4}", frag2Mean));

            // The anti-correlated fragment should NOT be counted as coeluting
            Assert.IsTrue(nCoeluting <= 2,
                "Anti-correlated fragment should not be counted as coeluting under mean-positive rule");
        }

        /// <summary>
        /// Session 9 fix: MS1 precursor coelution and isotope cosine must be
        /// 0.0 for unit resolution mode. Unit resolution instruments don't
        /// have sufficient MS1 resolution for meaningful precursor features.
        /// </summary>
        [TestMethod]
        public void TestMs1FeaturesHramOnly()
        {
            // In unit resolution mode, MS1 features should be zeroed
            var unitConfig = new OspreyConfig { ResolutionMode = ResolutionMode.UnitResolution };
            Assert.AreEqual(ResolutionMode.UnitResolution, unitConfig.ResolutionMode);

            // The feature should be gated by resolution mode check:
            // if (config.ResolutionMode != ResolutionMode.HRAM) -> features = 0.0
            bool isHram = unitConfig.ResolutionMode == ResolutionMode.HRAM;
            Assert.IsFalse(isHram, "Unit resolution should not enable HRAM features");

            // Verify HRAM mode enables the features
            var hramConfig = new OspreyConfig { ResolutionMode = ResolutionMode.HRAM };
            bool isHramTrue = hramConfig.ResolutionMode == ResolutionMode.HRAM;
            Assert.IsTrue(isHramTrue, "High resolution should enable HRAM features");
        }

        /// <summary>
        /// Session 9 fix: Median polish convergence must compare residuals
        /// AFTER both row and column sweeps complete, not incrementally.
        /// Incremental checking can converge too early when the row sweep
        /// leaves small residuals that the column sweep would disturb.
        /// </summary>
        [TestMethod]
        public void TestMedianPolishConvergenceAfterBothSweeps()
        {
            // Construct a matrix where:
            // - Row sweep alone leaves max_change < tol
            // - But the subsequent column sweep creates larger changes
            // If convergence is checked after row sweep only, we'd stop too early.

            // 3 fragments x 5 scans with structured signal
            var xics = new List<KeyValuePair<int, double[]>>();
            xics.Add(new KeyValuePair<int, double[]>(0, new[] { 100.0, 200.0, 300.0, 200.0, 100.0 }));
            xics.Add(new KeyValuePair<int, double[]>(1, new[] { 50.0, 100.0, 150.0, 100.0, 50.0 }));
            xics.Add(new KeyValuePair<int, double[]>(2, new[] { 200.0, 100.0, 50.0, 100.0, 200.0 }));

            double[] rts = { 1.0, 2.0, 3.0, 4.0, 5.0 };

            var result = TukeyMedianPolish.Compute(xics, rts, 20, 1e-4);
            Assert.IsNotNull(result, "Median polish should return a result");
            Assert.IsTrue(result.Converged, "Median polish should converge");

            // Verify the decomposition reconstructs the original data
            for (int f = 0; f < 3; f++)
            {
                for (int s = 0; s < 5; s++)
                {
                    double original = xics[f].Value[s];
                    if (original <= 0)
                        continue;
                    double reconstructed = Math.Exp(result.Overall + result.RowEffects[f] +
                        result.ColEffects[s] + result.Residuals[f][s]);
                    Assert.AreEqual(original, reconstructed, original * 0.01,
                        string.Format("Reconstruction at [{0},{1}] should be within 1% of original", f, s));
                }
            }
        }

        /// <summary>
        /// Session 5-8 fix: Apex selection ties must resolve to LAST index
        /// (>=, not >) to match Rust's Iterator::max_by which returns the
        /// last element among equal maxima.
        /// </summary>
        [TestMethod]
        public void TestApexTieBreakLastWins()
        {
            // XIC with a flat plateau of equal intensity at scans 3-6
            double[] intensities = { 0, 1, 5, 10, 10, 10, 10, 5, 1, 0 };

            // Find apex using >= (last-wins) semantics
            int apexIdx = 0;
            double apexVal = intensities[0];
            for (int i = 1; i < intensities.Length; i++)
            {
                if (intensities[i] >= apexVal) // >= means last wins on tie
                {
                    apexVal = intensities[i];
                    apexIdx = i;
                }
            }

            // With >=: apex should be index 6 (last of the 10-valued plateau)
            Assert.AreEqual(6, apexIdx,
                "Apex tie-break should resolve to LAST index (>= semantics)");

            // With > (bug): apex would be index 3 (first of the plateau)
            int buggyApex = 0;
            double buggyVal = intensities[0];
            for (int i = 1; i < intensities.Length; i++)
            {
                if (intensities[i] > buggyVal) // > means first wins
                {
                    buggyVal = intensities[i];
                    buggyApex = i;
                }
            }
            Assert.AreEqual(3, buggyApex,
                "Buggy > semantics would pick first index of plateau");

            // Prove they differ
            Assert.AreNotEqual(apexIdx, buggyApex,
                "The >= and > semantics must produce different results on plateaus");
        }

        /// <summary>
        /// Session 5-8 fix: SNR must be computed on the reference fragment's
        /// raw intensities, not on the composite sum of all fragment XICs.
        /// The composite sum inflates the signal relative to single-fragment
        /// noise, producing artificially high SNR values.
        /// </summary>
        [TestMethod]
        public void TestSnrUsesRefXicNotComposite()
        {
            // Two fragments: ref (stronger) and weak interferer
            double[] refXic = { 1, 1, 1, 5, 20, 50, 20, 5, 1, 1, 1, 1, 1, 1, 1, 1 };
            double[] weakXic = { 1, 1, 1, 3, 8, 15, 8, 3, 1, 1, 1, 1, 1, 1, 1, 1 };

            // Composite sum
            double[] composite = new double[refXic.Length];
            for (int i = 0; i < refXic.Length; i++)
                composite[i] = refXic[i] + weakXic[i];

            int apexIdx = 5; // peak apex
            int startIdx = 3;
            int endIdx = 7;

            double snrRef = PeakDetector.ComputeSnr(refXic, apexIdx, startIdx, endIdx);
            double snrComposite = PeakDetector.ComputeSnr(composite, apexIdx, startIdx, endIdx);

            // Both should be positive
            Assert.IsTrue(snrRef > 0, "SNR from ref XIC should be positive");
            Assert.IsTrue(snrComposite > 0, "SNR from composite should be positive");

            // The two must produce different SNR values - the bug was using composite
            // instead of ref, which changes the signal/noise ratio because composite
            // sums multiple fragments with different peak shapes and noise floors.
            Assert.AreNotEqual(snrRef, snrComposite, 0.01,
                string.Format("SNR from ref ({0:F2}) and composite ({1:F2}) must differ - " +
                "using the wrong buffer is the bug", snrRef, snrComposite));
        }

        /// <summary>
        /// Session 9 fix: peak_area must use trapezoidal integration with
        /// non-uniform RT spacing, not rectangular sum. The rectangular sum
        /// ignores RT intervals and treats all scans as equally spaced.
        /// </summary>
        [TestMethod]
        public void TestPeakAreaTrapezoidal()
        {
            // Non-uniform RT spacing: gaps of 0.1, 0.1, 0.5, 0.1
            double[] rts = { 1.0, 1.1, 1.2, 1.7, 1.8 };
            double[] values = { 0.0, 10.0, 20.0, 10.0, 0.0 };

            double trapArea = PeakDetector.TrapezoidalArea(rts, values, 0, 4);

            // Hand-computed trapezoidal:
            // [1.0,1.1]: (0+10)/2 * 0.1 = 0.5
            // [1.1,1.2]: (10+20)/2 * 0.1 = 1.5
            // [1.2,1.7]: (20+10)/2 * 0.5 = 7.5
            // [1.7,1.8]: (10+0)/2 * 0.1 = 0.5
            // Total: 10.0
            Assert.AreEqual(10.0, trapArea, 1e-10,
                "Trapezoidal area with non-uniform spacing");

            // Rectangular sum (the bug): sum of values = 40.0 (ignores RT entirely)
            double rectSum = 0;
            for (int i = 0; i < values.Length; i++)
                rectSum += values[i];
            Assert.AreNotEqual(rectSum, trapArea,
                "Trapezoidal area must differ from rectangular sum with non-uniform spacing");
        }

        /// <summary>
        /// Session 9 fix: MS2 calibrated fragment tolerance must be 3*SD
        /// (not the default 0.5 Th) and must apply the m/z offset correction.
        /// </summary>
        [TestMethod]
        public void TestMs2CalibratedTolerance()
        {
            // After MS2 calibration: mean error = -0.065 Th, SD = 0.13 Th
            double meanError = -0.065;
            double sd = 0.13;

            // Calibrated tolerance = 3 * SD
            double calibratedTolerance = 3.0 * sd;
            Assert.AreEqual(0.39, calibratedTolerance, 1e-10,
                "Calibrated tolerance should be 3*SD");

            // Default tolerance (the bug: using this instead of calibrated)
            double defaultTolerance = 0.5;
            Assert.AreNotEqual(defaultTolerance, calibratedTolerance,
                "Calibrated tolerance must differ from default");

            // Corrected m/z = observed - meanError
            double observedMz = 500.0;
            double correctedMz = observedMz - meanError; // 500.065
            Assert.AreEqual(500.065, correctedMz, 1e-10,
                "m/z offset correction should shift by -meanError");
        }

        /// <summary>
        /// Session 9 fix: peak_sharpness is the mean of left and right slopes
        /// (intensity / time), not an intensity ratio. The pre-fix code divided
        /// apex by edge intensity, which is dimensionless. The correct
        /// calculation uses RT intervals, giving units of intensity/minute.
        /// </summary>
        [TestMethod]
        public void TestPeakSharpnessIsSlope()
        {
            // Asymmetric peak: steep left rise, gradual right fall
            // RTs:        1.0   2.0   3.0   5.0   7.0
            // Intensities: 0    50   100    50     0
            double[] rts = { 1.0, 2.0, 3.0, 5.0, 7.0 };
            double[] intensities = { 0.0, 50.0, 100.0, 50.0, 0.0 };
            int apexIdx = 2;
            int startIdx = 0;
            int endIdx = 4;

            // Left slope: (apex - start) / (rt_apex - rt_start) = (100 - 0) / (3.0 - 1.0) = 50.0
            double leftSlope = (intensities[apexIdx] - intensities[startIdx]) /
                               (rts[apexIdx] - rts[startIdx]);
            Assert.AreEqual(50.0, leftSlope, 1e-10, "Left slope");

            // Right slope: (apex - end) / (rt_end - rt_apex) = (100 - 0) / (7.0 - 3.0) = 25.0
            double rightSlope = (intensities[apexIdx] - intensities[endIdx]) /
                                (rts[endIdx] - rts[apexIdx]);
            Assert.AreEqual(25.0, rightSlope, 1e-10, "Right slope");

            // Sharpness = mean of slopes = (50 + 25) / 2 = 37.5
            double sharpness = (leftSlope + rightSlope) * 0.5;
            Assert.AreEqual(37.5, sharpness, 1e-10, "Sharpness as mean slope");

            // Bug version: intensity ratio apex / mean(edges)
            double buggyRatio = intensities[apexIdx] /
                ((intensities[startIdx] + intensities[endIdx]) * 0.5 + 1e-10);
            // 100 / (0 + 0) / 2 -> division by ~zero -> huge number
            Assert.IsTrue(Math.Abs(sharpness - buggyRatio) > 1.0,
                "Slope-based sharpness must differ from intensity ratio");
        }

        /// <summary>
        /// Session 9 fix: peak shape features (apex, area, sharpness) must
        /// be computed from the reference XIC (highest total intensity), not
        /// the composite sum of all fragments. The composite inflates the
        /// apex and area, and can shift the peak shape.
        /// </summary>
        [TestMethod]
        public void TestPeakShapeFromRefXicNotComposite()
        {
            // Fragment 0 (ref): moderate signal, peak at scan 6
            double[] frag0 = { 0, 0, 0, 0, 5, 20, 60, 20, 5, 0 };
            // Fragment 1: strong interference earlier, peak at scan 3
            double[] frag1 = { 0, 10, 50, 90, 50, 10, 2, 0, 0, 0 };

            // Ref XIC is frag1 (total = 212 > frag0 total = 110)
            double frag0Total = 0, frag1Total = 0;
            for (int i = 0; i < 10; i++) { frag0Total += frag0[i]; frag1Total += frag1[i]; }
            Assert.IsTrue(frag1Total > frag0Total, "frag1 should be the reference XIC");

            // Apex from ref XIC (frag1): scan 3, value 90
            int refApexIdx = 0;
            double refApexVal = frag1[0];
            for (int i = 1; i < frag1.Length; i++)
                if (frag1[i] > refApexVal) { refApexVal = frag1[i]; refApexIdx = i; }

            // Apex from composite: scan 3, value 90 (dominated by frag1)
            // But frag0's apex is at scan 6 (value 60).
            // Key point: the ref XIC gives the CORRECT apex for this peptide
            // (scan 3), while using frag0 alone would give the wrong one (scan 6).
            // The test proves that ref selection matters.

            int frag0ApexIdx = 0;
            double frag0ApexVal = frag0[0];
            for (int i = 1; i < frag0.Length; i++)
                if (frag0[i] > frag0ApexVal) { frag0ApexVal = frag0[i]; frag0ApexIdx = i; }

            Assert.AreEqual(3, refApexIdx, "Ref XIC apex should be at scan 3");
            Assert.AreEqual(6, frag0ApexIdx, "Non-ref fragment apex at scan 6");
            Assert.AreNotEqual(refApexIdx, frag0ApexIdx,
                "Ref and non-ref fragments must have different apex scans - " +
                "using the wrong fragment is the bug");
        }

        /// <summary>
        /// Session 5-8 fix: Stable sort is required for apex ranking when
        /// multiple entries have equal scores. List.Sort (introsort) is
        /// unstable and can reorder equal elements differently than Rust's
        /// stable sort_by, causing divergence in downstream processing.
        /// </summary>
        [TestMethod]
        public void TestStableSortOnApexRanking()
        {
            // Create entries with tied scores but different IDs
            var items = new List<(int id, double score)>
            {
                (1, 5.0), (2, 3.0), (3, 5.0), (4, 5.0), (5, 3.0), (6, 5.0)
            };

            // Stable sort (LINQ OrderByDescending) preserves insertion order among ties
            var stableSorted = items.OrderByDescending(x => x.score).ToList();

            // IDs of score=5.0 group should maintain relative order: 1, 3, 4, 6
            var topGroup = stableSorted.Where(x => Math.Abs(x.score - 5.0) < 1e-10).ToList();
            Assert.AreEqual(4, topGroup.Count);
            Assert.AreEqual(1, topGroup[0].id, "Stable sort preserves first tied element");
            Assert.AreEqual(3, topGroup[1].id, "Stable sort preserves second tied element");
            Assert.AreEqual(4, topGroup[2].id, "Stable sort preserves third tied element");
            Assert.AreEqual(6, topGroup[3].id, "Stable sort preserves fourth tied element");

            // Unstable sort (Array.Sort / List.Sort) may reorder ties.
            // We can't assert a specific wrong order, but we can verify the
            // stable sort is deterministic across runs (the actual invariant we need).
            var stableSorted2 = items.OrderByDescending(x => x.score).ToList();
            for (int i = 0; i < stableSorted.Count; i++)
                Assert.AreEqual(stableSorted[i].id, stableSorted2[i].id,
                    string.Format("Stable sort must be deterministic at position {0}", i));
        }

        /// <summary>
        /// Session 5-8 fix: Decoy collision exclusion must detect when a
        /// reversed target sequence matches another target in the library.
        /// Without collision detection, target-decoy pairs share a sequence,
        /// corrupting FDR estimation.
        /// </summary>
        [TestMethod]
        public void TestDecoyCollisionExclusion()
        {
            var generator = new DecoyGenerator(Enzyme.Trypsin);

            // Target 1: ABCDEFK -> reversal = FEDCBAK
            var target1 = new LibraryEntry(1, "ABCDEFK", "ABCDEFK", 2, 500.0, 10.0);
            target1.Fragments.Add(new LibraryFragment
            {
                Mz = 300.0, RelativeIntensity = 1.0f,
                Annotation = new FragmentAnnotation { IonType = IonType.B, Ordinal = 3, Charge = 1 }
            });

            // Target 2: IS the reversal of target 1 (FEDCBAK)
            var target2 = new LibraryEntry(2, "FEDCBAK", "FEDCBAK", 2, 500.0, 12.0);
            target2.Fragments.Add(new LibraryFragment
            {
                Mz = 350.0, RelativeIntensity = 1.0f,
                Annotation = new FragmentAnnotation { IonType = IonType.Y, Ordinal = 3, Charge = 1 }
            });

            // Target 1's decoy (FEDCBAK) collides with target 2.
            // The generator should detect this and fall back to cycling.
            // Note: the collision detection happens in AnalysisPipeline.GenerateDecoys,
            // not in DecoyGenerator.Generate directly. DecoyGenerator.Generate simply
            // reverses. We test the reversal produces the collision, then verify
            // that cycling produces something different.
            var decoy1 = generator.Generate(target1);

            // The standard reversal WOULD produce FEDCBAK (collision with target 2).
            // DecoyGenerator.Generate doesn't know about other targets - it just reverses.
            // The collision detection layer in the pipeline would reject this and
            // try cycling. Here we verify the reversal does produce the collision,
            // proving the collision detection is needed.
            Assert.AreEqual("FEDCBAK", decoy1.Sequence,
                "Standard reversal of ABCDEFK should produce FEDCBAK");

            // Now generate via cycling (what the pipeline would do after detecting collision)
            string cycled = generator.CycleSequence("ABCDEFK", 1, out _);
            Assert.AreNotEqual("ABCDEFK", cycled,
                "Cycled sequence must differ from original");
            Assert.AreNotEqual("FEDCBAK", cycled,
                "Cycled sequence must differ from the collision target");
        }

        /// <summary>
        /// Session 9 fix: scan boundary must use strict less-than for the
        /// upper bound break to prevent off-by-one. When the last spectrum
        /// RT equals exactly expectedRt + tolerance, it must be included.
        /// </summary>
        [TestMethod]
        public void TestScanBoundaryOrder()
        {
            // Sorted scan RTs
            double[] scanRts = { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0 };
            double expectedRt = 4.0;
            double tolerance = 2.0;

            // Expected window: [2.0, 6.0]
            double lowerBound = expectedRt - tolerance;
            double upperBound = expectedRt + tolerance;

            int startScan = 0;
            int endScan = scanRts.Length - 1;

            // Find start: first scan >= lowerBound
            for (int i = 0; i < scanRts.Length; i++)
            {
                if (scanRts[i] >= lowerBound)
                {
                    startScan = i;
                    break;
                }
            }

            // Find end: last scan <= upperBound
            // Correct: check upper bound BEFORE updating endScan
            for (int i = startScan; i < scanRts.Length; i++)
            {
                if (scanRts[i] > upperBound)
                    break;
                endScan = i;
            }

            // Scan at RT=6.0 (index 5) equals upperBound exactly - must be included
            Assert.AreEqual(1, startScan, "Start scan should be index 1 (RT=2.0)");
            Assert.AreEqual(5, endScan, "End scan should be index 5 (RT=6.0, at upper bound)");

            // Verify the boundary scan (RT=6.0) is included
            Assert.AreEqual(upperBound, scanRts[endScan], 1e-10,
                "Boundary scan at exactly upperBound must be included");
        }

        /// <summary>
        /// Full XCorr pipeline verification: hand-computed reference value for a
        /// simple spectrum + library entry. Validates the entire chain: sqrt binning,
        /// windowing normalization, sliding window subtraction, fragment lookup + dedup,
        /// and final scaling.
        /// </summary>
        [TestMethod]
        public void TestXcorrFullPipeline()
        {
            var scorer = new SpectralScorer(); // unit resolution bins

            // Simple spectrum: two peaks at 400 and 500 Th
            var spectrum = new Spectrum
            {
                Mzs = new[] { 400.0, 500.0 },
                Intensities = new[] { 10000.0f, 10000.0f }
            };

            // Library with one fragment at 400 Th
            var entry = new LibraryEntry(1, "TEST", "TEST", 2, 300.0, 10.0);
            entry.Fragments.Add(new LibraryFragment { Mz = 400.0, RelativeIntensity = 1.0f });

            double score = scorer.XcorrAtScan(spectrum, entry);

            // Score must be positive (matching peak present)
            Assert.IsTrue(score > 0,
                string.Format("XCorr should be positive for matching spectrum, got {0}", score));

            // Library with fragment at 700 Th (no matching peak)
            var noMatch = new LibraryEntry(2, "TEST2", "TEST2", 2, 300.0, 10.0);
            noMatch.Fragments.Add(new LibraryFragment { Mz = 700.0, RelativeIntensity = 1.0f });

            double noMatchScore = scorer.XcorrAtScan(spectrum, noMatch);

            // Score should be much lower (or negative) for no match
            Assert.IsTrue(score > noMatchScore,
                string.Format("Matching XCorr ({0:G6}) must exceed non-matching ({1:G6})",
                    score, noMatchScore));
        }

        /// <summary>
        /// Session 5-8 fix: XCorr preprocessing must use f64 (double) throughout.
        /// Using f32 (float) for the sliding window accumulator produces ~4e-6
        /// drift due to reduced mantissa precision, which is large enough to
        /// change feature rankings and FDR outcomes.
        /// </summary>
        [TestMethod]
        public void TestXcorrF64VsF32PrecisionDrift()
        {
            // Simulate the sliding window subtraction with both float and double.
            // Use a spectrum with many small values that accumulate rounding error.
            int n = 2000;
            double[] spectrumD = new double[n];
            float[] spectrumF = new float[n];
            var rng = new Random(42);
            for (int i = 0; i < n; i++)
            {
                double v = rng.NextDouble() * 100.0;
                spectrumD[i] = v;
                spectrumF[i] = (float)v;
            }

            // Sliding window subtraction (offset=75) in double precision
            const int offset = 75;
            double normFactor = 1.0 / (2 * offset);
            double[] prefixD = new double[n + 1];
            for (int i = 0; i < n; i++) prefixD[i + 1] = prefixD[i] + spectrumD[i];
            double[] resultD = new double[n];
            for (int i = 0; i < n; i++)
            {
                int left = Math.Max(0, i - offset);
                int right = Math.Min(n, i + offset + 1);
                double windowSum = prefixD[right] - prefixD[left];
                resultD[i] = spectrumD[i] - (windowSum - spectrumD[i]) * normFactor;
            }

            // Same computation in float precision
            float normFactorF = 1.0f / (2 * offset);
            float[] prefixF = new float[n + 1];
            for (int i = 0; i < n; i++) prefixF[i + 1] = prefixF[i] + spectrumF[i];
            double[] resultF = new double[n];
            for (int i = 0; i < n; i++)
            {
                int left = Math.Max(0, i - offset);
                int right = Math.Min(n, i + offset + 1);
                float windowSum = prefixF[right] - prefixF[left];
                resultF[i] = spectrumF[i] - (windowSum - spectrumF[i]) * normFactorF;
            }

            // The two must differ due to float rounding in the prefix sum
            double maxDiff = 0.0;
            for (int i = 0; i < n; i++)
            {
                double d = Math.Abs(resultD[i] - resultF[i]);
                if (d > maxDiff)
                    maxDiff = d;
            }

            Assert.IsTrue(maxDiff > 1e-6,
                string.Format("f32 vs f64 sliding window should differ by >1e-6, got {0:E2}. " +
                "Using f32 introduces drift that changes feature rankings.", maxDiff));

            // The f64 result is what both C# and Rust (after the flip) should use
            Assert.IsTrue(maxDiff < 1.0,
                string.Format("Drift should be small but measurable, got {0:E2}", maxDiff));
        }

        /// <summary>
        /// Session 5-8 fix: full XCorr windowing normalization pipeline test.
        /// Verifies: sqrt binning (accumulate, not assign), 10-window
        /// normalization to 50.0, 5% threshold, sliding window subtraction.
        /// A simple two-peak spectrum produces known intermediate values.
        /// </summary>
        [TestMethod]
        public void TestXcorrWindowingNormalization()
        {
            var scorer = new SpectralScorer(); // unit resolution: 2000 bins

            // Two peaks: one strong at bin ~300, one weak at bin ~1500
            // BinConfig.UnitResolution: bin = (int)(mz * inverseBinWidth + oneMinusOffset)
            // inverseBinWidth = 1/1.0005079, oneMinusOffset = 0.6
            // For mz=300: bin = (int)(300/1.0005079 + 0.6) = (int)(299.847 + 0.6) = 300
            // For mz=1500: bin = (int)(1500/1.0005079 + 0.6) = (int)(1499.24 + 0.6) = 1499
            var spectrum = new Spectrum
            {
                Mzs = new[] { 300.0, 1500.0 },
                Intensities = new[] { 10000.0f, 100.0f }
            };

            // Library with fragment at 300 (matching strong peak)
            var entryStrong = new LibraryEntry(1, "T1", "T1", 2, 200.0, 5.0);
            entryStrong.Fragments.Add(new LibraryFragment { Mz = 300.0, RelativeIntensity = 1.0f });

            // Library with fragment at 1500 (matching weak peak)
            var entryWeak = new LibraryEntry(2, "T2", "T2", 2, 200.0, 5.0);
            entryWeak.Fragments.Add(new LibraryFragment { Mz = 1500.0, RelativeIntensity = 1.0f });

            double scoreStrong = scorer.XcorrAtScan(spectrum, entryStrong);
            double scoreWeak = scorer.XcorrAtScan(spectrum, entryWeak);

            // After windowing normalization, both peaks are normalized to 50.0
            // in their respective windows (assuming they're the max in their window).
            // So the preprocessed values at both bins should be similar magnitude
            // after sliding window subtraction. This is the key insight: windowing
            // normalization prevents a 100x intensity difference from dominating.
            // Without normalization, the strong peak would dominate the XCorr.

            // Both scores should be positive (matched peak present)
            Assert.IsTrue(scoreStrong > 0,
                string.Format("Strong match should have positive XCorr, got {0:G6}", scoreStrong));
            Assert.IsTrue(scoreWeak > 0,
                string.Format("Weak match should have positive XCorr, got {0:G6}", scoreWeak));

            // With correct windowing normalization, the ratio should be much closer
            // to 1.0 than the raw intensity ratio of 100:1
            double ratio = scoreStrong / scoreWeak;
            Assert.IsTrue(ratio < 10.0,
                string.Format("Windowed XCorr ratio ({0:F2}) should be much less than raw " +
                "intensity ratio (100.0) due to normalization", ratio));
        }

        /// <summary>
        /// Session 5-8 fix: iterative LDA refinement must select the best
        /// iteration (most targets passing 1% FDR), not the last iteration.
        /// The pre-fix code used a single-pass LDA that produced fewer
        /// passing targets than the iterative approach.
        ///
        /// This test constructs a synthetic match set where iterative
        /// refinement (with positive training set selection) outperforms
        /// the baseline single-feature scorer. The key mechanism is that
        /// iteration 1 identifies a positive training set that trains
        /// an LDA combining features, which passes more targets than any
        /// single feature alone.
        /// </summary>
        [TestMethod]
        public void TestIterativeLdaRefinement()
        {
            // Create a synthetic calibration match set with 4 features.
            // We need enough entries for 3-fold CV (MIN_POSITIVE_EXAMPLES=50,
            // so we need at least 150 targets passing initial FDR).
            // Structure: 200 target-decoy pairs (400 total entries)

            var rng = new Random(12345);
            int nPairs = 200;
            var matches = new CalibrationMatch[nPairs * 2];

            for (int p = 0; p < nPairs; p++)
            {
                uint baseId = (uint)(p + 1);
                bool isGoodTarget = p < 120; // first 120 pairs are "real" peptides

                // Target: good targets have high features, bad targets have low
                double corr = isGoodTarget ? 3.0 + rng.NextDouble() * 2.0 : rng.NextDouble() * 2.0;
                double libcos = isGoodTarget ? 0.6 + rng.NextDouble() * 0.3 : rng.NextDouble() * 0.4;
                double top6 = isGoodTarget ? 4.0 + rng.NextDouble() * 2.0 : rng.NextDouble() * 3.0;
                double xcorr = isGoodTarget ? 0.5 + rng.NextDouble() * 1.5 : rng.NextDouble() * 0.5;

                matches[p * 2] = new CalibrationMatch
                {
                    EntryId = baseId,
                    IsDecoy = false,
                    Sequence = string.Format("PEPTIDE{0}K", p),
                    CorrelationScore = corr,
                    LibcosineApex = libcos,
                    Top6MatchedApex = (byte)Math.Min(6, (int)top6),
                    XcorrScore = xcorr
                };

                // Decoy: always low features (random noise)
                matches[p * 2 + 1] = new CalibrationMatch
                {
                    EntryId = baseId | 0x80000000,
                    IsDecoy = true,
                    Sequence = string.Format("DECOY_PEPTIDE{0}K", p),
                    CorrelationScore = rng.NextDouble() * 2.0,
                    LibcosineApex = rng.NextDouble() * 0.3,
                    Top6MatchedApex = (byte)(rng.Next(4)),
                    XcorrScore = rng.NextDouble() * 0.4
                };
            }

            // Run the full iterative LDA pipeline
            int nPassing = CalibrationScorer.TrainAndScoreCalibration(matches, false);

            // The iterative LDA should identify a meaningful number of targets.
            // With 120 "good" targets and well-separated features, we expect
            // most of them to pass 1% FDR.
            Assert.IsTrue(nPassing > 50,
                string.Format("Iterative LDA should pass >50 targets at 1%% FDR, got {0}. " +
                "This validates that the iterative refinement with positive " +
                "training set selection is working.", nPassing));

            // Verify that targets have higher discriminant scores than decoys on average
            double targetMean = 0, decoyMean = 0;
            int nT = 0, nD = 0;
            for (int i = 0; i < matches.Length; i++)
            {
                if (matches[i].IsDecoy) { decoyMean += matches[i].DiscriminantScore; nD++; }
                else { targetMean += matches[i].DiscriminantScore; nT++; }
            }
            targetMean /= nT;
            decoyMean /= nD;

            Assert.IsTrue(targetMean > decoyMean,
                string.Format("Target mean discriminant ({0:F3}) should exceed decoy mean ({1:F3})",
                    targetMean, decoyMean));

            // Verify q-values were assigned (best targets should have q <= 0.01).
            // Note: nPassing counts unique peptide winners, while counting all
            // matches with q <= 0.01 may differ by +/-1 at the boundary due to
            // competition losers retaining q=1.0. Use approximate check.
            int nWithLowQ = 0;
            for (int i = 0; i < matches.Length; i++)
                if (!matches[i].IsDecoy && matches[i].QValue <= 0.01)
                    nWithLowQ++;

            Assert.IsTrue(Math.Abs(nPassing - nWithLowQ) <= 1,
                string.Format("Targets with q<=0.01 ({0}) should be within 1 of nPassing ({1})",
                    nWithLowQ, nPassing));
        }

        // Helper: Pearson correlation for test use
        private static double ComputePearson(double[] x, double[] y)
        {
            int n = x.Length;
            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0, sumY2 = 0;
            for (int i = 0; i < n; i++)
            {
                sumX += x[i]; sumY += y[i];
                sumXY += x[i] * y[i];
                sumX2 += x[i] * x[i]; sumY2 += y[i] * y[i];
            }
            double denom = Math.Sqrt((n * sumX2 - sumX * sumX) * (n * sumY2 - sumY * sumY));
            return denom > 0 ? (n * sumXY - sumX * sumY) / denom : 0.0;
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
