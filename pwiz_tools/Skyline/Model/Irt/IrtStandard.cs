﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Irt
{
    [XmlRoot("irt_standard")]
    public class IrtStandard : XmlNamedElement
    {
        public static IrtStandard AUTO => new IrtStandard(IrtResources.IrtStandard_AUTO_Automatic, null, null, Array.Empty<DbIrtPeptide>());
        public static IrtStandard EMPTY => new IrtStandard(AuditLogStrings.None, null, null, Array.Empty<DbIrtPeptide>());

        public static readonly IrtStandard BIOGNOSYS_10 = new IrtStandard(@"Biognosys-10 (iRT-C18)", @"Biognosys10.sky", null,
            new[] {
                MakePeptide(@"GAGSSEPVTGLDAK",   0.00),
                MakePeptide(@"VEATFGVDESNAK",   12.39),
                MakePeptide(@"YILAGVENSK",      19.79),
                MakePeptide(@"TPVISGGPYEYR",    28.71),
                MakePeptide(@"TPVITGAPYEYR",    33.38),
                MakePeptide(@"DGLDAASYYAPVR",   42.26),
                MakePeptide(@"ADVTPADFSEWSK",   54.62),
                MakePeptide(@"GTFIIDPGGVIR",    70.52),
                MakePeptide(@"GTFIIDPAAVIR",    87.23),
                MakePeptide(@"LFLQFGAQGSPFLK", 100.00),
            });

        public static readonly IrtStandard BIOGNOSYS_11 = new IrtStandard(@"Biognosys-11 (iRT-C18)", @"Biognosys11.sky", null,
            new[] {
                MakePeptide(@"LGGNEQVTR",      -24.92),
                MakePeptide(@"GAGSSEPVTGLDAK",   0.00),
                MakePeptide(@"VEATFGVDESNAK",   12.39),
                MakePeptide(@"YILAGVENSK",      19.79),
                MakePeptide(@"TPVISGGPYEYR",    28.71),
                MakePeptide(@"TPVITGAPYEYR",    33.38),
                MakePeptide(@"DGLDAASYYAPVR",   42.26),
                MakePeptide(@"ADVTPADFSEWSK",   54.62),
                MakePeptide(@"GTFIIDPGGVIR",    70.52),
                MakePeptide(@"GTFIIDPAAVIR",    87.23),
                MakePeptide(@"LFLQFGAQGSPFLK", 100.00),
            });

        public static readonly IrtStandard PIERCE = new IrtStandard(@"Pierce (iRT-C18)", @"Pierce.sky", null,
            new[] {
                MakePeptide(@"SSAAPPPPPR",       -27.60),
                MakePeptide(@"GISNEGQNASIK",     -17.47),
                MakePeptide(@"HVLTSIGEK",         -9.98),
                MakePeptide(@"DIPVPKPK",          -3.88),
                MakePeptide(@"IGDYAGIK",           7.84),
                MakePeptide(@"TASEFDSAIAQDK",     18.42),
                MakePeptide(@"SAAGAFGPELSR",      26.22),
                MakePeptide(@"ELGQSGVDTYLQTK",    32.61),
                MakePeptide(@"SFANQPLEVVYSK",     51.41),
                MakePeptide(@"GLILVGGYGTR",       52.36),
                MakePeptide(@"GILFVGSGVSGGEEGAR", 54.27),
                MakePeptide(@"LTILEELR",          71.78),
                MakePeptide(@"ELASGLSFPVGFK",     79.61),
                MakePeptide(@"LSSEAPALFQFDLK",    90.41),
            });

        public static readonly IrtStandard REPLICAL = new IrtStandard(@"RePLiCal (iRT-C18)", @"RePLiCal.sky", null,
            new[]
            {
                // Sigma
                MakePeptide(@"VTASGDDSPSGK",                    -43.60),
                MakePeptide(@"ALAEDEGAK",                       -33.28),
                MakePeptide(@"ASADLQPDSQK",                     -25.62),
                MakePeptide(@"SSYVGDEASSK",                     -25.48),
                MakePeptide(@"AAAPEPETETETSSK",                 -23.00),
                MakePeptide(@"IVPEPQPK",                        -15.67),
                MakePeptide(@"GAIETEPAVK",                       -7.34),
                MakePeptide(@"FHPGTDEGDYQVK",                    -2.06),
                MakePeptide(@"VGYDLPGK",                          9.94),
                MakePeptide(@"SAGGAFGPELSK",                     18.84),
                MakePeptide(@"TASEFESAIDAQK",                    23.96),
                MakePeptide(@"GVNDNEEGFFSAK",                    35.33),
                MakePeptide(@"VGLFAGAGVGK",                      41.31),
                MakePeptide(@"TQLIDVEIAK",                       46.78),
                MakePeptide(@"LTVLESLSK",                        48.94),
                MakePeptide(@"LAPDLIVVAQTGGK",                   55.84),
                MakePeptide(@"LTIAPALLK",                        60.08),
                MakePeptide(@"ILTDIVGPEAPLVK",                   66.84),
                MakePeptide(@"LTIEEFLK",                         80.84),
                MakePeptide(@"TSAESILTTGPVVPVIVVK",              89.29),
                MakePeptide(@"ISSIDLSVLDSPLIPSATTGTSK",          95.78),
                MakePeptide(@"AGLEFGTTPEQPEETPLDDLAETDFQTFSGK", 103.80),
                MakePeptide(@"VVSLPDFFTFSK",                    107.18),
                MakePeptide(@"AVTTLAEAVVAATLGPK",               115.16),
                MakePeptide(@"IAFFESSFLSYLK",                   120.64),
                MakePeptide(@"SSIPVFGVDALPEALALVK",             126.72),
                MakePeptide(@"FLSSPFAVAEVFTGIVGK",              139.70),
            });

        public static readonly IrtStandard RTBEADS = new IrtStandard(@"RTBEADS (iRT-C18)", @"RTBEADS.sky", null,
            new[]
            {
                MakePeptide(@"NLAVQAQGK",      -19.05),
                MakePeptide(@"FIPEGSQGR",      -11.11),
                MakePeptide(@"FGQTPVQEGR",      -6.03),
                MakePeptide(@"ELALGQDGR",        0.95),
                MakePeptide(@"TGLQTLSSEK",       4.13),
                MakePeptide(@"AGIPNNQVLGK",     12.22),
                MakePeptide(@"ALDVIQAGGK",      13.81),
                MakePeptide(@"ALVQIVGK",        24.60),
                MakePeptide(@"NGFSIQVR",        30.48),
                MakePeptide(@"EGQLTPLIK",       33.97),
                MakePeptide(@"FQSVFTVTGR",      47.78),
                MakePeptide(@"SGIPDNAFQSFGR",   54.60),
                MakePeptide(@"AGFLEQIGAPQAALR", 71.90),
                MakePeptide(@"TGQSSLVPALTDFVR", 93.17),
            });

        public static readonly IrtStandard SCIEX = new IrtStandard(@"SCIEX PepCalMix (iRT-C18)", @"Sciex.sky", null,
            new[] {
                MakePeptide(@"AETSELHTSLK",        -9.99),
                MakePeptide(@"GAYVEVTAK",          -4.26),
                MakePeptide(@"IGNEQGVSR",         -35.06),
                MakePeptide(@"LVGTPAEER",         -16.52),
                MakePeptide(@"LDSTSIPVAK",          7.23),
                MakePeptide(@"AGLIVAEGVTK",        28.13),
                MakePeptide(@"LGLDFDSFR",          78.77),
                MakePeptide(@"GFTAYYIPR",          51.02),
                MakePeptide(@"SGGLLWQLVR",         94.08),
                MakePeptide(@"AVGANPEQLTR",         1.89),
                MakePeptide(@"SAEGLDASASLR",       11.86),
                MakePeptide(@"VFTPLEVDVAK",        58.06),
                MakePeptide(@"YIELAPGVDNSK",       29.75),
                MakePeptide(@"DGTFAVDGPGVIAK",     40.06),
                MakePeptide(@"VGNEIQYVALR",        41.56),
                MakePeptide(@"ALENDIGVPSDATVK",    31.04),
                MakePeptide(@"AVYFYAPQIPLYANK",    82.89),
                MakePeptide(@"TVESLFPEEAETPGSAVR", 57.69),
                MakePeptide(@"SPYVITGPGVVEYK",     45.47),
                MakePeptide(@"YDSINNTEVSGIR",      18.26),
            });

        public static readonly IrtStandard SIGMA = new IrtStandard(@"Sigma (iRT-C18)", @"Sigma.sky", null,
            new[] {
                MakePeptide(@"AEFAEVSK",            -2.71),
                MakePeptide(@"SGFSSVSVSR",           7.33),
                MakePeptide(@"ADEGISFR",            17.99),
                MakePeptide(@"DISLSDYK",            28.62),
                MakePeptide(@"LVNEVTEFAK",          33.44),
                MakePeptide(@"DQGGELLSLR",          46.82),
                MakePeptide(@"GLFIIDDK",            63.51),
                MakePeptide(@"YWGVASFLQK",          84.03),
                MakePeptide(@"TDELFQIEGLKEELAYLR", 101.47),
                MakePeptide(@"AVQQPDGLAVLGIFLK",   125.03),
            });

        public static readonly IrtStandard APOA1 = new IrtStandard(@"APOA1 (iRT-C18)", @"APOA1.sky", null,
            new[] {
                MakePeptide(@"AELQEGAR",      -30.74),
                MakePeptide(@"LHELQEK",       -29.14),
                MakePeptide(@"AHVDALR",       -26.80),
                MakePeptide(@"ATEHLSTLSEK",   -12.04),
                MakePeptide(@"AKPALEDLR",       5.06),
                MakePeptide(@"THLAPYSDELR",    12.84),
                MakePeptide(@"LSPLGEEMR",      25.31),
                MakePeptide(@"VQPYLDDFQK",     38.44),
                MakePeptide(@"WQEEMELYR",      45.59),
                MakePeptide(@"DYVSQFEGSALGK",  57.17),
                MakePeptide(@"LLDNWDSVTSTFSK", 75.91),
                MakePeptide(@"DLATVYVDVLK",    85.85),
                MakePeptide(@"QGLLPVLESFK",    96.87),
                MakePeptide(@"VSFLSALEEYTK",  106.32),
            });

        public static readonly IrtStandard CIRT_SHORT = new IrtStandard(@"CiRT (iRT-C18)", @"CiRT.sky", null,
            new[] {
                MakePeptide(@"DSYVGDEAQSK",                -14.83),
                MakePeptide(@"AGFAGDDAPR",                  -8.72),
                MakePeptide(@"ATAGDTHLGGEDFDNR",             5.18),
                MakePeptide(@"VATVSLPR",                    13.40),
                MakePeptide(@"ELISNASDALDK",                25.06),
                MakePeptide(@"IGPLGLSPK",                   29.44),
                MakePeptide(@"TTPSYVAFTDTER",               34.81),
                MakePeptide(@"VC[+57.021464]ENIPIVLC[+57.021464]GNK", 54.97),
                MakePeptide(@"DLTDYLMK",                    59.78),
                MakePeptide(@"LGEHNIDVLEGNEQFINAAK",        60.00),
                MakePeptide(@"SYELPDGQVITIGNER",            66.92),
                MakePeptide(@"YFPTQALNFAFK",                93.51),
                MakePeptide(@"SNYNFEKPFLWLAR",              93.98),
                MakePeptide(@"DSTLIMQLLR",                 101.79),
            });

        public static readonly IrtStandard CIRT = new IrtStandard(@"CiRT", @"CiRT_all.sky", null,
            new[] {
                MakePeptide(@"ADTLDPALLRPGR",               35.99),
                MakePeptide(@"AFEEAEK",                    -21.36),
                MakePeptide(@"AFLIEEQK",                    22.80),
                MakePeptide(@"AGFAGDDAPR",                  -9.82),
                MakePeptide(@"AGLQFPVGR",                   37.05),
                MakePeptide(@"AILGSVER",                     5.42),
                MakePeptide(@"APGFGDNR",                   -15.63),
                MakePeptide(@"AQIWDTAGQER",                 16.85),
                MakePeptide(@"ATAGDTHLGGEDFDNR",             3.19),
                MakePeptide(@"ATIGADFLTK",                  43.84),
                MakePeptide(@"AVANQTSATFLR",                19.25),
                MakePeptide(@"AVFPSIVGRPR",                 34.03),
                MakePeptide(@"C[+57.0]ATITPDEAR",          -10.14),
                MakePeptide(@"DAGTIAGLNVLR",                59.04),
                MakePeptide(@"DAHQSLLATR",                  -3.25),
                MakePeptide(@"DELTLEGIK",                   39.13),
                MakePeptide(@"DLMAC[+57.0]AQTGSGK",          0.31),
                MakePeptide(@"DLTDYLMK",                    60.01),
                MakePeptide(@"DNIQGITKPAIR",                12.60),
                MakePeptide(@"DNTGYDLK",                    -9.40),
                MakePeptide(@"DSTLIMQLLR",                 103.65),
                MakePeptide(@"DSYVGDEAQSK",                -15.51),
                MakePeptide(@"DVQEIFR",                     29.62),
                MakePeptide(@"DWNVDLIPK",                   70.54),
                MakePeptide(@"EAYPGDVFYLHSR",               46.35),
                MakePeptide(@"EC[+57.0]ADLWPR",             28.71),
                MakePeptide(@"EDAANNYAR",                  -23.23),
                MakePeptide(@"EGIPPDQQR",                  -15.84),
                MakePeptide(@"EHAALEPR",                   -22.61),
                MakePeptide(@"EIAQDFK",                     -4.05),
                MakePeptide(@"EIQTAVR",                    -17.07),
                MakePeptide(@"ELIIGDR",                     11.56),
                MakePeptide(@"ELISNASDALDK",                23.50),
                MakePeptide(@"EMVELPLR",                    47.97),
                MakePeptide(@"ESTLHLVLR",                   28.54),
                MakePeptide(@"EVDIGIPDATGR",                37.10),
                MakePeptide(@"FDDGAGGDNEVQR",              -11.32),
                MakePeptide(@"FDLMYAK",                     38.20),
                MakePeptide(@"FDNLYGC[+57.0]R",              9.61),
                MakePeptide(@"FEELC[+57.0]ADLFR",           73.50),
                MakePeptide(@"FELSGIPPAPR",                 52.50),
                MakePeptide(@"FELTGIPPAPR",                 53.10),
                MakePeptide(@"FPFAANSR",                    18.76),
                MakePeptide(@"FQSLGVAFYR",                  60.23),
                MakePeptide(@"FTQAGSEVSALLGR",              61.45),
                MakePeptide(@"FTVDLPK",                     37.86),
                MakePeptide(@"FVIGGPQGDAGLTGR",             40.55),
                MakePeptide(@"GC[+57.0]EVVVSGK",           -15.49),
                MakePeptide(@"GEEILSGAQR",                  -1.81),
                MakePeptide(@"GILFVGSGVSGGEEGAR",           51.15),
                MakePeptide(@"GILLYGPPGTGK",                45.37),
                MakePeptide(@"GIRPAINVGLSVSR",              37.98),
                MakePeptide(@"GNHEC[+57.0]ASINR",          -23.57),
                MakePeptide(@"GVC[+57.0]TEAGMYALR",         31.21),
                MakePeptide(@"GVLLYGPPGTGK",                28.12),
                MakePeptide(@"GVLMYGPPGTGK",                28.21),
                MakePeptide(@"HFSVEGQLEFR",                 41.11),
                MakePeptide(@"HITIFSPEGR",                  22.40),
                MakePeptide(@"HLQLAIR",                      9.43),
                MakePeptide(@"HLTGEFEK",                   -13.72),
                MakePeptide(@"HVFGQAAK",                   -24.54),
                MakePeptide(@"IC[+57.0]DFGLAR",             28.01),
                MakePeptide(@"IC[+57.0]GDIHGQYYDLLR",       50.35),
                MakePeptide(@"IETLDPALIRPGR",               43.43),
                MakePeptide(@"IGGIGTVPVGR",                 21.90),
                MakePeptide(@"IGLFGGAGVGK",                 43.29),
                MakePeptide(@"IGPLGLSPK",                   29.48),
                MakePeptide(@"IHETNLK",                    -25.54),
                MakePeptide(@"IINEPTAAAIAYGLDK",            65.72),
                MakePeptide(@"IYGFYDEC[+57.0]K",            31.70),
                MakePeptide(@"KPLLESGTLGTK",                 9.06),
                MakePeptide(@"LAEQAER",                    -25.09),
                MakePeptide(@"LGANSLLDLVVFGR",             134.01),
                MakePeptide(@"LIEDFLAR",                    56.93),
                MakePeptide(@"LILIESR",                     28.15),
                MakePeptide(@"LPLQDVYK",                    29.20),
                MakePeptide(@"LQIWDTAGQER",                 36.29),
                MakePeptide(@"LVIVGDGAC[+57.0]GK",          10.80),
                MakePeptide(@"LVLVGDGGTGK",                 12.02),
                MakePeptide(@"LYQVEYAFK",                   46.27),
                MakePeptide(@"MLSC[+57.0]AGADR",           -15.49),
                MakePeptide(@"NILGGTVFR",                   49.61),
                MakePeptide(@"NIVEAAAVR",                    5.74),
                MakePeptide(@"NLLSVAYK",                    34.34),
                MakePeptide(@"NLQYYDISAK",                  25.80),
                MakePeptide(@"NMSVIAHVDHGK",                -5.36),
                MakePeptide(@"QAVDVSPLR",                   11.34),
                MakePeptide(@"QTVAVGVIK",                    9.90),
                MakePeptide(@"SAPSTGGVK",                  -27.57),
                MakePeptide(@"SGQGAFGNMC[+57.0]R",           0.79),
                MakePeptide(@"SNYNFEKPFLWLAR",              96.02),
                MakePeptide(@"STELLIR",                     18.10),
                MakePeptide(@"STTTGHLIYK",                  -9.49),
                MakePeptide(@"SYELPDGQVITIGNER",            67.30),
                MakePeptide(@"TIAMDGTEGLVR",                32.82),
                MakePeptide(@"TIVMGASFR",                   29.53),
                MakePeptide(@"TLSDYNIQK",                    4.35),
                MakePeptide(@"TTIFSPEGR",                   15.18),
                MakePeptide(@"TTPSYVAFTDTER",               33.80),
                MakePeptide(@"TTVEYLIK",                    30.17),
                MakePeptide(@"VAVVAGYGDVGK",                15.33),
                MakePeptide(@"VC[+57.0]ENIPIVLC[+57.0]GNK", 49.07),
                MakePeptide(@"VLPSIVNEVLK",                 83.75),
                MakePeptide(@"VPAINVNDSVTK",                17.71),
                MakePeptide(@"VSTEVDAR",                   -20.14),
                MakePeptide(@"VVPGYGHAVLR",                  8.62),
                MakePeptide(@"WPFWLSPR",                    98.38),
                MakePeptide(@"YAWVLDK",                     41.60),
                MakePeptide(@"YDSTHGR",                    -57.06),
                MakePeptide(@"YFPTQALNFAFK",                95.40),
                MakePeptide(@"YLVLDEADR",                   27.69),
                MakePeptide(@"YPIEHGIVTNWDDMEK",            56.90),
                MakePeptide(@"YTQSNSVC[+57.0]YAK",         -12.79),
            });

        public static IEnumerable<IrtStandard> ALL => new[] {
            EMPTY, AUTO, BIOGNOSYS_10, BIOGNOSYS_11, PIERCE, REPLICAL, RTBEADS, SCIEX, SIGMA, APOA1, CIRT_SHORT
        };

        private static readonly HashSet<Target> ALL_TARGETS = new HashSet<Target>(ALL.SelectMany(l => l.Peptides.Select(p => p.ModifiedTarget)));

        /// <summary>
        /// Corrections in percentile of spectral library scan times for peptides with trailing elution profiles that remain
        /// detectable in DDA.
        /// </summary>
        private static readonly Dictionary<Target, double> _peptideSpectrumTimeSkewCorrections = new Dictionary<Target, double>
        {
            // Biognosys
            {new Target(@"YILAGVENSK"), 0.3},
            {new Target(@"DGLDAASYYAPVR"), 0.3},
            {new Target(@"LFLQFGAQGSPFLK"), 0.3}
        };

        public static double GetSpectrumTimePercentile(Target modifiedSequence)
        {
            double percentile;
            if (!_peptideSpectrumTimeSkewCorrections.TryGetValue(modifiedSequence, out percentile))
                percentile = 0.5;
            return percentile;
        }

        public IrtStandard(string name, string skyFile, string docXml, IEnumerable<DbIrtPeptide> peptides) : base(name)
        {
            Peptides = ImmutableList.ValueOf(peptides);
            _resourceSkyFile = skyFile;
            DocXml = docXml;
        }

        private string _resourceSkyFile;
        public string DocXml { get; private set; }
        public ImmutableList<DbIrtPeptide> Peptides { get; private set; }

        public override string AuditLogText => IsEmpty ? LogMessage.NONE : Name;
        public override bool IsName => !IsEmpty; // So EMPTY logs as None (unquoted) rather than "None"

        public bool IsEmpty => Equals(this, EMPTY);
        public bool IsAuto => Equals(this, AUTO);

        public bool HasDocument => !string.IsNullOrEmpty(_resourceSkyFile) || !string.IsNullOrEmpty(DocXml);

        public TextReader GetReader()
        {
            if (!string.IsNullOrEmpty(_resourceSkyFile))
            {
                var stream = typeof(IrtStandard).Assembly.GetManifestResourceStream(typeof(IrtStandard), @"StandardsDocuments." + _resourceSkyFile);
                if (stream != null)
                    return new StreamReader(stream);
            }
            return !string.IsNullOrEmpty(DocXml) ? new StringReader(DocXml) : null;
        }

        public SrmDocument GetDocument()
        {
            using (var reader = GetReader())
                return reader != null ? (SrmDocument) new XmlSerializer(typeof(SrmDocument)).Deserialize(reader) : null;
        }

        public SrmDocument ImportTo(SrmDocument document)
        {
            return ImportTo(document, null, out _);
        }

        public SrmDocument ImportTo(SrmDocument document, PeptideLibraries.FindLibrary findLibrary, out IdentityPath firstAdded)
        {
            firstAdded = null;
            using (var reader = GetReader())
            {
                return reader != null
                    ? document.ImportDocumentXml(reader,
                        string.Empty,
                        MeasuredResults.MergeAction.remove,
                        false,
                        findLibrary,
                        Settings.Default.StaticModList,
                        Settings.Default.HeavyModList,
                        document.Children.Any() ? new IdentityPath(document.Children.First().Id) : null,
                        out firstAdded,
                        out _,
                        false)
                    : null;
            }
        }

        public IEnumerable<Target> MissingFromDocument(SrmDocument document)
        {
            var found = new TargetMap<bool>(FindInDocument(document).Select(nodePep => new KeyValuePair<Target, bool>(nodePep.ModifiedTarget, true)));
            return Peptides.Where(pep => !found.ContainsKey(pep.ModifiedTarget)).Select(pep => pep.ModifiedTarget);
        }

        public IEnumerable<PeptideDocNode> FindInDocument(SrmDocument document)
        {
            var standardDoc = GetDocument();
            var docPeps = new TargetMap<Dictionary<int, SignedMz>>(standardDoc != null
                ? standardDoc.Peptides.Select(pep =>
                    new KeyValuePair<Target, Dictionary<int, SignedMz>>(pep.ModifiedTarget,
                        pep.TransitionGroups.ToDictionary(nodeTranGroup => nodeTranGroup.PrecursorCharge, nodeTranGroup => nodeTranGroup.PrecursorMz)))
                : Peptides.Select(pep => new KeyValuePair<Target, Dictionary<int, SignedMz>>(pep.ModifiedTarget, null)));
            // Compare precursor m/z to see if heavy labeled
            foreach (var nodePep in document.Peptides)
            {
                if (docPeps.TryGetValue(nodePep.ModifiedTarget, out var precursors) &&
                    (precursors == null || nodePep.TransitionGroups.Any(nodeTranGroup => precursors.TryGetValue(nodeTranGroup.PrecursorCharge, out var mz) && Math.Abs(mz - nodeTranGroup.PrecursorMz) < 1)))
                {
                    yield return nodePep;
                }
            }
        }

        /// <summary>
        /// Determines whether a collection of peptides matches this set of standard peptides.
        /// </summary>
        /// <param name="peptides">The collection of peptides to check.</param>
        /// <param name="irtTolerance">Tolerance used to compare iRTs; if null, iRTs are not required to match.</param>
        /// <returns>True if the collection of peptides matches the set of standard peptides; false otherwise.</returns>
        public bool IsMatch(IList<DbIrtPeptide> peptides, double? irtTolerance)
        {
            return Peptides.Count == peptides.Count && ContainsAll(peptides, irtTolerance);
        }

        /// <summary>
        /// Determines whether this set of standard peptides contains all peptides in a collection.
        /// </summary>
        /// <param name="peptides">The collection of peptides to check.</param>
        /// <param name="irtTolerance">Tolerance used to compare iRTs; if null, iRTs are not required to match.</param>
        /// <returns>True if the set of standard peptides contains all peptides in the collection; false otherwise.</returns>
        private bool ContainsAll(ICollection<DbIrtPeptide> peptides, double? irtTolerance)
        {
            return peptides.Count > 0 && peptides.All(peptide => MatchingStandard(peptide, irtTolerance) != null);
        }

        public bool IsSubset(ICollection<DbIrtPeptide> peptides, double? irtTolerance)
        {
            return Peptides.Count > 0 && Peptides.All(p => peptides.FirstOrDefault(peptide => Match(p, peptide, irtTolerance)) != null);
        }

        /// <summary>
        /// Determines whether a peptide is contained in this set of standard peptides.
        /// </summary>
        /// <param name="peptide">The peptide to check for.</param>
        /// <param name="irtTolerance">Tolerance used to compare iRTs; if null, iRTs are not required to match.</param>
        /// <returns>True if the peptide is contained in this set of standard peptides; false otherwise.</returns>
        public bool Contains(DbIrtPeptide peptide, double? irtTolerance)
        {
            return ContainsMatch(Peptides, peptide, irtTolerance);
        }

        public bool Contains(Target peptideModSeq)
        {
            return ContainsMatch(Peptides, peptideModSeq);
        }

        private DbIrtPeptide MatchingStandard(DbIrtPeptide peptide, double? irtTolerance)
        {
            return Peptides.FirstOrDefault(p => Match(peptide, p, irtTolerance));
        }

        public static bool ContainsMatch(IEnumerable<DbIrtPeptide> peptides, DbIrtPeptide peptide, double? irtTolerance)
        {
            return peptides.Any(p => Match(p, peptide, irtTolerance));
        }

        public static bool ContainsMatch(IEnumerable<DbIrtPeptide> peptides, Target peptideModSeq)
        {
            return peptides.Any(p => Equals(p.ModifiedTarget, peptideModSeq));
        }

        public static bool Match(DbIrtPeptide x, DbIrtPeptide y, double? irtTolerance)
        {
            return Equals(x.GetNormalizedModifiedSequence(), y.GetNormalizedModifiedSequence()) &&
                   (!irtTolerance.HasValue || Math.Abs(x.Irt - y.Irt) < irtTolerance.Value);
        }

        /// <summary>
        /// Determines whether all of the peptides in a collection are in any of the iRT standards.
        /// </summary>
        /// <param name="peptides">The collection of peptides to check.</param>
        /// <param name="irtTolerance">Tolerance used to compare iRTs; if null, iRTs are not required to match.</param>
        /// <returns>True if all of the peptides in the collection are in any of the iRT standards; false otherwise.</returns>
        public static bool AllStandards(IEnumerable<DbIrtPeptide> peptides, double? irtTolerance)
        {
            return peptides.All(peptide => AnyContains(peptide, irtTolerance));
        }

        /// <summary>
        /// Determines whether any of the iRT standards contain the peptide.
        /// </summary>
        /// <param name="peptide">The peptide to check for.</param>
        /// <param name="irtTolerance">Tolerance used to compare iRTs; if null, iRTs are not required to match.</param>
        /// <returns>True if the peptide is in any of the iRT standards; false otherwise.</returns>
        public static bool AnyContains(DbIrtPeptide peptide, double? irtTolerance)
        {
            // Shortcircuit with AnyContains, because it is much faster
            return AnyContains(peptide.ModifiedTarget) &&
                   ALL.Any(standard => standard.Contains(peptide, irtTolerance));
        }

        public static bool AnyContains(Target peptideModSeq)
        {
            return ALL_TARGETS.Contains(peptideModSeq);
        }

        private static DbIrtPeptide MakePeptide(string sequence, double time)
        {
            return new DbIrtPeptide(new Target(sequence), time, true, TimeSource.peak);
        }

        public static IrtStandard WhichStandard(IEnumerable<Target> peptides)
        {
            var list = peptides.Select(p => new DbIrtPeptide(p, 0, true, TimeSource.peak)).ToList();
            return ALL.Append(CIRT).FirstOrDefault(s => s.ContainsAll(list, null)) ?? EMPTY;
        }

        public IrtStandard ChangePeptides(IEnumerable<DbIrtPeptide> peptides)
        {
            return ChangeProp(ImClone(this), im => im.Peptides = ImmutableList.ValueOf(peptides));
        }

        public IrtStandard ChangeDocXml(string docXml)
        {
            return ChangeProp(ImClone(this), im => im.DocXml = docXml);
        }

        public static List<IrtStandard> BestMatch(IEnumerable<Target> targets)
        {
            var targetMap = new TargetMap<bool>(targets.Select(t => new KeyValuePair<Target, bool>(t, true)));
            var matches = ALL.Where(s => s.Peptides.Count > 0 && s.Peptides.All(pep => targetMap.ContainsKey(pep.ModifiedTarget))).ToList();

            if (matches.Contains(BIOGNOSYS_10) && matches.Contains(BIOGNOSYS_11))
                matches.Remove(BIOGNOSYS_10);

            return matches;
        }

        public static List<IrtStandard> BestMatch(ICollection<SpectrumMzInfo> targets)
        {
            var potentialMatches = BestMatch(targets.Select(t => t.Key.Target)).ToList();

            var matches = new List<IrtStandard>();
            foreach (var standard in potentialMatches)
            {
                var doc = standard.GetDocument();
                if (doc == null || doc.Peptides.All(nodePep => nodePep.ExplicitModsHeavy.Count == 0))
                {
                    matches.Add(standard);
                    continue;
                }

                // Compare precursor m/zs in document (heavy label check)
                var docPeps = new TargetMap<PeptideDocNode>(doc.Peptides.Select(pep => new KeyValuePair<Target, PeptideDocNode>(pep.ModifiedTarget, pep)));
                var matchTargets = doc.Peptides.Select(pep => pep.ModifiedTarget).ToHashSet();
                foreach (var target in targets)
                {
                    if (!docPeps.TryGetValue(target.Key.Target, out var nodePep))
                        continue;

                    var nodeTranGroup = nodePep.TransitionGroups.FirstOrDefault(precursor => precursor.PrecursorCharge == target.Key.Charge);
                    if (nodeTranGroup != null && Math.Abs(nodeTranGroup.PrecursorMz - target.PrecursorMz) < 1)
                        matchTargets.Remove(nodePep.ModifiedTarget);
                }
                if (matchTargets.Count == 0)
                    matches.Add(standard);
            }

            if (matches.Contains(BIOGNOSYS_10) && matches.Contains(BIOGNOSYS_11))
                matches.Remove(BIOGNOSYS_10);

            return matches;
        }

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private IrtStandard()
        {
        }

        private enum ATTR
        {
            resource_sky_file,
            sequence,
            value
        }

        private enum EL
        {
            irt_peptide,
            doc_xml
        }

        public static IrtStandard Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new IrtStandard());
        }

        public override void ReadXml(XmlReader reader)
        {
            // Read tag attributes
            base.ReadXml(reader);

            _resourceSkyFile = reader.GetAttribute(ATTR.resource_sky_file);

            bool empty = reader.IsEmptyElement;
            // Consume tag
            reader.Read();

            var listPeptides = new List<DbIrtPeptide>();
            if (!empty)
            {
                while (reader.IsStartElement(EL.irt_peptide))
                {
                    string sequence = reader.GetAttribute(ATTR.sequence);
                    double? value = reader.GetNullableDoubleAttribute(ATTR.value);
                    if (string.IsNullOrEmpty(sequence) || !value.HasValue)
                        throw new InvalidDataException(IrtResources.IrtStandard_ReadXml_Standard_peptides_must_specify_a_sequence_and_score_);
                    listPeptides.Add(MakePeptide(sequence, value.Value));
                    reader.Read();
                }

                if (reader.IsStartElement(EL.doc_xml))
                {
                    reader.ReadElementContentAsString();
                }
                reader.ReadEndElement();
            }

            Peptides = ImmutableList<DbIrtPeptide>.ValueOf(listPeptides);
        }

        public override void WriteXml(XmlWriter writer)
        {
            // Write tag attributes
            base.WriteXml(writer);

            writer.WriteAttributeIfString(ATTR.resource_sky_file, _resourceSkyFile);
            foreach (var dbIrtPeptide in Peptides)
            {
                writer.WriteStartElement(EL.irt_peptide);
                writer.WriteAttributeString(ATTR.sequence, dbIrtPeptide.Target.Sequence);
                writer.WriteAttribute(ATTR.value, dbIrtPeptide.Irt);
                writer.WriteEndElement();
            }

            if (!string.IsNullOrEmpty(DocXml))
            {
                writer.WriteStartElement(EL.doc_xml);
                writer.WriteString(DocXml);
                writer.WriteEndElement();
            }
        }

        #endregion

        #region object overrides

        protected bool Equals(IrtStandard other)
        {
            return base.Equals(other) &&
                   _resourceSkyFile == other._resourceSkyFile &&
                   DocXml == other.DocXml &&
                   Equals(Peptides, other.Peptides);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((IrtStandard)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = base.GetHashCode();
                hashCode = (hashCode * 397) ^ (_resourceSkyFile != null ? _resourceSkyFile.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (DocXml != null ? DocXml.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Peptides != null ? Peptides.GetHashCode() : 0);
                return hashCode;
            }
        }

        public override string ToString()
        {
            return Name;
        }

        #endregion
    }
}
