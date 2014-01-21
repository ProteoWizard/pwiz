/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.IO;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results.Scoring
{
    [XmlRoot("peak_feature_calculator")] // Not L10N
    public class FeatureCalculator : IXmlSerializable
    {
        public FeatureCalculator(Type type, double weight)
        {
            Type = type;
            Weight = weight;

            Validate();
        }

        public Type Type { get; private set; }
        public double Weight { get; private set; }

        /// <summary>
        /// For serialization
        /// </summary>
        protected FeatureCalculator()
        {
        }

        private enum ATTR2
        {
            type,
            weight
        }

        private void Validate()
        {
            if (Type == null)
                throw new InvalidDataException();
        }

// ReSharper disable MemberHidesStaticFromOuterClass
        public static FeatureCalculator Deserialize(XmlReader reader)
// ReSharper restore MemberHidesStaticFromOuterClass
        {
            return reader.Deserialize(new FeatureCalculator());
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            // Read tag attributes
            Type = reader.GetTypeAttribute(ATTR2.type);
            Weight = reader.GetDoubleAttribute(ATTR2.weight);

            // Consume tag
            reader.Read();

            Validate();
        }

        public void WriteXml(XmlWriter writer)
        {
            // Write tag attributes
            writer.WriteAttribute(ATTR2.type, Type);
            writer.WriteAttribute(ATTR2.weight, Weight);
        }

        public bool Equals(FeatureCalculator obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.Type == Type && Equals(obj.Weight, Weight);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof(FeatureCalculator)) return false;
            return Equals((FeatureCalculator)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Type.GetHashCode() * 397) ^ Weight.GetHashCode();
            }
        }
    }
}