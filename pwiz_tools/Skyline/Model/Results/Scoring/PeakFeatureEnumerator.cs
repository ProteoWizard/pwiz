/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results.Scoring
{
    public static class PeakFeatureEnumerator
    {
        public static PeakTransitionGroupFeatureSet GetPeakFeatures(this SrmDocument document,
                                                                               FeatureCalculators calcs,
                                                                               IProgressMonitor progressMonitor = null,
                                                                               bool verbose = false)
        {
            // Get features for each peptide
            int totalPeptides = document.MoleculeCount;
            int currentPeptide = 0;
            IProgressStatus status = new ProgressStatus(Resources.PeakFeatureEnumerator_GetPeakFeatures_Calculating_peak_group_scores);

            // Set up run ID dictionary
            var runEnumDict = new Dictionary<int, int>();
            var chromatograms = document.Settings.MeasuredResults.Chromatograms;
            foreach (var fileInfo in chromatograms.SelectMany(c => c.MSDataFileInfos))
            {
                runEnumDict.Add(fileInfo.FileIndex, runEnumDict.Count + 1);
            }

            // Using Parallel.For is quicker, but order needs to be maintained
            var moleculeGroupPairs = document.GetMoleculeGroupPairs();
            var peakFeatureLists = new PeakTransitionGroupFeatures[moleculeGroupPairs.Length][];
            int peakFeatureCount = 0;
            ParallelEx.For(0, moleculeGroupPairs.Length, i =>
            {
                var pair = moleculeGroupPairs[i];
                var nodePepGroup = pair.NodeMoleculeGroup;
                var nodePep = pair.NodeMolecule;
                if (nodePep.TransitionGroupCount == 0)
                    return;

                // Exclude standard peptides
                if (nodePep.GlobalStandardType != null)
                    return;

                if (progressMonitor != null)
                {
                    if (progressMonitor.IsCanceled)
                        throw new OperationCanceledException();

                    int? percentComplete = ProgressStatus.ThreadsafeIncementPercent(ref currentPeptide, totalPeptides);
                    if (percentComplete.HasValue && percentComplete.Value < 100)
                        progressMonitor.UpdateProgress(status = status.ChangePercentComplete(percentComplete.Value));
                }

                var peakFeatureList = new List<PeakTransitionGroupFeatures>();
                foreach (var peakFeature in GetPeakFeatures(document.Settings, nodePepGroup, nodePep, calcs, runEnumDict, verbose))
                {
                    if (peakFeature.PeakGroupFeatures.Any())
                    {
                        peakFeatureList.Add(peakFeature);
                        Interlocked.Increment(ref peakFeatureCount);
                    }
                }
                peakFeatureLists[i] = peakFeatureList.ToArray();
            });

            var result = new PeakTransitionGroupFeatures[peakFeatureCount];
            int peakFeatureCurrent = 0;
            int decoyCount = 0;
            foreach (var peakFeatureList in peakFeatureLists)
            {
                if (peakFeatureList == null)
                    continue;

                foreach (var peakFeature in peakFeatureList)
                {
                    result[peakFeatureCurrent++] = peakFeature;
                    if (peakFeature.IsDecoy)
                        decoyCount++;
                }
            }

            if (progressMonitor != null)
                progressMonitor.UpdateProgress(status.ChangePercentComplete(100));
            return new PeakTransitionGroupFeatureSet(decoyCount, result);
        }

        private static IEnumerable<PeakTransitionGroupFeatures> GetPeakFeatures(SrmSettings settings,
                                                                                PeptideGroupDocNode nodePepGroup,
                                                                                PeptideDocNode nodePep,
                                                                                FeatureCalculators calcs,
                                                                                IDictionary<int, int> runEnumDict,
                                                                                bool verbose)
        {
            // Get peptide features for each set of comparable groups
            foreach (var nodeGroups in ComparableGroups(nodePep))
            {
                var arrayGroups = nodeGroups.ToArray();
                var labelType = arrayGroups[0].TransitionGroup.LabelType;
                foreach (var peakFeature in GetPeakFeatures(settings, nodePepGroup, nodePep, labelType, arrayGroups, calcs, runEnumDict, verbose))
                {
                    yield return peakFeature;
                }
            }
        }

        public static IEnumerable<IEnumerable<TransitionGroupDocNode>> ComparableGroups(PeptideDocNode nodePep)
        {
            return nodePep.GetComparableGroups();
        }

        private static IEnumerable<PeakTransitionGroupFeatures> GetPeakFeatures(SrmSettings settings,
                                                                   PeptideGroupDocNode nodePepGroup,
                                                                   PeptideDocNode nodePep,
                                                                   IsotopeLabelType labelType,
                                                                   IList<TransitionGroupDocNode> nodeGroups,
                                                                   FeatureCalculators calcs,
                                                                   IDictionary<int, int> runEnumDict,
                                                                   bool verbose)
        {
            var chromatograms = settings.MeasuredResults.Chromatograms;
            float mzMatchTolerance = (float)settings.TransitionSettings.Instrument.MzMatchTolerance;
            var nodeGroupChromGroupInfos = new List<List<IList<ChromatogramGroupInfo>>>();
            foreach (var nodeGroup in nodeGroups)
            {
                var chromGroupInfos = settings.MeasuredResults
                    .LoadChromatogramsForAllReplicates(nodePep, nodeGroup, mzMatchTolerance);
                Assume.AreEqual(chromGroupInfos.Count, chromatograms.Count);
                nodeGroupChromGroupInfos.Add(chromGroupInfos);
            }
            ChromatogramGroupInfo.LoadPeaksForAll(nodeGroupChromGroupInfos.SelectMany(list1=>list1.SelectMany(list2=>list2)), true);

            for (int replicateIndex = 0; replicateIndex < chromatograms.Count; replicateIndex++)
            {
                var chromatogramSet = chromatograms[replicateIndex];
                foreach (var chromFileInfo in chromatogramSet.MSDataFileInfos)
                {
                    var peakId = new PeakTransitionGroupId(nodePepGroup, nodePep, labelType, chromatogramSet, chromFileInfo, runEnumDict);
                    var listRunFeatures = new List<PeakGroupFeatures>();
                    var msDataFileLocation = chromFileInfo.FilePath.GetLocation();
                    var chromGroupInfos = nodeGroupChromGroupInfos.Select(
                        list => list[replicateIndex].FirstOrDefault(chromGroupInfo =>
                            Equals(chromGroupInfo.FilePath.GetLocation(), msDataFileLocation))).ToList();
                    if (chromGroupInfos[0] == null)
                    {
                        continue;
                    }
                    
                    var summaryPeakData = new SummaryPeptidePeakData(settings, nodePep, nodeGroups, chromatogramSet, chromFileInfo, chromGroupInfos);
                    var context = new PeakScoringContext(settings);

                    while (summaryPeakData.NextPeakIndex())
                    {
                        if (!summaryPeakData.HasArea)
                            continue;

                        var features = new float[calcs.Count];

                        for (int i = 0; i < calcs.Count; i++)
                        {
                            features[i] = summaryPeakData.GetScore(context, calcs[i]);
                        }

                        // CONSIDER: Peak features can take up a lot of space in large scale DIA
                        //           It may be possible to save even more by using a smaller struct
                        //           when times are not required, which they are only for export
                        float retentionTime = 0, startTime = 0, endTime = 0;
                        if (verbose)
                        {
                            var peakTimes = summaryPeakData.RetentionTimeStatistics;
                            retentionTime = peakTimes.RetentionTime;
                            startTime = peakTimes.StartTime;
                            endTime = peakTimes.EndTime;
                        }
                        int peakIndex = summaryPeakData.UsedBestPeakIndex
                            ? summaryPeakData.BestPeakIndex
                            : summaryPeakData.PeakIndex;
                        listRunFeatures.Add(new PeakGroupFeatures(peakIndex, retentionTime, startTime, endTime, new FeatureScores(calcs.FeatureNames, ImmutableList.ValueOf(features))));
                    }

                    yield return new PeakTransitionGroupFeatures(peakId, listRunFeatures.ToArray(), verbose);
                }
            }
        }

        public class SummaryPeptidePeakData : IPeptidePeakData<ISummaryPeakData>
        {
            private static readonly IList<ITransitionGroupPeakData<ISummaryPeakData>> EMPTY_DATA = new ITransitionGroupPeakData<ISummaryPeakData>[0];

            private readonly ChromatogramGroupInfo _chromGroupInfoPrimary;
            private int _peakIndex;

            public SummaryPeptidePeakData(SrmSettings settings,
                                          PeptideDocNode nodePep,
                                          IList<TransitionGroupDocNode> nodeGroups,
                                          ChromatogramSet chromatogramSet,
                                          ChromFileInfo chromFileInfo,
                                          IList<ChromatogramGroupInfo> chromGroupInfos)
            {
                _peakIndex = -1;
                _chromGroupInfoPrimary = chromGroupInfos[0];

                NodePep = nodePep;
                FileInfo = chromFileInfo;
                TransitionGroupPeakData = new SummaryTransitionGroupPeakData[nodeGroups.Count];
                for (int i = 0; i < nodeGroups.Count; i++)
                {
                    TransitionGroupPeakData[i] = new SummaryTransitionGroupPeakData(settings, nodeGroups[i],
                        chromatogramSet, chromGroupInfos[i]);
                }
                // Avoid extra ToArray() calls, since they show up in a profiler for big files
                bool? standard;
                if (AllSameStandardType(TransitionGroupPeakData, out standard))
                {
                    AnalyteGroupPeakData = standard.HasValue && !standard.Value ? TransitionGroupPeakData : EMPTY_DATA;
                    StandardGroupPeakData = standard.HasValue && standard.Value ? TransitionGroupPeakData : EMPTY_DATA;
                }
                else
                {
                    AnalyteGroupPeakData = TransitionGroupPeakData.Where(t => !t.IsStandard).ToArray();
                    StandardGroupPeakData = TransitionGroupPeakData.Where(t => t.IsStandard).ToArray();
                }
            }

            private bool AllSameStandardType(IList<ITransitionGroupPeakData<ISummaryPeakData>> transitionGroupPeakData, out bool? standard)
            {
                standard = null;
                foreach (var peakData in transitionGroupPeakData)
                {
                    if (!standard.HasValue)
                        standard = peakData.IsStandard;
                    else if (standard.Value != peakData.IsStandard)
                    {
                        standard = null;
                        return false;
                    }
                }
                return true;
            }

            public PeptideDocNode NodePep { get; private set; }

            public ChromFileInfo FileInfo { get; private set; }

            public IList<ITransitionGroupPeakData<ISummaryPeakData>> TransitionGroupPeakData { get; private set; }

            public IList<ITransitionGroupPeakData<ISummaryPeakData>> AnalyteGroupPeakData { get; private set; }

            public IList<ITransitionGroupPeakData<ISummaryPeakData>> StandardGroupPeakData { get; private set; }

            public IList<ITransitionGroupPeakData<ISummaryPeakData>> BestAvailableGroupPeakData
            {
                get { return StandardGroupPeakData.Count > 0 ? StandardGroupPeakData : AnalyteGroupPeakData; }
            }

            private IEnumerable<ITransitionPeakData<ISummaryPeakData>> TransitionPeakData
            {
                get { return TransitionGroupPeakData.SelectMany(pd => pd.TransitionPeakData); }
            }

            /// <summary>
            /// Due to an problem with Crawdad peak finding and ChromDataPeakSet.Extend(), it used
            /// to be possible to end up with peaks that had the same start and end time boundaries
            /// and zero area.  This function helps to keep such peaks from being enumerated.
            /// </summary>
            public bool HasArea
            {
                get
                {
                    // Using a Linq expression showed up in the profiler
                    foreach (var tg in TransitionGroupPeakData)
                    {
                        foreach (var pd in tg.TransitionPeakData)
                        {
                            if (pd.PeakData != null && pd.PeakData.Area != 0)
                                return true;
                        }
                    }
                    return false;
                }
            }

            public int BestPeakIndex
            {
                get { return _chromGroupInfoPrimary.BestPeakIndex; }
            }

            public bool UsedBestPeakIndex { get; private set; }

            public int PeakIndex { get { return _peakIndex; } }

            public bool NextPeakIndex()
            {
                // By derault use _peakIndex
                UsedBestPeakIndex = false;
                int lastPeakIndex = _peakIndex;

                _peakIndex++;
                if (_peakIndex >= _chromGroupInfoPrimary.NumPeaks)
                    return false;

                if (!SetPeakIndex() || !MatchingPeaks())
                {
                    _peakIndex = _chromGroupInfoPrimary.NumPeaks;

                    // If v1.4 and prior style peaks, where only the best peaks are expected to match,
                    // try using the best peaks
                    foreach (SummaryTransitionGroupPeakData tranGroupPeakData in TransitionGroupPeakData)
                    {
                        tranGroupPeakData.SetBestIndex();
                        // Avoid using a best peak twice
                        if (tranGroupPeakData.PeakIndex <= lastPeakIndex)
                            return false;
                    }
                    // One last peak index using the best peak that just got set
                    UsedBestPeakIndex = true;
                    return MatchingPeaks();
                }

                return true;
            }

            private bool SetPeakIndex()
            {
                foreach (SummaryTransitionGroupPeakData tranGroupPeakData in TransitionGroupPeakData)
                {
                    if (!tranGroupPeakData.SetPeakIndex(_peakIndex))
                        return false;
                }
                return true;
            }

            private bool MatchingPeaks()
            {
                // Peaks should be very nearly the same width (off only because of float rounding error)
                // and all overlapping
                float width = 0;
                float start = float.MaxValue;
                float end = float.MinValue;
                foreach (var groupPeakData in TransitionGroupPeakData)
                {
                    foreach (var peakData in groupPeakData.TransitionPeakData)
                    {
                        if (peakData.PeakData == null)
                            return false;

                        float widthNext = peakData.PeakData.EndTime - peakData.PeakData.StartTime;
                        if (width == 0)
                        {
                            width = widthNext;
                            start = peakData.PeakData.StartTime;
                            end = peakData.PeakData.EndTime;
                        }
                        // Check for width mismatch
                        if (Math.Abs(widthNext - width) > 0.0001)
                            return false;
                        if (start > peakData.PeakData.StartTime)
                        {
                            // Check for disjoint peaks
                            if (start > peakData.PeakData.EndTime)
                                return false;
                            // Expand allowable range
                            start = peakData.PeakData.StartTime;
                        }
                        if (end < peakData.PeakData.EndTime)
                        {
                            // Check for disjoint peaks
                            if (end < peakData.PeakData.StartTime)
                                return false;
                            // Expand allowable range
                            end = peakData.PeakData.EndTime;
                        }
                    }
                }
                return true;
            }

            public PeakTimes RetentionTimeStatistics
            {
                get
                {
                    // Avoid extra allocations for deteriming the median time. Probably the median
                    // of the 3 most intense peaks is better than the median of everything anyway.
                    ITransitionPeakData<ISummaryPeakData> maxPeakData = null;
                    ITransitionPeakData<ISummaryPeakData> max2PeakData = null;
                    ITransitionPeakData<ISummaryPeakData> max3PeakData = null;

                    foreach (var tg in TransitionGroupPeakData)
                    {
                        foreach (var pd in tg.TransitionPeakData)
                        {
                            var tranPeakDataCurrent = pd;
                            if (maxPeakData == null || tranPeakDataCurrent.PeakData.Height > maxPeakData.PeakData.Height)
                                Helpers.Swap(ref tranPeakDataCurrent, ref maxPeakData);
                            if (tranPeakDataCurrent == null)
                                continue;
                            if (max2PeakData == null || tranPeakDataCurrent.PeakData.Height > max2PeakData.PeakData.Height)
                                Helpers.Swap(ref tranPeakDataCurrent, ref max2PeakData);
                            if (tranPeakDataCurrent == null)
                                continue;
                            if (max3PeakData == null || tranPeakDataCurrent.PeakData.Height > max3PeakData.PeakData.Height)
                                Helpers.Swap(ref tranPeakDataCurrent, ref max3PeakData);
                        }
                    }

                    float retentionTime = maxPeakData != null ? maxPeakData.PeakData.RetentionTime : 0;
                    float startTime = maxPeakData != null ? maxPeakData.PeakData.StartTime : 0;
                    float endTime = maxPeakData != null ? maxPeakData.PeakData.EndTime : 0;
                    float medianTime = 0;
                    if (max3PeakData != null)
                        medianTime = max2PeakData.PeakData.RetentionTime;
                    else if (max2PeakData != null && maxPeakData != null) // Keep ReSharper happy with second check
                        medianTime = (maxPeakData.PeakData.RetentionTime + max2PeakData.PeakData.RetentionTime) / 2;
                    else if (maxPeakData != null)
                        medianTime = maxPeakData.PeakData.RetentionTime;
                    return new PeakTimes(retentionTime, startTime, endTime, medianTime);
                }
            }

            public float GetScore(PeakScoringContext context, IPeakFeatureCalculator calc)
            {
                var summaryCalc = calc as SummaryPeakFeatureCalculator;
                if (summaryCalc != null)
                    return summaryCalc.Calculate(context, this);
                var groupPeakData = TransitionGroupPeakData.FirstOrDefault() as SummaryTransitionGroupPeakData;
                if (groupPeakData != null)
                    return groupPeakData.GetScore(calc.GetType());
                return float.NaN;
            }
        }

        public struct PeakTimes
        {
            public PeakTimes(float retentionTime, float startTime, float endTime, float medianTime) : this()
            {
                RetentionTime = retentionTime;
                StartTime = startTime;
                EndTime = endTime;
                MedianTime = medianTime;
            }

            public float RetentionTime { get; private set; }
            public float StartTime { get; private set; }
            public float EndTime { get; private set; }
            public float MedianTime { get; private set; }
        }

        public sealed class SummaryTransitionGroupPeakData : ITransitionGroupPeakData<ISummaryPeakData>
        {
            private static readonly IList<ITransitionPeakData<ISummaryPeakData>> EMPTY_DATA = new ITransitionPeakData<ISummaryPeakData>[0];

            private readonly ChromatogramGroupInfo _chromGroupInfo;
            private int _peakIndex;

            public SummaryTransitionGroupPeakData(SrmSettings settings,
                PeptideDocNode nodePep,
                TransitionGroupDocNode nodeGroup,
                ChromatogramSet chromatogramSet,
                MsDataFileUri filePath) : this(settings, nodeGroup, chromatogramSet,
                settings.LoadChromatogramGroup(chromatogramSet, filePath, nodePep, nodeGroup))
            {

            }


            public SummaryTransitionGroupPeakData(SrmSettings settings,
                                                  TransitionGroupDocNode nodeGroup,
                                                  ChromatogramSet chromatogramSet,
                                                  ChromatogramGroupInfo chromGroupInfo)
            {
                _peakIndex = -1;

                NodeGroup = nodeGroup;
                IsStandard = settings.PeptideSettings.Modifications.InternalStandardTypes
                    .Contains(nodeGroup.TransitionGroup.LabelType);
                TransitionPeakData = Ms1TranstionPeakData = Ms2TranstionPeakData = EMPTY_DATA;
                bool isDda = settings.TransitionSettings.FullScan.AcquisitionMethod ==
                             FullScanAcquisitionMethod.DDA;

                _chromGroupInfo = chromGroupInfo;
                if (_chromGroupInfo != null)
                {
                    int ms1Count = 0, ms2Count = 0, totalCount = 0;
                    // Assume there will be one per transtion
                    var listPeakData = new ITransitionPeakData<ISummaryPeakData>[nodeGroup.TransitionCount];
                    float mzMatchTolerance = (float)settings.TransitionSettings.Instrument.MzMatchTolerance;
                    foreach (var nodeTran in nodeGroup.Transitions)
                    {
                        var tranInfo = _chromGroupInfo.GetTransitionInfo(nodeTran, mzMatchTolerance, chromatogramSet.OptimizationFunction);
                        if (tranInfo == null)
                            continue;
                        listPeakData[totalCount++] = new SummaryTransitionPeakData(settings, nodeTran, chromatogramSet, tranInfo);
                        if (nodeTran.IsMs1)
                            ms1Count++;
                        else
                            ms2Count++;
                    }
                    // If something was missing reallocate, which can't be slower than List.ToArray()
                    if (totalCount < listPeakData.Length)
                    {
                        var peakDatasShort = new ITransitionPeakData<ISummaryPeakData>[totalCount];
                        Array.Copy(listPeakData, peakDatasShort, totalCount);
                        listPeakData = peakDatasShort;
                    }
                    TransitionPeakData = listPeakData.ToArray();
                    Ms1TranstionPeakData = GetTransitionTypePeakData(ms1Count, ms2Count, true);
                    Ms2TranstionPeakData = Ms2TranstionDotpData =
                        GetTransitionTypePeakData(ms1Count, ms2Count, false);
                    if (isDda)
                        Ms2TranstionPeakData = Array.Empty<ITransitionPeakData<ISummaryPeakData>>();
                }
            }

            private IList<ITransitionPeakData<ISummaryPeakData>> GetTransitionTypePeakData(int ms1Count, int ms2Count, bool ms1Type)
            {
                if ((ms1Type && ms1Count == 0) || (!ms1Type && ms2Count == 0))
                    return EMPTY_DATA;
                else if ((ms1Type && ms2Count == 0) || (!ms1Type && ms1Count == 0))
                    return TransitionPeakData;
                else if (ms1Type)
                    return GetTransitionPeakData(ms1Count, t => t.IsMs1);
                else
                    return GetTransitionPeakData(ms2Count, t => !t.IsMs1);
            }

            private IList<ITransitionPeakData<ISummaryPeakData>> GetTransitionPeakData(int count, Func<TransitionDocNode, bool> selectNode)
            {
                var arrayPeakData = new ITransitionPeakData<ISummaryPeakData>[count];
                int nextIndex = 0;
                foreach (var peakData in TransitionPeakData)
                {
                    if (selectNode(peakData.NodeTran))
                        arrayPeakData[nextIndex++] = peakData;
                }
                return arrayPeakData;
            }

            public TransitionGroupDocNode NodeGroup { get; private set; }
            public bool IsStandard { get; private set; }
            public IList<ITransitionPeakData<ISummaryPeakData>> TransitionPeakData { get; private set; }
            public IList<ITransitionPeakData<ISummaryPeakData>> Ms1TranstionPeakData { get; private set; }
            public IList<ITransitionPeakData<ISummaryPeakData>> Ms2TranstionPeakData { get; private set; }
            public IList<ITransitionPeakData<ISummaryPeakData>> Ms2TranstionDotpData { get; private set; }

            public IList<ITransitionPeakData<ISummaryPeakData>> DefaultTranstionPeakData
            {
                get { return Ms2TranstionPeakData.Count > 0 ? Ms2TranstionPeakData : Ms1TranstionPeakData; }
            }

            public bool SetPeakIndex(int peakIndex)
            {
                if (_chromGroupInfo == null || 0 > peakIndex || peakIndex >= _chromGroupInfo.NumPeaks)
                {
                    PeakIndex = -1;
                    return false;                    
                }
                PeakIndex = peakIndex;
                return true;
            }

            public void SetBestIndex()
            {
                PeakIndex = _chromGroupInfo != null ? _chromGroupInfo.BestPeakIndex : -1;
            }

            public int PeakIndex
            {
                get { return _peakIndex; }
                private set
                {
                    _peakIndex = value;
                    if (TransitionPeakData != null)
                    {
                        foreach (SummaryTransitionPeakData tranPeakData in TransitionPeakData)
                        {
                            tranPeakData.PeakIndex = _peakIndex;
                        }
                    }
                }
            }

            public float GetScore(Type scoreType)
            {
                return _chromGroupInfo.GetScore(scoreType, _peakIndex);
            }
        }

        private sealed class SummaryTransitionPeakData : ITransitionPeakData<ISummaryPeakData>
        {
            private int _peakIndex;
            private readonly ChromatogramInfo _chromInfo;

            public SummaryTransitionPeakData(SrmSettings settings,
                                             TransitionDocNode nodeTran,
                                             ChromatogramSet chromatogramSet,
                                             ChromatogramInfo tranChromInfo)
            {
                NodeTran = nodeTran;

                _chromInfo = tranChromInfo;
            }

            public int PeakIndex
            {
                set
                {
                    _peakIndex = value;
                    PeakData = null;
                    if (_peakIndex != -1 && _chromInfo != null)
                        PeakData = _chromInfo.GetPeak(_peakIndex);
                }
            }
            public TransitionDocNode NodeTran { get; private set; }
            public ISummaryPeakData PeakData { get; private set; }
        }
    }

    /// <summary>
    /// Indentifier for a group of transitions that get scored together.
    /// </summary>
    public sealed class PeakTransitionGroupId
    {
        public PeakTransitionGroupId(PeptideGroupDocNode nodePepGroup,
                                     PeptideDocNode nodePep,
                                     IsotopeLabelType labelType,
                                     ChromatogramSet chromatogramSet,
                                     ChromFileInfo chromFileInfo,
                                     IDictionary<int, int> runEnumDict)
        {
            NodePepGroup = nodePepGroup;
            LabelType = labelType;
            ChromatogramSet = chromatogramSet;
            FilePath = chromFileInfo.FilePath;
            FileId = chromFileInfo.FileId;
            Run = runEnumDict[FileId.GlobalIndex];

            // Avoid hanging onto the peptide, since it can end up being the primary memory root
            // for large-scale command-line processing
            RawTextId = nodePep.ModifiedTarget.ToString();
            RawUnmodifiedTextId = nodePep.Target.ToString();
            IsDecoy = nodePep.IsDecoy;
            Key = new PeakTransitionGroupIdKey(nodePep.Peptide, FileId);
        }

        public PeptideGroupDocNode NodePepGroup { get; private set; }
        public IsotopeLabelType LabelType { get; private set; }
        public ChromatogramSet ChromatogramSet { get; private set; }
        public ChromFileInfoId FileId { get; private set; }
        public MsDataFileUri FilePath { get; private set; }
        public int Run { get; private set; }

        public string RawTextId { get; private set; }
        public string RawUnmodifiedTextId { get; private set; }
        public bool IsDecoy { get; private set; }

        /// <summary>
        /// Guaranteed unique key for internal use
        /// </summary>
        public PeakTransitionGroupIdKey Key { get; private set; }

        /// <summary>
        /// Mostly unique string identifier for external use with R version of mProphet
        /// </summary>
        public override string ToString()
        {
            var sb = new StringBuilder();
            if (IsDecoy)
                sb.Append(@"DECOY_");
            sb.Append(RawTextId); // Modified sequence, or display name for custom ion
            if (!LabelType.IsLight)
                sb.Append(@"_").Append(LabelType);
            sb.Append(@"_run").Append(Run);
            return sb.ToString();
        }
    }

    public struct PeakTransitionGroupIdKey
    {
        public PeakTransitionGroupIdKey(Peptide peptide, ChromFileInfoId fileId) : this()
        {
            Peptide = peptide;
            FileId = fileId;
        }

        public Peptide Peptide { get; }
        public ChromFileInfoId FileId { get; }

        public bool Equals(PeakTransitionGroupIdKey other)
        {
            return ReferenceEquals(Peptide, other.Peptide) && ReferenceEquals(FileId, other.FileId);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is PeakTransitionGroupIdKey && Equals((PeakTransitionGroupIdKey) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (RuntimeHelpers.GetHashCode(Peptide)*397) ^ RuntimeHelpers.GetHashCode(FileId);
            }
        }
    }

    public interface IFeatureScoreProvider
    {
        PeakTransitionGroupFeatureSet GetFeatureScores(SrmDocument document, IPeakScoringModel scoringModel, IProgressMonitor progressMonitor);
    }

    public sealed class PeakTransitionGroupFeatureSet
    {
        public PeakTransitionGroupFeatureSet(int decoyCount, PeakTransitionGroupFeatures[] features)
        {
            DecoyCount = decoyCount;
            Features = features;
        }

        public int TargetCount { get { return Features.Length - DecoyCount; } }
        public int DecoyCount { get; private set; }
        public PeakTransitionGroupFeatures[] Features { get; private set; }
    }

    /// <summary>
    /// All features for all peak groups of a scored group of transitions.
    /// </summary>
    public struct PeakTransitionGroupFeatures
    {
        public PeakTransitionGroupFeatures(PeakTransitionGroupId id, IList<PeakGroupFeatures> peakGroupFeatures, bool verbose) : this()
        {
            Key = id.Key;
            IsDecoy = id.IsDecoy;
            PeakGroupFeatures = peakGroupFeatures;
            if (verbose)
                Id = id;    // Avoid holding this in memory unless required for verbose output
        }

        public PeakTransitionGroupIdKey Key { get; private set; }
        public bool IsDecoy { get; private set; }
        public IList<PeakGroupFeatures> PeakGroupFeatures { get; private set; }
        public PeakTransitionGroupId Id { get; private set; }
    }

    public struct PeakGroupFeatures
    {
        public PeakGroupFeatures(int peakIndex, float retentionTime, float startTime, float endTime,
            FeatureScores features) : this()
        {
            OriginalPeakIndex = peakIndex;
            // CONSIDER: This impacts memory consumption for large-scale DIA, and it is not clear anyone uses these
            RetentionTime = retentionTime;
            StartTime = startTime;
            EndTime = endTime;
            FeatureScores = features;
        }

        public int OriginalPeakIndex { get; private set; }
        public float RetentionTime { get; private set; }
        public float StartTime { get; private set; }
        public float EndTime { get; private set; }
        public FeatureScores FeatureScores { get; }
        public ImmutableList<float> Features
        {
            get { return FeatureScores.Values; }
        }
    }
}
