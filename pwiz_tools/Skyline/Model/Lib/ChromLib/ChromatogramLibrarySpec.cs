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
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Lib.ChromLib
{
    [XmlRoot("chromatogram_library_spec")]
    public class ChromatogramLibrarySpec : LibrarySpec
    {
        public static readonly string EXT = ".clib"; // Not L10N
        public static readonly PeptideRankId PEPTIDE_RANK_PEAK_AREA = new PeptideRankId("Peak area", Resources.ChromatogramLibrarySpec_PEPTIDE_RANK_PEAK_AREA_Peak_Area); // Not L10N
        public ChromatogramLibrarySpec(string name, string path) : base(name, path)
        {
            
        }
        public override Library LoadLibrary(ILoadMonitor loader)
        {
            return ChromatogramLibrary.LoadFromDatabase(this, loader);
        }

        public override IEnumerable<PeptideRankId> PeptideRankIds
        {
            get { return new[] {PEPTIDE_RANK_PEAK_AREA}; }
        }

        #region Implementation of IXmlSerializable
        /// <summary>
        /// For serialization
        /// </summary>
        private ChromatogramLibrarySpec()
        {
        }

        public static ChromatogramLibrarySpec Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new ChromatogramLibrarySpec());
        }
        #endregion
    }
}
