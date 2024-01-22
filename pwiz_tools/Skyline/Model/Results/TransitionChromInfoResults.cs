using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using NHibernate.UserTypes;
using pwiz.Common.Collections;
using pwiz.Common.Storage;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    public sealed class ReplicatePositions : IReadOnlyList<IEnumerable<int>>
    {
        private ImmutableList<int> _replicateEndPositions;

        public static ReplicatePositions FromResults<T>(IResults<T> results) where T : ChromInfo
        {
            return FromCounts(results.Select(chromInfoList => chromInfoList.Count));
        }

        public static ReplicatePositions FromCounts(IEnumerable<int> counts)
        {
            int total = 0;
            var endPositions = ImmutableList.ValueOf(counts.Select(count => total += count));
            return new ReplicatePositions(endPositions);
        }

        private ReplicatePositions(ImmutableList<int> endPositions)
        {
            _replicateEndPositions = endPositions;
        }

        public int Count
        {
            get { return _replicateEndPositions.Count; }
        }
        public int TotalCount
        {
            get
            {
                if (_replicateEndPositions.Count == 0)
                {
                    return 0;
                }
                return _replicateEndPositions[_replicateEndPositions.Count - 1];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<IEnumerable<int>> GetEnumerator()
        {
            return Enumerable.Range(0, Count).Select(i => this[i]).GetEnumerator();
        }

        public IEnumerable<int> this[int index] 
        {
            get
            {
                int start = index == 0 ? 0 : _replicateEndPositions[index - 1];
                int count = _replicateEndPositions[index] - start;
                return Enumerable.Range(start, count);
            }
        }
    }

    public abstract class AbstractResults : Immutable
    {
        protected AbstractResults(ImmutableList<ReferenceValue<ChromFileInfoId>> chromFileInfoIds)
        {
            ChromFileInfoIds = chromFileInfoIds;
        }

        protected AbstractResults(IEnumerable<ChromInfo> chromInfos) : this(
            ImmutableList.ValueOf(chromInfos.Select(chromInfo => ReferenceValue.Of(chromInfo.FileId))))
        {

        }
        public ImmutableList<ReferenceValue<ChromFileInfoId>> ChromFileInfoIds { get; protected set; }

        public int Count
        {
            get
            {
                return ChromFileInfoIds.Count;
            }
        }

        protected T GetListValue<T>(IReadOnlyList<T> list, int index)
        {
            return list == null ? default : list[index];
        }

        protected void SetList<T>(IEnumerator<IEnumerable> enumerator, out IReadOnlyList<T> list)
        {
            Assume.IsTrue(enumerator.MoveNext());
            list = (IReadOnlyList<T>)enumerator.Current;
        }

        [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
        protected static void StoreLists<TResult, TItem>(IEnumerable<TResult> results,
            Func<TResult, IReadOnlyList<TItem>> getFunc, Action<TResult, IReadOnlyList<TItem>> setFunc)
        {
            using var enResults = results.GetEnumerator();
            foreach (var newList in EfficientListStorage<TItem>.StoreLists(results.Select(getFunc)))
            {
                Assume.IsTrue(enResults.MoveNext());
                setFunc(enResults.Current, newList);
            }
        }
    }

    public class TransitionChromInfoResults : AbstractResults, IReadOnlyList<TransitionChromInfo>
    {
        public TransitionChromInfoResults(ImmutableList<ReferenceValue<ChromFileInfoId>> chromFileInfoIds,
            IEnumerable<IEnumerable> lists):base(chromFileInfoIds)
        {
            using var en = lists.GetEnumerator();
            SetList(en, out OptimizationStep);
            SetList(en, out MassError);
            SetList(en, out RetentionTime);
            SetList(en, out StartRetentionTime);
            SetList(en, out EndRetentionTime);
            SetList(en, out IonMobility);
            SetList(en, out Area);
            SetList(en, out BackgroundArea);
            SetList(en, out Height);
            SetList(en, out Fwhm);
            SetList(en, out IsFwhmDegenerate);
            SetList(en, out IsTruncated);
            SetList(en, out PointsAcrossPeak);
            SetList(en, out Identified);
            SetList(en, out Rank);
            SetList(en, out RankByLevel);
            SetList(en, out Annotations);
            SetList(en, out UserSet);
            SetList(en, out IsForcedIntegration);
            SetList(en, out PeakShapeValues);
            Assume.IsFalse(en.MoveNext());
        }

        public static TransitionChromInfoResults FromResults(IResults<TransitionChromInfo> results)
        {
            var transitionChromInfoResults =
                (results as ResultsImpl<TransitionChromInfo>)?.List as TransitionChromInfoResults;
            if (transitionChromInfoResults != null)
            {
                return transitionChromInfoResults;
            }

            return new TransitionChromInfoResults(results.SelectMany(chromInfoList => chromInfoList).ToList());
        }

        public TransitionChromInfoResults(ICollection<TransitionChromInfo> chromInfos) : base(chromInfos)
        {
            OptimizationStep = chromInfos.Select(c => c.OptimizationStep).ToList();
            MassError = chromInfos.Select(c => c.MassError).ToList();
            RetentionTime = chromInfos.Select(c => c.RetentionTime).ToList();
            StartRetentionTime = chromInfos.Select(c => c.StartRetentionTime).ToList();
            EndRetentionTime = chromInfos.Select(c => c.EndRetentionTime).ToList();
            IonMobility = chromInfos.Select(c => c.IonMobility).ToList();
            Area = chromInfos.Select(c => c.Area).ToList();
            BackgroundArea = chromInfos.Select(c => c.BackgroundArea).ToList();
            Height = chromInfos.Select(c => c.Height).ToList();
            Fwhm = chromInfos.Select(c => c.Fwhm).ToList();
            IsFwhmDegenerate = chromInfos.Select(c => c.IsFwhmDegenerate).ToList();
            IsTruncated = chromInfos.Select(c => c.IsTruncated).ToList();
            PointsAcrossPeak = chromInfos.Select(c => c.PointsAcrossPeak).ToList();
            Identified = chromInfos.Select(c => c.Identified).ToList();
            Rank = chromInfos.Select(c=>c.Rank).ToList();
            RankByLevel = chromInfos.Select(c => c.RankByLevel).ToList();
            Annotations = chromInfos.Select(c => c.Annotations).ToList();
            UserSet = chromInfos.Select(c => c.UserSet).ToList();
            IsForcedIntegration = chromInfos.Select(c => c.IsForcedIntegration).ToList();
            PeakShapeValues = chromInfos.Select(c => c.PeakShapeValues).ToList();
        }

        public TransitionChromInfoResults(IResults<TransitionChromInfo> results) : this(results.SelectMany(r => r)
            .ToList())
        {

        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<TransitionChromInfo> GetEnumerator()
        {
            return Enumerable.Range(0, Count).Select(GetTransitionChromInfo).GetEnumerator();
        }

        public TransitionChromInfo this[int index] => GetTransitionChromInfo(index);

        private IReadOnlyList<short> OptimizationStep;
        private IReadOnlyList<float?> MassError;
        private IReadOnlyList<float> RetentionTime;
        private IReadOnlyList<float> StartRetentionTime;
        private IReadOnlyList<float> EndRetentionTime;
        private IReadOnlyList<IonMobilityFilter> IonMobility;
        private IReadOnlyList<float> Area;
        private IReadOnlyList<float> BackgroundArea;
        private IReadOnlyList<float> Height;
        private IReadOnlyList<float> Fwhm;
        private IReadOnlyList<bool> IsFwhmDegenerate;
        private IReadOnlyList<bool?> IsTruncated;
        private IReadOnlyList<short?> PointsAcrossPeak;
        private IReadOnlyList<PeakIdentification> Identified;
        private IReadOnlyList<short> Rank;
        private IReadOnlyList<short> RankByLevel;
        private IReadOnlyList<Annotations> Annotations;
        private IReadOnlyList<UserSet> UserSet;
        private IReadOnlyList<bool> IsForcedIntegration;
        private IReadOnlyList<PeakShapeValues?> PeakShapeValues;
        public TransitionChromInfo GetTransitionChromInfo(int index)
        {
            return new TransitionChromInfo(GetListValue(ChromFileInfoIds, index),
                GetListValue(OptimizationStep, index), GetListValue(MassError, index),
                GetListValue(RetentionTime, index), GetListValue(StartRetentionTime, index),
                GetListValue(EndRetentionTime, index),
                GetListValue(IonMobility, index), GetListValue(Area, index),
                GetListValue(BackgroundArea, index), GetListValue(Height, index),
                GetListValue(Fwhm, index), GetListValue(IsFwhmDegenerate, index), GetListValue(IsTruncated, index),
                GetListValue(PointsAcrossPeak, index),
                GetListValue(Identified, index), GetListValue(Rank, index), GetListValue(RankByLevel, index),
                GetListValue(Annotations, index),
                GetListValue(UserSet, index), GetListValue(IsForcedIntegration, index),
                GetListValue(PeakShapeValues, index));
        }

        public IEnumerable<IEnumerable> GetLists()
        {
            return new IEnumerable[]
            {
                OptimizationStep,
                MassError,
                RetentionTime,
                StartRetentionTime,
                EndRetentionTime,
                IonMobility,
                Area,
                BackgroundArea,
                Height,
                Fwhm,
                IsFwhmDegenerate,
                IsTruncated,
                PointsAcrossPeak,
                Identified,
                Rank,
                RankByLevel,
                Annotations,
                UserSet,
                IsForcedIntegration,
                PeakShapeValues
            };
        }

        public static IEnumerable<TransitionDocNode> StoreResults(IEnumerable<TransitionDocNode> transitionDocNodes)
        {
            var newNodes = transitionDocNodes.ToArray();
            var transitionChromInfoResultsList = new List<Tuple<int, TransitionChromInfoResults>>();
            for (int i = 0; i < newNodes.Length; i++)
            {
                var docNode = newNodes[i];
                if (docNode.Results == null || !docNode.Results.Skip(1).Any())
                {
                    continue;
                }
                transitionChromInfoResultsList.Add(Tuple.Create(i, new TransitionChromInfoResults(docNode.Results)));
            }

            if (transitionChromInfoResultsList.Count == 0)
            {
                return newNodes;
            }

            StoreResults(transitionChromInfoResultsList.Select(tuple => tuple.Item2).ToList());
            ValueCache valueCache = new ValueCache();
            foreach (var tuple in transitionChromInfoResultsList)
            {
                var transitionDocNode = newNodes[tuple.Item1];
                var replicatePositions =
                    valueCache.CacheValue(ReplicatePositions.FromResults(transitionDocNode.Results));
                transitionDocNode =
                    transitionDocNode.ChangeResults(
                        new ResultsImpl<TransitionChromInfo>(replicatePositions, tuple.Item2));
                newNodes[tuple.Item1] = transitionDocNode;
            }

            return newNodes;
        }

        private static void StoreResults(IList<TransitionChromInfoResults> resultsList)
        {
            StoreLists(resultsList, r => r.OptimizationStep, (r, l) => r.OptimizationStep = l);
            StoreLists(resultsList, r=>r.MassError, (r,l)=>r.MassError = l);
            StoreLists(resultsList, r=>r.RetentionTime, (r,l)=>r.RetentionTime = l);
            StoreLists(resultsList, r=>r.StartRetentionTime, (r,l)=>r.StartRetentionTime = l);
            StoreLists(resultsList, r=>r.EndRetentionTime, (r,l)=>r.EndRetentionTime = l);
            StoreLists(resultsList, r=>r.IonMobility, (r,l)=>r.IonMobility = l);
            StoreLists(resultsList, r=>r.Area, (r,l)=>r.Area = l);
            StoreLists(resultsList, r=>r.BackgroundArea, (r,l)=>r.BackgroundArea = l);
            StoreLists(resultsList, r=>r.Height, (r,l)=>r.Height = l);
            StoreLists(resultsList, r => r.Fwhm, (r, l) => r.Fwhm = l);
            StoreLists(resultsList, r=>r.IsFwhmDegenerate, (r,l)=>r.IsFwhmDegenerate = l);
            StoreLists(resultsList, r=>r.IsTruncated, (r,l)=>r.IsTruncated = l);
            StoreLists(resultsList, r=>r.PointsAcrossPeak, (r,l)=>r.PointsAcrossPeak = l);
            StoreLists(resultsList, r=>r.Identified, (r,l)=>r.Identified = l);
            StoreLists(resultsList, r=>r.Rank, (r,l)=>r.Rank = l);
            StoreLists(resultsList, r=>r.RankByLevel, (r,l)=>r.RankByLevel = l);
            StoreLists(resultsList, r=>r.Annotations, (r,l)=>r.Annotations = l);
            StoreLists(resultsList, r=>r.UserSet, (r,l)=>r.UserSet = l);
            StoreLists(resultsList, r=>r.IsForcedIntegration, (r,l)=>r.IsForcedIntegration = l);
            StoreLists(resultsList, r=>r.PeakShapeValues, (r,l)=>r.PeakShapeValues = l);
        }
    }

    public class ResultsImpl<T> : IResults<T> where T : ChromInfo
    {
        public ResultsImpl(ReplicatePositions replicatePositions, IReadOnlyList<T> list)
        {
            ReplicatePositions = replicatePositions;
            List = list;
        }

        public ReplicatePositions ReplicatePositions { get; private set; }
        public IReadOnlyList<T> List { get; private set; }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<ChromInfoList<T>> GetEnumerator()
        {
            return Enumerable.Range(0, Count).Select(i => this[i]).GetEnumerator();
        }

        public int Count
        {
            get { return ReplicatePositions.Count; }
        }

        public ChromInfoList<T> this[int index]
        {
            get
            {
                return new ChromInfoList<T>(ReplicatePositions[index].Select(position => List[position]));
            }
        }
    }
}
