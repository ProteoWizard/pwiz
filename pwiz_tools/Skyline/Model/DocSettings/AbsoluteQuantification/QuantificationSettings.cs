/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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

using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.DocSettings.AbsoluteQuantification
{
    /// <summary>
    /// Settings related to absolute quantification which are part of <see cref="PeptideSettings"/>
    /// </summary>
    [XmlRoot("quantification")]
    public class QuantificationSettings : Immutable, IXmlSerializable
    {
        public static readonly QuantificationSettings DEFAULT 
            = new QuantificationSettings(RegressionWeighting.NONE);

        public QuantificationSettings(RegressionWeighting regressionWeighting)
        {
            RegressionWeighting = regressionWeighting;
            RegressionFit = RegressionFit.NONE;
            NormalizationMethod = NormalizationMethod.NONE;
            Units = null;
            LodCalculation = LodCalculation.NONE;
        }

        [Track]
        public RegressionWeighting RegressionWeighting { get; private set; }
        [Track]
        public RegressionFit RegressionFit { get; private set; }
        
        public QuantificationSettings ChangeRegressionWeighting(RegressionWeighting weighting)
        {
            return ChangeProp(ImClone(this), im => im.RegressionWeighting = weighting);
        }

        public QuantificationSettings ChangeRegressionFit(RegressionFit regressionFit)
        {
            return ChangeProp(ImClone(this), im => im.RegressionFit = regressionFit);
        }

        [Track]
        public NormalizationMethod NormalizationMethod { get; private set; }

        public QuantificationSettings ChangeNormalizationMethod(NormalizationMethod normalizationMethod)
        {
            return ChangeProp(ImClone(this), im => im.NormalizationMethod = normalizationMethod);
        }

        // TODO: custom localizer for null==all?
        [Track]
        public int? MsLevel { get; private set; }

        public QuantificationSettings ChangeMsLevel(int? level)
        {
            return ChangeProp(ImClone(this), im => im.MsLevel = level);
        }

        [Track]
        public string Units { get; private set; }

        public QuantificationSettings ChangeUnits(string units)
        {
            return ChangeProp(ImClone(this), im => im.Units = string.IsNullOrEmpty(units) ? null : units);
        }

        [Track]
        public LodCalculation LodCalculation { get; private set; }


        public QuantificationSettings ChangeLodCalculation(LodCalculation lodCalculation)
        {
            return ChangeProp(ImClone(this), im => im.LodCalculation = lodCalculation);
        }

        [Track]
        public bool SimpleRatios { get; private set; }

        public QuantificationSettings ChangeSimpleRatios(bool value)
        {
            return ChangeProp(ImClone(this), im => im.SimpleRatios = value);
        }

        [Track]
        public double? MaxLoqBias { get; private set; }
        [Track]
        public double? MaxLoqCv { get; private set; }

        public QuantificationSettings ChangeMaxLoqBias(double? maxLoqBias)
        {
            return ChangeProp(ImClone(this), im => im.MaxLoqBias = maxLoqBias);
        }

        public QuantificationSettings ChangeMaxLoqCv(double? maxLoqCv)
        {
            return ChangeProp(ImClone(this), im => im.MaxLoqCv = maxLoqCv);
        }

        [Track]
        public double? QualitativeIonRatioThreshold { get; private set; }

        public QuantificationSettings ChangeQualitativeIonRatioThreshold(double? ionRatioThreshold)
        {
            return ChangeProp(ImClone(this), im => im.QualitativeIonRatioThreshold = ionRatioThreshold);
        }

        #region Equality Members

        protected bool Equals(QuantificationSettings other)
        {
            return Equals(RegressionWeighting, other.RegressionWeighting) &&
                   Equals(RegressionFit, other.RegressionFit) && 
                   Equals(NormalizationMethod, other.NormalizationMethod) &&
                   Equals(MsLevel, other.MsLevel) &&
                   Equals(Units, other.Units) &&
                   Equals(LodCalculation, other.LodCalculation) &&
                   Equals(MaxLoqBias, other.MaxLoqBias) &&
                   Equals(MaxLoqCv, other.MaxLoqCv) &&
                   Equals(QualitativeIonRatioThreshold, other.QualitativeIonRatioThreshold) &&
                   Equals(SimpleRatios, other.SimpleRatios);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((QuantificationSettings) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = RegressionWeighting.GetHashCode();
                hashCode = (hashCode*397) ^ RegressionFit.GetHashCode();
                hashCode = (hashCode*397) ^ NormalizationMethod.GetHashCode();
                hashCode = (hashCode*397) ^ MsLevel.GetHashCode();
                hashCode = (hashCode*397) ^ (Units == null ? 0 : Units.GetHashCode());
                hashCode = (hashCode*397) ^ LodCalculation.GetHashCode();
                hashCode = (hashCode*397) ^ MaxLoqBias.GetHashCode();
                hashCode = (hashCode*397) ^ MaxLoqCv.GetHashCode();
                hashCode = (hashCode*397) ^ SimpleRatios.GetHashCode();
                return hashCode;
            }
        }

        #endregion

        #region XML Serialization
        private enum Attr
        {
            weighting,
            fit,
            normalization,
            ms_level,
            units,
            lod_calculation,
            max_loq_bias,
            max_loq_cv,
            qualitative_ion_ratio_threshold,
            simple_ratios
        }
        XmlSchema IXmlSerializable.GetSchema()
        {
            return null;
        }

        private QuantificationSettings()
        {
        }

        void IXmlSerializable.ReadXml(XmlReader reader)
        {
            RegressionWeighting = RegressionWeighting.Parse(reader.GetAttribute(Attr.weighting));
            RegressionFit = RegressionFit.Parse(reader.GetAttribute(Attr.fit));
            NormalizationMethod = NormalizationMethod.FromName(reader.GetAttribute(Attr.normalization));
            MsLevel = reader.GetNullableIntAttribute(Attr.ms_level);
            Units = reader.GetAttribute(Attr.units);
            LodCalculation = LodCalculation.Parse(reader.GetAttribute(Attr.lod_calculation));
            MaxLoqBias = reader.GetNullableDoubleAttribute(Attr.max_loq_bias);
            MaxLoqCv = reader.GetNullableDoubleAttribute(Attr.max_loq_cv);
            QualitativeIonRatioThreshold = reader.GetNullableDoubleAttribute(Attr.qualitative_ion_ratio_threshold);
            SimpleRatios = reader.GetBoolAttribute(Attr.simple_ratios, false);
            bool empty = reader.IsEmptyElement;
            reader.Read();
            if (!empty)
            {
                reader.ReadEndElement();
            }
        }

        void IXmlSerializable.WriteXml(XmlWriter writer)
        {
            if (null != RegressionWeighting)
            {
                writer.WriteAttributeString(Attr.weighting, RegressionWeighting.Name);
            }
            if (null != RegressionFit)
            {
                writer.WriteAttributeString(Attr.fit, RegressionFit.Name);
            }
            if (null != NormalizationMethod)
            {
                writer.WriteAttributeString(Attr.normalization, NormalizationMethod.Name);
            }
            writer.WriteAttributeNullable(Attr.ms_level, MsLevel);
            writer.WriteAttributeIfString(Attr.units, Units);
            if (LodCalculation != LodCalculation.NONE)
            {
                writer.WriteAttributeString(Attr.lod_calculation, LodCalculation.Name);
            }
            writer.WriteAttributeNullable(Attr.max_loq_bias, MaxLoqBias);
            writer.WriteAttributeNullable(Attr.max_loq_cv, MaxLoqCv);
            writer.WriteAttributeNullable(Attr.qualitative_ion_ratio_threshold, QualitativeIonRatioThreshold);
            writer.WriteAttribute(Attr.simple_ratios, SimpleRatios, false);
        }

        public static QuantificationSettings Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new QuantificationSettings());
        }

        public IFiguresOfMeritCalculator GetFiguresOfMeritCalculator()
        {
            if (RegressionFit.BILINEAR == RegressionFit && MaxLoqCv.HasValue && !MaxLoqBias.HasValue)
            {
                return new BootstrapFiguresOfMeritCalculator(MaxLoqCv.Value / 100);
            }

            return new SimpleFiguresOfMeritCalculator(LodCalculation, MaxLoqCv / 100, MaxLoqBias / 100);
        }
        #endregion
    }
}
