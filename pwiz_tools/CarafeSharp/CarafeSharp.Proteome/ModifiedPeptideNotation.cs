/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) main.java.ai.AIGear
 *   (get_modified_peptide, get_modified_peptide_diann, get_modified_peptide_encyclopedia,
 *   get_modified_peptide_skyline, get_skyline_modification_position, format_skyline_residue)
 *   and main.java.input.CModification (load_UniMods)
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
using System.Linq;
using System.Text;
using pwiz.CarafeSharp.Core;

namespace pwiz.CarafeSharp.Proteome
{
    /// <summary>The ModifiedPeptide notation of a Carafe library TSV (<c>-lf_type</c>).</summary>
    public enum ModifiedPeptideStyle
    {
        /// <summary><c>DIA-NN</c> or <c>DIANN</c>: <c>_PEPC[UniMod:4]K_</c>.</summary>
        dia_nn,

        /// <summary><c>EncyclopeDIA</c>: <c>_PEPC[Carbamidomethyl (C)]K_</c>.</summary>
        encyclopedia,

        /// <summary>Any other name: <c>_PEPC[Carbamidomethyl]K_</c>.</summary>
        generic,
    }

    /// <summary>
    /// Carafe's modified-peptide notations, reproduced with their quirks. Each is built from
    /// the peptidoform's modifications in order, replacing the modified residue's letter:
    /// <list type="bullet">
    /// <item>DIA-NN: Oxidation, Carbamidomethyl and Phospho become <c>M[UniMod:35]</c>,
    /// <c>C[UniMod:4]</c> and <c>S[UniMod:21]</c>; protein N-term acetyl is prefixed as
    /// <c>[UniMod:1]</c>; every other modification is written with parentheses,
    /// <c>N(UniMod:7)</c>, as Carafe's <c>psi_name_site2site_unimod_acc</c> map has it.</item>
    /// <item>EncyclopeDIA: <c>M[Oxidation (M)]</c>, <c>S[Phosphorylation (ST)]</c>, else
    /// <c>N[Deamidated (N)]</c>.</item>
    /// <item>Generic: <c>M[Oxidation]</c>, <c>S[Phospho]</c>, else the bare Unimod title in
    /// place of the residue letter.</item>
    /// <item>Skyline (.blib): the residue followed by the signed sum of the modifications'
    /// <c>top_modifications.tsv</c> masses on it, <c>C[+57.02146372057]</c>; protein N-term
    /// acetyl is placed on residue 1.</item>
    /// </list>
    /// A later modification on an already modified residue replaces its notation, as in Carafe
    /// (protein N-term acetyl with a residue-1 modification keeps only the latter in the TSV).
    /// A modification Carafe cannot place, such as a protein N-term acetyl in the EncyclopeDIA
    /// or generic notation (Carafe fails on index -1), is not supported.
    /// </summary>
    public static class ModifiedPeptideNotation
    {
        private const string PROTEIN_N_TERM_ACETYL = @"Acetyl@Protein_N-term";

        /// <summary>
        /// The notation for an <c>-lf_type</c> value, compared as Carafe does, ignoring case.
        /// </summary>
        public static ModifiedPeptideStyle GetStyle(string libraryFormat)
        {
            if (string.Equals(libraryFormat, @"DIANN", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(libraryFormat, @"DIA-NN", StringComparison.OrdinalIgnoreCase))
            {
                return ModifiedPeptideStyle.dia_nn;
            }
            return string.Equals(libraryFormat, @"EncyclopeDIA", StringComparison.OrdinalIgnoreCase)
                ? ModifiedPeptideStyle.encyclopedia
                : ModifiedPeptideStyle.generic;
        }

        /// <summary>The TSV ModifiedPeptide value, <c>_</c>-delimited.</summary>
        public static string Format(PeptideIsoform isoform, ModifiedPeptideStyle style)
        {
            string sequence = isoform.Sequence;
            if (isoform.Modifications.Count == 0)
                return @"_" + sequence + @"_";
            var residues = sequence.Select(c => c.ToString()).ToArray();
            foreach (var site in isoform.Modifications)
            {
                var modification = site.Modification;
                string name = AlphabaseName(modification);
                int index = site.Position - 1;
                if (style == ModifiedPeptideStyle.dia_nn && name == PROTEIN_N_TERM_ACETYL)
                {
                    residues[0] = @"[UniMod:1]" + residues[0];
                    continue;
                }
                if (index < 0 || index >= residues.Length)
                {
                    throw new NotSupportedException(string.Format(
                        @"Carafe cannot write {0} at site {1} of {2} in the {3} notation", name, site.Position, sequence, style));
                }
                residues[index] = ResidueNotation(modification, name, style);
            }
            return @"_" + string.Concat(residues) + @"_";
        }

        /// <summary>
        /// The BiblioSpec <c>peptideModSeq</c> and <c>Modifications</c> rows Carafe's Skyline
        /// library writes for a peptidoform.
        /// </summary>
        public static string FormatSkyline(PeptideIsoform isoform, out List<SkylineModification> modifications)
        {
            string sequence = isoform.Sequence;
            modifications = new List<SkylineModification>(isoform.Modifications.Count);
            if (isoform.Modifications.Count == 0)
                return sequence;
            var deltas = new decimal[sequence.Length];
            var modified = new bool[sequence.Length];
            foreach (var site in isoform.Modifications)
            {
                var modification = site.Modification;
                int position = GetSkylinePosition(AlphabaseName(modification), site.Position);
                if (position < 1 || position > sequence.Length)
                {
                    throw new NotSupportedException(string.Format(
                        @"Carafe cannot write {0} at Skyline position {1} of {2}", modification.Name, position, sequence));
                }
                deltas[position - 1] += modification.PreferredMass;
                modified[position - 1] = true;
                modifications.Add(new SkylineModification(position,
                    double.Parse(modification.PreferredMassText, NumberStyles.Float, CultureInfo.InvariantCulture)));
            }
            var builder = new StringBuilder(sequence.Length + 16 * isoform.Modifications.Count);
            for (int i = 0; i < sequence.Length; i++)
            {
                builder.Append(sequence[i]);
                if (modified[i])
                    builder.Append('[').Append(deltas[i] >= 0 ? @"+" : string.Empty).Append(ToPlainString(deltas[i])).Append(']');
            }
            return builder.ToString();
        }

        /// <summary>
        /// <c>get_skyline_modification_position</c>: protein N-term acetyl at site 0 moves to
        /// residue 1; every other site is used as it is.
        /// </summary>
        private static int GetSkylinePosition(string alphabaseName, int site)
        {
            return alphabaseName == PROTEIN_N_TERM_ACETYL && site == 0 ? 1 : site;
        }

        private static string ResidueNotation(CarafeModification modification, string name, ModifiedPeptideStyle style)
        {
            switch (style)
            {
                case ModifiedPeptideStyle.dia_nn:
                    switch (name)
                    {
                        case @"Oxidation@M":
                            return @"M[UniMod:35]";
                        case @"Carbamidomethyl@C":
                            return @"C[UniMod:4]";
                        case @"Phospho@S":
                            return @"S[UniMod:21]";
                        case @"Phospho@T":
                            return @"T[UniMod:21]";
                        case @"Phospho@Y":
                            return @"Y[UniMod:21]";
                    }
                    return string.Format(CultureInfo.InvariantCulture, @"{0}(UniMod:{1})", Site(modification), modification.UnimodAccession);
                case ModifiedPeptideStyle.encyclopedia:
                    switch (name)
                    {
                        case @"Oxidation@M":
                            return @"M[Oxidation (M)]";
                        case @"Carbamidomethyl@C":
                            return @"C[Carbamidomethyl (C)]";
                        case @"Phospho@S":
                            return @"S[Phosphorylation (ST)]";
                        case @"Phospho@T":
                            return @"T[Phosphorylation (ST)]";
                        case @"Phospho@Y":
                            return @"Y[Phosphorylation (Y)]";
                    }
                    return Site(modification) + @"[" + modification.UnimodTitle + @" (" + Site(modification) + @")]";
                default:
                    switch (name)
                    {
                        case @"Oxidation@M":
                            return @"M[Oxidation]";
                        case @"Carbamidomethyl@C":
                            return @"C[Carbamidomethyl]";
                        case @"Phospho@S":
                            return @"S[Phospho]";
                        case @"Phospho@T":
                            return @"T[Phospho]";
                        case @"Phospho@Y":
                            return @"Y[Phospho]";
                    }
                    return modification.UnimodTitle;
            }
        }

        private static string AlphabaseName(CarafeModification modification)
        {
            return modification.AlphabaseName ?? throw new NotSupportedException(string.Format(
                @"Carafe's library generation does not support the modification {0} ({1})", modification.Id, modification.Name));
        }

        /// <summary>The residue letter Carafe's Unimod-derived maps key the modification by.</summary>
        private static string Site(CarafeModification modification)
        {
            return modification.Target.HasValue ? modification.Target.Value.ToString() : string.Empty;
        }

        /// <summary><c>BigDecimal.stripTrailingZeros().toPlainString()</c>.</summary>
        private static string ToPlainString(decimal value)
        {
            return value.ToString(@"0.############################", CultureInfo.InvariantCulture);
        }
    }
}
