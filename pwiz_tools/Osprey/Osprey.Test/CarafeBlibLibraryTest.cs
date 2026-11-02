/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
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
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CarafeSharp.Contracts;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;
using pwiz.Osprey.Tasks;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// A library CarafeSharp built is searched by Osprey from the .blib alone: no pairing
    /// manifest, no FASTA. <c>TestData\CarafeSubset.zip</c> (see its README.txt) holds the
    /// library CarafeSharp's regression subset builds and the pairing manifest that subset was
    /// built from, which is the oracle here: loaded with its own decoys, every entry's pairing
    /// id is the manifest's index for its sequence, every decoy is the paired decoy of a target
    /// at its charge, and the entrapment pairing read from the entries is the manifest's. This
    /// is the Carafe-to-Osprey handoff the <c>DecoyPairs</c> table exists for.
    /// </summary>
    [TestClass]
    public class CarafeBlibLibraryTest
    {
        private const string DATA_ZIP = @"CarafeSubset.zip";
        private const string LIBRARY_FILE = @"carafe_spectral_library.blib";
        private const string MANIFEST_FILE = @"library_subset_pairing.tsv";

        [TestMethod]
        public void TestCarafeBlibLoadsItsPairingWithoutManifest()
        {
            string dir = Path.Combine(Path.GetTempPath(), @"osprey-carafe-" + Guid.NewGuid().ToString(@"N"));
            ZipFile.ExtractToDirectory(Path.Combine(AppContext.BaseDirectory, @"TestData", DATA_ZIP), dir);
            try
            {
                var entries = LibraryLoader.Load(new OspreyConfig
                {
                    LibrarySource = LibrarySource.FromPath(Path.Combine(dir, LIBRARY_FILE)),
                    FragmentTolerance = FragmentToleranceConfig.UnitResolution(0.5),
                    DecoysInLibrary = true,
                }, null, null);
                var manifest = DecoyPairingManifest.FromTsv(Path.Combine(dir, MANIFEST_FILE));
                var manifestIndex = manifest.PairIndices().ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
                var manifestKind = manifest.Kinds().ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);

                var problems = new List<string>();
                // Every entry: a manifest peptide, on the manifest's side, in the manifest's pairing.
                foreach (var entry in entries)
                {
                    string label = string.Format(@"{0}/{1}", entry.ModifiedSequence, entry.Charge);
                    if (!manifestIndex.TryGetValue(entry.Sequence, out uint index))
                    {
                        problems.Add(label + @": not in the manifest");
                        continue;
                    }
                    if (entry.PairingId != index)
                        problems.Add(string.Format(@"{0}: pairing id {1}, the manifest's is {2}", label, entry.PairingId, index));
                    var kind = manifestKind[entry.Sequence];
                    bool decoyKind = kind == PeptideKind.Decoy || kind == PeptideKind.PDecoy;
                    if (entry.IsDecoy != decoyKind)
                        problems.Add(string.Format(@"{0}: {1} in the library, {2} in the manifest", label, entry.IsDecoy ? @"decoy" : @"target", kind));
                }
                // Every decoy is paired: its id is its target's with the decoy bit, at the target's
                // charge, in the target's pairing.
                var targetsById = entries.Where(e => !e.IsDecoy).ToDictionary(e => e.Id);
                int nDecoys = 0;
                foreach (var decoy in entries.Where(e => e.IsDecoy))
                {
                    nDecoys++;
                    string label = string.Format(@"{0}/{1}", decoy.ModifiedSequence, decoy.Charge);
                    if ((decoy.Id & LibraryEntry.DECOY_ID_BIT) == 0 || !targetsById.TryGetValue(decoy.Id & ~LibraryEntry.DECOY_ID_BIT, out var target))
                        problems.Add(label + @": not paired with a target");
                    else if (target.Charge != decoy.Charge || target.PairingId != decoy.PairingId)
                        problems.Add(string.Format(@"{0}: paired with {1}/{2} in pairing {3}", label, target.ModifiedSequence, target.Charge, target.PairingId));
                }
                // The entrapment pairing read from the entries is the manifest's: every target-side
                // sequence on its manifest index, entrapment peptides among them, nothing excluded.
                var pairing = EntrapmentPairing.Build(entries.ToDictionary(e => e.Id));
                var targetSequences = entries.Where(e => !e.IsDecoy).Select(e => e.Sequence).Distinct(StringComparer.Ordinal).ToList();
                foreach (string sequence in targetSequences)
                {
                    if (pairing.ExcludedEntrapment.Contains(sequence))
                        problems.Add(sequence + @": excluded from the entrapment pairing");
                    else if (!pairing.PairIndexBySeq.TryGetValue(sequence, out uint index))
                        problems.Add(sequence + @": not in the entrapment pairing");
                    else if (index != manifestIndex[sequence])
                        problems.Add(string.Format(@"{0}: entrapment pairing {1}, the manifest's is {2}", sequence, index, manifestIndex[sequence]));
                }
                Assert.AreEqual(0, problems.Count, string.Format(@"{0} problem(s) in {1} entries:{2}{3}", problems.Count, entries.Count,
                    Environment.NewLine, string.Join(Environment.NewLine, problems.Take(40))));

                // The subset exercises what was checked: decoys, and entrapment paired with targets.
                Assert.IsTrue(nDecoys > 0, @"no decoys");
                Assert.AreEqual(targetSequences.Count, pairing.PairIndexBySeq.Count);
                var originalIndices = new HashSet<uint>(targetSequences.Where(s => manifestKind[s] == PeptideKind.Target)
                    .Select(s => pairing.PairIndexBySeq[s]));
                int entrapmentPairs = targetSequences.Count(s => manifestKind[s] == PeptideKind.PTarget &&
                                                                originalIndices.Contains(pairing.PairIndexBySeq[s]));
                Assert.IsTrue(entrapmentPairs > 0, @"no entrapment peptide paired with a target");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }
    }
}
