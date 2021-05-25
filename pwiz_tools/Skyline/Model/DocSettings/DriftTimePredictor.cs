﻿/*
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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.DocSettings
{
    /// <summary>
    /// OBSOLETE.
    /// Retained for backward compatibility (de)serialization: this information used to be in Peptide Settings by historical accident
    /// </summary>
    [XmlRoot("predict_drift_time")]
    public class DriftTimePredictor : XmlNamedElement
    {

        private LibKeyMap<IonMobilityAndCCS> _measuredMobilityIons;
        private IonMobilityWindowWidthCalculator _windowWidthCalculator;

        public DriftTimePredictor(string name,
            IDictionary<LibKey, IonMobilityAndCCS> measuredMobilityIons,
            IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType windowWidthMode,
            double resolvingPower,
            double widthAtIonMobilityZero, double widthAtIonMobilityMax,
            double fixedPeakWidth)
            : base(name)
        {
            _windowWidthCalculator = new IonMobilityWindowWidthCalculator(windowWidthMode,
                resolvingPower, widthAtIonMobilityZero, widthAtIonMobilityMax, fixedPeakWidth);
            MeasuredMobilityIons = measuredMobilityIons;
            Validate();
        }

        public bool IsEmpty
        {
            get { return (_measuredMobilityIons == null || !_measuredMobilityIons.Any()) && 
                         (_windowWidthCalculator ==null || _windowWidthCalculator.IsEmpty); }
        }

        public IDictionary<LibKey, IonMobilityAndCCS> MeasuredMobilityIons
        {
            get { return _measuredMobilityIons == null ? null : _measuredMobilityIons.AsDictionary(); }
            private set { _measuredMobilityIons = LibKeyMap<IonMobilityAndCCS>.FromDictionary(value); }
        }

        public TransitionIonMobilityFiltering CreateTransitionIonMobilityFiltering(string dbDir)
        {
            // Create .imsdb library from MeasuredMobilityIons, for backward compatibility
            var oldDict = MeasuredMobilityIons;
            var dict = oldDict == null
                ? null
                : oldDict.ToDictionary(kvp => kvp.Key,
                    kvp => new List<IonMobilityAndCCS>() {kvp.Value});

            if ((dict == null || dict.Count == 0) && IonMobilityWindowWidthCalculator.IsNullOrEmpty(_windowWidthCalculator))
            {
                return TransitionIonMobilityFiltering.EMPTY;
            }
            var val = new TransitionIonMobilityFiltering(Name, dbDir, dict,
                true,
                _windowWidthCalculator);
            return val.ChangeLibrary(val.IonMobilityLibrary.Initialize(null));
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
                var key = new LibKey(im.Target, im.Charge);
                if (!dict.ContainsKey(key))
                {
                    dict.Add(key, im.IonMobilityInfo);
                }
            }

            if (dict.Any())
                MeasuredMobilityIons = dict;

            if (reader.Name.Equals(name)) // Make sure we haven't stepped off the end
            {
                reader.ReadEndElement(); // Consume end tag
            }

            Validate();
        }

        public override void WriteXml(XmlWriter writer)
        {
            // Write attributes
            writer.WriteAttributeString(@"name", Name);
            _windowWidthCalculator.WriteXML(writer, true, false); // Write in legacy format where this is a peptide setting

            // Write all measured ion mobilities
            if (MeasuredMobilityIons != null)
            {
                foreach (var im in MeasuredMobilityIons)
                {
                    writer.WriteStartElement(EL.measured_dt); // N.B. EL.measured_dt is a misnomer, this covers all IMS types
                    var mdt = new MeasuredIonMobility(im.Key.Target, im.Key.Adduct, im.Value);
                    mdt.WriteXml(writer);
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


        #endregion

    }
}