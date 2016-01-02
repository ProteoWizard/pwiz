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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Optimization;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Util;
using pwiz.Skyline.Model.Lib;

namespace pwiz.Skyline.Model.DocSettings
{
    /// <summary>
    /// Model object for all Skyline document settings information.
    /// 
    /// Due to the need to store this information in the user's
    /// settings store both as instances of the complete settings
    /// hierarchy, and many parts in separate lists, all of the
    /// components are made self-serializable.
    /// 
    /// Intially, this was done using attributes in the <see cref="System.Xml"/>
    /// namespace, but this proved too limiting due to lack of
    /// support for object immutability, in-object validation, and
    /// <see cref="Nullable{T}"/> properties.  Therefore, all xml
    /// serialization is now coded explicitly using local project
    /// helper classes.
    /// </summary>
    [XmlRoot("settings_summary")]
    public class SrmSettings : XmlNamedElement, IPeptideFilter
    {
        public SrmSettings(string name, PeptideSettings peptideSettings, TransitionSettings transitionSettings, DataSettings dataSettings, DocumentRetentionTimes documentRetentionTimes)
            : base(name)
        {
            PeptideSettings = peptideSettings;
            TransitionSettings = transitionSettings;
            DataSettings = dataSettings;
            DocumentRetentionTimes = documentRetentionTimes;

            // Create cached calculator instances
            CreatePrecursorMassCalcs();
            CreateFragmentMassCalcs();
        }

        public PeptideSettings PeptideSettings { get; private set; }

        public TransitionSettings TransitionSettings { get; private set; }

        public DataSettings DataSettings { get; private set; }

        public MeasuredResults MeasuredResults { get; private set; }

        /// <summary>
        /// Unfortunately, because the MeasuredResults property can be null, this
        /// property needs to be present to allow disabling of joining from before
        /// the MeasuredResults object is created.
        /// </summary>
        public bool IsResultsJoiningDisabled { get; private set; }

        public DocumentRetentionTimes DocumentRetentionTimes { get; private set; }

        public bool HasResults { get { return MeasuredResults != null; } }

        public bool HasLibraries { get { return PeptideSettings.Libraries.HasLibraries; } }

        public bool HasDocumentLibrary { get { return PeptideSettings.Libraries.HasDocumentLibrary; } }

        public bool HasRTPrediction { get { return PeptideSettings.Prediction.RetentionTime != null; } }

        public bool HasRTCalcPersisted
        {
            get
            {
                return HasRTPrediction && PeptideSettings.Prediction.RetentionTime.Calculator.PersistencePath != null;
            }
        }

        public bool HasOptimizationLibrary
        {
            get
            {
                return TransitionSettings.Prediction.OptimizedLibrary != null &&
                       !TransitionSettings.Prediction.OptimizedLibrary.IsNone;
            }
        }

        public bool HasOptimizationLibraryPersisted
        {
            get
            {
                return HasOptimizationLibrary && TransitionSettings.Prediction.OptimizedLibrary.PersistencePath != null;
            }
        }

        public bool HasDriftTimePrediction { get { return PeptideSettings.Prediction.DriftTimePredictor != null; } }

        public bool HasIonMobilityLibraryPersisted
        {
            get
            {
                return HasDriftTimePrediction && 
                    PeptideSettings.Prediction.DriftTimePredictor.IonMobilityLibrary != null &&
                    !PeptideSettings.Prediction.DriftTimePredictor.IonMobilityLibrary.IsNone &&
                    PeptideSettings.Prediction.DriftTimePredictor.IonMobilityLibrary.PersistencePath != null;
            }
        }

        public bool HasBackgroundProteome { get { return !PeptideSettings.BackgroundProteome.IsNone; } }

        public RelativeRT GetRelativeRT(IsotopeLabelType labelType, string seq, ExplicitMods mods)
        {
            if (labelType.IsLight)
                return RelativeRT.Matching;
            // Default is matching
            RelativeRT relativeRT = RelativeRT.Matching;
            // One unkown modification makes everything unknown
            // One preceding modification with no unknowns make relative RT preceding
            // Overlapping overrides matching
            if (mods != null && mods.IsModified(labelType))
            {
                foreach (var mod in mods.GetModifications(labelType))
                {
                    if (mod.Modification.RelativeRT == RelativeRT.Unknown)
                        return RelativeRT.Unknown;

                    if (mod.Modification.RelativeRT == RelativeRT.Preceding)
                        relativeRT = RelativeRT.Preceding;
                    else if (mod.Modification.RelativeRT == RelativeRT.Overlapping &&
                             relativeRT == RelativeRT.Matching)
                        relativeRT = RelativeRT.Overlapping;
                }
            }
            else
            {
                foreach (var mod in PeptideSettings.Modifications.GetModifications(labelType))
                {
                    if (!mod.IsMod(seq))
                        continue;
                    if (mod.RelativeRT == RelativeRT.Unknown)
                        return RelativeRT.Unknown;
                    
                    if (mod.RelativeRT == RelativeRT.Preceding)
                        relativeRT = RelativeRT.Preceding;
                    else if (mod.RelativeRT == RelativeRT.Overlapping &&
                             relativeRT == RelativeRT.Matching)
                        relativeRT = RelativeRT.Overlapping;
                }
            }
            return relativeRT;
        }

        // Cached calculators
        private ImmutableList<TypedMassCalc> _precursorMassCalcs;
        private ImmutableList<TypedMassCalc> _fragmentMassCalcs;

        private static SequenceMassCalc GetBaseCalc(IsotopeLabelType labelType,
            ExplicitMods mods, IList<TypedMassCalc> massCalcs)
        {
            if (mods == null)
                return null;

            var calcLightImplicit = massCalcs[0].MassCalc;

            // If the light type is not modified
            if (!mods.IsModified(IsotopeLabelType.light))
            {
                // If requesting the light calculator or an unmodified heavy,
                // then no explicit calculator is required.
                if (labelType.IsLight || !mods.IsModified(labelType))
                    return null;
                
                // Otherwise, use its calculator as the base for a heavy type
                // to make sure the implicit light modifications are included.
                return calcLightImplicit;
            }
            // If the type requested is not modified, it must be a heavy type
            // with the light type modified.  In this case, return the heavy
            // calculator as the base, to which the light explicit modifications
            // may be applied to get modified masses.
            if (!mods.IsModified(labelType))
                return GetMassCalc(labelType, massCalcs);

            // If the light modifications are variable, and this is a type for
            // which explicit modifications exist (including the light type itself),
            // then return the light calculator as base.
            if (mods.IsVariableStaticMods)
                return calcLightImplicit;

            // If both light and this type are modified, then us a base calculator
            // that contains no modifications at all.
            return (calcLightImplicit.MassType == MassType.Monoisotopic ?
                MonoisotopicMassCalc : AverageMassCalc);            
        }

        private static SequenceMassCalc GetMassCalc(IsotopeLabelType labelType, IList<TypedMassCalc> massCalcs)
        {
            int index = massCalcs.IndexOf(calc => ReferenceEquals(labelType, calc.LabelType));
            if (index == -1)
                return null;
            return massCalcs[index].MassCalc;
        }

        public bool HasPrecursorCalc(IsotopeLabelType labelType, ExplicitMods mods)
        {
            if (labelType.IsLight)
                return true;
            return TryGetPrecursorCalc(labelType, mods) != null;
        }

        public IPrecursorMassCalc GetPrecursorCalc(IsotopeLabelType labelType, ExplicitMods mods)
        {
            var precursorCalc =  TryGetPrecursorCalc(labelType, mods);
            if (precursorCalc == null)
            {
                // Try to track down this exception:
                // https://skyline.gs.washington.edu/labkey/announcements/home/issues/exceptions/thread.view?entityId=217d79c8-9a84-1032-ae5f-da2025829168&_anchor=19667#row:19667
                throw new InvalidDataException(
                    String.Format("unable to locate precursor calculator for isotope label type {0} and mods {1}", // Not L10N
                        labelType == null ? "(null)" : labelType.ToString(), // Not L10N
                        mods == null ? "(null)" : mods.ToString())); // Not L10N
            }
            return precursorCalc;
        }

        // For use with small molecules where we don't deal with modifications
        public IPrecursorMassCalc GetDefaultPrecursorCalc()
        {
            return _precursorMassCalcs[0].MassCalc;
        }

        public IPrecursorMassCalc TryGetPrecursorCalc(IsotopeLabelType labelType, ExplicitMods mods)
        {
            var massCalcBase = GetBaseCalc(labelType, mods, _precursorMassCalcs);
            if (massCalcBase != null)
            {
                if (mods == null)
                    return null;
                // If this type is not explicitly modified, then it must be
                // heavy with explicit light modifications.
                if (!mods.IsModified(labelType))
                    labelType = IsotopeLabelType.light;
                if (!labelType.IsLight && !mods.HasModifications(labelType))
                    return null;
                return new ExplicitSequenceMassCalc(mods, massCalcBase, labelType);
            }
            return GetMassCalc(labelType, _precursorMassCalcs);
        }

        public double GetPrecursorMass(IsotopeLabelType labelType, string seq, ExplicitMods mods)
        {
            return GetPrecursorCalc(labelType, mods).GetPrecursorMass(seq);
        }

        public IFragmentMassCalc GetFragmentCalc(IsotopeLabelType labelType, ExplicitMods mods)
        {
            var massCalcBase = GetBaseCalc(labelType, mods, _fragmentMassCalcs);
            if (massCalcBase != null)
            {
                // If this type is not explicitly modified, then it must be
                // heavy with explicit light modifications.
                if (!mods.IsModified(labelType))
                    labelType = IsotopeLabelType.light;
                if (!labelType.IsLight && !mods.HasModifications(labelType))
                    return null;
                return new ExplicitSequenceMassCalc(mods, massCalcBase, labelType);
            }
            return GetMassCalc(labelType, _fragmentMassCalcs);
        }

        /// <summary>
        /// For use with small molecules, where we don't deal with modifications
        /// </summary>
        public IFragmentMassCalc GetDefaultFragmentCalc()
        {
            return _fragmentMassCalcs[0].MassCalc;
        }

        public double GetFragmentMass(IsotopeLabelType labelType, ExplicitMods mods,
                                      Transition transition, IsotopeDistInfo isotopeDist)
        {
            // Return the singly protonated mass of the peptide fragment, or custom ion mass before electron removal
            IFragmentMassCalc calc = GetFragmentCalc(labelType, mods);
            if (calc == null && transition.IsCustom())
            {
                // Small molecules provide their own ion formula, just use the standard calculator
                calc = GetDefaultFragmentCalc();
            }
            if (calc == null)
            {
                Assume.Fail(string.Format("Unable to locate fragment calculator for isotope label type {0} and mods {1}", // Not L10N
                        labelType == null ? "(null)" : labelType.ToString(), // Not L10N
                        mods == null ? "(null)" : mods.ToString())); // Not L10N
                return 0;   // Keep resharper happy
            }
            return calc.GetFragmentMass(transition, isotopeDist);
        }

        public ChromSource GetChromSource(TransitionDocNode nodeTran)
        {
            if (TransitionSettings.FullScan.IsEnabledMs && nodeTran.IsMs1)
                return ChromSource.ms1;
            // TODO: Allow SIM
            return ChromSource.fragment;
        }

        public string GetModifiedSequence(string seq,
                                          IsotopeLabelType labelType,
                                          ExplicitMods mods,
                                          SequenceModFormatType format = SequenceModFormatType.mass_diff,
                                          bool useExplicitModsOnly = false)
        {
            return GetPrecursorCalc(labelType, mods).GetModifiedSequence(seq, format, useExplicitModsOnly);
        }

        public string GetDisplayName(PeptideDocNode nodePep)
        {
            return nodePep.Peptide.IsCustomIon ? nodePep.CustomIon.DisplayName : nodePep.ModifiedSequenceDisplay;
        }

        public string GetModifiedSequence(PeptideDocNode nodePep)
        {
            Assume.IsFalse(nodePep.Peptide.IsCustomIon);
            Assume.IsNotNull(nodePep.ModifiedSequence);
            return nodePep.ModifiedSequence;
        }

        public string GetSourceTextId(PeptideDocNode nodePep)
        {
            Assume.IsNotNull(nodePep.SourceTextId);
            return nodePep.SourceTextId;
        }

        private static readonly SequenceMassCalc MONOISOTOPIC_MASS_CALC = new SequenceMassCalc(MassType.Monoisotopic);

        /// <summary>
        /// Default unmodified <see cref="SequenceMassCalc"/> for monoisotopic masses.
        /// </summary>
        public static SequenceMassCalc MonoisotopicMassCalc { get { return MONOISOTOPIC_MASS_CALC; } }

        private static readonly SequenceMassCalc AVERAGE_MASS_CALC = new SequenceMassCalc(MassType.Average);

        /// <summary>
        /// Default unmodified <see cref="SequenceMassCalc"/> for average masses.
        /// </summary>
        public static SequenceMassCalc AverageMassCalc { get { return AVERAGE_MASS_CALC; } }

        public double GetRegressionMz(PeptideDocNode nodePep, TransitionGroupDocNode nodeGroup)
        {
            double mz = nodeGroup.PrecursorMz;
            // Always use the light m/z value to ensure regression values are consistent between light and heavy
            if (!nodeGroup.TransitionGroup.LabelType.IsLight)
            {
                if (nodePep.Peptide.IsCustomIon)
                {
                    double mass = nodeGroup.TransitionGroup.CustomIon.GetMass(TransitionSettings.Prediction.PrecursorMassType);
                    mz = BioMassCalc.CalculateIonMz(mass, nodeGroup.TransitionGroup.PrecursorCharge);
                }
                else
                {
                    double massH = GetPrecursorMass(IsotopeLabelType.light,
                        nodePep.Peptide.Sequence, nodePep.ExplicitMods);
                    mz = SequenceMassCalc.GetMZ(massH, nodeGroup.TransitionGroup.PrecursorCharge);
                }
            }
            return mz;
        }

        #region Property change methods

        public SrmSettings ChangePeptideSettings(PeptideSettings prop)
        {
            SrmSettings settings = ChangeProp(ImClone(this), im => im.PeptideSettings = prop);

            // If modifications have change, then new mass calculators are needed.
            if (!Equals(prop.Modifications, PeptideSettings.Modifications))
            {
                settings.CreatePrecursorMassCalcs();
                settings.CreateFragmentMassCalcs();
            }

            return settings;
        }

        public SrmSettings ChangeTransitionSettings(TransitionSettings prop)
        {
            // If changing to only MS1 filtering, make sure Integrat All is on
            if (IsOnlyMsEnabled(prop.FullScan) && !IsOnlyMsEnabled(TransitionSettings.FullScan))
            {
                if (!prop.Integration.IsIntegrateAll)
                    prop = prop.ChangeIntegration(prop.Integration.ChangeIntegrateAll(true));
            }

            SrmSettings settings = ChangeProp(ImClone(this), im => im.TransitionSettings = prop);

            if (prop.Prediction.PrecursorMassType != TransitionSettings.Prediction.PrecursorMassType)
                settings.CreatePrecursorMassCalcs();
            if (prop.Prediction.FragmentMassType != TransitionSettings.Prediction.FragmentMassType)
                settings.CreateFragmentMassCalcs();

            return settings;
        }

        private static bool IsOnlyMsEnabled(TransitionFullScan fullScan)
        {
            return fullScan.IsEnabledMs && !fullScan.IsEnabledMsMs;
        }

        public SrmSettings ChangeDataSettings(DataSettings prop)
        {
            return ChangeProp(ImClone(this), im => im.DataSettings = prop);
        }

        public SrmSettings ChangeMeasuredResults(MeasuredResults prop)
        {
            return ChangeProp(ImClone(this), im => im.MeasuredResults = prop);
        }

        public SrmSettings ChangeIsResultsJoiningDisabled(bool prop)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im.IsResultsJoiningDisabled = prop;
                if (HasResults)
                    im.MeasuredResults = im.MeasuredResults.ChangeIsJoiningDisabled(prop);
            });
        }

        public SrmSettings ChangeDocumentRetentionTimes(DocumentRetentionTimes prop)
        {
            return ChangeProp(ImClone(this), im => im.DocumentRetentionTimes = prop);
        }
        
        public SrmSettings MakeSavable()
        {
            return MakeSavable(Name);
        }

        public SrmSettings MakeSavable(string saveName)
        {
            // If the name is already set, and there are no measured results or document library
            // then this instance will do.
            if (Equals(Name, saveName) && MeasuredResults == null && !PeptideSettings.Libraries.HasDocumentLibrary)
                return this;

            // Change the name, and remove results information which is document specific
            SrmSettings settingsSavable = (SrmSettings) ChangeName(saveName);
            settingsSavable = settingsSavable.ChangePeptideLibraries(lib => lib.ChangeDocumentLibrary(false));
            settingsSavable.MeasuredResults = null;
            return settingsSavable;
        }

        #endregion

        private void CreatePrecursorMassCalcs()
        {
            _precursorMassCalcs = CreateMassCalcs(TransitionSettings.Prediction.PrecursorMassType);
        }

        private void CreateFragmentMassCalcs()
        {
            _fragmentMassCalcs = CreateMassCalcs(TransitionSettings.Prediction.FragmentMassType);
        }

        private ImmutableList<TypedMassCalc> CreateMassCalcs(MassType type)
        {
            var calcs = new List<TypedMassCalc>();

            var mods = PeptideSettings.Modifications;

            var modsStatic = mods.StaticModifications;
            calcs.Add(new TypedMassCalc(IsotopeLabelType.light, CreateMassCalc(type, modsStatic)));

            foreach (var typedMods in mods.GetHeavyModifications())
            {
                // Only add a heavy calculator for this type if it contains
                // implicit modifications.
                var modsHeavy = typedMods.Modifications;
                if (modsHeavy.Contains(mod => !mod.IsExplicit))
                {
                    calcs.Add(new TypedMassCalc(typedMods.LabelType,
                        CreateMassCalc(type, modsStatic, modsHeavy)));
                }
            }

            return MakeReadOnly(calcs.ToArray());
        }

        private static SequenceMassCalc CreateMassCalc(MassType type, IEnumerable<StaticMod> staticMods)
        {
            return CreateMassCalc(type, staticMods, null);
        }

        private static SequenceMassCalc CreateMassCalc(MassType type, IEnumerable<StaticMod> staticMods, IEnumerable<StaticMod> heavyMods)
        {
            SequenceMassCalc calc = new SequenceMassCalc(type);
            // Add implicit modifications to the mass calculator
            calc.AddStaticModifications(from mod in staticMods
                                        where !mod.IsExplicit
                                        select mod);
            if (heavyMods != null)
            {
                calc.AddHeavyModifications(from mod in heavyMods
                                           where !mod.IsExplicit
                                           select mod);
            }
            return calc;
        }

        /// <summary>
        /// Cached standard types
        /// </summary>
        private ImmutableDictionary<string, ImmutableList<PeptideDocNode>> _cachedPeptideStandards;
        private static readonly PeptideDocNode[] EMPTY_STANDARDS = new PeptideDocNode[0];

        public IEnumerable<PeptideDocNode> GetPeptideStandards(string standardType)
        {
            ImmutableList<PeptideDocNode> standardPeptides;
            if (_cachedPeptideStandards == null || !_cachedPeptideStandards.TryGetValue(standardType, out standardPeptides))
                return EMPTY_STANDARDS; // So that emptiness is reference equal

            return standardPeptides;
        }

        public SrmSettings CachePeptideStandards(IList<DocNode> peptideGroupDocNodesOrig,
                                                 IList<DocNode> peptideGroupDocNodes)
        {
            // First check to see if anything could have possibly changed to avoid work, if nothing could have
            if (!IsStandardsChange(peptideGroupDocNodesOrig, peptideGroupDocNodes))
                return this;

            // Build an initial mutable dictionay and lists
            var cachedPeptideStandards = new Dictionary<string, IList<PeptideDocNode>>();
            foreach (PeptideGroupDocNode nodePepGroup in peptideGroupDocNodes)
            {
                foreach (var nodePep in nodePepGroup.Molecules)
                {
                    string standardType = nodePep.GlobalStandardType;
                    if (standardType == null)
                        continue;
                    IList<PeptideDocNode> listPeptideAndGroup;
                    if (!cachedPeptideStandards.TryGetValue(standardType, out listPeptideAndGroup))
                    {
                        listPeptideAndGroup = new List<PeptideDocNode>();
                        cachedPeptideStandards.Add(standardType, listPeptideAndGroup);
                    }
                    listPeptideAndGroup.Add(nodePep);
                }
            }
            // Create new read-only lists, if necessary
            bool createdNewList = false;
            var cachedPeptideStandardsRo = new Dictionary<string, ImmutableList<PeptideDocNode>>();
            foreach (var pair in cachedPeptideStandards)
            {
                string standardType = pair.Key;
                var peptidesNew = pair.Value;
                ImmutableList<PeptideDocNode> peptides;
                if (_cachedPeptideStandards == null ||
                    !_cachedPeptideStandards.TryGetValue(standardType, out peptides) ||
                    !ArrayUtil.EqualsDeep(peptides, peptidesNew))
                {
                    peptides = MakeReadOnly(peptidesNew);
                    createdNewList = true;
                }
                cachedPeptideStandardsRo.Add(standardType, peptides);
            }
            // If no new lists and count of standards did not change, then nothing has changed
            if (!createdNewList)
            {
                if (_cachedPeptideStandards == null || _cachedPeptideStandards.Count == cachedPeptideStandardsRo.Count)
                    return this;
            }
            var prop = new ImmutableDictionary<string, ImmutableList<PeptideDocNode>>(cachedPeptideStandardsRo);
            return ChangeProp(ImClone(this), im => im._cachedPeptideStandards = prop);
        }

        /// <summary>
        /// Returns true, if the change could have impacted the Global Standard Type cache.
        /// Used to short-circuit a full recalculation of the cache when nothing could have changed.
        /// </summary>
        private bool IsStandardsChange(IList<DocNode> peptideGroupDocNodesOrig,
                                       IList<DocNode> peptideGroupDocNodes)
        {
            if (peptideGroupDocNodes.Count != peptideGroupDocNodesOrig.Count)
                return true;
            for (int i = 0; i < peptideGroupDocNodes.Count; i++)
            {
                var nodePepGroup = (PeptideGroupDocNode) peptideGroupDocNodes[i];
                var nodePepGroupOrig = (PeptideGroupDocNode) peptideGroupDocNodesOrig[i];
                if (ReferenceEquals(nodePepGroup, nodePepGroupOrig))
                    continue;
                if (!ReferenceEquals(nodePepGroup.Id, nodePepGroupOrig.Id))
                    return true;
                var peptideDocNodes = nodePepGroup.Children;
                var peptideDocNodesOrig = nodePepGroupOrig.Children;
                if (peptideDocNodes.Count != peptideDocNodesOrig.Count)
                    return true;
                for (int j = 0; j < peptideDocNodes.Count; j++)
                {
                    var nodePep = (PeptideDocNode) peptideDocNodes[j];
                    var nodePepOrig = (PeptideDocNode) peptideDocNodesOrig[j];
                    if (ReferenceEquals(nodePep, nodePepOrig))
                        continue;
                    if (!ReferenceEquals(nodePep.Id, nodePepOrig.Id) ||
                        !Equals(nodePep.GlobalStandardType, nodePepOrig.GlobalStandardType))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public bool HasGlobalStandardArea
        {
            get
            {
                return _cachedPeptideStandards != null &&
                    _cachedPeptideStandards.ContainsKey(PeptideDocNode.STANDARD_TYPE_NORMALIZAITON);
            }
        }

        public double CalcGlobalStandardArea(int resultsIndex, ChromFileInfoId fileId)
        {
            double globalStandardArea = 0;
            var peptideStandards = GetPeptideStandards(PeptideDocNode.STANDARD_TYPE_NORMALIZAITON);
            if (peptideStandards != null)
            {
                foreach (var nodeGroup in peptideStandards.SelectMany(nodePep => nodePep.TransitionGroups))
                {
                    var chromInfos = nodeGroup.GetSafeChromInfo(resultsIndex);
                    if (chromInfos == null)
                        continue;
                    foreach (var groupChromInfo in chromInfos)
                    {
                        if (ReferenceEquals(fileId, groupChromInfo.FileId) &&
                                groupChromInfo.OptimizationStep == 0 &&
                                groupChromInfo.Area.HasValue)
                            globalStandardArea += groupChromInfo.Area.Value;
                    }
                }
            }
            return globalStandardArea;
        }

        public bool LibrariesContainMeasurablePeptide(Peptide peptide, IList<int> precursorCharges, ExplicitMods mods)
        {
            if (LibrariesContainMeasurablePeptide(peptide, IsotopeLabelType.light, precursorCharges, mods))
                return true;

            // If light version not found, try heavy
            foreach (var labelType in GetHeavyLabelTypes(mods))
            {                
                if (LibrariesContainMeasurablePeptide(peptide, labelType, precursorCharges, mods))
                    return true;
            }
            return false;
        }

        private bool LibrariesContainMeasurablePeptide(Peptide peptide, IsotopeLabelType labelType,
            IEnumerable<int> precursorCharges, ExplicitMods mods)
        {
            string sequenceMod = GetModifiedSequence(peptide.Sequence, labelType, mods);
            foreach (int charge in precursorCharges)
            {
                if (LibrariesContain(sequenceMod, charge))
                {
                    // Make sure the peptide for the found spectrum is measurable on
                    // the current instrument.
                    double precursorMass = GetPrecursorMass(labelType, peptide.Sequence, mods);
                    if (IsMeasurable(precursorMass, charge))
                        return true;
                }
            }
            return false;
        }

        private bool IsMeasurable(double precursorMass, int charge)
        {
            double precursorMz = SequenceMassCalc.GetMZ(precursorMass, charge);
            return TransitionSettings.IsMeasurablePrecursor(precursorMz);
        }

        public bool LibrariesContain(string sequenceMod, int charge)
        {
            return PeptideSettings.Libraries.Contains(new LibKey(sequenceMod, charge));
        }

        public bool LibrariesContainAny(string sequence)
        {
            return PeptideSettings.Libraries.ContainsAny(new LibSeqKey(sequence));
        }

        public bool TryGetLibInfo(string sequence, int charge, ExplicitMods mods,
            out IsotopeLabelType type, out SpectrumHeaderInfo libInfo)
        {
            if (sequence == null)
            {
                type = null;
                libInfo = null;
                return false;
            }
            var libraries = PeptideSettings.Libraries;
            foreach (var typedSequence in GetTypedSequences(sequence, mods))
            {
                var key = new LibKey(typedSequence.ModifiedSequence, charge);
                if (libraries.TryGetLibInfo(key, out libInfo))
                {
                    type = typedSequence.LabelType;
                    return true;
                }
            }

            type = IsotopeLabelType.light;
            libInfo = null;
            return false;
        }

        public bool TryLoadSpectrum(string sequence, int charge, ExplicitMods mods,
            out IsotopeLabelType type, out SpectrumPeaksInfo spectrum)
        {
            var libraries = PeptideSettings.Libraries;
            foreach (var typedSequence in GetTypedSequences(sequence, mods))
            {
                var key = new LibKey(typedSequence.ModifiedSequence, charge);
                if (libraries.TryLoadSpectrum(key, out spectrum))
                {
                    type = typedSequence.LabelType;
                    return true;
                }
            }

            type = IsotopeLabelType.light;
            spectrum = null;
            return false;
        }

        public bool TryGetRetentionTimes(string sequence, int charge, ExplicitMods mods, MsDataFileUri filePath,
            out IsotopeLabelType type, out double[] retentionTimes)
        {
            var libraries = PeptideSettings.Libraries;
            foreach (var typedSequence in GetTypedSequences(sequence, mods))
            {
                var key = new LibKey(typedSequence.ModifiedSequence, charge);
                if (libraries.TryGetRetentionTimes(key, filePath, out retentionTimes))
                {
                    type = typedSequence.LabelType;
                    return true;
                }
            }

            type = IsotopeLabelType.light;
            retentionTimes = null;
            return false;
        }

        public double[] GetBestRetentionTimes(PeptideDocNode nodePep, MsDataFileUri filePath)
        {
            if (!nodePep.IsProteomic)
                return new double[0]; // No retention time prediction for small molecules

            string lookupSequence = nodePep.SourceUnmodifiedTextId;
            var lookupMods = nodePep.SourceExplicitMods;
            if (filePath != null)
            {
                var times = GetRetentionTimes(filePath, lookupSequence, lookupMods);
                if (times.Length > 0)
                    return times;
                times = GetAllRetentionTimes(filePath, lookupSequence, lookupMods);
                if (times.Length > 0)
                    return times;
            }
            return GetUnalignedRetentionTimes(lookupSequence, lookupMods);
        }

        public double[] GetRetentionTimes(string filePath, string peptideSequence, ExplicitMods explicitMods,
            RetentionTimeAlignmentIndex alignmentIndex = null)
        {
            return GetRetentionTimes(MsDataFileUri.Parse(filePath), peptideSequence, explicitMods, alignmentIndex);
        }

        public double[] GetRetentionTimes(MsDataFileUri filePath, string peptideSequence, ExplicitMods explicitMods,
            RetentionTimeAlignmentIndex alignmentIndex = null)
        {
            string basename = filePath.GetFileNameWithoutExtension();
            var source = DocumentRetentionTimes.RetentionTimeSources.Find(basename);
            if (source == null)
            {
                return new double[0];
            }
            var library = PeptideSettings.Libraries.GetLibrary(source.Library);
            if (library == null)
            {
                return new double[0];
            }
            var modifiedSequences = GetTypedSequences(peptideSequence, explicitMods)
                .Select(typedSequence => typedSequence.ModifiedSequence);

            int? index = (alignmentIndex != null ? alignmentIndex.FileIndex : null);

            var times = library.GetRetentionTimesWithSequences(source.Name, modifiedSequences, ref index).ToArray();

            if (alignmentIndex != null)
                alignmentIndex.FileIndex = index;
            return times;
        }

        public double[] GetAlignedRetentionTimes(MsDataFileUri filePath, string peptideSequence, ExplicitMods explicitMods)
        {
            string basename = filePath.GetFileNameWithoutExtension();
            var fileAlignments = DocumentRetentionTimes.FileAlignments.Find(basename);

            return GetAlignedRetentionTimes(new RetentionTimeAlignmentIndices(fileAlignments), peptideSequence, explicitMods);
        }

        public double[] GetAlignedRetentionTimes(RetentionTimeAlignmentIndices alignmentIndices, string peptideSequence, ExplicitMods explicitMods)
        {
            var times = new List<double>();
            if (alignmentIndices != null)
            {
                foreach (var alignmentIndex in alignmentIndices)
                {
                    var unalignedTimes = GetRetentionTimes(MsDataFileUri.Parse(alignmentIndex.Alignment.Name), peptideSequence, explicitMods, alignmentIndex);
                    foreach (var unalignedTime in unalignedTimes)
                    {
                        var alignedTime = alignmentIndex.Alignment.RegressionLine.GetY(unalignedTime);
                        times.Add(alignedTime);
                    }
                }
            }
            return times.ToArray();
        }

        public double[] GetUnalignedRetentionTimes(string peptideSequence, ExplicitMods explicitMods)
        {
            var times = new List<double>();
            var modifiedSequences = GetTypedSequences(peptideSequence, explicitMods)
                .Select(typedSequence => typedSequence.ModifiedSequence).ToArray();
            foreach (var library in PeptideSettings.Libraries.Libraries)
            {
                if (null == library)
                {
                    continue;
                }
                foreach (var source in library.ListRetentionTimeSources())
                {
                    int? index = null;
                    times.AddRange(library.GetRetentionTimesWithSequences(source.Name, modifiedSequences, ref index));
                }
            }
            return times.ToArray();
        }

        public double[] GetRetentionTimesNotAlignedTo(MsDataFileUri fileNotAlignedTo, string peptideSequence,
            ExplicitMods explicitMods)
        {
            var times = new List<double>();
            string basename = fileNotAlignedTo.GetFileNameWithoutExtension();
            var fileAlignments = DocumentRetentionTimes.FileAlignments.Find(basename);
            var modifiedSequences = GetTypedSequences(peptideSequence, explicitMods)
                .Select(typedSequence => typedSequence.ModifiedSequence).ToArray();

            foreach (var library in PeptideSettings.Libraries.Libraries)
            {
                if (null == library)
                {
                    continue;
                }
                foreach (var source in library.ListRetentionTimeSources())
                {
                    if (MeasuredResults.IsBaseNameMatch(source.Name, basename))
                    {
                        continue;
                    }
                    if (null != fileAlignments)
                    {
                        if (null != fileAlignments.RetentionTimeAlignments.Find(source.Name))
                        {
                            continue;
                        }
                    }
                    int? indexIgnore = null;
                    times.AddRange(library.GetRetentionTimesWithSequences(source.Name, modifiedSequences, ref indexIgnore));
                }
            }
            return times.ToArray();
        }

        public double[] GetAllRetentionTimes(string filePath, string peptideSequence, ExplicitMods explicitMods)
        {
            return GetAllRetentionTimes(MsDataFileUri.Parse(filePath), peptideSequence, explicitMods);
        }

        public double[] GetAllRetentionTimes(MsDataFileUri filePath, string peptideSequence, ExplicitMods explicitMods)
        {
            var times = new List<double>();
            times.AddRange(GetRetentionTimes(filePath, peptideSequence, explicitMods));
            times.AddRange(GetAlignedRetentionTimes(filePath, peptideSequence, explicitMods));
            return times.ToArray();
        }

        private IEnumerable<TypedSequence> GetTypedSequences(string sequence, ExplicitMods mods)
        {
            var labelType = IsotopeLabelType.light;
            string modifiedSequence = GetModifiedSequence(sequence, labelType, mods);
            yield return new TypedSequence(modifiedSequence, labelType);

            foreach (var labelTypeHeavy in GetHeavyLabelTypes(mods))
            {
                modifiedSequence = GetModifiedSequence(sequence, labelTypeHeavy, mods);
                yield return new TypedSequence(modifiedSequence, labelTypeHeavy);
            }
        }

        private struct TypedSequence
        {
            public TypedSequence(string modifiedSequence, IsotopeLabelType labelType)
                : this()
            {
                ModifiedSequence = modifiedSequence;
                LabelType = labelType;
            }

            public string ModifiedSequence { get; private set; }
            public IsotopeLabelType LabelType { get; private set; }
        }

        public LibraryRetentionTimes GetRetentionTimes(MsDataFileUri filePath)
        {
            var libraries = PeptideSettings.Libraries;
            LibraryRetentionTimes retentionTimes;
            if (libraries.TryGetRetentionTimes(filePath, out retentionTimes))
                return retentionTimes;
            return null;
        }

        public LibraryRetentionTimes GetRetentionTimes(string name)
        {
            return GetRetentionTimes(new MsDataFilePath(name));
        }

        public LibraryIonMobilityInfo GetIonMobilities(MsDataFileUri filePath)
        {
            var libraries = PeptideSettings.Libraries;
            LibraryIonMobilityInfo ionMobilities;
            if (libraries.TryGetIonMobilities(filePath, out ionMobilities))
                return ionMobilities;
            return null;
        }

        /// <summary>
        /// Returns the times at which a peptide was found in a particular file.
        /// </summary>
        public double[] GetRetentionTimes(LibraryRetentionTimes retentionTimes, string sequence, ExplicitMods mods)
        {
            return (from typedSequence in GetTypedSequences(sequence, mods)
                    from time in retentionTimes.GetRetentionTimes(typedSequence.ModifiedSequence)
                    select time)
                .ToArray();
        }

        /// <summary>
        /// Loads a list of all non-redundant spectra found in all loaded libraries 
        /// matching the criteria passed in.
        /// </summary>
        /// <param name="sequence"> The sequence to match. </param>
        /// <param name="charge"> The charge to match. </param>
        /// <param name="mods"> The modifications to match. </param>
        /// <returns> Returns a list of the matching spectra. </returns>
        public IEnumerable<SpectrumInfo> GetBestSpectra(string sequence, int charge, ExplicitMods mods)
        {
            var libraries = PeptideSettings.Libraries;
            return from typedSequence in GetTypedSequences(sequence, mods)
                   let key = new LibKey(typedSequence.ModifiedSequence, charge)
                   from spectrumInfo in libraries.GetSpectra(key, typedSequence.LabelType, true)
                   select spectrumInfo;
        }

        /// <summary>
        /// Loads a list of all the spectra found in all loaded libraries 
        /// matching the criteria passed in.
        /// </summary>
        /// <param name="sequence"> The sequence to match. </param>
        /// <param name="charge"> The charge to match. </param>
        /// <param name="labelType">The primary label type to match</param>
        /// <param name="mods"> The modifications to match. </param>
        /// <returns> Returns a list of the matching spectra. </returns>
        public IEnumerable<SpectrumInfo> GetRedundantSpectra(string sequence, int charge, IsotopeLabelType labelType,
                                                       ExplicitMods mods)
        {
            string sequenceMod = GetModifiedSequence(sequence, labelType, mods);
            return PeptideSettings.Libraries.GetSpectra(new LibKey(sequenceMod, charge), labelType, false);
        }

        private IEnumerable<IsotopeLabelType> GetHeavyLabelTypes(ExplicitMods mods)
        {
            foreach (var typedMods in PeptideSettings.Modifications.GetHeavyModifications())
            {
                IsotopeLabelType labelType = typedMods.LabelType;
                if (HasPrecursorCalc(labelType, mods))
                    yield return labelType;
            }
        }

        #region Implementation of IPeptideFilter

        public bool Accept(SrmSettings settings, Peptide peptide, ExplicitMods explicitMods, out bool allowVariableMods)
        {
            return Accept(settings,
                          peptide,
                          explicitMods,
                          TransitionSettings.Filter.PrecursorCharges,
                          PeptideFilterType.fasta,
                          out allowVariableMods);
        }

        /// <summary>
        /// Returns true if a peptide sequence would yield any usable <see cref="PeptideDocNode"/>
        /// elements with the current filter settings, taking into account variable modifications.
        /// </summary>
        public bool Accept(string peptideSequence, int missedCleavages)
        {
            var peptide = new Peptide(null, peptideSequence, null, null, missedCleavages);
            var enumDocNodes = peptide.CreateDocNodes(this, this);
            return enumDocNodes.GetEnumerator().MoveNext();
        }

        public bool Accept(string peptideSequence)
        {
            int missedCleavages = PeptideSettings.Enzyme.CountCleavagePoints(peptideSequence);
            return missedCleavages <= PeptideSettings.DigestSettings.MaxMissedCleavages
                   && Accept(peptideSequence, missedCleavages);
        }

        /// <summary>
        /// This version of Accept is used to select transition groups after the peptide
        /// itself has already been screened.  For this reason, it only applies library
        /// filtering.
        /// </summary>
        public bool Accept(SrmSettings settings, Peptide peptide, ExplicitMods mods, int charge)
        {
            bool allowVariableMods;
            return Accept(settings, peptide, mods, new[] { charge }, PeptideFilterType.library, out allowVariableMods);
        }

        private enum PeptideFilterType
        {
            full,   // Filter all peptides with both filter and library settings
            fasta,  // Apply filter settings only to peptides with an associated FASTA sequence
            library // Only filter with library settings
        }

        private bool Accept(SrmSettings settings,
                            Peptide peptide,
                            ExplicitMods mods,
                            IList<int> precursorCharges,
                            PeptideFilterType filterType,
                            out bool allowVariableMods)
        {
            // Assume variable modifications are not allowed until proven otherwise
            allowVariableMods = false;
            // Only filter user specified peptides based on the heuristic
            // filter when explicitly requested.
            bool useFilter = filterType == PeptideFilterType.full;
            if (filterType == PeptideFilterType.fasta)
                useFilter = peptide.Begin.HasValue;

            var libraries = PeptideSettings.Libraries;
            if (!libraries.HasLibraries || libraries.Pick == PeptidePick.filter)
            {
                if (!useFilter)
                {
                    allowVariableMods = true;
                    return true;
                }
                return PeptideSettings.Filter.Accept(settings, peptide, mods, out allowVariableMods);
            }

            // Check if the peptide is in the library for one of the
            // acceptable precursor charges.
            bool inLibrary = false;
            // If the libraries are not fully loaded, then act like nothing
            // could be found in the libraries.  This will be corrected when
            // the libraries are loaded.
            if (libraries.IsLoaded && 
                // Only check the library, if this is a custom ion or a peptide that already has
                // a variable modification, or the library contains some form of the peptide.
                // This is a performance improvement over checking every variable modification
                // of a peptide when it is not even in the library.
                (peptide.IsCustomIon || (mods != null && mods.IsVariableStaticMods) || LibrariesContainAny(peptide.Sequence)))
            {
                // Only allow variable modifications, if the peptide has no modifications
                // or already checking variable modifications, and there is reason to check
                // the library.  Failing to do this check profiled as a performance bottleneck.
                allowVariableMods = mods == null || mods.IsVariableStaticMods;

                inLibrary = LibrariesContainMeasurablePeptide(peptide, precursorCharges, mods);
            }

            switch (libraries.Pick)
            {
                case PeptidePick.library:
                    return inLibrary;
                case PeptidePick.both:
                    return inLibrary && (!useFilter || PeptideSettings.Filter.Accept(settings, peptide, mods, out allowVariableMods));
                default:
                    return inLibrary || (!useFilter || PeptideSettings.Filter.Accept(settings, peptide, mods, out allowVariableMods));
            }
        }

        public int? MaxVariableMods
        {
            get { return PeptideSettings.Modifications.MaxVariableMods; }
        }

        #endregion

        public void UpdateLists(string documentFilePath)
        {
            Settings defSet = Settings.Default;

            // Make sure all settings are contained in the appropriate lists.
            // CONSIDER: Simple List.Contains() checks mean that values with the same name
            //           but differing values will be overwritten.
            if (!defSet.EnzymeList.Contains(PeptideSettings.Enzyme))
                defSet.EnzymeList.Add(PeptideSettings.Enzyme);
            // Extra null checks to avoid ReSharper warnings.
            if (PeptideSettings.Prediction != null)
            {
                if (PeptideSettings.Prediction.RetentionTime != null)
                {
                    if (!defSet.RetentionTimeList.Contains(PeptideSettings.Prediction.RetentionTime))
                        defSet.RetentionTimeList.Add(PeptideSettings.Prediction.RetentionTime);
                    if (!defSet.RTScoreCalculatorList.Contains(PeptideSettings.Prediction.RetentionTime.Calculator))
                        defSet.RTScoreCalculatorList.Add(PeptideSettings.Prediction.RetentionTime.Calculator);
                }
                if (PeptideSettings.Prediction.DriftTimePredictor != null)
                {
                    if (!defSet.DriftTimePredictorList.Contains(PeptideSettings.Prediction.DriftTimePredictor))
                        defSet.DriftTimePredictorList.Add(PeptideSettings.Prediction.DriftTimePredictor);
                    if (PeptideSettings.Prediction.DriftTimePredictor.IonMobilityLibrary != null &&
                        !defSet.IonMobilityLibraryList.Contains(PeptideSettings.Prediction.DriftTimePredictor.IonMobilityLibrary))
                    {
                        defSet.IonMobilityLibraryList.Add(PeptideSettings.Prediction.DriftTimePredictor.IonMobilityLibrary);
                    }
                }
            }
            if (PeptideSettings.Filter != null)
            {
                foreach (PeptideExcludeRegex exclude in PeptideSettings.Filter.Exclusions)
                {
                    if (!defSet.PeptideExcludeList.Contains(exclude))
                        defSet.PeptideExcludeList.Add(exclude);
                }
            }
            // First remove all old document local specs.
            defSet.SpectralLibraryList.RemoveDocumentLocalLibraries();
            // Then add any specs belonging to this document.
            if (PeptideSettings.Libraries.HasLibraries)
            {
                foreach (LibrarySpec librarySpec in PeptideSettings.Libraries.LibrarySpecs)
                {
                    if (librarySpec != null && !defSet.SpectralLibraryList.Contains(librarySpec))
                        defSet.SpectralLibraryList.Add(librarySpec);
                }
            }

            // If this document has a document library, add it to the specs list
            if (PeptideSettings.Libraries.HasDocumentLibrary)
            {
                string outputPath = Path.ChangeExtension(documentFilePath, BiblioSpecLiteSpec.EXT);
                if (File.Exists(outputPath))
                {
                    string docFileName = Path.GetFileNameWithoutExtension(documentFilePath);
                    var documentLibSpec = new BiblioSpecLiteSpec(docFileName, outputPath);
                    if (!defSet.SpectralLibraryList.Contains(documentLibSpec))
                    {
                        defSet.SpectralLibraryList.Add(documentLibSpec.ChangeDocumentLibrary(true));
                    }
                }
            }

            if (PeptideSettings.Integration.PeakScoringModel != null &&
                !Equals(PeptideSettings.Integration.PeakScoringModel.Name, LegacyScoringModel.DEFAULT_NAME))
            {
                defSet.PeakScoringModelList.Add(PeptideSettings.Integration.PeakScoringModel);
            }

            UpdateDefaultModifications(true, true);
            if (TransitionSettings.Prediction != null)
            {
                TransitionPrediction prediction = TransitionSettings.Prediction;
                if (!defSet.CollisionEnergyList.Contains(prediction.CollisionEnergy))
                    defSet.CollisionEnergyList.Add(prediction.CollisionEnergy);
                if (prediction.DeclusteringPotential != null &&
                        !defSet.DeclusterPotentialList.Contains(prediction.DeclusteringPotential))
                    defSet.DeclusterPotentialList.Add(prediction.DeclusteringPotential);
                if (prediction.CompensationVoltage != null &&
                        !defSet.CompensationVoltageList.Contains(prediction.CompensationVoltage))
                    defSet.CompensationVoltageList.Add(prediction.CompensationVoltage);
                if (!Equals(prediction.OptimizedLibrary, OptimizationLibrary.NONE) &&
                        Equals(defSet.GetOptimizationLibraryByName(prediction.OptimizedLibrary.Name), OptimizationLibrary.NONE))
                    defSet.OptimizationLibraryList.Add(prediction.OptimizedLibrary);
            }
            if (TransitionSettings.Filter != null)
            {
                foreach (var measuredIon in TransitionSettings.Filter.MeasuredIons)
                {
                    if (!defSet.MeasuredIonList.Contains(measuredIon))
                        defSet.MeasuredIonList.Add(measuredIon);
                }
            }
            if (TransitionSettings.FullScan.IsotopeEnrichments != null)
            {
                if (!defSet.IsotopeEnrichmentsList.ContainsKey(TransitionSettings.FullScan.IsotopeEnrichments.Name))
                {
                    defSet.IsotopeEnrichmentsList.Add(TransitionSettings.FullScan.IsotopeEnrichments);
                }
            }
            if (TransitionSettings.FullScan.IsolationScheme != null)
            {
                if (!defSet.IsolationSchemeList.ContainsKey(TransitionSettings.FullScan.IsolationScheme.Name))
                {
                    defSet.IsolationSchemeList.Add(TransitionSettings.FullScan.IsolationScheme);
                }
            }
            foreach (var annotationDef in DataSettings.AnnotationDefs)
            {
                if (!defSet.AnnotationDefList.Contains(annotationDef))
                {
                    defSet.AnnotationDefList.Add(annotationDef);
                }
            }
            foreach (var groupComparisonDef in DataSettings.GroupComparisonDefs)
            {
                if (!defSet.GroupComparisonDefList.Contains(groupComparisonDef))
                {
                    defSet.GroupComparisonDefList.Add(groupComparisonDef);
                }
            }
            var mainViewSpecList = defSet.PersistedViews.GetViewSpecList(PersistedViews.MainGroup.Id);
            foreach (var viewSpec in DataSettings.ViewSpecList.ViewSpecs)
            {
                mainViewSpecList = mainViewSpecList.ReplaceView(viewSpec.Name, viewSpec);
            }
            defSet.PersistedViews.SetViewSpecList(PersistedViews.MainGroup.Id, mainViewSpecList);
            if (!PeptideSettings.BackgroundProteome.IsNone)
            {
                if (!defSet.BackgroundProteomeList.ContainsKey(PeptideSettings.BackgroundProteome.Name))
                {
                    defSet.BackgroundProteomeList.Add(PeptideSettings.BackgroundProteome);
                }
            }
        }

        public SrmSettings ConnectIrtDatabase(Func<RCalcIrt, RCalcIrt> findCalculatorSpec)
        {
            if (PeptideSettings.Prediction.RetentionTime == null)
                return this;

            var iRTCalc = PeptideSettings.Prediction.RetentionTime.Calculator as RCalcIrt;
            if (iRTCalc == null)
                return this;

            var iRTCalcNew = findCalculatorSpec(iRTCalc);
            if (iRTCalcNew == null)
            {
                // cancel
                return null;
            }
            if (iRTCalcNew.DatabasePath == iRTCalc.DatabasePath)
            {
                return this;
            }

            return this.ChangePeptidePrediction(predict =>
                predict.ChangeRetentionTime(!iRTCalcNew.IsNone
                    ? predict.RetentionTime.ChangeCalculator(iRTCalcNew)
                    : null));
        }

        public SrmSettings ConnectOptimizationDatabase(Func<OptimizationLibrary, OptimizationLibrary> findLibrarySpec)
        {
            var lib = TransitionSettings.Prediction.OptimizedLibrary;
            if (lib == null)
                return this;

            var libNew = findLibrarySpec(lib);
            if (libNew == null)
            {
                // cancel
                return null;
            }
            if (libNew.DatabasePath == lib.DatabasePath)
            {
                return this;
            }

            return this.ChangeTransitionPrediction(predict =>
                predict.ChangeOptimizationLibrary(!libNew.IsNone ? libNew : OptimizationLibrary.NONE));
        }

        public SrmSettings ConnectIonMobilityLibrary(Func<IonMobilityLibrarySpec, IonMobilityLibrarySpec> findIonMobilityLibSpec)
        {
            if (PeptideSettings.Prediction.DriftTimePredictor == null)
                return this;

            var ionMobilityLibrary = PeptideSettings.Prediction.DriftTimePredictor.IonMobilityLibrary;
            if (ionMobilityLibrary == null)
                return this;

            var ionMobilityLibSpec = findIonMobilityLibSpec(ionMobilityLibrary);
            if (ionMobilityLibSpec == null)
            {
                // cancel
                return null;
            }
            if (ionMobilityLibSpec.PersistencePath == ionMobilityLibrary.PersistencePath)
            {
                return this;
            }

            return this.ChangePeptidePrediction(predict =>
                predict.ChangeDriftTimePredictor(!ionMobilityLibSpec.IsNone
                    ? predict.DriftTimePredictor.ChangeLibrary(ionMobilityLibSpec)
                    : null));
        }

        public SrmSettings ConnectLibrarySpecs(Func<Library, LibrarySpec> findLibrarySpec, string docLibPath = null)
        {
            var libraries = PeptideSettings.Libraries;
            bool hasDocLib = libraries.HasDocumentLibrary && null != docLibPath;
            if (!libraries.HasLibraries && !hasDocLib)
                return this;

            int len = libraries.Libraries.Count;
            int docLibShift = hasDocLib ? 1 : 0;
            LibrarySpec[] librarySpecs = new LibrarySpec[len + docLibShift];
            for (int i = 0; i < len; i++)
            {
                int iSpec = i + docLibShift;
                var library = libraries.Libraries[i];
                if (library == null)
                {
                    librarySpecs[iSpec] = libraries.LibrarySpecs[i];
                    if (librarySpecs[iSpec] == null)
                        throw new InvalidDataException(Resources.SrmSettings_ConnectLibrarySpecs_Settings_missing_library_spec);
                    continue;
                }

                librarySpecs[iSpec] = findLibrarySpec(library);
                if (librarySpecs[iSpec] == null)
                    return null;    // Canceled
                if (librarySpecs[iSpec].FilePath == null)
                {
                    // Disconnect the libraries, if not canceled, but no path
                    // specified.
                    return ChangePeptideSettings(PeptideSettings.ChangeLibraries(libraries.Disconnect()));
                }
            }

            if (hasDocLib)
            {
                string docLibName = Path.GetFileNameWithoutExtension(docLibPath);
                librarySpecs[0] = new BiblioSpecLiteSpec(docLibName, docLibPath).ChangeDocumentLibrary(true);
            }

            if (ArrayUtil.EqualsDeep(librarySpecs, libraries.LibrarySpecs))
                return this;

            libraries = libraries.ChangeLibrarySpecs(librarySpecs);
            return ChangePeptideSettings(PeptideSettings.ChangeLibraries(libraries));
        }

        public SrmSettings ConnectBackgroundProteome(Func<BackgroundProteomeSpec, BackgroundProteomeSpec> findBackgroundProteome)
        {
            var backgroundProteome = PeptideSettings.BackgroundProteome;
            if (backgroundProteome.IsNone)
            {
                return this;
            }
            var backgroundProteomeSpecNew = findBackgroundProteome(backgroundProteome);
            if (backgroundProteomeSpecNew == null)
            {
                // cancel
                return null;
            }
            if (backgroundProteomeSpecNew.DatabasePath == backgroundProteome.DatabasePath)
            {
                return this;
            }
            return ChangePeptideSettings(PeptideSettings.ChangeBackgroundProteome(
                new BackgroundProteome(backgroundProteomeSpecNew)));
        }

        public void UpdateDefaultModifications(bool overwrite)
        {
            UpdateDefaultModifications(overwrite, false);
        }

        public void UpdateDefaultModifications(bool overwrite, bool allowVariableOverwrite)
        {
            var defSet = Settings.Default;

            IList<StaticMod> newStaticMods = new List<StaticMod>();
            IList<StaticMod> newHeavyMods = new List<StaticMod>();

            foreach (var mod in PeptideSettings.Modifications.GetModifications(IsotopeLabelType.light))
            {
                if (!defSet.StaticModList.Contains(mod.ChangeExplicit(true)) && 
                    // If the list contains the mod as an implicit mod, it can be overwritten with a variable mod.
                    (!defSet.StaticModList.Contains(mod.ChangeExplicit(false)) || (mod.IsVariable && allowVariableOverwrite)) &&
                    // A variable modification set explicitly, can show up as explicit only in a document.
                    // This condition makes sure it doesn't overwrite the existing variable mod.
                    (!mod.IsExplicit || !defSet.StaticModList.Contains(mod.ChangeVariable(true))))
                {
                    newStaticMods.Add(mod.IsUserSet ? mod.ChangeExplicit(false) : mod);
                    if (!overwrite)
                    {
                        var modName = mod.Name;
                        foreach (StaticMod existingMod in defSet.StaticModList)
                        {
                            if (Equals(existingMod.Name, modName))
                            {
                                throw new InvalidDataException(
                                    string.Format(Resources.SrmSettings_UpdateDefaultModifications_The_modification__0__already_exists_with_a_different_definition,
                                                  modName));
                            }
                        }
                    }
                }
            }

            foreach (var typedMods in PeptideSettings.Modifications.GetHeavyModifications())
            {
                foreach (StaticMod mod in typedMods.Modifications)
                {
                    if (!defSet.HeavyModList.Contains(mod.ChangeExplicit(false)) && !defSet.HeavyModList.Contains(mod.ChangeExplicit(true)))
                    {
                        newHeavyMods.Add(mod.IsExplicit ? mod.ChangeExplicit(false) : mod);
                        if (!overwrite)
                        {
                            var modName = mod.Name;
                            foreach (StaticMod existingMod in defSet.HeavyModList)
                            {
                                if (Equals(existingMod.Name, modName))
                                {
                                    throw new InvalidDataException(
                                        string.Format(Resources.SrmSettings_UpdateDefaultModifications_The_modification__0__already_exists_with_a_different_definition,
                                                      modName));
                                }
                            }
                        }
                    }
                }
            }
            foreach(StaticMod mod in newStaticMods)
                defSet.StaticModList.Add(mod);
            foreach(StaticMod mod in newHeavyMods)
                defSet.HeavyModList.Add(mod);
         }

        /// <summary>
        /// Returns true if any of the runs in this Document have been successfully aligned 
        /// with any other run which has peptide ID times.
        /// </summary>
        public bool HasAlignedTimes()
        {
            return
                DocumentRetentionTimes.FileAlignments.Values.Any(
                    fileRetentionTimeAlignments => fileRetentionTimeAlignments.RetentionTimeAlignments.Count > 0);
        }

        /// <summary>
        /// Returns true if there are any runs in this Document that have not been aligned against
        /// all of the runs in the Libraries in this Document.
        /// </summary>
        public bool HasUnalignedTimes()
        {
            if (!HasResults)
            {
                return false;
            }
            foreach (var chromatogramSet in MeasuredResults.Chromatograms)
            {
                foreach (var msDataFileInfo in chromatogramSet.MSDataFileInfos)
                {
                    var fileAlignments = DocumentRetentionTimes.FileAlignments.Find(msDataFileInfo);
                    if (fileAlignments == null)
                    {
                        return true;
                    }
                    foreach (var source in DocumentRetentionTimes.RetentionTimeSources.Values)
                    {
                        if (source.Name == fileAlignments.Name)
                        {
                            continue;
                        }
                        if (null == fileAlignments.RetentionTimeAlignments.Find(source.Name))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }


        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private SrmSettings()
        {
        }

        /// <summary>
        /// Makes sure settings loaded from XML are complete, and uses default
        /// values where anything is missing.
        /// </summary>
        private void ValidateLoad()
        {
            SrmSettings defaults = SrmSettingsList.GetDefault();
            PeptideSettings defPep = defaults.PeptideSettings;
            if (PeptideSettings == null)
                PeptideSettings = defPep;
            else
            {
                PeptideSettings = PeptideSettings.MergeDefaults(defPep);
            }

            TransitionSettings defTran = defaults.TransitionSettings;
            if (TransitionSettings == null)
                TransitionSettings = defTran;
            else
            {
                TransitionPrediction prediction = TransitionSettings.Prediction;
                // Backward compatibility: handle the move of RetentionTime
                // from TransitionSettings to PeptideSettings after v0.1.0.0
                if (TransitionSettings.Prediction == null)
                    prediction = defTran.Prediction;
                else if (prediction.RetentionTime != null)
                {
                    if (PeptideSettings.Prediction != null && // Make Resharper happy
                        PeptideSettings.Prediction.RetentionTime == null)
                    {
                        PeptideSettings = PeptideSettings.ChangePrediction(
                            new PeptidePrediction(prediction.RetentionTime));
                    }
                    prediction = new TransitionPrediction(prediction);
                }

                TransitionFilter filter = TransitionSettings.Filter ?? defTran.Filter;
                TransitionLibraries libraries = TransitionSettings.Libraries ?? defTran.Libraries;
                TransitionIntegration integration = TransitionSettings.Integration ?? defTran.Integration;
                TransitionInstrument instrument = TransitionSettings.Instrument ?? defTran.Instrument;
                TransitionFullScan fullScan = TransitionSettings.FullScan ?? defTran.FullScan;
                // Backward compatibility with v2.1, get a RT filter length from peptide prediction settings
                if (fullScan.RetentionTimeFilterType == RetentionTimeFilterType.scheduling_windows &&
                    fullScan.RetentionTimeFilterLength == 0 &&
                    PeptideSettings.Prediction != null)
                {
                    double rtFilterLen = 0;
                    if (PeptideSettings.Prediction.UseMeasuredRTs)
                    {
                        rtFilterLen = PeptideSettings.Prediction.MeasuredRTWindow.Value / 2;
                    }
                    else if (PeptideSettings.Prediction.RetentionTime != null)
                    {
                        rtFilterLen = PeptideSettings.Prediction.RetentionTime.TimeWindow / 2;
                    }
                    if (rtFilterLen > 0)
                    {
                        fullScan = fullScan.ChangeRetentionTimeFilter(fullScan.RetentionTimeFilterType, rtFilterLen);
                    }
                }
                TransitionSettings transitionSettings = new TransitionSettings(prediction,
                                                                               filter,
                                                                               libraries,
                                                                               integration,
                                                                               instrument,
                                                                               fullScan);
                // If the above null checks result in a changed PeptideSettings object,
                // then use the changed version.
                if (!Equals(TransitionSettings, transitionSettings))
                    TransitionSettings = transitionSettings;
            }

            // Initialize mass calculators
            CreatePrecursorMassCalcs();
            CreateFragmentMassCalcs();
        }

        public static SrmSettings Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new SrmSettings());
        }

        public override void ReadXml(XmlReader reader)
        {
            // Read tag attributes
            base.ReadXml(reader);
            reader.ReadStartElement();
            PeptideSettings = reader.DeserializeElement<PeptideSettings>();
            TransitionSettings = reader.DeserializeElement<TransitionSettings>();

            // 10.23.12 -- The order of <measured_results> and <data_settings> has been switched to enable parsing (in Panorama)
            // of all annotation definitions before reading any replicate annotations in <measured_results>.
            // We want Skyline to be able to read older documents where <measured_results> come before <data_settings>
            if (reader.IsStartElement(new XmlElementHelper<MeasuredResults>().ElementNames[0]))
            {
                MeasuredResults = reader.DeserializeElement<MeasuredResults>();
                DataSettings = reader.DeserializeElement<DataSettings>() ?? DataSettings.DEFAULT;   
            }
            else
            {
                DataSettings = reader.DeserializeElement<DataSettings>() ?? DataSettings.DEFAULT;
                MeasuredResults = reader.DeserializeElement<MeasuredResults>();
            }
            
            DocumentRetentionTimes = reader.DeserializeElement<DocumentRetentionTimes>() ?? DocumentRetentionTimes.EMPTY;
            reader.ReadEndElement();
            ValidateLoad();
        }

        public override void WriteXml(XmlWriter writer)
        {
            // Write tag attributes
            base.WriteXml(writer);
            writer.WriteElement(PeptideSettings);
            writer.WriteElement(TransitionSettings);
            writer.WriteElement(DataSettings);
            if (MeasuredResults != null)
                writer.WriteElement(MeasuredResults);
            if (!DocumentRetentionTimes.IsEmpty)
                writer.WriteElement(DocumentRetentionTimes);
        }

        #endregion

        #region object overrides

        public bool Equals(SrmSettings obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return base.Equals(obj) &&
                Equals(obj.PeptideSettings, PeptideSettings) &&
                Equals(obj.TransitionSettings, TransitionSettings) &&
                Equals(obj.DataSettings, DataSettings) &&
                Equals(obj.MeasuredResults, MeasuredResults) &&
                Equals(obj.DocumentRetentionTimes, DocumentRetentionTimes);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as SrmSettings);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = base.GetHashCode();
                result = (result*397) ^ PeptideSettings.GetHashCode();
                result = (result*397) ^ TransitionSettings.GetHashCode();
                result = (result*397) ^ DataSettings.GetHashCode();
                result = (result*397) ^ (MeasuredResults != null ? MeasuredResults.GetHashCode() : 0);
                result = (result*397) ^ DocumentRetentionTimes.GetHashCode();
                return result;
            }
        }

        #endregion
    }

    /// <summary>
    /// Enum used to specify the representation of modifications in a sequence
    /// </summary>
    public enum SequenceModFormatType { mass_diff, mass_diff_narrow, three_letter_code };

    public interface IPrecursorMassCalc
    {
        MassType MassType { get; }
        double GetPrecursorMass(string seq);
        double GetPrecursorMass(CustomIon customIon);
        bool IsModified(string seq);
        string GetModifiedSequence(string seq, bool formatNarrow);
        string GetModifiedSequence(string seq, SequenceModFormatType format, bool explicitModsOnly);
        double GetAAModMass(char aa, int seqIndex, int seqLength);
        MassDistribution GetMzDistribution(string seq, int charge, IsotopeAbundances abundances);
        MassDistribution GetMZDistributionFromFormula(string formula, int charge, IsotopeAbundances abundances);
        MassDistribution GetMZDistributionSinglePoint(double mz, int charge);
        string GetIonFormula(string peptideSequence, int charge);
    }

    public interface IFragmentMassCalc
    {
        MassType MassType { get; }
        double[,] GetFragmentIonMasses(string seq);
        double GetFragmentMass(Transition transition, IsotopeDistInfo isotopeDist);
        double GetPrecursorFragmentMass(string seq);
    }

    public class SrmSettingsChangeMonitor : IDisposable
    {
        private readonly IProgressMonitor _progressMonitor;
        private readonly string _formatString;
        private readonly IDocumentContainer _documentContainer;
        private readonly SrmDocument _startDocument;

        private ProgressStatus _status;
        private int _groupCount;
        private int? _moleculeCount;
        private int _seenGroupCount;
        private int _seenMoleculeCount;

        public SrmSettingsChangeMonitor(IProgressMonitor progressMonitor, string formatString,
            IDocumentContainer documentContainer = null, SrmDocument startDocument = null)
        {
            _progressMonitor = progressMonitor;
            _documentContainer = documentContainer;
            _startDocument = startDocument;
            if (_startDocument == null && documentContainer != null)
                _startDocument = documentContainer.Document;

            if (!formatString.Contains('{'))
                _status = new ProgressStatus(formatString);
            else
            {
                _formatString = formatString;
                // Set status string to empty, since it should be reset very quickly
                _status = new ProgressStatus(string.Empty);
            }
        }

        public void ProcessGroup(PeptideGroupDocNode nodeGroup)
        {
            bool messageChange = _formatString != null;
            if (messageChange)
                _status = _status.ChangeMessage(string.Format(_formatString, nodeGroup.Name));
            int? percentComplete = ProgressStatus.ThreadsafeIncementPercent(ref _seenGroupCount, GroupCount);
            UpdateProgress(percentComplete, messageChange);
        }

        public void ProcessMolecule(PeptideDocNode nodePep)
        {
            int? percentComplete = ProgressStatus.ThreadsafeIncementPercent(ref _seenMoleculeCount, MoleculeCount);
            UpdateProgress(percentComplete, false);
        }

        private void UpdateProgress(int? percentComplete, bool forceUpdate)
        {
            // Stop processing if the document changes, since the SetDocument call will fail
            if (_progressMonitor.IsCanceled || (_documentContainer != null && !ReferenceEquals(_startDocument, _documentContainer.Document)))
                throw new OperationCanceledException();

            if (percentComplete.HasValue)
                ChangeProgress(status => status.ChangePercentComplete(percentComplete.Value));
            else if (forceUpdate)
                ChangeProgress(status => status);
        }

        public void ChangeProgress(Func<ProgressStatus, ProgressStatus> change)
        {
            var status = _status;
            var statusNew = change(status);
            if (ReferenceEquals(status, Interlocked.CompareExchange(ref _status, statusNew, status)))
                _progressMonitor.UpdateProgress(statusNew);
        }

        public void Dispose()
        {
            if (_seenGroupCount + _seenMoleculeCount > 0)
                _progressMonitor.UpdateProgress(_status = _status.Complete());
        }

        public int GroupCount
        {
            get {  return _groupCount; }
            // Avoid divide by zero errors by always having at least 1 group
            set { _groupCount = value != 0 ? value : 1; }
        }

        public int? MoleculeCount
        {
            get { return _moleculeCount; }
            // Avoid divied by zero errors by using null when molecule count is zero
            set { _moleculeCount = value.HasValue && value > 0 ? value : null; }
        }
    }
    /// <summary>
    /// A class that allows pre-calculation of settings changes for use
    /// in updating the <see cref="DocNode"/> tree in a <see cref="SrmDocument"/>
    /// when settings change.
    /// <para>
    /// This calculation is the core of making sure the document accurately
    /// reflects changes to the settings without causing unnecessary automated
    /// recalculation of parts of the document that may have been manually
    /// tuned.
    /// </para><para>
    /// All changes to this class should get thorough unit testing.
    /// </para>
    /// </summary>
    public class SrmSettingsDiff
    {
// ReSharper disable InconsistentNaming
        public static SrmSettingsDiff ALL
        {
            get { return new SrmSettingsDiff(); }
        }

        public static SrmSettingsDiff PROPS
        {
            get
            {
                return new SrmSettingsDiff(false)
                { DiffPeptideProps = true, DiffTransitionGroupProps = true, DiffTransitionProps = true };
            }
        }
// ReSharper restore InconsistentNaming

        private readonly bool _isUnexplainedExplicitModificationAllowed;

        /// <summary>
        /// For use in creating new nodes, where everything should be created
        /// from scratch with the current settings.
        /// </summary>
        public SrmSettingsDiff()
            : this(true)
        {
            
        }

        public SrmSettingsDiff(bool allOn)
        {
            if (!allOn)
                return;

            DiffPeptides = true;
            DiffPeptideProps = true;
            DiffTransitionGroups = true;
            DiffTransitionGroupProps = true;
            DiffTransitions = true;
            DiffTransitionProps = true;
            DiffResults = true;
        }

        public SrmSettingsDiff(SrmSettingsDiff diff, SrmSettingsDiff diffUnion)
        {
            DiffPeptides = diff.DiffPeptides || diffUnion.DiffPeptides;
            DiffPeptideProps = diff.DiffPeptideProps || diffUnion.DiffPeptideProps;
            DiffTransitionGroups = diff.DiffTransitionGroups || diffUnion.DiffTransitionGroups;
            DiffTransitionGroupProps = diff.DiffTransitionGroupProps || diffUnion.DiffTransitionGroupProps;
            DiffTransitions = diff.DiffTransitions || diffUnion.DiffTransitions;
            DiffTransitionProps = diff.DiffTransitionProps || diffUnion.DiffTransitionProps;
            DiffResults = diff.DiffResults || diffUnion.DiffResults;
            DiffResultsAll = diff.DiffResultsAll || diffUnion.DiffResultsAll;
            SettingsOld = diff.SettingsOld;
        }

        public SrmSettingsDiff(bool diffPeptides, bool diffPeptideProps,
            bool diffTransitionGroups, bool diffTransitionGroupProps,
            bool diffTransitions, bool diffTransitionProps)
        {
            DiffPeptides = diffPeptides;
            DiffPeptideProps = diffPeptideProps;
            DiffTransitionGroups = diffTransitionGroups;
            DiffTransitionGroupProps = diffTransitionGroupProps;
            DiffTransitions = diffTransitions;
            DiffTransitionProps = diffTransitionProps;
        }

        /// <summary>
        /// Used for changes that involve only results changes.  Usually used to force recalculation of
        /// all results information.
        /// </summary>
        /// <param name="settingsCurrent">The current settings which are used as <see cref="SettingsOld"/></param>
        /// <param name="allResults">True if all results information should be recalculated</param>
        public SrmSettingsDiff(SrmSettings settingsCurrent, bool allResults)
        {
            SettingsOld = settingsCurrent;

            DiffResults = true;
            DiffResultsAll = allResults;
        }

        /// <summary>
        /// Calculates the differences between the settings for two document states.
        /// </summary>
        /// <param name="settingsOld">The previous document settings</param>
        /// <param name="settingsNew">New document settings to chage to</param>
        public SrmSettingsDiff(SrmSettings settingsOld, SrmSettings settingsNew)
            : this(settingsOld, settingsNew, false)
        {
        }

        /// <summary>
        /// Calculates the differences between the settings for two document states.
        /// </summary>
        /// <param name="settingsOld">The previous document settings</param>
        /// <param name="settingsNew">New document settings to chage to</param>
        /// <param name="isUnexplainedExplicitModificationAllowed">True if this settings change should not check
        /// explicit modifications against the global settings to make sure they are present</param>
        public SrmSettingsDiff(SrmSettings settingsOld, SrmSettings settingsNew,
            bool isUnexplainedExplicitModificationAllowed)
        {
            _isUnexplainedExplicitModificationAllowed = isUnexplainedExplicitModificationAllowed;

            SettingsOld = settingsOld;

            PeptideSettings newPep = settingsNew.PeptideSettings;
            PeptideSettings oldPep = settingsOld.PeptideSettings;
            TransitionSettings newTran = settingsNew.TransitionSettings;
            TransitionSettings oldTran = settingsOld.TransitionSettings;

            // Figure out whether precursor charges differ for determining
            // both peptide and transition group changes.
            bool precursorsDiff = !ArrayUtil.EqualsDeep(newTran.Filter.PrecursorCharges,
                                                        oldTran.Filter.PrecursorCharges) ||
                                  // Also changing auto-select could change precursors
                                  newTran.Filter.AutoSelect != oldTran.Filter.AutoSelect ||
                                  // And changing DIA isolation scheme could change precursors
                                  !Equals(newTran.FullScan.IsolationScheme, oldTran.FullScan.IsolationScheme);

            // Change peptides if enzyme, digestion or filter settings changed
            DiffPeptides = !newPep.Enzyme.Equals(oldPep.Enzyme) ||
                                  !newPep.DigestSettings.Equals(oldPep.DigestSettings) ||
                                  !newPep.Filter.Equals(oldPep.Filter) ||
                                  // If precursors differ, and peptide picks depend on the library
                                  (precursorsDiff && newPep.Libraries.HasLibraries && newPep.Libraries.Pick != PeptidePick.filter) ||
                                  // If variable modifications changed
                                  newPep.Modifications.MaxVariableMods != oldPep.Modifications.MaxVariableMods ||
                                  !ArrayUtil.EqualsDeep(newPep.Modifications.VariableModifications.ToArray(),
                                                        oldPep.Modifications.VariableModifications.ToArray());

            // Peptide standard types can change with iRT calculator
            DiffPeptideProps = !ReferenceEquals(newPep.Prediction.RetentionTime,
                                                oldPep.Prediction.RetentionTime);

            var oldLib = oldPep.Libraries;
            var newLib = newPep.Libraries;

            bool libraryChange = !ReferenceEquals(newLib, oldLib);
            bool diffLibraries = libraryChange && !EquivalentLibraries(newLib, oldLib);
            if (!DiffPeptides && libraryChange)
            {
                // If libraries have been removed, update peptides, if picking algorithm
                // allowed peptides outside the filter.
                if (!newLib.HasLibraries)
                {
                    DiffPeptides = oldLib.Pick == PeptidePick.library ||
                                   oldLib.Pick == PeptidePick.either;
                }
                // If the libraries are not loaded, wait until they are to make any changes
                else if (newLib.IsLoaded)
                {
                    // If no libraries were used before, or the picking algorithm has changed,
                    // or the peptide ranking ID has changed, or the peptide count limit has change,
                    // or the picking algorithm relies on the libraries, and the libraries have changed
                    DiffPeptides = (!oldLib.HasLibraries ||
                        newLib.Pick != oldLib.Pick ||
                        !Equals(newLib.RankId, oldLib.RankId) ||
                        !Equals(newLib.PeptideCount, oldLib.PeptideCount) ||
                        (newLib.Pick != PeptidePick.filter && diffLibraries));
                }
            }

            // Calculate changes in global implicit modifications
            var oldMods = oldPep.Modifications;
            var newMods = newPep.Modifications;
            bool diffStaticMods = !StaticMod.EquivalentImplicitMods(newMods.StaticModifications,
                                           oldMods.StaticModifications);
            bool diffHeavyMods = false;
            var enumNewHeavyMods = newMods.GetHeavyModifications().GetEnumerator();
            foreach (var oldTypedMods in oldMods.GetHeavyModifications())
            {
                if (!enumNewHeavyMods.MoveNext())   // synch with foreach
                {
                    // If fewer heavy label types
                    diffHeavyMods = true;
                    break;
                }
                var newTypedMods = enumNewHeavyMods.Current;
                if (newTypedMods == null || // ReSharper
                        !Equals(newTypedMods.LabelType, oldTypedMods.LabelType) ||
                        !StaticMod.EquivalentImplicitMods(newTypedMods.Modifications,
                                                          oldTypedMods.Modifications))
                {
                    // If label types or implicit modifications differ
                    diffHeavyMods = true;
                    break;
                }
            }
            // If not different yet, then make sure nothing was added
            if (!diffHeavyMods)
                diffHeavyMods = enumNewHeavyMods.MoveNext();

            // Set explicit differences, if no differences in the global implicit modifications,
            // but the modifications have changed.
            if (!diffStaticMods && !diffHeavyMods && !ReferenceEquals(oldPep.Modifications, newPep.Modifications))
                DiffExplicit = true;

            // Change transition groups if precursor charges or heavy group
            // existence changed, or if the IsolationScheme range changed
            var isolationRangesNew = newTran.FullScan.IsolationScheme == null ? null : newTran.FullScan.IsolationScheme.PrespecifiedIsolationWindows;
            var isolationRangesOld = oldTran.FullScan.IsolationScheme == null ? null : oldTran.FullScan.IsolationScheme.PrespecifiedIsolationWindows;
            bool diffInstrumentRange = newTran.Instrument.MinMz != oldTran.Instrument.MinMz ||
                                       newTran.Instrument.MaxMz != oldTran.Instrument.MaxMz ||
                                       !Equals(isolationRangesNew, isolationRangesOld);
            bool diffIsolationScheme = !Equals(newTran.FullScan.IsolationScheme, oldTran.FullScan.IsolationScheme);
            DiffTransitionGroups = precursorsDiff || diffHeavyMods || diffInstrumentRange || diffIsolationScheme;                

            // If libraries changed, then transition groups should change whenever
            // peptides change also.
            if (!DiffTransitionGroups && libraryChange)
                DiffTransitionGroups = DiffPeptides;

            // Any change in modifications or precursor mass-type forces a recalc
            // of precursor m/z values, as
            bool enrichmentsChanged = !Equals(newTran.FullScan.IsotopeEnrichments, oldTran.FullScan.IsotopeEnrichments);
            DiffTransitionGroupProps = diffStaticMods || diffHeavyMods ||
                                 !newTran.Prediction.PrecursorMassType.Equals(oldTran.Prediction.PrecursorMassType) ||
                                 // Or changes to MS1 filtering that change the expected isotope distribution
                                 !newTran.FullScan.PrecursorMassAnalyzer.Equals(oldTran.FullScan.PrecursorMassAnalyzer) ||
                                 !Equals(newTran.FullScan.PrecursorRes, oldTran.FullScan.PrecursorRes) ||
                                 !Equals(newTran.FullScan.PrecursorResMz, oldTran.FullScan.PrecursorResMz) ||
                                 // Or isotope enrichments
                                 enrichmentsChanged
                                 ;

            if (!DiffTransitionGroupProps && libraryChange)
            {
                // Make sure transition group library properties are updated, as long as the
                // libraries are loaded and have changed.
                DiffTransitionGroupProps = !newLib.HasLibraries || (newLib.IsLoaded && diffLibraries);
            }

            // Change transitions if anything in the transition filter changes.
            DiffTransitions = !newTran.Filter.Equals(oldTran.Filter) ||
                              !newTran.Libraries.Equals(oldTran.Libraries) ||
                              // Or libraries changed, and picking based on libraries
                              (libraryChange && DiffTransitionGroupProps &&
                                    newTran.Libraries.Pick != TransitionLibraryPick.none) ||
                              // If instrument min or max m/z changed
                              diffInstrumentRange ||
                              newTran.Instrument.IsDynamicMin != oldTran.Instrument.IsDynamicMin ||
                              // If loss modifications changed
                              newPep.Modifications.MaxNeutralLosses != oldPep.Modifications.MaxNeutralLosses ||
                              !ArrayUtil.EqualsDeep(newPep.Modifications.NeutralLossModifications.ToArray(),
                                                    oldPep.Modifications.NeutralLossModifications.ToArray()) ||
                              // MS1 filtering changed select peaks
                              newTran.FullScan.PrecursorIsotopes != oldTran.FullScan.PrecursorIsotopes ||
                              newTran.FullScan.PrecursorIsotopeFilter != oldTran.FullScan.PrecursorIsotopeFilter ||
                              (newTran.FullScan.PrecursorIsotopes != FullScanPrecursorIsotopes.None && enrichmentsChanged)
                              ;

            // If the library loded state has changed, make sure the library properties are up to date,
            // but avoid changing the chosen transitions.
            // CONSIDER: The way library transition ranking is currently implemented makes this too slow
//            if (!DiffTransitionGroupProps && libraryChange && newLib.IsLoaded && !oldLib.IsLoaded)
//                DiffTransitionGroupProps = true;

            // Any change in modifications or fragment mass-type forces a recalc
            // of transition m/z values, as
            DiffTransitionProps = diffStaticMods || diffHeavyMods ||
                                 !newTran.Prediction.FragmentMassType.Equals(oldTran.Prediction.FragmentMassType) ||
                                 (libraryChange && DiffTransitionGroupProps) ||
                                 // Any change in transitions can change transition rankings
                                 // if a library is in use.
                                 (newLib.HasLibraries && DiffTransitions) ||
                                 // If using MS1 isotopes, an enrichment change can change transition masses
                                 (newTran.FullScan.PrecursorIsotopes != FullScanPrecursorIsotopes.None && enrichmentsChanged)
                                 ;

            // If the results changed, then update the results information which has changed
            DiffResults = !EqualExceptAnnotations(settingsNew.MeasuredResults, settingsOld.MeasuredResults);
            // If the integration strategy has changed, then force a full update of all results
            if (newTran.Integration.IsIntegrateAll != oldTran.Integration.IsIntegrateAll)
                DiffResults = DiffResultsAll = true;
            // If the match tolerance has changed, then force a full update of all results
            if (newTran.Instrument.MzMatchTolerance != oldTran.Instrument.MzMatchTolerance)
                DiffResults = DiffResultsAll = true;
            // If internal standard type or all types changed, update all results to recalculate ratios.
            if (!ArrayUtil.EqualsDeep(newMods.InternalStandardTypes, oldMods.InternalStandardTypes) ||
                !ArrayUtil.EqualsDeep(newMods.GetModificationTypes().ToArray(), oldMods.GetModificationTypes().ToArray()))
            {
                DiffResults = true;
            }
            // Results handler is temporary. Any time the document has one, it means the results
            // must be updated and reintegration applied.
            if (newPep.Integration.ResultsHandler != null)
                DiffResults = true;
            // Avoid updating results while in a bulk import operation without UI
            if (settingsNew.IsResultsJoiningDisabled)
                DiffResults = false;
            // Force update if the bulk import has just completed
            else if (settingsOld.HasResults && settingsOld.MeasuredResults.IsResultsUpdateRequired)
                DiffResults = true;
        }

        private static bool EqualExceptAnnotations(MeasuredResults measuredResultsNew, MeasuredResults measuredResultsOld)
        {
            if (ReferenceEquals(measuredResultsNew, measuredResultsOld))
            {
                return true;
            }
            if (measuredResultsNew == null || measuredResultsOld == null)
            {
                return false;
            }
            if (measuredResultsNew.Chromatograms.Count != measuredResultsOld.Chromatograms.Count)
            {
                return false;
            }
            if (!ArrayUtil.EqualsDeep(measuredResultsNew.CachedFilePaths.ToArray(),
                                      measuredResultsOld.CachedFilePaths.ToArray()))
            {
                return false;
            }
            for (int i = 0; i < measuredResultsNew.Chromatograms.Count; i++)
            {
                var chromatogramSetNew = measuredResultsNew.Chromatograms[i].ChangeAnnotations(Annotations.EMPTY).ChangeUseForRetentionTimeFilter(false)
                    .ChangeAnalyteConcentration(null).ChangeSampleType(SampleType.DEFAULT);
                var chromatogramSetOld = measuredResultsOld.Chromatograms[i].ChangeAnnotations(Annotations.EMPTY).ChangeUseForRetentionTimeFilter(false)
                    .ChangeAnalyteConcentration(null).ChangeSampleType(SampleType.DEFAULT);
                if (!chromatogramSetNew.Equals(chromatogramSetOld))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool EquivalentLibraries(PeptideLibraries newLib, PeptideLibraries oldLib)
        {
            if (ReferenceEquals(newLib, oldLib))
                return true;
            if (!ArrayUtil.ReferencesEqual(newLib.LibrarySpecs, oldLib.LibrarySpecs))
                return false;
            // If library spec arrays are equal, then library arrays should
            // at least have the same number of elements
            Debug.Assert(newLib.Libraries.Count == oldLib.Libraries.Count);
            for (int i = 0; i < newLib.Libraries.Count; i++)
            {
                var oldLibrary = oldLib.Libraries[i];
                var newLibrary = newLib.Libraries[i];
                // If the old settings had a library that had never been
                // loaded before or differs from the previously loaded library,
                // then peptides must be updated.
                if (oldLibrary == null ||
                    newLibrary == null ||
                    // Do not check for difference in loaded state!!  This will cause
                    // all precursors to load library spectra during file open, which
                    // is too slow.
                    // oldLibrary.IsLoaded != newLibrary.IsLoaded ||
                    !oldLibrary.IsSameLibrary(newLibrary) ||
                    oldLibrary.CompareRevisions(newLibrary) != 0)
                {
                    return false;
                }
            }
            return true;
        }

        public SrmSettings SettingsOld { get; private set; }

        public SrmSettingsChangeMonitor Monitor { get; set; }

        public bool DiffPeptides { get; private set; }
        public bool DiffPeptideProps { get; private set; }
        public bool DiffExplicit { get; private set; }
        public bool DiffTransitionGroups { get; private set; }
        public bool DiffTransitionGroupProps { get; private set; }
        public bool DiffTransitions { get; private set; }
        public bool DiffTransitionProps { get; private set; }
        public bool DiffResults { get; private set; }
        public bool DiffResultsAll { get; private set; }

        public bool RequiresDocNodeUpdate
        {
            get
            {
                return DiffPeptides || DiffPeptideProps || DiffExplicit ||
                       DiffTransitionGroups || DiffTransitionGroupProps ||
                       DiffTransitions || DiffTransitionProps ||
                       DiffResults;
            }
        }

        public bool IsResultsOnly
        {
            get
            {
                return DiffResults &&
                    !(DiffPeptides || DiffPeptideProps || DiffExplicit ||
                      DiffTransitionGroups || DiffTransitionGroupProps ||
                      DiffTransitions || DiffTransitionProps);
            }
        }

        /// <summary>
        /// Adding nodes to a document with explicit modifications can be tricky, since they must
        /// be added before the document can be interrogated about what explicit modifications it
        /// containes.  And, usually nodes experience a ChangeSettings function call before they
        /// are added to the document with the existing settings for the document, which may not
        /// yet contain the required explicit modifications.  This flag prevents these unexplained
        /// explicit modifications from being stripped before a node can be added to the document
        /// to recalculate the explicit modifications on the document.
        /// </summary>
        public bool IsUnexplainedExplicitModificationAllowed
        {
            get { return SettingsOld == null || _isUnexplainedExplicitModificationAllowed; }
        }
    }
}