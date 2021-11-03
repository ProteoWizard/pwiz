using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.PeakFinding;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results.Scoring;

namespace pwiz.Skyline.Model.Results
{
    public class OnDemandFeatureCalculator
    {
        public OnDemandFeatureCalculator(FeatureCalculators calculators, SrmDocument document, PeptideDocNode peptideDocNode, int replicateIndex, ChromFileInfo chromFileInfo)
        {
            Calculators = calculators;
            Document = document;
            PeptideDocNode = peptideDocNode;
            ChromFileInfo = chromFileInfo;
            ReplicateIndex = replicateIndex;
        }

        public FeatureCalculators Calculators { get; }
        public SrmDocument Document { get; }
        public PeptideDocNode PeptideDocNode { get; }
        public int ReplicateIndex { get; }
        public ChromFileInfo ChromFileInfo { get; }
        public ChromatogramSet ChromatogramSet
        {
            get { return Document.Settings.MeasuredResults.Chromatograms[ReplicateIndex]; }
        }

        public float MzMatchTolerance
        {
            get
            {
                return (float) Document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            }
        }

        public FeatureValues CalculateTransitionGroupFeatureValues(TransitionGroup transitionGroup)
        {
            foreach (var groupScores in CalculateAllComparableGroupScores())
            {
                if (groupScores.Item1.Any(tg =>
                    ReferenceEquals(tg.TransitionGroup, transitionGroup)))
                {
                    return groupScores.Item2;
                }
            }

            return null;
        }

        public List<Tuple<ImmutableList<TransitionGroupDocNode>, FeatureValues>> CalculateAllComparableGroupScores()
        {
            var list = new List<Tuple<ImmutableList<TransitionGroupDocNode>, FeatureValues>>();
            var peptideChromDataSets = MakePeptideChromDataSets();
            peptideChromDataSets.PickChromatogramPeaks(GetTransitionPeakBounds);
            foreach (var comparableSet in peptideChromDataSets.ComparableDataSets.Select(ImmutableList.ValueOf))
            {
                var groupNodes = ImmutableList.ValueOf(comparableSet.Select(dataSet => dataSet.NodeGroup));
                if (groupNodes.Contains(null))
                {
                    continue;
                }

                var scores = CalculateComparableGroupScores(peptideChromDataSets, comparableSet).FirstOrDefault();
                if (scores == null)
                {
                    continue;
                }

                list.Add(Tuple.Create(groupNodes, scores));
            }

            return list;
        }

        internal IEnumerable<FeatureValues> CalculateComparableGroupScores(PeptideChromDataSets peptideChromDataSets,
            IList<ChromDataSet> comparableSet)
        {
            var transitionGroups = comparableSet.Select(dataSet => dataSet.NodeGroup).ToList();
            var chromatogramGroupInfos = peptideChromDataSets.MakeChromatogramGroupInfos(comparableSet).ToList();
            return CalculateChromatogramGroupScores(transitionGroups, chromatogramGroupInfos);
        }

        public IEnumerable<FeatureValues> CalculateCandidatePeakScores(TransitionGroupDocNode transitionGroup, ChromatogramGroupInfo chromatogramGroupInfo)
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

                var otherChromatogramGroupInfo = Document.Settings.LoadChromatogramGroup(
                    ChromatogramSet, ChromFileInfo.FilePath, PeptideDocNode, otherTransitionGroup);
                if (otherChromatogramGroupInfo != null)
                {
                    transitionGroups.Add(otherTransitionGroup);
                    chromatogramGroupInfos.Add(otherChromatogramGroupInfo);
                }
            }

            return CalculateChromatogramGroupScores(transitionGroups, chromatogramGroupInfos);
        }

        internal IEnumerable<FeatureValues> CalculateChromatogramGroupScores(
            IList<TransitionGroupDocNode> transitionGroups, IList<ChromatogramGroupInfo> chromatogramGroupInfos)
        {
            var context = new PeakScoringContext(Document);
            var summaryData = new PeakFeatureEnumerator.SummaryPeptidePeakData(
                Document, PeptideDocNode, transitionGroups, Document.MeasuredResults.Chromatograms[ReplicateIndex],
                ChromFileInfo, chromatogramGroupInfos);
            while (summaryData.NextPeakIndex())
            {
                var scores = new List<float>();
                foreach (var calculator in Calculators)
                {
                    if (calculator is SummaryPeakFeatureCalculator)
                    {
                        scores.Add(calculator.Calculate(context, summaryData));
                    }
                    else if (calculator is DetailedPeakFeatureCalculator)
                    {
                        scores.Add(chromatogramGroupInfos[0].GetScore(calculator.GetType(), summaryData.PeakIndex));
                    }
                }

                yield return new FeatureValues(Calculators, ImmutableList.ValueOf(scores));
            }
        }

        public virtual PeakBounds GetTransitionPeakBounds(TransitionGroup transitionGroup, Transition transition)
        {
            var chromInfos = ((TransitionDocNode) ((TransitionGroupDocNode) PeptideDocNode
                .FindNode(transitionGroup))?.FindNode(transition))?.GetSafeChromInfo(ReplicateIndex);
            if (chromInfos == null)
            {
                return null;
            }

            foreach (var transitionChromInfo in chromInfos)
            {
                if (transitionChromInfo?.OptimizationStep == 0 &&
                    ReferenceEquals(transitionChromInfo.FileId, ChromFileInfo.FileId))
                {
                    if (transitionChromInfo.IsEmpty)
                    {
                        return null;
                    }

                    return new PeakBounds(transitionChromInfo.StartRetentionTime, transitionChromInfo.EndRetentionTime);
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
            var peptideChromDataSets = new PeptideChromDataSets(PeptideDocNode, Document, ChromFileInfo,
                Calculators.Detailed, false);
            foreach (var transitionGroup in PeptideDocNode.TransitionGroups)
            {
                var chromDatas = new List<ChromData>();
                var chromatogramGroupInfo = LoadChromatogramGroupInfo(transitionGroup);
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
                    chromatogramInfo.Transform(TransformChrom.interpolated);
                    var interpolatedTimeIntensities = chromatogramInfo.TimeIntensities;
                    chromDatas.Add(new ChromData(transition, rawTimeIntensities, interpolatedTimeIntensities));
                }

                if (!chromDatas.Any())
                {
                    continue;
                }

                var chromDataSet = new ChromDataSet(true, PeptideDocNode, transitionGroup,
                    Document.Settings.TransitionSettings.FullScan.AcquisitionMethod, chromDatas.ToArray());
                peptideChromDataSets.Add(PeptideDocNode, chromDataSet);
            }

            return peptideChromDataSets;
        }

        public ChromatogramGroupInfo LoadChromatogramGroupInfo(TransitionGroupDocNode transitionGroup)
        {
            var measuredResults = Document.Settings.MeasuredResults;
            if (!measuredResults.TryLoadChromatogram(measuredResults.Chromatograms[ReplicateIndex], PeptideDocNode, transitionGroup,
                MzMatchTolerance, out var infos))
            {
                return null;
            }

            foreach (var chromatogramInfo in infos)
            {
                if (Equals(chromatogramInfo.FilePath, ChromFileInfo.FilePath))
                {
                    return chromatogramInfo;
                }
            }

            return null;
        }
    }
}
