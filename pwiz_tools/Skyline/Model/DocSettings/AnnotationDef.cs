/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
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
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.DocSettings
{
    /// <summary>
    /// Definition of annotations in either the global Settings or an SrmDocument.
    /// Annotations have a value type which is one of the enum values in
    /// AnnoationDef.AnnotationType.  Annotations which are "value_list" have
    /// a list of items representing the possible values.  Value list annotations
    /// always also allow the "null" value which is not in the item list.
    /// </summary>
    [XmlRoot("annotation")]    
    public sealed class AnnotationDef : XmlNamedElement
    {
        /// <summary>
        /// A prefix that is often prepended to annotation names when annotations coexist 
        /// with other built in columns or attributes.
        /// </summary>
        public const string ANNOTATION_PREFIX = "annotation_";

        public AnnotationDef(String name, AnnotationTarget annotationTargets, AnnotationType type, IList<String> items) : base(name)
        {
            AnnotationTargets = annotationTargets;
            Type = type;
            Items = new ReadOnlyCollection<string>(items);
        }
        private AnnotationDef()
        {
        }
        
        public AnnotationTarget AnnotationTargets { get; private set; }
        public AnnotationType Type { get; private set; }
        public IList<String> Items { get; private set; }

        private enum Attr
        {
            targets,
            type,
        }
        private enum El
        {
            value,
        }

        public static AnnotationDef Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new AnnotationDef());
        }

        public override void ReadXml(XmlReader reader)
        {
            base.ReadXml(reader);
            AnnotationTargets = reader.GetEnumAttribute(Attr.targets, (AnnotationTarget) 0);
            Type = reader.GetEnumAttribute(Attr.type, AnnotationType.text);
            var items = new List<string>();
            if (reader.IsEmptyElement)
            {
                reader.Read();
            }
            else
            {
                reader.Read();
                while (reader.IsStartElement(El.value))
                {
                    items.Add(reader.ReadElementString());
                }
                reader.ReadEndElement();
            }
            Items = new ReadOnlyCollection<string>(items);
        }

        public override void WriteXml(XmlWriter writer)
        {
            base.WriteXml(writer);
            writer.WriteAttribute(Attr.targets, AnnotationTargets);
            writer.WriteAttribute(Attr.type, Type);
            foreach (var value in Items)
            {
                writer.WriteElementString(El.value, value);
            }
        }


        #region object overrides

        public bool Equals(AnnotationDef other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return base.Equals(other) && Equals(other.AnnotationTargets, AnnotationTargets) && ArrayUtil.EqualsDeep(other.Items, Items);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as AnnotationDef);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = base.GetHashCode();
                result = (result*397) ^ AnnotationTargets.GetHashCode();
                result = (result*397) ^ Items.GetHashCodeDeep();
                return result;
            }
        }

        #endregion

        [Flags]
        public enum AnnotationTarget
        {
            none = 0x00,
            protein = 0x01,
            peptide = 0x02,
            precursor = 0x04,
            transition = 0x08,
            precursor_result = 0x10,
            transition_result = 0x20,

            result_file = 0x40,
        }

        public enum AnnotationType
        {
            text,
            true_false,
            value_list,
        }

        public static string GetKey(string annotationName)
        {
            return escape(annotationName);
        }

        public static string GetColumnName(string annotationName)
        {
            return ANNOTATION_PREFIX + GetKey(annotationName);
        }

        public static string GetColumnKey(string columnName)
        {
            return columnName.Substring(ANNOTATION_PREFIX.Length);
        }

        public static string GetColumnDisplayName(string columnName)
        {
            return unescape(GetColumnKey(columnName));
        }

        public static bool IsAnnotationProperty(string propertyName)
        {
            return propertyName.StartsWith(ANNOTATION_PREFIX);
        }

        /// <summary>
        /// Converts free-text annotation name to a format that can be used
        /// as a column name in HQL.
        /// </summary>
        private static string escape(IEnumerable<char> annotationName)
        {
            StringBuilder result = new StringBuilder();
            foreach (char c in annotationName)
            {
                if (c == '_')
                    result.Append("__");
                else if (Char.IsLetterOrDigit(c))
                    result.Append(c);
                else
                    result.Append('_').Append(((int) c).ToString("X2")).Append('_');
            }
            return result.ToString();
        }

        /// <summary>
        /// Converts and escaped annotation name back to its free-text form.
        /// </summary>
        private static string unescape(string key)
        {
            // All escaping is based on the underscore character.  If it
            // is not present, then this key doesn't require unescaping.
            if (!key.Contains("_"))
                return key;

            StringBuilder result = new StringBuilder();
            for (int i = 0; i < key.Length; i++)
            {
                char c = key[i];
                if (c != '_')
                    result.Append(c);
                else
                {
                    int start = i + 1;
                    // find the matching underscore character
                    int end = key.IndexOf('_', start);
                    // if none found, to be safe, append the rest of the string and quit
                    if (end == -1)
                    {
                        result.Append(key.Substring(i));
                        break;
                    }
                    int charVal;
                    // double underscore gets converted to underscore
                    if (end == start)
                        result.Append('_');
                        // _XX_ gets converted to the corresponding character code for XX
                    else if (Int32.TryParse(key.Substring(start, end - start),
                                          NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out charVal))
                    {
                        result.Append((char)charVal);
                    }
                        // otherwise just preserve the original text
                    else
                    {
                        result.Append(key.Substring(i, end - i + 1));
                    }
                    i = end;
                }
            }
            return result.ToString();
        }
    }
}
