/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
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
using pwiz.Osprey.Core;

namespace pwiz.Osprey.FDR.Reconciliation
{
    /// <summary>
    /// The target precursors the blib reports, decided from first-pass experiment-level q-values
    /// one run at a time: the same two gates as the blib writer (SecondPassFdrTask). A peptide is
    /// eligible when its experiment q at the configured <see cref="FdrLevel"/> passes; each of
    /// its charge states is reported when its experiment precursor q passes, and a peptide with
    /// no passing charge state reports its best one.
    ///
    /// <para>The cross-run reconciliation set of a second pass that carries first-pass q-values
    /// forward (transfer). There every experiment-wide value is the first pass's and nothing
    /// re-competes - reconciliation improves only each run's peak - so no precursor outside this
    /// set can reach the blib, and reconciling one is work without a consumer - and the set that admitted any
    /// precursor passing at any level in any run grew with the run count (1.37x the reported set
    /// at 3 SEA-AD runs, 1.55x at 16).</para>
    ///
    /// <para>Experiment-level q-values are experiment-wide, so each entry decides its own
    /// peptide and charge state and the gates fold over the runs in one walk; the blib writer's
    /// two walks give the same set.</para>
    /// </summary>
    public sealed class ExperimentOutputSet
    {
        private readonly FdrLevel _fdrLevel;
        private readonly double _experimentFdr;
        private readonly HashSet<string> _passingPeptides = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<(string ModifiedSequence, byte Charge)> _passingCharges =
            new HashSet<(string ModifiedSequence, byte Charge)>();
        private readonly Dictionary<string, (byte Charge, double Qvalue)> _bestChargeByPeptide =
            new Dictionary<string, (byte Charge, double Qvalue)>(StringComparer.Ordinal);
        private readonly Dictionary<(string ModifiedSequence, byte Charge), uint> _baseIdByPrecursor =
            new Dictionary<(string ModifiedSequence, byte Charge), uint>();

        public ExperimentOutputSet(FdrLevel fdrLevel, double experimentFdr)
        {
            _fdrLevel = fdrLevel;
            _experimentFdr = experimentFdr;
        }

        /// <summary>Fold one run's first-pass entries in. Decoys are skipped.</summary>
        public void AddFile(IReadOnlyList<FdrEntry> entries)
        {
            if (entries == null)
                return;
            foreach (var entry in entries)
            {
                Add(entry.EntryId, entry.IsDecoy, entry.ModifiedSequence, entry.Charge,
                    entry.ExperimentPrecursorQvalue, entry.EffectiveExperimentQvalue(_fdrLevel));
            }
        }

        /// <summary>
        /// Fold one entry in from its identity and first-pass experiment q-values, for a caller
        /// holding those apart from the entry (the second pass, whose entries carry the
        /// identities and whose first-pass experiment records carry the q-values). Decoys are
        /// skipped. <paramref name="effectiveExperimentQvalue"/> is the q at the configured
        /// <see cref="FdrLevel"/>.
        /// </summary>
        public void Add(uint entryId, bool isDecoy, string modifiedSequence, byte charge,
            double experimentPrecursorQvalue, double effectiveExperimentQvalue)
        {
            if (isDecoy || string.IsNullOrEmpty(modifiedSequence))
                return;
            var precursor = (modifiedSequence, charge);
            _baseIdByPrecursor[precursor] = entryId & 0x7FFFFFFFu;
            if (effectiveExperimentQvalue <= _experimentFdr)
                _passingPeptides.Add(modifiedSequence);
            if (experimentPrecursorQvalue <= _experimentFdr)
                _passingCharges.Add(precursor);
            if (!_bestChargeByPeptide.TryGetValue(modifiedSequence, out var best) || experimentPrecursorQvalue < best.Qvalue)
                _bestChargeByPeptide[modifiedSequence] = (charge, experimentPrecursorQvalue);
        }

        /// <summary>
        /// The reported precursors, as (modified sequence, charge) for gap fill and as (base id,
        /// charge) for the planner, which pairs decoys by base id.
        /// </summary>
        public void Finish(out HashSet<(string ModifiedSequence, byte Charge)> precursors,
            out HashSet<(uint, byte)> baseIds)
        {
            precursors = new HashSet<(string ModifiedSequence, byte Charge)>();
            foreach (var precursor in _passingCharges)
            {
                if (_passingPeptides.Contains(precursor.ModifiedSequence))
                    precursors.Add(precursor);
            }
            foreach (string peptide in _passingPeptides)
            {
                if (_bestChargeByPeptide.TryGetValue(peptide, out var best) && best.Qvalue > _experimentFdr)
                    precursors.Add((peptide, best.Charge));
            }
            baseIds = new HashSet<(uint, byte)>();
            foreach (var precursor in precursors)
                baseIds.Add((_baseIdByPrecursor[precursor], precursor.Charge));
        }
    }
}
