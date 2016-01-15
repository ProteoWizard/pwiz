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
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results.Scoring
{
    public static class PeakFeatureEnumerator
    {
        public static PeakTransitionGroupFeatures[] GetPeakFeatures(this SrmDocument document,
                                                                               IList<IPeakFeatureCalculator> calcs,
                                                                               IProgressMonitor progressMonitor = null,
                                                                               bool includeMzFilters = false)
        {
            // Get features for each peptide
            int totalPeptides = document.MoleculeCount;
            int currentPeptide = 0;
            var status = new ProgressStatus(Resources.PeakFeatureEnumerator_GetPeakFeatures_Calculating_peak_group_scores);

            // Set up run ID dictionary
            var runEnumDict = new Dictionary<int, int>();
            var chromatograms = document.Settings.MeasuredResults.Chromatograms;
            foreach (var fileInfo in chromatograms.SelectMany(c => c.MSDataFileInfos))
            {
                runEnumDict.Add(fileInfo.FileIndex, runEnumDict.Count + 1);
            }

            // Using Parallel.For is quicker, but order needs to be maintained
            var moleculeGroupPairs = document.GetMoleculeGroupPairs();
            var peakFeatureLists = new List<PeakTransitionGroupFeatures>[moleculeGroupPairs.Length];
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
                peakFeatureLists[i] = peakFeatureList;
                foreach (var peakFeature in document.GetPeakFeatures(nodePepGroup, nodePep, calcs, runEnumDict, includeMzFilters))
                {
                    if (peakFeature.PeakGroupFeatures.Any())
                    {
                        peakFeatureList.Add(peakFeature);
                        Interlocked.Increment(ref peakFeatureCount);
                    }
                }
            });

            var result = new PeakTransitionGroupFeatures[peakFeatureCount];
            int peakFeatureCurrent = 0;
            foreach (var peakFeatureList in peakFeatureLists)
            {
                if (peakFeatureList == null)
                    continue;

                foreach (var peakFeature in peakFeatureList)
                    result[peakFeatureCurrent++] = peakFeature;
            }

            if (progressMonitor != null)
                progressMonitor.UpdateProgress(status.ChangePercentComplete(100));
            return result;
        }

        private static IEnumerable<PeakTransitionGroupFeatures> GetPeakFeatures(this SrmDocument document,
                                                                                PeptideGroupDocNode nodePepGroup,
                                                                                PeptideDocNode nodePep,
                                                                                IList<IPeakFeatureCalculator> calcs,
                                                                                IDictionary<int, int> runEnumDict,
                                                                                bool includeMzFilters)
        {
            // Get peptide features for each set of comparable groups
            foreach (var nodeGroups in ComparableGroups(nodePep))
            {
                var arrayGroups = nodeGroups.ToArray();
                var labelType = arrayGroups[0].TransitionGroup.LabelType;
                foreach (var peakFeature in document.GetPeakFeatures(nodePepGroup, nodePep, labelType, arrayGroups, calcs, runEnumDict, includeMzFilters))
                {
                    yield return peakFeature;
                }
            }
        }

        public static IEnumerable<IEnumerable<TransitionGroupDocNode>> ComparableGroups(PeptideDocNode nodePep)
        {
            // Everything with relative RT not unknown makes up a comparable set
            yield return from nodeGroup in nodePep.TransitionGroups
                         where nodeGroup.RelativeRT != RelativeRT.Unknown
                         select nodeGroup;

            // Each other label type makes up a comparable set, i.e. charge states of a label type
            foreach (var nodeGroups in from nodeGroup in nodePep.TransitionGroups
                                            where nodeGroup.RelativeRT == RelativeRT.Unknown
                                            group nodeGroup by nodeGroup.TransitionGroup.LabelType)
            {
                yield return nodeGroups;
            }
        }

        private static IEnumerable<PeakTransitionGroupFeatures> GetPeakFeatures(this SrmDocument document,
                                                                   PeptideGroupDocNode nodePepGroup,
                                                                   PeptideDocNode nodePep,
                                                                   IsotopeLabelType labelType,
                                                                   IList<TransitionGroupDocNode> nodeGroups,
                                                                   IList<IPeakFeatureCalculator> calcs,
                                                                   IDictionary<int, int> runEnumDict,
                                                                   bool includeMzFilters)
        {
            var chromatograms = document.Settings.MeasuredResults.Chromatograms;
            float mzMatchTolerance = (float)document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            foreach (var chromatogramSet in chromatograms)
            {
                ChromatogramGroupInfo[] arrayChromInfo;
                if (!document.Settings.MeasuredResults.TryLoadChromatogram(chromatogramSet, nodePep, nodeGroups[0],
                                                                           mzMatchTolerance, false, out arrayChromInfo))
                {
                    continue;
                }

                foreach (var chromGroupInfo in arrayChromInfo)
                {
                    var peakId = new PeakTransitionGroupId(nodePepGroup, nodePep, labelType, chromatogramSet, chromGroupInfo, runEnumDict);
                    var listRunFeatures = new List<PeakGroupFeatures>();

                    var summaryPeakData = new SummaryPeptidePeakData(document, nodePep, nodeGroups, chromatogramSet, chromGroupInfo);
                    var context = new PeakScoringContext(document);

                    while (summaryPeakData.NextPeakIndex())
                    {
                        if (!summaryPeakData.HasArea)
                            continue;

                        var features = new float[calcs.Count];

                        for (int i = 0; i < calcs.Count; i++)
                        {
                            features[i] = summaryPeakData.GetScore(context, calcs[i]);
                        }

                        MzFilterPairs[] mzFilters = null;
                        if (includeMzFilters)
                            mzFilters = summaryPeakData.GetMzFilters(context);

                        float retentionTime = summaryPeakData.RetentionTime;
                        float startTime = summaryPeakData.StartTime;
                        float endTime = summaryPeakData.EndTime;
                        bool isMaxPeakIndex = summaryPeakData.IsMaxPeakIndex;
                        listRunFeatures.Add(new PeakGroupFeatures(summaryPeakData.PeakIndex, retentionTime, startTime, endTime,
                            isMaxPeakIndex, features, mzFilters));
                    }

                    yield return new PeakTransitionGroupFeatures(peakId, listRunFeatures.ToArray());
                }
            }
        }

        private class SummaryPeptidePeakData : IPeptidePeakData<ISummaryPeakData>
        {
            private static readonly IList<ITransitionGroupPeakData<ISummaryPeakData>> EMPTY_DATA = new ITransitionGroupPeakData<ISummaryPeakData>[0];

            private readonly ChromatogramGroupInfo _chromGroupInfoPrimary;
            private int _peakIndex;

            public SummaryPeptidePeakData(SrmDocument document,
                                          PeptideDocNode nodePep,
                                          IList<TransitionGroupDocNode> nodeGroups,
                                          ChromatogramSet chromatogramSet,
                                          ChromatogramGroupInfo chromGroupInfoPrimary)
            {
                _peakIndex = -1;
                _chromGroupInfoPrimary = chromGroupInfoPrimary;

                NodePep = nodePep;
                FileInfo = chromatogramSet.GetFileInfo(chromGroupInfoPrimary);
                TransitionGroupPeakData = new SummaryTransitionGroupPeakData[nodeGroups.Count];
                for (int i = 0; i < nodeGroups.Count; i++)
                {
                    TransitionGroupPeakData[i] = new SummaryTransitionGroupPeakData(document,
                        nodePep, nodeGroups[i], chromatogramSet, chromGroupInfoPrimary);
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
                get { return TransitionPeakData.FirstOrDefault(pd => pd.PeakData != null && pd.PeakData.Area != 0) != null; }
            }

            public bool IsMaxPeakIndex
            {
                get { return _peakIndex == _chromGroupInfoPrimary.BestPeakIndex; }
            }

            public int PeakIndex { get { return _peakIndex; } }

            public bool NextPeakIndex()
            {
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

            public float RetentionTime
            {
                get
                {
                    ISummaryPeakData maxPeakData = null;
                    foreach (var tranPeakData in TransitionGroupPeakData.SelectMany(g => g.TransitionPeakData))
                    {
                        if (maxPeakData == null || tranPeakData.PeakData.Height > maxPeakData.Height)
                            maxPeakData = tranPeakData.PeakData;
                    }
                    return maxPeakData != null ? maxPeakData.RetentionTime : 0;
                }
            }

            public float StartTime
            {
                get
                {
                    ISummaryPeakData maxPeakData = null;
                    foreach (var tranPeakData in TransitionGroupPeakData.SelectMany(g => g.TransitionPeakData))
                    {
                        if (maxPeakData == null || tranPeakData.PeakData.Height > maxPeakData.Height)
                            maxPeakData = tranPeakData.PeakData;
                    }
                    return maxPeakData != null ? maxPeakData.StartTime : 0;
                }
            }

            public float EndTime
            {
                get
                {
                    ISummaryPeakData maxPeakData = null;
                    foreach (var tranPeakData in TransitionGroupPeakData.SelectMany(g => g.TransitionPeakData))
                    {
                        if (maxPeakData == null || tranPeakData.PeakData.Height > maxPeakData.Height)
                            maxPeakData = tranPeakData.PeakData;
                    }
                    return maxPeakData != null ? maxPeakData.EndTime : 0;
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

            public MzFilterPairs[] GetMzFilters(PeakScoringContext context)
            {
                var listMzFilters = new List<MzFilterPairs>();
                var fullScan = context.Document.Settings.TransitionSettings.FullScan;
                // Impossible to report precursor filter for results dependent DIA
                if (fullScan.AcquisitionMethod == FullScanAcquisitionMethod.DIA && fullScan.IsolationScheme.FromResults)
                        return listMzFilters.ToArray();
                foreach (var transitionGroupPeakData in TransitionGroupPeakData)
                {
                    double targetMzDia = 0, widthMzDia = 0;
                    if (fullScan.AcquisitionMethod == FullScanAcquisitionMethod.DIA)
                    {
                        var isolationWindows = fullScan.IsolationScheme.GetIsolationWindowsContaining(
                            transitionGroupPeakData.NodeGroup.PrecursorMz);

                        double start = double.MaxValue, end = double.MinValue;
                        foreach (var isolationWindow in isolationWindows)
                        {
                            start = Math.Min(start, isolationWindow.Start);
                            end = Math.Max(end, isolationWindow.End);
                        }
                        // This should not happen, but if no containing windows were found, give up.
                        if (start > end)
                            return new MzFilterPairs[0];
                        targetMzDia = (start + end) / 2;
                        widthMzDia = end - start;
                    }
                    foreach (var transitionPeakData in transitionGroupPeakData.TransitionPeakData)
                    {
                        // Skip forced integration peaks
                        if (transitionPeakData.PeakData == null)
                            continue;

                        // Default to SRM filters
                        var mzFilter = new MzFilterPairs
                        {
                            TargetPrecursorMz = transitionGroupPeakData.NodeGroup.PrecursorMz,
                            WidthPrecursorMz = 0.7,
                            TargetProductMz = transitionPeakData.NodeTran.Mz,
                            WidthProductMz = 0.7,
                            IsForcedIntegration = transitionPeakData.PeakData.IsForcedIntegration
                        };
                        if (fullScan.IsEnabled)
                        {
                            if (transitionPeakData.NodeTran.IsMs1)
                            {
                                // Move product mz to precursor mz for isotope transitions
                                mzFilter.TargetPrecursorMz = mzFilter.TargetProductMz.Value;
                                mzFilter.WidthPrecursorMz = fullScan.GetPrecursorFilterWindow(mzFilter.TargetPrecursorMz);
                                mzFilter.TargetProductMz = null;
                                mzFilter.WidthProductMz = null;
                            }
                            else
                            {
                                if (fullScan.AcquisitionMethod == FullScanAcquisitionMethod.Targeted)
                                {
                                    mzFilter.WidthPrecursorMz = 2.0;
                                }
                                else if (fullScan.AcquisitionMethod == FullScanAcquisitionMethod.DIA)
                                {
                                    mzFilter.TargetPrecursorMz = targetMzDia;
                                    mzFilter.WidthPrecursorMz = widthMzDia;
                                }
                                mzFilter.WidthProductMz = fullScan.GetProductFilterWindow(mzFilter.TargetProductMz.Value);
                            }
                        }
                        listMzFilters.Add(mzFilter);
                    }
                }
                return listMzFilters.ToArray();
            }
        }

        private sealed class SummaryTransitionGroupPeakData : ITransitionGroupPeakData<ISummaryPeakData>
        {
            private static readonly IList<ITransitionPeakData<ISummaryPeakData>> EMPTY_DATA = new ITransitionPeakData<ISummaryPeakData>[0];

            private readonly ChromatogramGroupInfo _chromGroupInfo;
            private int _peakIndex;

            public SummaryTransitionGroupPeakData(SrmDocument document,
                                                  PeptideDocNode nodePep,
                                                  TransitionGroupDocNode nodeGroup,
                                                  ChromatogramSet chromatogramSet,
                                                  ChromatogramGroupInfo chromGroupInfoPrimary)
            {
                _peakIndex = -1;

                NodeGroup = nodeGroup;
                IsStandard = document.Settings.PeptideSettings.Modifications.InternalStandardTypes
                    .Contains(nodeGroup.TransitionGroup.LabelType);
                TransitionPeakData = Ms1TranstionPeakData = Ms2TranstionPeakData = EMPTY_DATA;

                ChromatogramGroupInfo[] arrayChromInfo;
                var measuredResults = document.Settings.MeasuredResults;
                float mzMatchTolerance = (float) document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
                if (measuredResults.TryLoadChromatogram(chromatogramSet, nodePep, nodeGroup, mzMatchTolerance, false,
                                                        out arrayChromInfo))
                {
                    _chromGroupInfo = arrayChromInfo.FirstOrDefault(ci =>
                        Equals(ci.FilePath, chromGroupInfoPrimary.FilePath));

                    if (_chromGroupInfo != null)
                    {
                        int ms1Count = 0, ms2Count = 0;
                        var listPeakData = new List<ITransitionPeakData<ISummaryPeakData>>();
                        foreach (var nodeTran in nodeGroup.Transitions)
                        {
                            var tranInfo = _chromGroupInfo.GetTransitionInfo((float) nodeTran.Mz, mzMatchTolerance);
                            if (tranInfo == null)
                                continue;
                            listPeakData.Add(new SummaryTransitionPeakData(document, nodeTran, chromatogramSet, tranInfo));
                            if (nodeTran.IsMs1)
                                ms1Count++;
                            else
                                ms2Count++;
                        }
                        TransitionPeakData = listPeakData.ToArray();
                        Ms1TranstionPeakData = GetTransitionTypePeakData(ms1Count, ms2Count, true);
                        Ms2TranstionPeakData = GetTransitionTypePeakData(ms1Count, ms2Count, false);
                    }
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

            public SummaryTransitionPeakData(SrmDocument document,
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
                                     ChromatogramGroupInfo chromGroupInfo,
                                     IDictionary<int, int> runEnumDict)
        {
            NodePepGroup = nodePepGroup;
            NodePep = nodePep;
            LabelType = labelType;
            ChromatogramSet = chromatogramSet;
            FilePath = chromGroupInfo.FilePath;
            FileId = chromatogramSet.FindFile(chromGroupInfo);
            Run = runEnumDict[FileId.GlobalIndex];
        }

        public PeptideGroupDocNode NodePepGroup { get; private set; }
        public PeptideDocNode NodePep { get; private set; }
        public IsotopeLabelType LabelType { get; private set; }
        public ChromatogramSet ChromatogramSet { get; private set; }
        public ChromFileInfoId FileId { get; private set; }
        public MsDataFileUri FilePath { get; private set; }
        public int Run { get; private set; }

        /// <summary>
        /// Guaranteed unique key for internal use
        /// </summary>
        public PeakTransitionGroupIdKey Key
        {
            get { return new PeakTransitionGroupIdKey(NodePep.Id.GlobalIndex, FileId.GlobalIndex); }
        }

        /// <summary>
        /// Mostly unique string identifier for external use with R version of mProphet
        /// </summary>
        public override string ToString()
        {
            var sb = new StringBuilder();
            if (NodePep.IsDecoy)
                sb.Append("DECOY_"); // Not L10N
            sb.Append(NodePep.RawTextId); // Modified sequence, or display name for custom ion
            if (!LabelType.IsLight)
                sb.Append("_").Append(LabelType); // Not L10N
            sb.Append("_run").Append(Run); // Not L10N
            return sb.ToString();
        }
    }

    public struct PeakTransitionGroupIdKey
    {
        public PeakTransitionGroupIdKey(int pepIndex, int fileIndex) : this()
        {
            PepIndex = pepIndex;
            FileIndex = fileIndex;
        }

        public int PepIndex { get; private set; }
        public int FileIndex { get; private set; }

        public bool Equals(PeakTransitionGroupIdKey other)
        {
            return PepIndex == other.PepIndex && FileIndex == other.FileIndex;
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
                return (PepIndex*397) ^ FileIndex;
            }
        }
    }

    /// <summary>
    /// All features for all peak groups of a scored group of transitions.
    /// </summary>
    public sealed class PeakTransitionGroupFeatures
    {
        public PeakTransitionGroupFeatures(PeakTransitionGroupId id, IList<PeakGroupFeatures> peakGroupFeatures)
        {
            Id = id;
            PeakGroupFeatures = peakGroupFeatures;
        }

        public PeakTransitionGroupId Id { get; private set; }
        public IList<PeakGroupFeatures> PeakGroupFeatures { get; private set; }
    }

    public sealed class PeakGroupFeatures
    {
        public PeakGroupFeatures(int peakIndex, float retentionTime, float startTime, float endTime, bool isMaxPeak,
            float[] features, MzFilterPairs[] filterPairs)
        {
            OriginalPeakIndex = peakIndex;
            RetentionTime = retentionTime;
            StartTime = startTime;
            EndTime = endTime;
            IsMaxPeak = isMaxPeak;
            Features = features;
            FilterPairs = filterPairs;
        }

        public int OriginalPeakIndex { get; private set; }
        public float RetentionTime { get; private set; }
        public float StartTime { get; private set; }
        public float EndTime { get; private set; }
        public bool IsMaxPeak { get; private set; } // Max peak picked during import
        public float[] Features { get; private set; }
        public MzFilterPairs[] FilterPairs { get; private set; }

        public string GetFilterPairsText(CultureInfo cultureInfo)
        {
            if (FilterPairs == null)
                return string.Empty;

            var sb = new StringBuilder();
            foreach (var filterPairs in FilterPairs)
            {
                sb.Append('(');
                sb.Append(filterPairs.TargetPrecursorMz.ToString(cultureInfo));
                sb.Append(cultureInfo.TextInfo.ListSeparator).Append(filterPairs.WidthPrecursorMz.ToString(cultureInfo));
                if (filterPairs.TargetProductMz.HasValue && filterPairs.WidthProductMz.HasValue)
                {
                    sb.Append(cultureInfo.TextInfo.ListSeparator).Append(filterPairs.TargetProductMz);
                    sb.Append(cultureInfo.TextInfo.ListSeparator).Append(filterPairs.WidthProductMz);
                }
                sb.Append(cultureInfo.TextInfo.ListSeparator).Append(filterPairs.IsForcedIntegration ? 0 : 1);
                sb.Append(')');
            }
            return sb.ToString();
        }
    }

    public struct MzFilterPairs
    {
        public double TargetPrecursorMz { get; set; }
        public double WidthPrecursorMz { get; set; }
        public double? TargetProductMz { get; set; }
        public double? WidthProductMz { get; set; }
        public bool IsForcedIntegration { get; set; }
    }
}
