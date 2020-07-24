using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.DocSettings
{
    [XmlRoot("extracted_metadata_rule")]
    public class ExtractedMetadataRule : Immutable, IXmlSerializable
    {
        public static ExtractedMetadataRule EMPTY = new ExtractedMetadataRule();
        public string SourceColumn { get; private set; }

        public ExtractedMetadataRule ChangeSourceColumn(string value)
        {
            return ChangeProp(ImClone(this), im => im.SourceColumn = value);
        }

        public string MatchRegularExpression { get; private set; }

        public ExtractedMetadataRule ChangeMatchRegularExpression(string value)
        {
            return ChangeProp(ImClone(this), im => im.MatchRegularExpression = value);
        }
        public string TargetColumn { get; private set; }

        public ExtractedMetadataRule ChangeTargetColumn(string value)
        {
            return ChangeProp(ImClone(this), im => im.TargetColumn = value);
        }
        protected bool Equals(ExtractedMetadataRule other)
        {
            return SourceColumn == other.SourceColumn && MatchRegularExpression == other.MatchRegularExpression && TargetColumn == other.TargetColumn;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ExtractedMetadataRule)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (MatchRegularExpression != null ? MatchRegularExpression.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (SourceColumn != null ? SourceColumn.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (TargetColumn != null ? TargetColumn.GetHashCode() : 0);
                return hashCode;
            }
        }

        #region Serialization
        private enum ATTR
        {
            source_column,
            match_regular_expression,
            target_column,
        }

        XmlSchema IXmlSerializable.GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            if (null != SourceColumn)
            {
                throw new InvalidOperationException();
            }
            MatchRegularExpression = reader.GetAttribute(ATTR.match_regular_expression);
            SourceColumn = reader.GetAttribute(ATTR.source_column);
            TargetColumn = reader.GetAttribute(ATTR.target_column);
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteAttributeIfString(ATTR.source_column, SourceColumn);
            writer.WriteAttributeIfString(ATTR.match_regular_expression, MatchRegularExpression);
            writer.WriteAttribute(ATTR.target_column, TargetColumn);
        }

        public static ExtractedMetadataRule Deserialize(XmlReader reader)
        {
            var extractedMetadataDef = new ExtractedMetadataRule();
            ((IXmlSerializable)extractedMetadataDef).ReadXml(reader);
            return extractedMetadataDef;
        }

        #endregion Serialization

    }
    [XmlRoot("extracted_metadata_rules")]
    public class ExtractedMetadataRuleSet : Immutable, IXmlSerializable
    {
        public ExtractedMetadataRuleSet(string rowSource, IEnumerable<ExtractedMetadataRule> rules)
        {
            RowSource = rowSource;
            Rules = ImmutableList.ValueOfOrEmpty(rules);
        }

        public string RowSource { get; private set; }
        public ImmutableList<ExtractedMetadataRule> Rules { get; private set; }

        public ExtractedMetadataRuleSet ChangeRules(IEnumerable<ExtractedMetadataRule> rules)
        {
            return ChangeProp(ImClone(this), im => im.Rules = ImmutableList.ValueOf(rules));
        }

        private enum ATTR
        {
            rowsource,
        }

        XmlSchema IXmlSerializable.GetSchema()
        {
            return null;
        }

        void IXmlSerializable.ReadXml(XmlReader reader)
        {
            if (Rules != null)
            {
                throw new InvalidOperationException();
            }
            
        }

        void IXmlSerializable.WriteXml(XmlWriter writer)
        {
            throw new NotImplementedException();
        }
    }
}