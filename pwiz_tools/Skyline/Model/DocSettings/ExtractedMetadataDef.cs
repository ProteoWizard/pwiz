using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.DocSettings
{
    [XmlRoot("extracted_metadata")]
    public class ExtractedMetadataDef : XmlNamedElement, IXmlSerializable
    {
        public string SourceColumn { get; private set; }

        public ExtractedMetadataDef ChangeSourceColumn(string value)
        {
            return ChangeProp(ImClone(this), im => im.SourceColumn = value);
        }

        public string MatchRegularExpression { get; private set; }

        public ExtractedMetadataDef ChangeMatchRegularExpression(string value)
        {
            return ChangeProp(ImClone(this), im => im.MatchRegularExpression = value);
        }
        public string ValueIfMatch { get; private set; }

        public ExtractedMetadataDef ChangeValueIfMatch(string value)
        {
            return ChangeProp(ImClone(this), im => im.ValueIfMatch = value);
        }
        public string ValueIfNoMatch { get; private set; }

        public ExtractedMetadataDef ChangeValueIfNoMatch(string value)
        {
            return ChangeProp(ImClone(this), im => im.ValueIfNoMatch = value);
        }
        public string TargetColumn { get; private set; }

        public ExtractedMetadataDef ChangeTargetColumn(string value)
        {
            return ChangeProp(ImClone(this), im => im.TargetColumn = value);
        }
        protected bool Equals(ExtractedMetadataDef other)
        {
            return SourceColumn == other.SourceColumn && MatchRegularExpression == other.MatchRegularExpression &&
                   ValueIfMatch == other.ValueIfMatch &&
                   ValueIfNoMatch == other.ValueIfNoMatch && TargetColumn == other.TargetColumn;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ExtractedMetadataDef) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (MatchRegularExpression != null ? MatchRegularExpression.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (SourceColumn != null ? SourceColumn.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (ValueIfMatch != null ? ValueIfMatch.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (ValueIfNoMatch != null ? ValueIfNoMatch.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (TargetColumn != null ? TargetColumn.GetHashCode() : 0);
                return hashCode;
            }
        }

        #region Serialization
        private enum ATTR
        {
            source_column,
            match_regular_expression,
            value_if_match,
            value_if_no_match,
            target_column,
        }

        XmlSchema IXmlSerializable.GetSchema()
        {
            return null;
        }

        public override void ReadXml(XmlReader reader)
        {
            base.ReadXml(reader);
            MatchRegularExpression = reader.GetAttribute(ATTR.match_regular_expression);
            ValueIfMatch = reader.GetAttribute(ATTR.value_if_match);
            ValueIfNoMatch = reader.GetAttribute(ATTR.value_if_no_match);
            SourceColumn = reader.GetAttribute(ATTR.source_column);
            TargetColumn = reader.GetAttribute(ATTR.target_column);
        }

        public override void WriteXml(XmlWriter writer)
        {
            base.WriteXml(writer);
            writer.WriteAttributeIfString(ATTR.source_column, SourceColumn);
            writer.WriteAttributeIfString(ATTR.match_regular_expression, MatchRegularExpression);

            writer.WriteAttributeIfString(ATTR.value_if_match, ValueIfMatch);
            writer.WriteAttributeIfString(ATTR.value_if_no_match, ValueIfNoMatch);
            writer.WriteAttribute(ATTR.target_column, TargetColumn);
        }

        public static ExtractedMetadataDef Deserialize(XmlReader reader)
        {
            var extractedMetadataDef = new ExtractedMetadataDef();
            ((IXmlSerializable) extractedMetadataDef).ReadXml(reader);
            return extractedMetadataDef;
        }

        #endregion Serialization
    }
}