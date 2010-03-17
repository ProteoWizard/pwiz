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
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

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
            // Read past the property element
            reader.Read();

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

        protected virtual IXmlElementHelper<TValue>[] GetXmlElementHelpers()
        {
            return new [] { new XmlElementHelper<TValue>() };
        }

        #endregion // IXmlSerializable Members
    }

    /// <summary>
    /// A set of extension functions on <see cref="XmlWriter"/> and
    /// <see cref="XmlReader"/> to simplify common tasks in XML serialization
    /// code.
    /// </summary>
    public static class XmlUtil
    {
        public static string ToAttr<T>(T? value)
            where T : struct
        {
            return value == null ? null : value.ToString();
        }

        public static void WriteAttributeString(this XmlWriter writer, Enum name, string value)
        {
            writer.WriteAttributeString(name.ToString(), value);
        }

        public static void WriteAttributeNullable<T>(this XmlWriter writer, Enum name, T? value)
            where T : struct
        {
            if (value.HasValue)
                writer.WriteAttributeString(name, value.ToString());
        }

        public static void WriteAttributeNullable(this XmlWriter writer, Enum name, bool? value)
        {
            if (value.HasValue)
                writer.WriteAttribute(name, value.Value);
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

        public static void WriteAttribute<T>(this XmlWriter writer, Enum name, T value)
        {
            writer.WriteAttributeString(name, value.ToString());
        }

        public static void WriteAttribute<T>(this XmlWriter writer, Enum name, T value, T defaultValue)
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
            writer.WriteStartElement(name.ToString());
        }

        public static void WriteElement<T>(this XmlWriter writer, T child)
            where T : IXmlSerializable
        {
            var helper = new XmlElementHelper<T>();
            writer.WriteElement(helper.ElementNames[0], child);
        }

        public static void WriteElementString<T>(this XmlWriter writer, Enum name, T child)
        {
            writer.WriteStartElement(name);
            writer.WriteString(child.ToString());
            writer.WriteEndElement();
        }

        public static void WriteElement(this XmlWriter writer, Enum name, IXmlSerializable child)
        {
            writer.WriteElement(name.ToString(), child);
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
                    throw new InvalidDataException("Attemt to serialize list missing an element.");
                IXmlElementHelper<TItem> helper = FindHelper(item, helpers);
                if (helper == null)
                    throw new InvalidOperationException("Attempt to serialize list containing invalid type.");
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
            return reader.GetAttribute(name.ToString());
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
                throw new InvalidDataException(string.Format("The value '{0}' is not valid for the attribute {1}.", value, name), x);
            }
        }

        private static bool ConvertToBoolean(string attribute)
        {
            return Convert.ToBoolean(attribute, CultureInfo.InvariantCulture);
        }

        public static bool? GetNullableBoolAttribute(this XmlReader reader, Enum name)
        {
            return reader.GetAttribute<bool>(name, ConvertToBoolean);
        }

        public static bool GetBoolAttribute(this XmlReader reader, Enum name)
        {
            return reader.GetBoolAttribute(name, false);
        }

        public static bool GetBoolAttribute(this XmlReader reader, Enum name, bool defaultValue)
        {
            return reader.GetNullableBoolAttribute(name) ?? defaultValue;
        }

        private static int ConvertToInt32(string attribute)
        {
            return Convert.ToInt32(attribute, CultureInfo.InvariantCulture);
        }

        public static int? GetNullableIntAttribute(this XmlReader reader, Enum name)
        {
            return reader.GetAttribute<int>(name, ConvertToInt32);
        }

        public static int GetIntAttribute(this XmlReader reader, Enum name)
        {
            return reader.GetIntAttribute(name, 0);
        }

        public static int GetIntAttribute(this XmlReader reader, Enum name, int defaultValue)
        {
            return reader.GetNullableIntAttribute(name) ?? defaultValue;
        }

        private static double ConvertToDouble(string attribute)
        {
            return Convert.ToDouble(attribute, CultureInfo.InvariantCulture);
        }

        public static double? GetNullableDoubleAttribute(this XmlReader reader, Enum name)
        {
            return reader.GetAttribute<double>(name, ConvertToDouble);
        }

        public static double GetDoubleAttribute(this XmlReader reader, Enum name)
        {
            return reader.GetDoubleAttribute(name, 0.0);
        }

        public static double GetDoubleAttribute(this XmlReader reader, Enum name, double defaultValue)
        {
            return reader.GetNullableDoubleAttribute(name) ?? defaultValue;
        }

        private static float ConvertToSingle(string attribute)
        {
            return Convert.ToSingle(attribute, CultureInfo.InvariantCulture);
        }

        public static float? GetNullableFloatAttribute(this XmlReader reader, Enum name)
        {
            return reader.GetAttribute<float>(name, ConvertToSingle);
        }

        public static float GetFloatAttribute(this XmlReader reader, Enum name)
        {
            return reader.GetFloatAttribute(name, 0.0f);
        }

        public static float GetFloatAttribute(this XmlReader reader, Enum name, float defaultValue)
        {
            return reader.GetNullableFloatAttribute(name) ?? defaultValue;
        }

        public static T GetEnumAttribute<T>(this XmlReader reader, Enum name, T defaultValue)
        {
            return reader.GetEnumAttribute(name, defaultValue, false);
        }

        public static T GetEnumAttribute<T>(this XmlReader reader, Enum name, T defaultValue, bool lower)
        {
            string value = reader.GetAttribute(name);
            if (!string.IsNullOrEmpty(value))
            {
                try
                {
                    return (T)Enum.Parse(typeof(T), (lower ? value.ToLower() : value));
                }
                catch (ArgumentException x)
                {
                    throw new InvalidDataException(string.Format("The value '{0}' is not valid for the attribute {1}.", value, name), x);
                }
            }
            return defaultValue;
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
            return reader.IsStartElement(name.ToString());
        }

        public static void ReadStartElement(this XmlReader reader, Enum name)
        {
            reader.ReadStartElement(name.ToString());
        }

        public static void ReadElementContentAsDoubleInvariant(this XmlReader reader)
        {
            Convert.ToDouble(reader.ReadElementContentAsString(), CultureInfo.InvariantCulture);
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

        public static T Deserialize<T>(this XmlReader reader, T objNew)
            where T : IXmlSerializable
        {
            objNew.ReadXml(reader);
            return objNew;
        }

        public static T DeserializeElement<T>(this XmlReader reader)
            where T : class
        {
            return DeserializeElement<T>(reader, null);
        }

        public static T DeserializeElement<T>(this XmlReader reader, Enum name)
            where T : class
        {
            var helper = new XmlElementHelper<T>(name == null ? null : name.ToString());
            if (reader.IsStartElement(helper.ElementNames))
                return helper.Deserialize(reader);
            return null;
        }
    }

    public interface IXmlElementHelper<T>
    {
        string[] ElementNames { get; }

        T Deserialize(XmlReader reader);

        bool IsType(object item);
    }

    /// <summary>
    /// Helper for reading and writing XML elements in <see cref="IXmlSerializable"/>
    /// implementations.  If the type supplied has the <see cref="XmlRootAttribute"/>
    /// then it is self-naming.  Otherwise a name for the element must be supplied.
    /// </summary>
    /// <typeparam name="T">Type for which this helper is used</typeparam>
    public sealed class XmlElementHelper<T> : IXmlElementHelper<T>
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
                Type type = typeof(T);
                XmlRootAttribute[] attrs = (XmlRootAttribute[])
                    type.GetCustomAttributes(typeof(XmlRootAttribute), false);

                if (attrs.Length < 1)
                    throw new InvalidOperationException(string.Format("The class {0} has no {1}.", type.FullName, typeof(XmlRootAttribute).Name));

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
        public T Deserialize(XmlReader reader)
        {
            // Unit tests depend on exceptions being thrown.
//            try
//            {
                return (T)typeof(T).InvokeMember("Deserialize",
                                                 BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod,
                                                 null, null, new[] { reader });
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
            return typeof(T) == item.GetType();
        }
    }

    public sealed class XmlElementHelperSuper<T, TSup> : IXmlElementHelper<TSup>
        where T : TSup
    {
        private readonly XmlElementHelper<T> _helper;

        public XmlElementHelperSuper()
        {
            _helper = new XmlElementHelper<T>();
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
}