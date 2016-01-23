using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Irt
{
    public class IrtStandard
    {
        public static readonly IrtStandard NULL = new IrtStandard(string.Empty, null, new DbIrtPeptide[0]);

        public static readonly IrtStandard BIOGNOSYS = new IrtStandard("Biognosys (iRT-C18)", "Biognosys.sky", // Not L10N
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

        public static readonly ImmutableList<IrtStandard> ALL = ImmutableList.ValueOf(new[] {
            NULL, BIOGNOSYS, PIERCE, SIGMA, APOA1
        });

        public IrtStandard(string name, string skyFile, IEnumerable<DbIrtPeptide> peptides)
        {
            Name = name;
            Peptides = ImmutableList.ValueOf(peptides);
            _resourceSkyFile = skyFile;
        }

        private readonly string _resourceSkyFile;
        public string Name { get; private set; }
        public ImmutableList<DbIrtPeptide> Peptides { get; private set; }

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
            return Peptides.Count == peptides.Count &&
                   peptides.All(peptide => MatchingStandard(peptide, irtTolerance) != null);
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

        private DbIrtPeptide MatchingStandard(DbIrtPeptide peptide, double? irtTolerance)
        {
            return Peptides.FirstOrDefault(p => Match(peptide, p, irtTolerance));
        }

        public static bool ContainsMatch(IEnumerable<DbIrtPeptide> peptides, DbIrtPeptide peptide, double? irtTolerance)
        {
            return peptides.Any(p => Match(p, peptide, irtTolerance));
        }

        public static bool Match(DbIrtPeptide x, DbIrtPeptide y, double? irtTolerance)
        {
            return Equals(x.PeptideModSeq, y.PeptideModSeq) && (!irtTolerance.HasValue || Math.Abs(x.Irt - y.Irt) < irtTolerance.Value);
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
            return ALL.Any(standard => standard.Contains(peptide, irtTolerance));
        }

        private static DbIrtPeptide MakePeptide(string sequence, double time)
        {
            return new DbIrtPeptide(sequence, time, true, TimeSource.peak);
        }

        public static IrtStandard WhichStandard(IEnumerable<string> peptides)
        {
            return ALL.FirstOrDefault(s => s.IsMatch(peptides.Select(p => MakePeptide(p, 0)).ToList(), null)) ?? NULL;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
