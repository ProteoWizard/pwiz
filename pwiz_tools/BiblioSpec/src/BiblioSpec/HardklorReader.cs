// Port of pwiz_tools/BiblioSpec/src/HardklorReader.{h,cpp}
//
// Parses .hk.bs.kro files — the output of BullseyeSharp post-processing Skyline's
// modified Hardklor builds. Files are tab-delimited with a header row; every row is an
// MS1-only feature (precursor-only PSM) with chemical formula + averagine mass offset
// + per-feature retention-time bounds.
//
// cpp subclasses SslReader (HardklorReader.h:39) and only overrides setColumnsAndSeparators
// + addDataLine. The C# port doesn't have a virtual column-setter hook on SslReader, so
// HardklorReader is a standalone BuildParser that reuses SslPSM and mirrors SslReader's
// per-file group + flush flow. Format-specific behavior:
//   * Score column is "Best Correlation" (a cosine-angle); converted to normalized contrast
//     angle: 1 - acos(min(1, cosineAngle)) * 2 / PI (cpp parity: HardklorReader.h:79).
//   * "Charge" populates both PSM.Charge and SmallMolMetadata.PrecursorAdduct as "[M{+z}H]".
//   * "Averagine" populates SmallMolMetadata.ChemicalFormula verbatim (the trailing
//     "[+x.xxx]" suffix carries the mass-shift to make the formula match the reported m/z).
//   * "FeatureName" populates SmallMolMetadata.MoleculeName.
//   * Every PSM is marked precursor-only and tagged with PsmScoreType.HardklorIdotp; the
//     OptionalSort hook in BuildParser then sorts by (mass asc, charge asc, score desc).

using System.Globalization;

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// Parses BiblioSpec <c>.hk.bs.kro</c> files. Each row is one MS1 feature; the reader
/// groups rows by source spectrum file (the "File" column) and flushes one file's worth
/// of PSMs at a time, matching the SslReader pattern.
/// </summary>
/// <remarks>
/// Port of <c>BiblioSpec::HardklorReader</c> at
/// <c>pwiz_tools/BiblioSpec/src/HardklorReader.{h,cpp}</c>. The cpp class extends
/// <c>SslReader</c>; the C# port stands alone and reuses <see cref="SslPSM"/>.
/// </remarks>
public sealed class HardklorReader : BuildParser
{
    private readonly string _hkName;
    private readonly string _hkDir;

    // cpp parity: HardklorReader.h:42 hasHeader_ = true. The .hk.bs.kro format always has a
    // header row.
    private const bool HasHeader = true;

    // Per-file PSM map. SortedDictionary matches std::map iteration order so fileID
    // assignment in the .blib lines up byte-for-byte with cpp when the input references
    // more than one spectrum file. cpp parity: SslReader.h:266.
    private readonly SortedDictionary<string, List<PSM?>> _fileMap = new(StringComparer.Ordinal);

    // cpp parity: HardklorReader.cpp:87-97 — required columns. Every row must populate all
    // of these; missing column in header => throw.
    private static readonly string[] _requiredColumns =
    {
        "File",
        "Charge",
        "Base Isotope Peak",
        "Best Correlation",
        "Best RTime",
        "First RTime",
        "Last RTime",
        "Averagine",
        "FeatureName",
    };

    private readonly Dictionary<string, Action<SslPSM, string, int>> _columnSetters;

    /// <summary>
    /// Returns true if <paramref name="path"/> ends with <c>.hk.bs.kro</c>. cpp parity:
    /// the cpp reader factory in BlibBuild.cpp matches the same multi-segment extension.
    /// </summary>
    public static bool AcceptsExtension(string path) =>
        path.EndsWith(".hk.bs.kro", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Construct a HardklorReader bound to <paramref name="builder"/> and the
    /// <c>.hk.bs.kro</c> file at <paramref name="hkFilename"/>.
    /// </summary>
    /// <remarks>cpp parity: HardklorReader.cpp:37.</remarks>
    public HardklorReader(BlibBuilder builder, string hkFilename, ProgressIndicator? parentProgress)
        : base(builder, hkFilename, parentProgress)
    {
        _hkName = hkFilename;
        _hkDir = BlibUtils.GetPath(_hkName);

        // cpp parity: HardklorReader.cpp:87-100 — column -> setter table. Header lookups are
        // ordinal-case-insensitive (cpp's DelimitedFileReader does exact match; the hardklor
        // header tokens are well-defined so OrdinalIgnoreCase is safe and tolerant).
        _columnSetters = new Dictionary<string, Action<SslPSM, string, int>>(StringComparer.OrdinalIgnoreCase)
        {
            ["File"] = SetFile,
            ["Charge"] = SetChargeAndAdduct,
            ["Base Isotope Peak"] = SetPrecursorMzDeclared,
            ["Best Correlation"] = SetIdotP,
            ["Best RTime"] = SetRetentionTime,
            ["First RTime"] = SetStartTime,
            ["Last RTime"] = SetEndTime,
            ["Averagine"] = SetChemicalFormulaAndMassShift,
            ["FeatureName"] = SetFeatureName,
        };
    }

    /// <summary>Score types this reader produces. cpp parity: always HARDKLOR_IDOTP.</summary>
    public override IList<PsmScoreType> GetScoreTypes() =>
        new[] { PsmScoreType.HardklorIdotp };

    /// <summary>
    /// Apply the per-row RT overrides parsed from "Best RTime" / "First RTime" / "Last RTime"
    /// (kept on the SslPSM by the column setters). cpp parity: HardklorReader inherits
    /// SslReader::applyPsmOverrideValues. Unlike SslReader, Hardklor rows always carry RT
    /// info, so the override is unconditional (no <c>retentionTime != 0</c> gate).
    /// </summary>
    public override void ApplyPsmOverrideValues(PSM psm, SpecData specData)
    {
        ArgumentNullException.ThrowIfNull(psm);
        ArgumentNullException.ThrowIfNull(specData);

        if (psm is SslPSM ssl)
        {
            specData.RetentionTime = ssl.RtInfo.RetentionTime;
            specData.StartTime = ssl.RtInfo.StartTime;
            specData.EndTime = ssl.RtInfo.EndTime;
        }
    }

    /// <summary>
    /// Parse the .hk.bs.kro file, group its rows by source spectrum file, and flush each
    /// group to the library. cpp parity: HardklorReader inherits SslReader::parseFile.
    /// </summary>
    public override bool ParseFile()
    {
        Parse();

        if (_fileMap.Count > 1)
        {
            InitSpecFileProgress(_fileMap.Count);
        }

        foreach (var fileEntry in _fileMap)
        {
            var filename = fileEntry.Key;

            try
            {
                // Try the filename as-is first.
                SetSpecFileName(filename);
            }
            catch (BlibException)
            {
                // Fall back to the hk-dir-relative path.
                SetSpecFileName(_hkDir + filename);
            }

            Psms.Clear();
            foreach (var p in fileEntry.Value)
                Psms.Add(p);

            // cpp parity: HardklorReader.cpp:105 — setPrecursorOnly() is called per row; the
            // lookup style is then always NameId because every PSM is precursor-only with
            // a generated SpecName.
            LookUpBy = SpecIdType.NameId;

            BuildTables(PsmScoreType.HardklorIdotp);
        }

        return true;
    }

    // ----- parse loop -----------------------------------------------------------------

    private void Parse()
    {
        Verbosity.Debug("Parsing File.");

        _fileMap.Clear();

        using var reader = new StreamReader(_hkName);

        string? rawHeader = null;
        if (HasHeader)
        {
            rawHeader = reader.ReadLine();
            if (rawHeader is null)
            {
                throw new BlibException(true, $"Hardklor file is empty: {_hkName}");
            }
        }

        var columnIndexByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (rawHeader is not null)
        {
            var headerCols = SplitTabs(rawHeader);
            for (var c = 0; c < headerCols.Length; c++)
            {
                var name = headerCols[c].Trim();
                if (name.Length == 0) continue;
                columnIndexByName[name] = c;
            }
        }

        foreach (var required in _requiredColumns)
        {
            if (!columnIndexByName.ContainsKey(required))
            {
                throw new BlibException(true,
                    $"Required column '{required}' is missing from header in '{_hkName}'.");
            }
        }

        // Pre-resolve each known column to (index, setter) so the per-row loop is a tight
        // walk over only the columns that actually exist.
        var activeColumns = new List<(int Index, Action<SslPSM, string, int> Setter)>();
        foreach (var kv in _columnSetters)
        {
            if (columnIndexByName.TryGetValue(kv.Key, out var idx))
                activeColumns.Add((idx, kv.Value));
        }

        var lineNumber = HasHeader ? 1 : 0;
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            lineNumber++;
            if (line.Length == 0) continue;

            var cols = SplitTabs(line);

            var newPsm = new SslPSM();
            foreach (var (idx, setter) in activeColumns)
            {
                // cpp parity: HardklorReader.cpp:100 — defineSeparatorsNoEscape: tab-delim,
                // no quoting, no escape (Windows path style in the File column would otherwise
                // be mangled by the default backslash-escape). We don't strip quotes here for
                // the same reason.
                var value = idx < cols.Length ? cols[idx].TrimStart() : string.Empty;
                try
                {
                    setter(newPsm, value, lineNumber);
                }
                catch (BlibException ex)
                {
                    throw new BlibException(true,
                        $"{_hkName} line {lineNumber.ToString(CultureInfo.InvariantCulture)}: {ex.Message}");
                }
            }

            AddDataLine(newPsm);
        }
    }

    /// <summary>
    /// Finalize a parsed row and stash it in the per-file map. cpp parity:
    /// HardklorReader.cpp:104 <c>addDataLine</c>.
    /// </summary>
    private void AddDataLine(SslPSM newPsm)
    {
        // cpp parity: HardklorReader.cpp:105 — Hardklor rows are MS1-only.
        newPsm.SetPrecursorOnly();
        newPsm.ScoreType = PsmScoreType.HardklorIdotp;

        // cpp parity: SslReader.cpp:89 — completeness gate. For Hardklor the small-mol
        // metadata is what must be complete-enough (precursor-only, no peptide sequence).
        if (!newPsm.IsCompleteEnough())
        {
            throw new BlibException(false, "Incomplete description: " + newPsm.IdAsString());
        }

        if (!_fileMap.TryGetValue(newPsm.Filename, out var list))
        {
            list = new List<PSM?> { newPsm };
            _fileMap[newPsm.Filename] = list;
        }
        else
        {
            list.Add(newPsm);
        }
    }

    // ----- column setters (cpp parity: HardklorReader.h:50-86 + reused SslReader setters) -

    // cpp parity: SslReader.h:50 — file column is required, throw on empty.
    private static void SetFile(SslPSM psm, string value, int lineNumber)
    {
        if (value.Length == 0)
            throw new BlibException(false, "Missing filename.");
        psm.Filename = value;
    }

    // cpp parity: HardklorReader.h:60 — setChargeAndAdduct. The integer charge feeds both
    // PSM.Charge and the [M+zH] adduct string.
    private static void SetChargeAndAdduct(SslPSM psm, string value, int lineNumber)
    {
        if (value.Length == 0)
            throw new BlibException(false, "Missing charge.");
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var z))
            throw new BlibException(false, $"Non-numeric charge value: {value}.");
        psm.Charge = z;
        // cpp parity: HardklorReader.h:63 — snprintf("[M%+dH]", charge). The '%+d' modifier
        // always emits a sign. C#'s standard ToString("+#;-#") handles the same; we use a
        // format string for clarity.
        psm.SmallMolMetadata.PrecursorAdduct = string.Format(
            CultureInfo.InvariantCulture, "[M{0:+#;-#}H]", z);
    }

    // cpp parity: SslReader.h:215 — setPrecursorMzDeclared. "Base Isotope Peak" is the
    // reported m/z; SmallMolMetadata.PrecursorMzDeclared carries it through to the .blib.
    private static void SetPrecursorMzDeclared(SslPSM psm, string value, int lineNumber)
    {
        if (value.Length == 0) return;
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            throw new BlibException(false, $"Non-numeric precursorMZ: {value}");
        psm.SmallMolMetadata.PrecursorMzDeclared = d;
    }

    // cpp parity: HardklorReader.h:71 — setIdotP. Hardklor reports cosine-angle correlation
    // (CAC); we convert to normalized contrast angle (NCA) for the .blib:
    //   NCA = 1 - acos(min(1, CAC)) * 2 / PI
    // Empty value => score 0 (cpp parity: HardklorReader.h:73).
    private static void SetIdotP(SslPSM psm, string value, int lineNumber)
    {
        if (value.Length == 0)
        {
            psm.Score = 0;
            return;
        }
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var cosineAngle))
            throw new BlibException(false, $"Non-numeric score: {value}");
        psm.Score = 1.0 - Math.Acos(Math.Min(1.0, cosineAngle)) * 2.0 / Math.PI;
    }

    // cpp parity: SslReader.h:116 — setRetentionTime.
    private static void SetRetentionTime(SslPSM psm, string value, int lineNumber)
    {
        if (value.Length == 0) return;
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            throw new BlibException(false, $"Non-numeric retention time: {value}");
        psm.RtInfo.RetentionTime = d;
    }

    // cpp parity: SslReader.h:126 — setStartTime.
    private static void SetStartTime(SslPSM psm, string value, int lineNumber)
    {
        if (value.Length == 0) return;
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            throw new BlibException(false, $"Non-numeric start time: {value}");
        psm.RtInfo.StartTime = d;
    }

    // cpp parity: SslReader.h:137 — setEndTime.
    private static void SetEndTime(SslPSM psm, string value, int lineNumber)
    {
        if (value.Length == 0) return;
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            throw new BlibException(false, $"Non-numeric end time: {value}");
        psm.RtInfo.EndTime = d;
    }

    // cpp parity: HardklorReader.h:51 — setChemicalFormulaAndMassShift. Skyline's Hardklor
    // emits "H21C14N4O4[+3.038518]" — formula plus a bracketed mass offset that lets the
    // displayed isotope envelope line up with the reported m/z. We store it verbatim.
    private static void SetChemicalFormulaAndMassShift(SslPSM psm, string value, int lineNumber)
    {
        psm.SmallMolMetadata.ChemicalFormula = value;
    }

    // cpp parity: HardklorReader.h:67 — setFeatureName. Skyline's hardklor build emits
    // "mass<value>_RT<value>" here so BiblioSpec can recognize features detected in
    // multiple files after RT alignment.
    private static void SetFeatureName(SslPSM psm, string value, int lineNumber)
    {
        psm.SmallMolMetadata.MoleculeName = value;
    }

    // ----- helpers --------------------------------------------------------------------

    private static string[] SplitTabs(string line)
    {
        if (line.Length > 0 && line[line.Length - 1] == '\r')
            line = line.Substring(0, line.Length - 1);
        return line.Split('\t');
    }
}
