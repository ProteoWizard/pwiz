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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;
using pwiz.Osprey.Tasks;

namespace pwiz.Osprey.Test
{
    public partial class SubsetPipelineTest
    {
        /// <summary>
        /// A library is the same object in memory whichever file it came from: the subset .tsv
        /// loaded directly, and loaded back from its .blib export, are compared entry by entry
        /// and field by field - identity, modifications, every fragment's m/z, intensity and
        /// annotation, proteins - and every difference is listed. The library-decoy subset goes
        /// through <see cref="LibraryLoader"/> as well: the .tsv with its pairing manifest,
        /// and its export loaded with no manifest at all, must agree on every entry's pairing id
        /// and on the entrapment pairing built from them. The regression's
        /// <c>-LibraryAsBlib</c> legs prove the same thing at the search's output, forty
        /// minutes later; this is the seconds-long form of that gate, and the one to run first
        /// when a loader changes. The derived library the loader keeps of a .tsv is the same
        /// round trip made by the loader itself (<see cref="DerivedLibraryDifferences"/>), its
        /// lean loads included.
        /// </summary>
        [TestMethod]
        public void TestLibraryLoadsIdenticallyFromTsvAndBlib()
        {
            var report = new List<string>();
            report.AddRange(RoundTripDifferences(_dataDir, LIBRARY_FILE, FragmentToleranceConfig.UnitResolution(0.5)));
            report.AddRange(RoundTripDifferences(ExtractAstral(), ASTRAL_LIBRARY_FILE, FragmentToleranceConfig.Hram(10)));
            report.AddRange(LoaderRoundTripDifferences());
            report.AddRange(DerivedLibraryDifferences());
            // The same library with its modifications printed as masses, as DIA-NN prints them:
            // identified by residue and mass, so the entries read the same as from the UniMod text.
            var twoMasses = DiannTsvLoader.ParseModifications(@"KDC[+57.021464]PWPC[+57.021464]PK");
            CollectionAssert.AreEqual(new[] { 2, 6 }, twoMasses.Select(m => m.Position).ToArray());
            CollectionAssert.AreEqual(new int?[] { 4, 4 }, twoMasses.Select(m => m.UnimodId).ToArray());
            // The flanking-residue notation is one residue and a dot at each end, and the dots of
            // mass brackets are not it: the text between the first and last dot once cut a peptide
            // with two mass brackets down to what lay between the two masses.
            Assert.AreEqual(@"PEPTIDE", DiannTsvLoader.StripFlankingChars(@"K.PEPTIDE.R"));
            Assert.AreEqual(@"KDC[+57.021464]PWPC[+57.021464]PK", DiannTsvLoader.StripFlankingChars(@"_KDC[+57.021464]PWPC[+57.021464]PK_"));
            Assert.AreEqual(@"C[+57.021464]PK", DiannTsvLoader.StripFlankingChars(@"R.C[+57.021464]PK.A"));
            string massNotation = WriteLibraryCopy(@"mass-notation-library.tsv", @"ModifiedPeptide", (fields, column) =>
            {
                fields[column] = fields[column].Replace(@"[UniMod:4]", @"[+57.021464]");
                return fields;
            });
            report.AddRange(RoundTripDifferences(_testDir, Path.GetFileName(massNotation), FragmentToleranceConfig.UnitResolution(0.5)));
            var massNotationEntries = new DiannTsvLoader(FragmentToleranceConfig.UnitResolution(0.5)).Load(massNotation);
            var twoCysteines = massNotationEntries.First(e => e.Sequence == @"KDCPWPCPK");
            CollectionAssert.AreEqual(new[] { 2, 6 }, twoCysteines.Modifications.Select(m => m.Position).ToArray(), twoCysteines.ModifiedSequence);
            var carbamidomethyl = massNotationEntries.SelectMany(e => e.Modifications).First();
            Assert.AreEqual(4, carbamidomethyl.UnimodId);
            Assert.AreEqual(UniMod.Find(4).Name, carbamidomethyl.Name);
            Assert.AreEqual(UniMod.Find(4).Mass, carbamidomethyl.MassDelta);
            Assert.AreEqual(0, report.Count, string.Join(Environment.NewLine, report));
        }

        /// <summary>
        /// The differences between the subset library loaded from its .tsv and loaded back from
        /// its .blib export: a count per field, then the first forty lines. Empty when identical.
        /// </summary>
        private List<string> RoundTripDifferences(string dataDir, string libraryFile, FragmentToleranceConfig tolerance)
        {
            string tsv = Path.Combine(dataDir, libraryFile);
            var fromTsv = new DiannTsvLoader(tolerance).Load(tsv);
            string blib = Path.Combine(_testDir, Path.GetFileNameWithoutExtension(libraryFile) + @".blib");
            LibraryBlibWriter.Write(blib, fromTsv, Path.GetFileName(tsv));
            var fromBlib = new BlibLoader(tolerance).Load(blib);
            return DescribeDifferences(libraryFile, LibraryDifferences(fromTsv, fromBlib), fromTsv.Count);
        }

        /// <summary>
        /// The differences between the library-decoy subset loaded by <see cref="LibraryLoader"/>
        /// from its .tsv with the pairing manifest, and loaded by it from the .blib export of
        /// that library ALONE: every entry's fields, its pairing id among them, and the
        /// entrapment pairing <see cref="EntrapmentPairing.Build"/> reads from the ids. Empty
        /// when the .blib carries the whole design the manifest gave.
        /// </summary>
        private List<string> LoaderRoundTripDifferences()
        {
            var tolerance = FragmentToleranceConfig.UnitResolution(0.5);
            string tsv = Path.Combine(_dataDir, LIBDECOY_FILE);
            // An entrapment library that brings no pairing is refused, never paired by guesswork.
            var refusal = Assert.ThrowsException<InvalidDataException>(() => LibraryLoader.Load(new OspreyConfig
            {
                LibrarySource = LibrarySource.FromPath(tsv),
                FragmentTolerance = tolerance,
                DecoysInLibrary = true,
            }, null, null));
            Assert.AreEqual(string.Format(
                OspreyIOResources.LibraryLoader_Load_The_library__0__has_entrapment_peptides_but_nothing_pairs_them_with_their_targets__Give_the_pairing_manifest_with__1__or_search_a__blib_that_carries_a_DecoyPairs_table_,
                tsv, OspreyArgNames.Text(OspreyArgNames.DECOY_PAIRING_MANIFEST)), refusal.Message);
            var fromTsv = LibraryLoader.Load(new OspreyConfig
            {
                LibrarySource = LibrarySource.FromPath(tsv),
                FragmentTolerance = tolerance,
                DecoysInLibrary = true,
                DecoyPairingManifestPath = Path.Combine(_dataDir, LIBDECOY_PAIRING_FILE),
            }, null, null);
            string blib = Path.Combine(_testDir, Path.GetFileNameWithoutExtension(LIBDECOY_FILE) + @".blib");
            LibraryBlibWriter.Write(blib, fromTsv, Path.GetFileName(tsv));
            var fromBlib = LibraryLoader.Load(new OspreyConfig
            {
                LibrarySource = LibrarySource.FromPath(blib),
                FragmentTolerance = tolerance,
                DecoysInLibrary = true,
            }, null, null);

            var differences = LibraryDifferences(fromTsv, fromBlib);
            var pairingTsv = EntrapmentPairing.Build(fromTsv.ToDictionary(e => e.Id));
            var pairingBlib = EntrapmentPairing.Build(fromBlib.ToDictionary(e => e.Id));
            // The subset must exercise what is compared: entrapment peptides paired with targets.
            int pairedGroups = pairingTsv.PairIndexBySeq.GroupBy(kv => kv.Value).Count(g => g.Count() > 1);
            if (pairedGroups == 0)
                differences.Add(@"pairing: the .tsv pairs no peptide with another, so the subset proves nothing about entrapment pairing");
            foreach (var kv in pairingTsv.PairIndexBySeq)
            {
                if (!pairingBlib.PairIndexBySeq.TryGetValue(kv.Key, out uint idx))
                    differences.Add(string.Format(@"{0}: pairing: {1} vs none", kv.Key, kv.Value));
                else if (idx != kv.Value)
                    differences.Add(string.Format(@"{0}: pairing: {1} vs {2}", kv.Key, kv.Value, idx));
            }
            foreach (var kv in pairingBlib.PairIndexBySeq)
            {
                if (!pairingTsv.PairIndexBySeq.ContainsKey(kv.Key))
                    differences.Add(string.Format(@"{0}: pairing: none vs {1}", kv.Key, kv.Value));
            }
            foreach (string seq in pairingTsv.ExcludedEntrapment.Except(pairingBlib.ExcludedEntrapment))
                differences.Add(string.Format(@"{0}: excluded: true vs false", seq));
            foreach (string seq in pairingBlib.ExcludedEntrapment.Except(pairingTsv.ExcludedEntrapment))
                differences.Add(string.Format(@"{0}: excluded: false vs true", seq));
            return DescribeDifferences(LIBDECOY_FILE + @" (LibraryLoader, no manifest for the .blib)", differences, fromTsv.Count);
        }

        /// <summary>
        /// The derived library. The first <see cref="LibraryLoader"/> load of the library-decoy
        /// subset from its .tsv writes <c>&lt;library&gt;.blib</c> in the cache directory, and
        /// a <c>.libcache</c> left there by the reload cache it replaced is deleted, at exactly
        /// that path, once the derived library is written (a cache is rebuilt from its source,
        /// never carried forward); the next load says it read the derived library and gives
        /// the identical library, entry by entry; a load under another configuration says the
        /// derived library no longer matches and rebuilds it. The two lean loads through the
        /// derived library: under <c>OmitFragments</c> every entry keeps its scalars with a
        /// readable empty spectrum, and under <c>RetainFragmentsFor</c> the library is exactly
        /// what a full load followed by <see cref="LibraryFragmentRelease.ReleaseFragments"/>
        /// leaves - released, not empty, where the set does not name the entry's base id, decoys
        /// following their targets.
        /// </summary>
        private List<string> DerivedLibraryDifferences()
        {
            string dir = CreateDir(@"derived");
            string tsv = Path.Combine(dir, LIBDECOY_FILE);
            File.Copy(Path.Combine(_dataDir, LIBDECOY_FILE), tsv);
            string manifest = Path.Combine(dir, LIBDECOY_PAIRING_FILE);
            File.Copy(Path.Combine(_dataDir, LIBDECOY_PAIRING_FILE), manifest);
            string cacheDir = ArtifactPaths.ResolveCacheDir(tsv);
            string derived = Path.Combine(cacheDir, LIBDECOY_FILE + LibraryLoader.DERIVED_LIBRARY_EXT);
            // The reload cache an earlier build left beside the library: its bytes are never
            // read, so any file at the path stands in for one.
            string retiredCache = Path.Combine(cacheDir, LIBDECOY_FILE + LibraryLoader.RETIRED_CACHE_EXT);
            File.WriteAllText(retiredCache, @"OSPRLBR");
            var differences = new List<string>();

            var firstLog = new StringBuilder();
            var first = LoadDerived(tsv, manifest, FragmentToleranceConfig.UnitResolution(0.5), LibraryLoadOptions.Default, firstLog);
            if (!File.Exists(derived))
                differences.Add(@"derived: no derived library written at " + derived);
            if (File.Exists(retiredCache))
                differences.Add(@"derived: the .libcache was not removed at " + retiredCache);
            string saved = string.Format(OspreyIOResources.LibraryLoader_Load_Saved_the_derived_library___0__precursors__to___1__,
                first.Count, derived);
            string loaded = string.Format(OspreyIOResources.LibraryLoader_Load_Loaded__0__library_precursors_from_the_derived_library___1__,
                first.Count, derived);
            string removed = string.Format(OspreyIOResources.LibraryLoader_DeleteRetiredCache_Removed_the_library_cache___0____the_derived_library_replaces_it_,
                retiredCache);
            StringAssert.Contains(firstLog.ToString(), saved);
            StringAssert.Contains(firstLog.ToString(), removed);
            Assert.IsFalse(firstLog.ToString().Contains(loaded), firstLog.ToString());

            // The second load reads the derived library and gives the same library, and removes
            // a .libcache too: the derived library is in place whether this load wrote or read it.
            File.WriteAllText(retiredCache, @"OSPRLBR");
            var secondLog = new StringBuilder();
            var second = LoadDerived(tsv, manifest, FragmentToleranceConfig.UnitResolution(0.5), LibraryLoadOptions.Default, secondLog);
            StringAssert.Contains(secondLog.ToString(), loaded);
            StringAssert.Contains(secondLog.ToString(), removed);
            Assert.IsFalse(secondLog.ToString().Contains(saved), secondLog.ToString());
            if (File.Exists(retiredCache))
                differences.Add(@"derived: the .libcache was not removed by the read at " + retiredCache);
            // With none left, a load says nothing about one.
            var thirdLog = new StringBuilder();
            LoadDerived(tsv, manifest, FragmentToleranceConfig.UnitResolution(0.5), LibraryLoadOptions.Default, thirdLog);
            Assert.IsFalse(thirdLog.ToString().Contains(removed), thirdLog.ToString());
            differences.AddRange(LibraryDifferences(first, second));

            // Every entry's scalars with a readable empty spectrum: the FirstPassFDR worker's load.
            var lean = LoadDerived(tsv, manifest, FragmentToleranceConfig.UnitResolution(0.5),
                new LibraryLoadOptions { OmitFragments = true }, new StringBuilder());
            Assert.AreEqual(first.Count, lean.Count);
            for (int i = 0; i < first.Count; i++)
            {
                AssertSameIdentity(first[i], lean[i], string.Format(@"entry {0} (id {1})", i, first[i].Id));
                Assert.IsFalse(lean[i].IsSpectrumReleased);
                Assert.AreEqual(0, lean[i].Fragments.Count);
            }

            // Every other target's base id, which carries its paired decoy along: the set must
            // both retain and release, and on both sides, or the comparison proves nothing.
            var retained = new HashSet<uint>(first.Where(e => !e.IsDecoy).Select(e => e.Id).Where((id, i) => i % 2 == 0));
            var skipped = LoadDerived(tsv, manifest, FragmentToleranceConfig.UnitResolution(0.5),
                new LibraryLoadOptions { RetainFragmentsFor = retained }, new StringBuilder());
            int nReleased = LibraryFragmentRelease.ReleaseFragments(second, retained);
            Assert.IsTrue(nReleased > 0 && nReleased < second.Count, nReleased.ToString());
            Assert.IsTrue(skipped.Any(e => e.IsDecoy && e.IsSpectrumReleased), @"no decoy released");
            Assert.IsTrue(skipped.Any(e => e.IsDecoy && !e.IsSpectrumReleased), @"no decoy retained");
            AssertRetainMatchesRelease(second, skipped, retained);

            // Another configuration - a tolerance the types were not computed within - finds the
            // derived library stale and rebuilds it, under its own hash.
            var rebuiltLog = new StringBuilder();
            var rebuilt = LoadDerived(tsv, manifest, FragmentToleranceConfig.UnitResolution(0.6), LibraryLoadOptions.Default, rebuiltLog);
            StringAssert.Contains(rebuiltLog.ToString(), string.Format(
                OspreyIOResources.LibraryLoader_Load_The_derived_library___0___was_built_from_a_different_version_or_configuration__rebuilding_it_from_the_source_library_,
                derived));
            StringAssert.Contains(rebuiltLog.ToString(), string.Format(
                OspreyIOResources.LibraryLoader_Load_Saved_the_derived_library___0__precursors__to___1__, rebuilt.Count, derived));

            // Decoys the manifest declares but no accession prefix marks (a predictor that
            // stripped the prefix): a copy loses the prefix on half its decoy precursors, by
            // sequence so every row of a precursor agrees, while the other half keeps it (a load
            // whose prefixes mark no decoy at all is refused before the manifest can mark any).
            // The derived library must keep the accessions as loaded - its DecoyPairs table marks
            // the decoys - so the warm run reads the entries the parse made, not prefixed copies.
            string unprefixedDir = CreateDir(@"derived-unprefixed");
            string unprefixedTsv = Path.Combine(unprefixedDir, LIBDECOY_FILE);
            var lines = File.ReadAllLines(tsv);
            var header = lines[0].Split('\t');
            int proteinColumn = Array.IndexOf(header, @"ProteinID");
            int sequenceColumn = Array.IndexOf(header, @"StrippedPeptide");
            Assert.IsTrue(proteinColumn >= 0 && sequenceColumn >= 0, lines[0]);
            const string subsetDecoyPrefix = "decoy_";
            for (int i = 1; i < lines.Length; i++)
            {
                var fields = lines[i].Split('\t');
                if (!fields[proteinColumn].StartsWith(subsetDecoyPrefix, StringComparison.Ordinal) || fields[sequenceColumn][0] > 'M')
                    continue;
                fields[proteinColumn] = fields[proteinColumn].Substring(subsetDecoyPrefix.Length);
                lines[i] = string.Join("\t", fields);
            }
            File.WriteAllLines(unprefixedTsv, lines);
            // The manifest replaces a decoy's accessions with its own source-protein accessions,
            // so its copy loses the prefix on the same decoys, or every decoy would end prefixed.
            string unprefixedManifest = Path.Combine(unprefixedDir, LIBDECOY_PAIRING_FILE);
            var manifestLines = File.ReadAllLines(manifest);
            var manifestHeader = manifestLines[0].Split('\t');
            int manifestSequence = Array.IndexOf(manifestHeader, @"sequence");
            int manifestDecoy = Array.IndexOf(manifestHeader, @"decoy");
            int manifestProteins = Array.IndexOf(manifestHeader, @"proteins");
            Assert.IsTrue(manifestSequence >= 0 && manifestDecoy >= 0 && manifestProteins >= 0, manifestLines[0]);
            for (int i = 1; i < manifestLines.Length; i++)
            {
                var fields = manifestLines[i].Split('\t');
                if (fields[manifestDecoy] != @"Yes" || fields[manifestSequence][0] > 'M' ||
                    !fields[manifestProteins].StartsWith(subsetDecoyPrefix, StringComparison.Ordinal))
                {
                    continue;
                }
                fields[manifestProteins] = fields[manifestProteins].Substring(subsetDecoyPrefix.Length);
                manifestLines[i] = string.Join("\t", fields);
            }
            File.WriteAllLines(unprefixedManifest, manifestLines);
            var unprefixedLog = new StringBuilder();
            var unprefixedFirst = LoadDerived(unprefixedTsv, unprefixedManifest, FragmentToleranceConfig.UnitResolution(0.5),
                LibraryLoadOptions.Default, unprefixedLog);
            var prefixes = new OspreyConfig().DecoyPrefixes;
            Assert.IsTrue(unprefixedFirst.Any(e => e.IsDecoy && !e.LooksLikeLibraryDecoy(prefixes)),
                string.Format(@"no decoy without a prefix: the copy proves nothing ({0} decoys of {1}; first decoy accessions: {2}){3}{4}",
                    unprefixedFirst.Count(e => e.IsDecoy), unprefixedFirst.Count,
                    string.Join(@";", unprefixedFirst.First(e => e.IsDecoy).ProteinIds), Environment.NewLine, unprefixedLog));
            var unprefixedSecond = LoadDerived(unprefixedTsv, unprefixedManifest, FragmentToleranceConfig.UnitResolution(0.5),
                LibraryLoadOptions.Default, new StringBuilder());
            differences.AddRange(LibraryDifferences(unprefixedFirst, unprefixedSecond).Select(line => @"unprefixed decoys " + line));
            return DescribeDifferences(LIBDECOY_FILE + @" (LibraryLoader, derived library)", differences, first.Count);
        }

        /// <summary>The library-decoy subset at <paramref name="tsv"/> with its manifest, loaded by <see cref="LibraryLoader"/> with <paramref name="options"/>, its log in <paramref name="log"/>.</summary>
        private static List<LibraryEntry> LoadDerived(string tsv, string manifest, FragmentToleranceConfig tolerance,
            LibraryLoadOptions options, StringBuilder log)
        {
            return LibraryLoader.Load(new OspreyConfig
            {
                LibrarySource = LibrarySource.FromPath(tsv),
                FragmentTolerance = tolerance,
                DecoysInLibrary = true,
                DecoyPairingManifestPath = manifest,
            }, options, OspreyLog.FromDelegate(line => log.AppendLine(line)), line => log.AppendLine(line));
        }

        /// <summary>Every member but the spectrum, equal on <paramref name="a"/> and <paramref name="b"/>.</summary>
        private static void AssertSameIdentity(LibraryEntry a, LibraryEntry b, string what)
        {
            Assert.AreEqual(a.Id, b.Id, what);
            Assert.AreEqual(a.Sequence, b.Sequence, what);
            Assert.AreEqual(a.ModifiedSequence, b.ModifiedSequence, what);
            Assert.AreEqual(a.Charge, b.Charge, what);
            Assert.AreEqual(a.PrecursorMz, b.PrecursorMz, 1e-10, what);
            Assert.AreEqual(a.RetentionTime, b.RetentionTime, 1e-10, what);
            Assert.AreEqual(a.RtCalibrated, b.RtCalibrated, what);
            Assert.AreEqual(a.IsDecoy, b.IsDecoy, what);
            Assert.AreEqual(a.PairingId, b.PairingId, what);
            Assert.AreEqual(a.Modifications.Count, b.Modifications.Count, what);
            CollectionAssert.AreEqual(a.ProteinIds.ToArray(), b.ProteinIds.ToArray(), what);
            CollectionAssert.AreEqual(a.GeneNames.ToArray(), b.GeneNames.ToArray(), what);
        }

        /// <summary>
        /// <paramref name="skipped"/>, loaded with the retain set, is entry by entry the state
        /// <paramref name="released"/> was left in by a full load and
        /// <see cref="LibraryFragmentRelease.ReleaseFragments"/> with the same set: identity
        /// untouched on both sides, a skipped spectrum RELEASED (asked through
        /// <see cref="LibraryEntry.IsSpectrumReleased"/>, and the throw on reading it asserted
        /// rather than inferred, since equality would also hold if both sides had quietly become
        /// readable-empty), a retained one carrying the identical peaks.
        /// </summary>
        private static void AssertRetainMatchesRelease(IReadOnlyList<LibraryEntry> released, IReadOnlyList<LibraryEntry> skipped,
            HashSet<uint> retained)
        {
            Assert.AreEqual(released.Count, skipped.Count);
            for (int i = 0; i < released.Count; i++)
            {
                var a = released[i];
                var b = skipped[i];
                string what = string.Format(CultureInfo.InvariantCulture, @"entry {0} (id {1})", i, a.Id);
                AssertSameIdentity(a, b, what);
                Assert.AreEqual(a.IsSpectrumReleased, b.IsSpectrumReleased, what);
                bool expectReleased = !retained.Contains(a.Id & ScoringTaskShared.BASE_ID_MASK);
                Assert.AreEqual(expectReleased, b.IsSpectrumReleased, what);
                if (expectReleased)
                {
                    Assert.ThrowsException<InvalidOperationException>(() =>
                    {
                        int unused = b.Fragments.Count;
                    }, what + @": a skipped spectrum must throw on read, not read as empty");
                    continue;
                }
                CollectionAssert.AreEqual(a.Fragments.Select(FormatFragment).ToArray(), b.Fragments.Select(FormatFragment).ToArray(), what);
            }
        }

        /// <summary>
        /// <paramref name="differences"/> as a report: a count per field, then the first forty
        /// lines. Empty when there are none.
        /// </summary>
        private static List<string> DescribeDifferences(string libraryFile, List<string> differences, int entryCount)
        {
            if (differences.Count == 0)
                return differences;
            var report = new List<string>
            {
                string.Format(@"{0}: {1} difference(s) between the .tsv and its .blib ({2} entries)", libraryFile,
                    differences.Count, entryCount)
            };
            report.AddRange(differences.GroupBy(line => line.Split(':')[1].Trim().Split('[')[0])
                .Select(group => string.Format(@"  {0}: {1}", group.Key, group.Count())));
            report.AddRange(differences.Take(40).Select(line => @"  " + line));
            return report;
        }

        /// <summary>
        /// Every difference between two loads of one library, entry by entry in order, as
        /// "entry: field: a vs b" lines - all of them, so the summary counts are complete.
        /// </summary>
        private static List<string> LibraryDifferences(IReadOnlyList<LibraryEntry> a, IReadOnlyList<LibraryEntry> b)
        {
            var lines = new List<string>();
            if (a.Count != b.Count)
                lines.Add(string.Format(@"entry count: {0} vs {1}", a.Count, b.Count));
            int count = Math.Min(a.Count, b.Count);
            for (int i = 0; i < count; i++)
            {
                var ea = a[i];
                var eb = b[i];
                string entry = string.Format(@"#{0} {1}/{2}", i, ea.ModifiedSequence, ea.Charge);
                Compare(lines, entry, @"Id", ea.Id, eb.Id);
                Compare(lines, entry, @"Sequence", ea.Sequence, eb.Sequence);
                Compare(lines, entry, @"ModifiedSequence", ea.ModifiedSequence, eb.ModifiedSequence);
                Compare(lines, entry, @"Charge", ea.Charge, eb.Charge);
                Compare(lines, entry, @"PrecursorMz", ea.PrecursorMz, eb.PrecursorMz);
                Compare(lines, entry, @"RetentionTime", ea.RetentionTime, eb.RetentionTime);
                Compare(lines, entry, @"RtCalibrated", ea.RtCalibrated, eb.RtCalibrated);
                Compare(lines, entry, @"IsDecoy", ea.IsDecoy, eb.IsDecoy);
                Compare(lines, entry, @"PairingId", ea.PairingId, eb.PairingId);
                CompareLists(lines, entry, @"Modifications", ea.Modifications, eb.Modifications,
                    m => string.Format(CultureInfo.InvariantCulture, @"{0}@{1} {2:R} {3} {4}", m.Name, m.Position, m.MassDelta, m.UnimodId, string.Empty).TrimEnd());
                CompareLists(lines, entry, @"Fragments", ea.Fragments, eb.Fragments, FormatFragment);
                CompareLists(lines, entry, @"ProteinIds", ea.ProteinIds, eb.ProteinIds, p => p);
                CompareLists(lines, entry, @"GeneNames", ea.GeneNames, eb.GeneNames, g => g);
            }
            return lines;
        }

        private static string FormatFragment(LibraryFragment f)
        {
            var annotation = f.Annotation;
            return string.Format(CultureInfo.InvariantCulture, @"{0:R} {1:R} {2}{3}^{4} loss={5}/{6:R}",
                f.Mz, f.RelativeIntensity, annotation.IonType, annotation.Ordinal, annotation.Charge,
                annotation.NeutralLoss, annotation.CustomLossMass);
        }

        private static void Compare<T>(List<string> lines, string entry, string field, T a, T b)
        {
            if (!Equals(a, b))
                lines.Add(string.Format(CultureInfo.InvariantCulture, @"{0}: {1}: {2} vs {3}", entry, field, a, b));
        }

        private static void CompareLists<T>(List<string> lines, string entry, string field,
            IReadOnlyList<T> a, IReadOnlyList<T> b, Func<T, string> format)
        {
            var listA = (a ?? Array.Empty<T>()).Select(format).ToList();
            var listB = (b ?? Array.Empty<T>()).Select(format).ToList();
            if (listA.Count != listB.Count)
                lines.Add(string.Format(@"{0}: {1} count: {2} vs {3}", entry, field, listA.Count, listB.Count));
            for (int i = 0; i < Math.Min(listA.Count, listB.Count); i++)
            {
                if (listA[i] != listB[i])
                    lines.Add(string.Format(@"{0}: {1}[{2}]: {3} vs {4}", entry, field, i, listA[i], listB[i]));
            }
        }
    }
}
