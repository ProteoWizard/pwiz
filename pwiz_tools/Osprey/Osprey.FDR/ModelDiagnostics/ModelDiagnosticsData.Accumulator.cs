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
        /// <see cref="FdrEntry"/> pool resident. Fed one row at a time from the projection
        /// score-pass sink (<c>FdrProjectionSinkBase.Accept</c>) in nested (file, row) order;
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
        /// the reduced state. So the streamed reduction reproduces the resident reduction
        /// element-for-element, while the 340M-row pre-compaction pool that OOM'd an 82-file
        /// --model-diagnostics run at the join is never materialized -- the accumulator holds only
        /// ~unique-precursor / ~base_id-sized maps.
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

            // Per-file passing key-sets for the cross-run reproducibility view
            // (== BuildCrossRunDetection): real targets (run/exp gate) + entrapment (run/exp gate).
            private readonly List<HashSet<string>> _runSets;
            private readonly List<HashSet<string>> _expSets;
            private readonly List<HashSet<string>> _entRunSets;
            private readonly List<HashSet<string>> _entExpSets;
            private bool _anyEntrapment;

            // Win fraction: base_id -> [best target score, best decoy score] + target-side class
            // (== BuildWinFraction).
            private readonly Dictionary<uint, double[]> _bt = new Dictionary<uint, double[]>();
            private readonly Dictionary<uint, EntrapmentClass> _tClass =
                new Dictionary<uint, EntrapmentClass>();

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
                _runSets = NewSets(_nFiles);
                _expSets = NewSets(_nFiles);
                _entRunSets = NewSets(_nFiles);
                _entExpSets = NewSets(_nFiles);
            }

            private static List<HashSet<string>> NewSets(int n)
            {
                var list = new List<HashSet<string>>(n);
                for (int i = 0; i < n; i++)
                    list.Add(new HashSet<string>(StringComparer.Ordinal));
                return list;
            }

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
                            _entRunSets[fileIdx].Add(key);
                            if (expOk)
                                _entExpSets[fileIdx].Add(key);
                            _anyEntrapment = true;
                        }
                        else
                        {
                            _runSets[fileIdx].Add(key);
                            if (expOk)
                                _expSets[fileIdx].Add(key);
                        }
                    }
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
                data.CrossRun = new CrossRunDetection
                {
                    RunNames = _runNames,
                    PerRun = ComputeCrossRunView(_runSets, _anyEntrapment ? _entRunSets : null, _nFiles, r),
                    Experiment = ComputeCrossRunView(_expSets, _anyEntrapment ? _entExpSets : null, _nFiles, r),
                };

                data.WinFraction = BuildWinFractionFromReduced(_bt, _tClass);

                if (data.HasEntrapment)
                    data.FdpViews = BuildFdpViewsFromPrecs(precs, r, 1);

                return data;
            }
        }
    }
}
