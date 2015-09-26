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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Lib
{
    [XmlRoot("nist_lib_spec")]
    public sealed class NistLibSpec : NistLibSpecBase
    {
        public const string EXT = ".msp"; // Not L10N

        public NistLibSpec(string name, string path)
            : base(name, path)
        {
        }

        public override Library LoadLibrary(ILoadMonitor loader)
        {
            return NistLibrary.Load(this, loader);
        }

        #region Implementation of IXmlSerializable
        
        /// <summary>
        /// For serialization
        /// </summary>
        private NistLibSpec()
        {
        }

        public static NistLibSpec Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new NistLibSpec());
        }

        #endregion

        #region object overrides

        public bool Equals(NistLibSpec other)
        {
            return base.Equals(other);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as NistLibSpec);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        #endregion
    }

    public abstract class NistLibSpecBase : LibrarySpec
    {
        public static readonly PeptideRankId PEP_RANK_TFRATIO =
            new PeptideRankId("TFRatio", Resources.NistLibSpecBase_PEP_RANK_TFRATIO_TFRatio); // Not L10N

        private static readonly PeptideRankId[] RANK_IDS = { PEP_RANK_COPIES, PEP_RANK_TOTAL_INTENSITY, PEP_RANK_PICKED_INTENSITY, PEP_RANK_TFRATIO};

        protected NistLibSpecBase(string name, string path)
            : base(name, path)
        {
        }

        public override IEnumerable<PeptideRankId> PeptideRankIds
        {
            get { return RANK_IDS; }
        }

        #region Implementation of IXmlSerializable
        
        /// <summary>
        /// For serialization
        /// </summary>
        protected NistLibSpecBase()
        {
        }

        #endregion
    }

    [XmlRoot("nist_spectrum_info")]
    public sealed class NistSpectrumHeaderInfo : NistSpectrumHeaderInfoBase
    {
        public NistSpectrumHeaderInfo(string libraryName, float tfRatio, float totalIntensity, int spectrumCount)
            : base(libraryName, tfRatio, totalIntensity, spectrumCount)
        {
        }

        #region Implementation of IXmlSerializable
        
        /// <summary>
        /// For serialization
        /// </summary>
        private NistSpectrumHeaderInfo()
        {
        }

        public static NistSpectrumHeaderInfo Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new NistSpectrumHeaderInfo());
        }

        #endregion
    }

    public class NistSpectrumHeaderInfoBase : SpectrumHeaderInfo
    {
        public NistSpectrumHeaderInfoBase(string libraryName, float tfRatio, float totalIntensity, int spectrumCount)
            : base(libraryName)
        {
            TFRatio = tfRatio;
            TotalIntensity = totalIntensity;
            SpectrumCount = spectrumCount;
        }

        public int SpectrumCount { get; private set; }
        public float TotalIntensity { get; private set; }
// ReSharper disable InconsistentNaming
        public float TFRatio { get; private set; }
// ReSharper restore InconsistentNaming

        public override float GetRankValue(PeptideRankId rankId)
        {
            if (ReferenceEquals(rankId, NistLibSpecBase.PEP_RANK_TFRATIO))
                return TFRatio;
            if (ReferenceEquals(rankId, LibrarySpec.PEP_RANK_TOTAL_INTENSITY))
                return TotalIntensity;
            if (ReferenceEquals(rankId, LibrarySpec.PEP_RANK_COPIES))
                return SpectrumCount;

            return base.GetRankValue(rankId);
        }

        public override IEnumerable<KeyValuePair<PeptideRankId, string>> RankValues
        {
            get
            {
                yield return new KeyValuePair<PeptideRankId, string>(NistLibSpecBase.PEP_RANK_TFRATIO,
                    TFRatio.ToString(LocalizationHelper.CurrentCulture));
                yield return new KeyValuePair<PeptideRankId, string>(LibrarySpec.PEP_RANK_TOTAL_INTENSITY,
                    string.Format("{0:F0}", TotalIntensity)); // Not L10N
                yield return new KeyValuePair<PeptideRankId, string>(LibrarySpec.PEP_RANK_COPIES,
                    SpectrumCount.ToString(LocalizationHelper.CurrentCulture));
            }
        }

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For XML serialization
        /// </summary>
        protected NistSpectrumHeaderInfoBase()
        {
        }

        private enum ATTR
        {
            count_measured,
            total_intensity,
            tfratio
        }

        public override void ReadXml(XmlReader reader)
        {
            // Read tag attributes
            base.ReadXml(reader);
            SpectrumCount = reader.GetIntAttribute(ATTR.count_measured);
            TotalIntensity = reader.GetFloatAttribute(ATTR.total_intensity);
            TFRatio = reader.GetFloatAttribute(ATTR.tfratio);
            // Consume tag
            reader.Read();
        }

        public override void WriteXml(XmlWriter writer)
        {
            // Write tag attributes
            base.WriteXml(writer);
            writer.WriteAttribute(ATTR.count_measured, SpectrumCount);
            writer.WriteAttribute(ATTR.total_intensity, TotalIntensity);
            writer.WriteAttribute(ATTR.tfratio, TFRatio);
        }

        #endregion

        #region object overrides

        public bool Equals(NistSpectrumHeaderInfoBase obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return base.Equals(obj) && obj.SpectrumCount == SpectrumCount && obj.TotalIntensity == TotalIntensity &&
                   obj.TFRatio == TFRatio;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as NistSpectrumHeaderInfoBase);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = base.GetHashCode();
                result = (result*397) ^ SpectrumCount;
                result = (result*397) ^ TotalIntensity.GetHashCode();
                result = (result*397) ^ TFRatio.GetHashCode();
                return result;
            }
        }

        #endregion
    }

    [XmlRoot("nist_library")]
    public sealed class NistLibrary : NistLibraryBase
    {
        public const string DEFAULT_AUTHORITY = "nist.gov"; // Not L10N

        public const string EXT_CACHE = ".slc"; // Not L10N  

        public static NistLibrary Load(LibrarySpec spec, ILoadMonitor loader)
        {
            return (NistLibrary) Load(spec, new NistLibrary(spec), loader);            
        }

        /// <summary>
        /// Controlled access to this <see cref="Immutable"/> class, which should be
        /// created through <see cref="Load(LibrarySpec,ILoadMonitor)"/>.
        /// </summary>
        private NistLibrary(LibrarySpec spec)
            : base(spec, EXT_CACHE)
        {
        }

        public override LibraryDetails LibraryDetails
        {
            get
            {
                LibraryDetails details = new LibraryDetails { Format = "NIST", PeptideCount = SpectrumCount }; // Not L10N

                if (!string.IsNullOrEmpty(Id))
                {
                    details.Id = Id;
                }
                if (!string.IsNullOrEmpty(Revision))
                {
                    details.Revision = Revision;
                }

                details.AddLink(LibraryLink.NIST);
                return details;
            }
        }

        protected override SpectrumHeaderInfo CreateSpectrumHeaderInfo(NistSpectrumInfo info)
        {
            return new NistSpectrumHeaderInfo(Name, info.TFRatio, info.TotalIntensity, info.Copies);
        }

        public override LibrarySpec CreateSpec(string path)
        {
            return new NistLibSpec(Name, path);
        }

        public override string SpecFilter
        {
            get { return TextUtil.FileDialogFilterAll(Resources.NistLibrary_SpecFilter_NIST_Spectral_Library, NistLibSpec.EXT); }
        }

        #region Implementation of IXmlSerializable
        
        /// <summary>
        /// For serialization
        /// </summary>
        private NistLibrary()
        {
        }

        public static NistLibrary Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new NistLibrary());
        }

        #endregion

        #region object overrides

        public bool Equals(NistLibrary other)
        {
            return base.Equals(other);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as NistLibrary);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        #endregion
    }

    public abstract class NistLibraryBase : CachedLibrary<NistSpectrumInfo>
    {
        private const int FORMAT_VERSION_CACHE = 4;

        private static readonly Regex REGEX_BASENAME = new Regex(@"NIST_(.*)_v(\d+\.\d+)_(\d\d\d\d\-\d\d-\d\d)"); // Not L10N


        private static readonly Dictionary<string, string> MODIFICATION_MASSES = new Dictionary<string, string>
            {
                // Modification values taken from http://chemdata.nist.gov/mass-spc/ftp/mass-spc/PepLib.pdf
                {"Oxidation", SequenceMassCalc.GetModDiffDescription(15.994915)}, // Not L10N
                // Use SpectraST (taken from Unimod) values below
//                {"Carbamidomethyl", SequenceMassCalc.GetModDiffDescription(57.02146)},            
//                {"ICAT_light", SequenceMassCalc.GetModDiffDescription(227.12)},
//                NOTE: The value below differs significantly from the Unimod value
//                {"ICAT_heavy", SequenceMassCalc.GetModDiffDescription(236.12)},
//                {"AB_old_ICATd0", SequenceMassCalc.GetModDiffDescription(442.20)},
//                NOTE: The value below differs significantly from the Unimod value
//                {"AB_old_ICATd8", SequenceMassCalc.GetModDiffDescription(450.20)},
//                {"Acetyl", SequenceMassCalc.GetModDiffDescription(42.0106)},
//                {"Deamidation", SequenceMassCalc.GetModDiffDescription(0.9840)},
//                NOTE: The value was wrong in this version (39.994915 in Unimod)
//                {"Pyro-cmC", SequenceMassCalc.GetModDiffDescription(-17.026549)},
//                {"Pyro-glu", SequenceMassCalc.GetModDiffDescription(-17.026549)},
//                {"Pyro_glu", SequenceMassCalc.GetModDiffDescription(-18.010565)},
//                {"Amide", SequenceMassCalc.GetModDiffDescription(-0.984016)},
//                {"Phospho", SequenceMassCalc.GetModDiffDescription(79.9663)},
//                {"Methyl", SequenceMassCalc.GetModDiffDescription(14.0157)},
//                {"Carbamyl", SequenceMassCalc.GetModDiffDescription(43.00581)},

                // Modification values taken from SpectraST
                // Not L10N
                // http://sashimi.svn.sourceforge.net/viewvc/sashimi/trunk/trans_proteomic_pipeline/src/Search/SpectraST/Peptide.cpp?revision=5277&view=markup
                // line 1196
// ReSharper disable NonLocalizedString
                {"ICAT_light", SequenceMassCalc.GetModDiffDescription(227.126991)}, 
                {"ICAT-C", SequenceMassCalc.GetModDiffDescription(227.126991)}, // PSI new name
                {"ICAT_heavy", SequenceMassCalc.GetModDiffDescription(236.157185)},
                {"ICAT-C:13C(9)", SequenceMassCalc.GetModDiffDescription(236.157185)}, // PSI new name
                {"AB_old_ICATd0", SequenceMassCalc.GetModDiffDescription(442.224991)},
                {"ICAT-D", SequenceMassCalc.GetModDiffDescription(442.224991)}, // PSI new name
                {"AB_old_ICATd8", SequenceMassCalc.GetModDiffDescription(450.275205)},
                {"ICAT-D:2H(8)", SequenceMassCalc.GetModDiffDescription(450.275205)}, // PSI new name
                {"Carbamidomethyl", SequenceMassCalc.GetModDiffDescription(57.021464)},
                {"Carboxymethyl", SequenceMassCalc.GetModDiffDescription(58.005479)},
                {"Propionamide", SequenceMassCalc.GetModDiffDescription(71.037114)}, // alkylation of acrylamide to cysteines
                {"Propionamide:2H(3)", SequenceMassCalc.GetModDiffDescription(74.055944)}, // alkylation of heavy acrylamide to cysteines
                {"Propionamide:13C(3)", SequenceMassCalc.GetModDiffDescription(74.047178)}, // alkylation of heavy acrylamide to cysteines
                {"Acetyl", SequenceMassCalc.GetModDiffDescription(42.010565)}, // acetylation of N terminus
                {"Deamidation", SequenceMassCalc.GetModDiffDescription(0.984016)},
                {"Deamidated", SequenceMassCalc.GetModDiffDescription(0.984016)}, // PSI new name
                {"Pyro-cmC", SequenceMassCalc.GetModDiffDescription(39.994915)}, // cyclicization of N-terminal CAM-cysteine (FIXED value 01/27/07)
                {"Pyro-carbamidomethyl", SequenceMassCalc.GetModDiffDescription(39.994915)}, // PSI new name
                {"Pyro-glu", SequenceMassCalc.GetModDiffDescription(-17.026549)}, // loss of NH3 from glutamine
                {"Gln->pyro-Glu", SequenceMassCalc.GetModDiffDescription(-17.026549)}, // PSI new name
                {"Pyro_glu", SequenceMassCalc.GetModDiffDescription(-18.010565)}, // loss of H2O from glutamic acid
                {"Glu->pyro-Glu", SequenceMassCalc.GetModDiffDescription(-18.010565)}, // PSI new name
                {"Amide", SequenceMassCalc.GetModDiffDescription(-0.984016)}, // amidation of C terminus
                {"Amidated", SequenceMassCalc.GetModDiffDescription(-0.984016)}, // PSI new name
                {"Phospho", SequenceMassCalc.GetModDiffDescription(79.966331)}, // phosphorylation
                {"Methyl", SequenceMassCalc.GetModDiffDescription(14.015650)}, // methylation
                {"Carbamyl", SequenceMassCalc.GetModDiffDescription(43.005814)}, // carbamylation of N terminus or lysines
                {"iTRAQ4plex", SequenceMassCalc.GetModDiffDescription(144.102063)}, // iTRAQ 4-plex
                {"PEO-Iodoacetyl-LC-Biotin", SequenceMassCalc.GetModDiffDescription(414.52)}, // Hui Zhang's PEO alkylation agent on cysteines
                {"Label:13C(6)", SequenceMassCalc.GetModDiffDescription(6.020129)}, // SILAC heavy lysine and arginine (+6)
                {"Label:13C(6)15N(1)", SequenceMassCalc.GetModDiffDescription(7.017165)},
                {"Label:13C(6)15N(2)", SequenceMassCalc.GetModDiffDescription(8.014199)}, // SILAC heavy lysine (+8)
                {"Label:13C(6)15N(3)", SequenceMassCalc.GetModDiffDescription(9.011235)},  
                {"Label:13C(6)15N(4)", SequenceMassCalc.GetModDiffDescription(10.008269)}, // SILAC heavy arginine (+10)
                {"Methylthio", SequenceMassCalc.GetModDiffDescription(45.987721)}, // methylthiolated cysteine (cys blocking by MMTS)
                {"Leucyl", SequenceMassCalc.GetModDiffDescription(113.08406)}, // leucine added to N-term or K
                {"Leucyl:13C(6)15N(1)", SequenceMassCalc.GetModDiffDescription(120.101224)}, // heavy leucine added to N-term or K
                {"Nitro", SequenceMassCalc.GetModDiffDescription(44.985078)},
                {"Dimethyl", SequenceMassCalc.GetModDiffDescription(28.031300)},
                {"Trimethyl", SequenceMassCalc.GetModDiffDescription(42.046950)},  
                {"Bromo", SequenceMassCalc.GetModDiffDescription(77.910511)},
                // Ubl chains
                {"SUMO_1", SequenceMassCalc.GetModDiffDescription(2135.920495)}, // SUMO-1 Tryptic/LysC tail
                {"SUMO_2_3_Tryp", SequenceMassCalc.GetModDiffDescription(3549.536567)}, // SUMO-2/3 Tryptic tail
                {"Smt3_R93A_Tryp", SequenceMassCalc.GetModDiffDescription(3812.747563)}, // Smt3_R93A Tryptic tail
                {"Smt3_R93A_LysC", SequenceMassCalc.GetModDiffDescription(4544.074787)}, // Smt3_R93A LysC tail
                {"NEDD8_LysC", SequenceMassCalc.GetModDiffDescription(1555.956231)}, // NEDD8 LysC tail
                {"Rub1_LysC", SequenceMassCalc.GetModDiffDescription(2454.341699)}, // Rub1 LysC tail
                {"Ub_LysC", SequenceMassCalc.GetModDiffDescription(1431.831075)}, // Ubiquitin LysC tail
                {"GlyGly", SequenceMassCalc.GetModDiffDescription(114.042927)}, // Ubiquitin/NEDD8 Tryptic tail (2 glycines)
            };

#pragma warning disable 169
        private static readonly Dictionary<string, string> MODIFICATION_MASSES_AVG = new Dictionary<string, string>
#pragma warning restore 169
            {
                // Modification values taken from SpectraST
                // http://sashimi.svn.sourceforge.net/viewvc/sashimi/trunk/trans_proteomic_pipeline/src/Search/SpectraST/Peptide.cpp?revision=5277&view=markup
                // line 1078
                {"ICAT_light", SequenceMassCalc.GetModDiffDescription(227.2603)}, 
                {"ICAT-C", SequenceMassCalc.GetModDiffDescription(227.2603)}, // PSI new name
                {"ICAT_heavy", SequenceMassCalc.GetModDiffDescription(236.1942)},
                {"ICAT-C:13C(9)", SequenceMassCalc.GetModDiffDescription(236.1942)}, // PSI new name
                {"AB_old_ICATd0", SequenceMassCalc.GetModDiffDescription(442.5728)},
                {"ICAT-D", SequenceMassCalc.GetModDiffDescription(442.5728)}, // PSI new name
                {"AB_old_ICATd8", SequenceMassCalc.GetModDiffDescription(450.6221)},
                {"ICAT-D:2H(8)", SequenceMassCalc.GetModDiffDescription(450.6221)}, // PSI new name
                {"Carbamidomethyl", SequenceMassCalc.GetModDiffDescription(57.0513)},  
                {"Carboxymethyl", SequenceMassCalc.GetModDiffDescription(58.0361)},  
                {"Propionamide", SequenceMassCalc.GetModDiffDescription(71.0779)}, // alkylation of acrylamide to cysteines
                {"Propionamide:2H(3)", SequenceMassCalc.GetModDiffDescription(74.0964)}, // alkylation of heavy acrylamide to cysteines
                {"Propionamide:13C(3)", SequenceMassCalc.GetModDiffDescription(74.0558)},  
                {"Oxidation", SequenceMassCalc.GetModDiffDescription(15.9994)},  
                {"Acetyl", SequenceMassCalc.GetModDiffDescription(42.0106)}, // acetylation of N terminus  
                {"Deamidation", SequenceMassCalc.GetModDiffDescription(0.9848)},
                {"Deamidated", SequenceMassCalc.GetModDiffDescription(0.9848)}, // PSI new name
                {"Pyro-cmC", SequenceMassCalc.GetModDiffDescription(40.0208)}, // cyclicization of N-terminal CAM-cysteine (FIXED value 01/27/07)
                {"Pyro-carbamidomethyl", SequenceMassCalc.GetModDiffDescription(40.0208)}, // PSI new name
                {"Pyro-glu", SequenceMassCalc.GetModDiffDescription(-17.0305)}, // loss of NH3 from glutamine
                {"Gln->pyro-Glu", SequenceMassCalc.GetModDiffDescription(-17.0305)}, // PSI new name
                {"Pyro_glu", SequenceMassCalc.GetModDiffDescription(-18.01524)}, // loss of H2O from glutamic acid
                {"Glu->pyro-Glu", SequenceMassCalc.GetModDiffDescription(-18.01524)}, // PSI new name
                {"Amide", SequenceMassCalc.GetModDiffDescription(-0.9848)}, // amidation of C terminus
                {"Amidated", SequenceMassCalc.GetModDiffDescription(-0.9848)}, // PSI new name  
                {"Phospho", SequenceMassCalc.GetModDiffDescription(79.9799)}, // phosphorylation  
                {"Methyl", SequenceMassCalc.GetModDiffDescription(14.0266)}, // methylation  
                {"Carbamyl", SequenceMassCalc.GetModDiffDescription(43.0247)}, // carbamylation of N terminus or lysines
                {"iTRAQ4plex", SequenceMassCalc.GetModDiffDescription( 144.1544)}, // iTRAQ on N terminus or K
                {"PEO-Iodoacetyl-LC-Biotin", SequenceMassCalc.GetModDiffDescription( 414.52)}, // Hui Zhang's PEO alkylation agent on cysteines
                {"Label:13C(6)", SequenceMassCalc.GetModDiffDescription(5.9559)}, // SILAC heavy lysine or arginine (+6)
                {"Label:13C(6)15N(1)", SequenceMassCalc.GetModDiffDescription(6.9490)},
                {"Label:13C(6)15N(2)", SequenceMassCalc.GetModDiffDescription(7.9427)}, // SILAC heavy lysine (+8)
                {"Label:13C(6)15N(3)", SequenceMassCalc.GetModDiffDescription(8.9358)},  
                {"Label:13C(6)15N(4)", SequenceMassCalc.GetModDiffDescription(9.9296)}, // SILAC heavy arginine (+10)
                {"Methylthio", SequenceMassCalc.GetModDiffDescription(46.0916)}, // methylthiolated cysteine (cys blocking by MMTS)
                {"Leucyl", SequenceMassCalc.GetModDiffDescription(113.1594)}, // leucine added to N-term or K
                {"Leucyl:13C(6)15N(1)", SequenceMassCalc.GetModDiffDescription(120.1087)}, // heavy leucine added to N-term or K 
                {"Nitro", SequenceMassCalc.GetModDiffDescription(44.9976)},
                {"Dimethyl", SequenceMassCalc.GetModDiffDescription(28.0532)},
                {"Trimethyl", SequenceMassCalc.GetModDiffDescription(42.0797)},
                {"Bromo", SequenceMassCalc.GetModDiffDescription(78.8961)},                  
                // Ubl chains
                {"SUMO_1", SequenceMassCalc.GetModDiffDescription(2137.2567)}, // SUMO-1 Tryptic/LysC tail
                {"SUMO_2_3_Tryp", SequenceMassCalc.GetModDiffDescription(3551.7075)}, // SUMO-2/3 Tryptic tail
                {"Smt3_R93A_Tryp", SequenceMassCalc.GetModDiffDescription(3815.0935)}, // Smt3_R93A Tryptic tail
                {"Smt3_R93A_LysC", SequenceMassCalc.GetModDiffDescription(4546.9153)}, // Smt3_R93A LysC tail
                {"NEDD8_LysC", SequenceMassCalc.GetModDiffDescription(1556.9154)}, // NEDD8 LysC tail
                {"Rub1_LysC", SequenceMassCalc.GetModDiffDescription(2455.9015)}, // Rub1 LysC tail
                {"Ub_LysC", SequenceMassCalc.GetModDiffDescription(1432.6894)}, // Ubiquitin LysC tail
                {"GlyGly", SequenceMassCalc.GetModDiffDescription(114.1026)}, // Ubiquitin/NEDD8 Tryptic tail (2 glycines)
            };
        // ReSharper restore NonLocalizedString
        private IPooledStream _readStream;

        protected static Library Load(LibrarySpec spec, NistLibraryBase library, ILoadMonitor loader)
        {
            if (library.Load(loader))
                return library;
            return null;            
        }

        /// <summary>
        /// Controlled access to this <see cref="Immutable"/> class, which should be
        /// created through <see cref="Load(LibrarySpec,NistLibraryBase,ILoadMonitor)"/>.
        /// </summary>
        protected NistLibraryBase(LibrarySpec spec, string extCache)
            : base(spec)
        {
            FilePath = spec.FilePath;

            CachePath = Path.ChangeExtension(FilePath, extCache);

            string baseName = Path.GetFileNameWithoutExtension(FilePath) ?? string.Empty; // ReSharper
            Match match = REGEX_BASENAME.Match(baseName);
            if (match.Success)
            {
                Id = match.Groups[1].Value;
                Revision = match.Groups[3].Value;
            }
        }

        /// <summary>
        /// A date string (yyyy-mm-dd) associate with the library.
        /// </summary>
        public string Revision { get; private set; }

        /// <summary>
        /// The ID name assigned to this library by NIST.
        /// </summary>
        public string Id { get; private set; }

        /// <summary>
        /// Path to the file on disk from which this library was loaded.  This value
        /// may be null, if the library was deserialized from XML and has not yet
        /// been loaded.
        /// </summary>
        public string FilePath { get; private set; }

        public override IPooledStream ReadStream { get { return _readStream; } }

        private Stream CreateStream(ILoadMonitor loader)
        {
            if (_readStream == null)
                _readStream = loader.StreamManager.CreatePooledStream(CachePath, false);
            return ReadStream.Stream;
        }

        public override bool IsSameLibrary(Library library)
        {
            NistLibrary nlib = library as NistLibrary;
            if (nlib != null)
                return Equals(Id, nlib.Id);
            return false;
        }

        public override int CompareRevisions(Library library)
        {
            // Not a valid request, if the two libraries are not the same.
            Debug.Assert(IsSameLibrary(library));
            string libRevision = ((NistLibrary)library).Revision;
            if (Revision == null && libRevision == null)
                return 0;
            if (Revision == null)
                return -1;
            if (libRevision == null)
                return 1;
            return String.CompareOrdinal(Revision, libRevision);
        }

        // ReSharper disable UnusedMember.Local
        private enum LibHeaders
        {
            format_version,
            num_spectra,
            location_headers_lo,
            location_headers_hi,

            count
        }

        private enum SpectrumHeaders
        {
//            pre_mz,
            seq_key_hash,
            seq_key_length,
            charge,
            tf_ratio,
            total_intensity,
            copies,
            num_peaks,
            compressed_size,
            location_peaks_lo,
            location_peaks_hi,
            seq_len,

            count
        }
        // ReSharper restore UnusedMember.Local

        private bool Load(ILoadMonitor loader)
        {
            ProgressStatus status = new ProgressStatus(string.Empty);
            loader.UpdateProgress(status);

            bool cached = loader.StreamManager.IsCached(FilePath, CachePath);
            if (Load(loader, status, cached))
                return true;

            // If loading from the cache failed, rebuild it.
            if (cached)
            {
                // Not sure the cause, but found a case where LibraryPath was CachePath (.splc)
                if (Equals(FilePath, CachePath))
                    loader.UpdateProgress(status.ChangeErrorException(new IOException(string.Format(Resources.NistLibraryBase_Load_The_file___0___is_not_a_valid_library_, FilePath))));
                else
                {
                    // Reset readStream so we don't read corrupt file.
                    _readStream = null;
                    if (Load(loader, status, false))
                        return true;
                }
            }

            return false;
        }

        private bool Load(ILoadMonitor loader, ProgressStatus status, bool cached)
        {
            try
            {
                int loadPercent = 100;
                if (!cached)
                {
                    // Building the cache will take 95% of the load time.
                    loadPercent = 5;

                    status = status.ChangeMessage(string.Format(Resources.NistLibraryBase_Load_Building_binary_cache_for__0__library, Path.GetFileName(FilePath)));
                    status = status.ChangePercentComplete(0);

                    loader.UpdateProgress(status);

                    if (!CreateCache(loader, status, 100 - loadPercent))
                        return false;
                }

                status = status.ChangeMessage(string.Format(Resources.NistLibraryBase_Load_Loading__0__library, Path.GetFileName(FilePath)));
                loader.UpdateProgress(status);

                // Use a buffered stream for initial read
                BufferedStream stream = new BufferedStream(CreateStream(loader), 32 * 1024);

                // Read library header from the end of the cache
                int countHeader = (int) LibHeaders.count*4;
                stream.Seek(-countHeader, SeekOrigin.End);

                byte[] libHeader = new byte[countHeader];
                ReadComplete(stream, libHeader, countHeader);

                int formatVersion = GetInt32(libHeader, (int)LibHeaders.format_version);
                if (formatVersion != FORMAT_VERSION_CACHE)
                    return false;

                int numSpectra = GetInt32(libHeader, (int) LibHeaders.num_spectra);
                var libraryEntries = new NistSpectrumInfo[numSpectra];
                var setSequences = new Dictionary<LibSeqKey, bool>(numSpectra);
                
                // Seek to beginning of spectrum headers
                long locationHeaders = BitConverter.ToInt64(libHeader, ((int)LibHeaders.location_headers_lo)*4);
                stream.Seek(locationHeaders, SeekOrigin.Begin);

                countHeader = (int) SpectrumHeaders.count*4;
                byte[] specHeader = new byte[1024];
                byte[] specSequence = new byte[1024];
                for (int i = 0; i < numSpectra; i++)
                {
                    int percent = (100 - loadPercent) + (i * loadPercent / numSpectra);
                    if (status.PercentComplete != percent)
                    {
                        // Check for cancellation after each integer change in percent loaded.
                        if (loader.IsCanceled)
                        {
                            loader.UpdateProgress(status.Cancel());
                            return false;
                        }

                        // If not cancelled, update progress.
                        loader.UpdateProgress(status = status.ChangePercentComplete(percent));                        
                    }

                    // Read spectrum header
                    ReadComplete(stream, specHeader, countHeader);

                    int seqKeyHash = GetInt32(specHeader, (int) SpectrumHeaders.seq_key_hash);
                    int seqKeyLength = GetInt32(specHeader, (int) SpectrumHeaders.seq_key_length);
                    int charge = GetInt32(specHeader, (int)SpectrumHeaders.charge);
                    if (charge == 0 || charge > TransitionGroup.MAX_PRECURSOR_CHARGE)
                        throw new InvalidDataException(Resources.NistLibraryBase_Load_Invalid_precursor_charge_found_File_may_be_corrupted);

                    float tfRatio = BitConverter.ToSingle(specHeader, ((int) SpectrumHeaders.tf_ratio)*4);
                    float totalIntensity = BitConverter.ToSingle(specHeader, ((int)SpectrumHeaders.total_intensity) * 4);
                    int copies = GetInt32(specHeader, (int)SpectrumHeaders.copies);                    
                    int numPeaks = GetInt32(specHeader, (int)SpectrumHeaders.num_peaks);
                    int compressedSize = GetInt32(specHeader, (int)SpectrumHeaders.compressed_size);
                    long location = BitConverter.ToInt64(specHeader, ((int) SpectrumHeaders.location_peaks_lo)*4);
                    int seqLength = GetInt32(specHeader, (int)SpectrumHeaders.seq_len);

                    // Read sequence information
                    ReadComplete(stream, specSequence, seqLength);

                    // Add new entry
                    LibKey key = new LibKey(specSequence, 0, seqLength, charge);
                    
                    libraryEntries[i] = new NistSpectrumInfo(key, tfRatio, totalIntensity,
                                                              (ushort)copies, (ushort)numPeaks, compressedSize, location);
                    
                    if (seqKeyLength > 0)
                    {
                        LibSeqKey seqKey = new LibSeqKey(key, seqKeyHash, seqKeyLength);
                        setSequences.Add(seqKey, true);
                    }
                }
                // Checksum = checksum.ChecksumValue;
                _libraryEntries = libraryEntries;
                _setSequences = setSequences;

                loader.UpdateProgress(status.Complete());

                // Create the stream from which the spectra will be read
                CreateStream(loader);
                
                return true;
            }
            catch (InvalidDataException x)
            {
                if (!cached)
                    loader.UpdateProgress(status.ChangeErrorException(x));
                return false;
            }
            catch (IOException x)
            {
                if (!cached)
                    loader.UpdateProgress(status.ChangeErrorException(x));
                return false;
            }
            catch (Exception x)
            {
                if (!cached)
                {
                    x = new Exception(string.Format(Resources.NistLibraryBase_Load_Failed_loading_library__0__, FilePath), x);
                    loader.UpdateProgress(status.ChangeErrorException(x));
                }
                return false;
            }
            finally
            {
                if (ReadStream != null)
                {
                    // Close the read stream to ensure we never leak it.
                    // This only costs on extra open, the first time the
                    // active document tries to read.
                    try { ReadStream.CloseStream(); }
                    catch (IOException) { }
                }
            }
        }

        // Not L10N
// ReSharper disable NonLocalizedString
        private static readonly Regex REGEX_NAME = new Regex(@"^Name: ([A-Z()\[\]0-9]+)/(\d)"); // NIST libraries can contain M(O) and SpectraST M[16]
        private static readonly Regex REGEX_NUM_PEAKS = new Regex(@"^Num ?[pP]eaks: (\d+)");  // NIST uses "Num peaks" and SpectraST "NumPeaks"
        private static readonly Regex REGEX_COMMENT = new Regex(@"^Comment: ");
        private static readonly Regex REGEX_MODS = new Regex(@" Mods=([^ ]+) ");
        private static readonly Regex REGEX_TF_RATIO = new Regex(@" Tfratio=([^ ]+) ");
        private static readonly Regex REGEX_SAMPLE = new Regex(@" Nreps=\d+/(\d+)");  // Observer spectrum count
        private static readonly char[] MAJOR_SEP = {'/'};
        private static readonly char[] MINOR_SEP = {','};
// ReSharper restore NonLocalizedString
        private bool CreateCache(ILoadMonitor loader, ProgressStatus status, int percent)
        {
            var sm = loader.StreamManager;
            long size = sm.GetLength(FilePath);
            
            long readChars = 0;

            using (TextReader reader = sm.CreateReader(FilePath))
            using (FileSaver fs = new FileSaver(CachePath, sm))
            using (Stream outStream = sm.CreateStream(fs.SafeName, FileMode.Create, true))
            {
                var libraryEntries = new List<NistSpectrumInfo>(10000);
                var setSequences = new Dictionary<LibSeqKey, bool>(10000);

                long lineCount = 0;
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    lineCount++;

                    // Update status trying to approximate position in the file
                    readChars += line.Length;
                    int percentComplete = (int) (readChars * percent / size);
                    if (status.PercentComplete != percentComplete)
                    {
                        // Check for cancellation after each integer change in percent loaded.
                        if (loader.IsCanceled)
                        {
                            loader.UpdateProgress(status.Cancel());
                            return false;
                        }

                        // If not cancelled, update progress.
                        loader.UpdateProgress(status = status.ChangePercentComplete(percentComplete));
                    }


                    // Read until name line
                    Match match = REGEX_NAME.Match(line);
                    if (!match.Success)
                        continue;

                    string sequence = match.Groups[1].Value;
                    int charge = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);

                    int numPeaks = 0;
                    float tfRatio = 1000;
                    int copies = 1;

                    // Process until the start of the peaks
                    while ((line = reader.ReadLine()) != null)
                    {
                        lineCount++;

                        readChars += line.Length;

                        match = REGEX_NUM_PEAKS.Match(line);
                        if (match.Success)
                        {
                            numPeaks = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                            break;
                        }

                        match = REGEX_COMMENT.Match(line);
                        if (match.Success)
                        {
                            match = REGEX_MODS.Match(line);
                            if (match.Success)
                                sequence = Modify(sequence, match.Groups[1].Value);

                            match = REGEX_SAMPLE.Match(line);
                            if (match.Success)
                                copies = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);

                            match = REGEX_TF_RATIO.Match(line);
                            if (match.Success)
                                tfRatio = float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                        }

                        if (line.StartsWith("_EOF_")) // Not L10N
                            ThrowIOException(lineCount, Resources.NistLibraryBase_CreateCache_Unexpected_end_of_file);
                        else if (line.StartsWith("Name:")) // Not L10N
                            break;
                    }

                    if (numPeaks == 0)
                        ThrowIOException(lineCount, string.Format(Resources.NistLibraryBase_CreateCache_No_peaks_found_for_peptide__0__, sequence));
                    if (numPeaks > ushort.MaxValue)
                        ThrowIOException(lineCount, string.Format(Resources.NistLibraryBase_CreateCache_Peak_count_for_MS_MS_spectrum_excedes_maximum__0__, ushort.MaxValue));

                    double totalIntensity = 0;

                    int mzBytes = sizeof(float)*numPeaks;
                    byte[] peaks = new byte[mzBytes*2];
                    for (int i = 0; i < numPeaks; i++)
                    {
                        line = reader.ReadLine();
                        if (line == null)
                        {
                            ThrowIOException(lineCount, string.Format(Resources.NistLibraryBase_CreateCache_Unexpected_end_of_file_in_peaks_for__0__, sequence));
                            break;  // ReSharper
                        }
                        lineCount++;
                        readChars += line.Length;

                        // Parse out mass and intensity as quickly as possible, since
                        // this will be the most repeated parsing code.
                        int iTab1 = line.IndexOf('\t'); // Not L10N
                        int iTab2 = (iTab1 == -1 ? -1 : line.IndexOf('\t', iTab1 + 1)); // Not L10N
                        if (iTab1 == -1 || iTab2 == -1)
                            ThrowIOException(lineCount, string.Format(Resources.NistLibraryBase_CreateCache_Invalid_format_at_peak__0__for__1__, i + 1, sequence));

                        string mzField = line.Substring(0, iTab1++);
                        string intensityField = line.Substring(iTab1, iTab2 - iTab1);

                        int offset = i*4;
                        Array.Copy(BitConverter.GetBytes(float.Parse(mzField, CultureInfo.InvariantCulture)), 0, peaks, offset, 4);
                        float intensity = float.Parse(intensityField, CultureInfo.InvariantCulture);
                        Array.Copy(BitConverter.GetBytes(intensity), 0, peaks, mzBytes + offset, 4);
                        totalIntensity += intensity;
                    }
                    // Peak list compression turns out to have a 4x impact on time to
                    // create the cache.  Using zero below turns it off, or 1 to turn
                    // it on, and take the performance hit for 40% less disk use, though
                    // because the text library files are so large, this difference
                    // represents only 5% of the cost of having the library on disk.
                    byte[] peaksCompressed = peaks.Compress(0);
                    int lenCompressed = peaksCompressed.Length;
                    long location = outStream.Position;
                    outStream.Write(peaksCompressed, 0, lenCompressed);
                    
                    var key = new LibKey(sequence, charge);
                    var info = new NistSpectrumInfo(key, tfRatio, Convert.ToSingle(totalIntensity),
                                                    (ushort) copies, (ushort) numPeaks, lenCompressed, location);
                    
                    libraryEntries.Add(info);
                }

                var libraryEntriesArray = libraryEntries.ToArray();

                Array.Sort(libraryEntriesArray, CompareSpectrumInfo);

                long locationHeaders = outStream.Position;
                foreach (var info in libraryEntriesArray)
                {
                    LibSeqKey seqKey = new LibSeqKey(info.Key);
                    if (setSequences.ContainsKey(seqKey))
                    {
                        outStream.Write(BitConverter.GetBytes(0), 0, sizeof(int));
                        outStream.Write(BitConverter.GetBytes(-1), 0, sizeof(int));
                    }
                    else
                    {
                        // If it is unique, it will need to be added at cache load time.
                        outStream.Write(BitConverter.GetBytes(seqKey.GetHashCode()), 0, sizeof(int));
                        outStream.Write(BitConverter.GetBytes(seqKey.Length), 0, sizeof(int));
                        setSequences.Add(seqKey, true);
                    }
                    outStream.Write(BitConverter.GetBytes(info.Key.Charge), 0, sizeof (int));
                    outStream.Write(BitConverter.GetBytes(info.TFRatio), 0, sizeof (float));
                    outStream.Write(BitConverter.GetBytes(info.TotalIntensity), 0, sizeof (float));
                    outStream.Write(BitConverter.GetBytes(info.Copies), 0, sizeof (int));
                    outStream.Write(BitConverter.GetBytes(info.NumPeaks), 0, sizeof (int));
                    outStream.Write(BitConverter.GetBytes(info.CompressedSize), 0, sizeof (int));
                    outStream.Write(BitConverter.GetBytes(info.Location), 0, sizeof (long));
                    info.Key.WriteSequence(outStream);
                }

                outStream.Write(BitConverter.GetBytes(FORMAT_VERSION_CACHE), 0, sizeof(int));
                outStream.Write(BitConverter.GetBytes(libraryEntriesArray.Length), 0, sizeof(int));
                outStream.Write(BitConverter.GetBytes(locationHeaders), 0, sizeof(long));

                
                sm.Finish(outStream);
                fs.Commit();
                sm.SetCache(FilePath, CachePath);
            }

            loader.UpdateProgress(status.Complete());

            return true;
        }
    
        private void ThrowIOException(long lineNum, string message)
        {
            throw new IOException(string.Format(Resources.NistLibraryBase_ThrowIOException__0__line__1__2__, FilePath,
                                                lineNum, message));
        }

        private static string Modify(string sequence, string mod)
        {
            // If no modifications, just return the input sequence
            bool clean = (sequence.IndexOfAny(new[] { '(', '[' }) == -1); // Not L10N
            if (clean && Equals(mod, "0")) // Not L10N
                return sequence;

            // Parse the modification spec, and insert [+/-00.0] modifiers
            string[] mods = mod.Split(MAJOR_SEP);

            StringBuilder sb = new StringBuilder(sequence.Length);
            bool inMod = false;
            int i = 0, iMod = 1, iNextMod = -1;
            string massDiffDesc = null;
            foreach (char c in sequence)
            {
                while (iNextMod < i && iMod < mods.Length)
                    iNextMod = GetMod(mods[iMod++], out massDiffDesc);

                // At least for Oxidation the sequence already contains
                // inserted identifiers that look like M(O) for Methyonine
                // with oxidation.  So, these are removed.
                if (c == '(' || c == '[') // Not L10N
                    inMod = true;
                else if (inMod)
                {
                    if (c == ')' || c == ']') // Not L10N
                        inMod = false;
                }
                else
                {
                    sb.Append(c);
                    if (iNextMod == i)
                        sb.Append(massDiffDesc);
                    i++;
                }
            }
            return sb.ToString();
        }

        private static int GetMod(string mod, out string massDiff)
        {
            string[] parts = mod.Split(MINOR_SEP);
            if (parts.Length < 3)
            {
                massDiff = string.Empty;
                return -1;
            }
            int index = int.Parse(parts[0], CultureInfo.InvariantCulture);
            // If it is an unknown modification, insert a sequence modifier
            // that will cause this sequence never to match anything.  These
            // are rare, and can be viewed by placing a breakpoint on the
            // line where if is true.
            if (!MODIFICATION_MASSES.TryGetValue(parts[2], out massDiff))
                massDiff = "[?]"; // Not L10N
            return index;
        }

        protected override SpectrumPeaksInfo.MI[] ReadSpectrum(NistSpectrumInfo info)
        {
            byte[] peaksCompressed = new byte[info.CompressedSize];
            lock (ReadStream)
            {
                Stream fs = ReadStream.Stream;

                // Seek to stored location
                fs.Seek(info.Location, SeekOrigin.Begin);

                // Single read to get all the peaks
                if (fs.Read(peaksCompressed, 0, peaksCompressed.Length) < peaksCompressed.Length)
                    throw new IOException(Resources.NistLibraryBase_ReadSpectrum_Failure_trying_to_read_peaks);
            }

            int mzBytes = sizeof(float)*info.NumPeaks;
            byte[] peaks = peaksCompressed.Uncompress(mzBytes*2);

            // Build the list
            var arrayMI = new SpectrumPeaksInfo.MI[info.NumPeaks];

            for (int i = 0; i < info.NumPeaks; i++)
            {
                int offset = i*4;
                arrayMI[i].Mz = BitConverter.ToSingle(peaks, offset);
                arrayMI[i].Intensity = BitConverter.ToSingle(peaks, mzBytes + offset);
            }

            return arrayMI;
        }

        #region Implementation of IXmlSerializable
        
        /// <summary>
        /// For serialization
        /// </summary>
        protected NistLibraryBase()
        {
        }

        private enum ATTR
        {
            id,
            revision
        }

        public override void ReadXml(XmlReader reader)
        {
            // Read tag attributes
            base.ReadXml(reader);
            Id = reader.GetAttribute(ATTR.id);
            Revision = reader.GetAttribute(ATTR.revision);
            // Consume tag
            reader.Read();
        }

        public override void WriteXml(XmlWriter writer)
        {
            // Write tag attributes
            base.WriteXml(writer);
            writer.WriteAttributeIfString(ATTR.id, Id);
            writer.WriteAttributeIfString(ATTR.revision, Revision);
        }

        #endregion

        #region object overrides

        public bool Equals(NistLibraryBase obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return base.Equals(obj) &&
                Equals(obj.Id, Id) &&
                Equals(obj.Revision, Revision) &&
                Equals(obj.FilePath, FilePath);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as NistLibraryBase);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = base.GetHashCode();
                result = (result * 397) ^ Id.GetHashCode();
                result = (result * 397) ^ Revision.GetHashCode();
                result = (result * 397) ^ (FilePath != null ? FilePath.GetHashCode() : 0);
                return result;
            }
        }

        #endregion
    }

    public struct NistSpectrumInfo : ICachedSpectrumInfo
    {
        private readonly LibKey _key;
        private readonly float _tfRatio;
        private readonly float _totalIntensity;
        private readonly ushort _copies;
        private readonly ushort _numPeaks;
        private readonly int _compressedSize;
        private readonly long _location;

        public NistSpectrumInfo(LibKey key, float tfRatio, float totalIntensity,
            ushort copies, ushort numPeaks, int compressedSize, long location)
        {
            _key = key;
            _totalIntensity = totalIntensity;
            _tfRatio = tfRatio;
            _copies = copies;
            _numPeaks = numPeaks;
            _compressedSize = compressedSize;
            _location = location;
        }

        public LibKey Key { get { return _key; } }
// ReSharper disable InconsistentNaming
        public float TFRatio { get { return _tfRatio; } }
// ReSharper restore InconsistentNaming
        public float TotalIntensity { get { return _totalIntensity; } }
        public int Copies { get { return _copies; } }
        public int NumPeaks { get { return _numPeaks; } }
        public int CompressedSize { get { return _compressedSize; }}
        public long Location { get { return _location; } }
    }
}
