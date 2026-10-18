/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
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
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR;
using pwiz.Osprey.FDR.ModelDiagnostics;
using pwiz.Osprey.IO;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// The tail <c>[COUNT]</c> tally both projection passes report (issue #4355 struct-shrink
    /// S0): per-file passing target/decoy counts and the distinct passing precursors, where
    /// passing is <c>EffectiveRunQvalue &lt;= RunFdr</c>. It reads RUN-scope q-values only, so
    /// the first pass tallies a file as soon as its run q-values exist, and the second pass as
    /// rows stream to its sidecar.
    /// </summary>
    internal sealed class FdrPassingTally
    {
        private readonly FdrLevel _fdrLevel;
        private readonly double _runFdr;
        private readonly string _passLabel;
        private readonly int[] _fileTargets;
        private readonly int[] _fileDecoys;
        // Distinct passing precursors (modseq|charge) for the unique-precursors [COUNT] line, its
        // only reader. Null when --perf-stats is off: the tally is a string per passing row, tens
        // of millions at cohort scale, for a line that would not be written.
        private readonly HashSet<string> _passingPrecursors;

        public FdrPassingTally(int nFiles, OspreyConfig config, string passLabel)
        {
            _fdrLevel = config.FdrLevel;
            _runFdr = config.RunFdr;
            _passLabel = passLabel;
            _fileTargets = new int[nFiles];
            _fileDecoys = new int[nFiles];
            _passingPrecursors = LogTag.COUNT.IsEnabled ? new HashSet<string>(StringComparer.Ordinal) : null;
        }

        /// <summary>Per-file passing-target counts, in file order. Complete once every file has
        /// been tallied.</summary>
        public IReadOnlyList<int> FilePassingTargets => _fileTargets;

        /// <summary>A set for one file's distinct passing precursors, or null when the
        /// <c>[COUNT]</c> line that reads them is off.</summary>
        public HashSet<string> NewPassingSet()
        {
            return _passingPrecursors != null ? new HashSet<string>(StringComparer.Ordinal) : null;
        }

        public bool Passes(double runPrecursorQvalue, double runPeptideQvalue)
        {
            return FdrQValues.EffectiveRunQvalue(_fdrLevel, runPrecursorQvalue, runPeptideQvalue) <= _runFdr;
        }

        /// <summary>Count one row; a passing target also joins the distinct-precursor set unless
        /// <paramref name="countPrecursor"/> is false because its file's set was built apart.</summary>
        public void CountRow(int fileIdx, bool isDecoy, string peptide, byte charge, bool passing,
            bool countPrecursor)
        {
            if (!passing)
                return;
            if (isDecoy)
            {
                _fileDecoys[fileIdx]++;
                return;
            }
            _fileTargets[fileIdx]++;
            if (_passingPrecursors != null && countPrecursor)
                _passingPrecursors.Add(PassingKey(peptide, charge));
        }

        /// <summary>One file's counts, tallied apart (on a file lane), and its passing set.</summary>
        public void AddFile(int fileIdx, int targets, int decoys, HashSet<string> passing)
        {
            _fileTargets[fileIdx] += targets;
            _fileDecoys[fileIdx] += decoys;
            AddPassing(passing);
        }

        public void AddPassing(HashSet<string> passing)
        {
            if (passing != null)
                _passingPrecursors.UnionWith(passing);
        }

        public static string PassingKey(string peptide, byte charge)
        {
            return peptide + @"|" + charge;
        }

        public void Log(IOspreyLog log, FdrProjectionSet projections)
        {
            int nTargetPassing = 0;
            int nDecoyPassing = 0;
            var perFile = projections.PerFile;
            for (int f = 0; f < perFile.Count; f++)
            {
                log.LogInfo(LogTag.COUNT, @"{0} Percolator pass [{1}]: {2} targets, {3} decoys at {4:0%} FDR",
                    _passLabel, perFile[f].Key, _fileTargets[f], _fileDecoys[f], _runFdr);
                nTargetPassing += _fileTargets[f];
                nDecoyPassing += _fileDecoys[f];
            }

            log.LogInfo(string.Format(
                OspreyTasksResources.FdrProjectionSinkBase_Finish__0__Percolator_results___1__targets___2__decoys_pass__3__FDR,
                PercolatorEngine.PassDisplayName(_passLabel), nTargetPassing, nDecoyPassing, _runFdr));
            log.LogInfo(LogTag.COUNT, @"{0} total across files: {1}",
                _passLabel, nTargetPassing);
            if (_passingPrecursors != null)
            {
                log.LogInfo(LogTag.COUNT, @"{0} unique precursors (best q across files): {1}",
                    _passLabel, _passingPrecursors.Count);
            }
        }
    }

    /// <summary>
    /// First-pass sink of the streaming score pass (<c>PercolatorScorer.RunStreamingFirstPass</c>).
    /// Keeps NO per-row state and writes no per-file file: pass 1 writes each file's
    /// <c>.1st-pass.fdr_scores.bin</c> itself as it finishes the file (<c>FileRunScopeSink</c>),
    /// and the resume gate adopts the rest. What this sink produces is:
    /// <list type="bullet">
    /// <item>the <c>[COUNT]</c> tally, from each file's run-scope output;</item>
    /// <item>the experiment-scope records, one per distinct entry_id, into the
    /// <c>experiment</c> accumulator (format v5, issue #4486), which the caller writes once to
    /// <see cref="FdrExperimentSidecar"/> after protein FDR fills in the protein q
    /// (<see cref="ApplyProteinQvalues"/>);</item>
    /// <item>with <c>--model-diagnostics</c>, the streaming report fold over every
    /// pre-compaction row.</item>
    /// </list>
    /// </summary>
    internal sealed class FdrFirstPassSink : IFdrFirstPassSink
    {
        private readonly FdrProjectionSet _projections;
        private readonly FdrPassingTally _tally;
        private readonly FdrExperimentAccumulator _experiment;
        // Streaming --model-diagnostics accumulator (null off the report path): folds every
        // pre-compaction row into the reduced report structures so the projection path can emit
        // the pass-1 report without holding the resident FdrEntry pool.
        private readonly ModelDiagnosticsData.Accumulator _mdiagAccumulator;
        // Each experiment entry's peptide, in the order the entries arrived, held only until
        // first-pass protein FDR resolves the protein q-values from them (ApplyProteinQvalues).
        private readonly List<uint> _entryIds = new List<uint>();
        private readonly List<string> _peptides = new List<string>();

        public FdrFirstPassSink(
            FdrProjectionSet projections, OspreyConfig config, string passLabel,
            FdrExperimentAccumulator experiment,
            ModelDiagnosticsData.Accumulator mdiagAccumulator = null)
        {
            _projections = projections;
            _tally = new FdrPassingTally(projections.PerFile.Count, config, passLabel);
            _experiment = experiment;
            _mdiagAccumulator = mdiagAccumulator;
        }

        /// <summary>
        /// Per-file passing-target counts (<c>!IsDecoy &amp;&amp; EffectiveRunQvalue &lt;=
        /// RunFdr</c>), in <see cref="FdrProjectionSet.PerFile"/> order, for the 1st-pass
        /// per-file passing-count logging. Fully populated once the score pass ends.
        /// </summary>
        public IReadOnlyList<int> FilePassingTargets => _tally.FilePassingTargets;

        public object PrepareRunScope(int fileIdx, FdrRunScopeRows rows)
        {
            var file = new RunScopeTally(rows.Count, _tally.NewPassingSet());
            for (int r = 0; r < rows.Count; r++)
            {
                if (!_tally.Passes(rows.RunPrecursorQvalues[r], rows.RunPeptideQvalues[r]))
                    continue;
                if (rows.IsDecoys[r])
                {
                    file.Decoys++;
                    continue;
                }
                file.Targets++;
                file.Passing?.Add(FdrPassingTally.PassingKey(rows.Peptides[r], rows.Charges[r]));
            }
            return file;
        }

        public void AcceptRunScope(int fileIdx, object prepared)
        {
            var file = (RunScopeTally)prepared;
            // Fewer rows than the counts-only producer recorded means the score pass and that
            // producer read the parquet differently. Every later reader of this file's sidecar
            // would then be pinned to the wrong rows, so fail here rather than at the first one.
            if (file.Count < _projections.RowCount(fileIdx))
            {
                throw new InvalidOperationException(string.Format(
                    @"First-pass score pass for '{0}' emitted {1} rows but {2} were recorded -- " +
                    @"the parquet row count is inconsistent.",
                    _projections.PerFile[fileIdx].Key, file.Count, _projections.RowCount(fileIdx)));
            }
            _tally.AddFile(fileIdx, file.Targets, file.Decoys, file.Passing);
        }

        public void AcceptExperimentEntry(uint entryId, string peptide, double experimentAggregateScore,
            double experimentPrecursorQvalue, double experimentPeptideQvalue, double pep, double peptidePep)
        {
            // The protein q is the one value not known yet - it needs the pooled parsimony +
            // picked-protein FDR that runs after this pass - so it goes in at its 1.0 default
            // and ApplyProteinQvalues replaces it before the file is written.
            _experiment.Add(entryId, experimentPrecursorQvalue, experimentPeptideQvalue,
                1.0, experimentAggregateScore, pep, peptidePep);
            _entryIds.Add(entryId);
            _peptides.Add(peptide);
        }

        /// <summary>
        /// Set every entry's protein q-value from first-pass protein FDR's peptide -> q map, by
        /// the peptide its rows carried (1.0 for a peptide the map does not hold), then drop the
        /// entry peptides, which nothing else reads.
        ///
        /// <para>This used to re-read every run's parquet to recover each entry's peptide - 63 s
        /// of the 446-run CHS cohort's FirstPassFDR - when the score pass had already seen it
        /// on the same rows, read the same way.</para>
        /// </summary>
        public void ApplyProteinQvalues(IReadOnlyDictionary<string, double> peptideQvalues)
        {
            for (int i = 0; i < _entryIds.Count; i++)
            {
                if (!peptideQvalues.TryGetValue(_peptides[i], out double q))
                    q = 1.0;
                _experiment.SetProteinQvalue(_entryIds[i], q);
            }
            _entryIds.Clear();
            _entryIds.TrimExcess();
            _peptides.Clear();
            _peptides.TrimExcess();
        }

        public bool FoldsRowExperimentValues => _mdiagAccumulator != null;

        public object PrepareRowFold(int fileIdx, FdrFileRows rows)
        {
            var fold = _mdiagAccumulator.BeginFile(fileIdx);
            for (int r = 0; r < rows.Count; r++)
            {
                FdrQValues q = rows.QAt(r);
                fold.Add(rows.Peptides[r], rows.Charges[r], rows.EntryIds[r], rows.IsDecoys[r], rows.Scores[r], in q);
            }
            return fold;
        }

        public void AcceptRowFold(int fileIdx, object prepared)
        {
            _mdiagAccumulator.MergeFile((ModelDiagnosticsData.Accumulator.FileFold)prepared);
        }

        public void Finish(IOspreyLog log)
        {
            _tally.Log(log, _projections);
        }

        private sealed class RunScopeTally
        {
            public RunScopeTally(int count, HashSet<string> passing)
            {
                Count = count;
                Passing = passing;
            }

            public int Count { get; }
            public HashSet<string> Passing { get; }
            public int Targets { get; set; }
            public int Decoys { get; set; }
        }
    }

    /// <summary>
    /// 2nd-pass sink (issue #4355 struct-shrink S0, delivers C1): assembles each
    /// <see cref="FdrScoreRecord"/> during the score pass from the streamed q-values,
    /// buffers one file at a time in projection order, and flushes the per-file
    /// <c>.2nd-pass.fdr_scores.bin</c> directly via the caller's <c>flushFile</c> callback --
    /// so the q-values are NEVER stored on the projection (2nd-pass peak 80 -> 32 B). Empty
    /// survivor files are flushed with a 0-record sidecar in <see cref="Finish"/>, matching
    /// the resident write block. It also owns the tail <c>[COUNT]</c> tally, because it is the
    /// only place the q-values are live once they are off the struct.
    ///
    /// <para>The row's EXPERIMENT-scope values go to the <c>experiment</c> accumulator rather
    /// than into the per-file record (format v5, issue #4486), including the survivor's
    /// <c>ExperimentProteinQvalue</c> that <c>resolveProteinQ</c> supplies per file - it is
    /// still looked up by entry_id here, because the lean struct does not carry it, but it now
    /// lands in the analysis-wide record instead of once per run.</para>
    /// </summary>
    internal sealed class FdrStreamingSink : IFdrFileLaneSink
    {
        private readonly FdrProjectionSet _projections;
        private readonly FdrPassingTally _tally;
        private readonly Func<string, IReadOnlyDictionary<uint, double>> _resolveProteinQ;
        private readonly Action<string, IReadOnlyList<FdrScoreRecord>> _flushFile;
        private readonly FdrExperimentAccumulator _experiment;
        private readonly bool[] _flushed;
        // Files whose passing-precursor keys PrepareFile built on a file lane and AcceptPrepared
        // merged, so Accept must not add their rows a second time.
        private readonly bool[] _prepared;
        private readonly List<FdrScoreRecord> _buffer;
        private int _curFileIdx;
        private IReadOnlyDictionary<uint, double> _curProteinQ;

        public FdrStreamingSink(
            FdrProjectionSet projections, OspreyConfig config, string passLabel,
            Func<string, IReadOnlyDictionary<uint, double>> resolveProteinQ,
            Action<string, IReadOnlyList<FdrScoreRecord>> flushFile,
            FdrExperimentAccumulator experiment)
        {
            _projections = projections;
            _tally = new FdrPassingTally(projections.PerFile.Count, config, passLabel);
            _resolveProteinQ = resolveProteinQ;
            _flushFile = flushFile;
            _experiment = experiment;
            _flushed = new bool[projections.PerFile.Count];
            _prepared = new bool[projections.PerFile.Count];
            _buffer = new List<FdrScoreRecord>();
            _curFileIdx = -1;
        }

        public void Accept(int fileIdx, int rowIdx, uint entryId, bool isDecoy,
            byte charge, string peptide, double score, double experimentAggregateScore,
            double apexRt, in FdrQValues q)
        {
            _tally.CountRow(fileIdx, isDecoy, peptide, charge,
                _tally.Passes(q.RunPrecursorQvalue, q.RunPeptideQvalue), !_prepared[fileIdx]);

            // Resolve this file's entry_id -> ExperimentProteinQvalue map once, at its first
            // row (rows are contiguous per file in Accept order). This is the value
            // BuildFromEntries used to carry onto the struct; the survivor buffer is
            // not mutated between projection build and here, so the lookup reproduces it.
            if (fileIdx != _curFileIdx)
            {
                _curFileIdx = fileIdx;
                _curProteinQ = _resolveProteinQ(_projections.PerFile[fileIdx].Key);
            }
            double experimentProteinQvalue;
            if (_curProteinQ == null || !_curProteinQ.TryGetValue(entryId, out experimentProteinQvalue))
                experimentProteinQvalue = 1.0;

            _buffer.Add(new FdrScoreRecord(
                entryId, score,
                q.RunPrecursorQvalue, q.RunPeptideQvalue, apexRt));
            _experiment.Add(entryId,
                q.ExperimentPrecursorQvalue, q.ExperimentPeptideQvalue,
                experimentProteinQvalue, experimentAggregateScore, q.Pep, q.ExperimentPeptidePep);

            // Last row of this file: flush its sidecar and release the buffer. RowCount
            // (not the row list) so the 2nd-pass resident projection and the 1st-pass
            // streaming counts-only projection both resolve the file's row count.
            if (rowIdx == _projections.RowCount(fileIdx) - 1)
            {
                _flushFile(_projections.PerFile[fileIdx].Key, _buffer);
                _flushed[fileIdx] = true;
                _buffer.Clear();
            }
        }

        /// <summary>
        /// The order-free share of <see cref="Accept"/> for a whole file, on a file lane: the
        /// file's distinct passing precursors (a set whose size is all the [COUNT] line reads).
        /// Null when the line is off.
        /// </summary>
        public object PrepareFile(int fileIdx, FdrFileRows rows)
        {
            var passing = _tally.NewPassingSet();
            if (passing == null)
                return null;
            for (int r = 0; r < rows.Count; r++)
            {
                FdrQValues q = rows.QAt(r);
                if (!rows.IsDecoys[r] && _tally.Passes(q.RunPrecursorQvalue, q.RunPeptideQvalue))
                    passing.Add(FdrPassingTally.PassingKey(rows.Peptides[r], rows.Charges[r]));
            }
            return passing;
        }

        public void AcceptPrepared(int fileIdx, object prepared)
        {
            if (!(prepared is HashSet<string> passing))
                return;
            _tally.AddPassing(passing);
            _prepared[fileIdx] = true;
        }

        public void Finish(IOspreyLog log)
        {
            // Flush the deferred empty-file sidecars first; the [COUNT] lines follow so they land
            // at the end of the score pass, where the retired inline block emitted them.
            var perFile = _projections.PerFile;
            var empty = Array.Empty<FdrScoreRecord>();
            for (int f = 0; f < perFile.Count; f++)
            {
                if (!_flushed[f])
                {
                    _flushFile(perFile[f].Key, empty);
                    _flushed[f] = true;
                }
            }
            _tally.Log(log, _projections);
        }
    }
}
