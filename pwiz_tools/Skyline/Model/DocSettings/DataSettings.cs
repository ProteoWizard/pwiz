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
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Properties;
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
        public DataSettings(IEnumerable<AnnotationDef> annotationDefs)
        {
            _annotationDefs = MakeReadOnly(annotationDefs);
            _groupComparisonDefs = MakeReadOnly(new GroupComparisonDef[0]);
            ViewSpecList = ViewSpecList.EMPTY;
        }

        public ImmutableList<AnnotationDef> AnnotationDefs
        {
            get { return _annotationDefs; }
        }

        public ImmutableList<GroupComparisonDef> GroupComparisonDefs
        {
            get { return _groupComparisonDefs;}
        }

        public ViewSpecList ViewSpecList { get; private set; }

        public Uri PanoramaPublishUri { get; private set; }

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
                throw new ArgumentException(string.Format(Resources.DataSettings_ChangePanoramaPublishUri_The_URI__0__is_not_well_formed_, newUri));
            return ChangeProp(ImClone(this), im => im.PanoramaPublishUri = newUri);
        }

        public DataSettings ChangeDocumentGuid()
        {
            return ChangeProp(ImClone(this), im => im.DocumentGuid = Guid.NewGuid().ToString());
        }
        #endregion

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
        }

        private enum Attr
        {
            panorama_publish_uri,
            document_guid
        }

        public void WriteXml(XmlWriter writer)
        {
            if(PanoramaPublishUri != null)
                writer.WriteAttributeIfString(Attr.panorama_publish_uri, PanoramaPublishUri.ToString());
//            Assume.IsFalse(string.IsNullOrEmpty(DocumentGuid)); // Should have a document GUID by this point
            if(!string.IsNullOrEmpty(DocumentGuid))
                writer.WriteAttributeString(Attr.document_guid, DocumentGuid);
            var elements = AnnotationDefs.Cast<IXmlSerializable>().Concat(GroupComparisonDefs);
            if (ViewSpecList.ViewSpecs.Any())
            {
                elements = elements.Concat(new[] {ViewSpecList});
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
                };
        }
        #endregion

        #region object overrides
        public bool Equals(DataSettings other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return ArrayUtil.EqualsDeep(other._annotationDefs, _annotationDefs)
                   && ArrayUtil.EqualsDeep(other._groupComparisonDefs, _groupComparisonDefs)
                   && Equals(ViewSpecList, other.ViewSpecList)
                   && Equals(PanoramaPublishUri, other.PanoramaPublishUri)
                   && Equals(DocumentGuid, other.DocumentGuid);
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
                int result = _annotationDefs.GetHashCodeDeep();
                result = result*397 + _groupComparisonDefs.GetHashCodeDeep();
                result = result*397 + ViewSpecList.GetHashCode();
                result = result*397 + (PanoramaPublishUri == null ? 0 : PanoramaPublishUri.GetHashCode());
                result = result*397 + (DocumentGuid == null ? 0 : DocumentGuid.GetHashCode());
                return result;
            }
        }
        #endregion
    }
}
