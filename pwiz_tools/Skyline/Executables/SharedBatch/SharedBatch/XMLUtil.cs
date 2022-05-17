using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;

namespace SharedBatch
{
    /// <summary>
    /// A set of extension functions on <see cref="XmlWriter"/> and
    /// <see cref="XmlReader"/> to simplify common tasks in XML serialization
    /// code.
    /// </summary>
    public static class XmlUtil
    {
        //public const string XML_DIRECTIVE = "<?xml version=\"1.0\" encoding=\"utf-16\"?>\r\n";  // Not L10N

        public static void WriteAttributeString(this XmlWriter writer, Enum name, string value)
        {
            // ReSharper disable SpecifyACultureInStringConversionExplicitly
            writer.WriteAttributeString(name.ToString(), value);
            // ReSharper restore SpecifyACultureInStringConversionExplicitly
        }

        /// <summary>
        /// Always writes a bool value, if one is present, or nothing for a null value.
        /// </summary>
        public static void WriteAttributeNullable(this XmlWriter writer, Enum name, bool? value)
        {
            if (value.HasValue)
                writer.WriteAttribute(name, value.Value, !value.Value);
        }

        public static void WriteAttributeNullable(this XmlWriter writer, Enum name, int? value)
        {
            if (value.HasValue)
                writer.WriteAttribute(name, value.Value);
        }

        public static void WriteAttributeNullable(this XmlWriter writer, Enum name, float? value)
        {
            if (value.HasValue)
                writer.WriteAttribute(name, value.Value);
        }

        public static void WriteAttributeNullable(this XmlWriter writer, Enum name, double? value)
        {
            if (value.HasValue)
                writer.WriteAttribute(name, value.Value);
        }

        public static void WriteAttributeIfString(this XmlWriter writer, Enum name, string value)
        {
            if (!string.IsNullOrEmpty(value))
                writer.WriteAttributeString(name, value);
        }

        public static void WriteAttribute<TAttr>(this XmlWriter writer, Enum name, TAttr value)
        {
            writer.WriteAttributeString(name, value.ToString());
        }

        public static void WriteAttribute<TAttr>(this XmlWriter writer, Enum name, TAttr value, TAttr defaultValue)
        {
            if (!Equals(value, defaultValue))
                writer.WriteAttributeString(name, value.ToString());
        }

        public static void WriteAttribute(this XmlWriter writer, Enum name, bool value)
        {
            WriteAttribute(writer, name, value, false);
        }

        public static void WriteAttribute(this XmlWriter writer, Enum name, bool value, bool defaultValue)
        {
            if (value != defaultValue)
                writer.WriteAttributeString(name, value.ToString(CultureInfo.InvariantCulture).ToLowerInvariant());
        }

        public static void WriteAttribute(this XmlWriter writer, Enum name, int value)
        {
            writer.WriteAttributeString(name, value.ToString(CultureInfo.InvariantCulture));
        }

        public static void WriteAttribute(this XmlWriter writer, Enum name, double value)
        {
            writer.WriteAttributeString(name, value.ToString(CultureInfo.InvariantCulture));
        }
        
        public static void WriteAttribute(this XmlWriter writer, Enum name, decimal value)
        {
            writer.WriteAttributeString(name, value.ToString(CultureInfo.InvariantCulture));
        }

        public static void WriteAttribute(this XmlWriter writer, Enum name, float value)
        {
            writer.WriteAttributeString(name, value.ToString(CultureInfo.InvariantCulture));
        }

        public static void WriteStartElement(this XmlWriter writer, Enum name)
        {
            // ReSharper disable SpecifyACultureInStringConversionExplicitly
            writer.WriteStartElement(name.ToString());
            // ReSharper restore SpecifyACultureInStringConversionExplicitly
        }

        public static void WriteElementString(this XmlWriter writer, Enum name, double child)
        {
            writer.WriteElementString(name, child.ToString(CultureInfo.InvariantCulture));
        }

        public static void WriteElementString(this XmlWriter writer, Enum name, float child)
        {
            writer.WriteElementString(name, child.ToString(CultureInfo.InvariantCulture));
        }

        public static void WriteElementString<TChild>(this XmlWriter writer, Enum name, TChild child)
        {
            writer.WriteStartElement(name);
            writer.WriteString(child.ToString());
            writer.WriteEndElement();
        }

        public static void WriteElementString(this XmlWriter writer, string name, double child)
        {
            writer.WriteElementString(name, child.ToString(CultureInfo.InvariantCulture));
        }

        public static void WriteElementString(this XmlWriter writer, string name, float child)
        {
            writer.WriteElementString(name, child.ToString(CultureInfo.InvariantCulture));
        }

        public static void WriteElementString<TChild>(this XmlWriter writer, string name, TChild child)
        {
            writer.WriteStartElement(name);
            writer.WriteString(child.ToString());
            writer.WriteEndElement();
        }

        public static void WriteElement(this XmlWriter writer, Enum name, IXmlSerializable child)
        {
            // ReSharper disable SpecifyACultureInStringConversionExplicitly
            writer.WriteElement(name.ToString(), child);
            // ReSharper restore SpecifyACultureInStringConversionExplicitly
        }

        public static void WriteElement(this XmlWriter writer, string name, IXmlSerializable child)
        {
            writer.WriteStartElement(name);
            child.WriteXml(writer);
            writer.WriteEndElement();
        }

        public static void WriteElementList<TItem>(this XmlWriter writer, Enum name, string nameItem,
                                                   IEnumerable<TItem> list)
            where TItem : IXmlSerializable
        {
            writer.WriteStartElement(name);
            foreach (TItem item in list)
                writer.WriteElement(nameItem, item);
            writer.WriteEndElement();
        }



        public static string GetAttribute(this XmlReader reader, Enum name)
        {
            // ReSharper disable SpecifyACultureInStringConversionExplicitly
            return reader.GetAttribute(name.ToString());
            // ReSharper restore SpecifyACultureInStringConversionExplicitly
        }

        public static TOutput? GetAttribute<TOutput>(this XmlReader reader, Enum name,
                                                     Converter<string, TOutput> converter)
            where TOutput : struct
        {
            string value = reader.GetAttribute(name);
            if (value == null)
                return null;
            try
            {
                return converter(value);
            }
            catch (Exception x)
            {
                throw new InvalidDataException(string.Format("The value {0} is not valid for the attribute {1}", value, name), x);
            }
        }

        public static TOutput? GetAttribute<TOutput>(this XmlReader reader, string name,
                                                     Converter<string, TOutput> converter)
            where TOutput : struct
        {
            string value = reader.GetAttribute(name);
            if (value == null)
                return null;
            try
            {
                return converter(value);
            }
            catch (Exception x)
            {
                throw new InvalidDataException(string.Format("The value {0} is not valid for the attribute {1}", value, name), x);
            }
        }

        private static bool ConvertToBoolean(string attribute)
        {
            return Convert.ToBoolean(attribute, CultureInfo.InvariantCulture);
        }

        public static bool? GetNullableBoolAttribute(this XmlReader reader, Enum name)
        {
            return reader.GetAttribute(name, ConvertToBoolean);
        }

        public static bool GetBoolAttribute(this XmlReader reader, Enum name)
        {
            return reader.GetBoolAttribute(name, false);
        }

        public static bool GetBoolAttribute(this XmlReader reader, Enum name, bool defaultValue)
        {
            return reader.GetNullableBoolAttribute(name) ?? defaultValue;
        }

        public static bool? GetNullableBoolAttribute(this XmlReader reader, string name)
        {
            return reader.GetAttribute(name, ConvertToBoolean);
        }

        public static bool GetBoolAttribute(this XmlReader reader, string name)
        {
            return reader.GetBoolAttribute(name, false);
        }

        public static bool GetBoolAttribute(this XmlReader reader, string name, bool defaultValue)
        {
            return reader.GetNullableBoolAttribute(name) ?? defaultValue;
        }

        private static int ConvertToInt32(string attribute)
        {
            return Convert.ToInt32(attribute, CultureInfo.InvariantCulture);
        }

        public static int? GetNullableIntAttribute(this XmlReader reader, Enum name)
        {
            return reader.GetAttribute(name, ConvertToInt32);
        }

        public static int GetIntAttribute(this XmlReader reader, Enum name)
        {
            return reader.GetIntAttribute(name, 0);
        }

        public static int GetIntAttribute(this XmlReader reader, Enum name, int defaultValue)
        {
            return reader.GetNullableIntAttribute(name) ?? defaultValue;
        }

        public static int? GetNullableIntAttribute(this XmlReader reader, string name)
        {
            return reader.GetAttribute(name, ConvertToInt32);
        }

        public static int GetIntAttribute(this XmlReader reader, string name)
        {
            return reader.GetIntAttribute(name, 0);
        }

        public static int GetIntAttribute(this XmlReader reader, string name, int defaultValue)
        {
            return reader.GetNullableIntAttribute(name) ?? defaultValue;
        }

        private static double ConvertToDouble(string attribute)
        {
            return Convert.ToDouble(attribute, CultureInfo.InvariantCulture);
        }

        public static double? GetNullableDoubleAttribute(this XmlReader reader, Enum name)
        {
            return reader.GetAttribute(name, ConvertToDouble);
        }

        public static double GetDoubleAttribute(this XmlReader reader, Enum name)
        {
            return reader.GetDoubleAttribute(name, 0.0);
        }

        public static double GetDoubleAttribute(this XmlReader reader, Enum name, double defaultValue)
        {
            return reader.GetNullableDoubleAttribute(name) ?? defaultValue;
        }

        public static double? GetNullableDoubleAttribute(this XmlReader reader, string name)
        {
            return reader.GetAttribute(name, ConvertToDouble);
        }

        public static double GetDoubleAttribute(this XmlReader reader, string name)
        {
            return reader.GetDoubleAttribute(name, 0.0);
        }

        public static double GetDoubleAttribute(this XmlReader reader, string name, double defaultValue)
        {
            return reader.GetNullableDoubleAttribute(name) ?? defaultValue;
        }

        private static float ConvertToSingle(string attribute)
        {
            return Convert.ToSingle(attribute, CultureInfo.InvariantCulture);
        }

        public static float? GetNullableFloatAttribute(this XmlReader reader, Enum name)
        {
            return reader.GetAttribute(name, ConvertToSingle);
        }

        public static float GetFloatAttribute(this XmlReader reader, Enum name)
        {
            return reader.GetFloatAttribute(name, 0.0f);
        }

        public static float GetFloatAttribute(this XmlReader reader, Enum name, float defaultValue)
        {
            return reader.GetNullableFloatAttribute(name) ?? defaultValue;
        }

        public static float? GetNullableFloatAttribute(this XmlReader reader, string name)
        {
            return reader.GetAttribute(name, ConvertToSingle);
        }

        public static float GetFloatAttribute(this XmlReader reader, string name)
        {
            return reader.GetFloatAttribute(name, 0.0f);
        }

        public static float GetFloatAttribute(this XmlReader reader, string name, float defaultValue)
        {
            return reader.GetNullableFloatAttribute(name) ?? defaultValue;
        }

        public enum EnumCase { Unkown, Lower, Upper }

        public static TAttr GetEnumAttribute<TAttr>(this XmlReader reader, Enum name, TAttr defaultValue)
        {
            return reader.GetEnumAttribute(name, defaultValue, EnumCase.Unkown);
        }

        public static TAttr GetEnumAttribute<TAttr>(this XmlReader reader, Enum name, TAttr defaultValue, EnumCase enumCase)
        {
            string value = reader.GetAttribute(name);
            if (!string.IsNullOrEmpty(value))
            {
                try
                {
                    return (TAttr)Enum.Parse(typeof(TAttr), GetEnumString(value, enumCase));
                }
                catch (ArgumentException x)
                {
                    throw new InvalidDataException(string.Format("The value {0} is not valid for the attribute {1}", value, name), x);
                }
            }
            return defaultValue;
        }

        public static TAttr GetEnumAttribute<TAttr>(this XmlReader reader, string name, TAttr defaultValue)
        {
            return reader.GetEnumAttribute(name, defaultValue, EnumCase.Unkown);
        }

        public static TAttr GetEnumAttribute<TAttr>(this XmlReader reader, string name, TAttr defaultValue, EnumCase enumCase)
        {
            string value = reader.GetAttribute(name);
            if (!string.IsNullOrEmpty(value))
            {
                try
                {
                    return (TAttr)Enum.Parse(typeof(TAttr), GetEnumString(value, enumCase));
                }
                catch (ArgumentException x)
                {
                    throw new InvalidDataException(string.Format("The value {0} is not valid for the attribute {1}", value, name), x);
                }
            }
            return defaultValue;
        }

        private static string GetEnumString(string value, EnumCase enumCase)
        {
            switch (enumCase)
            {
                case EnumCase.Lower:
                    return value.ToLowerInvariant();
                case EnumCase.Upper:
                    return value.ToUpperInvariant();
                default:
                    return value;
            }
        }

        public static Type GetTypeAttribute(this XmlReader reader, Enum name)
        {
            var type = reader.GetAttribute(name);
            if (type == null)
                throw new InvalidDataException();
            return Type.GetType(type);
        }

        public static bool IsStartElement(this XmlReader reader, string[] names)
        {
            foreach (var name in names)
            {
                if (reader.IsStartElement(name))
                    return true;
            }
            return false;
        }

        public static bool IsStartElement(this XmlReader reader, Enum name)
        {
            // ReSharper disable SpecifyACultureInStringConversionExplicitly
            return reader.IsStartElement(name.ToString());
            // ReSharper restore SpecifyACultureInStringConversionExplicitly
        }

        public static void ReadStartElement(this XmlReader reader, Enum name)
        {
            // ReSharper disable SpecifyACultureInStringConversionExplicitly
            reader.ReadStartElement(name.ToString());
            // ReSharper restore SpecifyACultureInStringConversionExplicitly
        }

        public static double ReadElementContentAsDoubleInvariant(this XmlReader reader)
        {
            return Convert.ToDouble(reader.ReadElementContentAsString(), CultureInfo.InvariantCulture);
        }

        public static TObj Deserialize<TObj>(this XmlReader reader, TObj objNew)
            where TObj : IXmlSerializable
        {
            objNew.ReadXml(reader);
            return objNew;
        }

        public static readonly Regex RegexXmlError = new Regex(@"\((\d+), ?(\d+)\)"); // Not L10N

        public static bool TryGetXmlLineColumn(string message, out int line, out int column)
        {
            line = column = 0;

            if (!message.Contains("XML")) // Not L10N
                return false;

            Match match = RegexXmlError.Match(message);
            if (!match.Success)
                return false;
            if (!int.TryParse(match.Groups[1].Value, out line))
                return false;
            if (!int.TryParse(match.Groups[2].Value, out column))
                return false;
            return true;
        }

        public static bool ReadNextElement(XmlReader reader, string name)
        {
            if (reader.NodeType == XmlNodeType.Element && reader.Name.Equals(name))
                return true;
            do
            {
                if (!reader.Read())
                    return false;
            } while (reader.NodeType != XmlNodeType.Element);
            return reader.Name.Equals(name);
        }

        public static bool ReadUntilElement(XmlReader reader)
        {
            do
            {
                if (!reader.Read()) return false;
            } while (reader.NodeType != XmlNodeType.Element);
            return true;
        }

        public static bool IsEndElement(this XmlReader reader, string name)
        {
            return reader.NodeType == XmlNodeType.EndElement && reader.Name.Equals(name);
        }

        public static bool IsElement(this XmlReader reader, string name)
        {
            return reader.NodeType == XmlNodeType.Element && reader.Name.Equals(name);
        }
    }
}
