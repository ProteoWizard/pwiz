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
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Irt
{
    public class MeasuredPeptide : IPeptideData
    {
        public MeasuredPeptide()
        {
        }

        public MeasuredPeptide(Target seq, double rt)
        {
            Target = seq;
            RetentionTime = rt;
        }

        public MeasuredPeptide(MeasuredPeptide other) : this(other.Target, other.RetentionTime)
        {
        }

        public Target Target { get; set; }
        public double RetentionTime { get; set; }
        public string Sequence { get { return Target == null ? string.Empty : Target.ToSerializableString(); } }

        public static string ValidateSequence(Target sequence)
        {
            if (sequence.IsEmpty)
                return Resources.MeasuredPeptide_ValidateSequence_A_modified_peptide_sequence_is_required_for_each_entry;
            if (sequence.IsProteomic)
            {
                if (!FastaSequence.IsExSequence(sequence.Sequence))
                    return string.Format(Resources.MeasuredPeptide_ValidateSequence_The_sequence__0__is_not_a_valid_modified_peptide_sequence, sequence);
            }
            return null;
        }

        public static string ValidateRetentionTime(string rtText, bool allowNegative)
        {
            double rtValue;
            if (rtText == null || !double.TryParse(rtText, out rtValue))
                return Resources.MeasuredPeptide_ValidateRetentionTime_Measured_retention_times_must_be_valid_decimal_numbers;
            if (!allowNegative && rtValue <= 0)
                return Resources.MeasuredPeptide_ValidateRetentionTime_Measured_retention_times_must_be_greater_than_zero;
            return null;
        }
    }

    public class IrtPeptidePicker
    {
        private ScoredPeptide[] _scoredPeptides;
        private ScoredPeptide[] _cirtPeptides;
        private readonly TargetMap<double> _cirtAll;

        public IrtPeptidePicker()
        {
            _scoredPeptides = null;
            _cirtPeptides = null;
            _cirtAll = new TargetMap<double>(IrtStandard.CIRT.Peptides.Select(pep =>
                new KeyValuePair<Target, double>(pep.ModifiedTarget, pep.Irt)));
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
                    .Select(info => mProphetResultsHandler.GetPeakFeatureStatistics(nodePep.Peptide, info.FileId))
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
            var success = IrtRegression.TryGet<RegressionLine>(rts, irts, count, out var line, removedValues);
            regression = (RegressionLine) line;
            if (!success)
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
            PeptideBucket<ScoredPeptide>[] buckets = null;
            if (cirt && TryGetCirtRegression(count, out _, out List<ScoredPeptide> scoredCirtPeptides))
            {
                // If each bucket contains at least one, prompt to use CiRT peptides
                var cirtBuckets = PeptideBucket<ScoredPeptide>.BucketPeptides(scoredCirtPeptides, BucketBoundaries);
                if (cirtBuckets.All(bucket => !bucket.Empty))
                    buckets = cirtBuckets;
            }

            if (buckets == null)
                buckets = exclude == null || exclude.Count == 0
                    ? PeptideBucket<ScoredPeptide>.BucketPeptides(_scoredPeptides, BucketBoundaries)
                    : PeptideBucket<ScoredPeptide>.BucketPeptides(_scoredPeptides.Where(pep => !exclude.Contains(pep.Peptide.Target)), BucketBoundaries);
            var endBuckets = new[] { buckets.First(), buckets.Last() };
            var midBuckets = buckets.Skip(1).Take(buckets.Length - 2).ToArray();

            var bestPeptides = new List<MeasuredPeptide>();
            while (bestPeptides.Count < count && buckets.Any(bucket => !bucket.Empty))
            {
                bestPeptides.AddRange(PeptideBucket<ScoredPeptide>.Pop(endBuckets, endBuckets.Length, true)
                    .Take(Math.Min(endBuckets.Length, count - bestPeptides.Count)).Select(pep => pep.Peptide));
                bestPeptides.AddRange(PeptideBucket<ScoredPeptide>.Pop(midBuckets, midBuckets.Length, false)
                    .Take(Math.Min(midBuckets.Length, count - bestPeptides.Count)).Select(pep => pep.Peptide));
            }
            bestPeptides.Sort((x, y) => x.RetentionTime.CompareTo(y.RetentionTime));
            return bestPeptides.Select(pep => new MeasuredPeptide(pep)).ToList();
        }

        public static IEnumerable<Target> Pick(int count, DbIrtPeptide[] peptides, IEnumerable<Target> outliers)
        {
            var targets = new TargetMap<List<DbIrtPeptide>>(peptides.Select(pep =>
                new KeyValuePair<Target, List<DbIrtPeptide>>(pep.ModifiedTarget, new List<DbIrtPeptide>())));
            foreach (var pep in peptides)
                targets[pep.ModifiedTarget].Add(pep);
            var distinctPeps = new List<DbIrtPeptide>();
            foreach (var list in targets.Values)
            {
                if (list.Count == 0)
                    continue;

                var median = new Statistics(list.Select(pep => pep.Irt)).Median();
                DbIrtPeptide best = null;
                var minDiff = double.MaxValue;
                foreach (var pep in list)
                {
                    var diff = Math.Abs(pep.Irt - median);
                    if (diff < minDiff)
                    {
                        minDiff = diff;
                        best = pep;
                    }
                }
                distinctPeps.Add(best);
            }

            var outlierMap = new TargetMap<bool>(outliers.Select(target => new KeyValuePair<Target, bool>(target, true)));
            if (distinctPeps.Count(pep => !outlierMap.ContainsKey(pep.ModifiedTarget)) >= count)
            {
                // don't use outliers if we have enough other values
                distinctPeps.RemoveAll(pep => outlierMap.ContainsKey(pep.ModifiedTarget));
            }

            distinctPeps.Sort((x, y) => x.Irt.CompareTo(y.Irt));
            var minIrt = distinctPeps.First().Irt;
            var maxIrt = distinctPeps.Last().Irt;
            var gradientLength = maxIrt - minIrt;
            for (var i = 0; i < count; i++)
            {
                var targetRt = minIrt + i * (gradientLength / (count - 1));
                for (var j = 0; j < distinctPeps.Count; j++)
                {
                    if (j + 1 > distinctPeps.Count - 1 ||
                        Math.Abs(distinctPeps[j].Irt - targetRt) < Math.Abs(distinctPeps[j + 1].Irt - targetRt))
                    {
                        yield return distinctPeps[j].ModifiedTarget;
                        distinctPeps.RemoveAt(j);
                        break;
                    }
                }
            }
        }

        public static void SetStandards(IEnumerable<DbIrtPeptide> peptides, IEnumerable<Target> standards)
        {
            var standardMap = new TargetMap<bool>(standards.Select(target => new KeyValuePair<Target, bool>(target, true)));
            foreach (var pep in peptides.Where(pep => standardMap.ContainsKey(pep.ModifiedTarget)))
                pep.Standard = true;
        }

        public static void SetStandards(IEnumerable<DbIrtPeptide> peptides, IrtStandard standard)
        {
            SetStandards(peptides, standard.Peptides.Select(pep => pep.ModifiedTarget));
        }

        private interface IBucketable
        {
            double Time { get; }
            float Score { get; } // lower scores get picked first
        }

        private class ScoredPeptide : IBucketable
        {
            public MeasuredPeptide Peptide { get; }
            public PeptideDocNode NodePep { get; }
            public float Score { get; }

            public double Time => Peptide.RetentionTime;

            public ScoredPeptide(MeasuredPeptide peptide, PeptideDocNode nodePep, float score)
            {
                Peptide = peptide;
                NodePep = nodePep;
                Score = score;
            }
        }

        private class PeptideBucket<T> where T : IBucketable
        {
            private readonly double _maxTime;
            private readonly List<T> _peptides;

            public bool Empty => _peptides.Count == 0;

            private PeptideBucket(double maxTime)
            {
                _maxTime = maxTime;
                _peptides = new List<T>();
            }

            private float? Peek()
            {
                return !Empty ? (float?)_peptides.First().Score : null;
            }

            private T Pop()
            {
                if (Empty)
                    return default(T);
                var pep = _peptides.First();
                _peptides.RemoveAt(0);
                return pep;
            }

            public static PeptideBucket<T>[] BucketPeptides(IEnumerable<T> peptides, IEnumerable<double> rtBoundaries)
            {
                // peptides must be sorted by retention time (low to high)
                var buckets = rtBoundaries.OrderBy(x => x).Select(boundary => new PeptideBucket<T>(boundary)).ToArray();
                var curBucketIdx = 0;
                var curBucket = buckets[0];
                foreach (var pep in peptides)
                {
                    if (pep.Time > curBucket._maxTime)
                        curBucket = buckets[++curBucketIdx];
                    curBucket._peptides.Add(pep);
                }
                buckets.ForEach(bucket => bucket._peptides.Sort((x, y) => x.Score.CompareTo(y.Score)));
                return buckets;
            }

            public static IEnumerable<T> Pop(PeptideBucket<T>[] buckets, int num, bool limitOne)
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

        public static IEnumerable<DbIrtPeptide> Pick(IEnumerable<IRetentionTimeProvider> providers, int numPick)
        {
            double minRt = double.MaxValue;
            double maxRt = double.MinValue;

            var times = new Dictionary<Target, List<double>>();
            foreach (var provider in providers)
            {
                foreach (var rt in provider.PeptideRetentionTimes)
                {
                    if (rt.RetentionTime < minRt)
                        minRt = rt.RetentionTime;
                    if (rt.RetentionTime > maxRt)
                        maxRt = rt.RetentionTime;
                    if (!times.ContainsKey(rt.PeptideSequence))
                        times[rt.PeptideSequence] = new List<double>();
                    times[rt.PeptideSequence].Add(rt.RetentionTime);
                }
            }

            numPick = Math.Min(numPick, times.Keys.Count);
            var targetStats = times.ToDictionary(pair => pair.Key, pair => new Statistics(pair.Value));

            var binSize = (maxRt - minRt) / numPick;
            var curBinLimit = minRt;
            var picked = new List<DbIrtPeptide>();
            while (targetStats.Count > 0)
            {
                Dictionary<Target, Statistics> curBinCandidates;
                if (picked.Count < numPick - 1)
                {
                    curBinLimit += binSize;
                    curBinCandidates = targetStats.Where(pair => pair.Value.Median() <= curBinLimit).ToDictionary(pair => pair.Key, pair => pair.Value);
                    foreach (var target in curBinCandidates.Select(pair => pair.Key))
                        targetStats.Remove(target);
                }
                else
                {
                    curBinCandidates = new Dictionary<Target, Statistics>(targetStats);
                    targetStats.Clear();
                }
                if (curBinCandidates.Count == 0)
                    continue;
                var maxCount = curBinCandidates.Max(x => x.Value.Length);
                curBinCandidates = curBinCandidates.Where(pair => pair.Value.Length == maxCount).ToDictionary(pair => pair.Key, pair => pair.Value);
                Target best = null;
                if (maxCount == 1)
                {
                    var minDiff = double.MaxValue;
                    var binCenter = curBinLimit - binSize / 2;
                    foreach (var target in curBinCandidates)
                    {
                        var diff = Math.Abs(target.Value.Mean() - binCenter);
                        if (diff < minDiff)
                        {
                            minDiff = diff;
                            best = target.Key;
                        }
                    }
                }
                else
                {
                    var minVariance = double.MaxValue;
                    foreach (var target in curBinCandidates)
                    {
                        var variance = target.Value.Variance();
                        if (variance < minVariance)
                        {
                            minVariance = variance;
                            best = target.Key;
                        }
                    }
                }
                // ReSharper disable once AssignNullToNotNullAttribute
                picked.Add(new DbIrtPeptide(best, curBinCandidates[best].Median(), true, TimeSource.peak));
            }
            return picked;
        }
    }
}
