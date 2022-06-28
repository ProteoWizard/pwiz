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
using System.Linq;
using System.Globalization;
using System.IO;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Optimization;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.DocSettings
{
    [XmlRoot("transition_settings")]
    public class TransitionSettings : Immutable, IValidating, IXmlSerializable
    {
        public TransitionSettings(TransitionPrediction prediction,
                                  TransitionFilter filter,
                                  TransitionLibraries libraries,
                                  TransitionIntegration integration,
                                  TransitionInstrument instrument,
                                  TransitionFullScan fullScan,
                                  TransitionIonMobilityFiltering ionMobilityFiltering)
        {
            Prediction = prediction;
            Filter = filter;
            Libraries = libraries;
            Integration = integration;
            Instrument = instrument;
            FullScan = fullScan;
            IonMobilityFiltering = ionMobilityFiltering ?? TransitionIonMobilityFiltering.EMPTY;

            DoValidate();
        }

        [TrackChildren(true)]
        public TransitionPrediction Prediction { get; private set; }

        [TrackChildren(true)]
        public TransitionFilter Filter { get; private set; }

        [TrackChildren(true)]
        public TransitionLibraries Libraries { get; private set; }

        [TrackChildren(true)]
        public TransitionIntegration Integration { get; private set; }

        [TrackChildren(true)]
        public TransitionInstrument Instrument { get; private set; }

        [TrackChildren(true)]
        public TransitionFullScan FullScan { get; private set; }

        [TrackChildren(true)]
        public TransitionIonMobilityFiltering IonMobilityFiltering { get; private set; }
        
        public bool IsMeasurablePrecursor(double mz)
        {
            if (!Instrument.IsMeasurable(mz))
                return false;
            return FullScan.IsolationScheme == null || FullScan.IsolationScheme.IsInRangeMz(mz);
        }

        public bool Accept(Target sequence, double precursorMz, IonType type, int cleavageOffset, double ionMz, int start, int end, double startMz)
        {
            if (Filter.ExclusionUseDIAWindow && FullScan.IsolationScheme != null &&
                FullScan.IsolationScheme.MayFallIntoSameWindow(ionMz, precursorMz))
            {
                return false;
            }

            return Filter.Accept(sequence, precursorMz, type, cleavageOffset, ionMz, start, end, startMz);
        }

        #region Property change methods

        public TransitionSettings ChangePrediction(TransitionPrediction prop)
        {
            return ChangeProp(ImClone(this), im => im.Prediction = prop);
        }

        public TransitionSettings ChangeFilter(TransitionFilter prop)
        {
            return ChangeProp(ImClone(this), im => im.Filter = prop);
        }

        public TransitionSettings ChangeLibraries(TransitionLibraries prop)
        {
            return ChangeProp(ImClone(this), im => im.Libraries = prop);
        }

        public TransitionSettings ChangeIntegration(TransitionIntegration prop)
        {
            return ChangeProp(ImClone(this), im => im.Integration = prop);
        }

        public TransitionSettings ChangeIonMobilityFiltering(TransitionIonMobilityFiltering prop)
        {
            return ChangeProp(ImClone(this), im => im.IonMobilityFiltering = prop ?? TransitionIonMobilityFiltering.EMPTY);
        }

        public TransitionSettings ChangeInstrument(TransitionInstrument prop)
        {
            return ChangeProp(ImClone(this), im => im.Instrument = prop);
        }

        public TransitionSettings ChangeFullScan(TransitionFullScan prop)
        {
            return ChangeProp(ImClone(this), im => im.FullScan = prop);
        }

        


        #endregion

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private TransitionSettings()
        {
        }

        void IValidating.Validate()
        {
            DoValidate();
        }

        private void DoValidate()
        {
            // Be careful in this validate function, since it occurs before SrmSettings.ValidateLoad()
            // This means any of the sub-settings objects may be null, and they will get the defaults
            if (FullScan != null && FullScan.IsHighResPrecursor &&
                    Prediction != null && Prediction.PrecursorMassType.IsAverage())
            {
                throw new InvalidDataException(
                    Resources.TransitionSettings_DoValidate_High_resolution_MS1_filtering_requires_use_of_monoisotopic_precursor_masses);
            }

            if (FullScan != null && FullScan.IsolationScheme != null && FullScan.IsolationScheme.WindowsPerScan.HasValue &&
                (Instrument == null || !Instrument.MaxInclusions.HasValue))
            {
                throw new InvalidDataException(
                    Resources.TransitionSettings_DoValidate_The_instrument_s_firmware_inclusion_limit_must_be_specified_before_doing_a_multiplexed_DIA_scan);
            }
        }

        public static TransitionSettings Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new TransitionSettings());
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            // Consume tag
            if (reader.IsEmptyElement)
                reader.Read();
            else
            {
                reader.ReadStartElement();

                // Read child elements.
                Prediction = reader.DeserializeElement<TransitionPrediction>();
                Filter = reader.DeserializeElement<TransitionFilter>();
                IonMobilityFiltering = reader.IsStartElement(TransitionIonMobilityFiltering.EL.ion_mobility_filtering) ?
                    reader.DeserializeElement<TransitionIonMobilityFiltering>() :
                    TransitionIonMobilityFiltering.EMPTY; // Looks like a pre-20.2 format
                Libraries = reader.DeserializeElement<TransitionLibraries>();
                Integration = reader.DeserializeElement<TransitionIntegration>();
                Instrument = reader.DeserializeElement<TransitionInstrument>();
                FullScan = reader.DeserializeElement<TransitionFullScan>();
                // Backward compatibility with v0.7.1
                if (FullScan == null && Instrument != null && Instrument.PrecursorAcquisitionMethod != FullScanAcquisitionMethod.None)
                {
                    FullScan = new TransitionFullScan(Instrument.PrecursorAcquisitionMethod,
                                                      TransitionFullScan.CreateIsolationSchemeForFilter(
                                                          Instrument.PrecursorAcquisitionMethod,
                                                          Instrument.PrecursorFilter, null),
                                                      FullScanMassAnalyzerType.qit,
                                                      Instrument.ProductFilter/TransitionFullScan.RES_PER_FILTER, null,
                                                      FullScanPrecursorIsotopes.None, null,
                                                      FullScanMassAnalyzerType.none, null, null, false, false,
                                                      null, RetentionTimeFilterType.none, 0);
                    Instrument = Instrument.ClearFullScanSettings();
                }

                reader.ReadEndElement();                
            }

            DoValidate();
        }

        public void WriteXml(XmlWriter writer)
        {
            // Write child elements
            writer.WriteElement(Prediction);
            writer.WriteElement(Filter);
            if (!IonMobilityFiltering.IsEmpty)
                writer.WriteElement(IonMobilityFiltering);
            writer.WriteElement(Libraries);
            writer.WriteElement(Integration);
            writer.WriteElement(Instrument);
            // Avoid breaking documents for older versions, if no full-scan
            // filtering is in use.
            if (FullScan.IsEnabled)
                writer.WriteElement(FullScan);
        }

        #endregion

        #region object overrides

        public bool Equals(TransitionSettings obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            // N.B. This multi-statement implementation makes debugging easier
            if (!Equals(obj.Prediction, Prediction))
                return false;
            if (!Equals(obj.Filter, Filter))
                return false;
            if (!Equals(obj.IonMobilityFiltering, IonMobilityFiltering))
                return false;
            if (!Equals(obj.Libraries, Libraries))
                return false;
            if (!Equals(obj.Integration, Integration))
                return false;
            if (!Equals(obj.Instrument, Instrument))
                return false;
            if (!Equals(obj.FullScan, FullScan))
                return false;
            return true;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (TransitionSettings)) return false;
            return Equals((TransitionSettings) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = Prediction.GetHashCode();
                result = (result * 397) ^ Filter.GetHashCode();
                result = (result * 397) ^ IonMobilityFiltering.GetHashCode();
                result = (result * 397) ^ Libraries.GetHashCode();
                result = (result * 397) ^ Integration.GetHashCode();
                result = (result * 397) ^ Instrument.GetHashCode();
                result = (result * 397) ^ FullScan.GetHashCode();
                return result;
            }
        }

        #endregion
    }

// ReSharper disable InconsistentNaming
    public enum OptimizedMethodType { None, Precursor, Transition }
// ReSharper restore InconsistentNaming
    public static class OptimizedMethodTypeExtension
    {
        private static string[] LOCALIZED_VALUES
        {
            get
            {
                return new[]
                {
                    Resources.OptimizedMethodTypeExtension_LOCALIZED_VALUES_None,
                    Resources.OptimizedMethodTypeExtension_LOCALIZED_VALUES_Precursor,
                    Resources.OptimizedMethodTypeExtension_LOCALIZED_VALUES_Transition
                };
            }
        }
        public static string GetLocalizedString(this OptimizedMethodType val)
        {
            return LOCALIZED_VALUES[(int)val];
        }

        public static OptimizedMethodType GetEnum(string enumValue)
        {
            return Helpers.EnumFromLocalizedString<OptimizedMethodType>(enumValue, LOCALIZED_VALUES);
        }

        public static OptimizedMethodType GetEnum(string enumValue, OptimizedMethodType defaultValue)
        {
            return Helpers.EnumFromLocalizedString(enumValue, LOCALIZED_VALUES, defaultValue);
        }  
    }

    [XmlRoot("transition_prediction")]
    public class TransitionPrediction : Immutable, IValidating, IXmlSerializable
    {
        public TransitionPrediction(MassType precursorMassType, MassType fragmentMassType,
                                    CollisionEnergyRegression collisionEnergy,
                                    DeclusteringPotentialRegression declusteringPotential,
                                    CompensationVoltageParameters compensationVoltage,
                                    OptimizationLibrary optimizationLibrary,
                                    OptimizedMethodType optimizedMethodType)
        {
            PrecursorMassType = precursorMassType;
            FragmentMassType = fragmentMassType;
            CollisionEnergy = collisionEnergy;
            DeclusteringPotential = declusteringPotential;
            CompensationVoltage = compensationVoltage;
            OptimizedLibrary = optimizationLibrary;
            OptimizedMethodType = optimizedMethodType;

            DoValidate();
        }

        public TransitionPrediction(TransitionPrediction copy)
            : this(copy.PrecursorMassType,
                   copy.FragmentMassType,
                   copy.CollisionEnergy,
                   copy.DeclusteringPotential,
                   copy.CompensationVoltage,
                   copy.OptimizedLibrary,
                   copy.OptimizedMethodType)
        {
        }

        [Track]
        public MassType PrecursorMassType { get; private set; }

        [Track]
        public MassType FragmentMassType { get; private set; }

        
        public CollisionEnergyRegression CollisionEnergy { get; private set; }

        [TrackChildren]
        public CollisionEnergyRegression NonNullCollisionEnergy
        {
            get { return CollisionEnergy ?? CollisionEnergyList.GetDefault(); }
        }

        public DeclusteringPotentialRegression DeclusteringPotential { get; private set; }

        [TrackChildren]
        public DeclusteringPotentialRegression NonNullDeclusteringPotential
        {
            get { return DeclusteringPotential ?? DeclusterPotentialList.GetDefault(); }
        }

        public CompensationVoltageParameters CompensationVoltage { get; private set; }

        [TrackChildren]
        public CompensationVoltageParameters NonNullCompensationVoltage
        {
            get { return CompensationVoltage ?? CompensationVoltageList.GetDefault(); }
        }

        public OptimizationLibrary OptimizedLibrary { get; set; }

        [TrackChildren]
        public OptimizationLibrary NonNullOptimizedLibrary
        {
            get { return OptimizedLibrary ?? OptimizationLibraryList.GetDefault(); }
        }

        [Track]
        public OptimizedMethodType OptimizedMethodType { get; private set; }

        [Track]
        public bool UseOptimizationValues { get { return OptimizedMethodType != OptimizedMethodType.None; } }

        /// <summary>
        /// This element is here for backward compatibility with the
        /// 0.1.0.0 document format.  It is not cloned, or checked for
        /// equality.  Its value, if not null, must be moved to
        /// <see cref="PeptidePrediction"/>.
        /// </summary>
        public RetentionTimeRegression RetentionTime { get; set; }

        public OptimizableRegression GetOptimizeFunction(string optimize)
        {
            if (Equals(optimize, ExportOptimize.CE))
                return CollisionEnergy;
            else if (Equals(optimize, ExportOptimize.DP))
            {
                if (DeclusteringPotential == null)
                    throw new InvalidDataException(
                        Resources.
                            SkylineWindow_ImportResults_A_regression_for_declustering_potention_must_be_selected_in_the_Prediction_tab_of_the_Transition_Settings_in_order_to_import_optimization_data_for_decluserting_potential);

                return DeclusteringPotential;
            }
            else if (Equals(optimize, ExportOptimize.COV_ROUGH) || Equals(optimize, ExportOptimize.COV_MEDIUM) || Equals(optimize, ExportOptimize.COV_FINE))
            {
                if (CompensationVoltage == null)
                    throw new InvalidDataException(
                        Resources
                            .TransitionPrediction_GetOptimizeFunction_Compensation_voltage_parameters_must_be_selected_in_the_Prediction_tab_of_the_Transition_Settings_in_order_to_import_optimization_data_for_compensation_voltage);

                switch (CompensationVoltageParameters.GetTuneLevel(optimize))
                {
                    case CompensationVoltageParameters.Tuning.fine:
                        return CompensationVoltage.RegressionFine;
                    case CompensationVoltageParameters.Tuning.medium:
                        return CompensationVoltage.RegressionMedium;
                    default:
                        return CompensationVoltage.RegressionRough;
                }
            }
            return null;
        }

        #region Property change methods

        public TransitionPrediction ChangePrecursorMassType(MassType prop)
        {
            return ChangeProp(ImClone(this), im => im.PrecursorMassType = prop);
        }

        public TransitionPrediction ChangeFragmentMassType(MassType prop)
        {
            return ChangeProp(ImClone(this), im => im.FragmentMassType = prop);
        }

        public TransitionPrediction ChangeCollisionEnergy(CollisionEnergyRegression prop)
        {
            return ChangeProp(ImClone(this), im => im.CollisionEnergy = prop);
        }

        public TransitionPrediction ChangeDeclusteringPotential(DeclusteringPotentialRegression prop)
        {
            return ChangeProp(ImClone(this), im => im.DeclusteringPotential = prop);
        }

        public TransitionPrediction ChangeCompensationVoltage(CompensationVoltageParameters prop)
        {
            return ChangeProp(ImClone(this), im => im.CompensationVoltage = prop);
        }

        public TransitionPrediction ChangeOptimizationLibrary(OptimizationLibrary prop)
        {
            return ChangeProp(ImClone(this), im => im.OptimizedLibrary = prop);
        }

        public TransitionPrediction ChangeOptimizedMethodType(OptimizedMethodType prop)
        {
            return ChangeProp(ImClone(this), im => im.OptimizedMethodType = prop);
        }

        #endregion

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private TransitionPrediction()
        {
        }

        private enum ATTR
        {
            precursor_mass_type,
            fragment_mass_type,
            optimize_by
        }

        void IValidating.Validate()
        {
            DoValidate();
        }

        private void DoValidate()
        {
            if (CollisionEnergy == null)
                throw new InvalidDataException(Resources.TransitionPrediction_DoValidate_Transition_prediction_requires_a_collision_energy_regression_function);
        }

        public static TransitionPrediction Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new TransitionPrediction());
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            // Read start tag attributes
            PrecursorMassType = reader.GetEnumAttribute(ATTR.precursor_mass_type, MassType.Monoisotopic);
            FragmentMassType = reader.GetEnumAttribute(ATTR.fragment_mass_type, MassType.Monoisotopic);
            OptimizedMethodType = reader.GetEnumAttribute(ATTR.optimize_by, OptimizedMethodType.None);

            // Consume tag
            if (reader.IsEmptyElement)
            {
                reader.Read();

                CollisionEnergy = CollisionEnergyList.NONE;
                OptimizedLibrary = OptimizationLibrary.NONE;
            }
            else
            {
                reader.ReadStartElement();

                // Read child elements.
                CollisionEnergy = reader.DeserializeElement<CollisionEnergyRegression>() ?? CollisionEnergyList.NONE;
                RetentionTime = reader.DeserializeElement<RetentionTimeRegression>();   // v0.1.0 support
                DeclusteringPotential = reader.DeserializeElement<DeclusteringPotentialRegression>();
                OptimizedLibrary = reader.DeserializeElement<OptimizationLibrary>() ?? OptimizationLibrary.NONE;
                CompensationVoltage = reader.DeserializeElement<CompensationVoltageParameters>();

                reader.ReadEndElement();
            }

            DoValidate();
        }

        public void WriteXml(XmlWriter writer)
        {
            // Write attributes
            writer.WriteAttribute(ATTR.precursor_mass_type, PrecursorMassType);
            writer.WriteAttribute(ATTR.fragment_mass_type, FragmentMassType);
            writer.WriteAttribute(ATTR.optimize_by, OptimizedMethodType);
            // Write child elements
            if (!CollisionEnergy.Equals(CollisionEnergyList.NONE))
                writer.WriteElement(CollisionEnergy);
            if (DeclusteringPotential != null)
                writer.WriteElement(DeclusteringPotential);
            if (OptimizedLibrary != null && !OptimizedLibrary.IsNone)
                writer.WriteElement(OptimizedLibrary);
            if (CompensationVoltage != null)
                writer.WriteElement(CompensationVoltage);
        }

        #endregion

        #region object overrides

        public bool Equals(TransitionPrediction obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj.PrecursorMassType, PrecursorMassType) &&
                   Equals(obj.FragmentMassType, FragmentMassType) &&
                   Equals(obj.CollisionEnergy, CollisionEnergy) &&
                   Equals(obj.DeclusteringPotential, DeclusteringPotential) &&
                   Equals(obj.CompensationVoltage, CompensationVoltage) &&
                   Equals(obj.OptimizedLibrary, OptimizedLibrary) &&
                   Equals(obj.OptimizedMethodType, OptimizedMethodType);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (TransitionPrediction)) return false;
            return Equals((TransitionPrediction) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = PrecursorMassType.GetHashCode();
                result = (result*397) ^ FragmentMassType.GetHashCode();
                result = (result*397) ^ (CollisionEnergy != null ? CollisionEnergy.GetHashCode() : 0);
                result = (result*397) ^ (DeclusteringPotential != null ? DeclusteringPotential.GetHashCode() : 0);
                result = (result*397) ^ (CompensationVoltage != null ? CompensationVoltage.GetHashCode() : 0);
                result = (result*397) ^ OptimizedLibrary.GetHashCode();
                result = (result*397) ^ OptimizedMethodType.GetHashCode();
                return result;
            }
        }

        #endregion
    }

    [XmlRoot("transition_filter")]
    public class TransitionFilter : Immutable, IXmlSerializable
    {
        public const double MIN_EXCLUSION_WINDOW = 0.01;
        public const double MAX_EXCLUSION_WINDOW = 50.0;

        public const string DEFAULT_START_FINDER = "m/z > precursor";
        public const string DEFAULT_END_FINDER = "3 ions";


        private ImmutableList<Adduct> _peptidePrecursorCharges;
        private ImmutableList<Adduct> _peptideProductCharges;
        private ImmutableList<IonType> _peptideIonTypes;
        private ImmutableList<Adduct> _smallMoleculePrecursorAdducts;
        private ImmutableList<Adduct> _smallMoleculeFragmentAdducts;
        private ImmutableList<IonType> _smallMoleculeIonTypes;
        private ImmutableList<MeasuredIon> _measuredIons;
        private StartFragmentFinder _fragmentRangeFirst;
        private EndFragmentFinder _fragmentRangeLast;

        public TransitionFilter(IList<Adduct> peptidePrecursorCharges,
                                IList<Adduct> peptideProductCharges,
                                IList<IonType> peptideIonTypes,
                                IList<Adduct> smallMoleculePrecursorAdducts,
                                IList<Adduct> smallMoleculeFragmentAdducts,
                                IList<IonType> smallMoleculeIonTypes,
                                string fragmentRangeFirstName,
                                string fragmentRangeLastName,
                                IList<MeasuredIon> measuredIons,
                                double precursorMzWindow,
                                bool exclusionUseDIAWindow,
                                bool autoSelect)
        {
            PeptidePrecursorCharges = peptidePrecursorCharges;
            PeptideProductCharges = peptideProductCharges;
            SmallMoleculePrecursorAdducts = smallMoleculePrecursorAdducts;
            SmallMoleculeFragmentAdducts = smallMoleculeFragmentAdducts;
            PeptideIonTypes = peptideIonTypes;
            SmallMoleculeIonTypes = smallMoleculeIonTypes;
            FragmentRangeFirstName = fragmentRangeFirstName;
            FragmentRangeLastName = fragmentRangeLastName;
            MeasuredIons = measuredIons;
            PrecursorMzWindow = precursorMzWindow;
            ExclusionUseDIAWindow = exclusionUseDIAWindow;
            AutoSelect = autoSelect;

            Validate();
        }

        public static string AdductListToString(IList<Adduct> list)
        {
            return list.ToArray().ToString(@", "); // CONSIDER: localize? Internationalization of comma?
        }

        [Track]
        public string PeptidePrecursorChargesString
        {
            get { return AdductListToString(PeptidePrecursorCharges); }
        }

        [Track]
        public string PeptideProductChargesString
        {
            get { return AdductListToString(PeptideProductCharges); }
        }

        [Track]
        public string SmallMoleculePrecursorAdductsString
        {
            get { return AdductListToString(SmallMoleculePrecursorAdducts); }
        }

        [Track]
        public string SmallMoleculeFragmentAdductsString
        {
            get { return AdductListToString(SmallMoleculeFragmentAdducts); }
        }

        [Track]
        public string PeptideIonTypesString
        {
            get { return ToStringIonTypes(PeptideIonTypes, true); }
        }

        [Track]
        public string SmallMoleculeIonTypesString
        {
            get { return ToStringSmallMoleculeIonTypes(SmallMoleculeIonTypes, true); }
        }

        public IList<Adduct> PeptidePrecursorCharges
        {
            get { return _peptidePrecursorCharges; }
            private set
            {
                ValidateCharges(Resources.TransitionFilter_PrecursorCharges_Precursor_charges, value,
                    TransitionGroup.MIN_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE, true);
                _peptidePrecursorCharges = MakeChargeCollection(value);
            }
        }

        public IList<Adduct> PeptideProductCharges
        {
            get { return _peptideProductCharges; }
            private set
            {
                ValidateCharges(Resources.TransitionFilter_ProductCharges_Product_ion_charges, value,
                    Transition.MIN_PRODUCT_CHARGE, Transition.MAX_PRODUCT_CHARGE, true);
                _peptideProductCharges = MakeChargeCollection(value);
            }
        }

        public IList<Adduct> SmallMoleculePrecursorAdducts
        {
            get { return _smallMoleculePrecursorAdducts; }
            private set
            {
                ValidateCharges(Resources.TransitionFilter_SmallMoleculePrecursorAdducts_Small_molecule_precursor_adducts, value,
                    TransitionGroup.MIN_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE, false);
                _smallMoleculePrecursorAdducts = MakeChargeCollection(value);
            }
        }

        public IList<Adduct> SmallMoleculeFragmentAdducts
        {
            get { return _smallMoleculeFragmentAdducts; }
            set { _smallMoleculeFragmentAdducts = MakeChargeCollection(value); }
        }

        public static ImmutableList<Adduct> MakeChargeCollection(IList<Adduct> charges)
        {
            var arrayCharges = charges.ToArrayStd();
            Array.Sort(arrayCharges);
            return MakeReadOnly(arrayCharges);
        }

        public IList<IonType> PeptideIonTypes
        {
            get { return _peptideIonTypes; }
            private set
            {
                if (value.Count == 0)
                    throw new InvalidDataException(Resources.TransitionFilter_IonTypes_At_least_one_ion_type_is_required);
                TypeSafeEnum.ValidateList(value);
                _peptideIonTypes = MakeReadOnly(value);
            }
        }

        public IList<IonType> SmallMoleculeIonTypes
        {
            get { return _smallMoleculeIonTypes; }
            private set
            {
                if (value.Count == 0)
                    throw new InvalidDataException(Resources.TransitionFilter_IonTypes_At_least_one_ion_type_is_required);
                TypeSafeEnum.ValidateList(value);
                _smallMoleculeIonTypes = MakeReadOnly(value);
            }
        }

        public IStartFragmentFinder FragmentRangeFirst { get { return _fragmentRangeFirst; } }

        public string FragmentRangeFirstName
        {
            get { return _fragmentRangeFirst.Name; }
            private set
            {
                _fragmentRangeFirst = (StartFragmentFinder)GetStartFragmentFinder(value);
                if (_fragmentRangeFirst == null)
                {
                    throw new InvalidDataException(
                        string.Format(Resources.TransitionFilter_FragmentRangeFirstName_Unsupported_first_fragment_name__0__,
                                      FragmentRangeFirst));
                }
            }
        }

        [Track]
        public LabeledValues<string> StartFragmentFinderLabel
        {
            get { return _fragmentRangeFirst; }
        }

        [Track]
        public LabeledValues<string> EndFragmentFinderLabel
        {
            get { return _fragmentRangeLast; }
        }

        public IEndFragmentFinder FragmentRangeLast { get { return _fragmentRangeLast; } }

        public string FragmentRangeLastName
        {
            get { return _fragmentRangeLast.Name; }
            private set
            {
                _fragmentRangeLast = (EndFragmentFinder)GetEndFragmentFinder(value);
                if (_fragmentRangeLast == null)
                {
                    throw new InvalidDataException(
                        string.Format(Resources.TransitionFilter_FragmentRangeLastName_Unsupported_last_fragment_name__0__,
                                      FragmentRangeLast));
                }
            }
        }

        [TrackChildren]
        public IList<MeasuredIon> MeasuredIons
        {
            get { return _measuredIons; }
            private set { _measuredIons = MakeReadOnly(value); }
        }

        public bool IsSpecialFragment(Target sequence, IonType ionType, int cleavageOffset)
        {
            return MeasuredIons.Contains(m => m.IsMatch(sequence, ionType, cleavageOffset));
        }

        /// <summary>
        /// Returns false if the transition is a reporter type custom ion that is no longer
        /// available in the settings.
        /// </summary>
        public bool IsAvailableReporterIon(TransitionDocNode nodeTransition)
        {
            if (!nodeTransition.Transition.IsCustom() || nodeTransition.Transition.IsNonReporterCustomIon())
                return true;
            return MeasuredIons.Contains(m => Equals(m.SettingsCustomIon, nodeTransition.Transition.CustomIon));
        }

        /// <summary>
        /// A m/z window width around the precursor m/z where transitions are not allowed.
        /// </summary>
        [Track]
        public double PrecursorMzWindow { get; private set; }

        /// <summary>
        /// Returns true if the ion m/z value is within the precursor m/z exclusion window.
        /// i.e. within 1/2 of the window width of the precursor m/z.
        /// </summary>
        public bool IsExcluded(double ionMz, double precursorMz)
        {
            return PrecursorMzWindow != 0 && Math.Abs(ionMz - precursorMz)*2 < PrecursorMzWindow;
        }

        public bool Accept(Target sequence, double precursorMz, IonType type, int cleavageOffset, double ionMz, int start, int end, double startMz)
        {
            if (IsExcluded(ionMz, precursorMz))
                return false;
            if (start <= cleavageOffset && cleavageOffset <= end && startMz <= ionMz)
                return true;            
            return IsSpecialFragment(sequence, type, cleavageOffset);
        }

        [Track]
        public bool AutoSelect { get; private set; }

        /// <summary>
        /// Whether to use the DIA window for exclusion of fragments (exclude fragments falling 
        /// into the same DIA window as the precursor)
        /// </summary>
        [Track]
        public bool ExclusionUseDIAWindow { get; private set; }

        #region Property change methods

        public TransitionFilter ChangePeptidePrecursorCharges(IList<Adduct> prop)
        {
            return ChangeProp(ImClone(this), im => im.PeptidePrecursorCharges = prop);
        }

        public TransitionFilter ChangePeptideProductCharges(IList<Adduct> prop)
        {
            return ChangeProp(ImClone(this), im => im.PeptideProductCharges = prop);
        }

        public TransitionFilter ChangePeptideIonTypes(IList<IonType> prop)
        {
            return ChangeProp(ImClone(this), im => im.PeptideIonTypes = prop);
        }

        public TransitionFilter ChangeSmallMoleculePrecursorAdducts(IList<Adduct> prop)
        {
            return ChangeProp(ImClone(this), im => im.SmallMoleculePrecursorAdducts = prop);
        }
        public TransitionFilter ChangeSmallMoleculeFragmentAdducts(IList<Adduct> prop)
        {
            return ChangeProp(ImClone(this), im => im.SmallMoleculeFragmentAdducts = prop);
        }
        public TransitionFilter ChangeSmallMoleculeIonTypes(IList<IonType> prop)
        {
            return ChangeProp(ImClone(this), im => im.SmallMoleculeIonTypes = prop);
        }

        public TransitionFilter ChangeFragmentRangeFirstName(string prop)
        {
            return ChangeProp(ImClone(this), im => im.FragmentRangeFirstName = prop);
        }

        public TransitionFilter ChangeFragmentRangeLastName(string prop)
        {
            return ChangeProp(ImClone(this), im => im.FragmentRangeLastName = prop);
        }

        public TransitionFilter ChangeMeasuredIons(IList<MeasuredIon> prop)
        {
            return ChangeProp(ImClone(this), im => im.MeasuredIons = prop);
        }

        public TransitionFilter ChangePrecursorMzWindow(double prop)
        {
            return ChangeProp(ImClone(this), im => im.PrecursorMzWindow = prop);
        }

        public TransitionFilter ChangeAutoSelect(bool prop)
        {
            return ChangeProp(ImClone(this), im => im.AutoSelect = prop);
        }

        public TransitionFilter ChangeExclusionUseDIAWindow(bool prop)
        {
            return ChangeProp(ImClone(this), im => im.ExclusionUseDIAWindow = prop);
        }

        #endregion

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private TransitionFilter()
        {
        }

        private enum ATTR
        {
            precursor_charges,
            product_charges,
            fragment_types,
            fragment_range_first,
            fragment_range_last,
            include_n_proline,
            // Old misspelling v0.1
            include_n_prolene,
            include_c_glu_asp,
            precursor_mz_window,
            exclusion_use_dia_window,
            auto_select,
            precursor_adducts,  // For small molecule use
            product_adducts,  // For small molecule use
            small_molecule_fragment_types,
        }

        public static void ValidateCharges(string label, ICollection<Adduct> adducts, int min, int max, bool proteomic)
        {
            if (adducts == null || adducts.Count == 0)
                throw new InvalidDataException(string.Format(Resources.TransitionFilter_ValidateCharges__0__cannot_be_empty, label));
            var seen = new HashSet<Adduct>();
            foreach (var adduct in adducts)
            {
                if (seen.Contains(adduct))
                    throw new InvalidDataException(string.Format(Resources.TransitionFilter_ValidateCharges_Precursor_charges_specified_charge__0__more_than_once, adduct));
                if (!IsChargeInRange(adduct, min, max, proteomic))
                    throw new InvalidDataException(string.Format(Resources.TransitionFilter_ValidateCharges_Invalid_charge__1__found__0__must_be_between__2__and__3__, label, adduct, min, max));
                seen.Add(adduct);
            }            
        }

        public static bool IsChargeInRange(Adduct adduct, int minCharge, int maxCharge, bool proteomic)
        {
            int chargeCompare = proteomic ? adduct.AdductCharge : Math.Abs(adduct.AdductCharge);
            return minCharge <= chargeCompare && chargeCompare <= maxCharge;
        }

        public void Validate()
        {
            if (PrecursorMzWindow != 0)
            {
                if (MIN_EXCLUSION_WINDOW > PrecursorMzWindow || PrecursorMzWindow > MAX_EXCLUSION_WINDOW)
                {
                    throw new InvalidDataException(
                        string.Format(Resources.TransitionFilter_Validate_A_precursor_exclusion_window_must_be_between__0__and__1__,
                                      MIN_EXCLUSION_WINDOW, MAX_EXCLUSION_WINDOW));
                }
            }
            // It should not be possible to reach this error in the UI -- for development only
            if (ExclusionUseDIAWindow)
            {
                if (PrecursorMzWindow != 0)
                {
                    throw new InvalidDataException(@"Cannot set the precursor exclusion window and also request to use DIA window.");
                }              
            }
        }

        public static TransitionFilter Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new TransitionFilter());
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            // Read start tag attributes
            PeptidePrecursorCharges = RemoveNegativeAdducts(ArrayUtil.Parse(reader.GetAttribute(ATTR.precursor_charges), Adduct.FromStringAssumeProtonated, TextUtil.SEPARATOR_CSV, new Adduct[0])).ToList();
            PeptideProductCharges = RemoveNegativeAdducts(ArrayUtil.Parse(reader.GetAttribute(ATTR.product_charges), Adduct.FromStringAssumeProtonated, TextUtil.SEPARATOR_CSV, new Adduct[0])).ToList();
            PeptideIonTypes = ParseTypes(reader.GetAttribute(ATTR.fragment_types), new[] { IonType.y });
            SmallMoleculePrecursorAdducts = ArrayUtil.Parse(reader.GetAttribute(ATTR.precursor_adducts), Adduct.FromStringAssumeChargeOnly, TextUtil.SEPARATOR_CSV, Transition.DEFAULT_MOLECULE_CHARGES);
            SmallMoleculeFragmentAdducts = ArrayUtil.Parse(reader.GetAttribute(ATTR.product_adducts), Adduct.FromStringAssumeChargeOnly, TextUtil.SEPARATOR_CSV, Transition.DEFAULT_MOLECULE_FRAGMENT_CHARGES);
            SmallMoleculeIonTypes = ParseSmallMoleculeTypes(reader.GetAttribute(ATTR.small_molecule_fragment_types), Transition.DEFAULT_MOLECULE_FILTER_ION_TYPES);
            FragmentRangeFirstName = reader.GetAttribute(ATTR.fragment_range_first);
            FragmentRangeLastName = reader.GetAttribute(ATTR.fragment_range_last);
            PrecursorMzWindow = reader.GetDoubleAttribute(ATTR.precursor_mz_window);
            // First, try old misspelling of proline
            bool legacyProline = reader.GetBoolAttribute(ATTR.include_n_prolene);
            // Second, try correct spelling
            legacyProline = reader.GetBoolAttribute(ATTR.include_n_proline, legacyProline);
            bool lecacyGluAsp = reader.GetBoolAttribute(ATTR.include_c_glu_asp);
            ExclusionUseDIAWindow = reader.GetBoolAttribute(ATTR.exclusion_use_dia_window);
            AutoSelect = reader.GetBoolAttribute(ATTR.auto_select);

            // Consume tag
            reader.Read();

            // Read special ions
            var measuredIons = new List<MeasuredIon>();
            reader.ReadElements(measuredIons);

            if (measuredIons.Count > 0)
                reader.ReadEndElement();
            
            if (legacyProline)
                measuredIons.Add(MeasuredIonList.NTERM_PROLINE_LEGACY);
            if (lecacyGluAsp)
                measuredIons.Add(MeasuredIonList.CTERM_GLU_ASP_LEGACY);

            MeasuredIons = measuredIons.ToArray();

            Validate();
        }

        public void WriteXml(XmlWriter writer)
        {
            // Write attributes
            writer.WriteAttributeString(ATTR.precursor_charges, PeptidePrecursorCharges.ToString(TextUtil.SEPARATOR_CSV.ToString(CultureInfo.InvariantCulture)));
            writer.WriteAttributeString(ATTR.product_charges, PeptideProductCharges.ToString(TextUtil.SEPARATOR_CSV.ToString(CultureInfo.InvariantCulture)));
            writer.WriteAttributeString(ATTR.precursor_adducts, SmallMoleculePrecursorAdducts.ToString(TextUtil.SEPARATOR_CSV.ToString(CultureInfo.InvariantCulture)));
            writer.WriteAttributeString(ATTR.product_adducts, SmallMoleculeFragmentAdducts.ToString(TextUtil.SEPARATOR_CSV.ToString(CultureInfo.InvariantCulture)));
            writer.WriteAttributeString(ATTR.fragment_types, ToStringIonTypes(PeptideIonTypes, false));
            writer.WriteAttributeString(ATTR.small_molecule_fragment_types, ToStringSmallMoleculeIonTypes(SmallMoleculeIonTypes, false));
            writer.WriteAttributeString(ATTR.fragment_range_first, FragmentRangeFirstName);
            writer.WriteAttributeString(ATTR.fragment_range_last, FragmentRangeLastName);
            writer.WriteAttribute(ATTR.precursor_mz_window, PrecursorMzWindow);
            writer.WriteAttribute(ATTR.exclusion_use_dia_window, ExclusionUseDIAWindow);
            writer.WriteAttribute(ATTR.auto_select, AutoSelect);
            writer.WriteElements(MeasuredIons);
        }

        public const string PRECURSOR_ION_CHAR = "p";
        public const string SMALL_MOLECULE_FRAGMENT_CHAR = "f";

        public static string ToStringSmallMoleculeIonTypes(IList<IonType> ionTypes, bool spaces)
        {
            string sep = TextUtil.SEPARATOR_CSV.ToString(CultureInfo.InvariantCulture);
            if (spaces)
                sep += TextUtil.SEPARATOR_SPACE;
            return ionTypes.ToString(sep).Replace(IonType.precursor.ToString(), PRECURSOR_ION_CHAR).Replace(IonType.custom.ToString(), SMALL_MOLECULE_FRAGMENT_CHAR);
        }

        public static string ToStringIonTypes(IList<IonType> ionTypes, bool spaces)
        {
            string sep = TextUtil.SEPARATOR_CSV.ToString(CultureInfo.InvariantCulture);
            if (spaces)
                sep += TextUtil.SEPARATOR_SPACE;
            var stringsList = ionTypes.Select(ion => ion.GetInputAliases().First()).ToList();
            return stringsList.ToString(sep).Replace(IonType.precursor.ToString(), PRECURSOR_ION_CHAR);
        }

        public static IonType[] ParseTypes(string s, IonType[] defaultTypes)
        {
            return ArrayUtil.Parse(s, ParseIonType, TextUtil.SEPARATOR_CSV, defaultTypes);
        }

        public static IonType[] ParseSmallMoleculeTypes(string s, IonType[] defaultTypes)
        {
            return ArrayUtil.Parse(s, ParseSmallMoleculeIonType, TextUtil.SEPARATOR_CSV, defaultTypes);
        }

        public static IonType ParseIonType(string s)
        {
            s = s.ToLowerInvariant();
            if (s == PRECURSOR_ION_CHAR)
            {
                return IonType.precursor;
            }
            IonType ionType = IonTypeExtension.GetEnum(s);
            if (ionType == IonType.custom)
            {
                throw new ArgumentException();
            }
            return ionType;
        }

        public static IonType ParseSmallMoleculeIonType(string s)
        {
            s = s.ToLowerInvariant();
            if (s == PRECURSOR_ION_CHAR)
            {
                return IonType.precursor;
            }
            if (s == SMALL_MOLECULE_FRAGMENT_CHAR)
            {
                return IonType.custom;
            }
            throw new ArgumentException();
        }

        /// <summary>
        /// Removes the adducts whose charge is less than 0.
        /// There was a time when Skyline erroneously allowed negative charges for peptides,
        /// so we remove those invalid values when deserializing the TransitionFilter
        /// </summary>
        private IEnumerable<Adduct> RemoveNegativeAdducts(IEnumerable<Adduct> adducts)
        {
            bool anyUnfiltered = false;
            bool anyReturned = false;
            foreach (var adduct in adducts)
            {
                anyUnfiltered = true;
                if (adduct.AdductCharge >= 0)
                {
                    anyReturned = true;
                    yield return adduct;
                }
            }
            // Make sure we have at least one adduct if we had one before
            if (anyUnfiltered && !anyReturned)
            {
                yield return Adduct.FromChargeProtonated(1);
            }
        }

        #endregion

        #region object overrides

        public bool Equals(TransitionFilter obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return ArrayUtil.EqualsDeep(obj._peptidePrecursorCharges, _peptidePrecursorCharges) &&
                   ArrayUtil.EqualsDeep(obj._peptideProductCharges, _peptideProductCharges) &&
                   ArrayUtil.EqualsDeep(obj._peptideIonTypes, _peptideIonTypes) &&
                   ArrayUtil.EqualsDeep(obj._smallMoleculePrecursorAdducts, _smallMoleculePrecursorAdducts) &&
                   ArrayUtil.EqualsDeep(obj._smallMoleculeIonTypes, _smallMoleculeIonTypes) &&
                   Equals(obj.FragmentRangeFirst, FragmentRangeFirst) &&
                   Equals(obj.FragmentRangeLast, FragmentRangeLast) &&
                   ArrayUtil.EqualsDeep(obj.MeasuredIons, MeasuredIons) &&
                   obj.PrecursorMzWindow.Equals(PrecursorMzWindow) &&
                   obj.ExclusionUseDIAWindow.Equals(ExclusionUseDIAWindow) &&
                   obj.AutoSelect.Equals(AutoSelect);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (TransitionFilter)) return false;
            return Equals((TransitionFilter) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = _peptidePrecursorCharges.GetHashCodeDeep();
                result = (result * 397) ^ _peptideProductCharges.GetHashCodeDeep();
                result = (result * 397) ^ _peptideIonTypes.GetHashCodeDeep();
                result = (result * 397) ^ _smallMoleculePrecursorAdducts.GetHashCodeDeep();
                result = (result * 397) ^ _smallMoleculeIonTypes.GetHashCodeDeep();
                result = (result * 397) ^ FragmentRangeFirst.GetHashCode();
                result = (result*397) ^ FragmentRangeLast.GetHashCode();
                result = (result*397) ^ MeasuredIons.GetHashCodeDeep();
                result = (result*397) ^ PrecursorMzWindow.GetHashCode();
                result = (result*397) ^ ExclusionUseDIAWindow.GetHashCode();
                result = (result*397) ^ AutoSelect.GetHashCode();
                return result;
            }
        }

        #endregion

        private static MappedList<string, StartFragmentFinder> _fragmentStartFinders;
        private static Dictionary<string, string> _mapLegacyStartNames;
        private static MappedList<string, EndFragmentFinder> _fragmentEndFinders;
        private static Dictionary<string, string> _mapLegacyEndNames;

        public static IEnumerable<string> GetStartFragmentFinderLabels()
        {
            return FragmentStartFinders.Select(f => f.Label);
        }

        public static IEnumerable<string> GetFilterStartFragmentFinderLabels()
        {
            return FragmentStartFinders
                .Where(f => f is OrdinalFragmentFinder || Equals(DEFAULT_START_FINDER, f.Name))
                .Select(f => f.Label);
        }

        public static string GetStartFragmentNameFromLabel(string label)
        {
            for (int i = 0; i < FragmentStartFinders.SafeLength(); i++)
            {
                if (string.Equals(FragmentStartFinders[i].Label, label))
                {
                    return FragmentStartFinders[i].Name;
                }
            }
            throw new ArgumentException(string.Format(Resources.TransitionFilter_GetStartFragmentNameFromLabel_The_label__0__is_not_a_valid_start_fragment_filter_, label));
        }

        public static IStartFragmentFinder GetStartFragmentFinder(string finderName)
        {
            if (!string.IsNullOrEmpty(finderName))
            {
                StartFragmentFinder result;
                string name;
                if (FragmentStartFinders.TryGetValue(finderName, out result))
                    return result;
                if (_mapLegacyStartNames.TryGetValue(finderName, out name))
                    return FragmentStartFinders[name];
                // Backward compatibility with when finder index used to get written to XML
                int index;
                if (int.TryParse(finderName, out index))
                    return FragmentStartFinders[index];
            }
            throw new InvalidDataException(string.Format(Resources.TransitionFilter_GetEndFragmentFinder_Unknown_fragment_name_in_Transition_Settings__0__, finderName));
        }

        private static MappedList<string, StartFragmentFinder> FragmentStartFinders
        {
            get
            {
                if (_fragmentStartFinders == null)
                {
                    _fragmentStartFinders = new MappedList<string, StartFragmentFinder>
                    {
                        StartFragmentFinder.ION_1,
                        StartFragmentFinder.ION_2,
                        StartFragmentFinder.ION_3,
                        StartFragmentFinder.ION_4,
                        StartFragmentFinder.MZ_PRECURSOR,
                        StartFragmentFinder.MZ_PRECURSOR_MINUS_1,
                        StartFragmentFinder.MZ_PRECURSOR_MINUS_2,
                        StartFragmentFinder.MZ_PRECURSOR_PLUS_1,
                        StartFragmentFinder.MZ_PRECURSOR_PLUS_2
                    };

                    _mapLegacyStartNames = new Dictionary<string, string>
                                               {
                                                   {@"y1", @"ion 1"},
                                                   {@"y2", @"ion 2"},
                                                   {@"y3", @"ion 3"},
                                                   {@"y4", @"ion 4"},
                                               };
                }
                return _fragmentStartFinders;
            }
        }

        public static string GetEndFragmentNameFromLabel(string label)
        {
            for (int i = 0; i < FragmentEndFinders.SafeLength(); i++)
            {
                if (string.Equals(FragmentEndFinders[i].Label, label))
                {
                    return FragmentEndFinders[i].Name;
                }
            }
            throw new ArgumentException(string.Format(Resources.TransitionFilter_GetEndFragmentNameFromLabel_The_label__0__is_not_a_valid_end_fragment_filter_, label));
        }

        public static IEnumerable<string> GetEndFragmentFinderLabels()
        {
            return FragmentEndFinders.Select(f => f.Label);
        }

        public static IEnumerable<string> GetFilterEndFragmentFinderLabels()
        {
            return FragmentEndFinders
                .Where(f => f is LastFragmentFinder)
                .Select(f => f.Label);
        }

        public static IEndFragmentFinder GetEndFragmentFinder(string finderName)
        {
            if (!string.IsNullOrEmpty(finderName))
            {
                EndFragmentFinder result;
                string name;
                if (FragmentEndFinders.TryGetValue(finderName, out result))
                    return result;
                if (_mapLegacyEndNames.TryGetValue(finderName, out name))
                    return FragmentEndFinders[name];
                // Backward compatibility with when finder index used to get written to XML
                int index;
                if (int.TryParse(finderName, out index))
                    return FragmentEndFinders[index];
            }
            throw new InvalidDataException(string.Format(Resources.TransitionFilter_GetEndFragmentFinder_Unknown_fragment_name_in_Transition_Settings__0__, finderName));
        }

        private class KeyContainerWrapper<T> : IKeyContainer<T>
        {
            private readonly LabeledValues<T>  _labeledValue;

            public KeyContainerWrapper(LabeledValues<T> labeledValue)
            {
                _labeledValue = labeledValue;
            }

            public T GetKey()
            {
                return _labeledValue.Name;
            }
        }

        private static MappedList<string, EndFragmentFinder> FragmentEndFinders
        {
            get
            {
                if (_fragmentEndFinders == null)
                {
                    _fragmentEndFinders = new MappedList<string, EndFragmentFinder>
                                              {
                                                  // Use functions for labels to make sure resources come from the right thread locale
                                                  EndFragmentFinder.LAST_ION,
                                                  EndFragmentFinder.LAST_ION_MINUS_1,
                                                  EndFragmentFinder.LAST_ION_MINUS_2,
                                                  EndFragmentFinder.LAST_ION_MINUS_3,
                                                  EndFragmentFinder.ION_1,
                                                  EndFragmentFinder.IONS_2,
                                                  EndFragmentFinder.IONS_3,
                                                  EndFragmentFinder.IONS_4,
                                                  EndFragmentFinder.IONS_5,
                                                  EndFragmentFinder.IONS_6,
                                              };

                    _mapLegacyEndNames = new Dictionary<string, string>
                                             {
                                                   {@"last y-ion", @"last ion"},
                                                   {@"last y-ion - 1", @"last ion - 1"},
                                                   {@"last y-ion - 2", @"last ion - 2"},
                                                   {@"last y-ion - 3", @"last ion - 3"},
                                                   {@"start + 3", @"3 ions"},
                                                   {@"start + 4", @"4 ions"},
                                                   {@"start + 5", @"5 ions"},
                                                   {@"start + 6", @"6 ions"},
                                             };
                }
                return _fragmentEndFinders;
            }
        }

        public abstract class StartFragmentFinder : LabeledValues<string>, IStartFragmentFinder
        {
            public static readonly StartFragmentFinder ION_1 = new OrdinalFragmentFinder(@"ion 1", () => Resources.TransitionFilter_FragmentStartFinders_ion_1, 1);
            public static readonly StartFragmentFinder ION_2 = new OrdinalFragmentFinder(@"ion 2", () => Resources.TransitionFilter_FragmentStartFinders_ion_2, 2);
            public static readonly StartFragmentFinder ION_3 = new OrdinalFragmentFinder(@"ion 3", () => Resources.TransitionFilter_FragmentStartFinders_ion_3, 3);
            public static readonly StartFragmentFinder ION_4 = new OrdinalFragmentFinder(@"ion 4", () => Resources.TransitionFilter_FragmentStartFinders_ion_4, 4);

            public static readonly StartFragmentFinder MZ_PRECURSOR = new MzFragmentFinder(DEFAULT_START_FINDER, () => Resources.TransitionFilter_FragmentStartFinders_m_z_precursor, 0);

            public static readonly StartFragmentFinder MZ_PRECURSOR_MINUS_1 = new MzFragmentFinder(@"(m/z > precursor) - 1", () => Resources.TransitionFilter_FragmentStartFinders_m_z_precursor_minus_1, -1);
            public static readonly StartFragmentFinder MZ_PRECURSOR_MINUS_2 = new MzFragmentFinder(@"(m/z > precursor) - 2", () => Resources.TransitionFilter_FragmentStartFinders_m_z_precursor_minus_2, -2);
            public static readonly StartFragmentFinder MZ_PRECURSOR_PLUS_1 = new MzFragmentFinder(@"(m/z > precursor) + 1", () => Resources.TransitionFilter_FragmentStartFinders_m_z_precursor_plus_1, 1);
            public static readonly StartFragmentFinder MZ_PRECURSOR_PLUS_2 = new MzFragmentFinder(@"(m/z > precursor) + 2", () => Resources.TransitionFilter_FragmentStartFinders_m_z_precursor_plus_2, 2);

            protected StartFragmentFinder(string name, Func<string> label)
                : base(name, label)
            {
            }

            public string GetKey()
            {
                return Name;
            }

            public abstract int FindStartFragment(IonTable<TypedMass> masses, IonType type, Adduct adduct, double precursorMz, double precursorMzWindow, out double startMz);
        }

        private class OrdinalFragmentFinder : StartFragmentFinder
        {
            private readonly int _ordinal;

            public OrdinalFragmentFinder(string name, Func<string> label, int ordinal)
                : base(name, label)
            {
                _ordinal = Math.Max(1, ordinal);
            }

            #region IStartFragmentFinder Members

            public override int FindStartFragment(IonTable<TypedMass> masses, IonType type, Adduct adduct, double precursorMz, double precursorMzWindow, out double startMz)
            {
                startMz = 0;
                int length = masses.GetLength(1);
                Debug.Assert(length > 0);

                if (type.IsNTerminal())
                    return Math.Min(_ordinal, length) - 1;
                
                return Math.Max(0, length - _ordinal);
            }

            #endregion
        }

        private class MzFragmentFinder : StartFragmentFinder
        {
            private readonly int _offset;

            public MzFragmentFinder(string name, Func<string> label, int offset)
                : base(name, label)
            {
                _offset = offset;
            }

            #region IStartFragmentFinder Members

            public override int FindStartFragment(IonTable<TypedMass> masses, IonType type, Adduct adduct,
                double precursorMz, double precursorMzWindow, out double startMz)
            {
                int start = FindStartFragment(masses, type, adduct, precursorMz, precursorMzWindow);
                // If the start is not the precursor m/z, but some offset from it, use the
                // m/z of the fragment that was chosen as the start.  Otherwise, use the precursor m/z.
                // Unfortunately, this means you really want ion m/z values >= start m/z
                // when start m/z is based on the first allowable fragment, but
                // m/z values > start m/z when start m/z is the precursor m/z. At this point,
                // using >= always is recommended for simplicity.
                startMz = (_offset != 0 ? SequenceMassCalc.GetMZ(masses[type, start], adduct) : precursorMz);
                return start;
            }

            private int FindStartFragment(IonTable<TypedMass> masses, IonType type, Adduct adduct,
                                          double precursorMz, double precursorMzWindow)
            {
                int offset = _offset;
                int length = masses.GetLength(1);
                if (length == 0)
                    throw new ArgumentException(@"Invalid attempt to find a fragment in a peptide without fragment ions.");
                if (!masses.ContainsIonType(type))
                    throw new IndexOutOfRangeException(@"Ion type {0} not found in masses array");

                // Make sure to start outside the precursor m/z window
                double thresholdMz = precursorMz + precursorMzWindow / 2;

                if (type.IsNTerminal())
                {
                    for (int i = 0; i < length; i++)
                    {
                        if (SequenceMassCalc.GetMZ(masses[type, i], adduct) > thresholdMz)
                        {
                            int indexRet;
                            do
                            {
                                indexRet = Math.Max(0, Math.Min(length - 1, i + offset));
                                offset--;
                            }
                            // Be sure not to start with a m/z value inside the exclusion window
                            while (precursorMzWindow > 0 && offset < 0 && i + offset >= 0 &&
                                Math.Abs(SequenceMassCalc.GetMZ(masses[type, indexRet], adduct) - precursorMz)*2 < precursorMzWindow);
                            return indexRet;
                        }
                    }
                    return length - 1;
                }

                for (int i = length - 1; i >= 0; i--)
                {
                    if (SequenceMassCalc.GetMZ(masses[type, i], adduct) > thresholdMz)
                    {
                        int indexRet;
                        do
                        {
                            indexRet = Math.Max(0, Math.Min(length - 1, i - offset));
                            offset--;
                        }
                            // Be sure not to start with a m/z value inside the exclusion window
                        while (precursorMzWindow > 0 && offset < 0 && i - offset < length &&
                               Math.Abs(SequenceMassCalc.GetMZ(masses[type, indexRet], adduct) - precursorMz)*2 < precursorMzWindow);
                        return indexRet;
                    }
                }
                return 0;
            }

            #endregion
        }

        public abstract class EndFragmentFinder : LabeledValues<string>, IEndFragmentFinder
        {
            public static readonly EndFragmentFinder LAST_ION = new LastFragmentFinder(@"last ion", () => Resources.TransitionFilter_FragmentEndFinders_last_ion, 0);
            public static readonly EndFragmentFinder LAST_ION_MINUS_1 = new LastFragmentFinder(@"last ion - 1", () => Resources.TransitionFilter_FragmentEndFinders_last_ion_minus_1, 1);
            public static readonly EndFragmentFinder LAST_ION_MINUS_2 = new LastFragmentFinder(@"last ion - 2", () => Resources.TransitionFilter_FragmentEndFinders_last_ion_minus_2, 2);
            public static readonly EndFragmentFinder LAST_ION_MINUS_3 = new LastFragmentFinder(@"last ion - 3", () => Resources.TransitionFilter_FragmentEndFinders_last_ion_minus_3, 3);

            public static readonly EndFragmentFinder ION_1 = new DeltaFragmentFinder(@"1 ion", () => Resources.TransitionFilter_FragmentEndFinders_1_ion, 1);
            public static readonly EndFragmentFinder IONS_2 = new DeltaFragmentFinder(@"2 ions", () => Resources.TransitionFilter_FragmentEndFinders_2_ions, 2);
            public static readonly EndFragmentFinder IONS_3 = new DeltaFragmentFinder(DEFAULT_END_FINDER, () => Resources.TransitionFilter_FragmentEndFinders_3_ions, 3);
            public static readonly EndFragmentFinder IONS_4 = new DeltaFragmentFinder(@"4 ions", () => Resources.TransitionFilter_FragmentEndFinders_4_ions, 4);
            public static readonly EndFragmentFinder IONS_5 = new DeltaFragmentFinder(@"5 ions", () => Resources.TransitionFilter_FragmentEndFinders_5_ions, 5);
            public static readonly EndFragmentFinder IONS_6 = new DeltaFragmentFinder(@"6 ions", () => Resources.TransitionFilter_FragmentEndFinders_6_ions, 6);

            protected EndFragmentFinder(string name, Func<string> label)
                : base(name, label)
            {
            }

            public abstract int FindEndFragment(IonType type, int start, int length);

            public string GetKey()
            {
                return Name;
            }
        }

        private class LastFragmentFinder : EndFragmentFinder
        {
            private readonly int _offset;

            public LastFragmentFinder(string name, Func<string> label, int offset)
                : base(name, label)
            {
                _offset = offset;
            }

            #region IEndFragmentFinder Members

            public override int FindEndFragment(IonType type, int start, int length)
            {
                Debug.Assert(length > 0);

                int end = length - 1;
                if (type.IsNTerminal())
                    return Math.Max(0, end - _offset);
                
                return Math.Min(end, _offset);
            }

            #endregion
        }

        private class DeltaFragmentFinder : EndFragmentFinder, IEndCountFragmentFinder
        {
            private readonly int _count;

            public DeltaFragmentFinder(string name, Func<string> label, int count)
                : base(name, label)
            {
                _count = Math.Max(1, count);
            }

            #region IEndCountFragmentFinder Members

            public int Count
            {
                get { return _count; }
            }

            public override int FindEndFragment(IonType type, int start, int length)
            {
                Debug.Assert(length > 0);

                if (type.IsNTerminal())
                    return Math.Min(start + _count, length) - 1;
                
                return Math.Max(0, start - _count + 1);
            }

            #endregion
        }
    }

    public interface IStartFragmentFinder : IKeyContainer<string>
    {
        string Label { get; }

        int FindStartFragment(IonTable<TypedMass> masses, IonType type, Adduct adduct, double precursorMz, double precursorMzWindow, out double startMz);
    }

    public interface IEndFragmentFinder : IKeyContainer<string>
    {
        string Label { get; }

        int FindEndFragment(IonType type, int start, int length);
    }

    public interface IEndCountFragmentFinder : IEndFragmentFinder
    {
        int Count { get; }
    }

    public enum TransitionLibraryPick { none, all, filter, all_plus }

    [XmlRoot("transition_libraries")]
    public class TransitionLibraries : Immutable, IValidating, IXmlSerializable
    {
        public const int MIN_ION_COUNT = 1;
        public const int MAX_ION_COUNT = 50;
        public const double MIN_MATCH_TOLERANCE = 0.001;    // Reduced from 0.1 to 0.001 (1 ppm at 1000 m/z) for high accuracy MS/MS
        public const double MAX_MATCH_TOLERANCE = 1.0;

        public TransitionLibraries(double ionMatchTolerance, int minIonCount, int ionCount, TransitionLibraryPick pick)
        {
            IonMatchTolerance = ionMatchTolerance;
            MinIonCount = minIonCount;
            IonCount = ionCount;
            Pick = pick;

            DoValidate();
        }

        [Track]
        public double IonMatchTolerance { get; private set; }

        [Track]
        public int MinIonCount { get; private set; }

        [Track]
        public int IonCount { get; private set; }

        [Track]
        public TransitionLibraryPick Pick { get; private set; }

        [Track]
        public bool PickMostIntenseIons
        {
            get { return Pick != TransitionLibraryPick.none; }
        }

        public bool HasMinIonCount(TransitionGroupDocNode nodeGroup)
        {
            if (!nodeGroup.HasLibInfo || Pick == TransitionLibraryPick.none)
                return true;
            // Slight problem with nodeTran.IsMs1, not being entirely deterministic. So for now we check for library
            // info to differentiate in corner cases.
            // CONSIDER: Maybe we should be storing a ChromSource on the transition at the time it is created, which
            // seems to be the only time we really know that a precursor ion is being created for MS1 filtering
            // or not.
            return nodeGroup.Transitions.Count(nodeTran => !nodeTran.IsMs1 || nodeTran.HasLibInfo) >= MinIonCount;
        }

        #region Property change methods

        public TransitionLibraries ChangeIonMatchTolerance(double prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.IonMatchTolerance = v, prop);
        }

        public TransitionLibraries ChangeMinIonCount(int prop)
        {
            return ChangeProp(ImClone(this), im => im.MinIonCount = prop);
        }

        public TransitionLibraries ChangeIonCount(int prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.IonCount = v, prop);
        }

        public TransitionLibraries ChangePick(TransitionLibraryPick prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.Pick = v, prop);
        }

        #endregion

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private TransitionLibraries()
        {
        }

        private enum ATTR
        {
            ion_match_tolerance,
            min_ion_count,
            ion_count,
            pick_from,
        }

        void IValidating.Validate()
        {
            DoValidate();
        }

        private void DoValidate()
        {
            if (MIN_MATCH_TOLERANCE > IonMatchTolerance || IonMatchTolerance > MAX_MATCH_TOLERANCE)
            {
                throw new InvalidDataException(string.Format(Resources.TransitionLibraries_DoValidate_Library_ion_match_tolerance_value__0__must_be_between__1__and__2__,
                                                             IonMatchTolerance, MIN_MATCH_TOLERANCE, MAX_MATCH_TOLERANCE));                
            }
            if (0 > MinIonCount || MinIonCount > MAX_ION_COUNT)
            {
                throw new InvalidDataException(string.Format(Resources.TransitionLibraries_DoValidate_Library_min_ion_count_value__0__must_be_between__1__and__2__,
                                                             MinIonCount, 0, MAX_ION_COUNT));
            }
            if (MIN_ION_COUNT > IonCount || IonCount > MAX_ION_COUNT)
            {
                throw new InvalidDataException(string.Format(Resources.TransitionLibraries_DoValidate_Library_ion_count_value__0__must_be_between__1__and__2__,
                                                             IonCount, MIN_ION_COUNT, MAX_ION_COUNT));
            }
            if (MinIonCount > IonCount)
            {
                throw new InvalidDataException(string.Format(Resources.TransitionLibraries_DoValidate_Library_ion_count_value__0__must_not_be_less_than_min_ion_count_value__1__,
                                                             IonCount, MinIonCount));
            }
        }

        public static TransitionLibraries Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new TransitionLibraries());
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            // Read start tag attributes
            IonMatchTolerance = reader.GetDoubleAttribute(ATTR.ion_match_tolerance);
            MinIonCount = reader.GetIntAttribute(ATTR.min_ion_count);
            IonCount = reader.GetIntAttribute(ATTR.ion_count);
            Pick = reader.GetEnumAttribute(ATTR.pick_from, TransitionLibraryPick.all);

            // Consume tag
            reader.Read();

            DoValidate();
        }

        public void WriteXml(XmlWriter writer)
        {
            // Write attributes
            writer.WriteAttribute(ATTR.ion_match_tolerance, IonMatchTolerance);
            writer.WriteAttribute(ATTR.min_ion_count, MinIonCount);
            writer.WriteAttribute(ATTR.ion_count, IonCount);
            writer.WriteAttribute(ATTR.pick_from, Pick);
        }

        #endregion

        #region object overrides

        public bool Equals(TransitionLibraries obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.IonMatchTolerance == IonMatchTolerance && obj.MinIonCount == MinIonCount && obj.IonCount == IonCount && Equals(obj.Pick, Pick);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (TransitionLibraries)) return false;
            return Equals((TransitionLibraries) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = IonMatchTolerance.GetHashCode();
                result = (result*397) ^ MinIonCount;
                result = (result*397) ^ IonCount;
                result = (result*397) ^ Pick.GetHashCode();
                return result;
            }
        }

        #endregion
    }

// ReSharper disable InconsistentNaming
    public enum FullScanPrecursorIsotopes { None, Count, Percent }
    public static class FullScanPrecursorIsotopesExtension
    {
        private static string[] LOCALIZED_VALUES
        {
            get
            {
                return new[]
                {
                    Resources.FullScanPrecursorIsotopesExtension_LOCALIZED_VALUES_None,
                    Resources.FullScanPrecursorIsotopesExtension_LOCALIZED_VALUES_Count,
                    Resources.FullScanPrecursorIsotopesExtension_LOCALIZED_VALUES_Percent
                };
            }
        }
        public static string GetLocalizedString(this FullScanPrecursorIsotopes val)
        {
            return LOCALIZED_VALUES[(int)val];
        }

        public static FullScanPrecursorIsotopes GetEnum(string enumValue)
        {
            return Helpers.EnumFromLocalizedString<FullScanPrecursorIsotopes>(enumValue, LOCALIZED_VALUES);
        }

        public static FullScanPrecursorIsotopes GetEnum(string enumValue, FullScanPrecursorIsotopes defaultEnum)
        {
            return Helpers.EnumFromLocalizedString(enumValue, LOCALIZED_VALUES, defaultEnum);
        }
    }
// ReSharper restore InconsistentNaming

    [XmlRoot("transition_instrument")]
    public sealed class TransitionInstrument : Immutable, IValidating, IXmlSerializable
    {
        public const int MIN_MEASUREABLE_MZ = 10;
        public const int MIN_MZ_RANGE = 100;
        public const int MAX_MEASURABLE_MZ = 10000;
        public const double MIN_MZ_MATCH_TOLERANCE = 0.0001;
        public const double MAX_MZ_MATCH_TOLERANCE = 0.6;
        public const double DEFAULT_MZ_MATCH_TOLERANCE = 0.055;
        public const int MIN_TRANSITION_MAX_ORIGINAL = 50;
        public const int MIN_TRANSITION_MAX = 320;
        public const int MAX_TRANSITION_MAX = 10000;
        public const int MIN_INCLUSION_MAX = 100;
        public const int MAX_INCLUSION_MAX = 5000;
        public const int MIN_TIME = 0;
        public const int MAX_TIME = 500;
        public const int MIN_TIME_RANGE = 5;

        public static double GetThermoDynamicMin(double precursorMz)
        {
            const double activationQ = 0.25;
            return ((int) (precursorMz*(activationQ/0.908))/5.0)*5.0;
        }

        public TransitionInstrument(int minMz,
                                    int maxMz,
                                    bool isDynamicMin,
                                    double mzMatchTolerance,
                                    int? maxTransitions,
                                    int? maxInclusions,
                                    int? minTime,
                                    int? maxTime)
        {
            MinMz = minMz;
            MaxMz = maxMz;
            IsDynamicMin = isDynamicMin;
            MzMatchTolerance = mzMatchTolerance;
            MaxTransitions = maxTransitions;
            MaxInclusions = maxInclusions;
            MinTime = minTime;
            MaxTime = maxTime;

            DoValidate();
        }

        [Track]
        public int MinMz { get; private set; }

        [Track]
        public int MaxMz { get; private set; }

        public bool IsMeasurable(double mz)
        {
            return MinMz <= mz && mz <= MaxMz;
        }

        [Track]
        public bool IsDynamicMin { get; private set; }

        public int GetMinMz(double precursorMz)
        {
            return (IsDynamicMin ? (int)GetThermoDynamicMin(precursorMz) : MinMz);
        }

        public bool IsMeasurable(double mz, double precursorMz)
        {
            if (IsDynamicMin && mz <= GetMinMz(precursorMz))
                return false;

            return GetMinMz(precursorMz) <= mz && mz <= MaxMz;
        }

        [Track]
        public double MzMatchTolerance { get; private set; }

        public bool IsMzMatch(double mz1, double mz2)
        {
            return Math.Abs(mz1 - mz1) <= MzMatchTolerance;
        }

        [Track]
        public int? MaxTransitions { get; private set; }

        [Track]
        public int? MaxInclusions { get; private set; }

        [Track]
        public int? MinTime { get; private set; }

        [Track]
        public int? MaxTime { get; private set; }

        // Backward compatibility with 0.7.1

        public FullScanAcquisitionMethod PrecursorAcquisitionMethod { get; private set; }

        public double? PrecursorFilter { get; private set; }

        public string ProductFilterType { get; private set; }

        public double? ProductFilter { get; private set; }

        [Track(defaultValues: typeof(DefaultValuesFalse))]
        public bool TriggeredAcquisition { get; private set; }

        #region Property change methods

        public TransitionInstrument ChangeMinMz(int prop)
        {
            return ChangeProp(ImClone(this), im => im.MinMz = prop);
        }

        public TransitionInstrument ChangeMaxMz(int prop)
        {
            return ChangeProp(ImClone(this), im => im.MaxMz = prop);
        }

        public TransitionInstrument ChangeIsDynamicMin(bool prop)
        {
            return ChangeProp(ImClone(this), im => im.IsDynamicMin = prop);
        }

        public TransitionInstrument ChangeMzMatchTolerance(double prop)
        {
            return ChangeProp(ImClone(this), im => im.MzMatchTolerance = prop);
        }

        public TransitionInstrument ChangeMaxTransitions(int? prop)
        {
            return ChangeProp(ImClone(this), im => im.MaxTransitions = prop);
        }

        public TransitionInstrument ChangeMaxInclusions(int? prop)
        {
            return ChangeProp(ImClone(this), im => im.MaxInclusions = prop);
        }

        public TransitionInstrument ChangeMinTime(int? prop)
        {
            return ChangeProp(ImClone(this), im => im.MinTime = prop);
        }

        public TransitionInstrument ChangeMaxTime(int? prop)
        {
            return ChangeProp(ImClone(this), im => im.MaxTime = prop);
        }

        // Backward compatibility with 0.7.1

        public TransitionInstrument ClearFullScanSettings()
        {
            return ChangeProp(ImClone(this), im =>
                                                 {
                                                     im.PrecursorAcquisitionMethod = FullScanAcquisitionMethod.None;
                                                     im.PrecursorFilter = null;
                                                     im.ProductFilterType = null;
                                                     im.ProductFilter = null;
                                                 });
        }

        public TransitionInstrument ChangeTriggeredAcquisition( bool triggeredAcquisition)
        {
            return ChangeProp(ImClone(this), im => im.TriggeredAcquisition = triggeredAcquisition);
        }
        #endregion

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private TransitionInstrument()
        {
        }

        private enum ATTR
        {
            min_mz,
            max_mz,
            min_time,
            max_time,
            dynamic_min,
            mz_match_tolerance,
            max_transitions,
            max_inclusions,
            triggered_acquisition,

            // Backward compatibility with 0.7.1
            precursor_filter_type,
            precursor_filter,
            product_filter
        }

        void IValidating.Validate()
        {
            DoValidate();
        }

        private void DoValidate()
        {
            if (MIN_MEASUREABLE_MZ > MinMz || MinMz > MAX_MEASURABLE_MZ - MIN_MZ_RANGE)
            {
                throw new InvalidDataException(
                    string.Format(Resources.TransitionInstrument_DoValidate_Instrument_minimum_mz_value__0__must_be_between__1__and__2__,
                                  MinMz, MIN_MEASUREABLE_MZ, MAX_MEASURABLE_MZ - MIN_MZ_RANGE));
            }
            if (MinMz + MIN_MZ_RANGE > MaxMz)
            {
                throw new InvalidDataException(
                    string.Format(Resources.TransitionInstrument_DoValidate_Instrument_maximum_m_z_value__0__is_less_than__1__from_minimum__2__,
                                  MaxMz, MIN_MZ_RANGE, MinMz));
            }
            if (MaxMz > MAX_MEASURABLE_MZ)
            {
                throw new InvalidDataException(
                    string.Format(Resources.TransitionInstrument_DoValidate_Instrument_maximum_mz_exceeds_allowable_maximum__0__,
                                  MAX_MEASURABLE_MZ));
            }
            if (MIN_MZ_MATCH_TOLERANCE > MzMatchTolerance || MzMatchTolerance > MAX_MZ_MATCH_TOLERANCE)
            {
                throw new InvalidDataException(
                    string.Format(Resources.TransitionInstrument_DoValidate_The_mz_match_tolerance__0__must_be_between__1__and__2__,
                                  MzMatchTolerance, MIN_MZ_MATCH_TOLERANCE, MAX_MZ_MATCH_TOLERANCE));
            }
            if (MIN_TRANSITION_MAX_ORIGINAL > MaxTransitions || MaxTransitions > MAX_TRANSITION_MAX)
            {
                throw new InvalidDataException(
                    string.Format(Resources.TransitionInstrument_DoValidate_The_maximum_number_of_transitions__0__must_be_between__1__and__2__,
                                  MaxTransitions, MIN_TRANSITION_MAX_ORIGINAL, MAX_TRANSITION_MAX));
            }
            if (MIN_INCLUSION_MAX > MaxInclusions || MaxInclusions > MAX_INCLUSION_MAX)
            {
                throw new InvalidDataException(
                    string.Format(Resources.TransitionInstrument_DoValidate_The_maximum_number_of_inclusions__0__must_be_between__1__and__2__,
                                  MaxInclusions, MIN_INCLUSION_MAX, MAX_INCLUSION_MAX));
            }
            if (MinTime.HasValue && (MIN_TIME > MinTime || MinTime > MAX_TIME))
            {
                throw new InvalidDataException(
                    string.Format(Resources.TransitionInstrument_DoValidate_The_minimum_retention_time__0__must_be_between__1__and__2__,
                                  MinTime, MIN_TIME, MAX_TIME));
            }
            if (MaxTime.HasValue && (MIN_TIME > MaxTime || MaxTime > MAX_TIME))
            {
                throw new InvalidDataException(
                    string.Format(Resources.TransitionInstrument_DoValidate_The_maximum_retention_time__0__must_be_between__1__and__2__,
                                  MaxTime, MIN_TIME, MAX_TIME));
            }
            if (MinTime.HasValue && MaxTime.HasValue && MaxTime.Value - MinTime.Value < MIN_TIME_RANGE)
            {
                throw new InvalidDataException(
                    string.Format(Resources.TransitionInstrument_DoValidate_The_allowable_retention_time_range__0__to__1__must_be_at_least__2__minutes_apart_,
                                  MinTime, MaxTime, MIN_TIME_RANGE));
            }
            if (PrecursorAcquisitionMethod == FullScanAcquisitionMethod.None)
            {
                if (ProductFilterType != null || PrecursorFilter.HasValue || ProductFilter.HasValue)
                {
                    throw new InvalidDataException(
                        string.Format(Resources.TransitionInstrument_DoValidate_No_other_full_scan_MS_MS_filter_settings_are_allowed_when_precursor_filter_is_none));
                }
            }
            else if (PrecursorAcquisitionMethod == FullScanAcquisitionMethod.DIA)
            {
                const double minFilter = TransitionFullScan.MIN_PRECURSOR_MULTI_FILTER;
                const double maxFilter = TransitionFullScan.MAX_PRECURSOR_MULTI_FILTER;
                if (!PrecursorFilter.HasValue || minFilter > PrecursorFilter || PrecursorFilter > maxFilter)
                {
                    throw new InvalidDataException(
                        string.Format(Resources.TransitionInstrument_DoValidate_The_precursor_mz_filter_must_be_between__0__and__1__,
                                      minFilter, maxFilter));
                }
            }
        }

        public static TransitionInstrument Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new TransitionInstrument());
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            // Read start tag attributes
            IsDynamicMin = reader.GetBoolAttribute(ATTR.dynamic_min);
            MinMz = reader.GetIntAttribute(ATTR.min_mz);
            MaxMz = reader.GetIntAttribute(ATTR.max_mz);
            MzMatchTolerance = reader.GetDoubleAttribute(ATTR.mz_match_tolerance, DEFAULT_MZ_MATCH_TOLERANCE);
            MinTime = reader.GetNullableIntAttribute(ATTR.min_time);
            MaxTime = reader.GetNullableIntAttribute(ATTR.max_time);
            MaxTransitions = reader.GetNullableIntAttribute(ATTR.max_transitions);
            MaxInclusions = reader.GetNullableIntAttribute(ATTR.max_inclusions);
            TriggeredAcquisition = reader.GetBoolAttribute(ATTR.triggered_acquisition, false);

            // Full-scan filter parameters (backward compatibility w/ 0.7.1)
            PrecursorAcquisitionMethod = FullScanAcquisitionMethod.FromLegacyName(reader.GetAttribute(ATTR.precursor_filter_type))
                ?? FullScanAcquisitionMethod.None;
            if (PrecursorAcquisitionMethod != FullScanAcquisitionMethod.None)
            {
                if (PrecursorAcquisitionMethod == FullScanAcquisitionMethod.DIA)
                {
                    PrecursorFilter = reader.GetDoubleAttribute(ATTR.precursor_filter,
                                                                TransitionFullScan.DEFAULT_PRECURSOR_MULTI_FILTER);
                }

                ProductFilter = reader.GetDoubleAttribute(ATTR.product_filter,
                    TransitionFullScan.DEFAULT_RES_VALUES[(int) FullScanMassAnalyzerType.qit]);
            }

            // Consume tag
            reader.Read();

            DoValidate();
        }

        public void WriteXml(XmlWriter writer)
        {
            // Write attributes
            writer.WriteAttribute(ATTR.dynamic_min, IsDynamicMin);
            writer.WriteAttribute(ATTR.min_mz, MinMz);
            writer.WriteAttribute(ATTR.max_mz, MaxMz);
            writer.WriteAttribute(ATTR.mz_match_tolerance, MzMatchTolerance);
            writer.WriteAttributeNullable(ATTR.min_time, MinTime);
            writer.WriteAttributeNullable(ATTR.max_time, MaxTime);
            writer.WriteAttributeNullable(ATTR.max_transitions, MaxTransitions);
            writer.WriteAttributeNullable(ATTR.max_inclusions, MaxInclusions);
            writer.WriteAttribute(ATTR.triggered_acquisition, TriggeredAcquisition, false);
        }

        #endregion

        #region object overrides

        public bool Equals(TransitionInstrument other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return other.MinMz == MinMz &&
                other.MaxMz == MaxMz &&
                other.IsDynamicMin.Equals(IsDynamicMin) &&
                other.MzMatchTolerance.Equals(MzMatchTolerance) &&
                other.MinTime.Equals(MinTime) &&
                other.MaxTime.Equals(MaxTime) &&
                other.MaxTransitions.Equals(MaxTransitions) &&
                other.MaxInclusions.Equals(MaxInclusions) &&
                Equals(other.PrecursorAcquisitionMethod, PrecursorAcquisitionMethod) &&
                other.PrecursorFilter.Equals(PrecursorFilter) &&
                Equals(other.ProductFilterType, ProductFilterType) &&
                other.ProductFilter.Equals(ProductFilter) &&
                other.TriggeredAcquisition.Equals(TriggeredAcquisition);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (TransitionInstrument)) return false;
            return Equals((TransitionInstrument) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = MinMz;
                result = (result*397) ^ MaxMz;
                result = (result*397) ^ IsDynamicMin.GetHashCode();
                result = (result*397) ^ MzMatchTolerance.GetHashCode();
                result = (result*397) ^ (MinTime.HasValue ? MinTime.Value : 0);
                result = (result*397) ^ (MaxTime.HasValue ? MaxTime.Value : 0);
                result = (result*397) ^ (MaxTransitions.HasValue ? MaxTransitions.Value : 0);
                result = (result*397) ^ (MaxInclusions.HasValue ? MaxInclusions.Value : 0);
                result = (result*397) ^ PrecursorAcquisitionMethod.GetHashCode();
                result = (result*397) ^ (PrecursorFilter.HasValue ? PrecursorFilter.Value.GetHashCode() : 0);
                result = (result*397) ^ (ProductFilterType != null ? ProductFilterType.GetHashCode() : 0);
                result = (result*397) ^ (ProductFilter.HasValue ? ProductFilter.Value.GetHashCode() : 0);
                return result;
            }
        }

        #endregion
    }

    public enum FullScanMassAnalyzerType { none = -1, centroided, qit, tof, orbitrap, ft_icr }
    public enum RetentionTimeFilterType {none, scheduling_windows, ms2_ids}

    [XmlRoot("transition_full_scan")]
    public sealed class TransitionFullScan : Immutable, IValidating, IXmlSerializable
    {
        public static readonly TransitionFullScan DEFAULT = new TransitionFullScan
        {
            RetentionTimeFilterType = RetentionTimeFilterType.ms2_ids,
            RetentionTimeFilterLength = DEFAULT_TIME_AROUND_MS2_IDS,
        };
        
        // Calculate precursor single filter window values by doubling match tolerance values
        public const double MIN_PRECURSOR_MULTI_FILTER = TransitionInstrument.MIN_MZ_MATCH_TOLERANCE*2;
        public const double MAX_PRECURSOR_MULTI_FILTER = 10*1000;
        public const double MAX_PRECURSOR_MULTI_FILTER_MARGIN = 5;
        public const double DEFAULT_PRECURSOR_MULTI_FILTER = 2.0;
        public const double DEFAULT_PRECURSOR_MULTI_FILTER_MARGIN = 0.5;
        // Calculate product low accuracy filter window values by doubling ion match tolerance values
        public const double MIN_LO_RES = 0.1;
        public const double MAX_LO_RES = 2.0;
        public const double MIN_HI_RES = 1000;
        public const double MAX_HI_RES = 10*1000*1000;
        public const double MIN_CENTROID_PPM = 1;
        public const double MAX_CENTROID_PPM = 1000;
        public const double DEFAULT_RES_MZ = 400;
        public const double MIN_RES_MZ = 50;
        public const double MAX_RES_MZ = 2000;
        public const double RES_PER_FILTER = 2;
        public const double RES_PER_FILTER_SELECTIVE = 1;
        public const double MIN_ISOTOPE_PERCENT = 0;
        public const double MAX_ISOTOPE_PERCENT = 100;
        public const double DEFAULT_ISOTOPE_PERCENT = 20;
        public const int MIN_ISOTOPE_COUNT = 1;
        public const int MAX_ISOTOPE_COUNT = 10;
        public const int DEFAULT_ISOTOPE_COUNT = 3;
        public const double ISOTOPE_PEAK_CENTERING_RES = 0.1;
        public const double MIN_ISOTOPE_PEAK_ABUNDANCE = 0.01;
        public const double DEFAULT_CENTROIDED_PPM = 10;

        public const string QIT = "QIT";
        public const string ORBITRAP = "Orbitrap";
        public const string TOF = "TOF";
        public const string FT_ICR = "FT-ICR";
        public const string CENTROIDED = "Centroided";

        public static readonly string[] MASS_ANALYZERS = { CENTROIDED, QIT, TOF, ORBITRAP, FT_ICR };
        public static readonly double[] DEFAULT_RES_VALUES = { DEFAULT_CENTROIDED_PPM, 0.7, 30 * 1000, 60 * 1000, 100 * 1000 };
        public static readonly double DEFAULT_RES_QIT = DEFAULT_RES_VALUES[1];
        public const double DEFAULT_TIME_AROUND_MS2_IDS = 5.0;

        private double _cachedPrecursorRes;
        private double _cachedProductRes;

        public TransitionFullScan()
        {
            ProductMassAnalyzer = FullScanMassAnalyzerType.none;
            PrecursorMassAnalyzer = FullScanMassAnalyzerType.none;
            AcquisitionMethod = FullScanAcquisitionMethod.None;
            DoValidate();
        }
        public TransitionFullScan(FullScanAcquisitionMethod acquisitionMethod,
                                    IsolationScheme isolationScheme,
                                    FullScanMassAnalyzerType productMassAnalyzer,
                                    double? productRes,
                                    double? productResMz,
                                    FullScanPrecursorIsotopes precursorIsotopes,
                                    double? precursorIsotopeFilter,
                                    FullScanMassAnalyzerType precursorMassAnalyzer,
                                    double? precursorRes,
                                    double? precursorResMz,
                                    bool ignoreSim,
                                    bool selectiveExtraction,
                                    IsotopeEnrichments isotopeEnrichments,
                                    RetentionTimeFilterType retentionTimeFilterType,
                                    double retentionTimeFilterMinutes)
        {
            AcquisitionMethod = acquisitionMethod;
            IsolationScheme = isolationScheme;
            ProductMassAnalyzer = productMassAnalyzer;
            ProductRes = productRes;
            ProductResMz = productResMz;
            PrecursorIsotopes = precursorIsotopes;
            PrecursorIsotopeFilter = precursorIsotopeFilter;
            PrecursorMassAnalyzer = precursorMassAnalyzer;
            PrecursorRes = precursorRes;
            PrecursorResMz = precursorResMz;
            IgnoreSimScans = ignoreSim;

            UseSelectiveExtraction = selectiveExtraction;

            IsotopeEnrichments = isotopeEnrichments;

            RetentionTimeFilterType = retentionTimeFilterType;
            RetentionTimeFilterLength = retentionTimeFilterMinutes;

            DoValidate();
        }

        // Applies to both MS1 and MS/MS because it is related to sample complexity
        [Track]
        public bool UseSelectiveExtraction { get; private set; }

        [Track]
        public bool IgnoreSimScans { get; private set; }

        public double ResPerFilter { get { return UseSelectiveExtraction ? RES_PER_FILTER_SELECTIVE : RES_PER_FILTER; } }

        // MS/MS filtering

        [Track]
        public FullScanAcquisitionMethod AcquisitionMethod { get; private set; }

        [TrackChildren]
        public IsolationScheme IsolationScheme { get; private set; }

        public double? PrecursorFilter
        {
            get { return IsolationScheme == null ? null : IsolationScheme.PrecursorFilter; }
        }

        public double? PrecursorRightFilter
        {
            get { return IsolationScheme == null ? null : IsolationScheme.PrecursorRightFilter; }
        }

        /// <summary>
        /// Backward compatibility
        /// </summary>
        public static IsolationScheme CreateIsolationSchemeForFilter(FullScanAcquisitionMethod acquisitionMethod, double? precursorFilter, double? precursorRightFilter)
        {
            if (acquisitionMethod == FullScanAcquisitionMethod.None)
            {
                throw new InvalidDataException(Resources.TransitionFullScan_CreateIsolationSchemeForFilter_Tried_to_create_an_isolation_scheme_for_non_DIA_mode);
            }

            if (acquisitionMethod == FullScanAcquisitionMethod.DIA)
            {
                if (!precursorFilter.HasValue)
                {
                    throw new InvalidDataException(Resources.TransitionFullScan_CreateIsolationSchemeForFilter_Tried_to_create_an_isolation_scheme_without_precursor_filter);
                }
                else
                {
                    string name = precursorRightFilter.HasValue
                        ? string.Format(Resources.TransitionFullScanCreateIsolationSchemeForFilterResults__0__0__1_0__Th,
                            precursorFilter.Value, precursorRightFilter.Value)
                        : string.Format(Resources.TransitionFullScanCreateIsolationSchemeForFilterResults__0__0__Th,
                            precursorFilter.Value);
                    return new IsolationScheme(name, precursorFilter, precursorRightFilter);
                }
            }

            // No scheme for Targeted acquisition mode.
            return null;
        }

        private abstract class ResLocalizer : CustomPropertyLocalizer
        {
            private static readonly string MASS_ACCURACY = @"MassAccuracy";
            private static readonly string RESOLUTION = @"Resolution";
            private static readonly string RESOLVING_POWER = @"ResolvingPower";

            protected ResLocalizer(PropertyPath path) : base(path, true) { }

            private string LocalizeInternal(object obj)
            {
                if (obj == null || obj.GetType() != typeof(FullScanMassAnalyzerType))
                    return null;

                var massAnalyzer = (FullScanMassAnalyzerType)obj;

                switch (massAnalyzer)
                {
                    case FullScanMassAnalyzerType.centroided:
                        return MASS_ACCURACY;
                    case FullScanMassAnalyzerType.qit:
                        return RESOLUTION;
                    case FullScanMassAnalyzerType.tof:
                    case FullScanMassAnalyzerType.orbitrap:
                    case FullScanMassAnalyzerType.ft_icr:
                        return RESOLVING_POWER;
                    default:
                        return null;
                }
            }

            protected override string Localize(ObjectPair<object> pair)
            {
                return LocalizeInternal(pair.NewObject) ?? LocalizeInternal(pair.OldObject);
            }

            public override string[] PossibleResourceNames
            {
                get { return new[] { MASS_ACCURACY, RESOLUTION, RESOLVING_POWER }; }
            }
        }

        private class ProductResLocalizer : ResLocalizer
        {
            public ProductResLocalizer() : base(PropertyPath.Parse(@"ProductMassAnalyzer")) { }
        }

        private class PrecursorResLocalizer : ResLocalizer
        {
            public PrecursorResLocalizer() : base(PropertyPath.Parse(@"PrecursorMassAnalyzer")) { }
        }

        [Track]
        public FullScanMassAnalyzerType ProductMassAnalyzer { get; private set; }

        [Track(customLocalizer: typeof(ProductResLocalizer))]
        public double? ProductRes { get; private set; }  // Resolving Power or mass accuracy, depending on ProductMassAnalyzer value

        [Track]
        public double? ProductResMz { get; private set; }

        // MS1 filtering

        [Track]
        public FullScanPrecursorIsotopes PrecursorIsotopes { get; private set; }

        [Track]
        public double? PrecursorIsotopeFilter { get; private set; }

        [Track]
        public FullScanMassAnalyzerType PrecursorMassAnalyzer { get; private set; }

        [Track(customLocalizer: typeof(PrecursorResLocalizer))]
        public double? PrecursorRes { get; private set; }  // Resolving Power or mass accuracy, depending on PrecursorMassAnalyzer value

        [Track]
        public double? PrecursorResMz { get; private set; }

        private class DefaultEnrichments : DefaultValues
        {
            protected override IEnumerable<object> _values
            {
                get { yield return IsotopeEnrichmentsList.DEFAULT; }
            }
        }


        [TrackChildren(defaultValues: typeof(DefaultEnrichments))]
        public IsotopeEnrichments IsotopeEnrichments { get; private set; }

        public IsotopeAbundances IsotopeAbundances
        {
            get { return IsotopeEnrichments != null ? IsotopeEnrichments.IsotopeAbundances : null; }
        }

        [Track]
        public RetentionTimeFilterType RetentionTimeFilterType { get; private set; }
        [Track]
        public double RetentionTimeFilterLength { get; private set; }

        public bool IsEnabled
        {
            get { return IsEnabledMs || IsEnabledMsMs; }
        }

        public bool IsEnabledMs
        {
            get { return PrecursorIsotopes != FullScanPrecursorIsotopes.None; }
        }

        public bool IsCentroidedMs
        {
            get { return IsEnabledMs && PrecursorMassAnalyzer == FullScanMassAnalyzerType.centroided; }
        }

        public bool IsEnabledMsMs
        {
            get { return AcquisitionMethod != FullScanAcquisitionMethod.None; }
        }

        public bool IsCentroidedMsMs
        {
            get { return IsEnabledMsMs && ProductMassAnalyzer == FullScanMassAnalyzerType.centroided; }
        }

        public bool IsHighResPrecursor { get { return IsHighResAnalyzer(PrecursorMassAnalyzer); } }

        public bool IsHighResProduct { get { return IsHighResAnalyzer(ProductMassAnalyzer); } }

        public static bool IsHighResAnalyzer(FullScanMassAnalyzerType analyzerType)
        {
            return analyzerType != FullScanMassAnalyzerType.none &&
                   analyzerType != FullScanMassAnalyzerType.qit;
        }

        public double GetPrecursorFilterWindow(double mzQ1)
        {
            return GetFilterWindow(PrecursorMassAnalyzer, _cachedPrecursorRes, mzQ1);
        }

        public double GetProductFilterWindow(double mzQ3)
        {
            return GetFilterWindow(ProductMassAnalyzer, _cachedProductRes, mzQ3);
        }

        private static double GetFilterWindow(FullScanMassAnalyzerType analyzerType, double cached, double mz)
        {
            switch (analyzerType)
            {
                case FullScanMassAnalyzerType.orbitrap:
                    return mz * Math.Sqrt(mz) / cached;
                case FullScanMassAnalyzerType.tof:
                    return mz / cached;
                case FullScanMassAnalyzerType.ft_icr:
                    return mz * mz / cached;
                case FullScanMassAnalyzerType.centroided:
                    return mz * cached;
                default:
                    return cached;
            }
        }

        private double GetDenominator(FullScanMassAnalyzerType analyzerType, double res, double resMz)
        {
            switch (analyzerType)
            {
                case FullScanMassAnalyzerType.tof:
                    return res / ResPerFilter;
                case FullScanMassAnalyzerType.orbitrap:
                    return Math.Sqrt(resMz) * res / ResPerFilter;
                case FullScanMassAnalyzerType.ft_icr:
                    return resMz * res / ResPerFilter;
                case FullScanMassAnalyzerType.centroided:
                    return res*2 / 1E+6; // 1/PPM
                default:
                    return res * ResPerFilter;
            }
        }

        public static string MassAnalyzerToString(FullScanMassAnalyzerType type)
        {
            return (type != FullScanMassAnalyzerType.none ?
                MASS_ANALYZERS[(int) type] : null);
        }

        public static FullScanMassAnalyzerType ParseMassAnalyzer(string type)
        {
            return (FullScanMassAnalyzerType) MASS_ANALYZERS.IndexOf(s => Equals(s, type));
        }


        public IEnumerable<int> SelectMassIndices(IsotopeDistInfo isotopeDists, bool useFilter)
        {
            if (isotopeDists == null)
            {
                yield return 0;
            }
            else
            {
                int countPeaks = isotopeDists.CountPeaks;

                if (!useFilter)
                {
                    for (int i = 0; i < countPeaks; i++)
                        yield return isotopeDists.PeakIndexToMassIndex(i);
                }
                else if (PrecursorIsotopes == FullScanPrecursorIsotopes.Count)
                {
                    int maxMassIndex = isotopeDists.PeakIndexToMassIndex(countPeaks);
                    countPeaks = Math.Min((int)(PrecursorIsotopeFilter ?? 1), maxMassIndex);

                    for (int i = 0; i < countPeaks; i++)
                        yield return i;
                }
                else if (PrecursorIsotopes == FullScanPrecursorIsotopes.Percent)
                {
                    double minAbundancePercent = (PrecursorIsotopeFilter ?? 0)/100;
                    double baseMassPercent = isotopeDists.BaseMassPercent;
                    for (int i = 0; i < countPeaks; i++)
                    {
                        int massIndex = isotopeDists.PeakIndexToMassIndex(i);
                        if (isotopeDists.GetProportionI(massIndex) / baseMassPercent >= minAbundancePercent)
                            yield return massIndex;
                    }
                }
            }
        }

        #region Property change methods

        public TransitionFullScan ChangeAcquisitionMethod(FullScanAcquisitionMethod typeProp, IsolationScheme isolationScheme)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im.AcquisitionMethod = typeProp;
                im.IsolationScheme = isolationScheme;
                // Make sure the change results in a valid object, or an exception
                // will be thrown.
                if (im.AcquisitionMethod == FullScanAcquisitionMethod.None)
                {
                    im.ProductMassAnalyzer = FullScanMassAnalyzerType.none;
                    im.ProductRes = im.ProductResMz = null;
                }
                else if (im.ProductMassAnalyzer == FullScanMassAnalyzerType.none)
                {
                    im.ProductMassAnalyzer = FullScanMassAnalyzerType.qit;
                    im.ProductRes = DEFAULT_RES_QIT;
                }
            });
        }

        public TransitionFullScan ChangeProductResolution(FullScanMassAnalyzerType typeProp, double? prop, double? mzProp)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im.ProductMassAnalyzer = typeProp;
                im.ProductRes = prop;
                if (typeProp != FullScanMassAnalyzerType.centroided)
                    im.ProductResMz = mzProp;
                else
                    im.ProductResMz = null;
                // Make sure the change results in a valid object, or an exception
                // will be thrown.
                if (im.AcquisitionMethod == FullScanAcquisitionMethod.None)
                    im.AcquisitionMethod = FullScanAcquisitionMethod.Targeted;
            });
        }

        public TransitionFullScan ChangePrecursorIsotopes(FullScanPrecursorIsotopes typeProp, double? prop,
                                                          IsotopeEnrichments isotopeEnrichments)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im.PrecursorIsotopes = typeProp;
                im.PrecursorIsotopeFilter = prop;
                im.IsotopeEnrichments = isotopeEnrichments;
                // Make sure the change results in a valid object, or an exception
                // will be thrown.
                if (im.PrecursorIsotopes == FullScanPrecursorIsotopes.None)
                {
                    im.PrecursorMassAnalyzer = FullScanMassAnalyzerType.none;
                    im.PrecursorRes = im.PrecursorResMz = null;
                }
                else if (im.PrecursorMassAnalyzer == FullScanMassAnalyzerType.none)
                {
                    if (im.PrecursorIsotopes == FullScanPrecursorIsotopes.Count &&
                            im.PrecursorIsotopeFilter == 1 && IsotopeEnrichments == null)
                    {
                        im.PrecursorMassAnalyzer = FullScanMassAnalyzerType.qit;
                        im.PrecursorRes = DEFAULT_RES_QIT;
                    }
                    else
                    {
                        im.PrecursorMassAnalyzer = FullScanMassAnalyzerType.tof;
                        im.PrecursorRes = DEFAULT_RES_VALUES[(int) FullScanMassAnalyzerType.tof];
                    }
                }
            });
        }

        public TransitionFullScan ChangePrecursorResolution(FullScanMassAnalyzerType typeProp, double? prop, double? mzProp)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im.PrecursorMassAnalyzer = typeProp;
                im.PrecursorRes = prop;
                if (typeProp != FullScanMassAnalyzerType.centroided)
                    im.PrecursorResMz = mzProp;
                else
                    im.PrecursorResMz = null;
                // Make sure the change results in a valid object, or an exception
                // will be thrown.
                if (im.PrecursorIsotopes == FullScanPrecursorIsotopes.None)
                {
                    im.PrecursorIsotopes = FullScanPrecursorIsotopes.Count;
                    im.PrecursorIsotopeFilter = 1;
                }
                if (!im.IsHighResPrecursor)
                {
                    im.IsotopeEnrichments = null;
                }
            });
        }

        public TransitionFullScan ChangeUseSelectiveExtraction(bool prop)
        {
            return ChangeProp(ImClone(this), im => im.UseSelectiveExtraction = prop);
        }

        public TransitionFullScan ChangeRetentionTimeFilter(RetentionTimeFilterType retentionTimeFilterType,
            double retentionTimeFilterLength)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im.RetentionTimeFilterType = retentionTimeFilterType;
                im.RetentionTimeFilterLength = retentionTimeFilterLength;
            });
        }

        #endregion

        #region Implementation of IXmlSerializable

        private enum ATTR
        {
            acquisition_method,
            precursor_filter_type,  // Skyline 1.2 and earlier
            precursor_filter,
            precursor_left_filter,
            precursor_right_filter,
            product_mass_analyzer,
            product_res,
            product_res_mz,
            precursor_isotopes,
            precursor_isotope_filter,
            precursor_mass_analyzer,
            precursor_res,
            precursor_res_mz,
            selective_extraction,
            scheduled_filter, // deprecated
            retention_time_filter_type,
            retention_time_filter_length,
            ignore_sim_scans,
        }

        void IValidating.Validate()
        {
            DoValidate();
        }

        private void DoValidate()
        {
            if (PrecursorIsotopes == FullScanPrecursorIsotopes.None)
            {
                if (PrecursorMassAnalyzer != FullScanMassAnalyzerType.none || PrecursorIsotopeFilter.HasValue || PrecursorRes.HasValue || PrecursorResMz.HasValue || IgnoreSimScans)
                    throw new InvalidDataException(Resources.TransitionFullScan_DoValidate_No_other_full_scan_MS1_filter_settings_are_allowed_when_no_precursor_isotopes_are_included);
            }
            else
            {
                if (PrecursorMassAnalyzer == FullScanMassAnalyzerType.qit)
                {
                    if (PrecursorIsotopes != FullScanPrecursorIsotopes.Count || PrecursorIsotopeFilter != 1)
                    {
                        throw new InvalidDataException(Resources.TransitionFullScan_DoValidate_For_MS1_filtering_with_a_QIT_mass_analyzer_only_1_isotope_peak_is_supported);
                    }
                }
                else
                {
                    if (IsotopeEnrichments == null)
                        IsotopeEnrichments = IsotopeEnrichmentsList.DEFAULT;
                    if (PrecursorIsotopes == FullScanPrecursorIsotopes.Count)
                    {
                        ValidateRange(PrecursorIsotopeFilter, MIN_ISOTOPE_COUNT, MAX_ISOTOPE_COUNT,
                                      Resources.TransitionFullScan_DoValidate_The_precursor_isotope_count_for_MS1_filtering_must_be_between__0__and__1__peaks);
                    }
                    else
                    {
                        ValidateRange(PrecursorIsotopeFilter, MIN_ISOTOPE_PERCENT, MAX_ISOTOPE_PERCENT,
                                      Resources.TransitionFullScan_DoValidate_The_precursor_isotope_percent_for_MS1_filtering_must_be_between__0___and__1___of_the_base_peak);
                    }
                }
                _cachedPrecursorRes = ValidateRes(PrecursorMassAnalyzer, PrecursorRes, PrecursorResMz);
            }

            if (AcquisitionMethod == FullScanAcquisitionMethod.None)
            {
                if (ProductMassAnalyzer != FullScanMassAnalyzerType.none || IsolationScheme != null || ProductRes.HasValue || ProductResMz.HasValue)
                    throw new InvalidDataException(Resources.TransitionInstrument_DoValidate_No_other_full_scan_MS_MS_filter_settings_are_allowed_when_precursor_filter_is_none);
            }
            else
            {
                if (AcquisitionMethod == FullScanAcquisitionMethod.DIA)
                {
                    if (IsolationScheme == null)
                        throw new InvalidDataException(Resources.TransitionFullScan_DoValidate_An_isolation_window_width_value_is_required_in_DIA_mode);
                }
                else
                {
                    if (IsolationScheme != null)
                    {
                        string message = string.Format(Resources
                                .TransitionFullScan_DoValidate_An_isolation_window_width_value_is_not_allowed_in__0___mode,
                            AcquisitionMethod);
                        throw new InvalidDataException(message);
                    }
                }

                _cachedProductRes = ValidateRes(ProductMassAnalyzer, ProductRes, ProductResMz);
            }
        }

        public static void ValidateRange(double? value, double min, double max, string messageFormat)
        {
            if (!value.HasValue || min > value.Value || value.Value > max)
                throw new InvalidDataException(string.Format(messageFormat, min, max));
        }

        private double ValidateRes(FullScanMassAnalyzerType analyzerType, double? res, double? resMz)
        {
            bool expectMz = false;
            if (analyzerType == FullScanMassAnalyzerType.qit)
            {
                if (!res.HasValue || MIN_LO_RES > res.Value || res.Value > MAX_LO_RES)
                {
                    throw new InvalidDataException(
                        string.Format(Resources.TransitionFullScan_ValidateRes_Resolution_must_be_between__0__and__1__for_QIT_,
                                      MIN_LO_RES, MAX_LO_RES));
                }
            }
            else if (analyzerType == FullScanMassAnalyzerType.centroided)
            {
                if (!res.HasValue || MIN_CENTROID_PPM > res.Value || res.Value > MAX_CENTROID_PPM)
                {
                    throw new InvalidDataException(string.Format(Resources.TransitionFullScan_ValidateRes_Mass_accuracy_must_be_between__0__and__1__for_centroided_data_,
                        MIN_CENTROID_PPM, MAX_CENTROID_PPM));
                }
            }
            else
            {
                if (!res.HasValue || MIN_HI_RES > res.Value || res.Value > MAX_HI_RES)
                {
                    throw new InvalidDataException(
                        string.Format(Resources.TransitionFullScan_ValidateRes_Resolving_power_must_be_between__0__and__1__for__2__,
                                      MIN_HI_RES, MAX_HI_RES, MassAnalyzerToString(analyzerType)));
                }
                expectMz = (analyzerType != FullScanMassAnalyzerType.tof);
            }
            if (expectMz)
            {
                if (!resMz.HasValue || MIN_RES_MZ > resMz.Value || resMz.Value > MAX_RES_MZ)
                {
                    throw new InvalidDataException(
                        string.Format(Resources.TransitionFullScan_ValidateRes_The_mz_value_at_which_the_resolving_power_is_calibrated_is_required_for__0__,
                                      MassAnalyzerToString(analyzerType)));
                }
            }
            else if (resMz.HasValue)
            {
                throw new InvalidDataException(
                    string.Format(Resources.TransitionFullScan_ValidateRes_Unexpected_resolving_power_mz_value_for__0__,
                                  MassAnalyzerToString(analyzerType)));
            }

            return GetDenominator(analyzerType, res.Value, resMz ?? 0);
        }

        public static TransitionFullScan Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new TransitionFullScan());
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            // Get precursor filter types from Skyline 1.2 and earlier, or acquisition method from later releases.
            AcquisitionMethod = FullScanAcquisitionMethod.FromLegacyName(reader.GetAttribute(ATTR.precursor_filter_type)) ??
                FullScanAcquisitionMethod.FromName(reader.GetAttribute(ATTR.acquisition_method));

            if (AcquisitionMethod != FullScanAcquisitionMethod.None)
            {
                ProductMassAnalyzer = reader.GetEnumAttribute(ATTR.product_mass_analyzer, FullScanMassAnalyzerType.qit);
                ProductRes = reader.GetDoubleAttribute(ATTR.product_res,
                    DEFAULT_RES_VALUES[(int) ProductMassAnalyzer]);
                if (ProductMassAnalyzer == FullScanMassAnalyzerType.ft_icr || ProductMassAnalyzer == FullScanMassAnalyzerType.orbitrap)
                    ProductResMz = reader.GetNullableDoubleAttribute(ATTR.product_res_mz) ?? DEFAULT_RES_MZ;
            }
            
            PrecursorIsotopes = reader.GetEnumAttribute(ATTR.precursor_isotopes,
                                             FullScanPrecursorIsotopes.None);
            if (PrecursorIsotopes != FullScanPrecursorIsotopes.None)
            {
                PrecursorIsotopeFilter = reader.GetDoubleAttribute(ATTR.precursor_isotope_filter,
                    PrecursorIsotopes == FullScanPrecursorIsotopes.Count ? DEFAULT_ISOTOPE_COUNT : DEFAULT_ISOTOPE_PERCENT);

                PrecursorMassAnalyzer = reader.GetEnumAttribute(ATTR.precursor_mass_analyzer, FullScanMassAnalyzerType.none);
                PrecursorRes = reader.GetDoubleAttribute(ATTR.precursor_res,
                    DEFAULT_RES_VALUES[(int)PrecursorMassAnalyzer]);
                if (PrecursorMassAnalyzer == FullScanMassAnalyzerType.ft_icr || PrecursorMassAnalyzer == FullScanMassAnalyzerType.orbitrap)
                    PrecursorResMz = reader.GetNullableDoubleAttribute(ATTR.precursor_res_mz) ?? DEFAULT_RES_MZ;
            }
            else
            {
                // Backward compatibility with before PrecursorIsotopes were added.
                PrecursorMassAnalyzer = reader.GetEnumAttribute(ATTR.precursor_mass_analyzer, FullScanMassAnalyzerType.none);
                if (PrecursorMassAnalyzer != FullScanMassAnalyzerType.none)
                {
                    PrecursorIsotopes = FullScanPrecursorIsotopes.Count;
                    PrecursorIsotopeFilter = 1;
                    PrecursorRes = reader.GetDoubleAttribute(ATTR.precursor_res,
                        DEFAULT_RES_VALUES[(int)PrecursorMassAnalyzer]);
                    if (PrecursorMassAnalyzer == FullScanMassAnalyzerType.ft_icr || PrecursorMassAnalyzer == FullScanMassAnalyzerType.orbitrap)
                        PrecursorResMz = reader.GetNullableDoubleAttribute(ATTR.precursor_res_mz) ?? DEFAULT_RES_MZ;
                }
            }

            IgnoreSimScans = reader.GetBoolAttribute(ATTR.ignore_sim_scans);
            UseSelectiveExtraction = reader.GetBoolAttribute(ATTR.selective_extraction);
            RetentionTimeFilterType = RetentionTimeFilterType.none;
            RetentionTimeFilterLength = 0;
            if (reader.GetBoolAttribute(ATTR.scheduled_filter))
            {
                // backwards compatibility to version 1.2
                RetentionTimeFilterType = RetentionTimeFilterType.scheduling_windows;
            }
            else
            {
                RetentionTimeFilterType = reader.GetEnumAttribute(ATTR.retention_time_filter_type, RetentionTimeFilterType.none);
                RetentionTimeFilterLength = reader.GetDoubleAttribute(ATTR.retention_time_filter_length);
            }

            // Create isolation scheme for backward compatibility.
            if (AcquisitionMethod == FullScanAcquisitionMethod.DIA)
            {
                double? precursorFilter = reader.GetNullableDoubleAttribute(ATTR.precursor_filter);
                double? precursorRightFilter = null;
                if (!precursorFilter.HasValue)
                {
                    precursorFilter = reader.GetNullableDoubleAttribute(ATTR.precursor_left_filter);
                    if (precursorFilter.HasValue)
                        precursorRightFilter = reader.GetDoubleAttribute(ATTR.precursor_right_filter, precursorFilter.Value);
                    else
                        precursorFilter = DEFAULT_PRECURSOR_MULTI_FILTER;
                }
                // May get overwritten below
                IsolationScheme = CreateIsolationSchemeForFilter(AcquisitionMethod, precursorFilter, precursorRightFilter);
            }

            bool hasInnerTags = !reader.IsEmptyElement;

            // Consume tag
            reader.Read();

            if (hasInnerTags)
            {
                // Read enrichment tags, if present.
                var readHelper = new XmlElementHelper<IsotopeEnrichments>();
                if (reader.IsStartElement(readHelper.ElementNames))
                {
                    IsotopeEnrichments = readHelper.Deserialize(reader);
                }

                // Read isolation window tags, if present.
                var readIsolationHelper = new XmlElementHelper<IsolationScheme>();
                if (reader.IsStartElement(readIsolationHelper.ElementNames))
                {
                    IsolationScheme = readIsolationHelper.Deserialize(reader);
                }

                // If there is an inner tag, there must be an end tag.
                reader.ReadEndElement();
            }

            DoValidate();
        }

        public void WriteXml(XmlWriter writer)
        {
            // Write attributes
            if (AcquisitionMethod != FullScanAcquisitionMethod.None)
            {
                writer.WriteAttribute(ATTR.acquisition_method, AcquisitionMethod.Name);
                writer.WriteAttribute(ATTR.product_mass_analyzer, ProductMassAnalyzer);
                writer.WriteAttributeNullable(ATTR.product_res, ProductRes);
                writer.WriteAttributeNullable(ATTR.product_res_mz, ProductResMz);
            }
            if (PrecursorIsotopes != FullScanPrecursorIsotopes.None)
            {
                writer.WriteAttribute(ATTR.precursor_isotopes, PrecursorIsotopes);
                writer.WriteAttributeNullable(ATTR.precursor_isotope_filter, PrecursorIsotopeFilter);
                writer.WriteAttribute(ATTR.precursor_mass_analyzer, PrecursorMassAnalyzer);
                writer.WriteAttributeNullable(ATTR.precursor_res, PrecursorRes);
                writer.WriteAttributeNullable(ATTR.precursor_res_mz, PrecursorResMz);
            }
            if (IgnoreSimScans)
                writer.WriteAttribute(ATTR.ignore_sim_scans, true);
            if (UseSelectiveExtraction)
                writer.WriteAttribute(ATTR.selective_extraction, true);
            if (RetentionTimeFilterType != RetentionTimeFilterType.none)
            {
                writer.WriteAttribute(ATTR.retention_time_filter_type, RetentionTimeFilterType);
                if (RetentionTimeFilterType != RetentionTimeFilterType.none)
                {
                    writer.WriteAttribute(ATTR.retention_time_filter_length, RetentionTimeFilterLength);
                }
            }
            if (IsotopeEnrichments != null)
                writer.WriteElement(IsotopeEnrichments);
            if (AcquisitionMethod == FullScanAcquisitionMethod.DIA && IsolationScheme != null)
                writer.WriteElement(IsolationScheme);
        }

        #endregion

        #region object overrides

        public bool Equals(TransitionFullScan other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.AcquisitionMethod, AcquisitionMethod) &&
                Equals(other.IsolationScheme, IsolationScheme) &&
                Equals(other.ProductMassAnalyzer, ProductMassAnalyzer) &&
                other.ProductRes.Equals(ProductRes) &&
                other.ProductResMz.Equals(ProductResMz) &&
                Equals(other.PrecursorIsotopes, PrecursorIsotopes) &&
                other.PrecursorIsotopeFilter.Equals(PrecursorIsotopeFilter) &&
                Equals(other.IsotopeEnrichments, IsotopeEnrichments) &&
                Equals(other.PrecursorMassAnalyzer, PrecursorMassAnalyzer) &&
                other.PrecursorRes.Equals(PrecursorRes) &&
                other.IgnoreSimScans == IgnoreSimScans &&
                other.UseSelectiveExtraction == UseSelectiveExtraction &&
                other.PrecursorResMz.Equals(PrecursorResMz) &&
                other.RetentionTimeFilterType == RetentionTimeFilterType &&
                other.RetentionTimeFilterLength == RetentionTimeFilterLength;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (TransitionFullScan)) return false;
            return Equals((TransitionFullScan) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = AcquisitionMethod.GetHashCode();
                result = (result * 397) ^ (IsolationScheme != null ? IsolationScheme.GetHashCode() : 0);
                result = (result*397) ^ ProductMassAnalyzer.GetHashCode();
                result = (result*397) ^ (ProductRes.HasValue ? ProductRes.Value.GetHashCode() : 0);
                result = (result*397) ^ (ProductResMz.HasValue ? ProductResMz.Value.GetHashCode() : 0);
                result = (result*397) ^ PrecursorIsotopes.GetHashCode();
                result = (result*397) ^ (PrecursorIsotopeFilter.HasValue ? PrecursorIsotopeFilter.Value.GetHashCode() : 0);
                result = (result*397) ^ (IsotopeEnrichments != null ? IsotopeEnrichments.GetHashCode() : 0);
                result = (result*397) ^ PrecursorMassAnalyzer.GetHashCode();
                result = (result*397) ^ (PrecursorRes.HasValue ? PrecursorRes.Value.GetHashCode() : 0);
                result = (result*397) ^ (PrecursorResMz.HasValue ? PrecursorResMz.Value.GetHashCode() : 0);
                result = (result*397) ^ IgnoreSimScans.GetHashCode();
                result = (result*397) ^ UseSelectiveExtraction.GetHashCode();
                result = (result*397) ^ RetentionTimeFilterType.GetHashCode();
                result = (result*397) ^ RetentionTimeFilterLength.GetHashCode();
                return result;
            }
        }

        #endregion
    }

    [XmlRoot("transition_integration")]
    public sealed class TransitionIntegration : Immutable, IValidating, IXmlSerializable
    {
        [Track]
        public bool IsIntegrateAll { get; private set; }

        [Track]
        public string SynchronizedIntegrationGroupBy { get; private set; }

        [Track]
        public bool SynchronizedIntegrationAll { get; private set; }

        [Track]
        public string[] SynchronizedIntegrationTargets { get; private set; }

        #region Property change methods

        public TransitionIntegration ChangeIntegrateAll(bool prop)
        {
            return ChangeProp(ImClone(this), im => im.IsIntegrateAll = prop);
        }

        public TransitionIntegration ChangeSynchronizedIntegration(string groupBy, bool all, string[] targets)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im.SynchronizedIntegrationGroupBy = groupBy;
                im.SynchronizedIntegrationAll = all;
                im.SynchronizedIntegrationTargets = !all && targets.Length > 0 ? targets : Array.Empty<string>();
            });
        }

        #endregion

        #region Implementation of IXmlSerializable

        private enum ATTR
        {
            integrate_all,
            group_by,
            all,
            value
        }

        private enum EL
        {
            synchronize_integration,
            target
        }

        void IValidating.Validate()
        {
        }

        public static TransitionIntegration Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new TransitionIntegration());
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            // Read start tag attributes
            IsIntegrateAll = reader.GetBoolAttribute(ATTR.integrate_all);

            // Consume tag
            reader.Read();

            if (reader.IsStartElement(EL.synchronize_integration))
            {
                SynchronizedIntegrationGroupBy = reader.GetAttribute(ATTR.group_by) ?? string.Empty;
                SynchronizedIntegrationAll = reader.GetBoolAttribute(ATTR.all);
                SynchronizedIntegrationTargets = Array.Empty<string>();

                if (!reader.IsEmptyElement)
                {
                    // Consume synchronize_integration start tag
                    reader.Read();

                    // Read synchronization values
                    var syncTargets = new List<string>();
                    while (reader.IsStartElement(EL.target))
                    {
                        syncTargets.Add(reader.GetAttribute(ATTR.value));
                        reader.Read();
                    }
                    SynchronizedIntegrationTargets = syncTargets.ToArray();
                }

                // Consume synchronize_integration end tag
                reader.Read();
                // Consume transition_integration end tag
                reader.Read();
            }
        }

        public void WriteXml(XmlWriter writer)
        {
            // Write attributes
            writer.WriteAttribute(ATTR.integrate_all, IsIntegrateAll);

            // Write synchronize_integration
            var hasGroupBy = SynchronizedIntegrationGroupBy != null;
            var hasSyncTargets = SynchronizedIntegrationTargets != null && SynchronizedIntegrationTargets.Length > 0;
            if (hasGroupBy || SynchronizedIntegrationAll || hasSyncTargets)
            {
                writer.WriteStartElement(EL.synchronize_integration);

                if (hasGroupBy)
                {
                    writer.WriteAttribute(ATTR.group_by, SynchronizedIntegrationGroupBy);
                }

                if (SynchronizedIntegrationAll)
                {
                    writer.WriteAttribute(ATTR.all, SynchronizedIntegrationAll);
                }

                if (hasSyncTargets)
                {
                    foreach (var target in SynchronizedIntegrationTargets)
                    {
                        writer.WriteStartElement(EL.target);
                        writer.WriteAttribute(ATTR.value, target);
                        writer.WriteEndElement();
                    }
                }

                writer.WriteEndElement();
            }
        }

        #endregion

        #region object overrides

        public bool Equals(TransitionIntegration other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return other.IsIntegrateAll.Equals(IsIntegrateAll) &&
                   Equals(other.SynchronizedIntegrationGroupBy, SynchronizedIntegrationGroupBy) &&
                   Equals(other.SynchronizedIntegrationAll, SynchronizedIntegrationAll) &&
                   ArrayUtil.EqualsDeep(other.SynchronizedIntegrationTargets, SynchronizedIntegrationTargets);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (TransitionIntegration)) return false;
            return Equals((TransitionIntegration) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = IsIntegrateAll.GetHashCode();
                if (SynchronizedIntegrationGroupBy != null)
                    result = (result * 397) ^ SynchronizedIntegrationGroupBy.GetHashCode();
                result = (result * 397) ^ SynchronizedIntegrationAll.GetHashCode();
                result = (result * 397) ^ ArrayUtil.GetHashCodeDeep(SynchronizedIntegrationTargets);
                return result;
            }
        }

        #endregion
    }
}
