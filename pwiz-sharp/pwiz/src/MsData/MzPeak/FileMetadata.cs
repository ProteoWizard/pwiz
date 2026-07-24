using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pwiz.Data.MsData.MzPeak;

/// <summary>
/// File-level metadata records — deserialized from the Parquet KV JSON strings in
/// spectra_metadata.parquet / chromatograms_metadata.parquet. The mzPeak writer
/// emits these once per file (not per row) because they describe the whole run:
/// instrument config, software stack, sample info, source files, etc.
/// </summary>
public sealed record FileMetadata(
    FileDescription FileDescription,
    IReadOnlyList<InstrumentConfiguration> InstrumentConfigurations,
    IReadOnlyList<DataProcessingMethod> DataProcessingMethods,
    IReadOnlyList<SoftwareInfo> Software,
    IReadOnlyList<SampleInfo> Samples,
    RunInfo Run,
    long SpectrumCount,
    long SpectrumDataPointCount,
    // Document-level mzML id (MSData.Id). Round-trips the <mzML id="..."> attribute; cross-stack
    // readers that don't model it simply ignore the extra KV entry.
    string? DocumentId = null,
    // referenceableParamGroupList: shared CV-param bundles spectra/scans reference by id.
    IReadOnlyList<ParamGroupInfo>? ParamGroups = null);

public sealed record FileDescription(
    IReadOnlyList<MzPeakCvParam> Contents,
    IReadOnlyList<SourceFile> SourceFiles);

public sealed record SourceFile(
    string Id,
    string Name,
    string? Location,
    IReadOnlyList<MzPeakCvParam> Parameters);

public sealed record InstrumentConfiguration(
    int Id,
    IReadOnlyList<ComponentInfo> Components,
    string? SoftwareReference,
    IReadOnlyList<MzPeakCvParam> Parameters,
    // pwiz's instrument-configuration id is an arbitrary string (e.g. "LCQ Deca"); the columnar
    // <c>Id</c> above is the cross-stack integer index. OriginalId preserves the string so the
    // round-trip restores the real id and every scan/run reference that points at it.
    string? OriginalId = null,
    // referenceableParamGroup ids this instrument configuration references.
    IReadOnlyList<string>? ParamGroupRefs = null);

/// <summary>One source / analyzer / detector in an instrument's component chain.</summary>
public sealed record ComponentInfo(
    string Type,                                  // "source" | "analyzer" | "detector"
    int Order,
    IReadOnlyList<MzPeakCvParam> Parameters);

public sealed record DataProcessingMethod(
    string Id,
    IReadOnlyList<ProcessingMethodInfo> Methods);

/// <summary>One ordered processing step within a <see cref="DataProcessingMethod"/>.</summary>
public sealed record ProcessingMethodInfo(
    int Order,
    string? SoftwareReference,
    IReadOnlyList<MzPeakCvParam> Parameters);

/// <summary>A referenceableParamGroup: an id plus the CV/user params it bundles.</summary>
public sealed record ParamGroupInfo(
    string Id,
    IReadOnlyList<MzPeakCvParam> Parameters);

/// <summary>
/// One auxiliary binary/integer data array on a spectrum or chromatogram — i.e. an array beyond
/// the canonical m/z+intensity (spectrum) or time+intensity (chromatogram) pair: ion-mobility
/// arrays, "ms level" non-standard arrays, resolution/baseline/SN arrays, etc. <see cref="Params"/>
/// carries the identifying CV/user params (array-type term, name, unit) minus the binary-encoding
/// terms. Exactly one of <see cref="DoubleValues"/> / <see cref="IntValues"/> is populated per
/// <see cref="IsInteger"/>. Serialized as a JSON list in a per-row <c>auxiliary_arrays</c> column.
/// </summary>
public sealed record AuxiliaryArrayData(
    IReadOnlyList<MzPeakCvParam> Params,
    bool IsInteger,
    IReadOnlyList<double>? DoubleValues,
    IReadOnlyList<long>? IntValues);

/// <summary>JSON (de)serialization for the per-row auxiliary_arrays column.</summary>
public static class AuxiliaryArrays
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Serialize a list of auxiliary arrays to a JSON string, or null when empty.</summary>
    public static string? Serialize(IReadOnlyList<AuxiliaryArrayData>? arrays) =>
        arrays is null || arrays.Count == 0 ? null : JsonSerializer.Serialize(arrays, Opts);

    /// <summary>Parse the JSON string back into auxiliary arrays; null/empty → empty list.</summary>
    public static IReadOnlyList<AuxiliaryArrayData> Parse(string? json) =>
        string.IsNullOrEmpty(json)
            ? System.Array.Empty<AuxiliaryArrayData>()
            : JsonSerializer.Deserialize<List<AuxiliaryArrayData>>(json, Opts) ?? new List<AuxiliaryArrayData>();
}

/// <summary>
/// One extra scan (beyond scan[0]) of a spectrum's scanList — used for combined ion-mobility
/// spectra whose scanList holds one scan per mobility bin. Carries the scan's full CV/user params
/// and its scan windows (each a param list). scan[0] still rides the typed columns.
/// </summary>
public sealed record ExtraScanData(
    IReadOnlyList<MzPeakCvParam> Params,
    IReadOnlyList<IReadOnlyList<MzPeakCvParam>> ScanWindows,
    // The scan's spectrumRef (source spectrum for a combined scan), if any.
    string? SpectrumId = null);

/// <summary>JSON (de)serialization for the per-spectrum extra-scans column.</summary>
public static class ExtraScans
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Serialize extra scans to JSON, or null when there are none.</summary>
    public static string? Serialize(IReadOnlyList<ExtraScanData>? scans) =>
        scans is null || scans.Count == 0 ? null : JsonSerializer.Serialize(scans, Opts);

    /// <summary>Parse extra scans; null/empty → empty list.</summary>
    public static IReadOnlyList<ExtraScanData> Parse(string? json) =>
        string.IsNullOrEmpty(json)
            ? System.Array.Empty<ExtraScanData>()
            : JsonSerializer.Deserialize<List<ExtraScanData>>(json, Opts) ?? new List<ExtraScanData>();
}

/// <summary>
/// JSON (de)serialization for per-scan scan-window free-form params: a list (per scan window, in
/// order) of the window's CV/user params beyond the typed lower/upper limit columns.
/// </summary>
public static class ScanWindowParams
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Serialize per-window param lists; null when every window's list is empty.</summary>
    public static string? Serialize(IReadOnlyList<IReadOnlyList<MzPeakCvParam>>? windows) =>
        windows is null || windows.Count == 0 || windows.All(w => w.Count == 0)
            ? null
            : JsonSerializer.Serialize(windows, Opts);

    /// <summary>Parse back to per-window param lists; null/empty → empty list.</summary>
    public static IReadOnlyList<IReadOnlyList<MzPeakCvParam>> Parse(string? json)
    {
        if (string.IsNullOrEmpty(json)) return System.Array.Empty<IReadOnlyList<MzPeakCvParam>>();
        // IReadOnlyList<T> is covariant, so List<List<T>> satisfies IReadOnlyList<IReadOnlyList<T>>.
        return JsonSerializer.Deserialize<List<List<MzPeakCvParam>>>(json, Opts)
               ?? (IReadOnlyList<IReadOnlyList<MzPeakCvParam>>)System.Array.Empty<IReadOnlyList<MzPeakCvParam>>();
    }
}

public sealed record SoftwareInfo(
    string Id,
    string? Version,
    IReadOnlyList<MzPeakCvParam> Parameters);

public sealed record SampleInfo(
    string Id,
    string? Name,
    IReadOnlyList<MzPeakCvParam> Parameters);

public sealed record RunInfo(
    string Id,
    string? DefaultDataProcessingId,
    int? DefaultInstrumentId,           // null when the run has no default instrument configuration
    string? DefaultSourceFileId,
    string? StartTime,
    IReadOnlyList<MzPeakCvParam> Parameters);

/// <summary>
/// CvParam shape used in the file-level JSON blocks. Distinct from the columnar
/// Phase 2.5 <see cref="MzPeakReader.CvParam"/> because the JSON form has a single
/// polymorphic <c>value</c> field, whereas the Parquet columnar form splits it
/// into four typed columns (value.string / value.integer / value.float /
/// value.boolean). Same logical semantics, different on-disk encoding.
/// </summary>
public sealed record MzPeakCvParam(
    string? Name,
    string? Accession,
    [property: JsonConverter(typeof(PolymorphicValueConverter))] object? Value,
    string? Unit);

/// <summary>
/// Maps the JSON value union (string | integer | floating | boolean | null) to
/// a concrete .NET type. We don't preserve a wrapping JsonElement — the caller
/// usually just wants the value, not the JSON kind.
/// </summary>
internal sealed class PolymorphicValueConverter : JsonConverter<object?>
{
    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.Number => reader.TryGetInt64(out var i) ? (object)i : reader.GetDouble(),
            _ => throw new JsonException($"Unexpected token {reader.TokenType} for CvParam value"),
        };

    public override void Write(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case null: writer.WriteNullValue(); break;
            case string s: writer.WriteStringValue(s); break;
            case bool b: writer.WriteBooleanValue(b); break;
            case long l: writer.WriteNumberValue(l); break;
            case int i: writer.WriteNumberValue(i); break;
            case double d: writer.WriteNumberValue(d); break;
            case float f: writer.WriteNumberValue(f); break;
            default: throw new JsonException($"Unsupported CvParam value type {value.GetType()}");
        }
    }
}

internal static class FileMetadataDeserializer
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Parse the KV strings from a parquet file's KeyValueMetadata into one
    /// typed FileMetadata record. Throws on malformed input — these blocks
    /// are written by the same writer that wrote the schema, so failures
    /// here are programmer / corruption errors, not user input.
    /// </summary>
    public static FileMetadata Parse(IReadOnlyDictionary<string, string> kv)
    {
        return new FileMetadata(
            FileDescription:           Get<FileDescription>(kv, "file_description"),
            InstrumentConfigurations:  Get<IReadOnlyList<InstrumentConfiguration>>(kv, "instrument_configuration_list"),
            DataProcessingMethods:     Get<IReadOnlyList<DataProcessingMethod>>(kv, "data_processing_method_list"),
            Software:                  Get<IReadOnlyList<SoftwareInfo>>(kv, "software_list"),
            Samples:                   Get<IReadOnlyList<SampleInfo>>(kv, "sample_list"),
            Run:                       Get<RunInfo>(kv, "run"),
            SpectrumCount:             GetLong(kv, "spectrum_count"),
            SpectrumDataPointCount:    GetLong(kv, "spectrum_data_point_count"),
            DocumentId:                GetStringOrNull(kv, "document_id"),
            ParamGroups:               GetOrNull<IReadOnlyList<ParamGroupInfo>>(kv, "referenceable_param_group_list"));
    }

    private static T Get<T>(IReadOnlyDictionary<string, string> kv, string key) =>
        JsonSerializer.Deserialize<T>(kv[key], JsonOpts)
        ?? throw new InvalidDataException($"Required KV '{key}' deserialized to null.");

    private static T? GetOrNull<T>(IReadOnlyDictionary<string, string> kv, string key) where T : class =>
        kv.TryGetValue(key, out var json) ? JsonSerializer.Deserialize<T>(json, JsonOpts) : null;

    private static string? GetStringOrNull(IReadOnlyDictionary<string, string> kv, string key) =>
        kv.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v) ? v : null;

    private static long GetLong(IReadOnlyDictionary<string, string> kv, string key) =>
        long.Parse(kv[key], System.Globalization.CultureInfo.InvariantCulture);
}
