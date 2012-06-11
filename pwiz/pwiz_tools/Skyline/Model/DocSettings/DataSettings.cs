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
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
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
        private ReadOnlyCollection<AnnotationDef> _annotationDefs;
        public DataSettings(IList<AnnotationDef> annotationDefs)
        {
            AnnotationDefs = annotationDefs;
        }

        public IList<AnnotationDef> AnnotationDefs
        {
            get { return _annotationDefs; }
            private set { _annotationDefs = MakeReadOnly(value); }
        }

        #region Property change methods
        public DataSettings ChangeAnnotationDefs(IList<AnnotationDef> annotationDefs)
        {
            return ChangeProp(ImClone(this), (im, v) => im.AnnotationDefs = v, annotationDefs);
        }
        #endregion

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
            var annotationDefs = new List<AnnotationDef>();
            // Consume tag
            if (reader.IsEmptyElement)
                reader.Read();
            else
            {
                reader.ReadStartElement();
                while (true)
                {
                    var annotationDef = reader.DeserializeElement<AnnotationDef>();
                    if (annotationDef == null)
                    {
                        break;
                    }
                    annotationDefs.Add(annotationDef);
                }
                reader.ReadEndElement();
            }
            AnnotationDefs = annotationDefs;
        }

        public void WriteXml(XmlWriter writer)
        {
            foreach (var annotationDef in AnnotationDefs)
            {
                writer.WriteElement(annotationDef);
            }
        }

        #region object overrides
        public bool Equals(DataSettings other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return ArrayUtil.EqualsDeep(other._annotationDefs, _annotationDefs);
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
            return _annotationDefs.GetHashCodeDeep();
        }
        #endregion
    }
}
