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
using System.Text;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Results.Scoring
{
    public static class PeakFeatureEnumerator
    {
        public static IEnumerable<PeakTransitionGroupFeatures> GetPeakFeatures(this SrmDocument document,
                                                                               IList<IPeakFeatureCalculator> calcs,
                                                                               IProgressMonitor progressMonitor = null)
        {
            // Get features for each peptide
            int totalPeptides = document.PeptideCount;
            int currentPeptide = 0;
            var status = new ProgressStatus(string.Empty);

            // Exclude RT standard peptides
            RetentionTimeRegression rtRegression = null;
            if (document.Settings.PeptideSettings.Prediction.RetentionTime != null)
                rtRegression = document.Settings.PeptideSettings.Prediction.RetentionTime;

            foreach (var nodePepGroup in document.PeptideGroups)
            {
                foreach (var nodePep in nodePepGroup.Peptides)
                {
                    if (rtRegression != null && rtRegression.IsStandardPeptide(nodePep))
                        continue;

                    if (progressMonitor != null)
                    {
                        int percentComplete = currentPeptide++*100/totalPeptides;
                        if (percentComplete < 100)
                        {
                            progressMonitor.UpdateProgress(status =
                                status.ChangeMessage(string.Format("Calculating peak group scores for {0}",
                                    nodePep.ModifiedSequenceDisplay))
                                      .ChangePercentComplete(percentComplete));
                        }
                    }

                    foreach (var peakFeature in document.GetPeakFeatures(nodePepGroup, nodePep, calcs))
                    {
                        yield return peakFeature;
                    }
                }
            }

            if (progressMonitor != null)
                progressMonitor.UpdateProgress(status.ChangePercentComplete(100));
        }

        private static IEnumerable<PeakTransitionGroupFeatures> GetPeakFeatures(this SrmDocument document,
                                                                                PeptideGroupDocNode nodePepGroup,
                                                                                PeptideDocNode nodePep,
                                                                                IList<IPeakFeatureCalculator> calcs)
        {
            // Get peptide features for each set of comparable groups
            return (from nodeGroups in ComparableGroups(nodePep)
                    select nodeGroups.ToArray() into arrayGroups let labelType = arrayGroups[0].TransitionGroup.LabelType
                    select document.GetPeakFeatures(nodePepGroup, nodePep, labelType, arrayGroups, calcs)).SelectMany(f => f);
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
                                                                   IList<IPeakFeatureCalculator> calcs)
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
                    var peakId = new PeakTransitionGroupId(nodePepGroup, nodePep, labelType, chromatogramSet, chromGroupInfo);
                    var listRunFeatures = new List<PeakGroupFeatures>();

                    var summaryPeakData = new SummaryPeptidePeakData(document, nodePep, nodeGroups, chromatogramSet, chromGroupInfo);
                    var context = new PeakScoringContext(document);

                    while (summaryPeakData.NextPeakIndex())
                    {
                        if (!summaryPeakData.HasArea)
                            continue;

                        var listFeatures = new List<float>();

                        foreach (var calc in calcs)
                        {
                            listFeatures.Add(summaryPeakData.GetScore(context, calc));
                        }

                        float retentionTime = summaryPeakData.RetentionTime;
                        listRunFeatures.Add(new PeakGroupFeatures(retentionTime, listFeatures.ToArray()));
                    }

                    yield return new PeakTransitionGroupFeatures(peakId, listRunFeatures);
                }
            }
        }

        private class SummaryPeptidePeakData : IPeptidePeakData<ISummaryPeakData>
        {
            private readonly ChromatogramGroupInfo _chromGroupInfoPrimary;
            private int _peakIndex;

            public SummaryPeptidePeakData(SrmDocument document,
                                          PeptideDocNode nodePep,
                                          IEnumerable<TransitionGroupDocNode> nodeGroups,
                                          ChromatogramSet chromatogramSet,
                                          ChromatogramGroupInfo chromGroupInfoPrimary)
            {
                _peakIndex = -1;
                _chromGroupInfoPrimary = chromGroupInfoPrimary;

                NodePep = nodePep;
                FileInfo = chromatogramSet.GetFileInfo(chromGroupInfoPrimary);
                TransitionGroupPeakData = nodeGroups.Select(
                    nodeGroup => new SummaryTransitionGroupPeakData(document,
                                                                    nodePep,
                                                                    nodeGroup,
                                                                    chromatogramSet,
                                                                    chromGroupInfoPrimary)).ToArray();
            }

            public PeptideDocNode NodePep { get; private set; }

            public ChromFileInfo FileInfo { get; private set; }

            public IList<ITransitionGroupPeakData<ISummaryPeakData>> TransitionGroupPeakData { get; private set; }

            private IEnumerable<ITransitionPeakData<ISummaryPeakData>> TransitionPeakData
            {
                get { return TransitionGroupPeakData.SelectMany(pd => pd.TranstionPeakData); }
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
                    foreach (var peakData in groupPeakData.TranstionPeakData)
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
                    foreach (var tranPeakData in TransitionGroupPeakData.SelectMany(g => g.TranstionPeakData))
                    {
                        if (maxPeakData == null || tranPeakData.PeakData.Height > maxPeakData.Height)
                            maxPeakData = tranPeakData.PeakData;
                    }
                    return maxPeakData != null ? maxPeakData.RetentionTime : 0;
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
                return 0;
            }
        }

        private sealed class SummaryTransitionGroupPeakData : ITransitionGroupPeakData<ISummaryPeakData>
        {
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
                        TranstionPeakData = nodeGroup.Transitions.Select(nodeTran =>
                            new SummaryTransitionPeakData(document, nodeTran, chromatogramSet,
                                _chromGroupInfo.GetTransitionInfo((float) nodeTran.Mz, mzMatchTolerance))).ToArray();
                    }
                }
            }

            public TransitionGroupDocNode NodeGroup { get; private set; }
            public bool IsStandard { get; private set; }
            public IList<ITransitionPeakData<ISummaryPeakData>> TranstionPeakData { get; private set; }

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
                    if (TranstionPeakData != null)
                    {
                        foreach (SummaryTransitionPeakData tranPeakData in TranstionPeakData)
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
                                     ChromatogramGroupInfo chromGroupInfo)
        {
            NodePepGroup = nodePepGroup;
            NodePep = nodePep;
            LabelType = labelType;
            ChromatogramSet = chromatogramSet;
            FilePath = chromGroupInfo.FilePath;
            var fileId = chromatogramSet.FindFile(chromGroupInfo);
            Run = fileId.GlobalIndex;
        }

        public PeptideGroupDocNode NodePepGroup { get; private set; }
        public PeptideDocNode NodePep { get; private set; }
        public IsotopeLabelType LabelType { get; private set; }
        public ChromatogramSet ChromatogramSet { get; private set; }
        public string FilePath { get; private set; }
        public int Run { get; private set; }

        public override string ToString()
        {
            var sb = new StringBuilder();
            if (NodePep.IsDecoy)
                sb.Append("DECOY_");
            sb.Append(NodePep.ModifiedSequence);
            if (!LabelType.IsLight)
                sb.Append("_").Append(LabelType);
            sb.Append("_run").Append(Run);
            return sb.ToString();
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
        public PeakGroupFeatures(float retentionTime, float[] features)
        {
            RetentionTime = retentionTime;
            Features = features;
        }

        public float RetentionTime { get; private set; }
        public float[] Features { get; private set; }
    }
}
