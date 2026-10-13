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
    /// columns into parallel buffers that are REUSED across runs, so after the largest run nothing
    /// is allocated per run at all, and nothing lands on the large object heap once per run.</para>
    ///
    /// <para><see cref="TryJoinFirstPassSidecar"/> applies the sidecar with the same rules as
    /// <see cref="FdrScoresSidecar.TryRead(string, IList{FdrEntry}, FdrScoresSidecar.Pass, Func{uint, bool}, IReadOnlyDictionary{uint, FdrExperimentRecord})"/>,
    /// because the report is compared against a committed golden and these rows must fold to
    /// the same numbers the stubs did.</para>
    /// </summary>
    public sealed class FirstPassFoldRows
    {
        private readonly List<uint> _entryIds = new List<uint>();
        private readonly List<byte> _charges = new List<byte>();
        private readonly List<bool> _isDecoys = new List<bool>();
        private readonly List<string> _modifiedSequences = new List<string>();
        private readonly Dictionary<uint, int> _rowByEntryId = new Dictionary<uint, int>();
        private double[] _scores = Array.Empty<double>();
        private double[] _runPrecursorQvalues = Array.Empty<double>();
        private double[] _runPeptideQvalues = Array.Empty<double>();
        private double[] _experimentPrecursorQvalues = Array.Empty<double>();
        private double[] _experimentPeptideQvalues = Array.Empty<double>();

        /// <summary>Rows read from the current run's parquet.</summary>
        public int Count => _entryIds.Count;

        /// <summary>
        /// Replace the buffers' contents with <paramref name="parquetPath"/>'s rows, in parquet
        /// row order, skipping the row groups <c>LoadFdrStubsFromParquet</c> skips - the two
        /// readers share that rule. Each modified sequence is canonicalized through
        /// <paramref name="sequencePool"/> as the stub loader did, because the fold keeps
        /// sequences it has seen and would otherwise keep one copy per run.
        /// </summary>
        public void Load(string parquetPath, LibraryStringInterner sequencePool)
        {
            _entryIds.Clear();
            _charges.Clear();
            _isDecoys.Clear();
            _modifiedSequences.Clear();
            ParquetScoreCache.ReadFdrStubScalars(parquetPath,
                (entryId, charge, isDecoy, coelutionSum, modifiedSequence, apexRt) =>
                {
                    _entryIds.Add(entryId);
                    _charges.Add(charge);
                    _isDecoys.Add(isDecoy);
                    _modifiedSequences.Add(Canonicalize(modifiedSequence, sequencePool));
                },
                StubColumns.SkipCoelutionSum);
        }

        /// <summary>
        /// Join the run's first-pass sidecar onto the loaded rows by entry_id. False when the
        /// sidecar is unreadable, or carries a record with no row here that
        /// <paramref name="expectedAbsent"/> does not account for - in which case the values
        /// must not be used.
        ///
        /// <para>Same rules as <c>FdrScoresSidecar.TryRead</c>, in full: a record lands on the
        /// LAST row with its entry_id; a row with no record keeps Score 0 and q = 1; and the
        /// experiment q-values reach only rows a record landed on, never every row sharing an
        /// entry_id (see that method for what widening it cost).</para>
        /// </summary>
        public bool TryJoinFirstPassSidecar(string sidecarPath,
            IReadOnlyDictionary<uint, FdrExperimentRecord> experimentRecords,
            Func<uint, bool> expectedAbsent)
        {
            int count = Count;
            EnsureCapacity(count);
            Array.Clear(_scores, 0, count);
            Fill(_runPrecursorQvalues, count, 1.0);
            Fill(_runPeptideQvalues, count, 1.0);
            Fill(_experimentPrecursorQvalues, count, 1.0);
            Fill(_experimentPeptideQvalues, count, 1.0);

            _rowByEntryId.Clear();
            for (int i = 0; i < count; i++)
                _rowByEntryId[_entryIds[i]] = i;

            bool unaccounted = false;
            bool read = FdrScoresSidecar.ReadRecords(sidecarPath, FdrScoresSidecar.Pass.FirstPass, record =>
            {
                if (unaccounted)
                    return;
                if (!_rowByEntryId.TryGetValue(record.EntryId, out int row))
                {
                    if (expectedAbsent == null || !expectedAbsent(record.EntryId))
                        unaccounted = true;
                    return;
                }
                _scores[row] = record.Score;
                _runPrecursorQvalues[row] = record.RunPrecursorQvalue;
                _runPeptideQvalues[row] = record.RunPeptideQvalue;
                if (experimentRecords != null &&
                    experimentRecords.TryGetValue(record.EntryId, out var experiment))
                {
                    _experimentPrecursorQvalues[row] = experiment.ExperimentPrecursorQvalue;
                    _experimentPeptideQvalues[row] = experiment.ExperimentPeptideQvalue;
                }
            });
            // The map is per run and as large as the run; nothing reads it after the join.
            _rowByEntryId.Clear();
            return read && !unaccounted;
        }

        /// <summary>The modified sequence of row <paramref name="row"/>.</summary>
        public string ModifiedSequence(int row) => _modifiedSequences[row];

        /// <summary>The precursor charge of row <paramref name="row"/>.</summary>
        public byte Charge(int row) => _charges[row];

        /// <summary>The library entry_id of row <paramref name="row"/>.</summary>
        public uint EntryId(int row) => _entryIds[row];

        /// <summary>Whether row <paramref name="row"/> is a decoy.</summary>
        public bool IsDecoy(int row) => _isDecoys[row];

        /// <summary>The first-pass SVM score of row <paramref name="row"/>, after the join.</summary>
        public double Score(int row) => _scores[row];

        /// <summary>The run-level precursor q-value of row <paramref name="row"/>, after the join.</summary>
        public double RunPrecursorQvalue(int row) => _runPrecursorQvalues[row];

        /// <summary>The run-level peptide q-value of row <paramref name="row"/>, after the join.</summary>
        public double RunPeptideQvalue(int row) => _runPeptideQvalues[row];

        /// <summary>The experiment-level precursor q-value of row <paramref name="row"/>, after the join.</summary>
        public double ExperimentPrecursorQvalue(int row) => _experimentPrecursorQvalues[row];

        /// <summary>The experiment-level peptide q-value of row <paramref name="row"/>, after the join.</summary>
        public double ExperimentPeptideQvalue(int row) => _experimentPeptideQvalues[row];

        private void EnsureCapacity(int count)
        {
            if (_scores.Length >= count)
                return;
            _scores = new double[count];
            _runPrecursorQvalues = new double[count];
            _runPeptideQvalues = new double[count];
            _experimentPrecursorQvalues = new double[count];
            _experimentPeptideQvalues = new double[count];
        }

        private static void Fill(double[] values, int count, double value)
        {
            for (int i = 0; i < count; i++)
                values[i] = value;
        }

        /// <summary>
        /// The pool's shared instance for <paramref name="value"/>, or the value unchanged when
        /// no pool was supplied - as the stub loader canonicalizes, empty included.
        /// </summary>
        private static string Canonicalize(string value, LibraryStringInterner pool)
        {
            if (pool == null || string.IsNullOrEmpty(value))
                return value;
            return pool.Intern(value);
        }
    }
}
