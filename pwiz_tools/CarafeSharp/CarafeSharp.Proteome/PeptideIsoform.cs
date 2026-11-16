/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) main.java.ai.AIGear.convert_modification
 *   and main.java.input.ModificationUtils.getModificationString
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
using System.Linq;
using pwiz.CarafeSharp.Core;

namespace pwiz.CarafeSharp.Proteome
{
    /// <summary>
    /// One peptidoform of Carafe's library: a sequence with its modifications in the order
    /// compomics holds them (the variable ones in combination order, then the fixed ones), and
    /// the mass compomics computes from them, to the bit.
    /// </summary>
    public sealed class PeptideIsoform
    {
        public PeptideIsoform(string sequence, IReadOnlyList<ModificationSite> modifications)
        {
            Sequence = sequence;
            Modifications = modifications;
            Mass = CompomicsMasses.PeptideMass(sequence, modifications.Select(m => m.Modification.Mass));
        }

        public string Sequence { get; }

        public IReadOnlyList<ModificationSite> Modifications { get; }

        /// <summary>compomics <c>Peptide.getMass</c>.</summary>
        public double Mass { get; }

        /// <summary>
        /// The precursor m/z Carafe's Java computes, <c>(mass + z * proton) / z</c> with the
        /// compomics proton.
        /// </summary>
        public double GetMz(int charge)
        {
            return (Mass + charge * CompomicsMasses.PROTON) / charge;
        }

        /// <summary>
        /// The alphabase form Carafe hands its Python: <c>mods</c> are the alphabase names and
        /// <c>mod_sites</c> the compomics sites, in the same order. A modification Carafe's
        /// library generation has no alphabase name for is not supported, as Carafe stops on it.
        /// </summary>
        public PeptideForm ToAlphabase()
        {
            var names = new string[Modifications.Count];
            var sites = new int[Modifications.Count];
            for (int i = 0; i < names.Length; i++)
            {
                var modification = Modifications[i].Modification;
                names[i] = modification.AlphabaseName ?? throw new NotSupportedException(string.Format(
                    @"Carafe's library generation does not support the modification {0} ({1})", modification.Id, modification.Name));
                sites[i] = Modifications[i].Position;
            }
            return new PeptideForm(Sequence, names, sites);
        }

        public override string ToString()
        {
            return Sequence + (Modifications.Count > 0 ? @"|" + string.Join(@";", Modifications) : string.Empty);
        }
    }
}
