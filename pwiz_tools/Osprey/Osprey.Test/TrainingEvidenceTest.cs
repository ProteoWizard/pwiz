/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using pwiz.Osprey.Chromatography;
using pwiz.Osprey.Core;
using pwiz.Osprey.Scoring;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// The training export's per-ion evidence on synthetic spectra whose answer is known: a
    /// clean co-eluting ladder fits Osprey's median polish with R^2 and correlations near 1; a
    /// spike on one ion at the apex shows as a large apex residual, robust z and apex ratio;
    /// the XIC boundary values are the first and last peak scans; a co-eluting claimant that
    /// shares a peak is counted in both scopes and flagged as the better claimant; a peak too
    /// short to fit has no polish; and the recomputed cosine reproduces the one Osprey scored.
    /// </summary>
    [TestClass]
    public class TrainingEvidenceTest
    {
        private const string SEQUENCE = @"PEPTIDEK";
        private const int APEX = 5;             // scan index of the elution apex
        private const int FIRST = 2, LAST = 8;  // the final boundaries, 7 scans
        private const double RT0 = 10.0, RT_STEP = 0.1;
        private const uint SCAN0 = 100;
        private const double INTENSITY = 1e5;

        // Library fragments of PEPTIDEK: the six most intense are the polish core.
        private static readonly (IonType Type, int Ordinal, float Rel)[] LIBRARY =
        {
            (IonType.Y, 7, 1.0f), (IonType.Y, 6, 0.8f), (IonType.Y, 5, 0.6f), (IonType.Y, 4, 0.5f),
            (IonType.Y, 3, 0.4f), (IonType.B, 3, 0.3f), (IonType.B, 4, 0.2f), (IonType.B, 5, 0.1f),
        };

        [TestMethod]
        public void TestTrainingEvidence()
        {
            AssertCleanPrecursor();
            AssertCosineReproducesTheScorer();
            AssertInterferingSpike();
            AssertBoundaryValues();
            AssertSharedPeakClaimants();
            AssertDegenerateShortPeak();
            AssertSinglyChargedLadder();
            AssertDoubleCountingNeighbors();
            AssertDoubleCountingNeighborsTakeTheDedupsOrder();
            AssertProteinIdsMatchTheScoresParquet();
        }

        private static void AssertCleanPrecursor()
        {
            var entry = Peptide(SEQUENCE, 2, 1);
            var window = Window(entry, spikeSlot: -1);
            var row = Row(entry, FIRST, LAST);
            var record = Compute(entry, row, window, null);

            Assert.AreEqual(4 * (SEQUENCE.Length - 1), record.NSlots);
            Assert.AreEqual(LAST - FIRST + 1, record.NPeakScans);
            Assert.IsTrue(record.MpFitted && record.MpConverged);
            Assert.AreEqual(6, record.MpNCore);
            Assert.IsTrue(record.MpCosine > 0.99, @"a clean ladder matches its library");
            Assert.AreEqual(LIBRARY.Length, record.NIonsObserved, @"every library ion is observed at the apex");

            int y7 = Slot(IonType.Y, 7);
            foreach (var fragment in LIBRARY.Take(6))
            {
                int slot = Slot(fragment.Type, fragment.Ordinal);
                byte expected = TrainingIonFlags.APPLICABLE | TrainingIonFlags.IN_SCAN_RANGE |
                                TrainingIonFlags.MATCHED_AT_APEX | TrainingIonFlags.CORE |
                                TrainingIonFlags.LIBRARY_ANNOTATED;
                Assert.AreEqual(expected, record.IonFlags[slot], string.Format(@"flags of core slot {0}", slot));
                Assert.AreEqual(fragment.Rel, record.LibraryRelIntensity[slot]);
                Assert.IsTrue(record.CorrPolish[slot] > 0.99, @"a core ion follows the polish profile");
                Assert.IsTrue(record.CorrReference[slot] > 0.99, @"and the reference XIC");
                Assert.IsTrue(record.PolishR2[slot] > 0.98, @"and fits it");
                Assert.IsTrue(Math.Abs(record.PolishApexResidual[slot]) < 0.1, @"with no apex residual");
                // Within the fixture's 3% noise. The scale is the median ABSOLUTE residual,
                // which the polish's own medians pull toward zero, so a noise-level residual
                // reads as a few units of z - the spike below reads as more than a hundred.
                Assert.IsTrue(Math.Abs(record.PolishOutlierZ[slot]) < 6, @"and no outlier at the apex");
                Assert.AreEqual(1.0, record.PolishApexRatio[slot], 0.1);
                Assert.AreEqual(LAST - FIRST + 1, record.NFiniteScans[slot]);
            }
            Assert.AreEqual(1.0f, record.PolishRelIntensity[y7], 1e-3, @"the strongest ion is 1");
            Assert.AreEqual(0.6, record.PolishRelIntensity[Slot(IonType.Y, 5)], 0.05, @"the fit recovers the library ratios");

            // A library ion outside the core is annotated but not core.
            int b4 = Slot(IonType.B, 4);
            Assert.AreEqual(0, record.IonFlags[b4] & TrainingIonFlags.CORE);
            Assert.AreNotEqual(0, record.IonFlags[b4] & TrainingIonFlags.LIBRARY_ANNOTATED);

            // An ion with no peak: applicable, unmatched, zero at the apex, no projection. y1
            // (K, m/z 147) also lies below the 200 m/z scan window.
            int y1 = Slot(IonType.Y, 1);
            Assert.AreEqual(TrainingIonFlags.APPLICABLE, record.IonFlags[y1]);
            Assert.AreEqual(0f, record.ApexIntensity[y1]);
            Assert.IsTrue(float.IsNaN(record.ApexMzError[y1]));
            Assert.IsTrue(float.IsNaN(record.PolishRowEffect[y1]));
            Assert.AreEqual(0f, record.PolishApexRatio[y1]);
            Assert.IsTrue(float.IsNaN(record.LibraryRelIntensity[y1]));
        }

        /// <summary>
        /// The parity the export reports, checked against the scorer itself rather than a copy
        /// of its recipe: <see cref="CoelutionScorer"/> scores the synthetic window at fixed
        /// boundaries - the boundary override Stage 6 scores every reconciled row with - and the
        /// export's refit over the scored row's peak reproduces its
        /// <c>median_polish_cosine</c> bit for bit. The flag reports a disagreement rather than
        /// assuming agreement.
        /// </summary>
        private static void AssertCosineReproducesTheScorer()
        {
            var entry = Peptide(SEQUENCE, 2, 1);
            var window = Window(entry, spikeSlot: -1);
            var config = Settings(false).SearchConfig;
            var context = new ScoringContext(config, @"synthetic")
            {
                BoundaryOverrides = new Dictionary<uint, (double Apex, double Start, double End)>
                {
                    { entry.Id, (RT0 + RT_STEP * APEX, RT0 + RT_STEP * FIRST, RT0 + RT_STEP * LAST) },
                },
            };
            var scorer = context.Resolution.CreateScorer();
            context.EnsureXcorrScratchPool(scorer.BinConfig.NBins);
            var scored = new CoelutionScorer(null).ScoreWindow(new IsolationWindow(500.0, 2.0, 2.0),
                new List<LibraryEntry> { entry }, new List<Spectrum>(window.Spectra), new List<MS1Spectrum>(),
                null, MzCalibrationResult.Uncalibrated(), 5.0, 1.0, scorer, context).Single();
            Assert.AreEqual(SCAN0 + APEX, scored.ScanNumber, @"the scorer's apex is the fixture's");
            Assert.IsTrue(scored.Features[15] > 0.99, @"the scorer fit the clean ladder");

            var record = Compute(entry, scored, window, null);
            Assert.AreEqual(scored.Features[15], record.MpCosine, 0.0, @"the export's cosine is the scorer's, bit for bit");
            Assert.IsTrue(record.MpCosineParity);
            scored.Features[15] += 1e-9;
            Assert.IsFalse(Compute(entry, scored, window, null).MpCosineParity);
        }

        /// <summary>
        /// A spike at the apex on one non-core ion: the robust fit ignores it, so the ion's
        /// apex residual is about ln(6), its robust z is large, and its apex ratio about 6.
        /// </summary>
        private static void AssertInterferingSpike()
        {
            var entry = Peptide(SEQUENCE, 2, 1);
            int b4 = Slot(IonType.B, 4);
            var window = Window(entry, spikeSlot: b4);
            var record = Compute(entry, Row(entry, FIRST, LAST), window, null);
            Assert.AreEqual(Math.Log(6), record.PolishApexResidual[b4], 0.15);
            Assert.IsTrue(record.PolishOutlierZ[b4] > 20, @"the spike is an outlier against the core residual noise");
            Assert.IsTrue(record.PolishApexRatio[b4] > 4, @"observed well above the fit at the apex");
            Assert.IsTrue(record.PolishPosResidMax[b4] >= record.PolishApexResidual[b4]);
            // The core, and so the cosine, is untouched.
            Assert.IsTrue(record.MpCosine > 0.99);
            Assert.IsTrue(Math.Abs(record.PolishOutlierZ[Slot(IonType.Y, 7)]) < 6);
        }

        private static void AssertBoundaryValues()
        {
            var entry = Peptide(SEQUENCE, 2, 1);
            var window = Window(entry, spikeSlot: -1);
            var record = Compute(entry, Row(entry, FIRST, LAST), window, null);
            int y7 = Slot(IonType.Y, 7);
            Assert.AreEqual(Intensity(y7Index: 0, FIRST), record.XicStart[y7], 1e-3);
            Assert.AreEqual(Intensity(y7Index: 0, LAST), record.XicEnd[y7], 1e-3);
            Assert.AreEqual(Intensity(y7Index: 0, APEX), record.XicMax[y7], 1e-3);
            Assert.IsTrue(record.BoundaryStartRatioMedian > 0 && record.BoundaryStartRatioMedian < 0.5);
            Assert.IsTrue(record.BoundaryEndRatioMedian > 0 && record.BoundaryEndRatioMedian < 0.5);

            var withXics = Compute(entry, Row(entry, FIRST, LAST), window, null, writeXics: true);
            Assert.AreEqual(record.NPeakScans, withXics.XicRts.Length);
            Assert.AreEqual(record.NSlots * record.NPeakScans, withXics.XicIntensities.Length);
            Assert.AreEqual(record.XicStart[y7], withXics.XicIntensities[y7 * record.NPeakScans]);
            Assert.IsNull(record.XicRts, @"the matrix is written only when asked for");
        }

        /// <summary>
        /// GGGGDEK shares PEPTIDEK's y1-y3 (DEK) and peaks at the same scan with a better
        /// q-value: y3 is shared in both scopes and flagged. Another charge state of PEPTIDEK
        /// itself is not a competitor.
        /// </summary>
        private static void AssertSharedPeakClaimants()
        {
            var entry = Peptide(SEQUENCE, 2, 1);
            var window = Window(entry, spikeSlot: -1);
            var row = Row(entry, FIRST, LAST);
            var tolerance = Settings(false).SearchConfig.FragmentTolerance;
            var other = Peptide(@"GGGGDEK", 2, 2);
            var otherRow = Row(other, FIRST + 1, LAST - 1);
            var sameCharge3 = Peptide(SEQUENCE, 3, 3);
            var claimants = new List<TrainingClaimant>
            {
                new TrainingClaimant(entry, row, 0.005, 3.0, window, tolerance),
                new TrainingClaimant(other, otherRow, 0.001, 5.0, window, tolerance),
                new TrainingClaimant(sameCharge3, Row(sameCharge3, FIRST, LAST), 0.001, 5.0, window, tolerance),
            };
            var record = TrainingEvidence.Compute(entry, row, 0.005, 3.0, window, claimants, Settings(false));
            Assert.AreEqual(1, record.NCoelutingClaimants, @"itself and its own other charge state are not claimants");
            Assert.AreEqual(1, record.NSameApexClaimants);

            int y3 = Slot(IonType.Y, 3);
            Assert.AreEqual(1, record.SharedApexN[y3], @"same apex scan, same observed peak");
            Assert.AreEqual(1, record.SharedCoeluteN[y3], @"co-eluting, same ion m/z");
            Assert.AreNotEqual(0, record.IonFlags[y3] & TrainingIonFlags.BETTER_CLAIMANT_APEX);
            Assert.AreNotEqual(0, record.IonFlags[y3] & TrainingIonFlags.BETTER_CLAIMANT_COELUTE);
            Assert.AreEqual(0.001f, record.MinClaimantQ[y3], 1e-9);

            // y1 is shared by m/z but has no peak, so only the co-elution scope sees it.
            int y1 = Slot(IonType.Y, 1);
            Assert.AreEqual(0, record.SharedApexN[y1]);
            Assert.AreEqual(1, record.SharedCoeluteN[y1]);

            int y7 = Slot(IonType.Y, 7);
            Assert.AreEqual(0, record.SharedApexN[y7]);
            Assert.AreEqual(0, record.SharedCoeluteN[y7]);
            Assert.IsTrue(float.IsNaN(record.MinClaimantQ[y7]));
            Assert.AreEqual(0, record.IonFlags[y7] & (TrainingIonFlags.BETTER_CLAIMANT_APEX | TrainingIonFlags.BETTER_CLAIMANT_COELUTE));

            // Against a WORSE claimant the counts stand but the flags do not.
            var worse = new List<TrainingClaimant> { new TrainingClaimant(other, otherRow, 0.009, 1.0, window, tolerance) };
            var vsWorse = TrainingEvidence.Compute(entry, row, 0.005, 3.0, window, worse, Settings(false));
            Assert.AreEqual(1, vsWorse.SharedApexN[y3]);
            Assert.AreEqual(0, vsWorse.IonFlags[y3] & (TrainingIonFlags.BETTER_CLAIMANT_APEX | TrainingIonFlags.BETTER_CLAIMANT_COELUTE));
        }

        /// <summary>
        /// A one-scan peak has no fit - the scorer skips the polish under three scans - so the
        /// cosine is the scorer's 0 and the per-ion projections are absent.
        /// </summary>
        private static void AssertDegenerateShortPeak()
        {
            var entry = Peptide(SEQUENCE, 2, 1);
            var window = Window(entry, spikeSlot: -1);
            var row = Row(entry, APEX, APEX);
            row.Features[15] = 0.0;
            var record = Compute(entry, row, window, null);
            Assert.AreEqual(1, record.NPeakScans);
            Assert.IsFalse(record.MpFitted);
            Assert.AreEqual(0.0, record.MpCosine);
            Assert.IsTrue(record.MpCosineParity);
            Assert.IsTrue(double.IsNaN(record.MpResidualMad));
            int y7 = Slot(IonType.Y, 7);
            Assert.IsTrue(float.IsNaN(record.CorrPolish[y7]));
            Assert.IsTrue(float.IsNaN(record.PolishRowEffect[y7]));
            Assert.AreEqual(record.XicStart[y7], record.XicEnd[y7]);
            Assert.AreNotEqual(0, record.IonFlags[y7] & TrainingIonFlags.MATCHED_AT_APEX, @"the apex match does not need a fit");
        }

        private static void AssertSinglyChargedLadder()
        {
            var entry = Peptide(SEQUENCE, 1, 1);
            var window = Window(entry, spikeSlot: -1);
            var record = Compute(entry, Row(entry, FIRST, LAST), window, null);
            for (int slot = 0; slot < record.NSlots; slot++)
            {
                bool z2 = FragmentLadder.ChargeOf(slot) == 2;
                Assert.AreEqual(!z2, (record.IonFlags[slot] & TrainingIonFlags.APPLICABLE) != 0);
                if (z2)
                {
                    Assert.IsTrue(double.IsNaN(record.IonMz[slot]));
                    Assert.IsTrue(float.IsNaN(record.ApexIntensity[slot]));
                    Assert.AreEqual(0, record.NFiniteScans[slot]);
                }
            }
            Assert.AreEqual(record.NSlots / 2, record.NIonsApplicable);
        }

        private static void AssertDoubleCountingNeighbors()
        {
            var entry = Peptide(SEQUENCE, 2, 1);
            var twin = Peptide(SEQUENCE, 2, 7);  // same fragments, another id
            var unrelated = Peptide(@"GGGGAAAR", 2, 8);
            var neighbors = new List<(LibraryEntry Entry, double ApexRt)>
            {
                (entry, 10.50), (unrelated, 10.50), (twin, 10.51), (twin, 12.0),
            }.OrderBy(n => n.ApexRt).ToList();
            Assert.AreEqual(1, TrainingEvidence.CountDoubleCountingNeighbors(entry, 10.50, neighbors, 0.05, 0.02, ToleranceUnit.Mz),
                @"the twin within the neighborhood collides; itself, the unrelated entry and the far twin do not");
            Assert.AreEqual(0, TrainingEvidence.CountDoubleCountingNeighbors(entry, 10.50, neighbors, 0.001, 0.02, ToleranceUnit.Mz));
        }

        /// <summary>
        /// The collision test is not symmetric - it counts how many of the FIRST entry's top
        /// fragments the second one matches - and the dedup always passes the entry that comes
        /// first in its (apex, base_id, entry_id) order first. Here two of A's fragments match
        /// B's one close peak, which collides, while B's one peak alone does not, so B counts
        /// A as a neighbor only when A is asked first, as the dedup asks.
        /// </summary>
        private static void AssertDoubleCountingNeighborsTakeTheDedupsOrder()
        {
            var a = WithFragments(Peptide(SEQUENCE, 2, 1), 100.00, 100.01, 600.0, 700.0, 800.0, 900.0);
            var b = WithFragments(Peptide(@"GGGGAAAR", 2, 2), 100.005, 1000.0, 1100.0);
            Assert.IsTrue(ScoringPipeline.SharesDoubleCountingFragments(a.Fragments, b.Fragments, 0.02, ToleranceUnit.Mz));
            Assert.IsFalse(ScoringPipeline.SharesDoubleCountingFragments(b.Fragments, a.Fragments, 0.02, ToleranceUnit.Mz));
            foreach (double apexB in new[] { 10.51, 10.50 })
            {
                var neighbors = new List<(LibraryEntry Entry, double ApexRt)> { (a, 10.50), (b, apexB) };
                Assert.AreEqual(1, TrainingEvidence.CountDoubleCountingNeighbors(b, apexB, neighbors, 0.05, 0.02, ToleranceUnit.Mz),
                    @"A comes first in the dedup's order, so the pair is tested as (A, B)");
                Assert.AreEqual(1, TrainingEvidence.CountDoubleCountingNeighbors(a, 10.50, neighbors, 0.05, 0.02, ToleranceUnit.Mz));
            }
        }

        /// <summary>
        /// <c>protein_ids</c> is written the way the scores parquet writes it: the accessions
        /// joined with ';', empty for an entry with none, and NULL only when the library has no
        /// list at all.
        /// </summary>
        private static void AssertProteinIdsMatchTheScoresParquet()
        {
            var entry = Peptide(SEQUENCE, 2, 1);
            var window = Window(entry, spikeSlot: -1);
            var row = Row(entry, FIRST, LAST);
            entry.ProteinIds = new List<string> { @"P1", @"P2" };
            Assert.AreEqual(@"P1;P2", Compute(entry, row, window, null).ProteinIds);
            entry.ProteinIds = new List<string>();
            Assert.AreEqual(string.Empty, Compute(entry, row, window, null).ProteinIds);
            entry.ProteinIds = null;
            Assert.IsNull(Compute(entry, row, window, null).ProteinIds);
        }

        // ---- fixtures ------------------------------------------------------------------------

        /// <summary>The entry with its library spectrum replaced by these m/z, in falling intensity.</summary>
        private static LibraryEntry WithFragments(LibraryEntry entry, params double[] mzs)
        {
            entry.Fragments = mzs.Select((mz, i) => new LibraryFragment { Mz = mz, RelativeIntensity = 1f - 0.1f * i }).ToList();
            return entry;
        }

        private static TrainingRecord Compute(LibraryEntry entry, FdrEntry row, TrainingEvidenceWindow window,
            IReadOnlyList<TrainingClaimant> claimants, bool writeXics = false)
        {
            return TrainingEvidence.Compute(entry, row, 0.005, 3.0, window, claimants, Settings(writeXics));
        }

        private static TrainingEvidenceSettings Settings(bool writeXics)
        {
            return new TrainingEvidenceSettings
            {
                SearchConfig = new OspreyConfig
                {
                    FragmentTolerance = new FragmentToleranceConfig { Tolerance = 20, Unit = ToleranceUnit.Ppm },
                },
                Ms2ScanWindow = new[] { 200.0, 1500.0 },
                WriteXics = writeXics,
            };
        }

        private static int Slot(IonType type, int ordinal)
        {
            return FragmentLadder.SlotOf(new FragmentAnnotation { IonType = type, Ordinal = (byte)ordinal, Charge = 1 },
                SEQUENCE.Length);
        }

        private static LibraryEntry Peptide(string sequence, byte charge, uint id)
        {
            var ladder = FragmentLadder.Build(sequence, null, charge);
            var entry = new LibraryEntry(id, sequence, sequence, charge, 500.0, 10.5);
            var fragments = new List<LibraryFragment>();
            foreach (var f in LIBRARY)
            {
                var annotation = new FragmentAnnotation { IonType = f.Type, Ordinal = (byte)f.Ordinal, Charge = 1 };
                int slot = FragmentLadder.SlotOf(annotation, sequence.Length);
                if (slot < 0)
                    continue;
                fragments.Add(new LibraryFragment { Mz = ladder[slot], RelativeIntensity = f.Rel, Annotation = annotation });
            }
            entry.Fragments = fragments;
            return entry;
        }

        /// <summary>
        /// Eleven scans of one isolation window: every library ion of <paramref name="entry"/>
        /// elutes on a shared Gaussian with a few percent of deterministic noise, plus two
        /// unrelated peaks. <paramref name="spikeSlot"/>, when set, is multiplied six-fold at
        /// the apex.
        /// </summary>
        private static TrainingEvidenceWindow Window(LibraryEntry entry, int spikeSlot)
        {
            var ladder = FragmentLadder.Build(entry.Sequence, null, entry.Charge);
            var spectra = new List<Spectrum>();
            for (int s = 0; s <= 10; s++)
            {
                var peaks = new List<(double Mz, float Intensity)> { (150.5, 1000f), (1234.5, 2000f) };
                for (int f = 0; f < LIBRARY.Length; f++)
                {
                    int slot = FragmentLadder.SlotOf(new FragmentAnnotation
                        { IonType = LIBRARY[f].Type, Ordinal = (byte)LIBRARY[f].Ordinal, Charge = 1 }, entry.Sequence.Length);
                    float intensity = (float)(INTENSITY * LIBRARY[f].Rel * Profile(s) * Noise(f, s));
                    if (slot == spikeSlot && s == APEX)
                        intensity *= 6;
                    peaks.Add((ladder[slot], intensity));
                }
                peaks.Sort((a, b) => a.Mz.CompareTo(b.Mz)); // Array.Sort OK: distinct fixture m/z values, so no ties
                spectra.Add(new Spectrum
                {
                    ScanNumber = SCAN0 + (uint)s,
                    RetentionTime = RT0 + RT_STEP * s,
                    PrecursorMz = 500.0,
                    IsolationWindow = new IsolationWindow(500.0, 2.0, 2.0),
                    Mzs = peaks.Select(p => p.Mz).ToArray(),
                    Intensities = peaks.Select(p => p.Intensity).ToArray(),
                });
            }
            // Handed over out of order: the window sorts them as the scorer does.
            spectra.Reverse();
            return new TrainingEvidenceWindow(spectra);
        }

        /// <summary>
        /// The reconciled row for a peak over scans [first, last] with its apex at the scan of
        /// highest signal and a reference XIC of y7 over the peak. Its scored features are
        /// zero; <see cref="AssertCosineReproducesTheScorer"/> takes a row from the scorer.
        /// </summary>
        private static FdrEntry Row(LibraryEntry entry, int first, int last)
        {
            int apex = Math.Min(Math.Max(APEX, first), last);
            return new FdrEntry
            {
                EntryId = entry.Id,
                Charge = entry.Charge,
                ScanNumber = SCAN0 + (uint)apex,
                ApexRt = RT0 + RT_STEP * apex,
                StartRt = RT0 + RT_STEP * first,
                EndRt = RT0 + RT_STEP * last,
                Features = new double[21],
                ReferenceXicRts = Enumerable.Range(first, last - first + 1).Select(s => RT0 + RT_STEP * s).ToArray(),
                ReferenceXicIntensities = Enumerable.Range(first, last - first + 1).Select(s => Intensity(0, s)).ToArray(),
            };
        }

        private static double Intensity(int y7Index, int scan)
        {
            return (float)(INTENSITY * LIBRARY[y7Index].Rel * Profile(scan) * Noise(y7Index, scan));
        }

        private static double Profile(int scan)
        {
            double d = scan - APEX;
            return Math.Exp(-d * d / (2 * 1.2 * 1.2));
        }

        private static double Noise(int fragment, int scan)
        {
            return 1.0 + 0.03 * Math.Sin(7 * fragment + 3 * scan);
        }
    }
}
