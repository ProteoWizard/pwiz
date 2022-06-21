/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2022 University of Washington - Seattle, WA
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
using pwiz.Common.PeakFinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    public class OnDemandFeatureCalculator
    {
        private Dictionary<TransitionGroup, ChromatogramGroupInfo> _chromatogramGroupInfos =
            new Dictionary<TransitionGroup, ChromatogramGroupInfo>(new IdentityEqualityComparer<TransitionGroup>());

        private ScoreQValueMap _scoreQValueMap;

        public OnDemandFeatureCalculator(FeatureCalculators calculators, SrmSettings settings,
            PeptideDocNode peptideDocNode, int replicateIndex, ChromFileInfo chromFileInfo)
        {
            Calculators = calculators;
            Settings = settings;
            PeptideDocNode = peptideDocNode;
            ChromFileInfo = chromFileInfo;
            ReplicateIndex = replicateIndex;
            _scoreQValueMap = settings.PeptideSettings.Integration.ScoreQValueMap;
        }

        public FeatureCalculators Calculators { get; }
        public SrmSettings Settings { get; }
        public PeptideDocNode PeptideDocNode { get; }
        public int ReplicateIndex { get; }
        public ChromFileInfo ChromFileInfo { get; }
        public ChromatogramSet ChromatogramSet
        {
            get { return Settings.MeasuredResults.Chromatograms[ReplicateIndex]; }
        }

        public float MzMatchTolerance
        {
            get
            {
                return (float) Settings.TransitionSettings.Instrument.MzMatchTolerance;
            }
        }

        public CandidatePeakGroupData GetChosenPeakGroupData(TransitionGroup transitionGroup)
        {
            foreach (var groupScores in GetChosenPeakGroupDataForAllComparableGroups())
            {
                if (groupScores.Item1.Any(tg => ReferenceEquals(tg.TransitionGroup, transitionGroup)))
                {
                    return groupScores.Item2;
                }
            }

            return null;
        }

        public List<Tuple<ImmutableList<TransitionGroupDocNode>, CandidatePeakGroupData>>
            GetChosenPeakGroupDataForAllComparableGroups()
        {
            var list = new List<Tuple<ImmutableList<TransitionGroupDocNode>, CandidatePeakGroupData>>();
            var peptideChromDataSets = MakePeptideChromDataSets();
            peptideChromDataSets.PickChromatogramPeaks(GetTransitionPeakBounds);
            foreach (var comparableSet in peptideChromDataSets.ComparableDataSets.Select(ImmutableList.ValueOf))
            {
                var groupNodes = ImmutableList.ValueOf(comparableSet.Select(dataSet => dataSet.NodeGroup));
                if (groupNodes.Contains(null))
                {
                    continue;
                }

                var scores = CalculateScoresForComparableGroup(peptideChromDataSets, comparableSet).FirstOrDefault();
                if (scores == null)
                {
                    continue;
                }

                double minStartTime = double.MaxValue;
                double maxEndTime = double.MinValue;
                double apexTime = double.MinValue;
                double apexHeight = 0;
                foreach (var groupNode in groupNodes)
                {
                    var transitionGroupChromInfo = groupNode.GetSafeChromInfo(ReplicateIndex)
                        .FirstOrDefault(chromInfo =>
                            0 == chromInfo.OptimizationStep && ReferenceEquals(chromInfo.FileId, ChromFileInfo.FileId));
                    if (transitionGroupChromInfo?.StartRetentionTime != null)
                    {
                        minStartTime = Math.Min(minStartTime, transitionGroupChromInfo.StartRetentionTime.Value);
                    }

                    if (transitionGroupChromInfo?.EndRetentionTime != null)
                    {
                        maxEndTime = Math.Max(maxEndTime, transitionGroupChromInfo.EndRetentionTime.Value);
                    }

                    if (transitionGroupChromInfo?.RetentionTime != null && (transitionGroupChromInfo.Height ?? 0) > apexHeight)
                    {
                        apexHeight = transitionGroupChromInfo.Height ?? 0;
                        apexTime = transitionGroupChromInfo.RetentionTime ?? double.MinValue;
                    }
                }

                var candidatePeakData = new CandidatePeakGroupData(null, apexTime, minStartTime, maxEndTime, true, MakePeakScore(scores), false);
                list.Add(Tuple.Create(groupNodes, candidatePeakData));
            }

            return list;
        }

        internal IEnumerable<FeatureScores> CalculateScoresForComparableGroup(PeptideChromDataSets peptideChromDataSets,
            IList<ChromDataSet> comparableSet)
        {
            var transitionGroups = comparableSet.Select(dataSet => dataSet.NodeGroup).ToList();
            var chromatogramGroupInfos = peptideChromDataSets.MakeChromatogramGroupInfos(comparableSet).ToList();
            if (chromatogramGroupInfos.Count == 0)
            {
                return Array.Empty<FeatureScores>();
            }
            return CalculateChromatogramGroupScores(transitionGroups, chromatogramGroupInfos);
        }

        public IEnumerable<CandidatePeakGroupData> GetCandidatePeakGroups(TransitionGroup transitionGroup)
        {
            var transitionGroupDocNode = (TransitionGroupDocNode) PeptideDocNode.FindNode(transitionGroup);
            var chromatogramGroupInfo = GetChromatogramGroupInfo(transitionGroup);
            if (transitionGroupDocNode == null || chromatogramGroupInfo == null)
            {
                return Array.Empty<CandidatePeakGroupData>();
            }

            return CalculateCandidatePeakScores(transitionGroupDocNode, chromatogramGroupInfo);
        }

        public IList<CandidatePeakGroupData> CalculateCandidatePeakScores(TransitionGroupDocNode transitionGroup, ChromatogramGroupInfo chromatogramGroupInfo)
        {
            var transitionGroups = new List<TransitionGroupDocNode> {transitionGroup};
            var chromatogramGroupInfos = new List<ChromatogramGroupInfo> {chromatogramGroupInfo};
            foreach (var otherTransitionGroup in PeptideDocNode.TransitionGroups)
            {
                if (ReferenceEquals(otherTransitionGroup.TransitionGroup, transitionGroup.TransitionGroup))
                {
                    continue;
                }

                if (transitionGroup.RelativeRT == RelativeRT.Unknown)
                {
                    if (!Equals(otherTransitionGroup.LabelType, transitionGroup.LabelType))
                    {
                        continue;
                    }
                }
                else
                {
                    if (otherTransitionGroup.RelativeRT == RelativeRT.Unknown)
                    {
                        continue;
                    }
                }

                var otherChromatogramGroupInfo = Settings.LoadChromatogramGroup(
                    ChromatogramSet, ChromFileInfo.FilePath, PeptideDocNode, otherTransitionGroup);
                if (otherChromatogramGroupInfo != null)
                {
                    transitionGroups.Add(otherTransitionGroup);
                    chromatogramGroupInfos.Add(otherChromatogramGroupInfo);
                }
            }

            return MakePeakGroupData(transitionGroups, chromatogramGroupInfos, CalculateChromatogramGroupScores(transitionGroups, chromatogramGroupInfos).ToList());
        }

        public IList<CandidatePeakGroupData> MakePeakGroupData(IList<TransitionGroupDocNode> transitionGroups,
            IList<ChromatogramGroupInfo> chromatogramGroupInfos, IList<FeatureScores> peakGroupFeatureValues)
        {
            var chromatogramInfos = new List<ChromatogramInfo>();
            
            var transitionChromInfos = new List<TransitionChromInfo>();
            for (int iTransitionGroup = 0; iTransitionGroup < transitionGroups.Count; iTransitionGroup++)
            {
                var transitionGroupDocNode = transitionGroups[iTransitionGroup];
                var chromatogramGroupInfo = chromatogramGroupInfos[iTransitionGroup];
                foreach (var transition in transitionGroupDocNode.Transitions)
                {
                    var chromatogramInfo = chromatogramGroupInfo.GetTransitionInfo(transition, MzMatchTolerance,
                        TransformChrom.raw, ChromatogramSet.OptimizationFunction);
                    if (chromatogramInfo == null)
                    {
                        continue;
                    }

                    chromatogramInfos.Add(chromatogramInfo);
                    transitionChromInfos.Add(FindTransitionChromInfo(transition));
                }
            }

            var peakGroupDatas = new List<CandidatePeakGroupData>();
            for (int peakIndex = 0; peakIndex < peakGroupFeatureValues.Count; peakIndex++)
            {
                peakGroupDatas.Add(MakeCandidatePeakGroupData(peakIndex, chromatogramInfos, transitionChromInfos, peakGroupFeatureValues[peakIndex]));
            }

            return peakGroupDatas;
        }

        private CandidatePeakGroupData MakeCandidatePeakGroupData(int peakIndex,
            IList<ChromatogramInfo> chromatogramInfos, IList<TransitionChromInfo> transitionChromInfos,
            FeatureScores featureScores)
        {
            Assume.AreEqual(chromatogramInfos.Count, transitionChromInfos.Count);
            bool isChosen = true;
            double minStartTime = double.MaxValue;
            double maxEndTime = double.MinValue;
            double apexTime = double.MinValue;
            double apexHeight = 0;
            for (int iTransition = 0; iTransition < transitionChromInfos.Count; iTransition++)
            {
                var transitionChromInfo = transitionChromInfos[iTransition];
                var chromatogramInfo = chromatogramInfos[iTransition];
                var chromPeak = chromatogramInfo.GetPeak(peakIndex);
                if (chromPeak.IsEmpty)
                {
                    if (transitionChromInfo != null && !transitionChromInfo.IsEmpty)
                    {
                        isChosen = false;
                    }
                }
                else
                {
                    if (transitionChromInfo == null || transitionChromInfo.IsEmpty || transitionChromInfo.StartRetentionTime != chromPeak.StartTime ||
                        transitionChromInfo.EndRetentionTime != chromPeak.EndTime)
                    {
                        isChosen = false;
                    }

                    minStartTime = Math.Min(minStartTime, chromPeak.StartTime);
                    maxEndTime = Math.Max(maxEndTime, chromPeak.EndTime);
                    if (chromPeak.Height > apexHeight)
                    {
                        apexHeight = chromPeak.Height;
                        apexTime = chromPeak.RetentionTime;
                    }
                }
            }

            bool originallyBestPeak = chromatogramInfos.All(info => info.Header.MaxPeakIndex == peakIndex);
            return CandidatePeakGroupData.FoundPeak(peakIndex, apexTime, minStartTime, maxEndTime, isChosen,
                MakePeakScore(featureScores), originallyBestPeak);
        }

        private PeakGroupScore MakePeakScore(FeatureScores featureScores)
        {
            var model = Settings.PeptideSettings.Integration.PeakScoringModel;
            if (model == null || !model.IsTrained)
            {
                model = LegacyScoringModel.DEFAULT_MODEL;
            }
            return PeakGroupScore.MakePeakScores(featureScores, model, _scoreQValueMap);
        }

        internal IEnumerable<FeatureScores> CalculateChromatogramGroupScores(
            IList<TransitionGroupDocNode> transitionGroups, IList<ChromatogramGroupInfo> chromatogramGroupInfos)
        {
            var context = new PeakScoringContext(Settings);
            if (chromatogramGroupInfos.IsNullOrEmpty())
                yield break;
            var summaryData = new PeakFeatureEnumerator.SummaryPeptidePeakData(
                Settings, PeptideDocNode, transitionGroups, Settings.MeasuredResults.Chromatograms[ReplicateIndex],
                ChromFileInfo, chromatogramGroupInfos);
            while (summaryData.NextPeakIndex())
            {
                var scores = new List<float>();
                foreach (var calculator in Calculators)
                {
                    if (calculator is SummaryPeakFeatureCalculator)
                    {
                        // Retention time difference is not currently used in picking peaks for iRT standards
                        if (PeptideDocNode.GlobalStandardType == StandardType.IRT &&
                            calculator is MQuestRetentionTimePredictionCalc)
                            scores.Add(float.NaN);
                        else
                            scores.Add(calculator.Calculate(context, summaryData));
                    }
                    else if (calculator is DetailedPeakFeatureCalculator)
                    {
                        scores.Add(chromatogramGroupInfos[0].GetScore(calculator.GetType(), summaryData.UsedBestPeakIndex ? summaryData.BestPeakIndex : summaryData.PeakIndex));
                    }
                }

                yield return new FeatureScores(Calculators.FeatureNames, ImmutableList.ValueOf(scores));
            }
        }

        public virtual PeakBounds GetTransitionPeakBounds(TransitionGroup transitionGroup, Transition transition)
        {
            var transitionChromInfo = FindTransitionChromInfo((TransitionDocNode) ((TransitionGroupDocNode) PeptideDocNode
                .FindNode(transitionGroup))?.FindNode(transition));
            if (transitionChromInfo == null || transitionChromInfo.IsEmpty)
            {
                return null;
            }
            return new PeakBounds(transitionChromInfo.StartRetentionTime, transitionChromInfo.EndRetentionTime);
        }

        private TransitionChromInfo FindTransitionChromInfo(TransitionDocNode transitionDocNode)
        {
            foreach (var transitionChromInfo in transitionDocNode.GetSafeChromInfo(ReplicateIndex))
            {
                if (transitionChromInfo.OptimizationStep == 0 &&
                    ReferenceEquals(transitionChromInfo.FileId, ChromFileInfo.FileId))
                {
                    return transitionChromInfo;
                }
            }

            return null;
        }
        public IEnumerable<float> ScorePeak(double startTime, double endTime, IEnumerable<DetailedPeakFeatureCalculator> calculators)
        {
            var peptideChromDataSets = MakePeptideChromDataSets();
            var explicitPeakBounds = new PeakBounds(startTime, endTime);
            peptideChromDataSets.PickChromatogramPeaks(explicitPeakBounds);
            return peptideChromDataSets.DataSets[0].PeakSets.First().DetailScores;
        }

        internal PeptideChromDataSets MakePeptideChromDataSets()
        {
            var peptideChromDataSets = new PeptideChromDataSets(PeptideDocNode, Settings, ChromFileInfo,
                Calculators.Detailed, false);
            foreach (var transitionGroup in PeptideDocNode.TransitionGroups)
            {
                var chromDatas = new List<ChromData>();
                var chromatogramGroupInfo = GetChromatogramGroupInfo(transitionGroup.TransitionGroup);
                if (chromatogramGroupInfo == null)
                {
                    continue;
                }
                foreach (var transition in transitionGroup.Transitions)
                {
                    var chromatogramInfo =
                        chromatogramGroupInfo.GetTransitionInfo(transition, MzMatchTolerance, TransformChrom.raw, null);
                    if (chromatogramInfo == null)
                    {
                        continue;
                    }
                    var rawTimeIntensities = chromatogramInfo.TimeIntensities;
                    var chromKey = new ChromKey(PeptideDocNode.ModifiedTarget, transitionGroup.PrecursorMz, null,
                        transition.Mz, 0, 0, transition.IsMs1 ? ChromSource.ms1 : ChromSource.fragment,
                        ChromExtractor.summed, true, false);
                    chromDatas.Add(new ChromData(chromKey, transition, rawTimeIntensities, rawTimeIntensities));
                }

                if (!chromDatas.Any())
                {
                    continue;
                }

                var chromDataSet = new ChromDataSet(true, PeptideDocNode, transitionGroup,
                    Settings.TransitionSettings.FullScan.AcquisitionMethod, chromDatas.ToArray());
                peptideChromDataSets.Add(PeptideDocNode, chromDataSet);
            }

            return peptideChromDataSets;
        }

        public ChromatogramGroupInfo GetChromatogramGroupInfo(TransitionGroup transitionGroup)
        {
            if (_chromatogramGroupInfos.TryGetValue(transitionGroup, out var chromatogramGroupInfo))
            {
                return chromatogramGroupInfo;
            }

            var transitionGroupDocNode = (TransitionGroupDocNode) PeptideDocNode.FindNode(transitionGroup);
            return LoadChromatogramGroupInfo(transitionGroupDocNode);
        }

        private IList<ChromatogramGroupInfo> LoadChromatogramGroupInfos(TransitionGroupDocNode transitionGroup)
        {
            var measuredResults = Settings.MeasuredResults;
            ChromatogramGroupInfo[] infoSet;
            if (!measuredResults.TryLoadChromatogram(measuredResults.Chromatograms[ReplicateIndex], PeptideDocNode,
                    transitionGroup,
                    MzMatchTolerance, out infoSet))
            {
                return ImmutableList.Empty<ChromatogramGroupInfo>();
            }

            return infoSet;
        }

        private ChromatogramGroupInfo LoadChromatogramGroupInfo(TransitionGroupDocNode transitionGroup)
        {
            var infos = LoadChromatogramGroupInfos(transitionGroup);
            foreach (var chromatogramInfo in infos)
            {
                if (Equals(chromatogramInfo.FilePath, ChromFileInfo.FilePath))
                {
                    _chromatogramGroupInfos.Add(transitionGroup.TransitionGroup, chromatogramInfo);
                    return chromatogramInfo;
                }
            }

            return null;
        }

        public static OnDemandFeatureCalculator GetFeatureCalculator(SrmDocument document, IdentityPath peptideIdentityPath, int replicateIndex, ChromFileInfoId chromFileInfoId)
        {
            var peptideDocNode = document.FindNode(peptideIdentityPath) as PeptideDocNode;
            if (peptideDocNode == null)
            {
                return null;
            }

            if (!document.Settings.HasResults || replicateIndex < 0 ||
                replicateIndex >= document.Settings.MeasuredResults.Chromatograms.Count)
            {
                return null;
            }

            var chromatogramSet = document.Settings.MeasuredResults.Chromatograms[replicateIndex];
            ChromFileInfo chromFileInfo;
            if (chromFileInfoId != null)
            {
                chromFileInfo = chromatogramSet.GetFileInfo(chromFileInfoId);
            }
            else
            {
                chromFileInfo = chromatogramSet.MSDataFileInfos.FirstOrDefault();
            }

            if (chromFileInfo == null)
            {
                return null;
            }

            return new OnDemandFeatureCalculator(FeatureCalculators.ALL, document.Settings, peptideDocNode,
                replicateIndex, chromFileInfo);
        }
    }
}
