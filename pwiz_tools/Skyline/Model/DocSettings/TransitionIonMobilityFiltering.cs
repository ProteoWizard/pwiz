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
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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

        public static TransitionIonMobilityFiltering EMPTY = new TransitionIonMobilityFiltering(IonMobility.IonMobilityLibrary.NONE, false, IonMobilityWindowWidthCalculator.EMPTY);

        public static bool IsNullOrEmpty(TransitionIonMobilityFiltering f) { return f == null || f.IsEmpty; }

        public TransitionIonMobilityFiltering(string name, string dbDir, IDictionary<LibKey, List<IonMobilityAndCCS>> dict, bool useSpectralLibraryIonMobilityValues, IonMobilityWindowWidthCalculator filterWindowWidthCalculator)
        {
            IonMobilityLibrary = IonMobility.IonMobilityLibrary.CreateFromDictionary(name, dbDir, dict);
            UseSpectralLibraryIonMobilityValues = useSpectralLibraryIonMobilityValues;
            FilterWindowWidthCalculator = filterWindowWidthCalculator;

            Validate();
        }

        public TransitionIonMobilityFiltering(IonMobilityLibrarySpec ionMobilityLibrary, bool useSpectralLibraryIonMobilityValues, IonMobilityWindowWidthCalculator filterWindowWidthCalculator)
        {
            IonMobilityLibrary = ionMobilityLibrary ?? IonMobility.IonMobilityLibrary.NONE;
            UseSpectralLibraryIonMobilityValues = useSpectralLibraryIonMobilityValues;
            FilterWindowWidthCalculator = filterWindowWidthCalculator;

            Validate();
        }

        public bool IsEmpty { get { return Equals(EMPTY); } }

        [TrackChildren]

        public IonMobilityLibrarySpec IonMobilityLibrary { get; private set; }

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

        public TransitionIonMobilityFiltering ChangeLibrary(IonMobilityLibrarySpec prop)
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

        public List<IonMobilityAndCCS> GetIonMobilityInfo(LibKey peptide,
            IIonMobilityFunctionsProvider ionMobilityFunctionsProvider)
        {
            var ionMobilities = GetIonMobilityInfoFromLibrary(peptide);
            // Convert from CCS to ion mobility if possible
            if (ionMobilityFunctionsProvider != null &&
                ionMobilityFunctionsProvider.ProvidesCollisionalCrossSectionConverter &&
                ionMobilities != null && peptide.PrecursorMz.HasValue)
            {
                var result = new List<IonMobilityAndCCS>();
                foreach (var im in ionMobilities)
                {
                    var updated = im;
                    if (im.CollisionalCrossSectionSqA.HasValue)
                    {
                        var ionMobilityValue = ionMobilityFunctionsProvider.IonMobilityFromCCS(
                            im.CollisionalCrossSectionSqA.Value,
                            peptide.PrecursorMz.Value, peptide.Charge);
                        if (!Equals(ionMobilityValue, im.IonMobility))
                        {
                            updated = IonMobilityAndCCS.GetIonMobilityAndCCS(ionMobilityValue,
                                im.CollisionalCrossSectionSqA, im.HighEnergyIonMobilityOffset);
                        }
                    }

                    result.Add(updated);
                }
                return result;
            }
            else
            {
                return ionMobilities;
            }
        }

        public List<IonMobilityAndCCS> GetIonMobilityInfoFromLibrary(LibKey ion)
        {
            // Locate this target in ion mobility library, if any
            if (IonMobilityLibrary != null && !IonMobilityLibrary.IsNone)
            {
                var dict = IonMobilityLibrary.GetIonMobilityDict();
                if (dict != null && dict.TryGetValue(ion, out var imList))
                {
                    return imList;
                }
            }

            return null;
        }

        /// <summary>
        /// Get the ion mobility(ies) for the charged peptide, and the width of the window
        /// centered on each 
        /// </summary>
        public IonMobilityFilterSet GetIonMobilityInfo(LibKey peptide,
            IIonMobilityFunctionsProvider ionMobilityFunctionsProvider, double ionMobilityRangeMax)
        {
            var ionMobilities = GetIonMobilityInfo(peptide, ionMobilityFunctionsProvider);
            if (ionMobilities != null)
            {
                var result = new List<IonMobilityFilter>();
                foreach (var ionMobility in ionMobilities)
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
                    result.Add( IonMobilityFilter.GetIonMobilityFilter(ionMobility, ionMobilityWindowWidth));
                }

                return IonMobilityFilterSet.GetIonMobilityFilterSet(result);
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
                IonMobilityLibrary = IonMobility.IonMobilityLibrary.NONE;
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
                var imLib = IonMobilityLibrary as IonMobilityLibrary;
                if (imLib != null)
                    writer.WriteElement(imLib);
            }

        }
        #endregion

        public bool Equals(TransitionIonMobilityFiltering other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(IonMobilityLibrary, other.IonMobilityLibrary) && 
                   UseSpectralLibraryIonMobilityValues == other.UseSpectralLibraryIonMobilityValues && 
                   Equals(FilterWindowWidthCalculator, other.FilterWindowWidthCalculator);
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
            spectral_library_drift_times_resolving_power, // Obsolete Pre-20.12  misnomer, used for IMS types other than drift time 
            resolving_power,
            window_width_calc_type,
            peak_width_calc_type, // Obsolete Pre-20.12
            spectral_library_drift_times_peak_width_calc_type, // Obsolete Pre-20.12  misnomer, used for IMS types other than drift time
            width_at_dt_zero, // Obsolete Pre-20.12 misnomer, used for IMS types other than drift time
            width_at_ion_mobility_zero,
            width_at_dt_max, // Obsolete Pre-20.12 misnomer, used for IMS types other than drift time
            width_at_ion_mobility_max,
            fixed_width
        }

        public static readonly IonMobilityWindowWidthCalculator EMPTY =
            new IonMobilityWindowWidthCalculator(IonMobilityWindowWidthType.none, 0, 0, 0, 0);

        private static string AttrPrefix(bool isSpectralLibrary) { return isSpectralLibrary ? @"spectral_library_" : string.Empty; }

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
        public IonMobilityWindowWidthCalculator(XmlReader reader, bool isPreVersion_20_12, bool isSpectralLibrary) : // Before v19.2 this was in Peptide settings, and used overly specific "drift time" terms for ion mobility
            this(
                reader.GetEnumAttribute(IonMobilityPeakWidthCalcTypeAttr(isPreVersion_20_12, isSpectralLibrary), IonMobilityWindowWidthType.none),
                reader.GetDoubleAttribute(IonMobilityPeakWidthResolvingPowerAttr(isPreVersion_20_12, isSpectralLibrary), 0),
                reader.GetDoubleAttribute(AttrPrefix(isSpectralLibrary) + (isPreVersion_20_12 ? ATTR.width_at_dt_zero : ATTR.width_at_ion_mobility_zero), 0),
                reader.GetDoubleAttribute(AttrPrefix(isSpectralLibrary) + (isPreVersion_20_12 ? ATTR.width_at_dt_max : ATTR.width_at_ion_mobility_max), 0),
                reader.GetDoubleAttribute(ATTR.fixed_width, 0))
        {
            // Check to see if this was an old-style serialization of an empty object
            if (isPreVersion_20_12 && isSpectralLibrary && WindowWidthMode==IonMobilityWindowWidthType.resolving_power)
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

        private static string IonMobilityPeakWidthCalcTypeAttr(bool isPre20_12, bool isSpectralLibrary)
        {
            return isPre20_12 ?
                (isSpectralLibrary ? ATTR.spectral_library_drift_times_peak_width_calc_type : ATTR.peak_width_calc_type).ToString() :
                ATTR.window_width_calc_type.ToString();
        }

        private static string IonMobilityPeakWidthResolvingPowerAttr(bool isPre20_12, bool isSpectralLibrary)
        {
            return isPre20_12 ?
                (isSpectralLibrary ? ATTR.spectral_library_drift_times_resolving_power : ATTR.resolving_power).ToString() :
                ATTR.resolving_power.ToString();
        }

        public void WriteXML(XmlWriter writer,
            bool isPre20_12, // For serializing to old-style documents
            bool isSpectralLibrary) // For serializing to old-style documents
        {
            if (WindowWidthMode != IonMobilityWindowWidthType.none) // Don't serialize if empty
            {
                writer.WriteAttribute(IonMobilityPeakWidthCalcTypeAttr(isPre20_12, isSpectralLibrary), WindowWidthMode);
                // Only write attributes that make sense for current width calculation mode, or that are nonzero (and might be useful later)
                if (WindowWidthMode == IonMobilityWindowWidthType.resolving_power || ResolvingPower != 0)
                    writer.WriteAttribute(IonMobilityPeakWidthResolvingPowerAttr(isPre20_12, isSpectralLibrary), ResolvingPower);
                if (WindowWidthMode == IonMobilityWindowWidthType.linear_range || PeakWidthAtIonMobilityValueZero != 0 || PeakWidthAtIonMobilityValueMax != 0)
                {
                    writer.WriteAttribute(isPre20_12 ? (AttrPrefix(isSpectralLibrary) + ATTR.width_at_dt_zero) : ATTR.width_at_ion_mobility_zero.ToString(), PeakWidthAtIonMobilityValueZero);
                    writer.WriteAttribute(isPre20_12 ? AttrPrefix(isSpectralLibrary) + ATTR.width_at_dt_max : ATTR.width_at_ion_mobility_max.ToString(), PeakWidthAtIonMobilityValueMax);
                }
                if (!isPre20_12 && (WindowWidthMode == IonMobilityWindowWidthType.fixed_width || FixedWindowWidth != 0))
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
                    if (ResolvingPower <= 0)
                        return Resources.DriftTimePredictor_Validate_Resolving_power_must_be_greater_than_0_;
                    break;
                case IonMobilityWindowWidthType.linear_range:
                    if (PeakWidthAtIonMobilityValueZero < 0 ||
                        PeakWidthAtIonMobilityValueMax < PeakWidthAtIonMobilityValueZero)
                        return Resources.DriftTimeWindowWidthCalculator_Validate_Peak_width_must_be_non_negative_;
                    break;
                case IonMobilityWindowWidthType.fixed_width:
                    if (FixedWindowWidth < 0)
                        return Resources.DriftTimeWindowWidthCalculator_Validate_Fixed_window_width_must_be_non_negative_;
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
                writer.WriteAttributeNullable(ATTR.ion_mobility, IonMobilityInfo.IonMobility.Mobility);
                if ((IonMobilityInfo.HighEnergyIonMobilityOffset??0) != 0)
                    writer.WriteAttribute(ATTR.high_energy_ion_mobility_offset, IonMobilityInfo.HighEnergyIonMobilityOffset);
                writer.WriteAttributeNullable(ATTR.ccs, IonMobilityInfo.CollisionalCrossSectionSqA);
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

        IonMobilityAndCCS GetLibraryMeasuredIonMobilityAndHighEnergyOffset(LibKey peptide, double mz, IIonMobilityFunctionsProvider instrumentInfo);

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
            double? highEnergyIonMobilityOffset)
        {
            IonMobility = ionMobility;
            CollisionalCrossSectionSqA = collisionalCrossSectionSqA;
            HighEnergyIonMobilityOffset = highEnergyIonMobilityOffset;
        }

        public static IonMobilityAndCCS GetIonMobilityAndCCS(double? ionMobilityValue, eIonMobilityUnits units, double? collisionalCrossSectionSqA, double? highEnergyIonMobilityValueOffset)
        {
            return GetIonMobilityAndCCS(IonMobilityValue.GetIonMobilityValue(ionMobilityValue, units), collisionalCrossSectionSqA, highEnergyIonMobilityValueOffset);
        }

        public static IonMobilityAndCCS GetIonMobilityAndCCS(IonMobilityValue ionMobilityValue, double? collisionalCrossSectionSqA, double? highEnergyIonMobilityValueOffset)
        {
            return ionMobilityValue.HasValue || collisionalCrossSectionSqA.HasValue ? new IonMobilityAndCCS(ionMobilityValue, collisionalCrossSectionSqA, highEnergyIonMobilityValueOffset) : EMPTY;
        }

        [Track]
        public string Units
        {
            get { return IonMobilityFilter.IonMobilityUnitsL10NString(IonMobility.Units); }
        }

        [TrackChildren(ignoreName: true)]
        public IonMobilityValue IonMobility { get; private set; }
        [Track]
        public double? CollisionalCrossSectionSqA { get; private set; }
        [Track]
        public double? HighEnergyIonMobilityOffset { get; private set; } // As in Waters MSe, where product ions fly a bit faster due to added kinetic energy

        public double? GetHighEnergyIonMobility()
        {
            if (IonMobility.HasValue)
            {
                return IonMobility.Mobility + HighEnergyIonMobilityOffset;
            }
            return null;
        }

        /// <summary>
        /// Merge non-empty parts of other into a copy of this
        /// </summary>
        public IonMobilityAndCCS Merge(IonMobilityAndCCS other)
        {
            var val = this;
            if (other.HasCollisionalCrossSection && !Equals(other.CollisionalCrossSectionSqA, CollisionalCrossSectionSqA))
                val = ChangeProp(ImClone(this), im => im.CollisionalCrossSectionSqA = other.CollisionalCrossSectionSqA);
            if (!Equals(other.IonMobility, IonMobility))
                val = ChangeProp(ImClone(this), im => im.IonMobility = IonMobility.Merge(other.IonMobility));
            if (other.HighEnergyIonMobilityOffset.HasValue && other.HighEnergyIonMobilityOffset != HighEnergyIonMobilityOffset)
                val = ChangeProp(ImClone(this), im => im.HighEnergyIonMobilityOffset = other.HighEnergyIonMobilityOffset);

            return val;
        }

        public bool HasCollisionalCrossSection { get { return (CollisionalCrossSectionSqA ?? 0) != 0; } }
        public bool HasIonMobilityValue { get { return IonMobility.HasValue; } }
        public bool IsEmpty { get { return !HasIonMobilityValue && !HasCollisionalCrossSection; } }
        public static bool IsNullOrEmpty(IonMobilityAndCCS val) {  return val == null || val.IsEmpty; }

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
                hashCode = (hashCode * 397) ^ HighEnergyIonMobilityOffset.GetHashCode();
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
            var diff = HighEnergyIonMobilityOffset - other.HighEnergyIonMobilityOffset;
            if (diff > 0)
                return 1;
            else if (diff < 0)
                return -1;
            return 0;
        }

        public override string ToString() // For debug convenience
        {
            return string.Format(@"ccs{0}/{1}/he{2}", CollisionalCrossSectionSqA, IonMobility, HighEnergyIonMobilityOffset);
        }
    }

    /// <summary>
    /// Collection that helps support multiple conformers (ions with more than one possible CCS value)
    /// </summary>
    public class IonMobilityFilterSet : Immutable, IComparable<IonMobilityFilterSet>, IEquatable<IonMobilityFilterSet>, IEnumerable<IonMobilityFilter>
    {
        public static IonMobilityFilterSet EMPTY = new IonMobilityFilterSet(new HashSet<IonMobilityFilter>());

        private HashSet<IonMobilityFilter> _filters;

        public static bool IsNullOrEmpty(IonMobilityFilterSet filterSet)
        {
            return filterSet == null || filterSet.IsEmpty;
        }

        private IonMobilityFilterSet(HashSet<IonMobilityFilter> filters) // Use GetIonMobilityFilterSet() instead, for efficiency with empty lists
        {
            _filters = filters;
        }

        public static IonMobilityFilterSet GetIonMobilityFilterSet(IonMobilityFilter filter)
        {
            if (IonMobilityFilter.IsNullOrEmpty(filter))
                return EMPTY;
            return new IonMobilityFilterSet(new HashSet<IonMobilityFilter>(){ filter});
        }

        public static IonMobilityFilterSet GetIonMobilityFilterSet(IEnumerable<IonMobilityFilter> filters)
        {
            if (filters == null)
                return EMPTY;
            var result = filters.Where(f => f != null && !f.IsEmpty).ToHashSet();
            return !result.Any() ? EMPTY : new IonMobilityFilterSet(result);
        }

        public IonMobilityFilterSet Add(IonMobilityFilterSet other)
        {
            if (IsNullOrEmpty(other) || other._filters.All(f => _filters.Contains(f)))
                return this;
            var result = new HashSet<IonMobilityFilter>(_filters);
            foreach (var f in other._filters)
            {
                result.Add(f);
            }
            return new IonMobilityFilterSet(result);
        }

        public IonMobilityFilterSet Merge(IonMobilityFilterSet ionMobilities, bool isMs1)
        {
            if (Count == 0)
            {
                return ionMobilities;
            }

            var result = ionMobilities._filters;
            foreach (var ionMobility in ionMobilities)
            {
                // Look for entries with same CCS but otherwise differing values, such as after a settings change that affects window size
                var existing = _filters.FirstOrDefault(f => 
                    (f.CollisionalCrossSectionSqA.HasValue && Equals(f.CollisionalCrossSectionSqA, ionMobility.CollisionalCrossSectionSqA)) ||
                    (f.HasIonMobilityValue && Equals(f.IonMobilityAndCCS.IonMobility, ionMobility.IonMobilityAndCCS.IonMobility)));
                if (existing == null && Count == 1)
                {
                    existing = _filters.First();
                }
                if (existing == null)
                {
                    result.Add(ionMobility);
                    continue;
                }

                var updated = existing.Merge(ionMobility);
                if (!Equals(existing, updated))
                {
                    result = result.Where(f => !Equals(f, existing)).ToHashSet();
                    result.Add(updated);
                }
            }
            return Equals(result, _filters) ? this : new IonMobilityFilterSet(result);
        }


        public bool Equals(IonMobilityFilterSet other)
        {
            return !ReferenceEquals(null, other) && 
                   (ReferenceEquals(this, other) || _filters.SetEquals(other._filters));
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((IonMobilityFilterSet)obj);
        }

        public override int GetHashCode()
        {
            return (_filters != null ? _filters.GetHashCode() : 0);
        }

        public static bool operator ==(IonMobilityFilterSet left, IonMobilityFilterSet right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(IonMobilityFilterSet left, IonMobilityFilterSet right)
        {
            return !Equals(left, right);
        }

        public bool IsEmpty
        {
            get { return _filters == null || _filters.All(im => im.IsEmpty); }
        }

        public int Count => IsEmpty ? 0 : _filters.Count;

        public bool ContainsIonMobility(IonMobilityValue ionMobility, bool useHighEnergyOffset)
        {
            if (IsEmpty || IonMobilityValue.IsNullOrEmpty(ionMobility))
                return true; // It doesn't NOT include it
            return _filters.Any(f => f.ContainsIonMobility(ionMobility, useHighEnergyOffset));
        }

        public void GetIonMobilityRange(bool useHighEnergyOffset, out double? minIonMobility, out double? maxIonMobility)
        {
            var minIonMobilityFound = double.MaxValue;
            var maxIonMobilityFound = double.MinValue;
            foreach (var im in _filters.Where(i => !IonMobilityFilter.IsNullOrEmpty(i)))
            {
                var halfWin = (im.IonMobilityExtractionWindowWidth ?? 0) / 2;
                var center = im.IonMobilityAndCCS.IonMobility.Mobility.Value + 
                             (useHighEnergyOffset ? im.HighEnergyIonMobilityOffset ?? 0 : 0);
                minIonMobilityFound = Math.Min(minIonMobilityFound, center - halfWin);
                maxIonMobilityFound = Math.Max(maxIonMobilityFound, center + halfWin);
            }

            if (minIonMobilityFound > maxIonMobilityFound)
            {
                minIonMobility = maxIonMobility = null;
            }
            else
            {
                minIonMobility = minIonMobilityFound;
                maxIonMobility = maxIonMobilityFound;
            }
        }

        public SkylineDocumentProto.Types.IonMobilityFilters ToProtoIonMobilityFilters()
        {
            if (IsEmpty)
            {
                return null;
            }
            SkylineDocumentProto.Types.IonMobilityFilters protoFilters = new SkylineDocumentProto.Types.IonMobilityFilters();
            foreach (var filter in _filters)
            {
                protoFilters.Values.Add(new SkylineDocumentProto.Types.IonMobilityFilter()
                {
                    IonMobility = DataValues.ToOptional(filter.IonMobilityAndCCS.IonMobility.Mobility),
                    IonMobilityWindow = DataValues.ToOptional(filter.IonMobilityExtractionWindowWidth)
                });
            }
            return protoFilters;
        }

        public static IonMobilityFilterSet FromProtoIonMobilityFilters(SkylineDocumentProto.Types.IonMobilityFilters protoIonMobilityFilters,
            eIonMobilityUnits units)
        {
            if (protoIonMobilityFilters == null || protoIonMobilityFilters.Values.Count == 0)
            {
                return EMPTY;
            }

            var result = new HashSet<IonMobilityFilter>();
            foreach (var value in protoIonMobilityFilters.Values)
            {
                var im = IonMobilityAndCCS.GetIonMobilityAndCCS(
                    IonMobilityValue.GetIonMobilityValue(value.IonMobility.Value, units), null, null);
                result.Add(IonMobilityFilter.GetIonMobilityFilter(im, value.IonMobilityWindow.Value));
            }
            return result.Any(f => !IonMobilityFilter.IsNullOrEmpty(f)) ? new IonMobilityFilterSet(result) : EMPTY;
        }

        public IonMobilityFilterSet ApplyOffset()
        {
            if (_filters.All(item => (item.HighEnergyIonMobilityOffset ?? 0) == 0))
                return this;
            var result = new HashSet<IonMobilityFilter>();
            foreach (var item in _filters)
            {
                result.Add(item.ApplyOffset());
            }

            return new IonMobilityFilterSet(result);
        }


        public void WriteXML(XmlWriter writer, IonMobilityFilter.SerializationElementType parentElementType)
        {
            if (!IsEmpty)
            {
                foreach (var filter in _filters.Where(f => !IonMobilityFilter.IsNullOrEmpty(f)))
                {
                    writer.WriteStartElement(DocumentSerializer.EL.ion_mobility_filter);
                    filter.WriteAttributes(writer, parentElementType);
                    writer.WriteEndElement();
                }
            }
        }


        public static IonMobilityFilterSet ReadXML(XmlReader reader)
        {
            var list = new List<IonMobilityFilter>();
            while (reader.IsStartElement(DocumentSerializer.EL.ion_mobility_filter))
            {
                var filter = IonMobilityFilter.ReadXMLAttributes(reader);
                if (!IonMobilityFilter.IsNullOrEmpty(filter))
                    list.Add(filter);
                reader.ReadElementString(); // Advance the reader
            }
            return GetIonMobilityFilterSet(list);
        }


        Tuple<double, double> GetRange(bool isHighEnergy)
        {
            double minVal = double.MaxValue;
            double maxVal = double.MinValue;
            foreach (var im in _filters)
            {
                if (im.HasIonMobilityValue)
                {
                    var halfWin = (im.IonMobilityExtractionWindowWidth ?? 0) / 2;
                    minVal = Math.Min(minVal, (im.IonMobilityAndCCS.IonMobility.Mobility ?? 0) - halfWin);
                    maxVal = Math.Min(maxVal, (im.IonMobilityAndCCS.IonMobility.Mobility ?? 0) + halfWin);
                }
            }
            return new Tuple<double, double>(minVal, maxVal);
        }

        public int CompareTo(IonMobilityFilterSet other)
        {
            if (Equals(other, null))
                return 1;
            var otherList = other._filters.ToList();
            var thisList = _filters.ToList();
            for (var i = 0; i < thisList.Count; i++)
            {
                if (i >= otherList.Count)
                    return 1;
                var result = thisList[i].CompareTo(otherList[i]);
                if (result != 0)
                    return result;
            }

            return 0;
        }

        public IEnumerator<IonMobilityFilter> GetEnumerator()
        {
            return ((IEnumerable<IonMobilityFilter>)_filters).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<IonMobilityFilter>)_filters).GetEnumerator();
        }

        public override string ToString() // For debugging convenience
        {
            return IsEmpty ? @"empty" :  string.Join(@",", _filters.Select(f => f.ToString()));
        }
    }

    /// <summary>
    /// Contains the ion mobility and window used to filter scans
    /// </summary>
    public class IonMobilityFilter : Immutable, IComparable, IEquatable<IonMobilityFilter>
    {
        public static readonly IonMobilityFilter EMPTY = new IonMobilityFilter(IonMobilityAndCCS.EMPTY, null);

        public static bool IsNullOrEmpty(IonMobilityFilter filter)
        {
            return filter == null || filter.IsEmpty;
        }

        public static IonMobilityFilter GetIonMobilityFilter(IonMobilityAndCCS ionMobility,
            double? ionMobilityExtractionWindowWidth)
        {
            if (ionMobility.IsEmpty
                && !ionMobilityExtractionWindowWidth.HasValue)
            {
                return EMPTY;
            }
            return new IonMobilityFilter(ionMobility,
                ionMobilityExtractionWindowWidth);
        }

        // Returns an updated copy of itself with high energy offset applied to mobility value, then zeroed out
        public IonMobilityFilter ApplyOffset()
        {
            if ((HighEnergyIonMobilityOffset??0) == 0 || !IonMobilityAndCCS.IonMobility.HasValue)
                return this;
            return GetIonMobilityFilter(
                IonMobilityAndCCS.GetIonMobilityAndCCS(
                IonMobilityAndCCS.IonMobility.ChangeIonMobility(IonMobilityAndCCS.IonMobility.Mobility.Value + HighEnergyIonMobilityOffset.Value), 
                IonMobilityAndCCS.CollisionalCrossSectionSqA, null),
                IonMobilityExtractionWindowWidth);
        }

        private IonMobilityFilter(IonMobilityAndCCS ionMobilityAndCCS,
            double? ionMobilityExtractionWindowWidth)
        {
            IonMobilityAndCCS = ionMobilityAndCCS;
            IonMobilityExtractionWindowWidth = ionMobilityExtractionWindowWidth;
        }
        public IonMobilityAndCCS IonMobilityAndCCS { get; private set; }
        public double? CollisionalCrossSectionSqA => IonMobilityAndCCS.CollisionalCrossSectionSqA; // The CCS value used to get the ion mobility, if known
        public double? IonMobilityExtractionWindowWidth { get; private set; }
        public double? HighEnergyIonMobilityOffset => IonMobilityAndCCS.HighEnergyIonMobilityOffset; // As in Waters MsE, where ions move a bit faster due to more energetic collision in the high energy part of the cycle
        public eIonMobilityUnits IonMobilityUnits { get { return HasIonMobilityValue ? IonMobilityAndCCS.IonMobility.Units : eIonMobilityUnits.none; } }

        public bool HasIonMobilityValue { get { return IonMobilityAndCCS.HasIonMobilityValue; } }
        public bool IsEmpty { get { return !HasIonMobilityValue; } }

        /// <summary>
        /// Populate values in this with non-empty values in other
        /// </summary>
        public IonMobilityFilter Merge(IonMobilityFilter other)
        {

            var val = Equals(other.IonMobilityExtractionWindowWidth, IonMobilityExtractionWindowWidth) ? 
                this : 
                ChangeProp(ImClone(this), im => im.IonMobilityExtractionWindowWidth = other.IonMobilityExtractionWindowWidth);

            var ionMobilityAndCCS = val.IonMobilityAndCCS.Merge(other.IonMobilityAndCCS);
            val = Equals(ionMobilityAndCCS, IonMobilityAndCCS) ? 
                val : 
                ChangeProp(ImClone(val), im => im.IonMobilityAndCCS = ionMobilityAndCCS);

            return val;
        }


        public bool ContainsIonMobility(IonMobilityValue val, bool useHighEnergyOffset)
        {
            if (IsNullOrEmpty(this) || val == null || ! val.HasValue)
                return true; // It doesn't NOT include it
            var im = useHighEnergyOffset ? val.Mobility.Value - HighEnergyIonMobilityOffset : val.Mobility.Value;
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
                    return Resources.IonMobilityFilter_IonMobilityUnitsString__1_K0__Vs_cm_2_;
                case eIonMobilityUnits.drift_time_msec:
                    return Resources.IonMobilityFilter_IonMobilityUnitsString_Drift_Time__ms_;
                case eIonMobilityUnits.compensation_V:
                    return Resources.IonMobilityFilter_IonMobilityUnitsString_Compensation_Voltage__V_;
                case eIonMobilityUnits.none:
                    return Resources.IonMobilityFilter_IonMobilityUnitsL10NString_None;
                default:
                    return null;
            }
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
                foreach (var culture in new[] {@"en", @"zh-CHS", @"ja"})
                {
                    var tryCulture = new CultureInfo(culture);
                    Thread.CurrentThread.CurrentUICulture = tryCulture;
                    foreach (eIonMobilityUnits u in Enum.GetValues(typeof(eIonMobilityUnits)))
                    {
                        var ionMobilityUnitsL10NString = IonMobilityUnitsL10NString(u);
                        if (string.Equals(units, ionMobilityUnitsL10NString, StringComparison.OrdinalIgnoreCase))
                        {
                            result = u;
                            success = true;
                            break;
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

        public enum SerializationElementType { TransitionChromInfo, TransitionGroupChromInfo, LegacyMeasuredIonMObility }
        public void WriteAttributes(XmlWriter writer, SerializationElementType serializationElementType)
        {
            if (IsEmpty)
                return;

            if (serializationElementType == SerializationElementType.TransitionGroupChromInfo)
            {
                // Express in terms of ms1 vs fragment
                writer.WriteAttributeNullable(DocumentSerializer.ATTR.ion_mobility_ms1, IonMobilityAndCCS.IonMobility.Mobility);
                var ion_mobility_fragment = IonMobilityAndCCS.IonMobility.Mobility.HasValue ?
                    (double?) (IonMobilityAndCCS.IonMobility.Mobility.Value + (IonMobilityAndCCS.HighEnergyIonMobilityOffset ?? 0)) :
                    null;
                writer.WriteAttributeNullable(DocumentSerializer.ATTR.ion_mobility_fragment, ion_mobility_fragment);
            }
            else
            {
                // Express in terms of mobility and high energy offset
                writer.WriteAttributeNullable(DocumentSerializer.ATTR.ion_mobility, IonMobilityAndCCS.IonMobility.Mobility);
                if ((IonMobilityAndCCS.HighEnergyIonMobilityOffset ?? 0) != 0)
                    writer.WriteAttributeNullable(DocumentSerializer.ATTR.ion_mobility_high_energy_offset, IonMobilityAndCCS.HighEnergyIonMobilityOffset);
            }
            writer.WriteAttributeNullable(DocumentSerializer.ATTR.ion_mobility_window, IonMobilityExtractionWindowWidth);
            if (serializationElementType != SerializationElementType.TransitionChromInfo)
            {
                writer.WriteAttributeNullable(DocumentSerializer.ATTR.ccs, CollisionalCrossSectionSqA);
                writer.WriteAttributeString(DocumentSerializer.ATTR.ion_mobility_type, IonMobilityUnits.ToString());
            }
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
                    var ionMobilityUnits = SmallMoleculeTransitionListReader.IonMobilityUnitsFromAttributeValue(ionMobilityUnitsString);
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

    public interface IIonMobilityLibrary
    {
        string Name { get; }
        IList<IonMobilityAndCCS> GetIonMobilityInfo(LibKey chargedPeptide); // An ion may have multiple conformers (CCS values)
    }

    public abstract class IonMobilityLibrarySpec : XmlNamedElement, IIonMobilityLibrary
    {
        protected IonMobilityLibrarySpec(string name)
            : base(name)
        {
        }

        /// <summary>
        /// Get the ion mobility(ies) for the charged molecule.
        /// </summary>
        /// <param name="chargedPeptide"></param>
        /// <returns>ion mobility, or null</returns>
        public abstract IList<IonMobilityAndCCS> GetIonMobilityInfo(LibKey chargedPeptide);
        public abstract ImmutableDictionary<LibKey, List<IonMobilityAndCCS>> GetIonMobilityDict();

        public virtual int Count { get { return -1; } }  // How many entries in library?

        public virtual bool IsUsable { get { return true; } }

        public virtual bool IsNone { get { return false; } }

        public virtual IonMobilityLibrarySpec Initialize(IProgressMonitor loadMonitor)
        {
            return this;
        }

        public virtual string PersistencePath { get { return null; } }

        public virtual string PersistMinimized(string pathDestDir, SrmDocument document, IDictionary<LibKey, LibKey> smallMoleculeConversionInfo)
        {
            return null;
        }

        /// <summary>
        /// For serialization
        /// </summary>
        public IonMobilityLibrarySpec()
        {
        }

        // Special XML writing capability for backward compatibility
        public virtual void WriteXml(XmlWriter writer, IonMobilityWindowWidthCalculator extraInfoForPre20_12)
        {
        }

    }



}
