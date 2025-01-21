using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.DocSettings
{
    [XmlRoot("imputation")]
    public class ImputationSettings : Immutable, IXmlSerializable
    {
        public static readonly ImputationSettings DEFAULT = new ImputationSettings();

        [Track(defaultValues: typeof(DefaultValuesNull))]
        public double? MaxRtShift { get; private set; }

        public ImputationSettings ChangeMaxRtShift(double? value)
        {
            return ChangeProp(ImClone(this), im => im.MaxRtShift = value);
        }

        [Track(defaultValues: typeof(DefaultValuesNull))]
        public double? MaxPeakWidthVariation { get; private set; }

        public ImputationSettings ChangeMaxPeakWidthVariation(double? value)
        {
            return ChangeProp(ImClone(this), im => im.MaxPeakWidthVariation = value);
        }

        [Track(defaultValues: typeof(DefaultValuesNull))]
        public string RtCalcName { get; private set; }

        public ImputationSettings ChangeRtCalcName(string value)
        {
            return ChangeProp(ImClone(this), im => im.RtCalcName = value);
        }

        [Track(defaultValues: typeof(DefaultValuesNull))]
        public string RegressionMethodName { get; private set; }

        public ImputationSettings ChangeRegressionMethodName(string value)
        {
            return ChangeProp(ImClone(this), im => im.RegressionMethodName = value);
        }

        protected bool Equals(ImputationSettings other)
        {
            return Nullable.Equals(MaxRtShift, other.MaxRtShift) &&
                   Nullable.Equals(MaxPeakWidthVariation, other.MaxPeakWidthVariation) &&
                   RtCalcName == other.RtCalcName && RegressionMethodName == other.RegressionMethodName;
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ImputationSettings)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = MaxRtShift.GetHashCode();
                hashCode = (hashCode * 397) ^ MaxPeakWidthVariation.GetHashCode();
                hashCode = (hashCode * 397) ^ (RtCalcName != null ? RtCalcName.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (RegressionMethodName != null ? RegressionMethodName.GetHashCode() : 0);
                return hashCode;
            }
        }

        #region XML Serialization
        private enum Attr
        {
            max_rt_shift,
            max_peak_width_var,
            rt_calc,
            regression_method
        }
        XmlSchema IXmlSerializable.GetSchema()
        {
            return null;
        }

        private ImputationSettings()
        {
        }

        void IXmlSerializable.ReadXml(XmlReader reader)
        {
            MaxRtShift = reader.GetNullableDoubleAttribute(Attr.max_rt_shift);
            MaxPeakWidthVariation = reader.GetNullableDoubleAttribute(Attr.max_peak_width_var);
            RtCalcName = reader.GetAttribute(Attr.rt_calc);
            RegressionMethodName = reader.GetAttribute(Attr.regression_method);
            bool empty = reader.IsEmptyElement;
            reader.Read();
            if (!empty)
            {
                reader.ReadEndElement();
            }
        }

        void IXmlSerializable.WriteXml(XmlWriter writer)
        {
            writer.WriteAttributeNullable(Attr.max_rt_shift, MaxRtShift);
            writer.WriteAttributeNullable(Attr.max_peak_width_var, MaxPeakWidthVariation);
            writer.WriteAttributeIfString(Attr.rt_calc, RtCalcName);
            writer.WriteAttributeIfString(Attr.regression_method, RegressionMethodName);
        }

        public static ImputationSettings Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new ImputationSettings());
        }
        #endregion
    }
}
