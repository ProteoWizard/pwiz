using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Irt
{
    public class IrtStandard : IAuditLogObject
    {
        public static readonly IrtStandard NULL = new IrtStandard(string.Empty, null, new DbIrtPeptide[0]);

        public static readonly IrtStandard BIOGNOSYS_10 = new IrtStandard("Biognosys-10 (iRT-C18)", "Biognosys10.sky", // Not L10N
            new[] {
                MakePeptide("GAGSSEPVTGLDAK",   0.00), // Not L10N
                MakePeptide("VEATFGVDESNAK",   12.39), // Not L10N
                MakePeptide("YILAGVENSK",      19.79), // Not L10N
                MakePeptide("TPVISGGPYEYR",    28.71), // Not L10N
                MakePeptide("TPVITGAPYEYR",    33.38), // Not L10N
                MakePeptide("DGLDAASYYAPVR",   42.26), // Not L10N
                MakePeptide("ADVTPADFSEWSK",   54.62), // Not L10N
                MakePeptide("GTFIIDPGGVIR",    70.52), // Not L10N
                MakePeptide("GTFIIDPAAVIR",    87.23), // Not L10N
                MakePeptide("LFLQFGAQGSPFLK", 100.00), // Not L10N
            });

        public static readonly IrtStandard BIOGNOSYS_11 = new IrtStandard("Biognosys-11 (iRT-C18)", "Biognosys11.sky", // Not L10N
            new[] {
                MakePeptide("LGGNEQVTR",      -24.92), // Not L10N
                MakePeptide("GAGSSEPVTGLDAK",   0.00), // Not L10N
                MakePeptide("VEATFGVDESNAK",   12.39), // Not L10N
                MakePeptide("YILAGVENSK",      19.79), // Not L10N
                MakePeptide("TPVISGGPYEYR",    28.71), // Not L10N
                MakePeptide("TPVITGAPYEYR",    33.38), // Not L10N
                MakePeptide("DGLDAASYYAPVR",   42.26), // Not L10N
                MakePeptide("ADVTPADFSEWSK",   54.62), // Not L10N
                MakePeptide("GTFIIDPGGVIR",    70.52), // Not L10N
                MakePeptide("GTFIIDPAAVIR",    87.23), // Not L10N
                MakePeptide("LFLQFGAQGSPFLK", 100.00), // Not L10N
            });

        public static readonly IrtStandard PIERCE = new IrtStandard("Pierce (iRT-C18)", "Pierce.sky", // Not L10N
            new[] {
                MakePeptide("SSAAPPPPPR",       -27.60), // Not L10N
                MakePeptide("GISNEGQNASIK",     -17.47), // Not L10N
                MakePeptide("HVLTSIGEK",         -9.98), // Not L10N
                MakePeptide("DIPVPKPK",          -3.88), // Not L10N
                MakePeptide("IGDYAGIK",           7.84), // Not L10N
                MakePeptide("TASEFDSAIAQDK",     18.42), // Not L10N
                MakePeptide("SAAGAFGPELSR",      26.22), // Not L10N
                MakePeptide("ELGQSGVDTYLQTK",    32.61), // Not L10N
                MakePeptide("SFANQPLEVVYSK",     51.41), // Not L10N
                MakePeptide("GLILVGGYGTR",       52.36), // Not L10N
                MakePeptide("GILFVGSGVSGGEEGAR", 54.27), // Not L10N
                MakePeptide("LTILEELR",          71.78), // Not L10N
                MakePeptide("ELASGLSFPVGFK",     79.61), // Not L10N
                MakePeptide("LSSEAPALFQFDLK",    90.41), // Not L10N
            });

        public static readonly IrtStandard REPLICAL = new IrtStandard("RePLiCal (iRT-C18)", "RePLiCal.sky", // Not L10N
            new[]
            {
                // Sigma
                MakePeptide("VTASGDDSPSGK",                    -43.60), // Not L10N
                MakePeptide("ALAEDEGAK",                       -33.28), // Not L10N
                MakePeptide("ASADLQPDSQK",                     -25.62), // Not L10N
                MakePeptide("SSYVGDEASSK",                     -25.48), // Not L10N
                MakePeptide("AAAPEPETETETSSK",                 -23.00), // Not L10N
                MakePeptide("IVPEPQPK",                        -15.67), // Not L10N
                MakePeptide("GAIETEPAVK",                       -7.34), // Not L10N
                MakePeptide("FHPGTDEGDYQVK",                    -2.06), // Not L10N
                MakePeptide("VGYDLPGK",                          9.94), // Not L10N
                MakePeptide("SAGGAFGPELSK",                     18.84), // Not L10N
                MakePeptide("TASEFESAIDAQK",                    23.96), // Not L10N
                MakePeptide("GVNDNEEGFFSAK",                    35.33), // Not L10N
                MakePeptide("VGLFAGAGVGK",                      41.31), // Not L10N
                MakePeptide("TQLIDVEIAK",                       46.78), // Not L10N
                MakePeptide("LTVLESLSK",                        48.94), // Not L10N
                MakePeptide("LAPDLIVVAQTGGK",                   55.84), // Not L10N
                MakePeptide("LTIAPALLK",                        60.08), // Not L10N
                MakePeptide("ILTDIVGPEAPLVK",                   66.84), // Not L10N
                MakePeptide("LTIEEFLK",                         80.84), // Not L10N
                MakePeptide("TSAESILTTGPVVPVIVVK",              89.29), // Not L10N
                MakePeptide("ISSIDLSVLDSPLIPSATTGTSK",          95.78), // Not L10N
                MakePeptide("AGLEFGTTPEQPEETPLDDLAETDFQTFSGK", 103.80), // Not L10N
                MakePeptide("VVSLPDFFTFSK",                    107.18), // Not L10N
                MakePeptide("AVTTLAEAVVAATLGPK",               115.16), // Not L10N
                MakePeptide("IAFFESSFLSYLK",                   120.64), // Not L10N
                MakePeptide("SSIPVFGVDALPEALALVK",             126.72), // Not L10N
                MakePeptide("FLSSPFAVAEVFTGIVGK",              139.70), // Not L10N
            });

        public static readonly IrtStandard RTBEADS = new IrtStandard("RTBEADS (iRT-C18)", "RTBEADS.sky", // Not L10N
            new[]
            {
                MakePeptide("NLAVQAQGK",      -19.05), // Not L10N
                MakePeptide("FIPEGSQGR",      -11.11), // Not L10N
                MakePeptide("FGQTPVQEGR",      -6.03), // Not L10N
                MakePeptide("ELALGQDGR",        0.95), // Not L10N
                MakePeptide("TGLQTLSSEK",       4.13), // Not L10N
                MakePeptide("AGIPNNQVLGK",     12.22), // Not L10N
                MakePeptide("ALDVIQAGGK",      13.81), // Not L10N
                MakePeptide("ALVQIVGK",        24.60), // Not L10N
                MakePeptide("NGFSIQVR",        30.48), // Not L10N
                MakePeptide("EGQLTPLIK",       33.97), // Not L10N
                MakePeptide("FQSVFTVTGR",      47.78), // Not L10N
                MakePeptide("SGIPDNAFQSFGR",   54.60), // Not L10N
                MakePeptide("AGFLEQIGAPQAALR", 71.90), // Not L10N
                MakePeptide("TGQSSLVPALTDFVR", 93.17), // Not L10N
            });

        public static readonly IrtStandard SCIEX = new IrtStandard("SCIEX PepCalMix (iRT-C18)", "Sciex.sky", // Not L10N
            new[] {
                MakePeptide("AETSELHTSLK",        -9.99), // Not L10N
                MakePeptide("GAYVEVTAK",          -4.26), // Not L10N
                MakePeptide("IGNEQGVSR",         -35.06), // Not L10N
                MakePeptide("LVGTPAEER",         -16.52), // Not L10N
                MakePeptide("LDSTSIPVAK",          7.23), // Not L10N
                MakePeptide("AGLIVAEGVTK",        28.13), // Not L10N
                MakePeptide("LGLDFDSFR",          78.77), // Not L10N
                MakePeptide("GFTAYYIPR",          51.02), // Not L10N
                MakePeptide("SGGLLWQLVR",         94.08), // Not L10N
                MakePeptide("AVGANPEQLTR",         1.89), // Not L10N
                MakePeptide("SAEGLDASASLR",       11.86), // Not L10N
                MakePeptide("VFTPLEVDVAK",        58.06), // Not L10N
                MakePeptide("YIELAPGVDNSK",       29.75), // Not L10N
                MakePeptide("DGTFAVDGPGVIAK",     40.06), // Not L10N
                MakePeptide("VGNEIQYVALR",        41.56), // Not L10N
                MakePeptide("ALENDIGVPSDATVK",    31.04), // Not L10N
                MakePeptide("AVYFYAPQIPLYANK",    82.89), // Not L10N
                MakePeptide("TVESLFPEEAETPGSAVR", 57.69), // Not L10N
                MakePeptide("SPYVITGPGVVEYK",     45.47), // Not L10N
                MakePeptide("YDSINNTEVSGIR",      18.26), // Not L10N
            });

        public static readonly IrtStandard SIGMA = new IrtStandard("Sigma (iRT-C18)", "Sigma.sky", // Not L10N
            new[] {
                MakePeptide("AEFAEVSK",            -2.71), // Not L10N
                MakePeptide("SGFSSVSVSR",           7.33), // Not L10N
                MakePeptide("ADEGISFR",            17.99), // Not L10N
                MakePeptide("DISLSDYK",            28.62), // Not L10N
                MakePeptide("LVNEVTEFAK",          33.44), // Not L10N
                MakePeptide("DQGGELLSLR",          46.82), // Not L10N
                MakePeptide("GLFIIDDK",            63.51), // Not L10N
                MakePeptide("YWGVASFLQK",          84.03), // Not L10N
                MakePeptide("TDELFQIEGLKEELAYLR", 101.47), // Not L10N
                MakePeptide("AVQQPDGLAVLGIFLK",   125.03), // Not L10N
            });

        public static readonly IrtStandard APOA1 = new IrtStandard("APOA1 (iRT-C18)", "APOA1.sky", // Not L10N
            new[] {
                MakePeptide("AELQEGAR",      -30.74), // Not L10N
                MakePeptide("LHELQEK",       -29.14), // Not L10N
                MakePeptide("AHVDALR",       -26.80), // Not L10N
                MakePeptide("ATEHLSTLSEK",   -12.04), // Not L10N
                MakePeptide("AKPALEDLR",       5.06), // Not L10N
                MakePeptide("THLAPYSDELR",    12.84), // Not L10N
                MakePeptide("LSPLGEEMR",      25.31), // Not L10N
                MakePeptide("VQPYLDDFQK",     38.44), // Not L10N
                MakePeptide("WQEEMELYR",      45.59), // Not L10N
                MakePeptide("DYVSQFEGSALGK",  57.17), // Not L10N
                MakePeptide("LLDNWDSVTSTFSK", 75.91), // Not L10N
                MakePeptide("DLATVYVDVLK",    85.85), // Not L10N
                MakePeptide("QGLLPVLESFK",    96.87), // Not L10N
                MakePeptide("VSFLSALEEYTK",  106.32), // Not L10N
            });

        public static readonly IrtStandard CIRT_SHORT = new IrtStandard("CiRT (iRT-C18)", "CiRT.sky", // Not L10N
            new[] {
                MakePeptide("DSYVGDEAQSK",                -14.83), // Not L10N
                MakePeptide("AGFAGDDAPR",                  -8.72), // Not L10N
                MakePeptide("ATAGDTHLGGEDFDNR",             5.18), // Not L10N
                MakePeptide("VATVSLPR",                    13.40), // Not L10N
                MakePeptide("ELISNASDALDK",                25.06), // Not L10N
                MakePeptide("IGPLGLSPK",                   29.44), // Not L10N
                MakePeptide("TTPSYVAFTDTER",               34.81), // Not L10N
                MakePeptide("VC[+57.0]ENIPIVLC[+57.0]GNK", 54.97), // Not L10N
                MakePeptide("DLTDYLMK",                    59.78), // Not L10N
                MakePeptide("LGEHNIDVLEGNEQFINAAK",        60.00), // Not L10N
                MakePeptide("SYELPDGQVITIGNER",            66.92), // Not L10N
                MakePeptide("YFPTQALNFAFK",                93.51), // Not L10N
                MakePeptide("SNYNFEKPFLWLAR",              93.98), // Not L10N
                MakePeptide("DSTLIMQLLR",                 101.79), // Not L10N
            });

        public static readonly IrtStandard CIRT = new IrtStandard("CiRT", null, // Not L10N
            new[] {
                MakePeptide("ADTLDPALLRPGR",               35.99), // Not L10N
                MakePeptide("AFEEAEK",                    -21.36), // Not L10N
                MakePeptide("AFLIEEQK",                    22.80), // Not L10N
                MakePeptide("AGFAGDDAPR",                  -9.82), // Not L10N
                MakePeptide("AGLQFPVGR",                   37.05), // Not L10N
                MakePeptide("AILGSVER",                     5.42), // Not L10N
                MakePeptide("APGFGDNR",                   -15.63), // Not L10N
                MakePeptide("AQIWDTAGQER",                 16.85), // Not L10N
                MakePeptide("ATAGDTHLGGEDFDNR",             3.19), // Not L10N
                MakePeptide("ATIGADFLTK",                  43.84), // Not L10N
                MakePeptide("AVANQTSATFLR",                19.25), // Not L10N
                MakePeptide("AVFPSIVGRPR",                 34.03), // Not L10N
                MakePeptide("C[+57.0]ATITPDEAR",          -10.14), // Not L10N
                MakePeptide("DAGTIAGLNVLR",                59.04), // Not L10N
                MakePeptide("DAHQSLLATR",                  -3.25), // Not L10N
                MakePeptide("DELTLEGIK",                   39.13), // Not L10N
                MakePeptide("DLMAC[+57.0]AQTGSGK",          0.31), // Not L10N
                MakePeptide("DLTDYLMK",                    60.01), // Not L10N
                MakePeptide("DNIQGITKPAIR",                12.60), // Not L10N
                MakePeptide("DNTGYDLK",                    -9.40), // Not L10N
                MakePeptide("DSTLIMQLLR",                 103.65), // Not L10N
                MakePeptide("DSYVGDEAQSK",                -15.51), // Not L10N
                MakePeptide("DVQEIFR",                     29.62), // Not L10N
                MakePeptide("DWNVDLIPK",                   70.54), // Not L10N
                MakePeptide("EAYPGDVFYLHSR",               46.35), // Not L10N
                MakePeptide("EC[+57.0]ADLWPR",             28.71), // Not L10N
                MakePeptide("EDAANNYAR",                  -23.23), // Not L10N
                MakePeptide("EGIPPDQQR",                  -15.84), // Not L10N
                MakePeptide("EHAALEPR",                   -22.61), // Not L10N
                MakePeptide("EIAQDFK",                     -4.05), // Not L10N
                MakePeptide("EIQTAVR",                    -17.07), // Not L10N
                MakePeptide("ELIIGDR",                     11.56), // Not L10N
                MakePeptide("ELISNASDALDK",                23.50), // Not L10N
                MakePeptide("EMVELPLR",                    47.97), // Not L10N
                MakePeptide("ESTLHLVLR",                   28.54), // Not L10N
                MakePeptide("EVDIGIPDATGR",                37.10), // Not L10N
                MakePeptide("FDDGAGGDNEVQR",              -11.32), // Not L10N
                MakePeptide("FDLMYAK",                     38.20), // Not L10N
                MakePeptide("FDNLYGC[+57.0]R",              9.61), // Not L10N
                MakePeptide("FEELC[+57.0]ADLFR",           73.50), // Not L10N
                MakePeptide("FELSGIPPAPR",                 52.50), // Not L10N
                MakePeptide("FELTGIPPAPR",                 53.10), // Not L10N
                MakePeptide("FPFAANSR",                    18.76), // Not L10N
                MakePeptide("FQSLGVAFYR",                  60.23), // Not L10N
                MakePeptide("FTQAGSEVSALLGR",              61.45), // Not L10N
                MakePeptide("FTVDLPK",                     37.86), // Not L10N
                MakePeptide("FVIGGPQGDAGLTGR",             40.55), // Not L10N
                MakePeptide("GC[+57.0]EVVVSGK",           -15.49), // Not L10N
                MakePeptide("GEEILSGAQR",                  -1.81), // Not L10N
                MakePeptide("GILFVGSGVSGGEEGAR",           51.15), // Not L10N
                MakePeptide("GILLYGPPGTGK",                45.37), // Not L10N
                MakePeptide("GIRPAINVGLSVSR",              37.98), // Not L10N
                MakePeptide("GNHEC[+57.0]ASINR",          -23.57), // Not L10N
                MakePeptide("GVC[+57.0]TEAGMYALR",         31.21), // Not L10N
                MakePeptide("GVLLYGPPGTGK",                28.12), // Not L10N
                MakePeptide("GVLMYGPPGTGK",                28.21), // Not L10N
                MakePeptide("HFSVEGQLEFR",                 41.11), // Not L10N
                MakePeptide("HITIFSPEGR",                  22.40), // Not L10N
                MakePeptide("HLQLAIR",                      9.43), // Not L10N
                MakePeptide("HLTGEFEK",                   -13.72), // Not L10N
                MakePeptide("HVFGQAAK",                   -24.54), // Not L10N
                MakePeptide("IC[+57.0]DFGLAR",             28.01), // Not L10N
                MakePeptide("IC[+57.0]GDIHGQYYDLLR",       50.35), // Not L10N
                MakePeptide("IETLDPALIRPGR",               43.43), // Not L10N
                MakePeptide("IGGIGTVPVGR",                 21.90), // Not L10N
                MakePeptide("IGLFGGAGVGK",                 43.29), // Not L10N
                MakePeptide("IGPLGLSPK",                   29.48), // Not L10N
                MakePeptide("IHETNLK",                    -25.54), // Not L10N
                MakePeptide("IINEPTAAAIAYGLDK",            65.72), // Not L10N
                MakePeptide("IYGFYDEC[+57.0]K",            31.70), // Not L10N
                MakePeptide("KPLLESGTLGTK",                 9.06), // Not L10N
                MakePeptide("LAEQAER",                    -25.09), // Not L10N
                MakePeptide("LGANSLLDLVVFGR",             134.01), // Not L10N
                MakePeptide("LIEDFLAR",                    56.93), // Not L10N
                MakePeptide("LILIESR",                     28.15), // Not L10N
                MakePeptide("LPLQDVYK",                    29.20), // Not L10N
                MakePeptide("LQIWDTAGQER",                 36.29), // Not L10N
                MakePeptide("LVIVGDGAC[+57.0]GK",          10.80), // Not L10N
                MakePeptide("LVLVGDGGTGK",                 12.02), // Not L10N
                MakePeptide("LYQVEYAFK",                   46.27), // Not L10N
                MakePeptide("MLSC[+57.0]AGADR",           -15.49), // Not L10N
                MakePeptide("NILGGTVFR",                   49.61), // Not L10N
                MakePeptide("NIVEAAAVR",                    5.74), // Not L10N
                MakePeptide("NLLSVAYK",                    34.34), // Not L10N
                MakePeptide("NLQYYDISAK",                  25.80), // Not L10N
                MakePeptide("NMSVIAHVDHGK",                -5.36), // Not L10N
                MakePeptide("QAVDVSPLR",                   11.34), // Not L10N
                MakePeptide("QTVAVGVIK",                    9.90), // Not L10N
                MakePeptide("SAPSTGGVK",                  -27.57), // Not L10N
                MakePeptide("SGQGAFGNMC[+57.0]R",           0.79), // Not L10N
                MakePeptide("SNYNFEKPFLWLAR",              96.02), // Not L10N
                MakePeptide("STELLIR",                     18.10), // Not L10N
                MakePeptide("STTTGHLIYK",                  -9.49), // Not L10N
                MakePeptide("SYELPDGQVITIGNER",            67.30), // Not L10N
                MakePeptide("TIAMDGTEGLVR",                32.82), // Not L10N
                MakePeptide("TIVMGASFR",                   29.53), // Not L10N
                MakePeptide("TLSDYNIQK",                    4.35), // Not L10N
                MakePeptide("TTIFSPEGR",                   15.18), // Not L10N
                MakePeptide("TTPSYVAFTDTER",               33.80), // Not L10N
                MakePeptide("TTVEYLIK",                    30.17), // Not L10N
                MakePeptide("VAVVAGYGDVGK",                15.33), // Not L10N
                MakePeptide("VC[+57.0]ENIPIVLC[+57.0]GNK", 49.07), // Not L10N
                MakePeptide("VLPSIVNEVLK",                 83.75), // Not L10N
                MakePeptide("VPAINVNDSVTK",                17.71), // Not L10N
                MakePeptide("VSTEVDAR",                   -20.14), // Not L10N
                MakePeptide("VVPGYGHAVLR",                  8.62), // Not L10N
                MakePeptide("WPFWLSPR",                    98.38), // Not L10N
                MakePeptide("YAWVLDK",                     41.60), // Not L10N
                MakePeptide("YDSTHGR",                    -57.06), // Not L10N
                MakePeptide("YFPTQALNFAFK",                95.40), // Not L10N
                MakePeptide("YLVLDEADR",                   27.69), // Not L10N
                MakePeptide("YPIEHGIVTNWDDMEK",            56.90), // Not L10N
                MakePeptide("YTQSNSVC[+57.0]YAK",         -12.79), // Not L10N
            });

        public static readonly ImmutableList<IrtStandard> ALL = ImmutableList.ValueOf(new[] {
            NULL, BIOGNOSYS_10, BIOGNOSYS_11, PIERCE, REPLICAL, RTBEADS, SCIEX, SIGMA, APOA1, CIRT_SHORT
        });

        private static readonly HashSet<Target> ALL_TARGETS = new HashSet<Target>(ALL.SelectMany(l => l.Peptides.Select(p => p.ModifiedTarget)));

        /// <summary>
        /// Corrections in percentile of spectral library scan times for peptides with trailing elution profiles that remain
        /// detectable in DDA.
        /// </summary>
        private static readonly Dictionary<Target, double> _peptideSpectrumTimeSkewCorrections = new Dictionary<Target, double>
        {
            // Biognosys
            {new Target("YILAGVENSK"), 0.3}, // Not L10N
            {new Target("DGLDAASYYAPVR"), 0.3}, // Not L10N
            {new Target("LFLQFGAQGSPFLK"), 0.3} // Not L10N
        };

        public static double GetSpectrumTimePercentile(Target modifiedSequence)
        {
            double percentile;
            if (!_peptideSpectrumTimeSkewCorrections.TryGetValue(modifiedSequence, out percentile))
                percentile = 0.5;
            return percentile;
        }

        public IrtStandard(string name, string skyFile, IEnumerable<DbIrtPeptide> peptides)
        {
            Name = name;
            Peptides = ImmutableList.ValueOf(peptides);
            _resourceSkyFile = skyFile;
        }

        private readonly string _resourceSkyFile;
        public string Name { get; private set; }
        public ImmutableList<DbIrtPeptide> Peptides { get; private set; }

        public string AuditLogText { get { return Name; } }
        public bool IsName { get { return true; } }

        public TextReader DocumentReader
        {
            get
            {
                try
                {
                    var stream = typeof(IrtStandard).Assembly.GetManifestResourceStream(typeof(IrtStandard), "StandardsDocuments." + _resourceSkyFile); // Not L10N
                    return stream != null ? new StreamReader(stream) : null;
                }
                catch (Exception)
                {
                    return null;
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

        public static IrtStandard WhichStandard(ICollection<Target> peptides, out HashSet<Target> missingPeptides)
        {
            var standard = ALL.FirstOrDefault(s => s.ContainsAll(peptides.Select(p => MakePeptide(p.Sequence, 0)).ToList(), null)) ?? NULL;
            missingPeptides = new HashSet<Target>(standard.Peptides.Where(s => !peptides.Any(p => p.Equals(s.Target))).Select(s => s.Target));
            return standard;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
