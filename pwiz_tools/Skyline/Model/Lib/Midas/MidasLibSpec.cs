/*
 * Original author: Kaipo Tamura <kaipot .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Lib.Midas
{
    [XmlRoot("midas_lib_spec")]
    public sealed class MidasLibSpec : LibrarySpec
    {
        public const string EXT = ".midas";
        public const string PREFIX = "MIDAS_";

        private static readonly PeptideRankId[] RANK_IDS = { PEP_RANK_COPIES, PEP_RANK_PICKED_INTENSITY };

        public static string FILTER_MIDAS
        {
            get { return TextUtil.FileDialogFilterAll(Resources.MidasLibrary_SpecFilter_MIDAS_Spectral_Library, EXT); }
        }

        public static string GetName(string documentPath, IEnumerable<LibrarySpec> libSpecs = null)
        {
            var name = PREFIX + Path.GetFileNameWithoutExtension(documentPath);
            return libSpecs != null
                ? Helpers.GetUniqueName(name, libSpecs.Select(libSpec => libSpec.Name).ToArray())
                : name;
        }
        
        public static string GetLibraryFileName(string documentPath)
        {
            return Path.ChangeExtension(documentPath, EXT);
        }

        public MidasLibSpec(string name, string path)
            : base(name, path)
        {
        }

        public override string Filter
        {
            get { return FILTER_MIDAS; }
        }

        public override Library LoadLibrary(ILoadMonitor loader)
        {
            return MidasLibrary.Load(this, loader);
        }

        public override IEnumerable<PeptideRankId> PeptideRankIds
        {
            get { return RANK_IDS; }
        }

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private MidasLibSpec()
        {
        }

        public static MidasLibSpec Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new MidasLibSpec());
        }

        #endregion
    }
}
