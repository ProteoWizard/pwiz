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
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.DocSettings.MetadataExtraction
{
    public class MetadataRuleStep : Immutable
    {
        public static MetadataRuleStep EMPTY = new MetadataRuleStep();
        [Track]
        public PropertyPath Source { get; private set; }

        public MetadataRuleStep ChangeSource(PropertyPath value)
        {
            return ChangeProp(ImClone(this), im => im.Source = value);
        }

        [Track(defaultValues: typeof(DefaultValuesNull))]
        public string Pattern { get; private set; }

        public MetadataRuleStep ChangePattern(string value)
        {
            return ChangeProp(ImClone(this), im => im.Pattern = value);
        }
        [Track(defaultValues: typeof(DefaultValuesNull))]
        public string Replacement { get; private set; }

        public MetadataRuleStep ChangeReplacement(string replacement)
        {
            return ChangeProp(ImClone(this), im => im.Replacement = replacement);
        }
        [Track]
        public PropertyPath Target { get; private set; }

        public MetadataRuleStep ChangeTarget(PropertyPath value)
        {
            return ChangeProp(ImClone(this), im => im.Target = value);
        }
        protected bool Equals(MetadataRuleStep other)
        {
            return Equals(Source, other.Source) && Equals(Pattern, other.Pattern) &&
                   Equals(Target, other.Target) && Equals(Replacement, other.Replacement);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((MetadataRuleStep)obj);
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
    [XmlRoot("metadata_rule")]
    public class MetadataRule : Immutable, IXmlSerializable, IKeyContainer<string>
    {
        public MetadataRule(Type rowType) : this(rowType.FullName, null)
        {

        }
        public MetadataRule(string rowSource, IEnumerable<MetadataRuleStep> rules)
        {
            RowSource = rowSource;
            Steps = ImmutableList.ValueOfOrEmpty(rules);
        }

        [Track]
        public string Name { get; private set; }

        public MetadataRule ChangeName(string name)
        {
            return ChangeProp(ImClone(this), im => im.Name = name);
        }

        [Track]
        public string RowSource { get; private set; }
        [TrackChildren]
        public ImmutableList<MetadataRuleStep> Steps { get; private set; }

        public MetadataRule ChangeSteps(IEnumerable<MetadataRuleStep> rules)
        {
            return ChangeProp(ImClone(this), im => im.Steps = ImmutableList.ValueOf(rules));
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
            step
        }

        private MetadataRule()
        {

        }

        public void ReadXml(XmlReader reader)
        {
            if (Steps != null)
            {
                throw new InvalidOperationException();
            }

            var xElement = (XElement) XNode.ReadFrom(reader);
            Name = xElement.Attribute(ATTR.name)?.Value;
            RowSource = xElement.Attribute(ATTR.rowsource)?.Value;
            var rules = new List<MetadataRuleStep>();
            foreach (var child in xElement.Elements(EL.step))
            {
                var rule = new MetadataRuleStep();
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
            Steps = ImmutableList.ValueOf(rules);
        }

        void IXmlSerializable.WriteXml(XmlWriter writer)
        {
            writer.WriteAttributeIfString(ATTR.name, Name);
            writer.WriteAttributeIfString(ATTR.rowsource, RowSource);
            foreach (var rule in Steps)
            {
                writer.WriteStartElement(EL.step);
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

        public static MetadataRule Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new MetadataRule());
        }

        protected bool Equals(MetadataRule other)
        {
            return Name == other.Name && RowSource == other.RowSource && Equals(Steps, other.Steps);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((MetadataRule) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Name != null ? Name.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (RowSource != null ? RowSource.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Steps != null ? Steps.GetHashCode() : 0);
                return hashCode;
            }
        }
    }

    public class MetadataRuleSetList : SettingsList<MetadataRule>
    {
        public override IEnumerable<MetadataRule> GetDefaults(int revisionIndex)
        {
            yield break;
        }

        public override string Title
        {
            get { return Resources.MetadataRuleSetList_Title_Define_Metadata_Rules; }
        }
        public override string Label
        {
            get { return Resources.MetadataRuleSetList_Label_Metadata_Rule; }
        }

        public override MetadataRule CopyItem(MetadataRule item)
        {
            return item.ChangeName(string.Empty);
        }

        public override MetadataRule EditItem(Control owner, MetadataRule item, IEnumerable<MetadataRule> existing, object tag)
        {
            var documentContainer = (IDocumentContainer) tag;
            using (var dlg = new MetadataRuleEditor(documentContainer, item, existing))
            {
                if (dlg.ShowDialog(owner) == DialogResult.OK)
                {
                    return dlg.MetadataRule;
                }
                else
                {
                    return null;
                }
            }
        }
    }
}