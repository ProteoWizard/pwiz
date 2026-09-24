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

using System.Collections.Generic;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.FDR
{
    /// <summary>
    /// The best-of-runs floors experiment-scope q-values are raised to: for each entry_id, and
    /// for each <c>(ModifiedSequence, IsDecoy)</c> peptide identity, the minimum over runs of the
    /// combined run q-value. Folded over every run, applied per row - the two halves of
    /// <see cref="PercolatorEngine.ClampExperimentQToBestRun"/>, held together as one object so a
    /// caller that folds in one place and applies in another does not carry two loose maps
    /// between them.
    ///
    /// <para>Both are needed and neither derives from the other: experiment precursor q floors
    /// against the per-entry minimum, experiment peptide q against the per-peptide one. Both key
    /// on the target/decoy-specific identity, so a target can never inherit its paired decoy's
    /// good run. <see cref="PercolatorEngine.AccumulateExperimentQFloors"/> documents why the
    /// floor exists at all.</para>
    ///
    /// <para>O(distinct), not O(observations): 45,724 precursors at 257 runs, against the
    /// 137 M entries those runs hold. That is what lets a streamed Stage 7 fold this over runs
    /// it rebuilds and drops one at a time rather than holding the pool.</para>
    /// </summary>
    public sealed class ExperimentQFloors
    {
        private readonly Dictionary<uint, double> _minRunQByEntryId;
        private readonly Dictionary<(string ModifiedSequence, bool IsDecoy), double> _minRunQByPeptide;
        private readonly Dictionary<uint, (string ModifiedSequence, bool IsDecoy)> _peptideByEntryId;

        public ExperimentQFloors()
        {
            _minRunQByEntryId = new Dictionary<uint, double>();
            _minRunQByPeptide = new Dictionary<(string ModifiedSequence, bool IsDecoy), double>();
            _peptideByEntryId = new Dictionary<uint, (string ModifiedSequence, bool IsDecoy)>();
        }

        /// <summary>Distinct entry_ids folded so far.</summary>
        public int EntryIdCount
        {
            get { return _minRunQByEntryId.Count; }
        }

        /// <summary>Distinct peptide identities folded so far.</summary>
        public int PeptideCount
        {
            get { return _minRunQByPeptide.Count; }
        }

        /// <summary>
        /// The peptide identity an entry_id was observed under, for counting distinct peptides
        /// rather than the per-precursor records that carry them.
        /// </summary>
        public bool TryGetPeptide(uint entryId, out (string ModifiedSequence, bool IsDecoy) peptide)
        {
            return _peptideByEntryId.TryGetValue(entryId, out peptide);
        }

        /// <summary>Fold ONE run's entries into the floors, identities included.</summary>
        public void Accumulate(IReadOnlyList<FdrEntry> entries)
        {
            PercolatorEngine.AccumulateExperimentQFloors(
                entries, _minRunQByEntryId, _minRunQByPeptide);
            ObserveIdentities(entries);
        }

        /// <summary>
        /// Record which peptide each entry_id belongs to, from entries that carry it. Every
        /// entry_id names one peptide, so the first sighting settles it and later ones agree.
        ///
        /// <para>Taken from the ENTRIES and never from the library, which is the whole point. A
        /// <c>--task SecondPassFDR</c> node loads a library with no GENERATED decoys in it, so
        /// resolving a decoy entry_id through <c>LibraryById</c> answers on the straight route
        /// and returns nothing on the distributed one. Measured on Stellar: 166,680 of 333,404
        /// experiment records differed between the two routes, every one of them a decoy, with
        /// the entry floors agreeing exactly. The survivor pool is built from the same artifacts
        /// on both routes and carries the same identity on both; the library is not.</para>
        ///
        /// <para>A library-decoy dataset cannot catch this - there the decoys come from the
        /// library file and are present on both routes - so only a generated-decoy leg exposes
        /// it.</para>
        /// </summary>
        public void ObserveIdentities(IReadOnlyList<FdrEntry> entries)
        {
            foreach (var e in entries)
            {
                // An entry with no modified sequence has no peptide identity to floor against,
                // which is how the row-wise fold treats it too.
                if (string.IsNullOrEmpty(e.ModifiedSequence) || _peptideByEntryId.ContainsKey(e.EntryId))
                    continue;
                _peptideByEntryId[e.EntryId] = (e.ModifiedSequence, e.IsDecoy);
            }
        }

        /// <summary>
        /// Fold ONE per-run observation into the ENTRY floor, from the two run q-values a
        /// per-file FDR record carries. The streaming half of this type: a caller already
        /// reading those records for another purpose gets the floor for nothing, instead of
        /// re-materialising every run to recover a number that was in its hand at the time.
        ///
        /// <para>The peptide floor cannot be folded here - a per-file record carries no peptide
        /// identity - so it is DERIVED afterwards by
        /// <see cref="DerivePeptideFloors"/>, from identities the survivor walk supplies.</para>
        /// </summary>
        public void Observe(uint entryId, double runPrecursorQvalue, double runPeptideQvalue)
        {
            // FdrLevel.Both: the combined run q the blib ID line and OspreyRunScores.RunQValue
            // both use, so "reported => some run passes at BOTH granularities" is the invariant
            // this floor enforces. See PercolatorEngine.AccumulateExperimentQFloors.
            double runBoth = runPrecursorQvalue > runPeptideQvalue
                ? runPrecursorQvalue
                : runPeptideQvalue;
            if (!_minRunQByEntryId.TryGetValue(entryId, out double cur) || runBoth < cur)
                _minRunQByEntryId[entryId] = runBoth;
        }

        /// <summary>
        /// Derive the PEPTIDE floors from the entry floors already folded, grouping entry_ids
        /// through the identities recorded by <see cref="ObserveIdentities"/>.
        ///
        /// <para>Exact, not an approximation, because <c>min</c> is associative: the minimum over
        /// every row of a peptide equals the minimum, over the entries carrying that peptide, of
        /// each entry's own minimum over its rows. That is what lets the entry floors come from
        /// the per-file records - where they are free - and the identities from the survivor
        /// walk, where it is already happening, with neither having to carry the other.</para>
        ///
        /// <para>Idempotent, and safe after <see cref="Accumulate"/> has already folded the
        /// peptide map row-wise: both reduce the same values under the same key.</para>
        /// </summary>
        public void DerivePeptideFloors()
        {
            _minRunQByPeptide.Clear();
            foreach (var kvp in _minRunQByEntryId)
            {
                if (!_peptideByEntryId.TryGetValue(kvp.Key, out var pkey))
                    continue;
                if (!_minRunQByPeptide.TryGetValue(pkey, out double cur) || kvp.Value < cur)
                    _minRunQByPeptide[pkey] = kvp.Value;
            }
        }

        /// <summary>
        /// This entry's two floors as a pair, for a consumer stamping them onto a record keyed by
        /// entry_id. The peptide half is <see cref="double.NaN"/> when the entry has no known
        /// peptide identity, which a reader must treat as "no peptide floor applies" rather than
        /// as a floor of zero.
        /// </summary>
        public (double Entry, double Peptide) FloorsFor(uint entryId)
        {
            double entryFloor = MinRunQFor(entryId);
            if (!_peptideByEntryId.TryGetValue(entryId, out var pkey))
                return (entryFloor, double.NaN);
            return (entryFloor, MinRunQFor(pkey.ModifiedSequence, pkey.IsDecoy));
        }

        /// <summary>
        /// Raise ONE run's experiment q-values to these floors, returning how many values the
        /// call actually raised.
        /// </summary>
        public int Apply(IReadOnlyList<FdrEntry> entries)
        {
            return PercolatorEngine.ApplyExperimentQFloors(
                entries, _minRunQByEntryId, _minRunQByPeptide);
        }

        /// <summary>
        /// This entry_id's floor, or <see cref="double.NaN"/> when no run contributed one.
        /// NaN rather than 1.0 or 0.0 because the two are different statements: a floor of 0.0
        /// raises nothing and would be indistinguishable from "every run agreed this entry is
        /// certain", while NaN says the fold never saw the entry and a reader must not treat
        /// what it persists as final.
        /// </summary>
        public double MinRunQFor(uint entryId)
        {
            return _minRunQByEntryId.TryGetValue(entryId, out double floor) ? floor : double.NaN;
        }

        /// <summary>
        /// This peptide identity's floor, or <see cref="double.NaN"/> when it is unknown - an
        /// entry with no modified sequence (a Parquet stub read without the column) has no
        /// peptide identity to floor against, which <see cref="Apply"/> treats the same way.
        /// </summary>
        public double MinRunQFor(string modifiedSequence, bool isDecoy)
        {
            if (string.IsNullOrEmpty(modifiedSequence))
                return double.NaN;
            return _minRunQByPeptide.TryGetValue((modifiedSequence, isDecoy), out double floor)
                ? floor
                : double.NaN;
        }
    }
}
