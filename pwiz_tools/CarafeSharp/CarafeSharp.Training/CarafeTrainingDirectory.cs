/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe): the training files AIGear.java writes
 *   and src/main/resources/py/v2/ai.py reads
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
using pwiz.CarafeSharp.Core;
using pwiz.CarafeSharp.Models;

namespace pwiz.CarafeSharp.Training
{
    /// <summary>
    /// Reads the training data Carafe's Java writes for its Python fine-tuning:
    /// <c>psm_pdv.txt</c> (one row per training spectrum, with its row range in the fragment
    /// tables), <c>fragment_intensity_df.tsv</c> and <c>fragment_intensity_valid.tsv</c> (the
    /// matched intensities and the masking counts, columns b_z1, b_z2, y_z1, y_z2, one row per
    /// fragment position), and <c>rt_train_data.tsv</c>. Used to fine-tune on exactly the data
    /// Carafe trained on, which is how CarafeSharp's trainer is compared with Carafe's.
    /// </summary>
    public static class CarafeTrainingDirectory
    {
        public const string PSM_FILE = @"psm_pdv.txt";
        public const string INTENSITY_FILE = @"fragment_intensity_df.tsv";
        public const string VALID_FILE = @"fragment_intensity_valid.tsv";
        public const string RT_FILE = @"rt_train_data.tsv";

        private const char TAB = '\t';

        private static readonly string[] FRAGMENT_COLUMNS = { @"b_z1", @"b_z2", @"y_z1", @"y_z2" };

        /// <summary>
        /// The MS2 training spectra in <c>psm_pdv.txt</c> order. Carafe passes one collision energy
        /// and instrument for all of them (<c>--nce</c>, <c>--instrument</c>).
        /// </summary>
        public static IReadOnlyList<Ms2TrainingExample> ReadMs2(string directory, double nce, string instrument)
        {
            var psms = ReadTable(Path.Combine(directory, PSM_FILE));
            double[][] intensities = ReadFragmentTable(Path.Combine(directory, INTENSITY_FILE));
            double[][] invalid = File.Exists(Path.Combine(directory, VALID_FILE))
                ? ReadFragmentTable(Path.Combine(directory, VALID_FILE))
                : null;
            var examples = new List<Ms2TrainingExample>(psms.Rows.Count);
            foreach (var row in psms.Rows)
            {
                string sequence = psms.Get(row, @"peptide");
                var peptide = PeptideForm.FromAlphabase(sequence, psms.Get(row, @"mods"), psms.Get(row, @"mod_sites"));
                int charge = int.Parse(psms.Get(row, @"charge"), CultureInfo.InvariantCulture);
                int start = int.Parse(psms.Get(row, @"frag_start_idx"), CultureInfo.InvariantCulture);
                int stop = int.Parse(psms.Get(row, @"frag_stop_idx"), CultureInfo.InvariantCulture);
                if (stop - start != sequence.Length - 1)
                    throw new InvalidDataException(string.Format(@"{0}: {1} has fragment rows {2}-{3}.", PSM_FILE, sequence, start, stop));
                examples.Add(new Ms2TrainingExample(new PrecursorForm(peptide, charge), nce, instrument,
                    Flatten(intensities, start, stop), invalid == null ? new double[(stop - start) * FRAGMENT_COLUMNS.Length] : Flatten(invalid, start, stop)));
            }
            return examples;
        }

        /// <summary>The RT training rows of <c>rt_train_data.tsv</c>, one per identification.</summary>
        public static IReadOnlyList<RtTrainingExample> ReadRt(string directory)
        {
            var table = ReadTable(Path.Combine(directory, RT_FILE));
            return table.Rows.Select(row => new RtTrainingExample(
                PeptideForm.FromAlphabase(table.Get(row, @"sequence"), table.Get(row, @"mods"), table.Get(row, @"mod_sites")),
                double.Parse(table.Get(row, @"rt_norm"), NumberStyles.Float, CultureInfo.InvariantCulture))).ToArray();
        }

        /// <summary>
        /// Writes training rows as Carafe's training tables, the columns <see cref="ReadMs2"/> and
        /// <see cref="ReadRt"/> read (plus the ion counts), so CarafeSharp's training data can be
        /// inspected and compared with Carafe's by the same tools, and re-trained on.
        /// </summary>
        public static void Write(string directory, IReadOnlyList<RtTrainingExample> rt, IReadOnlyList<Ms2TrainingExample> ms2)
        {
            Directory.CreateDirectory(directory);
            using (var psms = new StreamWriter(Path.Combine(directory, PSM_FILE)))
            using (var intensities = new StreamWriter(Path.Combine(directory, INTENSITY_FILE)))
            using (var valid = new StreamWriter(Path.Combine(directory, VALID_FILE)))
            {
                psms.WriteLine(string.Join(TAB, @"psm_id", @"peptide", @"charge", @"mods", @"mod_sites", @"frag_start_idx",
                    @"frag_stop_idx", @"n_valid_fragment_ions", @"n_total_matched_ions"));
                string header = string.Join(TAB, FRAGMENT_COLUMNS);
                intensities.WriteLine(header);
                valid.WriteLine(header);
                int row = 0;
                for (int i = 0; i < ms2.Count; i++)
                {
                    var example = ms2[i];
                    int rows = example.Precursor.Peptide.Length - 1;
                    int matched = example.Intensities.Count(v => v > 0);
                    int validMatched = Enumerable.Range(0, example.Intensities.Length)
                        .Count(s => example.Intensities[s] > 0 && example.Invalid[s] <= 0);
                    psms.WriteLine(string.Join(TAB, (i + 1).ToString(CultureInfo.InvariantCulture), example.Sequence,
                        example.Precursor.Charge.ToString(CultureInfo.InvariantCulture), example.Precursor.Peptide.ModsText,
                        example.Precursor.Peptide.ModSitesText, row.ToString(CultureInfo.InvariantCulture),
                        (row + rows).ToString(CultureInfo.InvariantCulture), validMatched.ToString(CultureInfo.InvariantCulture),
                        matched.ToString(CultureInfo.InvariantCulture)));
                    for (int r = 0; r < rows; r++)
                    {
                        var cells = Enumerable.Range(r * FRAGMENT_COLUMNS.Length, FRAGMENT_COLUMNS.Length).ToArray();
                        intensities.WriteLine(string.Join(TAB, cells.Select(c => example.Intensities[c].ToString(@"R", CultureInfo.InvariantCulture))));
                        valid.WriteLine(string.Join(TAB, cells.Select(c => example.Invalid[c].ToString(CultureInfo.InvariantCulture))));
                    }
                    row += rows;
                }
            }
            using (var rtFile = new StreamWriter(Path.Combine(directory, RT_FILE)))
            {
                rtFile.WriteLine(string.Join(TAB, @"peptide", @"sequence", @"mods", @"mod_sites", @"rt_norm"));
                foreach (var example in rt)
                {
                    rtFile.WriteLine(string.Join(TAB, example.Peptide.Sequence, example.Peptide.Sequence, example.Peptide.ModsText,
                        example.Peptide.ModSitesText, example.RtNorm.ToString(@"R", CultureInfo.InvariantCulture)));
                }
            }
        }

        private static double[] Flatten(double[][] table, int start, int stop)
        {
            var values = new double[(stop - start) * FRAGMENT_COLUMNS.Length];
            for (int row = start; row < stop; row++)
                Array.Copy(table[row], 0, values, (row - start) * FRAGMENT_COLUMNS.Length, FRAGMENT_COLUMNS.Length);
            return values;
        }

        /// <summary>
        /// A fragment table as rows of b_z1, b_z2, y_z1, y_z2; a missing column reads as 0, as
        /// Carafe fills it.
        /// </summary>
        private static double[][] ReadFragmentTable(string path)
        {
            var table = ReadTable(path);
            var indexes = FRAGMENT_COLUMNS.Select(c => table.Columns.TryGetValue(c, out int i) ? i : -1).ToArray();
            return table.Rows.Select(row => indexes.Select(i => i < 0 || string.IsNullOrEmpty(row[i])
                ? 0.0
                : double.Parse(row[i], NumberStyles.Float, CultureInfo.InvariantCulture)).ToArray()).ToArray();
        }

        private static TsvTable ReadTable(string path)
        {
            using (var reader = new StreamReader(path))
            {
                string header = reader.ReadLine() ?? throw new InvalidDataException(path + @" is empty.");
                var columns = header.Split('\t').Select((name, index) => (name, index))
                    .ToDictionary(p => p.name, p => p.index, StringComparer.Ordinal);
                var rows = new List<string[]>();
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Length > 0)
                        rows.Add(line.Split('\t'));
                }
                return new TsvTable(path, columns, rows);
            }
        }

        private sealed class TsvTable
        {
            private readonly string _path;

            public TsvTable(string path, Dictionary<string, int> columns, List<string[]> rows)
            {
                _path = path;
                Columns = columns;
                Rows = rows;
            }

            public Dictionary<string, int> Columns { get; }

            public List<string[]> Rows { get; }

            /// <summary>A cell by column name; an absent trailing cell reads as empty.</summary>
            public string Get(string[] row, string column)
            {
                if (!Columns.TryGetValue(column, out int index))
                    throw new InvalidDataException(string.Format(@"{0} has no column {1}.", _path, column));
                return index < row.Length ? row[index] : string.Empty;
            }
        }
    }
}
