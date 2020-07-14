﻿/*
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
using System.Linq;
using System.Text;

namespace pwiz.Skyline.Controls.Graphs
{
    /// <summary>
    /// Generate unique short identifiers for peptides and custom molecules.
    /// </summary>
    public class UniquePrefixGenerator
    {
        private readonly Dictionary<string, Dictionary<string, List<string>>> _peptidePrefixDictionary =
            new Dictionary<string, Dictionary<string, List<string>>>();
        private readonly Dictionary<string, Dictionary<string, List<string>>> _customIonPrefixDictionary =
            new Dictionary<string, Dictionary<string, List<string>>>();
        private readonly int _minLength;
        private readonly int _commonStartLength; // For non-peptide names, remove any common leading characters
        private const char Ellipsis = '…';

        /// <summary>
        /// Create a prefix generator for the given list of sequences and molecule names.
        /// </summary>
        /// <param name="names">Pairs with text and flag indicating whether text is a peptide sequence.</param>
        /// <param name="minLength">The minimum prefix length to generate.</param>
        public UniquePrefixGenerator(IEnumerable<Tuple<string, bool>> names, int minLength)
        {
            _minLength = minLength;
            var namelist = names.ToList();

            // Find longest common leading string in non-peptide names
            List<string> molecules = namelist.Where(n => n != null && !n.Item2 && n.Item1 != null).Select(s => s.Item1).ToList();
            if (molecules.Count > 1)
            {
                string commonLead = molecules.First(x => x.Length == molecules.Select(y => y.Length).Min()); // Find shortest in list
                while (commonLead.Length > 0 && !molecules.All(s => s.StartsWith(commonLead)))
                    commonLead = commonLead.Substring(0, commonLead.Length - 1);
                _commonStartLength = (commonLead.Length > minLength) ? commonLead.Length : 0; // Very short common leads are OK
                // In case of ["foo bar C10", "foo bar C12"] we'd like to just drop "foo bar " and get ["C10", "C12"]
                if (_commonStartLength > minLength && (commonLead.LastIndexOf(' ') >= _commonStartLength - minLength))
                    _commonStartLength = commonLead.LastIndexOf(' ')+1;
            }
            else
            {
                _commonStartLength = 0;
            }

            foreach (var s in namelist)
            {
                if ((s != null) && (s.Item1 != null))
                {
                    bool isPeptide = s.Item2;
                    AddString(isPeptide ? StripModifications(s.Item1) : s.Item1.Substring(_commonStartLength), isPeptide);
                }
            }
        }

        private string ShorterOf(string elided, string original)
        {
            return (elided.Length < original.Length) ? elided : original;
        }

        /// <summary>
        /// Return a unique prefix for the given identifier (which must have been included in the
        /// list of identifiers given to the constructor).
        /// </summary>
        public string GetUniquePrefix(string identifier, bool isPeptideSeq)
        {
            if (identifier == null)
                return null;
            if (isPeptideSeq)
                identifier = StripModifications(identifier);
            else
                identifier = identifier.Substring(_commonStartLength);

            // Get sequences that match this prefix, and ones that match both prefix and suffix.
            var prefix = GetPrefix(identifier);
            var matchingPrefixes = isPeptideSeq ? _peptidePrefixDictionary[prefix] : _customIonPrefixDictionary[prefix];
            var suffix = GetSuffix(identifier);
            var matchingPrefixAndSuffix = matchingPrefixes[suffix];

            // If there is only one sequence with this prefix, return the prefix (unless the identifer is already short enough).
            if (matchingPrefixes.Count == 1 && matchingPrefixAndSuffix.Count == 1)
                return ShorterOf((identifier.Length > (_minLength * 2)+1) ? prefix + Ellipsis : identifier, identifier);

            // If there is only one sequence with this prefix/suffix, return the combo.
            if (matchingPrefixAndSuffix.Count == 1)
                return ShorterOf((identifier.Length > (_minLength*2)+1) ? prefix + Ellipsis + suffix : identifier, identifier);

            // If the matching sequences can be differentiated by length, use length specifier.
            int matchingLengthCount = 0;
            foreach (var s in matchingPrefixAndSuffix)
            {
                if (s.Length == identifier.Length)
                    matchingLengthCount++;
            }
            if (matchingLengthCount == 1)
                return ShorterOf(string.Format(@"{0}({1})", prefix, identifier.Length - _minLength), identifier);

            // Use ellipses to indicate common parts of matching sequences.
            var matches = new List<string>();
            foreach (var s in matchingPrefixAndSuffix)
            {
                if (!s.Equals(identifier))
                    matches.Add(s);
            }
            int lastDifference = _minLength;
            for (int i = _minLength; i < identifier.Length; i++)
            {
                // Remove any matches that don't match the current character of this sequence.
                int matchCount = matches.Count;
                for (int j = matchCount - 1; j >= 0; j--)
                {
                    if (matches[j].Length <= i || matches[j][i] != identifier[i])
                        matches.RemoveAt(j);
                }

                // If we found any non-matching sequences, add the non-matching character to
                // this prefix.
                if (matchCount > matches.Count)
                {
                    if (lastDifference < i)
                        prefix += ((i > lastDifference+1) ? Ellipsis : identifier[lastDifference]);  // silly to replace single char with ellipsis
                    lastDifference = i + 1;
                    prefix += identifier[i];

                    // If there are no remaining matches, we are done.
                    if (matches.Count == 0)
                        return ShorterOf( (i < identifier.Length - 1) ? prefix + Ellipsis : prefix, identifier);
                }
            }

            // If we got here, then it means that there is something else which matches this identifier's suffix
            // and is longer.  Return either the prefix with the length specifier, or the entire identifier.
            return ShorterOf(string.Format(@"{0}({1})", prefix, identifier.Length), identifier);
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

                if (modificationIndex > index)
                {
                    sb.Append(seq.Substring(index, modificationIndex - 1 - index));
                }
                sb.Append(seq.Substring(modificationIndex-1, 1).ToLower());
                index = seq.IndexOf(']', modificationIndex + 1) + 1;
                if (index == 0)
                    return sb.ToString();   // safety
            }
        }

        /// <summary>
        /// Add a string to the prefix generator.
        /// </summary>
        /// <param name="name">Sequence with modifications stripped, or molecule name.</param>
        /// <param name="isSequence">if true, treat name as a peptide sequence</param>
        private void AddString(string name, bool isSequence)
        {
            // Add to dictionary of sequences with this prefix.
            var prefix = GetPrefix(name);
            Dictionary<string, List<string>> prefixMatches;
            var dict = isSequence ? _peptidePrefixDictionary : _customIonPrefixDictionary;
            if (!dict.TryGetValue(prefix, out prefixMatches))
               dict[prefix] = prefixMatches = new Dictionary<string, List<string>>();

            // Add to dictionary of sequences with this prefix and suffix.
            var suffix = GetSuffix(name);
            List<string> prefixSuffixMatches;
            if (!prefixMatches.TryGetValue(suffix, out prefixSuffixMatches))
                prefixMatches[suffix] = prefixSuffixMatches = new List<string>();

            // Add to list of sequences that have the same prefix and suffix.
            if (!prefixSuffixMatches.Contains(name))
                prefixSuffixMatches.Add(name);
        }

        /// <summary>
        /// Return the first n letters for the given sequence or molecule.
        /// </summary>
        private string GetPrefix(string name)
        {
            return name.Substring(0, Math.Min(name.Length, _minLength));
        }

        /// <summary>
        /// Return the last n letters for the given sequence or molecule.
        /// </summary>
        private string GetSuffix(string name)
        {
            return (name.Length > _minLength)
                ? name.Substring(Math.Max(_minLength, name.Length - _minLength))
                : string.Empty;
        }
    }
}
