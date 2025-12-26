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
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using Sprache;

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

        private static readonly Parser<ElementLocator> FullLocatorParser = CreateFullLocatorParser();

        private static Parser<ElementLocator> CreateFullLocatorParser()
        {
            // Parser for escaped quote ("") inside a quoted string, returning a single quote char
            var escapedQuote = Sprache.Parse.String("\"\"").Return('"');

            // Parser for a character inside a quoted string (escaped quote or any non-quote char)
            var quotedChar = escapedQuote.Or(Sprache.Parse.CharExcept('"'));

            // Parser for a quoted string: "..." where "" represents a literal "
            var quotedString = Sprache.Parse.Char('"')
                .Then(_ => quotedChar.Many().Text())
                .Then(content => Sprache.Parse.Char('"').Return(content));

            // Parser for an unquoted string (no special chars), can be empty
            var unquotedString = Sprache.Parse.CharExcept("/?&=\"").Many().Text();

            // Parser for a string value (quoted or unquoted)
            var stringValue = quotedString.Or(unquotedString);

            // Parser for an attribute value (after '='): can be empty or a string
            var attributeValue = Sprache.Parse.Char('=').Then(_ => stringValue);

            // Parser for an attribute: name(=value)?
            var attribute = stringValue
                .Then(name => attributeValue.Optional()
                    .Select(value => new KeyValuePair<string, string>(name, value.GetOrDefault())));

            // Parser for attributes section: ?(attr1(&attr2)*)
            var attributesSection = Sprache.Parse.Char('?')
                .Then(_ => attribute)
                .Then(first => Sprache.Parse.Char('&').Then(_ => attribute).Many()
                    .Select(rest => new[] { first }.Concat(rest)));

            // Parser for a single segment: name(?attrs)?
            var segment = stringValue
                .Then(name => attributesSection.Optional()
                    .Select(attrs => new ElementLocator(name, attrs.GetOrElse(Enumerable.Empty<KeyValuePair<string, string>>()))));

            // Parser for the path (segments separated by /)
            var pathParser = segment.DelimitedBy(Sprache.Parse.Char('/'));

            // Full parser for Type:path (type can be empty)
            return Sprache.Parse.CharExcept(':').Many().Text()
                .Then(type => Sprache.Parse.Char(':')
                    .Then(_ => pathParser)
                    .Select(segments => BuildLocatorFromSegments(type, segments)));
        }

        private static ElementLocator BuildLocatorFromSegments(string type, IEnumerable<ElementLocator> segments)
        {
            ElementLocator result = null;
            foreach (var segment in segments)
            {
                result = segment.ChangeParent(result);
            }
            return result?.ChangeType(type);
        }

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
            var parseResult = FullLocatorParser.TryParse(str);
            if (!parseResult.WasSuccessful || parseResult.Value == null)
            {
                string message = string.Format(ElementLocatorsResources.ElementLocator_Parse__0__is_not_a_valid_element_locator_, str);
                throw new FormatException(message);
            }
            return parseResult.Value;
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

    }
}
