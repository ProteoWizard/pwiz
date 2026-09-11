// Port of pwiz_tools/BiblioSpec/src/TSVReader.{h,cpp}
//
// Parses several tab-delimited TSV variants used by spectral-library / DIA-result tools:
//   * OpenSWATH result TSV  (filename/RT/FullPeptideName/Charge/m/z/decoy/aggr_Peak_Area/...)
//   * OpenSWATH assay TSV   (NormalizedRetentionTime/ModifiedPeptideSequence/PrecursorCharge/...)
//   * AlphaPepDeep         (NormalizedRetentionTimeAPD/ModifiedPeptideSequence/FragmentCharge/...)
//   * Bruker Paser library  (NormalizedRetentionTime/ModifiedPeptideSequence/.../DecoyMobility/GeneName)
//   * Bruker Paser result   (File.Name/Modified.Sequence/Q.Value/.../Precursor.Mz/Ms1.Area/Fragment.Info)
//
// The cpp file has a factory `TSVReader::create(...)` that sniffs the header row and picks
// the right concrete subclass. The C# port keeps the same shape: <see cref="TSVReader.Create"/>
// returns a <see cref="BuildParser"/> for the matching variant, and each variant is an inner
// nested class so the file is self-contained.

using System.Globalization;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Unimod;

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// Public dispatcher that lives on the BlibBuilder reader-factory list. Sniffs the .tsv
/// header to pick the right concrete TSV subclass (OpenSWATH result/assay, AlphaPepDeep,
/// Bruker Paser library/result) and delegates to it.
/// </summary>
/// <remarks>
/// <para>Port of <c>BiblioSpec::TSVReader</c> at <c>pwiz_tools/BiblioSpec/src/TSVReader.{h,cpp}</c>.
/// Concrete variants are <see cref="TSVReader.OpenSwathResultReader"/>,
/// <see cref="TSVReader.OpenSwathAssayReader"/>, <see cref="TSVReader.AlphaPepDeepReader"/>,
/// <see cref="TSVReader.PaserLibraryReader"/>, <see cref="TSVReader.PaserResultReader"/>.</para>
/// <para>cpp's factory throws if no header match is found ("Did not find required columns.
/// Only OpenSWATH result, OpenSWATH assay, AlphaPepDeep, and Paser .tsv files are supported.");
/// we mirror this in the constructor.</para>
/// </remarks>
public sealed class TSVReader : BuildParser
{
    private readonly BuildParser _inner;

    /// <summary>Returns true if <paramref name="path"/> ends with <c>.tsv</c> (case-insensitive).</summary>
    public static bool AcceptsExtension(string path) =>
        path.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Construct a TSVReader. Internally sniffs the header to pick a concrete variant.
    /// </summary>
    public TSVReader(BlibBuilder builder, string tsvName, ProgressIndicator? parentProgress)
        : base(builder, tsvName, parentProgress)
    {
        _inner = CreateInner(builder, tsvName, parentProgress)
                 ?? throw new BlibException(false,
                     "Did not find required columns. Only OpenSWATH result, OpenSWATH assay, "
                     + "AlphaPepDeep, and Paser .tsv files are supported.");
    }

    /// <summary>Returns true if the header in <paramref name="tsvName"/> can be dispatched.</summary>
    public static bool HasKnownHeader(string tsvName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tsvName);
        var headerColumns = ReadHeaderColumns(tsvName);
        return AlphaPepDeepReader.HasExpectedColumns(headerColumns)
            || PaserLibraryReader.HasExpectedColumns(headerColumns)
            || PaserResultReader.HasExpectedColumns(headerColumns)
            || OpenSwathResultReader.HasExpectedColumns(headerColumns)
            || OpenSwathAssayReader.HasExpectedColumns(headerColumns);
    }

    /// <inheritdoc/>
    public override bool ParseFile() => _inner.ParseFile();

    /// <inheritdoc/>
    public override IList<PsmScoreType> GetScoreTypes() => _inner.GetScoreTypes();

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
        }
        base.Dispose(disposing);
    }

    // --- Header sniffing -------------------------------------------------------------

    private static BuildParser? CreateInner(BlibBuilder builder, string tsvName, ProgressIndicator? parentProgress)
    {
        var headerColumns = ReadHeaderColumns(tsvName);

        // cpp parity: TSVReader.cpp:1051 — try AlphaPepDeep first, then Paser library, then
        // Paser result, then OpenSWATH result, then OpenSWATH assay. Order matters because
        // some headers overlap (e.g. AlphaPepDeep's unique col is NormalizedRetentionTimeAPD;
        // PaserLibrary has DecoyMobility / GeneName as differentiators).
        if (AlphaPepDeepReader.HasExpectedColumns(headerColumns))
            return new AlphaPepDeepReader(builder, tsvName, parentProgress);
        if (PaserLibraryReader.HasExpectedColumns(headerColumns))
            return new PaserLibraryReader(builder, tsvName, parentProgress);
        if (PaserResultReader.HasExpectedColumns(headerColumns))
            return new PaserResultReader(builder, tsvName, parentProgress);
        if (OpenSwathResultReader.HasExpectedColumns(headerColumns))
            return new OpenSwathResultReader(builder, tsvName, parentProgress);
        if (OpenSwathAssayReader.HasExpectedColumns(headerColumns))
            return new OpenSwathAssayReader(builder, tsvName, parentProgress);
        return null;
    }

    private static HashSet<string> ReadHeaderColumns(string tsvName)
    {
        using var reader = new StreamReader(tsvName);
        var line = ReadLfTerminatedLine(reader) ?? string.Empty;
        return new HashSet<string>(SplitTabs(line), StringComparer.Ordinal);
    }

    internal static string[] SplitTabs(string line)
    {
        if (line.Length > 0 && line[^1] == '\r')
            line = line.Substring(0, line.Length - 1);
        return line.Split('\t');
    }

    /// <summary>
    /// Read a line terminated only by <c>\n</c> (trailing <c>\r</c> stripped), mirroring
    /// <c>pwiz::util::getlinePortable</c>. <see cref="StreamReader.ReadLine"/> treats lone
    /// <c>\r</c> bytes as line terminators too, which would split mixed-line-ending TSV
    /// files (e.g. <c>openswath_test.tsv</c> has both <c>\r\n</c> and bare <c>\r</c>
    /// inside the same row), causing PSM specKey numbers to drift from cpp by 1 per
    /// "extra" line.
    /// </summary>
    internal static string? ReadLfTerminatedLine(StreamReader reader)
    {
        if (reader.EndOfStream) return null;
        var sb = new System.Text.StringBuilder();
        int ch;
        while ((ch = reader.Read()) != -1)
        {
            if (ch == '\n')
                break;
            sb.Append((char)ch);
        }
        if (ch == -1 && sb.Length == 0) return null;
        // strip trailing \r (matches getlinePortable's bal::trim_right_if).
        if (sb.Length > 0 && sb[sb.Length - 1] == '\r')
            sb.Length--;
        return sb.ToString();
    }

    // --- Shared sequence parsing -----------------------------------------------------

    /// <summary>
    /// Strip <c>(UNIMOD:N)</c> tokens from <paramref name="seq"/>, returning the bare
    /// amino-acid sequence and one <see cref="SeqMod"/> per token. cpp parity:
    /// TSVReader.cpp:1142.
    /// </summary>
    public static bool ParseSequence(string seq, out string unmodSeq, out List<SeqMod> mods)
    {
        ArgumentNullException.ThrowIfNull(seq);
        unmodSeq = string.Empty;
        mods = new List<SeqMod>();

        // cpp parity: uppercase + strip a single leading '.' for normalized input.
        var seqUpper = seq.ToUpperInvariant();
        if (seqUpper.Length > 0 && seqUpper[0] == '.')
            seqUpper = seqUpper.Substring(1);

        const string searchString = "(UNIMOD:";
        var working = seqUpper;
        int idx;
        while ((idx = working.IndexOf(searchString, StringComparison.Ordinal)) >= 0)
        {
            var j = idx + searchString.Length;
            var k = working.IndexOf(')', j);
            if (k < 0)
            {
                Verbosity.Error($"Invalid sequence '{seq}'");
                return false;
            }
            var idStr = working.Substring(j, k - j);
            if (!int.TryParse(idStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                Verbosity.Error($"Non-numeric modification ID in sequence '{seq}'");
                return false;
            }

            var cvid = (CVID)(id + (int)CVID.UNIMOD_unimod_root_node);
            var modInfo = Unimod.Modification(cvid);
            if (modInfo is null)
            {
                Verbosity.Error($"Unknown modification ID {id.ToString(CultureInfo.InvariantCulture)} in sequence '{seq}'");
                return false;
            }

            // Excise the `(UNIMOD:NN)` substring including both parens.
            working = working.Remove(idx, k - idx + 1);
            // cpp parity: TSVReader.cpp:1195 — SeqMod position is 1-based (max(1, idx)).
            mods.Add(new SeqMod(Math.Max(1, idx), modInfo.DeltaMonoisotopicMass));
        }

        unmodSeq = working;
        return true;
    }

    // ===== Common per-row line accumulator ==========================================

    /// <summary>
    /// Holds the parsed-but-untranslated values from one TSV row. Each variant declares
    /// which columns it cares about by mapping column name → setter on this type.
    /// </summary>
    /// <remarks>cpp parity: TSVReader.h:55 <c>class TSVLine</c>.</remarks>
    internal sealed class TSVLine
    {
        public string Filename = string.Empty;
        public double Rt;                       // minutes
        public string Sequence = string.Empty;
        public string StrippedSequence = string.Empty;
        public int Charge;
        public int FragmentCharge;
        public string FragmentLossType = string.Empty;
        public double Mz;
        public string ProteinName = string.Empty;
        public bool Decoy;
        public double LeftWidth;
        public double RightWidth;
        public double Ce;
        public double IonMobility;
        public IonMobilityType IonMobilityUnits;
        public double Ccs;
        public string PeakArea = string.Empty;
        public string FragmentAnnotation = string.Empty;
        public int FragmentSeriesNumber;
        public double Score;

        // Setter functions — cpp parity: TSVLine::insertXxx static methods.
        public static void InsertFilename(TSVLine l, string v) => l.Filename = v;
        public static void InsertRtSeconds(TSVLine l, string v) => l.Rt = ParseDoubleOrZero(v) / 60.0;
        public static void InsertRtMinutes(TSVLine l, string v) => l.Rt = ParseDoubleOrZero(v);
        public static void InsertRtNormalized(TSVLine l, string v) => l.Rt = ParseDoubleOrZero(v) / 60.0;
        public static void InsertRtStartMinutes(TSVLine l, string v) => l.LeftWidth = ParseDoubleOrZero(v);
        public static void InsertRtEndMinutes(TSVLine l, string v) => l.RightWidth = ParseDoubleOrZero(v);
        public static void InsertSequence(TSVLine l, string v) => l.Sequence = v;
        public static void InsertStrippedSequence(TSVLine l, string v) => l.StrippedSequence = v;
        public static void InsertFragmentLossType(TSVLine l, string v) => l.FragmentLossType = v;
        public static void InsertFragmentCharge(TSVLine l, string v) => l.FragmentCharge = ParseIntOrZero(v);
        public static void InsertCharge(TSVLine l, string v) => l.Charge = ParseIntOrZero(v);
        public static void InsertIonMobilityUnits(TSVLine l, string v) =>
            l.IonMobilityUnits = (IonMobilityType)ParseIntOrZero(v);
        public static void InsertMz(TSVLine l, string v) => l.Mz = ParseDoubleOrZero(v);
        public static void InsertProteinName(TSVLine l, string v) => l.ProteinName = v;
        public static void InsertDecoy(TSVLine l, string v) => l.Decoy = v == "1";
        public static void InsertLeftWidthSeconds(TSVLine l, string v) => l.LeftWidth = ParseDoubleOrZero(v) / 60.0;
        public static void InsertRightWidthSeconds(TSVLine l, string v) => l.RightWidth = ParseDoubleOrZero(v) / 60.0;
        public static void InsertProductMz(TSVLine l, string v) => l.LeftWidth = ParseDoubleOrZero(v);
        public static void InsertPeakArea(TSVLine l, string v) => l.PeakArea = v;
        public static void InsertFragmentAnnotation(TSVLine l, string v) => l.FragmentAnnotation = v;
        public static void InsertFragmentSeriesNumber(TSVLine l, string v) => l.FragmentSeriesNumber = ParseIntOrZero(v);
        public static void InsertScore(TSVLine l, string v) => l.Score = ParseDoubleOrZero(v);
        public static void InsertCE(TSVLine l, string v) => l.Ce = ParseDoubleOrZero(v);
        public static void InsertIonMobility(TSVLine l, string v) => l.IonMobility = ParseDoubleOrZero(v);
        public static void InsertCollisionalCrossSection(TSVLine l, string v) => l.Ccs = ParseDoubleOrZero(v);
        public static void Ignore(TSVLine l, string v) { /* no-op */ }

        private static int ParseIntOrZero(string v) =>
            string.IsNullOrEmpty(v) ? 0 : int.Parse(v, NumberStyles.Integer, CultureInfo.InvariantCulture);
        private static double ParseDoubleOrZero(string v) =>
            string.IsNullOrEmpty(v) ? 0 : double.Parse(v, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    /// <summary>cpp parity: TSVReader.h:159 — column-name + position + setter triple.</summary>
    internal sealed class TSVColumnTranslator
    {
        public string Name { get; }
        public int Position { get; set; }
        public Action<TSVLine, string> Inserter { get; }

        public TSVColumnTranslator(string name, int position, Action<TSVLine, string> inserter)
        {
            Name = name;
            Position = position;
            Inserter = inserter;
        }
    }

    // ===== TSVPSM ===================================================================

    /// <summary>
    /// Extends <see cref="PSM"/> with the per-spectrum bits TSV rows carry. cpp parity:
    /// TSVReader.h:31 <c>struct TSVPSM</c>.
    /// </summary>
    internal sealed class TSVPSM : PSM
    {
        public double Rt;
        public double Mz;
        public double LeftWidth;
        public double RightWidth;
        public double Ce;
        public List<double> Mzs { get; } = new();
        public List<double> Intensities { get; } = new();
    }

    // ===== Concrete variants ========================================================

    /// <summary>
    /// Shared scaffolding for every concrete TSV reader subclass: column-set bookkeeping,
    /// the file open / header parse, the row-by-row dispatch loop, and the unmodified
    /// `(UNIMOD:N)` sequence parser. cpp parity: TSVReader.h:166 <c>class TSVReader</c>.
    /// </summary>
    internal abstract class TSVReaderBase : BuildParser
    {
        protected readonly string TsvName;
        protected readonly double ScoreThreshold;
        protected int LineNum;

        // cpp parity: TSVReader.h:199 — std::map<string, vector<TSVPSM*>>.
        protected readonly SortedDictionary<string, List<TSVPSM>> FileMap =
            new(StringComparer.Ordinal);

        protected readonly List<TSVColumnTranslator> TargetColumns = new();
        protected readonly List<TSVColumnTranslator> OptionalColumns = new();

        protected TSVReaderBase(BlibBuilder builder, string tsvName, ProgressIndicator? parentProgress)
            : base(builder, tsvName, parentProgress)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tsvName);
            TsvName = tsvName;
            ScoreThreshold = GetScoreThreshold(BuildInput.GenericQValueInput);
            LineNum = 1;

            // cpp parity: TSVReader.cpp:1018 — set spec-file to the tsv (no existence check;
            // the spec reader handed back from SpecReader pulls peaks straight off TSVPSM).
            SetSpecFileName(tsvName, checkFile: false);
            SpecReader = new TSVSpecFileReader();
        }

        /// <summary>
        /// Read the file header from disk and resolve the position of each column we care about.
        /// </summary>
        protected void OpenAndParseHeader()
        {
            using var reader = new StreamReader(TsvName);
            var line = ReadLfTerminatedLine(reader);
            if (line is null) return;

            // cpp parity: TSVReader.cpp:1070 — split the header on tab, look up each column
            // against the target / optional column tables.
            var tokens = SplitTabs(line);
            int colNumber = 0;
            foreach (var token in tokens)
            {
                bool found = false;
                foreach (var tc in TargetColumns)
                {
                    if (string.Equals(token, tc.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        tc.Position = colNumber;
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    foreach (var oc in OptionalColumns)
                    {
                        if (string.Equals(token, oc.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            oc.Position = colNumber;
                            // cpp parity: TSVReader.cpp:1084 — copy matched optionals into the
                            // target list so the per-row loop knows about them.
                            TargetColumns.Add(oc);
                            break;
                        }
                    }
                }
                colNumber++;
            }

            // cpp parity: TSVReader.cpp:1093 — sort target columns by column number so the
            // per-row loop fetches them in order.
            TargetColumns.Sort((a, b) => a.Position.CompareTo(b.Position));
        }

        /// <summary>
        /// Walk every data row of the TSV, dispatching each cell through the column setters
        /// then calling <see cref="StoreLine"/>. cpp parity: TSVReader.cpp:1096 <c>collectPsms</c>.
        /// </summary>
        protected void CollectPsms(Dictionary<string, Protein> proteins)
        {
            using var reader = new StreamReader(TsvName);
            ReadLfTerminatedLine(reader); // skip header

            string? line;
            while ((line = ReadLfTerminatedLine(reader)) != null)
            {
                LineNum++;

                var entry = new TSVLine();
                int col = 0;
                int targetIdx = 0;

                try
                {
                    foreach (var token in SplitTabs(line))
                    {
                        if (targetIdx < TargetColumns.Count
                            && col == TargetColumns[targetIdx].Position)
                        {
                            TargetColumns[targetIdx].Inserter(entry, token);
                            targetIdx++;
                            if (targetIdx == TargetColumns.Count) break;
                        }
                        col++;
                    }
                    if (targetIdx != TargetColumns.Count)
                    {
                        Verbosity.Warn($"Skipping invalid line {LineNum.ToString(CultureInfo.InvariantCulture)}");
                        continue;
                    }
                }
                catch (BlibException e)
                {
                    throw new BlibException(false, string.Format(CultureInfo.InvariantCulture,
                        "{0} caught at line {1}, column {2}",
                        e.Message, LineNum, col + 1));
                }
                catch (Exception e)
                {
                    throw new BlibException(false, string.Format(CultureInfo.InvariantCulture,
                        "{0} caught at line {1}, column {2}",
                        e.Message, LineNum, col + 1));
                }

                StoreLine(entry, proteins);
            }
        }

        protected abstract void StoreLine(TSVLine line, Dictionary<string, Protein> proteins);

        /// <summary>Helper: split a protein-name string and attach each Protein to <paramref name="psm"/>.</summary>
        protected static void AttachProteins(TSVPSM psm, string proteinNamesRaw, char delimiter,
            Dictionary<string, Protein> proteins, bool skipFirst)
        {
            if (string.IsNullOrEmpty(proteinNamesRaw)) return;
            var parts = proteinNamesRaw.Split(delimiter);
            // cpp parity: TSVReader.cpp:155 — `parts.begin() + 1` skips the first token
            // (matches cpp OpenSwath/AlphaPepDeep/PaserLibrary/PaserResult logic).
            int start = skipFirst && parts.Length > 1 ? 1 : 0;
            for (int i = start; i < parts.Length; i++)
            {
                var name = parts[i];
                if (!proteins.TryGetValue(name, out var prot))
                {
                    prot = new Protein(name);
                    proteins[name] = prot;
                }
                psm.Proteins.Add(prot);
            }
        }

        /// <summary>Helper: drain <see cref="FileMap"/>, flushing each group with the supplied score type and workflow.</summary>
        protected void FlushFileMap(PsmScoreType scoreType, WorkflowType workflow)
        {
            Verbosity.Debug("Building tables");
            InitSpecFileProgress(FileMap.Count);
            foreach (var kvp in FileMap)
            {
                Psms.Clear();
                foreach (var p in kvp.Value)
                    Psms.Add(p);
                SetSpecFileName(kvp.Key, checkFile: false);
                BuildTables(scoreType, kvp.Key, showSpecProgress: false, workflow);
            }
        }
    }

    // --- OpenSwathResultReader ------------------------------------------------------

    /// <summary>cpp parity: TSVReader.cpp:38 — OpenSWATH result TSV.</summary>
    internal sealed class OpenSwathResultReader : TSVReaderBase
    {
        private static readonly string[] RequiredColumnNames =
        {
            "filename", "RT", "FullPeptideName", "Charge", "m/z",
            "decoy", "aggr_Peak_Area", "aggr_Fragment_Annotation", "m_score",
        };

        public OpenSwathResultReader(BlibBuilder builder, string tsvName, ProgressIndicator? parentProgress)
            : base(builder, tsvName, parentProgress)
        {
            // cpp parity: TSVReader.cpp:41 — required columns.
            TargetColumns.Add(new TSVColumnTranslator("filename", -1, TSVLine.InsertFilename));
            TargetColumns.Add(new TSVColumnTranslator("RT", -1, TSVLine.InsertRtSeconds));
            TargetColumns.Add(new TSVColumnTranslator("FullPeptideName", -1, TSVLine.InsertSequence));
            TargetColumns.Add(new TSVColumnTranslator("Charge", -1, TSVLine.InsertCharge));
            TargetColumns.Add(new TSVColumnTranslator("m/z", -1, TSVLine.InsertMz));
            TargetColumns.Add(new TSVColumnTranslator("decoy", -1, TSVLine.InsertDecoy));
            TargetColumns.Add(new TSVColumnTranslator("aggr_Peak_Area", -1, TSVLine.InsertPeakArea));
            TargetColumns.Add(new TSVColumnTranslator("aggr_Fragment_Annotation", -1, TSVLine.InsertFragmentAnnotation));
            TargetColumns.Add(new TSVColumnTranslator("m_score", -1, TSVLine.InsertScore));

            // cpp parity: TSVReader.cpp:54 — optional.
            OptionalColumns.Add(new TSVColumnTranslator("ProteinName", -1, TSVLine.InsertProteinName));
            OptionalColumns.Add(new TSVColumnTranslator("leftWidth", -1, TSVLine.InsertLeftWidthSeconds));
            OptionalColumns.Add(new TSVColumnTranslator("rightWidth", -1, TSVLine.InsertRightWidthSeconds));

            OpenAndParseHeader();

            // cpp parity: TSVReader.cpp:75 — verify all required columns matched. If m_score
            // is the only missing one and the score threshold is wide-open (>=1), drop it
            // gracefully; otherwise throw a parity error message.
            for (int i = TargetColumns.Count - 1; i >= 0; i--)
            {
                if (TargetColumns[i].Position < 0)
                {
                    if (string.Equals(TargetColumns[i].Name, "m_score", StringComparison.OrdinalIgnoreCase))
                    {
                        if (ScoreThreshold < 1)
                        {
                            throw new BlibException(false,
                                $"Did not find required column '{TargetColumns[i].Name}'. You may set the "
                                + "cut-off score to 0 to force building the library without scores.");
                        }
                        TargetColumns.RemoveAt(i);
                        continue;
                    }
                    throw new BlibException(false,
                        $"Did not find required column '{TargetColumns[i].Name}'. Only OpenSWATH .tsv files are supported.");
                }
            }
        }

        public static bool HasExpectedColumns(HashSet<string> headerColumns)
        {
            foreach (var required in RequiredColumnNames)
                if (!headerColumns.Contains(required))
                    return false;
            return true;
        }

        public override bool ParseFile()
        {
            Verbosity.Debug("Collecting PSMs");
            var proteins = new Dictionary<string, Protein>(StringComparer.Ordinal);
            CollectPsms(proteins);
            FlushFileMap(PsmScoreType.GenericQValue, WorkflowType.Dia);
            return true;
        }

        public override IList<PsmScoreType> GetScoreTypes() => new[] { PsmScoreType.GenericQValue };

        protected override void StoreLine(TSVLine line, Dictionary<string, Protein> proteins)
        {
            if (line.Decoy)
            {
                Verbosity.Comment(VerbosityLevel.Detail,
                    $"Not saving decoy PSM (line {LineNum.ToString(CultureInfo.InvariantCulture)})");
                return;
            }
            if (line.Score > ScoreThreshold)
            {
                Verbosity.Comment(VerbosityLevel.Detail,
                    $"Not saving PSM with score {line.Score.ToString(CultureInfo.InvariantCulture)} "
                    + $"(line {LineNum.ToString(CultureInfo.InvariantCulture)})");
                FilteredOutPsmCount++;
                // cpp parity: TSVReader.cpp:139 — ensure the file shows up in the FileMap even
                // if no PSM passes (so BuildTables emits the empty-spec-file warning).
                if (!FileMap.ContainsKey(line.Filename))
                    FileMap[line.Filename] = new List<TSVPSM>();
                return;
            }

            var psm = new TSVPSM
            {
                SpecKey = LineNum,
                Rt = line.Rt,
            };
            if (!TSVReader.ParseSequence(line.Sequence, out var unmod, out var mods))
                return;
            psm.UnmodSeq = unmod;
            foreach (var m in mods) psm.Mods.Add(m);
            psm.Charge = line.Charge;
            psm.Mz = line.Mz;

            // cpp parity: TSVReader.cpp:152 — split protein on '/' and skip the first token.
            AttachProteins(psm, line.ProteinName, '/', proteins, skipFirst: true);

            psm.LeftWidth = line.LeftWidth;
            psm.RightWidth = line.RightWidth;
            psm.Score = line.Score;
            if (!ParsePeaks(line.PeakArea, line.FragmentAnnotation, psm.Mzs, psm.Intensities))
                return;

            if (!FileMap.TryGetValue(line.Filename, out var list))
            {
                list = new List<TSVPSM>();
                FileMap[line.Filename] = list;
            }
            list.Add(psm);
        }

        // cpp parity: TSVReader.cpp:185 — parse `aggr_Peak_Area` (semicolon-separated areas)
        // + `aggr_Fragment_Annotation` (semicolon-separated `<n>_<type><ionnum>_<charge>_<seq>_<info>`)
        // into mzs / intensities.
        private bool ParsePeaks(string peakArea, string fragmentAnnotation,
            List<double> mz, List<double> intensity)
        {
            mz.Clear();
            intensity.Clear();

            var areaParts = peakArea.Split(';');
            foreach (var s in areaParts)
            {
                if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                {
                    Verbosity.Error($"Invalid peak area '{s}' on line {LineNum.ToString(CultureInfo.InvariantCulture)}");
                    return false;
                }
                intensity.Add(v);
            }
            int numPeaks = areaParts.Length;

            var annParts = fragmentAnnotation.Split(';');
            if (annParts.Length != numPeaks)
            {
                Verbosity.Error(string.Format(CultureInfo.InvariantCulture,
                    "Number of peak areas ({0}) did not match number of fragment annotations ({1}) on line {2}",
                    numPeaks, annParts.Length, LineNum));
                return false;
            }
            foreach (var ann in annParts)
            {
                var parts2 = ann.Split('_');
                if (parts2.Length != 5 || parts2[1].Length < 2)
                {
                    Verbosity.Error($"Unexpected format for fragment annotations on line {LineNum.ToString(CultureInfo.InvariantCulture)}: {ann}");
                    return false;
                }
                if (!TSVReader.ParseSequence(parts2[3], out var fragSeq, out var fragMods))
                {
                    Verbosity.Error($"Unexpected format for fragment annotations on line {LineNum.ToString(CultureInfo.InvariantCulture)}: {ann}");
                    return false;
                }
                if (!int.TryParse(parts2[1].AsSpan(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var ionNum)
                    || !int.TryParse(parts2[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ionCharge))
                {
                    Verbosity.Error($"Unexpected format for fragment annotations on line {LineNum.ToString(CultureInfo.InvariantCulture)}: {ann}");
                    return false;
                }
                if (!CalcIonMz(fragSeq, fragMods, parts2[1][0], ionNum, ionCharge, out var ionMz))
                    return false;
                mz.Add(ionMz);
            }
            return true;
        }

        // cpp parity: TSVReader.cpp:252 — calc theoretical b/y ion m/z.
        private bool CalcIonMz(string seq, List<SeqMod> mods, char ionType, int ionNum, int ionCharge, out double ionMz)
        {
            ionMz = ionCharge * AminoAcidMasses.ProtonMass;
            if (ionNum < 1 || ionNum > seq.Length)
            {
                Verbosity.Error(string.Format(CultureInfo.InvariantCulture,
                    "Invalid ion number {0} on line {1} (must be between 1 and {2} for peptide '{3}')",
                    ionNum, LineNum, seq.Length, seq));
                return false;
            }

            int seqStart;
            int seqEnd;
            switch (ionType)
            {
                case 'b':
                    seqStart = 0;
                    seqEnd = ionNum;
                    break;
                case 'y':
                    seqStart = seq.Length - ionNum;
                    seqEnd = seq.Length;
                    // cpp parity: TSVReader.cpp:280 — y ions get H2O on top.
                    ionMz += 2 * _hMass + _oMass;
                    break;
                default:
                    Verbosity.Error($"Invalid ion type '{ionType}' on line {LineNum.ToString(CultureInfo.InvariantCulture)}");
                    return false;
            }

            for (int i = seqStart; i < seqEnd; i++)
            {
                var c = seq[i];
                if (c < 'A' || c > 'Z')
                {
                    Verbosity.Error(string.Format(CultureInfo.InvariantCulture,
                        "Invalid character '{0}' in sequence '{1}' on line {2}", c, seq, LineNum));
                    return false;
                }
                ionMz += _aaMasses[c];
            }
            foreach (var mod in mods)
            {
                int modIndex = mod.Position - 1;
                if (seqStart <= modIndex && modIndex < seqEnd)
                    ionMz += mod.DeltaMass;
            }
            ionMz /= ionCharge;
            return true;
        }

        // Monoisotopic residue + H / O masses used by CalcIonMz. cpp parity:
        // TSVReader.cpp:1024 initialises the mass table via AminoAcidMasses.initializeMass(masses_, 1)
        // and references 'h' / 'o' indices. The same call works in C#.
        private static readonly double[] _aaMasses = AminoAcidMasses.BuildMassTable(monoisotopic: true);
        private static readonly double _hMass = _aaMasses['h'];
        private static readonly double _oMass = _aaMasses['o'];
    }

    // --- OpenSwathAssayReader -------------------------------------------------------

    /// <summary>cpp parity: TSVReader.cpp:462 — OpenSWATH assay (library) TSV.</summary>
    internal sealed class OpenSwathAssayReader : TSVReaderBase
    {
        private static readonly string[] RequiredColumnNames =
        {
            "NormalizedRetentionTime", "ModifiedPeptideSequence", "PrecursorCharge", "PrecursorMz",
            "Decoy", "ProductMz", "LibraryIntensity", "FragmentType", "FragmentSeriesNumber",
        };

        private TSVPSM _currentPsm = new();
        private string _currentSequence = string.Empty;

        public OpenSwathAssayReader(BlibBuilder builder, string tsvName, ProgressIndicator? parentProgress)
            : base(builder, tsvName, parentProgress)
        {
            TargetColumns.Add(new TSVColumnTranslator("NormalizedRetentionTime", -1, TSVLine.InsertRtNormalized));
            TargetColumns.Add(new TSVColumnTranslator("ModifiedPeptideSequence", -1, TSVLine.InsertSequence));
            TargetColumns.Add(new TSVColumnTranslator("PrecursorCharge", -1, TSVLine.InsertCharge));
            TargetColumns.Add(new TSVColumnTranslator("PrecursorMz", -1, TSVLine.InsertMz));
            TargetColumns.Add(new TSVColumnTranslator("Decoy", -1, TSVLine.InsertDecoy));
            TargetColumns.Add(new TSVColumnTranslator("ProductMz", -1, TSVLine.InsertProductMz));
            TargetColumns.Add(new TSVColumnTranslator("LibraryIntensity", -1, TSVLine.InsertPeakArea));
            TargetColumns.Add(new TSVColumnTranslator("FragmentType", -1, TSVLine.InsertFragmentAnnotation));
            TargetColumns.Add(new TSVColumnTranslator("FragmentSeriesNumber", -1, TSVLine.InsertFragmentSeriesNumber));

            OptionalColumns.Add(new TSVColumnTranslator("ProteinId", -1, TSVLine.InsertProteinName));
            OptionalColumns.Add(new TSVColumnTranslator("CollisionEnergy", -1, TSVLine.InsertCE));
            OptionalColumns.Add(new TSVColumnTranslator("IonMobility", -1, TSVLine.InsertIonMobility));
            OptionalColumns.Add(new TSVColumnTranslator("PrecursorIonMobility", -1, TSVLine.InsertIonMobility));

            OpenAndParseHeader();
        }

        public static bool HasExpectedColumns(HashSet<string> headerColumns)
        {
            foreach (var required in RequiredColumnNames)
                if (!headerColumns.Contains(required))
                    return false;
            return true;
        }

        public override bool ParseFile()
        {
            Verbosity.Debug("Collecting PSMs");
            var proteins = new Dictionary<string, Protein>(StringComparer.Ordinal);
            CollectPsms(proteins);

            if (!string.IsNullOrEmpty(_currentSequence))
            {
                if (!FileMap.TryGetValue(TsvName, out var list))
                {
                    list = new List<TSVPSM>();
                    FileMap[TsvName] = list;
                }
                list.Add(_currentPsm);
                _currentPsm = new TSVPSM();
                _currentSequence = string.Empty;
            }

            FlushFileMap(PsmScoreType.UnknownScoreType, WorkflowType.Dia);
            return true;
        }

        public override IList<PsmScoreType> GetScoreTypes() => new[] { PsmScoreType.UnknownScoreType };

        protected override void StoreLine(TSVLine line, Dictionary<string, Protein> proteins)
        {
            if (line.Decoy) return;

            if (line.Mz != _currentPsm.Mz || line.Sequence != _currentSequence)
            {
                if (!string.IsNullOrEmpty(_currentSequence))
                {
                    if (!FileMap.TryGetValue(TsvName, out var list))
                    {
                        list = new List<TSVPSM>();
                        FileMap[TsvName] = list;
                    }
                    list.Add(_currentPsm);
                    _currentPsm = new TSVPSM();
                }
                _currentSequence = line.Sequence;

                _currentPsm.SpecKey = LineNum;
                _currentPsm.Rt = line.Rt;
                if (!TSVReader.ParseSequence(line.Sequence, out var unmod, out var mods))
                    return;
                _currentPsm.UnmodSeq = unmod;
                foreach (var m in mods) _currentPsm.Mods.Add(m);
                _currentPsm.Charge = line.Charge;
                _currentPsm.Mz = line.Mz;

                // cpp parity: TSVReader.cpp:577 — split on ';' and skip the first token.
                AttachProteins(_currentPsm, line.ProteinName, ';', proteins, skipFirst: true);

                if (line.IonMobility > 0)
                {
                    _currentPsm.IonMobility = line.IonMobility;
                    _currentPsm.IonMobilityType = IonMobilityType.DriftTimeMsec;
                }
            }

            _currentPsm.Mzs.Add(line.LeftWidth);   // ProductMz was routed here
            _currentPsm.Intensities.Add(double.Parse(line.PeakArea,
                NumberStyles.Float, CultureInfo.InvariantCulture));
        }
    }

    // --- AlphaPepDeepReader ---------------------------------------------------------

    /// <summary>cpp parity: TSVReader.cpp:311 — AlphaPepDeep TSV.</summary>
    internal sealed class AlphaPepDeepReader : TSVReaderBase
    {
        private static readonly string[] RequiredColumnNames =
        {
            "StrippedPeptide", "FragmentCharge", "FragmentLossType", "NormalizedRetentionTimeAPD",
            "ModifiedPeptideSequence", "PrecursorCharge", "PrecursorMz", "Decoy",
            "ProductMz", "LibraryIntensity", "FragmentType", "FragmentSeriesNumber",
        };

        private TSVPSM _currentPsm = new();
        private string _currentSequence = string.Empty;

        public AlphaPepDeepReader(BlibBuilder builder, string tsvName, ProgressIndicator? parentProgress)
            : base(builder, tsvName, parentProgress)
        {
            TargetColumns.Add(new TSVColumnTranslator("StrippedPeptide", -1, TSVLine.InsertStrippedSequence));
            TargetColumns.Add(new TSVColumnTranslator("FragmentCharge", -1, TSVLine.InsertFragmentCharge));
            TargetColumns.Add(new TSVColumnTranslator("FragmentLossType", -1, TSVLine.InsertFragmentLossType));
            TargetColumns.Add(new TSVColumnTranslator("NormalizedRetentionTimeAPD", -1, TSVLine.InsertRtMinutes));
            TargetColumns.Add(new TSVColumnTranslator("ModifiedPeptideSequence", -1, TSVLine.InsertSequence));
            TargetColumns.Add(new TSVColumnTranslator("PrecursorCharge", -1, TSVLine.InsertCharge));
            TargetColumns.Add(new TSVColumnTranslator("PrecursorMz", -1, TSVLine.InsertMz));
            TargetColumns.Add(new TSVColumnTranslator("Decoy", -1, TSVLine.InsertDecoy));
            TargetColumns.Add(new TSVColumnTranslator("ProductMz", -1, TSVLine.InsertProductMz));
            TargetColumns.Add(new TSVColumnTranslator("LibraryIntensity", -1, TSVLine.InsertPeakArea));
            TargetColumns.Add(new TSVColumnTranslator("FragmentType", -1, TSVLine.InsertFragmentAnnotation));
            TargetColumns.Add(new TSVColumnTranslator("FragmentSeriesNumber", -1, TSVLine.InsertFragmentSeriesNumber));

            OptionalColumns.Add(new TSVColumnTranslator("ProteinId", -1, TSVLine.InsertProteinName));
            OptionalColumns.Add(new TSVColumnTranslator("IonMobility", -1, TSVLine.InsertIonMobility));
            OptionalColumns.Add(new TSVColumnTranslator("IonMobilityUnits", -1, TSVLine.InsertIonMobilityUnits));
            OptionalColumns.Add(new TSVColumnTranslator("CollisionalCrossSection", -1, TSVLine.InsertCollisionalCrossSection));
            OptionalColumns.Add(new TSVColumnTranslator("CollisionEnergy", -1, TSVLine.InsertCE));
            OptionalColumns.Add(new TSVColumnTranslator("PrecursorIonMobility", -1, TSVLine.InsertIonMobility));

            OpenAndParseHeader();
        }

        public static bool HasExpectedColumns(HashSet<string> headerColumns)
        {
            foreach (var required in RequiredColumnNames)
                if (!headerColumns.Contains(required))
                    return false;
            return true;
        }

        public override bool ParseFile()
        {
            Verbosity.Debug("Collecting PSMs");
            var proteins = new Dictionary<string, Protein>(StringComparer.Ordinal);
            CollectPsms(proteins);

            if (!string.IsNullOrEmpty(_currentSequence))
            {
                if (!FileMap.TryGetValue(TsvName, out var list))
                {
                    list = new List<TSVPSM>();
                    FileMap[TsvName] = list;
                }
                list.Add(_currentPsm);
                _currentPsm = new TSVPSM();
                _currentSequence = string.Empty;
            }

            FlushFileMap(PsmScoreType.UnknownScoreType, WorkflowType.Dda);
            return true;
        }

        public override IList<PsmScoreType> GetScoreTypes() => new[] { PsmScoreType.UnknownScoreType };

        protected override void StoreLine(TSVLine line, Dictionary<string, Protein> proteins)
        {
            if (line.Decoy) return;

            if (line.Mz != _currentPsm.Mz || line.Sequence != _currentSequence)
            {
                if (!string.IsNullOrEmpty(_currentSequence))
                {
                    if (!FileMap.TryGetValue(TsvName, out var list))
                    {
                        list = new List<TSVPSM>();
                        FileMap[TsvName] = list;
                    }
                    list.Add(_currentPsm);
                    _currentPsm = new TSVPSM();
                }
                _currentSequence = line.Sequence;

                _currentPsm.SpecKey = LineNum;
                _currentPsm.Rt = line.Rt;
                if (!TSVReader.ParseSequence(line.Sequence, out var unmod, out var mods))
                    return;
                _currentPsm.UnmodSeq = unmod;
                foreach (var m in mods) _currentPsm.Mods.Add(m);
                _currentPsm.Charge = line.Charge;
                _currentPsm.Mz = line.Mz;

                AttachProteins(_currentPsm, line.ProteinName, ';', proteins, skipFirst: true);

                if (line.IonMobility > 0)
                {
                    _currentPsm.IonMobility = line.IonMobility;
                    _currentPsm.IonMobilityType = line.IonMobilityUnits;
                    _currentPsm.Ccs = line.Ccs;
                }
                else if (line.Ccs > 0)
                {
                    _currentPsm.Ccs = line.Ccs;
                }
            }

            _currentPsm.Mzs.Add(line.LeftWidth);
            _currentPsm.Intensities.Add(double.Parse(line.PeakArea,
                NumberStyles.Float, CultureInfo.InvariantCulture));
        }
    }

    // --- PaserLibraryReader / PaserResultReader -------------------------------------

    /// <summary>cpp parity: TSVReader.cpp:605 — Bruker Paser library TSV.</summary>
    internal sealed class PaserLibraryReader : TSVReaderBase
    {
        // cpp parity: differentiator columns vs OpenSwathAssay: DecoyMobility + GeneName.
        private static readonly string[] RequiredColumnNames =
        {
            "NormalizedRetentionTime", "ModifiedPeptideSequence", "PrecursorCharge", "PrecursorMz",
            "DecoyMobility", "ProductMz", "LibraryIntensity", "FragmentType", "FragmentSeriesNumber",
            "GeneName",
        };

        private TSVPSM? _currentPsm = new();
        private string _currentSequence = string.Empty;
        private List<PSM>? _currentResultPsms;
        private readonly Dictionary<string, List<PSM>>? _resultPsmMap;

        public PaserLibraryReader(BlibBuilder builder, string tsvName, ProgressIndicator? parentProgress,
            Dictionary<string, List<PSM>>? resultPsmMap = null)
            : base(builder, tsvName, parentProgress)
        {
            _resultPsmMap = resultPsmMap;

            TargetColumns.Add(new TSVColumnTranslator("NormalizedRetentionTime", -1, TSVLine.InsertRtMinutes));
            TargetColumns.Add(new TSVColumnTranslator("ModifiedPeptideSequence", -1, TSVLine.InsertSequence));
            TargetColumns.Add(new TSVColumnTranslator("PrecursorCharge", -1, TSVLine.InsertCharge));
            TargetColumns.Add(new TSVColumnTranslator("PrecursorMz", -1, TSVLine.InsertMz));
            TargetColumns.Add(new TSVColumnTranslator("DecoyMobility", -1, TSVLine.Ignore));
            TargetColumns.Add(new TSVColumnTranslator("ProductMz", -1, TSVLine.InsertProductMz));
            TargetColumns.Add(new TSVColumnTranslator("LibraryIntensity", -1, TSVLine.InsertPeakArea));
            TargetColumns.Add(new TSVColumnTranslator("FragmentType", -1, TSVLine.InsertFragmentAnnotation));
            TargetColumns.Add(new TSVColumnTranslator("FragmentSeriesNumber", -1, TSVLine.InsertFragmentSeriesNumber));
            TargetColumns.Add(new TSVColumnTranslator("GeneName", -1, TSVLine.Ignore));

            OptionalColumns.Add(new TSVColumnTranslator("ProteinId", -1, TSVLine.InsertProteinName));
            OptionalColumns.Add(new TSVColumnTranslator("IonMobility", -1, TSVLine.InsertIonMobility));
            OptionalColumns.Add(new TSVColumnTranslator("PrecursorIonMobility", -1, TSVLine.InsertIonMobility));

            OpenAndParseHeader();
        }

        public static bool HasExpectedColumns(HashSet<string> headerColumns)
        {
            foreach (var required in RequiredColumnNames)
                if (!headerColumns.Contains(required))
                    return false;
            return true;
        }

        public override bool ParseFile()
        {
            if (_resultPsmMap is null)
                Verbosity.Debug("Collecting PSMs");

            var proteins = new Dictionary<string, Protein>(StringComparer.Ordinal);
            CollectPsms(proteins);

            if (_resultPsmMap is null && !string.IsNullOrEmpty(_currentSequence) && _currentPsm is not null)
            {
                if (!FileMap.TryGetValue(TsvName, out var list))
                {
                    list = new List<TSVPSM>();
                    FileMap[TsvName] = list;
                }
                list.Add(_currentPsm);
                _currentPsm = null;
            }
            else if (_resultPsmMap is not null)
            {
                // cpp parity: TSVReader.cpp:690 — early return when called by PaserResultReader.
                return true;
            }

            FlushFileMap(PsmScoreType.UnknownScoreType, WorkflowType.Dda);
            return true;
        }

        public override IList<PsmScoreType> GetScoreTypes() => new[] { PsmScoreType.UnknownScoreType };

        protected override void StoreLine(TSVLine line, Dictionary<string, Protein> proteins)
        {
            if (line.Decoy) return;

            if (_resultPsmMap is not null)
            {
                if (_currentPsm is null || line.Mz != _currentPsm.Mz || line.Sequence != _currentSequence)
                {
                    var key = line.Sequence + line.Charge.ToString(CultureInfo.InvariantCulture);
                    if (!_resultPsmMap.TryGetValue(key, out var found))
                    {
                        Verbosity.Comment(VerbosityLevel.Detail,
                            $"No result found for library entry {key}");
                        return;
                    }
                    _currentResultPsms = found;
                }

                if (_currentResultPsms is null) return;
                foreach (var p in _currentResultPsms)
                {
                    if (p is TSVPSM tp)
                    {
                        tp.Mzs.Add(line.LeftWidth);
                        tp.Intensities.Add(double.Parse(line.PeakArea,
                            NumberStyles.Float, CultureInfo.InvariantCulture));
                    }
                }
                return;
            }

            if (_currentPsm is null)
                _currentPsm = new TSVPSM();

            if (line.Mz != _currentPsm.Mz || line.Sequence != _currentSequence)
            {
                if (!string.IsNullOrEmpty(_currentSequence))
                {
                    if (!FileMap.TryGetValue(TsvName, out var list))
                    {
                        list = new List<TSVPSM>();
                        FileMap[TsvName] = list;
                    }
                    list.Add(_currentPsm);
                    _currentPsm = new TSVPSM();
                }
                _currentSequence = line.Sequence;

                _currentPsm.SpecKey = LineNum;
                _currentPsm.Rt = line.Rt;
                if (!TSVReader.ParseSequence(line.Sequence, out var unmod, out var mods))
                    return;
                _currentPsm.UnmodSeq = unmod;
                foreach (var m in mods) _currentPsm.Mods.Add(m);
                _currentPsm.Charge = line.Charge;
                _currentPsm.Mz = line.Mz;

                AttachProteins(_currentPsm, line.ProteinName, ';', proteins, skipFirst: true);

                if (line.IonMobility > 0)
                {
                    _currentPsm.IonMobility = line.IonMobility;
                    _currentPsm.IonMobilityType = IonMobilityType.InverseReducedVsecPerCm2;
                }
            }

            _currentPsm.Mzs.Add(line.LeftWidth);
            _currentPsm.Intensities.Add(double.Parse(line.PeakArea,
                NumberStyles.Float, CultureInfo.InvariantCulture));
        }
    }

    /// <summary>cpp parity: TSVReader.cpp:795 — Bruker Paser result TSV (DIA-NN-shaped).</summary>
    internal sealed class PaserResultReader : TSVReaderBase
    {
        private static readonly string[] RequiredColumnNames =
        {
            "File.Name", "RT", "RT.Start", "RT.Stop", "Modified.Sequence",
            "Q.Value", "Precursor.Charge", "Precursor.Mz", "Ms1.Area",
        };

        private readonly Dictionary<string, List<PSM>> _resultPsmMap =
            new(StringComparer.Ordinal);
        // cpp parity: TSVReader.cpp:939 uses std::map (sorted ascending) so the per-file
        // fileID assignment order matches cpp byte-for-byte.
        private readonly SortedDictionary<string, List<PSM>> _filePsmMap =
            new(StringComparer.Ordinal);
        private readonly string _libraryTsvPath = string.Empty;

        public PaserResultReader(BlibBuilder builder, string tsvName, ProgressIndicator? parentProgress)
            : base(builder, tsvName, parentProgress)
        {
            TargetColumns.Add(new TSVColumnTranslator("File.Name", -1, TSVLine.InsertFilename));
            TargetColumns.Add(new TSVColumnTranslator("RT", -1, TSVLine.InsertRtMinutes));
            TargetColumns.Add(new TSVColumnTranslator("RT.Start", -1, TSVLine.InsertRtStartMinutes));
            TargetColumns.Add(new TSVColumnTranslator("RT.Stop", -1, TSVLine.InsertRtEndMinutes));
            TargetColumns.Add(new TSVColumnTranslator("Modified.Sequence", -1, TSVLine.InsertSequence));
            TargetColumns.Add(new TSVColumnTranslator("Q.Value", -1, TSVLine.InsertScore));
            TargetColumns.Add(new TSVColumnTranslator("Precursor.Charge", -1, TSVLine.InsertCharge));
            TargetColumns.Add(new TSVColumnTranslator("Precursor.Mz", -1, TSVLine.InsertMz));
            TargetColumns.Add(new TSVColumnTranslator("Ms1.Area", -1, TSVLine.InsertPeakArea));

            OptionalColumns.Add(new TSVColumnTranslator("Protein.Ids", -1, TSVLine.InsertProteinName));
            OptionalColumns.Add(new TSVColumnTranslator("Exp.1/K0", -1, TSVLine.InsertIonMobility));

            OpenAndParseHeader();

            // cpp parity: TSVReader.cpp:884 — find one and only one `_ip2_ip2*.tsv` library file
            // next to the results TSV.
            var dir = BlibUtils.GetPath(tsvName);
            if (string.IsNullOrEmpty(dir)) dir = ".";
            var ip2Files = Directory.GetFiles(dir, "_ip2_ip2*.tsv");
            if (ip2Files.Length > 1)
            {
                throw new BlibException(false,
                    "found more than one ip2_ip2 library TSV file in the same directory as the Paser results; "
                    + "move the TSV files to a separate directory with one ip2_ip2 TSV");
            }
            if (ip2Files.Length == 0)
            {
                throw new BlibException(false,
                    "missing required ip2_ip2 library TSV file corresponding to Paser results "
                    + "(it should start with '_ip2_ip2_' and end with '.tsv')");
            }
            _libraryTsvPath = ip2Files[0];
        }

        public static bool HasExpectedColumns(HashSet<string> headerColumns)
        {
            foreach (var required in RequiredColumnNames)
            {
                if (!headerColumns.Contains(required))
                {
                    Verbosity.Comment(VerbosityLevel.Detail, $"did not find column '{required}'");
                    return false;
                }
            }
            return true;
        }

        public override bool ParseFile()
        {
            Verbosity.Debug("Collecting PSMs");
            var proteins = new Dictionary<string, Protein>(StringComparer.Ordinal);
            CollectPsms(proteins);

            if (!string.IsNullOrEmpty(_libraryTsvPath))
                AddLibraryInfo();

            Verbosity.Debug("Building tables");
            InitSpecFileProgress(_filePsmMap.Count);
            foreach (var kv in _filePsmMap)
            {
                Psms.Clear();
                foreach (var p in kv.Value) Psms.Add(p);
                SetSpecFileName(kv.Key, checkFile: false);
                BuildTables(PsmScoreType.GenericQValue, kv.Key, showSpecProgress: false);
            }

            _resultPsmMap.Clear();
            return true;
        }

        public override IList<PsmScoreType> GetScoreTypes() => new[] { PsmScoreType.GenericQValue };

        private void AddLibraryInfo()
        {
            Verbosity.Debug($"Collecting peaks from library '{_libraryTsvPath}'");
            using var libraryReader = new PaserLibraryReader(BlibMaker, _libraryTsvPath,
                ParentProgress, _resultPsmMap);
            libraryReader.ParseFile();
        }

        protected override void StoreLine(TSVLine line, Dictionary<string, Protein> proteins)
        {
            if (line.Decoy) return;

            // cpp parity: TSVReader.cpp:955 — strip trailing `" - run name"` from filename.
            var filename = line.Filename;
            var sep = filename.LastIndexOf(" - ", StringComparison.Ordinal);
            if (sep >= 0)
                filename = filename.Substring(0, sep);

            var psm = new TSVPSM
            {
                Score = line.Score,
                SpecKey = LineNum,
                Rt = line.Rt,
                LeftWidth = line.LeftWidth,
                RightWidth = line.RightWidth,
            };
            if (!TSVReader.ParseSequence(line.Sequence, out var unmod, out var mods))
                return;
            psm.UnmodSeq = unmod;
            foreach (var m in mods) psm.Mods.Add(m);
            psm.Charge = line.Charge;
            psm.Mz = line.Mz;

            AttachProteins(psm, line.ProteinName, ';', proteins, skipFirst: true);

            if (line.IonMobility > 0)
            {
                psm.IonMobility = line.IonMobility;
                psm.IonMobilityType = IonMobilityType.InverseReducedVsecPerCm2;
            }

            if (!_filePsmMap.TryGetValue(filename, out var fileList))
            {
                fileList = new List<PSM>();
                _filePsmMap[filename] = fileList;
            }
            fileList.Add(psm);

            var key = line.Sequence + line.Charge.ToString(CultureInfo.InvariantCulture);
            if (!_resultPsmMap.TryGetValue(key, out var resultList))
            {
                resultList = new List<PSM>();
                _resultPsmMap[key] = resultList;
            }
            resultList.Add(psm);
        }
    }

    // --- Inner spec reader ---------------------------------------------------------

    /// <summary>
    /// Inner <see cref="ISpecFileReader"/> that pulls peaks straight off a <see cref="TSVPSM"/>.
    /// cpp parity: TSVReader.cpp:1208 — overrides <c>getSpectrum(PSM*, ...)</c>.
    /// </summary>
    internal sealed class TSVSpecFileReader : SpecFileReaderBase
    {
        public override void OpenFile(string path, bool mzSort = false) { /* no-op */ }
        public override SpecIdType IdType { set { /* no-op */ } }

        public override bool GetSpectrum(PSM psm, SpecIdType findBy, SpecData returnData, bool getPeaks)
        {
            ArgumentNullException.ThrowIfNull(psm);
            ArgumentNullException.ThrowIfNull(returnData);
            if (psm is not TSVPSM t) return false;

            returnData.Id = t.SpecKey;
            returnData.RetentionTime = t.Rt;
            returnData.StartTime = t.LeftWidth;
            returnData.EndTime = t.RightWidth;
            returnData.Mz = t.Mz;
            returnData.Ccs = (float)t.Ccs;
            returnData.NumPeaks = t.Mzs.Count;

            if (getPeaks)
            {
                returnData.Mzs = new double[returnData.NumPeaks];
                returnData.Intensities = new float[returnData.NumPeaks];
                for (int i = 0; i < returnData.NumPeaks; i++)
                {
                    returnData.Mzs[i] = t.Mzs[i];
                    returnData.Intensities[i] = (float)t.Intensities[i];
                }
            }
            else
            {
                returnData.Mzs = null;
                returnData.Intensities = null;
            }
            return true;
        }

        public override bool GetSpectrum(int identifier, SpecData returnData, SpecIdType findBy, bool getPeaks = true)
            => false;
        public override bool GetSpectrum(string identifier, SpecData returnData, bool getPeaks = true)
            => false;
        public override bool GetNextSpectrum(SpecData returnData, bool getPeaks = true)
            => false;
    }
}
