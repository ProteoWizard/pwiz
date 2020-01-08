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
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Lib
{
    [XmlRoot("nist_lib_spec")]
    public sealed class NistLibSpec : NistLibSpecBase
    {
        public const string EXT = ".msp";

        public static string FILTER_MSP
        {
            get { return TextUtil.FileDialogFilterAll(Resources.NistLibrary_SpecFilter_NIST_Spectral_Library, EXT); }            
        }

        public NistLibSpec(string name, string path)
            : base(name, path)
        {
        }

        public override string Filter
        {
            get { return FILTER_MSP; }
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
            new PeptideRankId(@"TFRatio", () => Resources.NistLibSpecBase_PEP_RANK_TFRATIO_TFRatio);

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
        public NistSpectrumHeaderInfo(string libraryName, float tfRatio, double? rt, double? irt, float totalIntensity, int spectrumCount)
            : base(libraryName, tfRatio, rt, irt, totalIntensity, spectrumCount)
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
        public NistSpectrumHeaderInfoBase(string libraryName, float tfRatio, double? rt, double? irt, float totalIntensity, int spectrumCount)
            : base(libraryName)
        {
            TFRatio = tfRatio;
            RT = rt;
            iRT = irt;
            TotalIntensity = totalIntensity;
            SpectrumCount = spectrumCount;
        }

        public int SpectrumCount { get; private set; }
        public float TotalIntensity { get; private set; }
// ReSharper disable InconsistentNaming
        public float TFRatio { get; private set; }
// ReSharper restore InconsistentNaming
        public double? RT { get; private set; }
        public double? iRT { get; private set; }

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
                    string.Format(@"{0:F0}", TotalIntensity));
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
        public const string DEFAULT_AUTHORITY = "nist.gov";

        public const string EXT_CACHE = ".slc";

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
                LibraryDetails details = new LibraryDetails { Format = @"NIST", SpectrumCount = SpectrumCount };

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

        public override LibraryFiles LibraryFiles
        {
            // NIST libraries don't have source file information
            get { return new LibraryFiles(); }
        }

        protected override SpectrumHeaderInfo CreateSpectrumHeaderInfo(NistSpectrumInfo info)
        {
            return new NistSpectrumHeaderInfo(Name, info.TFRatio, info.RT, info.iRT, info.TotalIntensity, info.Copies);
        }

        protected override LibrarySpec CreateSpec()
        {
            return new NistLibSpec(Name, FilePath);
        }

        public override string SpecFilter
        {
            get { return NistLibSpec.FILTER_MSP; }
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
        // Version 6 adds peak annotations
        private const int FORMAT_VERSION_CACHE = 6; 

        private static readonly Regex REGEX_BASENAME = new Regex(@"NIST_(.*)_v(\d+\.\d+)_(\d\d\d\d\-\d\d-\d\d)");


        // ReSharper disable LocalizableElement
        private static readonly Dictionary<string, string> MODIFICATION_MASSES = new Dictionary<string, string>
            {
                // Modification values taken from http://chemdata.nist.gov/mass-spc/ftp/mass-spc/PepLib.pdf
                {"Oxidation", SequenceMassCalc.GetModDiffDescription(15.994915)},
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

                // http://sashimi.svn.sourceforge.net/viewvc/sashimi/trunk/trans_proteomic_pipeline/src/Search/SpectraST/Peptide.cpp?revision=5277&view=markup
                // line 1196
// ReSharper disable LocalizableElement
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
        // ReSharper restore LocalizableElement

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
        // ReSharper restore LocalizableElement
        private IPooledStream _readStream;
        private bool _anyRetentionTimes;

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
            NistLibraryBase nlib = library as NistLibraryBase;
            if (nlib != null)
                return Equals(Id, nlib.Id);
            return false;
        }

        public override int CompareRevisions(Library library)
        {
            // Not a valid request, if the two libraries are not the same.
            Debug.Assert(IsSameLibrary(library));
            string libRevision = ((NistLibraryBase)library).Revision;
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

        private bool Load(ILoadMonitor loader, IProgressStatus status, bool cached)
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

                var valueCache = new ValueCache();
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
                
                // Seek to beginning of spectrum headers
                long locationHeaders = BitConverter.ToInt64(libHeader, ((int)LibHeaders.location_headers_lo)*4);
                stream.Seek(locationHeaders, SeekOrigin.Begin);
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
                    int charge = ReadSize(stream);
                    if (charge == 0 || charge > TransitionGroup.MAX_PRECURSOR_CHARGE)
                        throw new InvalidDataException(Resources.NistLibraryBase_Load_Invalid_precursor_charge_found_File_may_be_corrupted);

                    float tfRatio = PrimitiveArrays.ReadOneValue<float>(stream);
                    bool hasRt = PrimitiveArrays.ReadOneValue<bool>(stream);
                    double? rt = hasRt ? PrimitiveArrays.ReadOneValue<double>(stream) : (double?)null;
                    bool hasIrt = PrimitiveArrays.ReadOneValue<bool>(stream);
                    double? irt = hasIrt ? PrimitiveArrays.ReadOneValue<double>(stream) : (double?)null;
                    float totalIntensity = PrimitiveArrays.ReadOneValue<float>(stream);
                    int copies = ReadSize(stream);
                    int numPeaks = ReadSize(stream);
                    int compressedSize = ReadSize(stream);
                    int annotationsSize = ReadSize(stream);
                    long location = PrimitiveArrays.ReadOneValue<long>(stream);
                    LibKey key = LibKey.Read(valueCache, stream);
                    libraryEntries[i] = new NistSpectrumInfo(key, tfRatio, rt, irt, totalIntensity,
                                                              (ushort)copies, (ushort)numPeaks, compressedSize, annotationsSize, location);
                }
                // Checksum = checksum.ChecksumValue;
                SetLibraryEntries(libraryEntries);

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

        
// ReSharper disable LocalizableElement
        private static readonly string NAME = "Name:";
        private static readonly Regex REGEX_NAME = new Regex(@"^Name: ([A-Z()\[\]0-9]+)/(\d)"); // NIST libraries can contain M(O) and SpectraST M[16] TODO: Spectrast also has c- and n-term mods but we reject such entries for now - see example in TestLibraryExplorer
        private static readonly Regex REGEX_NUM_PEAKS = new Regex(@"^Num ?[pP]eaks: (\d+)");  // NIST uses "Num peaks" and SpectraST "NumPeaks"
        private static readonly string COMMENT = "Comment: ";
        private static readonly Regex REGEX_MODS = new Regex(@" Mods=([^ ]+) ");
        private static readonly Regex REGEX_TF_RATIO = new Regex(@" Tfratio=([^ ]+) ");
        private static readonly Regex REGEX_RT = new Regex(@" RetentionTime=([^ ,]+)"); // In a comment
        private static readonly Regex REGEX_RT_LINE = new Regex(@"^RetentionTime(Mins)*: ([^ ]+)"); // On its own line
        private static readonly Regex REGEX_IRT = new Regex(@" iRT=([^ ,]+)");
        private static readonly Regex REGEX_RI_LINE = new Regex(@"^(?:Synon:.* )?RI: ([^ ]+)"); // Retention Index for GC
        private static readonly Regex REGEX_SAMPLE = new Regex(@" Nreps=\d+/(\d+)");  // Observer spectrum count
        private static readonly char[] MAJOR_SEP = {'/'};
        private static readonly char[] MINOR_SEP = {','};
        // Small molecule items
        private static readonly Regex REGEX_NAME_SMALLMOL = new Regex(@"^Name: (.*)"); // small molecule names can be anything
        private static readonly string SYNON = "Synon: ";
        private static readonly Regex REGEX_INCHIKEY = new Regex(@"^(?:Synon:.* )?InChIKey: (.*)");
        private static readonly Regex REGEX_INCHI = new Regex(@"^(?:Synon:.* )?InChI: (?:InChI\=)?(.*)");
        private static readonly Regex REGEX_FORMULA = new Regex(@"^Formula: (.*)");
        private static readonly Regex REGEX_CAS = new Regex(@"^(?:Synon:.* )?CAS#?: (\d+-\d+-\d)"); // CONSIDER(bspratt): capture NIST# as well?
        private static readonly Regex REGEX_KEGG = new Regex(@"^(?:Synon:.* )?KEGG: (.*)");
        private static readonly Regex REGEX_ADDUCT = new Regex(@"^Precursor_type: (.*)");

// ReSharper restore LocalizableElement
        private bool CreateCache(ILoadMonitor loader, IProgressStatus status, int percent)
        {
            var sm = loader.StreamManager;
            long size = sm.GetLength(FilePath);
            
            long readChars = 0;
            var knownKeys = new HashSet<LibKey>();
            var ambiguousKeys = new HashSet<LibKey>();

            using (TextReader reader = sm.CreateReader(FilePath))
            using (FileSaver fs = new FileSaver(CachePath, sm))
            using (Stream outStream = sm.CreateStream(fs.SafeName, FileMode.Create, true))
            {
                var libraryEntries = new List<NistSpectrumInfo>(10000);

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
                    if (!line.StartsWith(NAME))
                        continue;
                    Match match = REGEX_NAME.Match(line);
                    var isPeptide = true;
                    var isGC = false;
                    if (!match.Success)
                    {
                        isPeptide = false;
                        match = REGEX_NAME_SMALLMOL.Match(line);
                    }
                    if (!match.Success)
                        continue;

                    string sequence = match.Groups[1].Value; // Peptide sequence or molecule name
                    var charge = isPeptide ? int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) : 0;
                    var adduct = Adduct.EMPTY;
                    string formula = null;
                    var otherKeys = new Dictionary<string, string>();
                    string inChiKey = null;

                    int numPeaks = 0;
                    float tfRatio = 1000;
                    double? rt = null, irt = null;
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

                        if (line.StartsWith(SYNON))
                        {
                            isPeptide = false;
                        }

                        match = REGEX_RT_LINE.Match(line); // RT may also be found in comments (originally only in comments)
                        if (match.Success)
                        {
                            rt = GetRetentionTime(match.Groups[2].Value, !string.IsNullOrEmpty(match.Groups[1].Value)); // RetentionTime: vs RetentionTimeMins:
                            continue;
                        }

                        match = REGEX_RI_LINE.Match(line);
                        if (match.Success)
                        {
                            // Note using RT as stand-in for RI (retention Time vs retention Index, LC vs GC)
                            // CONSIDER: track RT and RI simultaneously so lib is useful for GC and LC?
                            rt = GetRetentionTime(match.Groups[1].Value, true); 
                            isGC = true;
                            continue;
                        }

                        if (!isPeptide)
                        {
                            match = REGEX_FORMULA.Match(line);
                            if (match.Success)
                            {
                                formula = match.Groups[1].Value;
                                continue;
                            }
                            match = REGEX_INCHIKEY.Match(line);
                            if (match.Success)
                            {
                                inChiKey = match.Groups[1].Value;
                                continue;
                            }
                            match = REGEX_INCHI.Match(line);
                            if (match.Success)
                            {
                                otherKeys.Add(MoleculeAccessionNumbers.TagInChI, match.Groups[1].Value);
                                continue;
                            }
                            match = REGEX_CAS.Match(line);
                            if (match.Success)
                            {
                                otherKeys.Add(MoleculeAccessionNumbers.TagCAS, match.Groups[1].Value);
                                continue;
                            }
                            match = REGEX_KEGG.Match(line);
                            if (match.Success)
                            {
                                otherKeys.Add(MoleculeAccessionNumbers.TagKEGG, match.Groups[1].Value);
                                continue;
                            }
                            match = REGEX_ADDUCT.Match(line);
                            if (match.Success)
                            {
                                // We've seen strange adducts for MS3 data like "[109.1]+" - just pass over these
                                if (!Adduct.TryParse(match.Groups[1].Value, out adduct))
                                    adduct = Adduct.EMPTY;
                                continue;
                            }
                        }

                        // For peptides, a lot of useful info is jammed into the COMMENT line and must be further picked apart
                        if (line.StartsWith(COMMENT))
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

                            match = REGEX_RT.Match(line);
                            if (match.Success)
                                rt = GetRetentionTime(match.Groups[1].Value, false);

                            match = REGEX_IRT.Match(line);
                            if (match.Success)
                                irt = GetRetentionTime(match.Groups[1].Value, false);
                        }

                        if (line.StartsWith(@"_EOF_"))
                            ThrowIOException(lineCount, Resources.NistLibraryBase_CreateCache_Unexpected_end_of_file);
                        else if (line.StartsWith(@"Name:"))
                            break;
                    }

                    if (isGC && adduct.IsEmpty)
                    {
                        // GCMS is generally EI 
                        adduct = Adduct.M_PLUS;
                    }

                    if (charge == 0 && adduct.IsEmpty)
                        continue; // In the end, couldn't understand this as a peptide nor as a small molecule - ignore. CONSIDER(bspratt): throw an error? Historical behavior is to be silent.

                    if (numPeaks == 0)
                        ThrowIOException(lineCount, string.Format(Resources.NistLibraryBase_CreateCache_No_peaks_found_for_peptide__0__, sequence));
                    if (numPeaks > ushort.MaxValue)
                        ThrowIOException(lineCount, string.Format(Resources.NistLibraryBase_CreateCache_Peak_count_for_MS_MS_spectrum_excedes_maximum__0__, ushort.MaxValue));

                    double totalIntensity = 0;
                    var annotations = isPeptide ? null : new List<List<SpectrumPeakAnnotation>>(); // List of lists, as each peak may have multiple annotations
                    int mzBytes = sizeof(float)*numPeaks;
                    byte[] peaks = new byte[mzBytes*2];
                    for (int i = 0; i < numPeaks; i++)
                    {
                        var linePeaks = reader.ReadLine();
                        if (linePeaks == null)
                        {
                            ThrowIOException(lineCount, string.Format(Resources.NistLibraryBase_CreateCache_Unexpected_end_of_file_in_peaks_for__0__, sequence));
                            break;  // ReSharper
                        }
                        lineCount++;
                        readChars += linePeaks.Length;
                        IEnumerable<string> lines = null;

                        // Parse out mass and intensity as quickly as possible, since
                        // this will be the most repeated parsing code.
                        var sep = TextUtil.SEPARATOR_TSV; 
                        int iSeperator1 = linePeaks.IndexOf(sep); 
                        if (iSeperator1 == -1) // Using space instead of tab, maybe?
                        {
                            sep = ' '; 
                            iSeperator1 = linePeaks.IndexOf(sep); 
                            var iColon = linePeaks.IndexOf(':');
                            if (iColon > -1 && iColon < iSeperator1)
                            {
                                // Looks like a Golm GMD file e.g. "70:10 76:35 77:1000 78:110 79:42 \n80:4 81:7 86:6 87:5 88:21 " etc
                                sep = ':';
                                lines = linePeaks.Split(' ');
                                iSeperator1 = linePeaks.IndexOf(sep);
                            }
                        }

                        if (lines == null)
                            lines = new[] {linePeaks};

                        foreach (var linePeak in lines)
                        {
                            if (string.IsNullOrEmpty(linePeak))
                            {
                                continue;
                            }
                            if (iSeperator1 < 0)
                            {
                                iSeperator1 = linePeak.IndexOf(sep);
                                i++;
                            }

                            int iSeperator2 = (iSeperator1 == -1 ? -1 : linePeak.IndexOf(sep, iSeperator1 + 1)); 
                            if (iSeperator2 == -1)
                            {
                                if (iSeperator1 != -1)
                                {
                                    iSeperator2 = linePeak.Length; // Some entries don't have annotation columns
                                }
                                else
                                {
                                    ThrowIoExceptionInvalidPeakFormat(lineCount, i, sequence);
                                }
                            }
                            string mzField = linePeak.Substring(0, iSeperator1++);
                            string intensityField = linePeak.Substring(iSeperator1, iSeperator2 - iSeperator1);

                            int offset = i*4;
                            if (!TryParseFloatUncertainCulture(mzField, out var mz))
                            {
                                ThrowIoExceptionInvalidPeakFormat(lineCount, i, sequence);
                            }
                            Array.Copy(BitConverter.GetBytes(mz), 0, peaks, offset, 4);
                            if (!TryParseFloatUncertainCulture(intensityField, out var intensity))
                            {
                                ThrowIoExceptionInvalidPeakFormat(lineCount, i, sequence);
                            }
                            Array.Copy(BitConverter.GetBytes(intensity), 0, peaks, mzBytes + offset, 4);
                            totalIntensity += intensity;
                            if (!isPeptide)
                            {
                                ParseFragmentAnnotation(iSeperator2, annotations, i, linePeak, adduct, charge, mz);
                            }

                            iSeperator1 = -1; // Next line, if any
                        }
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
                    int lenAnnotations = 0;
                    if (annotations != null && annotations.Any(a => a != null && !a.All(SpectrumPeakAnnotation.IsNullOrEmpty)))
                    {
                        var annotationsTSV = Encoding.UTF8.GetBytes(SpectrumPeakAnnotation.ToCacheFormat(annotations)); 
                        lenAnnotations = annotationsTSV.Length;
                        outStream.Write(annotationsTSV, 0, lenAnnotations);
                    }
                    var key = isPeptide ? new LibKey(sequence, charge) : new LibKey(SmallMoleculeLibraryAttributes.Create(sequence, formula, inChiKey, otherKeys), adduct);
                    var info = new NistSpectrumInfo(key, tfRatio, rt, irt, Convert.ToSingle(totalIntensity),
                                                    (ushort) copies, (ushort) numPeaks, lenCompressed, lenAnnotations, location);
                    if (!isPeptide)
                    {
                        // Keep an eye out for ambiguous keys, probably due to library containing multiple machine types etc
                        if (!knownKeys.Add(key))
                        {
                            ambiguousKeys.Add(key); // Already in knownKeys, note ambiguity
                        }
                    }                    
                    libraryEntries.Add(info);
                }

                var libraryEntriesArray = libraryEntries.ToArray();

                long locationHeaders = outStream.Position;
                foreach (var info in libraryEntriesArray)
                {
                    outStream.Write(BitConverter.GetBytes(info.Key.Charge), 0, sizeof (int));
                    outStream.Write(BitConverter.GetBytes(info.TFRatio), 0, sizeof (float));
                    if (info.RT.HasValue)
                    {
                        outStream.Write(BitConverter.GetBytes(true), 0, sizeof(bool));
                        outStream.Write(BitConverter.GetBytes(info.RT.Value), 0, sizeof(double));
                    }
                    else
                    {
                        outStream.Write(BitConverter.GetBytes(false), 0, sizeof(bool));
                    }
                    if (info.iRT.HasValue)
                    {
                        outStream.Write(BitConverter.GetBytes(true), 0, sizeof(bool));
                        outStream.Write(BitConverter.GetBytes(info.iRT.Value), 0, sizeof(double));
                    }
                    else
                    {
                        outStream.Write(BitConverter.GetBytes(false), 0, sizeof(bool));
                    }
                    outStream.Write(BitConverter.GetBytes(info.TotalIntensity), 0, sizeof (float));
                    outStream.Write(BitConverter.GetBytes(info.Copies), 0, sizeof (int));
                    outStream.Write(BitConverter.GetBytes(info.NumPeaks), 0, sizeof (int));
                    outStream.Write(BitConverter.GetBytes(info.CompressedSize), 0, sizeof(int));
                    outStream.Write(BitConverter.GetBytes(info.AnnotationsSize), 0, sizeof(int));
                    outStream.Write(BitConverter.GetBytes(info.Location), 0, sizeof(long));
                    info.Key.Write(outStream);
                }

                outStream.Write(BitConverter.GetBytes(FORMAT_VERSION_CACHE), 0, sizeof(int));
                outStream.Write(BitConverter.GetBytes(libraryEntriesArray.Length), 0, sizeof(int));
                outStream.Write(BitConverter.GetBytes(locationHeaders), 0, sizeof(long));

                
                sm.Finish(outStream);
                fs.Commit();
                sm.SetCache(FilePath, CachePath);
            }

            loader.UpdateProgress(status.Complete());

            //
            // Especially for small molecules, make user aware if imported library contains multiple entries for the same key.
            // This is probably because the library contains entries for different instruments, CE values etc but we don't track that.
            // N.B. we've tolerated this ambiguity for peptides since the beginning, don't change that behavior now
            //
            if (ambiguousKeys.Any() && ambiguousKeys.All(k => k.IsSmallMoleculeKey))
            {
                var pairType = ambiguousKeys.Any(k => k.IsSmallMoleculeKey)
                    ? Resources.NistLibraryBase_CreateCache_molecule_adduct
                    : Resources.NistLibraryBase_CreateCache_peptide_charge;
                var warning = string.Format(Resources.NistLibraryBase_CreateCache_,
                    pairType, TextUtil.LineSeparate(ambiguousKeys.Select(k => k.ToString())));
                throw new InvalidDataException(warning);
            }

            return true;
        }

        private void ThrowIoExceptionInvalidPeakFormat(long lineCount, int i, string sequence)
        {
            ThrowIOException(lineCount,
                string.Format(Resources.NistLibraryBase_CreateCache_Invalid_format_at_peak__0__for__1__, i + 1, sequence));
        }

        private static void ParseFragmentAnnotation(int iTab2, List<List<SpectrumPeakAnnotation>> annotations, int i, string line, Adduct adduct, int charge,
            float mz)
        {
            if (iTab2 == -1)
            {
                annotations[i] = null;
            }
            else
            {
                // Split, for example, "y7-18/-0.06 62/63 0.3" into name="y7-18/-0.06" and note="62/63 0.3"
                var annot = line.Substring(iTab2).Trim();
                // ReSharper disable LocalizableElement
                if (annot.StartsWith("\"")) 
                {
                    annot = annot.Substring(1);
                }
                if (annot.EndsWith("\"")) 
                {
                    annot = annot.Substring(0, annot.Length - 1);
                }
                annot = annot.Trim();
                var space = annot.IndexOf(" ", StringComparison.Ordinal); 
                if (space < 0)
                {
                    annotations.Add(null); // Interesting annotations have more than one part
                }
                else
                {
                    var name = annot.Substring(0, space).Trim();
                    if (string.IsNullOrEmpty(name) || name.StartsWith("?")) 
                    {
                        annotations.Add(null);
                    }
                    else
                    {
                        var note = annot.Substring(space + 1).Trim();
                        var z = Adduct.IsNullOrEmpty(adduct) ? charge : adduct.AdductCharge;
                        var fragment_adduct = z > 0 ? Adduct.M_PLUS : Adduct.M_MINUS;
                        var ion = new CustomIon(null, fragment_adduct,
                            fragment_adduct.MassFromMz(mz, MassType.Monoisotopic),
                            fragment_adduct.MassFromMz(mz, MassType.Average),
                            name);
                        annotations.Add(new List<SpectrumPeakAnnotation> {SpectrumPeakAnnotation.Create(ion, note)});
                    }
                }
                // ReSharper restore LocalizableElement
            }
        }

        private void ThrowIOException(long lineNum, string message)
        {
            throw new IOException(string.Format(Resources.NistLibraryBase_ThrowIOException__0__line__1__2__, FilePath,
                                                lineNum, message));
        }

        private static string Modify(string sequence, string mod)
        {
            // If no modifications, just return the input sequence
            bool clean = (sequence.IndexOfAny(new[] { '(', '[' }) == -1); 
            if (clean && Equals(mod, @"0")) 
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
                if (c == '(' || c == '[') 
                    inMod = true;
                else if (inMod)
                {
                    if (c == ')' || c == ']') 
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
                massDiff = @"[?]"; 
            return index;
        }

        private static double? GetRetentionTime(string rtString, bool isMinutes)
        {
            double rt;
            var valString = rtString.Split(MINOR_SEP).First();
            if (!TryParseDoubleUncertainCulture(valString, out rt))
                return null;
            return isMinutes ? rt : rt / 60;
        }

        private static bool TryParseDoubleUncertainCulture(string valString, out double dval)
        {
            // .MSP from Golm GMD may have European decimals
            if (!double.TryParse(valString, NumberStyles.Float, CultureInfo.InvariantCulture, out dval) &&
                !double.TryParse(valString.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out dval))
            {
                return false;
            }
            return true;
        }

        private static bool TryParseFloatUncertainCulture(string valString, out float fval)
        {
            // .MSP from Golm GMD may have European decimals
            if (!float.TryParse(valString, NumberStyles.Float, CultureInfo.InvariantCulture, out fval) &&
                !float.TryParse(valString.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out fval))
            {
                return false;
            }
            return true;
        }

        protected override void SetLibraryEntries(IEnumerable<NistSpectrumInfo> entries)
        {
            base.SetLibraryEntries(entries);
            _anyRetentionTimes = _libraryEntries.Any(entry => entry.RT.HasValue);
        }

        protected override SpectrumPeaksInfo.MI[] ReadSpectrum(NistSpectrumInfo info)
        {
            byte[] peaksCompressed = new byte[info.CompressedSize];
            byte[] annotationsBytes = info.AnnotationsSize > 0 ? new byte[info.AnnotationsSize] : null;
            lock (ReadStream)
            {
                try
                {
                    Stream fs = ReadStream.Stream;

                    // Seek to stored location
                    fs.Seek(info.Location, SeekOrigin.Begin);

                    // Single read to get all the peaks
                    if (fs.Read(peaksCompressed, 0, peaksCompressed.Length) < peaksCompressed.Length)
                        throw new IOException(Resources.NistLibraryBase_ReadSpectrum_Failure_trying_to_read_peaks);
                    if (annotationsBytes != null && fs.Read(annotationsBytes, 0, info.AnnotationsSize) < info.AnnotationsSize)
                        throw new IOException(Resources.NistLibraryBase_ReadSpectrum_Failure_trying_to_read_peaks);
                }
                catch (Exception)
                {
                    // If an exception is thrown, close the stream in case the failure is something
                    // like a network failure that can be remedied by re-opening the stream.
                    ReadStream.CloseStream();
                    throw;
                }
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
            if (annotationsBytes != null)
            {
                var annotations = SpectrumPeakAnnotation.FromCacheFormat(Encoding.UTF8.GetString(annotationsBytes));
                if (annotations.Count != info.NumPeaks)
                {
                    throw new IOException(Resources.NistLibraryBase_ReadSpectrum_Failure_trying_to_read_peaks);
                }
                for (int i = 0; i < info.NumPeaks; i++)
                {
                    arrayMI[i].Annotations =annotations[i];
                }
            }

            return arrayMI;
        }

        public override IEnumerable<SpectrumInfoLibrary> GetSpectra(LibKey key, IsotopeLabelType labelType, LibraryRedundancy redundancy)
        {
            int i = FindEntry(key);
            if (i != -1)
            {
                yield return new SpectrumInfoLibrary(this, labelType, i)
                {
                    SpectrumHeaderInfo = CreateSpectrumHeaderInfo(_libraryEntries[i]),
                    RetentionTime = _libraryEntries[i].RT
                };
            }
        }

        public override IList<RetentionTimeSource> ListRetentionTimeSources()
        {
            if (_libraryEntries == null)
                return base.ListRetentionTimeSources();

            if (!_anyRetentionTimes)
            {
                return base.ListRetentionTimeSources();
            }
            return new List<RetentionTimeSource> {new RetentionTimeSource(FilePath, Name)};
        }

        public override IEnumerable<double> GetRetentionTimesWithSequences(string filePath, IEnumerable<Target> peptideSequences, ref int? fileIndex)
        {
            return Equals(FilePath, filePath)
                ? LibraryEntriesWithSequences(peptideSequences).Where(spectrum => spectrum.RT.HasValue).Select(spectrum => spectrum.RT.Value)
                : base.GetRetentionTimesWithSequences(filePath, peptideSequences, ref fileIndex);
        }

        public override bool TryGetIrts(out LibraryRetentionTimes retentionTimes)
        {
            var dictionary = new Dictionary<Target, Tuple<TimeSource, double[]>>();
            foreach (var entry in _libraryEntries)
            {
                if (!entry.iRT.HasValue)
                    continue;

                var iRT = entry.iRT.Value;

                Tuple<TimeSource, double[]> tuple;
                if (!dictionary.TryGetValue(entry.Key.Target, out tuple))
                {
                    tuple = Tuple.Create(TimeSource.peak, new[] {iRT});
                }
                else
                {
                    var newIrt = new double[tuple.Item2.Length + 1];
                    tuple.Item2.CopyTo(newIrt, 0);
                    newIrt[newIrt.Length - 1] = iRT;
                    tuple = Tuple.Create(tuple.Item1, newIrt);
                }
                dictionary[entry.Key.Target] = tuple;
            }
            
            retentionTimes = new LibraryRetentionTimes(null, dictionary);
            return dictionary.Any();
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
        private readonly double? _rt;
        private readonly double? _irt;
        private readonly float _totalIntensity;
        private readonly ushort _copies;
        private readonly ushort _numPeaks;
        private readonly int _compressedSize;
        private readonly int _annotationsSize;
        private readonly long _location;

        public NistSpectrumInfo(LibKey key, float tfRatio, double? rt, double? irt, float totalIntensity,
            ushort copies, ushort numPeaks, int compressedSize, int annotationsSize, long location)
        {
            _key = key;
            _totalIntensity = totalIntensity;
            _tfRatio = tfRatio;
            _rt = rt;
            _irt = irt;
            _copies = copies;
            _numPeaks = numPeaks;
            _compressedSize = compressedSize;
            _annotationsSize = annotationsSize;
            _location = location;
        }

        public LibKey Key { get { return _key; } }
// ReSharper disable InconsistentNaming
        public float TFRatio { get { return _tfRatio; } }
// ReSharper restore InconsistentNaming
        public double? RT { get { return _rt; } }
        public double? iRT { get { return _irt; } }
        public float TotalIntensity { get { return _totalIntensity; } }
        public int Copies { get { return _copies; } }
        public int NumPeaks { get { return _numPeaks; } }
        public int CompressedSize { get { return _compressedSize; } }
        public int AnnotationsSize { get { return _annotationsSize; } }
        public long Location { get { return _location; } }
    }
}
