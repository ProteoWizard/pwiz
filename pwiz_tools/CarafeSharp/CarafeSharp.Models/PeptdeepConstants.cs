/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) src/main/resources/py/v2/models.py,
 *   itself extracted from AlphaPeptDeep (https://github.com/MannLabs/alphapeptdeep), Apache-2.0
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

namespace pwiz.CarafeSharp.Models
{
    /// <summary>
    /// The fixed vocabulary the pretrained AlphaPeptDeep models were trained with. Changing any
    /// of these breaks compatibility with the published weights.
    /// </summary>
    public static class PeptdeepConstants
    {
        /// <summary>
        /// Element order of the 109-wide modification feature vector. The first six are fed to
        /// the network as raw counts; the rest go through a learned 103-to-2 projection.
        /// </summary>
        public static readonly string[] MOD_ELEMENTS =
        {
            @"C", @"H", @"N", @"O", @"P", @"S",
            @"B", @"F", @"I", @"K", @"U", @"V", @"W", @"X", @"Y",
            @"Ac", @"Ag", @"Al", @"Am", @"Ar", @"As", @"At", @"Au", @"Ba", @"Be", @"Bi", @"Bk", @"Br",
            @"Ca", @"Cd", @"Ce", @"Cf", @"Cl", @"Cm", @"Co", @"Cr", @"Cs", @"Cu", @"Dy", @"Er", @"Es",
            @"Eu", @"Fe", @"Fm", @"Fr", @"Ga", @"Gd", @"Ge", @"He", @"Hf", @"Hg", @"Ho", @"In", @"Ir",
            @"Kr", @"La", @"Li", @"Lr", @"Lu", @"Md", @"Mg", @"Mn", @"Mo", @"Na", @"Nb", @"Nd", @"Ne",
            @"Ni", @"No", @"Np", @"Os", @"Pa", @"Pb", @"Pd", @"Pm", @"Po", @"Pr", @"Pt", @"Pu", @"Ra",
            @"Rb", @"Re", @"Rh", @"Rn", @"Ru", @"Sb", @"Sc", @"Se", @"Si", @"Sm", @"Sn", @"Sr", @"Ta",
            @"Tb", @"Tc", @"Te", @"Th", @"Ti", @"Tl", @"Tm", @"Xe", @"Yb", @"Zn", @"Zr",
            @"2H", @"13C", @"15N", @"18O", @"?",
        };

        public static readonly int MOD_FEATURE_SIZE = MOD_ELEMENTS.Length;

        /// <summary>Number of leading mod features passed through unprojected.</summary>
        public const int MOD_FIX_FIRST_K = 6;

        /// <summary>Width of the modification embedding (6 raw + 2 projected).</summary>
        public const int MOD_HIDDEN = 8;

        /// <summary>A-Z plus the 0 padding index.</summary>
        public const int AA_EMBEDDING_SIZE = 27;

        public const int MAX_INSTRUMENT_NUM = 8;

        /// <summary>
        /// Positional encoding length; a peptide plus its two terminal pad tokens must fit.
        /// </summary>
        public const int MAX_SEQUENCE_LENGTH = 200;

        public const double CHARGE_FACTOR = 0.1;

        public const double NCE_FACTOR = 0.01;

        /// <summary>Predicted relative intensities below this are set to 0.</summary>
        public const float MIN_INTENSITY = 1e-4f;

        /// <summary>Fragment types the MS2 network emits, in column order.</summary>
        public static readonly string[] CHARGED_FRAG_TYPES =
        {
            @"b_z1", @"b_z2", @"y_z1", @"y_z2",
            @"b_modloss_z1", @"b_modloss_z2", @"y_modloss_z1", @"y_modloss_z2",
        };

        /// <summary>The non-modloss columns: b_z1, b_z2, y_z1, y_z2.</summary>
        public const int NUM_NON_MODLOSS_FRAG_TYPES = 4;

        public const int NUM_MODLOSS_FRAG_TYPES = 4;

        /// <summary>Instrument families with a trained index, in index order.</summary>
        private static readonly string[] INSTRUMENTS = { @"QE", @"Lumos", @"timsTOF", @"SciexTOF", @"ThermoTOF" };

        /// <summary>
        /// peptdeep's instrument grouping (<c>settings['model_mgr']['instrument_group']</c>),
        /// keyed by upper-case instrument name. Anything not listed maps to Lumos.
        /// </summary>
        private static readonly Dictionary<string, string> INSTRUMENT_GROUPS = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { @"THERMOTOF", @"ThermoTOF" },
            { @"ASTRAL", @"Lumos" },
            { @"LUMOS", @"Lumos" },
            { @"QE", @"QE" },
            { @"TIMSTOF", @"timsTOF" },
            { @"SCIEXTOF", @"SciexTOF" },
            { @"FUSION", @"Lumos" },
            { @"ECLIPSE", @"Lumos" },
            { @"VELOS", @"Lumos" },
            { @"ELITE", @"Lumos" },
            { @"ORBITRAPTRIBRID", @"Lumos" },
            { @"THERMOTRIBRID", @"Lumos" },
            { @"QE+", @"QE" },
            { @"QEHF", @"QE" },
            { @"QEHFX", @"QE" },
            { @"EXPLORIS", @"QE" },
            { @"EXPLORIS480", @"QE" },
        };

        private const string DEFAULT_INSTRUMENT_GROUP = @"Lumos";

        /// <summary>
        /// The instrument family peptdeep's ModelManager assigns to an instrument name
        /// (for example Eclipse and Astral become Lumos, Exploris becomes QE).
        /// </summary>
        public static string GetInstrumentGroup(string instrumentName)
        {
            if (string.IsNullOrEmpty(instrumentName))
                return DEFAULT_INSTRUMENT_GROUP;
            return INSTRUMENT_GROUPS.TryGetValue(instrumentName.ToUpperInvariant(), out string group)
                ? group
                : DEFAULT_INSTRUMENT_GROUP;
        }

        /// <summary>
        /// The embedding index for an instrument name: its family's position in the trained
        /// instrument list, or the reserved unknown index (7) for a family outside it.
        /// </summary>
        public static int GetInstrumentIndex(string instrumentName)
        {
            string group = GetInstrumentGroup(instrumentName);
            for (int i = 0; i < INSTRUMENTS.Length; i++)
            {
                if (string.Equals(INSTRUMENTS[i], group, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return MAX_INSTRUMENT_NUM - 1;
        }
    }
}
