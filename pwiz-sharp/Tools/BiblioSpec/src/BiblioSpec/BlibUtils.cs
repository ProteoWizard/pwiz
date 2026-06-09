// Port of pwiz_tools/BiblioSpec/src/BlibUtils.{h,cpp}

using System.Buffers;
using System.Diagnostics;
using System.Reflection;

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// The three possible ways a spectrum may be identified in both result files and spectrum files.
/// </summary>
/// <remarks>Port of <c>BiblioSpec::SPEC_ID_TYPE</c>. Numeric values match the cpp enum so
/// they can be persisted to and compared against .blib columns.</remarks>
public enum SpecIdType
{
    /// <summary>Unknown / unspecified (cpp: UNKNOWN = -1).</summary>
    Unknown = -1,
    /// <summary>Spectrum identified by scan number (cpp: SCAN_NUM_ID).</summary>
    ScanNumberId = 0,
    /// <summary>Spectrum identified by zero-based index (cpp: INDEX_ID).</summary>
    IndexId = 1,
    /// <summary>Spectrum identified by native id / name (cpp: NAME_ID).</summary>
    NameId = 2,
}

/// <summary>Supported ion mobility units. Must agree with <c>pwiz.CLI.analysis.SpectrumList_IonMobility</c>.</summary>
/// <remarks>Port of <c>BiblioSpec::IONMOBILITY_TYPE</c>. Numeric values match the cpp enum.</remarks>
public enum IonMobilityType
{
    /// <summary>No ion mobility.</summary>
    None = 0,
    /// <summary>Drift time in milliseconds.</summary>
    DriftTimeMsec = 1,
    /// <summary>Inverse reduced mobility (1/K0) in V·sec/cm^2.</summary>
    InverseReducedVsecPerCm2 = 2,
    /// <summary>FAIMS compensation voltage (V).</summary>
    CompensationV = 3,
}

/// <summary>Workflow type for SpectrumSourceFiles. Port of <c>BiblioSpec::WORKFLOW_TYPE</c>.</summary>
public enum WorkflowType
{
    /// <summary>Data-dependent acquisition.</summary>
    Dda = 0,
    /// <summary>Data-independent acquisition.</summary>
    Dia = 1,
}

/// <summary>
/// All possible scores from different search algorithms. Port of <c>BiblioSpec::PSM_SCORE_TYPE</c>.
/// </summary>
/// <remarks>
/// Numeric values must match cpp because they are persisted to .blib columns. Whenever this
/// enum grows, update <see cref="BlibUtils.ScoreTypeToString"/> in lockstep.
/// </remarks>
public enum PsmScoreType
{
    /// <summary>Default for ssl files.</summary>
    UnknownScoreType = 0,
    /// <summary>sequest/percolator .sqt files.</summary>
    PercolatorQValue,
    /// <summary>pepxml files.</summary>
    PeptideProphetSomething,
    /// <summary>pepxml files (currently scoreless).</summary>
    SpectrumMill,
    /// <summary>idpxml files.</summary>
    IdPickerFdr,
    /// <summary>mascot .dat files (.pep.xml?, .mzid?).</summary>
    MascotIonsScore,
    /// <summary>tandem .xtan.xml files.</summary>
    TandemExpectationValue,
    /// <summary>protein pilot .group.xml files.</summary>
    ProteinPilotConfidence,
    /// <summary>scaffold .mzid files.</summary>
    ScaffoldSomething,
    /// <summary>Waters MSE .csv files.</summary>
    WatersMsePeptideScore,
    /// <summary>pepxml files.</summary>
    OmssaExpectationScore,
    /// <summary>pepxml with expectation score.</summary>
    ProteinProspectorExpect,
    /// <summary>sequest (no percolator) .sqt files.</summary>
    SequestXCorr,
    /// <summary>maxquant msms.txt files.</summary>
    MaxQuantScore,
    /// <summary>pepxml files with morpheus scores.</summary>
    MorpheusScore,
    /// <summary>pepxml files with ms-gfdb scores.</summary>
    MsgfScore,
    /// <summary>pepxml files with peaks confidence scores.</summary>
    PeaksConfidenceScore,
    /// <summary>byonic .mzid files.</summary>
    ByonicPep,
    /// <summary>peptideshaker .mzid files.</summary>
    PeptideShakerConfidence,
    /// <summary>Generic q-value.</summary>
    GenericQValue,
    /// <summary>Hardklor iDotP score (cosine angle correlation → normalized contrast angle).</summary>
    HardklorIdotp,
}

/// <summary>
/// How a score should be interpreted — as a probability of identification being correct,
/// incorrect, or as a raw (non-probability) score. Port of <c>BiblioSpec::PROBABILITY_TYPE</c>
/// (anonymously declared in BlibUtils.cpp).
/// </summary>
internal enum ProbabilityType
{
    None = 0,
    ProbabilityCorrect = 1,
    ProbabilityIncorrect = 2,
}

/// <summary>
/// Miscellaneous BiblioSpec helpers. Anything that maps cleanly to BCL
/// (<see cref="System.IO.Path"/>, <see cref="string"/> ops) uses BCL directly; this class only
/// hosts the genuinely BiblioSpec-specific helpers + the enum-to-string lookup tables.
/// </summary>
/// <remarks>Port of <c>BiblioSpec::BlibUtils</c> free functions.</remarks>
public static class BlibUtils
{
    private static readonly SearchValues<char> _pathSeparators = SearchValues.Create("/\\");

    // Lookup tables mirroring BlibUtils.cpp:37 — strict ordering matches PsmScoreType ordinals.
    private static readonly (string Name, ProbabilityType Probability)[] _scoreTypes =
    {
        ("UNKNOWN", ProbabilityType.None),
        ("PERCOLATOR QVALUE", ProbabilityType.ProbabilityIncorrect),
        ("PEPTIDE PROPHET SOMETHING", ProbabilityType.ProbabilityCorrect),
        ("SPECTRUM MILL", ProbabilityType.None),
        ("IDPICKER FDR", ProbabilityType.ProbabilityIncorrect),
        ("MASCOT IONS SCORE", ProbabilityType.ProbabilityIncorrect),
        ("TANDEM EXPECTATION VALUE", ProbabilityType.ProbabilityIncorrect),
        ("PROTEIN PILOT CONFIDENCE", ProbabilityType.ProbabilityCorrect),
        ("SCAFFOLD SOMETHING", ProbabilityType.ProbabilityCorrect),
        ("WATERS MSE PEPTIDE SCORE", ProbabilityType.None),
        ("OMSSA EXPECTATION SCORE", ProbabilityType.ProbabilityIncorrect),
        ("PROTEIN PROSPECTOR EXPECTATION SCORE", ProbabilityType.ProbabilityIncorrect),
        ("SEQUEST XCORR", ProbabilityType.ProbabilityIncorrect),
        ("MAXQUANT SCORE", ProbabilityType.ProbabilityIncorrect),
        ("MORPHEUS SCORE", ProbabilityType.ProbabilityIncorrect),
        ("MSGF+ SCORE", ProbabilityType.ProbabilityIncorrect),
        ("PEAKS CONFIDENCE SCORE", ProbabilityType.ProbabilityIncorrect),
        ("BYONIC SCORE", ProbabilityType.ProbabilityIncorrect),
        ("PEPTIDE SHAKER CONFIDENCE", ProbabilityType.ProbabilityCorrect),
        ("GENERIC Q-VALUE", ProbabilityType.ProbabilityIncorrect),
        ("HARDKLOR IDOTP", ProbabilityType.ProbabilityCorrect),
    };

    /// <summary>
    /// Translate a string value into its corresponding score type. Returns
    /// <see cref="PsmScoreType.UnknownScoreType"/> if the string does not match any known type.
    /// </summary>
    public static PsmScoreType StringToScoreType(string scoreName)
    {
        ArgumentNullException.ThrowIfNull(scoreName);
        for (var i = 0; i < _scoreTypes.Length; i++)
        {
            if (string.Equals(_scoreTypes[i].Name, scoreName, StringComparison.Ordinal))
                return (PsmScoreType)i;
        }
        return PsmScoreType.UnknownScoreType;
    }

    /// <summary>Returns the string representation of the score type.</summary>
    public static string ScoreTypeToString(PsmScoreType scoreType)
    {
        var idx = (int)scoreType;
        if (idx < 0 || idx >= _scoreTypes.Length)
            return "UNKNOWN";
        return _scoreTypes[idx].Name;
    }

    /// <summary>Returns the string representation of the score's cutoff / probability type.</summary>
    public static string ScoreTypeToProbabilityTypeString(PsmScoreType scoreType)
    {
        var idx = (int)scoreType;
        if (idx < 0 || idx >= _scoreTypes.Length)
            return "UNKNOWN";
        return _scoreTypes[idx].Probability switch
        {
            ProbabilityType.None => "NOT_A_PROBABILITY_VALUE",
            ProbabilityType.ProbabilityCorrect => "PROBABILITY_THAT_IDENTIFICATION_IS_CORRECT",
            ProbabilityType.ProbabilityIncorrect => "PROBABILITY_THAT_IDENTIFICATION_IS_INCORRECT",
            _ => "UNKNOWN",
        };
    }

    /// <summary>
    /// Translate a <see cref="SpecIdType"/> to its display string. Throws
    /// <see cref="BlibException"/> on unknown input, matching cpp.
    /// </summary>
    public static string SpecIdTypeToString(SpecIdType specIdType) => specIdType switch
    {
        SpecIdType.ScanNumberId => "scan number",
        SpecIdType.IndexId => "index",
        SpecIdType.NameId => "nativeID",
        _ => throw new BlibException(true, "unknown specIdType"),
    };

    /// <summary>
    /// Translate an <see cref="IonMobilityType"/> to its display string. Throws
    /// <see cref="BlibException"/> on unknown input, matching cpp.
    /// </summary>
    public static string IonMobilityTypeToString(IonMobilityType ionMobilityType) => ionMobilityType switch
    {
        IonMobilityType.None => "none",
        IonMobilityType.DriftTimeMsec => "driftTime(msec)",
        IonMobilityType.InverseReducedVsecPerCm2 => "inverseK0(Vsec/cm^2)",
        IonMobilityType.CompensationV => "compensation(V)",
        _ => throw new BlibException(true, "unknown ion mobility type"),
    };

    /// <summary>
    /// Parse a display string back into an <see cref="IonMobilityType"/>. Accepts the canonical
    /// strings emitted by <see cref="IonMobilityTypeToString"/> plus the same set of aliases the
    /// cpp version accepts (case-insensitive). Throws <see cref="BlibException"/> on unknown input.
    /// </summary>
    public static IonMobilityType ParseIonMobilityType(string ionMobilityType)
    {
        ArgumentNullException.ThrowIfNull(ionMobilityType);

        if (Eq(ionMobilityType, "none")) return IonMobilityType.None;

        if (Eq(ionMobilityType, "driftTime(msec)")) return IonMobilityType.DriftTimeMsec;
        if (Eq(ionMobilityType, "ms")) return IonMobilityType.DriftTimeMsec;
        if (Eq(ionMobilityType, "msec")) return IonMobilityType.DriftTimeMsec;

        if (Eq(ionMobilityType, "inverseK0(Vsec/cm^2)")) return IonMobilityType.InverseReducedVsecPerCm2;
        if (Eq(ionMobilityType, "inverseK0")) return IonMobilityType.InverseReducedVsecPerCm2;
        if (Eq(ionMobilityType, "1/K0")) return IonMobilityType.InverseReducedVsecPerCm2;
        if (Eq(ionMobilityType, "Vsec/cm^2")) return IonMobilityType.InverseReducedVsecPerCm2;
        if (Eq(ionMobilityType, "Vsec/cm2")) return IonMobilityType.InverseReducedVsecPerCm2;

        if (Eq(ionMobilityType, "compensation(V)")) return IonMobilityType.CompensationV;
        if (Eq(ionMobilityType, "V")) return IonMobilityType.CompensationV;

        throw new BlibException(true, $"unknown ion mobility type: {ionMobilityType}");

        static bool Eq(string a, string b) =>
            string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Return an absolute path for <paramref name="filename"/>. For filenames with no path,
    /// prepends current working directory. Does not check that the file exists and does not
    /// resolve symbolic links.
    /// </summary>
    /// <remarks>
    /// cpp BlibUtils.cpp:163 uses <c>bfs::system_complete</c> + <c>make_preferred</c>. The BCL
    /// equivalent is <see cref="Path.GetFullPath(string)"/>, which on Windows already returns
    /// backslash-preferred paths.
    /// </remarks>
    public static string GetAbsoluteFilePath(string filename)
    {
        ArgumentNullException.ThrowIfNull(filename);
        return Path.GetFullPath(filename);
    }

    /// <summary>
    /// Return everything before the last <c>/</c> or <c>\</c>, INCLUDING the separator.
    /// Returns an empty string if neither found.
    /// </summary>
    /// <remarks>
    /// cpp BlibUtils.cpp:184 — note this preserves the trailing separator (substr(0, lastSlash+1)),
    /// which is different from <see cref="Path.GetDirectoryName(string)"/>. Preserved for parity.
    /// </remarks>
    public static string GetPath(string fullFileName)
    {
        ArgumentNullException.ThrowIfNull(fullFileName);
        var lastSlash = fullFileName.AsSpan().LastIndexOfAny(_pathSeparators);
        return lastSlash < 0 ? string.Empty : fullFileName.Substring(0, lastSlash + 1);
    }

    /// <summary>
    /// Return everything between the last <c>/</c> or <c>\</c> and the last <c>.</c>. Returns the
    /// whole string if neither found.
    /// </summary>
    /// <remarks>cpp BlibUtils.cpp:207. Note this returns extension-less, slashless basename.</remarks>
    public static string GetFileRoot(string fullFileName)
    {
        ArgumentNullException.ThrowIfNull(fullFileName);
        var lastSlash = fullFileName.AsSpan().LastIndexOfAny(_pathSeparators);
        var start = lastSlash < 0 ? 0 : lastSlash + 1;
        var lastDot = fullFileName.LastIndexOf('.');
        if (lastDot < start)
            return fullFileName.Substring(start);
        return fullFileName.Substring(start, lastDot - start);
    }

    /// <summary>
    /// Returns true if <paramref name="filename"/> ends exactly with <paramref name="ext"/>.
    /// Assumes <paramref name="ext"/> includes the leading dot.
    /// </summary>
    /// <remarks>cpp BlibUtils.cpp:232 — case-sensitive comparison, matching cpp <c>strcmp</c>.</remarks>
    public static bool HasExtension(string filename, string ext)
    {
        ArgumentNullException.ThrowIfNull(filename);
        ArgumentNullException.ThrowIfNull(ext);
        return filename.EndsWith(ext, StringComparison.Ordinal);
    }

    /// <summary>
    /// Replace all characters after the last <c>.</c> with <paramref name="ext"/>. If no dot
    /// is found, concatenate <c>.ext</c> onto the filename.
    /// </summary>
    /// <remarks>
    /// cpp BlibUtils.cpp:272. <paramref name="ext"/> should NOT contain the leading dot
    /// (the dot is supplied by this method).
    /// </remarks>
    public static string ReplaceExtension(string filename, string ext)
    {
        ArgumentNullException.ThrowIfNull(filename);
        ArgumentNullException.ThrowIfNull(ext);
        var lastDot = filename.LastIndexOf('.');
        return lastDot < 0
            ? string.Concat(filename, ".", ext)
            : string.Concat(filename.AsSpan(0, lastDot + 1), ext.AsSpan());
    }

    /// <summary>
    /// Sum the masses of amino acids and modifications from the given mass table (as
    /// initialised by <see cref="AminoAcidMasses.InitializeMass"/>).
    /// </summary>
    /// <remarks>
    /// cpp BlibUtils.cpp:289 — modifications are inline as <c>[mass]</c>, e.g. <c>"PEPM[15.99]TIDE"</c>.
    /// Illegal characters trigger <see cref="Verbosity.Error(string)"/> which throws
    /// <see cref="BlibException"/>.
    /// </remarks>
    public static double GetPeptideMass(string modifiedSeq, double[] masses)
    {
        ArgumentNullException.ThrowIfNull(modifiedSeq);
        ArgumentNullException.ThrowIfNull(masses);

        double mass = 0;
        for (var i = 0; i < modifiedSeq.Length; i++)
        {
            var aa = modifiedSeq[i];
            if (aa == '[')
            {
                var end = modifiedSeq.IndexOf(']', i);
                if (end < 0)
                {
                    Verbosity.Error(
                        $"Unterminated modification bracket at position {i} in {modifiedSeq}.");
                }
                // cpp: modifiedSeq.substr(i + 1, end - i) — note that's end-i, not end-(i+1).
                // This includes the ']' character in the parse range; atof stops at non-numeric
                // characters so it doesn't actually affect the value. Preserved for parity.
                var modStr = modifiedSeq.Substring(i + 1, end - i);
                _ = double.TryParse(
                    modStr,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var modMass);
                mass += modMass;
                i = end;
            }
            else if (aa >= 'A' && aa <= 'Z')
            {
                mass += masses[aa];
            }
            else
            {
                Verbosity.Error($"Illegal character {aa} for computing mass of {modifiedSeq}.");
            }
        }
        return mass;
    }

    /// <summary>
    /// Replace every occurrence of <paramref name="findChar"/> with <paramref name="replaceChar"/>
    /// in <paramref name="s"/>, returning the resulting string and the number of substitutions.
    /// </summary>
    /// <remarks>cpp BlibUtils.cpp:257 mutates in place; in C# we return a new string + the count.</remarks>
    public static (string Result, int Count) ReplaceAllChar(string s, char findChar, char replaceChar)
    {
        ArgumentNullException.ThrowIfNull(s);
        var count = 0;
        for (var i = 0; i < s.Length; i++)
            if (s[i] == findChar) count++;
        return count == 0 ? (s, 0) : (s.Replace(findChar, replaceChar), count);
    }

    /// <summary>Delete any trailing spaces or tabs from <paramref name="str"/>.</summary>
    /// <remarks>
    /// cpp BlibUtils.cpp:316 — <c>bal::trim_right</c> trims all whitespace, so we use
    /// <see cref="string.TrimEnd()"/> with no args (BCL whitespace set) for parity with boost.
    /// </remarks>
    public static string DeleteTrailingWhitespace(string str)
    {
        ArgumentNullException.ThrowIfNull(str);
        return str.TrimEnd();
    }

    /// <summary>
    /// Index of the largest element in <paramref name="elements"/>. On ties, returns the first
    /// occurrence. Returns 0 for an empty list (matches cpp semantics).
    /// </summary>
    /// <remarks>cpp BlibUtils.h:227 template <c>getMaxElementIndex</c>.</remarks>
    public static int GetMaxElementIndex<T>(IReadOnlyList<T> elements) where T : IComparable<T>
    {
        ArgumentNullException.ThrowIfNull(elements);
        if (elements.Count == 0) return 0;
        var maxIdx = 0;
        var max = elements[0];
        for (var i = 1; i < elements.Count; i++)
        {
            if (elements[i].CompareTo(max) > 0)
            {
                max = elements[i];
                maxIdx = i;
            }
        }
        return maxIdx;
    }

    /// <summary>
    /// Returns the full path to the directory containing the currently-executing assembly.
    /// </summary>
    /// <remarks>
    /// cpp BlibUtils.cpp:345 uses <c>GetModuleFileName</c> / <c>readlink("/proc/self/exe")</c>.
    /// The BCL equivalent is <see cref="Assembly.Location"/> on the entry assembly — but for
    /// single-file deployments / unit-test hosts this can return empty, so we fall back to
    /// <see cref="Process.GetCurrentProcess"/>. Always ends with the platform separator.
    /// </remarks>
    public static string GetExeDirectory()
    {
        string? path = null;
        var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var asmLoc = asm.Location;
        if (!string.IsNullOrEmpty(asmLoc))
        {
            path = Path.GetDirectoryName(asmLoc);
        }
        if (string.IsNullOrEmpty(path))
        {
            using var proc = Process.GetCurrentProcess();
            var module = proc.MainModule?.FileName;
            if (!string.IsNullOrEmpty(module))
            {
                path = Path.GetDirectoryName(module);
            }
        }
        if (string.IsNullOrEmpty(path))
        {
            throw new BlibException(false, "Could not find the location of this executable.");
        }
        return path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }
}
