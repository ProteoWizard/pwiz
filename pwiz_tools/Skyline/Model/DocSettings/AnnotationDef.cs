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
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Properties;
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
        public static readonly AnnotationDef EMPTY = new AnnotationDef()
        {
            _items = ImmutableList<string>.EMPTY,
            Type = AnnotationType.text,
            AnnotationTargets = AnnotationTargetSet.EMPTY
        };
        /// <summary>
        /// A prefix that is often prepended to annotation names when annotations coexist 
        /// with other built in columns or attributes.
        /// </summary>
        public const string ANNOTATION_PREFIX = "annotation_";

        private ImmutableList<string> _items;
        private TypeSafeEnum<AnnotationType> _type;

        public AnnotationDef(String name, AnnotationTargetSet annotationTargets, AnnotationType type, IList<String> items) 
            : this(name, annotationTargets, new ListPropertyType(type, null), items)
        {
        }

        public AnnotationDef(string name, AnnotationTargetSet annotationTargets, ListPropertyType listPropertyType, IList<String> items) 
            : base(name)
        {
            AnnotationTargets = annotationTargets;
            Type = listPropertyType.AnnotationType;
            Lookup = listPropertyType.Lookup;
            _items = MakeReadOnly(items) ?? ImmutableList.Empty<string>();
        }

        private AnnotationDef()
        {
        }

        [Track(defaultValues: typeof(DefaultValuesNullOrEmpty))]
        public AnnotationTargetSet AnnotationTargets { get; private set; }
        [Track]
        public AnnotationType Type { get { return _type; } private set { _type = value; } }
        public ListPropertyType ListPropertyType { get { return new ListPropertyType(Type, Lookup);} }

        public AnnotationDef ChangeType(AnnotationType type)
        {
            return ChangeProp(ImClone(this), im => im.Type = type);
        }
        public string Lookup { get; private set; }

        public AnnotationDef ChangeLookup(string lookup)
        {
            return ChangeProp(ImClone(this), im => im.Lookup = string.IsNullOrEmpty(lookup) ? null : lookup);
        }

        public AnnotationExpression Expression { get; private set; }

        public AnnotationDef ChangeExpression(AnnotationExpression expression)
        {
            return ChangeProp(ImClone(this), im => im.Expression = expression);
        }
        public Type ValueType
        {
            get
            {
                switch (Type)
                {
                    case AnnotationType.true_false:
                        return typeof (bool);
                    case AnnotationType.number:
                        return typeof (double);
                    default:
                        return typeof (string);
                }
            }
        }

        private class AnnotationDefValuesDefault : DefaultValues
        {
            public override bool IsDefault(object obj, object parentObject)
            {
                var def = (AnnotationDef) parentObject;
                return def.Type != AnnotationType.value_list;
            }
        }

        [Track(defaultValues: typeof(AnnotationDefValuesDefault))]
        public ImmutableList<String> Items
        {
            get { return _items; }
        }

        public AnnotationDef ChangeItems(IEnumerable<string> prop)
        {
            return ChangeProp(ImClone(this), im => im._items = MakeReadOnly(prop));
        }

        /// <summary>
        /// Returns the error message to be shown when the value cannot be parsed.
        /// </summary>
        [Localizable(true)]
        public string ValidationErrorMessage
        {
            get
            {
                switch (Type)
                {
                    default:
                        return Resources.AnnotationDef_ValidationErrorMessage_Invalid_value;
                    case AnnotationType.number:
                        return Resources.AnnotationDef_ValidationErrorMessage_Value_must_be_a_number;
                }
            }
        }

        public string ToPersistedString(object value)
        {
            if (value is bool)
            {
                return ((bool) value) == false ? null : Name;
            }
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        public object ParsePersistedString(string str)
        {
            if (AnnotationType.true_false == Type)
            {
                return !string.IsNullOrEmpty(str);
            }
            if (AnnotationType.number == Type)
            {
                return ParseNumber(str);
            }
            return str;
        }

        public static double? ParseNumber(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return null;
            }
            double value;
            if (double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return value;
            }
            return double.NaN;
        }

        private enum Attr
        {
            targets,
            type,
            lookup,
        }
        private enum El
        {
            value,
            expression,
        }

        public static AnnotationDef Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new AnnotationDef());
        }

        public override void ReadXml(XmlReader reader)
        {
            base.ReadXml(reader);
            AnnotationTargets = AnnotationTargetSet.Parse(reader.GetAttribute(Attr.targets));
            // In older documents, it's possible for the "type" attribute value to be "-1".
            Type = TypeSafeEnum.ValidateOrDefault(reader.GetEnumAttribute(Attr.type, AnnotationType.text),
                AnnotationType.text);
            Lookup = reader.GetAttribute(Attr.lookup);
            var items = new List<string>();
            if (reader.IsEmptyElement)
            {
                reader.Read();
            }
            else
            {
                reader.Read();
                while (true)
                {
                    if (reader.IsStartElement(El.value))
                    {
                        items.Add(reader.ReadElementString());
                    }
                    else if (reader.IsStartElement(El.expression))
                    {
                        Expression = reader.DeserializeElement<AnnotationExpression>();
                    }
                    else
                    {
                        break;
                    }
                }
                reader.ReadEndElement();
            }
            _items = MakeReadOnly(items);
        }

        public override void WriteXml(XmlWriter writer)
        {
            base.WriteXml(writer);
            if (!AnnotationTargets.IsEmpty)
            {
                writer.WriteAttribute(Attr.targets, AnnotationTargets);
            }
            writer.WriteAttribute(Attr.type, Type);
            writer.WriteAttributeIfString(Attr.lookup, Lookup);
            foreach (var value in Items)
            {
                writer.WriteElementString(El.value, value);
            }

            if (Expression != null)
            {
                writer.WriteElement(Expression);
            }
        }


        #region object overrides

        public bool Equals(AnnotationDef other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return base.Equals(other) && Equals(other.AnnotationTargets, AnnotationTargets) &&
                   other.Type == Type && ArrayUtil.EqualsDeep(other.Items, Items) && Equals(other.Lookup, Lookup) &&
                   Equals(other.Expression, Expression);
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
                result = (result * 397) ^ Type.GetHashCode();
                result = (result * 397) ^ Items.GetHashCodeDeep();
                result = (result * 397) ^ (Lookup == null ? 0 : Lookup.GetHashCode());
                result = (result * 397) ^ (Expression == null ? 0 : Expression.GetHashCode());
                return result;
            }
        }

        #endregion

        public enum AnnotationTarget
        {
            protein,
            peptide,
            precursor,
            transition,
            replicate,
            precursor_result,
            transition_result,
        }

        public class AnnotationTargetSet : ValueSet<AnnotationTargetSet, AnnotationTarget>
        {
            protected override IEnumerable<AnnotationTarget> ParseElements(string stringValue)
            {
                if (@"none" == stringValue)
                {
                    // AnnotationTarget used to have the [Flags] attribute, and "none" is what
                    // was written out for an empty set.  Handle "none" here just in case
                    // that value was ever persisted.
                    return new AnnotationTarget[0];
                }
                return base.ParseElements(stringValue);
            }
        }

        public static string AnnotationTargetPluralName(AnnotationTarget annotationTarget)
        {
            switch (annotationTarget)
            {
                case AnnotationTarget.protein:
                    return Resources.AnnotationDef_AnnotationTarget_Proteins;
                case AnnotationTarget.peptide:
                    return Resources.AnnotationDef_AnnotationTarget_Peptides;
                case AnnotationTarget.precursor:
                    return Resources.AnnotationDef_AnnotationTarget_Precursors;
                case AnnotationTarget.transition:
                    return Resources.AnnotationDef_AnnotationTarget_Transitions;
                case AnnotationTarget.replicate:
                    return Resources.AnnotationDef_AnnotationTarget_Replicates;
                case AnnotationTarget.precursor_result:
                    return Resources.AnnotationDef_AnnotationTarget_PrecursorResults;
                case AnnotationTarget.transition_result:
                    return Resources.AnnotationDef_AnnotationTarget_TransitionResults;
                default:
                    throw new ArgumentException(string.Format(@"Invalid annotation target: {0}", annotationTarget), nameof(annotationTarget)); // CONSIDER: localize?
            }
        }

        public enum AnnotationType
        {
            text,
            number,
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
                    result.Append(@"__");
                else if (Char.IsLetterOrDigit(c))
                    result.Append(c);
                else
                    result.Append('_').Append(((int)c).ToString(@"X2")).Append('_');
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
            if (!key.Contains(@"_"))
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
