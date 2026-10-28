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

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace pwiz.CarafeSharp.Core
{
    /// <summary>
    /// The precursor table Skyline writes for a library prediction, as it wrote it for peptdeep:
    /// tab-separated, one precursor per line, under a header naming alphabase's columns in any
    /// order - the bare sequence, the modification names and their sites in alphabase's notation
    /// (<see cref="PeptideForm.FromAlphabase"/>), and the charge. Osprey's <c>--precursors</c>
    /// reads it; declared here, free of the models, so a command line can name its columns without
    /// touching the torch runtime.
    /// </summary>
    public static class PrecursorTable
    {
        public const string SEQUENCE = @"sequence";
        public const string MODS = @"mods";
        public const string MOD_SITES = @"mod_sites";
        public const string CHARGE = @"charge";

        public static readonly IReadOnlyList<string> COLUMNS = new[] { SEQUENCE, MODS, MOD_SITES, CHARGE };

        /// <summary>The table's precursors in its order; a blank line is skipped.</summary>
        public static List<PrecursorForm> Read(string path)
        {
            var precursors = new List<PrecursorForm>();
            using var reader = new StreamReader(path);
            string header = reader.ReadLine();
            if (header == null)
                throw new InvalidDataException(string.Format(@"The precursor table {0} is empty.", path));
            var columns = header.Split('\t').Select((name, index) => (name, index)).ToDictionary(c => c.name, c => c.index);
            foreach (string column in COLUMNS)
            {
                if (!columns.ContainsKey(column))
                    throw new InvalidDataException(string.Format(@"The precursor table {0} has no {1} column.", path, column));
            }
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Length == 0)
                    continue;
                var fields = line.Split('\t');
                var peptide = PeptideForm.FromAlphabase(fields[columns[SEQUENCE]], fields[columns[MODS]], fields[columns[MOD_SITES]]);
                precursors.Add(new PrecursorForm(peptide, int.Parse(fields[columns[CHARGE]], CultureInfo.InvariantCulture)));
            }
            return precursors;
        }
    }
}
