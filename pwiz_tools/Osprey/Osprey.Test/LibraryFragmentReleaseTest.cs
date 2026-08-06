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
            ValidateOnlyUnscorableFragmentsAreReleased();
            ValidateIdentityFieldsSurvive();
            ValidateValidityKeySuffix();
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

            // A null gap-fill plan is legal (planning skipped) and must not throw.
            var survivorsOnly = LibraryFragmentRelease.BuildRetainedBaseIds(survivors, null);
            Assert.AreEqual(1, survivorsOnly.Count);
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
        /// EMPTY on the default arm so no existing output directory is invalidated; non-empty on
        /// the resident opt-out so an in-place A/B cannot adopt the other arm's outputs and
        /// report a memory saving it never computed.
        /// </summary>
        private static void ValidateValidityKeySuffix()
        {
            bool saved = OspreyEnvironment.ReleaseLibraryFragments;
            try
            {
                OspreyEnvironment.ReleaseLibraryFragments = true;
                Assert.AreEqual(string.Empty,
                    OspreyEnvironment.ReleaseLibraryFragmentsValidityKeySuffix());

                OspreyEnvironment.ReleaseLibraryFragments = false;
                Assert.AreNotEqual(string.Empty,
                    OspreyEnvironment.ReleaseLibraryFragmentsValidityKeySuffix());
            }
            finally
            {
                OspreyEnvironment.ReleaseLibraryFragments = saved;
            }
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
