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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
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
        public SrmSettings(string name, PeptideSettings peptideSettings, TransitionSettings transitionSettings, DataSettings dataSettings)
            : base(name)
        {
            PeptideSettings = peptideSettings;
            TransitionSettings = transitionSettings;
            DataSettings = dataSettings;

            // Create cached calculator instances
            CreatePrecursorMassCalcs();
            CreateFragmentMassCalcs();
        }

        public PeptideSettings PeptideSettings { get; private set; }

        public TransitionSettings TransitionSettings { get; private set; }

        public DataSettings DataSettings { get; private set; }

        public MeasuredResults MeasuredResults { get; private set; }

        public bool HasResults { get { return MeasuredResults != null; } }

        public bool HasLibraries { get { return PeptideSettings.Libraries.HasLibraries; } }

        public bool HasBackgroundProteome { get { return !PeptideSettings.BackgroundProteome.IsNone; } }

        public bool IsLoaded
        {
            get
            {
                return (!HasResults || MeasuredResults.IsLoaded) &&
                       (!HasLibraries || PeptideSettings.Libraries.IsLoaded);
                // BackgroundProteome?
            }
        }

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
                    else if (mod.Modification.RelativeRT == RelativeRT.Preceding)
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
                    else if (mod.RelativeRT == RelativeRT.Preceding)
                        relativeRT = RelativeRT.Preceding;
                    else if (mod.RelativeRT == RelativeRT.Overlapping &&
                            relativeRT == RelativeRT.Matching)
                        relativeRT = RelativeRT.Overlapping;
                }
            }
            return relativeRT;
        }
        
        // Cached calculators
        private ReadOnlyCollection<TypedMassCalc> _precursorMassCalcs;
        private ReadOnlyCollection<TypedMassCalc> _fragmentMassCalcs;

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
            return GetPrecursorCalc(labelType, mods) != null;
        }

        public IPrecursorMassCalc GetPrecursorCalc(IsotopeLabelType labelType, ExplicitMods mods)
        {
            var massCalcBase = GetBaseCalc(labelType, mods, _precursorMassCalcs);
            if (massCalcBase != null)
            {
                // If this type is not explicitly modified, then it must be
                // heavy with explicit light modifications.
                if (!mods.IsModified(labelType))
                    labelType = IsotopeLabelType.light;
                if (!labelType.IsLight && !mods.HasModifications(labelType))
                    return null;
                return new ExplicitSequenceMassCalc(massCalcBase,
                                                    mods.GetModMasses(massCalcBase.MassType, labelType).ToArray(),
                                                    mods.IsVariableStaticMods);
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
                return new ExplicitSequenceMassCalc(massCalcBase,
                                                    mods.GetModMasses(massCalcBase.MassType, labelType),
                                                    mods.IsVariableStaticMods);
            }
            return GetMassCalc(labelType, _fragmentMassCalcs);
        }

        public double GetFragmentMass(Transition transition, ExplicitMods mods)
        {
            return GetFragmentMass(IsotopeLabelType.light, mods, transition);
        }

        public double GetFragmentMass(IsotopeLabelType labelType, ExplicitMods mods, Transition transition)
        {
            return GetFragmentCalc(labelType, mods).GetFragmentMass(transition);
        }

        public string GetModifiedSequence(string seq, IsotopeLabelType labelType, ExplicitMods mods)
        {
            return GetPrecursorCalc(labelType, mods).GetModifiedSequence(seq, false);
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
                double massH = GetPrecursorMass(IsotopeLabelType.light,
                    nodePep.Peptide.Sequence, nodePep.ExplicitMods);
                mz = SequenceMassCalc.GetMZ(massH, nodeGroup.TransitionGroup.PrecursorCharge);
            }
            return mz;
        }

        #region Property change methods

        public SrmSettings ChangePeptideSettings(PeptideSettings prop)
        {
            SrmSettings settings = ChangeProp(ImClone(this), (im, v) => im.PeptideSettings = v, prop);

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
            SrmSettings settings = ChangeProp(ImClone(this), (im, v) => im.TransitionSettings = v, prop);

            if (prop.Prediction.PrecursorMassType != TransitionSettings.Prediction.PrecursorMassType)
                settings.CreatePrecursorMassCalcs();
            if (prop.Prediction.FragmentMassType != TransitionSettings.Prediction.FragmentMassType)
                settings.CreateFragmentMassCalcs();

            return settings;
        }

        public SrmSettings ChangeDataSettings(DataSettings prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.DataSettings = prop, prop);
        }

        public SrmSettings ChangeMeasuredResults(MeasuredResults prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.MeasuredResults = v, prop);
        }
        
        public SrmSettings MakeSavable()
        {
            return MakeSavable(Name);
        }

        public SrmSettings MakeSavable(string saveName)
        {
            // If the name is already set, and there are no measured results
            // then this instance will do.
            if (Equals(Name, saveName) && MeasuredResults == null)
                return this;

            // Change the name, and remove results information which is document specific
            SrmSettings settingsSavable = (SrmSettings) ChangeName(saveName);
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

        private ReadOnlyCollection<TypedMassCalc> CreateMassCalcs(MassType type)
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

        public bool LibrariesContainMeasurablePeptide(Peptide peptide, IEnumerable<int> precursorCharges, ExplicitMods mods)
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
            return TransitionSettings.Instrument.IsMeasurable(precursorMz);
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
            var libraries = PeptideSettings.Libraries;

            type = IsotopeLabelType.light;

            string sequenceMod = GetModifiedSequence(sequence, IsotopeLabelType.light, mods);
            if (libraries.TryGetLibInfo(new LibKey(sequenceMod, charge), out libInfo))
                return true;

            foreach (var labelType in GetHeavyLabelTypes(mods))
            {
                // If light version not found, try heavy
                sequenceMod = GetModifiedSequence(sequence, labelType, mods);
                if (libraries.TryGetLibInfo(new LibKey(sequenceMod, charge), out libInfo))
                {
                    type = labelType;
                    return true;
                }
            }

            libInfo = null;
            return false;
        }

        public bool TryLoadSpectrum(string sequence, int charge, ExplicitMods mods,
            out IsotopeLabelType type, out SpectrumPeaksInfo spectrum)
        {
            var libraries = PeptideSettings.Libraries;

            type = IsotopeLabelType.light;

            string sequenceMod = GetModifiedSequence(sequence, IsotopeLabelType.light, mods);
            if (libraries.TryLoadSpectrum(new LibKey(sequenceMod, charge), out spectrum))
                return true;

            foreach (var labelType in GetHeavyLabelTypes(mods))
            {
                // If light version not found, try heavy
                sequenceMod = GetModifiedSequence(sequence, labelType, mods);
                if (libraries.TryLoadSpectrum(new LibKey(sequenceMod, charge), out spectrum))
                {
                    type = labelType;
                    return true;
                }
            }

            spectrum = null;
            return false;
        }

        /// <summary>
        /// Loads a list of all the spectra found in all loaded libraries 
        /// matching the criteria passed in.
        /// </summary>
        /// <param name="sequence"> The sequence to match. </param>
        /// <param name="charge"> The charge to match. </param>
        /// <param name="mods"> The modifications to match. </param>
        /// <param name="spectra"> Used to return a list of the matching spectra. </param>
        /// <returns> Returns true if at least one spectrum was found; false otherwise. </returns>
        public bool TryLoadSpectra(string sequence, int charge, ExplicitMods mods,
            out IList<SpectrumInfo> spectra)
        {
            var libraries = PeptideSettings.Libraries;

            spectra = new List<SpectrumInfo>();

            string sequenceMod = GetModifiedSequence(sequence, IsotopeLabelType.light, mods);
            libraries.AddSpectra(new LibKey(sequenceMod, charge), IsotopeLabelType.light, ref spectra);

            foreach (var labelType in GetHeavyLabelTypes(mods))
            {
                sequenceMod = GetModifiedSequence(sequence, labelType, mods);
                libraries.AddSpectra(new LibKey(sequenceMod, charge), labelType, ref spectra);
            }

            return spectra.Count > 0;
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

        public bool Accept(Peptide peptide, ExplicitMods explicitMods, out bool allowVariableMods)
        {
            return Accept(peptide,
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

        /// <summary>
        /// This version of Accept is used to select transition groups after the peptide
        /// itself has already been screened.  For this reason, it only applies library
        /// filtering.
        /// </summary>
        public bool Accept(Peptide peptide, ExplicitMods mods, int charge)
        {
            bool allowVariableMods;
            return Accept(peptide, mods, new[] { charge }, PeptideFilterType.library, out allowVariableMods);
        }

        private enum PeptideFilterType
        {
            full,   // Filter all peptides with both filter and library settings
            fasta,  // Apply filter settings only to peptides with an associated FASTA sequence
            library // Only filter with library settings
        }

        private bool Accept(Peptide peptide,
                            ExplicitMods mods,
                            IEnumerable<int> precursorCharges,
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
                return PeptideSettings.Filter.Accept(peptide, null, out allowVariableMods);
            }

            // Check if the peptide is in the library for one of the
            // acceptable precursor charges.
            bool inLibrary = false;
            // If the libraries are not fully loaded, then act like nothing
            // could be found in the libraries.  This will be corrected when
            // the libraries are loaded.
            if (libraries.IsLoaded && 
                // Only check the library, if this is already a variable modification,
                // or the library contains some form of the peptide.
                ((mods != null && mods.IsVariableStaticMods) || LibrariesContainAny(peptide.Sequence)))
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
                    return inLibrary && (!useFilter || PeptideSettings.Filter.Accept(peptide, null, out allowVariableMods));
                default:
                    return inLibrary || (!useFilter || PeptideSettings.Filter.Accept(peptide, null, out allowVariableMods));
            }
        }

        #endregion

        public void UpdateLists()
        {
            Settings defSet = Settings.Default;

            // Make sure all settings are contained in the appropriate lists.
            // CONSIDER: Simple List.Contains() checks mean that values with the same name
            //           but differing values will be overwritten.
            if (!defSet.EnzymeList.Contains(PeptideSettings.Enzyme))
                defSet.EnzymeList.Add(PeptideSettings.Enzyme);
            // Extra null checks to avoid ReSharper warnings.
            if (PeptideSettings.Prediction != null &&
                    PeptideSettings.Prediction.RetentionTime != null &&
                    !defSet.RetentionTimeList.Contains(PeptideSettings.Prediction.RetentionTime))
                defSet.RetentionTimeList.Add(PeptideSettings.Prediction.RetentionTime);
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
            UpdateDefaultModifications(true);
            if (TransitionSettings.Prediction != null)
            {
                TransitionPrediction prediction = TransitionSettings.Prediction;
                if (!defSet.CollisionEnergyList.Contains(prediction.CollisionEnergy))
                    defSet.CollisionEnergyList.Add(prediction.CollisionEnergy);
                if (prediction.DeclusteringPotential != null &&
                        !defSet.DeclusterPotentialList.Contains(prediction.DeclusteringPotential))
                    defSet.DeclusterPotentialList.Add(prediction.DeclusteringPotential);
            }
            if (TransitionSettings.Filter != null)
            {
                foreach (var measuredIon in TransitionSettings.Filter.MeasuredIons)
                {
                    if (!defSet.MeasuredIonList.Contains(measuredIon))
                        defSet.MeasuredIonList.Add(measuredIon);
                }
            }
            foreach (var annotationDef in DataSettings.AnnotationDefs)
            {
                if (!defSet.AnnotationDefList.ContainsKey(annotationDef.Name))
                {
                    defSet.AnnotationDefList.Add(annotationDef);
                }
            }
            if (!PeptideSettings.BackgroundProteome.IsNone)
            {
                if (!defSet.BackgroundProteomeList.ContainsKey(PeptideSettings.BackgroundProteome.Name))
                {
                    defSet.BackgroundProteomeList.Add(PeptideSettings.BackgroundProteome);
                }
            }
        }

        public SrmSettings ConnectLibrarySpecs(Func<Library, LibrarySpec> findLibrarySpec)
        {
            var libraries = PeptideSettings.Libraries;
            if (!libraries.HasLibraries)
                return this;

            LibrarySpec[] librarySpecs = new LibrarySpec[libraries.Libraries.Count];
            for (int i = 0; i < librarySpecs.Length; i++)
            {
                var library = libraries.Libraries[i];
                if (library == null)
                {
                    librarySpecs[i] = libraries.LibrarySpecs[i];
                    if (librarySpecs[i] == null)
                        throw new InvalidDataException("Settings missing library spec.");
                    continue;
                }

                librarySpecs[i] = findLibrarySpec(library);
                if (librarySpecs[i] == null)
                    return null;    // Canceled
                if (librarySpecs[i].FilePath == null)
                {
                    // Disconnect the libraries, if not canceled, but no path
                    // specified.
                    return ChangePeptideSettings(PeptideSettings.ChangeLibraries(libraries.Disconnect()));
                }
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
            var defSet = Settings.Default;

            IList<StaticMod> newStaticMods = new List<StaticMod>();
            IList<StaticMod> newHeavyMods = new List<StaticMod>();

            foreach (var mod in PeptideSettings.Modifications.GetModifications(IsotopeLabelType.light))
            {
                if (!defSet.StaticModList.Contains(mod.ChangeExplicit(true)) && !defSet.StaticModList.Contains(mod.ChangeExplicit(false)) &&
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
                                throw new InvalidDataException(
                                    string.Format("The modification '{0}' already exists with a different definition.", modName));
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
                                    throw new InvalidDataException(
                                        string.Format("The modification '{0}' already exists with a different definition.", modName));
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
                Enzyme enzyme = PeptideSettings.Enzyme ?? defPep.Enzyme;
                DigestSettings digestSettings = PeptideSettings.DigestSettings ?? defPep.DigestSettings;
                PeptidePrediction prediction = PeptideSettings.Prediction ?? defPep.Prediction;
                PeptideFilter filter = PeptideSettings.Filter ?? defPep.Filter;
                PeptideLibraries libraries = PeptideSettings.Libraries ?? defPep.Libraries;
                BackgroundProteome backgroundProteome = PeptideSettings.BackgroundProteome ?? defPep.BackgroundProteome;
                PeptideModifications modifications = PeptideSettings.Modifications ?? defPep.Modifications;

                PeptideSettings peptideSettings = new PeptideSettings(enzyme, digestSettings, prediction, filter,
                    libraries, modifications, backgroundProteome);
                // If the above null checks result in a changed PeptideSettings object,
                // then use the changed version.
                if (!Equals(PeptideSettings, peptideSettings))
                    PeptideSettings = peptideSettings;
            }

            TransitionSettings defTran = defaults.TransitionSettings;
            if (TransitionSettings == null)
                TransitionSettings = defaults.TransitionSettings;
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
                        PeptideSettings = new PeptideSettings(PeptideSettings.Enzyme,
                                                              PeptideSettings.DigestSettings,
                                                              new PeptidePrediction(prediction.RetentionTime),
                                                              PeptideSettings.Filter,
                                                              PeptideSettings.Libraries,
                                                              PeptideSettings.Modifications,
                                                              PeptideSettings.BackgroundProteome);
                    }
                    prediction = new TransitionPrediction(prediction);
                }

                TransitionFilter filter = TransitionSettings.Filter ?? defTran.Filter;
                TransitionLibraries libraries = TransitionSettings.Libraries ?? defTran.Libraries;
                TransitionIntegration integration = TransitionSettings.Integration ?? defTran.Integration;
                TransitionInstrument instrument = TransitionSettings.Instrument ?? defTran.Instrument;
                TransitionSettings transitionSettings = new TransitionSettings(prediction,
                                                                               filter,
                                                                               libraries,
                                                                               integration,
                                                                               instrument);
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
            MeasuredResults = reader.DeserializeElement<MeasuredResults>();
            DataSettings = reader.DeserializeElement<DataSettings>() ?? new DataSettings(new AnnotationDef[0]);
            reader.ReadEndElement();
            ValidateLoad();
        }

        public override void WriteXml(XmlWriter writer)
        {
            // Write tag attributes
            base.WriteXml(writer);
            writer.WriteElement(PeptideSettings);
            writer.WriteElement(TransitionSettings);
            if (MeasuredResults != null)
                writer.WriteElement(MeasuredResults);
            writer.WriteElement(DataSettings);
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
                Equals(obj.DataSettings, DataSettings);
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
                return result;
            }
        }

        #endregion
    }

    public interface IPrecursorMassCalc
    {
        MassType MassType { get;}
        double GetPrecursorMass(string seq);
        bool IsModified(string seq);
        string GetModifiedSequence(string seq, bool formatNarrow);
    }

    public interface IFragmentMassCalc
    {
        MassType MassType { get; }
        double[,] GetFragmentIonMasses(string seq);
        double GetFragmentMass(Transition transition);
        double GetFragmentMass(string seq, IonType type, int ordinal);
        double GetPrecursorFragmentMass(string seq);
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
                return new SrmSettingsDiff
                { DiffPeptides = false, DiffTransitionGroups = false, DiffTransitions = false };
            }
        }
// ReSharper restore InconsistentNaming

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
            // DiffPeptideProps = true; Currently not possible
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

        public SrmSettingsDiff(SrmSettings settingsCurrent, bool allResults)
        {
            SettingsOld = settingsCurrent;

            DiffResults = true;
            DiffResultsAll = allResults;
        }

        public SrmSettingsDiff(SrmSettings settingsOld, SrmSettings settingsNew)
        {
            SettingsOld = settingsOld;

            PeptideSettings newPep = settingsNew.PeptideSettings;
            PeptideSettings oldPep = settingsOld.PeptideSettings;
            TransitionSettings newTran = settingsNew.TransitionSettings;
            TransitionSettings oldTran = settingsOld.TransitionSettings;

            // Figure out whether precursor charges differ for determining
            // both peptide and transition group changes.
            bool precursorsDiff = !ArrayUtil.EqualsDeep(newTran.Filter.PrecursorCharges,
                                                        oldTran.Filter.PrecursorCharges);

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

            // Currently no calculated values on peptides
            DiffPeptideProps = false;

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
                if (!Equals(newTypedMods.LabelType, oldTypedMods.LabelType) ||
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
            // existence changed
            bool diffInstrumentRange = newTran.Instrument.MinMz != oldTran.Instrument.MinMz ||
                                       newTran.Instrument.MaxMz != oldTran.Instrument.MaxMz;
            DiffTransitionGroups = precursorsDiff || diffHeavyMods || diffInstrumentRange;                

            // If libraries changed, then transition groups should change whenever
            // peptides change also.
            if (!DiffTransitionGroups && libraryChange)
                DiffTransitionGroups = DiffPeptides;

            // Any change in modifications or precursor mass-type forces a recalc
            // of precursor m/z values, as
            DiffTransitionGroupProps = diffStaticMods || diffHeavyMods ||
                                 !newTran.Prediction.PrecursorMassType.Equals(oldTran.Prediction.PrecursorMassType);

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
                                                    oldPep.Modifications.NeutralLossModifications.ToArray());

            // Any change in modifications or fragment mass-type forces a recalc
            // of transition m/z values, as
            DiffTransitionProps = diffStaticMods || diffHeavyMods ||
                                 !newTran.Prediction.FragmentMassType.Equals(oldTran.Prediction.FragmentMassType) ||
                                 (libraryChange && DiffTransitionGroupProps) ||
                                 // Any change in transitions can change transition rankings
                                 // if a library is in use.
                                 (newLib.HasLibraries && DiffTransitions);

            // If the results changed, then update the results information which has changed
            DiffResults = !ReferenceEquals(settingsNew.MeasuredResults, settingsOld.MeasuredResults);
            // If the integration strategy has changed, then force a full update of all results
            if (newTran.Integration.IsIntegrateAll != oldTran.Integration.IsIntegrateAll)
                DiffResults = DiffResultsAll = true;
            // If the match tolerance has changed, then force a full update of all results
            if (newTran.Instrument.MzMatchTolerance != oldTran.Instrument.MzMatchTolerance)
                DiffResults = DiffResultsAll = true;
            // If internal standard type changed, update all results to recalculate ratios.
            if (!ArrayUtil.EqualsDeep(newMods.InternalStandardTypes, oldMods.InternalStandardTypes))
            {
                DiffResults = true;
            }
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
                    !oldLibrary.IsSameLibrary(newLibrary) ||
                    oldLibrary.CompareRevisions(newLibrary) != 0)
                {
                    return false;
                }
            }
            return true;
        }

        public SrmSettings SettingsOld { get; private set; }

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
    }
}