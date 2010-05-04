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

        public RelativeRT GetRelativeRT(IsotopeLabelType type, string seq, ExplicitMods mods)
        {
            if (type == IsotopeLabelType.light)
                return RelativeRT.Matching;
            // Default is matching
            RelativeRT relativeRT = RelativeRT.Matching;
            // One unkown modification makes everything unknown
            // One preceding modification with no unknowns make relative RT preceding
            // Overlapping overrides matching
            if (mods != null)
            {
                foreach (var mod in mods.HeavyModifications)
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
                foreach (var mod in PeptideSettings.Modifications.HeavyModifications)
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
        private SequenceMassCalc PrecursorMassCalc { get; set; }

        private SequenceMassCalc PrecursorHeavyCalc { get; set; }

        private SequenceMassCalc FragmentMassCalc { get; set; }

        private SequenceMassCalc FragmentHeavyCalc { get; set; }

        private static SequenceMassCalc GetBaseCalc(SequenceMassCalc calcImplicit)
        {
            return (calcImplicit.MassType == MassType.Monoisotopic ?
                MonoisotopicMassCalc : AverageMassCalc);            
        }

        public bool HasPrecursorCalc(IsotopeLabelType type, ExplicitMods mods)
        {
            return (type == IsotopeLabelType.light ||
                (mods == null ? PrecursorHeavyCalc != null : mods.HasHeavyModifications));
        }

        public IPrecursorMassCalc GetPrecursorCalc(IsotopeLabelType type, ExplicitMods mods)
        {
            if (mods != null)
            {
                if (type != IsotopeLabelType.light && !mods.HasHeavyModifications)
                    return null;
                var massCalcBase = GetBaseCalc(PrecursorMassCalc);
                return new ExplicitSequenceMassCalc(massCalcBase, mods.GetModMasses(massCalcBase.MassType, type));
            }
            return (type == IsotopeLabelType.heavy ? PrecursorHeavyCalc : PrecursorMassCalc);
        }

        public double GetPrecursorMass(IsotopeLabelType type, string seq, ExplicitMods mods)
        {
            return GetPrecursorCalc(type, mods).GetPrecursorMass(seq);
        }

        public IFragmentMassCalc GetFragmentCalc(IsotopeLabelType type, ExplicitMods mods)
        {
            if (mods != null)
            {
                if (type != IsotopeLabelType.light && !mods.HasHeavyModifications)
                    return null;
                var massCalcBase = GetBaseCalc(FragmentMassCalc);
                return new ExplicitSequenceMassCalc(massCalcBase, mods.GetModMasses(massCalcBase.MassType, type));
            }
            return (type == IsotopeLabelType.heavy ? FragmentHeavyCalc : FragmentMassCalc);
        }

        public double GetFragmentMass(Transition transition, ExplicitMods mods)
        {
            return GetFragmentMass(IsotopeLabelType.light, mods, transition);
        }

        public double GetFragmentMass(IsotopeLabelType type, ExplicitMods mods, Transition transition)
        {
            return GetFragmentCalc(type, mods).GetFragmentMass(transition);
        }

        public string GetModifiedSequence(string seq, IsotopeLabelType type, ExplicitMods mods)
        {
            return GetPrecursorCalc(type, mods).GetModifiedSequence(seq, false);
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
            if (nodeGroup.TransitionGroup.LabelType != IsotopeLabelType.light)
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
            SequenceMassCalc light, heavy;
            CreateMassCalcs(TransitionSettings.Prediction.PrecursorMassType, out light, out heavy);
            PrecursorMassCalc = light;
            PrecursorHeavyCalc = heavy;
        }

        private void CreateFragmentMassCalcs()
        {
            SequenceMassCalc light, heavy;
            CreateMassCalcs(TransitionSettings.Prediction.FragmentMassType, out light, out heavy);
            FragmentMassCalc = light;
            FragmentHeavyCalc = heavy;
        }

        private void CreateMassCalcs(MassType type, out SequenceMassCalc light, out SequenceMassCalc heavy)
        {
            var mods = PeptideSettings.Modifications;

            var modsStatic = mods.StaticModifications;
            light = CreateMassCalc(type, modsStatic);

            if (!mods.HasHeavyImplicitModifications)
                heavy = null;
            else
            {
                var modsHeavy = mods.HeavyModifications;
                heavy = CreateMassCalc(type, modsStatic, modsHeavy);
            }
        }

        private static SequenceMassCalc CreateMassCalc(MassType type, params IEnumerable<StaticMod>[] arrayMods)
        {
            SequenceMassCalc calc = new SequenceMassCalc(type);
            foreach (IEnumerable<StaticMod> mods in arrayMods)
            {
                // Add implicit modifications to the mass calculator
                calc.AddStaticModifications(from mod in mods
                                            where !mod.IsExplicit
                                            select mod);
            }
            return calc;
        }

        public bool Contains(string sequence, int charge, ExplicitMods mods,
            out IsotopeLabelType type)
        {
            var libraries = PeptideSettings.Libraries;

            type = IsotopeLabelType.light;

            string sequenceMod = GetModifiedSequence(sequence, IsotopeLabelType.light, mods);
            if (libraries.Contains(new LibKey(sequenceMod, charge)))
                return true;

            if (HasPrecursorCalc(IsotopeLabelType.heavy, mods))
            {
                // If light version not found, try heavy
                sequenceMod = GetModifiedSequence(sequence, IsotopeLabelType.heavy, mods);
                if (libraries.Contains(new LibKey(sequenceMod, charge)))
                {
                    type = IsotopeLabelType.heavy;
                    return true;
                }                
            }
            return false;
        }

        public bool TryGetLibInfo(string sequence, int charge, ExplicitMods mods,
            out IsotopeLabelType type, out SpectrumHeaderInfo libInfo)
        {
            var libraries = PeptideSettings.Libraries;

            type = IsotopeLabelType.light;

            string sequenceMod = GetModifiedSequence(sequence, IsotopeLabelType.light, mods);
            if (libraries.TryGetLibInfo(new LibKey(sequenceMod, charge), out libInfo))
                return true;

            if (HasPrecursorCalc(IsotopeLabelType.heavy, mods))
            {
                // If light version not found, try heavy
                sequenceMod = GetModifiedSequence(sequence, IsotopeLabelType.heavy, mods);
                if (libraries.TryGetLibInfo(new LibKey(sequenceMod, charge), out libInfo))
                {
                    type = IsotopeLabelType.heavy;
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

            if (HasPrecursorCalc(IsotopeLabelType.heavy, mods))
            {
                // If light version not found, try heavy
                sequenceMod = GetModifiedSequence(sequence, IsotopeLabelType.heavy, mods);
                if (libraries.TryLoadSpectrum(new LibKey(sequenceMod, charge), out spectrum))
                {
                    type = IsotopeLabelType.heavy;
                    return true;
                }
            }

            spectrum = null;
            return false;
        }

        #region Implementation of IPeptideFilter

        public bool Accept(Peptide peptide)
        {
            return Accept(peptide, null, TransitionSettings.Filter.PrecursorCharges, false);
        }

        public bool Accept(Peptide peptide, bool filterUserPeptides)
        {
            return Accept(peptide, null, TransitionSettings.Filter.PrecursorCharges, filterUserPeptides);            
        }

        public bool Accept(Peptide peptide, ExplicitMods mods, int charge)
        {
            return Accept(peptide, mods, new[] {charge}, false);
        }

        private bool Accept(Peptide peptide, ExplicitMods mods, IEnumerable<int> precursorCharges, bool filterUserPeptides)
        {
            var libraries = PeptideSettings.Libraries;
            if (!libraries.HasLibraries || libraries.Pick == PeptidePick.filter)
            {
                // Only filter user specified peptides based on the heuristic
                // filter when explicitly requested.
                if (!filterUserPeptides && !peptide.Begin.HasValue)
                    return true;

                return PeptideSettings.Filter.Accept(peptide);
            }

            // Check if the peptide is in the library for one of the
            // acceptable precursor charges.
            bool inLibrary = false;
            // If the libraries are not fully loaded, then act like nothing
            // could be found in the libraries.  This will be corrected when
            // the libraries are loaded.
            if (libraries.IsLoaded)
            {
                double? precursorMass = null;
                foreach (int charge in precursorCharges)
                {
                    IsotopeLabelType type;
                    if (Contains(peptide.Sequence, charge, mods, out type))
                    {
                        // It is in the library.  Make sure it is measurable on the selected instrument.
                        precursorMass = precursorMass ??
                            GetPrecursorMass(IsotopeLabelType.light, peptide.Sequence, null);
                        double precursorMz = SequenceMassCalc.GetMZ(precursorMass.Value, charge);
                        var instrument = TransitionSettings.Instrument;
                        if (instrument.IsMeasurable(precursorMz))
                            inLibrary = true;
                    }
                }
            }

            switch (libraries.Pick)
            {
                case PeptidePick.library:
                    return inLibrary;
                case PeptidePick.both:
                    return inLibrary && PeptideSettings.Filter.Accept(peptide);
                default:
                    return inLibrary || PeptideSettings.Filter.Accept(peptide);
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
            if (PeptideSettings.Modifications != null)
            {
                foreach (StaticMod mod in PeptideSettings.Modifications.StaticModifications)
                {
                    if (!defSet.StaticModList.Contains(mod))
                        defSet.StaticModList.Add(mod.IsExplicit ? mod.ChangeExplicit(false) : mod);
                }
                foreach (StaticMod mod in PeptideSettings.Modifications.HeavyModifications)
                {
                    if (!defSet.HeavyModList.Contains(mod))
                        defSet.HeavyModList.Add(mod.IsExplicit ? mod.ChangeExplicit(false) : mod);
                }
            }
            if (TransitionSettings.Prediction != null)
            {
                TransitionPrediction prediction = TransitionSettings.Prediction;
                if (!defSet.CollisionEnergyList.Contains(prediction.CollisionEnergy))
                    defSet.CollisionEnergyList.Add(prediction.CollisionEnergy);
                if (prediction.DeclusteringPotential != null &&
                        !defSet.DeclusterPotentialList.Contains(prediction.DeclusteringPotential))
                    defSet.DeclusterPotentialList.Add(prediction.DeclusteringPotential);
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
                // Library specs should always be null after loading
                Debug.Assert(libraries.LibrarySpecs[i] == null);

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
        double GetPrecursorMass(string seq);
        bool IsModified(string seq);
        string GetModifiedSequence(string seq, bool formatNarrow);
    }

    public interface IFragmentMassCalc
    {
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
                                  (precursorsDiff && newPep.Libraries.HasLibraries && newPep.Libraries.Pick != PeptidePick.filter);

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
            bool diffHeavyMods = !StaticMod.EquivalentImplicitMods(newMods.HeavyModifications,
                                           oldMods.HeavyModifications);
            // Set explicit differences, if no differences in the global implicit modifications,
            // but the modifications have changed.
            if (!diffStaticMods && !diffHeavyMods && !ReferenceEquals(oldPep.Modifications, newPep.Modifications))
                DiffExplicit = true;

            // Change transition groups if precursor charges or heavy group
            // existence changed
            DiffTransitionGroups = precursorsDiff || diffHeavyMods;
                

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
                                    newTran.Libraries.Pick != TransitionLibraryPick.none);

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