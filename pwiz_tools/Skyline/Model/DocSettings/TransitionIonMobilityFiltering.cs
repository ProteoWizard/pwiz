/*
 * Original author: Brian Pratt <bspratt .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.IonMobility;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Serialization;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using Enum = System.Enum;

namespace pwiz.Skyline.Model.DocSettings
{
    /// <summary>
    /// Settings that determine Skyline's ion mobility filtering behavior
    /// </summary>
    [XmlRoot("ion_mobility_filtering")]
    public class TransitionIonMobilityFiltering : Immutable, IValidating, IXmlSerializable, IEquatable<TransitionIonMobilityFiltering>
    {

        public static TransitionIonMobilityFiltering EMPTY = new TransitionIonMobilityFiltering(IonMobilityLibrary.NONE, false, IonMobilityWindowWidthCalculator.EMPTY);

        public static bool IsNullOrEmpty(TransitionIonMobilityFiltering f) { return f == null || f.IsEmpty; }

        public TransitionIonMobilityFiltering(string name, string dbDir, IDictionary<LibKey, List<IonMobilityAndCCS>> dict, bool useSpectralLibraryIonMobilityValues, IonMobilityWindowWidthCalculator filterWindowWidthCalculator)
        {
            IonMobilityLibrary = IonMobilityLibrary.CreateFromDictionary(name, dbDir, dict);
            UseSpectralLibraryIonMobilityValues = useSpectralLibraryIonMobilityValues;
            FilterWindowWidthCalculator = filterWindowWidthCalculator;

            Validate();
        }

        public TransitionIonMobilityFiltering(IonMobilityLibrary ionMobilityLibrary, bool useSpectralLibraryIonMobilityValues, IonMobilityWindowWidthCalculator filterWindowWidthCalculator)
        {
            IonMobilityLibrary = ionMobilityLibrary ?? IonMobilityLibrary.NONE;
            UseSpectralLibraryIonMobilityValues = useSpectralLibraryIonMobilityValues;
            FilterWindowWidthCalculator = filterWindowWidthCalculator;

            Validate();
        }

        public bool IsEmpty { get { return Equals(EMPTY); } }

        [TrackChildren]
        public IonMobilityLibrary IonMobilityLibrary { get; private set; }

        [Track]
        public bool UseSpectralLibraryIonMobilityValues { get; private set; }

        [TrackChildren(ignoreName: true)]
        public IonMobilityWindowWidthCalculator FilterWindowWidthCalculator { get; private set; }

        public TransitionIonMobilityFiltering ChangeUseSpectralLibraryIonMobilityValues(bool prop)
        {
            return ChangeProp(ImClone(this), im => im.UseSpectralLibraryIonMobilityValues = prop);
        }

        public TransitionIonMobilityFiltering ChangeFilterWindowWidthCalculator(IonMobilityWindowWidthCalculator prop)
        {
            return ChangeProp(ImClone(this), im => im.FilterWindowWidthCalculator = prop);
        }

        public TransitionIonMobilityFiltering ChangeLibrary(IonMobilityLibrary prop)
        {
            return ChangeProp(ImClone(this), im => im.IonMobilityLibrary = prop);
        }


        public void Validate()
        {
            DoValidate();
        }

        private void DoValidate()
        {
            if (UseSpectralLibraryIonMobilityValues)
            {
                if (FilterWindowWidthCalculator == null)
                    FilterWindowWidthCalculator = IonMobilityWindowWidthCalculator.EMPTY;
                string errmsg = FilterWindowWidthCalculator.Validate();
                if (errmsg != null)
                {
                    throw new InvalidDataException(errmsg);
                }
            }

            // Defer further validation to the SrmSettings object
        }

        public eIonMobilityUnits GetFirstSeenIonMobilityUnits()
        {
            if (IonMobilityLibrary != null && !IonMobilityLibrary.IsNone)
            {
                var dict = IonMobilityLibrary.GetIonMobilityLibKeyMap();
                var val =
                    dict?.AsDictionary().Values.FirstOrDefault
                        (v => v.Any(l => l.IonMobility.Units != eIonMobilityUnits.none));
                if (val != null)
                {
                    var item = val.FirstOrDefault(i => i.IonMobility.Units != eIonMobilityUnits.none);
                    if (item!=null)
                    {
                        return item.IonMobility.Units;
                    }
                }
            }

            return eIonMobilityUnits.none; // Didn't find anything
        }

        public IonMobilityAndCCS GetIonMobilityFilter(LibKey ion, double mz,
            IIonMobilityFunctionsProvider ionMobilityFunctionsProvider)
        {
            var ionMobilities = GetIonMobilityInfoFromLibrary(ion); // Library may contain multiple conformers - just use first seen TODO:bspratt full support for multiple conformers
            // Convert from CCS to ion mobility if possible
            if (ionMobilities != null && 
                ionMobilityFunctionsProvider != null &&
                ionMobilityFunctionsProvider.ProvidesCollisionalCrossSectionConverter)
            {
                var result = ionMobilities.FirstOrDefault();
                if (!IonMobilityAndCCS.IsNullOrEmpty(result))
                {
                    // ReSharper disable once PossibleNullReferenceException
                    if (result.CollisionalCrossSectionSqA.HasValue)
                    {
                        var ionMobilityValue = ionMobilityFunctionsProvider.IonMobilityFromCCS(
                            result.CollisionalCrossSectionSqA.Value,
                            ion.PrecursorMz ?? mz, ion.Charge, ion);
                        if (ionMobilityValue.HasValue && // Successful CCS->IM conversion
                            !Equals(ionMobilityValue, result.IonMobility))
                        {
                            result = IonMobilityAndCCS.GetIonMobilityAndCCS(ionMobilityValue,
                                result.CollisionalCrossSectionSqA, result.HighEnergyIonMobilityValueOffset);
                        }
                    }
                }
                return result;
            }
            else
            {
                return ionMobilities?.FirstOrDefault();
            }
        }

        public List<IonMobilityAndCCS> GetIonMobilityInfoFromLibrary(LibKey ion)
        {
            // Locate this target in ion mobility library, if any
            if (IonMobilityLibrary != null && !IonMobilityLibrary.IsNone)
            {
                var dict = IonMobilityLibrary.GetIonMobilityLibKeyMap();
                if (dict != null && dict.TryGetValue(ion, out var imList))
                {
                    return imList;
                }
            }

            return null;
        }

        /// <summary>
        /// Get the ion mobility for the ion, and the width of the window centered thereon
        /// </summary>
        public IonMobilityFilter GetIonMobilityFilter(LibKey ion, double mz,
            IIonMobilityFunctionsProvider ionMobilityFunctionsProvider, double ionMobilityRangeMax)
        {
            var ionMobility = GetIonMobilityFilter(ion, mz, ionMobilityFunctionsProvider);
            if (ionMobility != null)
            {
                double? ionMobilityWindowWidth;
                if (ionMobility.IonMobility.HasValue)
                {
                    ionMobilityWindowWidth =
                        FilterWindowWidthCalculator.WidthAt(ionMobility.IonMobility.Mobility.Value, ionMobilityRangeMax);
                }
                else
                {
                    ionMobilityWindowWidth = null;
                }

                return IonMobilityFilter.GetIonMobilityFilter(ionMobility, ionMobilityWindowWidth);
            }
            else
            {
                return null;
            }

        }

        #region Implementation of IXmlSerializable

        public enum ATTR
        {
            ion_mobility_library,
            use_spectral_library_ion_mobility_values
        }

        public enum EL
        {
            ion_mobility_filtering
        }

        protected TransitionIonMobilityFiltering()
        {

        }

        public XmlSchema GetSchema()
        {
            return null;
        }


        public static TransitionIonMobilityFiltering Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new TransitionIonMobilityFiltering());
        }


        public void ReadXml(XmlReader reader)
        {

            var name = reader.Name;
            UseSpectralLibraryIonMobilityValues = reader.GetBoolAttribute(ATTR.use_spectral_library_ion_mobility_values, false);
            bool isBackwardCompatibility = !reader.IsStartElement(EL.ion_mobility_filtering);
            FilterWindowWidthCalculator = new IonMobilityWindowWidthCalculator(reader, false, isBackwardCompatibility); // Just reads attributes, does not advance reader
            // Consume start tag
            reader.ReadStartElement();
            var readHelper = new XmlElementHelper<IonMobilityLibrary>();
            if (reader.IsStartElement(readHelper.ElementNames))
            {
                IonMobilityLibrary = readHelper.Deserialize(reader);
                reader.ReadEndElement();
            }
            if (IonMobilityLibrary == null)
            {
                IonMobilityLibrary = IonMobilityLibrary.NONE;
            }
            Validate();
        }

        public void WriteXml(XmlWriter writer)
        {
            // Write attributes
            writer.WriteAttribute(ATTR.use_spectral_library_ion_mobility_values, UseSpectralLibraryIonMobilityValues);
            FilterWindowWidthCalculator.WriteXML(writer, false, true);
            // Write ion mobility library info
            if (IonMobilityLibrary != null && !IonMobilityLibrary.IsNone)
            {
                writer.WriteElement(IonMobilityLibrary);
            }

        }
        #endregion

        public bool Equals(TransitionIonMobilityFiltering other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            if (!Equals(IonMobilityLibrary, other.IonMobilityLibrary))
                return false;
            if (UseSpectralLibraryIonMobilityValues != other.UseSpectralLibraryIonMobilityValues)
                return false;
            if (!Equals(FilterWindowWidthCalculator, other.FilterWindowWidthCalculator))
                return false;
            return true;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((TransitionIonMobilityFiltering) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = UseSpectralLibraryIonMobilityValues.GetHashCode();
                hashCode = (hashCode * 397) ^ (FilterWindowWidthCalculator != null ? FilterWindowWidthCalculator.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (IonMobilityLibrary != null ? IonMobilityLibrary.GetHashCode() : 0);
                return hashCode;
            }
        }
    }

    public class IonMobilityWindowWidthCalculator : IEquatable<IonMobilityWindowWidthCalculator>
    {
        public enum ATTR
        {
            spectral_library_drift_times_resolving_power, // Obsolete Pre-20.13  misnomer, used for IMS types other than drift time 
            resolving_power,
            window_width_calc_type,
            peak_width_calc_type, // Obsolete Pre-20.12
            spectral_library_drift_times_peak_width_calc_type, // Obsolete Pre-20.13  misnomer, used for IMS types other than drift time
            width_at_dt_zero, // Obsolete Pre-20.13 misnomer, used for IMS types other than drift time
            width_at_ion_mobility_zero,
            width_at_dt_max, // Obsolete Pre-20.13 misnomer, used for IMS types other than drift time
            width_at_ion_mobility_max,
            fixed_width
        }

        public static readonly IonMobilityWindowWidthCalculator EMPTY =
            new IonMobilityWindowWidthCalculator(IonMobilityWindowWidthType.none, 0, 0, 0, 0);

        private static string AttrPrefix(bool isSpectralLibrary) { return isSpectralLibrary ? @"spectral_library_drift_times_" : string.Empty; }

        public IonMobilityWindowWidthCalculator(double resolvingPower)
            : this(IonMobilityWindowWidthType.resolving_power, resolvingPower, 0, 0, 0)
        {
        }

        public IonMobilityWindowWidthCalculator(IonMobilityWindowWidthType windowWidthMode,
            double resolvingPower,
            double widthAtIonMobilityValueZero,
            double widthAtIonMobilityValueMax,
            double fixedWindowWidth)
        {
            ResolvingPower = resolvingPower;
            PeakWidthAtIonMobilityValueZero = widthAtIonMobilityValueZero;
            PeakWidthAtIonMobilityValueMax = widthAtIonMobilityValueMax;
            FixedWindowWidth = fixedWindowWidth;
            if (windowWidthMode == IonMobilityWindowWidthType.none && resolvingPower != 0) // Reading an older format?
            {
                WindowWidthMode = IonMobilityWindowWidthType.resolving_power;
            }
            else
            {
                WindowWidthMode = windowWidthMode;
            }
        }

        /// <summary>
        /// Read various generations of XML serialization
        /// </summary>
        public IonMobilityWindowWidthCalculator(XmlReader reader, bool isLegacyPeptideSetting, bool isSpectralLibrary) : // Formerly this was in Peptide settings, and used overly specific "drift time" terms for ion mobility
            this(
                reader.GetEnumAttribute(IonMobilityPeakWidthCalcTypeAttr(isLegacyPeptideSetting, isSpectralLibrary), IonMobilityWindowWidthType.none),
                reader.GetDoubleAttribute(IonMobilityPeakWidthResolvingPowerAttr(isLegacyPeptideSetting, isSpectralLibrary), 0),
                reader.GetDoubleAttribute(AttrPrefix(isSpectralLibrary) + (isLegacyPeptideSetting ? ATTR.width_at_dt_zero : ATTR.width_at_ion_mobility_zero), 0),
                reader.GetDoubleAttribute(AttrPrefix(isSpectralLibrary) + (isLegacyPeptideSetting ? ATTR.width_at_dt_max : ATTR.width_at_ion_mobility_max), 0),
                reader.GetDoubleAttribute(ATTR.fixed_width, 0))
        {
            // Check to see if this was an old-style serialization of an empty object
            if (isLegacyPeptideSetting && isSpectralLibrary && WindowWidthMode==IonMobilityWindowWidthType.resolving_power)
            {
                var saveWindowWidthMode = WindowWidthMode;
                WindowWidthMode = IonMobilityWindowWidthType.none; // Modern object's way to indicate emptiness
                if (!IsEmpty) // Are all the other values in this object empty as well?
                {
                    WindowWidthMode = saveWindowWidthMode; // Not empty, restore the actual deserialized value
                }
            }
        }

        public static bool IsNullOrEmpty(IonMobilityWindowWidthCalculator val)
        {
            return val == null || val.Equals(EMPTY);
        }

        private static string IonMobilityPeakWidthCalcTypeAttr(bool isLegacyPeptideSetting, bool isSpectralLibrary)
        {
            return isLegacyPeptideSetting ?
                (isSpectralLibrary ? ATTR.spectral_library_drift_times_peak_width_calc_type : ATTR.peak_width_calc_type).ToString() :
                ATTR.window_width_calc_type.ToString();
        }

        private static string IonMobilityPeakWidthResolvingPowerAttr(bool isLegacyPeptideSetting, bool isSpectralLibrary)
        {
            return isLegacyPeptideSetting ?
                (isSpectralLibrary ? ATTR.spectral_library_drift_times_resolving_power : ATTR.resolving_power).ToString() :
                ATTR.resolving_power.ToString();
        }

        public void WriteXML(XmlWriter writer,
            bool isLegacyPeptideSetting, // For serializing to old-style documents where this was a peptide settings member
            bool isSpectralLibrary) // For serializing to old-style documents
        {
            if (WindowWidthMode != IonMobilityWindowWidthType.none) // Don't serialize if empty
            {
                writer.WriteAttribute(IonMobilityPeakWidthCalcTypeAttr(isLegacyPeptideSetting, isSpectralLibrary), WindowWidthMode);
                // Only write attributes that make sense for current width calculation mode, or that are nonzero (and might be useful later)
                if (WindowWidthMode == IonMobilityWindowWidthType.resolving_power || ResolvingPower != 0)
                    writer.WriteAttribute(IonMobilityPeakWidthResolvingPowerAttr(isLegacyPeptideSetting, isSpectralLibrary), ResolvingPower);
                if (WindowWidthMode == IonMobilityWindowWidthType.linear_range || PeakWidthAtIonMobilityValueZero != 0 || PeakWidthAtIonMobilityValueMax != 0)
                {
                    writer.WriteAttribute(isLegacyPeptideSetting ? (AttrPrefix(isSpectralLibrary) + ATTR.width_at_dt_zero) : ATTR.width_at_ion_mobility_zero.ToString(), PeakWidthAtIonMobilityValueZero);
                    writer.WriteAttribute(isLegacyPeptideSetting ? AttrPrefix(isSpectralLibrary) + ATTR.width_at_dt_max : ATTR.width_at_ion_mobility_max.ToString(), PeakWidthAtIonMobilityValueMax);
                }
                if (!isLegacyPeptideSetting && (WindowWidthMode == IonMobilityWindowWidthType.fixed_width || FixedWindowWidth != 0))
                    writer.WriteAttribute(ATTR.fixed_width, FixedWindowWidth);
            }
        }

        public enum IonMobilityWindowWidthType
        {
            resolving_power, // Agilent, etc
            linear_range, // Waters SONAR etc
            fixed_width,  // Agilent "high resolution demultiplexing"

            none // this needs to be last
        };

        [Track]
        public IonMobilityWindowWidthType WindowWidthMode { get; private set; }

        // TODO: custom localizer
        // For Water-style (SONAR) linear peak width calcs
        [Track]
        public double PeakWidthAtIonMobilityValueZero { get; private set; }
        [Track]
        public double PeakWidthAtIonMobilityValueMax { get; private set; }

        // For Agilent-style resolving power peak width calcs
        [Track] public double ResolvingPower { get; private set; }

        // For Agilent "high resolution demultiplexing"
        [Track] public double FixedWindowWidth { get; private set; }

        public double WidthAt(double ionMobility, double ionMobilityMax)
        {
            switch (WindowWidthMode)
            {
                case IonMobilityWindowWidthType.resolving_power:
                    return Math.Abs((ResolvingPower > 0 ? 2.0 / ResolvingPower : double.MaxValue) *
                                    ionMobility); // 2.0*ionMobility/resolvingPower
                case IonMobilityWindowWidthType.fixed_width:
                    return FixedWindowWidth;
                case IonMobilityWindowWidthType.linear_range:
                    Assume.IsTrue(ionMobilityMax != 0,
                        @"Expected ionMobilityMax value != 0 for linear range ion mobility window calculation");
                    return PeakWidthAtIonMobilityValueZero +
                           Math.Abs(ionMobility * (PeakWidthAtIonMobilityValueMax - PeakWidthAtIonMobilityValueZero) /
                                    ionMobilityMax);
            }
            return 0;
        }

        public string Validate()
        {

            switch (WindowWidthMode)
            {
                case IonMobilityWindowWidthType.resolving_power:
                    if (ResolvingPower < 0) // Accept 0 as "no IMS filtering"
                        return Resources.DriftTimePredictor_Validate_Resolving_power_must_be_greater_than_0_;
                    break;
                case IonMobilityWindowWidthType.linear_range:
                    if (PeakWidthAtIonMobilityValueZero < 0 ||
                        PeakWidthAtIonMobilityValueMax < PeakWidthAtIonMobilityValueZero)
                        return Resources.DriftTimeWindowWidthCalculator_Validate_Peak_width_must_be_non_negative_;
                    break;
                case IonMobilityWindowWidthType.fixed_width:
                    if (FixedWindowWidth < 0)
                        return DocSettingsResources.DriftTimeWindowWidthCalculator_Validate_Fixed_window_width_must_be_non_negative_;
                    break;
            }

            return null;
        }

        public bool IsEmpty
        {
            get { return Equals(EMPTY); }
        }

        public bool Equals(IonMobilityWindowWidthCalculator other)
        {
            return other != null &&
                   Equals(other.WindowWidthMode, WindowWidthMode) &&
                   Equals(other.ResolvingPower, ResolvingPower) &&
                   Equals(other.FixedWindowWidth, FixedWindowWidth) &&
                   Equals(other.PeakWidthAtIonMobilityValueZero, PeakWidthAtIonMobilityValueZero) &&
                   Equals(other.PeakWidthAtIonMobilityValueMax, PeakWidthAtIonMobilityValueMax);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as IonMobilityWindowWidthCalculator);
        }

        public override int GetHashCode()
        {
            int result = WindowWidthMode.GetHashCode();
            result = (result * 397) ^ ResolvingPower.GetHashCode();
            result = (result * 397) ^ FixedWindowWidth.GetHashCode();
            result = (result * 397) ^ PeakWidthAtIonMobilityValueZero.GetHashCode();
            result = (result * 397) ^ PeakWidthAtIonMobilityValueMax.GetHashCode();
            return result;
        }

        public override string ToString() // For debug use, not user-facing
        {
            return string.Format(@"{0} r{1} l{2},{3} f{4}", WindowWidthMode, ResolvingPower, PeakWidthAtIonMobilityValueZero, PeakWidthAtIonMobilityValueMax, FixedWindowWidth);
        }
    }

    /// <summary>
    /// Represents an observed ion mobility value for
    /// a molecule with a given adduct.
    /// OBSOLETE, retained only for backward compatibility in (de)serialization
    /// </summary>
    public sealed class MeasuredIonMobility : IXmlSerializable, IComparable<MeasuredIonMobility>
    {
        public Target Target { get; private set; } // ModifiedSequence for peptides, PrimaryEquivalenceKey for small molecules

        public Adduct Charge { get; private set; }
        public IonMobilityAndCCS IonMobilityInfo { get; private set; }

        public MeasuredIonMobility(Target target, Adduct charge, IonMobilityAndCCS ionMobilityInfo)
        {
            Target = target;
            Charge = charge;
            IonMobilityInfo = ionMobilityInfo;
        }

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private MeasuredIonMobility()
        {
        }

        public enum ATTR
        {
            modified_sequence, // Pre-19.12
            target,
            charge,
            drift_time, // Obsolete even before 19.1
            ion_mobility,
            ccs,
            high_energy_drift_time_offset, // Obsolete even before 19.1
            high_energy_ion_mobility_offset,
            ion_mobility_units
        }

        public static MeasuredIonMobility Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new MeasuredIonMobility());
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            // Read tag attributes
            var target = reader.GetAttribute(ATTR.target);
            if (target.IsNullOrEmpty())
                target = reader.GetAttribute(ATTR.modified_sequence); // Backward compatibility
            Target = Target.FromSerializableString(target);
            var adductOrCharge = reader.GetAttribute(ATTR.charge);  // May be a bare number or an adduct description
            Charge = Target.IsProteomic
                ? Adduct.FromStringAssumeProtonated(adductOrCharge)
                : Adduct.FromStringAssumeProtonatedNonProteomic(adductOrCharge);
            var ionMobilityValue = reader.GetNullableDoubleAttribute(ATTR.drift_time); // Obsolete, backward compatibility
            if (ionMobilityValue.HasValue)
            {
                IonMobilityInfo = IonMobilityAndCCS.GetIonMobilityAndCCS(IonMobilityValue.GetIonMobilityValue(
                        ionMobilityValue.Value,
                        eIonMobilityUnits.drift_time_msec),
                    reader.GetNullableDoubleAttribute(ATTR.ccs),
                    reader.GetDoubleAttribute(ATTR.high_energy_drift_time_offset, 0));
            }
            else
            {
                ionMobilityValue = reader.GetNullableDoubleAttribute(ATTR.ion_mobility);
                if (ionMobilityValue.HasValue)
                {
                    var units = SmallMoleculeTransitionListReader.IonMobilityUnitsFromAttributeValue(
                        reader.GetAttribute(ATTR.ion_mobility_units));
                    IonMobilityInfo = IonMobilityAndCCS.GetIonMobilityAndCCS(IonMobilityValue.GetIonMobilityValue(
                            ionMobilityValue.Value,
                            units),
                        reader.GetNullableDoubleAttribute(ATTR.ccs),
                        reader.GetDoubleAttribute(ATTR.high_energy_ion_mobility_offset, 0));
                }
                else
                {
                    IonMobilityInfo = IonMobilityAndCCS.EMPTY;
                }
            }

            // Consume tag
            reader.Read();
        }

        public void WriteXml(XmlWriter writer)
        {
            // Write tag attributes
            writer.WriteAttribute(ATTR.modified_sequence, Target.ToSerializableString());
            writer.WriteAttribute(ATTR.charge, Target.IsProteomic ? Charge.ToString() : Charge.AdductFormula);
            if (IonMobilityInfo.IonMobility.Units != eIonMobilityUnits.none)
            {
                if ((IonMobilityInfo.IonMobility.Mobility ?? 0) != 0)
                    writer.WriteAttribute(ATTR.ion_mobility, IonMobilityInfo.IonMobility.Mobility);
                if ((IonMobilityInfo.HighEnergyIonMobilityValueOffset??0) != 0)
                    writer.WriteAttribute(ATTR.high_energy_ion_mobility_offset, IonMobilityInfo.HighEnergyIonMobilityValueOffset);
                if ((IonMobilityInfo.CollisionalCrossSectionSqA ?? 0) != 0)
                    writer.WriteAttribute(ATTR.ccs, IonMobilityInfo.CollisionalCrossSectionSqA);
                writer.WriteAttributeString(ATTR.ion_mobility_units, IonMobilityInfo.IonMobility.Units.ToString());
            }
        }

        #endregion

        #region object overrides

        public bool Equals(MeasuredIonMobility obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj.Target, Target) && Equals(obj.Charge, Charge) &&
                   Equals(obj.IonMobilityInfo, IonMobilityInfo);
        }

        public int CompareTo(MeasuredIonMobility other)
        {
            int result = Target.CompareTo(other.Target);
            if (result != 0)
                return result;

            result = Adduct.Compare(Charge, other.Charge);
            if (result != 0)
                return result;

            return IonMobilityInfo.CompareTo(other.IonMobilityInfo);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof(MeasuredIonMobility)) return false;
            return Equals((MeasuredIonMobility)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = (Target != null ? Target.GetHashCode() : 0);
                result = (result * 397) ^ IonMobilityInfo.GetHashCode();
                result = (result * 397) ^ Charge.GetHashCode();
                return result;
            }
        }

        #endregion
    }

    public interface IIonMobilityInfoProvider
    {
        string Name { get; }

        bool SupportsMultipleConformers { get; } // Spectral libraries don't provide for more than one CCS per ion, but ion mobility libraries may

        IonMobilityAndCCS GetLibraryMeasuredIonMobilityAndCCS(LibKey peptide, double mz, IIonMobilityFunctionsProvider instrumentInfo);

        IDictionary<LibKey, IonMobilityAndCCS[]> GetIonMobilityDict();

    }

    /// <summary>
    /// Contains ion mobility and its Collisional Cross Section basis (if known), 
    /// and the effect on ion mobility in high energy spectra as in Waters MSe
    /// </summary>
    public class IonMobilityAndCCS : Immutable, IComparable
    {
        public static readonly IonMobilityAndCCS EMPTY = new IonMobilityAndCCS(IonMobilityValue.EMPTY, null, null);


        private IonMobilityAndCCS(IonMobilityValue ionMobility, double? collisionalCrossSectionSqA,
            double? highEnergyIonMobilityValueOffset)
        {
            IonMobility = ionMobility;
            CollisionalCrossSectionSqA = collisionalCrossSectionSqA == 0 ? null : collisionalCrossSectionSqA;
            HighEnergyIonMobilityValueOffset = highEnergyIonMobilityValueOffset;
        }

        public static IonMobilityAndCCS GetIonMobilityAndCCS(double? ionMobilityValue, eIonMobilityUnits units,
            double? collisionalCrossSectionSqA, double? highEnergyIonMobilityValueOffset)
        {
            return GetIonMobilityAndCCS(IonMobilityValue.GetIonMobilityValue(ionMobilityValue, units),
                collisionalCrossSectionSqA, highEnergyIonMobilityValueOffset);
        }

        public static IonMobilityAndCCS GetIonMobilityAndCCS(IonMobilityValue ionMobilityValue,
            double? collisionalCrossSectionSqA, double? highEnergyIonMobilityValueOffset)
        {
            return ionMobilityValue.HasValue || (collisionalCrossSectionSqA??0) != 0
                ? new IonMobilityAndCCS(ionMobilityValue, collisionalCrossSectionSqA, highEnergyIonMobilityValueOffset)
                : EMPTY;
        }

        public static IonMobilityAndCCS GetIonMobilityAndCCS(ExplicitTransitionGroupValues values)
        {
            return values.IonMobility.HasValue || values.CollisionalCrossSectionSqA.HasValue
                ? new IonMobilityAndCCS(IonMobilityValue.GetIonMobilityValue(values.IonMobility, values.IonMobilityUnits), values.CollisionalCrossSectionSqA, null)
                : EMPTY;
        }

        [Track]
        public string Units
        {
            get { return IonMobilityFilter.IonMobilityUnitsL10NString(IonMobility.Units); }
        }

        [TrackChildren(ignoreName: true)] public IonMobilityValue IonMobility { get; private set; }
        [Track] public double? CollisionalCrossSectionSqA { get; private set; }

        [Track]
        public double? HighEnergyIonMobilityValueOffset // As in Waters MSe, where product ions fly a bit faster due to added kinetic energy. A negative value means the fragment flies faster (so has a smaller drift time)
        {
            get;
            private set;
        } 

        public double? GetHighEnergyIonMobility()
        {
            if (IonMobility.HasValue)
            {
                return IonMobility.Mobility + (HighEnergyIonMobilityValueOffset??0);
            }

            return null;
        }

        /// <summary>
        /// Merge non-empty parts of other into a copy of this
        /// </summary>
        public IonMobilityAndCCS Merge(IonMobilityAndCCS other)
        {
            var val = this;
            if (other.HasCollisionalCrossSection &&
                !Equals(other.CollisionalCrossSectionSqA, CollisionalCrossSectionSqA))
            {
                val = ChangeProp(ImClone(this), im => im.CollisionalCrossSectionSqA = other.CollisionalCrossSectionSqA);
            }
            if (other.HasIonMobilityValue && 
                !Equals(other.IonMobility, IonMobility))
            {
                val = ChangeProp(ImClone(this), im => im.IonMobility = IonMobility.Merge(other.IonMobility));
            }
            if (other.HighEnergyIonMobilityValueOffset.HasValue &&
                other.HighEnergyIonMobilityValueOffset != HighEnergyIonMobilityValueOffset)
            {
                val = ChangeProp(ImClone(this),
                    im => im.HighEnergyIonMobilityValueOffset = other.HighEnergyIonMobilityValueOffset);
            }
            return val;
        }

        public IonMobilityAndCCS ChangeCollisionalCrossSection(double? ccs)
        {
            return Equals(ccs, CollisionalCrossSectionSqA) ? 
                this :
                ChangeProp(ImClone(this), im => im.CollisionalCrossSectionSqA = ccs);
        }

        public IonMobilityAndCCS ChangeHighEnergyIonMobilityOffset(double? offset)
        {
            return Equals(offset, HighEnergyIonMobilityValueOffset) ?
                this :
                ChangeProp(ImClone(this), im => im.HighEnergyIonMobilityValueOffset = offset);
        }

        public IonMobilityAndCCS ChangeIonMobilityUnits(eIonMobilityUnits units)
        {
            return Equals(units, IonMobility.Units) ?
                this : 
                ChangeProp(ImClone(this), im => im.IonMobility = im.IonMobility.ChangeIonMobilityUnits(units));
        }

        public bool HasCollisionalCrossSection { get { return (CollisionalCrossSectionSqA ?? 0) != 0; } }
        public bool HasIonMobilityValue { get { return IonMobility.HasValue; } }
        public bool IsEmpty { get { return !HasIonMobilityValue && !HasCollisionalCrossSection; } }
        public static bool IsNullOrEmpty(IonMobilityAndCCS val) {  return val == null || val.IsEmpty; }

        public void Write(Stream stream)
        {
            PrimitiveArrays.WriteOneValue(stream, IonMobility.Mobility ?? 0);
            PrimitiveArrays.WriteOneValue(stream, (int)IonMobility.Units);
            PrimitiveArrays.WriteOneValue(stream, CollisionalCrossSectionSqA ?? 0);
            PrimitiveArrays.WriteOneValue(stream, HighEnergyIonMobilityValueOffset ?? 0);
        }

        public static IonMobilityAndCCS Read(Stream stream)
        {
            double ionMobility = PrimitiveArrays.ReadOneValue<double>(stream);
            eIonMobilityUnits units = (eIonMobilityUnits)PrimitiveArrays.ReadOneValue<int>(stream);
            double collisionalCrossSectionSqA = PrimitiveArrays.ReadOneValue<double>(stream);
            double highEnergyOffset = PrimitiveArrays.ReadOneValue<double>(stream);
            return ionMobility == 0 && collisionalCrossSectionSqA == 0 && highEnergyOffset == 0 ?
                EMPTY :
                GetIonMobilityAndCCS(IonMobilityValue.GetIonMobilityValue(ionMobility != 0 ? ionMobility : (double?)null, units), 
                    collisionalCrossSectionSqA > 0 ? collisionalCrossSectionSqA : (double?)null, highEnergyOffset);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return 0 == CompareTo(obj as IonMobilityAndCCS);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = IonMobility.GetHashCode();
                hashCode = (hashCode * 397) ^ CollisionalCrossSectionSqA.GetHashCode();
                hashCode = (hashCode * 397) ^ HighEnergyIonMobilityValueOffset.GetHashCode();
                return hashCode;
            }
        }

        public int CompareTo(object obj)
        {
            if (!(obj is IonMobilityAndCCS other))
                return 1;
            var val = IonMobility.CompareTo(other.IonMobility);
            if (val != 0)
                return val;
            val = Nullable.Compare(CollisionalCrossSectionSqA, other.CollisionalCrossSectionSqA);
            if (val != 0)
                return val;
            var diff = HighEnergyIonMobilityValueOffset - other.HighEnergyIonMobilityValueOffset;
            if (diff > 0)
                return 1;
            else if (diff < 0)
                return -1;
            return 0;
        }

        public override string ToString() // For debug convenience
        {
            return string.Format(@"ccs{0}/{1}/he{2}/{3}", CollisionalCrossSectionSqA, IonMobility, HighEnergyIonMobilityValueOffset, Units);
        }
    }


    /// <summary>
    /// Contains the ion mobility and window used to filter scans
    /// </summary>
    public class IonMobilityFilter : Immutable, IComparable, IEquatable<IonMobilityFilter>
    {
        public static readonly IonMobilityFilter EMPTY = new IonMobilityFilter(IonMobilityAndCCS.EMPTY, null);
        public const double DoubleToIntEpsilon = 0.001; // Allow for a little rounding in double<->int conversion in SONAR use

        public static bool IsNullOrEmpty(IonMobilityFilter filter)
        {
            return filter == null || filter.IsEmpty;
        }

        public static IonMobilityFilter GetIonMobilityFilter(IonMobilityAndCCS ionMobility,
            double? ionMobilityExtractionWindowWidth)
        {
            if (ionMobility.IsEmpty || // Nothing to filter
                (ionMobilityExtractionWindowWidth??0) == 0) // No window set, or zero window
            {
                return EMPTY;
            }
            return new IonMobilityFilter(ionMobility,
                ionMobilityExtractionWindowWidth);
        }

        public static IonMobilityFilter GetIonMobilityFilter(double? ionMobility,
            eIonMobilityUnits units,
            double? ionMobilityExtractionWindowWidth,
            double? collisionalCrossSectionSqA)
        {
            if (!ionMobility.HasValue
                && !ionMobilityExtractionWindowWidth.HasValue)
            {
                return EMPTY;
            }
            return new IonMobilityFilter(IonMobilityAndCCS.GetIonMobilityAndCCS(ionMobility, units, collisionalCrossSectionSqA, null),
                ionMobilityExtractionWindowWidth);
        }

        public static IonMobilityFilter GetIonMobilityFilter(IonMobilityValue ionMobility,
            double? ionMobilityExtractionWindowWidth,
            double? collisionalCrossSectionSqA)
        {
            if (IonMobilityValue.IsNullOrEmpty(ionMobility)
                && !ionMobilityExtractionWindowWidth.HasValue)
            {
                return EMPTY;
            }
            return new IonMobilityFilter(IonMobilityAndCCS.GetIonMobilityAndCCS(ionMobility, collisionalCrossSectionSqA, null),
                ionMobilityExtractionWindowWidth);
        }



        public IonMobilityFilter ChangeIonMobilityUnits(eIonMobilityUnits units)
        {
            if (Equals(IonMobilityUnits, units))
                return this;
            return ChangeProp(ImClone(this), im => im.IonMobilityAndCCS = IonMobilityAndCCS.ChangeIonMobilityUnits(units));

        }

        public IonMobilityFilter ChangeCollisionCrossSection(double ?ccs)
        {
            return Equals(CollisionalCrossSectionSqA, ccs) ? this : 
                ChangeProp(ImClone(this), im => im.IonMobilityAndCCS = IonMobilityAndCCS.ChangeCollisionalCrossSection(ccs));
        }

        private IonMobilityFilter(IonMobilityAndCCS ionMobilityAndCCS,
            double? ionMobilityExtractionWindowWidth)
        {
            IonMobilityAndCCS = ionMobilityAndCCS;
            IonMobilityExtractionWindowWidth = ionMobilityExtractionWindowWidth;
            // Sanity check for SONAR filters - bounds should evaluate as integers since they're bins
            Assume.IsTrue(IonMobilityUnits != eIonMobilityUnits.waters_sonar || 
                          !IonMobilityAndCCS.HasCollisionalCrossSection &&
                          !IonMobilityAndCCS.HighEnergyIonMobilityValueOffset.HasValue &&
                          Math.Abs(IonMobilityAndCCS.IonMobility.Mobility.Value - 0.5 * IonMobilityExtractionWindowWidth.Value - 
                                   Math.Round(IonMobilityAndCCS.IonMobility.Mobility.Value - 0.5 * IonMobilityExtractionWindowWidth.Value)) <= DoubleToIntEpsilon,
                @"unexpected values for Waters SONAR filtering");
        }
        public IonMobilityAndCCS IonMobilityAndCCS { get; private set; }
        public double? CollisionalCrossSectionSqA => IonMobilityAndCCS.CollisionalCrossSectionSqA; // The CCS value used to get the ion mobility, if known
        public double? IonMobilityExtractionWindowWidth { get; private set; }
        public double? HighEnergyIonMobilityOffset => IonMobilityAndCCS.HighEnergyIonMobilityValueOffset; // As in Waters MsE, where ions move a bit faster due to more energetic collision in the high energy part of the cycle
        public eIonMobilityUnits IonMobilityUnits { get { return HasIonMobilityValue ? IonMobilityAndCCS.IonMobility.Units : eIonMobilityUnits.none; } }
        public IonMobilityValue IonMobility => IonMobilityAndCCS.IonMobility;
        public bool HasIonMobilityValue => IonMobilityAndCCS.HasIonMobilityValue;
        public bool IsEmpty { get { return IonMobilityAndCCS.IsEmpty; } }

        // Used by TransitionGroupDocNode.AddChromInfo to aggregate ion mobility information from all transitions.
        // All transitions, whether MS1 or MS2, are expected to have the same sets of CCS values, though MS2 transitions
        // may have distinct ion mobilities. All transitions may have different ion mobilities and/or extraction widths
        // than those currently held if a settings change and reimport has caused them to be recalculated.
        public IonMobilityFilter Merge(IonMobilityFilter other, bool isMs1)
        {
            var val = this;

            // Filling in MS1 or MS2 data, or just more of what we already know
            if (isMs1)
            {
                // We expect these all to be the same for MS1, but can't assert that here because we
                // may be in the process of re-import with a different filter setting.
                if (!Equals(other.IonMobilityAndCCS, val.IonMobilityAndCCS))
                    val = ChangeProp(ImClone(val), im => im.IonMobilityAndCCS = other.IonMobilityAndCCS);
                if (!Equals(other.IonMobilityExtractionWindowWidth, IonMobilityExtractionWindowWidth))
                    val = ChangeProp(ImClone(this), im => im.IonMobilityExtractionWindowWidth = other.IonMobilityExtractionWindowWidth);
            }
            else
            {
                // We expect these all to be the same for MS/MS, but can't assert that here because we
                // may be in the process of re-import with a different filter setting.
                // CCS isn't really a fragment concept, they carry around the CCS of the parent ion if they have a CCS at all
                var thisHighEnergyIonMobility =
                    IonMobilityAndCCS.IonMobility.Mobility.Value + (HighEnergyIonMobilityOffset ?? 0);
                var otherHighEnergyIonMobility =
                    other.IonMobilityAndCCS.IonMobility.Mobility.Value +(other.HighEnergyIonMobilityOffset ?? 0);
                if (!Equals(otherHighEnergyIonMobility, thisHighEnergyIonMobility))
                   val = ChangeProp(ImClone(val), im => im.IonMobilityAndCCS = IonMobilityAndCCS.ChangeHighEnergyIonMobilityOffset(otherHighEnergyIonMobility- IonMobilityAndCCS.IonMobility.Mobility.Value));
            }

            if (!IonMobilityExtractionWindowWidth.HasValue && other.IonMobilityExtractionWindowWidth.HasValue)
                val = ChangeProp(ImClone(this), im => im.IonMobilityExtractionWindowWidth = other.IonMobilityExtractionWindowWidth);
            if (other.CollisionalCrossSectionSqA.HasValue && !Equals(other.CollisionalCrossSectionSqA, val.CollisionalCrossSectionSqA))
                val = ChangeProp(ImClone(val), im => im.IonMobilityAndCCS = IonMobilityAndCCS.ChangeCollisionalCrossSection(other.CollisionalCrossSectionSqA));
            return val;

        }

        public IonMobilityFilter ApplyOffset(double offset)
        {
            if (offset == 0 || !IonMobility.HasValue)
                return this;
            return GetIonMobilityFilter(IonMobility.Mobility + offset, IonMobilityUnits, IonMobilityExtractionWindowWidth, CollisionalCrossSectionSqA);
        }

        public bool ContainsIonMobility(double val, bool useHighEnergyOffset)
        {
            if (IsEmpty)
                return true; // It doesn't NOT include it
            var im = useHighEnergyOffset ? val - HighEnergyIonMobilityOffset : val;
            var lo = IonMobilityAndCCS.IonMobility.Mobility.Value - IonMobilityExtractionWindowWidth.Value / 2;
            if (im < lo)
                return false;
            return im <= lo + IonMobilityExtractionWindowWidth.Value;
        }

        public static string IonMobilityUnitsL10NString(eIonMobilityUnits units)
        {
            switch (units)
            {
                case eIonMobilityUnits.inverse_K0_Vsec_per_cm2:
                    return DocSettingsResources.IonMobilityFilter_IonMobilityUnitsString__1_K0__Vs_cm_2_;
                case eIonMobilityUnits.drift_time_msec:
                    return Resources.IonMobilityFilter_IonMobilityUnitsString_Drift_Time__ms_;
                case eIonMobilityUnits.compensation_V:
                    return DocSettingsResources.IonMobilityFilter_IonMobilityUnitsString_Compensation_Voltage__V_;
                case eIonMobilityUnits.waters_sonar: // Not really ion mobility, but uses IMS hardware and our IMS filtering code
                case eIonMobilityUnits.none:
                    return DocSettingsResources.IonMobilityFilter_IonMobilityUnitsL10NString_None;
                default:
                    return null;
            }
        }

        public static bool AcceptNegativeMobilityValues(eIonMobilityUnits units)
        {
            return units == eIonMobilityUnits.compensation_V;
        }

        public static eIonMobilityUnits IonMobilityUnitsFromL10NString(string units)
        {
            if (TryParseIonMobilityUnits(units, out var result))
                return result;
            return eIonMobilityUnits.none;
        }

        public static bool TryParseIonMobilityUnits(string units, out eIonMobilityUnits result)
        {
            result = eIonMobilityUnits.none;
            if (string.IsNullOrEmpty(units))
            {
                return false;
            }
            foreach (eIonMobilityUnits u in Enum.GetValues(typeof(eIonMobilityUnits)))
            {
                var ionMobilityUnitsL10NString = IonMobilityUnitsL10NString(u);
                if (string.Equals(units, ionMobilityUnitsL10NString, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(units, u.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    result = u;
                    return true;
                }
            }
            // No match - maybe it's in a different language
            var success = false;
            var currentCulture = Thread.CurrentThread.CurrentUICulture;
            try
            {
                foreach (var tryCulture in CultureUtil.AvailableDisplayLanguages())
                {
                    Thread.CurrentThread.CurrentUICulture = tryCulture;
                    foreach (eIonMobilityUnits u in Enum.GetValues(typeof(eIonMobilityUnits)))
                    {
                        if (u != eIonMobilityUnits.none)
                        {
                            var ionMobilityUnitsL10NString = IonMobilityUnitsL10NString(u);
                            if (string.Equals(units, ionMobilityUnitsL10NString, StringComparison.OrdinalIgnoreCase))
                            {
                                result = u;
                                success = true;
                                break;
                            }
                        }
                    }
                    if (success)
                        break;
                }
            }
            finally
            {
                Thread.CurrentThread.CurrentUICulture = currentCulture;
            }

            return success;
        }

        public static string[] KnownIonMobilityTypes
        {
            get
            {
                var result = new List<string>();
                var currentCulture = Thread.CurrentThread.CurrentUICulture;
                foreach (var tryCulture in CultureUtil.AvailableDisplayLanguages())
                {
                    Thread.CurrentThread.CurrentUICulture = tryCulture;
                    foreach (eIonMobilityUnits u in Enum.GetValues(typeof(eIonMobilityUnits)))
                    {
                        if (u != eIonMobilityUnits.none && u!= eIonMobilityUnits.unknown)
                            result.Add(IonMobilityUnitsL10NString(u));
                    }
                }
                Thread.CurrentThread.CurrentUICulture = currentCulture;
                return result.ToArray();
            }
        }

        public enum SerializationElementType { TransitionChromInfo, TransitionGroupChromInfo, LegacyMeasuredIonMobility }

        /// <summary>
        /// XML serialization with support for different element contexts, and
        /// backward compatibility doc formats where IM settings were in peptide
        /// settings instead of transition settings
        /// </summary>
        public void WriteAttributes(XmlWriter writer, SerializationElementType serializationElementType, DocumentFormat skylineVersion)
        {
            if (IsEmpty)
                return;

            if (serializationElementType == SerializationElementType.TransitionGroupChromInfo)
            {
                // Express in terms of ms1 vs fragment
                writer.WriteAttributeNullable(skylineVersion < DocumentFormat.TRANSITION_SETTINGS_ION_MOBILITY ? 
                        DocumentSerializer.ATTR.ion_mobility_ms1 :
                        DocumentSerializer.ATTR.ion_mobility, 
                    IonMobilityAndCCS.IonMobility.Mobility);
                if (skylineVersion < DocumentFormat.TRANSITION_SETTINGS_ION_MOBILITY)
                {
                    var ion_mobility_fragment = IonMobilityAndCCS.IonMobility.Mobility.HasValue ?
                        (double?) (IonMobilityAndCCS.IonMobility.Mobility.Value + (IonMobilityAndCCS.HighEnergyIonMobilityValueOffset ?? 0)) :
                        null;
                    writer.WriteAttributeNullable(DocumentSerializer.ATTR.ion_mobility_fragment, ion_mobility_fragment);
                }
            }
            else
            {
                // Express in terms of mobility and high energy offset
                writer.WriteAttributeNullable(DocumentSerializer.ATTR.ion_mobility, IonMobilityAndCCS.IonMobility.Mobility);
                if ((IonMobilityAndCCS.HighEnergyIonMobilityValueOffset ?? 0) != 0)
                    writer.WriteAttributeNullable(DocumentSerializer.ATTR.ion_mobility_high_energy_offset, IonMobilityAndCCS.HighEnergyIonMobilityValueOffset);
            }
            writer.WriteAttributeNullable(DocumentSerializer.ATTR.ion_mobility_window, IonMobilityExtractionWindowWidth);
            writer.WriteAttributeNullable(DocumentSerializer.ATTR.ccs, CollisionalCrossSectionSqA);
            writer.WriteAttributeString(DocumentSerializer.ATTR.ion_mobility_type, IonMobilityUnits.ToString());
        }

        public static IonMobilityFilter ReadXMLAttributes(XmlReader reader)
        {
            var ionMobilityFilter = EMPTY;
            var driftTime = reader.GetNullableDoubleAttribute(DocumentSerializer.ATTR.drift_time); // Historical format
            var ccs = reader.GetNullableDoubleAttribute(DocumentSerializer.ATTR.ccs);
            if (driftTime.HasValue)
            {
                var driftTimeWindow = reader.GetNullableDoubleAttribute(DocumentSerializer.ATTR.drift_time_window);
                var highEnergyOffset = reader.GetNullableDoubleAttribute(DocumentSerializer.ATTR.ion_mobility_high_energy_offset);
                var im = IonMobilityAndCCS.GetIonMobilityAndCCS(driftTime.Value,
                    eIonMobilityUnits.drift_time_msec, ccs, highEnergyOffset);
                ionMobilityFilter = GetIonMobilityFilter(im , driftTimeWindow);
            }
            else
            {
                var ionMobility = reader.GetNullableDoubleAttribute(DocumentSerializer.ATTR.ion_mobility);
                if (ionMobility.HasValue)
                {
                    var ionMobilityWindow = reader.GetNullableDoubleAttribute(DocumentSerializer.ATTR.ion_mobility_window);
                    string ionMobilityUnitsString = reader.GetAttribute(DocumentSerializer.ATTR.ion_mobility_type);
                    var ionMobilityUnits = string.IsNullOrEmpty(ionMobilityUnitsString) ? eIonMobilityUnits.unknown :
                        SmallMoleculeTransitionListReader.IonMobilityUnitsFromAttributeValue(ionMobilityUnitsString);
                    var highEnergyOffset = reader.GetNullableDoubleAttribute(DocumentSerializer.ATTR.ion_mobility_high_energy_offset);
                    var im = IonMobilityAndCCS.GetIonMobilityAndCCS(IonMobilityValue.GetIonMobilityValue(
                        ionMobility.Value,
                        ionMobilityUnits), ccs, highEnergyOffset);
                    ionMobilityFilter = GetIonMobilityFilter(im, ionMobilityWindow);
                }
            }
            return ionMobilityFilter;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return 0 == CompareTo(obj as IonMobilityFilter);
        }

        public bool Equals(IonMobilityFilter other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(IonMobilityAndCCS, other.IonMobilityAndCCS) && Nullable.Equals(IonMobilityExtractionWindowWidth, other.IonMobilityExtractionWindowWidth);
        }

        public static bool operator ==(IonMobilityFilter left, IonMobilityFilter right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(IonMobilityFilter left, IonMobilityFilter right)
        {
            return !Equals(left, right);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((IonMobilityAndCCS != null ? IonMobilityAndCCS.GetHashCode() : 0) * 397) ^ IonMobilityExtractionWindowWidth.GetHashCode();
            }
        }

        public int CompareTo(object obj)
        {
            var other = obj as IonMobilityFilter;
            if (other == null)
                return 1;
            var val = IonMobilityAndCCS.CompareTo(other.IonMobilityAndCCS);
            if (val != 0)
                return val;
            val = Nullable.Compare(CollisionalCrossSectionSqA, other.CollisionalCrossSectionSqA);
            if (val != 0)
                return val;
            return Nullable.Compare(IonMobilityExtractionWindowWidth, other.IonMobilityExtractionWindowWidth);
        }

        public override string ToString() // For debugging convenience, not user-facing
        {
            return string.Format(@"{0}/w{1:F04}", IonMobilityAndCCS, IonMobilityExtractionWindowWidth );
        }

    }


}
