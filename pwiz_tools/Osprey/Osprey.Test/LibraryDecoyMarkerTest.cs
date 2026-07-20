/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.7) <noreply .at. anthropic.com>
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

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Cross-impl tests for library-supplied decoy detection and marking.
    /// Ports the 7 Rust unit tests from
    /// <c>osprey-core/src/types.rs</c> at maccoss/osprey commit
    /// <c>6630ab1</c> (Added support for library-supplied decoys via
    /// prefix detection).
    /// </summary>
    [TestClass]
    public class LibraryDecoyMarkerTest
    {
        private static LibraryEntry MakeLibEntry(uint id, params string[] proteinIds)
        {
            var e = new LibraryEntry(id, @"PEPTIDE", @"PEPTIDE", 2, 500.0, 10.0);
            e.ProteinIds = proteinIds;
            return e;
        }

        [TestMethod]
        public void LooksLikeLibraryDecoyMatchesProteinPrefix()
        {
            var e = MakeLibEntry(1, @"DECOY_P12345", @"P67890");
            Assert.IsTrue(e.LooksLikeLibraryDecoy(new List<string> { @"DECOY_" }));
        }

        [TestMethod]
        public void LooksLikeLibraryDecoyIsCaseInsensitive()
        {
            var e = MakeLibEntry(1, @"Rev_P12345");
            Assert.IsTrue(e.LooksLikeLibraryDecoy(new List<string> { @"rev_" }));
            var e2 = MakeLibEntry(2, @"DECOY_P1");
            Assert.IsTrue(e2.LooksLikeLibraryDecoy(new List<string> { @"decoy_" }));
        }

        [TestMethod]
        public void LooksLikeLibraryDecoyRequiresPrefixPosition()
        {
            // A protein with `rev_` in the middle should NOT match -- it's a prefix test.
            var e = MakeLibEntry(1, @"P12345_rev_suffix");
            Assert.IsFalse(e.LooksLikeLibraryDecoy(new List<string> { @"rev_" }));
        }

        [TestMethod]
        public void LooksLikeLibraryDecoyEmptyInputsReturnFalse()
        {
            var e = MakeLibEntry(1, @"P12345");
            Assert.IsFalse(e.LooksLikeLibraryDecoy(new List<string>()));
            var empty = MakeLibEntry(2);
            Assert.IsFalse(empty.LooksLikeLibraryDecoy(new List<string> { @"rev_" }));
        }

        [TestMethod]
        public void LooksLikeLibraryDecoyMatchesAnyProtein()
        {
            // A shared peptide: one accession is a target, the other a decoy.
            // Any-match policy means this is treated as a decoy.
            var e = MakeLibEntry(1, @"P12345", @"DECOY_P12345");
            Assert.IsTrue(e.LooksLikeLibraryDecoy(new List<string> { @"DECOY_" }));
        }

        [TestMethod]
        public void ApplyLibraryDecoyMarkingSetsFlagAndHighBit()
        {
            var lib = new List<LibraryEntry>
            {
                MakeLibEntry(1, @"P12345"),       // target
                MakeLibEntry(2, @"DECOY_P12345"), // decoy
                MakeLibEntry(3, @"rev_P67890"),   // decoy via rev_
                MakeLibEntry(4, @"P67890"),       // target
            };
            var prefixes = new List<string> { @"DECOY_", @"rev_" };
            LibraryDecoyMarker.ApplyLibraryDecoyMarking(lib, prefixes, out var stats);
            Assert.AreEqual(2, stats.NMarked);
            Assert.IsFalse(lib[0].IsDecoy);
            Assert.IsTrue(lib[1].IsDecoy);
            Assert.IsTrue(lib[2].IsDecoy);
            Assert.IsFalse(lib[3].IsDecoy);
            // Decoy IDs have the high bit set; targets do not.
            Assert.AreEqual(0u, lib[0].Id & LibraryEntry.DECOY_ID_BIT);
            Assert.IsTrue((lib[1].Id & LibraryEntry.DECOY_ID_BIT) != 0u);
            Assert.IsTrue((lib[2].Id & LibraryEntry.DECOY_ID_BIT) != 0u);
            Assert.AreEqual(0u, lib[3].Id & LibraryEntry.DECOY_ID_BIT);
            // base_id (low 31 bits) is preserved.
            Assert.AreEqual(2u, lib[1].Id & 0x7FFFFFFFu);
            Assert.AreEqual(3u, lib[2].Id & 0x7FFFFFFFu);
        }

        [TestMethod]
        public void ApplyLibraryDecoyMarkingIsIdempotent()
        {
            var lib = new List<LibraryEntry> { MakeLibEntry(1, @"DECOY_P12345") };
            var prefixes = new List<string> { @"DECOY_" };
            LibraryDecoyMarker.ApplyLibraryDecoyMarking(lib, prefixes, out var stats1);
            uint idAfterFirst = lib[0].Id;
            LibraryDecoyMarker.ApplyLibraryDecoyMarking(lib, prefixes, out var stats2);
            Assert.AreEqual(1, stats1.NViaPrefix);
            Assert.AreEqual(0, stats1.NViaColumn);
            // Second pass: nothing new by prefix; high bit already set so
            // no column-canonicalisation needed either.
            Assert.AreEqual(0, stats2.NMarked);
            Assert.AreEqual(idAfterFirst, lib[0].Id); // no double-OR of high bit
        }

        [TestMethod]
        public void ApplyLibraryDecoyMarkingCanonicalisesLoaderFlaggedDecoys()
        {
            // Simulate: the DIA-NN loader read a Decoy=1 column and set
            // IsDecoy = true, but did NOT set DECOY_ID_BIT on the Id.
            // The marking pass should fix that.
            var lib = new List<LibraryEntry>
            {
                MakeLibEntry(7, @"SomeProtein"),     // target (untouched by loader)
                MakeLibEntry(8, @"SomeOtherProtein"), // loader-flagged below
            };
            lib[1].IsDecoy = true;
            LibraryDecoyMarker.ApplyLibraryDecoyMarking(
                lib, new List<string> { @"DECOY_" }, out var stats);
            // The loader-flagged entry got its high bit set; the prefix
            // scan contributed nothing.
            Assert.AreEqual(1, stats.NViaColumn);
            Assert.AreEqual(0, stats.NViaPrefix);
            Assert.AreEqual(0u, lib[0].Id & LibraryEntry.DECOY_ID_BIT);
            Assert.IsTrue((lib[1].Id & LibraryEntry.DECOY_ID_BIT) != 0u);
            Assert.AreEqual(8u, lib[1].Id & 0x7FFFFFFFu);
        }
    }
}
