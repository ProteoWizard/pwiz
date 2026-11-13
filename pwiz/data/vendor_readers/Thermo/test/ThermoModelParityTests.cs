using System.Globalization;
using Pwiz.Data.Common.Cv;
using Pwiz.Vendor.Thermo;

namespace Pwiz.Vendor.Thermo.Tests;

/// <summary>
/// Asserts the C# Thermo reader's model-keyed translation functions return byte-equal
/// results to cpp pwiz on every <c>InstrumentModelType</c>. The reference TSV is generated
/// by <c>pwiz/data/vendor_readers/Thermo/Reader_Thermo_Test.cpp</c> at cpp test time and
/// checked in as <c>ThermoModelReference.tsv</c>.
/// </summary>
/// <remarks>
/// <para>The cpp test iterates every <c>InstrumentModelType</c> enum value and dumps
/// one row per (model, kind, index) tuple:</para>
/// <list type="bullet">
///   <item><c>cv</c>: <c>translateAsInstrumentModel(model)</c> — the marketing-name CV term.</item>
///   <item><c>ic</c>: <c>createInstrumentConfigurations(commonSource, model)</c> — one row per IC,
///         with the source component stripped (added per-run from the first scan's ionization).</item>
///   <item><c>ion</c>: <c>getIonSourcesForInstrumentModel(model)</c> — supported ionization types.</item>
///   <item><c>analyzer</c>: <c>getMassAnalyzersForInstrumentModel(model)</c> — supported analyzers.</item>
///   <item><c>detector</c>: <c>getDetectorsForInstrumentModel(model)</c> — supported detectors.</item>
/// </list>
/// <para>The C# port only consumes the <c>cv</c> and <c>ic</c> outputs (it derives ionization
/// per scan and analyzer per scan rather than as static model lists). The other rows are
/// preserved in the TSV for future use and ignored here. When cpp adds an instrument model
/// or changes a recipe, re-run <c>Reader_Thermo_Test.cpp</c> to regenerate the TSV and this
/// test will surface the divergence.</para>
/// </remarks>
[TestClass]
public class ThermoModelParityTests
{
    private static string FindReference()
    {
        // The TSV ships next to the test assembly (csproj None Include + CopyToOutputDirectory).
        string path = Path.Combine(AppContext.BaseDirectory, "ThermoModelReference.tsv");
        if (!File.Exists(path))
            Assert.Inconclusive($"ThermoModelReference.tsv not found at {path}. " +
                "Rebuild Reader_Thermo_Test.cpp to regenerate it.");
        return path;
    }

    private sealed record ModelRow(string EnumName, string Kind, int Index, string Values);

    private static IEnumerable<ModelRow> ParseReference(string path)
    {
        foreach (var line in File.ReadLines(path))
        {
            if (line.Length == 0 || line[0] == '#') continue;
            var parts = line.Split('\t');
            if (parts.Length != 4) continue;
            yield return new ModelRow(
                EnumName: parts[0],
                Kind: parts[1],
                Index: int.Parse(parts[2], CultureInfo.InvariantCulture),
                Values: parts[3]);
        }
    }

    /// <summary>For every model the cpp test enumerates, assert
    /// <see cref="ThermoInstrumentModel.Translate"/> returns the same CV term that cpp's
    /// <c>translateAsInstrumentModel</c> did. The cpp side dumps the enum's canonical name
    /// (from <c>nameToModelMapping</c>); our <see cref="ThermoInstrumentModel.Translate"/> is
    /// the inverse mapping (string → CVID).</summary>
    [TestMethod]
    public void TranslateInstrumentModel_MatchesCpp()
    {
        string referencePath = FindReference();
        var failures = new List<string>();
        foreach (var row in ParseReference(referencePath).Where(r => r.Kind == "cv"))
        {
            CVID actual = ThermoInstrumentModel.Translate(row.EnumName);
            string actualName = actual == CVID.CVID_Unknown
                ? "Unknown"
                : CvLookup.CvTermInfo(actual).Name;
            if (actualName != row.Values)
                failures.Add($"  {row.EnumName}: cpp='{row.Values}' vs sharp='{actualName}'");
        }
        if (failures.Count > 0)
            Assert.Fail($"TranslateInstrumentModel diverges on {failures.Count} model(s):\n"
                + string.Join("\n", failures));
    }

    /// <summary>For every model, assert
    /// <c>Reader_Thermo.GetInstrumentConfigurationComponents</c> returns the same per-IC
    /// component chain (CVID list, source excluded) that cpp's
    /// <c>createInstrumentConfigurations</c> emits. Models that yield zero ICs in cpp (the
    /// inactive Tempus_TOF / Neptune / Triton legacy slots) are tolerated as zero ICs in
    /// C# too — both implementations decline to produce a configuration there.</summary>
    [TestMethod]
    public void InstrumentConfigurations_MatchCpp()
    {
        string referencePath = FindReference();

        // Group "ic" rows by enum_name → ordered list of component-CSV strings (one per IC).
        var cppByModel = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var row in ParseReference(referencePath).Where(r => r.Kind == "ic"))
        {
            if (!cppByModel.TryGetValue(row.EnumName, out var list))
                cppByModel[row.EnumName] = list = new List<string>();
            // Index gaps shouldn't happen but tolerate them with padding.
            while (list.Count <= row.Index) list.Add(string.Empty);
            list[row.Index] = row.Values;
        }

        var failures = new List<string>();
        foreach (var (enumName, cppRecipe) in cppByModel)
        {
            CVID modelCv = ThermoInstrumentModel.Translate(enumName);
            var sharpRecipe = Reader_Thermo.GetInstrumentConfigurationComponents(modelCv);
            // cpp emits an "empty IC" row (no components) for Tempus_TOF / Neptune / Triton.
            // Sharp's GetInstrumentConfigurationComponents returns Count==0 for those models.
            // Treat both as equivalent.
            bool cppHasContent = cppRecipe.Any(s => !string.IsNullOrEmpty(s));
            if (!cppHasContent && sharpRecipe.Count == 0) continue;

            if (sharpRecipe.Count != cppRecipe.Count)
            {
                failures.Add($"  {enumName}: cpp has {cppRecipe.Count} IC(s), sharp has {sharpRecipe.Count}");
                continue;
            }
            for (int i = 0; i < sharpRecipe.Count; i++)
            {
                string sharpCsv = string.Join(",", sharpRecipe[i].Select(c => CvLookup.CvTermInfo(c).Name));
                if (sharpCsv != cppRecipe[i])
                    failures.Add($"  {enumName} IC#{i}:\n      cpp='{cppRecipe[i]}'\n    sharp='{sharpCsv}'");
            }
        }
        if (failures.Count > 0)
            Assert.Fail($"createInstrumentConfigurations diverges on {failures.Count} entry(ies):\n"
                + string.Join("\n", failures));
    }
}
