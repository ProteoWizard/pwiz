/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
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

using System.Globalization;
using System.Text;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// The peptide text a blib row stores, from the forms a library or a search result carries:
    /// flanking residues stripped (<c>K.PEPTIDE.R</c>, <c>_PEPTIDE_</c>, <c>-PEPTIDE-</c>) and
    /// UniMod ids converted to mass shifts (<c>[UniMod:4]</c> to <c>[+57.0215]</c>), the notation
    /// Skyline matches a modification by. Ported from osprey-io/src/output/blib.rs.
    /// </summary>
    public static class BlibPeptideText
    {
        /// <summary>
        /// Strip flanking amino acid characters from peptide sequences.
        /// Handles formats like "K.PEPTIDE.R", "_PEPTIDE_", "_.PEPTIDE._", "-PEPTIDE-".
        /// Dots inside modification brackets (e.g., [+57.021]) are preserved.
        /// </summary>
        public static string StripFlankingChars(string seq)
        {
            string trimmed = seq.Trim('_', '.', '-');

            int firstDot = FindDotOutsideBrackets(trimmed, false);
            int lastDot = FindDotOutsideBrackets(trimmed, true);
            if (firstDot >= 0 && lastDot >= 0 && firstDot < lastDot)
            {
                return trimmed.Substring(firstDot + 1, lastDot - firstDot - 1);
            }

            return trimmed;
        }

        /// <summary>
        /// Convert UniMod notation (e.g., [UniMod:4]) to mass notation (e.g., [+57.0215])
        /// in modified peptide sequences.
        /// </summary>
        public static string ConvertUnimodToMass(string seq)
        {
            var result = new StringBuilder(seq.Length);
            int i = 0;
            while (i < seq.Length)
            {
                if (seq[i] == '[')
                {
                    int close = seq.IndexOf(']', i);
                    if (close < 0)
                    {
                        result.Append(seq[i]);
                        i++;
                        continue;
                    }

                    string content = seq.Substring(i + 1, close - i - 1);
                    string idStr = null;

                    if (content.StartsWith(@"UniMod:"))
                        idStr = content.Substring(7);
                    else if (content.StartsWith(@"UNIMOD:"))
                        idStr = content.Substring(7);

                    if (idStr != null)
                    {
                        int unimodId;
                        if (int.TryParse(idStr, out unimodId))
                        {
                            double mass;
                            if (TryGetUnimodMass(unimodId, out mass))
                            {
                                result.AppendFormat(CultureInfo.InvariantCulture, mass >= 0.0 ? @"[+{0:F4}]" : @"[{0:F4}]", mass);
                                i = close + 1;
                                continue;
                            }
                        }
                    }

                    // Not a recognized UniMod, keep as-is
                    result.Append(seq.Substring(i, close - i + 1));
                    i = close + 1;
                }
                else
                {
                    result.Append(seq[i]);
                    i++;
                }
            }
            return result.ToString();
        }

        /// <summary>
        /// Look up monoisotopic mass for a UniMod accession ID.
        /// Returns true if found.
        /// </summary>
        public static bool TryGetUnimodMass(int unimodId, out double mass)
        {
            var entry = UniMod.Find(unimodId);
            mass = entry?.Mass ?? 0;
            return entry != null;
        }

        private static int FindDotOutsideBrackets(string s, bool reverse)
        {
            int depth = 0;
            if (reverse)
            {
                for (int i = s.Length - 1; i >= 0; i--)
                {
                    if (s[i] == ']')
                        depth++;
                    else if (s[i] == '[')
                        depth--;
                    else if (s[i] == '.' && depth == 0)
                        return i;
                }
                return -1;
            }

            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '[')
                    depth++;
                else if (s[i] == ']')
                    depth--;
                else if (s[i] == '.' && depth == 0)
                    return i;
            }
            return -1;
        }
    }
}
