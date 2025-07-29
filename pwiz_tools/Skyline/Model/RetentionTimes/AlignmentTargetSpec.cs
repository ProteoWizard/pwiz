using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.RetentionTimes
{
    [XmlRoot("alignment")]
    public class AlignmentTargetSpec : Immutable, IXmlSerializable
    {
        public static readonly AlignmentTargetSpec None = new AlignmentTargetSpec(@"none");
        public static readonly AlignmentTargetSpec Default = new AlignmentTargetSpec(@"default");
        public static readonly AlignmentTargetSpec Calculator = new AlignmentTargetSpec(@"calculator");
        public static readonly AlignmentTargetSpec Library = new AlignmentTargetSpec(@"library");
        public static readonly AlignmentTargetSpec ChromatogramPeaks = new AlignmentTargetSpec(@"chromatogram_peaks");

        private AlignmentTargetSpec(string type)
        {
            Type = type;
        }

        public string Type { get; private set; }

        public AlignmentTargetSpec ChangeType(string value)
        {
            return ChangeProp(ImClone(this), im => im.Type = value);
        }

        public string Name { get; private set; }

        public AlignmentTargetSpec ChangeName(string value)
        {
            return ChangeProp(ImClone(this), im => im.Name = value);
        }

        public string RegressionMethod { get; private set; }

        public AlignmentTargetSpec ChangeRegressionMethod(string value)
        {
            return ChangeProp(ImClone(this), im => im.RegressionMethod = value);
        }

        protected bool Equals(AlignmentTargetSpec other)
        {
            return Type == other.Type && Name == other.Name && RegressionMethod == other.RegressionMethod;
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((AlignmentTargetSpec)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Type != null ? Type.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Name != null ? Name.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (RegressionMethod != null ? RegressionMethod.GetHashCode() : 0);
                return hashCode;
            }
        }

        XmlSchema IXmlSerializable.GetSchema()
        {
            return null;
        }

        private AlignmentTargetSpec()
        {
        }

        private enum ATTR
        {
            type,
            name,
            regression_method
        }
        void IXmlSerializable.ReadXml(XmlReader reader)
        {
            if (Type != null)
            {
                throw new InvalidOperationException();
            }

            Type = reader.GetAttribute(ATTR.type) ?? Default.Type;
            Name = reader.GetAttribute(ATTR.name);
            RegressionMethod = reader.GetAttribute(ATTR.regression_method);
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteAttribute(ATTR.type, Type);
            writer.WriteAttributeIfString(ATTR.name, Name);
            writer.WriteAttributeIfString(ATTR.regression_method, RegressionMethod);
        }

        public static IList<AlignmentTargetSpec> GetOptions(PeptideSettings peptideSettings)
        {
            var list = new List<AlignmentTargetSpec>
            {
                None,
                Default
            };
            if (peptideSettings.Prediction?.RetentionTime?.Calculator != null)
            {
                list.Add(Calculator);
            }

            foreach (var library in peptideSettings.Libraries.Libraries)
            {
                if (true == library?.IsLoaded && library.ListRetentionTimeSources().Any())
                {
                    list.Add(Library.ChangeName(library.Name));
                }
            }
            list.Add(ChromatogramPeaks);
            return list;
        }

        public RtCalculatorOption ToRtCalculatorOption(PeptideSettings peptideSettings)
        {
            if (Type == Default.Type)
            {
                return RtCalculatorOption.GetDefault(peptideSettings);
            }

            if (Type == Library.Type)
            {
                return new RtCalculatorOption.Library(Name);
            }

            if (Type == Calculator.Type)
            {
                return new RtCalculatorOption.Irt(Name ?? peptideSettings.Prediction?.RetentionTime?.Calculator?.Name);
            }

            if (Type == ChromatogramPeaks.Type)
            {
                return RtCalculatorOption.MedianDocRetentionTimes;
            }

            return null;
        }
        public static AlignmentTargetSpec Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new AlignmentTargetSpec());
        }

        public string GetLabel(PeptideSettings peptideSettings)
        {
            if (Type == None.Type)
            {
                return Resources.SettingsList_ELEMENT_NONE_None;
            }

            if (Type == Default.Type)
            {
                var defaultOption = RtCalculatorOption.GetDefault(peptideSettings);
                if (defaultOption == null)
                {
                    return "Default";
                }

                return string.Format("Default ({0})", defaultOption.DisplayName);
            }

            if (Type == Library.Type)
            {
                return new RtCalculatorOption.Library(Name).DisplayName;
            }

            if (Type == Calculator.Type)
            {
                return new RtCalculatorOption.Irt(Name).DisplayName;
            }

            if (Type == ChromatogramPeaks.Type)
            {
                return RtCalculatorOption.MedianDocRetentionTimes.DisplayName;
            }

            return "!!Invalid!!";
        }

        public bool TryGetAlignmentTarget(SrmSettings settings, out AlignmentTarget alignmentTarget)
        {
            if (Type == None.Type)
            {
                alignmentTarget = null;
                return true;
            }

            var calculatorOption = ToRtCalculatorOption(settings.PeptideSettings);
            if (calculatorOption == null)
            {
                alignmentTarget = null;
                return false;
            }
            alignmentTarget = calculatorOption.GetAlignmentTarget(settings);
            return alignmentTarget != null;
        }
    }
}
