using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.DocSettings
{
    [XmlRoot("audit_log_entry")]
    public class AuditLogRow : XmlNamedElement
    {
        public AuditLogRow(PropertyPath propertyPath, DateTime timeStamp, object oldValue, object newValue)
        {
            PropertyPath = propertyPath;
            OldValue = oldValue;
            NewValue = newValue;
            TimeStamp = timeStamp;
        }

        private AuditLogRow()
        {

        }

        public PropertyPath PropertyPath { get; private set; }
        public DateTime TimeStamp { get; private set; }

        public object OldValue { get; private set; }
        public object NewValue { get; private set; }
        

        private enum ATTR
        {
            property_path,
            time_stamp
        }

        private enum EL
        {
            old_value,
            new_value
        }


        public static AuditLogRow Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new AuditLogRow());
        }

        private Type PropertyPathToType(PropertyPath path)
        {
            if (path.IsLookup || path.IsUnboundLookup)
                path = path.Parent;

            var type = Type.GetType(path.ToString());
            if (type != null)
                return type;

            var prop = PropertyPathToType(path.Parent).GetProperty(path.Name);
            if (prop == null)
                return null;

            type = prop.PropertyType;

            if (type.HasElementType)
                return type.GetElementType();
            else if (type.IsGenericType)
                return type.GenericTypeArguments[0];

            return type;
        }

        public override void ReadXml(XmlReader reader)
        {
            PropertyPath = PropertyPath.Parse(reader.GetAttribute(ATTR.property_path));
            TimeStamp = DateTime.Parse(reader.GetAttribute(ATTR.time_stamp));
            reader.ReadStartElement();

            var name = "pwiz.Skyline.Model.DocSettings." + (PropertyPath.IsLookup ? PropertyPath.Parent.ToString() : PropertyPath.ToString());
            var newPath = PropertyPath.Parse(name);
            var type = PropertyPathToType(newPath);

            if (typeof(IXmlSerializable).IsAssignableFrom(type))
            {
                var methodInfo = typeof(XmlUtil).GetMethod("DeserializeElement", new[] {typeof(XmlReader)});
                // ReSharper disable once PossibleNullReferenceException
                methodInfo = methodInfo.MakeGenericMethod(type);
                OldValue = methodInfo.Invoke(null, new[] {reader});
                NewValue = methodInfo.Invoke(null, new[] {reader});
            }
            else
            {
                var oldStr = reader.ReadElementContentAsString();
                var newStr = reader.ReadElementContentAsString();

                if (type.IsEnum)
                {
                    OldValue = Enum.Parse(type, oldStr);
                    NewValue = Enum.Parse(type, newStr);
                }
                else
                {
                    OldValue = Convert.ChangeType(oldStr, type);
                    NewValue = Convert.ChangeType(newStr, type);
                }
            }

            reader.Read();
        }

        public override void WriteXml(XmlWriter writer)
        {
            writer.WriteAttribute(ATTR.property_path, PropertyPath.ToString());
            writer.WriteAttribute(ATTR.time_stamp, TimeStamp.ToString(CultureInfo.InvariantCulture));

            var oldVal = OldValue as IXmlSerializable;
            var newVal = NewValue as IXmlSerializable;
            if (oldVal != null && newVal != null)
            {
                var method = typeof(XmlUtil).GetMethods().Single(m => m.Name == "WriteElement" && m.ContainsGenericParameters);
                method = method.MakeGenericMethod(oldVal.GetType());

                method.Invoke(null, new object[] { writer, oldVal });
                method.Invoke(null, new object[] { writer, newVal });
            }
            else if (oldVal == null && newVal == null)
            {
                writer.WriteElementString(EL.old_value, OldValue != null ? OldValue.ToString() : string.Empty);
                writer.WriteElementString(EL.new_value, NewValue != null ? NewValue.ToString() : string.Empty);
            }
        }

        protected bool Equals(AuditLogRow other)
        {
            return base.Equals(other) && Equals(PropertyPath, other.PropertyPath) && Equals(OldValue, other.OldValue) &&
                   Equals(NewValue, other.NewValue) && TimeStamp.Equals(other.TimeStamp);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((AuditLogRow) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = base.GetHashCode();
                hashCode = (hashCode * 397) ^ (PropertyPath != null ? PropertyPath.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (OldValue != null ? OldValue.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (NewValue != null ? NewValue.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ TimeStamp.GetHashCode();
                return hashCode;
            }
        }
    }
}