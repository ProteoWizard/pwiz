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
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.Chemistry;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

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
                                  TransitionFullScan fullScan)
        {
            Prediction = prediction;
            Filter = filter;
            Libraries = libraries;
            Integration = integration;
            Instrument = instrument;
            FullScan = fullScan;

            DoValidate();
        }

        public TransitionPrediction Prediction { get; private set; }

        public TransitionFilter Filter { get; private set; }

        public TransitionLibraries Libraries { get; private set; }

        public TransitionIntegration Integration { get; private set; }

        public TransitionInstrument Instrument { get; private set; }

        public TransitionFullScan FullScan { get; private set; }

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
                    Prediction != null && Prediction.PrecursorMassType != MassType.Monoisotopic)
            {
                throw new InvalidDataException("High resolution MS1 filtering requires use of monoisotopic precursor masses.");
            }

            if (FullScan != null && FullScan.IsolationScheme != null && FullScan.IsolationScheme.WindowsPerScan.HasValue &&
                (Instrument == null || !Instrument.MaxInclusions.HasValue))
            {
                throw new InvalidDataException("The instrument's firmware inclusion limit must be specified before doing a multiplexed DIA scan.");
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
                Libraries = reader.DeserializeElement<TransitionLibraries>();
                Integration = reader.DeserializeElement<TransitionIntegration>();
                Instrument = reader.DeserializeElement<TransitionInstrument>();
                FullScan = reader.DeserializeElement<TransitionFullScan>();
                // Backward compatibility with v0.7.1
                if (FullScan == null && Instrument != null && Instrument.PrecursorAcquisitionMethod != FullScanAcquisitionMethod.None)
                {
                    FullScan = new TransitionFullScan(Instrument.PrecursorAcquisitionMethod,
                                                      TransitionFullScan.CreateIsolationSchemeForFilter(Instrument.PrecursorAcquisitionMethod, Instrument.PrecursorFilter, null),
                                                      FullScanMassAnalyzerType.qit,
                                                      Instrument.ProductFilter/TransitionFullScan.RES_PER_FILTER, null,
                                                      FullScanPrecursorIsotopes.None, null,
                                                      FullScanMassAnalyzerType.none, null, null,
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
            return Equals(obj.Prediction, Prediction) &&
                   Equals(obj.Filter, Filter) &&
                   Equals(obj.Libraries, Libraries) &&
                   Equals(obj.Integration, Integration) &&
                   Equals(obj.Instrument, Instrument) &&
                   Equals(obj.FullScan, FullScan);
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

    [XmlRoot("transition_prediction")]
    public class TransitionPrediction : Immutable, IValidating, IXmlSerializable
    {
        public TransitionPrediction(MassType precursorMassType, MassType fragmentMassType,
                                    CollisionEnergyRegression collisionEnergy,
                                    DeclusteringPotentialRegression declusteringPotential,
                                    OptimizedMethodType optimizedMethodType)
        {
            PrecursorMassType = precursorMassType;
            FragmentMassType = fragmentMassType;
            CollisionEnergy = collisionEnergy;
            DeclusteringPotential = declusteringPotential;
            OptimizedMethodType = optimizedMethodType;

            DoValidate();
        }

        public TransitionPrediction(TransitionPrediction copy)
            : this(copy.PrecursorMassType,
                   copy.FragmentMassType,
                   copy.CollisionEnergy,
                   copy.DeclusteringPotential,
                   copy.OptimizedMethodType)
        {
        }

        public MassType PrecursorMassType { get; private set; }

        public MassType FragmentMassType { get; private set; }

        public CollisionEnergyRegression CollisionEnergy { get; private set; }

        public DeclusteringPotentialRegression DeclusteringPotential { get; private set; }

        public OptimizedMethodType OptimizedMethodType { get; private set; }

        /// <summary>
        /// This element is here for backward compatibility with the
        /// 0.1.0.0 document format.  It is not cloned, or checked for
        /// equality.  Its value, if not null, must be moved to
        /// <see cref="PeptidePrediction"/>.
        /// </summary>
        public RetentionTimeRegression RetentionTime { get; set; }

        #region Property change methods

        public TransitionPrediction ChangePrecursorMassType(MassType prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.PrecursorMassType = v, prop);
        }

        public TransitionPrediction ChangeFragmentMassType(MassType prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.FragmentMassType = v, prop);
        }

        public TransitionPrediction ChangeCollisionEnergy(CollisionEnergyRegression prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.CollisionEnergy = v, prop);
        }

        public TransitionPrediction ChangeDeclusteringPotential(DeclusteringPotentialRegression prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.DeclusteringPotential = v, prop);
        }

        public TransitionPrediction ChangeOptimizedMethodType(OptimizedMethodType prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.OptimizedMethodType = v, prop);
        }
        
        public TransitionPrediction ChangeRetentionTime(RetentionTimeRegression prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.RetentionTime = v, prop);
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
                throw new InvalidDataException("Transition prediction requires a collision energy regression function.");
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
                reader.Read();
            else
            {
                reader.ReadStartElement();

                // Read child elements.
                CollisionEnergy = reader.DeserializeElement<CollisionEnergyRegression>();
                RetentionTime = reader.DeserializeElement<RetentionTimeRegression>();   // v0.1.0 support
                DeclusteringPotential = reader.DeserializeElement<DeclusteringPotentialRegression>();

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
            writer.WriteElement(CollisionEnergy);
            if (DeclusteringPotential != null)
                writer.WriteElement(DeclusteringPotential);
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

        private ReadOnlyCollection<int> _precursorCharges;
        private ReadOnlyCollection<int> _productCharges;
        private ReadOnlyCollection<IonType> _ionTypes;
        private ReadOnlyCollection<MeasuredIon> _measuredIons;
        private StartFragmentFinder _fragmentRangeFirst;
        private EndFragmentFinder _fragmentRangeLast;

        public TransitionFilter(IList<int> precursorCharges,
                                IList<int> productCharges,
                                IList<IonType> ionTypes,
                                string fragmentRangeFirstName,
                                string fragmentRangeLastName,
                                IList<MeasuredIon> measuredIons,
                                double precursorMzWindow,
                                bool autoSelect)
        {
            PrecursorCharges = precursorCharges;
            ProductCharges = productCharges;
            IonTypes = ionTypes;
            FragmentRangeFirstName = fragmentRangeFirstName;
            FragmentRangeLastName = fragmentRangeLastName;
            MeasuredIons = measuredIons;
            PrecursorMzWindow = precursorMzWindow;
            AutoSelect = autoSelect;

            Validate();
        }

        public IList<int> PrecursorCharges
        {
            get { return _precursorCharges; }
            private set
            {
                ValidateCharges("Precursor charges", value,
                    TransitionGroup.MIN_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE);
                _precursorCharges = MakeChargeCollection(value);
            }
        }

        public IList<int> ProductCharges
        {
            get { return _productCharges; }
            private set
            {
                ValidateCharges("Product ion charges", value,
                    Transition.MIN_PRODUCT_CHARGE, Transition.MAX_PRODUCT_CHARGE);
                _productCharges = MakeChargeCollection(value);
            }
        }

        private static ReadOnlyCollection<int> MakeChargeCollection(IList<int> charges)
        {
            var arrayCharges = charges.ToArrayStd();
            Array.Sort(arrayCharges);
            return MakeReadOnly(arrayCharges);
        }

        public IList<IonType> IonTypes
        {
            get { return _ionTypes; }
            private set
            {
                if (value.Count == 0)
                    throw new InvalidDataException("At least one ion type is required.");
                _ionTypes = MakeReadOnly(value);
            }
        }

        public IStartFragmentFinder FragmentRangeFirst { get { return _fragmentRangeFirst;  } }

        public string FragmentRangeFirstName
        {
            get { return _fragmentRangeFirst.Name; }
            private set
            {
                _fragmentRangeFirst = (StartFragmentFinder)GetStartFragmentFinder(value);
                if (_fragmentRangeFirst == null)
                    throw new InvalidDataException(string.Format("Unsupported first fragment name {0}.", FragmentRangeFirst));                
            }
        }

        public IEndFragmentFinder FragmentRangeLast { get { return _fragmentRangeLast; } }

        public string FragmentRangeLastName
        {
            get { return _fragmentRangeLast.Name; }
            private set
            {
                _fragmentRangeLast = (EndFragmentFinder)GetEndFragmentFinder(value);
                if (_fragmentRangeLast == null)
                    throw new InvalidDataException(string.Format("Unsupported last fragment name {0}.", FragmentRangeLast));                
            }
        }

        public IList<MeasuredIon> MeasuredIons
        {
            get { return _measuredIons; }
            private set { _measuredIons = MakeReadOnly(value); }
        }

        public bool IsSpecialFragment(string sequence, IonType ionType, int cleavageOffset)
        {
            return MeasuredIons.Contains(m => m.IsMatch(sequence, ionType, cleavageOffset));
        }

        /// <summary>
        /// A m/z window width around the precursor m/z where transitions are not allowed.
        /// </summary>
        public double PrecursorMzWindow { get; private set; }

        /// <summary>
        /// Returns true if the ion m/z value is within the precursor m/z exclusion window.
        /// i.e. within 1/2 of the window width of the precursor m/z.
        /// </summary>
        public bool IsExcluded(double ionMz, double precursorMz)
        {
            return PrecursorMzWindow != 0 && Math.Abs(ionMz - precursorMz)*2 < PrecursorMzWindow;
        }

        public bool Accept(string sequence, double precursorMz, IonType type, int cleavageOffset, double ionMz, int start, int end, double startMz)
        {
            if (IsExcluded(ionMz, precursorMz))
                return false;
            if (start <= cleavageOffset && cleavageOffset <= end && startMz <= ionMz)
                return true;            
            return IsSpecialFragment(sequence, type, cleavageOffset);
        }

        public bool AutoSelect { get; private set; }

        #region Property change methods

        public TransitionFilter ChangePrecursorCharges(IList<int> prop)
        {
            return ChangeProp(ImClone(this), im => im.PrecursorCharges = prop);
        }

        public TransitionFilter ChangeProductCharges(IList<int> prop)
        {
            return ChangeProp(ImClone(this), im => im.ProductCharges = prop);
        }

        public TransitionFilter ChangeIonTypes(IList<IonType> prop)
        {
            return ChangeProp(ImClone(this), im => im.IonTypes = prop);
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
            auto_select
        }

        private static void ValidateCharges(string label, ICollection<int> charges, int min, int max)
        {
            if (charges == null || charges.Count == 0)
                throw new InvalidDataException(string.Format("{0} cannot be empty.", label));
            HashSet<int> seen = new HashSet<int>();
            foreach (int charge in charges)
            {
                if (seen.Contains(charge))
                    throw new InvalidDataException(string.Format("Precursor charges specified charge {0} more than once.", charge));
                if (min > charge || charge > max)
                    throw new InvalidDataException(string.Format("Invalid charge {1} found.  {0} must be between {2} and {3}.", label, charge, min, max));
                seen.Add(charge);
            }            
        }

        public void Validate()
        {
            if (PrecursorMzWindow != 0)
            {
                if (MIN_EXCLUSION_WINDOW > PrecursorMzWindow || PrecursorMzWindow > MAX_EXCLUSION_WINDOW)
                    throw new InvalidDataException(string.Format("A precursor exclusion window must be between {0} and {1}.", MIN_EXCLUSION_WINDOW, MAX_EXCLUSION_WINDOW));
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
            PrecursorCharges = ParseInts(reader.GetAttribute(ATTR.precursor_charges));
            ProductCharges = ParseInts(reader.GetAttribute(ATTR.product_charges));
            IonTypes = ParseTypes(reader.GetAttribute(ATTR.fragment_types), new[] { IonType.y });
            FragmentRangeFirstName = reader.GetAttribute(ATTR.fragment_range_first);
            FragmentRangeLastName = reader.GetAttribute(ATTR.fragment_range_last);
            PrecursorMzWindow = reader.GetDoubleAttribute(ATTR.precursor_mz_window);
            // First, try old misspelling of proline
            bool legacyProline = reader.GetBoolAttribute(ATTR.include_n_prolene);
            // Second, try correct spelling
            legacyProline = reader.GetBoolAttribute(ATTR.include_n_proline, legacyProline);
            bool lecacyGluAsp = reader.GetBoolAttribute(ATTR.include_c_glu_asp);
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
            writer.WriteAttributeString(ATTR.precursor_charges, PrecursorCharges.ToString(","));
            writer.WriteAttributeString(ATTR.product_charges, ProductCharges.ToString(","));
            writer.WriteAttributeString(ATTR.fragment_types, ToStringIonTypes(false));
            writer.WriteAttributeString(ATTR.fragment_range_first, FragmentRangeFirstName);
            writer.WriteAttributeString(ATTR.fragment_range_last, FragmentRangeLastName);
            writer.WriteAttribute(ATTR.precursor_mz_window, PrecursorMzWindow);
            writer.WriteAttribute(ATTR.auto_select, AutoSelect);
            writer.WriteElements(MeasuredIons);
        }

        public string ToStringIonTypes(bool spaces)
        {
            return IonTypes.ToString(spaces ? ", " : ",").Replace(IonType.precursor.ToString(), "p");
        }

        private static int[] ParseInts(string s)
        {
            return ArrayUtil.Parse(s, Convert.ToInt32, ',', new int[0]);
        }

        public static IonType[] ParseTypes(string s, IonType[] defaultTypes)
        {
            return ArrayUtil.Parse(s, v => (IonType)Enum.Parse(typeof(IonType), v.ToLower().Replace("p", IonType.precursor.ToString())), ',',
                defaultTypes);
        }

        #endregion

        #region object overrides

        public bool Equals(TransitionFilter obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return ArrayUtil.EqualsDeep(obj._precursorCharges, _precursorCharges) &&
                   ArrayUtil.EqualsDeep(obj._productCharges, _productCharges) &&
                   ArrayUtil.EqualsDeep(obj._ionTypes, _ionTypes) &&
                   Equals(obj.FragmentRangeFirst, FragmentRangeFirst) &&
                   Equals(obj.FragmentRangeLast, FragmentRangeLast) &&
                   ArrayUtil.EqualsDeep(obj.MeasuredIons, MeasuredIons) &&
                   obj.PrecursorMzWindow.Equals(PrecursorMzWindow) &&
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
                int result = _precursorCharges.GetHashCodeDeep();
                result = (result*397) ^ _productCharges.GetHashCodeDeep();
                result = (result*397) ^ _ionTypes.GetHashCodeDeep();
                result = (result*397) ^ FragmentRangeFirst.GetHashCode();
                result = (result*397) ^ FragmentRangeLast.GetHashCode();
                result = (result*397) ^ MeasuredIons.GetHashCodeDeep();
                result = (result*397) ^ PrecursorMzWindow.GetHashCode();
                result = (result*397) ^ AutoSelect.GetHashCode();
                return result;
            }
        }

        #endregion

        private static MappedList<string, StartFragmentFinder> _fragmentStartFinders;
        private static Dictionary<string, string> _mapLegacyStartNames;
        private static MappedList<string, EndFragmentFinder> _fragmentEndFinders;
        private static Dictionary<string, string> _mapLegacyEndNames;

        public static IEnumerable<string> GetStartFragmentFinderNames()
        {
            return FragmentStartFinders.Keys;
        }

        public static IStartFragmentFinder GetStartFragmentFinder(string finderName)
        {
            if (!string.IsNullOrEmpty(finderName))
            {
                StartFragmentFinder result;
                if (FragmentStartFinders.TryGetValue(finderName, out result))
                    return result;
                if (_mapLegacyStartNames.TryGetValue(finderName, out finderName))
                    return FragmentStartFinders[finderName];
            }
            return null;
        }

        private static MappedList<string, StartFragmentFinder> FragmentStartFinders
        {
            get
            {
                if (_fragmentStartFinders == null)
                {
                    _fragmentStartFinders = new MappedList<string, StartFragmentFinder>
                    {
                        new OrdinalFragmentFinder("ion 1", 1),
                        new OrdinalFragmentFinder("ion 2", 2),
                        new OrdinalFragmentFinder("ion 3", 3),
                        new OrdinalFragmentFinder("ion 4", 4),
                        new MzFragmentFinder("m/z > precursor", 0),
                        new MzFragmentFinder("(m/z > precursor) - 1", -1),
                        new MzFragmentFinder("(m/z > precursor) - 2", -2),
                        new MzFragmentFinder("(m/z > precursor) + 1", 1),
                        new MzFragmentFinder("(m/z > precursor) + 2", 2)
                    };

                    _mapLegacyStartNames = new Dictionary<string, string>
                                               {
                                                   {"y1", "ion 1"},
                                                   {"y2", "ion 2"},
                                                   {"y3", "ion 3"},
                                                   {"y4", "ion 4"},
                                               };
                }
                return _fragmentStartFinders;
            }
        }

        public static IEnumerable<string> GetEndFragmentFinderNames()
        {
            return FragmentEndFinders.Keys;
        }

        public static IEndFragmentFinder GetEndFragmentFinder(string finderName)
        {
            if (!string.IsNullOrEmpty(finderName))
            {
                EndFragmentFinder result;
                if (FragmentEndFinders.TryGetValue(finderName, out result))
                    return result;
                if (_mapLegacyEndNames.TryGetValue(finderName, out finderName))
                    return FragmentEndFinders[finderName];
            }
            return null;
        }

        private static MappedList<string, EndFragmentFinder> FragmentEndFinders
        {
            get
            {
                if (_fragmentEndFinders == null)
                {
                    _fragmentEndFinders = new MappedList<string, EndFragmentFinder>
                    {
                        new LastFragmentFinder("last ion", 0),
                        new LastFragmentFinder("last ion - 1", 1),
                        new LastFragmentFinder("last ion - 2", 2),
                        new LastFragmentFinder("last ion - 3", 3),
                        new DeltaFragmentFinder("1 ion", 1),
                        new DeltaFragmentFinder("2 ions", 2),
                        new DeltaFragmentFinder("3 ions", 3),
                        new DeltaFragmentFinder("4 ions", 4),
                        new DeltaFragmentFinder("5 ions", 5),
                        new DeltaFragmentFinder("6 ions", 6)
                    };

                    _mapLegacyEndNames = new Dictionary<string, string>
                                               {
                                                   {"last y-ion", "last ion"},
                                                   {"last y-ion - 1", "last ion - 1"},
                                                   {"last y-ion - 2", "last ion - 2"},
                                                   {"last y-ion - 3", "last ion - 3"},
                                                   {"start + 3", "3 ions"},
                                                   {"start + 4", "4 ions"},
                                                   {"start + 5", "5 ions"},
                                                   {"start + 6", "6 ions"},
                                               };
                }
                return _fragmentEndFinders;
            }
        }

        private abstract class StartFragmentFinder : NamedElement, IStartFragmentFinder
        {
            protected StartFragmentFinder(string name)
                : base(name)
            {
            }

            public abstract int FindStartFragment(double[,] masses, IonType type, int charge, double precursorMz, double precursorMzWindow, out double startMz);
        }

        private class OrdinalFragmentFinder : StartFragmentFinder
        {
            private readonly int _ordinal;

            public OrdinalFragmentFinder(string name, int ordinal)
                : base(name)
            {
                _ordinal = Math.Max(1, ordinal);
            }

            #region IStartFragmentFinder Members

            public override int FindStartFragment(double[,] masses, IonType type, int charge, double precursorMz, double precursorMzWindow, out double startMz)
            {
                startMz = 0;
                int length = masses.GetLength(1);
                Debug.Assert(length > 0);

                if (Transition.IsNTerminal(type))
                    return Math.Min(_ordinal, length) - 1;
                
                return Math.Max(0, length - _ordinal);
            }

            #endregion
        }

        private class MzFragmentFinder : StartFragmentFinder
        {
            private readonly int _offset;

            public MzFragmentFinder(string name, int offset)
                : base(name)
            {
                _offset = offset;
            }

            #region IStartFragmentFinder Members

            public override int FindStartFragment(double[,] masses, IonType type, int charge,
                double precursorMz, double precursorMzWindow, out double startMz)
            {
                int start = FindStartFragment(masses, type, charge, precursorMz, precursorMzWindow);
                // If the start is not the precursor m/z, but some offset from it, use the
                // m/z of the fragment that was chosen as the start.  Otherwise, use the precursor m/z.
                // Unfortunately, this means you really want ion m/z values >= start m/z
                // when start m/z is based on the first allowable fragment, but
                // m/z values > start m/z when start m/z is the precursor m/z. At this point,
                // using >= always is recommended for simplicity.
                startMz = (_offset != 0 ? SequenceMassCalc.GetMZ(masses[(int) type, start], charge) : precursorMz);
                return start;
            }

            private int FindStartFragment(double[,] masses, IonType type, int charge,
                                          double precursorMz, double precursorMzWindow)
            {
                int offset = _offset;
                int length = masses.GetLength(1);
                if (length == 0)
                    throw new ArgumentException("Invalid attempt to find a fragment in a peptide without fragment ions.");
                int typeIndex = (int) type;
                if (0 > typeIndex || typeIndex >= masses.Length)
                    throw new IndexOutOfRangeException(string.Format("Ion type {0} not found in masses array", type));

                // Make sure to start outside the precursor m/z window
                double thresholdMz = precursorMz + precursorMzWindow / 2;

                if (Transition.IsNTerminal(type))
                {
                    for (int i = 0; i < length; i++)
                    {
                        if (SequenceMassCalc.GetMZ(masses[(int)type, i], charge) > thresholdMz)
                        {
                            int indexRet;
                            do
                            {
                                indexRet = Math.Max(0, Math.Min(length - 1, i + offset));
                                offset--;
                            }
                            // Be sure not to start with a m/z value inside the exclusion window
                            while (precursorMzWindow > 0 && offset < 0 && i + offset >= 0 &&
                                Math.Abs(SequenceMassCalc.GetMZ(masses[(int)type, indexRet], charge) - precursorMz)*2 < precursorMzWindow);
                            return indexRet;
                        }
                    }
                    return length - 1;
                }

                for (int i = length - 1; i >= 0; i--)
                {
                    if (SequenceMassCalc.GetMZ(masses[(int)type, i], charge) > thresholdMz)
                    {
                        int indexRet;
                        do
                        {
                            indexRet = Math.Max(0, Math.Min(length - 1, i - offset));
                            offset--;
                        }
                            // Be sure not to start with a m/z value inside the exclusion window
                        while (precursorMzWindow > 0 && offset < 0 && i - offset < length &&
                               Math.Abs(SequenceMassCalc.GetMZ(masses[(int)type, indexRet], charge) - precursorMz)*2 < precursorMzWindow);
                        return indexRet;
                    }
                }
                return 0;
            }

            #endregion
        }

        private abstract class EndFragmentFinder : NamedElement, IEndFragmentFinder
        {
            protected EndFragmentFinder(string name)
                : base(name)
            {
            }

            public abstract int FindEndFragment(IonType type, int start, int length);
        }

        private class LastFragmentFinder : EndFragmentFinder
        {
            private readonly int _offset;

            public LastFragmentFinder(string name, int offset)
                : base(name)
            {
                _offset = offset;
            }

            #region IEndFragmentFinder Members

            public override int FindEndFragment(IonType type, int start, int length)
            {
                Debug.Assert(length > 0);

                int end = length - 1;
                if (Transition.IsNTerminal(type))
                    return Math.Max(0, end - _offset);
                
                return Math.Min(end, _offset);
            }

            #endregion
        }

        private class DeltaFragmentFinder : EndFragmentFinder, IEndCountFragmentFinder
        {
            private readonly int _count;

            public DeltaFragmentFinder(string name, int count)
                : base(name)
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

                if (Transition.IsNTerminal(type))
                    return Math.Min(start + _count, length) - 1;
                
                return Math.Max(0, start - _count + 1);
            }

            #endregion
        }
    }

    public interface IStartFragmentFinder : IKeyContainer<string>
    {
        int FindStartFragment(double[,] masses, IonType type, int charge, double precursorMz, double precursorMzWindow, out double startMz);
    }

    public interface IEndFragmentFinder : IKeyContainer<string>
    {
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
        public const int MAX_ION_COUNT = 10;
        public const double MIN_MATCH_TOLERANCE = 0.1;
        public const double MAX_MATCH_TOLERANCE = 1.0;

        public TransitionLibraries(double ionMatchTolerance, int ionCount, TransitionLibraryPick pick)
        {
            IonMatchTolerance = ionMatchTolerance;
            IonCount = ionCount;
            Pick = pick;

            DoValidate();
        }

        public double IonMatchTolerance { get; private set; }
        
        public int IonCount { get; private set; }

        public TransitionLibraryPick Pick { get; private set; }

        #region Property change methods

        public TransitionLibraries ChangeIonMatchTolerance(double prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.IonMatchTolerance = v, prop);
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
                throw new InvalidDataException(string.Format("Library ion match tolerance value {0} must be between {1} and {2}.",
                                                             IonMatchTolerance, MIN_MATCH_TOLERANCE, MAX_MATCH_TOLERANCE));                
            }
            if (MIN_ION_COUNT > IonCount || IonCount > MAX_ION_COUNT)
            {
                throw new InvalidDataException(string.Format("Library ion count value {0} must be between {1} and {2}.",
                                                             IonCount, MIN_ION_COUNT, MAX_ION_COUNT));
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
            writer.WriteAttribute(ATTR.ion_count, IonCount);
            writer.WriteAttribute(ATTR.pick_from, Pick);
        }

        #endregion

        #region object overrides

        public bool Equals(TransitionLibraries obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.IonMatchTolerance == IonMatchTolerance && obj.IonCount == IonCount && Equals(obj.Pick, Pick);
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
                result = (result*397) ^ IonCount;
                result = (result*397) ^ Pick.GetHashCode();
                return result;
            }
        }

        #endregion
    }

// ReSharper disable InconsistentNaming
    public enum LegacyAcquisitionMethod   { None, Single, Multiple }    // Skyline 1.2 and earlier
    public enum FullScanAcquisitionMethod { None, Targeted, DIA }
    public enum FullScanPrecursorIsotopes { None, Count, Percent }
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

        public int MinMz { get; private set; }

        public int MaxMz { get; private set; }

        public bool IsMeasurable(double mz)
        {
            return MinMz <= mz && mz <= MaxMz;
        }

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

        public double MzMatchTolerance { get; private set; }

        public bool IsMzMatch(double mz1, double mz2)
        {
            return Math.Abs(mz1 - mz1) <= MzMatchTolerance;
        }

        public int? MaxTransitions { get; private set; }
        
        public int? MaxInclusions { get; private set; }

        public int? MinTime { get; private set; }

        public int? MaxTime { get; private set; }

        // Backward compatibility with 0.7.1

        public FullScanAcquisitionMethod PrecursorAcquisitionMethod { get; private set; }

        public double? PrecursorFilter { get; private set; }

        public string ProductFilterType { get; private set; }

        public double? ProductFilter { get; private set; }

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
                throw new InvalidDataException(string.Format("Instrument minimum m/z value {0} must be between {1} and {2}.",
                                                             MinMz, MIN_MEASUREABLE_MZ, MAX_MEASURABLE_MZ - MIN_MZ_RANGE));
            }
            if (MinMz + MIN_MZ_RANGE > MaxMz)
            {
                throw new InvalidDataException(string.Format("Instrument maximum m/z value {0} is less than {1} from minimum {2}.",
                                                             MaxMz, MIN_MZ_RANGE, MinMz));
            }
            if (MaxMz > MAX_MEASURABLE_MZ)
            {
                throw new InvalidDataException(string.Format("Instrument maximum m/z exceeds allowable maximum {0}.",
                                                             MAX_MEASURABLE_MZ));
            }
            if (MIN_MZ_MATCH_TOLERANCE > MzMatchTolerance || MzMatchTolerance > MAX_MZ_MATCH_TOLERANCE)
            {
                throw new InvalidDataException(string.Format("The m/z match tolerance {0} must be between {1} and {2}.",
                    MzMatchTolerance, MIN_MZ_MATCH_TOLERANCE, MAX_MZ_MATCH_TOLERANCE));
            }
            if (MIN_TRANSITION_MAX_ORIGINAL > MaxTransitions || MaxTransitions > MAX_TRANSITION_MAX)
            {
                throw new InvalidDataException(string.Format("The maximum number of transitions {0} must be between {1} and {2}.",
                    MaxTransitions, MIN_TRANSITION_MAX_ORIGINAL, MAX_TRANSITION_MAX));
            }
            if (MIN_INCLUSION_MAX > MaxInclusions || MaxInclusions > MAX_INCLUSION_MAX)
            {
                throw new InvalidDataException(string.Format("The maximum number of inclusions {0} must be between {1} and {2}.",
                    MaxInclusions, MIN_INCLUSION_MAX, MAX_INCLUSION_MAX));
            }
            if (MinTime.HasValue && (MIN_TIME > MinTime || MinTime > MAX_TIME))
            {
                throw new InvalidDataException(string.Format("The minimum retention time {0} must be between {1} and {2}.",
                    MinTime, MIN_TIME, MAX_TIME));
            }
            if (MaxTime.HasValue && (MIN_TIME > MaxTime || MaxTime > MAX_TIME))
            {
                throw new InvalidDataException(string.Format("The maximum retention time {0} must be between {1} and {2}.",
                    MaxTime, MIN_TIME, MAX_TIME));
            }
            if (MinTime.HasValue && MaxTime.HasValue && MaxTime.Value - MinTime.Value < MIN_TIME_RANGE)
            {
                throw new InvalidDataException(string.Format("The allowable retention time range {0} to {1} must be at least {2} minutes apart.",
                    MinTime, MaxTime, MIN_TIME_RANGE));
            }
            if (PrecursorAcquisitionMethod == FullScanAcquisitionMethod.None)
            {
                if (ProductFilterType != null || PrecursorFilter.HasValue || ProductFilter.HasValue)
                    throw new InvalidDataException(string.Format("No other full-scan MS/MS filter settings are allowed when precursor filter is none."));
            }
            else if (PrecursorAcquisitionMethod == FullScanAcquisitionMethod.DIA)
            {
                const double minFilter = TransitionFullScan.MIN_PRECURSOR_MULTI_FILTER;
                const double maxFilter = TransitionFullScan.MAX_PRECURSOR_MULTI_FILTER;
                if (!PrecursorFilter.HasValue || minFilter > PrecursorFilter || PrecursorFilter > maxFilter)
                    throw new InvalidDataException(string.Format("The precursor m/z filter must be between {0} and {1}",
                        minFilter, maxFilter));
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

            // Full-scan filter parameters (backward compatibility w/ 0.7.1)
            var legacyFilterType = reader.GetEnumAttribute(ATTR.precursor_filter_type, LegacyAcquisitionMethod.None);
            PrecursorAcquisitionMethod = TransitionFullScan.TranslateLegacyFilterType(legacyFilterType);
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
                other.ProductFilter.Equals(ProductFilter);
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

    public enum FullScanMassAnalyzerType { none = -1, qit, tof, orbitrap, ft_icr }
    public enum RetentionTimeFilterType {none, scheduling_windows, ms2_ids}

    [XmlRoot("transition_full_scan")]
    public sealed class TransitionFullScan : Immutable, IValidating, IXmlSerializable
    {
        // Calculate precursor single filter window values by doubling match tolerance values
        public const double MIN_PRECURSOR_MULTI_FILTER = TransitionInstrument.MIN_MZ_MATCH_TOLERANCE*2;
        public const double MAX_PRECURSOR_MULTI_FILTER = 10*1000;
        public const double DEFAULT_PRECURSOR_MULTI_FILTER = 2.0;
        // Calculate product low accuracy filter window values by doubling ion match tolerance values
        public const double MIN_LO_RES = 0.1;
        public const double MAX_LO_RES = 2.0;
        public const double MIN_HI_RES = 1000;
        public const double MAX_HI_RES = 10*1000*1000;
        public const double DEFAULT_RES_MZ = 400;
        public const double MIN_RES_MZ = 50;
        public const double MAX_RES_MZ = 2000;
        public const double RES_PER_FILTER = 2;
        public const double MIN_ISOTOPE_PERCENT = 0;
        public const double MAX_ISOTOPE_PERCENT = 100;
        public const double DEFAULT_ISOTOPE_PERCENT = 20;
        public const int MIN_ISOTOPE_COUNT = 1;
        public const int MAX_ISOTOPE_COUNT = 5;
        public const int DEFAULT_ISOTOPE_COUNT = 3;
        public const double ISOTOPE_PEAK_CENTERING_RES = 0.1;
        public const double MIN_ISOTOPE_PEAK_ABUNDANCE = 0.01;

        public const string QIT = "QIT";
        public const string ORBITRAP = "Orbitrap";
        public const string TOF = "TOF";
        public const string FT_ICR = "FT-ICR";

        public static readonly string[] MASS_ANALYZERS = new[] {QIT, TOF, ORBITRAP, FT_ICR};
        public static readonly double[] DEFAULT_RES_VALUES = new[] {0.7, 10*1000, 60*1000, 100*1000};
        public static readonly double DEFAULT_RES_QIT = DEFAULT_RES_VALUES[0];

        private double _cachedPrecursorRes;
        private double _cachedProductRes;

        public TransitionFullScan()
        {
            ProductMassAnalyzer = FullScanMassAnalyzerType.none;
            PrecursorMassAnalyzer = FullScanMassAnalyzerType.none;

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

            IsotopeEnrichments = isotopeEnrichments;

            RetentionTimeFilterType = retentionTimeFilterType;
            RetentionTimeFilterLength = retentionTimeFilterMinutes;

            DoValidate();
        }

        // MS/MS filtering

        public FullScanAcquisitionMethod AcquisitionMethod { get; private set; }

        public IsolationScheme IsolationScheme { get; private set; }

        public double? PrecursorFilter
        {
            get { return IsolationScheme == null ? null : IsolationScheme.PrecursorFilter; }
        }

        public double? PrecursorRightFilter
        {
            get { return IsolationScheme == null ? null : IsolationScheme.PrecursorRightFilter; }
        }

        public static IsolationScheme CreateIsolationSchemeForFilter(FullScanAcquisitionMethod acquisitionMethod, double? precursorFilter, double? precursorRightFilter)
        {
            switch (acquisitionMethod)
            {
                case FullScanAcquisitionMethod.None:
                    throw new InvalidDataException("Tried to create an isolation scheme for non-DIA mode");
                
                case FullScanAcquisitionMethod.DIA:
                    if (!precursorFilter.HasValue)
                    {
                        throw new InvalidDataException("Tried to create an isolation scheme without precursor filter");
                    }
                    else
                    {
                        string name = precursorRightFilter.HasValue
                            ? string.Format("Results {0:0.##},{1:0.##} Th", precursorFilter.Value,
                                precursorRightFilter.Value)
                            : string.Format("Results {0:0.##} Th", precursorFilter.Value);
                        return new IsolationScheme(name, precursorFilter, precursorRightFilter);
                    }
            }

            // No scheme for Targeted acquisition mode.
            return null;
        }

        public FullScanMassAnalyzerType ProductMassAnalyzer { get; private set; }

        public double? ProductRes { get; private set; }

        public double? ProductResMz { get; private set; }

        // MS1 filtering

        public FullScanPrecursorIsotopes PrecursorIsotopes { get; private set; }

        public double? PrecursorIsotopeFilter { get; private set; }

        public FullScanMassAnalyzerType PrecursorMassAnalyzer { get; private set; }

        public double? PrecursorRes { get; private set; }

        public double? PrecursorResMz { get; private set; }

        public IsotopeEnrichments IsotopeEnrichments { get; private set; }

        public IsotopeAbundances IsotopeAbundances
        {
            get { return IsotopeEnrichments != null ? IsotopeEnrichments.IsotopeAbundances : null; }
        }

        public RetentionTimeFilterType RetentionTimeFilterType { get; private set; }
        public double RetentionTimeFilterLength { get; private set; }

        public bool IsEnabled
        {
            get { return IsEnabledMs || IsEnabledMsMs; }
        }

        public bool IsEnabledMs
        {
            get { return PrecursorIsotopes != FullScanPrecursorIsotopes.None; }
        }

        public bool IsEnabledMsMs
        {
            get { return AcquisitionMethod != FullScanAcquisitionMethod.None; }
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
               default:
                    return cached;
            }
        }

        private static double GetDenominator(FullScanMassAnalyzerType analyzerType, double res, double resMz)
        {
            switch (analyzerType)
            {
                case FullScanMassAnalyzerType.tof:
                    return res / RES_PER_FILTER;
                case FullScanMassAnalyzerType.orbitrap:
                    return Math.Sqrt(resMz) * res / RES_PER_FILTER;
                case FullScanMassAnalyzerType.ft_icr:
                    return resMz * res / RES_PER_FILTER;
                default:
                    return res * RES_PER_FILTER;
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
                im.ProductResMz = mzProp;
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
                im.PrecursorResMz = mzProp;
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
            scheduled_filter, // deprecated
            retention_time_filter_type,
            retention_time_filter_length,
        }

        void IValidating.Validate()
        {
            DoValidate();
        }

        private void DoValidate()
        {
            if (PrecursorIsotopes == FullScanPrecursorIsotopes.None)
            {
                if (PrecursorMassAnalyzer != FullScanMassAnalyzerType.none || PrecursorIsotopeFilter.HasValue || PrecursorRes.HasValue || PrecursorResMz.HasValue)
                    throw new InvalidDataException("No other full-scan MS1 filter settings are allowed when no precursor isotopes are included.");
            }
            else
            {
                if (PrecursorMassAnalyzer == FullScanMassAnalyzerType.qit)
                {
                    if (PrecursorIsotopes != FullScanPrecursorIsotopes.Count || PrecursorIsotopeFilter != 1)
                    {
                        throw new InvalidDataException("For MS1 filtering with a QIT mass analyzer only 1 isotope peak is supported.");
                    }
                }
                else
                {
                    if (IsotopeEnrichments == null)
                        IsotopeEnrichments = IsotopeEnrichments.DEFAULT;
                    if (PrecursorIsotopes == FullScanPrecursorIsotopes.Count)
                    {
                        ValidateRange(PrecursorIsotopeFilter, MIN_ISOTOPE_COUNT, MAX_ISOTOPE_COUNT,
                                      "The precursor isotope count for MS1 filtering must be between {0} and {1} peaks.");
                    }
                    else
                    {
                        ValidateRange(PrecursorIsotopeFilter, MIN_ISOTOPE_PERCENT, MAX_ISOTOPE_PERCENT,
                                      "The precursor isotope percent for MS1 filtering must be between {0}% and {1}% of the base peak.");
                    }
                }
                _cachedPrecursorRes = ValidateRes(PrecursorMassAnalyzer, PrecursorRes, PrecursorResMz);
            }

            if (AcquisitionMethod == FullScanAcquisitionMethod.None)
            {
                if (ProductMassAnalyzer != FullScanMassAnalyzerType.none || IsolationScheme != null || ProductRes.HasValue || ProductResMz.HasValue)
                    throw new InvalidDataException("No other full-scan MS/MS filter settings are allowed when precursor filter is none.");
            }
            else
            {
                if (AcquisitionMethod == FullScanAcquisitionMethod.Targeted)
                {
                    if (IsolationScheme != null)
                        throw new InvalidDataException("An isolation window width value is not allowed in Targeted mode.");
                }
                else
                {
                    if (IsolationScheme == null)
                        throw new InvalidDataException("An isolation window width value is required in DIA mode.");
                }

                _cachedProductRes = ValidateRes(ProductMassAnalyzer, ProductRes, ProductResMz);
            }
        }

        public static void ValidateRange(double? value, double min, double max, string messageFormat)
        {
            if (!value.HasValue || min > value.Value || value.Value > max)
                throw new InvalidDataException(string.Format(messageFormat, min, max));
        }

        private static double ValidateRes(FullScanMassAnalyzerType analyzerType, double? res, double? resMz)
        {
            bool expectMz = false;
            if (analyzerType == FullScanMassAnalyzerType.qit)
            {
                if (!res.HasValue || MIN_LO_RES > res.Value || res.Value > MAX_LO_RES)
                    throw new InvalidDataException(string.Format("The precursor resolution must be between {0} and {1} for QIT.",
                                                                 MIN_LO_RES, MAX_LO_RES));
            }
            else
            {
                if (!res.HasValue || MIN_HI_RES > res.Value || res.Value > MAX_HI_RES)
                    throw new InvalidDataException(string.Format("The precursor resolving power must be between {0} and {1} for {2}.",
                                                                 MIN_HI_RES, MAX_HI_RES, MassAnalyzerToString(analyzerType)));
                expectMz = (analyzerType != FullScanMassAnalyzerType.tof);
            }
            if (expectMz)
            {
                if (!resMz.HasValue || MIN_RES_MZ > resMz.Value || resMz.Value > MAX_RES_MZ)
                    throw new InvalidDataException(string.Format("The m/z value at which the resolving power is calibrated is required for {0}.",
                        MassAnalyzerToString(analyzerType)));
            }
            else if (resMz.HasValue)
            {
                throw new InvalidDataException(string.Format("Unexpected resolving power m/z value for {0}",
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
            AcquisitionMethod = reader.GetAttribute(ATTR.precursor_filter_type) != null
                ? TranslateLegacyFilterType(reader.GetEnumAttribute(ATTR.precursor_filter_type, LegacyAcquisitionMethod.None))
                : reader.GetEnumAttribute(ATTR.acquisition_method, FullScanAcquisitionMethod.None);

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
                RetentionTimeFilterLength = reader.GetIntAttribute(ATTR.retention_time_filter_length);
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

        public static FullScanAcquisitionMethod TranslateLegacyFilterType(LegacyAcquisitionMethod legacyFilterType)
        {
            switch (legacyFilterType)
            {
                case LegacyAcquisitionMethod.Single:    return FullScanAcquisitionMethod.Targeted;
                case LegacyAcquisitionMethod.Multiple:  return FullScanAcquisitionMethod.DIA;
                default:                                return FullScanAcquisitionMethod.None;
            }
        }

        public void WriteXml(XmlWriter writer)
        {
            // Write attributes
            if (AcquisitionMethod != FullScanAcquisitionMethod.None)
            {
                writer.WriteAttribute(ATTR.acquisition_method, AcquisitionMethod);
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
            if (RetentionTimeFilterType != RetentionTimeFilterType.none)
            {
                writer.WriteAttribute(ATTR.retention_time_filter_type, RetentionTimeFilterType);
                if (RetentionTimeFilterType == RetentionTimeFilterType.ms2_ids)
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
        public bool IsIntegrateAll { get; private set; }

        #region Property change methods

        public TransitionIntegration ChangeIntegrateAll(bool prop)
        {
            return ChangeProp(ImClone(this), im => im.IsIntegrateAll = prop);
        }        

        #endregion

        #region Implementation of IXmlSerializable

        private enum ATTR
        {
            integrate_all,
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
        }

        public void WriteXml(XmlWriter writer)
        {
            // Write attributes
            writer.WriteAttribute(ATTR.integrate_all, IsIntegrateAll);
        }

        #endregion

        #region object overrides

        public bool Equals(TransitionIntegration other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return other.IsIntegrateAll.Equals(IsIntegrateAll);
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
            return IsIntegrateAll.GetHashCode();
        }

        #endregion
    }
}