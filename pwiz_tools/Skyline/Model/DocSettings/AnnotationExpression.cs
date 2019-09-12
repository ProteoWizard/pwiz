using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using JetBrains.Annotations;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.DocSettings
{
    [XmlRoot("expression")]
    public class AnnotationExpression : Immutable, IXmlSerializable
    {
        private enum Attr
        {
            column,
            aggregate_op
        }

        public AnnotationExpression([NotNull] PropertyPath column)
        {
            if (column == null)
            {
                throw new ArgumentNullException();
            }
            Column = column;
        }

        public PropertyPath Column { get; private set; }
        public AggregateOperation AggregateOperation { get; private set; }

        public AnnotationExpression ChangeAggregateOperation(AggregateOperation aggregateOperation)
        {
            return ChangeProp(ImClone(this), im => im.AggregateOperation = aggregateOperation);
        }

        private AnnotationExpression()
        {
        }
        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            if (Column != null)
            {
                throw new InvalidOperationException();
            }

            Column = PropertyPath.Parse(reader.GetAttribute(Attr.column));
            string strAggregateOp = reader.GetAttribute(Attr.aggregate_op);
            if (strAggregateOp != null)
            {
                AggregateOperation = AggregateOperation.FromName(strAggregateOp);
            }

            reader.Read();
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteAttributeString(Attr.column, Column.ToString());
            if (null != AggregateOperation)
            {
                writer.WriteAttributeString(Attr.aggregate_op, AggregateOperation.Name);
            }
        }

        protected bool Equals(AnnotationExpression other)
        {
            return Equals(Column, other.Column) && Equals(AggregateOperation, other.AggregateOperation);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((AnnotationExpression) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Column != null ? Column.GetHashCode() : 0) * 397) ^ (AggregateOperation != null ? AggregateOperation.GetHashCode() : 0);
            }
        }

        public static AnnotationExpression Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new AnnotationExpression());
        }
    }
}
