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
using System.IO;
using System.Linq;
using System.Threading;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.IonMobility;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Lib.BlibData;
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
        public static string ADD_RANDOM { get { return ModelResources.DecoyGeneration_ADD_RANDOM_Random_Mass_Shift; } }
        public static string SHUFFLE_SEQUENCE { get { return Resources.DecoyGeneration_SHUFFLE_SEQUENCE_Shuffle_Sequence; } }
        public static string REVERSE_SEQUENCE { get { return ModelResources.DecoyGeneration_REVERSE_SEQUENCE_Reverse_Sequence; } }

        public static IEnumerable<string> Methods
        {
            get { return new[] { SHUFFLE_SEQUENCE, REVERSE_SEQUENCE, ADD_RANDOM }; }
        }
    }

    public enum AreaCVTransitions { all, best, count }

    public enum AreaCVMsLevel { precursors, products }

    public sealed class RefinementSettings : AuditLogOperationSettings<RefinementSettings>, IAuditLogComparable
    {
        private bool _removeDuplicatePeptides;

        public RefinementSettings()
        {
            NormalizationMethod = NormalizeOption.NONE;
            MSLevel = AreaCVMsLevel.products;
            GroupComparisonNames = new List<string>();
            GroupComparisonDefs = new List<GroupComparisonDef>();
        }

        public override MessageInfo MessageInfo
        {
            get { return new MessageInfo(MessageType.refined_targets, SrmDocument.DOCUMENT_TYPE.none); }
        }

        public struct PeptideCharge
        {
            public PeptideCharge(Target sequence, Adduct charge) : this()
            {
                Sequence = sequence;
                Charge = charge;
            }

            public Target Sequence { get; private set; }
            public Adduct Charge { get; private set; }
        }

        public enum ProteinSpecType {  name, accession, preferred }

        public object GetDefaultObject(ObjectInfo<object> info)
        {
            return new RefinementSettings();

        }

        // Document
        [Track]
        public int? MinPeptidesPerProtein { get; set; }
        [Track]
        public bool RemoveRepeatedPeptides { get; set; }
        [Track]
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
        [Track]
        public bool RemoveMissingLibrary { get; set; }
        [Track]
        public int? MinTransitionsPepPrecursor { get; set; }

        private class RefineLabelTypeLocalizer : CustomPropertyLocalizer
        {
            private static readonly string ADD_LABEL_TYPE = @"AddRefineLabelType";
            private static readonly string REMOVE_LABEL_TYPE = @"RefineLabelType";

            public RefineLabelTypeLocalizer() : base(PropertyPath.Parse(@"AddLabelType"), true)
            {
            }

            private string LocalizeInternal(object obj)
            {
                if (obj == null || obj.GetType() != typeof(bool))
                    return null;

                return (bool) obj ? ADD_LABEL_TYPE : REMOVE_LABEL_TYPE;
            }

            protected override string Localize(ObjectPair<object> objectPair)
            {
                return LocalizeInternal(objectPair.NewObject) ?? LocalizeInternal(objectPair.OldObject);
            }

            public override string[] PossibleResourceNames => new[] {ADD_LABEL_TYPE, REMOVE_LABEL_TYPE};
        }

        [Track(customLocalizer:typeof(RefineLabelTypeLocalizer))]
        public IsotopeLabelType RefineLabelType { get; set; }
        public bool AddLabelType { get; set; }
        public PickLevel AutoPickChildrenAll { get; set; }
        [Track]
        public bool AutoPickPeptidesAll { get { return (AutoPickChildrenAll & PickLevel.peptides) != 0; } }
        [Track]
        public bool AutoPickPrecursorsAll { get { return (AutoPickChildrenAll & PickLevel.precursors) != 0; } }
        [Track]
        public bool AutoPickTransitionsAll { get { return (AutoPickChildrenAll & PickLevel.transitions) != 0; } }
        // Results
        [Track]
        public double? MinPeakFoundRatio { get; set; }
        [Track]
        public double? MaxPeakFoundRatio { get; set; }
        [Track]
        public double? MaxPepPeakRank { get; set; }
        [Track]
        public bool MaxPrecursorPeakOnly { get; set; }
        [Track]
        public double? MaxPeakRank { get; set; }


        public IEnumerable<LibraryKey> AcceptedPeptides { get; set; }
        public IEnumerable<string> AcceptedProteins { get; set; }
        public ProteinSpecType AcceptProteinType { get; set; }
        public bool AcceptModified { get; set; }
        
        // Some properties, including this one are not tracked,
        // since they are not used by the Edit > Refine > Advanced dialog.
        // These properties create their own log messages
        public int? MinPrecursorsPerPeptide { get; set; }

        [Track]
        public bool PreferLargeIons { get; set; }
        [Track]
        public bool RemoveMissingResults { get; set; }
        [Track]
        public double? RTRegressionThreshold { get; set; }
        public int? RTRegressionPrecision { get; set; }
        [Track]
        public double? DotProductThreshold { get; set; }
        [Track]
        public double? IdotProductThreshold { get; set; }
        [Track]
        ReplicateInclusion ReplInclusion { get { return UseBestResult ? ReplicateInclusion.best : ReplicateInclusion.all; } }
        public bool UseBestResult { get; set; }
        public bool AutoPickChildrenOff { get; set; }
        public int NumberOfDecoys { get; set; }
        public string DecoysMethod { get; set; }

        public enum ReplicateInclusion { all, best }

        // Consistency
        [Track]
        public double? CVCutoff { get; set; }
        [Track]
        public double? QValueCutoff { get; set; }
        [Track]
        public int? MinimumDetections { get; set; }
        [Track(defaultValues: typeof(NormalizeOption.DefaultNone))]
        public NormalizeOption NormalizationMethod { get; set; }
        [Track]
        public AreaCVTransitions Transitions { get; set; }
        [Track]
        public int? CountTransitions { get; set; }
        [Track]
        public AreaCVMsLevel MSLevel { get; set; }
        [Track]
        public double? FoldChangeCutoff { get; set; }
        [Track]
        public double? AdjustedPValueCutoff { get; set; }
        [Track]
        public int? MSLevelGroupComparison { get; set; }
        [Track]
        public List<GroupComparisonDef> GroupComparisonDefs { get; set; }
        public List<string> GroupComparisonNames { get; set; }

        public SrmDocument Refine(SrmDocument document)
        {
            return Refine(document, null);
        }

        public SrmDocument Refine(SrmDocument document, SrmSettingsChangeMonitor progressMonitor)
        {
            if (progressMonitor != null)
            {
                var molCount = document.PeptideCount;
                if (CVCutoff.HasValue || QValueCutoff.HasValue)
                {
                    molCount *= 2;
                }

                if (AdjustedPValueCutoff.HasValue || FoldChangeCutoff.HasValue)
                {
                    molCount *= 4;
                }

                progressMonitor.MoleculeCount = molCount;
            }

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
            TargetMap<List<Adduct>> acceptedPeptides = null;
            if (AcceptedPeptides != null)
            {
                var acceptedPeptidesDict = new Dictionary<Target, List<Adduct>>();
                foreach (var peptideCharge in AcceptedPeptides)
                {
                    List<Adduct> charges;
                    if (!acceptedPeptidesDict.TryGetValue(peptideCharge.Target, out charges))
                    {
                        charges = !peptideCharge.Adduct.IsEmpty ? new List<Adduct> {peptideCharge.Adduct} : null;
                        acceptedPeptidesDict.Add(peptideCharge.Target, charges);
                    }
                    else if (charges != null)
                    {
                        if (!peptideCharge.Adduct.IsEmpty)
                            charges.Add(peptideCharge.Adduct);
                        else
                            acceptedPeptidesDict[peptideCharge.Target] = null;
                    }
                }
                acceptedPeptides = new TargetMap<List<Adduct>>(acceptedPeptidesDict);
            }
            HashSet<string> acceptedProteins = (AcceptedProteins != null ? new HashSet<string>(AcceptedProteins) : null);

            var listPepGroups = new List<PeptideGroupDocNode>();
            // Excluding proteins with too few peptides, since they can impact results
            // of the duplicate peptide check.
            int minPeptides = MinPeptidesPerProtein ?? 0;
            foreach (PeptideGroupDocNode nodePepGroup in document.Children)
            {
//                if (progressMonitor != null)
//                    progressMonitor.ProcessGroup(nodePepGroup);

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
                        new SrmSettingsDiff(true, false, false, false, false, false, progressMonitor));
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
                        var identity = nodePep.Peptide.IsCustomMolecule
                            ? new RefinementIdentity(nodePep.Peptide.CustomMolecule)
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
            var refined = (SrmDocument)document.ChangeChildrenChecked(listPepGroups.ToArray(), true);
            var refinedBasic = refined;
            if (CVCutoff.HasValue || QValueCutoff.HasValue)
            {
                if (!document.Settings.HasResults || document.MeasuredResults.Chromatograms.Count < 2)
                {
                    throw new Exception(
                        Resources.RefinementSettings_Refine_The_document_must_contain_at_least_2_replicates_to_refine_based_on_consistency_);
                }

                if (NormalizationMethod.Is(GroupComparison.NormalizationMethod.GLOBAL_STANDARDS) &&
                    !document.Settings.HasGlobalStandardArea)
                {
                    // error
                    throw new Exception(ModelResources.RefinementSettings_Refine_The_document_does_not_have_a_global_standard_to_normalize_by_);
                }

                var cvcutoff = CVCutoff.HasValue ? CVCutoff.Value : double.NaN;
                var qvalue = QValueCutoff.HasValue ? QValueCutoff.Value : double.NaN;
                var minDetections = MinimumDetections.HasValue ? MinimumDetections.Value : -1;
                var countTransitions = CountTransitions.HasValue ? CountTransitions.Value : -1;
                var normalizedValueCalculator = new NormalizedValueCalculator(refined);
                var data = new AreaCVRefinementData(normalizedValueCalculator, new AreaCVRefinementSettings(cvcutoff, qvalue, minDetections, NormalizationMethod,
                    Transitions, countTransitions, MSLevel), CancellationToken.None, progressMonitor);
                refined = data.RemoveAboveCVCutoff(refined);
            }

            if (AdjustedPValueCutoff.HasValue || FoldChangeCutoff.HasValue)
            {
                var pValueCutoff = AdjustedPValueCutoff.HasValue ? AdjustedPValueCutoff.Value : double.NaN;
                var foldChangeCutoff = FoldChangeCutoff.HasValue ? FoldChangeCutoff.Value : double.NaN;
                var groupComparisonData = new GroupComparisonRefinementData(refined, pValueCutoff, foldChangeCutoff,
                    MSLevelGroupComparison, GroupComparisonDefs, progressMonitor);
                refined = groupComparisonData.RemoveBelowCutoffs(refined);
            }

            if (minPeptides > 0 && !ReferenceEquals(refined, refinedBasic))
            {
                // One last pass to remove proteins without enough peptides
                refined = (SrmDocument)document.ChangeChildrenChecked(
                    refined.MoleculeGroups.Where(n => n.Children.Count >= minPeptides).ToArray(), true);
            }
            return refined;
        }

        private NormalizeOption GetLabelIndex(IsotopeLabelType type, SrmDocument doc)
        {
            if (type != null)
            {
                var mods = doc.Settings.PeptideSettings.Modifications.RatioInternalStandardTypes;
                var idx = mods.IndexOf(mod => Equals(mod.Name, type.Name));
                if (idx == -1)
                {
                    // error
                    throw new Exception(ModelResources.RefinementSettings_GetLabelIndex_The_document_does_not_contain_the_given_reference_type_);
                }
                return NormalizeOption.FromIsotopeLabelType(type);
            }

            return NormalizeOption.NONE;
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
                                           TargetMap<List<Adduct>> acceptedPeptides,
                                           SrmSettingsChangeMonitor progressMonitor)
        {
            var listPeptides = new List<PeptideDocNode>();
            int minPrecursors = MinPrecursorsPerPeptide ?? 0;
            foreach (PeptideDocNode nodePep in nodePepGroup.Children)
            {
                if (progressMonitor != null)
                    progressMonitor.ProcessMolecule(nodePep);

                // Avoid removing standards as part of refinement
                if (nodePep.GlobalStandardType != null)
                {
                    listPeptides.Add(nodePep);
                    continue;
                }

                if (outlierIds.Contains(nodePep.Id.GlobalIndex))
                    continue;

                // If there is a set of accepted peptides, and this is not one of them
                // then skip it.
                List<Adduct> acceptedCharges = null;
                if (acceptedPeptides != null &&
                    !acceptedPeptides.TryGetValue(AcceptModified ? nodePep.ModifiedTarget : nodePep.Target, out acceptedCharges))
                {
                    continue;
                }

                if (RemoveMissingLibrary && !nodePep.HasLibInfo)
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
                    var identity = nodePepRefined.Peptide.IsCustomMolecule
                        ? new RefinementIdentity(nodePep.Peptide.CustomMolecule)
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
                var unrankedPeptides = new List<PeptideDocNode>();
                for (int i = 0; i < countPeps; i++)
                {
                    var nodePep = listPeptides[i];
                    // Only peptides with children can possible be ranked by area
                    // Those without should be removed by this operation
                    if (nodePep.Children.Count == 0)
                        continue;     
                    if (nodePep.GlobalStandardType != null 
                        || nodePep.TransitionGroups.All(tranGroup=>internalStandardTypes.Contains(tranGroup.LabelType)))
                    {
                        // Peptides which are internal standards get added back no matter what
                        unrankedPeptides.Add(nodePep);
                        continue;
                    }
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
                listPeptides.AddRange(unrankedPeptides);
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
                                      List<Adduct> acceptedCharges)
        {
            int minTrans = MinTransitionsPepPrecursor ?? 0;

            bool addedGroups = false;
            var listGroups = new List<TransitionGroupDocNode>();
            foreach (TransitionGroupDocNode nodeGroup in nodePep.Children)
            {
                if (acceptedCharges != null && !acceptedCharges.Contains(nodeGroup.TransitionGroup.PrecursorAdduct))
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
                nodeGroupRefined = Refine(nodeGroupRefined, bestResultIndex, document.Settings.TransitionSettings.Integration.IsIntegrateAll);
                // Avoid removing a standard precursor because it lacks the minimum number of transitions
                if (nodeGroupRefined.Children.Count < minTrans && nodePep.GlobalStandardType == null)
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
                                                        nodeGroup.TransitionGroup.PrecursorAdduct,
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

            if (MaxPrecursorPeakOnly && listGroups.Count > 1 &&
                listGroups.Select(g => g.PrecursorAdduct.Unlabeled).Distinct().Count() > 1)
            {
                var chargeGroups =
                    (from g in listGroups
                        group g by g.TransitionGroup.PrecursorAdduct.Unlabeled
                        into ga
                        select new {Adduct = ga.Key, Area = ga.Sum(gg => gg.AveragePeakArea)}).ToArray();

                if (chargeGroups.Any(n => n.Area > 0))
                {
                    // Assume that the probability of two measured areas being exactly equal is low
                    // enough that taking just one is not an issue.
                    var bestCharge = chargeGroups.Aggregate((n1, n2) => n1.Area > n2.Area ? n1 : n2);
                    listGroups = listGroups.Where(g => Equals(g.PrecursorAdduct.Unlabeled, bestCharge.Adduct)).ToList();
                }
            }

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
                if (nodeGroupChild.TransitionGroup.PrecursorAdduct.Equals(nodeGroup.TransitionGroup.PrecursorAdduct) &&
                        Equals(RefineLabelType, nodeGroupChild.TransitionGroup.LabelType))
                    return false;
            }
            foreach (TransitionGroupDocNode nodeGroupAdded in listGroups)
            {
                if (nodeGroupAdded.TransitionGroup.PrecursorAdduct.Equals(nodeGroup.TransitionGroup.PrecursorAdduct) &&
                        Equals(RefineLabelType, nodeGroupAdded.TransitionGroup.LabelType))
                    return false;
            }
            return true;
        }

// ReSharper disable SuggestBaseTypeForParameter
        private TransitionGroupDocNode Refine(TransitionGroupDocNode nodeGroup, int bestResultIndex, bool integrateAll)
// ReSharper restore SuggestBaseTypeForParameter
        {
            var listTrans = new List<TransitionDocNode>();
            foreach (TransitionDocNode nodeTran in nodeGroup.Children)
            {
                double? peakFoundRatio = nodeTran.GetPeakCountRatio(bestResultIndex, integrateAll);
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

        public enum ConvertToSmallMoleculesMode
        {
            none,        // No conversion - call to ConvertToSmallMolecules is a no-op
            formulas,    // Convert peptides to custom ions with ion formulas
            masses_and_names,  // Convert peptides to custom ions but retain just the masses, and names for use in ratio calcs
            masses_only  // Convert peptides to custom ions but retain just the masses, no formulas or names so ratio calcs have to work on sorted mz
        };

        public enum ConvertToSmallMoleculesChargesMode
        {
            none, // Leave charges alone
            invert, // Invert charges
            invert_some // Invert every other transition group
        }

        /// <summary>
        /// Adjust library info for small molecules
        /// </summary>
        public static Results<TransitionGroupChromInfo>ConvertTransitionGroupChromInfoLibraryInfoToSmallMolecules(TransitionGroupDocNode transitionGroupDocNode, 
            ConvertToSmallMoleculesMode mode, ConvertToSmallMoleculesChargesMode invertChargesMode)
        {
            if (transitionGroupDocNode.Results == null)
                return null;
            if (invertChargesMode == ConvertToSmallMoleculesChargesMode.none && mode != ConvertToSmallMoleculesMode.masses_only)
            {
                return transitionGroupDocNode.Results;
            }
            // No libraries for small molecules without IDs, or when inverting polarity in conversion (too much bother adjusting mz in libs), so lose the dotp
            var listResultsNew = new List<ChromInfoList<TransitionGroupChromInfo>>();
            foreach (var info in transitionGroupDocNode.Results)
            {
                var infoNew = new List<TransitionGroupChromInfo>();
                foreach (var result in info)
                {
                    infoNew.Add(result.ChangeLibraryDotProduct(null));
                }
                listResultsNew.Add(new ChromInfoList<TransitionGroupChromInfo>(infoNew));
            }
            var resultsNew = new Results<TransitionGroupChromInfo>(listResultsNew);
            return resultsNew;
        }

        public static CustomMolecule ConvertToSmallMolecule(ConvertToSmallMoleculesMode mode,
            SrmDocument document, PeptideDocNode nodePep,
            Dictionary<LibKey, LibKey> smallMoleculeConversionPrecursorMap = null)
        {
            return ConvertToSmallMolecule(mode, document, nodePep, out _, 0, null, smallMoleculeConversionPrecursorMap);
        }

        public static CustomMolecule ConvertToSmallMolecule(ConvertToSmallMoleculesMode mode, 
            SrmDocument document, PeptideDocNode nodePep, out Adduct adduct, int precursorCharge, IsotopeLabelType isotopeLabelType,
            Dictionary<LibKey, LibKey> smallMoleculeConversionPrecursorMap = null)
        {
            // We're just using this masscalc to get the ion formula, so mono vs average doesn't matter
            isotopeLabelType = isotopeLabelType ?? IsotopeLabelType.light;
            if (nodePep == null) // Can happen when called from document grid handler when doc changes
            {
                adduct = Adduct.EMPTY;
                return CustomMolecule.EMPTY;
            }
            var peptideTarget = nodePep.Peptide.Target;
            var masscalc = document.Settings.TryGetPrecursorCalc(isotopeLabelType, nodePep.ExplicitMods);
            if (masscalc == null)
            {
                // No support in mods for this label type
                masscalc = new SequenceMassCalc(MassType.Monoisotopic);
            }
            // Determine the molecular formula of the charged/labeled peptide
            var moleculeFormula = masscalc.GetMolecularFormula(peptideTarget.Sequence); // Get molecular formula, possibly with isotopes in it (as with iTraq)
            adduct = 
                Adduct.NonProteomicProtonatedFromCharge(precursorCharge, BioMassCalc.FindIsotopeLabelsInFormula(moleculeFormula.Molecule));
            if (BioMassCalc.ContainsIsotopicElement(moleculeFormula.Molecule))
            {
                // Isotopes are already accounted for in the adduct
                moleculeFormula = BioMassCalc.StripLabelsFromFormula(moleculeFormula);
            }

            var mol = ParsedMolecule.Create(moleculeFormula); // Convert to ParsedMolecule
            var customMolecule = new CustomMolecule(mol, TestingConvertedFromProteomicPeptideNameDecorator + masscalc.GetModifiedSequence(peptideTarget, false)); // Make sure name isn't a valid peptide seq

            if (mode == ConvertToSmallMoleculesMode.masses_only)
            {
                // No formulas or names, just masses - see how we handle that
                customMolecule = new CustomMolecule(customMolecule.MonoisotopicMass,
                    customMolecule.AverageMass);
            }
            else if (mode == ConvertToSmallMoleculesMode.masses_and_names)
            {
                // Just masses and names - see how we handle that
                customMolecule = new CustomMolecule(customMolecule.MonoisotopicMass,
                    customMolecule.AverageMass, customMolecule.Name);
            }
            // Collect information for converting libraries
            var chargeAndModifiedSequence = new LibKey(masscalc.GetModifiedSequence(peptideTarget, SequenceModFormatType.lib_precision, false), precursorCharge);
            if (smallMoleculeConversionPrecursorMap != null && !smallMoleculeConversionPrecursorMap.ContainsKey(chargeAndModifiedSequence))
            {
                smallMoleculeConversionPrecursorMap.Add(chargeAndModifiedSequence, new LibKey(customMolecule.GetSmallMoleculeLibraryAttributes(), adduct));
            }
            return customMolecule;
        }

        public const string TestingConvertedFromProteomic = "zzzTestingConvertedFromProteomic";
        public static string TestingConvertedFromProteomicPeptideNameDecorator = @"pep_"; // Testing aid: use this to make sure name of a converted peptide isn't a valid peptide seq
        
        public static CustomMolecule MoleculeFromPeptideSequence(string sequence)
        {
            var moleculeFormula = SrmSettings.MonoisotopicMassCalc.GetMolecularFormula(sequence);
            var mol = ParsedMolecule.Create(moleculeFormula); // Convert to ParsedMolecule
            var customMolecule = new CustomMolecule(mol,
                RefinementSettings.TestingConvertedFromProteomicPeptideNameDecorator + sequence);
            return customMolecule;
        }

        public SrmDocument ConvertToSmallMolecules(SrmDocument document, 
            string pathForLibraryFiles, // In case we translate libraries etc
            ConvertToSmallMoleculesMode mode = ConvertToSmallMoleculesMode.formulas, 
            ConvertToSmallMoleculesChargesMode invertChargesMode = ConvertToSmallMoleculesChargesMode.none, 
            bool ignoreDecoys=false, bool addAnnotations = true)
        {
            if (mode == ConvertToSmallMoleculesMode.none)
                return document;
            var newdoc = new SrmDocument(document.Settings);
            var note = addAnnotations ? new Annotations(TestingConvertedFromProteomic, null, 1) : Annotations.EMPTY; // Optionally mark this as a testing node so we don't sort it
            var precursorMap = new Dictionary<LibKey, LibKey>(); // Map int,modSeq to adduct,molecule

            var invertCharges = invertChargesMode == ConvertToSmallMoleculesChargesMode.invert;
            var canConvertLibraries =
                invertChargesMode == ConvertToSmallMoleculesChargesMode.none && // Too much trouble adjusting mz in libs
                mode != ConvertToSmallMoleculesMode.masses_only && // Need a proper ID for libraries
                document.Settings.PeptideSettings.Libraries.IsLoaded; // If original doc never loaded libraries, don't worry about converting

            // Retention time prediction
            var prediction = newdoc.Settings.PeptideSettings.Prediction;
            var peptideTimes = prediction?.RetentionTime?.PeptideTimes;
            if (canConvertLibraries && peptideTimes != null)
            {
                var calc = prediction.RetentionTime.Calculator;
                var newDbFile =  calc.PersistAsSmallMolecules(Path.GetDirectoryName(calc.PersistencePath), newdoc);
                if (newDbFile != null)
                {
                    var irtCalcName = Path.GetFileNameWithoutExtension(newDbFile);
                    var calcIrt = new RCalcIrt(irtCalcName, newDbFile);
                    var retentionTimeRegression = prediction.RetentionTime.ChangeCalculator(calcIrt);
                    retentionTimeRegression = (RetentionTimeRegression)retentionTimeRegression.ChangeName(irtCalcName);
                    newdoc = newdoc.ChangeSettings(newdoc.Settings.ChangePeptidePrediction(p =>
                        prediction.ChangeRetentionTime(retentionTimeRegression)));
                }
            }

            // Make small molecule filter settings look like peptide filter settings
            var ionTypes = new List<IonType>();
            foreach (var ionType in document.Settings.TransitionSettings.Filter.PeptideIonTypes)
            {
                if (ionType == IonType.precursor)
                    ionTypes.Add(ionType);
                else if (!ionTypes.Contains(IonType.custom))
                    ionTypes.Add(IonType.custom);
            }
            // Precursor charges
            var precursorAdducts = new List<Adduct>();
            foreach (var charge in document.Settings.TransitionSettings.Filter.PeptidePrecursorCharges)
            {
                switch (invertChargesMode)
                {
                    case ConvertToSmallMoleculesChargesMode.invert:
                        precursorAdducts.Add(Adduct.FromCharge(-charge.AdductCharge, Adduct.ADDUCT_TYPE.non_proteomic));
                        break;
                    case ConvertToSmallMoleculesChargesMode.invert_some:
                        precursorAdducts.Add(Adduct.FromCharge(charge.AdductCharge, Adduct.ADDUCT_TYPE.non_proteomic));
                        precursorAdducts.Add(Adduct.FromCharge(-charge.AdductCharge, Adduct.ADDUCT_TYPE.non_proteomic));
                        break;
                    default:
                        precursorAdducts.Add(Adduct.FromCharge(charge.AdductCharge, Adduct.ADDUCT_TYPE.non_proteomic));
                        break;
                }
            }
            // Fragment charges
            var fragmentAdducts = new List<Adduct>();
            foreach (var charge in document.Settings.TransitionSettings.Filter.PeptideProductCharges)
            {
                switch (invertChargesMode)
                {
                    case ConvertToSmallMoleculesChargesMode.invert:
                        fragmentAdducts.Add(Adduct.FromChargeNoMass(-charge.AdductCharge));
                        break;
                    case ConvertToSmallMoleculesChargesMode.invert_some:
                        fragmentAdducts.Add(Adduct.FromChargeNoMass(charge.AdductCharge));
                        fragmentAdducts.Add(Adduct.FromChargeNoMass(-charge.AdductCharge));
                        break;
                    default:
                        fragmentAdducts.Add(Adduct.FromChargeNoMass(charge.AdductCharge));
                        break;
                }
            }

            newdoc = newdoc.ChangeSettings(newdoc.Settings.ChangeTransitionSettings(newdoc.Settings.TransitionSettings.ChangeFilter(
                newdoc.Settings.TransitionSettings.Filter.ChangeSmallMoleculeIonTypes(ionTypes).
                ChangeSmallMoleculePrecursorAdducts(precursorAdducts).ChangeSmallMoleculeFragmentAdducts(fragmentAdducts))));

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
                        if (invertChargesMode == ConvertToSmallMoleculesChargesMode.invert_some)
                        {
                            invertCharges = !invertCharges;
                        }
                        var peptideAsMolecule = ConvertToSmallMolecule(mode, document, mol);
                        var newPeptide = new Peptide(peptideAsMolecule);
                        var newPeptideDocNode = new PeptideDocNode(newPeptide, newdoc.Settings,
                            mol.ExplicitMods != null && mol.ExplicitMods.HasIsotopeLabels ? mol.ExplicitMods : null, // Custom molecules use modifications - but just the static isotope labels 
                            null,
                            mol.GlobalStandardType, mol.Rank, mol.ExplicitRetentionTime, note, mol.Results, new TransitionGroupDocNode[0],
                            mol.AutoManageChildren);

                        foreach (var transitionGroupDocNode in mol.TransitionGroups)
                        {
                            if (transitionGroupDocNode.IsDecoy)
                            {
                                if (ignoreDecoys)
                                    continue;
                                throw new Exception(@"There is no translation from decoy to small molecules");
                            }


                            var precursorCharge = transitionGroupDocNode.TransitionGroup.PrecursorAdduct.AdductCharge * (invertCharges ? -1 : 1);
                            var isotopeLabelType = transitionGroupDocNode.TransitionGroup.LabelType;
                            Adduct adduct;
                            ConvertToSmallMolecule(mode, document, mol, out adduct, precursorCharge, isotopeLabelType, precursorMap);

                            var newTransitionGroup = new TransitionGroup(newPeptide, adduct, isotopeLabelType);
                            // Deal with library info - remove now if we can't use it due to charge swap or loss of molecule ID, otherwise clean it up later
                            SpectrumHeaderInfo libInfo;
                            if (canConvertLibraries && transitionGroupDocNode.HasLibInfo)
                            {
                                libInfo = transitionGroupDocNode.LibInfo.LibraryName.Contains(BiblioSpecLiteSpec.DotConvertedToSmallMolecules)
                                    ? transitionGroupDocNode.LibInfo
                                    : transitionGroupDocNode.LibInfo.ChangeLibraryName(transitionGroupDocNode.LibInfo.LibraryName + BiblioSpecLiteSpec.DotConvertedToSmallMolecules);
                            }
                            else
                            {
                                libInfo = null;
                            }
                            var resultsNew = ConvertTransitionGroupChromInfoLibraryInfoToSmallMolecules(transitionGroupDocNode, mode, invertChargesMode);
                            var newTransitionGroupDocNode = new TransitionGroupDocNode(newTransitionGroup,
                                transitionGroupDocNode.Annotations.Merge(note), document.Settings,
                                null, libInfo, transitionGroupDocNode.ExplicitValues, resultsNew, null,
                                transitionGroupDocNode.AutoManageChildren);
                            var mzShiftPrecursor = invertCharges ? 2.0 * BioMassCalc.MassProton : 0;  // We removed hydrogen rather than added
                            var mzShiftFragment = invertCharges ? -2.0 * BioMassCalc.MassElectron : 0; // We will move proton masses to the fragment and use charge-only adducts
                            Assume.IsTrue(Math.Abs(newTransitionGroupDocNode.PrecursorMz.Value + mzShiftPrecursor - transitionGroupDocNode.PrecursorMz.Value) <= 1E-5);

                            foreach (var transition in transitionGroupDocNode.Transitions)
                            {
                                var mass = TypedMass.ZERO_MONO_MASSH;
                                var ionType = IonType.custom;
                                CustomMolecule transitionCustomMolecule;
                                if (transition.Transition.IonType == IonType.precursor)
                                {
                                    ionType = IonType.precursor;
                                    transitionCustomMolecule = null; // Precursor transition uses the parent molecule
                                    if (transition.Transition.MassIndex > 0)
                                    {
                                        mass = newTransitionGroupDocNode.IsotopeDist.GetMassI(transition.Transition.MassIndex);
                                    }
                                    else
                                    {
                                        mass = newTransitionGroupDocNode.GetPrecursorIonMass();
                                    }
                                }
                                else if (transition.Transition.IonType == IonType.custom)
                                {
                                    transitionCustomMolecule = transition.Transition.CustomIon;
                                    mass = transitionCustomMolecule.MonoisotopicMass;
                                }
                                else
                                {
                                    // CONSIDER - try to get fragment formula?
                                    var mzMassType = transition.MzMassType.IsMonoisotopic() ? MassType.Monoisotopic : MassType.Average;
                                    // Account for adduct mass here, since we're going to replace it with a charge-only adduct to mimic normal small mol use
                                    var chargeOnly = Adduct.FromChargeNoMass(transition.Transition.Charge);
                                    mass = chargeOnly.MassFromMz(transition.Mz, mzMassType);
                                    // We can't really get at both mono and average mass from m/z, but for test purposes this is fine
                                    var massMono = new TypedMass(mass.Value, MassType.Monoisotopic);
                                    var massAverage = new TypedMass(mass.Value, MassType.Average);
                                    var name = transition.HasLoss ?
                                        string.Format(@"{0}[-{1}]", transition.Transition.FragmentIonName, (int)transition.LostMass) :
                                        transition.Transition.FragmentIonName;
                                    transitionCustomMolecule = new CustomMolecule(massMono, massAverage, name);
                                }
                                if (ionType != IonType.precursor)
                                {
                                    if (mode == ConvertToSmallMoleculesMode.masses_and_names)
                                    {
                                        // Discard the formula if we're testing the use of mass-with-names (for matching in ratio calcs) target specification
                                        transitionCustomMolecule = new CustomMolecule(transitionCustomMolecule.MonoisotopicMass, transitionCustomMolecule.AverageMass,
                                            transition.Transition.FragmentIonName);
                                    }
                                    else if (mode == ConvertToSmallMoleculesMode.masses_only)
                                    {
                                        // Discard the formula and name if we're testing the use of mass-only target specification
                                        transitionCustomMolecule = new CustomMolecule(transitionCustomMolecule.MonoisotopicMass, transitionCustomMolecule.AverageMass);
                                    }
                                }
                                // Normally in small molecule world fragment transition adducts are charge only
                                var transitionAdduct = (transition.Transition.IonType == IonType.precursor) 
                                    ? adduct.ChangeCharge(transition.Transition.Charge*(invertCharges ? -1 : 1))
                                    : Adduct.FromChargeNoMass(transition.Transition.Charge*(invertCharges ? -1 : 1)); // We don't label fragments
                                // Deal with library info - remove now if we can't use it due to charge swap or loss of molecule ID, otherwise clean it up later
                                var transitionLibInfo = transition.HasLibInfo && canConvertLibraries
                                    ? transition.LibInfo
                                    : null;
                                var newTransition = new Transition(newTransitionGroup, ionType,
                                    null, transition.Transition.MassIndex, transitionAdduct, null, transitionCustomMolecule);
                                var newTransitionDocNode = new TransitionDocNode(newTransition, transition.Annotations.Merge(note),
                                    null, mass, transition.QuantInfo.ChangeLibInfo(transitionLibInfo), ExplicitTransitionValues.EMPTY, 
                                    transition.Results);
                                var mzShift = transition.Transition.IonType == IonType.precursor ?
                                    mzShiftPrecursor :
                                    mzShiftFragment;
                                Assume.IsTrue(Math.Abs(newTransitionDocNode.Mz + mzShift - transition.Mz.Value) <= .5 * BioMassCalc.MassElectron, String.Format(@"unexpected mz difference {0}-{1}={2}", newTransitionDocNode.Mz, transition.Mz, newTransitionDocNode.Mz - transition.Mz.Value));
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

            if (newdoc.Settings.TransitionSettings.IonMobilityFiltering.IonMobilityLibrary != null &&
                newdoc.Settings.TransitionSettings.IonMobilityFiltering.IonMobilityLibrary.IsUsable)
            {
                var mapped = new List<PrecursorIonMobilities>();
                foreach (var kvp in precursorMap)
                {
                    var im = document.Settings.TransitionSettings.IonMobilityFiltering.GetIonMobilityFilter(kvp.Key, 0, null);
                    if (im != null)
                    {
                        mapped.Add(new PrecursorIonMobilities(kvp.Value, im));
                    }
                }

                var name = Path.GetFileNameWithoutExtension(newdoc.Settings.TransitionSettings.IonMobilityFiltering.IonMobilityLibrary.FilePath) +
                           ModelResources.RefinementSettings_ConvertToSmallMolecules_Converted_To_Small_Molecules;
                var path = Path.Combine(pathForLibraryFiles, name + IonMobilityDb.EXT);
                var db = IonMobilityDb.CreateIonMobilityDb(path, name, false).UpdateIonMobilities(mapped);
                var spec = new IonMobilityLibrary(name, path, db);
                var newSettings = newdoc.Settings.ChangeTransitionIonMobilityFiltering(t => t.ChangeLibrary(spec));
                newdoc = newdoc.ChangeSettings(newSettings);
            }

            if (canConvertLibraries)
            {
                // Output a new set of libraries with known charge,modifiedSeq transformed to adduct,molecule
                var dictOldNamesToNew = new Dictionary<string, string>();
                var oldGroupLibInfos = new List<SpectrumHeaderInfo>();
                var oldTransitionLibInfos = new List<TransitionLibInfo>();
                if (document.Settings.PeptideSettings.Libraries.HasLibraries)
                {
                    oldGroupLibInfos.AddRange(document.MoleculeTransitionGroups.Select(group => group.LibInfo));
                    oldTransitionLibInfos.AddRange(document.MoleculeTransitions.Select(t => t.LibInfo));
                    var newSettings = BlibDb.MinimizeLibrariesAndConvertToSmallMolecules(document,
                        pathForLibraryFiles, 
                        ModelResources.RefinementSettings_ConvertToSmallMolecules_Converted_To_Small_Molecules,
                        precursorMap, dictOldNamesToNew, null).Settings;
                    CloseLibraryStreams(document);
                    newdoc = newdoc.ChangeSettings(newdoc.Settings.
                        ChangePeptideLibraries(l => newSettings.PeptideSettings.Libraries));
                }
                if (dictOldNamesToNew.Any())
                {
                    // Restore library info for use with revised libraries
                    var oldGroupLibInfoIndex = 0;
                    var oldTransitionLibInfoIndex = 0;
                    newdoc = (SrmDocument)newdoc.ChangeAll(node =>
                        {
                            var nodeGroup = node as TransitionGroupDocNode;
                            if (nodeGroup != null)
                            {
                                var groupLibInfo = oldGroupLibInfos[oldGroupLibInfoIndex++];
                                if (groupLibInfo == null)
                                    return node;
                                var libName = groupLibInfo.LibraryName;
                                var libNameNew = dictOldNamesToNew[libName];
                                if (Equals(libName, libNameNew))
                                    return node;
                                groupLibInfo = groupLibInfo.ChangeLibraryName(libNameNew);
                                return nodeGroup.ChangeLibInfo(groupLibInfo);
                            }
                            var nodeTran = node as TransitionDocNode;
                            if (nodeTran == null)
                                return node;
                            var libInfo = oldTransitionLibInfos[oldTransitionLibInfoIndex++];
                            if (libInfo == null)
                                return node;
                            return nodeTran.ChangeLibInfo(libInfo);
                        },
                        (int)SrmDocument.Level.Transitions);
                }
                if (document.Settings.HasIonMobilityLibraryPersisted)
                {
                    var newDbPath = document.Settings.TransitionSettings.IonMobilityFiltering.IonMobilityLibrary
                        .PersistMinimized(pathForLibraryFiles, document, precursorMap, out var newLoadedDb);
                    var spec = new IonMobilityLibrary(document.Settings.TransitionSettings.IonMobilityFiltering.IonMobilityLibrary.Name + 
                                                      @" " + ModelResources.RefinementSettings_ConvertToSmallMolecules_Converted_To_Small_Molecules, newDbPath, newLoadedDb);
                    newdoc = newdoc.ChangeSettings(newdoc.Settings.ChangeTransitionIonMobilityFiltering(im => im.ChangeLibrary(spec)));
                }
            }

            newdoc = ForceReloadChromatograms(newdoc);
            CloseLibraryStreams(newdoc);
            return newdoc;
        }

        /// <summary>
        /// Force TransitionGroupDocNode.UpdateResults to look at all of the chromatogram data again,
        /// and make sure that it matches what is in the document.
        /// This is accomplished by changing "mzMatchTolerance" to a slightly different value and
        /// changing it back again.
        /// </summary>
        private SrmDocument ForceReloadChromatograms(SrmDocument document)
        {
            double mzMatchToleranceOld = document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            // Find the double value which is the smallest step away from the current value
            double mzMatchToleranceNew =
                BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(mzMatchToleranceOld) - 1);
            document = document.ChangeSettings(document.Settings.ChangeTransitionInstrument(instrument =>
                instrument.ChangeMzMatchTolerance(mzMatchToleranceNew)));
            return document.ChangeSettings(document.Settings.ChangeTransitionInstrument(instrument =>
                instrument.ChangeMzMatchTolerance(mzMatchToleranceOld)));

        }

        /// <summary>
        /// Closes all library streams on a document.
        /// Use this when "ChangeSettings" was called on a document that hads libraries,
        /// and that document is not owned by a DocumentContainer.
        /// </summary>
        private void CloseLibraryStreams(SrmDocument doc)
        {
            foreach (var library in doc.Settings.PeptideSettings.Libraries.Libraries)
            {
                foreach (var stream in library.ReadStreams)
                {
                    stream.CloseStream();
                }
            }
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

        public static SrmDocument RemoveDecoys(SrmDocument document)
        {
            // Remove the existing decoys
            return (SrmDocument) document.RemoveAll(document.MoleculeGroups.Where(nodePeptideGroup => nodePeptideGroup.IsDecoy)
                                                        .Select(nodePeptideGroup => nodePeptideGroup.Id.GlobalIndex).ToArray()); 
        }

        public static ModifiedDocument ModifyDocumentByRemovingDecoys(SrmDocument originalDocument)
        {
            var modifiedDocument = new ModifiedDocument(RemoveDecoys(originalDocument));
            var deletedMoleculeGroups = originalDocument.MoleculeGroups.Where(moleculeGroup =>
                modifiedDocument.Document.FindNodeIndex(moleculeGroup.PeptideGroup) < 0).ToList();
            var docPair = SrmDocumentPair.Create(originalDocument, modifiedDocument.Document, SrmDocument.DOCUMENT_TYPE.none);
            modifiedDocument = modifiedDocument.ChangeAuditLogEntry(SkylineWindow.CreateDeleteNodesEntry(docPair,
                deletedMoleculeGroups.Select(
                    moleculeGroup => AuditLogEntry.GetNodeName(originalDocument, moleculeGroup).ToString()), null));
            return modifiedDocument;
        }

        public ModifiedDocument ModifyDocumentByGeneratingDecoys(SrmDocument document)
        {
            var modifiedDocument = new ModifiedDocument(GenerateDecoys(document));
            if (ReferenceEquals(document, modifiedDocument.Document))
            {
                return null;
            }
            var plural = NumberOfDecoys > 1;
            modifiedDocument = modifiedDocument.ChangeAuditLogEntry(AuditLogEntry.CreateSingleMessageEntry(
                new MessageInfo(
                    plural ? MessageType.added_peptide_decoys : MessageType.added_peptide_decoy,
                    modifiedDocument.Document.DocumentType,
                    NumberOfDecoys, DecoysMethod)));
            return modifiedDocument;
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
            {
                var random = new Random(RANDOM_SEED);
                return GenerateDecoysFunc(document, numDecoys, true, m => GetShuffledPeptideSequence(m, random));
            }
            
            if (decoysMethod == DecoyGeneration.REVERSE_SEQUENCE)
                return GenerateDecoysFunc(document, numDecoys, false, GetReversedPeptideSequence);

            return GenerateDecoysFunc(document, numDecoys, false, null);
        }

        private struct SequenceMods
        {
            public SequenceMods(PeptideDocNode nodePep) : this()
            {
                Peptide = nodePep.Peptide;
                Sequence = Peptide.Target.Sequence;
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

        private static SrmDocument GenerateDecoysFunc(SrmDocument document, int numDecoys, bool multiCycle, Func<SequenceMods, SequenceMods> genDecoySequence)
        {
            // Loop through the existing tree in random order creating decoys
            var settings = document.Settings;
            var enzyme = settings.PeptideSettings.Enzyme;

            var decoyNodePepList = new List<PeptideDocNode>();
            var setDecoyKeys = new HashSet<PeptideModKey>();
            var randomShift = new Random(RANDOM_SEED);
            while (numDecoys > 0)
            {
                int startDecoys = numDecoys;
                foreach (var nodePep in document.Peptides.ToArray().RandomOrder(RANDOM_SEED))
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

                    const int maxIterations = 10; // Maximum number of times to try generating decoy
                    for (var iteration = 0; iteration < maxIterations; iteration++)
                    {
                        var seqMods = new SequenceMods(nodePep);
                        if (genDecoySequence != null)
                        {
                            seqMods = genDecoySequence(seqMods);
                        }
                        var peptide = nodePep.Peptide;
                        var decoyPeptide = new Peptide(null, seqMods.Sequence, null, null, enzyme.CountCleavagePoints(seqMods.Sequence), true);
                        if (seqMods.Mods != null)
                            seqMods.Mods = seqMods.Mods.ChangePeptide(decoyPeptide);

                        var retry = false;
                        foreach (var comparableGroups in PeakFeatureEnumerator.ComparableGroups(nodePep))
                        {
                            var decoyNodeTranGroupList = GetDecoyGroups(nodePep, decoyPeptide, seqMods.Mods,
                                comparableGroups, document, Equals(seqMods.Sequence, peptide.Sequence), randomShift);
                            if (decoyNodeTranGroupList.Count == 0)
                                continue;

                            var nodePepNew = new PeptideDocNode(decoyPeptide, settings, seqMods.Mods,
                                null, nodePep.ExplicitRetentionTime, decoyNodeTranGroupList.ToArray(), false);

                            // Avoid adding empty peptide nodes
                            nodePepNew = nodePepNew.ChangeSettings(settings, SrmSettingsDiff.ALL);
                            if (nodePepNew.Children.Count == 0)
                                continue;

                            if (multiCycle && nodePepNew.ChangeSettings(settings, SrmSettingsDiff.ALL).TransitionCount != nodePep.TransitionCount)
                            {
                                // Try to generate a new decoy if multi-cycle and the generated decoy has a different number of transitions than the target
                                retry = true;
                                break;
                            }

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
                        if (!retry)
                            break;
                    }
                }
                // Stop if not multi-cycle or the number of decoys has not changed.
                if (!multiCycle || startDecoys == numDecoys)
                    break;
            }
            var decoyNodePepGroup = new PeptideGroupDocNode(new PeptideGroup(true), Annotations.EMPTY,
                PeptideGroup.DECOYS, null, decoyNodePepList.ToArray(), false);
            decoyNodePepGroup = decoyNodePepGroup.ChangeSettings(document.Settings, SrmSettingsDiff.ALL);

            if (decoyNodePepGroup.PeptideCount > 0)
            {
                decoyNodePepGroup.CheckDecoys(document, out _, out _, out var proportionDecoysMatch);
                decoyNodePepGroup = decoyNodePepGroup.ChangeProportionDecoysMatch(proportionDecoysMatch);
            }

            return (SrmDocument)document.Add(decoyNodePepGroup);
        }

        private static List<TransitionGroupDocNode> GetDecoyGroups(PeptideDocNode nodePep, Peptide decoyPeptide,
            ExplicitMods mods, IEnumerable<TransitionGroupDocNode> comparableGroups, SrmDocument document, bool shiftMass, Random randomShift)
        {
            var decoyNodeTranGroupList = new List<TransitionGroupDocNode>();

            var chargeToPrecursor = new Tuple<int, TransitionGroupDocNode>[2*(TransitionGroup.MAX_PRECURSOR_CHARGE+1)]; // Allow for negative charges
            foreach (TransitionGroupDocNode nodeGroup in comparableGroups)
            {
                var transGroup = nodeGroup.TransitionGroup;

                int precursorMassShift;
                TransitionGroupDocNode nodeGroupPrimary = null;

                var primaryPrecursor = chargeToPrecursor[TransitionGroup.MAX_PRECURSOR_CHARGE + nodeGroup.TransitionGroup.PrecursorAdduct.AdductCharge]; // Allow for negative charges
                if (primaryPrecursor != null)
                {
                    precursorMassShift = primaryPrecursor.Item1;
                    nodeGroupPrimary = primaryPrecursor.Item2;
                }
                else if (shiftMass)
                {
                    precursorMassShift = GetPrecursorMassShift(randomShift);
                }
                else
                {
                    precursorMassShift = TransitionGroup.ALTERED_SEQUENCE_DECOY_MZ_SHIFT;
                }

                var decoyGroup = new TransitionGroup(decoyPeptide, transGroup.PrecursorAdduct,
                                                        transGroup.LabelType, false, precursorMassShift);

                var decoyNodeTranList = nodeGroupPrimary != null
                    ? decoyGroup.GetMatchingTransitions(document.Settings, nodeGroupPrimary, mods)
                    : GetDecoyTransitions(nodeGroup, decoyGroup, shiftMass, randomShift);

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
                    chargeToPrecursor[TransitionGroup.MAX_PRECURSOR_CHARGE + transGroup.PrecursorAdduct.AdductCharge] = // Allow for negative charges
                        new Tuple<int, TransitionGroupDocNode>(precursorMassShift, nodeGroupDecoy);
                }
            }

            return decoyNodeTranGroupList;
        }

        private static TransitionDocNode[] GetDecoyTransitions(TransitionGroupDocNode nodeGroup, TransitionGroup decoyGroup, bool shiftMass, Random randomShift)
        {
            var decoyNodeTranList = new List<TransitionDocNode>();
            foreach (var nodeTran in nodeGroup.Transitions)
            {
                var transition = nodeTran.Transition;
                int productMassShift = 0;
                if (shiftMass)
                    productMassShift = GetProductMassShift(randomShift);
                else if (transition.IsPrecursor() && decoyGroup.DecoyMassShift.HasValue)
                    productMassShift = decoyGroup.DecoyMassShift.Value;
                var decoyTransition = new Transition(decoyGroup, transition.IonType, transition.CleavageOffset,
                                                     transition.MassIndex, transition.Adduct, productMassShift, transition.CustomIon);
                decoyNodeTranList.Add(new TransitionDocNode(decoyTransition, nodeTran.Losses, nodeTran.MzMassType.IsAverage() ? TypedMass.ZERO_AVERAGE_MASSH : TypedMass.ZERO_MONO_MASSH, 
                                                            nodeTran.QuantInfo, nodeTran.ExplicitValues));
            }
            return decoyNodeTranList.ToArray();
        }

        private const int RANDOM_SEED = 7*7*7*7*7; // 7^5 recommended by Brian S.

        private static int GetPrecursorMassShift(Random random)
        {
            // Do not allow zero for the mass shift of the precursor
            int massShift = random.Next(TransitionGroup.MIN_PRECURSOR_DECOY_MASS_SHIFT,
                                          TransitionGroup.MAX_PRECURSOR_DECOY_MASS_SHIFT);
            return massShift < 0 ? massShift : massShift + 1;
        }

        private static int GetProductMassShift(Random random)
        {
            int massShift = random.Next(Transition.MIN_PRODUCT_DECOY_MASS_SHIFT,
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

        private static SequenceMods GetShuffledPeptideSequence(SequenceMods seqMods, Random random)
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
                    Helpers.Swap(ref newIndices[random.Next(newIndices.Length)], ref newIndices[random.Next(newIndices.Length)]);

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
//            private readonly Adduct _bestCharge;

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
                    group nodeGroup by nodeGroup.TransitionGroup.PrecursorAdduct into g
                    select new {Charge = g.Key, Area = g.Sum(ng => ng.GetPeakArea(bestResultIndex))};

                // Store the best charge state and its area
                var bestChargeGroup = chargeGroups.OrderBy(cg => cg.Area).First();
//                _bestCharge = bestChargeGroup.Charge;
                Area = bestChargeGroup.Area ?? 0;

                Index = index;
            }

            public float Area { get; private set; }
            public int Index { get; private set; }
            public int Rank { get; set; }

            public PeptideDocNode Peptide
            {
                get { return _nodePep; }
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
            public RefinementIdentity(CustomMolecule customMolecule)
            {
                CustomMolecule = customMolecule;
            }

            public RefinementIdentity(string sequence)
            {
                Sequence = sequence;
            }

            public RefinementIdentity(Target id)
            {
                CustomMolecule = id.IsProteomic ? null : id.Molecule;
                Sequence = id.IsProteomic ? id.Sequence : null;
            }

            private CustomMolecule CustomMolecule { get; set; }
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
                result = (result*397) ^ (CustomMolecule != null ? CustomMolecule.GetHashCode() : 0);
                return result;
            }

            private bool Equals(RefinementIdentity identity)
            {
                return Equals(identity.Sequence, Sequence) &&
                       Equals(identity.CustomMolecule, CustomMolecule);
            }

            public override string ToString()
            {
                return CustomMolecule != null ? CustomMolecule.ToString() : Sequence;
            }
        }
    }
}
