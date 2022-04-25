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
using pwiz.Skyline.Model.Crosslinking;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Optimization;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.IonMobility;
using pwiz.Skyline.Util;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Lib.Midas;
using pwiz.Skyline.Model.Lists;
using pwiz.Skyline.Model.Serialization;

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

        [TrackChildren]
        public PeptideSettings PeptideSettings { get; private set; }

        [TrackChildren]
        public TransitionSettings TransitionSettings { get; private set; }

        [TrackChildren]
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

        public bool HasDriftTimePrediction { get { return TransitionSettings.IonMobilityFiltering.IonMobilityLibrary != null; } }

        public bool HasIonMobilityLibraryPersisted
        {
            get
            {
                return HasDriftTimePrediction &&
                       TransitionSettings.IonMobilityFiltering.IonMobilityLibrary != null &&
                    !TransitionSettings.IonMobilityFiltering.IonMobilityLibrary.IsNone &&
                       TransitionSettings.IonMobilityFiltering.IonMobilityLibrary.FilePath != null;
            }
        }

        public bool HasBackgroundProteome { get { return !PeptideSettings.BackgroundProteome.IsNone; } }

        public RelativeRT GetRelativeRT(IsotopeLabelType labelType, Target seq, ExplicitMods mods)
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
                    if (!mod.IsMod(seq.Sequence))
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
            if (ExplicitMods.IsNullOrEmpty(mods))
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
            return (calcLightImplicit.MassType.IsMonoisotopic() ?
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

        public bool SupportsPrecursor(TransitionGroupDocNode transitionGroup, ExplicitMods mods)
        {
            if (transitionGroup.IsLight)
            {
                return true;
            }
            if (transitionGroup.IsCustomIon)
            {
                return PeptideSettings.Modifications.GetHeavyModificationTypes().Contains(transitionGroup.LabelType);
            }
            return HasPrecursorCalc(transitionGroup.LabelType, mods);
        }

        public IPrecursorMassCalc GetPrecursorCalc(IsotopeLabelType labelType, ExplicitMods mods)
        {
            var precursorCalc =  TryGetPrecursorCalc(labelType, mods);
            if (precursorCalc == null)
            {
                // Try to track down this exception:
                // https://skyline.gs.washington.edu/labkey/announcements/home/issues/exceptions/thread.view?entityId=217d79c8-9a84-1032-ae5f-da2025829168&_anchor=19667#row:19667
                throw new InvalidDataException(
                    String.Format(@"unable to locate precursor calculator for isotope label type {0} and mods {1}",
                        labelType == null ? @"(null)" : labelType.ToString(),
                        mods == null ? @"(null)" : mods.ToString()));
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
            var result = GetMassCalc(labelType, _precursorMassCalcs);
            if (result == null && ReferenceEquals(mods, ExplicitMods.EMPTY))
            {
                result = GetMassCalc(IsotopeLabelType.light, _precursorMassCalcs); // Small molecules
            }
            return result;
        }

        public TypedMass GetPrecursorMass(IsotopeLabelType labelType, Target seq, ExplicitMods mods)
        {
            var precursorCalc = GetPrecursorCalc(labelType, mods);

            if (mods != null && mods.HasCrosslinks)
            {
                var crosslinkBuilder = new CrosslinkBuilder(this, new Peptide(seq), mods, labelType);
                return crosslinkBuilder.GetPrecursorMass(precursorCalc.MassType);
            }
            return precursorCalc.GetPrecursorMass(seq);
        }

        public TypedMass GetPrecursorMass(IsotopeLabelType labelType, CustomMolecule mol, TypedModifications mods, Adduct adductForIsotopeLabels, out string isotopicFormula)
        {
            return GetPrecursorCalc(labelType, ExplicitMods.EMPTY).GetPrecursorMass(mol, mods, adductForIsotopeLabels, out isotopicFormula);
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
            var result = GetMassCalc(labelType, _fragmentMassCalcs);
            if (result == null && ReferenceEquals(mods, ExplicitMods.EMPTY))
            {
                result = GetMassCalc(IsotopeLabelType.light, _fragmentMassCalcs); // Small molecules
            }
            return result;
        }

        /// <summary>
        /// For use with small molecules, where we don't deal with modifications
        /// </summary>
        public IFragmentMassCalc GetDefaultFragmentCalc()
        {
            return _fragmentMassCalcs[0].MassCalc;
        }

        public TypedMass GetFragmentMass(TransitionGroup group, ExplicitMods mods,
            Transition transition, IsotopeDistInfo isotopeDist)
        {
            Assume.IsTrue(mods == null || !mods.HasCrosslinks, @"Use ComplexFragmentIon.GetFragmentMass");
            // Return the singly protonated mass (massH) of the peptide fragment, or custom molecule mass before electron removal
            var labelType = group == null ? IsotopeLabelType.light : group.LabelType;
            return GetSimpleFragmentMass(labelType, mods, transition, isotopeDist);
        }

        private TypedMass GetSimpleFragmentMass(IsotopeLabelType labelType, ExplicitMods mods, Transition transition,
            IsotopeDistInfo isotopeDist)
        {
            IFragmentMassCalc calc;
            if (transition.IsNonReporterCustomIon())
            {
                // Small molecules provide their own molecule formula, just use the standard calculator
                calc = GetDefaultFragmentCalc();
            }
            else
            {
                calc = GetFragmentCalc(labelType, mods);
            }
            if (calc == null)
            {
                Assume.Fail(string.Format(@"Unable to locate fragment calculator for isotope label type {0} and mods {1}",
                    labelType == null ? @"(null)" : labelType.ToString(),
                    mods == null ? @"(null)" : mods.ToString()));
                return TypedMass.ZERO_MONO_MASSH;   // Keep resharper happy
            }
            return calc.GetFragmentMass(transition, isotopeDist);

        }

        public TypedMass RecalculateTransitionMass(ExplicitMods explicitMods, TransitionDocNode transition,
            IsotopeDistInfo isotopeDist)
        {
            if (explicitMods == null || !explicitMods.HasCrosslinks)
            {
                return GetSimpleFragmentMass(transition.Transition.Group.LabelType, explicitMods, transition.Transition,
                    isotopeDist);
            }

            return transition.ComplexFragmentIon.GetFragmentMass(this, explicitMods);
        }

        public ChromSource GetChromSource(TransitionDocNode nodeTran)
        {
            if (TransitionSettings.FullScan.IsEnabledMs && nodeTran.IsMs1)
                return ChromSource.ms1;
            // TODO: Allow SIM
            return ChromSource.fragment;
        }

        public Target GetModifiedSequence(Target seq,
                                          IsotopeLabelType labelType,
                                          ExplicitMods mods,
                                          SequenceModFormatType format = SequenceModFormatType.full_precision,
                                          bool useExplicitModsOnly = false)
        {
            if (mods != null && mods.HasCrosslinks)
            {
                return GetCrosslinkModifiedSequence(seq, labelType, mods);
            }
            return GetPrecursorCalc(labelType, mods).GetModifiedSequence(seq, format, useExplicitModsOnly);
        }

        public Target GetCrosslinkModifiedSequence(Target seq, IsotopeLabelType labelType, ExplicitMods mods)
        {
            var peptideStructure = new PeptideStructure(new Peptide(seq), mods);
            var crosslinkModifiedSequence =
                CrosslinkedSequence.GetCrosslinkedSequence(this, peptideStructure, labelType);
            string strModifiedSequence = TransitionSettings.Prediction.PrecursorMassType.IsMonoisotopic()
                ? crosslinkModifiedSequence.MonoisotopicMasses
                : crosslinkModifiedSequence.AverageMasses;
            return new Target(strModifiedSequence);
        }

        public Adduct GetModifiedAdduct(Adduct adduct, string neutralFormula,
                                          IsotopeLabelType labelType,
                                          ExplicitMods mods)
        {
            return GetPrecursorCalc(labelType, mods).GetModifiedAdduct(adduct, neutralFormula);
        }

        public string GetDisplayName(PeptideDocNode nodePep)
        {
            return nodePep.Peptide.IsCustomMolecule ? nodePep.CustomMolecule.DisplayName : nodePep.ModifiedSequenceDisplay;
        }

        public Target GetModifiedSequence(PeptideDocNode nodePep)
        {
            if (nodePep.Peptide.IsCustomMolecule)
                return nodePep.ModifiedTarget;
            Assume.IsNotNull(nodePep.ModifiedSequence);
            return nodePep.ModifiedTarget;
        }

        public Target GetSourceTarget(PeptideDocNode nodePep)
        {
            Assume.IsNotNull(nodePep.SourceTextId);
            return nodePep.SourceModifiedTarget;
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
                if (nodePep.Peptide.IsCustomMolecule)
                {
                    double mass = nodeGroup.TransitionGroup.CustomMolecule.GetMass(TransitionSettings.Prediction.PrecursorMassType); // No need for mods since we want light mass
                    mz = nodeGroup.TransitionGroup.PrecursorAdduct.MzFromNeutralMass(mass, TransitionSettings.Prediction.PrecursorMassType);
                }
                else
                {
                    var massH = GetPrecursorMass(IsotopeLabelType.light,
                        nodePep.Peptide.Target, nodePep.ExplicitMods);
                    mz = SequenceMassCalc.GetMZ(massH, nodeGroup.TransitionGroup.PrecursorAdduct);
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
            var dataSettings = settingsSavable.DataSettings;
            dataSettings = dataSettings.ChangeListDefs(dataSettings.Lists.Select(list => list.DeleteAllRows()));
            settingsSavable = settingsSavable.ChangeDataSettings(dataSettings);
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
                                        where !mod.IsExplicit && null == mod.CrosslinkerSettings
                                        select mod);
            if (heavyMods != null)
            {
                calc.AddHeavyModifications(from mod in heavyMods
                                           where !mod.IsExplicit && null == mod.CrosslinkerSettings
                                           select mod);
            }
            return calc;
        }

        /// <summary>
        /// Cached standard types
        /// </summary>
        private ImmutableDictionary<StandardType, ImmutableList<PeptideDocNode>> _cachedPeptideStandards;
        private static readonly PeptideDocNode[] EMPTY_STANDARDS = new PeptideDocNode[0];

        public IEnumerable<PeptideDocNode> GetPeptideStandards(StandardType standardType)
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
            var cachedPeptideStandards = new Dictionary<StandardType, IList<PeptideDocNode>>();
            foreach (PeptideGroupDocNode nodePepGroup in peptideGroupDocNodes)
            {
                foreach (var nodePep in nodePepGroup.Molecules)
                {
                    var standardType = nodePep.GlobalStandardType;
                    if (standardType == null)
                        continue;
                    IList<PeptideDocNode> listPeptideAndGroup;
                    if (!cachedPeptideStandards.TryGetValue(standardType, out listPeptideAndGroup))
                    {
                        listPeptideAndGroup = new List<PeptideDocNode>();
                        cachedPeptideStandards.Add(standardType, listPeptideAndGroup);
                    }
                    // Update the PeptideChromInfo before adding it to the list
                    var nodeWithUpdatedResults = nodePep.ChangeSettings(this, new SrmSettingsDiff(this, true));
                    if (nodePep.Equals(nodeWithUpdatedResults))
                    {
                        listPeptideAndGroup.Add(nodePep);
                    }
                    else
                    {
                        listPeptideAndGroup.Add(nodeWithUpdatedResults);
                    }
                }
            }
            // Create new read-only lists, if necessary
            bool createdNewList = false;
            var cachedPeptideStandardsRo = new Dictionary<StandardType, ImmutableList<PeptideDocNode>>();
            foreach (var pair in cachedPeptideStandards)
            {
                var standardType = pair.Key;
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
            var prop = new ImmutableDictionary<StandardType, ImmutableList<PeptideDocNode>>(cachedPeptideStandardsRo);
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
                var nodePepGroupOrig = (PeptideGroupDocNode)peptideGroupDocNodesOrig[i];
                // In case the peptides have been freed during command-line processing
                if (nodePepGroupOrig.Children.Count > 0 && nodePepGroupOrig.Children.First() == null)
                    return false;   // No standards can change in this case currently
                var nodePepGroup = (PeptideGroupDocNode)peptideGroupDocNodes[i];
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
                        !Equals(nodePep.GlobalStandardType, nodePepOrig.GlobalStandardType) ||
                        // Need the new version, if the results have changed, or ratios will not be valid
                        !ReferenceEquals(nodePep.Results, nodePepOrig.Results))
                    {
                        return true;
                    }
                    if (Equals(nodePep.GlobalStandardType, StandardType.GLOBAL_STANDARD) ||
                        Equals(nodePep.GlobalStandardType, StandardType.SURROGATE_STANDARD))
                    {
                        if (!ReferenceEquals(nodePep, nodePepOrig))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }


        /// <summary>
        /// Returns true if the settings have changed in a way that might require
        /// all of the results text in the SequenceTree to be updated.
        /// </summary>
        public bool IsGlobalRatioChange(SrmSettings other)
        {
            if (PeptideSettings.Quantification.SimpleRatios != other.PeptideSettings.Quantification.SimpleRatios)
            {
                return true;
            }

            if (_cachedPeptideStandards == null)
            {
                return other._cachedPeptideStandards != null;
            }

            if (other._cachedPeptideStandards == null)
            {
                return true;
            }

            foreach (var entry in _cachedPeptideStandards)
            {
                if (!other._cachedPeptideStandards.TryGetValue(entry.Key, out var otherValue))
                {
                    return true;
                }

                if (!Equals(entry.Value, otherValue))
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasGlobalStandardArea
        {
            get
            {
                if (HasResults && MeasuredResults.HasGlobalStandardArea)
                {
                    return true;
                }
                return _cachedPeptideStandards != null &&
                       _cachedPeptideStandards.ContainsKey(PeptideDocNode.STANDARD_TYPE_GLOBAL);

            }
        }

        public bool HasTicArea
        {
            get
            {
                if (!HasResults)
                {
                    // If we have no results yet then assume that the TIC would be available
                    return true;
                }

                return MeasuredResults.GetMedianTicArea().HasValue;
            }
        }

        public double? GetTicNormalizationDenominator(int replicateIndex, ChromFileInfoId fileId)
        {
            var fileInfo = MeasuredResults.Chromatograms[replicateIndex].GetFileInfo(fileId);
            if (fileInfo == null || !fileInfo.TicArea.HasValue)
            {
                return null;
            }

            var medianTicArea = MeasuredResults.GetMedianTicArea();
            if (!medianTicArea.HasValue)
            {
                return null;
            }

            return fileInfo.TicArea.Value / medianTicArea.Value;
        }

        public double CalcGlobalStandardArea(int resultsIndex, ChromFileInfo fileInfo)
        {
            if (fileInfo.ExplicitGlobalStandardArea.HasValue)
            {
                return fileInfo.ExplicitGlobalStandardArea.Value;
            }
            double globalStandardArea = 0;
            var peptideStandards = GetPeptideStandards(StandardType.GLOBAL_STANDARD);
            if (peptideStandards != null)
            {
                foreach (var nodeGroup in peptideStandards.SelectMany(nodePep => nodePep.TransitionGroups))
                {
                    var chromInfos = nodeGroup.GetSafeChromInfo(resultsIndex);
                    foreach (var groupChromInfo in chromInfos)
                    {
                        if (ReferenceEquals(fileInfo.FileId, groupChromInfo.FileId) &&
                                groupChromInfo.OptimizationStep == 0 &&
                                groupChromInfo.Area.HasValue)
                            globalStandardArea += groupChromInfo.Area.Value;
                    }
                }
            }
            return globalStandardArea;
        }

        public IEnumerable<PeptideDocNode> GetInternalStandards(string internalStandardName)
        {
            if (null == internalStandardName)
            {
                return GetPeptideStandards(StandardType.GLOBAL_STANDARD);
            }
            return GetPeptideStandards(StandardType.SURROGATE_STANDARD)
                .Where(pep => pep.ModifiedTarget.Sequence == internalStandardName);
        }

        public bool LibrariesContainMeasurablePeptide(Peptide peptide, IList<Adduct> precursorCharges, ExplicitMods mods)
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
            IEnumerable<Adduct> precursorAdducts, ExplicitMods mods)
        {
            var key = GetModifiedSequence(peptide.Target, labelType, mods);
            foreach (var precursorAdduct in precursorAdducts)
            {
                var adduct = peptide.IsCustomMolecule ? GetModifiedAdduct(precursorAdduct, peptide.CustomMolecule.UnlabeledFormula, labelType, mods) : precursorAdduct;
                if (LibrariesContain(key, adduct))
                {
                    // Make sure the peptide for the found spectrum is measurable on
                    // the current instrument.
                    var precursorMass =  peptide.IsCustomMolecule ?
                        peptide.CustomMolecule.MonoisotopicMass : // Label information is in the adduct
                        GetPrecursorMass(labelType, peptide.Target, mods);
                    if (IsMeasurable(precursorMass, adduct))
                        return true;
                }
            }
            return false;
        }

        private bool IsMeasurable(TypedMass precursorMass, Adduct adduct)
        {
            double precursorMz = SequenceMassCalc.GetMZ(precursorMass, adduct);
            return TransitionSettings.IsMeasurablePrecursor(precursorMz);
        }

        public bool LibrariesContain(Target sequenceMod, Adduct charge)
        {
            return PeptideSettings.Libraries.Contains(new LibKey(sequenceMod, charge));
        }

        public bool LibrariesContainAny(Target sequence)
        {
            return PeptideSettings.Libraries.ContainsAny(sequence);
        }

        public bool TryGetLibInfo(Peptide peptide, Adduct adduct, ExplicitMods mods,
            out IsotopeLabelType type, out SpectrumHeaderInfo libInfo)
        {
            if (peptide.IsCustomMolecule)
            {
                return TryGetLibInfoSmallMolecule(peptide, adduct, mods, out type, out libInfo);
            }
            var libraries = PeptideSettings.Libraries;
            var sequence = peptide.Target;
            if (sequence == null)
            {
                type = null;
                libInfo = null;
                return false;
            }
            foreach (var typedSequence in GetTypedSequences(sequence, mods, adduct))
            {
                var key = new LibKey(typedSequence.ModifiedSequence, typedSequence.Adduct);
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

        private bool TryGetLibInfoSmallMolecule(Peptide peptide, Adduct adduct, ExplicitMods mods, out IsotopeLabelType type,
            out SpectrumHeaderInfo libInfo)
        {
            // Try molecule with combinations of adduct+label
            var libraries = PeptideSettings.Libraries;
            var keybase = peptide.CustomMolecule.GetSmallMoleculeLibraryAttributes();
            if (keybase != null)
            {
                var unlabeled = new Target(peptide.CustomMolecule.ChangeFormula(peptide.CustomMolecule.UnlabeledFormula));
                foreach (var typedAdduct in GetTypedSequences(unlabeled, mods, adduct))
                {
                    var key = new LibKey(keybase, typedAdduct.Adduct);
                    if (libraries.TryGetLibInfo(key, out libInfo))
                    {
                        type = typedAdduct.LabelType;
                        return true;
                    }
                }
            }
            type = null;
            libInfo = null;
            return false;
        }

        public bool TryLoadSpectrum(Target sequence, Adduct adduct, ExplicitMods mods,
            out IsotopeLabelType type, out SpectrumPeaksInfo spectrum)
        {
            var libraries = PeptideSettings.Libraries;
            foreach (var typedSequence in GetTypedSequences(sequence, mods, adduct))
            {
                var key = new LibKey(typedSequence.ModifiedSequence, typedSequence.Adduct);
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

        /// <summary>
        /// Get ion mobility for the charged peptide from ion mobility library, or,
        /// failing that, from the provided spectral library if it has ion mobility values.
        /// If no ion mobility info is available, returns a new zero'd out ion mobility.
        /// </summary>
        public IonMobilityFilter GetIonMobilityFilter(PeptideDocNode nodePep,
            TransitionGroupDocNode nodeGroup,
            TransitionDocNode nodeTran,
            LibraryIonMobilityInfo libraryIonMobilityInfo,
            IIonMobilityFunctionsProvider instrumentInfo, // For converting CCS to IM if needed, or mz to IM for Waters SONAR
            double ionMobilityMax)
        {
            if (instrumentInfo != null && instrumentInfo.IsWatersSonarData)
            {
                // Waters SONAR uses the ion mobility hardware to filter on precursor mz bands, and emits data that claims to be IM but is really bin numbers
                // So here we map the mz filter to a fictional IM filter.
                return GetSonarMzIonMobilityFilter(nodeGroup.PrecursorMz, TransitionSettings.FullScan.GetPrecursorFilterWindow(nodeGroup.PrecursorMz), 
                    instrumentInfo);
            }
            if (nodeGroup.ExplicitValues.CollisionalCrossSectionSqA.HasValue && instrumentInfo != null && instrumentInfo.ProvidesCollisionalCrossSectionConverter)
            {
                // Use the explicitly specified CCS value if provided, and if we know how to convert to IM
                var im = instrumentInfo.IonMobilityFromCCS(nodeGroup.ExplicitValues.CollisionalCrossSectionSqA.Value,
                    nodeGroup.PrecursorMz, nodeGroup.TransitionGroup.PrecursorCharge);
                var imAndCCS = IonMobilityAndCCS.GetIonMobilityAndCCS(im,
                    nodeGroup.ExplicitValues.CollisionalCrossSectionSqA,ExplicitTransitionValues.Get(nodeTran).IonMobilityHighEnergyOffset ?? 0);
                // Now get the window width
                var windowIM = TransitionSettings.IonMobilityFiltering.FilterWindowWidthCalculator.WidthAt(imAndCCS.IonMobility.Mobility.Value, ionMobilityMax);
                return IonMobilityFilter.GetIonMobilityFilter(imAndCCS, windowIM);
            }
            else if (nodeGroup.ExplicitValues.IonMobility.HasValue)
            {
                // Use the explicitly specified IM value
                var imAndCCS = IonMobilityAndCCS.GetIonMobilityAndCCS(IonMobilityValue.GetIonMobilityValue(nodeGroup.ExplicitValues.IonMobility, nodeGroup.ExplicitValues.IonMobilityUnits),
                    nodeGroup.ExplicitValues.CollisionalCrossSectionSqA,
                    ExplicitTransitionValues.Get(nodeTran).IonMobilityHighEnergyOffset ?? 0);
                // Now get the window width
                var windowIM = TransitionSettings.IonMobilityFiltering.FilterWindowWidthCalculator.WidthAt(imAndCCS.IonMobility.Mobility.Value, ionMobilityMax);
                return IonMobilityFilter.GetIonMobilityFilter(imAndCCS, windowIM);
            }
            else
            {
                // Use library values
                return GetIonMobilityHelper(nodePep, nodeGroup,
                    instrumentInfo,
                    libraryIonMobilityInfo, ionMobilityMax);
            }
        }

        /// <summary>
        /// Waters SONAR mode uses the ion mobility hardware to filter on precursor mz, and reports the data as if it was drift time information.
        /// So for convenience map the mz extraction window to an ion mobility filter window.
        /// </summary>
        public static IonMobilityFilter GetSonarMzIonMobilityFilter(double mz, double windowMz, IIonMobilityFunctionsProvider instrumentInfo)
        {
            var binRange = instrumentInfo.SonarMzToBinRange(mz, windowMz / 2); // Convert to SONAR bin range
            return IonMobilityFilter.GetIonMobilityFilter( IonMobilityAndCCS.GetIonMobilityAndCCS(0.5 * (binRange.Item1 + binRange.Item2),
                    eIonMobilityUnits.waters_sonar, null, null),
                (binRange.Item2 - binRange.Item1) + IonMobilityFilter.DoubleToIntEpsilon); // Add a tiny bit to window size to account for double->int rounding in center value
        }

        /// <summary>
        /// Made public for testing purposes only: exercises library but doesn't handle explicitly set drift times.
        /// Use GetIonMobility() instead.
        /// </summary>
        public IonMobilityFilter GetIonMobilityHelper(PeptideDocNode nodePep, TransitionGroupDocNode nodeGroup,
            IIonMobilityFunctionsProvider ionMobilityFunctionsProvider,
            LibraryIonMobilityInfo libraryIonMobilityInfo,
            double ionMobilityMax)
        {
            foreach (var typedSequence in GetTypedSequences(nodePep.Target, nodePep.ExplicitMods, nodeGroup.PrecursorAdduct))
            {
                var chargedPeptide = new LibKey(typedSequence.ModifiedSequence, typedSequence.Adduct); // N.B. this may actually be a small molecule

                // Try for a ion mobility library value (.imsdb file)
                var result = TransitionSettings.IonMobilityFiltering.GetIonMobilityFilter(chargedPeptide, nodeGroup.PrecursorMz,  ionMobilityFunctionsProvider, ionMobilityMax);
                if (result != null && result.HasIonMobilityValue)
                    return result;

                // Try other sources - BiblioSpec, Chromatogram libraries etc
                if (libraryIonMobilityInfo != null)
                {
                    var imAndCCS = libraryIonMobilityInfo.GetLibraryMeasuredIonMobilityAndCCS(chargedPeptide, nodeGroup.PrecursorMz, ionMobilityFunctionsProvider);
                    if (imAndCCS.IonMobility.HasValue && TransitionSettings.IonMobilityFiltering.UseSpectralLibraryIonMobilityValues)
                    {
                        var ionMobilityWindow = TransitionSettings.IonMobilityFiltering.FilterWindowWidthCalculator.WidthAt(imAndCCS.IonMobility.Mobility.Value, ionMobilityMax);
                        return IonMobilityFilter.GetIonMobilityFilter(imAndCCS, ionMobilityWindow);
                    }
                }
            }
            return IonMobilityFilter.EMPTY;
        }



        public bool TryGetRetentionTimes(Target sequence, Adduct adduct, ExplicitMods mods, MsDataFileUri filePath,
            out IsotopeLabelType type, out double[] retentionTimes)
        {
            var libraries = PeptideSettings.Libraries;
            foreach (var typedSequence in GetTypedSequences(sequence, mods, adduct))
            {
                var key = new LibKey(typedSequence.ModifiedSequence, typedSequence.Adduct);
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

            var lookupSequence = nodePep.SourceUnmodifiedTarget;
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

        /// <summary>
        /// If any library has specified explicit peak boundaries for the peptide, then
        /// return a tuple of peakStartTime, peakEndTime.
        /// This method just returns the first peak boundary in any library.
        /// In theory, a library should only have one peak boundary for any peptide.
        /// </summary>
        public ExplicitPeakBounds GetExplicitPeakBounds(PeptideDocNode nodePep, MsDataFileUri filePath)
        {
            if (nodePep == null)
            {
                return null;
            }

            IEnumerable<Target> modifiedSequences = GetTypedSequences(
                nodePep.SourceUnmodifiedTarget, nodePep.SourceExplicitMods,
                Adduct.EMPTY, true).Select(typedSequence => typedSequence.ModifiedSequence);
            foreach (var library in PeptideSettings.Libraries.Libraries)
            {
                if (library == null || !library.UseExplicitPeakBounds)
                {
                    continue;
                }

                // ReSharper disable PossibleMultipleEnumeration
                // Do not worry about multiple enumerations of modifiedSequences. Most libraries do not have 
                // any explicit peak boundaries, so modifiedSequences gets enumerated zero times.
                var peakBoundaries = library.GetExplicitPeakBounds(filePath, modifiedSequences);
                // ReSharper restore PossibleMultipleEnumeration

                if (peakBoundaries != null)
                {
                    return peakBoundaries;
                }
            }
            return null;
        }

        public double[] GetRetentionTimes(string filePath, Target peptideSequence, ExplicitMods explicitMods,
            RetentionTimeAlignmentIndex alignmentIndex = null)
        {
            return GetRetentionTimes(MsDataFileUri.Parse(filePath), peptideSequence, explicitMods, alignmentIndex);
        }

        public double[] GetRetentionTimes(MsDataFileUri filePath, Target peptideSequence, ExplicitMods explicitMods,
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
            var modifiedSequences = GetTypedSequences(peptideSequence, explicitMods, Adduct.EMPTY, true)
                .Select(typedSequence => typedSequence.ModifiedSequence);

            int? index = (alignmentIndex != null ? alignmentIndex.FileIndex : null);

            var times = library.GetRetentionTimesWithSequences(source.Name, modifiedSequences, ref index).ToArray();

            if (alignmentIndex != null)
                alignmentIndex.FileIndex = index;
            return times;
        }

        public double[] GetAlignedRetentionTimes(MsDataFileUri filePath, Target peptideSequence, ExplicitMods explicitMods)
        {
            string basename = filePath.GetFileNameWithoutExtension();
            var fileAlignments = DocumentRetentionTimes.FileAlignments.Find(basename);

            return GetAlignedRetentionTimes(new RetentionTimeAlignmentIndices(fileAlignments), peptideSequence, explicitMods);
        }

        public double[] GetAlignedRetentionTimes(RetentionTimeAlignmentIndices alignmentIndices, Target peptideSequence, ExplicitMods explicitMods)
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

        public double[] GetUnalignedRetentionTimes(Target peptideSequence, ExplicitMods explicitMods)
        {
            var times = new List<double>();
            var modifiedSequences = GetTypedSequences(peptideSequence, explicitMods, Adduct.EMPTY, true)
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

        public double[] GetRetentionTimesNotAlignedTo(MsDataFileUri fileNotAlignedTo, Target peptideSequence,
            ExplicitMods explicitMods, SignedMz[] precursorMzs)
        {
            var times = new List<double>();
            string basename = fileNotAlignedTo.GetFileNameWithoutExtension();
            var fileAlignments = DocumentRetentionTimes.FileAlignments.Find(basename);
            var modifiedSequences = GetTypedSequences(peptideSequence, explicitMods, Adduct.EMPTY, true)
                .Select(typedSequence => typedSequence.ModifiedSequence).ToArray();

            foreach (var library in PeptideSettings.Libraries.Libraries.Where(library => library != null))
            {
                // ReSharper disable ConditionIsAlwaysTrueOrFalse
                // ReSharper disable HeuristicUnreachableCode
                if (library is MidasLibrary)
                {
                    foreach (var midasSpectra in precursorMzs.Select(precursorMz => GetMidasSpectra(precursorMz.Value)))
                    {
                        times.AddRange(midasSpectra.Where(spectrum => spectrum.RetentionTime.HasValue && !Equals(spectrum.FileName, fileNotAlignedTo.GetFileName()))
                                                   .Select(spectrum => spectrum.RetentionTime.GetValueOrDefault()));
                    }
                }
                else
                {
                    foreach (var source in library.ListRetentionTimeSources())
                    {
                        if (MeasuredResults.IsBaseNameMatch(source.Name, basename) ||
                            (null != fileAlignments && null != fileAlignments.RetentionTimeAlignments.Find(source.Name)))
                        {
                            continue;
                        }
                        int? indexIgnore = null;
                        // ReSharper disable PossibleMultipleEnumeration
                        times.AddRange(library.GetRetentionTimesWithSequences(source.Name, modifiedSequences, ref indexIgnore));
                        // ReSharper restore PossibleMultipleEnumeration
                    }
                }
                // ReSharper restore HeuristicUnreachableCode
                // ReSharper restore ConditionIsAlwaysTrueOrFalse
            }
            return times.ToArray();
        }

        public double[] GetAllRetentionTimes(string filePath, Target peptideSequence, ExplicitMods explicitMods)
        {
            return GetAllRetentionTimes(MsDataFileUri.Parse(filePath), peptideSequence, explicitMods);
        }

        public double[] GetAllRetentionTimes(MsDataFileUri filePath, Target peptideSequence, ExplicitMods explicitMods)
        {
            var times = new List<double>();
            times.AddRange(GetRetentionTimes(filePath, peptideSequence, explicitMods));
            times.AddRange(GetAlignedRetentionTimes(filePath, peptideSequence, explicitMods));
            return times.ToArray();
        }

        private IEnumerable<TypedSequence> GetTypedSequences(Target sequence, ExplicitMods mods, Adduct adduct, bool assumeProteomicWhenEmpty = false)
        {
            if (mods != null && mods.HasCrosslinks)
            {
                yield return new TypedSequence(GetCrosslinkModifiedSequence(sequence, IsotopeLabelType.light, mods), IsotopeLabelType.light, adduct);

                foreach (var labelTypeHeavy in GetHeavyLabelTypes(mods))
                {
                    yield return new TypedSequence(GetCrosslinkModifiedSequence(sequence, labelTypeHeavy, mods), labelTypeHeavy, adduct);
                }
            }
            else if (adduct.IsProteomic || (assumeProteomicWhenEmpty && adduct.IsEmpty))
            {
                var labelType = IsotopeLabelType.light;
                var modifiedSequence = GetModifiedSequence(sequence, labelType, mods);
                yield return new TypedSequence(modifiedSequence, labelType, adduct);

                foreach (var labelTypeHeavy in GetHeavyLabelTypes(mods))
                {
                    modifiedSequence = GetModifiedSequence(sequence, labelTypeHeavy, mods);
                    yield return new TypedSequence(modifiedSequence, labelTypeHeavy, adduct);
                }
            }
            else
            {
                if (adduct.IsEmpty)
                {
                    // Caller is going to ignore charge state for search purposes, and since adduct is where we store labels info there's nothing to do here
                    yield return new TypedSequence(sequence, IsotopeLabelType.light, adduct);
                    yield break;                    
                }

                // Small molecules express labels in the adduct
                var labelType = IsotopeLabelType.light;
                var formula = sequence.Molecule.UnlabeledFormula;
                var modifiedAdduct = GetModifiedAdduct(adduct, formula, labelType, mods);
                yield return new TypedSequence(sequence, labelType, modifiedAdduct);

                foreach (var labelTypeHeavy in GetHeavyLabelTypes(mods))
                {
                    modifiedAdduct = GetModifiedAdduct(adduct, formula, labelTypeHeavy, mods);
                    yield return new TypedSequence(sequence, labelTypeHeavy, modifiedAdduct);
                }
            }
        }

        private struct TypedSequence
        {
            public TypedSequence(Target modifiedSequence, IsotopeLabelType labelType, Adduct adduct)
                : this()
            {
                ModifiedSequence = modifiedSequence;
                LabelType = labelType;
                Adduct = adduct;
            }

            public Target ModifiedSequence { get; private set; }
            public IsotopeLabelType LabelType { get; private set; }
            public Adduct Adduct { get; private set; } // Small molecules express labels in the adduct rather than the formula
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

        public LibraryIonMobilityInfo GetIonMobilities(LibKey[] targetIons, MsDataFileUri filePath)
        {
            // Look in ion mobility library (.imsdb) if available, then fill gaps with spectral libs if requested
            var imFiltering = TransitionSettings.IonMobilityFiltering;
            if (imFiltering != null)
            {
                var dict = new Dictionary<LibKey, IonMobilityAndCCS[]>();
                if (imFiltering.IonMobilityLibrary != null && !imFiltering.IonMobilityLibrary.IsNone)
                {
                    foreach (var ion in targetIons)
                    {
                        var ims = imFiltering.GetIonMobilityInfoFromLibrary(ion);
                        if (ims != null && !dict.ContainsKey(ion)) // Beware precursors appearing more than once in document
                        {
                            dict.Add(ion, ims.ToArray());
                        }
                    }
                }
                if (dict.Count < targetIons.Length && imFiltering.UseSpectralLibraryIonMobilityValues && filePath != null)
                {
                    var libraries = PeptideSettings.Libraries;
                    if (libraries.TryGetSpectralLibraryIonMobilities(targetIons, filePath, out var ionMobilities) && ionMobilities != null)
                    {
                        var map = LibKeyMap<IonMobilityAndCCS[]>.FromDictionary(dict);
                        foreach (var im in ionMobilities.GetIonMobilityDict().Where(item => 
                            !map.TryGetValue(item.Key, out _)))
                        {
                            dict.Add(im.Key, im.Value);
                        }
                    }
                }

                return dict.Count > 0
                    ? new LibraryIonMobilityInfo(filePath?.GetFilePath(), true, dict)
                    : LibraryIonMobilityInfo.EMPTY;
            }
            return null;
        }

        /// <summary>
        /// Returns the times at which a peptide was found in a particular file.
        /// </summary>
        public double[] GetRetentionTimes(LibraryRetentionTimes retentionTimes, Target sequence, ExplicitMods mods, Adduct adduct)
        {
            return (from typedSequence in GetTypedSequences(sequence, mods, adduct)
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
        public IEnumerable<SpectrumInfoLibrary> GetBestSpectra(Target sequence, Adduct charge, ExplicitMods mods)
        {
            var libraries = PeptideSettings.Libraries;
            return from typedSequence in GetTypedSequences(sequence, mods, charge)
                   let key = typedSequence.ModifiedSequence.GetLibKey(typedSequence.Adduct)
                   from spectrumInfo in libraries.GetSpectra(key, typedSequence.LabelType, true)
                   select spectrumInfo;
        }

        public IEnumerable<SpectrumInfoLibrary> GetMidasSpectra(double precursorMz)
        {
            return PeptideSettings.Libraries.MidasLibraries.SelectMany(
                lib => lib.GetSpectra(new LibKey(precursorMz), IsotopeLabelType.light, LibraryRedundancy.all));
        }

        /// <summary>
        /// Loads a list of all the spectra found in all loaded libraries 
        /// matching the criteria passed in.
        /// </summary>
        /// <param name="peptide"> Supplies info on molecule to match. </param>
        /// <param name="sequence"> The sequence to match. </param>
        /// <param name="adduct"> The charge to match. </param>
        /// <param name="labelType">The primary label type to match</param>
        /// <param name="mods"> The modifications to match. </param>
        /// <returns> Returns a list of the matching spectra. </returns>
        public IEnumerable<SpectrumInfoLibrary> GetRedundantSpectra(Peptide peptide, Target sequence, Adduct adduct, IsotopeLabelType labelType,
                                                       ExplicitMods mods)
        {
            LibKey libKey;
            if (!peptide.IsCustomMolecule)
            {
                var sequenceMod = GetModifiedSequence(sequence, labelType, mods);
                libKey = new LibKey(sequenceMod, adduct);
            }
            else
            {
                // For small molecules, label is in the adduct
                libKey = new LibKey(peptide.CustomMolecule.PrimaryEquivalenceKey, GetModifiedAdduct(adduct, peptide.CustomMolecule.UnlabeledFormula, labelType, mods)); // TODO that should be a formula
            }
            return PeptideSettings.Libraries.GetSpectra(libKey, labelType, false);
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
                          peptide.IsCustomMolecule ? TransitionSettings.Filter.SmallMoleculePrecursorAdducts : TransitionSettings.Filter.PeptidePrecursorCharges,
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
        public bool Accept(SrmSettings settings, Peptide peptide, ExplicitMods mods, Adduct charge)
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
                            IList<Adduct> precursorCharges,
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
                // Only check the library, if this is a custom molecule or a peptide that already has
                // a variable modification, or the library contains some form of the peptide.
                // This is a performance improvement over checking every variable modification
                // of a peptide when it is not even in the library.
                (peptide.IsCustomMolecule || (mods != null && (mods.IsVariableStaticMods || mods.HasCrosslinks)) || LibrariesContainAny(peptide.Target)))
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
                defSet.EnzymeList.SetValue(PeptideSettings.Enzyme);
            // Extra null checks to avoid ReSharper warnings.
            if (PeptideSettings.Prediction != null)
            {
                if (PeptideSettings.Prediction.RetentionTime != null)
                {
                    if (!defSet.RetentionTimeList.Contains(PeptideSettings.Prediction.RetentionTime))
                        defSet.RetentionTimeList.SetValue(PeptideSettings.Prediction.RetentionTime);
                    if (!defSet.RTScoreCalculatorList.Contains(PeptideSettings.Prediction.RetentionTime.Calculator))
                        defSet.RTScoreCalculatorList.SetValue(PeptideSettings.Prediction.RetentionTime.Calculator);
                }
            }
            if (PeptideSettings.Filter != null)
            {
                foreach (PeptideExcludeRegex exclude in PeptideSettings.Filter.Exclusions)
                {
                    if (!defSet.PeptideExcludeList.Contains(exclude))
                        defSet.PeptideExcludeList.SetValue(exclude);
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
                        defSet.SpectralLibraryList.SetValue(librarySpec);
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
                    defSet.CollisionEnergyList.SetValue(prediction.CollisionEnergy);
                if (prediction.DeclusteringPotential != null &&
                        !defSet.DeclusterPotentialList.Contains(prediction.DeclusteringPotential))
                    defSet.DeclusterPotentialList.SetValue(prediction.DeclusteringPotential);
                if (prediction.CompensationVoltage != null &&
                        !defSet.CompensationVoltageList.Contains(prediction.CompensationVoltage))
                    defSet.CompensationVoltageList.SetValue(prediction.CompensationVoltage);
                if (!Equals(prediction.OptimizedLibrary, OptimizationLibrary.NONE) &&
                        Equals(defSet.GetOptimizationLibraryByName(prediction.OptimizedLibrary.Name), OptimizationLibrary.NONE))
                    defSet.OptimizationLibraryList.SetValue(prediction.OptimizedLibrary);
            }
            if (TransitionSettings.Filter != null)
            {
                foreach (var measuredIon in TransitionSettings.Filter.MeasuredIons)
                {
                    if (!defSet.MeasuredIonList.Contains(measuredIon))
                        defSet.MeasuredIonList.SetValue(measuredIon);
                }
            }
            if (TransitionSettings.IonMobilityFiltering != null)
            {
                if (TransitionSettings.IonMobilityFiltering.IonMobilityLibrary != null &&
                    !defSet.IonMobilityLibraryList.Contains(TransitionSettings.IonMobilityFiltering.IonMobilityLibrary))
                {
                    defSet.IonMobilityLibraryList.SetValue(TransitionSettings.IonMobilityFiltering.IonMobilityLibrary);
                }
            }

            if (TransitionSettings.FullScan.IsotopeEnrichments != null)
            {
                if (!defSet.IsotopeEnrichmentsList.Contains(TransitionSettings.FullScan.IsotopeEnrichments))
                {
                    defSet.IsotopeEnrichmentsList.SetValue(TransitionSettings.FullScan.IsotopeEnrichments);
                }
            }
            if (TransitionSettings.FullScan.IsolationScheme != null)
            {
                if (!defSet.IsolationSchemeList.Contains(TransitionSettings.FullScan.IsolationScheme))
                {
                    defSet.IsolationSchemeList.SetValue(TransitionSettings.FullScan.IsolationScheme);
                }
            }
            foreach (var annotationDef in DataSettings.AnnotationDefs)
            {
                if (!defSet.AnnotationDefList.Contains(annotationDef))
                {
                    defSet.AnnotationDefList.SetValue(annotationDef);
                }
            }
            foreach (var groupComparisonDef in DataSettings.GroupComparisonDefs)
            {
                if (!defSet.GroupComparisonDefList.Contains(groupComparisonDef))
                {
                    defSet.GroupComparisonDefList.SetValue(groupComparisonDef);
                }
            }

            foreach (var metadataRuleSet in DataSettings.MetadataRuleSets)
            {
                if (!defSet.MetadataRuleSets.Contains(metadataRuleSet))
                {
                    defSet.MetadataRuleSets.SetValue(metadataRuleSet);
                }
            }
            var mainViewSpecList = defSet.PersistedViews.GetViewSpecList(PersistedViews.MainGroup.Id);
            foreach (var viewSpec in DataSettings.ViewSpecList.ViewSpecLayouts)
            {
                mainViewSpecList = mainViewSpecList.ReplaceView(viewSpec.Name, viewSpec);
            }

            foreach (var listData in DataSettings.Lists)
            {
                var listDef = listData.DeleteAllRows();
                if (!defSet.ListDefList.Contains(listDef))
                {
                    defSet.ListDefList.SetValue(listDef);
                }
            }
            defSet.PersistedViews.SetViewSpecList(PersistedViews.MainGroup.Id, mainViewSpecList);
            if (!PeptideSettings.BackgroundProteome.IsNone)
            {
                if (!defSet.BackgroundProteomeList.Contains(PeptideSettings.BackgroundProteome))
                {
                    defSet.BackgroundProteomeList.SetValue(PeptideSettings.BackgroundProteome);
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

        public SrmSettings ConnectIonMobilityLibrary(Func<IonMobilityLibrary, IonMobilityLibrary> findIonMobilityLibSpec)
        {
            if (TransitionSettings.IonMobilityFiltering.IonMobilityLibrary == null)
                return this;

            var ionMobilityLibrary = TransitionSettings.IonMobilityFiltering.IonMobilityLibrary;
            if (ionMobilityLibrary == null || ionMobilityLibrary.IsNone)
                return this;

            var ionMobilityLibSpec = findIonMobilityLibSpec(ionMobilityLibrary);
            if (ionMobilityLibSpec == null)
            {
                // cancel
                return null;
            }
            if (ionMobilityLibSpec.FilePath == ionMobilityLibrary.FilePath && ionMobilityLibrary.IsUsable)
            {
                return this;
            }

            return this.ChangeTransitionIonMobilityFiltering(predict =>
                predict.ChangeLibrary(!ionMobilityLibSpec.IsNone
                    ? ionMobilityLibSpec
                    : null));
        }

        public SrmSettings ConnectLibrarySpecs(Func<Library, LibrarySpec, LibrarySpec> findLibrarySpec, string docLibPath = null)
        {
            var libraries = PeptideSettings.Libraries;
            bool hasDocLib = libraries.HasDocumentLibrary && null != docLibPath;
            if (!libraries.HasLibraries && !hasDocLib)
                return this;

            int len = libraries.Libraries.Count;
            int docLibShift = hasDocLib ? 1 : 0;
            LibrarySpec[] librarySpecs = new LibrarySpec[len + docLibShift];
            Library[] librariesNew = new Library[librarySpecs.Length];
            for (int i = 0; i < len; i++)
            {
                int iSpec = i + docLibShift;
                var library = libraries.Libraries[i];
                if (library == null)
                {
                    var librarySpec = libraries.LibrarySpecs[i];
                    if (librarySpec == null)
                        throw new InvalidDataException(Resources.SrmSettings_ConnectLibrarySpecs_Settings_missing_library_spec);
                    librarySpecs[iSpec] = librarySpec;
                    if (!File.Exists(librarySpec.FilePath))
                    {
                        librarySpecs[iSpec] = findLibrarySpec(null, librarySpec);
                        if (librarySpecs[iSpec] == null)
                            return null;    // Canceled
                    }

                    continue;
                }

                librariesNew[iSpec] = library;
                librarySpecs[iSpec] = findLibrarySpec(library, null);
                if (librarySpecs[iSpec] == null)
                    return null;    // Canceled
                librarySpecs[iSpec] = librarySpecs[iSpec]
                    .ChangeUseExplicitPeakBounds(library.UseExplicitPeakBounds);
                if (librarySpecs[iSpec].FilePath == null)
                {
                    // Disconnect the libraries, if not canceled, but no path
                    // specified.
                    return ChangePeptideSettings(PeptideSettings.ChangeLibraries(libraries.Disconnect()));
                }
            }

            if (hasDocLib)
            {
                var documentLibrarySpec = BiblioSpecLiteSpec.GetDocumentLibrarySpec(docLibPath);
                librariesNew[0] = BiblioSpecLiteLibrary.GetUnloadedDocumentLibrary(documentLibrarySpec);
                librarySpecs[0] = documentLibrarySpec;
            }

            if (ArrayUtil.EqualsDeep(librarySpecs, libraries.LibrarySpecs))
                return this;

            libraries = libraries.ChangeLibraries(librarySpecs, librariesNew);
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
                        if (defSet.StaticModList.Any(existingMod => Equals(existingMod.Name, modName)))
                        {
                            throw new InvalidDataException(
                                string.Format(Resources.SrmSettings_UpdateDefaultModifications_The_modification__0__already_exists_with_a_different_definition,
                                    modName));
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
                            if (defSet.HeavyModList.Any(existingMod => Equals(existingMod.Name, modName)))
                            {
                                throw new InvalidDataException(
                                    string.Format(Resources.SrmSettings_UpdateDefaultModifications_The_modification__0__already_exists_with_a_different_definition,
                                        modName));
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

        /// <summary>
        /// Removes features from the SrmSettings that are not supported by the particular DocumentFormat.
        /// Only used during DocumentSerializer.WriteXml(). So it is not important that the settings remain
        /// usable to the current implementation.
        /// </summary>
        public SrmSettings RemoveUnsupportedFeatures(DocumentFormat documentFormat)
        {
            var result = this;
            if (documentFormat <= DocumentFormat.VERSION_4_2)
            {
                result = result.ChangeDataSettings(DataSettings.ChangeListDefs(new ListData[0]));
            }
            if (documentFormat < DocumentFormat.VERSION_20_1)
            {
                if (MeasuredResults != null)
                {
                    result = result.ChangeMeasuredResults(MeasuredResults.ChangeChromatograms(
                        MeasuredResults.Chromatograms.Select(c => c.RestoreLegacyUriParameters()).ToArray()));
                }
            }
            if (documentFormat < DocumentFormat.VERSION_21_11)
            {
                result = result.ChangeMeasuredResults(result.MeasuredResults?.ClearImportTimes());
            }
            if (documentFormat < DocumentFormat.TRANSITION_SETTINGS_ION_MOBILITY &&
                !TransitionIonMobilityFiltering.IsNullOrEmpty(result.TransitionSettings.IonMobilityFiltering))
            {
                // Move the ion mobility information from transition settings back to peptide settings where it formerly resided
                result = result.ChangePeptideSettings(result.PeptideSettings.ChangePrediction(
                    result.PeptideSettings.Prediction.ChangeObsoleteIonMobilityValues(result.TransitionSettings
                        .IonMobilityFiltering))).ChangeTransitionSettings(
                    result.TransitionSettings.ChangeIonMobilityFiltering(TransitionIonMobilityFiltering.EMPTY));
            }

            return result;
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
                TransitionIonMobilityFiltering ionMobility = TransitionSettings.IonMobilityFiltering ?? defTran.IonMobilityFiltering;
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
                                                                               fullScan,
                                                                               ionMobility);
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

            if (PeptideSettings?.Prediction?.ObsoleteIonMobilityValues != null &&
                !PeptideSettings.Prediction.ObsoleteIonMobilityValues.IsEmpty)
            {
                // Reading an older format where ion mobility filtering values were peptide settings instead of transition settings
                TransitionSettings =
                    TransitionSettings.ChangeIonMobilityFiltering(PeptideSettings.Prediction.ObsoleteIonMobilityValues);
                PeptideSettings =
                    PeptideSettings.ChangePrediction(PeptideSettings.Prediction.ChangeObsoleteIonMobilityValues(TransitionIonMobilityFiltering.EMPTY));
            }

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
    /// Transient values useful for ChangeSettings
    /// </summary>
    public class DocumentSettingsContext
    {
        public DocumentSettingsContext(IEnumerable<PeptideDocNode> peptideDocNodesPrecalculatedForUniquenessCheck,
            Dictionary<Target, bool> uniquenessDict)
        {
            PeptideDocNodesPrecalculatedForUniquenessCheck = peptideDocNodesPrecalculatedForUniquenessCheck;
            UniquenessDict = uniquenessDict;
        }
        public IEnumerable<PeptideDocNode> PeptideDocNodesPrecalculatedForUniquenessCheck { get; private set; }
        public Dictionary<Target, bool> UniquenessDict { get; private set; }
    }

    /// <summary>
    /// Enum used to specify the representation of modifications in a sequence
    /// </summary>
    public enum SequenceModFormatType { mass_diff, mass_diff_narrow, three_letter_code, full_precision, lib_precision }

    public interface IPrecursorMassCalc
    {
        MassType MassType { get; }
        TypedMass GetPrecursorMass(Target seq);
        TypedMass GetPrecursorMass(CustomMolecule custom, TypedModifications mods, Adduct adductForIsotopeLabels, out string isotopicFormula);
        bool IsModified(Target seq);
        Target GetModifiedSequence(Target seq, bool narrow);
        Target GetModifiedSequence(Target seq, SequenceModFormatType format, bool explicitModsOnly);
        Target GetModifiedSequenceDisplay(Target seq);
        double GetAAModMass(char aa, int seqIndex, int seqLength);
        MassDistribution GetMzDistribution(Target target, Adduct adduct, IsotopeAbundances abundances);
        MassDistribution GetMZDistributionFromFormula(string formula, Adduct adduct, IsotopeAbundances abundances);
        MassDistribution GetMZDistributionSinglePoint(double mz);
        string GetMolecularFormula(string peptideSequence);
        bool HasLabels { get; }
        Adduct GetModifiedAdduct(Adduct adduct, string neutralFormula);
    }

    /// <summary>
    /// Special purpose lookup table for ion types, special indexing
    /// takes into account  the fact that IonType.custom = -1.
    /// This could just be a dictionary of course, but it's a speed thing.
    /// </summary>
    public class IonTable<T>
    {
        private readonly T[,] _store;
        private const IonType _ionType0 = IonType.custom; // We don't expect to see IonType.precursor
        private const int OFFSET_I = -(int)_ionType0;
        public static IonTable<T> EMPTY = new IonTable<T>(_ionType0-1, 0);

        public IonTable(IonType lastIonType, int length)
        {
            _store = new T[(int)lastIonType + OFFSET_I + 1, length];
        }

        public bool ContainsIonType(IonType type)
        {
            return (int)type + OFFSET_I >= 0 && (int)type + OFFSET_I < _store.GetLength(0);
        }
        public int GetLength(int dim)
        {
            return _store.GetLength(dim);
        }

        public T this[IonType ionType, int index]
        {
            get
            {
                return _store[(int)ionType  + OFFSET_I, index];
            }
            set
            {
                _store[(int)ionType + OFFSET_I, index] = value;
            }
        }    
    }


    public interface IFragmentMassCalc
    {
        MassType MassType { get; }
        IonTable<TypedMass> GetFragmentIonMasses(Target seq);
        TypedMass GetFragmentMass(Transition transition, IsotopeDistInfo isotopeDist);
        TypedMass GetPrecursorFragmentMass(Target seq);
    }

    public class SrmSettingsChangeMonitor : IDisposable
    {
        private readonly IProgressMonitor _progressMonitor;
        private readonly string _formatString;
        private readonly IDocumentContainer _documentContainer;
        private readonly SrmDocument _startDocument;

        private IProgressStatus _status;
        private int _groupCount;
        private int? _moleculeCount;
        private int _seenGroupCount;
        private int _seenMoleculeCount;

        public SrmSettingsChangeMonitor(IProgressMonitor progressMonitor, string formatString, IProgressStatus status)
        {
            _progressMonitor = progressMonitor;
            if (formatString.Contains('{'))
                _formatString = formatString;
            _status = status;
            if (_status == null)
            {
                if (_formatString == null)
                    _status = new ProgressStatus(formatString);
                else
                {
                    // Set status string to empty, since it should be reset very quickly
                    _status = new ProgressStatus(string.Empty);
                }
            }
        }

        public SrmSettingsChangeMonitor(IProgressMonitor progressMonitor, string formatString,
            IDocumentContainer documentContainer = null, SrmDocument startDocument = null)
            :this(progressMonitor, formatString, null)
        {
            _documentContainer = documentContainer;
            _startDocument = startDocument;
            if (_startDocument == null && documentContainer != null)
                _startDocument = documentContainer.Document;
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

        public void ChangeProgress(Func<IProgressStatus, IProgressStatus> change)
        {
            var status = _status;
            var statusNew = change(status);
            if (ReferenceEquals(status, Interlocked.CompareExchange(ref _status, statusNew, status)))
                _progressMonitor.UpdateProgress(statusNew);
        }

        public bool IsCanceled()
        {
            return _progressMonitor.IsCanceled;
        }

        public void Dispose()
        {
            if (_seenGroupCount + _seenMoleculeCount > 0)
                _progressMonitor.UpdateProgress(_status = _status.Complete());
        }

        public int GroupCount
        {
            // Avoid divide by zero errors by always having at least 1 group
            get {  return _groupCount != 0 ? _groupCount : 1; }
            set { _groupCount = value; }
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
            bool diffTransitions, bool diffTransitionProps,
            SrmSettingsChangeMonitor monitor = null)
        {
            DiffPeptides = diffPeptides;
            DiffPeptideProps = diffPeptideProps;
            DiffTransitionGroups = diffTransitionGroups;
            DiffTransitionGroupProps = diffTransitionGroupProps;
            DiffTransitions = diffTransitions;
            DiffTransitionProps = diffTransitionProps;
            Monitor = monitor;
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
            bool precursorsDiff = !ArrayUtil.EqualsDeep(newTran.Filter.PeptidePrecursorCharges,
                                                        oldTran.Filter.PeptidePrecursorCharges) ||
                                  !ArrayUtil.EqualsDeep(newTran.Filter.SmallMoleculePrecursorAdducts,
                                                        oldTran.Filter.SmallMoleculePrecursorAdducts) ||
                                  // Also changing auto-select could change precursors
                                  newTran.Filter.AutoSelect != oldTran.Filter.AutoSelect ||
                                  // And changing DIA isolation scheme could change precursors
                                  !Equals(newTran.FullScan.IsolationScheme, oldTran.FullScan.IsolationScheme);

            // Background proteome uniqueness constraints - only considered a change if constraint type
            // changes, or if constraint is non-None and background proteome or digestion enzyme changes
            // Background proteome uniqueness constraints - only considered a change if constraint type
            // changes, or if constraint is non-None and background proteome or digestion enzyme changes
            bool uniquenessConstraintChange = !Equals(newPep.Filter.PeptideUniqueness, oldPep.Filter.PeptideUniqueness);
            if (!uniquenessConstraintChange && newPep.Filter.PeptideUniqueness != PeptideFilter.PeptideUniquenessConstraint.none)
            {
                if (newPep.BackgroundProteome != null || oldPep.BackgroundProteome != null)
                {
                    if (newPep.BackgroundProteome == null || oldPep.BackgroundProteome == null)
                    {
                        uniquenessConstraintChange = true;
                    }
                    else
                    {
                        uniquenessConstraintChange = !newPep.BackgroundProteome.EqualsSpec(oldPep.BackgroundProteome);
                    }
                }
                uniquenessConstraintChange = uniquenessConstraintChange || !newPep.DigestSettings.Equals(oldPep.DigestSettings);
            }

            // Change peptides if enzyme, digestion or filter settings changed
            DiffPeptides = !newPep.Enzyme.Equals(oldPep.Enzyme) ||
                                  !newPep.DigestSettings.Equals(oldPep.DigestSettings) ||
                                  !newPep.Filter.Equals(oldPep.Filter) ||
                                  uniquenessConstraintChange ||
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
            using (var enumNewHeavyMods = newMods.GetHeavyModifications().GetEnumerator())
            {
                foreach (var oldTypedMods in oldMods.GetHeavyModifications())
                {
                    if (!enumNewHeavyMods.MoveNext()) // synch with foreach
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
            }

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
            bool diffMinIonCount = !Equals(newTran.Libraries.MinIonCount, oldTran.Libraries.MinIonCount);
            DiffTransitionGroups = precursorsDiff || diffHeavyMods || diffInstrumentRange || diffIsolationScheme || diffMinIonCount;

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

            // Any time the transition groups can change, that can change the accepted peptides
            DiffPeptides = DiffPeptides || DiffTransitionGroups;

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
                              (newTran.FullScan.PrecursorIsotopes != FullScanPrecursorIsotopes.None && enrichmentsChanged) ||
                              !Equals(newTran.FullScan.PrecursorRes, oldTran.FullScan.PrecursorRes) ||
                              !Equals(newTran.FullScan.PrecursorResMz, oldTran.FullScan.PrecursorResMz);

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
                                 (newTran.FullScan.PrecursorIsotopes != FullScanPrecursorIsotopes.None && enrichmentsChanged) ||
                                  !Equals(newTran.FullScan.PrecursorRes, oldTran.FullScan.PrecursorRes) ||
                                  !Equals(newTran.FullScan.PrecursorResMz, oldTran.FullScan.PrecursorResMz)
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

            if (settingsNew.PeptideSettings.Quantification.SimpleRatios !=
                settingsOld.PeptideSettings.Quantification.SimpleRatios)
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
            if (!measuredResultsNew.CachedFileInfos.Select(info => info.ImportTime)
                .SequenceEqual(measuredResultsOld.CachedFileInfos.Select(info => info.ImportTime)))
            {
                return false;
            }
            for (int i = 0; i < measuredResultsNew.Chromatograms.Count; i++)
            {
                var chromatogramSetNew = measuredResultsNew.Chromatograms[i].ChangeAnnotations(Annotations.EMPTY).ChangeUseForRetentionTimeFilter(false)
                    .ChangeAnalyteConcentration(null).ChangeSampleType(SampleType.DEFAULT).ChangeName(string.Empty);
                var chromatogramSetOld = measuredResultsOld.Chromatograms[i].ChangeAnnotations(Annotations.EMPTY).ChangeUseForRetentionTimeFilter(false)
                    .ChangeAnalyteConcentration(null).ChangeSampleType(SampleType.DEFAULT).ChangeName(string.Empty);
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
                if (oldLibrary == null || newLibrary == null)
                {
                    return false;
                }
                if (oldLib.LibrarySpecs[i].IsDocumentLibrary && newLib.LibrarySpecs[i].IsDocumentLibrary)
                {
                    // Old library and new libary are not the same during loading, since
                    // we do not save out the LSID for the document library. Avoid recalculating
                    // library settings during document Open.
                    if (!oldLibrary.IsSameLibrary(newLibrary))
                        continue;
                }
                if (// Do not check for difference in loaded state!!  This will cause
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
