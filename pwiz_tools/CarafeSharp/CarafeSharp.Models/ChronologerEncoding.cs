/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Chronologer (https://github.com/searlelab/chronologer)
 *   src/chronologer/chronologer_utils/tensorize.py, and the preprocessing JSON of jchronologer
 *   (https://github.com/searlelab/jchronologer), both Apache-2.0
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
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using pwiz.CarafeSharp.Core;
using pwiz.CarafeSharp.Models.Modules;

namespace pwiz.CarafeSharp.Models
{
    /// <summary>
    /// Chronologer's peptide encoding (<c>tensorize.py</c>), read from jchronologer's preprocessing JSON.
    /// A peptide is first written as an EncyclopeDIA-style mass-annotated sequence
    /// (<c>[+42.010565]AC[+57.021464]DEK</c>); the JSON's rules turn each modified residue into a token of its
    /// own (<c>c</c>), and the sequence gets an N-terminal token (<c>-</c> free, <c>^</c> acetyl, <c>(</c>
    /// pyro-Glu, <c>)</c> cyclized carbamidomethyl-Cys) and the C-terminal <c>_</c>. Tokens are padded with 0 to
    /// <see cref="ModelChronologer.VECTOR_LENGTH"/>. A peptide Chronologer cannot encode is rejected, not guessed at:
    /// a modification it has no token for, a C-terminal modification, or a length outside
    /// <see cref="MIN_PEPTIDE_LENGTH"/> to <see cref="MaxPeptideLength"/>.
    /// </summary>
    public sealed class ChronologerEncoding
    {
        /// <summary>Chronologer's <c>min_peptide_len</c>; the maximum comes from the JSON.</summary>
        public const int MIN_PEPTIDE_LENGTH = 6;

        public static ChronologerEncoding Load(string path)
        {
            using (var document = JsonDocument.Parse(File.ReadAllBytes(path)))
            {
                var root = document.RootElement;
                var tokenOf = root.GetProperty(@"aa_to_int").EnumerateObject()
                    .ToDictionary(p => p.Name.Single(), p => p.Value.GetInt64());
                // Python's re accepts "{,6}" for "{0,6}"; .NET reads it as literal text.
                var modRules = root.GetProperty(@"mod_regex_rules").EnumerateArray()
                    .Select(r => (new Regex(RequiredString(r, @"pattern", path).Replace(@"{,", @"{0,"), RegexOptions.CultureInvariant),
                        RequiredString(r, @"token", path)))
                    .ToArray();
                var ntermKeys = root.GetProperty(@"nterm_keys").EnumerateObject()
                    .ToDictionary(p => p.Name, p => p.Value.GetString() ?? throw new InvalidDataException(path + @": an N-terminal key has no token."));
                int maxPeptideLength = root.GetProperty(@"max_peptide_len").GetInt32();
                int padding = root.GetProperty(@"padding_index").GetInt32();
                if (padding != 0 || maxPeptideLength + 2 != ModelChronologer.VECTOR_LENGTH ||
                    tokenOf.Count + 1 != ModelChronologer.TOKEN_COUNT)
                {
                    throw new InvalidDataException(string.Format(
                        @"{0} does not describe the Chronologer network: padding {1}, {2} tokens, maximum length {3}.",
                        path, padding, tokenOf.Count, maxPeptideLength));
                }
                return new ChronologerEncoding(tokenOf, modRules, ntermKeys, maxPeptideLength);
            }
        }

        /// <summary>
        /// The EncyclopeDIA-style mass-annotated sequence of <paramref name="form"/>, the form
        /// Chronologer's rules match: each modification's mass after its residue, a terminal modification
        /// before the sequence, and a residue-specific N-terminal one (<c>Gln->pyro-Glu@Q^Any_N-term</c>) on
        /// the first residue. Null when the form has a modification the alphabase table does not know, or a
        /// C-terminal one, which Chronologer has no token for.
        /// </summary>
        public static string ToModifiedSequence(PeptideForm form)
        {
            string prefix = string.Empty;
            var residueMods = new List<double>[form.Length + 1];
            for (int i = 0; i < form.ModNames.Count; i++)
            {
                string name = form.ModNames[i];
                int site = form.ModSites[i];
                if (site < 0 || !ModificationTable.TryGet(name, out var definition))
                    return null;
                if (site == 0)
                {
                    int at = name.IndexOf('@');
                    if (at < 0 || name.IndexOf('^', at) < 0)
                    {
                        prefix += Bracket(definition.Mass);
                        continue;
                    }
                    site = 1;
                }
                (residueMods[site] ?? (residueMods[site] = new List<double>())).Add(definition.Mass);
            }
            var sb = new StringBuilder(prefix);
            for (int i = 0; i < form.Length; i++)
            {
                sb.Append(form.Sequence[i]);
                foreach (double mass in residueMods[i + 1] ?? Enumerable.Empty<double>())
                    sb.Append(Bracket(mass));
            }
            return sb.ToString();
        }

        private readonly Dictionary<char, long> _tokenOf;
        private readonly (Regex Pattern, string Token)[] _modRules;
        private readonly Dictionary<string, string> _ntermKeys;

        private ChronologerEncoding(Dictionary<char, long> tokenOf, (Regex Pattern, string Token)[] modRules,
            Dictionary<string, string> ntermKeys, int maxPeptideLength)
        {
            _tokenOf = tokenOf;
            _modRules = modRules;
            _ntermKeys = ntermKeys;
            MaxPeptideLength = maxPeptideLength;
        }

        public int MaxPeptideLength { get; }

        /// <summary>The <see cref="ModelChronologer.VECTOR_LENGTH"/> tokens of a form, or null when it is rejected.</summary>
        public long[] Encode(PeptideForm form)
        {
            return EncodeModifiedSequence(ToModifiedSequence(form));
        }

        /// <summary>The tokens of a mass-annotated sequence, or null when it is rejected.</summary>
        internal long[] EncodeModifiedSequence(string modifiedSequence)
        {
            string coded = modifiedSequence == null ? null : ToCodedSequence(modifiedSequence);
            if (coded == null)
                return null;
            int residues = coded.Length - 2;
            if (residues < MIN_PEPTIDE_LENGTH || residues > MaxPeptideLength)
                return null;
            var tokens = new long[ModelChronologer.VECTOR_LENGTH];
            for (int i = 0; i < coded.Length; i++)
            {
                if (!_tokenOf.TryGetValue(coded[i], out tokens[i]))
                    return null;
            }
            return tokens;
        }

        /// <summary>
        /// Python's <c>modseq_to_codedseq</c>: the one-character-per-token sequence with its terminal tokens,
        /// before the length check; null when a modification has no token.
        /// </summary>
        internal string ToCodedSequence(string modifiedSequence)
        {
            string seq = modifiedSequence;
            foreach (var rule in _modRules)
                seq = rule.Pattern.Replace(seq, rule.Token);
            if (seq.Length == 0)
                return null;
            if (seq[0] == 'd')
                seq = @")" + seq;
            else if (seq[0] == 'e')
                seq = @"(" + seq;
            else if (seq[0] == '[')
            {
                // Python looks up seq[1:7], the sign and first five characters of the mass.
                int close = seq.IndexOf(']');
                if (close < 0 || seq.Length < 7 || !_ntermKeys.TryGetValue(seq.Substring(1, 6), out string token))
                    return null;
                seq = token + seq.Substring(close + 1);
            }
            else
                seq = @"-" + seq;
            seq += @"_";
            return seq.IndexOf('[') < 0 ? seq : null;
        }

        private static string Bracket(double mass)
        {
            return @"[" + mass.ToString(@"+0.000000;-0.000000", CultureInfo.InvariantCulture) + @"]";
        }

        private static string RequiredString(JsonElement element, string name, string path)
        {
            return element.GetProperty(name).GetString() ??
                   throw new InvalidDataException(string.Format(@"{0}: a modification rule has no {1}.", path, name));
        }
    }
}
