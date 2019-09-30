/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2019 University of Washington - Seattle, WA
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
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Serialization;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.DocSettings
{
    /// <summary>
    /// Describes a dictionary of molecules+adducts and their associated ion mobility (measured and CCS),
    /// and a means of calculating filter window widths for any given member of the dictionary.
    /// </summary>
    [XmlRoot("ion_mobility_calibration")]
    public class IonMobilityCalibration : XmlNamedElement, IAuditLogComparable
    {
        public static double? GetIonMobilityDisplay(double? ionMobility)
        {
            if (!ionMobility.HasValue)
                return null;
            return Math.Round(ionMobility.Value, 4);
        }

        private LibKeyMap<IonMobilityAndCCS> _measuredMobilityIons;

        public IonMobilityCalibration(string name,
            IDictionary<LibKey, IonMobilityAndCCS> measuredMobilityIons,
            IonMobilityWindowWidthCalculator.IonMobilityPeakWidthType peakWidthMode,
            double resolvingPower,
            double widthAtIonMobilityZero, double widthAtIonMobilityMax)
            : base(name)
        {
            WindowWidthCalculator = new IonMobilityWindowWidthCalculator(peakWidthMode,
                resolvingPower, widthAtIonMobilityZero, widthAtIonMobilityMax);
            MeasuredMobilityIons = measuredMobilityIons ?? new Dictionary<LibKey, IonMobilityAndCCS>();
            Validate();
        }

        public static readonly IonMobilityCalibration EMPTY = new IonMobilityCalibration(@"empty",
           null, IonMobilityWindowWidthCalculator.IonMobilityPeakWidthType.resolving_power, 0, 0, 0);

        public bool IsEmpty
        {
            get { return Equals(EMPTY); }
        }


        [TrackChildren(ignoreName: true)] public IonMobilityWindowWidthCalculator WindowWidthCalculator { get; set; }

        public IonMobilityAndCCS GetMeasuredIonMobility(LibKey chargedPeptide)
        {
            if (MeasuredMobilityIons != null)
            {
                IonMobilityAndCCS im;
                if (MeasuredMobilityIons.TryGetValue(chargedPeptide, out im))
                    return im;
            }

            return IonMobilityAndCCS.EMPTY;
        }

        /// <summary>
        /// Helper class for displaying instances of this class
        /// </summary>
        public class IonMobilityCalibrationItem
        {
            public IonMobilityCalibrationItem(LibKey libKey, IonMobilityAndCCS value)
            {
                Target = libKey.Target.AuditLogText;
                Adduct = libKey.Adduct;
                Value = value;
            }

            [Track]
            public string Target { get; set; } // Human readable representation of peptide or molecule

            [Track(customLocalizer: typeof(AdductLocalizer))]
            public Adduct Adduct { get; set; }

            [TrackChildren(ignoreName: true)] public IonMobilityAndCCS Value { get; set; }
        }

        public class AdductLocalizer : CustomPropertyLocalizer
        {
            private static readonly string CHARGE = @"Charge";
            private static readonly string ADDUCT = @"Adduct";

            public override string[] PossibleResourceNames
            {
                get { return new[] {CHARGE, ADDUCT}; }
            }

            protected override string Localize(ObjectPair<object> objectPair)
            {
                var newAdduct = (Adduct) objectPair.NewObject;

                return newAdduct.IsProteomic
                    ? CHARGE
                    : ADDUCT;
            }

            public AdductLocalizer() : base(PropertyPath.Parse(@"Adduct"), true)
            {
            }
        }

        [TrackChildren]
        public IDictionary<LibKey, IonMobilityCalibrationItem> IonMobilityCalibrationItems
        {
            get
            {
                if (MeasuredMobilityIons == null)
                    return new Dictionary<LibKey, IonMobilityCalibrationItem>();

                return new SortedDictionary<LibKey, IonMobilityCalibrationItem>(MeasuredMobilityIons.ToDictionary(pair => pair.Key,
                    pair => new IonMobilityCalibrationItem(pair.Key, pair.Value)), Comparer<LibKey>.Create(CompareLibKeys));
            }
        }

        private int CompareLibKeys(LibKey x, LibKey y)
        {
            return (x.Target, x.Adduct).CompareTo((y.Target, y.Adduct));
        }

        public IDictionary<LibKey, IonMobilityAndCCS> MeasuredMobilityIons
        {
            get { return _measuredMobilityIons == null ? null : _measuredMobilityIons.AsDictionary(); }
            private set { _measuredMobilityIons = LibKeyMap<IonMobilityAndCCS>.FromDictionary(value); }
        }

        public eIonMobilityUnits GetIonMobilityUnits()
        {
            foreach (eIonMobilityUnits units in Enum.GetValues(typeof(eIonMobilityUnits)))
            {
                if (units != eIonMobilityUnits.none && IsUsable(units))
                {
                    return units;
                }
            }

            return eIonMobilityUnits.none;
        }

        public bool IsUsable(eIonMobilityUnits units)
        {
            // We're usable if we have measured ion mobility values
            bool usable = (_measuredMobilityIons != null) &&
                          _measuredMobilityIons.Any(m => m.IonMobility.Units == units);
            return usable;
        }

        #region Property change methods

        public IonMobilityCalibration ChangeMeasuredIonMobilityValuesFromResults(SrmDocument document,
            string documentFilePath, bool useHighEnergyOffset, IProgressMonitor progressMonitor = null)
        {
            // Overwrite any existing measurements with newly derived ones
            Dictionary<LibKey, IonMobilityAndCCS> measured;
            using (var finder = new IonMobilityFinder(document, documentFilePath, progressMonitor)
                {UseHighEnergyOffset = useHighEnergyOffset})
            {
                measured = finder.FindIonMobilityPeaks(); // Returns null on cancel
            }

            return OverrideMeasuredIonMobilityValues(measured);
        }

        public IonMobilityCalibration ChangeMeasuredIonMobilityValues(IDictionary<LibKey, IonMobilityAndCCS> measured)
        {
            if (measured != null &&
                (MeasuredMobilityIons == null || !ArrayUtil.EqualsDeep(MeasuredMobilityIons, measured)))
                return ChangeProp(ImClone(this), im => im.MeasuredMobilityIons = measured);
            return this;
        }

        public IonMobilityCalibration OverrideMeasuredIonMobilityValues(
            IDictionary<LibKey, IonMobilityAndCCS> newValues)
        {
            if (newValues == null || newValues.Count == 0)
            {
                return this;
            }

            var newLibKeyMap = LibKeyMap<IonMobilityAndCCS>.FromDictionary(newValues);
            if (_measuredMobilityIons != null && _measuredMobilityIons.Count != 0)
            {
                newLibKeyMap = _measuredMobilityIons.OverrideWith(newLibKeyMap);
            }

            return ChangeProp(ImClone(this), im => im._measuredMobilityIons = newLibKeyMap);
        }

        public IonMobilityCalibration ChangeDriftTimeWindowWidthCalculator(IonMobilityWindowWidthCalculator prop)
        {
            return ChangeProp(ImClone(this), im => im.WindowWidthCalculator = prop);
        }

        public IonMobilityCalibration ChangeMeasuredMobilityIons(IDictionary<LibKey, IonMobilityAndCCS> prop)
        {
            return ChangeProp(ImClone(this), im => im.MeasuredMobilityIons = prop);
        }

        #endregion

        public IonMobilityAndCCS GetIonMobilityInfo(LibKey peptide,
            IIonMobilityFunctionsProvider ionMobilityFunctionsProvider)
        {
            var ionMobility = GetIonMobilityInfo(peptide);
            // Convert from CCS to ion mobility if possible
            if (ionMobilityFunctionsProvider != null &&
                ionMobilityFunctionsProvider.ProvidesCollisionalCrossSectionConverter &&
                ionMobility != null && ionMobility.HasCollisionalCrossSection && peptide.PrecursorMz.HasValue)
            {
                var ionMobilityValue = ionMobilityFunctionsProvider.IonMobilityFromCCS(
                    ionMobility.CollisionalCrossSectionSqA.Value,
                    peptide.PrecursorMz.Value, peptide.Charge);
                ionMobility = IonMobilityAndCCS.GetIonMobilityAndCCS(ionMobilityValue,
                    ionMobility.CollisionalCrossSectionSqA, ionMobility.HighEnergyIonMobilityValueOffset);
            }

            return ionMobility;
        }

        public IonMobilityAndCCS GetIonMobilityInfo(LibKey peptide)
        {
            // Do we see this in our list of observed ion mobilities?
            IonMobilityAndCCS ionMobility = null;
            if (MeasuredMobilityIons != null)
            {
                MeasuredMobilityIons.TryGetValue(peptide, out ionMobility);
            }

            return ionMobility;
        }

        /// <summary>
        /// Get the ion mobility for the charged peptide, and the width of the window
        /// centered on that based on the predictor's claimed resolving power
        /// </summary>
        public IonMobilityAndCCS GetIonMobilityInfo(LibKey peptide,
            IIonMobilityFunctionsProvider ionMobilityFunctionsProvider, double ionMobilityRangeMax,
            out double ionMobilityWindowWidth)
        {
            IonMobilityAndCCS ionMobility = GetIonMobilityInfo(peptide, ionMobilityFunctionsProvider);
            if (ionMobility != null && ionMobility.IonMobility.HasValue)
            {
                ionMobilityWindowWidth =
                    WindowWidthCalculator.WidthAt(ionMobility.IonMobility.Mobility.Value, ionMobilityRangeMax);
            }
            else
            {
                ionMobilityWindowWidth = 0;
            }

            return ionMobility;
        }

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private IonMobilityCalibration()
        {
            WindowWidthCalculator = IonMobilityWindowWidthCalculator.EMPTY;
            MeasuredMobilityIons = new Dictionary<LibKey, IonMobilityAndCCS>();
        }

        private void Validate()
        {
            // This is active if measured ion mobilities are provided
            if (MeasuredMobilityIons != null && MeasuredMobilityIons.Any())
            {
                var messages = new List<string>();
                var msg = WindowWidthCalculator.Validate();
                if (msg != null)
                    messages.Add(msg);
                if (messages.Any())
                    throw new InvalidDataException(TextUtil.LineSeparate(messages));
            }
        }

        public static IonMobilityCalibration Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new IonMobilityCalibration());
        }

        public override void ReadXml(XmlReader reader)
        {
            var name = reader.Name;
            // Read start tag attributes
            base.ReadXml(reader);
            WindowWidthCalculator = new IonMobilityWindowWidthCalculator(reader, false, false);

            // Consume start tag
            reader.ReadStartElement();

            // Read all measured ion mobilities
            var dict = new Dictionary<LibKey, IonMobilityAndCCS>();
            while (reader.IsStartElement(MeasuredIonMobility.EL.measured_ion_mobility))
            {
                var im = MeasuredIonMobility.Deserialize(reader);
                var key = new LibKey(im.Target, im.Adduct);
                if (!dict.ContainsKey(key))
                {
                    dict.Add(key, im.IonMobilityInfo);
                }
            }

            if (dict.Any())
                MeasuredMobilityIons = dict;

            if (reader.Name.Equals(name)) // Make sure we haven't stepped off the end
            {
                reader.Read(); // Consume end tag
            }

            Validate();
        }

        public override void WriteXml(XmlWriter writer)
        {
            WriteXml(writer, false, false);
        }

        public void WriteXml(XmlWriter writer, bool pre19_2, bool writeElement)
        {
            if (writeElement) // Has caller already begun serialization (as in Roundtrip test code)?
            {
                if (IsEmpty)
                    return;
                var elementName = pre19_2 ? DriftTimePredictor.EL.predict_drift_time.ToString() : ((XmlRootAttribute)Attribute.GetCustomAttribute(GetType(), typeof(XmlRootAttribute))).ElementName;
                writer.WriteStartElement(elementName);
            }
            writer.WriteAttributeString(@"name", Name);
            // Write attributes
            WindowWidthCalculator.WriteXML(writer, pre19_2, false);

            // Write all measured ion mobilities
            if (MeasuredMobilityIons != null)
            {
                foreach (var im in MeasuredMobilityIons)
                {
                    writer.WriteStartElement(pre19_2 ? DriftTimePredictor.EL.measured_dt.ToString() : MeasuredIonMobility.EL.measured_ion_mobility.ToString());
                    var mdt = new MeasuredIonMobility(im.Key.Target, im.Key.Adduct, im.Value);
                    mdt.WriteXml(writer, pre19_2);
                    writer.WriteEndElement();
                }
            }
            if (writeElement)
            {
                writer.WriteEndElement();
            }
        }

        #endregion

        #region object overrides

        public bool Equals(IonMobilityCalibration obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return base.Equals(obj) &&
                   ArrayUtil.EqualsDeep(obj.MeasuredMobilityIons, MeasuredMobilityIons) &&
                   Equals(obj.WindowWidthCalculator, WindowWidthCalculator);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as IonMobilityCalibration);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = base.GetHashCode();
                result = (result * 397) ^ CollectionUtil.GetHashCodeDeep(MeasuredMobilityIons);
                result = (result * 397) ^ WindowWidthCalculator.GetHashCode();
                return result;
            }
        }

        public object GetDefaultObject(ObjectInfo<object> info)
        {
            return IonMobilityCalibrationList.GetDefault();
        }

        #endregion
    }

    public class IonMobilityWindowWidthCalculator : IEquatable<IonMobilityWindowWidthCalculator>
    {
        public enum ATTR
        {
            spectral_library_drift_times_resolving_power, // Pre-19.2  misnomer, used for IMS types other than drift time
            resolving_power, 
            peak_width_calc_type, 
            spectral_library_drift_times_peak_width_calc_type, // Pre-19.2  misnomer, used for IMS types other than drift time
            width_at_dt_zero, // Pre-19.2 misnomer, used for IMS types other than drift time
            width_at_ion_mobility_zero,
            width_at_dt_max, // Pre-19.2 misnomer, used for IMS types other than drift time
            width_at_ion_mobility_max
        }

        public static readonly IonMobilityWindowWidthCalculator EMPTY =
            new IonMobilityWindowWidthCalculator(IonMobilityPeakWidthType.resolving_power, 0, 0, 0);

        private static string AttrPrefix(bool isSpectralLibrary) {  return isSpectralLibrary ? @"spectral_library_" : string.Empty; }

        public IonMobilityWindowWidthCalculator(IonMobilityPeakWidthType peakWidthMode,
            double resolvingPower,
            double widthAtIonMobilityValueZero,
            double widthAtIonMobilityValueMax)
        {
            PeakWidthMode = peakWidthMode;
            ResolvingPower = resolvingPower;
            PeakWidthAtIonMobilityValueZero = widthAtIonMobilityValueZero;
            PeakWidthAtIonMobilityValueMax = widthAtIonMobilityValueMax;
        }

        public IonMobilityWindowWidthCalculator(XmlReader reader, bool isPre19_2, bool isSpectralLibrary) : // Before v19.2 this was in Peptide settings, and used overly specific "drift time" terms for ion mobility
            this(
            reader.GetEnumAttribute(IonMobilityPeakWidthCalcTypeAttr(isPre19_2, isSpectralLibrary), IonMobilityPeakWidthType.resolving_power),
            reader.GetDoubleAttribute(IonMobilityPeakWidthResolvingPowerAttr( isPre19_2, isSpectralLibrary), 0),
            reader.GetDoubleAttribute(AttrPrefix(isSpectralLibrary) + (isPre19_2 ? ATTR.width_at_dt_zero : ATTR.width_at_ion_mobility_zero), 0),
            reader.GetDoubleAttribute(AttrPrefix(isSpectralLibrary) + (isPre19_2 ? ATTR.width_at_dt_max : ATTR.width_at_ion_mobility_max), 0))
        {
        }

        private static string IonMobilityPeakWidthCalcTypeAttr(bool isPre19_2, bool isSpectralLibrary)
        {
            return isPre19_2 ?
                (isSpectralLibrary ? ATTR.spectral_library_drift_times_peak_width_calc_type : ATTR.peak_width_calc_type).ToString() :
                (AttrPrefix(isSpectralLibrary) + ATTR.peak_width_calc_type);
        }

        private static string IonMobilityPeakWidthResolvingPowerAttr(bool isPre19_2, bool isSpectralLibrary)
        {
            return isPre19_2 ?
                (isSpectralLibrary ? ATTR.spectral_library_drift_times_resolving_power : ATTR.resolving_power).ToString() :
                (AttrPrefix(isSpectralLibrary) + ATTR.resolving_power);
        }

        public void WriteXML(XmlWriter writer, bool isPre19_2, bool isSpectralLibrary)
        {
            writer.WriteAttribute(IonMobilityPeakWidthCalcTypeAttr(isPre19_2, isSpectralLibrary), PeakWidthMode);
            writer.WriteAttribute(IonMobilityPeakWidthResolvingPowerAttr(isPre19_2, isSpectralLibrary), ResolvingPower);
            writer.WriteAttribute(AttrPrefix(isSpectralLibrary) + (isPre19_2 ? ATTR.width_at_dt_zero : ATTR.width_at_ion_mobility_zero), PeakWidthAtIonMobilityValueZero);
            writer.WriteAttribute(AttrPrefix(isSpectralLibrary) + (isPre19_2 ? ATTR.width_at_dt_max : ATTR.width_at_ion_mobility_max), PeakWidthAtIonMobilityValueMax);
        }

        public enum IonMobilityPeakWidthType
        {
            resolving_power, // Agilent, etc
            linear_range // Waters SONAR etc
        };

        [Track]
        public bool LinearPeakWidth
        {
            get { return PeakWidthMode == IonMobilityPeakWidthType.linear_range; }
        }

        public IonMobilityPeakWidthType PeakWidthMode { get; private set; }

        // TODO: custom localizer
        // For Water-style (SONAR) linear peak width calcs
        [Track]
        public double PeakWidthAtIonMobilityValueZero { get; private set; }
        [Track]
        public double PeakWidthAtIonMobilityValueMax { get; private set; }

        // For Agilent-style resolving power peak width calcs
        [Track] public double ResolvingPower { get; private set; }

        public double WidthAt(double ionMobility, double ionMobilityMax)
        {
            if (PeakWidthMode == IonMobilityPeakWidthType.resolving_power)
            {
                return Math.Abs((ResolvingPower > 0 ? 2.0 / ResolvingPower : double.MaxValue) *
                                ionMobility); // 2.0*ionMobility/resolvingPower
            }

            Assume.IsTrue(ionMobilityMax != 0,
                @"Expected ionMobilityMax value != 0 for linear range ion mobility window calculation");
            return PeakWidthAtIonMobilityValueZero +
                   Math.Abs(ionMobility * (PeakWidthAtIonMobilityValueMax - PeakWidthAtIonMobilityValueZero) /
                            ionMobilityMax);
        }

        public string Validate()
        {
            if (PeakWidthMode == IonMobilityPeakWidthType.resolving_power)
            {
                if (ResolvingPower <= 0)
                    return Resources.DriftTimePredictor_Validate_Resolving_power_must_be_greater_than_0_;
            }
            else
            {
                if (PeakWidthAtIonMobilityValueZero < 0 ||
                    PeakWidthAtIonMobilityValueMax < PeakWidthAtIonMobilityValueZero)
                    return Resources.DriftTimeWindowWidthCalculator_Validate_Peak_width_must_be_non_negative_;
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
                   Equals(other.PeakWidthMode, PeakWidthMode) &&
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
            int result = PeakWidthMode.GetHashCode();
            result = (result * 397) ^ ResolvingPower.GetHashCode();
            result = (result * 397) ^ PeakWidthAtIonMobilityValueZero.GetHashCode();
            result = (result * 397) ^ PeakWidthAtIonMobilityValueMax.GetHashCode();
            return result;
        }
    }

    /// <summary>
    /// Contains the ion mobility and window used to filter scans
    /// May also contain CCS value associated with the ion mobility, normally only when used in context of chromatogram extraction (not serialized)
    /// </summary>
    public class IonMobilityFilter : IComparable
    {
        public static readonly IonMobilityFilter EMPTY = new IonMobilityFilter(IonMobilityValue.EMPTY, null, null);

        public static IonMobilityFilter GetIonMobilityFilter(IonMobilityValue ionMobility,
            double? ionMobilityExtractionWindowWidth,
            double? collisionalCrossSectionSqA)
        {
            if (!ionMobility.HasValue
                && !ionMobilityExtractionWindowWidth.HasValue)
            {
                return EMPTY;
            }

            return new IonMobilityFilter(ionMobility,
                ionMobilityExtractionWindowWidth,
                collisionalCrossSectionSqA);
        }

        public IonMobilityFilter ApplyOffset(double offset)
        {
            if (offset == 0 || !IonMobility.HasValue)
                return this;
            return ChangeIonMobilityValue(IonMobility.ChangeIonMobility(IonMobility.Mobility + offset));
        }

        public IonMobilityFilter ChangeIonMobilityValue(IonMobilityValue value)
        {
            return (IonMobility.CompareTo(value) == 0)
                ? this
                : GetIonMobilityFilter(value, IonMobilityExtractionWindowWidth, CollisionalCrossSectionSqA);
        }

        public IonMobilityFilter ChangeIonMobilityValue(double? value, eIonMobilityUnits units)
        {
            var im = IonMobility.ChangeIonMobility(value, units);
            return (IonMobility.CompareTo(im) == 0)
                ? this
                : GetIonMobilityFilter(im, IonMobilityExtractionWindowWidth, CollisionalCrossSectionSqA);
        }

        public IonMobilityFilter ChangeIonMobilityValue(double? value)
        {
            var im = IonMobility.ChangeIonMobility(value, IonMobilityUnits);
            return (IonMobility.CompareTo(im) == 0)
                ? this
                : GetIonMobilityFilter(im, IonMobilityExtractionWindowWidth, CollisionalCrossSectionSqA);
        }

        public IonMobilityFilter ChangeExtractionWindowWidth(double? value)
        {
            return (IonMobilityExtractionWindowWidth == value)
                ? this
                : GetIonMobilityFilter(IonMobility, value, CollisionalCrossSectionSqA);
        }

        private IonMobilityFilter(IonMobilityValue ionMobility,
            double? ionMobilityExtractionWindowWidth,
            double? collisionalCrossSectionSqA)
        {
            IonMobility = ionMobility;
            IonMobilityExtractionWindowWidth = ionMobilityExtractionWindowWidth;
            CollisionalCrossSectionSqA = collisionalCrossSectionSqA;
        }

        public IonMobilityValue IonMobility { get; private set; }

        public double?
            CollisionalCrossSectionSqA { get; private set; } // The CCS value used to get the ion mobility, if known

        public double? IonMobilityExtractionWindowWidth { get; private set; }

        public eIonMobilityUnits IonMobilityUnits
        {
            get { return HasIonMobilityValue ? IonMobility.Units : eIonMobilityUnits.none; }
        }

        public bool HasIonMobilityValue
        {
            get { return IonMobility.HasValue; }
        }

        public bool IsEmpty
        {
            get { return !HasIonMobilityValue; }
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

        public void WriteXML(XmlWriter writer, bool omitTypeAndCCS)
        {
            if (IonMobility.Mobility.HasValue)
            {
                writer.WriteAttributeNullable(DocumentSerializer.ATTR.ion_mobility, IonMobility.Mobility);
                writer.WriteAttributeNullable(DocumentSerializer.ATTR.ion_mobility_window,
                    IonMobilityExtractionWindowWidth);
                if (omitTypeAndCCS)
                    return;
                writer.WriteAttributeNullable(DocumentSerializer.ATTR.ccs, CollisionalCrossSectionSqA);
                writer.WriteAttribute(DocumentSerializer.ATTR.ion_mobility_type, IonMobilityUnits.ToString());
            }
        }

        public static IonMobilityFilter ReadXML(XmlReader reader, IonMobilityFilter defaultIonMobilityValues)
        {
            var ionMobilityFilter = EMPTY;
            var driftTime = reader.GetNullableDoubleAttribute(DocumentSerializer.ATTR.drift_time);
            var ccs = reader.GetNullableDoubleAttribute(DocumentSerializer.ATTR.ccs);
            if (driftTime.HasValue)
            {
                var driftTimeWindow = reader.GetNullableDoubleAttribute(DocumentSerializer.ATTR.drift_time_window);
                ionMobilityFilter = GetIonMobilityFilter(IonMobilityValue.GetIonMobilityValue(driftTime.Value,
                    eIonMobilityUnits.drift_time_msec), driftTimeWindow, ccs);
            }
            else
            {
                var ionMobility = reader.GetNullableDoubleAttribute(DocumentSerializer.ATTR.ion_mobility);
                if (ionMobility.HasValue)
                {
                    var ionMobilityWindow =
                        reader.GetNullableDoubleAttribute(DocumentSerializer.ATTR.ion_mobility_window) ??
                        defaultIonMobilityValues.CollisionalCrossSectionSqA;
                    string ionMobilityUnitsString = reader.GetAttribute(DocumentSerializer.ATTR.ion_mobility_type);
                    var ionMobilityUnits =
                        SmallMoleculeTransitionListReader.IonMobilityUnitsFromAttributeValue(ionMobilityUnitsString);
                    ionMobilityFilter = GetIonMobilityFilter(IonMobilityValue.GetIonMobilityValue(ionMobility.Value,
                        ionMobilityUnits), ionMobilityWindow, ccs);
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

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = IonMobility.GetHashCode();
                hashCode = (hashCode * 397) ^ IonMobilityExtractionWindowWidth.GetHashCode();
                hashCode = (hashCode * 397) ^ CollisionalCrossSectionSqA.GetHashCode();
                return hashCode;
            }
        }

        public int CompareTo(object obj)
        {
            var other = obj as IonMobilityFilter;
            if (other == null)
                return 1;
            var val = IonMobility.CompareTo(other.IonMobility);
            if (val != 0)
                return val;
            val = Nullable.Compare(CollisionalCrossSectionSqA, other.CollisionalCrossSectionSqA);
            if (val != 0)
                return val;
            return Nullable.Compare(IonMobilityExtractionWindowWidth, other.IonMobilityExtractionWindowWidth);
        }

        public override string ToString() // For debugging convenience, not user-facing
        {
            string ionMobilityAbbrev = @"im";
            switch (IonMobility.Units)
            {
                case eIonMobilityUnits.drift_time_msec:
                    ionMobilityAbbrev = @"dt";
                    break;
                case eIonMobilityUnits.inverse_K0_Vsec_per_cm2:
                    ionMobilityAbbrev = @"irim";
                    break;
                case eIonMobilityUnits.compensation_V:
                    ionMobilityAbbrev = @"cv";
                    break;
            }

            return string.Format(@"{2}{0:F04}/w{1:F04}", IonMobility.Mobility, IonMobilityExtractionWindowWidth,
                ionMobilityAbbrev);
        }
    }

    /// <summary>
    /// Contains ion mobility and its Collisional Cross Section basis (if known), 
    /// and the effect on ion mobility in high energy spectra as in Waters MSe
    /// </summary>
    public class IonMobilityAndCCS : IComparable
    {
        public static readonly IonMobilityAndCCS EMPTY = new IonMobilityAndCCS(IonMobilityValue.EMPTY, null, 0);


        private IonMobilityAndCCS(IonMobilityValue ionMobility, double? collisionalCrossSectionSqA,
            double highEnergyIonMobilityValueOffset)
        {
            IonMobility = ionMobility;
            CollisionalCrossSectionSqA = collisionalCrossSectionSqA;
            HighEnergyIonMobilityValueOffset = highEnergyIonMobilityValueOffset;
        }

        public static IonMobilityAndCCS GetIonMobilityAndCCS(IonMobilityValue ionMobilityValue,
            double? collisionalCrossSectionSqA, double highEnergyIonMobilityValueOffset)
        {
            return ionMobilityValue.HasValue || collisionalCrossSectionSqA.HasValue
                ? new IonMobilityAndCCS(ionMobilityValue, collisionalCrossSectionSqA, highEnergyIonMobilityValueOffset)
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
        public double
            HighEnergyIonMobilityValueOffset
        {
            get;
            private set;
        } // As in Waters MSe, where product ions fly a bit faster due to added kinetic energy

        public double? GetHighEnergyDriftTimeMsec()
        {
            if (IonMobility.HasValue)
            {
                return IonMobility.Mobility + HighEnergyIonMobilityValueOffset;
            }

            return null;
        }

        public bool HasCollisionalCrossSection
        {
            get { return (CollisionalCrossSectionSqA ?? 0) != 0; }
        }

        public bool HasIonMobilityValue
        {
            get { return IonMobility.HasValue; }
        }

        public bool IsEmpty
        {
            get { return !HasIonMobilityValue && !HasCollisionalCrossSection; }
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return 0 == CompareTo(obj as IonMobilityAndCCS);
        }

        public IonMobilityAndCCS ChangeIonMobilityValue(IonMobilityValue ionMobility)
        {
            return IonMobility.CompareTo(ionMobility) == 0
                ? this
                : GetIonMobilityAndCCS(ionMobility, CollisionalCrossSectionSqA, HighEnergyIonMobilityValueOffset);
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
            IonMobilityAndCCS other = obj as IonMobilityAndCCS;
            if (other == null)
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
    }

    public interface IIonMobilityInfoProvider
    {
        string Name { get; }

        IonMobilityAndCCS GetLibraryMeasuredIonMobilityAndHighEnergyOffset(LibKey peptide, double mz,
            IIonMobilityFunctionsProvider instrumentInfo);

        IDictionary<LibKey, IonMobilityAndCCS[]> GetIonMobilityDict();
    }

    /// <summary>
    /// Represents an observed ion mobility value for
    /// a molecule with a given adduct.
    /// </summary>
    public sealed class MeasuredIonMobility : IXmlSerializable, IComparable<MeasuredIonMobility>
    {
        public Target Target { get; private set; } // ModifiedSequence for peptides, PrimaryEquivalenceKey for small molecules

        public Adduct Adduct { get; private set; }
        public IonMobilityAndCCS IonMobilityInfo { get; private set; }

        public MeasuredIonMobility(Target target, Adduct adduct, IonMobilityAndCCS ionMobilityInfo)
        {
            Target = target;
            Adduct = adduct;
            IonMobilityInfo = ionMobilityInfo;
        }

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private MeasuredIonMobility()
        {
        }

        public enum EL
        {
            measured_ion_mobility
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
            Adduct = Target.IsProteomic
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
            WriteXml(writer, false);
        }

        public void WriteXml(XmlWriter writer, bool pre19_2)
        {
            // Write tag attributes
            writer.WriteAttribute(pre19_2 ? ATTR.modified_sequence : ATTR.target,
                Target.ToSerializableString()); 
            writer.WriteAttribute(ATTR.charge, Target.IsProteomic ? Adduct.ToString() : Adduct.AdductFormula);
            if (IonMobilityInfo.IonMobility.Units != eIonMobilityUnits.none)
            {
                writer.WriteAttributeNullable(ATTR.ion_mobility, IonMobilityInfo.IonMobility.Mobility);
                writer.WriteAttribute(ATTR.high_energy_ion_mobility_offset,
                    IonMobilityInfo.HighEnergyIonMobilityValueOffset);
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
            return Equals(obj.Target, Target) && Equals(obj.Adduct, Adduct) &&
                   Equals(obj.IonMobilityInfo, IonMobilityInfo);
        }

        public int CompareTo(MeasuredIonMobility other)
        {
            int result = Target.CompareTo(other.Target);
            if (result != 0)
                return result;

            result = Adduct.Compare(Adduct, other.Adduct);
            if (result != 0)
                return result;

            return IonMobilityInfo.CompareTo(other.IonMobilityInfo);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof(MeasuredIonMobility)) return false;
            return Equals((MeasuredIonMobility) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = (Target != null ? Target.GetHashCode() : 0);
                result = (result * 397) ^ IonMobilityInfo.GetHashCode();
                result = (result * 397) ^ Adduct.GetHashCode();
                return result;
            }
        }

        #endregion
    }

    /// <summary>
    /// For serialization backward compatibility: this information used to be in Peptide Settings by historical accident
    /// </summary>
    [XmlRoot("predict_drift_time")]
    public class DriftTimePredictor : XmlNamedElement, IAuditLogComparable
    {

        private LibKeyMap<IonMobilityAndCCS> _measuredMobilityIons;
        private IonMobilityWindowWidthCalculator _windowWidthCalculator;

        public DriftTimePredictor(string name,
            Dictionary<LibKey, IonMobilityAndCCS> measuredMobilityIons,
            IonMobilityWindowWidthCalculator.IonMobilityPeakWidthType peakWidthMode,
            double resolvingPower,
            double widthAtIonMobilityZero, double widthAtIonMobilityMax)
            : base(name)
        {
            _windowWidthCalculator = new IonMobilityWindowWidthCalculator(peakWidthMode,
                resolvingPower, widthAtIonMobilityZero, widthAtIonMobilityMax);
            MeasuredMobilityIons = measuredMobilityIons;
            Validate();
        }

        public IDictionary<LibKey, IonMobilityAndCCS> MeasuredMobilityIons
        {
            get { return _measuredMobilityIons == null ? null : _measuredMobilityIons.AsDictionary(); }
            private set { _measuredMobilityIons = LibKeyMap<IonMobilityAndCCS>.FromDictionary(value); }
        }

        public IonMobilityCalibration IonMobilityCalibration
        {
            get
            {
                return new IonMobilityCalibration(Name, MeasuredMobilityIons,
                   _windowWidthCalculator.PeakWidthMode, _windowWidthCalculator.ResolvingPower, _windowWidthCalculator.PeakWidthAtIonMobilityValueZero, _windowWidthCalculator.PeakWidthAtIonMobilityValueMax);
            }
        }

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private DriftTimePredictor()
        {
        }

        public enum EL
        {
            predict_drift_time, // Misnomer - this is used for all IMS types, not just DT
            measured_dt // Misnomer - this is used for all IMS types, not just DT
        }

        private void Validate()
        {
            // This is active if measured ion mobilities are provided
            if (MeasuredMobilityIons != null && MeasuredMobilityIons.Any())
            {
                var messages = new List<string>();
                var msg = _windowWidthCalculator.Validate();
                if (msg != null)
                    messages.Add(msg);
                if (messages.Any())
                    throw new InvalidDataException(TextUtil.LineSeparate(messages));
            }
        }

        public static DriftTimePredictor Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new DriftTimePredictor());
        }

        public override void ReadXml(XmlReader reader)
        {
            var name = reader.Name;
            // Read start tag attributes
            base.ReadXml(reader);
            _windowWidthCalculator = new IonMobilityWindowWidthCalculator(reader, true, false);

            // Consume start tag
            reader.ReadStartElement();

            // Skip over ion_mobility_library stuff that never saw the light of day, but appears in some older tests
            while (reader.Name.Equals(@"ion_mobility_library") || reader.Name.Equals(@"regression_dt"))
            {
                reader.Read();
            }

            // Read all measured ion mobilities
            var dict = new Dictionary<LibKey, IonMobilityAndCCS>();
            while (reader.IsStartElement(EL.measured_dt)) // N.B. EL.measured_dt is a misnomer, this covers all IMS types
            {
                var im = MeasuredIonMobility.Deserialize(reader);
                var key = new LibKey(im.Target, im.Adduct);
                if (!dict.ContainsKey(key))
                {
                    dict.Add(key, im.IonMobilityInfo);
                }
            }

            if (dict.Any())
                MeasuredMobilityIons = dict;

            if (reader.Name.Equals(name)) // Make sure we haven't stepped off the end
            {
                reader.Read(); // Consume end tag
            }

            Validate();
        }

        public override void WriteXml(XmlWriter writer)
        {
            // Write attributes
            writer.WriteAttributeString(@"name", Name);
            _windowWidthCalculator.WriteXML(writer, true, false); // Write in pre-19.2 format

            // Write all measured ion mobilities
            if (MeasuredMobilityIons != null)
            {
                foreach (var im in MeasuredMobilityIons)
                {
                    writer.WriteStartElement(EL.measured_dt); // N.B. EL.measured_dt is a misnomer, this covers all IMS types
                    var mdt = new MeasuredIonMobility(im.Key.Target, im.Key.Adduct, im.Value);
                    mdt.WriteXml(writer, true); // Write in pre-19.2 format
                    writer.WriteEndElement();
                }
            }
        }

        #endregion

        #region object overrides

        public bool Equals(DriftTimePredictor obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return base.Equals(obj) &&
                   ArrayUtil.EqualsDeep(obj.MeasuredMobilityIons, MeasuredMobilityIons) &&
                   Equals(obj._windowWidthCalculator, _windowWidthCalculator);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as DriftTimePredictor);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = base.GetHashCode();
                result = (result * 397) ^ CollectionUtil.GetHashCodeDeep(MeasuredMobilityIons);
                result = (result * 397) ^ _windowWidthCalculator.GetHashCode();
                return result;
            }
        }

        public object GetDefaultObject(ObjectInfo<object> info)
        {
            return IonMobilityCalibrationList.GetDefault();
        }

        #endregion

    }

}