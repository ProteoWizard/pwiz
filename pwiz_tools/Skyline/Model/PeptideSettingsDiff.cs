using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;

namespace pwiz.Skyline.Model
{
    public class PeptideSettingsDiff
    {
        private static readonly IEqualityComparer<ChromFileInfoId> _fileIdComparer =
            new IdentityEqualityComparer<ChromFileInfoId>();
        private Dictionary<ChromatogramListKey, ImmutableList<ChromatogramGroupInfo>> _chromatogramLists =
            new Dictionary<ChromatogramListKey, ImmutableList<ChromatogramGroupInfo>>();
        public PeptideSettingsDiff(SrmSettings settings, PeptideDocNode nodePep) 
            : this(settings, nodePep, new SrmSettingsDiff(settings, true))
        {
        }
        public PeptideSettingsDiff(SrmSettings settingsNew, PeptideDocNode nodePep, SrmSettingsDiff diff) 
            : this(settingsNew, nodePep, nodePep.ExplicitMods, diff)
        {
        }

        public PeptideSettingsDiff(SrmSettings settingsNew, PeptideDocNode nodePep, ExplicitMods mods,
            SrmSettingsDiff diff)
        {
            SettingsNew = settingsNew;
            NodePep = nodePep;
            ExplicitMods = mods;
            SrmSettingsDiff = diff;
        }

        public PeptideSettingsDiff ChangeSettingsDiff(SrmSettingsDiff diffNew)
        {
            var peptideSettingsDiff = (PeptideSettingsDiff) MemberwiseClone();
            peptideSettingsDiff.SrmSettingsDiff = diffNew;
            return peptideSettingsDiff;
        }

        public SrmSettings SettingsNew { get; }

        public PeptideDocNode NodePep { get; }
        public ExplicitMods ExplicitMods { get; }
        public SrmSettingsDiff SrmSettingsDiff { get; private set; }

        public bool MustReadAllChromatograms()
        {
            if (null != SettingsNew.PeptideSettings.Integration.ResultsHandler)
            {
                return true;
            }

            var settingsOld = SrmSettingsDiff.SettingsOld;
            if (settingsOld == null)
            {
                return true;
            }

            if (SettingsNew.TransitionSettings.Instrument.MzMatchTolerance !=
                settingsOld.TransitionSettings.Instrument.MzMatchTolerance)
            {
                return true;
            }

            return false;
        }

        private void ReadPeaksForAllReplicates(TransitionGroupDocNode transitionGroupDocNode)
        {
            var allChromatogramGroupInfos =
                SettingsNew.MeasuredResults.LoadChromatogramsForAllReplicates(NodePep, transitionGroupDocNode,
                    MzMatchTolerance);
            ChromatogramGroupInfo.LoadPeaksForAll(allChromatogramGroupInfos.SelectMany(list => list), false);
            for (int replicateIndex = 0; replicateIndex < allChromatogramGroupInfos.Count; replicateIndex++)
            {
                var key = new ChromatogramListKey(transitionGroupDocNode.TransitionGroup, replicateIndex);
                _chromatogramLists[key] = ImmutableList.ValueOf(allChromatogramGroupInfos[replicateIndex]);
            }
        }

        private float MzMatchTolerance
        {
            get { return (float) SettingsNew.TransitionSettings.Instrument.MzMatchTolerance; }
        }

        public ImmutableList<ChromatogramGroupInfo> LoadChromatograms(int replicateIndex, TransitionGroupDocNode transitionGroupDocNode)
        {
            if (!SettingsNew.HasResults || replicateIndex < 0 ||
                replicateIndex >= SettingsNew.MeasuredResults.Chromatograms.Count)
            {
                return ImmutableList<ChromatogramGroupInfo>.EMPTY;
            }
            var key = new ChromatogramListKey(transitionGroupDocNode.TransitionGroup, replicateIndex);
            if (_chromatogramLists.TryGetValue(key, out var list))
            {
                return list;
            }

            if (MustReadAllChromatograms())
            {
                ReadPeaksForAllReplicates(transitionGroupDocNode);
                if (_chromatogramLists.TryGetValue(key, out list))
                {
                    return list;
                }
            }

            var measuredResults = SettingsNew.MeasuredResults;
            var chromatogramSet = measuredResults.Chromatograms[replicateIndex];
            measuredResults.TryLoadChromatogram(chromatogramSet, NodePep, transitionGroupDocNode, MzMatchTolerance,
                out var arrayChromGroupInfo);
            list = ImmutableList.ValueOfOrEmpty(arrayChromGroupInfo);
            _chromatogramLists[key] = list;
            return list;
        }

        public OnDemandFeatureCalculator GetOnDemandFeatureCalculator(int replicateIndex, ChromFileInfo chromFileInfo, PeptideDocNode peptideDocNode)
        {
            return new OnDemandFeatureCalculator(FeatureCalculators.ALL, SettingsNew, peptideDocNode, replicateIndex,
                chromFileInfo, LoadChromatograms);
        }

        private class ChromatogramListKey
        {
            public ChromatogramListKey(TransitionGroup transitionGroup, int replicateIndex)
            {
                TransitionGroup = transitionGroup;
                ReplicateIndex = replicateIndex;
            }

            public TransitionGroup TransitionGroup { get; }
            public int ReplicateIndex { get; }

            protected bool Equals(ChromatogramListKey other)
            {
                return ReferenceEquals(TransitionGroup, other.TransitionGroup) &&
                       ReplicateIndex == other.ReplicateIndex;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((ChromatogramListKey) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (RuntimeHelpers.GetHashCode(TransitionGroup) * 397) ^ ReplicateIndex;
                }
            }
        }

        public PeptideDocNode RecalculateScores(PeptideDocNode peptideDocNode)
        {
            var model = SettingsNew.PeptideSettings.Integration.PeakScoringModel;
            if (model == null || !model.IsTrained)
            {
                return peptideDocNode;
            }
            if (peptideDocNode.Results == null)
            {
                return peptideDocNode;
            }

            for (int replicateIndex = 0; replicateIndex < peptideDocNode.Results.Count; replicateIndex++)
            {
                peptideDocNode = RecalculateScores(replicateIndex, peptideDocNode);
            }

            return peptideDocNode;
        }

        private PeptideDocNode RecalculateScores(int replicateIndex, PeptideDocNode peptideDocNode)
        {
            var original = peptideDocNode;
            peptideDocNode = TryCopyOldScores(replicateIndex, peptideDocNode);
            if (!peptideDocNode.TransitionGroups.Any(tg => NeedsScores(replicateIndex, tg)))
            {
                if (!ReferenceEquals(original, peptideDocNode))
                {
                    Console.Out.WriteLine("changed");
                }
                return peptideDocNode;
            }

            var fileIds = new HashSet<ChromFileInfoId>(_fileIdComparer);
            for (int iTransitionGroup = 0; iTransitionGroup < peptideDocNode.Children.Count; iTransitionGroup++)
            {
                var transitionGroupDocNode = (TransitionGroupDocNode) peptideDocNode.Children[iTransitionGroup];
                var chromInfos = transitionGroupDocNode.GetSafeChromInfo(replicateIndex);
                fileIds.UnionWith(chromInfos
                    .Where(chromInfo => chromInfo.OptimizationStep == 0 && !chromInfo.ZScore.HasValue)
                    .Select(chromInfo => chromInfo.FileId));
            }

            if (fileIds.Count == 0)
            {
                return peptideDocNode;
            }
            var chromatogramSet = SettingsNew.MeasuredResults.Chromatograms[replicateIndex];
            foreach (var chromFileInfo in chromatogramSet.MSDataFileInfos)
            {
                if (!fileIds.Contains(chromFileInfo.FileId))
                {
                    continue;
                }

                peptideDocNode = RecalculateScoresForFile(replicateIndex, peptideDocNode, chromFileInfo);
            }

            return peptideDocNode;
        }

        private PeptideDocNode RecalculateScoresForFile(int replicateIndex, PeptideDocNode peptideDocNode,
            ChromFileInfo chromFileInfo)
        {
            var scoringModel = SettingsNew.PeptideSettings.Integration.PeakScoringModel;
            var onDemandScoreCalculator =
                GetOnDemandFeatureCalculator(replicateIndex, chromFileInfo, peptideDocNode);
            var peakGroupDatas = onDemandScoreCalculator.GetChosenPeakGroupDataForAllComparableGroups();
            var newTransitionGroups = new List<DocNode>();
            foreach (var transitionGroup in peptideDocNode.TransitionGroups)
            {
                CandidatePeakGroupData peakGroupData;
                if (peakGroupDatas.Count == 1)
                {
                    peakGroupData = peakGroupDatas[0].Item2;
                }
                else
                {
                    peakGroupData = peakGroupDatas
                        .FirstOrDefault(tuple => tuple.Item1.Any(tg => ReferenceEquals(tg, transitionGroup)))?.Item2;
                }

                if (peakGroupData == null)
                {
                    newTransitionGroups.Add(transitionGroup);
                    continue;
                }

                var newChromInfos = new List<TransitionGroupChromInfo>();
                foreach (var chromInfo in transitionGroup.GetSafeChromInfo(replicateIndex))
                {
                    if (chromInfo.OptimizationStep != 0 || !ReferenceEquals(chromInfo.FileId, chromFileInfo.FileId))
                    {
                        newChromInfos.Add(chromInfo);
                        continue;
                    }

                    var newScore = peakGroupData.Score.ModelScore;
                    newScore = newScore + scoringModel?.Parameters?.Bias;
                    newChromInfos.Add(chromInfo.ChangeScore(null, (float?)newScore));
                }

                if (newChromInfos.SequenceEqual(transitionGroup.GetSafeChromInfo(replicateIndex)))
                {
                    newTransitionGroups.Add(transitionGroup);
                    continue;
                }
                newTransitionGroups.Add(transitionGroup.ChangeResults(transitionGroup.Results.ChangeAt(replicateIndex, new ChromInfoList<TransitionGroupChromInfo>(newChromInfos))));
            }

            return (PeptideDocNode) peptideDocNode.ChangeChildrenChecked(newTransitionGroups);
        }

        private bool NeedsScores(int replicateIndex, TransitionGroupDocNode transitionGroupDocNode)
        {
            return transitionGroupDocNode.GetSafeChromInfo(replicateIndex)
                .Any(chromInfo => 0 == chromInfo.OptimizationStep && !chromInfo.ZScore.HasValue);
        }

        private PeptideDocNode ClearScores(int replicateIndex, PeptideDocNode peptideDocNode)
        {
            var newTransitionGroups = new List<DocNode>();
            foreach (var transitionGroup in peptideDocNode.TransitionGroups)
            {
                var chromInfoList = transitionGroup.GetSafeChromInfo(replicateIndex);
                if (!chromInfoList.Any(chromInfo => chromInfo.ZScore.HasValue || chromInfo.QValue.HasValue))
                {
                    newTransitionGroups.Add(transitionGroup);
                    continue;
                }

                chromInfoList =
                    new ChromInfoList<TransitionGroupChromInfo>(chromInfoList.Select(chromInfo =>
                        chromInfo.ChangeScore(null, null)));
                newTransitionGroups.Add(
                    transitionGroup.ChangeResults(transitionGroup.Results.ChangeAt(replicateIndex, chromInfoList)));
            }

            return (PeptideDocNode) peptideDocNode.ChangeChildrenChecked(newTransitionGroups);
        }

        private PeptideDocNode TryCopyOldScores(int replicateIndex, PeptideDocNode peptideDocNode)
        {
            if (NodePep.Results == null || replicateIndex >= NodePep.Results.Count)
            {
                return peptideDocNode;
            }

            peptideDocNode = ClearScores(replicateIndex, peptideDocNode);
            var measuredResults = SettingsNew.MeasuredResults;
            if (measuredResults.HasNewChromatogramData(replicateIndex))
            {
                return peptideDocNode;
            }

            if (NodePep.Children.Count != peptideDocNode.Children.Count)
            {
                return peptideDocNode;
            }

            var newTransitionGroups = new List<DocNode>();
            for (int iTransitionGroup = 0; iTransitionGroup < peptideDocNode.Children.Count; iTransitionGroup++)
            {
                var tg1 = (TransitionGroupDocNode) peptideDocNode.Children[iTransitionGroup];
                var chromInfoList1 = tg1.GetSafeChromInfo(replicateIndex);
                if (!NeedsScores(replicateIndex, tg1))
                {
                    newTransitionGroups.Add(tg1);
                    continue;
                }
                var tg2 = (TransitionGroupDocNode)NodePep.Children[iTransitionGroup];
                var chromInfoList2 = tg2.GetSafeChromInfo(replicateIndex);
                if (chromInfoList2.Count != chromInfoList1.Count)
                {
                    return peptideDocNode;
                }
                if (chromInfoList2.All(chromInfo => !chromInfo.ZScore.HasValue))
                {
                    newTransitionGroups.Add(tg1);
                    continue;
                }
                if (!ReferenceEquals(tg1.TransitionGroup, tg2.TransitionGroup))
                {
                    return peptideDocNode;
                }

                if (!PeakBoundariesMatch(replicateIndex, tg1, tg2))
                {
                    return peptideDocNode;
                }

                var newChromInfoList = new List<TransitionGroupChromInfo>();
                for (int i = 0; i < chromInfoList1.Count; i++)
                {
                    var chromInfo1 = chromInfoList1[i];
                    var chromInfo2 = chromInfoList2[i];
                    if (!ReferenceEquals(chromInfo1.FileId, chromInfo2.FileId) ||
                        chromInfo1.OptimizationStep != chromInfo2.OptimizationStep)
                    {
                        return peptideDocNode;
                    }
                    newChromInfoList.Add(chromInfo1.ChangeScore(chromInfo2.QValue, chromInfo2.ZScore));
                }

                if (tg1.GetSafeChromInfo(replicateIndex).SequenceEqual(newChromInfoList))
                {
                    newTransitionGroups.Add(tg1);
                    continue;
                }
                newTransitionGroups.Add(tg1.ChangeResults(tg1.Results.ChangeAt(replicateIndex,
                    new ChromInfoList<TransitionGroupChromInfo>(newChromInfoList))));
            }

            return (PeptideDocNode) peptideDocNode.ChangeChildrenChecked(newTransitionGroups);
        }

        private bool PeakBoundariesMatch(int replicateIndex, TransitionGroupDocNode tg1, TransitionGroupDocNode tg2)
        {
            if (tg1.Children.Count != tg2.Children.Count)
            {
                return false;
            }

            // if (!BoundariesMatch(tg1.GetSafeChromInfo(replicateIndex), tg2.GetSafeChromInfo(replicateIndex),
            //         (c1, c2) => c1.StartRetentionTime == c2.StartRetentionTime &&
            //                     c1.EndRetentionTime == c2.EndRetentionTime))
            // {
            //     return false;
            // };

            for (int iTransition = 0; iTransition < tg1.Children.Count; iTransition++)
            {
                var transition1 = (TransitionDocNode) tg1.Children[iTransition];
                var transition2 = (TransitionDocNode) tg2.Children[iTransition];
                if (!ReferenceEquals(transition1.Transition, transition2.Transition))
                {
                    return false;
                }

                if (!BoundariesMatch(transition1.GetSafeChromInfo(replicateIndex),
                        transition2.GetSafeChromInfo(replicateIndex),
                        (c1, c2) => c1.StartRetentionTime == c2.StartRetentionTime &&
                                    c1.EndRetentionTime == c2.EndRetentionTime))
                {
                    return false;
                }
            }
            return true;
        }

        private bool BoundariesMatch<TChromInfo>(ChromInfoList<TChromInfo> list1, ChromInfoList<TChromInfo> list2, Func<TChromInfo, TChromInfo, bool> compareFunc)
            where TChromInfo : ChromInfo
        {
            if (list1.Count != list2.Count)
            {
                return false;
            }

            for (int i = 0; i < list1.Count; i++)
            {
                var chromInfo1 = list1[i];
                var chromInfo2 = list2[i];
                if (!ReferenceEquals(chromInfo1.FileId, chromInfo2.FileId))
                {
                    return false;
                }

                if (!compareFunc(chromInfo1, chromInfo2))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
