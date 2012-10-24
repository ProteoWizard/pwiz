/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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

using System.IO;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.RetentionTimes
{
    /// <summary>
    /// Holds the name of the library that has retention time information for a 
    /// particular data file.
    /// The <see cref="RetentionTimeSource.Name"/> property of this object
    /// is the base name (<see cref="Path.GetFileNameWithoutExtension" />) of a
    /// data file and the <see cref="Library"/> property is a library name
    /// in the document.
    /// </summary>
    [XmlRoot("rt_source")]
    public class RetentionTimeSource : XmlNamedElement
    {
        public RetentionTimeSource(string name, string library) : base(name)
        {
            Library = library;
        }

        public string Library { get; private set; }

        #region object overrides
		public bool Equals(RetentionTimeSource other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return base.Equals(other) && Equals(other.Library, Library);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as RetentionTimeSource);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (base.GetHashCode()*397) ^ (Library != null ? Library.GetHashCode() : 0);
            }
        }
	    #endregion
        #region Implementation of IXmlSerializable
        private enum Attr
        {
            library,
        }
        /// <summary>
        /// For serialization
        /// </summary>
        private RetentionTimeSource()
        {
        }

        public static RetentionTimeSource Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new RetentionTimeSource());
        }

        public override void ReadXml(XmlReader reader)
        {
            base.ReadXml(reader);
            Library = reader.GetAttribute(Attr.library);
            reader.Read();
        }

        public override void WriteXml(XmlWriter writer)
        {
            base.WriteXml(writer);
            writer.WriteAttribute(Attr.library, Library);
        }
        #endregion

    }
}
