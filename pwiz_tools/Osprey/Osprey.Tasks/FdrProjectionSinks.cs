/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
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

        protected FdrProjectionSinkBase(
            FdrProjectionSet projections, OspreyConfig config, string passLabel)
        {
            Projections = projections;
            _fdrLevel = config.FdrLevel;
            _runFdr = config.RunFdr;
            _passLabel = passLabel;
            int nFiles = projections.PerFile.Count;
            _fileTargets = new int[nFiles];
            _fileDecoys = new int[nFiles];
            _bestQByPrecursor = new Dictionary<string, double>(StringComparer.Ordinal);
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
            double score, in FdrQValues q)
        {
            // Tail [COUNT] tally, identical to the retired inline block: passing =
            // EffectiveRunQvalue <= RunFdr, split target/decoy; best-q-per-precursor
            // over passing targets keyed by modseq|charge (looked up from the lean row).
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
                var proj = Projections.PerFile[fileIdx].Value[rowIdx];
                string pkey = Projections.PeptideById[proj.PeptideId] + "|" + proj.Charge;
                double existing;
                if (!_bestQByPrecursor.TryGetValue(pkey, out existing) || eff < existing)
                    _bestQByPrecursor[pkey] = eff;
            }

            AcceptOutput(fileIdx, rowIdx, entryId, isDecoy, score, in q);
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
            bool isDecoy, double score, in FdrQValues q);

        /// <summary>Flush any deferred per-file output before the [COUNT] tally is logged.</summary>
        protected virtual void OnFinish()
        {
        }
    }

    /// <summary>
    /// 1st-pass sink (issue #4355 struct-shrink S1, two-phase sidecar): keeps ONLY the
    /// two q-values that must stay resident across the whole pass -- <c>RunPeptideQ</c>
    /// and <c>RunProteinQ</c> -- in a 16 B/row <see cref="FdrProjectionOutputs"/> array
    /// (1st-pass resident projection = 48 B), and streams the other four q-values
    /// straight to disk. During the score pass (phase 1) it buffers each file's PARTIAL
    /// 60-byte <see cref="FdrScoreRecord"/>s in projection order -- with the
    /// <c>run_protein_qvalue</c> field held at its 1.0 placeholder -- and flushes the
    /// per-file <c>.1st-pass.fdr_scores.bin</c> via the caller's <c>flushPartial</c>
    /// callback at the file's last row, so the four streamed q-values are never held
    /// resident. Empty survivor files are flushed with a 0-record sidecar in
    /// <see cref="OnFinish"/>. Protein FDR + compaction read <see cref="Outputs"/>; after
    /// protein FDR the caller runs phase 2, patching each record's <c>[52..60]</c> from
    /// the resident <c>RunProteinQ</c>. The byte layout is single-sourced through
    /// <c>FdrScoresSidecar.WriteRecord</c>, so the phase-1 file is byte-identical to the
    /// pre-S1 single-phase write except for the placeholder column the patch overwrites.
    /// </summary>
    internal sealed class FdrStoringSink : FdrProjectionSinkBase
    {
        private readonly FdrProjectionOutputs _outputs;
        private readonly Func<string, IReadOnlyList<FdrScoreRecord>, int> _flushPartial;
        private readonly bool[] _flushed;
        private readonly List<FdrScoreRecord> _buffer;
        private int _partialWriteFailures;

        public FdrStoringSink(
            FdrProjectionSet projections, OspreyConfig config, string passLabel,
            Func<string, IReadOnlyList<FdrScoreRecord>, int> flushPartial)
            : base(projections, config, passLabel)
        {
            _outputs = new FdrProjectionOutputs(projections);
            _flushPartial = flushPartial;
            _flushed = new bool[projections.PerFile.Count];
            _buffer = new List<FdrScoreRecord>();
        }

        public FdrProjectionOutputs Outputs => _outputs;

        /// <summary>
        /// Number of per-file phase-1 partial-sidecar writes that failed during the score
        /// pass (the <c>flushPartial</c> callback returns each file's failure count). The
        /// caller adds this to the phase-2 patch failures for the StopAfterStage5 gate.
        /// Owned here (not a captured local) so the callback stays a plain delegate.
        /// </summary>
        public int PartialWriteFailures => _partialWriteFailures;

        protected override void AcceptOutput(int fileIdx, int rowIdx, uint entryId,
            bool isDecoy, double score, in FdrQValues q)
        {
            // Resident: keep ONLY the run peptide q-value (protein FDR + compaction need
            // it across all rows); run protein q-value stays at its 1.0 placeholder until
            // first-pass protein FDR fills it (issue #4355 struct-shrink S1).
            _outputs.SetRunPeptideQvalue(fileIdx, rowIdx, q.RunPeptideQvalue);

            // Phase 1 of the two-phase sidecar: buffer this row's PARTIAL record
            // (run_protein_qvalue = 1.0 placeholder) in projection order and flush the
            // per-file .1st-pass.fdr_scores.bin at the file's last row. The four streamed
            // q-values (RunPrecursorQ, ExpPrecursorQ, ExpPeptideQ, Pep) go straight to
            // disk here and are never kept resident; phase 2 patches [52..60] afterward.
            _buffer.Add(new FdrScoreRecord(
                entryId, score,
                q.RunPrecursorQvalue, q.RunPeptideQvalue,
                q.ExperimentPrecursorQvalue, q.ExperimentPeptideQvalue,
                q.Pep, 1.0));

            if (rowIdx == Projections.PerFile[fileIdx].Value.Count - 1)
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
                    _partialWriteFailures += _flushPartial(perFile[f].Key, empty);
                    _flushed[f] = true;
                }
            }
        }
    }

    /// <summary>
    /// 2nd-pass sink (issue #4355 struct-shrink S0, delivers C1): assembles each
    /// <see cref="FdrScoreRecord"/> during the score pass from the streamed q-values +
    /// the survivor's <c>RunProteinQvalue</c> looked up by entry_id (it is no longer
    /// carried on the lean struct), buffers one file at a time in projection order, and
    /// flushes the per-file <c>.2nd-pass.fdr_scores.bin</c> directly via the caller's
    /// <c>flushFile</c> callback -- so the q-values are NEVER stored on the projection
    /// (2nd-pass peak 80 -> 32 B). Empty survivor files are flushed with a 0-record
    /// sidecar in <see cref="OnFinish"/>, matching the resident write block.
    /// </summary>
    internal sealed class FdrStreamingSink : FdrProjectionSinkBase
    {
        private readonly Func<string, IReadOnlyDictionary<uint, double>> _resolveProteinQ;
        private readonly Action<string, IReadOnlyList<FdrScoreRecord>> _flushFile;
        private readonly bool[] _flushed;
        private readonly List<FdrScoreRecord> _buffer;
        private int _curFileIdx;
        private IReadOnlyDictionary<uint, double> _curProteinQ;

        public FdrStreamingSink(
            FdrProjectionSet projections, OspreyConfig config, string passLabel,
            Func<string, IReadOnlyDictionary<uint, double>> resolveProteinQ,
            Action<string, IReadOnlyList<FdrScoreRecord>> flushFile)
            : base(projections, config, passLabel)
        {
            _resolveProteinQ = resolveProteinQ;
            _flushFile = flushFile;
            _flushed = new bool[projections.PerFile.Count];
            _buffer = new List<FdrScoreRecord>();
            _curFileIdx = -1;
        }

        protected override void AcceptOutput(int fileIdx, int rowIdx, uint entryId,
            bool isDecoy, double score, in FdrQValues q)
        {
            // Resolve this file's entry_id -> RunProteinQvalue map once, at its first
            // row (rows are contiguous per file in Accept order). This is the value
            // BuildFromEntries used to carry onto the struct; the survivor buffer is
            // not mutated between projection build and here, so the lookup reproduces it.
            if (fileIdx != _curFileIdx)
            {
                _curFileIdx = fileIdx;
                _curProteinQ = _resolveProteinQ(Projections.PerFile[fileIdx].Key);
            }
            double runProteinQvalue;
            if (_curProteinQ == null || !_curProteinQ.TryGetValue(entryId, out runProteinQvalue))
                runProteinQvalue = 1.0;

            _buffer.Add(new FdrScoreRecord(
                entryId, score,
                q.RunPrecursorQvalue, q.RunPeptideQvalue,
                q.ExperimentPrecursorQvalue, q.ExperimentPeptideQvalue,
                q.Pep, runProteinQvalue));

            // Last row of this file: flush its sidecar and release the buffer.
            if (rowIdx == Projections.PerFile[fileIdx].Value.Count - 1)
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
