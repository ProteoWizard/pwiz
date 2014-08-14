/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.Xml;
using System.Xml.Serialization;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Lib.ChromLib
{
    [XmlRoot("chromatogram_library_spectrum_header_info")]
    public class ChromLibSpectrumHeaderInfo : SpectrumHeaderInfo
    {
        public ChromLibSpectrumHeaderInfo(string libraryName, double peakArea) : base(libraryName)
        {
            PeakArea = peakArea;
        }

        public double PeakArea { get; private set; }

        public override float GetRankValue(PeptideRankId rankId)
        {
            if (rankId == ChromatogramLibrarySpec.PEPTIDE_RANK_PEAK_AREA)
            {
                return (float) PeakArea;
            }
            return base.GetRankValue(rankId);
        }

        public override IEnumerable<KeyValuePair<PeptideRankId, string>> RankValues
        {
            get
            {
                return new[]
                    {
                        new KeyValuePair<PeptideRankId, string>(
                            ChromatogramLibrarySpec.PEPTIDE_RANK_PEAK_AREA,
                            string.Format("{0:F0}", PeakArea)) // Not L10N
                    };
            }
        }
        #region Implementation of IXmlSerializable

        /// <summary>
        /// For XML serialization
        /// </summary>
        private ChromLibSpectrumHeaderInfo()
        {
        }

        private enum ATTR
        {
            peak_area
        }

        public static ChromLibSpectrumHeaderInfo Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new ChromLibSpectrumHeaderInfo());
        }

        public override void ReadXml(XmlReader reader)
        {
            // Read tag attributes
            base.ReadXml(reader);
            PeakArea = reader.GetDoubleAttribute(ATTR.peak_area);
            // Consume tag
            reader.Read();
        }

        public override void WriteXml(XmlWriter writer)
        {
            // Write tag attributes
            base.WriteXml(writer);
            writer.WriteAttribute(ATTR.peak_area, PeakArea);
        }
        #endregion

        #region object overrides
        protected bool Equals(ChromLibSpectrumHeaderInfo other)
        {
            return base.Equals(other) && PeakArea.Equals(other.PeakArea);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ChromLibSpectrumHeaderInfo) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (base.GetHashCode()*397) ^ PeakArea.GetHashCode();
            }
        }
        #endregion
    }
}
