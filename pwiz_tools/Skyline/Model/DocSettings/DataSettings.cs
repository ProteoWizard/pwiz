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
using System.Linq;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.DocSettings.MetadataExtraction;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Lists;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.DocSettings
{
    /// <summary>
    /// Data settings in an SrmDocument.  This includes annotation definitions, and in the future will
    /// include Report definitions.
    /// </summary>
    [XmlRoot("data_settings")]
    public class DataSettings : Immutable, IXmlSerializable
    {
        public static DataSettings DEFAULT = new DataSettings(new AnnotationDef[0]);
        private ImmutableList<AnnotationDef> _annotationDefs;
        private ImmutableList<GroupComparisonDef> _groupComparisonDefs;
        private bool _auditLogging;

        public DataSettings(IEnumerable<AnnotationDef> annotationDefs)
        {
            _annotationDefs = MakeReadOnly(annotationDefs);
            _groupComparisonDefs = MakeReadOnly(new GroupComparisonDef[0]);
            ViewSpecList = ViewSpecList.EMPTY;
            AuditLogging = true;
            Lists = ImmutableList<ListData>.EMPTY;
            MetadataRuleSets = ImmutableList<MetadataRuleSet>.EMPTY;
            RelativeAbundanceFormatting = RelativeAbundanceFormatting.DEFAULT;
        }

        [TrackChildren(true)]
        public ImmutableList<AnnotationDef> AnnotationDefs
        {
            get { return _annotationDefs; }
        }

        [TrackChildren(true)]
        public ImmutableList<GroupComparisonDef> GroupComparisonDefs
        {
            get { return _groupComparisonDefs;}
        }

        [TrackChildren(true)]
        public ImmutableList<ListData> Lists { get; private set; }

        public ListData FindList(string name)
        {
            return Lists.FirstOrDefault(list => list.ListDef.Name == name);
        }

        [TrackChildren(ignoreName:true)]
        public ViewSpecList ViewSpecList { get; private set; }

        [Track]
        public Uri PanoramaPublishUri { get; private set; }

        [TrackChildren]
        public ImmutableList<MetadataRuleSet> MetadataRuleSets
        {
            get;
            private set;
        }

        [TrackChildren]
        public RelativeAbundanceFormatting RelativeAbundanceFormatting { get; private set; }

        public DataSettings ChangeRelativeAbundanceFormatting(RelativeAbundanceFormatting relativeAbundanceFormatting)
        {
            return ChangeProp(ImClone(this),
                im => im.RelativeAbundanceFormatting =
                    relativeAbundanceFormatting ?? RelativeAbundanceFormatting.DEFAULT);
        }

        public DataSettings ChangeExtractedMetadata(IEnumerable<MetadataRuleSet> extractedMetadata)
        {
            return ChangeProp(ImClone(this),
                im => im.MetadataRuleSets = ImmutableList.ValueOfOrEmpty(extractedMetadata));
        }

        /// <summary>
        /// True if audit logging is enabled for this document. ModifyDocument calls will generate audit log entries that can be viewed in the
        /// AuditLogForm and are written to a separate file (.skyl) when the document is saved. If audit logging is disabled, no entries are kept
        /// (they are still created for descriptive undo-redo messages) and the audit log file is deleted (if existent) when the document is saved
        ///
        /// Generally AuditLogging will always be true in tests, even if it gets set to false.
        /// (Unless IgnoreTestCheck is true, which is used by the AuditLogSaving test to actually disable the audit log.
        /// </summary>
        [Track]
        public bool AuditLogging
        {
            get { return (Program.FunctionalTest && !AuditLogList.IgnoreTestChecks) || _auditLogging; }

            private set { _auditLogging = value; }
        }

        /// <summary>
        /// Returns whether audit logging would be enabled for this document if a unit test were not running
        /// </summary>
        public bool IsAuditLoggingEnabled
        {
            get { return _auditLogging; }
        }

        public string DocumentGuid { get; private set; }

        #region Property change methods
        public DataSettings ChangeAnnotationDefs(IList<AnnotationDef> annotationDefs)
        {
            return ChangeProp(ImClone(this), im=> im._annotationDefs = MakeReadOnly(annotationDefs));
        }

        public DataSettings ChangeGroupComparisonDefs(IList<GroupComparisonDef> groupComparisonDefs)
        {
            return ChangeProp(ImClone(this), im => im._groupComparisonDefs = MakeReadOnly(groupComparisonDefs));
        }

        public DataSettings ChangeViewSpecList(ViewSpecList viewSpecList)
        {
            return ChangeProp(ImClone(this), im => im.ViewSpecList = viewSpecList);
        }

        public DataSettings ChangePanoramaPublishUri(Uri newUri)
        {
            if (!newUri.IsWellFormedOriginalString()) // https://msdn.microsoft.com/en-us/library/system.uri.iswellformedoriginalstring
                throw new ArgumentException(string.Format(DocSettingsResources.DataSettings_ChangePanoramaPublishUri_The_URI__0__is_not_well_formed_, newUri));
            return ChangeProp(ImClone(this), im => im.PanoramaPublishUri = newUri);
        }

        public DataSettings ChangeAuditLogging(bool enabled)
        {
            return ChangeProp(ImClone(this), im => im.AuditLogging = enabled);
        }

        public DataSettings ChangeDocumentGuid()
        {
            return ChangeProp(ImClone(this), im => im.DocumentGuid = Guid.NewGuid().ToString());
        }

        public DataSettings ChangeListDefs(IEnumerable<ListData> lists)
        {
            return ChangeProp(ImClone(this), im => im.Lists = ImmutableList.ValueOfOrEmpty(lists));
        }

        public DataSettings ReplaceList(ListData listData)
        {
            int index = Lists.IndexOf(list => list.ListDef.Name == listData.ListDef.Name);
            if (index < 0)
            {
                throw new ArgumentException();
            }
            return ChangeProp(ImClone(this), im => im.Lists = im.Lists.ReplaceAt(index, listData));
        }

        public DataSettings AddListDef(ListData listDef)
        {
            var listDatas = Lists.ToList();
            int index = GroupComparisonDefs.IndexOf(def => def.Name == listDef.ListName);
            if (index < 0)
            {
                listDatas.Add(listDef);
            }
            else
            {
                listDatas[index] = listDef; // CONSIDER: Preserve data?
            }
            return ChangeListDefs(listDatas);
        }

        public DataSettings AddGroupComparisonDef(GroupComparisonDef groupComparisonDef)
        {
            var groupComparisonDefs = GroupComparisonDefs.ToList();
            int index = GroupComparisonDefs.IndexOf(def => def.Name == groupComparisonDef.Name);
            if (index < 0)
            {
                groupComparisonDefs.Add(groupComparisonDef);
            }
            else
            {
                groupComparisonDefs[index] = groupComparisonDef;
            }
            return ChangeGroupComparisonDefs(groupComparisonDefs);
        }

        #endregion

        #region Serialization Methods
        /// <summary>
        /// For serialization
        /// </summary>
        private DataSettings()
        {
        }

        public static DataSettings Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new DataSettings());
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            string uri = reader.GetAttribute(Attr.panorama_publish_uri);
            if (!string.IsNullOrEmpty(uri))
                PanoramaPublishUri = new Uri(uri);
            string docGuid = reader.GetAttribute(Attr.document_guid);
            if (!string.IsNullOrEmpty(docGuid))
                DocumentGuid = docGuid;
            AuditLogging = reader.GetBoolAttribute(Attr.audit_logging);

            var allElements = new List<IXmlSerializable>();
            // Consume tag
            if (reader.IsEmptyElement)
                reader.Read();
            else
            {
                reader.ReadStartElement();
                reader.ReadElements(allElements, GetElementHelpers());
                reader.ReadEndElement();
            }
            _annotationDefs = MakeReadOnly(allElements.OfType<AnnotationDef>());
            _groupComparisonDefs = MakeReadOnly(allElements.OfType<GroupComparisonDef>());
            ViewSpecList = allElements.OfType<ViewSpecList>().FirstOrDefault() ?? ViewSpecList.EMPTY;
            Lists= ImmutableList.ValueOf(allElements.OfType<ListData>());
            MetadataRuleSets = ImmutableList.ValueOf(allElements.OfType<MetadataRuleSet>());
            RelativeAbundanceFormatting = allElements.OfType<RelativeAbundanceFormatting>()
                .FirstOrDefault() ?? RelativeAbundanceFormatting.DEFAULT;
        }

        private enum Attr
        {
            panorama_publish_uri,
            document_guid,
            audit_logging
        }

        public void WriteXml(XmlWriter writer)
        {
            if(PanoramaPublishUri != null)
                writer.WriteAttributeIfString(Attr.panorama_publish_uri, PanoramaPublishUri.ToString());
//            Assume.IsFalse(string.IsNullOrEmpty(DocumentGuid)); // Should have a document GUID by this point
            if(!string.IsNullOrEmpty(DocumentGuid))
                writer.WriteAttributeString(Attr.document_guid, DocumentGuid);
            writer.WriteAttribute(Attr.audit_logging, _auditLogging);
            var elements = AnnotationDefs.Cast<IXmlSerializable>()
                .Concat(GroupComparisonDefs)
                .Concat(Lists)
                .Concat(MetadataRuleSets);
            if (ViewSpecList.ViewSpecs.Any())
            {
                elements = elements.Concat(new[] {ViewSpecList});
            }

            if (!Equals(RelativeAbundanceFormatting, RelativeAbundanceFormatting.DEFAULT))
            {
                elements = elements.Append(RelativeAbundanceFormatting);
            }
            writer.WriteElements(elements, GetElementHelpers());
        }

        private static IXmlElementHelper<IXmlSerializable>[] GetElementHelpers()
        {
            return new IXmlElementHelper<IXmlSerializable>[]
            {
                new XmlElementHelperSuper<AnnotationDef, IXmlSerializable>(),
                new XmlElementHelperSuper<GroupComparisonDef, IXmlSerializable>(),
                new XmlElementHelperSuper<ViewSpecList, IXmlSerializable>(),
                new XmlElementHelperSuper<ListData, IXmlSerializable>(),
                new XmlElementHelperSuper<MetadataRuleSet, IXmlSerializable>(),
                new XmlElementHelperSuper<RelativeAbundanceFormatting, IXmlSerializable>()
            };
        }
        #endregion

        #region object overrides
        public bool Equals(DataSettings other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other._annotationDefs, _annotationDefs)
                   && Equals(other._groupComparisonDefs, _groupComparisonDefs)
                   && Equals(ViewSpecList, other.ViewSpecList)
                   && Equals(PanoramaPublishUri, other.PanoramaPublishUri)
                   && Equals(AuditLogging, other.AuditLogging)
                   && Equals(DocumentGuid, other.DocumentGuid)
                   && Equals(Lists, other.Lists)
                   && Equals(MetadataRuleSets, other.MetadataRuleSets)
                   && Equals(RelativeAbundanceFormatting, other.RelativeAbundanceFormatting);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (DataSettings)) return false;
            return Equals((DataSettings) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = _annotationDefs.GetHashCode();
                result = result * 397 ^ _groupComparisonDefs.GetHashCode();
                result = result * 397 ^ ViewSpecList.GetHashCode();
                result = result * 397 ^ (PanoramaPublishUri == null ? 0 : PanoramaPublishUri.GetHashCode());
                result = result * 397 ^ AuditLogging.GetHashCode();
                result = result * 397 ^ (DocumentGuid == null ? 0 : DocumentGuid.GetHashCode());
                result = result * 397 ^ Lists.GetHashCode();
                result = result * 397 ^ MetadataRuleSets.GetHashCode();
                result = result * 397 ^ RelativeAbundanceFormatting.GetHashCode();
                return result;
            }
        }
        #endregion
    }
}
