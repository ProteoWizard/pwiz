using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results.Scoring;

namespace pwiz.Skyline.Model.Results
{
    /// <summary>
    /// Chromatogram results summary data for a single <see cref="DocNode"/>.
    /// This list will contain one element per replicate (i.e. full run of the nodes
    /// in this document), which may take one or more injections into the instrument to
    /// accomplish.
    /// 
    /// The set of injections is encapsulated in the <see cref="ChromatogramSet"/> class,
    /// and is not relevant to this class.  The elements in this class are ordered
    /// to correspond to the elements in the document's <see cref="ChromatogramSet"/> list
    /// in <see cref="SrmSettings.MeasuredResults"/>.  This collection will have the same
    /// number of items as the chromatograms list.
    /// </summary>
    public abstract class Results<TItem> : Immutable, IList<ChromInfoList<TItem>>
        where TItem : ChromInfo
    {
        private ReplicatePositions _replicatePositions;
        private ImmutableList<ReferenceValue<ChromFileInfoId>> _fileIds;
        protected ImmutableList<ReferenceValue<ChromFileInfoId>> FileIds
        {
            get { return _fileIds; }
        }
        public ReplicatePositions ReplicatePositions
        {
            get { return _replicatePositions; }
        }
        protected abstract TItem GetItemAt(int i);
        protected abstract void SetItems(IList<TItem> items);

        protected Results(params ChromInfoList<TItem>[] elements)
        {
            SetChromInfoLists(elements);
        }

        protected Results(IList<ChromInfoList<TItem>> elements)
        {
            SetChromInfoLists(elements);
        }

        private void SetChromInfoLists<TList>(IEnumerable<TList> chromInfoLists) where TList : IList<TItem>
        {
            List<int> counts = new List<int>();
            List<TItem> flatList = new List<TItem>();
            foreach (var chromInfoList in chromInfoLists)
            {
                if (chromInfoList == null)
                {
                    counts.Add(0);
                }
                else
                {
                    counts.Add(chromInfoList.Count);
                    flatList.AddRange(chromInfoList);
                }
            }

            _replicatePositions = ReplicatePositions.FromCounts(counts);
            SetFlatList(flatList);
        }

        private void SetFlatList(IList<TItem> items)
        {
            var newFileIds = items.Select(item => ReferenceValue.Of(item.FileId)).ToImmutable();
            if (!Equals(newFileIds, FileIds))
            {
                _fileIds = newFileIds;
            }
            SetItems(items);
        }

        public int Count
        {
            get { return ReplicatePositions.ReplicateCount; }
        }

        public ChromInfoList<TItem> this[int index]
        {
            get => new ChromInfoList<TItem>(GetItemsForReplicate(index));
            set => throw new InvalidOperationException();
        }

        private IEnumerable<TItem> GetItemsForReplicate(int index)
        {
            return Enumerable.Range(ReplicatePositions.GetStart(index), ReplicatePositions.GetCount(index)).Select(GetItemAt);
        }

        public Results<TItem> ChangeAt(int index, ChromInfoList<TItem> list)
        {
            var newList = this.ToList();
            newList[index] = list;
            return ChangeProp(ImClone(this), im =>
            {
                im.SetChromInfoLists(newList);
            });
        }

        public Results<TItem> ChangeResults<TList>(IList<TList> newItems) where TList:IList<TItem>
        {
            return ChangeProp(ImClone(this), im => im.SetChromInfoLists(newItems));
        }

        public Results<TItem> Merge(List<IList<TItem>> chromInfoSet)
        {
            if (chromInfoSet.Contains(null))
            {
                chromInfoSet = chromInfoSet.Select(list => list ?? Array.Empty<TItem>()).ToList();
            }
            // if (ContentEquals(chromInfoSet))
            // {
            //     return this;
            // }

            return ChangeResults(chromInfoSet);
        }

        protected bool Equals(Results<TItem> other)
        {
            return Equals(ReplicatePositions, other.ReplicatePositions);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((Results<TItem>)obj);
        }

        public override int GetHashCode()
        {
            return ReplicatePositions.GetHashCode();
        }

        public bool EqualsIncludingFileIds(Results<TItem> results)
        {
            return Equals(this, results) && Equals(_fileIds, results._fileIds);
        }

        private bool ContentEquals<TList>(IList<TList> lists) where TList:IEnumerable<TItem>
        {
            if (lists.Count != Count)
            {
                return false;
            }

            for (int replicateIndex = 0; replicateIndex < Count; replicateIndex++)
            {
                if (!lists[replicateIndex].SequenceEqual(GetItemsForReplicate(replicateIndex)))
                {
                    return false;
                }
            }

            return true;
        }

        public Results<TItem> ChangeChromInfo(ChromFileInfoId id, TItem newChromInfo)
        {
            var elements = new List<ChromInfoList<TItem>>();
            bool found = false;
            foreach (var replicate in this)
            {
                var chromInfoList = new List<TItem>();
                if (replicate.IsEmpty)
                {
                    elements.Add(default(ChromInfoList<TItem>));
                    continue;
                }
                foreach (var chromInfo in replicate)
                {
                    if (!ReferenceEquals(chromInfo.FileId, id))
                        chromInfoList.Add(chromInfo);
                    else
                    {
                        found = true;
                        chromInfoList.Add(newChromInfo);
                    }
                }
                elements.Add(new ChromInfoList<TItem>(chromInfoList));
            }
            if (!found)
                throw new InvalidOperationException(ResultsResources.ResultsGrid_ChangeChromInfo_Element_not_found);
            return ChangeResults(elements);
        }

        public static bool EqualsDeep(Results<TItem> resultsOld, Results<TItem> results)
        {
            if (resultsOld == null && results == null)
                return true;
            if (resultsOld == null || results == null)
                return false;
            return resultsOld.EqualsIncludingFileIds(results);
        }

        public float? GetAverageValue(Func<TItem, float?> getVal)
        {
            int valCount = 0;
            double valTotal = 0;

            foreach (var result in this)
            {
                if (result.IsEmpty)
                    continue;
                foreach (var chromInfo in result)
                {
                    if (Equals(chromInfo, default(TItem)))
                        continue;
                    float? val = getVal(chromInfo);
                    if (!val.HasValue)
                        continue;

                    valTotal += val.Value;
                    valCount++;
                }
            }

            if (valCount == 0)
                return null;

            return (float)(valTotal / valCount);            
        }

        public float? GetBestPeakValue(Func<TItem, RatedPeakValue> getVal)
        {
            double ratingBest = double.MinValue;
            float? valBest = null;

            foreach (var result in this)
            {
                if (result.IsEmpty)
                    continue;
                foreach (var chromInfo in result)
                {
                    if (Equals(chromInfo, default(TItem)))
                        continue;
                    RatedPeakValue rateVal = getVal(chromInfo);
                    if (rateVal.Rating > ratingBest)
                    {
                        ratingBest = rateVal.Rating;
                        valBest = rateVal.Value;
                    }
                }
            }

            return valBest;
        }

        public void Validate(SrmSettings settings)
        {
            var chromatogramSets = settings.MeasuredResults.Chromatograms;
            if (chromatogramSets.Count != Count)
            {
                throw new InvalidDataException(
                    string.Format(ResultsResources.Results_Validate_DocNode_results_count__0__does_not_match_document_results_count__1__,
                        Count, chromatogramSets.Count));
            }

            for (int i = 0; i < chromatogramSets.Count; i++)
            {
                var chromList = this[i];
                if (chromList.IsEmpty)
                    continue;

                var chromatogramSet = chromatogramSets[i];
                if (chromList.Any(chromInfo => chromatogramSet.IndexOfId(chromInfo.FileId) == -1))
                {
                    throw new InvalidDataException(
                        string.Format(ResultsResources.Results_Validate_DocNode_peak_info_found_for_file_with_no_match_in_document_results));
                }
            }
        }



        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<ChromInfoList<TItem>> GetEnumerator()
        {
            for (int replicateIndex = 0; replicateIndex < ReplicatePositions.ReplicateCount; replicateIndex++)
            {
                yield return new ChromInfoList<TItem>(GetItemsForReplicate(replicateIndex));
            }
        }

        public bool Contains(ChromInfoList<TItem> item)
        {
            return IndexOf(item) >= 0;
        }

        public void CopyTo(ChromInfoList<TItem>[] array, int arrayIndex)
        {
            foreach (var item in this)
            {
                array[arrayIndex++] = item;
            }
        }

        public bool IsReadOnly
        {
            get { return true; }
        }
        public int IndexOf(ChromInfoList<TItem> item)
        {
            for (int replicateIndex = 0; replicateIndex < ReplicatePositions.ReplicateCount; replicateIndex++)
            {
                if (item.SequenceEqual(GetItemsForReplicate(replicateIndex)))
                {
                    return replicateIndex;
                }
            }

            return -1;
        }

        void ICollection<ChromInfoList<TItem>>.Add(ChromInfoList<TItem> item)
        {
            throw new InvalidOperationException();
        }

        void ICollection<ChromInfoList<TItem>>.Clear()
        {
            throw new InvalidOperationException();
        }

        bool ICollection<ChromInfoList<TItem>>.Remove(ChromInfoList<TItem> item)
        {
            throw new InvalidOperationException();
        }

        void IList<ChromInfoList<TItem>>.Insert(int index, ChromInfoList<TItem> item)
        {
            throw new InvalidOperationException();
        }

        void IList<ChromInfoList<TItem>>.RemoveAt(int index)
        {
            throw new InvalidOperationException();
        }

        public Results<TItem> ValueFromCache(ValueCache valueCache)
        {
            bool anyChanges = false;
            var result = ChangeProp(ImClone(this), im =>
            {
                anyChanges = im.CacheMembers(valueCache);
            });
            if (anyChanges)
            {
                return result;
            }

            return this;
        }

        /// <summary>
        /// Replace any field values that are duplicates of values that are in the <see cref="ValueCache"/>.
        /// Returns true if any field value was changed by using an identical value from the ValueCache.
        /// </summary>
        protected virtual bool CacheMembers(ValueCache valueCache)
        {
            return CacheValue(valueCache, ref _replicatePositions) | CacheValue(valueCache, ref _fileIds);
        }

        protected static bool CacheValue<T>(ValueCache valueCache, ref T value)
        {
            var newValue = valueCache.CacheValue(value);
            bool changed = !ReferenceEquals(newValue, value);
            value = newValue;
            return changed;
        }
    }

    public class TransitionResults : Results<TransitionChromInfo>
    {
        public static readonly TransitionResults Empty = new TransitionResults();

        [CanBeNull]
        private ImmutableList<int> _optimizationSteps;
        [CanBeNull]
        private ImmutableList<int?> _massErrorInts;
        private ImmutableList<float> _retentionTimes;
        private ImmutableList<float> _startRetentionTimes;
        private ImmutableList<float> _endRetentionTimes;
        [CanBeNull]
        private ImmutableList<IonMobilityFilter> _ionMobilities;
        private ImmutableList<float> _areas;
        private ImmutableList<float> _backgroundAreas;
        private ImmutableList<float> _heights;
        private ImmutableList<float> _fwhms;
        private ImmutableList<int> _ranks;
        private ImmutableList<int> _ranksByLevel;
        [CanBeNull]
        private ImmutableList<Annotations> _annotations;
        [CanBeNull]
        private ImmutableList<UserSet> _userSets;

        private ImmutableList<PeakShapeValues?> _peakShapeValues;
        private ImmutableList<Flags> _flags;
        [CanBeNull]
        private ImmutableList<int?> _pointsAcrossPeaks;
        private ImmutableList<PeakIdentification> _peakIdentifications;


        [Flags]
        private enum Flags : byte
        {
            IsFwhmDegenerate = 1,
            IsForcedIntegration = 2,
            Truncated = 4,
            TruncatedKnown = 8,
        }
        

        protected override TransitionChromInfo GetItemAt(int i)
        {
            bool? truncated;
            var flags = _flags[i];
            if (0 == (flags & Flags.TruncatedKnown))
            {
                truncated = null;
            }
            else
            {
                truncated = 0 != (flags & Flags.Truncated);
            }


            return new TransitionChromInfo(FileIds[i], _optimizationSteps?[i] ?? 0, _massErrorInts?[i] / 10f,
                _retentionTimes[i], _startRetentionTimes[i], _endRetentionTimes[i], _ionMobilities?[i], _areas[i],
                _backgroundAreas[i], _heights[i], _fwhms[i], 0 != (_flags[i] & Flags.IsFwhmDegenerate),
                truncated, (short?)_pointsAcrossPeaks?[i], _peakIdentifications[i], (short) _ranks[i],
                (short) _ranksByLevel[i], _annotations?[i], _userSets?[i] ?? UserSet.FALSE, 0 != (_flags[i] & Flags.IsForcedIntegration),
                _peakShapeValues?[i]);
        }

        protected override void SetItems(IList<TransitionChromInfo> items)
        {
            _optimizationSteps = items.Select(item => (int)item.OptimizationStep).ToImmutable().UnlessAllEqual(0);

            _massErrorInts = items.Select(item => To10x(item.MassError)).Nullables().UnlessAllEqual(null);
            _retentionTimes = items.Select(item => item.RetentionTime).ToImmutable();
            _startRetentionTimes = items.Select(item => item.StartRetentionTime).ToImmutable();
            _endRetentionTimes = items.Select(item => item.EndRetentionTime).ToImmutable();
            _ionMobilities = items.Select(item => item.IonMobility).ToImmutable().MaybeConstant();
            _areas = items.Select(item => item.Area).ToImmutable();
            _backgroundAreas = items.Select(item => item.BackgroundArea).ToImmutable();
            _heights = items.Select(item => item.Height).ToImmutable();
            _fwhms = items.Select(item => item.Fwhm).ToImmutable();
            _ranks = items.Select(item => (int)item.Rank).ToImmutable();
            _ranksByLevel = items.Select(item => (int)item.RankByLevel).ToImmutable();
            _annotations = items.Select(item => item.Annotations).ToImmutable().UnlessAllEqual(null);
            _userSets = items.Select(item => item.UserSet).ToImmutable().UnlessAllEqual(UserSet.FALSE);
            _peakShapeValues = items.Select(item => item.PeakShapeValues).Nullables().UnlessAllEqual(null);
            _flags = items.Select(GetFlags).ToImmutable().MaybeConstant();
            _pointsAcrossPeaks = items.Select(item => (int?)item.PointsAcrossPeak).Nullables().UnlessAllEqual(null);
            _peakIdentifications = items.Select(item => item.Identified).ToImmutable().MaybeConstant();
        }

        private static int? To10x(float? value)
        {
            return value.HasValue ? (int?) ChromPeak.To10x(value.Value) : null;
        }

        private static Flags GetFlags(TransitionChromInfo transitionChromInfo)
        {
            Flags flags = 0;
            if (transitionChromInfo.IsFwhmDegenerate)
            {
                flags |= Flags.IsFwhmDegenerate;
            }

            if (transitionChromInfo.IsForcedIntegration)
            {
                flags |= Flags.IsForcedIntegration;
            }

            if (transitionChromInfo.IsTruncated.HasValue)
            {
                flags |= Flags.TruncatedKnown;
                if (transitionChromInfo.IsTruncated.Value)
                {
                    flags |= Flags.Truncated;
                }
            }

            return flags;
        }

        protected bool Equals(TransitionResults other)
        {
            return base.Equals(other) && Equals(_optimizationSteps, other._optimizationSteps) &&
                   Equals(_massErrorInts, other._massErrorInts) && Equals(_retentionTimes, other._retentionTimes) &&
                   Equals(_startRetentionTimes, other._startRetentionTimes) &&
                   Equals(_endRetentionTimes, other._endRetentionTimes) &&
                   Equals(_ionMobilities, other._ionMobilities) && Equals(_areas, other._areas) &&
                   Equals(_backgroundAreas, other._backgroundAreas) && Equals(_heights, other._heights) &&
                   Equals(_fwhms, other._fwhms) && Equals(_ranks, other._ranks) &&
                   Equals(_ranksByLevel, other._ranksByLevel) && Equals(_annotations, other._annotations) &&
                   Equals(_userSets, other._userSets) && Equals(_peakShapeValues, other._peakShapeValues) &&
                   Equals(_flags, other._flags) && Equals(_pointsAcrossPeaks, other._pointsAcrossPeaks) &&
                   Equals(_peakIdentifications, other._peakIdentifications);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((TransitionResults)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = base.GetHashCode();
                hashCode = (hashCode * 397) ^ (_optimizationSteps?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (_massErrorInts?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (_retentionTimes?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (_startRetentionTimes?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (_endRetentionTimes?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (_ionMobilities?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (_areas?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (_backgroundAreas?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (_heights?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (_fwhms?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (_ranks?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (_ranksByLevel?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (_annotations?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (_userSets?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (_peakShapeValues?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (_flags?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (_pointsAcrossPeaks?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (_peakIdentifications?.GetHashCode() ?? 0);
                return hashCode;
            }
        }

        protected override bool CacheMembers(ValueCache valueCache)
        {
            return base.CacheMembers(valueCache)
                   | CacheValue(valueCache, ref _optimizationSteps)
                   | CacheValue(valueCache, ref _retentionTimes)
                   | CacheValue(valueCache, ref _startRetentionTimes)
                   | CacheValue(valueCache, ref _endRetentionTimes)
                   | CacheValue(valueCache, ref _ionMobilities)
                   | CacheValue(valueCache, ref _ranks)
                   | CacheValue(valueCache, ref _ranksByLevel)
                   | CacheValue(valueCache, ref _peakIdentifications);
        }
    }

    public class TransitionGroupResults : Results<TransitionGroupChromInfo>
    {
        public static readonly TransitionGroupResults Empty = new TransitionGroupResults();

        [CanBeNull]
        private ImmutableList<int> _optimizationSteps;
        private ImmutableList<float> _peakCountRatios;
        [CanBeNull]
        private ImmutableList<float?> _retentionTimes;
        [CanBeNull]
        private ImmutableList<float?> _startTimes;
        [CanBeNull]
        private ImmutableList<float?> _endTimes;
        [CanBeNull]
        private ImmutableList<TransitionGroupIonMobilityInfo> _ionMobilityInfos;
        [CanBeNull]
        private ImmutableList<float?> _fwhms;
        [CanBeNull]
        private ImmutableList<float?> _areas;
        [CanBeNull]
        private ImmutableList<float?> _areasMs1;
        [CanBeNull]
        private ImmutableList<float?> _areasFragment;
        [CanBeNull]
        private ImmutableList<float?> _backgroundAreas;
        [CanBeNull]
        private ImmutableList<float?> _backgroundAreasMs1;
        [CanBeNull]
        private ImmutableList<float?> _backgroundAreasFragment;
        [CanBeNull]
        private ImmutableList<float?> _heights;
        [CanBeNull]
        private ImmutableList<float?> _massErrors;
        [CanBeNull]
        private ImmutableList<int?> _truncateds;
        private ImmutableList<PeakIdentification> _peakIdentifications;
        [CanBeNull]
        private ImmutableList<float?> _libraryDotProducts;
        [CanBeNull]
        private ImmutableList<float?> _isotopeDotProducts;
        [CanBeNull]
        private ImmutableList<float?> _qValues;
        [CanBeNull]
        private ImmutableList<float?> _zScores;
        [CanBeNull]
        private ImmutableList<Annotations> _annotations;
        [CanBeNull]
        private ImmutableList<UserSet> _userSets;

        protected override TransitionGroupChromInfo GetItemAt(int i)
        {
            return new TransitionGroupChromInfo(FileIds[i], _optimizationSteps?[i] ?? 0, _peakCountRatios[i],
                _retentionTimes?[i], _startTimes?[i], _endTimes?[i], _ionMobilityInfos?[i] ?? TransitionGroupIonMobilityInfo.EMPTY, _fwhms?[i], _areas?[i],
                _areasMs1?[i], _areasFragment?[i], _backgroundAreas?[i], _backgroundAreasMs1?[i],
                _backgroundAreasFragment?[i], _heights?[i], _massErrors?[i], _truncateds?[i], _peakIdentifications[i],
                _libraryDotProducts?[i], _isotopeDotProducts?[i], _qValues?[i], _zScores?[i],_annotations?[i], _userSets?[i] ?? UserSet.FALSE);
        }

        protected override void SetItems(IList<TransitionGroupChromInfo> items)
        {
            _optimizationSteps = items.Select(item => (int) item.OptimizationStep).ToImmutable().UnlessAllEqual(0);
            _peakCountRatios = items.Select(item => item.PeakCountRatio).ToImmutable().MaybeConstant();
            _retentionTimes = items.Select(item => item.RetentionTime).Nullables().UnlessAllNull();
            _startTimes = items.Select(item => item.StartRetentionTime).Nullables().UnlessAllNull();
            _endTimes = items.Select(item => item.EndRetentionTime).Nullables().UnlessAllNull();
            _ionMobilityInfos = items.Select(item => item.IonMobilityInfo).ToImmutable().UnlessAllEqual(TransitionGroupIonMobilityInfo.EMPTY);
            _fwhms = items.Select(item => item.Fwhm).Nullables().UnlessAllNull();
            _areas = items.Select(item => item.Area).Nullables().UnlessAllNull();
            _areasFragment = items.Select(item => item.AreaFragment).Nullables().UnlessAllNull();
            _areasMs1 = items.Select(item => item.AreaMs1).Nullables().UnlessAllNull();
            _backgroundAreas = items.Select(item => item.BackgroundArea).Nullables();
            _backgroundAreasMs1 = items.Select(item => item.BackgroundAreaMs1).Nullables().UnlessAllNull();
            _backgroundAreasFragment =
                items.Select(item => item.BackgroundAreaFragment).Nullables().UnlessAllNull();
            _heights = items.Select(item => item.Height).Nullables().UnlessAllNull();
            _massErrors = items.Select(item=>item.MassError).Nullables().UnlessAllNull();
            _truncateds = items.Select(item=>item.Truncated).Nullables().UnlessAllNull();
            _peakIdentifications = items.Select(item => item.Identified).ToImmutable().MaybeConstant();
            _libraryDotProducts = items.Select(item => item.LibraryDotProduct).Nullables().UnlessAllNull();
            _isotopeDotProducts = items.Select(item=>item.IsotopeDotProduct).Nullables().UnlessAllNull();
            _qValues = items.Select(item => item.QValue).Nullables().UnlessAllNull();
            _zScores = items.Select(item=>item.ZScore).Nullables().UnlessAllNull();
            _annotations = items.Select(item => item.Annotations).ToImmutable().UnlessAllNull();
            _userSets = items.Select(item => item.UserSet).ToImmutable().UnlessAllEqual(UserSet.FALSE);
        }

        protected bool Equals(TransitionGroupResults other)
        {
            return base.Equals(other) && Equals(_optimizationSteps, other._optimizationSteps) &&
                   Equals(_peakCountRatios, other._peakCountRatios) && Equals(_retentionTimes, other._retentionTimes) &&
                   Equals(_startTimes, other._startTimes) && Equals(_endTimes, other._endTimes) &&
                   Equals(_ionMobilityInfos, other._ionMobilityInfos) && Equals(_fwhms, other._fwhms) &&
                   Equals(_areas, other._areas) && Equals(_areasMs1, other._areasMs1) &&
                   Equals(_areasFragment, other._areasFragment) && Equals(_backgroundAreas, other._backgroundAreas) &&
                   Equals(_backgroundAreasMs1, other._backgroundAreasMs1) &&
                   Equals(_backgroundAreasFragment, other._backgroundAreasFragment) &&
                   Equals(_heights, other._heights) && Equals(_massErrors, other._massErrors) &&
                   Equals(_truncateds, other._truncateds) && Equals(_peakIdentifications, other._peakIdentifications) &&
                   Equals(_libraryDotProducts, other._libraryDotProducts) &&
                   Equals(_isotopeDotProducts, other._isotopeDotProducts) && Equals(_qValues, other._qValues) &&
                   Equals(_zScores, other._zScores) && Equals(_annotations, other._annotations) &&
                   Equals(_userSets, other._userSets);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((TransitionGroupResults)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = base.GetHashCode();
                hashCode = (hashCode * 397) ^ (_optimizationSteps?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (_peakCountRatios?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (_retentionTimes?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (_startTimes?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (_endTimes?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (_ionMobilityInfos?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (_fwhms?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (_areas?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (_areasMs1?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (_areasFragment?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (_backgroundAreas?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (_backgroundAreasMs1?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (_backgroundAreasFragment?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (_heights?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (_massErrors?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (_truncateds?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (_peakIdentifications?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (_libraryDotProducts?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (_isotopeDotProducts?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (_qValues?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (_zScores?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (_annotations?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (_userSets?.GetHashCode() ?? 0);
                return hashCode;
            }
        }

        protected override bool CacheMembers(ValueCache valueCache)
        {
            return base.CacheMembers(valueCache)
                   | CacheValue(valueCache, ref _optimizationSteps)
                   | CacheValue(valueCache, ref _peakCountRatios)
                   | CacheValue(valueCache, ref _retentionTimes)
                   | CacheValue(valueCache, ref _startTimes)
                   | CacheValue(valueCache, ref _endTimes)
                   | CacheValue(valueCache, ref _ionMobilityInfos)
                   | CacheValue(valueCache, ref _peakIdentifications);
        }
    }

    public class PeptideResults : Results<PeptideChromInfo>
    {
        public static readonly PeptideResults Empty = new PeptideResults();

        private ImmutableList<ImmutableList<PeptideLabelRatio>> _labelRatios;
        private ImmutableList<float> _peakCountRatios;
        private ImmutableList<float?> _retentionTimes;
        private ImmutableList<bool> _excludeFromCalibration;
        private ImmutableList<double?> _analyteConcentrations;

        protected override PeptideChromInfo GetItemAt(int i)
        {
            return new PeptideChromInfo(FileIds[i], _peakCountRatios[i], _retentionTimes[i], _labelRatios[i])
                .ChangeExcludeFromCalibration(_excludeFromCalibration?[i] ?? false)
                .ChangeAnalyteConcentration(_analyteConcentrations?[i]);
        }

        protected override void SetItems(IList<PeptideChromInfo> items)
        {
            _labelRatios = items.Select(item => item.LabelRatios.ToImmutable()).ToImmutable();
            _peakCountRatios = items.Select(item => item.PeakCountRatio).ToImmutable().MaybeConstant();
            _retentionTimes = items.Select(item => item.RetentionTime).Nullables();
            _excludeFromCalibration =
                items.Select(item => item.ExcludeFromCalibration).Booleans().UnlessAllEqual(false);
            _analyteConcentrations = items.Select(item => item.AnalyteConcentration).Nullables().UnlessAllNull();
        }

        protected bool Equals(PeptideResults other)
        {
            return base.Equals(other) && Equals(_labelRatios, other._labelRatios) &&
                   Equals(_peakCountRatios, other._peakCountRatios) && Equals(_retentionTimes, other._retentionTimes) &&
                   Equals(_excludeFromCalibration, other._excludeFromCalibration) &&
                   Equals(_analyteConcentrations, other._analyteConcentrations);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((PeptideResults)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = base.GetHashCode();
                hashCode = (hashCode * 397) ^ (_labelRatios?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (_peakCountRatios?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (_retentionTimes?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (_excludeFromCalibration?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (_analyteConcentrations?.GetHashCode() ?? 0);
                return hashCode;
            }
        }
    }
}