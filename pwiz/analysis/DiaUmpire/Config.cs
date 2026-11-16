using System.Globalization;
using Pwiz.Data.MsData;

namespace Pwiz.Analysis.DiaUmpire;

/// <summary>DIA windowing scheme.</summary>
public enum TargetWindowScheme
{
    /// <summary>Fixed-width SWATH windows (specified by <see cref="Config.DiaFixedWindowSize"/>).</summary>
    SwathFixed,
    /// <summary>Variable-width SWATH windows (loaded from the <c>==window setting</c> block of the params file).</summary>
    SwathVariable,
}

/// <summary>A DIA isolation window with the spectra that fall inside it.</summary>
public sealed class TargetWindow
{
    /// <summary>Creates a target window for the given m/z range.</summary>
    public TargetWindow(MzRange mzRange) { MzRange = mzRange; }
    /// <summary>The m/z range of this isolation window.</summary>
    public MzRange MzRange { get; }
    /// <summary>Indices (into the inner SpectrumList) of MS2 spectra targeting this window.</summary>
    public List<int> SpectraInRange { get; } = new();

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is TargetWindow w && MzRange == w.MzRange;
    /// <inheritdoc/>
    public override int GetHashCode() => MzRange.GetHashCode();
}

/// <summary>
/// DIA-Umpire configuration: instrument parameters, DIA windowing, threading, and
/// spill-file format. Port of cpp <c>DiaUmpire::Config</c> in <c>DiaUmpire.hpp</c>.
/// </summary>
/// <remarks>
/// Construct with a path to a DIA-Umpire <c>.params</c> file, or with an empty
/// path to get the cpp-default <c>TTOF5600</c> settings. The constructor mirrors
/// cpp <c>Config::Config</c>: applies a TTOF5600 default block first, then
/// overlays user values from the params file. The params file is line-oriented
/// (key=value, # comments, an optional <c>==window setting begin</c>/<c>end</c>
/// block listing variable SWATH window edges).
/// </remarks>
public sealed class Config
{
    /// <summary>Instrument parameters. Mutated by the .params file parser.</summary>
    public InstrumentParameter InstrumentParameters { get; } = new();

    /// <summary>Windowing scheme (fixed vs variable).</summary>
    public TargetWindowScheme DiaTargetWindowScheme { get; set; }

    /// <summary>Fixed window size (Da) when <see cref="DiaTargetWindowScheme"/> is <see cref="TargetWindowScheme.SwathFixed"/>.</summary>
    public int DiaFixedWindowSize { get; set; }

    /// <summary>Variable-window edges parsed from the params file's <c>==window setting</c> block.</summary>
    public List<TargetWindow> DiaVariableWindows { get; } = new();

    /// <summary>Emit per-cluster TSVs for MS1 precursor clusters (debugging).</summary>
    public bool ExportMs1ClusterTable { get; set; }
    /// <summary>Emit per-cluster TSVs for MS2 fragment clusters (debugging).</summary>
    public bool ExportMs2ClusterTable { get; set; }
    /// <summary>Emit separate MGFs per quality bucket.</summary>
    public bool ExportSeparateQualityMGFs { get; set; }

    /// <summary>Max parallel threads. 0 = Environment.ProcessorCount / 2 (set by ctor).</summary>
    public int MaxThreads { get; set; }
    /// <summary>Max nested threads within a single window's work. 0 = Environment.ProcessorCount (set by ctor).</summary>
    public int MaxNestedThreads { get; set; }
    /// <summary>If true, parallelize over DIA windows; else parallelize within each window.</summary>
    public bool MultithreadOverWindows { get; set; } = true;

    /// <summary>
    /// In-memory mode: spill data stays in <see cref="MSData"/> instances rather than
    /// hitting a file. Differs from cpp, which defaults to mz5 spill files; pwiz-sharp
    /// uses memory for now (mz5 write isn't ported). If/when memory becomes an issue,
    /// add a per-window mzMLb spill (we have the writer).
    /// </summary>
    public WriteFormat SpillFileFormat { get; set; } = WriteFormat.MzMLb;

    /// <summary>Default ctor with TTOF5600 baseline params.</summary>
    public Config() : this(string.Empty) { }

    /// <summary>Loads parameters from a DIA-Umpire <c>.params</c> file (empty path = TTOF5600 defaults).</summary>
    public Config(string paramsFilepath)
    {
        if (!string.IsNullOrEmpty(paramsFilepath) && !File.Exists(paramsFilepath))
            throw new FileNotFoundException(
                $"[DiaUmpire.Config] params file \"{paramsFilepath}\" does not exist", paramsFilepath);

        // TTOF5600 defaults (matches cpp Config::Config baseline before file overlay).
        var p = InstrumentParameters;
        p.MS1PPM = 30;
        p.MS2PPM = 40;
        p.SN = 2f;
        p.MS2SN = 2f;
        p.MinMSIntensity = 5f;
        p.MinMSMSIntensity = 1f;
        p.MinRTRange = 0.1f;
        p.MaxNoPeakCluster = 4;
        p.MinNoPeakCluster = 2;
        p.MaxMS2NoPeakCluster = 4;
        p.MinMS2NoPeakCluster = 2;
        p.MaxCurveRTRange = 1.5f;
        p.Resolution = 17000;
        p.RTtol = 0.1f;
        p.Denoise = true;
        p.EstimateBG = true;
        p.RemoveGroupedPeaks = true;

        if (string.IsNullOrEmpty(paramsFilepath))
        {
            ApplyThreadDefaults();
            return;
        }

        Parse(paramsFilepath);
        ApplyThreadDefaults();
    }

    private void ApplyThreadDefaults()
    {
        if (MaxThreads == 0) MaxThreads = System.Math.Max(1, System.Environment.ProcessorCount / 2);
        if (MaxNestedThreads == 0) MaxNestedThreads = System.Environment.ProcessorCount;
    }

    private void Parse(string paramsFilepath)
    {
        using var reader = new StreamReader(paramsFilepath);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0 || line[0] == '#') continue;

            if (line == "==window setting begin")
            {
                while ((line = reader.ReadLine()) is not null && line != "==window setting end")
                {
                    if (line.Length == 0) continue;
                    var tokens = line.Split('\t');
                    if (tokens.Length != 2)
                        throw new FormatException(
                            $"Invalid variable window \"{line}\" — expected 2 tab-separated values (start and end m/z)");
                    DiaVariableWindows.Add(new TargetWindow(new MzRange(
                        float.Parse(tokens[0], CultureInfo.InvariantCulture),
                        float.Parse(tokens[1], CultureInfo.InvariantCulture))));
                }
                continue;
            }

            int eq = line.IndexOf('=');
            if (eq < 0) continue;
            string type = line[..eq].Trim();
            if (type.StartsWith("para.", System.StringComparison.Ordinal)) type = type[5..];
            if (type.StartsWith("SE.", System.StringComparison.Ordinal)) type = type[3..];
            string value = line[(eq + 1)..].Trim().ToLowerInvariant();

            ApplyOne(type, value);
        }
    }

    private void ApplyOne(string type, string value)
    {
        var p = InstrumentParameters;
        switch (type)
        {
            case "ExportPrecursorPeak": ExportMs1ClusterTable = ParseBool(value); break;
            case "ExportFragmentPeak":  ExportMs2ClusterTable = ParseBool(value); break;
            case "RPmax":               p.RPmax = int.Parse(value, CultureInfo.InvariantCulture); break;
            case "RFmax":               p.RFmax = int.Parse(value, CultureInfo.InvariantCulture); break;
            case "CorrThreshold":       p.CorrThreshold = float.Parse(value, CultureInfo.InvariantCulture); break;
            case "DeltaApex":           p.DeltaApex = float.Parse(value, CultureInfo.InvariantCulture); break;
            case "RTOverlap":           p.RTOverlap = float.Parse(value, CultureInfo.InvariantCulture); break;
            case "BoostComplementaryIon": p.BoostComplementaryIon = ParseBool(value); break;
            case "AdjustFragIntensity": p.AdjustFragIntensity = ParseBool(value); break;
            case "MS1PPM":              p.MS1PPM = float.Parse(value, CultureInfo.InvariantCulture); break;
            case "MS2PPM":              p.MS2PPM = float.Parse(value, CultureInfo.InvariantCulture); break;
            case "SN":                  p.SN = float.Parse(value, CultureInfo.InvariantCulture); break;
            case "MS2SN":               p.MS2SN = float.Parse(value, CultureInfo.InvariantCulture); break;
            case "MinMSIntensity":      p.MinMSIntensity = float.Parse(value, CultureInfo.InvariantCulture); break;
            case "MinMSMSIntensity":    p.MinMSMSIntensity = float.Parse(value, CultureInfo.InvariantCulture); break;
            case "MinRTRange":          p.MinRTRange = float.Parse(value, CultureInfo.InvariantCulture); break;
            case "MaxNoPeakCluster":
                // cpp applies this to both MS1 and MS2 max
                p.MaxNoPeakCluster = int.Parse(value, CultureInfo.InvariantCulture);
                p.MaxMS2NoPeakCluster = p.MaxNoPeakCluster;
                break;
            case "MinNoPeakCluster":
                p.MinNoPeakCluster = int.Parse(value, CultureInfo.InvariantCulture);
                p.MinMS2NoPeakCluster = p.MinNoPeakCluster;
                break;
            case "MinMS2NoPeakCluster": p.MinMS2NoPeakCluster = int.Parse(value, CultureInfo.InvariantCulture); break;
            case "MaxCurveRTRange":     p.MaxCurveRTRange = float.Parse(value, CultureInfo.InvariantCulture); break;
            case "Resolution":          p.Resolution = int.Parse(value, CultureInfo.InvariantCulture); break;
            case "RTtol":               p.RTtol = float.Parse(value, CultureInfo.InvariantCulture); break;
            case "NoPeakPerMin":        p.NoPeakPerMin = int.Parse(value, CultureInfo.InvariantCulture); break;
            case "StartCharge":         p.StartCharge = int.Parse(value, CultureInfo.InvariantCulture); break;
            case "EndCharge":           p.EndCharge = int.Parse(value, CultureInfo.InvariantCulture); break;
            case "MS2StartCharge":      p.MS2StartCharge = int.Parse(value, CultureInfo.InvariantCulture); break;
            case "MS2EndCharge":        p.MS2EndCharge = int.Parse(value, CultureInfo.InvariantCulture); break;
            case "NoMissedScan":        p.NoMissedScan = int.Parse(value, CultureInfo.InvariantCulture); break;
            case "Denoise":             p.Denoise = ParseBool(value); break;
            case "EstimateBG":          p.EstimateBG = ParseBool(value); break;
            case "RemoveGroupedPeaks":  p.RemoveGroupedPeaks = ParseBool(value); break;
            case "MinFrag":             p.MinFrag = int.Parse(value, CultureInfo.InvariantCulture); break;
            case "IsoPattern":          p.IsoPattern = float.Parse(value, CultureInfo.InvariantCulture); break;
            case "StartRT":             p.StartRT = float.Parse(value, CultureInfo.InvariantCulture); break;
            case "EndRT":               p.EndRT = float.Parse(value, CultureInfo.InvariantCulture); break;
            case "RemoveGroupedPeaksRTOverlap": p.RemoveGroupedPeaksRTOverlap = float.Parse(value, CultureInfo.InvariantCulture); break;
            case "RemoveGroupedPeaksCorr": p.RemoveGroupedPeaksCorr = float.Parse(value, CultureInfo.InvariantCulture); break;
            case "MinMZ":               p.MinMZ = float.Parse(value, CultureInfo.InvariantCulture); break;
            case "MinPrecursorMass":    p.MinPrecursorMass = float.Parse(value, CultureInfo.InvariantCulture); break;
            case "MaxPrecursorMass":    p.MaxPrecursorMass = float.Parse(value, CultureInfo.InvariantCulture); break;
            case "IsoCorrThreshold":    p.IsoCorrThreshold = float.Parse(value, CultureInfo.InvariantCulture); break;
            case "MassDefectFilter":    p.MassDefectFilter = ParseBool(value); break;
            case "MassDefectOffset":    p.MassDefectOffset = float.Parse(value, CultureInfo.InvariantCulture); break;
            case "WindowType":
                DiaTargetWindowScheme = value switch
                {
                    "swath" => TargetWindowScheme.SwathFixed,
                    "v_swath" => TargetWindowScheme.SwathVariable,
                    _ => throw new FormatException("Only SWATH and V_SWATH modes are supported for WindowType"),
                };
                break;
            case "WindowSize":          DiaFixedWindowSize = int.Parse(value, CultureInfo.InvariantCulture); break;
            case "Thread":              MaxThreads = int.Parse(value, CultureInfo.InvariantCulture); break;
            case "NestedThreads":       MaxNestedThreads = int.Parse(value, CultureInfo.InvariantCulture); break;
            case "MultithreadOverWindows": MultithreadOverWindows = ParseBool(value); break;
            case "SpillFileFormat":
                SpillFileFormat = value switch
                {
                    "mzml" => WriteFormat.Mzml,
                    "mzmlb" => WriteFormat.MzMLb,
                    // cpp accepts "mz5" but pwiz-sharp doesn't have an mz5 writer; coerce to mzMLb.
                    "mz5" => WriteFormat.MzMLb,
                    _ => throw new FormatException("Only mzML and mzMLb are supported spill file formats in pwiz-sharp"),
                };
                break;
            // Unknown keys are ignored (matches cpp's loop behavior).
        }
    }

    private static bool ParseBool(string value) => value switch
    {
        "true" or "1" => true,
        "false" or "0" => false,
        _ => bool.Parse(value),
    };
}
