/*
 * Original author: Kaipo Tamura <kaipot .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2019 University of Washington - Seattle, WA
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
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.SettingsUI;

namespace pwiz.Skyline.Model.Irt
{
    public class IrtPeptidePicker
    {
        private ScoredPeptide[] _scoredPeptides;
        private ScoredPeptide[] _cirtPeptides;
        private readonly Dictionary<Target, double> _cirtAll;

        public IrtPeptidePicker()
        {
            _scoredPeptides = null;
            _cirtPeptides = null;
            _cirtAll = IrtStandard.CIRT.Peptides.ToDictionary(pep => pep.ModifiedTarget, pep => pep.Irt);
        }

        public bool HasScoredPeptides => _scoredPeptides != null && _scoredPeptides.Length > 0;
        public int CirtPeptideCount => _cirtPeptides != null ? _cirtPeptides.Length : 0;
        private double MinRt => _scoredPeptides.First().Peptide.RetentionTime;
        private double MaxRt => _scoredPeptides.Last().Peptide.RetentionTime;
        private double RtRange => MaxRt - MinRt;
        private IEnumerable<double> BucketBoundaries => new[]
        {
            MinRt + RtRange * 1 / 8,
            MinRt + RtRange * 2 / 8,
            MinRt + RtRange * 4 / 8,
            MinRt + RtRange * 6 / 8,
            MinRt + RtRange * 7 / 8,
            double.MaxValue
        };

        public double? CirtIrt(Target target)
        {
            return _cirtAll.TryGetValue(target, out var irt) ? irt : (double?) null;
        }

        public void ScorePeptides(SrmDocument doc, IProgressMonitor progressMonitor)
        {
            var model = doc.Settings.PeptideSettings.Integration.PeakScoringModel;
            if (model == null || !model.IsTrained)
                model = LegacyScoringModel.DEFAULT_MODEL;

            var mProphetResultsHandler = new MProphetResultsHandler(doc, model);
            mProphetResultsHandler.ScoreFeatures(progressMonitor, true);
            if (progressMonitor.IsCanceled)
                return;

            var scoredPeptidesDict = new Dictionary<Target, ScoredPeptide>();
            foreach (var nodePep in doc.Molecules.Where(pep => pep.PercentileMeasuredRetentionTime.HasValue && !pep.IsDecoy))
            {
                var allStats = doc.MeasuredResults.MSDataFileInfos
                    .Select(info => mProphetResultsHandler.GetPeakFeatureStatistics(nodePep.Id.GlobalIndex, info.FileId.GlobalIndex))
                    .Where(stats => stats != null).ToArray();
                var value = float.MaxValue;
                if (allStats.Length > 0)
                {
                    value = model is MProphetPeakScoringModel
                        ? allStats.Select(stats => stats.QValue.Value).Max()
                        : -allStats.Select(stats => stats.BestScore).Min();
                }
                if (!scoredPeptidesDict.TryGetValue(nodePep.ModifiedTarget, out var existing) || value < existing.Score)
                    scoredPeptidesDict[nodePep.ModifiedTarget] = new ScoredPeptide(
                        new MeasuredPeptide(doc.Settings.GetModifiedSequence(nodePep), nodePep.PercentileMeasuredRetentionTime.Value), nodePep, value);
            }
            _scoredPeptides = scoredPeptidesDict.Values.OrderBy(pep => pep.Peptide.RetentionTime).ToArray();
            _cirtPeptides = _scoredPeptides.Where(pep => _cirtAll.ContainsKey(pep.Peptide.Target)).ToArray();
        }

        public bool TryGetCirtRegression(int count, out RegressionLine regression, out IEnumerable<Tuple<DbIrtPeptide, PeptideDocNode>> matchedPeptides)
        {
            matchedPeptides = null;
            var success = TryGetCirtRegression(count, out regression, out List<ScoredPeptide> peptides);
            if (success)
            {
                matchedPeptides = peptides.Select(pep => Tuple.Create(
                    new DbIrtPeptide(pep.Peptide.Target, _cirtAll[pep.Peptide.Target], true, TimeSource.peak),
                    pep.NodePep));
            }
            return success;
        }

        private bool TryGetCirtRegression(int count, out RegressionLine regression, out List<ScoredPeptide> peptides)
        {
            peptides = new List<ScoredPeptide>(_cirtPeptides);
            var rts = _cirtPeptides.Select(pep => pep.Peptide.RetentionTime).ToList();
            var irts = _cirtPeptides.Select(pep => _cirtAll[pep.Peptide.Target]).ToList();
            var removedValues = new List<Tuple<double, double>>();
            if (!RCalcIrt.TryGetRegressionLine(rts, irts, count, out regression, removedValues))
                return false;

            for (var i = peptides.Count - 1; i >= 0; i--)
            {
                if (removedValues.Contains(Tuple.Create(rts[i], irts[i])))
                    peptides.RemoveAt(i);
            }
            return peptides.Count >= count;
        }

        /// <summary>
        /// This algorithm will determine a number of evenly spaced retention times for the given document,
        /// and then determine an optimal set of peptides from the document. That is, a set of peptides that
        /// are as close as possible to the chosen retention times.
        ///
        /// The returned list is guaranteed to be sorted by retention time.
        /// </summary>
        /// <param name="count">The number of peptides to be picked</param>
        /// <param name="exclude">Peptides that cannot be picked</param>
        /// <param name="cirt">Use CiRT peptides, if possible</param>
        public List<MeasuredPeptide> Pick(int count, ICollection<Target> exclude, bool cirt)
        {
            PeptideBucket[] buckets = null;
            if (cirt && TryGetCirtRegression(count, out _, out List<ScoredPeptide> scoredCirtPeptides))
            {
                // If each bucket contains at least one, prompt to use CiRT peptides
                var cirtBuckets = PeptideBucket.BucketPeptides(scoredCirtPeptides, BucketBoundaries);
                if (cirtBuckets.All(bucket => !bucket.Empty))
                    buckets = cirtBuckets;
            }

            if (buckets == null)
                buckets = exclude == null || exclude.Count == 0
                    ? PeptideBucket.BucketPeptides(_scoredPeptides, BucketBoundaries)
                    : PeptideBucket.BucketPeptides(_scoredPeptides.Where(pep => !exclude.Contains(pep.Peptide.Target)), BucketBoundaries);
            var endBuckets = new[] { buckets.First(), buckets.Last() };
            var midBuckets = buckets.Skip(1).Take(buckets.Length - 2).ToArray();

            var bestPeptides = new List<MeasuredPeptide>();
            while (bestPeptides.Count < count && buckets.Any(bucket => !bucket.Empty))
            {
                bestPeptides.AddRange(PeptideBucket.Pop(endBuckets, endBuckets.Length, true).Take(Math.Min(endBuckets.Length, count - bestPeptides.Count)));
                bestPeptides.AddRange(PeptideBucket.Pop(midBuckets, midBuckets.Length, false).Take(Math.Min(midBuckets.Length, count - bestPeptides.Count)));
            }
            bestPeptides.Sort((x, y) => x.RetentionTime.CompareTo(y.RetentionTime));
            return bestPeptides;
        }

        private class ScoredPeptide
        {
            public MeasuredPeptide Peptide { get; }
            public PeptideDocNode NodePep { get; }
            public float Score { get; }

            public ScoredPeptide(MeasuredPeptide peptide, PeptideDocNode nodePep, float score)
            {
                Peptide = peptide;
                NodePep = nodePep;
                Score = score;
            }
        }

        private class PeptideBucket
        {
            private readonly double _maxRetentionTime;
            private readonly List<ScoredPeptide> _peptides;

            public bool Empty => _peptides.Count == 0;

            private PeptideBucket(double maxRetentionTime)
            {
                _maxRetentionTime = maxRetentionTime;
                _peptides = new List<ScoredPeptide>();
            }

            private float? Peek()
            {
                return !Empty ? (float?)_peptides.First().Score : null;
            }

            private MeasuredPeptide Pop()
            {
                if (Empty)
                    return null;
                var pep = new MeasuredPeptide(_peptides.First().Peptide);
                _peptides.RemoveAt(0);
                return pep;
            }

            public static PeptideBucket[] BucketPeptides(IEnumerable<ScoredPeptide> peptides, IEnumerable<double> rtBoundaries)
            {
                // peptides must be sorted by retention time (low to high)
                var buckets = rtBoundaries.OrderBy(x => x).Select(boundary => new PeptideBucket(boundary)).ToArray();
                var curBucketIdx = 0;
                var curBucket = buckets[0];
                foreach (var pep in peptides)
                {
                    if (pep.Peptide.RetentionTime > curBucket._maxRetentionTime)
                        curBucket = buckets[++curBucketIdx];
                    curBucket._peptides.Add(pep);
                }
                buckets.ForEach(bucket => bucket._peptides.Sort((x, y) => x.Score.CompareTo(y.Score)));
                return buckets;
            }

            public static IEnumerable<MeasuredPeptide> Pop(PeptideBucket[] buckets, int num, bool limitOne)
            {
                // buckets must be sorted by score (best to worst)
                var popped = 0;
                while (popped < num)
                {
                    var validBuckets = buckets.Where(bucket => !bucket.Empty).OrderBy(bucket => bucket.Peek().Value).ToArray();
                    foreach (var bucket in validBuckets)
                    {
                        yield return bucket.Pop();
                        if (++popped == num)
                            yield break;
                    }
                    if (validBuckets.Length == 0 || limitOne)
                        yield break;
                }
            }
        }
    }
}
