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
using System.Linq;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    [Flags]
    public enum PickLevel
    {
        peptides = 0x1,
        precursors = 0x2,
        transitions = 0x4,

        all = peptides | precursors | transitions
    }

    public static class DecoyGeneration
    {
        public static string ADD_RANDOM { get { return Resources.DecoyGeneration_ADD_RANDOM_Random_Mass_Shift; } }
        public static string SHUFFLE_SEQUENCE { get { return Resources.DecoyGeneration_SHUFFLE_SEQUENCE_Shuffle_Sequence; } }
        public static string REVERSE_SEQUENCE { get { return Resources.DecoyGeneration_REVERSE_SEQUENCE_Reverse_Sequence; } }

        public static IEnumerable<string> Methods
        {
            get { return new[] { SHUFFLE_SEQUENCE, REVERSE_SEQUENCE, ADD_RANDOM }; }
        }
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

        public struct PeptideCharge
        {
            public PeptideCharge(string sequence, int? charge) : this()
            {
                Sequence = sequence;
                Charge = charge;
            }

            public string Sequence { get; private set; }
            public int? Charge { get; private set; }
        }

        public enum ProteinSpecType {  name, accession, preferred }

        public IEnumerable<PeptideCharge> AcceptedPeptides { get; set; }
        public IEnumerable<string> AcceptedProteins { get; set; }
        public ProteinSpecType AcceptProteinType { get; set; }
        public bool AcceptModified { get; set; }
        public bool RemoveRepeatedPeptides { get; set; }
        public int? MinPrecursorsPerPeptide { get; set; }
        public int? MinTransitionsPepPrecursor { get; set; }
        public IsotopeLabelType RefineLabelType { get; set; }
        public bool AddLabelType { get; set; }
        public double? MinPeakFoundRatio { get; set; }
        public double? MaxPeakFoundRatio { get; set; }
        public double? MaxPepPeakRank { get; set; }
        public double? MaxPeakRank { get; set; }
        public bool PreferLargeIons { get; set; }
        public bool RemoveMissingResults { get; set; }
        public double? RTRegressionThreshold { get; set; }
        public int? RTRegressionPrecision { get; set; }
        public double? DotProductThreshold { get; set; }
        public double? IdotProductThreshold { get; set; }
        public bool UseBestResult { get; set; }
        public PickLevel AutoPickChildrenAll { get; set; }
        public bool AutoPickPeptidesAll { get { return (AutoPickChildrenAll & PickLevel.peptides) != 0; } }
        public bool AutoPickPrecursorsAll { get { return (AutoPickChildrenAll & PickLevel.precursors) != 0; } }
        public bool AutoPickTransitionsAll { get { return (AutoPickChildrenAll & PickLevel.transitions) != 0; } }
        public bool AutoPickChildrenOff { get; set; }
        public int NumberOfDecoys { get; set; }
        public string DecoysMethod { get; set; }

        public SrmDocument Refine(SrmDocument document)
        {
            return Refine(document, null);
        }

        public SrmDocument Refine(SrmDocument document, SrmSettingsChangeMonitor progressMonitor)
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

            HashSet<RefinementIdentity> includedPeptides = (RemoveRepeatedPeptides ? new HashSet<RefinementIdentity>() : null);
            HashSet<RefinementIdentity> repeatedPeptides = (RemoveDuplicatePeptides ? new HashSet<RefinementIdentity>() : null);
            Dictionary<RefinementIdentity, List<int>> acceptedPeptides = null;
            if (AcceptedPeptides != null)
            {
                acceptedPeptides = new Dictionary<RefinementIdentity, List<int>>();
                foreach (var peptideCharge in AcceptedPeptides)
                {
                    List<int> charges;
                    if (!acceptedPeptides.TryGetValue(new RefinementIdentity(peptideCharge.Sequence), out charges))
                    {
                        charges = (peptideCharge.Charge.HasValue ? new List<int> {peptideCharge.Charge.Value} : null);
                        acceptedPeptides.Add(new RefinementIdentity(peptideCharge.Sequence), charges);
                    }
                    else if (charges != null)
                    {
                        if (peptideCharge.Charge.HasValue)
                            charges.Add(peptideCharge.Charge.Value);
                        else
                            acceptedPeptides[new RefinementIdentity(peptideCharge.Sequence)] = null;
                    }
                }
            }
            HashSet<string> acceptedProteins = (AcceptedProteins != null ? new HashSet<string>(AcceptedProteins) : null);

            var listPepGroups = new List<PeptideGroupDocNode>();
            // Excluding proteins with too few peptides, since they can impact results
            // of the duplicate peptide check.
            int minPeptides = MinPeptidesPerProtein ?? 0;
            foreach (PeptideGroupDocNode nodePepGroup in document.Children)
            {
                if (progressMonitor != null)
                    progressMonitor.ProcessGroup(nodePepGroup);

                if (acceptedProteins != null && !acceptedProteins.Contains(GetAcceptProteinKey(nodePepGroup)))
                    continue;

                PeptideGroupDocNode nodePepGroupRefined = nodePepGroup;
                // If auto-managing all peptides, make sure this flag is set correctly,
                // and update the peptides list, if necessary.
                if (AutoPickPeptidesAll && nodePepGroup.AutoManageChildren == AutoPickChildrenOff)
                {
                    nodePepGroupRefined =
                        (PeptideGroupDocNode) nodePepGroupRefined.ChangeAutoManageChildren(!AutoPickChildrenOff);
                    var settings = document.Settings;
                    if (!AutoPickChildrenOff && !settings.PeptideSettings.Filter.AutoSelect)
                        settings = settings.ChangePeptideFilter(filter => filter.ChangeAutoSelect(true));
                    nodePepGroupRefined = nodePepGroupRefined.ChangeSettings(settings,
                        new SrmSettingsDiff(true, false, false, false, false, false));
                }

                nodePepGroupRefined = Refine(nodePepGroupRefined, document, outlierIds,
                        includedPeptides, repeatedPeptides, acceptedPeptides, progressMonitor);

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
                        var identity = nodePep.Peptide.IsCustomIon
                            ? new RefinementIdentity(nodePep.Peptide.CustomIon)
                            : new RefinementIdentity(document.Settings.GetModifiedSequence(nodePep));
                        if (!repeatedPeptides.Contains(identity))
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

        private string GetAcceptProteinKey(PeptideGroupDocNode nodePepGroup)
        {
            switch (AcceptProteinType)
            {
                    case ProteinSpecType.accession:
                        return nodePepGroup.ProteinMetadata.Accession;
                    case ProteinSpecType.preferred:
                        return nodePepGroup.ProteinMetadata.PreferredName;
            }
            return nodePepGroup.Name;
        }

        private PeptideGroupDocNode Refine(PeptideGroupDocNode nodePepGroup,
                                           SrmDocument document,
                                           ICollection<int> outlierIds,
                                           ICollection<RefinementIdentity> includedPeptides,
                                           ICollection<RefinementIdentity> repeatedPeptides,
                                           Dictionary<RefinementIdentity, List<int>> acceptedPeptides,
                                           SrmSettingsChangeMonitor progressMonitor)
        {
            var listPeptides = new List<PeptideDocNode>();
            int minPrecursors = MinPrecursorsPerPeptide ?? 0;
            foreach (PeptideDocNode nodePep in nodePepGroup.Children)
            {
                if (progressMonitor != null)
                    progressMonitor.ProcessMolecule(nodePep);

                if (outlierIds.Contains(nodePep.Id.GlobalIndex))
                    continue;

                // If there is a set of accepted peptides, and this is not one of them
                // then skip it.
                List<int> acceptedCharges = null;
                if (acceptedPeptides != null &&
                    !acceptedPeptides.TryGetValue(AcceptModified ? new RefinementIdentity(nodePep.RawTextId) : new RefinementIdentity(nodePep.RawUnmodifiedTextId), out acceptedCharges))
                {
                    continue;
                }

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
                if (AutoPickPrecursorsAll && nodePep.AutoManageChildren == AutoPickChildrenOff)
                {
                    nodePepRefined = (PeptideDocNode) nodePepRefined.ChangeAutoManageChildren(!AutoPickChildrenOff);
                    var settings = document.Settings;
                    if (!settings.TransitionSettings.Filter.AutoSelect && !AutoPickChildrenOff)
                        settings = settings.ChangeTransitionFilter(filter => filter.ChangeAutoSelect(!AutoPickChildrenOff));
                    nodePepRefined = nodePepRefined.ChangeSettings(settings,
                        new SrmSettingsDiff(false, false, true, false, AutoPickTransitionsAll, false));
                }

                nodePepRefined = Refine(nodePepRefined, document, bestResultIndex, acceptedCharges);
                // Always remove peptides if all precursors have been removed by refinement
                if (!ReferenceEquals(nodePep, nodePepRefined) && nodePepRefined.Children.Count == 0)
                    continue;
                if (nodePepRefined.Children.Count < minPrecursors)
                    continue;

                if (includedPeptides != null)
                {
                    var identity = nodePepRefined.Peptide.IsCustomIon
                        ? new RefinementIdentity(nodePep.Peptide.CustomIon)
                        : new RefinementIdentity(document.Settings.GetModifiedSequence(nodePepRefined)); 
                    // Skip peptides already added
                    if (includedPeptides.Contains(identity))
                    {
                        // Record repeated peptides for removing duplicate peptides later
                        if (repeatedPeptides != null)
                            repeatedPeptides.Add(identity);
                        continue;                        
                    }
                    // Record all peptides seen
                    includedPeptides.Add(identity);
                }

                listPeptides.Add(nodePepRefined);
            }

            if (MaxPepPeakRank.HasValue)
            {
                // Calculate the average peak area for each peptide
                int countPeps = listPeptides.Count;
                var listAreaIndexes = new List<PepAreaSortInfo>();
                var internalStandardTypes = document.Settings.PeptideSettings.Modifications.InternalStandardTypes;
                for (int i = 0; i < countPeps; i++)
                {
                    var nodePep = listPeptides[i];
                    // Only peptides with children can possible be ranked by area
                    // Those without should be removed by this operation
                    if (nodePep.Children.Count == 0)
                        continue;                    
                    int bestResultIndex = (UseBestResult ? nodePep.BestResult : -1);
                    var sortInfo = new PepAreaSortInfo(nodePep, internalStandardTypes, bestResultIndex, listAreaIndexes.Count);
                    listAreaIndexes.Add(sortInfo);
                }

                listAreaIndexes.Sort((p1, p2) => Comparer.Default.Compare(p2.Area, p1.Area));
                
                // Store area ranks
                var arrayAreaIndexes = new PepAreaSortInfo[listAreaIndexes.Count];
                int iRank = 1;
                foreach (var areaIndex in listAreaIndexes)
                {
                    areaIndex.Rank = iRank++;
                    arrayAreaIndexes[areaIndex.Index] = areaIndex;
                }

                // Add back all peptides with low enough rank.
                listPeptides.Clear();
                foreach (var areaIndex in arrayAreaIndexes)
                {
                    if (areaIndex.Area == 0 || areaIndex.Rank > MaxPepPeakRank.Value)
                        continue;
                    listPeptides.Add(areaIndex.Peptide);
                }
            }

            // Change the children, but only change auto-management, if the child
            // identities have changed, not if their contents changed.
            var childrenNew = listPeptides.ToArray();
            bool updateAutoManage = !PeptideGroupDocNode.AreEquivalentChildren(nodePepGroup.Children, childrenNew);
            return (PeptideGroupDocNode)nodePepGroup.ChangeChildrenChecked(childrenNew, updateAutoManage);
        }

        private PeptideDocNode Refine(PeptideDocNode nodePep,
                                      SrmDocument document,
                                      int bestResultIndex,
                                      List<int> acceptedCharges)
        {
            int minTrans = MinTransitionsPepPrecursor ?? 0;

            bool addedGroups = false;
            var listGroups = new List<TransitionGroupDocNode>();
            foreach (TransitionGroupDocNode nodeGroup in nodePep.Children)
            {
                if (acceptedCharges != null && !acceptedCharges.Contains(nodeGroup.TransitionGroup.PrecursorCharge))
                    continue;

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
                if (AutoPickTransitionsAll && nodeGroup.AutoManageChildren == AutoPickChildrenOff)
                {
                    nodeGroupRefined = (TransitionGroupDocNode) nodeGroupRefined.ChangeAutoManageChildren(!AutoPickChildrenOff);
                    var settings = document.Settings;
                    if (!settings.TransitionSettings.Filter.AutoSelect && !AutoPickChildrenOff)
                        settings = settings.ChangeTransitionFilter(filter => filter.ChangeAutoSelect(!AutoPickChildrenOff));
                    nodeGroupRefined = nodeGroupRefined.ChangeSettings(settings, nodePep, nodePep.ExplicitMods,
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
                    if (IdotProductThreshold.HasValue)
                    {
                        float? idotProduct = nodeGroupRefined.GetIsotopeDotProduct(bestResultIndex);
                        if (idotProduct.HasValue && idotProduct.Value < IdotProductThreshold.Value)
                            continue;
                    }
                }

                // If this precursor node is going to be added, check to see if it
                // should be added with another matching isotope label type.
                var explicitMods = nodePep.ExplicitMods;
                if (IsLabelTypeRequired(nodePep, nodeGroup, listGroups) &&
                        document.Settings.TryGetPrecursorCalc(RefineLabelType, explicitMods) != null)
                {
                    // CONSIDER: This is a lot like some code in PeptideDocNode.ChangeSettings
                    Debug.Assert(RefineLabelType != null);  // Keep ReSharper from warning
                    var tranGroup = new TransitionGroup(nodePep.Peptide,
                                                        nodeGroup.TransitionGroup.PrecursorCharge,
                                                        RefineLabelType,
                                                        false,
                                                        nodeGroup.TransitionGroup.DecoyMassShift);
                    var settings = document.Settings;
//                    string sequence = nodePep.Peptide.Sequence;
                    TransitionDocNode[] transitions = nodePep.GetMatchingTransitions(
                        tranGroup, settings, explicitMods);

                    var nodeGroupMatch = new TransitionGroupDocNode(tranGroup,
                                                                    Annotations.EMPTY,
                                                                    settings,
                                                                    explicitMods,
                                                                    nodeGroup.LibInfo,
                                                                    nodeGroup.ExplicitValues,
                                                                    null,   // results
                                                                    transitions,
                                                                    transitions == null);

                    nodeGroupMatch = nodeGroupMatch.ChangeSettings(settings, nodePep, explicitMods, SrmSettingsDiff.ALL);

                    // Make sure it is measurable before adding it
                    if (settings.TransitionSettings.IsMeasurablePrecursor(nodeGroupMatch.PrecursorMz))
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
                    var areaIndex = listAreaIndexes[i];
                    // Never keep a transition with no peak area
                    ranks[areaIndex.Index] = (areaIndex.Area > 0 ? iRank++ : int.MaxValue);
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

        public static string SmallMoleculeNameFromPeptide(string peptideSequence, int precursorCharge)
        {
            if (precursorCharge == 0)
            {
                return peptideSequence;
            }
            else
            {
                return string.Format("{0}({1}H{2})", // Not L10N
                    peptideSequence,
                    (precursorCharge < 0) ? "-" : "+", // Not L10N
                    Math.Abs(precursorCharge));

            }
        }

        public enum ConvertToSmallMoleculesMode
        {
            none,        // No conversion - call to ConvertToSmallMolecules is a no-op
            formulas,    // Convert peptides to custom ions with ion formulas
            masses_and_names,  // Convert peptides to custom ions but retain just the masses, and names for use in ratio calcs
            masses_only  // Convert peptides to custom ions but retain just the masses, no formulas or names so ratio calcs have to work on sorted mz
        };

        /// <summary>
        /// Removes any library info - useful in test since for the moment at least small molecules don't support this and it won't roundtrip
        /// </summary>
        public static Results<TransitionGroupChromInfo> RemoveTransitionGroupChromInfoLibraryInfo(TransitionGroupDocNode transitionGroupDocNode)
        {
            if (transitionGroupDocNode.Results == null)
                return null;

            var listResultsNew = new List<ChromInfoList<TransitionGroupChromInfo>>();
            foreach (var info in transitionGroupDocNode.Results)
            {
                if (info != null)
                {
                    var infoNew = new List<TransitionGroupChromInfo>();
                    foreach (var result in info)
                    {
                        infoNew.Add(result.ChangeLibraryDotProduct(null));
                    }
                    listResultsNew.Add(new ChromInfoList<TransitionGroupChromInfo>(infoNew));
                }
            }
            var resultsNew = new Results<TransitionGroupChromInfo>(listResultsNew);
            return resultsNew;
        }

        public static DocNodeCustomIon ConvertToSmallMolecule(ConvertToSmallMoleculesMode mode, 
            SrmDocument document, PeptideDocNode nodePep, int precursorCharge = 0, IsotopeLabelType isotopeLabelType = null)
        {
            // We're just using this masscalc to get the ion formula, so mono vs average doesn't matter
            isotopeLabelType = isotopeLabelType ?? IsotopeLabelType.light;
            var peptideSequence = nodePep.Peptide.Sequence;
            var masscalc = document.Settings.TryGetPrecursorCalc(isotopeLabelType, nodePep.ExplicitMods) ?? new SequenceMassCalc(MassType.Monoisotopic);
            // Determine the molecular formula of the charged/labeled peptide
            var moleculeFormula = masscalc.GetIonFormula(peptideSequence, precursorCharge);
            var moleculeCustomIon = new DocNodeCustomIon(moleculeFormula,
                SmallMoleculeNameFromPeptide(peptideSequence, precursorCharge));
            if (mode == ConvertToSmallMoleculesMode.masses_only)
            {
                // No formulas or names, just masses - see how we handle that
                moleculeCustomIon = new DocNodeCustomIon(moleculeCustomIon.MonoisotopicMass,
                    moleculeCustomIon.AverageMass);
            }
            else if (mode == ConvertToSmallMoleculesMode.masses_and_names)
            {
                // Just masses and names - see how we handle that
                moleculeCustomIon = new DocNodeCustomIon(moleculeCustomIon.MonoisotopicMass,
                    moleculeCustomIon.AverageMass, moleculeCustomIon.Name);
            }
            return moleculeCustomIon;
        }

        public const string TestingConvertedFromProteomic = "zzzTestingConvertedFromProteomic"; // Not L10N

        public SrmDocument ConvertToSmallMolecules(SrmDocument document, 
            ConvertToSmallMoleculesMode mode = ConvertToSmallMoleculesMode.formulas, 
            bool invertCharges = false, 
            bool ignoreDecoys=false)
        {
            if (mode == ConvertToSmallMoleculesMode.none)
                return document;
            var newdoc = new SrmDocument(document.Settings);
            var note = new Annotations(TestingConvertedFromProteomic, null, 1); // Mark this as a testing node so we don't sort it

            newdoc = (SrmDocument)newdoc.ChangeIgnoreChangingChildren(true); // Retain copied results

            foreach (var peptideGroupDocNode in document.MoleculeGroups)
            {
                if (!peptideGroupDocNode.IsProteomic)
                {
                    newdoc = (SrmDocument)newdoc.Add(peptideGroupDocNode); // Already a small molecule
                }
                else
                {
                    var newPeptideGroup = new PeptideGroup();
                    var newPeptideGroupDocNode = new PeptideGroupDocNode(newPeptideGroup,
                        peptideGroupDocNode.Annotations.Merge(note), peptideGroupDocNode.Name,
                        peptideGroupDocNode.Description, new PeptideDocNode[0],
                        peptideGroupDocNode.AutoManageChildren);
                    foreach (var mol in peptideGroupDocNode.Molecules)
                    {
                        var peptideSequence = mol.Peptide.Sequence;
                        // Create a PeptideDocNode with the presumably baseline charge and label
                        var precursorCharge = (mol.TransitionGroups.Any() ? mol.TransitionGroups.First().TransitionGroup.PrecursorCharge : 0) * (invertCharges ? -1 : 1);
                        var isotopeLabelType = mol.TransitionGroups.Any() ? mol.TransitionGroups.First().TransitionGroup.LabelType : IsotopeLabelType.light;
                        var moleculeCustomIon = ConvertToSmallMolecule(mode, document, mol, precursorCharge, isotopeLabelType);
                        var precursorCustomIon = moleculeCustomIon;
                        var newPeptide = new Peptide(moleculeCustomIon);
                        var newPeptideDocNode = new PeptideDocNode(newPeptide, newdoc.Settings, null, null,
                            null, null, mol.ExplicitRetentionTime, note, mol.Results, new TransitionGroupDocNode[0],
                            mol.AutoManageChildren);

                        foreach (var transitionGroupDocNode in mol.TransitionGroups)
                        {
                            if (transitionGroupDocNode.IsDecoy)
                            {
                                if (ignoreDecoys)
                                    continue;
                                throw new Exception("There is no translation from decoy to small molecules"); // Not L10N
                            }

                            if (transitionGroupDocNode.TransitionGroup.PrecursorCharge != Math.Abs(precursorCharge) ||
                                !Equals(isotopeLabelType, transitionGroupDocNode.TransitionGroup.LabelType))
                            {
                                // Different charges or labels mean different ion formulas
                                precursorCharge = transitionGroupDocNode.TransitionGroup.PrecursorCharge * (invertCharges ? -1 : 1);
                                isotopeLabelType = transitionGroupDocNode.TransitionGroup.LabelType;
                                precursorCustomIon = ConvertToSmallMolecule(mode, document, mol, precursorCharge, isotopeLabelType);
                            }

                            var newTransitionGroup = new TransitionGroup(newPeptide, precursorCustomIon, precursorCharge, isotopeLabelType);
                            // Remove any library info, since for the moment at least small molecules don't support this and it won't roundtrip
                            var resultsNew = RemoveTransitionGroupChromInfoLibraryInfo(transitionGroupDocNode);
                            var newTransitionGroupDocNode = new TransitionGroupDocNode(newTransitionGroup,
                                transitionGroupDocNode.Annotations.Merge(note), document.Settings,
                                null, null, transitionGroupDocNode.ExplicitValues, resultsNew, null,
                                transitionGroupDocNode.AutoManageChildren);
                            var mzShift = invertCharges ? 2.0 * BioMassCalc.MassProton : 0;  // We removed hydrogen rather than added
                            Assume.IsTrue((Math.Abs(newTransitionGroupDocNode.PrecursorMz + mzShift - transitionGroupDocNode.PrecursorMz) - Math.Abs(transitionGroupDocNode.TransitionGroup.PrecursorCharge * BioMassCalc.MassElectron)) <= 1E-5);

                            foreach (var transition in transitionGroupDocNode.Transitions)
                            {
                                double mass = 0;
                                var transitionCharge = transition.Transition.Charge * (invertCharges ? -1 : 1);
                                var ionType = IonType.custom;
                                CustomIon transitionCustomIon;
                                double mzShiftTransition = 0;
                                if (transition.Transition.IonType == IonType.precursor)
                                {
                                    ionType = IonType.precursor;
                                    transitionCustomIon = new DocNodeCustomIon(precursorCustomIon.Formula,
                                        string.IsNullOrEmpty(precursorCustomIon.Formula) ? precursorCustomIon.MonoisotopicMass : (double?) null,
                                        string.IsNullOrEmpty(precursorCustomIon.Formula) ? precursorCustomIon.AverageMass : (double?) null,
                                        SmallMoleculeNameFromPeptide(peptideSequence, transitionCharge));
                                    mzShiftTransition = invertCharges ? 2.0 * BioMassCalc.MassProton : 0;  // We removed hydrogen rather than added
                                }
                                else if (transition.Transition.IonType == IonType.custom)
                                {
                                    transitionCustomIon = transition.Transition.CustomIon;
                                    mass = transitionCustomIon.MonoisotopicMass;
                                }
                                else
                                {
                                    // TODO - try to get fragment formula?
                                    mass = BioMassCalc.CalculateIonMassFromMz(transition.Mz, transition.Transition.Charge);
                                    transitionCustomIon = new DocNodeCustomIon(mass, mass,// We can't really get at mono vs average mass from m/z, but for test purposes this is fine
                                        transition.Transition.FragmentIonName);
                                }
                                if (mode == ConvertToSmallMoleculesMode.masses_and_names)
                                {
                                    // Discard the formula if we're testing the use of mass-with-names (for matching in ratio calcs) target specification
                                    transitionCustomIon = new DocNodeCustomIon(transitionCustomIon.MonoisotopicMass, transitionCustomIon.AverageMass,
                                        transition.Transition.FragmentIonName);
                                }
                                else if (mode == ConvertToSmallMoleculesMode.masses_only)
                                {
                                    // Discard the formula and name if we're testing the use of mass-only target specification
                                    transitionCustomIon = new DocNodeCustomIon(transitionCustomIon.MonoisotopicMass, transitionCustomIon.AverageMass);
                                }

                                var newTransition = new Transition(newTransitionGroup, ionType,
                                    null, transition.Transition.MassIndex, transition.Transition.Charge * (invertCharges ? -1 : 1), null,
                                    transitionCustomIon);
                                if (ionType == IonType.precursor)
                                {
                                    mass = document.Settings.GetFragmentMass(transitionGroupDocNode.TransitionGroup.LabelType, null, newTransition, newTransitionGroupDocNode.IsotopeDist);
                                }
                                var newTransitionDocNode = new TransitionDocNode(newTransition, transition.Annotations.Merge(note),
                                    null, mass, transition.IsotopeDistInfo, null,
                                    transition.Results);
                                Assume.IsTrue((Math.Abs(newTransitionDocNode.Mz + mzShiftTransition - transition.Mz) - Math.Abs(transitionGroupDocNode.TransitionGroup.PrecursorCharge * BioMassCalc.MassElectron)) <= 1E-5, String.Format("unexpected mz difference {0}-{1}={2}", newTransitionDocNode.Mz , transition.Mz, newTransitionDocNode.Mz - transition.Mz)); // Not L10N
                                newTransitionGroupDocNode =
                                    (TransitionGroupDocNode)newTransitionGroupDocNode.Add(newTransitionDocNode);
                            }
                            if (newPeptideDocNode != null)
                                newPeptideDocNode = (PeptideDocNode)newPeptideDocNode.Add(newTransitionGroupDocNode);
                        }
                        newPeptideGroupDocNode =
                            (PeptideGroupDocNode)newPeptideGroupDocNode.Add(newPeptideDocNode);
                    }
                    newdoc = (SrmDocument)newdoc.Add(newPeptideGroupDocNode);
                }
            }

            // No retention time prediction for small molecules (yet?)
            newdoc = newdoc.ChangeSettings(newdoc.Settings.ChangePeptideSettings(newdoc.Settings.PeptideSettings.ChangePrediction(
                        newdoc.Settings.PeptideSettings.Prediction.ChangeRetentionTime(null))));


            return newdoc;
        }

        public SrmDocument ConvertToExplicitRetentionTimes(SrmDocument document, double timeOffset, double winOffset)
        {
            for (bool changing = true; changing;)
            {
                changing = false;
                foreach (var peptideGroupDocNode in document.MoleculeGroups)
                {
                    var pepGroupPath = new IdentityPath(IdentityPath.ROOT, peptideGroupDocNode.Id);
                    foreach (var nodePep in peptideGroupDocNode.Molecules)
                    {
                        var pepPath = new IdentityPath(pepGroupPath, nodePep.Id);
                        var rt = nodePep.AverageMeasuredRetentionTime;
                        if (rt.HasValue)
                        {
                            double? rtWin = document.Settings.PeptideSettings.Prediction.MeasuredRTWindow;
                            var explicitRetentionTimeInfo = new ExplicitRetentionTimeInfo(rt.Value+timeOffset, rtWin+winOffset);
                            if (!explicitRetentionTimeInfo.Equals(nodePep.ExplicitRetentionTime))
                            {
                                document = (SrmDocument)document.ReplaceChild(pepPath.Parent, nodePep.ChangeExplicitRetentionTime(explicitRetentionTimeInfo));
                                changing = true;
                                break;
                            }
                        }
                    }
                    if (changing)
                        break;
                }
            }
            return document;
        }

        public SrmDocument RemoveDecoys(SrmDocument document)
        {
            // Remove the existing decoys
            return (SrmDocument) document.RemoveAll(document.MoleculeGroups.Where(nodePeptideGroup => nodePeptideGroup.IsDecoy)
                                                        .Select(nodePeptideGroup => nodePeptideGroup.Id.GlobalIndex).ToArray()); 
        }


        public SrmDocument GenerateDecoys(SrmDocument document)
        {
            return GenerateDecoys(document, NumberOfDecoys, DecoysMethod);
        }

        public SrmDocument GenerateDecoys(SrmDocument document, int numDecoys, string decoysMethod)
        {
            // Remove the existing decoys
            document = RemoveDecoys(document);

            if (decoysMethod == DecoyGeneration.SHUFFLE_SEQUENCE)
                return GenerateDecoysFunc(document, numDecoys, true, GetShuffledPeptideSequence);
            
            if (decoysMethod == DecoyGeneration.REVERSE_SEQUENCE)
                return GenerateDecoysFunc(document, numDecoys, false, GetReversedPeptideSequence);

            return GenerateDecoysFunc(document, numDecoys, false, null);
        }

        private struct SequenceMods
        {
            public SequenceMods(PeptideDocNode nodePep) : this()
            {
                Peptide = nodePep.Peptide;
                Sequence = Peptide.Sequence;
                Mods = nodePep.ExplicitMods;
            }

            public Peptide Peptide { get; private set; }
            public string Sequence { get; set; }
            public ExplicitMods Mods { get; set; }
        }

        public static int SuggestDecoyCount(SrmDocument document)
        {
            int count = 0;
            foreach (var nodePep in document.Peptides)
            {
                // Exclude any existing decoys and standard peptides
                if (nodePep.IsDecoy || nodePep.GlobalStandardType != null)
                    continue;

                count += PeakFeatureEnumerator.ComparableGroups(nodePep).Count();
            }
            return count;
        }

        private static SrmDocument GenerateDecoysFunc(SrmDocument document, int numDecoys, bool multiCycle,
                                                      Func<SequenceMods, SequenceMods> genDecoySequence)
        {
            // Loop through the existing tree in random order creating decoys
            var settings = document.Settings;
            var enzyme = settings.PeptideSettings.Enzyme;

            var decoyNodePepList = new List<PeptideDocNode>();
            var setDecoyKeys = new HashSet<PeptideModKey>();
            while (numDecoys > 0)
            {
                int startDecoys = numDecoys;
                foreach (var nodePep in document.Peptides.ToArray().RandomOrder())
                {
                    if (numDecoys == 0)
                        break;

                    // Decoys should not be based on standard peptides
                    if (nodePep.GlobalStandardType != null)
                        continue;
                    // If the non-terminal end of the peptide sequence is all a single character, skip this peptide,
                    // since it can't support decoy generation.
                    var sequence = nodePep.Peptide.Sequence;
                    if (genDecoySequence != null && sequence.Substring(0, sequence.Length - 1).Distinct().Count() == 1)
                        continue;

                    var seqMods = new SequenceMods(nodePep);
                    if (genDecoySequence != null)
                    {
                        seqMods = genDecoySequence(seqMods);
                    }
                    var peptide = nodePep.Peptide;
                    var decoyPeptide = new Peptide(null, seqMods.Sequence, null, null, enzyme.CountCleavagePoints(seqMods.Sequence), true);
                    if (seqMods.Mods != null)
                        seqMods.Mods = seqMods.Mods.ChangePeptide(decoyPeptide);

                    foreach (var comparableGroups in PeakFeatureEnumerator.ComparableGroups(nodePep))
                    {
                        var decoyNodeTranGroupList = GetDecoyGroups(nodePep, decoyPeptide, seqMods.Mods, comparableGroups, document,
                                                                    Equals(seqMods.Sequence, peptide.Sequence));
                        if (decoyNodeTranGroupList.Count == 0)
                            continue;

                        var nodePepNew = new PeptideDocNode(decoyPeptide, settings, seqMods.Mods,
                            null, nodePep.ExplicitRetentionTime, decoyNodeTranGroupList.ToArray(), false);

                        if (!Equals(nodePep.ModifiedSequence, nodePepNew.ModifiedSequence))
                        {
                            var sourceKey = new ModifiedSequenceMods(nodePep.ModifiedSequence, nodePep.ExplicitMods);
                            nodePepNew = nodePepNew.ChangeSourceKey(sourceKey);
                        }

                        // Avoid adding duplicate peptides
                        if (setDecoyKeys.Contains(nodePepNew.Key))
                            continue;
                        setDecoyKeys.Add(nodePepNew.Key);

                        decoyNodePepList.Add(nodePepNew);
                        numDecoys--;
                    }
                }
                // Stop if not multi-cycle or the number of decoys has not changed.
                if (!multiCycle || startDecoys == numDecoys)
                    break;
            }
            var decoyNodePepGroup = new PeptideGroupDocNode(new PeptideGroup(true), Annotations.EMPTY, PeptideGroup.DECOYS,
                                                            null, decoyNodePepList.ToArray(), false);
            decoyNodePepGroup = decoyNodePepGroup.ChangeSettings(document.Settings, SrmSettingsDiff.ALL);

            return (SrmDocument)document.Add(decoyNodePepGroup);
        }

        private static List<TransitionGroupDocNode> GetDecoyGroups(PeptideDocNode nodePep, Peptide decoyPeptide,
            ExplicitMods mods, IEnumerable<TransitionGroupDocNode> comparableGroups, SrmDocument document, bool shiftMass)
        {
            var decoyNodeTranGroupList = new List<TransitionGroupDocNode>();

            var chargeToPrecursor = new Tuple<int, TransitionGroupDocNode>[TransitionGroup.MAX_PRECURSOR_CHARGE+1];
            foreach (TransitionGroupDocNode nodeGroup in comparableGroups)
            {
                var transGroup = nodeGroup.TransitionGroup;

                int precursorMassShift;
                TransitionGroupDocNode nodeGroupPrimary = null;

                var primaryPrecursor = chargeToPrecursor[nodeGroup.TransitionGroup.PrecursorCharge];
                if (primaryPrecursor != null)
                {
                    precursorMassShift = primaryPrecursor.Item1;
                    nodeGroupPrimary = primaryPrecursor.Item2;
                }
                else if (shiftMass)
                {
                    precursorMassShift = GetPrecursorMassShift();
                }
                else
                {
                    precursorMassShift = TransitionGroup.ALTERED_SEQUENCE_DECOY_MZ_SHIFT;
                }

                var decoyGroup = new TransitionGroup(decoyPeptide, transGroup.PrecursorCharge,
                                                        transGroup.LabelType, false, precursorMassShift);

                var decoyNodeTranList = nodeGroupPrimary != null
                    ? decoyGroup.GetMatchingTransitions(document.Settings, nodeGroupPrimary, mods)
                    : GetDecoyTransitions(nodeGroup, decoyGroup, shiftMass);

                var nodeGroupDecoy = new TransitionGroupDocNode(decoyGroup,
                                                                Annotations.EMPTY,
                                                                document.Settings,
                                                                mods,
                                                                nodeGroup.LibInfo,
                                                                nodeGroup.ExplicitValues,
                                                                nodeGroup.Results,
                                                                decoyNodeTranList,
                                                                false);
                decoyNodeTranGroupList.Add(nodeGroupDecoy);

                if (primaryPrecursor == null)
                {
                    chargeToPrecursor[transGroup.PrecursorCharge] =
                        new Tuple<int, TransitionGroupDocNode>(precursorMassShift, nodeGroupDecoy);
                }
            }

            return decoyNodeTranGroupList;
        }

        private static TransitionDocNode[] GetDecoyTransitions(TransitionGroupDocNode nodeGroup, TransitionGroup decoyGroup, bool shiftMass)
        {
            var decoyNodeTranList = new List<TransitionDocNode>();
            foreach (var nodeTran in nodeGroup.Transitions)
            {
                var transition = nodeTran.Transition;
                int productMassShift = 0;
                if (shiftMass)
                    productMassShift = GetProductMassShift();
                else if (transition.IsPrecursor() && decoyGroup.DecoyMassShift.HasValue)
                    productMassShift = decoyGroup.DecoyMassShift.Value;
                var decoyTransition = new Transition(decoyGroup, transition.IonType, transition.CleavageOffset,
                                                     transition.MassIndex, transition.Charge, productMassShift, transition.CustomIon);
                decoyNodeTranList.Add(new TransitionDocNode(decoyTransition, nodeTran.Losses, 0,
                                                            nodeTran.IsotopeDistInfo, nodeTran.LibInfo));
            }
            return decoyNodeTranList.ToArray();
        }

        static readonly Random RANDOM = new Random();

        private static int GetPrecursorMassShift()
        {
            // Do not allow zero for the mass shift of the precursor
            int massShift = RANDOM.Next(TransitionGroup.MIN_PRECURSOR_DECOY_MASS_SHIFT,
                                          TransitionGroup.MAX_PRECURSOR_DECOY_MASS_SHIFT);
            return massShift < 0 ? massShift : massShift + 1;
        }

        private static int GetProductMassShift()
        {
            int massShift = RANDOM.Next(Transition.MIN_PRODUCT_DECOY_MASS_SHIFT,
                                        Transition.MAX_PRODUCT_DECOY_MASS_SHIFT);
            // TODO: Validation code (at least 5 from the precursor)
            return massShift < 0 ? massShift : massShift + 1;
        }

        private static TypedExplicitModifications GetStaticTypedMods(Peptide peptide, IList<ExplicitMod> staticMods)
        {
            return staticMods != null
                ? new TypedExplicitModifications(peptide, IsotopeLabelType.light, staticMods)
                : null;
        }

        private static SequenceMods GetReversedPeptideSequence(SequenceMods seqMods)
        {
            string sequence = seqMods.Sequence;
            char finalA = sequence.Last();
            sequence = sequence.Substring(0, sequence.Length - 1);
            int lenSeq = sequence.Length;

            char[] reversedArray = sequence.ToCharArray();
            Array.Reverse(reversedArray);
            seqMods.Sequence = new string(reversedArray) + finalA;
            if (seqMods.Mods != null)
            {
                var reversedStaticMods = GetReversedMods(seqMods.Mods.StaticModifications, lenSeq);
                var typedStaticMods = GetStaticTypedMods(seqMods.Peptide, reversedStaticMods);
                seqMods.Mods = new ExplicitMods(seqMods.Peptide,
                    reversedStaticMods,
                    GetReversedHeavyMods(seqMods, typedStaticMods, lenSeq),
                    seqMods.Mods.IsVariableStaticMods);
            }

            return seqMods;
        }

        private static IList<ExplicitMod> GetReversedMods(IEnumerable<ExplicitMod> mods, int lenSeq)
        {
            return GetRearrangedMods(mods, lenSeq, i => lenSeq - i - 1);
        }

        private static IEnumerable<TypedExplicitModifications> GetReversedHeavyMods(SequenceMods seqMods,
            TypedExplicitModifications typedStaticMods, int lenSeq)
        {
            var reversedHeavyMods = seqMods.Mods.GetHeavyModifications().Select(typedMod =>
                new TypedExplicitModifications(seqMods.Peptide, typedMod.LabelType,
                                               GetReversedMods(typedMod.Modifications, lenSeq)));
            foreach (var typedMods in reversedHeavyMods)
            {
                yield return typedMods.AddModMasses(typedStaticMods);
            }
        }

        private static SequenceMods GetShuffledPeptideSequence(SequenceMods seqMods)
        {
            string sequence = seqMods.Sequence;
            char finalA = sequence.Last();
            string sequencePrefix = sequence.Substring(0, sequence.Length - 1);
            int lenPrefix = sequencePrefix.Length;

            // Calculate a random shuffling of the current positions
            int[] newIndices = new int[lenPrefix];
            do
            {
                for (int i = 0; i < lenPrefix; i++)
                    newIndices[i] = i;
                for (int i = 0; i < lenPrefix; i++)
                    Helpers.Swap(ref newIndices[RANDOM.Next(newIndices.Length)], ref newIndices[RANDOM.Next(newIndices.Length)]);

                // Move the amino acids to their new positions
                char[] shuffledArray = new char[lenPrefix];
                for (int i = 0; i < lenPrefix; i++)
                    shuffledArray[newIndices[i]] = sequencePrefix[i];

                seqMods.Sequence = new string(shuffledArray) + finalA;
            }
            // Make sure random shuffling did not just result in the same sequence
            while (seqMods.Sequence.Equals(sequence));

            if (seqMods.Mods != null)
            {
                var shuffledStaticMods = GetShuffledMods(seqMods.Mods.StaticModifications, lenPrefix, newIndices);
                var typedStaticMods = GetStaticTypedMods(seqMods.Peptide, shuffledStaticMods);
                seqMods.Mods = new ExplicitMods(seqMods.Peptide,
                    shuffledStaticMods,
                    GetShuffledHeavyMods(seqMods, typedStaticMods, lenPrefix, newIndices),
                    seqMods.Mods.IsVariableStaticMods);
            }

            return seqMods;
        }

        private static IList<ExplicitMod> GetShuffledMods(IEnumerable<ExplicitMod> mods, int lenSeq, int[] newIndices)
        {
            return GetRearrangedMods(mods, lenSeq, i => newIndices[i]);
        }

        private static IEnumerable<TypedExplicitModifications> GetShuffledHeavyMods(SequenceMods seqMods,
            TypedExplicitModifications typedStaticMods, int lenSeq, int[] newIndices)
        {
            var shuffledHeavyMods = seqMods.Mods.GetHeavyModifications().Select(typedMod =>
                new TypedExplicitModifications(seqMods.Peptide, typedMod.LabelType,
                                               GetShuffledMods(typedMod.Modifications, lenSeq, newIndices)));
            foreach (var typedMods in shuffledHeavyMods)
            {
                yield return typedMods.AddModMasses(typedStaticMods);
            }
        }

        private static IList<ExplicitMod> GetRearrangedMods(IEnumerable<ExplicitMod> mods, int lenSeq,
                                                            Func<int, int> getNewIndex)
        {
            if (null == mods)
            {
                return null;
            }
            var arrayMods = mods.ToArray();
            for (int i = 0; i < arrayMods.Length; i++)
            {
                var mod = arrayMods[i];
                if (mod.IndexAA < lenSeq)
                    arrayMods[i] = new ExplicitMod(getNewIndex(mod.IndexAA), mod.Modification);
            }
            Array.Sort(arrayMods, (mod1, mod2) => Comparer.Default.Compare(mod1.IndexAA, mod2.IndexAA));
            return arrayMods;
        }

        private sealed class PepAreaSortInfo
        {
            private readonly PeptideDocNode _nodePep;
            private readonly int _bestCharge;

            public PepAreaSortInfo(PeptideDocNode nodePep,
                                   ICollection<IsotopeLabelType> internalStandardTypes,
                                   int bestResultIndex,
                                   int index)
            {
                _nodePep = nodePep;

                // Get transition group areas by charge state
                var chargeGroups =
                    from nodeGroup in nodePep.TransitionGroups
                    where !internalStandardTypes.Contains(nodeGroup.TransitionGroup.LabelType)
                    group nodeGroup by nodeGroup.TransitionGroup.PrecursorCharge into g
                    select new {Charge = g.Key, Area = g.Sum(ng => ng.GetPeakArea(bestResultIndex))};

                // Store the best charge state and its area
                var bestChargeGroup = chargeGroups.OrderBy(cg => cg.Area).First();
                _bestCharge = bestChargeGroup.Charge;
                Area = bestChargeGroup.Area ?? 0;

                Index = index;
            }

            public float Area { get; private set; }
            public int Index { get; private set; }
            public int Rank { get; set; }

            public PeptideDocNode Peptide
            {
                get
                {
                    return (PeptideDocNode) _nodePep.ChangeChildrenChecked(_nodePep.TransitionGroups.Where(
                        nodeGroup => nodeGroup.TransitionGroup.PrecursorCharge == _bestCharge).ToArray());
                }
            }
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

        private sealed class RefinementIdentity
        {
            public RefinementIdentity(CustomIon customIon)
            {
                CustomIon = customIon;
            }

            public RefinementIdentity(string sequence)
            {
                Sequence = sequence;
            }

            private CustomIon CustomIon { get; set; }
            private string Sequence { get; set; }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (!(obj is RefinementIdentity)) return false;
                return Equals((RefinementIdentity) obj);
            }

            public override int GetHashCode()
            {
                int result = Sequence != null ? Sequence.GetHashCode() : 0;
                result = (result*397) ^ (CustomIon != null ? CustomIon.GetHashCode() : 0);
                return result;
            }

            private bool Equals(RefinementIdentity identity)
            {
                return Equals(identity.Sequence, Sequence) &&
                       Equals(identity.CustomIon, CustomIon);
            }
        }
    }
}