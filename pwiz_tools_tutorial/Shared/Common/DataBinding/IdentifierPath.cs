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

namespace pwiz.Common.DataBinding
{
    /// <summary>
    /// Identifier for a property that is reached by getting a chain of
    /// properties by name from a root object.
    /// </summary>
    public class IdentifierPath : IComparable<IdentifierPath>
    {
        public static readonly IdentifierPath Root = new IdentifierPath();
        public IdentifierPath(IdentifierPath parent, string name)
        {
            Parent = parent;
            Name = name;
            Length = 1 + Parent.Length;
        }
        private IdentifierPath()
        {
            Parent = null;
            Name = null;
            Length = 0;
        }
        public bool IsRoot { get { return ReferenceEquals(this, Root); } }
        public IdentifierPath Parent { get; private set; }
        public string Name { get; private set; }
        public int Length { get; private set; }
        public int CompareTo(IdentifierPath that)
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
            if (Name == null)
            {
                return that.Name == null ? defResult : -1;
            }
            result = Name.CompareTo(that.Name);
            if (result != 0)
            {
                return result;
            }
            return defResult;
        }
        public bool StartsWith(IdentifierPath that)
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
                return "";
            }
            if (Parent.IsRoot)
            {
                return EscapeIfNeeded(Name);
            }
            return Parent + "." + EscapeIfNeeded(Name);
        }
        public static IdentifierPath Parse(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return Root;
            }
            IdentifierPath current = Root;
            int lastIndex = 0;
            while (true)
            {
                char ch = path[lastIndex];
                string name;
                switch (ch)
                {
                    case '[':
                        lastIndex++;
                        if (lastIndex >= path.Length || path[lastIndex] != ']')
                        {
                            throw new ArgumentException("Invalid character at " + lastIndex);
                        }
                        lastIndex++;
                        name = null;
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
                                    lastIndex = i - 1;
                                    break;
                                }
                                nameBuilder.Append(ch);
                            }
                        }
                        if (!atDoubleQuote)
                        {
                            throw new ArgumentException("Unterminated quote begun at " + lastIndex + ":" + path);
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
                current = new IdentifierPath(current, name);
                if (lastIndex == path.Length)
                {
                    return current;
                }
                ch = path[lastIndex];
                if (ch != '.')
                {
                    throw new ArgumentException("Unexpected character " + ch + " at " + lastIndex + ":" + path);
                }
                lastIndex++;
            }
        }

        #region object overrides

        public bool Equals(IdentifierPath other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.Parent, Parent) && Equals(other.Name, Name);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (IdentifierPath)) return false;
            return Equals((IdentifierPath) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Parent != null ? Parent.GetHashCode() : 0)*397) ^ (Name == null ? 0 : Name.GetHashCode());
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
                return "[]";
            }
            var result = new StringBuilder(text.Length + 5);
            result.Append('\"');
            foreach (var ch in text)
            {
                if (ch == '\"')
                {
                    result.Append("\"\"");
                }
                else
                {
                    result.Append(ch);
                }
            }
            result.Append("\"");
            return result.ToString();
        }
        public static string EscapeIfNeeded(string text)
        {
            return NeedsEscaping(text) ? Escape(text) : text;
        }
    }
}
