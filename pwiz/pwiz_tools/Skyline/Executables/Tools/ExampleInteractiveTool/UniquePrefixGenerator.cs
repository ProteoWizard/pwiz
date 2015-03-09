/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.Text;

namespace ExampleInteractiveTool
{
    /// <summary>
    /// Generate unique short identifiers for peptides.
    /// </summary>
    public class UniquePrefixGenerator
    {
        private readonly Dictionary<string, Dictionary<string, List<string>>> _prefixDictionary =
            new Dictionary<string, Dictionary<string, List<string>>>();
        private readonly int _minLength;
        private const char Ellipsis = '…';

        /// <summary>
        /// Create a prefix generator for the given sequences.
        /// </summary>
        /// <param name="seqs">Sequences with modifications.</param>
        /// <param name="minLength">The minimum prefix length to generate.</param>
        public UniquePrefixGenerator(IEnumerable<string> seqs, int minLength)
        {
            _minLength = minLength;

            foreach (var s in seqs)
            {
                if (s != null)
                    AddString(StripModifications(s));
            }
        }

        /// <summary>
        /// Return a unique prefix for the given sequence (which must have been included in the
        /// list of sequences given to the constructor).
        /// </summary>
        public string GetUniquePrefix(string seq)
        {
            if (seq == null)
                return null;
            seq = StripModifications(seq);

            // Get sequences that match this prefix, and ones that match both prefix and suffix.
            var prefix = GetPrefix(seq);
            var matchingPrefixes = _prefixDictionary[prefix];
            var suffix = GetSuffix(seq);
            var matchingPrefixAndSuffix = matchingPrefixes[suffix];

            // If there is only one sequence with this prefix, return the prefix.
            if (matchingPrefixes.Count == 1 && matchingPrefixAndSuffix.Count == 1)
                return prefix;

            // If there is only one sequence with this prefix/suffix, return the combo.
            if (matchingPrefixAndSuffix.Count == 1)
                return (seq.Length > _minLength * 2) ? prefix + Ellipsis + suffix : seq;

            // If the matching sequences can be differentiated by length, use length specifier.
            int matchingLengthCount = 0;
            foreach (var s in matchingPrefixAndSuffix)
            {
                if (s.Length == seq.Length)
                    matchingLengthCount++;
            }
            if (matchingLengthCount == 1)
                return string.Format("{0}({1})", prefix, seq.Length - _minLength); // Not L10N

            // Use ellipses to indicate common parts of matching sequences.
            var matches = new List<string>();
            foreach (var s in matchingPrefixAndSuffix)
            {
                if (!s.Equals(seq))
                    matches.Add(s);
            }
            int lastDifference = _minLength;
            for (int i = _minLength; i < seq.Length; i++)
            {
                // Remove any matches that don't match the current character of this sequence.
                int matchCount = matches.Count;
                for (int j = matchCount - 1; j >= 0; j--)
                {
                    if (matches[j].Length <= i || matches[j][i] != seq[i]) 
                        matches.RemoveAt(j);
                }

                // If we found any non-matching sequences, add the non-matching character to
                // this prefix.
                if (matchCount > matches.Count)
                {
                    if (lastDifference < i)
                        prefix += Ellipsis;
                    lastDifference = i + 1;
                    prefix += seq[i];

                    // If there are no remaining matches, we are done.
                    if (matches.Count == 0)
                        return (i < seq.Length - 1) ? prefix + Ellipsis : prefix;
                }
            }

            // If we got here, then it means that there is something else which matches this identifier's suffix
            // and is longer.  Return either the prefix with the length specifier, or the entire identifier.
            return ShorterOf(string.Format("{0}({1})", prefix, seq.Length), seq); // Not L10N
        }

        private static string ShorterOf(string elided, string original)
        {
            return (elided.Length < original.Length) ? elided : original;
        }

        /// <summary>
        /// Remove modifications from the given sequence, changing the modifications to lower case.
        /// </summary>
        /// <param name="seq">A sequence containing bracketed modifications.</param>
        /// <returns>The sequence with brackets removed and modifications represented using lower case letters.</returns>
        private string StripModifications(string seq)
        {
            var sb = new StringBuilder(seq.Length);
            int index = 0;
            while (true)
            {
                var modificationIndex = seq.IndexOf('[', index);
                if (modificationIndex < 0)
                {
                    sb.Append(seq.Substring(index));
                    return sb.ToString();
                }
                sb.Append(seq.Substring(index, modificationIndex - 1 - index));
                sb.Append(seq.Substring(modificationIndex-1, 1).ToLower());
                index = seq.IndexOf(']', modificationIndex + 1) + 1;
                if (index == 0)
                    return sb.ToString();   // safety
            }
        }

        /// <summary>
        /// Add a sequence to the prefix generator.
        /// </summary>
        /// <param name="seq">Sequence with modifications stripped.</param>
        private void AddString(string seq)
        {
            // Add to dictionary of sequences with this prefix.
            var prefix = GetPrefix(seq);
            Dictionary<string, List<string>> prefixMatches;
            if (!_prefixDictionary.TryGetValue(prefix, out prefixMatches))
                _prefixDictionary[prefix] = prefixMatches = new Dictionary<string, List<string>>();

            // Add to dictionary of sequences with this prefix and suffix.
            var suffix = GetSuffix(seq);
            List<string> prefixSuffixMatches;
            if (!prefixMatches.TryGetValue(suffix, out prefixSuffixMatches))
                prefixMatches[suffix] = prefixSuffixMatches = new List<string>();

            // Add to list of sequences that have the same prefix and suffix.
            if (!prefixSuffixMatches.Contains(seq))
                prefixSuffixMatches.Add(seq);
        }

        /// <summary>
        /// Return the first n letters for the given sequence.
        /// </summary>
        private string GetPrefix(string seq)
        {
            return seq.Substring(0, Math.Min(seq.Length, _minLength));
        }

        /// <summary>
        /// Return the last n letters for the given sequence.
        /// </summary>
        private string GetSuffix(string seq)
        {
            return (seq.Length > _minLength)
                ? seq.Substring(Math.Max(_minLength, seq.Length - _minLength))
                : string.Empty;
        }
    }
}
