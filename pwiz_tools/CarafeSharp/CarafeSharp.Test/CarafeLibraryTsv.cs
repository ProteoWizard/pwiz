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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using pwiz.CarafeSharp.IO;

namespace pwiz.CarafeSharp.Test
{
    /// <summary>
    /// The precursors of a Carafe library TSV, each with its rows' text in file order, keyed by
    /// ModifiedPeptide and PrecursorCharge, and a precursor-by-precursor comparison of two
    /// libraries.
    /// </summary>
    public sealed class CarafeLibraryTsv
    {
        private const int MODIFIED_PEPTIDE = 0;
        private const int STRIPPED_PEPTIDE = 1;
        private const int PRECURSOR_MZ = 2;
        private const int PRECURSOR_CHARGE = 3;
        private const int RETENTION_TIME = 4;
        private const int PROTEIN_ID = 5;
        private const int DECOY = 6;
        private const int FRAGMENT_MZ = 7;
        private const int RELATIVE_INTENSITY = 8;

        /// <summary>
        /// Reads the precursors whose stripped sequence passes <paramref name="keep"/> (all
        /// when null) and, when given, whose key is in <paramref name="keys"/>.
        /// </summary>
        public static CarafeLibraryTsv Read(string path, Func<string, bool> keep = null, ISet<string> keys = null)
        {
            var library = new CarafeLibraryTsv();
            using (var reader = new StreamReader(path, new UTF8Encoding(false), false, 1 << 20))
            {
                string header = reader.ReadLine();
                if (header != CarafeLibraryTsvWriter.HEADER)
                    throw new InvalidDataException(@"Not a Carafe library TSV: " + path);
                string line;
                string lastPrefix = null;
                bool lastKept = false;
                while ((line = reader.ReadLine()) != null)
                {
                    // Rows of a precursor are adjacent and share their first five columns, so
                    // the filters run once per precursor, on text before the fifth tab.
                    int prefixEnd = NthTab(line, RETENTION_TIME + 1);
                    if (lastPrefix == null || string.CompareOrdinal(line, 0, lastPrefix, 0, prefixEnd) != 0 || lastPrefix.Length != prefixEnd)
                    {
                        lastPrefix = line.Substring(0, prefixEnd);
                        string[] precursorCells = lastPrefix.Split('\t');
                        lastKept = (keep == null || keep(precursorCells[STRIPPED_PEPTIDE])) &&
                                   (keys == null || keys.Contains(Key(precursorCells[MODIFIED_PEPTIDE], precursorCells[PRECURSOR_CHARGE])));
                    }
                    if (!lastKept)
                        continue;
                    string[] cells = line.Split('\t');
                    string key = Key(cells[MODIFIED_PEPTIDE], cells[PRECURSOR_CHARGE]);
                    if (!library.Precursors.TryGetValue(key, out var precursor))
                        library.Precursors.Add(key, precursor = new Precursor(cells));
                    precursor.Rows.Add(line);
                    precursor.Fragments.Add(cells.Skip(FRAGMENT_MZ).ToArray());
                }
            }
            return library;
        }

        /// <summary>A precursor's key from its ModifiedPeptide and PrecursorCharge text.</summary>
        public static string Key(string modifiedPeptide, string charge)
        {
            return modifiedPeptide + "\t" + charge;
        }

        private CarafeLibraryTsv()
        {
        }

        public Dictionary<string, Precursor> Precursors { get; } = new Dictionary<string, Precursor>(StringComparer.Ordinal);

        /// <summary>
        /// Compares <paramref name="ours"/> with this reference, precursor by precursor. A
        /// ProteinID that differs only by extra accessions of <paramref name="explainedProtein"/>
        /// (Carafe's since-fixed NoCut methionine clip) is counted apart.
        /// </summary>
        public Comparison Compare(CarafeLibraryTsv ours, Func<Precursor, Precursor, bool> explainedProtein = null)
        {
            var result = new Comparison
            {
                ReferenceOnly = Precursors.Keys.Count(k => !ours.Precursors.ContainsKey(k)),
                OursOnly = ours.Precursors.Keys.Count(k => !Precursors.ContainsKey(k)),
            };
            foreach (var pair in Precursors)
            {
                if (!ours.Precursors.TryGetValue(pair.Key, out var mine))
                    continue;
                var reference = pair.Value;
                result.Common++;
                if (reference.PrecursorMz != mine.PrecursorMz)
                    result.PrecursorMzDiffers++;
                if (reference.RetentionTime != mine.RetentionTime)
                {
                    result.RetentionTimeDiffers++;
                    result.MaxRetentionTimeDiff = Math.Max(result.MaxRetentionTimeDiff,
                        Math.Abs(Parse(reference.RetentionTime) - Parse(mine.RetentionTime)));
                }
                if (reference.ProteinId != mine.ProteinId)
                {
                    if (explainedProtein != null && explainedProtein(reference, mine))
                        result.ProteinIdExplained++;
                    else
                        result.ProteinIdDiffers++;
                }
                if (reference.Decoy != mine.Decoy)
                    result.DecoyDiffers++;
                CompareFragments(reference, mine, result);
            }
            return result;
        }

        private static void CompareFragments(Precursor reference, Precursor mine, Comparison result)
        {
            result.ReferenceFragments += reference.Fragments.Count;
            if (reference.Fragments.Count == mine.Fragments.Count &&
                reference.Fragments.Zip(mine.Fragments, (a, b) => a.SequenceEqual(b)).All(same => same))
            {
                result.IdenticalFragmentLists++;
                return;
            }
            var referenceIons = reference.Fragments.ToDictionary(Ion);
            var ourIons = mine.Fragments.ToDictionary(Ion);
            result.FragmentsInOneList += referenceIons.Keys.Count(k => !ourIons.ContainsKey(k)) +
                                         ourIons.Keys.Count(k => !referenceIons.ContainsKey(k));
            foreach (var ion in referenceIons)
            {
                if (!ourIons.TryGetValue(ion.Key, out var fragment))
                    continue;
                if (ion.Value[0] != fragment[0])
                    result.FragmentMzDiffers++;
                if (ion.Value[1] != fragment[1])
                    result.IntensityTextDiffers++;
                result.MaxIntensityDiff = Math.Max(result.MaxIntensityDiff, Math.Abs(Parse(ion.Value[1]) - Parse(fragment[1])));
            }
            if (referenceIons.Count == ourIons.Count && referenceIons.Keys.All(ourIons.ContainsKey) &&
                !reference.Fragments.Select(Ion).SequenceEqual(mine.Fragments.Select(Ion)))
            {
                result.SameSetDifferentOrder++;
            }
        }

        /// <summary>FragmentType, FragmentNumber, FragmentCharge and FragmentLossType.</summary>
        private static string Ion(string[] fragment)
        {
            return string.Join(@"/", fragment.Skip(2));
        }

        private static double Parse(string text)
        {
            return double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        /// <summary>The index of the <paramref name="n"/>-th tab of <paramref name="line"/>.</summary>
        private static int NthTab(string line, int n)
        {
            int index = -1;
            for (int i = 0; i < n; i++)
            {
                index = line.IndexOf('\t', index + 1);
                if (index < 0)
                    throw new InvalidDataException(@"Short library TSV row: " + line);
            }
            return index;
        }

        /// <summary>One precursor of the TSV.</summary>
        public sealed class Precursor
        {
            public Precursor(string[] cells)
            {
                ModifiedPeptide = cells[MODIFIED_PEPTIDE];
                Sequence = cells[STRIPPED_PEPTIDE];
                PrecursorMz = cells[PRECURSOR_MZ];
                Charge = cells[PRECURSOR_CHARGE];
                RetentionTime = cells[RETENTION_TIME];
                ProteinId = cells[PROTEIN_ID];
                Decoy = cells[DECOY];
            }

            public string ModifiedPeptide { get; }
            public string Sequence { get; }
            public string PrecursorMz { get; }
            public string Charge { get; }
            public string RetentionTime { get; }
            public string ProteinId { get; }
            public string Decoy { get; }

            /// <summary>The TSV lines, without line ends, in file order.</summary>
            public List<string> Rows { get; } = new List<string>();

            /// <summary>FragmentMz, RelativeIntensity, FragmentType, FragmentNumber, FragmentCharge, FragmentLossType per row.</summary>
            public List<string[]> Fragments { get; } = new List<string[]>();
        }

        /// <summary>The counts of <see cref="Compare"/>.</summary>
        public sealed class Comparison
        {
            public int Common;
            public int ReferenceOnly;
            public int OursOnly;
            public int PrecursorMzDiffers;
            public int RetentionTimeDiffers;
            public double MaxRetentionTimeDiff;
            public int ProteinIdDiffers;
            public int ProteinIdExplained;
            public int DecoyDiffers;
            public int IdenticalFragmentLists;
            public int ReferenceFragments;
            public int FragmentsInOneList;
            public int SameSetDifferentOrder;
            public int FragmentMzDiffers;
            public int IntensityTextDiffers;
            public double MaxIntensityDiff;

            public double IdenticalFraction
            {
                get { return Common == 0 ? 0 : (double)IdenticalFragmentLists / Common; }
            }

            public override string ToString()
            {
                return string.Format(CultureInfo.InvariantCulture,
                    @"precursors common {0}, reference only {1}, ours only {2}; PrecursorMz text differs {3}; " +
                    @"Tr_recalibrated text differs {4} (max |diff| {5:F2}); ProteinID differs {6} (+{7} explained by the NoCut M clip or by records the subset left out); " +
                    @"Decoy differs {8}; identical fragment lists {9} ({10:P3}); fragments in one list only {11} of {12}; " +
                    @"same set in another order {13}; FragmentMz text differs {14}; RelativeIntensity text differs {15}; max |intensity diff| {16:F4}",
                    Common, ReferenceOnly, OursOnly, PrecursorMzDiffers, RetentionTimeDiffers, MaxRetentionTimeDiff, ProteinIdDiffers,
                    ProteinIdExplained, DecoyDiffers, IdenticalFragmentLists, IdenticalFraction, FragmentsInOneList, ReferenceFragments,
                    SameSetDifferentOrder, FragmentMzDiffers, IntensityTextDiffers, MaxIntensityDiff);
            }
        }
    }
}
