/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// XML serializable MappedList for use with lists that must be
    /// stored in the program settings.
    /// </summary>
    /// <typeparam name="TKey">Type of the key used in the map</typeparam>
    /// <typeparam name="TValue">Type stored in the collection</typeparam>
    public class XmlMappedList<TKey, TValue>
        : MappedList<TKey, TValue>, IXmlSerializable
        where TValue : IKeyContainer<TKey>, IXmlSerializable
    {
        /// <summary>
        /// Monotonically increasing revision number, supporting the ability
        /// to upgrade the elements in a settings list.
        /// </summary>
        public int RevisionIndex { get; set; }

        #region IXmlSerializable Members

        /// <summary>
        /// Provides a place to handle any post read validation or upgrading
        /// of values.
        /// </summary>
        protected virtual void ValidateLoad()
        {            
        }

        private enum EL
        {
            revision
        }

        private enum ATTR
        {
            index
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            bool isEmpty = reader.IsEmptyElement;

            // Read past the property element
            reader.Read();

            // For empty lists in Settings.Default
            if (isEmpty)
                return;

            if (reader.IsStartElement(EL.revision))
            {
                RevisionIndex = reader.GetIntAttribute(ATTR.index);
                reader.Read();  // Consume tag
            }
            var helpers = GetXmlElementHelpers();
            var helper = reader.FindHelper(helpers);

            // Read list items
            List<TValue> list = new List<TValue>();
            if (helper != null)
            {
                reader.ReadElements(list, helpers);
            }
            else
            {
                // Support for v0.1 format
                if (reader.IsEmptyElement)
                    reader.Read();
                else
                {
                    // Try to get through the elements with whatever
                    // names they were given, based on class names.
                    helper = new XmlElementHelper<TValue>();
                    reader.ReadStartElement();  // <ArrayOfType
                    while (reader.IsStartElement())
                        list.Add(helper.Deserialize(reader));   // <Type
                    reader.ReadEndElement();
                }
            }
            Clear();
            AddRange(list);                

            // Perform final list specific updates
            ValidateLoad();
        }

        public void WriteXml(XmlWriter writer)
        {
            try
            {
                // Write non-zero revision index
                if (RevisionIndex != 0)
                {
                    writer.WriteStartElement(EL.revision);
                    writer.WriteAttribute(ATTR.index, RevisionIndex);
                    writer.WriteEndElement();
                }
                // Write child elements directly into the property tag.
                writer.WriteElements(this, GetXmlElementHelpers());
            }
            catch (Exception ex)
            {
                // System.Xml will unfortunately swallow this exception when we rethrow it,
                // in the context of saving a settings list to user.config.
                // So we have to save it and throw it again after Settings.Default.Save is
                // complete - see SkylineWindow.OnClosing.
                Settings.Default.SaveException = ex;
                throw;
            }
        }

        protected virtual IXmlElementHelper<TValue>[] GetXmlElementHelpers()
        {
            return new IXmlElementHelper<TValue>[] { new XmlElementHelper<TValue>() };
        }

        #endregion // IXmlSerializable Members
    }

    [XmlRoot("dictionary")]
    public class SerializableDictionary<TKey, TValue>
        : Dictionary<TKey, TValue>, IXmlSerializable
    {
        #region IXmlSerializable Members
        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            XmlSerializer keySerializer = new XmlSerializer(typeof(TKey));
            XmlSerializer valueSerializer = new XmlSerializer(typeof(TValue));

            bool wasEmpty = reader.IsEmptyElement;
            reader.Read();

            if (wasEmpty)
                return;

            while (reader.NodeType != XmlNodeType.EndElement)
            {
                reader.ReadStartElement("item"); // Not L10N

                reader.ReadStartElement("key"); // Not L10N
                TKey key = (TKey)keySerializer.Deserialize(reader);
                reader.ReadEndElement();

                reader.ReadStartElement("value"); // Not L10N
                TValue value = (TValue)valueSerializer.Deserialize(reader);
                reader.ReadEndElement();

                Add(key, value);

                reader.ReadEndElement();
                reader.MoveToContent();
            }
            reader.ReadEndElement();
        }

        public void WriteXml(XmlWriter writer)
        {
            XmlSerializer keySerializer = new XmlSerializer(typeof(TKey));
            XmlSerializer valueSerializer = new XmlSerializer(typeof(TValue));

            foreach (TKey key in Keys)
            {
                writer.WriteStartElement("item"); // Not L10N

                writer.WriteStartElement("key"); // Not L10N
                keySerializer.Serialize(writer, key);
                writer.WriteEndElement();

                writer.WriteStartElement("value"); // Not L10N
                TValue value = this[key];
                valueSerializer.Serialize(writer, value);
                writer.WriteEndElement();

                writer.WriteEndElement();
            }
        }
        #endregion
    }

    /// <summary>
    /// A set of extension functions on <see cref="XmlWriter"/> and
    /// <see cref="XmlReader"/> to simplify common tasks in XML serialization
    /// code.
    /// </summary>
    public static class XmlUtil
    {
        public const string XML_DIRECTIVE = "<?xml version=\"1.0\" encoding=\"utf-16\"?>\r\n";  // Not L10N

        public static string ToAttr<TStruct>(TStruct? value)
            where TStruct : struct
        {
            return value == null ? null : value.ToString();
        }

        public static void WriteAttributeString(this XmlWriter writer, Enum name, string value)
        {
// ReSharper disable SpecifyACultureInStringConversionExplicitly
            writer.WriteAttributeString(name.ToString(), value);
// ReSharper restore SpecifyACultureInStringConversionExplicitly
        }

        public static void WriteAttributeNullable<TStruct>(this XmlWriter writer, Enum name, TStruct? value)
            where TStruct : struct
        {
            if (value.HasValue)
                writer.WriteAttributeString(name, value.ToString());
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

        public static void WriteAttributeNullableRoundTrip(this XmlWriter writer, Enum name, double? value)
        {
            if (value.HasValue)
                writer.WriteAttributeString(name, value.Value.ToString("G17", CultureInfo.InvariantCulture)); // Not L10N
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

        public static void WriteAttributeNullable<TStruct>(this XmlWriter writer, string name, TStruct? value)
            where TStruct : struct
        {
            if (value.HasValue)
                writer.WriteAttributeString(name, value.ToString());
        }

        /// <summary>
        /// Always writes a bool value, if one is present, or nothing for a null value.
        /// </summary>
        public static void WriteAttributeNullable(this XmlWriter writer, string name, bool? value)
        {
            if (value.HasValue)
                writer.WriteAttribute(name, value.Value, !value.Value);
        }

        public static void WriteAttributeNullable(this XmlWriter writer, string name, int? value)
        {
            if (value.HasValue)
                writer.WriteAttribute(name, value.Value);
        }

        public static void WriteAttributeNullable(this XmlWriter writer, string name, float? value)
        {
            if (value.HasValue)
                writer.WriteAttribute(name, value.Value);
        }

        public static void WriteAttributeNullable(this XmlWriter writer, string name, double? value)
        {
            if (value.HasValue)
                writer.WriteAttribute(name, value.Value);
        }

        public static void WriteAttributeIfString(this XmlWriter writer, string name, string value)
        {
            if (!string.IsNullOrEmpty(value))
                writer.WriteAttributeString(name, value);
        }

        public static void WriteAttribute<TAttr>(this XmlWriter writer, string name, TAttr value)
        {
            writer.WriteAttributeString(name, value.ToString());
        }

        public static void WriteAttribute<TAttr>(this XmlWriter writer, string name, TAttr value, TAttr defaultValue)
        {
            if (!Equals(value, defaultValue))
                writer.WriteAttributeString(name, value.ToString());
        }

        public static void WriteAttribute(this XmlWriter writer, string name, bool value)
        {
            WriteAttribute(writer, name, value, false);
        }

        public static void WriteAttribute(this XmlWriter writer, string name, bool value, bool defaultValue)
        {
            if (value != defaultValue)
                writer.WriteAttributeString(name, value.ToString(CultureInfo.InvariantCulture).ToLowerInvariant());
        }

        public static void WriteAttribute(this XmlWriter writer, string name, int value)
        {
            writer.WriteAttributeString(name, value.ToString(CultureInfo.InvariantCulture));
        }

        public static void WriteAttribute(this XmlWriter writer, string name, double value)
        {
            writer.WriteAttributeString(name, value.ToString(CultureInfo.InvariantCulture));
        }

        public static void WriteAttributeRoundTrip(this XmlWriter writer, string name, double value)
        {
            writer.WriteAttribute(name, value.ToString("G17", CultureInfo.InvariantCulture)); // Not L10N
        }

        public static void WriteAttribute(this XmlWriter writer, string name, float value)
        {
            writer.WriteAttributeString(name, value.ToString(CultureInfo.InvariantCulture));
        }

        public static void WriteElement<TChild>(this XmlWriter writer, TChild child)
            where TChild : IXmlSerializable
        {
            var helper = new XmlElementHelper<TChild>();
            writer.WriteElement(helper.ElementNames[0], child);
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

        public static void WriteElementList<TItem>(this XmlWriter writer, Enum name, IEnumerable<TItem> list)
            where TItem : IXmlSerializable
        {
            writer.WriteElementList(name, list, new XmlElementHelper<TItem>());
        }

        public static void WriteElementList<TItem>(this XmlWriter writer, Enum name, IEnumerable<TItem> list,
            params IXmlElementHelper<TItem>[] helpers)
            where TItem : IXmlSerializable
        {
            writer.WriteStartElement(name);
            writer.WriteElements(list, helpers);
            writer.WriteEndElement();
        }

        public static void WriteElements<TItem>(this XmlWriter writer, IEnumerable<TItem> list)
            where TItem : IXmlSerializable
        {
            writer.WriteElements(list, new XmlElementHelper<TItem>());
        }

        public static void WriteElements<TItem>(this XmlWriter writer, IEnumerable<TItem> list,
            params IXmlElementHelper<TItem>[] helpers)
            where TItem : IXmlSerializable
        {
            foreach (TItem item in list)
            {
                if (Equals(item, default(TItem)))
                    throw new InvalidDataException(Resources.XmlUtil_WriteElements_Attempt_to_serialize_list_missing_an_element);
                IXmlElementHelper<TItem> helper = FindHelper(item, helpers);
                if (helper == null)
                    throw new InvalidOperationException(string.Format(Resources.XmlUtil_WriteElements_Attempt_to_serialize_list_containing_invalid_type__0__, typeof(TItem)));
                writer.WriteElement(helper.ElementNames[0], item);
            }
        }

        public static IXmlElementHelper<TItem> FindHelper<TItem>(TItem item, IXmlElementHelper<TItem>[] helpers)
        {
            foreach (IXmlElementHelper<TItem> helper in helpers)
            {
                if (helper.IsType(item))
                    return helper;
            }
            return null;
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
                throw new InvalidDataException(string.Format(Resources.XmlUtil_GetAttribute_The_value__0__is_not_valid_for_the_attribute__1__, value, name), x);
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
                throw new InvalidDataException(string.Format(Resources.XmlUtil_GetAttribute_The_value__0__is_not_valid_for_the_attribute__1__, value, name), x);
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

        public enum EnumCase { unkown, lower, upper }

        public static TAttr GetEnumAttribute<TAttr>(this XmlReader reader, Enum name, TAttr defaultValue)
        {
            return reader.GetEnumAttribute(name, defaultValue, EnumCase.unkown);
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
                    throw new InvalidDataException(string.Format(Resources.XmlUtil_GetAttribute_The_value__0__is_not_valid_for_the_attribute__1__, value, name), x);
                }
            }
            return defaultValue;
        }

        public static TAttr GetEnumAttribute<TAttr>(this XmlReader reader, string name, TAttr defaultValue)
        {
            return reader.GetEnumAttribute(name, defaultValue, EnumCase.unkown);
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
                    throw new InvalidDataException(string.Format(Resources.XmlUtil_GetAttribute_The_value__0__is_not_valid_for_the_attribute__1__, value, name), x);
                }
            }
            return defaultValue;
        }

        private static string GetEnumString(string value, EnumCase enumCase)
        {
            switch (enumCase)
            {
                case EnumCase.lower:
                    return value.ToLowerInvariant();
                case EnumCase.upper:
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

        public static void ReadElementList<TItem>(this XmlReader reader, Enum name, List<TItem> list)
        {
            reader.ReadElementList(name, list, new XmlElementHelper<TItem>());
        }

        public static void ReadElementList<TItem>(this XmlReader reader, Enum name, List<TItem> list,
            params IXmlElementHelper<TItem>[] helpers)
        {
            if (reader.IsStartElement(name))
            {
                if (reader.IsEmptyElement)
                    reader.ReadStartElement(name);
                else
                {

                    reader.ReadStartElement(name);
                    reader.ReadElements(list, helpers);
                    reader.ReadEndElement();
                }
            }            
        }

        public static void ReadElements<TItem>(this XmlReader reader, List<TItem> list)
        {
            reader.ReadElements(list, new XmlElementHelper<TItem>());
        }

        public static void ReadElements<TItem>(this XmlReader reader, List<TItem> list,
            params IXmlElementHelper<TItem>[] helpers)
        {
            IXmlElementHelper<TItem> helper;
            while ((helper = reader.FindHelper(helpers)) != null)
                list.Add(helper.Deserialize(reader));            
        }

        public static IXmlElementHelper<TItem> FindHelper<TItem>(this XmlReader reader,
            IEnumerable<IXmlElementHelper<TItem>> helpers)
        {
            foreach (IXmlElementHelper<TItem> helper in helpers)
            {
                if (reader.IsStartElement(helper.ElementNames))
                    return helper;
            }
            return null;
        }

        public static TObj Deserialize<TObj>(this XmlReader reader, TObj objNew)
            where TObj : IXmlSerializable
        {
            objNew.ReadXml(reader);
            return objNew;
        }

        public static TObj DeserializeElement<TObj>(this XmlReader reader)
            where TObj : class
        {
            return DeserializeElement<TObj>(reader, null);
        }

        public static TObj DeserializeElement<TObj>(this XmlReader reader, Enum name)
            where TObj : class
        {
// ReSharper disable SpecifyACultureInStringConversionExplicitly
            var helper = new XmlElementHelper<TObj>(name == null ? null : name.ToString());
// ReSharper restore SpecifyACultureInStringConversionExplicitly
            if (reader.IsStartElement(helper.ElementNames))
                return helper.Deserialize(reader);
            return null;
        }

        public static string GetInvalidDataMessage(string path, Exception x)
        {
            StringBuilder sb = new StringBuilder();
            int line, column;
            if (!TryGetXmlLineColumn(x.Message, out line, out column))
                sb.Append(x.Message);
            else
            {
                if (line != 0)
                    sb.Append(
                        string.Format(
                            Resources.
                                XmlUtil_GetInvalidDataMessage_The_file_contains_an_error_on_line__0__at_column__1__,
                            line, column));
                else
                {
                    if (column == 0 && IsSmallAndWhiteSpace(path))
                    {
                        var message = TextUtil.LineSeparate(Resources.XmlUtil_GetInvalidDataMessage_The_file_is_empty,
                            Resources.XmlUtil_GetInvalidDataMessage_It_may_have_been_truncated_during_file_transfer);
                        return message;
                    }

                    return Resources.XmlUtil_GetInvalidDataMessage_The_file_does_not_appear_to_be_valid_XML;
                }
            }
            while (x != null)
            {
                if (x is InvalidDataException)
                {
                    sb.AppendLine().Append(x.Message);
                    break;
                }
                else if (x is VersionNewerException)
                {
                    sb = new StringBuilder(x.Message);
                    break;
                }
                x = x.InnerException;
            }
            return sb.ToString();
        }

        public static readonly Regex REGEX_XML_ERROR = new Regex(@"\((\d+), (\d+)\)"); // Not L10N

        public static bool TryGetXmlLineColumn(string message, out int line, out int column)
        {
            line = column = 0;

            if (!message.Contains("XML")) // Not L10N
                return false;

            Match match = REGEX_XML_ERROR.Match(message);
            if (!match.Success)
                return false;
            if (!int.TryParse(match.Groups[1].Value, out line))
                return false;
            if (!int.TryParse(match.Groups[2].Value, out column))
                return false;
            return true;
        }

        /// <summary>
        /// Returns true, if a file is less than or equal to 10 characters and
        /// all whitespace, or empty.
        /// </summary>
        /// <param name="path">Path to the file</param>
        /// <returns>True if small and whitespace</returns>
        private static bool IsSmallAndWhiteSpace(string path)
        {
            if (new FileInfo(path).Length > 10)
                return false;
            try
            {
                string text = File.ReadAllText(path);
                foreach (char c in text)
                {
                    if (!char.IsWhiteSpace(c))
                        return false;
                }
            }
            catch (Exception)
            {
                return false;   // Can't tell, really
            }
            return true;
        }

    }

    public interface IXmlElementHelper<out TElem>
    {
        string[] ElementNames { get; }

        TElem Deserialize(XmlReader reader);

        bool IsType(object item);
    }

    /// <summary>
    /// Helper for reading and writing XML elements in <see cref="IXmlSerializable"/>
    /// implementations.  If the type supplied has the <see cref="XmlRootAttribute"/>
    /// then it is self-naming.  Otherwise a name for the element must be supplied.
    /// </summary>
    /// <typeparam name="TElem">Type for which this helper is used</typeparam>
    public sealed class XmlElementHelper<TElem> : IXmlElementHelper<TElem>
    {
        /// <summary>
        /// Constructor for self-named types that have the
        /// <see cref="XmlRootAttribute"/>.
        /// </summary>
        public XmlElementHelper()
            : this(null)
        {
        }

        /// <summary>
        /// Constructor for types that are not self-naming.
        /// </summary>
        /// <param name="elementName">The element name to use</param>
        public XmlElementHelper(string elementName)
        {
            if (elementName != null)
                ElementNames = new[] {elementName};
            else
            {
                Type type = typeof(TElem);
                XmlRootAttribute[] attrs = (XmlRootAttribute[])
                    type.GetCustomAttributes(typeof(XmlRootAttribute), false);

                if (attrs.Length < 1)
                    throw new InvalidOperationException(
                        string.Format(Resources.XmlElementHelper_XmlElementHelper_The_class__0__has_no__1__,
                                      type.FullName, typeof (XmlRootAttribute).Name));

                XmlRootAliasAttribute[] aliases = (XmlRootAliasAttribute[])
                    type.GetCustomAttributes(typeof(XmlRootAliasAttribute), false);

                int len = attrs.Length + aliases.Length;
                ElementNames = new string[len];
                int i = 0;
                foreach (var attr in attrs)
                    ElementNames[i++] = attr.ElementName;
                foreach (var alias in aliases)
                    ElementNames[i++] = alias.ElementName;
            }
        }

        /// <summary>
        /// The element name for this type
        /// </summary>
        public string[] ElementNames { get; private set; }

        /// <summary>
        /// Deserializes an instance of this type from a <see cref="XmlReader"/>.
        /// </summary>
        /// <param name="reader">The reader from which to deserialize</param>
        /// <returns>An instance of the supported type</returns>
        public TElem Deserialize(XmlReader reader)
        {
            // Unit tests depend on exceptions being thrown.
//            try
//            {
                return (TElem)typeof(TElem).InvokeMember("Deserialize", // Not L10N
                                                 BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod,
                                                 null, null, new object[] { reader }, CultureInfo.InvariantCulture); 
//            }
//            catch (TargetInvocationException)
//            {
//                Debug.Assert(false, string.Format("Failed calling Deserialize() method for {0} element.", ElementName));
//                throw;
//            }
        }

        /// <summary>
        /// Checks for exact type match with the type for which the helper is
        /// created.
        /// </summary>
        /// <param name="item">An item to be serialized</param>
        /// <returns>True if this helper can serialize it</returns>
        public bool IsType(object item)
        {
            return typeof(TElem) == item.GetType();
        }
    }

    public sealed class XmlElementHelperSuper<TElem, TSup> : IXmlElementHelper<TSup>
        where TElem : TSup
    {
        private readonly XmlElementHelper<TElem> _helper;

        public XmlElementHelperSuper()
        {
            _helper = new XmlElementHelper<TElem>();
        }

        public string[] ElementNames
        {
            get { return _helper.ElementNames; }
        }

        public TSup Deserialize(XmlReader reader)
        {
            return _helper.Deserialize(reader);
        }

        public bool IsType(object item)
        {
            return _helper.IsType(item);
        }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    internal sealed class XmlRootAliasAttribute : Attribute
    {
        public XmlRootAliasAttribute()
        {
        }

        public XmlRootAliasAttribute(string elementName)
        {
            ElementName = elementName;
        }

        public string ElementName { get; set; }
    }

    /// <summary>
    /// For writing XML to memory with UTF8 encoding, because <see cref="StringWriter.Encoding"/>
    /// is read-only.
    /// </summary>
    public sealed class XmlStringWriter : StringWriter
    {
        public override Encoding Encoding { get { return Encoding.UTF8; } }
    }


    public class VersionNewerException : Exception
    {
        public VersionNewerException(string message)
            : base(message)
        {
        }
    }
}