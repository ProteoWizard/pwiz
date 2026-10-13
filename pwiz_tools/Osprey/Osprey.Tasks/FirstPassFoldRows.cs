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
using pwiz.Osprey.IO;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// One run's PRE-compaction first-pass rows, reduced to exactly the scalars the
    /// <c>--model-diagnostics</c> fold reads: identity (modified sequence, charge, entry_id,
    /// is_decoy) from the <c>.scores.parquet</c>, and the SVM score plus the run and experiment
    /// precursor/peptide q-values from the <c>.1st-pass.fdr_scores.bin</c> sidecar and the
    /// experiment records.
    ///
    /// <para>It replaces a full <see cref="FdrEntry"/> stub list per run (issue #4664). That list
    /// decoded eleven parquet columns into one object per row - ~3.8 M rows per run on the 446-run
    /// cohort - to hand the fold four of them, and was rebuilt for every run. This reads four
    /// columns into one array of <see cref="Row"/> structs that is REUSED across runs and grows
    /// geometrically, so it is reallocated a logarithmic number of times over the whole fold
    /// rather than once per run.</para>
    ///
    /// <para><see cref="TryJoinFirstPassSidecar"/> joins the sidecar through
    /// <see cref="FdrScoresSidecar.TryJoinRows"/>, the same walk
    /// <see cref="FdrScoresSidecar.TryRead(string, IList{FdrEntry}, FdrScoresSidecar.Pass, Func{uint, bool}, IReadOnlyDictionary{uint, FdrExperimentRecord})"/>
    /// uses, because the report is compared against a committed regression test and these rows
    /// must fold to the same numbers the stubs did.</para>
    /// </summary>
    public sealed class FirstPassFoldRows
    {
        /// <summary>
        /// One row: the parquet identity, then the first-pass statistics the join lands on it.
        /// A row is created with Score 0 and every q-value 1.0, the defaults a stub carried, so
        /// a row no sidecar record maps to folds exactly as its stub did.
        /// </summary>
        public struct Row
        {
            public uint EntryId;
            public byte Charge;
            public bool IsDecoy;
            public string ModifiedSequence;
            public double Score;
            public double RunPrecursorQvalue;
            public double RunPeptideQvalue;
            public double ExperimentPrecursorQvalue;
            public double ExperimentPeptideQvalue;
        }

        private readonly Dictionary<uint, int> _rowByEntryId = new Dictionary<uint, int>();
        private Row[] _rows = Array.Empty<Row>();
        private int _count;

        /// <summary>Rows read from the current run's parquet.</summary>
        public int Count => _count;

        /// <summary>
        /// Replace the rows with <paramref name="parquetPath"/>'s, in parquet row order, skipping
        /// the row groups <c>LoadFdrStubsFromParquet</c> skips - the two readers share that rule.
        /// Each modified sequence is canonicalized through <paramref name="sequencePool"/> as the
        /// stub loader did, because the fold keeps sequences it has seen and would otherwise keep
        /// one copy per run.
        /// </summary>
        public void Load(string parquetPath, LibraryStringInterner sequencePool)
        {
            _count = 0;
            ParquetScoreCache.ReadFdrStubScalars(parquetPath,
                (entryId, charge, isDecoy, coelutionSum, modifiedSequence, apexRt) =>
                {
                    if (_count == _rows.Length)
                    {
                        // Geometric, as List<T> grows: runs arriving in increasing size would
                        // otherwise reallocate the array for every run.
                        Array.Resize(ref _rows, Math.Max(1024, 2 * _rows.Length));
                    }
                    _rows[_count++] = new Row
                    {
                        EntryId = entryId,
                        Charge = charge,
                        IsDecoy = isDecoy,
                        ModifiedSequence = LibraryStringInterner.Canonicalize(sequencePool, modifiedSequence),
                        Score = 0,
                        RunPrecursorQvalue = 1.0,
                        RunPeptideQvalue = 1.0,
                        ExperimentPrecursorQvalue = 1.0,
                        ExperimentPeptideQvalue = 1.0,
                    };
                },
                StubColumns.SkipCoelutionSum);
        }

        /// <summary>
        /// Join the run's first-pass sidecar onto the loaded rows by entry_id, once per
        /// <see cref="Load"/>. False when the sidecar is unreadable, or carries a record with no
        /// row here that <paramref name="expectedAbsent"/> does not account for - in which case
        /// the values must not be used.
        ///
        /// <para>The join rules are <see cref="FdrScoresSidecar.TryJoinRows"/>'s, mapping each
        /// entry_id to its LAST row as <c>TryRead</c> does. A row with no record keeps the
        /// defaults it was loaded with.</para>
        /// </summary>
        public bool TryJoinFirstPassSidecar(string sidecarPath,
            IReadOnlyDictionary<uint, FdrExperimentRecord> experimentRecords,
            Func<uint, bool> expectedAbsent)
        {
            // Reused like the rows: Clear keeps the largest run's capacity, so the map is
            // allocated (and grown) a few times over the fold rather than once per run.
            _rowByEntryId.Clear();
            for (int i = 0; i < _count; i++)
                _rowByEntryId[_rows[i].EntryId] = i;

            return FdrScoresSidecar.TryJoinRows(sidecarPath, FdrScoresSidecar.Pass.FirstPass,
                _rowByEntryId, expectedAbsent, experimentRecords,
                (index, record, experiment) =>
                {
                    ref var row = ref _rows[index];
                    row.Score = record.Score;
                    row.RunPrecursorQvalue = record.RunPrecursorQvalue;
                    row.RunPeptideQvalue = record.RunPeptideQvalue;
                    if (experiment.HasValue)
                    {
                        row.ExperimentPrecursorQvalue = experiment.Value.ExperimentPrecursorQvalue;
                        row.ExperimentPeptideQvalue = experiment.Value.ExperimentPeptideQvalue;
                    }
                });
        }

        /// <summary>Row <paramref name="index"/>, read-only; valid until the next <see cref="Load"/>.</summary>
        public ref readonly Row this[int index]
        {
            get
            {
                if ((uint)index >= (uint)_count)
                    throw new ArgumentOutOfRangeException(nameof(index));
                return ref _rows[index];
            }
        }
    }
}
