/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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

using System;
using System.Collections.Generic;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// Collapses per-observation experiment-scope values into the one record per DISTINCT
    /// entry_id that <see cref="FdrExperimentSidecar"/> writes, and ENFORCES that the collapse
    /// is lossless.
    ///
    /// <para>The whole scope split rests on one claim: the four experiment-scope columns are a
    /// property of the precursor for the whole analysis, so every observation of a given
    /// entry_id carries the same four values and storing them once loses nothing. That claim is
    /// true of the code as written - one experiment competition assigns one value per entry_id,
    /// and <c>ProteinFdr.PropagateProteinQvalues</c> assigns one protein q per peptide across
    /// every file - but it is a claim about a computation two stages upstream, and it is exactly
    /// the kind of claim that stops being true quietly.</para>
    ///
    /// <para>So this asserts it on every observation rather than trusting it. A first-wins
    /// collapse over values that had silently diverged would write one file's number for all of
    /// them and report q-values that no run computed - a wrong answer that passes every
    /// self-consistency gate, because both routes would collapse the same way. Disagreement
    /// throws instead: the run dies with the entry_id and both values named.</para>
    /// </summary>
    public sealed class FdrExperimentAccumulator
    {
        private readonly Dictionary<uint, FdrExperimentRecord> _byEntryId;

        public FdrExperimentAccumulator()
        {
            _byEntryId = new Dictionary<uint, FdrExperimentRecord>();
        }

        /// <summary>Distinct entry_ids accumulated so far - the record count of the file.</summary>
        public int Count
        {
            get { return _byEntryId.Count; }
        }

        /// <summary>The accumulated records, for <see cref="FdrExperimentSidecar.Write"/>.</summary>
        public IReadOnlyDictionary<uint, FdrExperimentRecord> Records
        {
            get { return _byEntryId; }
        }

        /// <summary>
        /// Fold one observation's experiment-scope values in. First sighting of an entry_id
        /// stores them; every later sighting must agree exactly.
        /// </summary>
        public void Add(uint entryId, double experimentPrecursorQvalue,
            double experimentPeptideQvalue, double experimentProteinQvalue,
            double experimentAggregateScore)
        {
            var incoming = new FdrExperimentRecord(entryId, experimentPrecursorQvalue,
                experimentPeptideQvalue, experimentProteinQvalue, experimentAggregateScore);
            if (!_byEntryId.TryGetValue(entryId, out var existing))
            {
                _byEntryId[entryId] = incoming;
                return;
            }
            // Bitwise equality, not a tolerance. These are copies of one computed value, so any
            // difference at all means the premise of the collapse has failed, and a tolerance
            // would only decide how much of a wrong answer to accept.
            if (existing.ExperimentPrecursorQvalue.Equals(incoming.ExperimentPrecursorQvalue) &&
                existing.ExperimentPeptideQvalue.Equals(incoming.ExperimentPeptideQvalue) &&
                existing.ExperimentProteinQvalue.Equals(incoming.ExperimentProteinQvalue) &&
                existing.ExperimentAggregateScore.Equals(incoming.ExperimentAggregateScore))
            {
                return;
            }
            throw new InvalidOperationException(string.Format(
                @"Experiment-scope values disagree across observations of entry_id {0}: " +
                @"precursor_q {1} vs {2}, peptide_q {3} vs {4}, protein_q {5} vs {6}, " +
                @"aggregate_score {7} vs {8}. These columns are written once per entry_id " +
                @"because they are experiment-scope; a disagreement means they are not.",
                entryId,
                existing.ExperimentPrecursorQvalue, incoming.ExperimentPrecursorQvalue,
                existing.ExperimentPeptideQvalue, incoming.ExperimentPeptideQvalue,
                existing.ExperimentProteinQvalue, incoming.ExperimentProteinQvalue,
                existing.ExperimentAggregateScore, incoming.ExperimentAggregateScore));
        }

        /// <summary>
        /// Set one entry's protein q-value, for the producer that folds in the run's experiment
        /// q-values during its score pass and only learns the picked-protein q afterwards - the
        /// score pass supplies the 1.0 default and this replaces it. An entry_id this
        /// accumulator never saw is ignored rather than added: a protein q on its own is not a
        /// record, and inventing one would put an entry in the file with default q-values that
        /// no competition assigned.
        ///
        /// <para>Per entry rather than per map, because the caller resolves protein q one INPUT
        /// FILE at a time and there are as many files as the analysis has runs; a whole-map
        /// sweep per file would be O(files x distinct entries) for a result that is
        /// O(entries). Repeated sightings overwrite: protein q is looked up by modified
        /// sequence, and an entry_id names one precursor of one peptide in every file, so every
        /// file resolves the same value.</para>
        /// </summary>
        public void SetProteinQvalue(uint entryId, double proteinQvalue)
        {
            if (!_byEntryId.TryGetValue(entryId, out var r))
                return;
            _byEntryId[entryId] = new FdrExperimentRecord(r.EntryId,
                r.ExperimentPrecursorQvalue, r.ExperimentPeptideQvalue,
                proteinQvalue, r.ExperimentAggregateScore);
        }
    }
}
