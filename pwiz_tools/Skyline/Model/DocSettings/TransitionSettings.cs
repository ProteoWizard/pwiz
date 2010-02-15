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
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.DocSettings
{
    [XmlRoot("transition_settings")]
    public class TransitionSettings : Immutable, IXmlSerializable
    {
        public TransitionSettings(TransitionPrediction prediction,
                                  TransitionFilter filter,
                                  TransitionLibraries libraries,
                                  TransitionIntegration integration,
                                  TransitionInstrument instrument)
        {
            Prediction = prediction;
            Filter = filter;
            Libraries = libraries;
            Integration = integration;
            Instrument = instrument;
        }

        public TransitionPrediction Prediction { get; private set; }

        public TransitionFilter Filter { get; private set; }

        public TransitionLibraries Libraries { get; private set; }

        public TransitionIntegration Integration { get; private set; }

        public TransitionInstrument Instrument { get; private set; }

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

        #endregion

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private TransitionSettings()
        {
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

                reader.ReadEndElement();                
            }

            // Defer validation to the SrmSettings object
        }

        public void WriteXml(XmlWriter writer)
        {
            // Write child elements
            writer.WriteElement(Prediction);
            writer.WriteElement(Filter);
            writer.WriteElement(Libraries);
            writer.WriteElement(Integration);
            writer.WriteElement(Instrument);
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
                   Equals(obj.Instrument, Instrument);
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
        private ReadOnlyCollection<int> _precursorCharges;
        private ReadOnlyCollection<int> _productCharges;
        private ReadOnlyCollection<IonType> _ionTypes;
        private StartFragmentFinder _fragmentRangeFirst;
        private EndFragmentFinder _fragmentRangeLast;

        public TransitionFilter(IList<int> precursorCharges, IList<int> productCharges, IList<IonType> ionTypes,
                                string fragmentRangeFirstName, string fragmentRangeLastName,
                                bool includeNProline, bool includeCGluAsp, bool autoSelect)
        {
            PrecursorCharges = precursorCharges;
            ProductCharges = productCharges;
            IonTypes = ionTypes;
            FragmentRangeFirstName = fragmentRangeFirstName;
            FragmentRangeLastName = fragmentRangeLastName;
            IncludeNProline = includeNProline;
            IncludeCGluAsp = includeCGluAsp;
            AutoSelect = autoSelect;
        }

        public IList<int> PrecursorCharges
        {
            get { return _precursorCharges; }
            private set
            {
                ValidateCharges("Precursor charges", value,
                    TransitionGroup.MIN_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE);
                _precursorCharges = MakeReadOnly(value);
            }
        }

        public IList<int> ProductCharges
        {
            get { return _productCharges; }
            private set
            {
                ValidateCharges("Product ion charges", value,
                    Transition.MIN_PRODUCT_CHARGE, Transition.MAX_PRODUCT_CHARGE);
                _productCharges = MakeReadOnly(value);
            }
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

        public bool IncludeNProline { get; private set; }

        public bool IncludeCGluAsp { get; private set; }

        public bool AutoSelect { get; private set; }

        #region Property change methods

        public TransitionFilter ChangePrecursorCharges(IList<int> prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.PrecursorCharges = v, prop);
        }

        public TransitionFilter ChangeProductCharges(IList<int> prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.ProductCharges = v, prop);
        }

        public TransitionFilter ChangeIonTypes(IList<IonType> prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.IonTypes = v, prop);
        }

        public TransitionFilter ChangeFragmentRangeFirstName(string prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.FragmentRangeFirstName = v, prop);
        }

        public TransitionFilter ChangeFragmentRangeLastName(string prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.FragmentRangeLastName = v, prop);
        }

        public TransitionFilter ChangeIncludeNProline(bool prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.IncludeNProline = v, prop);
        }

        public TransitionFilter ChangeIncludeCGluAsp(bool prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.IncludeCGluAsp = v, prop);
        }

        public TransitionFilter ChangeAutoSelect(bool prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.AutoSelect = v, prop);
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
            IonTypes = ParseTypes(reader.GetAttribute(ATTR.fragment_types));
            FragmentRangeFirstName = reader.GetAttribute(ATTR.fragment_range_first);
            FragmentRangeLastName = reader.GetAttribute(ATTR.fragment_range_last);
            // First, try old misspeelling of proline
            IncludeNProline = reader.GetBoolAttribute(ATTR.include_n_prolene);
            // Second, try correct spelling
            IncludeNProline = reader.GetBoolAttribute(ATTR.include_n_proline, IncludeNProline);
            IncludeCGluAsp = reader.GetBoolAttribute(ATTR.include_c_glu_asp);
            AutoSelect = reader.GetBoolAttribute(ATTR.auto_select);

            // Consume tag
            reader.Read();
        }

        public void WriteXml(XmlWriter writer)
        {
            // Write attributes
            writer.WriteAttributeString(ATTR.precursor_charges, PrecursorCharges.ToString(","));
            writer.WriteAttributeString(ATTR.product_charges, ProductCharges.ToString(","));
            writer.WriteAttributeString(ATTR.fragment_types, IonTypes.ToString(","));
            writer.WriteAttributeString(ATTR.fragment_range_first, FragmentRangeFirstName);
            writer.WriteAttributeString(ATTR.fragment_range_last, FragmentRangeLastName);
            writer.WriteAttribute(ATTR.include_n_proline, IncludeNProline);
            writer.WriteAttribute(ATTR.include_c_glu_asp, IncludeCGluAsp);
            writer.WriteAttribute(ATTR.auto_select, AutoSelect);
        }

        private static int[] ParseInts(string s)
        {
            return ArrayUtil.Parse(s, Convert.ToInt32, ',', new int[0]);
        }

        private static IonType[] ParseTypes(string s)
        {
            return ArrayUtil.Parse(s, v => (IonType)Enum.Parse(typeof(IonType), v.ToLower()), ',',
                new[] { IonType.y });
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
                   obj.IncludeNProline.Equals(IncludeNProline) &&
                   obj.IncludeCGluAsp.Equals(IncludeCGluAsp) &&
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
                result = (result*397) ^ IncludeNProline.GetHashCode();
                result = (result*397) ^ IncludeCGluAsp.GetHashCode();
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
                else if (_mapLegacyStartNames.TryGetValue(finderName, out finderName))
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
                else if (_mapLegacyEndNames.TryGetValue(finderName, out finderName))
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

            public abstract int FindStartFragment(double[,] masses, IonType type, int charge, double precursorMz);
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

            public override int FindStartFragment(double[,] masses, IonType type, int charge, double precursorMz)
            {
                int length = masses.GetLength(1);
                Debug.Assert(length > 0);

                if (Transition.IsNTerminal(type))
                    return Math.Min(_ordinal, length) - 1;
                else
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

            public override int FindStartFragment(double[,] masses, IonType type, int charge, double precursorMz)
            {
                int length = masses.GetLength(1);
                Debug.Assert(length > 0);

                if (Transition.IsNTerminal(type))
                {
                    for (int i = 0; i < length; i++)
                    {
                        if (SequenceMassCalc.GetMZ(masses[(int)type, i], charge) > precursorMz)
                            return (Math.Max(0, Math.Min(length - 1, i + _offset)));
                    }
                    return length - 1;
                }
                else
                {
                    for (int i = length - 1; i >= 0; i--)
                    {
                        if (SequenceMassCalc.GetMZ(masses[(int)type, i], charge) > precursorMz)
                            return (Math.Max(0, Math.Min(length - 1, i - _offset)));                        
                    }
                    return 0;
                }
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
                else
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
                else
                    return Math.Max(0, start - _count + 1);                
            }

            #endregion
        }
    }

    public interface IStartFragmentFinder : IKeyContainer<string>
    {
        int FindStartFragment(double[,] masses, IonType type, int charge, double precursorMz);
    }

    public interface IEndFragmentFinder : IKeyContainer<string>
    {
        int FindEndFragment(IonType type, int start, int length);
    }

    public interface IEndCountFragmentFinder : IEndFragmentFinder
    {
        int Count { get; }
    }

    public enum TransitionLibraryPick { none, all, filter }

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

    [XmlRoot("transition_instrument")]
    public sealed class TransitionInstrument : Immutable, IValidating, IXmlSerializable
    {
        public const int MIN_MEASUREABLE_MZ = 10;
        public const int MIN_MZ_RANGE = 100;
        public const int MAX_MEASURABLE_MZ = 10000;
        public const double MIN_MZ_MATCH_TOLERANCE = 0.0001;
        public const double MAX_MZ_MATCH_TOLERANCE = 0.6;
        public const double DEFAULT_MZ_MATCH_TOLERANCE = 0.055;
        public const int MIN_TRANSITION_MAX = 50;
        public const int MAX_TRANSITION_MAX = 10000;

        public TransitionInstrument(int minMz, int maxMz, double mzMatchTolerance, int? maxTransitions)
        {
            MinMz = minMz;
            MaxMz = maxMz;
            MzMatchTolerance = mzMatchTolerance;
            MaxTransitions = maxTransitions;

            DoValidate();
        }

        public int MinMz { get; private set; }

        public int MaxMz { get; private set; }

        public bool IsMeasurable(double mz)
        {
            return MinMz <= mz && mz <= MaxMz;
        }

        public double MzMatchTolerance { get; private set; }

        public bool IsMzMatch(double mz1, double mz2)
        {
            return Math.Abs(mz1 - mz1) <= MzMatchTolerance;
        }

        public int? MaxTransitions { get; private set; }

        #region Property change methods

        public TransitionInstrument ChangeMinMz(int prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.MinMz = v, prop);
        }

        public TransitionInstrument ChangeMaxMz(int prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.MaxMz = v, prop);
        }

        public TransitionInstrument ChangeMzMatchTolerance(double prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.MzMatchTolerance = v, prop);
        }

        public TransitionInstrument ChangeMaxTransitions(int? prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.MaxTransitions = v, prop);
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
            mz_match_tolerance,
            max_transitions
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
            if (MIN_TRANSITION_MAX > MaxTransitions || MaxTransitions > MAX_TRANSITION_MAX)
            {
                throw new InvalidDataException(string.Format("The maximum number of transitions {0} must be between {1} and {2}.",
                    MaxTransitions, MIN_TRANSITION_MAX, MAX_TRANSITION_MAX));
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
            MinMz = reader.GetIntAttribute(ATTR.min_mz);
            MaxMz = reader.GetIntAttribute(ATTR.max_mz);
            MzMatchTolerance = reader.GetDoubleAttribute(ATTR.mz_match_tolerance, DEFAULT_MZ_MATCH_TOLERANCE);
            MaxTransitions = reader.GetNullableIntAttribute(ATTR.max_transitions);

            // Consume tag
            reader.Read();

            DoValidate();
        }

        public void WriteXml(XmlWriter writer)
        {
            // Write attributes
            writer.WriteAttribute(ATTR.min_mz, MinMz);
            writer.WriteAttribute(ATTR.max_mz, MaxMz);
            writer.WriteAttribute(ATTR.mz_match_tolerance, MzMatchTolerance);
            writer.WriteAttributeNullable(ATTR.max_transitions, MaxTransitions);
        }

        #endregion

        #region object overrides

        public bool Equals(TransitionInstrument other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return other.MinMz == MinMz &&
                other.MaxMz == MaxMz &&
                other.MzMatchTolerance == MzMatchTolerance &&
                other.MaxTransitions.Equals(MaxTransitions);
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
                result = (result*397) ^ MzMatchTolerance.GetHashCode();
                result = (result*397) ^ (MaxTransitions.HasValue ? MaxTransitions.Value : 0);
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