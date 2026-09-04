/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
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
using pwiz.Osprey.Core;

namespace pwiz.Osprey.FDR.ModelDiagnostics
{
    public sealed partial class ModelDiagnosticsData
    {
        /// <summary>
        /// Streaming builder for the pass-1 <see cref="ModelDiagnosticsData"/> that folds each
        /// first-pass FDR row into the SAME reduced structures the batch <see cref="Build"/>
        /// derives from the resident pool, WITHOUT ever holding the full pre-compaction
        /// <see cref="FdrEntry"/> pool resident. Fed one row at a time in nested (file, row)
        /// order by either of the two pre-compaction row sources - the projection score-pass
        /// sink (<c>FdrProjectionSinkBase.Accept</c>) as first-pass Percolator scores each row,
        /// or the streaming reconciled-bundle rehydrate
        /// (<c>RescoreHydration.HydrateCompactedStreaming</c>, per file after the 1st-pass
        /// sidecar overlay and before compaction discards the non-survivors);
        /// <see cref="Build"/> then runs the identical downstream builders over the accumulated
        /// reductions.
        ///
        /// Why this is byte-identical with the batch <see cref="Build"/>: every reduction here --
        /// best-per-precursor (max score, min q per modseq|charge), per-file passing counts,
        /// cross-run passing key-sets, and win-fraction per-base_id max scores -- is
        /// ORDER-INDEPENDENT. Within a (modseq|charge) key the class / is_decoy / pair_index are
        /// invariant (base_id is constant and a decoy carries a distinct sequence), so the
        /// max-score/min-q reduction lands on the same values whatever order rows arrive; the
        /// per-file counts and cross-run sets are tallies/sets keyed on identity; the win-fraction
        /// reduction is a per-base_id max. The shared downstream builders (score histogram,
        /// density ratio, id-yield, FDP views, cross-run views, win fraction) then consume only
        /// the reduced state and are order-safe (they sort, or are pure counts/sets) with ONE
        /// order-sensitive step: BuildScoreHistogram's decoy mean/std accumulate floating-point
        /// sums, which are not associative. Those stay bit-identical only because both paths
        /// enumerate the reduced best-per-precursor set in the SAME order -- FdrProjectionSet
        /// (BuildFromEntries / the streaming Builder, element-for-element parity) preserves the
        /// resident per-file FdrEntry row order, and the score pass walks perFile in the same
        /// nested order the batch ReduceToPrecs walks perFileEntries, so _best.Values enumerates
        /// identically. So the streamed reduction reproduces the resident reduction
        /// element-for-element, while the 340M-row pre-compaction pool that OOM'd an 82-file
        /// --model-diagnostics run at FirstPassFDR is never materialized -- the accumulator holds only
        /// ~unique-precursor / ~base_id-sized maps. (A future change that reorders projection rows
        /// within a file would threaten this last invariant -- keep the row order stable.)
        /// </summary>
        public sealed class Accumulator
        {
            private readonly IReadOnlyDictionary<uint, EntrapmentClass> _classByBaseId;
            private readonly IReadOnlyDictionary<uint, uint> _pairByBaseId;
            private readonly bool _haveManifest;
            private readonly double _entrapmentRatio;
            private readonly double _runFdr;
            private readonly FdrLevel _fdrLevel;
            private readonly string[] _runNames;
            private readonly int _nFiles;

            // Best-per-precursor, keyed modseq|charge (== ReduceToPrecs).
            private readonly Dictionary<string, Prec> _best =
                new Dictionary<string, Prec>(StringComparer.Ordinal);
            private int _nWithClass;
            private int _nWithoutClass;

            // Per-file passing counts at the run-level FDR (== BuildPerFile).
            private readonly int[] _fileTargets;
            private readonly int[] _fileDecoys;
            private readonly int[] _fileEntrap;

            // Cross-run reproducibility membership (== BuildCrossRunDetection): real targets
            // (run/exp gate) + entrapment (run/exp gate).
            //
            // FOLDED per run, not RETAINED per run. These were four List<HashSet<string>> sized
            // _nFiles - one passing-key set per run - which is the O(runs x entries) shape doc 00
            // names as "the single failure mode this architecture exists to prevent". Measured on
            // a 446-run CHS cohort: ~94 MB per run and still climbing at run 263, projecting
            // ~72 GB against a 63.7 GB box, so --model-diagnostics could not describe the cohort
            // it was asked about. Each stream now holds O(distinct) running state plus ONE run's
            // keys, and the view it produces is unchanged.
            private readonly CrossRunStream _runStream;
            private readonly CrossRunStream _expStream;
            private readonly CrossRunStream _entRunStream;
            private readonly CrossRunStream _entExpStream;
            private bool _anyEntrapment;

            // Win fraction: base_id -> [best target score, best decoy score] + target-side class
            // (== BuildWinFraction).
            private readonly Dictionary<uint, double[]> _bt = new Dictionary<uint, double[]>();
            private readonly Dictionary<uint, EntrapmentClass> _tClass =
                new Dictionary<uint, EntrapmentClass>();

            // Frontier: un-gated first-pass run-q distribution per target-side precursor, the
            // one input the reproducibility frontier needs beyond the gated cross-run sets.
            private readonly Dictionary<string, FrontierPrec> _frontier =
                new Dictionary<string, FrontierPrec>(StringComparer.Ordinal);
            // Within-file dedup buffer: the current file's per-precursor best run-q, flushed into
            // the bins at each file boundary (rows arrive in file-major order).
            private readonly Dictionary<string, double> _frontierFileMinQ =
                new Dictionary<string, double>(StringComparer.Ordinal);
            private int _frontierCurFile = -1;

            /// <param name="runNames">Input-file names in scoring (input-file) order -- the x for
            /// the per-file table and cross-run curves; also fixes <see cref="FileCount"/>.</param>
            /// <param name="classByBaseId">library base_id -> target-side entrapment class, exactly
            /// as passed to the batch <see cref="Build"/> (null/empty degrades to is_decoy-only).</param>
            /// <param name="pairByBaseId">library base_id -> peptide_pair_index (paired FDP), may be null.</param>
            /// <param name="entrapmentRatio">entrapment-to-target DB ratio r.</param>
            /// <param name="runFdr">configured run-level FDR.</param>
            /// <param name="fdrLevel">reported FDR control level (drives EffectiveRunQvalue).</param>
            public Accumulator(
                string[] runNames,
                IReadOnlyDictionary<uint, EntrapmentClass> classByBaseId,
                IReadOnlyDictionary<uint, uint> pairByBaseId,
                double entrapmentRatio,
                double runFdr,
                FdrLevel fdrLevel)
            {
                _runNames = runNames ?? throw new ArgumentNullException(nameof(runNames));
                _nFiles = runNames.Length;
                _classByBaseId = classByBaseId;
                _pairByBaseId = pairByBaseId;
                _haveManifest = classByBaseId != null && classByBaseId.Count > 0;
                _entrapmentRatio = entrapmentRatio;
                _runFdr = runFdr;
                _fdrLevel = fdrLevel;

                _fileTargets = new int[_nFiles];
                _fileDecoys = new int[_nFiles];
                _fileEntrap = new int[_nFiles];
                _runStream = new CrossRunStream(_nFiles);
                _expStream = new CrossRunStream(_nFiles);
                _entRunStream = new CrossRunStream(_nFiles);
                _entExpStream = new CrossRunStream(_nFiles);
            }

            /// <summary>
            /// The entrapment classification this accumulator was built with, so a sibling panel
            /// computed outside the streamed fold - the pass-1 peak co-assignment source, which
            /// reads apex RT off the FDR sidecars rather than the score pass - classifies rows
            /// identically without rebuilding it. Worth exposing rather than recomputing:
            /// classifying the searched library runs for minutes at 6.3M entries.
            /// </summary>
            public IReadOnlyDictionary<uint, EntrapmentClass> ClassByBaseId => _classByBaseId;


            /// <summary>
            /// Fold one scored, pre-compaction first-pass row into the reduced state, mirroring the
            /// per-entry work the batch ReduceToPrecs / BuildPerFile / BuildCrossRunDetection /
            /// BuildWinFraction passes do for one <see cref="FdrEntry"/>. Called once per projection
            /// row in nested (file, row) order from the score-pass sink. <paramref name="q"/> is the
            /// row's freshly computed first-pass q-values (the report reads only run/experiment
            /// precursor + peptide q; protein q is not needed and is filled after this pass).
            /// </summary>
            public void Add(int fileIdx, string modifiedSequence, byte charge, uint entryId,
                bool isDecoy, double score, in FdrQValues q)
            {
                uint baseId = entryId & BASE_ID_MASK;
                EntrapmentClass cls = Classify(isDecoy, baseId, _classByBaseId, _haveManifest,
                    ref _nWithClass, ref _nWithoutClass);
                string key = modifiedSequence + "|" + charge;

                // --- best-per-precursor (== ReduceToPrecs: max score, min q at each scope) ---
                uint pairIdx = 0;
                bool hasPair = _pairByBaseId != null && _pairByBaseId.TryGetValue(baseId, out pairIdx);
                if (!_best.TryGetValue(key, out var cur))
                {
                    cur = new Prec
                    {
                        Score = score,
                        QRunPrecursor = q.RunPrecursorQvalue,
                        QExpPrecursor = q.ExperimentPrecursorQvalue,
                        IsDecoy = isDecoy,
                        Class = cls,
                        PairIndex = pairIdx,
                        Charge = charge,
                        HasPair = hasPair,
                    };
                }
                else
                {
                    if (score > cur.Score)
                    {
                        cur.Score = score;
                        cur.IsDecoy = isDecoy;
                        cur.Class = cls;
                        cur.PairIndex = pairIdx;
                        cur.HasPair = hasPair;
                    }
                    if (q.RunPrecursorQvalue < cur.QRunPrecursor)
                        cur.QRunPrecursor = q.RunPrecursorQvalue;
                    if (q.ExperimentPrecursorQvalue < cur.QExpPrecursor)
                        cur.QExpPrecursor = q.ExperimentPrecursorQvalue;
                }
                _best[key] = cur;

                // --- per-file passing counts (== BuildPerFile) + cross-run key-sets
                //     (== BuildCrossRunDetection): the run-level FDR gate, decoys counted but
                //     excluded from the reproducibility sets, entrapment routed to its own sets. ---
                bool isEntrap = _haveManifest && _classByBaseId != null
                    && _classByBaseId.TryGetValue(baseId, out var pcls)
                    && pcls == EntrapmentClass.PTarget;
                if (q.EffectiveRunQvalue(_fdrLevel) <= _runFdr)
                {
                    if (isDecoy)
                    {
                        _fileDecoys[fileIdx]++;
                    }
                    else
                    {
                        if (isEntrap)
                            _fileEntrap[fileIdx]++;
                        else
                            _fileTargets[fileIdx]++;

                        bool expOk = q.EffectiveExperimentQvalue(_fdrLevel) <= _runFdr;
                        if (isEntrap)
                        {
                            _entRunStream.Add(fileIdx, key);
                            if (expOk)
                                _entExpStream.Add(fileIdx, key);
                            _anyEntrapment = true;
                        }
                        else
                        {
                            _runStream.Add(fileIdx, key);
                            if (expOk)
                                _expStream.Add(fileIdx, key);
                        }
                    }
                }

                // Frontier: fold the UN-GATED first-pass row into the within-file run-q tally
                // (target side only). On a new file, flush the previous file's per-precursor best
                // run-q into the bins first (rows arrive in file-major order).
                if (!isDecoy)
                {
                    if (fileIdx != _frontierCurFile)
                    {
                        if (_frontierCurFile >= 0)
                            FrontierFlushFile(_frontier, _frontierFileMinQ);
                        _frontierCurFile = fileIdx;
                    }
                    FrontierRow(_frontier, _frontierFileMinQ, key, isEntrap,
                        q.EffectiveRunQvalue(_fdrLevel), q.EffectiveExperimentQvalue(_fdrLevel));
                }

                // --- win fraction per base_id (== BuildWinFraction: best target vs best decoy) ---
                if (!_bt.TryGetValue(baseId, out var slot))
                {
                    slot = new[] { double.NegativeInfinity, double.NegativeInfinity };
                    _bt[baseId] = slot;
                }
                if (isDecoy)
                {
                    if (score > slot[1]) slot[1] = score;
                }
                else if (score > slot[0])
                {
                    slot[0] = score;
                    _tClass[baseId] = _haveManifest && _classByBaseId != null
                        && _classByBaseId.TryGetValue(baseId, out var c)
                        ? c : EntrapmentClass.Target;
                }
            }

            /// <summary>
            /// Assemble the pass-1 <see cref="ModelDiagnosticsData"/> from the accumulated
            /// reductions, running the SAME downstream builders the batch <see cref="Build"/> uses
            /// (only the reduction source differs). <paramref name="contributions"/> is the trained
            /// first-pass model (null on a non-Percolator / rehydrated run -> no Model tab).
            /// </summary>
            public ModelDiagnosticsData Build(FeatureContributions contributions)
            {
                var precs = _best.Values.ToList();
                var data = new ModelDiagnosticsData
                {
                    RunFdr = _runFdr,
                    FdrLevel = _fdrLevel.ToString(),
                    FileCount = _nFiles,
                    Model = new List<FeatureRow>(),
                };

                // Per-file passing summary (== BuildPerFile), one row per file in input order.
                var perFile = new List<FileSummaryRow>(_nFiles);
                for (int f = 0; f < _nFiles; f++)
                {
                    perFile.Add(new FileSummaryRow
                    {
                        File = _runNames[f],
                        Targets = _fileTargets[f],
                        Decoys = _fileDecoys[f],
                        Entrapment = _fileEntrap[f],
                    });
                }
                data.PerFile = perFile;

                foreach (var p in precs)
                {
                    switch (p.Class)
                    {
                        case EntrapmentClass.Target: data.NTarget++; break;
                        case EntrapmentClass.Decoy: data.NDecoy++; break;
                        case EntrapmentClass.PTarget: data.NPTarget++; break;
                        case EntrapmentClass.PDecoy: data.NPDecoy++; break;
                    }
                }
                data.HasEntrapment = data.NPTarget > 0;
                data.FeatureCount = contributions?.Features.Count ?? 0;
                data.NClassifiedFromManifest = _nWithClass;
                data.NUnclassified = _nWithoutClass;

                if (contributions != null)
                {
                    data.ModelComposite = contributions.Composite;
                    data.ModelDegenerate = contributions.IsDegenerate;
                    data.FeatureHistEdges = contributions.HistogramEdges;
                    data.Model = BuildFeatureRows(contributions);
                }

                data.Scores = BuildScoreHistogram(precs);
                data.DensityRatio = BuildDensityRatio(data.Scores, data.HasEntrapment);
                data.IdYield = BuildIdYield(precs);

                double r = _entrapmentRatio > 0 ? _entrapmentRatio : 1.0;
                // Close the run in progress and any trailing runs that contributed nothing, so
                // every file index has its entry - the batch loop gives an empty set the same
                // treatment. Same obligation as FrontierFlushFile below, for the same reason.
                _runStream.Finish();
                _expStream.Finish();
                _entRunStream.Finish();
                _entExpStream.Finish();

                data.CrossRun = new CrossRunDetection
                {
                    RunNames = _runNames,
                    PerRun = ComputeCrossRunView(_runStream, _anyEntrapment ? _entRunStream : null, _nFiles, r),
                    Experiment = ComputeCrossRunView(_expStream, _anyEntrapment ? _entExpStream : null, _nFiles, r),
                };

                data.WinFraction = BuildWinFractionFromReduced(_bt, _tClass);

                if (data.HasEntrapment)
                    data.FdpViews = BuildFdpViewsFromPrecs(precs, r, 1);

                // Reproducibility frontier (first-pass, pre-compaction; entrapment-gated).
                if (data.HasEntrapment)
                {
                    FrontierFlushFile(_frontier, _frontierFileMinQ);   // flush the final file
                    data.Frontier = BuildFrontier(_frontier.Values, _nFiles, r, _runFdr);
                }

                return data;
            }

            /// <summary>
            /// One cross-run membership reduction, folded run by run instead of retained run by
            /// run. Replaces a <c>List&lt;HashSet&lt;string&gt;&gt;</c> of N per-run key sets with
            /// O(distinct) running state plus ONE run's keys.
            ///
            /// <para>The reductions are exactly the ones the set-based
            /// <c>ComputeCrossRunView</c> loop performs, executed as each run completes rather
            /// than over N retained sets at the end: the per-run passing count, the cumulative
            /// union, the cumulative intersection, and the per-key run-count tally the histogram
            /// is binned from. Nothing here is a different formula - only a different moment.</para>
            ///
            /// <para><b>A run that contributes no rows still gets its entry.</b> The batch loop
            /// walks every index and hands an empty set to each, so a run whose rows were all
            /// filtered - or which had none at all, and for which <see cref="Add"/> is therefore
            /// never called - must still record its count, its union and its (empty) intersection.
            /// <see cref="CloseThrough"/> is what closes those skipped indices, and it is the one
            /// place a streamed reduction can silently disagree with the batch one.</para>
            /// </summary>
            internal sealed class CrossRunStream
            {
                private readonly int _nFiles;
                private readonly HashSet<string> _current = new HashSet<string>(StringComparer.Ordinal);
                private readonly HashSet<string> _union = new HashSet<string>(StringComparer.Ordinal);
                private readonly Dictionary<string, int> _runCount =
                    new Dictionary<string, int>(StringComparer.Ordinal);
                // Null until the first run closes, mirroring the batch loop's `inter == null`
                // seed: the intersection starts as run 0's set, not as the empty set.
                private HashSet<string> _inter;
                private int _curFile = -1;

                internal CrossRunStream(int nFiles)
                {
                    _nFiles = nFiles;
                    PerRunCount = new int[nFiles];
                    CumUnion = new int[nFiles];
                    CumIntersection = new int[nFiles];
                }

                internal int[] PerRunCount { get; }
                internal int[] CumUnion { get; }
                internal int[] CumIntersection { get; }
                internal IReadOnlyDictionary<string, int> RunCount => _runCount;

                /// <summary>Record <paramref name="key"/> as passing in run <paramref name="fileIdx"/>.
                /// Rows arrive in file-major order, so a change of index closes the previous run.</summary>
                internal void Add(int fileIdx, string key)
                {
                    if (fileIdx != _curFile)
                    {
                        CloseThrough(fileIdx);
                        _curFile = fileIdx;
                    }
                    _current.Add(key);
                }

                /// <summary>Close the run in progress and every remaining run, so all
                /// <see cref="_nFiles"/> entries are populated however few runs contributed.</summary>
                internal void Finish()
                {
                    CloseThrough(_nFiles);
                    _curFile = _nFiles;
                }

                /// <summary>
                /// Close each file index from the one in progress up to (but excluding)
                /// <paramref name="target"/>. The run in progress closes with the keys it
                /// gathered; every index between it and the target closes EMPTY, which is what
                /// the batch loop does for a run whose set holds nothing.
                /// </summary>
                private void CloseThrough(int target)
                {
                    for (int i = Math.Max(_curFile, 0); i < target && i < _nFiles; i++)
                    {
                        PerRunCount[i] = _current.Count;
                        _union.UnionWith(_current);
                        CumUnion[i] = _union.Count;
                        if (_inter == null)
                            _inter = new HashSet<string>(_current, StringComparer.Ordinal);
                        else
                            _inter.IntersectWith(_current);
                        CumIntersection[i] = _inter.Count;
                        foreach (var key in _current)
                        {
                            _runCount.TryGetValue(key, out int c);
                            _runCount[key] = c + 1;
                        }
                        _current.Clear();
                    }
                }
            }
        }
    }
}
