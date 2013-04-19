/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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
using System.Xml;
using System.Xml.Serialization;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Lib
{
    [XmlRoot("spectrast_lib_spec")]
    public sealed class SpectrastSpec : NistLibSpecBase
    {
        public const string EXT = ".sptxt"; // Not L10N

        public SpectrastSpec(string name, string path)
            : base(name, path)
        {
        }

        public override Library LoadLibrary(ILoadMonitor loader)
        {
            return SpectrastLibrary.Load(this, loader);
        }

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private SpectrastSpec()
        {
        }

        public static SpectrastSpec Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new SpectrastSpec());
        }

        #endregion

        #region object overrides

        public bool Equals(SpectrastSpec other)
        {
            return base.Equals(other);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as SpectrastSpec);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        #endregion
    }

    [XmlRoot("spectrast_spectrum_info")]
    public sealed class SpectrastSpectrumHeaderInfo : NistSpectrumHeaderInfoBase
    {
        public SpectrastSpectrumHeaderInfo(string libraryName, float tfRatio, float totalIntensity, int spectrumCount)
            : base(libraryName, tfRatio, totalIntensity, spectrumCount)
        {
        }

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private SpectrastSpectrumHeaderInfo()
        {
        }

        public static SpectrastSpectrumHeaderInfo Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new SpectrastSpectrumHeaderInfo());
        }

        #endregion
    }

    [XmlRoot("spectrast_library")]
    public sealed class SpectrastLibrary : NistLibraryBase
    {
        public const string EXT_CACHE = ".splc"; // Not L10N

        public static SpectrastLibrary Load(LibrarySpec spec, ILoadMonitor loader)
        {
            return (SpectrastLibrary)Load(spec, new SpectrastLibrary(spec), loader);
        }

        /// <summary>
        /// Controlled access to this <see cref="Immutable"/> class, which should be
        /// created through <see cref="Load(LibrarySpec,ILoadMonitor)"/>.
        /// </summary>
        private SpectrastLibrary(LibrarySpec spec)
            : base(spec, EXT_CACHE)
        {
        }

        public override LibraryDetails LibraryDetails
        {
            get
            {
                LibraryDetails details = new LibraryDetails { Format = "SpectraST", PeptideCount = SpectrumCount }; // Not L10N

                if (!string.IsNullOrEmpty(Id))
                {
                    details.Id = Id;
                }
                if (!string.IsNullOrEmpty(Revision))
                {
                    details.Revision = Revision;
                }
                
                details.AddLink(LibraryLink.PEPTIDEATLAS);
                details.AddLink(LibraryLink.NIST);

                return details;
            }
        }

        protected override SpectrumHeaderInfo CreateSpectrumHeaderInfo(NistSpectrumInfo info)
        {
            return new SpectrastSpectrumHeaderInfo(Name, info.TFRatio, info.TotalIntensity, info.Copies);
        }

        public override LibrarySpec CreateSpec(string path)
        {
            return new SpectrastSpec(Name, path);
        }

        public override string SpecFilter
        {
            get { return TextUtil.FileDialogFilterAll(Resources.SpectrastLibrary_SpecFilter_SpectraST_Library, SpectrastSpec.EXT); }
        }

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private SpectrastLibrary()
        {
        }

        public static SpectrastLibrary Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new SpectrastLibrary());
        }

        #endregion

        #region object overrides

        public bool Equals(SpectrastLibrary other)
        {
            return base.Equals(other);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as SpectrastLibrary);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        #endregion
    }
}
