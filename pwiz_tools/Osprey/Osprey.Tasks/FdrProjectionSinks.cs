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
    /// Shared base for the two projection output sinks (issue #4355 struct-shrink S0):
    /// owns the tail <c>[COUNT]</c> tally the retired inline block used to compute off
    /// the struct's q-value fields. During the score pass it accumulates, per row (in
    /// nested file/row order), the per-file passing target/decoy counts and the
    /// best-q-per-precursor set from the LIVE <see cref="FdrQValues"/> the write-back
    /// hands it (correction §0a: the q-values are no longer on the struct, so the tally
    /// must read them here). <see cref="Finish"/> emits the identical [COUNT] lines and
    /// gives the concrete sink a hook (<see cref="OnFinish"/>) to flush any deferred
    /// per-file output. The concrete sink handles the per-row OUTPUT via
    /// <see cref="AcceptOutput"/> -- parking it in a parallel array (1st pass) or
    /// streaming it to the sidecar (2nd pass).
    /// </summary>
    internal abstract class FdrProjectionSinkBase : IFdrOutputSink
    {
        protected readonly FdrProjectionSet Projections;
        private readonly FdrLevel _fdrLevel;
        private readonly double _runFdr;
        private readonly string _passLabel;
        private readonly int[] _fileTargets;
        private readonly int[] _fileDecoys;
        private readonly Dictionary<string, double> _bestQByPrecursor;
        // Streaming --model-diagnostics accumulator (null off the report path): folds every
        // pre-compaction row into the reduced report structures so the projection path can emit
        // the pass-1 report without holding the resident FdrEntry pool. Fed in Accept.
        private readonly ModelDiagnosticsData.Accumulator _mdiagAccumulator;

        protected FdrProjectionSinkBase(
            FdrProjectionSet projections, OspreyConfig config, string passLabel,
            ModelDiagnosticsData.Accumulator mdiagAccumulator = null)
        {
            Projections = projections;
            _fdrLevel = config.FdrLevel;
            _runFdr = config.RunFdr;
            _passLabel = passLabel;
            int nFiles = projections.PerFile.Count;
            _fileTargets = new int[nFiles];
            _fileDecoys = new int[nFiles];
            _bestQByPrecursor = new Dictionary<string, double>(StringComparer.Ordinal);
            _mdiagAccumulator = mdiagAccumulator;
        }

        /// <summary>
        /// Per-file passing-target counts (<c>!IsDecoy &amp;&amp; EffectiveRunQvalue &lt;=
        /// RunFdr</c>) accumulated during the score pass, in
        /// <see cref="FdrProjectionSet.PerFile"/> order. Exposed so the 1st-pass per-file
        /// passing-count logging can read the same tally the tail <c>[COUNT]</c> block
        /// uses, instead of recomputing <c>EffectiveRunQvalue</c> off the resident q-value
        /// array (issue #4355 struct-shrink S1: <c>RunPrecursorQvalue</c> is no longer
        /// resident, so <c>EffectiveRunQvalue</c> cannot be recomputed for
        /// <see cref="FdrLevel.Precursor"/>). Fully populated once the score pass ends.
        /// </summary>
        public IReadOnlyList<int> FilePassingTargets => _fileTargets;

        public void Accept(int fileIdx, int rowIdx, uint entryId, bool isDecoy,
            byte charge, string peptide, double score, double experimentAggregateScore,
            in FdrQValues q)
        {
            // Tail [COUNT] tally, identical to the retired inline block: passing =
            // EffectiveRunQvalue <= RunFdr, split target/decoy; best-q-per-precursor
            // over passing targets keyed by modseq|charge. peptide + charge are passed in
            // (issue #4355 struct-shrink S3 Stage B) so this works whether the caller holds
            // a resident projection (2nd pass) or streams the row straight from parquet.
            double eff = q.EffectiveRunQvalue(_fdrLevel);
            if (eff <= _runFdr)
            {
                if (isDecoy)
                    _fileDecoys[fileIdx]++;
                else
                    _fileTargets[fileIdx]++;
            }
            if (!isDecoy && eff <= _runFdr)
            {
                string pkey = peptide + "|" + charge;
                double existing;
                if (!_bestQByPrecursor.TryGetValue(pkey, out existing) || eff < existing)
                    _bestQByPrecursor[pkey] = eff;
            }

            // --model-diagnostics: fold this pre-compaction row into the streaming report
            // reductions (every row -- targets, decoys, entrapment, failing -- not just the
            // passing set the [COUNT] tally reads). Null off the report path.
            if (_mdiagAccumulator != null)
                _mdiagAccumulator.Add(fileIdx, peptide, charge, entryId, isDecoy, score, in q);

            AcceptOutput(fileIdx, rowIdx, entryId, isDecoy, score, experimentAggregateScore, in q);
        }

        public void Finish(Action<string> logInfo)
        {
            // Flush any deferred per-file output first (2nd-pass empty-file sidecars);
            // the [COUNT] lines follow so they land at the same position the retired
            // inline block emitted them (end of the score pass).
            OnFinish();

            int nTargetPassing = 0;
            int nDecoyPassing = 0;
            var perFile = Projections.PerFile;
            for (int f = 0; f < perFile.Count; f++)
            {
                logInfo(string.Format(
                    "[COUNT] {0} Percolator pass [{1}]: {2} targets, {3} decoys at {4:P0} FDR",
                    _passLabel, perFile[f].Key, _fileTargets[f], _fileDecoys[f], _runFdr));
                nTargetPassing += _fileTargets[f];
                nDecoyPassing += _fileDecoys[f];
            }

            logInfo(string.Format(
                "{0} Percolator results: {1} targets, {2} decoys pass {3:P1} FDR",
                _passLabel, nTargetPassing, nDecoyPassing, _runFdr));
            logInfo(string.Format(
                "[COUNT] {0} total across files: {1}",
                _passLabel, nTargetPassing));
            logInfo(string.Format(
                "[COUNT] {0} unique precursors (best q across files): {1}",
                _passLabel, _bestQByPrecursor.Count));
        }

        /// <summary>Handle one row's q-value output (park it, or stream it to the sidecar).</summary>
        protected abstract void AcceptOutput(int fileIdx, int rowIdx, uint entryId,
            bool isDecoy, double score, double experimentAggregateScore, in FdrQValues q);

        /// <summary>Flush any deferred per-file output before the [COUNT] tally is logged.</summary>
        protected virtual void OnFinish()
        {
        }
    }

    /// <summary>
    /// 1st-pass sink (issue #4355 struct-shrink S2): streams ALL of the score pass's per-row
    /// output straight to disk -- it keeps NO resident q-value array. It buffers each file's
    /// 36-byte RUN-scope <see cref="FdrScoreRecord"/>s in projection order and flushes the
    /// per-file <c>.1st-pass.fdr_scores.bin</c> via the caller's <c>flushPartial</c>
    /// callback at the file's last row, so a full pass's worth of q-values is never held
    /// resident (one file's buffer at a time). Empty survivor files are flushed with a
    /// 0-record sidecar in <see cref="OnFinish"/>. First-pass protein FDR + compaction
    /// stream <c>run_peptide_qvalue</c> back off this sidecar
    /// (see <c>FirstPassFdrTask.RunFirstPassProteinFdrStreaming</c>), so the resident
    /// <c>FdrProjectionOutputs</c> array the pre-S2 sink kept is gone.
    ///
    /// <para>The row's EXPERIMENT-scope values go to the <c>experiment</c> accumulator instead
    /// of into the per-file record (format v5, issue #4486): they are one value per distinct
    /// entry_id for the whole analysis, so the accumulator collapses them and the caller writes
    /// them once to <see cref="FdrExperimentSidecar"/>. That is what makes THIS file immutable -
    /// there is no longer a value in it that the score pass cannot know, so the two-phase
    /// write-then-patch it used to need for <c>experiment_protein_qvalue</c> is gone.</para>
    /// </summary>
    internal sealed class FdrStoringSink : FdrProjectionSinkBase
    {
        private readonly Func<string, IReadOnlyList<FdrScoreRecord>, int> _flushPartial;
        private readonly FdrExperimentAccumulator _experiment;
        private readonly bool[] _flushed;
        private readonly List<FdrScoreRecord> _buffer;
        private int _partialWriteFailures;

        public FdrStoringSink(
            FdrProjectionSet projections, OspreyConfig config, string passLabel,
            Func<string, IReadOnlyList<FdrScoreRecord>, int> flushPartial,
            FdrExperimentAccumulator experiment,
            ModelDiagnosticsData.Accumulator mdiagAccumulator = null)
            : base(projections, config, passLabel, mdiagAccumulator)
        {
            _flushPartial = flushPartial;
            _experiment = experiment;
            _flushed = new bool[projections.PerFile.Count];
            _buffer = new List<FdrScoreRecord>();
        }

        /// <summary>
        /// Number of per-file phase-1 partial-sidecar writes that failed during the score
        /// pass (the <c>flushPartial</c> callback returns each file's failure count). The
        /// caller adds this to the phase-2 patch failures for the StopAfterStage5 gate.
        /// Owned here (not a captured local) so the callback stays a plain delegate.
        /// </summary>
        public int PartialWriteFailures => _partialWriteFailures;

        protected override void AcceptOutput(int fileIdx, int rowIdx, uint entryId,
            bool isDecoy, double score, double experimentAggregateScore, in FdrQValues q)
        {
            // Buffer this row's RUN-scope record in projection order and flush the per-file
            // .1st-pass.fdr_scores.bin at the file's last row. Every column of it is final
            // here, so the file is written once and never revisited.
            _buffer.Add(new FdrScoreRecord(
                entryId, score,
                q.RunPrecursorQvalue, q.RunPeptideQvalue, q.Pep));

            // The EXPERIMENT-scope values collapse to one record per distinct entry_id. The
            // protein q is the one that is not known yet - it needs the pooled parsimony +
            // picked-protein FDR that runs after this pass - so it goes in at its 1.0 default
            // and RunFirstPassProteinFdrStreaming replaces it before the file is written. That
            // is a single 0.44 GB map being finished in memory, not 52.3 GB of sidecars being
            // rewritten on disk, which is what the placeholder-plus-patch used to cost.
            _experiment.Add(entryId,
                q.ExperimentPrecursorQvalue, q.ExperimentPeptideQvalue,
                1.0, experimentAggregateScore);

            // RowCount, not PerFile[fileIdx].Value.Count: on the 1st-pass streaming path the
            // projection carries per-file counts but NO resident rows (issue #4355 struct-shrink
            // S3 Stage B), so the last-row flush must key on the count, not an empty row list.
            if (rowIdx == Projections.RowCount(fileIdx) - 1)
            {
                _partialWriteFailures += _flushPartial(Projections.PerFile[fileIdx].Key, _buffer);
                _flushed[fileIdx] = true;
                _buffer.Clear();
            }
        }

        protected override void OnFinish()
        {
            // Files with no scored rows never hit the last-row flush above; write their
            // 0-record phase-1 partial sidecar so every file has a boundary file the
            // survivor reload can read (matching the pre-S1 single-phase write, which
            // wrote a sidecar per file unconditionally).
            var perFile = Projections.PerFile;
            var empty = Array.Empty<FdrScoreRecord>();
            for (int f = 0; f < perFile.Count; f++)
            {
                if (!_flushed[f])
                {
                    // A file with recorded rows that never reached its last-row flush means the
                    // 1st-pass streaming score pass emitted fewer rows than the counts-only producer
                    // recorded (the two independent parquet reads disagreed). Writing a 0-record
                    // sidecar here would silently corrupt .1st-pass.fdr_scores.bin, so fail loud.
                    if (Projections.RowCount(f) > 0)
                        throw new InvalidOperationException(string.Format(
                            @"First-pass sidecar flush for '{0}' never fired: {1} rows were recorded but " +
                            @"the score pass emitted fewer -- the parquet row count is inconsistent.",
                            perFile[f].Key, Projections.RowCount(f)));
                    _partialWriteFailures += _flushPartial(perFile[f].Key, empty);
                    _flushed[f] = true;
                }
            }
        }
    }

    /// <summary>
    /// 2nd-pass sink (issue #4355 struct-shrink S0, delivers C1): assembles each
    /// <see cref="FdrScoreRecord"/> during the score pass from the streamed q-values,
    /// buffers one file at a time in projection order, and flushes the per-file
    /// <c>.2nd-pass.fdr_scores.bin</c> directly via the caller's <c>flushFile</c> callback --
    /// so the q-values are NEVER stored on the projection (2nd-pass peak 80 -> 32 B). Empty
    /// survivor files are flushed with a 0-record sidecar in <see cref="OnFinish"/>, matching
    /// the resident write block.
    ///
    /// <para>The row's EXPERIMENT-scope values go to the <c>experiment</c> accumulator rather
    /// than into the per-file record (format v5, issue #4486), including the survivor's
    /// <c>ExperimentProteinQvalue</c> that <c>resolveProteinQ</c> supplies per file - it is
    /// still looked up by entry_id here, because the lean struct does not carry it, but it now
    /// lands in the analysis-wide record instead of once per run.</para>
    /// </summary>
    internal sealed class FdrStreamingSink : FdrProjectionSinkBase
    {
        private readonly Func<string, IReadOnlyDictionary<uint, double>> _resolveProteinQ;
        private readonly Action<string, IReadOnlyList<FdrScoreRecord>> _flushFile;
        private readonly FdrExperimentAccumulator _experiment;
        private readonly bool[] _flushed;
        private readonly List<FdrScoreRecord> _buffer;
        private int _curFileIdx;
        private IReadOnlyDictionary<uint, double> _curProteinQ;

        public FdrStreamingSink(
            FdrProjectionSet projections, OspreyConfig config, string passLabel,
            Func<string, IReadOnlyDictionary<uint, double>> resolveProteinQ,
            Action<string, IReadOnlyList<FdrScoreRecord>> flushFile,
            FdrExperimentAccumulator experiment)
            : base(projections, config, passLabel)
        {
            _resolveProteinQ = resolveProteinQ;
            _flushFile = flushFile;
            _experiment = experiment;
            _flushed = new bool[projections.PerFile.Count];
            _buffer = new List<FdrScoreRecord>();
            _curFileIdx = -1;
        }

        protected override void AcceptOutput(int fileIdx, int rowIdx, uint entryId,
            bool isDecoy, double score, double experimentAggregateScore, in FdrQValues q)
        {
            // Resolve this file's entry_id -> ExperimentProteinQvalue map once, at its first
            // row (rows are contiguous per file in Accept order). This is the value
            // BuildFromEntries used to carry onto the struct; the survivor buffer is
            // not mutated between projection build and here, so the lookup reproduces it.
            if (fileIdx != _curFileIdx)
            {
                _curFileIdx = fileIdx;
                _curProteinQ = _resolveProteinQ(Projections.PerFile[fileIdx].Key);
            }
            double experimentProteinQvalue;
            if (_curProteinQ == null || !_curProteinQ.TryGetValue(entryId, out experimentProteinQvalue))
                experimentProteinQvalue = 1.0;

            _buffer.Add(new FdrScoreRecord(
                entryId, score,
                q.RunPrecursorQvalue, q.RunPeptideQvalue, q.Pep));
            _experiment.Add(entryId,
                q.ExperimentPrecursorQvalue, q.ExperimentPeptideQvalue,
                experimentProteinQvalue, experimentAggregateScore);

            // Last row of this file: flush its sidecar and release the buffer. RowCount
            // (not the row list) so the 2nd-pass resident projection and the 1st-pass
            // streaming counts-only projection both resolve the file's row count.
            if (rowIdx == Projections.RowCount(fileIdx) - 1)
            {
                _flushFile(Projections.PerFile[fileIdx].Key, _buffer);
                _flushed[fileIdx] = true;
                _buffer.Clear();
            }
        }

        protected override void OnFinish()
        {
            var perFile = Projections.PerFile;
            var empty = Array.Empty<FdrScoreRecord>();
            for (int f = 0; f < perFile.Count; f++)
            {
                if (!_flushed[f])
                {
                    _flushFile(perFile[f].Key, empty);
                    _flushed[f] = true;
                }
            }
        }
    }
}
