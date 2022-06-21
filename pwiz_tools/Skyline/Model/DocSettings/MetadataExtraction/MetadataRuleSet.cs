/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.DocSettings.MetadataExtraction
{
    public class MetadataRule : Immutable
    {
        public static MetadataRule EMPTY = new MetadataRule();
        [Track]
        public PropertyPath Source { get; private set; }

        public MetadataRule ChangeSource(PropertyPath value)
        {
            return ChangeProp(ImClone(this), im => im.Source = value);
        }

        [Track(defaultValues: typeof(DefaultValuesNull))]
        public string Pattern { get; private set; }

        public MetadataRule ChangePattern(string value)
        {
            return ChangeProp(ImClone(this), im => im.Pattern = value);
        }
        [Track(defaultValues: typeof(DefaultValuesNull))]
        public string Replacement { get; private set; }

        public MetadataRule ChangeReplacement(string replacement)
        {
            return ChangeProp(ImClone(this), im => im.Replacement = replacement);
        }
        [Track]
        public PropertyPath Target { get; private set; }

        public MetadataRule ChangeTarget(PropertyPath value)
        {
            return ChangeProp(ImClone(this), im => im.Target = value);
        }
        protected bool Equals(MetadataRule other)
        {
            return Equals(Source, other.Source) && Equals(Pattern, other.Pattern) &&
                   Equals(Target, other.Target) && Equals(Replacement, other.Replacement);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((MetadataRule)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Pattern != null ? Pattern.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Replacement != null ? Replacement.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Source != null ? Source.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Target != null ? Target.GetHashCode() : 0);
                return hashCode;
            }
        }

    }
    [XmlRoot("metadata_rule_set")]
    public class MetadataRuleSet : Immutable, IXmlSerializable, IKeyContainer<string>
    {
        public MetadataRuleSet(Type rowType) : this(rowType.FullName, null)
        {

        }
        public MetadataRuleSet(string rowSource, IEnumerable<MetadataRule> rules)
        {
            RowSource = rowSource;
            Rules = ImmutableList.ValueOfOrEmpty(rules);
        }

        [Track]
        public string Name { get; private set; }

        public MetadataRuleSet ChangeName(string name)
        {
            return ChangeProp(ImClone(this), im => im.Name = name);
        }

        [Track]
        public string RowSource { get; private set; }
        [TrackChildren]
        public ImmutableList<MetadataRule> Rules { get; private set; }

        public MetadataRuleSet ChangeRules(IEnumerable<MetadataRule> rules)
        {
            return ChangeProp(ImClone(this), im => im.Rules = ImmutableList.ValueOf(rules));
        }

        private enum ATTR
        {
            name,
            rowsource,
            source,
            target,
            pattern,
            replacement
        }

        private enum EL
        {
            rule
        }

        private MetadataRuleSet()
        {

        }

        public void ReadXml(XmlReader reader)
        {
            if (Rules != null)
            {
                throw new InvalidOperationException();
            }

            var xElement = (XElement) XNode.ReadFrom(reader);
            Name = xElement.Attribute(ATTR.name)?.Value;
            RowSource = xElement.Attribute(ATTR.rowsource)?.Value;
            var rules = new List<MetadataRule>();
            foreach (var child in xElement.Elements(EL.rule))
            {
                var rule = new MetadataRule();
                var attrSource = child.Attribute(ATTR.source);
                if (attrSource != null)
                {
                    rule = rule.ChangeSource(PropertyPath.Parse(attrSource.Value));
                }

                rule = rule.ChangePattern(child.Attribute(ATTR.pattern)?.Value);
                rule = rule.ChangeReplacement(child.Attribute(ATTR.replacement)?.Value);
                var attrTarget = child.Attribute(ATTR.target);
                if (attrTarget != null)
                {
                    rule = rule.ChangeTarget(PropertyPath.Parse(attrTarget.Value));
                }
                rules.Add(rule);
            }
            Rules = ImmutableList.ValueOf(rules);
        }

        void IXmlSerializable.WriteXml(XmlWriter writer)
        {
            writer.WriteAttributeIfString(ATTR.name, Name);
            writer.WriteAttributeIfString(ATTR.rowsource, RowSource);
            foreach (var rule in Rules)
            {
                writer.WriteStartElement(EL.rule);
                writer.WriteAttribute(ATTR.source, rule.Source, null);
                writer.WriteAttributeIfString(ATTR.pattern, rule.Pattern);
                writer.WriteAttributeIfString(ATTR.replacement, rule.Replacement);
                writer.WriteAttribute(ATTR.target, rule.Target, null);
                writer.WriteEndElement();
            }
        }

        XmlSchema IXmlSerializable.GetSchema()
        {
            return null;
        }

        string IKeyContainer<string>.GetKey()
        {
            return Name;
        }

        public static MetadataRuleSet Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new MetadataRuleSet());
        }

        protected bool Equals(MetadataRuleSet other)
        {
            return Name == other.Name && RowSource == other.RowSource && Equals(Rules, other.Rules);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((MetadataRuleSet) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Name != null ? Name.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (RowSource != null ? RowSource.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Rules != null ? Rules.GetHashCode() : 0);
                return hashCode;
            }
        }
    }
}