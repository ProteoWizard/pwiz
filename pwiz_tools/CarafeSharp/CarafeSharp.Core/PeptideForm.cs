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
using System.Text;

namespace pwiz.CarafeSharp.Core
{
    /// <summary>
    /// A peptide sequence with its modifications, in alphabase notation: each modification is
    /// an alphabase name (<c>Carbamidomethyl@C</c>) placed at a site where 0 is the N-terminus,
    /// 1..n are residues and -1 is the C-terminus.
    /// </summary>
    public sealed class PeptideForm
    {
        /// <summary>
        /// Builds a form from alphabase's <c>mods</c> and <c>mod_sites</c> column values,
        /// both <c>;</c>-separated and possibly empty.
        /// </summary>
        public static PeptideForm FromAlphabase(string sequence, string modsText, string modSitesText)
        {
            var names = string.IsNullOrEmpty(modsText)
                ? Array.Empty<string>()
                : modsText.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            var sites = string.IsNullOrEmpty(modSitesText)
                ? Array.Empty<int>()
                : Array.ConvertAll(modSitesText.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries),
                    s => int.Parse(s, CultureInfo.InvariantCulture));
            return new PeptideForm(sequence, names, sites);
        }

        public PeptideForm(string sequence, IReadOnlyList<string> modNames = null, IReadOnlyList<int> modSites = null)
        {
            if (string.IsNullOrEmpty(sequence))
                throw new ArgumentException(@"Empty peptide sequence.", nameof(sequence));
            foreach (char aa in sequence)
            {
                if (aa < 'A' || aa > 'Z')
                    throw new ArgumentException(string.Format(@"Invalid residue '{0}' in {1}.", aa, sequence), nameof(sequence));
            }
            modNames = modNames ?? Array.Empty<string>();
            modSites = modSites ?? Array.Empty<int>();
            if (modNames.Count != modSites.Count)
                throw new ArgumentException(@"Modification names and sites differ in length.", nameof(modSites));
            foreach (int site in modSites)
            {
                if (site < -1 || site > sequence.Length)
                    throw new ArgumentException(string.Format(@"Modification site {0} is outside {1}.", site, sequence), nameof(modSites));
            }
            Sequence = sequence;
            ModNames = modNames;
            ModSites = modSites;
        }

        public string Sequence { get; }

        public int Length
        {
            get { return Sequence.Length; }
        }

        public IReadOnlyList<string> ModNames { get; }

        public IReadOnlyList<int> ModSites { get; }

        /// <summary>The <c>;</c>-joined alphabase <c>mods</c> column value.</summary>
        public string ModsText
        {
            get { return string.Join(@";", ModNames); }
        }

        /// <summary>The <c>;</c>-joined alphabase <c>mod_sites</c> column value.</summary>
        public string ModSitesText
        {
            get { return string.Join(@";", ModSites); }
        }

        public override string ToString()
        {
            var sb = new StringBuilder(Sequence);
            if (ModNames.Count > 0)
                sb.Append('|').Append(ModsText).Append('|').Append(ModSitesText);
            return sb.ToString();
        }
    }
}
