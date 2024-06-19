/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.DocSettings
{
    [XmlRoot("relative_abundance_formatting")]
    public class RelativeAbundanceFormatting : Immutable, IXmlSerializable
    {
        public static readonly RelativeAbundanceFormatting DEFAULT = new RelativeAbundanceFormatting
        {
            _colorRows = ImmutableList<MatchRgbHexColor>.EMPTY
        };
        private ImmutableList<MatchRgbHexColor> _colorRows;

        private RelativeAbundanceFormatting()
        {
        }

        [TrackChildren]
        public IEnumerable<MatchRgbHexColor> ColorRows
        {
            get
            {
                return _colorRows.Select(row => row.Clone());
            }
        }

        public RelativeAbundanceFormatting ChangeColorRows(IEnumerable<MatchRgbHexColor> colorRows)
        {
            var newColorRows = ImmutableList.ValueOfOrEmpty(colorRows?.Select(row => row.Clone()));
            if (Equals(newColorRows, _colorRows))
            {
                return this;
            }
            return ChangeProp(ImClone(this), im=>im._colorRows = newColorRows);
        }

        XmlSchema IXmlSerializable.GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            if (_colorRows != null)
            {
                throw new InvalidOperationException();
            }

            bool empty = reader.IsEmptyElement;
            reader.Read();
            if (!empty)
            {
                var colorRows = new List<MatchRgbHexColor>();
                reader.ReadElements(colorRows);
                _colorRows = ImmutableList.ValueOf(colorRows);
                reader.ReadEndElement();
            }
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteElements(_colorRows);
        }

        protected bool Equals(RelativeAbundanceFormatting other)
        {
            return _colorRows.Equals(other._colorRows);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((RelativeAbundanceFormatting)obj);
        }

        public override int GetHashCode()
        {
            return _colorRows.GetHashCode();
        }

        public static RelativeAbundanceFormatting Deserialize(XmlReader reader)
        {
            var relativeAbundanceFormatting = new RelativeAbundanceFormatting();
            relativeAbundanceFormatting.ReadXml(reader);
            return relativeAbundanceFormatting;
        }
    }
}
