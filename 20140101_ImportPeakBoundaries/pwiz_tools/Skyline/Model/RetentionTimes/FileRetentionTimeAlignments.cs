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

using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.RetentionTimes
{
    /// <summary>
    /// List of retention time alignments aligned to one particular data file.
    /// </summary>
    [XmlRoot("file_rt_alignments")]
    public class FileRetentionTimeAlignments : XmlNamedElement
    {
        public FileRetentionTimeAlignments(string dataFileName, IEnumerable<RetentionTimeAlignment> alignments) : base(dataFileName)
        {
            RetentionTimeAlignments = ResultNameMap.FromNamedElements(alignments);
        }

        public ResultNameMap<RetentionTimeAlignment> RetentionTimeAlignments { get; private set; }

        #region Object Overrides
        public bool Equals(FileRetentionTimeAlignments other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return base.Equals(other) && Equals(other.RetentionTimeAlignments, RetentionTimeAlignments);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as FileRetentionTimeAlignments);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (base.GetHashCode()*397) ^ RetentionTimeAlignments.GetHashCode();
            }
        }
        #endregion

        #region Implementation of IXmlSerializable
        private FileRetentionTimeAlignments()
        {
        }

        public override void WriteXml(XmlWriter writer)
        {
            base.WriteXml(writer);
            writer.WriteElements(RetentionTimeAlignments.Values);
        }

        public static FileRetentionTimeAlignments Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new FileRetentionTimeAlignments());
        }

        public override void ReadXml(XmlReader reader)
        {
            if (null != RetentionTimeAlignments)
            {
                throw new InvalidOperationException();
            }
            base.ReadXml(reader);
            var retentionTimeAlignments = new List<RetentionTimeAlignment>();
            if (reader.IsEmptyElement)
            {
                reader.Read();
            }
            else
            {
                // Read past the property element
                reader.Read();
                reader.ReadElements(retentionTimeAlignments);
                reader.ReadEndElement();
            }
            RetentionTimeAlignments = ResultNameMap.FromNamedElements(retentionTimeAlignments);
        }
        #endregion
    }

    [XmlRoot("rt_alignment")]
    public class RetentionTimeAlignment : XmlNamedElement
    {
        public RetentionTimeAlignment(string name, RegressionLine regressionLine) : base(name)
        {
            RegressionLine = regressionLine;
        }

        public RetentionTimeAlignment(string name, RegressionLineElement regressionLineElement) 
            : this(name, new RegressionLine(regressionLineElement.Slope, regressionLineElement.Intercept))
        {
        }

        public RegressionLine RegressionLine { get; private set; }
        #region Object Overrides
        public bool Equals(RetentionTimeAlignment other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return base.Equals(other) && Equals(other.RegressionLine, RegressionLine);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as RetentionTimeAlignment);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (base.GetHashCode() * 397) ^ RegressionLine.GetHashCode();
            }
        }
        
        #endregion
        #region Implementation of IXmlSerializable
        /// <summary>
        /// For serialization
        /// </summary>
        private RetentionTimeAlignment()
        {
        }

        public static RetentionTimeAlignment Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new RetentionTimeAlignment());
        }

        public override void ReadXml(XmlReader reader)
        {
            base.ReadXml(reader);
            RegressionLine = RegressionLine.Deserialize(reader);
            reader.Read();
        }

        public override void WriteXml(XmlWriter writer)
        {
            base.WriteXml(writer);
            RegressionLine.WriteXmlAttributes(writer);
        }
        #endregion
    }

    public class RetentionTimeAlignmentIndices : List<RetentionTimeAlignmentIndex>
    {
        public RetentionTimeAlignmentIndices(FileRetentionTimeAlignments alignments)
        {
            if (alignments != null)
            {
                foreach (var alignment in alignments.RetentionTimeAlignments.Values)
                    Add(new RetentionTimeAlignmentIndex(alignment));
            }
        }
    }

    public class RetentionTimeAlignmentIndex
    {
        public RetentionTimeAlignmentIndex(RetentionTimeAlignment alignment)
        {
            Alignment = alignment;
        }

        public RetentionTimeAlignment Alignment { get; private set; }
        public int? FileIndex { get; set; }
    }
}
