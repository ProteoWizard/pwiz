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

            // 1 = pre-compaction first pass, 2 = final reported pool. The reductions are the
            // same either way; this decides which ones are folded at all and which Build method
            // may be called. The Frontier is the one pass-1-only fold, and skipping it in pass 2
            // is not a micro-optimisation: it is a dictionary lookup and update per target row
            // over the whole reported pool, and nothing in Pass2Data reads the result.
            private readonly int _pass;

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
            // The file the row-by-row Add is folding now, merged when the next file starts or
            // a Build begins; and the last file merged, which merges must ascend from.
            private FileFold _open;
            private int _lastMergedFile = -1;

            /// <param name="runNames">Input-file names in scoring (input-file) order -- the x for
            /// the per-file table and cross-run curves; also fixes <see cref="FileCount"/>.</param>
            /// <param name="classByBaseId">library base_id -> target-side entrapment class, exactly
            /// as passed to the batch <see cref="Build"/> (null/empty degrades to is_decoy-only).</param>
            /// <param name="pairByBaseId">library base_id -> peptide_pair_index (paired FDP), may be null.</param>
            /// <param name="entrapmentRatio">entrapment-to-target DB ratio r.</param>
            /// <param name="runFdr">configured run-level FDR.</param>
            /// <param name="fdrLevel">reported FDR control level (drives EffectiveRunQvalue).</param>
            /// <param name="pass">1 for the pre-compaction first pass (<see cref="Build"/>),
            /// 2 for the final reported pool (<see cref="BuildPass2"/>). Decides which folds run.</param>
            public Accumulator(
                string[] runNames,
                IReadOnlyDictionary<uint, EntrapmentClass> classByBaseId,
                IReadOnlyDictionary<uint, uint> pairByBaseId,
                double entrapmentRatio,
                double runFdr,
                FdrLevel fdrLevel,
                int pass = 1)
            {
                if (pass != 1 && pass != 2)
                    throw new ArgumentOutOfRangeException(nameof(pass));
                _pass = pass;
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
                // Rows arrive file-major (IFdrOutputSink's contract), so each file's rows are
                // reduced into a FileFold of their own and merged when the next file starts. That
                // is the same path a file lane takes through BeginFile and MergeFile, so there is
                // one implementation of every reduction rather than a row-by-row one beside a
                // per-file one that could drift from it.
                if (_open == null || _open.FileIdx != fileIdx)
                {
                    FlushOpenFile();
                    _open = BeginFile(fileIdx);
                }
                _open.Add(modifiedSequence, charge, entryId, isDecoy, score, in q);
            }

            /// <summary>
            /// A fold for one file's rows. Building it reads only this accumulator's immutable
            /// configuration, so folds for different files can be built at once, on file lanes;
            /// merge each with <see cref="MergeFile"/>, in file order.
            /// </summary>
            public FileFold BeginFile(int fileIdx)
            {
                return new FileFold(this, fileIdx);
            }

            /// <summary>
            /// Folds one file's reductions in. Files must arrive in ascending order - the order
            /// the row-by-row <see cref="Add"/> sees them - and each exactly once.
            /// </summary>
            public void MergeFile(FileFold fold)
            {
                if (fold == null)
                    throw new ArgumentNullException(nameof(fold));
                FlushOpenFile();
                fold.MergeInto(this);
            }

            private void FlushOpenFile()
            {
                if (_open == null)
                    return;
                var open = _open;
                _open = null;
                open.MergeInto(this);
            }

            /// <summary>
            /// One file's share of <see cref="Add"/>: every reduction the accumulator keeps,
            /// computed over that file's rows alone, and merged by <see cref="MergeInto"/> so that
            /// merging the files in order reproduces folding every row in order.
            ///
            /// <para>Why the merge is exact, reduction by reduction. Best-per-precursor keeps the
            /// first row with the highest score and the lowest q-values; a file's own best, merged
            /// only when it is strictly higher, is the first highest row overall, and a minimum is
            /// a minimum. Counts add. A cross-run stream takes a file's passing keys as one set,
            /// in the order its rows added them. The win fraction keeps per base_id the highest
            /// target and decoy scores and the class of the first row reaching the highest target
            /// score - again strict on merge. The frontier keeps a precursor's first-seen
            /// entrapment flag and lowest experiment q, and counts each file's lowest run q once,
            /// which is what a file's own minimum flushed once per file does. Each collection
            /// gains a key when the first file holding it is merged, in that file's row order -
            /// the order the row-by-row fold inserted it in.</para>
            /// </summary>
            public sealed class FileFold
            {
                private readonly Accumulator _owner;
                private readonly Dictionary<string, Prec> _best = new Dictionary<string, Prec>(StringComparer.Ordinal);
                private int _nWithClass;
                private int _nWithoutClass;
                private int _targets;
                private int _decoys;
                private int _entrap;
                private readonly HashSet<string> _runKeys = new HashSet<string>(StringComparer.Ordinal);
                private readonly HashSet<string> _expKeys = new HashSet<string>(StringComparer.Ordinal);
                private readonly HashSet<string> _entRunKeys = new HashSet<string>(StringComparer.Ordinal);
                private readonly HashSet<string> _entExpKeys = new HashSet<string>(StringComparer.Ordinal);
                private readonly Dictionary<uint, double[]> _bt = new Dictionary<uint, double[]>();
                private readonly Dictionary<uint, EntrapmentClass> _tClass = new Dictionary<uint, EntrapmentClass>();
                // Pass 1 only: the precursor's first-seen entrapment flag and lowest experiment q,
                // and the file's lowest run q - the frontier's whole per-file input.
                private readonly Dictionary<string, (bool IsEntrapment, double MinExpQ)> _frontier;
                private readonly Dictionary<string, double> _frontierMinQ;

                internal FileFold(Accumulator owner, int fileIdx)
                {
                    _owner = owner;
                    FileIdx = fileIdx;
                    if (owner._pass == 1)
                    {
                        _frontier = new Dictionary<string, (bool IsEntrapment, double MinExpQ)>(StringComparer.Ordinal);
                        _frontierMinQ = new Dictionary<string, double>(StringComparer.Ordinal);
                    }
                }

                public int FileIdx { get; }

                /// <summary>Folds one row of this file; the per-row half of
                /// <see cref="Accumulator.Add"/>.</summary>
                public void Add(string modifiedSequence, byte charge, uint entryId,
                    bool isDecoy, double score, in FdrQValues q)
                {
                    var owner = _owner;
                    uint baseId = entryId & BASE_ID_MASK;
                    EntrapmentClass cls = Classify(isDecoy, baseId, owner._classByBaseId, owner._haveManifest,
                        ref _nWithClass, ref _nWithoutClass);
                    string key = modifiedSequence + @"|" + charge;

                    // --- best-per-precursor (== ReduceToPrecs: max score, min q at each scope) ---
                    uint pairIdx = 0;
                    bool hasPair = owner._pairByBaseId != null && owner._pairByBaseId.TryGetValue(baseId, out pairIdx);
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
                    bool isEntrap = owner._haveManifest && owner._classByBaseId != null
                        && owner._classByBaseId.TryGetValue(baseId, out var pcls)
                        && pcls == EntrapmentClass.PTarget;
                    if (q.EffectiveRunQvalue(owner._fdrLevel) <= owner._runFdr)
                    {
                        if (isDecoy)
                        {
                            _decoys++;
                        }
                        else
                        {
                            if (isEntrap)
                                _entrap++;
                            else
                                _targets++;

                            bool expOk = q.EffectiveExperimentQvalue(owner._fdrLevel) <= owner._runFdr;
                            if (isEntrap)
                            {
                                _entRunKeys.Add(key);
                                if (expOk)
                                    _entExpKeys.Add(key);
                            }
                            else
                            {
                                _runKeys.Add(key);
                                if (expOk)
                                    _expKeys.Add(key);
                            }
                        }
                    }

                    // Frontier: fold the UN-GATED first-pass row into the within-file run-q tally
                    // (target side only). Pass 1 only - Pass2Data has no Frontier card, so in pass 2
                    // this is work with no reader.
                    if (_frontier != null && !isDecoy)
                    {
                        double effExpQ = q.EffectiveExperimentQvalue(owner._fdrLevel);
                        if (_frontier.TryGetValue(key, out var fp))
                        {
                            if (effExpQ < fp.MinExpQ)
                                _frontier[key] = (fp.IsEntrapment, effExpQ);
                        }
                        else
                        {
                            // The comparison FrontierRow makes against a fresh precursor's MaxValue,
                            // not Math.Min, which would let a NaN through where it does not.
                            _frontier[key] = (isEntrap, effExpQ < double.MaxValue ? effExpQ : double.MaxValue);
                        }
                        double effRunQ = q.EffectiveRunQvalue(owner._fdrLevel);
                        if (!_frontierMinQ.TryGetValue(key, out double curRunQ) || effRunQ < curRunQ)
                            _frontierMinQ[key] = effRunQ;
                    }

                    // --- win fraction per base_id (== BuildWinFraction: best target vs best decoy) ---
                    if (!_bt.TryGetValue(baseId, out var slot))
                    {
                        slot = new[] { double.NegativeInfinity, double.NegativeInfinity };
                        _bt[baseId] = slot;
                    }
                    if (isDecoy)
                    {
                        if (score > slot[1])
                            slot[1] = score;
                    }
                    else if (score > slot[0])
                    {
                        slot[0] = score;
                        _tClass[baseId] = owner._haveManifest && owner._classByBaseId != null
                            && owner._classByBaseId.TryGetValue(baseId, out var c)
                            ? c : EntrapmentClass.Target;
                    }
                }

                internal void MergeInto(Accumulator acc)
                {
                    if (!ReferenceEquals(acc, _owner))
                        throw new ArgumentException(@"A file fold merges only into the accumulator that began it.");
                    if (FileIdx <= acc._lastMergedFile)
                    {
                        throw new InvalidOperationException(string.Format(
                            @"Model diagnostics: file {0} folded after file {1}; files must be merged once each, in order.",
                            FileIdx, acc._lastMergedFile));
                    }
                    acc._lastMergedFile = FileIdx;

                    foreach (var kv in _best)
                    {
                        var file = kv.Value;
                        if (!acc._best.TryGetValue(kv.Key, out var cur))
                        {
                            acc._best[kv.Key] = file;
                            continue;
                        }
                        if (file.Score > cur.Score)
                        {
                            cur.Score = file.Score;
                            cur.IsDecoy = file.IsDecoy;
                            cur.Class = file.Class;
                            cur.PairIndex = file.PairIndex;
                            cur.HasPair = file.HasPair;
                        }
                        if (file.QRunPrecursor < cur.QRunPrecursor)
                            cur.QRunPrecursor = file.QRunPrecursor;
                        if (file.QExpPrecursor < cur.QExpPrecursor)
                            cur.QExpPrecursor = file.QExpPrecursor;
                        acc._best[kv.Key] = cur;
                    }

                    acc._nWithClass += _nWithClass;
                    acc._nWithoutClass += _nWithoutClass;
                    acc._fileTargets[FileIdx] += _targets;
                    acc._fileDecoys[FileIdx] += _decoys;
                    acc._fileEntrap[FileIdx] += _entrap;

                    // A stream only hears of a file with keys, exactly as when each key was added.
                    acc._runStream.AddFile(FileIdx, _runKeys);
                    acc._expStream.AddFile(FileIdx, _expKeys);
                    acc._entRunStream.AddFile(FileIdx, _entRunKeys);
                    acc._entExpStream.AddFile(FileIdx, _entExpKeys);
                    if (_entRunKeys.Count > 0)
                        acc._anyEntrapment = true;

                    if (_frontier != null)
                    {
                        foreach (var kv in _frontier)
                        {
                            if (!acc._frontier.TryGetValue(kv.Key, out var fp))
                            {
                                fp = new FrontierPrec { IsEntrapment = kv.Value.IsEntrapment };
                                acc._frontier[kv.Key] = fp;
                            }
                            if (kv.Value.MinExpQ < fp.MinExpQ)
                                fp.MinExpQ = kv.Value.MinExpQ;
                        }
                        FrontierFlushFile(acc._frontier, _frontierMinQ);
                    }

                    // Classes first, in this file's own first-target order, judged against the
                    // highest target scores BEFORE this file's are merged: the class map gains a
                    // key at its first target row, which is not necessarily its base_id's first row.
                    foreach (var kv in _tClass)
                    {
                        double before = acc._bt.TryGetValue(kv.Key, out var accSlot)
                            ? accSlot[0] : double.NegativeInfinity;
                        if (_bt[kv.Key][0] > before)
                            acc._tClass[kv.Key] = kv.Value;
                    }
                    foreach (var kv in _bt)
                    {
                        if (!acc._bt.TryGetValue(kv.Key, out var accSlot))
                        {
                            acc._bt[kv.Key] = kv.Value;   // adopted: a merged fold is not used again
                            continue;
                        }
                        if (kv.Value[1] > accSlot[1])
                            accSlot[1] = kv.Value[1];
                        if (kv.Value[0] > accSlot[0])
                            accSlot[0] = kv.Value[0];
                    }
                }
            }

            /// <summary>
            /// Assemble the pass-1 <see cref="ModelDiagnosticsData"/> from the accumulated
            /// reductions, running the SAME downstream builders the batch <see cref="Build"/> uses
            /// (only the reduction source differs). <paramref name="contributions"/> describes the
            /// trained first-pass model: null on a resumed / rehydrated run (no Model tab), and for a
            /// tree model the per-feature distributions with no weight-based contribution table
            /// (<see cref="FeatureContributions.IsTreeEnsemble"/>).
            /// </summary>
            public ModelDiagnosticsData Build(FeatureContributions contributions)
            {
                FlushOpenFile();
                if (_pass != 1)
                    throw new InvalidOperationException(PassMismatch(1, nameof(BuildPass2)));
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
                    data.ModelIsTreeEnsemble = contributions.IsTreeEnsemble;
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
                    data.Frontier = BuildFrontier(_frontier.Values, _nFiles, r, _runFdr);
                }

                return data;
            }

            /// <summary>
            /// Assemble the pass-2 <see cref="Pass2Data"/> from the accumulated reductions,
            /// running the SAME downstream builders the batch <see cref="BuildPass2"/> uses over
            /// the resident pool. This is the streamed half of the fix for the last
            /// O(runs x entries) structure in Stage 7: the batch method takes
            /// <c>IReadOnlyList&lt;KeyValuePair&lt;string, List&lt;FdrEntry&gt;&gt;&gt;</c> and
            /// means it, so <c>--model-diagnostics</c> forced SecondPassFDR to hold every run's
            /// survivors resident and <c>CanStreamStage7Join</c> declined the streamed join
            /// outright whenever the report was asked for.
            ///
            /// <para>Eight of the nine pass-2 cards are reductions this accumulator already
            /// holds - best-per-precursor, per-file passing counts, cross-run membership and the
            /// per-base_id win-fraction maxima - so they cost a walk of the O(distinct) reduced
            /// state here rather than a walk of the pool. The ninth,
            /// <see cref="Pass2Data.CoAssignment"/>, is NOT foldable in one pass: its acceptance
            /// boundary is a reduction over every row that its per-row verdicts are then compared
            /// against, so it needs the pool twice. It is therefore built by the CALLER, from a
            /// second stream pass, and passed in - the same division pass 1 makes, where the
            /// panel comes from <c>PeakCoAssignmentSource</c> rather than from the fold.</para>
            ///
            /// <para>No <see cref="ProgressReporter"/> here, deliberately, where the batch
            /// BuildPass2 carries one per card (#4571). There the cards WERE the expensive part -
            /// six independent whole-pool walks. Here the pool walk has already happened in the
            /// caller's stream, which reports per run, and what is left walks only the reduced
            /// state. A card reporter would print six lines inside one second, on every run
            /// forever.</para>
            /// </summary>
            /// <param name="contributions">The retrained second-pass model, or null under
            /// confidence-transfer mode, which leaves the structural half null exactly as the
            /// batch path does.</param>
            /// <param name="coAssignment">The pass-2 co-assignment panel built from the caller's
            /// second stream pass; null leaves the panel out.</param>
            public Pass2Data BuildPass2(FeatureContributions contributions,
                CoAssignmentData coAssignment)
            {
                FlushOpenFile();
                if (_pass != 2)
                    throw new InvalidOperationException(PassMismatch(2, nameof(Build)));
                var precs = _best.Values.ToList();
                var pass2 = new Pass2Data();

                // Q-driven half: available whenever a second pass produced reported q-values
                // (retrain OR confidence transfer). Entrapment-independent except FdpViews.
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
                pass2.PerFile = perFile;
                pass2.IdYield = BuildIdYield(precs);

                double r = _entrapmentRatio > 0 ? _entrapmentRatio : 1.0;
                // Close the run in progress and any trailing runs that contributed nothing, so
                // every file index has its entry - the same obligation Build has, for the same
                // reason, and the one place a streamed reduction can silently disagree with the
                // batch one.
                _runStream.Finish();
                _expStream.Finish();
                _entRunStream.Finish();
                _entExpStream.Finish();
                pass2.CrossRun = new CrossRunDetection
                {
                    RunNames = _runNames,
                    PerRun = ComputeCrossRunView(_runStream, _anyEntrapment ? _entRunStream : null, _nFiles, r),
                    Experiment = ComputeCrossRunView(_expStream, _anyEntrapment ? _entExpStream : null, _nFiles, r),
                };

                pass2.FdpViews = BuildPass2FdpViews(precs, _entrapmentRatio);
                pass2.CoAssignment = coAssignment;

                // Structural half: only when the second pass retrained on the reported pool.
                // Null contributions (transfer mode) leave Model, DensityRatio and WinFraction
                // null and the report's structural cards show their n/a note.
                pass2.Model = BuildModelPass2(contributions, precs);
                if (pass2.Model != null)
                {
                    bool hasEntrapment = precs.Any(p => p.Class == EntrapmentClass.PTarget);
                    pass2.DensityRatio = BuildDensityRatio(pass2.Model.Scores, hasEntrapment);
                    pass2.WinFraction = BuildWinFractionFromReduced(_bt, _tClass);
                }
                return pass2;
            }

            // Both Build methods read reductions that only their own pass folds, so calling the
            // wrong one returns a plausible-looking object built from partly unfolded state
            // rather than failing. Name the other method: the caller's mistake is always that
            // the pass argument and the Build call disagree.
            private string PassMismatch(int expected, string otherMethod)
            {
                return string.Format(
                    @"ModelDiagnosticsData.Accumulator was constructed for pass {0} but built for pass {1}. Use {2} instead, or construct it with pass: {1}.",
                    _pass, expected, otherMethod);
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

                /// <summary>
                /// One file's passing keys, in the order its rows added them. Files arrive in
                /// file-major order, so a change of index closes the previous run. A file with none
                /// is not reported at all - as when keys were added one at a time - so its run
                /// closes with an empty set when a later file or Finish closes through it.
                /// </summary>
                internal void AddFile(int fileIdx, HashSet<string> keys)
                {
                    if (keys.Count == 0)
                        return;
                    if (fileIdx != _curFile)
                    {
                        CloseThrough(fileIdx);
                        _curFile = fileIdx;
                    }
                    _current.UnionWith(keys);
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
