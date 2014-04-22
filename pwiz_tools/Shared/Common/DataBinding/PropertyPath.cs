/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using System.Linq;
using System.Text;
using pwiz.Common.Properties;

namespace pwiz.Common.DataBinding
{
    /// <summary>
    /// Identifier for a property that is reached by getting a chain of
    /// properties by name from a root object.
    /// Parts of the PropertyPath are either property names, or 
    /// lookups into collections.
    /// In a <see cref="ViewSpec"/>, PropertyPaths may contain 
    /// "unbound" paths where one or more parts are lookups with a 
    /// null name.  These parts obtain a value when the rows are pivoted.
    /// PropertyPaths can be serialized to strings.
    /// In the serialized form, properties are preceded by a ".", and
    /// lookups are preceded by "!".
    /// </summary>
    public class PropertyPath : IComparable<PropertyPath>
    {
        public static readonly PropertyPath Root = new PropertyPath();
        private PropertyPath(PropertyPath parent, string name, bool isProperty)
        {
            Parent = parent;
            Name = name;
            IsProperty = isProperty;
            Length = 1 + Parent.Length;
        }
        private PropertyPath()
        {
            Parent = null;
            Name = null;
            IsProperty = false;
            Length = 0;
        }
        /// <summary>
        /// Returns a new PropertyPath which is a child of this,
        /// for the specified property name.
        /// </summary>
        public PropertyPath Property(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Name cannot be blank"); // Not L10N
            }
            return new PropertyPath(this, name, true);
        }
        /// <summary>
        /// Returns a new PropertyPath which is a child of this,
        /// and which looks up the specified key in a collection.
        /// </summary>
        public PropertyPath LookupByKey(string key)
        {
            if (null == key)
            {
                throw new ArgumentNullException("key"); // Not L10N
            }
            return new PropertyPath(this, key, false);
        }
        /// <summary>
        /// Returns a PropertyPath specifying the unbound lookup into
        /// the collection specified by this.
        /// </summary>
        public PropertyPath LookupAllItems()
        {
            return new PropertyPath(this, null, false);
        }
        public PropertyPath Concat(PropertyPath propertyPath)
        {
            if (propertyPath.IsRoot)
            {
                return this;
            }
            return new PropertyPath(Concat(propertyPath.Parent), propertyPath.Name, propertyPath.IsProperty);
        }

        public PropertyPath SetParent(PropertyPath newParent)
        {
            return new PropertyPath(newParent, Name, IsProperty);
        }

        public bool IsRoot { get { return ReferenceEquals(this, Root); } }
        public PropertyPath Parent { get; private set; }
        public string Name { get; private set; }
        public bool IsProperty { get; private set; }
        public bool IsLookup { get { return !IsProperty; } }
        public bool IsUnboundLookup { get { return null == Name; } }
        public int Length { get; private set; }
        
        public int CompareTo(PropertyPath that)
        {
            if (that == null)
            {
                return 1;
            }
            if (Length > that.Length)
            {
                return -that.CompareTo(this);
            }
            int defResult = Length < that.Length ? -1 : 0;
            while (Length < that.Length)
            {
                that = that.Parent;
            }
            int result;
            if (Length > 1)
            {
                result = Parent.CompareTo(that.Parent);
                if (result != 0)
                {
                    return result;
                }
            }
            result = -IsProperty.CompareTo(that.IsProperty);
            if (result != 0)
            {
                return result;
            }
            if (Name == null)
            {
                return that.Name == null ? defResult : -1;
            }
            result = String.Compare(Name, that.Name, StringComparison.Ordinal);
            if (result != 0)
            {
                return result;
            }
            return defResult;
        }
        public bool StartsWith(PropertyPath that)
        {
            if (that == null)
            {
                return true;
            }
            if (Length < that.Length)
            {
                return false;
            }
            if (Length == that.Length)
            {
                return Equals(that);
            }
            return Parent.StartsWith(that);
        }
        public override string ToString()
        {
            if (IsRoot)
            {
                return string.Empty;
            }
            if (IsUnboundLookup)
            {
                return Parent + "!*"; // Not L10N
            }
            if (IsLookup)
            {
                return Parent + "!" + EscapeIfNeeded(Name); // Not L10N
            }
            if (Parent.IsRoot)
            {
                return EscapeIfNeeded(Name);
            }
            return Parent + "." + EscapeIfNeeded(Name); // Not L10N
        }
        public static PropertyPath Parse(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return Root;
            }
            PropertyPath current = Root;
            int lastIndex = 0;
            bool lookup = false;
            while (true)
            {
                char ch = path[lastIndex];
                string name;
                switch (ch)
                {
                    case '*':
                        if (!lookup)
                        {
                            throw new ParseException(path, lastIndex, Resources.PropertyPath_Parse_Invalid_character____);
                        }
                        name = null;
                        lastIndex++;
                        break;
                    case '"':
                        var nameBuilder = new StringBuilder();
                        bool atDoubleQuote = false;
                        for (int i = lastIndex + 1; i < path.Length; i++)
                        {
                            ch = path[i];
                            if (ch == '"')
                            {
                                if (atDoubleQuote)
                                {
                                    atDoubleQuote = false;
                                    nameBuilder.Append('"');
                                }
                                else
                                {
                                    atDoubleQuote = true;
                                    if (i == path.Length - 1)
                                    {
                                        lastIndex = path.Length;
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                if (atDoubleQuote)
                                {
                                    lastIndex = i;
                                    break;
                                }
                                nameBuilder.Append(ch);
                            }
                        }
                        if (!atDoubleQuote)
                        {
                            throw new ParseException(path, lastIndex, Resources.PropertyPath_Parse_Unterminated_quote);
                        }
                        name = nameBuilder.ToString();
                        break;
                    default:
                        int index;
                        for (index = lastIndex; index < path.Length; index++)
                        {
                            if (!char.IsLetterOrDigit(path[index]))
                            {
                                break;
                            }
                        }
                        name = path.Substring(lastIndex, index - lastIndex);
                        lastIndex = index;
                        break;
                }
                if (lookup)
                {
                    if (null == name)
                    {
                        current = current.LookupAllItems();
                    }
                    else
                    {
                        current = current.LookupByKey(name);
                    }
                }
                else
                {
                    if (string.IsNullOrEmpty(name))
                    {
                        if (!current.IsRoot)
                        {
                            throw new ParseException(path, lastIndex, Resources.PropertyPath_Parse_Empty_name);
                        }
                    }
                    else
                    {
                        current = current.Property(name);
                    }
                }
                if (lastIndex == path.Length)
                {
                    return current;
                }
                ch = path[lastIndex];
                switch (ch)
                {
                    case '.':
                        if (lastIndex == 0)
                        {
                            throw new ParseException(path, 0, Resources.PropertyPath_Parse_Invalid_character);
                        }
                        lookup = false;
                        break;
                    case '!':
                        lookup = true;
                        break;
                    default:
                        throw new ParseException(path, lastIndex, Resources.PropertyPath_Parse_Invalid_character+ " " + ch); // Not L10N
                }
                lastIndex++;
            }
        }

        #region object overrides

        public bool Equals(PropertyPath other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.Parent, Parent) 
                && Equals(other.IsProperty, IsProperty) 
                && Equals(other.Name, Name);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (PropertyPath)) return false;
            return Equals((PropertyPath) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = (Parent != null ? Parent.GetHashCode() : 0);
                result = result*397 ^ (Name == null ? 0 : Name.GetHashCode());
                result = result*397 ^ IsProperty.GetHashCode();
                return result;
            }
        }

        #endregion
        public static bool NeedsEscaping(char ch)
        {
            return !Char.IsLetterOrDigit(ch);
        }
        public static bool NeedsEscaping(string text)
        {
            return string.IsNullOrEmpty(text) || text.Any(NeedsEscaping);
        }

        public static string Escape(string text)
        {
            if (text == null)
            {
                return "*"; // Not L10N
            }
            var result = new StringBuilder(text.Length + 5);
            result.Append('\"');
            foreach (var ch in text)
            {
                if (ch == '\"')
                {
                    result.Append("\"\""); // Not L10N
                }
                else
                {
                    result.Append(ch);
                }
            }
            result.Append("\""); // Not L10N
            return result.ToString();
        }
        public static string EscapeIfNeeded(string text)
        {
            return NeedsEscaping(text) ? Escape(text) : text;
        }

        public class ParseException : Exception
        {
            public ParseException(string input, int location, string errorMessage) 
                : base (string.Format(Resources.ParseException_ParseException_Error_parsing__0__at_location__1____2_, input, location, errorMessage))
            {
                Input = input;
                Location = location;
                ErrorMessage = errorMessage;
            }
            public string Input { get; private set; }
            public int Location { get; private set; }
            public string ErrorMessage { get; private set; }
        }
    }

    public enum LookupType
    {
        Unbound,
        Property,
        Index,
    }
}
