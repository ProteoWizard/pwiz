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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;

namespace pwiz.Osprey.Test
{
    public partial class SubsetPipelineTest
    {
        /// <summary>
        /// A library is the same object in memory whichever file it came from: the subset .tsv
        /// loaded directly, and loaded back from its .blib export, are compared entry by entry
        /// and field by field - identity, modifications, every fragment's m/z, intensity and
        /// annotation, proteins - and every difference is listed. The regression's
        /// <c>-LibraryAsBlib</c> legs prove the same thing at the search's output, forty
        /// minutes later; this is the seconds-long form of that gate, and the one to run first
        /// when a loader changes.
        /// </summary>
        [TestMethod]
        public void TestLibraryLoadsIdenticallyFromTsvAndBlib()
        {
            var report = new List<string>();
            report.AddRange(RoundTripDifferences(_dataDir, LIBRARY_FILE, FragmentToleranceConfig.UnitResolution(0.5)));
            report.AddRange(RoundTripDifferences(ExtractAstral(), ASTRAL_LIBRARY_FILE, FragmentToleranceConfig.Hram(10)));
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

            var differences = LibraryDifferences(fromTsv, fromBlib);
            if (differences.Count == 0)
                return differences;
            var report = new List<string>
            {
                string.Format(@"{0}: {1} difference(s) between the .tsv and its .blib ({2} entries)", libraryFile,
                    differences.Count, fromTsv.Count)
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
