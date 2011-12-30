/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;

namespace pwiz.Skyline.Model
{
    [Flags]
    public enum PickLevel
    {
        peptides = 0x1,
        precursors = 0x2,
        transitions = 0x4
    }

    public sealed class RefinementSettings
    {
        private bool _removeDuplicatePeptides;

        public int? MinPeptidesPerProtein { get; set; }
        public bool RemoveDuplicatePeptides
        {
            get { return _removeDuplicatePeptides; }
            set
            {
                _removeDuplicatePeptides = value;
                // Removing duplicate peptides implies removing
                // repeated peptids.
                if (_removeDuplicatePeptides)
                    RemoveRepeatedPeptides = true;
            }
        }

        public IEnumerable<string> AcceptedPeptides { get; set; }
        public bool RemoveRepeatedPeptides { get; set; }
        public int? MinTransitionsPepPrecursor { get; set; }
        public IsotopeLabelType RefineLabelType { get; set; }
        public bool AddLabelType { get; set; }
        public double? MinPeakFoundRatio { get; set; }
        public double? MaxPeakFoundRatio { get; set; }
        public double? MaxPeakRank { get; set; }
        public bool PreferLargeIons { get; set; }
        public bool RemoveMissingResults { get; set; }
        public double? RTRegressionThreshold { get; set; }
        public int? RTRegressionPrecision { get; set; }
        public double? DotProductThreshold { get; set; }
        public bool UseBestResult { get; set; }
        public PickLevel AutoPickChildrenAll { get; set; }
        public bool AutoPickPeptidesAll { get { return (AutoPickChildrenAll & PickLevel.peptides) != 0; } }
        public bool AutoPickPrecursorsAll { get { return (AutoPickChildrenAll & PickLevel.precursors) != 0; } }
        public bool AutoPickTransitionsAll { get { return (AutoPickChildrenAll & PickLevel.transitions) != 0; } }

        public SrmDocument Refine(SrmDocument document)
        {
            HashSet<int> outlierIds = new HashSet<int>();
            if (RTRegressionThreshold.HasValue)
            {
                // TODO: Move necessary code into Model.
                var outliers = RTLinearRegressionGraphPane.CalcOutliers(document,
                    RTRegressionThreshold.Value, RTRegressionPrecision, UseBestResult);

                foreach (var nodePep in outliers)
                    outlierIds.Add(nodePep.Id.GlobalIndex);
            }

            HashSet<string> includedPeptides = (RemoveRepeatedPeptides ? new HashSet<string>() : null);
            HashSet<string> repeatedPeptides = (RemoveDuplicatePeptides ? new HashSet<string>() : null);
            HashSet<string> acceptedPeptides = (AcceptedPeptides != null ?
                new HashSet<string>(AcceptedPeptides) : null);

            var listPepGroups = new List<PeptideGroupDocNode>();
            // Excluding proteins with too few peptides, since they can impact results
            // of the duplicate peptide check.
            int minPeptides = MinPeptidesPerProtein ?? 0;
            foreach (PeptideGroupDocNode nodePepGroup in document.Children)
            {
                PeptideGroupDocNode nodePepGroupRefined = nodePepGroup;
                // If auto-managing all peptides, make sure this flag is set correctly,
                // and update the peptides list, if necessary.
                if (AutoPickPeptidesAll && !nodePepGroup.AutoManageChildren)
                {
                    nodePepGroupRefined = (PeptideGroupDocNode) nodePepGroupRefined.ChangeAutoManageChildren(true);
                    var settings = document.Settings;
                    if (!settings.PeptideSettings.Filter.AutoSelect)
                        settings = settings.ChangePeptideFilter(filter => filter.ChangeAutoSelect(true));
                    nodePepGroupRefined = nodePepGroupRefined.ChangeSettings(settings,
                        new SrmSettingsDiff(true, false, false, false, false, false));
                }

                nodePepGroupRefined =
                    Refine(nodePepGroupRefined, document, outlierIds,
                        includedPeptides, repeatedPeptides, acceptedPeptides);

                if (nodePepGroupRefined.Children.Count < minPeptides)
                    continue;

                listPepGroups.Add(nodePepGroupRefined);
            }

            // Need a second pass, if all duplicate peptides should be removed,
            // and duplicates were found.
            if (repeatedPeptides != null && repeatedPeptides.Count > 0)
            {
                var listPepGroupsFiltered = new List<PeptideGroupDocNode>();
                foreach (PeptideGroupDocNode nodePepGroup in listPepGroups)
                {
                    var listPeptides = new List<PeptideDocNode>();
                    foreach (PeptideDocNode nodePep in nodePepGroup.Children)
                    {
                        string pepModSeq = document.Settings.GetModifiedSequence(nodePep.Peptide.Sequence,
                            IsotopeLabelType.light, nodePep.ExplicitMods);
                        if (!repeatedPeptides.Contains(pepModSeq))
                            listPeptides.Add(nodePep);
                    }

                    PeptideGroupDocNode nodePepGroupRefined = (PeptideGroupDocNode)
                        nodePepGroup.ChangeChildrenChecked(listPeptides.ToArray(), true);

                    if (nodePepGroupRefined.Children.Count < minPeptides)
                        continue;

                    listPepGroupsFiltered.Add(nodePepGroupRefined);
                }

                listPepGroups = listPepGroupsFiltered;                
            }

            return (SrmDocument) document.ChangeChildrenChecked(listPepGroups.ToArray(), true);
        }

        private PeptideGroupDocNode Refine(PeptideGroupDocNode nodePepGroup,
                                           SrmDocument document,
                                           ICollection<int> outlierIds,
                                           ICollection<string> includedPeptides,
                                           ICollection<string> repeatedPeptides,
                                           ICollection<string> acceptedPeptides)
        {
            var listPeptides = new List<PeptideDocNode>();
            foreach (PeptideDocNode nodePep in nodePepGroup.Children)
            {
                if (outlierIds.Contains(nodePep.Id.GlobalIndex))
                    continue;

                // If there is a set of accepted peptides, and this is not one of them
                // then skip it.
                if (acceptedPeptides != null && !acceptedPeptides.Contains(nodePep.Peptide.Sequence))
                    continue;

                int bestResultIndex = (UseBestResult ? nodePep.BestResult : -1);
                float? peakFoundRatio = nodePep.GetPeakCountRatio(bestResultIndex);
                if (!peakFoundRatio.HasValue)
                {
                    if (RemoveMissingResults)
                        continue;
                }
                else
                {
                    if (MinPeakFoundRatio.HasValue)
                    {
                        if (peakFoundRatio < MinPeakFoundRatio.Value)
                            continue;
                    }
                    if (MaxPeakFoundRatio.HasValue)
                    {
                        if (peakFoundRatio > MaxPeakFoundRatio.Value)
                            continue;
                    }
                }

                PeptideDocNode nodePepRefined = nodePep;
                if (AutoPickPrecursorsAll && !nodePep.AutoManageChildren)
                {
                    nodePepRefined = (PeptideDocNode) nodePepRefined.ChangeAutoManageChildren(true);
                    var settings = document.Settings;
                    if (!settings.TransitionSettings.Filter.AutoSelect)
                        settings = settings.ChangeTransitionFilter(filter => filter.ChangeAutoSelect(true));
                    nodePepRefined = nodePepRefined.ChangeSettings(settings,
                        new SrmSettingsDiff(false, false, true, false, false, false));
                }

                nodePepRefined = Refine(nodePepRefined, document, bestResultIndex);
                // Always remove peptides if all precursors have been removed by refinement
                if (!ReferenceEquals(nodePep, nodePepRefined) && nodePepRefined.Children.Count == 0)
                    continue;

                if (includedPeptides != null)
                {
                    string pepModSeq = document.Settings.GetModifiedSequence(nodePepRefined.Peptide.Sequence,
                        IsotopeLabelType.light, nodePepRefined.ExplicitMods);
                    // Skip peptides already added
                    if (includedPeptides.Contains(pepModSeq))
                    {
                        // Record repeated peptides for removing duplicate peptides later
                        if (repeatedPeptides != null)
                            repeatedPeptides.Add(pepModSeq);
                        continue;                        
                    }
                    // Record all peptides seen
                    includedPeptides.Add(pepModSeq);
                }

                listPeptides.Add(nodePepRefined);
            }

            // Change the children, but only change auto-management, if the child
            // identities have changed, not if their contents changed.
            var childrenNew = listPeptides.ToArray();
            bool updateAutoManage = !PeptideGroupDocNode.AreEquivalentChildren(nodePepGroup.Children, childrenNew);
            return (PeptideGroupDocNode)nodePepGroup.ChangeChildrenChecked(childrenNew, updateAutoManage);
        }

// ReSharper disable SuggestBaseTypeForParameter
        private PeptideDocNode Refine(PeptideDocNode nodePep, SrmDocument document, int bestResultIndex)
// ReSharper restore SuggestBaseTypeForParameter
        {
            int minTrans = MinTransitionsPepPrecursor ?? 0;

            bool addedGroups = false;
            var listGroups = new List<TransitionGroupDocNode>();
            foreach (TransitionGroupDocNode nodeGroup in nodePep.Children)
            {
                if (!AddLabelType && RefineLabelType != null && Equals(RefineLabelType, nodeGroup.TransitionGroup.LabelType))
                    continue;

                double? peakFoundRatio = nodeGroup.GetPeakCountRatio(bestResultIndex);
                if (!peakFoundRatio.HasValue)
                {
                    if (RemoveMissingResults)
                        continue;
                }
                else
                {
                    if (MinPeakFoundRatio.HasValue)
                    {
                        if (peakFoundRatio < MinPeakFoundRatio.Value)
                            continue;
                    }
                    if (MaxPeakFoundRatio.HasValue)
                    {
                        if (peakFoundRatio > MaxPeakFoundRatio.Value)
                            continue;
                    }
                }

                TransitionGroupDocNode nodeGroupRefined = nodeGroup;
                if (AutoPickTransitionsAll && !nodeGroup.AutoManageChildren)
                {
                    nodeGroupRefined = (TransitionGroupDocNode) nodeGroupRefined.ChangeAutoManageChildren(true);
                    var settings = document.Settings;
                    if (!settings.TransitionSettings.Filter.AutoSelect)
                        settings = settings.ChangeTransitionFilter(filter => filter.ChangeAutoSelect(true));
                    nodeGroupRefined = nodeGroupRefined.ChangeSettings(settings, nodePep.ExplicitMods,
                        new SrmSettingsDiff(false, false, false, false, true, false));
                }
                nodeGroupRefined = Refine(nodeGroupRefined, bestResultIndex);
                if (nodeGroupRefined.Children.Count < minTrans)
                    continue;

                if (peakFoundRatio.HasValue)
                {
                    if (DotProductThreshold.HasValue)
                    {
                        float? dotProduct = nodeGroupRefined.GetLibraryDotProduct(bestResultIndex);
                        if (dotProduct.HasValue && dotProduct.Value < DotProductThreshold.Value)
                            continue;
                    }
                }

                // If this precursor node is going to be added, check to see if it
                // should be added with another matching isotope label type.
                var explicitMods = nodePep.ExplicitMods;
                if (IsLabelTypeRequired(nodePep, nodeGroup, listGroups) &&
                        document.Settings.GetPrecursorCalc(RefineLabelType, explicitMods) != null)
                {
                    // CONSIDER: This is a lot like some code in PeptideDocNode.ChangeSettings
                    Debug.Assert(RefineLabelType != null);  // Keep ReSharper from warning
                    var tranGroup = new TransitionGroup(nodePep.Peptide, nodeGroup.TransitionGroup.PrecursorCharge,
                                                        RefineLabelType);
                    var settings = document.Settings;
//                    string sequence = nodePep.Peptide.Sequence;
                    TransitionDocNode[] transitions = nodePep.GetMatchingTransitions(
                        tranGroup, settings, explicitMods);

                    var nodeGroupMatch = new TransitionGroupDocNode(tranGroup, transitions);

                    nodeGroupMatch = nodeGroupMatch.ChangeSettings(settings, explicitMods, SrmSettingsDiff.ALL);

                    // Make sure it is measurable before adding it
                    if (settings.TransitionSettings.Instrument.IsMeasurable(nodeGroupMatch.PrecursorMz))
                    {
                        listGroups.Add(nodeGroupMatch);
                        addedGroups = true;
                    }
                }

                listGroups.Add(nodeGroupRefined);
            }

            // If groups were added, make sure everything is in the right order.
            if (addedGroups)
                listGroups.Sort(Peptide.CompareGroups);

            // Change the children, but only change auto-management, if the child
            // identities have changed, not if their contents changed.
            var childrenNew = listGroups.ToArray();
            bool updateAutoManage = !PeptideDocNode.AreEquivalentChildren(nodePep.Children, childrenNew);
            return (PeptideDocNode) nodePep.ChangeChildrenChecked(childrenNew, updateAutoManage);
        }

// ReSharper disable SuggestBaseTypeForParameter
        private bool IsLabelTypeRequired(PeptideDocNode nodePep, TransitionGroupDocNode nodeGroup,
            IEnumerable<TransitionGroupDocNode> listGroups)
// ReSharper restore SuggestBaseTypeForParameter
        {
            // If not adding a label type, or this precursor is already the label type being added,
            // then no further work is required
            if (!AddLabelType || RefineLabelType == null || Equals(RefineLabelType, nodeGroup.TransitionGroup.LabelType))
                return false;

            // If either the peptide or the list of new groups already contains the
            // label type to be added, then do not add
            foreach (TransitionGroupDocNode nodeGroupChild in nodePep.Children)
            {
                if (nodeGroupChild.TransitionGroup.PrecursorCharge == nodeGroup.TransitionGroup.PrecursorCharge &&
                        Equals(RefineLabelType, nodeGroupChild.TransitionGroup.LabelType))
                    return false;
            }
            foreach (TransitionGroupDocNode nodeGroupAdded in listGroups)
            {
                if (nodeGroupAdded.TransitionGroup.PrecursorCharge == nodeGroup.TransitionGroup.PrecursorCharge &&
                        Equals(RefineLabelType, nodeGroupAdded.TransitionGroup.LabelType))
                    return false;
            }
            return true;
        }

// ReSharper disable SuggestBaseTypeForParameter
        private TransitionGroupDocNode Refine(TransitionGroupDocNode nodeGroup, int bestResultIndex)
// ReSharper restore SuggestBaseTypeForParameter
        {
            var listTrans = new List<TransitionDocNode>();
            foreach (TransitionDocNode nodeTran in nodeGroup.Children)
            {
                double? peakFoundRatio = nodeTran.GetPeakCountRatio(bestResultIndex);
                if (!peakFoundRatio.HasValue)
                {
                    if (RemoveMissingResults)
                        continue;
                }
                else
                {
                    if (MinPeakFoundRatio.HasValue)
                    {
                        if (peakFoundRatio < MinPeakFoundRatio.Value)
                            continue;
                    }
                    if (MaxPeakFoundRatio.HasValue)
                    {
                        if (peakFoundRatio > MaxPeakFoundRatio.Value)
                            continue;
                    }
                }

                listTrans.Add(nodeTran);
            }

            TransitionGroupDocNode nodeGroupRefined = (TransitionGroupDocNode)
                nodeGroup.ChangeChildrenChecked(listTrans.ToArray(), true);

            if (MaxPeakRank.HasValue)
            {
                // Calculate the average peak area for each transition
                int countTrans = nodeGroupRefined.Children.Count;
                var listAreaIndexes = new List<AreaSortInfo>();
                for (int i = 0; i < countTrans; i++)
                {
                    var nodeTran = (TransitionDocNode) nodeGroupRefined.Children[i];
                    var sortInfo = new AreaSortInfo(nodeTran.GetPeakArea(bestResultIndex) ?? 0,
                                                    nodeTran.Transition.Ordinal,
                                                    nodeTran.Mz > nodeGroup.PrecursorMz,
                                                    i);
                    listAreaIndexes.Add(sortInfo);                    
                }
                // Sort to area order descending
                if (PreferLargeIons)
                {
                    // If prefering large ions, then larger ions get a slight area
                    // advantage over smaller ones
                    listAreaIndexes.Sort((p1, p2) =>
                    {
                        float areaAdjusted1 = p1.Area;
                        // If either transition is below the precursor m/z value,
                        // apply the fragment size correction.
                        if (!p1.AbovePrecusorMz || !p2.AbovePrecusorMz)
                        {
                            int deltaOrdinal = Math.Max(-5, Math.Min(5, p1.Ordinal - p2.Ordinal));
                            if (deltaOrdinal != 0)
                                deltaOrdinal += (deltaOrdinal > 0 ? 1 : -1);
                            areaAdjusted1 += areaAdjusted1 * 0.05f * deltaOrdinal;
                        }
                        return Comparer.Default.Compare(p2.Area, areaAdjusted1);
                    });
                }
                else
                {
                    listAreaIndexes.Sort((p1, p2) => Comparer.Default.Compare(p2.Area, p1.Area));                    
                }                 
                // Store area ranks by transition index
                var ranks = new int[countTrans];
                for (int i = 0, iRank = 1; i < countTrans; i++)
                {
                    ranks[listAreaIndexes[i].Index] = iRank++;
                }

                // Add back all transitions with low enough rank.
                listTrans.Clear();
                for (int i = 0; i < countTrans; i++)
                {
                    if (ranks[i] > MaxPeakRank.Value)
                        continue;
                    listTrans.Add((TransitionDocNode) nodeGroupRefined.Children[i]);
                }

                nodeGroupRefined = (TransitionGroupDocNode)
                    nodeGroupRefined.ChangeChildrenChecked(listTrans.ToArray(), true);
            }

            return nodeGroupRefined;
        }

        private sealed class AreaSortInfo
        {
            public AreaSortInfo(float area, int ordinal, bool abovePrecursorMz, int index)
            {
                Area = area;
                Ordinal = ordinal;
                AbovePrecusorMz = abovePrecursorMz;
                Index = index;
            }

            public float Area { get; private set; }
            public int Ordinal { get; private set; }
            public bool AbovePrecusorMz { get; private set; }
            public int Index { get; private set; }
        }
    }
}