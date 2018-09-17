/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.ElementLocators
{
    /// <summary>
    /// Unique identifier of anything in a Skyline Document.
    /// An ElementLocator starts with a typename followed by a colon.
    /// The element locator consists of a series of levels separated by slashes.
    /// A level has a name, optionally followed by a '?' and a series of attrname=attrvalue attributes.
    /// If a name, attrname, or attrvalue contains any special characters, then it gets quoted with double-quote characters,
    /// and any quotes within the thing get replaced with two double-quote characters.
    /// </summary>
    public sealed class ElementLocator : Immutable
    {
        // ReSharper disable once LocalizableElement
        private static readonly Regex REGEX_SPECIALCHARS = new Regex("[/?&=\"]");

        public ElementLocator(string name, IEnumerable<KeyValuePair<string, string>> attributes)
        {
            Name = name;
            Attributes = ImmutableList.ValueOfOrEmpty(attributes);
        }

        public ElementLocator Parent
        {
            get;
            private set;
        }

        /// <summary>
        /// The type of this ElementLocator. Valid ElementLocator's have a Type. This is used by <see cref="ElementRefs.FromObjectReference"/> 
        /// to decide what class of ElementRef to return. This should match the <see cref="ElementRef.ElementType"/> of some type of ElementRef.
        /// </summary>
        public string Type
        {
            get;
            private set;
        }
        public string Name { get; private set; }
        public ImmutableList<KeyValuePair<string, string>> Attributes { get; private set; }

        /// <summary>
        /// Returns an ElementLocator with the new parent.
        /// The parent's Type is changed to null, since only the child ElementLocator has a type. The type of its parent
        /// can be inferred from the type of the child.
        /// </summary>
        public ElementLocator ChangeParent(ElementLocator parent)
        {
            if (ReferenceEquals(Parent, parent))
            {
                return this;
            }
            if (parent != null && parent.Type != null)
            {
                parent = parent.ChangeType(null);
            }
            return ChangeProp(ImClone(this), im => im.Parent = parent);
        }

        public ElementLocator ChangeType(string type)
        {
            if (string.IsNullOrEmpty(type))
            {
                type = null;
            }
            if (ReferenceEquals(type, Type))
            {
                return this;
            }
            return ChangeProp(ImClone(this), im => im.Type = type);
        }

        public ElementLocator ChangeName(string name)
        {
            return ChangeProp(ImClone(this), im => im.Name = name);
        }

        public ElementLocator ChangeAttributes(IEnumerable<KeyValuePair<string, string>> attributes)
        {
            return ChangeProp(ImClone(this), im => im.Attributes = ImmutableList.ValueOfOrEmpty(attributes));
        }

        public KeyValuePair<string, string> FindAttribute(string name)
        {
            return Attributes.FirstOrDefault(attr => attr.Key == name);
        }

        private bool Equals(ElementLocator other)
        {
            return Equals(Parent, other.Parent) && string.Equals(Type, other.Type) && string.Equals(Name, other.Name) && Equals(Attributes, other.Attributes);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is ElementLocator && Equals((ElementLocator)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Parent != null ? Parent.GetHashCode() : 0;
                hashCode = (hashCode * 397) ^ (Type != null ? Type.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Name != null ? Name.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ Attributes.GetHashCode();
                return hashCode;
            }
        }

        public static ElementLocator Parse(string str)
        {
            int ichColon = str.IndexOf(':');
            if (ichColon < 0)
            {
                string message = string.Format(Resources.ElementLocator_Parse__0__is_not_a_valid_element_locator_, str);
                throw new FormatException(message);
            }
            var type = str.Substring(0, ichColon);
            ElementLocator result = null;
            var stringReader = new StringReader(str);
            stringReader.Read(new char[ichColon + 1], 0, ichColon + 1);
            foreach (var elementLocator in ParseParts(stringReader))
            {
                result = elementLocator.ChangeParent(result);
            }
            if (result == null)
            {
                string message = string.Format(Resources.ElementLocator_Parse__0__is_not_a_valid_element_locator_, str);
                throw new FormatException(message);
            }
            result = result.ChangeType(type);
            return result;
        }

        public override string ToString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(Type ?? string.Empty);
            stringBuilder.Append(@":");
            AppendToStringBuilder(stringBuilder);
            return stringBuilder.ToString();
        }

        private void AppendToStringBuilder(StringBuilder stringBuilder)
        {
            if (Parent != null)
            {
                Parent.AppendToStringBuilder(stringBuilder);
                stringBuilder.Append(@"/");
            }
            stringBuilder.Append(QuoteIfSpecial(Name));
            if (Attributes.Any())
            {
                stringBuilder.Append(@"?");
                stringBuilder.Append(string.Join(@"&", Attributes.Select(attr =>
                {
                    if (attr.Value == null)
                    {
                        return QuoteIfSpecial(attr.Key);
                    }
                    return QuoteIfSpecial(attr.Key) + @"=" + QuoteIfSpecial(attr.Value);
                })));
            }
        }

        public static string QuoteIfSpecial(string str)
        {
            if (!REGEX_SPECIALCHARS.Match(str).Success)
            {
                return str;
            }
            // ReSharper disable LocalizableElement
            return "\"" + str.Replace("\"", "\"\"") + "\"";
            // ReSharper restore LocalizableElement
        }

        private static TokenType? TokenTypeFromChar(char ch)
        {
            switch (ch)
            {
                case '/':
                    return TokenType.Slash;
                case '?':
                    return TokenType.Question;
                case '&':
                    return TokenType.And;
                case '=':
                    return TokenType.Equals;
            }
            return null;
        }


        private static string ReadQuotedString(StringReader reader, char chEndQuote)
        {
            StringBuilder stringBuilder = new StringBuilder();
            while (true)
            {
                int chInt = reader.Read();
                if (chInt < 0)
                {
                    throw UnexpectedException(reader, Resources.ElementLocator_ReadQuotedString_End_of_text);
                }
                char ch = (char)chInt;
                if (ch == chEndQuote)
                {
                    int chNext = reader.Peek();
                    if (chNext == chEndQuote)
                    {
                        reader.Read();
                        stringBuilder.Append(ch);
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    stringBuilder.Append(ch);
                }
            }
            return stringBuilder.ToString();
        }

        private static Exception UnexpectedException(StringReader reader, KeyValuePair<TokenType, string> token)
        {
            return UnexpectedException(reader, @"'" + token.Value + @"'");
        }

        private static Exception UnexpectedException(StringReader reader, string unexpected)
        {
            var remainder = reader.ReadToEnd();
            string fullText = reader.ToString();
            // ReSharper disable PossibleNullReferenceException
            int position = fullText.Length - remainder.Length;
            // ReSharper restore PossibleNullReferenceException
            string fullMessage = TextUtil.LineSeparate(string.Format(Resources.ElementLocator_UnexpectedException__0__was_unexpected_in___1___at_position__2__, unexpected, fullText, position));
            return new FormatException(fullMessage);
        }

        private static IEnumerable<KeyValuePair<TokenType, string>> EnumerateTokens(StringReader reader)
        {
            StringBuilder stringBuilder = new StringBuilder();
            while (true)
            {
                int chInt = reader.Read();
                if (chInt < 0)
                {
                    break;
                }
                if (chInt == '"')
                {
                    yield return new KeyValuePair<TokenType, string>(TokenType.String,
                        ReadQuotedString(reader, '"'));
                }
                else
                {
                    var tokenType = TokenTypeFromChar((char)chInt);
                    if (tokenType.HasValue)
                    {
                        if (stringBuilder.Length > 0)
                        {
                            yield return new KeyValuePair<TokenType, string>(TokenType.String,
                                stringBuilder.ToString());
                            stringBuilder.Clear();
                        }
                        yield return new KeyValuePair<TokenType, string>(tokenType.Value, null);
                    }
                    else
                    {
                        stringBuilder.Append((char)chInt);
                    }
                }
            }
            if (stringBuilder.Length > 0)
            {
                yield return new KeyValuePair<TokenType, string>(TokenType.String, stringBuilder.ToString());
            }
        }
        private static IEnumerable<ElementLocator> ParseParts(StringReader reader)
        {
            string keyName = null;
            string attributeName = null;
            string attributeValue = null;
            List<KeyValuePair<string, string>> attributes = new List<KeyValuePair<string, string>>();
            foreach (var tk in EnumerateTokens(reader))
            {
                switch (tk.Key)
                {
                    case TokenType.String:
                        if (keyName == null)
                        {
                            keyName = tk.Value;
                        }
                        else if (attributeName == null)
                        {
                            attributeName = tk.Value;
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(attributeValue))
                            {
                                throw UnexpectedException(reader, tk);
                            }
                            attributeValue = tk.Value;
                        }
                        break;
                    case TokenType.Slash:
                        if (attributeName != null)
                        {
                            attributes.Add(new KeyValuePair<string, string>(attributeName, attributeValue));
                        }
                        yield return new ElementLocator(keyName ?? string.Empty, attributes);
                        keyName = null;
                        attributeName = null;
                        attributeValue = null;
                        attributes.Clear();
                        break;
                    case TokenType.Question:
                        if (attributeName != null || attributes.Count > 0)
                        {
                            throw new FormatException();
                        }
                        keyName = keyName ?? string.Empty;
                        attributeName = null;
                        attributes.Clear();
                        break;
                    case TokenType.And:
                        if (keyName == null)
                        {
                            throw UnexpectedException(reader, tk);
                        }
                        attributes.Add(new KeyValuePair<string, string>(attributeName ?? string.Empty, attributeValue));
                        attributeName = null;
                        attributeValue = null;
                        break;
                    case TokenType.Equals:
                        attributeName = attributeName ?? string.Empty;
                        if (attributeValue != null)
                        {
                            throw UnexpectedException(reader, tk);
                        }
                        attributeValue = string.Empty;
                        break;
                }
            }
            if (keyName == null)
            {
                yield break;
            }
            if (attributeName != null)
            {
                attributes.Add(new KeyValuePair<string, string>(attributeName, attributeValue));
            }

            yield return new ElementLocator(keyName, attributes);
        }
        private enum TokenType
        {
            String,
            Slash,
            Question,
            And,
            Equals,
        }
    }
}
