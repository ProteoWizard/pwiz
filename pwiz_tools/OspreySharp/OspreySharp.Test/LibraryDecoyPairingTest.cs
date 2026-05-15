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
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.Test
{
    /// <summary>
    /// Cross-impl tests for composition-based library-decoy pairing.
    /// Ports the 7 Rust composition-pairing unit tests from
    /// <c>osprey-core/src/types.rs</c> at maccoss/osprey commit
    /// <c>a8ae4a1</c> (Added target-decoy pairing for library-supplied
    /// decoys).
    /// </summary>
    [TestClass]
    public class LibraryDecoyPairingTest
    {
        private static LibraryEntry MakePairedEntry(uint id, string sequence,
            byte charge, string[] proteinIds, bool isDecoy)
        {
            var e = new LibraryEntry(id, sequence, sequence, charge, 500.0, 10.0);
            foreach (var p in proteinIds)
                e.ProteinIds.Add(p);
            e.IsDecoy = isDecoy;
            return e;
        }

        [TestMethod]
        public void PairByCompositionSimplePermutation()
        {
            // Target PEPK in protein P1; decoy KPEP (permutation) in DECOY_P1.
            // Decoy already has IsDecoy=true and DECOY_ID_BIT set from marking pass.
            var lib = new List<LibraryEntry>
            {
                MakePairedEntry(1, @"PEPK", 2, new[] { @"P1" }, false),
                MakePairedEntry(2u | LibraryEntry.DECOY_ID_BIT, @"KPEP", 2,
                    new[] { @"DECOY_P1" }, true),
            };
            var stats = LibraryDecoyPairing.PairLibraryDecoysByComposition(
                lib, new List<string> { @"DECOY_" });
            Assert.AreEqual(1, stats.NPaired);
            Assert.AreEqual(0, stats.NUnpairedDecoys);
            Assert.AreEqual(0, stats.NUnpairedTargets);
            // Decoy's base_id should now match the target's id.
            Assert.AreEqual(lib[0].Id, lib[1].Id & 0x7FFFFFFFu);
            Assert.IsTrue((lib[1].Id & LibraryEntry.DECOY_ID_BIT) != 0u);
        }

        [TestMethod]
        public void PairByCompositionRequiresMatchingProtein()
        {
            // Same AA composition but different proteins -> no pairing.
            var lib = new List<LibraryEntry>
            {
                MakePairedEntry(1, @"PEPK", 2, new[] { @"P1" }, false),
                MakePairedEntry(2u | LibraryEntry.DECOY_ID_BIT, @"KPEP", 2,
                    new[] { @"DECOY_P2" }, true),
            };
            var stats = LibraryDecoyPairing.PairLibraryDecoysByComposition(
                lib, new List<string> { @"DECOY_" });
            Assert.AreEqual(0, stats.NPaired);
            Assert.AreEqual(1, stats.NUnpairedDecoys);
        }

        [TestMethod]
        public void PairByCompositionRequiresMatchingCharge()
        {
            // Same protein, same composition, different charge -> no pairing.
            var lib = new List<LibraryEntry>
            {
                MakePairedEntry(1, @"PEPK", 2, new[] { @"P1" }, false),
                MakePairedEntry(2u | LibraryEntry.DECOY_ID_BIT, @"KPEP", 3,
                    new[] { @"DECOY_P1" }, true),
            };
            var stats = LibraryDecoyPairing.PairLibraryDecoysByComposition(
                lib, new List<string> { @"DECOY_" });
            Assert.AreEqual(0, stats.NPaired);
        }

        [TestMethod]
        public void PairByCompositionOneToOneWithinCompositionGroup()
        {
            // Two target peptides in the same protein with the same AA
            // composition and two decoys with the same composition. Each
            // decoy must claim a distinct target (1:1).
            var lib = new List<LibraryEntry>
            {
                MakePairedEntry(1, @"PEPK", 2, new[] { @"P1" }, false),
                MakePairedEntry(2, @"EPKP", 2, new[] { @"P1" }, false),
                MakePairedEntry(3u | LibraryEntry.DECOY_ID_BIT, @"KPEP", 2,
                    new[] { @"DECOY_P1" }, true),
                MakePairedEntry(4u | LibraryEntry.DECOY_ID_BIT, @"PKPE", 2,
                    new[] { @"DECOY_P1" }, true),
            };
            var stats = LibraryDecoyPairing.PairLibraryDecoysByComposition(
                lib, new List<string> { @"DECOY_" });
            Assert.AreEqual(2, stats.NPaired);
            Assert.AreEqual(0, stats.NUnpairedDecoys);
            Assert.AreEqual(0, stats.NUnpairedTargets);
            uint d3Base = lib[2].Id & 0x7FFFFFFFu;
            uint d4Base = lib[3].Id & 0x7FFFFFFFu;
            Assert.AreNotEqual(d3Base, d4Base);
            Assert.IsTrue(d3Base == 1u || d3Base == 2u);
            Assert.IsTrue(d4Base == 1u || d4Base == 2u);
        }

        [TestMethod]
        public void PairByCompositionDeterministicAcrossInputOrder()
        {
            // Two compositionally-identical pairs; the pairing assignment
            // must not depend on the order entries appear in the library
            // vector.
            var prefixes = new List<string> { @"DECOY_" };

            List<LibraryEntry> libA = new List<LibraryEntry>
            {
                MakePairedEntry(1, @"PEPK", 2, new[] { @"P1" }, false),
                MakePairedEntry(2, @"EPKP", 2, new[] { @"P1" }, false),
                MakePairedEntry(3u | LibraryEntry.DECOY_ID_BIT, @"KPEP", 2,
                    new[] { @"DECOY_P1" }, true),
                MakePairedEntry(4u | LibraryEntry.DECOY_ID_BIT, @"PKPE", 2,
                    new[] { @"DECOY_P1" }, true),
            };
            List<LibraryEntry> libB = new List<LibraryEntry>
            {
                MakePairedEntry(4u | LibraryEntry.DECOY_ID_BIT, @"PKPE", 2,
                    new[] { @"DECOY_P1" }, true),
                MakePairedEntry(2, @"EPKP", 2, new[] { @"P1" }, false),
                MakePairedEntry(3u | LibraryEntry.DECOY_ID_BIT, @"KPEP", 2,
                    new[] { @"DECOY_P1" }, true),
                MakePairedEntry(1, @"PEPK", 2, new[] { @"P1" }, false),
            };
            LibraryDecoyPairing.PairLibraryDecoysByComposition(libA, prefixes);
            LibraryDecoyPairing.PairLibraryDecoysByComposition(libB, prefixes);

            Assert.AreEqual(DecoyBaseId(libA, @"KPEP"), DecoyBaseId(libB, @"KPEP"));
            Assert.AreEqual(DecoyBaseId(libA, @"PKPE"), DecoyBaseId(libB, @"PKPE"));
        }

        [TestMethod]
        public void PairByCompositionSharedPeptidePicksAvailableTarget()
        {
            // A decoy on a shared peptide with two possible targets in
            // different proteins. The decoy should claim whichever target
            // is reachable through any of its accessions.
            var lib = new List<LibraryEntry>
            {
                MakePairedEntry(1, @"PEPK", 2, new[] { @"P1", @"P2" }, false),
                MakePairedEntry(2u | LibraryEntry.DECOY_ID_BIT, @"KPEP", 2,
                    new[] { @"DECOY_P2" }, true),
            };
            var stats = LibraryDecoyPairing.PairLibraryDecoysByComposition(
                lib, new List<string> { @"DECOY_" });
            Assert.AreEqual(1, stats.NPaired);
            Assert.AreEqual(1u, lib[1].Id & 0x7FFFFFFFu);
        }

        [TestMethod]
        public void PairByCompositionReportsUnpairedCounts()
        {
            // 2 targets, 1 decoy pairs, 1 doesn't. 1 unpaired decoy + 1
            // unpaired target.
            var lib = new List<LibraryEntry>
            {
                MakePairedEntry(1, @"PEPK", 2, new[] { @"P1" }, false), // paired
                MakePairedEntry(2, @"AAAR", 2, new[] { @"P2" }, false), // unpaired target
                MakePairedEntry(3u | LibraryEntry.DECOY_ID_BIT, @"KPEP", 2,
                    new[] { @"DECOY_P1" }, true), // pairs to id=1
                MakePairedEntry(4u | LibraryEntry.DECOY_ID_BIT, @"XYZK", 2,
                    new[] { @"DECOY_P9" }, true), // no target
            };
            var stats = LibraryDecoyPairing.PairLibraryDecoysByComposition(
                lib, new List<string> { @"DECOY_" });
            Assert.AreEqual(2, stats.NTargets);
            Assert.AreEqual(2, stats.NDecoys);
            Assert.AreEqual(1, stats.NPaired);
            Assert.AreEqual(1, stats.NUnpairedDecoys);
            Assert.AreEqual(1, stats.NUnpairedTargets);
            Assert.AreEqual(0.5, stats.PairedFraction, 1e-9);
        }

        [TestMethod]
        public void PairByCompositionNoDecoysIsFullPairFraction()
        {
            // No decoys -> PairedFraction returns 1.0 (no failure to report).
            var lib = new List<LibraryEntry>
            {
                MakePairedEntry(1, @"PEPK", 2, new[] { @"P1" }, false),
            };
            var stats = LibraryDecoyPairing.PairLibraryDecoysByComposition(
                lib, new List<string> { @"DECOY_" });
            Assert.AreEqual(0, stats.NDecoys);
            Assert.AreEqual(1.0, stats.PairedFraction);
        }

        private static uint DecoyBaseId(IList<LibraryEntry> lib, string sequence)
        {
            foreach (var e in lib)
            {
                if (e.IsDecoy && e.Sequence == sequence)
                    return e.Id & 0x7FFFFFFFu;
            }
            Assert.Fail(@"Decoy with sequence {0} not found", sequence);
            return 0u;
        }
    }
}
