/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR.Reconciliation;
using pwiz.Osprey.Tasks;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Tests the Stage 5 -&gt; 6 library-fragment release (issue #4532): survivors and gap-fill
    /// candidates keep their spectra, everything else loses them, paired decoys ride along on
    /// the base_id, and identity fields survive on every entry.
    /// </summary>
    [TestClass]
    public class LibraryFragmentReleaseTest
    {
        private const uint DECOY_BIT = 0x80000000;

        [TestMethod]
        public void TestLibraryFragmentRelease()
        {
            ValidateGapFillCandidatesAreRetained();
            ValidateReportedPoolIsRetainedOnSecondPassFdr();
            ValidateOnlyUnscorableFragmentsAreReleased();
            ValidateIdentityFieldsSurvive();
            ValidateEveryLegThatHoldsTheLibraryReleasesIt();
            ValidateValidityKeySuffixTracksWhetherTheReleaseRan();
        }

        /// <summary>
        /// Gap-fill resolves the MISSING charge states of passing peptides, so it names entries
        /// that did NOT survive compaction - and Stage 6 scores them. If the retained set were
        /// survivors alone it would strip exactly the spectra gap-fill is about to ask for,
        /// which is the defect this assertion exists to catch.
        /// </summary>
        private static void ValidateGapFillCandidatesAreRetained()
        {
            var survivors = new HashSet<uint> { 10u };
            var gapFill = new Dictionary<string, List<GapFillTarget>>
            {
                { @"fileA", new List<GapFillTarget> { new GapFillTarget { TargetEntryId = 20u, DecoyEntryId = 20u | DECOY_BIT } } }
            };

            var retained = LibraryFragmentRelease.BuildRetainedBaseIds(survivors, gapFill);

            Assert.IsTrue(retained.Contains(10u), @"survivor base_id must be retained");
            Assert.IsTrue(retained.Contains(20u), @"gap-fill base_id must be retained");
            Assert.AreEqual(2, retained.Count);

            // A null gap-fill plan must not throw. NOT because "planning was skipped" - that
            // leaves the non-null empty initializer - but because the bundle-adopt path sources
            // it from the reconciliation envelope, which can carry nothing.
            var survivorsOnly = LibraryFragmentRelease.BuildRetainedBaseIds(survivors, null);
            Assert.AreEqual(1, survivorsOnly.Count);
        }

        /// <summary>
        /// SecondPassFDR has no survivors + gap-fill pair to work from - FirstPassFDR is excluded
        /// from a --task SecondPassFDR pipeline - so it retains every base_id in the final
        /// reported pool instead. Decoys included: a decoy row must retain its base_id rather
        /// than being skipped, or a decoy whose paired target did not survive would have its
        /// spectrum pulled out from under the pool it is still in.
        /// </summary>
        private static void ValidateReportedPoolIsRetainedOnSecondPassFdr()
        {
            var perFileEntries = new List<KeyValuePair<string, List<FdrEntry>>>
            {
                new KeyValuePair<string, List<FdrEntry>>(@"fileA", new List<FdrEntry>
                {
                    new FdrEntry { EntryId = 10u },
                    new FdrEntry { EntryId = 10u | DECOY_BIT }
                }),
                new KeyValuePair<string, List<FdrEntry>>(@"fileB", new List<FdrEntry>
                {
                    new FdrEntry { EntryId = 10u },
                    new FdrEntry { EntryId = 40u | DECOY_BIT }
                })
            };

            var retained = LibraryFragmentRelease.BuildRetainedBaseIds(perFileEntries);

            Assert.AreEqual(2, retained.Count, @"the decoy bit must be masked off, not counted twice");
            Assert.IsTrue(retained.Contains(10u));
            Assert.IsTrue(retained.Contains(40u), @"a decoy-only row still retains its base_id");
        }

        /// <summary>
        /// Entries outside the retained set lose Fragments; retained targets AND their paired
        /// decoys keep them, because the base_id is shared and the survivor set is
        /// pair-symmetric.
        /// </summary>
        private static void ValidateOnlyUnscorableFragmentsAreReleased()
        {
            var library = new List<LibraryEntry>
            {
                MakeEntry(10u),               // survivor target
                MakeEntry(10u | DECOY_BIT),   // its paired decoy - same base_id
                MakeEntry(20u),               // gap-fill target
                MakeEntry(30u),               // neither: released
                MakeEntry(30u | DECOY_BIT)    // its decoy: released too
            };
            var retained = new HashSet<uint> { 10u, 20u };

            int released = LibraryFragmentRelease.ReleaseFragments(library, retained);

            Assert.AreEqual(2, released);
            AssertRetained(library[0], @"survivor");
            AssertRetained(library[1], @"paired decoy of a survivor");
            AssertRetained(library[2], @"gap-fill candidate");
            AssertReleased(library[3]);
            AssertReleased(library[4]);

            // Idempotent: a second pass finds nothing left to release rather than double-counting.
            Assert.AreEqual(0, LibraryFragmentRelease.ReleaseFragments(library, retained));
        }

        /// <summary>
        /// The identity fields must survive on RELEASED entries too. ProteinFdr's parsimony and
        /// the protein-compact stratum walk the entire library after this point and read
        /// ModifiedSequence / ProteinIds, including for entries already judged false - so
        /// dropping whole entries would silently move protein FDR.
        /// </summary>
        private static void ValidateIdentityFieldsSurvive()
        {
            var library = new List<LibraryEntry> { MakeEntry(99u) };
            LibraryFragmentRelease.ReleaseFragments(library, new HashSet<uint>());

            AssertReleased(library[0]);
            Assert.AreEqual(@"PEPTIDER", library[0].ModifiedSequence);
            Assert.IsNotNull(library[0].ProteinIds);
            Assert.AreEqual(1, library[0].ProteinIds.Count);
            Assert.AreEqual(500.25, library[0].PrecursorMz, 1e-12);
        }

        /// <summary>
        /// The leg truth table. Two entries carry the weight.
        ///
        /// <para><c>--task SecondPassFDR</c> MUST release. That process holds the whole fragment
        /// set through second-pass Percolator, protein FDR and the blib write, and it is the one
        /// place a distributed run can free it - <c>FirstPassFdrTask</c>, where the Stage 5 -&gt; 6
        /// release lives, is excluded from that leg's pipeline entirely. It realized zero saving
        /// until SecondPassFDR grew its own release, so this row is the regression guard for
        /// the distributed path, which is where memory hurts most.</para>
        ///
        /// <para><c>--task FirstPassFDR</c> MUST NOT. Its gap-fill plan is unpopulated (the
        /// retained set would be survivors only, stripping the very entries gap-fill is about to
        /// score) and it already loads with <c>OmitFragments</c>, so a release there would
        /// report millions of entries freed having freed nothing - a fabricated saving logged
        /// directly above a [MEM] probe.</para>
        /// </summary>
        private static void ValidateEveryLegThatHoldsTheLibraryReleasesIt()
        {
            AssertRunsOnLeg(true, @"straight-through", new OspreyConfig());
            AssertRunsOnLeg(true, @"--task SecondPassFDR",
                WithInputScores(c => c.ExpectReconciledInput = true));
            AssertRunsOnLeg(true, @"--input-scores full pipeline", WithInputScores(_ => { }));
            AssertRunsOnLeg(false, @"--task FirstPassFDR",
                WithInputScores(c => c.StopAfterStage5 = true));

            // --fdrbench-pass 1 forces the RESIDENT first-pass pool, which never computes a
            // surviving base_id set, so there is nothing to release against.
            AssertRunsOnLeg(false, @"--fdrbench-pass 1", new OspreyConfig
            {
                OutputFdrBench = @"bench.tsv",
                FdrBenchPass = OspreyConfig.FDRBENCH_PASS_1
            });

            bool savedProjection = OspreyEnvironment.UseFdrProjection;
            bool savedRelease = OspreyEnvironment.ReleaseLibraryFragments;
            try
            {
                OspreyEnvironment.UseFdrProjection = false;
                AssertRunsOnLeg(false, @"OSPREY_FDR_PROJECTION=0", new OspreyConfig());
                OspreyEnvironment.UseFdrProjection = savedProjection;

                OspreyEnvironment.ReleaseLibraryFragments = false;
                AssertRunsOnLeg(false, @"OSPREY_RELEASE_LIBRARY_FRAGMENTS=0", new OspreyConfig());
            }
            finally
            {
                OspreyEnvironment.UseFdrProjection = savedProjection;
                OspreyEnvironment.ReleaseLibraryFragments = savedRelease;
            }
        }

        /// <summary>
        /// The suffix records whether the release RAN, not whether the flag was set. Keying it
        /// on the flag alone left the guarantee resting one gate upstream: a leg that never
        /// released (Stage 5 forced onto the resident path, say) stamped the same EMPTY suffix
        /// as a leg that did, so a later run WITH the release could adopt those outputs, skip
        /// the work, and report the release arm's memory profile without ever executing it.
        ///
        /// <para>It is EMPTY on a leg that could not have released either. There the two arms
        /// are literally the same run, so a term would invalidate output directories - forcing
        /// hours of re-scoring on an HPC resume - to record a difference that cannot exist.</para>
        /// </summary>
        private static void ValidateValidityKeySuffixTracksWhetherTheReleaseRan()
        {
            bool savedProjection = OspreyEnvironment.UseFdrProjection;
            bool savedRelease = OspreyEnvironment.ReleaseLibraryFragments;
            try
            {
                AssertSuffix(true, @"straight-through, released", new OspreyConfig());
                AssertSuffix(true, @"--task SecondPassFDR, released",
                    WithInputScores(c => c.ExpectReconciledInput = true));
                AssertSuffix(true, @"--task FirstPassFDR cannot release",
                    WithInputScores(c => c.StopAfterStage5 = true));

                OspreyEnvironment.UseFdrProjection = false;
                AssertSuffix(false, @"could have released, Stage 5 went resident instead",
                    new OspreyConfig());
                // SecondPassFDR's release is its own and does not ride the Stage 5 path.
                AssertSuffix(true, @"--task SecondPassFDR ignores OSPREY_FDR_PROJECTION",
                    WithInputScores(c => c.ExpectReconciledInput = true));
                OspreyEnvironment.UseFdrProjection = savedProjection;

                OspreyEnvironment.ReleaseLibraryFragments = false;
                AssertSuffix(false, @"opted out where a release was possible", new OspreyConfig());
                AssertSuffix(true, @"opted out where it was not possible anyway",
                    WithInputScores(c => c.StopAfterStage5 = true));
            }
            finally
            {
                OspreyEnvironment.UseFdrProjection = savedProjection;
                OspreyEnvironment.ReleaseLibraryFragments = savedRelease;
            }
        }

        private static void AssertRunsOnLeg(bool expected, string leg, OspreyConfig config)
        {
            Assert.AreEqual(expected, LibraryFragmentRelease.RunsOnThisLeg(MakeContext(config)),
                string.Format(@"{0}: the release must {1}run on this leg",
                    leg, expected ? string.Empty : @"NOT "));
        }

        private static void AssertSuffix(bool expectEmpty, string leg, OspreyConfig config)
        {
            string suffix = LibraryFragmentRelease.ValidityKeySuffix(MakeContext(config));
            Assert.AreEqual(expectEmpty, suffix.Length == 0, string.Format(
                @"{0}: expected {1} suffix, got '{2}'",
                leg, expectEmpty ? @"an empty" : @"a non-empty", suffix));
        }

        private static PipelineContext MakeContext(OspreyConfig config)
        {
            return new PipelineContext(config, AnalysisPipeline.CanonicalPipeline(), null, null, null);
        }

        private static OspreyConfig WithInputScores(Action<OspreyConfig> set)
        {
            var config = new OspreyConfig { InputScores = new List<string> { @"a.scores.parquet" } };
            set(config);
            return config;
        }

        /// <summary>
        /// A retained entry's spectrum must still be READABLE. Asserting only non-null would
        /// pass on a released entry too - the released state is deliberately non-null - so the
        /// assertion has to read through it. That also makes this the released-check in
        /// reverse: reading a wrongly-released entry throws here rather than passing quietly.
        /// </summary>
        private static void AssertRetained(LibraryEntry e, string what)
        {
            Assert.IsFalse(e.IsSpectrumReleased, what + @" must not be released");
            Assert.AreEqual(1, e.Fragments.Count, what + @" must keep a readable spectrum");
            Assert.AreEqual(300.1, e.Fragments[0].Mz, 1e-12);
        }

        /// <summary>
        /// A released entry's Fragments must be NON-NULL and must THROW on access. Both halves
        /// matter: every scorer guards with
        /// <c>entry.Fragments == null || entry.Fragments.Count == 0</c>, so a null (or an empty
        /// list) would be silently absorbed as "no spectrum" and score a degenerate zero. The
        /// non-null sentinel makes that same guard expression throw instead.
        /// </summary>
        private static void AssertReleased(LibraryEntry e)
        {
            Assert.IsTrue(e.IsSpectrumReleased);
            Assert.IsNotNull(e.Fragments, @"a null would be silently absorbed by every scorer guard");
            Assert.ThrowsException<InvalidOperationException>(() => _ = e.Fragments.Count);
            Assert.ThrowsException<InvalidOperationException>(() => _ = e.Fragments[0]);
            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                foreach (var unused in e.Fragments)
                    break;
            });
        }

        private static LibraryEntry MakeEntry(uint id)
        {
            return new LibraryEntry(id, @"PEPTIDER", @"PEPTIDER", 2, 500.25, 10.0)
            {
                ProteinIds = new List<string> { @"P12345" },
                Fragments = new List<LibraryFragment>
                {
                    new LibraryFragment { Mz = 300.1, RelativeIntensity = 1.0f }
                }
            };
        }
    }
}
