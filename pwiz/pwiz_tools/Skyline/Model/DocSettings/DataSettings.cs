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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.GroupComparison;
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
        private ReadOnlyCollection<AnnotationDef> _annotationDefs;
        private ReadOnlyCollection<GroupComparisonDef> _groupComparisonDefs;
        public DataSettings(IList<AnnotationDef> annotationDefs)
        {
            AnnotationDefs = annotationDefs;
            _groupComparisonDefs = MakeReadOnly(new GroupComparisonDef[0]);
        }

        public IList<AnnotationDef> AnnotationDefs
        {
            get { return _annotationDefs; }
            private set { _annotationDefs = MakeReadOnly(value); }
        }

        public IList<GroupComparisonDef> GroupComparisonDefs
        {
            get { return _groupComparisonDefs;}
            private set { _groupComparisonDefs = MakeReadOnly(value); }
        }

        #region Property change methods
        public DataSettings ChangeAnnotationDefs(IList<AnnotationDef> annotationDefs)
        {
            return ChangeProp(ImClone(this), (im, v) => im.AnnotationDefs = v, annotationDefs);
        }

        public DataSettings ChangeGroupComparisonDefs(IList<GroupComparisonDef> groupComparisonDefs)
        {
            return ChangeProp(ImClone(this), im => im.GroupComparisonDefs = groupComparisonDefs);
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
            var allElements = new List<XmlNamedElement>();
            // Consume tag
            if (reader.IsEmptyElement)
                reader.Read();
            else
            {
                reader.ReadStartElement();
                reader.ReadElements(allElements, GetElementHelpers());
                reader.ReadEndElement();
            }
            AnnotationDefs = allElements.OfType<AnnotationDef>().ToArray();
            GroupComparisonDefs = allElements.OfType<GroupComparisonDef>().ToArray();
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteElements(AnnotationDefs.Cast<XmlNamedElement>().Concat(GroupComparisonDefs), GetElementHelpers());
        }

        private static IXmlElementHelper<XmlNamedElement>[] GetElementHelpers()
        {
            return new IXmlElementHelper<XmlNamedElement>[]
                {
                    new XmlElementHelperSuper<AnnotationDef, XmlNamedElement>(),
                    new XmlElementHelperSuper<GroupComparisonDef, XmlNamedElement>(), 
                };
        }
        #endregion

        #region object overrides
        public bool Equals(DataSettings other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return ArrayUtil.EqualsDeep(other._annotationDefs, _annotationDefs) 
                && ArrayUtil.EqualsDeep(other._groupComparisonDefs, _groupComparisonDefs);
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
                return result;
            }
        }
        #endregion
    }
}
