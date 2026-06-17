using System.IO.Compression;
using System.Text.Json;
using ParquetSharp;
using ParquetSharp.Schema;

namespace Pwiz.Data.MsData.MzPeak;

/// <summary>
/// Phase 6+ writer using ParquetSharp's GroupNode schema API. Critical for
/// cross-stack compatibility: ParquetSharp's `Column&lt;T&gt;("foo.bar")` shortcut
/// produces a flat leaf with a literal-dot name (so Arrow sees one column called
/// "foo.bar"), but the mzPeak spec — and every other mzPeak consumer
/// (mzPeak.NET, pyarrow, Rust mzdata) — expects nested group structures, so
/// Arrow sees one Struct column called "foo" containing a field "bar".
///
/// This writer builds explicit GroupNode trees per file: each top-level entity
/// (spectrum, scan, precursor, selected_ion, chromatogram, point) is an Optional
/// GroupNode, with its CV-encoded leaves and parameter lists nested inside.
/// List columns use the 3-level Apache Arrow encoding (optional outer LIST →
/// repeated `list` group → optional `element`).
/// </summary>
public sealed class MzPeakWriter
{
    /// <summary>
    /// One precursor of a spectrum. Multi-precursor spectra carry several of
    /// these; mzPeak.NET reads precursors as a separate row-set keyed by
    /// <c>source_index</c> (the parent spectrum's row index), with
    /// <c>precursor_index</c> distinguishing siblings. The writer fans each
    /// spectrum into <c>max(1, Precursors.Count)</c> metadata rows so every
    /// precursor lands in its own row.
    /// </summary>
    public sealed record PrecursorToWrite(
        string? PrecursorId = null,
        double? IsolationTargetMz = null, double? IsolationLowerOffset = null, double? IsolationUpperOffset = null,
        double? CollisionEnergy = null, string? DissociationMethodCurie = null,
        // One selected ion per precursor (mzPeak supports lists; we model the common 1:1 case).
        double? SelectedIonMz = null, double? SelectedIonPeakIntensity = null, long? SelectedIonChargeState = null,
        double? SelectedIonIonMobilityValue = null, string? SelectedIonIonMobilityTypeCurie = null,
        IReadOnlyList<MzPeakReader.CvParam>? IsolationWindowParameters = null,
        IReadOnlyList<MzPeakReader.CvParam>? ActivationParameters = null,
        IReadOnlyList<MzPeakReader.CvParam>? SelectedIonParameters = null);

    public sealed record SpectrumToWrite(
        ulong Index, string Id, double Time, int? MsLevel, bool IsProfile,
        double[] Mz, float[] Intensity,
        // Scan scalars
        double? ScanStartTime = null, string? FilterString = null,
        uint? InstrumentConfigurationRef = null, double? IonInjectionTime = null,
        // scan[0] spectrumRef (source spectrum for a combined scan), if any.
        string? ScanSpectrumRef = null,
        // Lists
        double?[]? ScanWindowLowerLimits = null, double?[]? ScanWindowUpperLimits = null,
        // JSON of per-scan-window free-form params beyond the lower/upper limit columns.
        string? ScanWindowParamsJson = null,
        // JSON of scanList scans beyond scan[0] (combined ion-mobility spectra: one per mobility bin).
        string? ExtraScansJson = null,
        IReadOnlyList<MzPeakReader.CvParam>? SpectrumParameters = null,
        IReadOnlyList<MzPeakReader.CvParam>? ScanParameters = null,
        // Supplementary peaks
        double[]? SupplementaryPeaksMz = null, float[]? SupplementaryPeaksIntensity = null,
        // Spectrum-level CV-encoded scalars
        int? ScanPolarity = null,                                  // MS:1000465 (Int8 — +1 / -1)
        string? SpectrumTypeCurie = null,                          // MS:1000559
        long? NumberOfDataPoints = null,                           // MS:1003060 (defaults to Mz.Length)
        long? NumberOfPeaks = null,                                // MS:1003059 (defaults to SupplementaryPeaksMz?.Length)
        double? BasePeakMz = null,                                 // MS:1000504
        double? BasePeakIntensity = null,                          // MS:1000505
        double? TotalIonCurrent = null,                            // MS:1000285
        double? LowestObservedMz = null,                           // MS:1000528
        double? HighestObservedMz = null,                          // MS:1000527
        string? SpectrumDataProcessingRef = null,
        int? NumberOfAuxiliaryArrays = null,
        double?[]? MzDeltaModel = null,
        // Scan linking + IMS
        ulong? ScanSourceIndex = null,                             // defaults to Index
        double? ScanIonMobilityValue = null,
        string? ScanIonMobilityTypeCurie = null,
        long? PresetScanConfiguration = null,                      // MS:1000616
        // scanList combination method CURIE (MS:1000570 children, e.g. MS:1000795 no combination).
        string? ScanCombinationCurie = null,
        // referenceableParamGroup ids this spectrum references.
        IReadOnlyList<string>? ParamGroupRefs = null,
        // False when the source spectrum carries NEITHER profile nor centroid representation CV
        // (so the reader emits no representation term instead of defaulting to one). Defaults true.
        bool HasRepresentation = true,
        // Non-null when the spectrum's value array isn't m/z (e.g. UV/DAD wavelength array): the
        // array-type CURIE + its unit CURIE, so the reader rebuilds the right value array.
        string? ValueArrayCurie = null,
        string? ValueArrayUnitCurie = null,
        // JSON of auxiliary (non-m/z, non-intensity) binary/integer arrays on this spectrum.
        string? AuxiliaryArraysJson = null,
        // Zero or more precursors; writer fans these into separate rows.
        IReadOnlyList<PrecursorToWrite>? Precursors = null);

    /// <summary>
    /// Internal fan-out row: one row per (spectrum, precursor). Spectrum-level
    /// and scan-level columns are written only on <see cref="IsPrimary"/> rows
    /// (the first row for each spectrum); extra precursor rows get null in
    /// those groups but a populated precursor + selected_ion group.
    /// </summary>
    private sealed record Row(SpectrumToWrite Spectrum, PrecursorToWrite? Precursor, int PrecursorIdx, bool IsPrimary);

    public sealed record ChromatogramToWrite(
        ulong Index, string Id, string? ChromatogramTypeCurie, string? DataProcessingRef,
        double[] Time, float[] Intensity,
        // Unit CURIE of the time array (e.g. UO:0000010 second / UO:0000031 minute). mzML lets this
        // vary, and the point-layout time column can't carry it, so it travels as its own column.
        string? TimeUnitCurie = null,
        // Unit CURIE of the intensity array (counts / % / psi / µL·min⁻¹ / pascal — varies widely).
        string? IntensityUnitCurie = null,
        // Free-form chromatogram-level CV/user params not represented by a typed column (polarity, …).
        IReadOnlyList<MzPeakReader.CvParam>? Parameters = null,
        // JSON of auxiliary (non-time, non-intensity) binary/integer arrays (e.g. the "ms level" int array).
        string? AuxiliaryArraysJson = null);

    private const string CurieProfileSpectrum = "MS:1000128";
    private const string CurieCentroidSpectrum = "MS:1000127";

    public static void Write(
        string outputPath,
        IReadOnlyList<SpectrumToWrite> spectra,
        FileMetadata fileMetadata,
        IReadOnlyList<ChromatogramToWrite>? chromatograms = null)
    {
        chromatograms ??= Array.Empty<ChromatogramToWrite>();
        var stagingDir = Path.Combine(Path.GetTempPath(), $"mzpeak-write-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingDir);
        try
        {
            WriteSpectraMetadata(Path.Combine(stagingDir, "spectra_metadata.parquet"), spectra, fileMetadata);
            WriteSpectraData(Path.Combine(stagingDir, "spectra_data.parquet"), spectra);

            bool hasSupplementary = spectra.Any(s => s.SupplementaryPeaksMz is { Length: > 0 });
            if (hasSupplementary)
                WriteSpectraPeaks(Path.Combine(stagingDir, "spectra_peaks.parquet"), spectra);
            if (chromatograms.Count > 0)
            {
                WriteChromatogramsMetadata(Path.Combine(stagingDir, "chromatograms_metadata.parquet"), chromatograms);
                WriteChromatogramsData(Path.Combine(stagingDir, "chromatograms_data.parquet"), chromatograms);
            }
            WriteManifest(Path.Combine(stagingDir, "mzpeak_index.json"), chromatograms.Count > 0, hasSupplementary);

            if (File.Exists(outputPath)) File.Delete(outputPath);
            ZipFile.CreateFromDirectory(stagingDir, outputPath, CompressionLevel.NoCompression, includeBaseDirectory: false);
        }
        finally
        {
            try { Directory.Delete(stagingDir, recursive: true); } catch { /* best-effort */ }
        }
    }

    // ---------------- Schema builder helpers ----------------

    private static PrimitiveNode StringLeaf(string name)  => new(name, Repetition.Optional, LogicalType.String(),                  PhysicalType.ByteArray);
    private static PrimitiveNode DoubleLeaf(string name)  => new(name, Repetition.Optional, LogicalType.None(),                    PhysicalType.Double);
    private static PrimitiveNode FloatLeaf(string name)   => new(name, Repetition.Optional, LogicalType.None(),                    PhysicalType.Float);
    private static PrimitiveNode BoolLeaf(string name)    => new(name, Repetition.Optional, LogicalType.None(),                    PhysicalType.Boolean);
    private static PrimitiveNode LongLeaf(string name)    => new(name, Repetition.Optional, LogicalType.None(),                    PhysicalType.Int64);
    private static PrimitiveNode UInt64Leaf(string name)  => new(name, Repetition.Optional, LogicalType.Int(64, isSigned: false),  PhysicalType.Int64);
    private static PrimitiveNode UInt32Leaf(string name)  => new(name, Repetition.Optional, LogicalType.Int(32, isSigned: false),  PhysicalType.Int32);
    private static PrimitiveNode Int8Leaf(string name)    => new(name, Repetition.Optional, LogicalType.Int(8,  isSigned: true),   PhysicalType.Int32);

    private static GroupNode Struct(string name, params Node[] children) => new(name, Repetition.Optional, children);

    /// <summary>
    /// 3-level Apache Arrow LIST encoding:
    ///   optional group {name} (LIST) {
    ///     repeated group list {
    ///       optional group element { ...elementFields }
    ///     }
    ///   }
    /// </summary>
    private static GroupNode ListOf(string name, params Node[] elementFields)
    {
        var element = new GroupNode("element", Repetition.Optional, elementFields);
        var list = new GroupNode("list", Repetition.Repeated, new Node[] { element });
        return new GroupNode(name, Repetition.Optional, new Node[] { list }, LogicalType.List());
    }

    /// <summary>
    /// LIST encoding where the element is a leaf primitive directly (not a wrapper
    /// struct). Used for plain "list of doubles" / "list of strings" columns like
    /// spectrum.mz_delta_model.
    /// </summary>
    private static GroupNode ListOfLeaf(string name, PrimitiveNode elementLeaf)
    {
        var list = new GroupNode("list", Repetition.Repeated, new Node[] { elementLeaf });
        return new GroupNode(name, Repetition.Optional, new Node[] { list }, LogicalType.List());
    }

    private static GroupNode ListOfDoubleLeaf(string name) =>
        ListOfLeaf(name, DoubleLeaf("element"));

    /// <summary>The 5 CV-param leaves inside a parameters list element.</summary>
    private static Node[] CvParamElementFields() => new Node[]
    {
        StringLeaf("name"),
        StringLeaf("accession"),
        Struct("value", StringLeaf("string"), LongLeaf("integer"), DoubleLeaf("float"), BoolLeaf("boolean")),
        StringLeaf("unit"),
        StringLeaf("type"),    // xsd datatype hint for userParams (e.g. "xsd:float")
    };

    private static GroupNode CvParamList(string name = "parameters") => ListOf(name, CvParamElementFields());

    // ---------------- Spectra metadata ----------------

    private static PrimitiveNode Int32Leaf(string name)   => new(name, Repetition.Optional, LogicalType.None(), PhysicalType.Int32);

    private static GroupNode BuildSpectraMetadataSchema() => new(
        "schema", Repetition.Required,
        new Node[]
        {
            Struct("spectrum",
                UInt64Leaf("index"),
                StringLeaf("id"),
                DoubleLeaf("time"),
                Int8Leaf("MS_1000511_ms_level"),
                StringLeaf("MS_1000525_spectrum_representation"),
                Int8Leaf("MS_1000465_scan_polarity"),
                StringLeaf("MS_1000559_spectrum_type"),
                LongLeaf("MS_1003060_number_of_data_points"),
                LongLeaf("MS_1003059_number_of_peaks"),
                DoubleLeaf("MS_1000504_base_peak_mz_unit_MS_1000040"),
                DoubleLeaf("MS_1000505_base_peak_intensity_unit_MS_1000131"),
                DoubleLeaf("MS_1000285_total_ion_current_unit_MS_1000131"),
                DoubleLeaf("MS_1000528_lowest_observed_mz_unit_MS_1000040"),
                DoubleLeaf("MS_1000527_highest_observed_mz_unit_MS_1000040"),
                CvParamList(),
                StringLeaf("data_processing_ref"),
                ListOfDoubleLeaf("mz_delta_model"),
                Int32Leaf("number_of_auxiliary_arrays"),
                StringLeaf("MS_1000570_spectrum_combination"),
                ListOfLeaf("param_group_refs", StringLeaf("element")),
                StringLeaf("auxiliary_arrays"),
                StringLeaf("value_array_type"),
                StringLeaf("value_array_unit")),
            Struct("scan",
                UInt64Leaf("source_index"),
                UInt32Leaf("instrument_configuration_ref"),
                DoubleLeaf("ion_mobility_value"),
                StringLeaf("ion_mobility_type"),
                DoubleLeaf("MS_1000016_scan_start_time_unit_UO_0000031"),
                StringLeaf("MS_1000512_filter_string"),
                LongLeaf("MS_1000616_preset_scan_configuration"),
                DoubleLeaf("MS_1000927_ion_injection_time_unit_UO_0000028"),
                StringLeaf("spectrum_ref"),
                CvParamList(),
                ListOf("scan_windows",
                    DoubleLeaf("MS_1000501_scan_window_lower_limit_unit_MS_1000040"),
                    DoubleLeaf("MS_1000500_scan_window_upper_limit_unit_MS_1000040")),
                StringLeaf("scan_window_params"),
                StringLeaf("extra_scans")),
            Struct("precursor",
                UInt64Leaf("source_index"),
                UInt64Leaf("precursor_index"),
                StringLeaf("precursor_id"),
                Struct("isolation_window",
                    DoubleLeaf("MS_1000827_isolation_window_target_mz_unit_MS_1000040"),
                    DoubleLeaf("MS_1000828_isolation_window_lower_offset_unit_MS_1000040"),
                    DoubleLeaf("MS_1000829_isolation_window_upper_offset_unit_MS_1000040"),
                    CvParamList()),
                Struct("activation",
                    DoubleLeaf("MS_1000045_collision_energy_unit_UO_0000266"),
                    StringLeaf("MS_1000044_dissociation_method"),
                    CvParamList())),
            Struct("selected_ion",
                UInt64Leaf("source_index"),
                UInt64Leaf("precursor_index"),
                DoubleLeaf("ion_mobility_value"),
                StringLeaf("ion_mobility_type"),
                DoubleLeaf("MS_1000744_selected_ion_mz_unit_MS_1000040"),
                DoubleLeaf("MS_1000042_peak_intensity_unit_MS_1000131"),
                LongLeaf("MS_1000041_charge_state"),
                CvParamList()),
        });

    /// <summary>
    /// Fan each spectrum into one row per precursor (≥1 row even when there are
    /// no precursors). The first row of each spectrum is the "primary" — it
    /// carries spectrum-level and scan-level columns; secondary rows have null
    /// in those groups and carry only precursor + selected_ion data. Legacy
    /// single-precursor fields on SpectrumToWrite synthesise one PrecursorToWrite
    /// when <c>Precursors</c> is null.
    /// </summary>
    private static List<Row> BuildRows(IReadOnlyList<SpectrumToWrite> spectra)
    {
        var rows = new List<Row>(spectra.Count);
        foreach (var s in spectra)
        {
            var precs = s.Precursors;
            if (precs == null || precs.Count == 0)
            {
                rows.Add(new Row(s, null, 0, IsPrimary: true));
                continue;
            }
            for (int i = 0; i < precs.Count; i++)
                rows.Add(new Row(s, precs[i], i, IsPrimary: i == 0));
        }
        return rows;
    }

    // Group-presence predicates (passed to column writers; null outer Nested when false).
    private static readonly Func<Row, bool> Primary = r => r.IsPrimary;
    private static readonly Func<Row, bool> HasPrec = r => r.Precursor != null;

    private static void WriteSpectraMetadata(string path, IReadOnlyList<SpectrumToWrite> spectra, FileMetadata fileMetadata)
    {
        var schema = BuildSpectraMetadataSchema();
        var kv = BuildKeyValueMetadata(fileMetadata, spectra.Sum(s => (long)s.Mz.Length));

        using var props = new WriterPropertiesBuilder().Compression(Compression.Zstd).Build();
        using var fileWriter = new ParquetFileWriter(path, schema, props, keyValueMetadata: kv);

        // Split into row groups on spectrum (primary-row) boundaries so a spectrum's fan-out rows
        // never span groups — lets the reader lazily load just the group(s) covering a spectrum.
        var rows = BuildRows(spectra);
        foreach (var (rgStart, rgLen) in ChunkRowsOnPrimary(rows))
        {
            using var rg = fileWriter.AppendRowGroup();
            WriteSpectraRowGroupColumns(rg, rows.GetRange(rgStart, rgLen));
        }
        fileWriter.Close();
    }

    /// <summary>Target fan-out rows per spectra_metadata row group (split only on spectrum boundaries).</summary>
    private const int TargetRowsPerMetaGroup = 5000;

    private static IEnumerable<(int start, int len)> ChunkRowsOnPrimary(IReadOnlyList<Row> rows)
    {
        int n = rows.Count;
        if (n == 0) { yield return (0, 0); yield break; }
        int start = 0;
        while (start < n)
        {
            int end = Math.Min(n, start + TargetRowsPerMetaGroup);
            while (end < n && !rows[end].IsPrimary) end++;   // keep a spectrum's fan-out rows together
            yield return (start, end - start);
            start = end;
        }
    }

    private static void WriteSpectraRowGroupColumns(RowGroupWriter rg, IReadOnlyList<Row> rows)
    {
        // -------- spectrum group (primary rows only) --------
        WriteScalar(rg, rows, Primary, r => (ulong?)r.Spectrum.Index);
        WriteScalar(rg, rows, Primary, r => (string?)r.Spectrum.Id);
        WriteScalar(rg, rows, Primary, r => (double?)r.Spectrum.Time);
        WriteScalar(rg, rows, Primary, r => r.Spectrum.MsLevel is int m ? (sbyte?)checked((sbyte)m) : null);
        WriteScalar(rg, rows, Primary, r => !r.Spectrum.HasRepresentation ? null : (r.Spectrum.IsProfile ? CurieProfileSpectrum : CurieCentroidSpectrum));
        WriteScalar(rg, rows, Primary, r => r.Spectrum.ScanPolarity is int sp ? (sbyte?)checked((sbyte)sp) : null);
        WriteScalar(rg, rows, Primary, r => r.Spectrum.SpectrumTypeCurie);
        WriteScalar(rg, rows, Primary, r => (long?)(r.Spectrum.NumberOfDataPoints ?? r.Spectrum.Mz.Length));
        WriteScalar(rg, rows, Primary, r => r.Spectrum.NumberOfPeaks ?? (r.Spectrum.SupplementaryPeaksMz?.Length is int n ? (long?)n : null));
        WriteScalar(rg, rows, Primary, r => r.Spectrum.BasePeakMz);
        WriteScalar(rg, rows, Primary, r => r.Spectrum.BasePeakIntensity);
        WriteScalar(rg, rows, Primary, r => r.Spectrum.TotalIonCurrent);
        WriteScalar(rg, rows, Primary, r => r.Spectrum.LowestObservedMz);
        WriteScalar(rg, rows, Primary, r => r.Spectrum.HighestObservedMz);
        WriteCvParamLeaves(rg, rows, Primary, r => r.Spectrum.SpectrumParameters);
        WriteScalar(rg, rows, Primary, r => r.Spectrum.SpectrumDataProcessingRef);
        WriteMzDeltaModel(rg, rows);
        WriteScalar(rg, rows, Primary, r => r.Spectrum.NumberOfAuxiliaryArrays);
        WriteScalar(rg, rows, Primary, r => r.Spectrum.ScanCombinationCurie);
        WriteListString(rg, rows, Primary, r => r.Spectrum.ParamGroupRefs);
        WriteScalar(rg, rows, Primary, r => r.Spectrum.AuxiliaryArraysJson);
        WriteScalar(rg, rows, Primary, r => r.Spectrum.ValueArrayCurie);
        WriteScalar(rg, rows, Primary, r => r.Spectrum.ValueArrayUnitCurie);

        // -------- scan group (primary rows only) --------
        WriteScalar(rg, rows, Primary, r => (ulong?)(r.Spectrum.ScanSourceIndex ?? r.Spectrum.Index));
        WriteScalar(rg, rows, Primary, r => r.Spectrum.InstrumentConfigurationRef);
        WriteScalar(rg, rows, Primary, r => r.Spectrum.ScanIonMobilityValue);
        WriteScalar(rg, rows, Primary, r => r.Spectrum.ScanIonMobilityTypeCurie);
        WriteScalar(rg, rows, Primary, r => r.Spectrum.ScanStartTime);
        WriteScalar(rg, rows, Primary, r => r.Spectrum.FilterString);
        WriteScalar(rg, rows, Primary, r => r.Spectrum.PresetScanConfiguration);
        WriteScalar(rg, rows, Primary, r => r.Spectrum.IonInjectionTime);
        WriteScalar(rg, rows, Primary, r => r.Spectrum.ScanSpectrumRef);
        WriteCvParamLeaves(rg, rows, Primary, r => r.Spectrum.ScanParameters);
        WriteListScalar<double>(rg, rows, Primary, r => r.Spectrum.ScanWindowLowerLimits);
        WriteListScalar<double>(rg, rows, Primary, r => r.Spectrum.ScanWindowUpperLimits);
        WriteScalar(rg, rows, Primary, r => r.Spectrum.ScanWindowParamsJson);
        WriteScalar(rg, rows, Primary, r => r.Spectrum.ExtraScansJson);

        // -------- precursor group (rows with a precursor) --------
        WriteScalar(rg, rows, HasPrec, r => (ulong?)r.Spectrum.Index);
        WriteScalar(rg, rows, HasPrec, r => (ulong?)((ulong?)r.PrecursorIdx));
        WriteScalar(rg, rows, HasPrec, r => r.Precursor!.PrecursorId);
        // isolation_window sub-struct
        WriteScalar2(rg, rows, HasPrec, r => r.Precursor!.IsolationTargetMz);
        WriteScalar2(rg, rows, HasPrec, r => r.Precursor!.IsolationLowerOffset);
        WriteScalar2(rg, rows, HasPrec, r => r.Precursor!.IsolationUpperOffset);
        WriteCvParamLeaves2(rg, rows, HasPrec, r => r.Precursor!.IsolationWindowParameters);
        // activation sub-struct
        WriteScalar2(rg, rows, HasPrec, r => r.Precursor!.CollisionEnergy);
        WriteScalar2(rg, rows, HasPrec, r => r.Precursor!.DissociationMethodCurie);
        WriteCvParamLeaves2(rg, rows, HasPrec, r => r.Precursor!.ActivationParameters);

        // -------- selected_ion group (one per precursor row) --------
        WriteScalar(rg, rows, HasPrec, r => (ulong?)r.Spectrum.Index);
        WriteScalar(rg, rows, HasPrec, r => (ulong?)((ulong?)r.PrecursorIdx));
        WriteScalar(rg, rows, HasPrec, r => r.Precursor!.SelectedIonIonMobilityValue);
        WriteScalar(rg, rows, HasPrec, r => r.Precursor!.SelectedIonIonMobilityTypeCurie);
        WriteScalar(rg, rows, HasPrec, r => r.Precursor!.SelectedIonMz);
        WriteScalar(rg, rows, HasPrec, r => r.Precursor!.SelectedIonPeakIntensity);
        WriteScalar(rg, rows, HasPrec, r => r.Precursor!.SelectedIonChargeState);
        WriteCvParamLeaves(rg, rows, HasPrec, r => r.Precursor!.SelectedIonParameters);
    }

    // ---------------- Spectra binary data (canonical + supplementary peaks) ----------------

    private static GroupNode BuildPointSchema(string parentIndexName, string valueColumnName) => new(
        "schema", Repetition.Required,
        new Node[]
        {
            Struct("point",
                UInt64Leaf(parentIndexName),
                DoubleLeaf(valueColumnName),
                FloatLeaf("intensity")),
        });

    private static void WriteSpectraData(string path, IReadOnlyList<SpectrumToWrite> spectra)
        => WritePointLayout(path, "spectrum_index", "mz", "spectrum_array_index", BuildSpectraDataArrayIndex(),
            spectra.SelectMany(s => Enumerable.Range(0, s.Mz.Length).Select(i => ((ulong?)s.Index, (double?)s.Mz[i], (float?)s.Intensity[i]))));

    private static void WriteSpectraPeaks(string path, IReadOnlyList<SpectrumToWrite> spectra)
        => WritePointLayout(path, "spectrum_index", "mz", "spectrum_array_index", BuildSpectraDataArrayIndex(),
            spectra
                .Where(s => s.SupplementaryPeaksMz is not null)
                .SelectMany(s => Enumerable.Range(0, s.SupplementaryPeaksMz!.Length)
                    .Select(i => ((ulong?)s.Index, (double?)s.SupplementaryPeaksMz![i], (float?)s.SupplementaryPeaksIntensity![i]))));

    /// <summary>
    /// "spectrum_array_index" / "chromatogram_array_index" KV blocks tell consumers how to
    /// interpret the point-layout columns: which CV term identifies the mz vs intensity
    /// data, the prefix used in the parquet schema, etc. Required by mzPeak.NET's
    /// DataArraysReaderMeta to open the data file.
    /// </summary>
    private static string BuildSpectraDataArrayIndex() => System.Text.Json.JsonSerializer.Serialize(new
    {
        prefix = "point",
        entries = new object[]
        {
            new { context = "spectrum", path = "point.mz",        data_type = "MS:1000523", array_type = "MS:1000514", array_name = "m/z array",       unit = "MS:1000040" },
            new { context = "spectrum", path = "point.intensity", data_type = "MS:1000521", array_type = "MS:1000515", array_name = "intensity array", unit = "MS:1000131" },
        },
    });

    private static string BuildChromatogramsDataArrayIndex() => System.Text.Json.JsonSerializer.Serialize(new
    {
        prefix = "point",
        entries = new object[]
        {
            new { context = "chromatogram", path = "point.time",      data_type = "MS:1000523", array_type = "MS:1000595", array_name = "time array",      unit = "UO:0000031" },
            new { context = "chromatogram", path = "point.intensity", data_type = "MS:1000521", array_type = "MS:1000515", array_name = "intensity array", unit = "MS:1000131" },
        },
    });

    /// <summary>
    /// Target points per row group in the point-layout data files. Row groups are split only on
    /// parent-index (spectrum/chromatogram) boundaries so a parent's points never span groups —
    /// that lets the reader lazily load just the row group(s) covering a requested parent. The
    /// per-row-group parent-index range is recorded in the <c>point_row_group_ranges</c> KV.
    /// </summary>
    private const int TargetPointsPerRowGroup = 1_000_000;

    private const string PointRowGroupRangesKey = "point_row_group_ranges";

    private static void WritePointLayout(string path, string parentIndexName, string valueColumnName, string arrayIndexKey, string arrayIndexJson, IEnumerable<(ulong? idx, double? value, float? intensity)> rows)
    {
        var schema = BuildPointSchema(parentIndexName, valueColumnName);
        var (idxArr, valArr, intArr) = MaterializeTriple(rows);
        int total = idxArr.Length;

        // Compute row-group boundaries: ~TargetPointsPerRowGroup points each, extended so a parent's
        // contiguous points are never split across groups.
        var bounds = new List<(int start, int len)>();
        var ranges = new List<long[]>();
        int rgStart = 0;
        while (rgStart < total)
        {
            int rgEnd = Math.Min(total, rgStart + TargetPointsPerRowGroup);
            while (rgEnd < total && idxArr[rgEnd] == idxArr[rgEnd - 1]) rgEnd++;
            bounds.Add((rgStart, rgEnd - rgStart));
            ranges.Add(new[] { (long)(idxArr[rgStart] ?? 0), (long)(idxArr[rgEnd - 1] ?? 0) });
            rgStart = rgEnd;
        }
        if (bounds.Count == 0) { bounds.Add((0, 0)); ranges.Add(new long[] { -1, -1 }); }

        var kv = new Dictionary<string, string>
        {
            [arrayIndexKey] = arrayIndexJson,
            [PointRowGroupRangesKey] = System.Text.Json.JsonSerializer.Serialize(ranges),
        };
        using var props = new WriterPropertiesBuilder().Compression(Compression.Zstd).Build();
        using var fileWriter = new ParquetFileWriter(path, schema, props, keyValueMetadata: kv);
        foreach (var (start, len) in bounds)
        {
            using var rg = fileWriter.AppendRowGroup();
            // Leaves live inside the "point" group → 1 Nested wrap.
            WriteNestedScalar1Slice<ulong>(rg, idxArr, start, len);
            WriteNestedScalar1Slice<double>(rg, valArr, start, len);
            WriteNestedScalar1Slice<float>(rg, intArr, start, len);
        }
        fileWriter.Close();
    }

    private static void WriteNestedScalar1Slice<T>(RowGroupWriter rg, T?[] values, int start, int len) where T : struct
    {
        using var w = rg.NextColumn().LogicalWriter<Nested<T?>?>();
        var arr = new Nested<T?>?[len];
        for (int i = 0; i < len; i++) arr[i] = new Nested<T?>(values[start + i]);
        w.WriteBatch(arr);
    }

    private static void WriteNestedScalar1<T>(RowGroupWriter rg, T?[] values) where T : struct
    {
        using var w = rg.NextColumn().LogicalWriter<Nested<T?>?>();
        var arr = new Nested<T?>?[values.Length];
        for (int i = 0; i < values.Length; i++) arr[i] = new Nested<T?>(values[i]);
        w.WriteBatch(arr);
    }

    private static (ulong?[], double?[], float?[]) MaterializeTriple(IEnumerable<(ulong? idx, double? value, float? intensity)> rows)
    {
        var list = rows as IList<(ulong? idx, double? value, float? intensity)> ?? rows.ToList();
        int n = list.Count;
        var idx = new ulong?[n]; var val = new double?[n]; var inten = new float?[n];
        for (int i = 0; i < n; i++) { idx[i] = list[i].idx; val[i] = list[i].value; inten[i] = list[i].intensity; }
        return (idx, val, inten);
    }

    // ---------------- Chromatograms ----------------

    private static GroupNode BuildChromatogramsMetadataSchema() => new(
        "schema", Repetition.Required,
        new Node[]
        {
            Struct("chromatogram",
                UInt64Leaf("index"),
                StringLeaf("id"),
                StringLeaf("MS_1000626_chromatogram_type"),
                StringLeaf("data_processing_ref"),
                StringLeaf("MS_1000595_time_array_unit"),
                StringLeaf("MS_1000515_intensity_array_unit"),
                CvParamList(),
                StringLeaf("auxiliary_arrays")),
            // mzPeak.NET's ChromatogramMetadataReader unconditionally calls
            // batch.Column("precursor") / batch.Column("selected_ion") on the
            // chromatograms_metadata file. The demo populates both for SIM-style
            // chromatograms. We currently don't write any precursor/selected_ion
            // data per chromatogram, so emit empty struct groups with a single
            // null-valued field each just to satisfy mzPeak.NET's column lookup.
            Struct("precursor", UInt64Leaf("source_index")),
            Struct("selected_ion", UInt64Leaf("source_index")),
        });

    private static void WriteChromatogramsMetadata(string path, IReadOnlyList<ChromatogramToWrite> chromatograms)
    {
        var schema = BuildChromatogramsMetadataSchema();
        using var props = new WriterPropertiesBuilder().Compression(Compression.Zstd).Build();
        using var fileWriter = new ParquetFileWriter(path, schema, props);
        using var rg = fileWriter.AppendRowGroup();
        // Leaves under the "chromatogram" group — 1-deep Nested wrap.
        WriteNestedScalar1(rg, chromatograms.Select(c => (ulong?)c.Index).ToArray());
        WriteChromString(rg, chromatograms, c => c.Id);
        WriteChromString(rg, chromatograms, c => c.ChromatogramTypeCurie);
        WriteChromString(rg, chromatograms, c => c.DataProcessingRef);
        WriteChromString(rg, chromatograms, c => c.TimeUnitCurie);
        WriteChromString(rg, chromatograms, c => c.IntensityUnitCurie);
        WriteChromCvParamLeaves(rg, chromatograms, c => c.Parameters);
        WriteChromString(rg, chromatograms, c => c.AuxiliaryArraysJson);
        // precursor.source_index — emit nulls (we don't track per-chromatogram precursors yet)
        WriteNestedScalar1<ulong>(rg, new ulong?[chromatograms.Count]);
        // selected_ion.source_index — same
        WriteNestedScalar1<ulong>(rg, new ulong?[chromatograms.Count]);
        fileWriter.Close();
    }

    private static void WriteChromString(RowGroupWriter rg, IReadOnlyList<ChromatogramToWrite> chroms, Func<ChromatogramToWrite, string?> selector)
    {
        using var w = rg.NextColumn().LogicalWriter<Nested<string?>?>();
        var arr = new Nested<string?>?[chroms.Count];
        for (int i = 0; i < chroms.Count; i++) arr[i] = new Nested<string?>(selector(chroms[i]));
        w.WriteBatch(arr);
    }

    // Chromatogram-level CvParam list — same 8 leaves as the spectrum/scan param lists, but one
    // optional group deep (the chromatogram struct), always present.
    private static void WriteChromCvParamLeaves(RowGroupWriter rg, IReadOnlyList<ChromatogramToWrite> chroms,
        Func<ChromatogramToWrite, IReadOnlyList<MzPeakReader.CvParam>?> getter)
    {
        // name/accession/unit/type are direct element fields (one group level above the list);
        // value.* live inside the nested `value` struct, so they need one extra Nested level.
        WriteChromListedString(rg, chroms, getter, p => p.Name);
        WriteChromListedString(rg, chroms, getter, p => p.Accession);
        WriteChromListedStringValue(rg, chroms, getter, p => p.ValueString);
        WriteChromListedValue<long>(rg, chroms, getter, p => p.ValueInteger);
        WriteChromListedValue<double>(rg, chroms, getter, p => p.ValueFloat);
        WriteChromListedValue<bool>(rg, chroms, getter, p => p.ValueBoolean);
        WriteChromListedString(rg, chroms, getter, p => p.Unit);
        WriteChromListedString(rg, chroms, getter, p => p.Type);
    }

    // Direct element field (name / accession / unit / type): chromatogram-group → list → element-field.
    private static void WriteChromListedString(RowGroupWriter rg, IReadOnlyList<ChromatogramToWrite> chroms,
        Func<ChromatogramToWrite, IReadOnlyList<MzPeakReader.CvParam>?> getter, Func<MzPeakReader.CvParam, string?> selector)
    {
        using var w = rg.NextColumn().LogicalWriter<Nested<Nested<string?>?[]>?>();
        var arr = new Nested<Nested<string?>?[]>?[chroms.Count];
        for (int i = 0; i < chroms.Count; i++)
        {
            var p = getter(chroms[i]);
            var elems = (p is null || p.Count == 0)
                ? Array.Empty<Nested<string?>?>()
                : p.Select(x => (Nested<string?>?)new Nested<string?>(selector(x))).ToArray();
            arr[i] = new Nested<Nested<string?>?[]>(elems);
        }
        w.WriteBatch(arr);
    }

    // value.string leaf: one extra Nested for the `value` sub-struct.
    private static void WriteChromListedStringValue(RowGroupWriter rg, IReadOnlyList<ChromatogramToWrite> chroms,
        Func<ChromatogramToWrite, IReadOnlyList<MzPeakReader.CvParam>?> getter, Func<MzPeakReader.CvParam, string?> selector)
    {
        using var w = rg.NextColumn().LogicalWriter<Nested<Nested<Nested<string?>?>?[]>?>();
        var arr = new Nested<Nested<Nested<string?>?>?[]>?[chroms.Count];
        for (int i = 0; i < chroms.Count; i++)
        {
            var p = getter(chroms[i]);
            var elems = (p is null || p.Count == 0)
                ? Array.Empty<Nested<Nested<string?>?>?>()
                : p.Select(x => (Nested<Nested<string?>?>?)new Nested<Nested<string?>?>(new Nested<string?>(selector(x)))).ToArray();
            arr[i] = new Nested<Nested<Nested<string?>?>?[]>(elems);
        }
        w.WriteBatch(arr);
    }

    // value.{integer,float,boolean} leaf: one extra Nested for the `value` sub-struct.
    private static void WriteChromListedValue<T>(RowGroupWriter rg, IReadOnlyList<ChromatogramToWrite> chroms,
        Func<ChromatogramToWrite, IReadOnlyList<MzPeakReader.CvParam>?> getter, Func<MzPeakReader.CvParam, T?> selector)
        where T : unmanaged
    {
        using var w = rg.NextColumn().LogicalWriter<Nested<Nested<Nested<T?>?>?[]>?>();
        var arr = new Nested<Nested<Nested<T?>?>?[]>?[chroms.Count];
        for (int i = 0; i < chroms.Count; i++)
        {
            var p = getter(chroms[i]);
            var elems = (p is null || p.Count == 0)
                ? Array.Empty<Nested<Nested<T?>?>?>()
                : p.Select(x => (Nested<Nested<T?>?>?)new Nested<Nested<T?>?>(new Nested<T?>(selector(x)))).ToArray();
            arr[i] = new Nested<Nested<Nested<T?>?>?[]>(elems);
        }
        w.WriteBatch(arr);
    }

    private static void WriteChromatogramsData(string path, IReadOnlyList<ChromatogramToWrite> chromatograms)
        => WritePointLayout(path, "chromatogram_index", "time", "chromatogram_array_index", BuildChromatogramsDataArrayIndex(),
            chromatograms.SelectMany(c => Enumerable.Range(0, c.Time.Length).Select(i => ((ulong?)c.Index, (double?)c.Time[i], (float?)c.Intensity[i]))));

    // ---------------- Write helpers ----------------

    // ===== Column writers — operate over the fan-out Row list =====
    //
    // Every entity group (spectrum, scan, precursor, selected_ion) is optional
    // in the parquet schema. The "present" predicate gates the outer Nested:
    // when it returns false (e.g. a non-primary row for the spectrum group),
    // the outer wrapper is null, which serialises as "this group is absent".

    private static void WriteScalar<T>(RowGroupWriter rg, IReadOnlyList<Row> rows, Func<Row, bool> present, Func<Row, T?> selector)
        where T : struct
    {
        using var w = rg.NextColumn().LogicalWriter<Nested<T?>?>();
        var arr = new Nested<T?>?[rows.Count];
        for (int i = 0; i < rows.Count; i++)
            arr[i] = present(rows[i]) ? new Nested<T?>(selector(rows[i])) : null;
        w.WriteBatch(arr);
    }

    private static void WriteScalar(RowGroupWriter rg, IReadOnlyList<Row> rows, Func<Row, bool> present, Func<Row, string?> selector)
    {
        using var w = rg.NextColumn().LogicalWriter<Nested<string?>?>();
        var arr = new Nested<string?>?[rows.Count];
        for (int i = 0; i < rows.Count; i++)
            arr[i] = present(rows[i]) ? new Nested<string?>(selector(rows[i])) : null;
        w.WriteBatch(arr);
    }

    // Two optional groups deep — outer wraps the entity group, inner wraps the
    // sub-struct (e.g. precursor.isolation_window). Both gate on `present`.
    private static void WriteScalar2<T>(RowGroupWriter rg, IReadOnlyList<Row> rows, Func<Row, bool> present, Func<Row, T?> selector)
        where T : struct
    {
        using var w = rg.NextColumn().LogicalWriter<Nested<Nested<T?>?>?>();
        var arr = new Nested<Nested<T?>?>?[rows.Count];
        for (int i = 0; i < rows.Count; i++)
            arr[i] = present(rows[i]) ? new Nested<Nested<T?>?>(new Nested<T?>(selector(rows[i]))) : null;
        w.WriteBatch(arr);
    }

    private static void WriteScalar2(RowGroupWriter rg, IReadOnlyList<Row> rows, Func<Row, bool> present, Func<Row, string?> selector)
    {
        using var w = rg.NextColumn().LogicalWriter<Nested<Nested<string?>?>?>();
        var arr = new Nested<Nested<string?>?>?[rows.Count];
        for (int i = 0; i < rows.Count; i++)
            arr[i] = present(rows[i]) ? new Nested<Nested<string?>?>(new Nested<string?>(selector(rows[i]))) : null;
        w.WriteBatch(arr);
    }

    // List of leaf doubles inside one optional group (spectrum.mz_delta_model).
    private static void WriteMzDeltaModel(RowGroupWriter rg, IReadOnlyList<Row> rows)
    {
        using var w = rg.NextColumn().LogicalWriter<Nested<double?[]>?>();
        var arr = new Nested<double?[]>?[rows.Count];
        for (int i = 0; i < rows.Count; i++)
            arr[i] = rows[i].IsPrimary
                ? new Nested<double?[]>(rows[i].Spectrum.MzDeltaModel ?? Array.Empty<double?>())
                : null;
        w.WriteBatch(arr);
    }

    // List-of-leaf (plain strings) inside one optional group, mirroring WriteMzDeltaModel's
    // Nested<element?[]> shape (no inner element-struct wrap).
    private static void WriteListString(RowGroupWriter rg, IReadOnlyList<Row> rows, Func<Row, bool> present, Func<Row, IReadOnlyList<string>?> selector)
    {
        using var w = rg.NextColumn().LogicalWriter<Nested<string?[]>?>();
        var arr = new Nested<string?[]>?[rows.Count];
        for (int i = 0; i < rows.Count; i++)
        {
            if (!present(rows[i])) { arr[i] = null; continue; }
            var src = selector(rows[i]);
            arr[i] = new Nested<string?[]>(src is null || src.Count == 0
                ? Array.Empty<string?>()
                : src.Select(x => (string?)x).ToArray());
        }
        w.WriteBatch(arr);
    }

    // scan_window-style list inside one optional group, with element-struct wrap.
    private static void WriteListScalar<T>(RowGroupWriter rg, IReadOnlyList<Row> rows, Func<Row, bool> present, Func<Row, T?[]?> selector)
        where T : unmanaged
    {
        using var w = rg.NextColumn().LogicalWriter<Nested<Nested<T?>?[]>?>();
        var arr = new Nested<Nested<T?>?[]>?[rows.Count];
        for (int i = 0; i < rows.Count; i++)
        {
            if (!present(rows[i])) { arr[i] = null; continue; }
            var src = selector(rows[i]);
            var elems = (src is null || src.Length == 0)
                ? Array.Empty<Nested<T?>?>()
                : src.Select(x => (Nested<T?>?)new Nested<T?>(x)).ToArray();
            arr[i] = new Nested<Nested<T?>?[]>(elems);
        }
        w.WriteBatch(arr);
    }

    // All 7 leaves of a CvParam list inside one optional entity group.
    private static void WriteCvParamLeaves(
        RowGroupWriter rg, IReadOnlyList<Row> rows, Func<Row, bool> present,
        Func<Row, IReadOnlyList<MzPeakReader.CvParam>?> getter)
    {
        WriteListedStringLeaf1(rg, rows, present, getter, p => p.Name);
        WriteListedStringLeaf1(rg, rows, present, getter, p => p.Accession);
        WriteListedStringLeaf1Value(rg, rows, present, getter, p => p.ValueString);
        WriteListedValueLeaf1Value<long>(rg, rows, present, getter, p => p.ValueInteger);
        WriteListedValueLeaf1Value<double>(rg, rows, present, getter, p => p.ValueFloat);
        WriteListedValueLeaf1Value<bool>(rg, rows, present, getter, p => p.ValueBoolean);
        WriteListedStringLeaf1(rg, rows, present, getter, p => p.Unit);
        WriteListedStringLeaf1(rg, rows, present, getter, p => p.Type);
    }

    // Same shape, two outer groups deep (precursor.isolation_window.parameters etc.).
    private static void WriteCvParamLeaves2(
        RowGroupWriter rg, IReadOnlyList<Row> rows, Func<Row, bool> present,
        Func<Row, IReadOnlyList<MzPeakReader.CvParam>?> getter)
    {
        WriteListedStringLeaf2(rg, rows, present, getter, p => p.Name);
        WriteListedStringLeaf2(rg, rows, present, getter, p => p.Accession);
        WriteListedStringLeaf2Value(rg, rows, present, getter, p => p.ValueString);
        WriteListedValueLeaf2Value<long>(rg, rows, present, getter, p => p.ValueInteger);
        WriteListedValueLeaf2Value<double>(rg, rows, present, getter, p => p.ValueFloat);
        WriteListedValueLeaf2Value<bool>(rg, rows, present, getter, p => p.ValueBoolean);
        WriteListedStringLeaf2(rg, rows, present, getter, p => p.Unit);
        WriteListedStringLeaf2(rg, rows, present, getter, p => p.Type);
    }

    private static void WriteListedStringLeaf1(
        RowGroupWriter rg, IReadOnlyList<Row> rows, Func<Row, bool> present,
        Func<Row, IReadOnlyList<MzPeakReader.CvParam>?> getter,
        Func<MzPeakReader.CvParam, string?> selector)
    {
        using var w = rg.NextColumn().LogicalWriter<Nested<Nested<string?>?[]>?>();
        var arr = new Nested<Nested<string?>?[]>?[rows.Count];
        for (int i = 0; i < rows.Count; i++)
        {
            if (!present(rows[i])) { arr[i] = null; continue; }
            var p = getter(rows[i]);
            var elems = (p is null || p.Count == 0)
                ? Array.Empty<Nested<string?>?>()
                : p.Select(x => (Nested<string?>?)new Nested<string?>(selector(x))).ToArray();
            arr[i] = new Nested<Nested<string?>?[]>(elems);
        }
        w.WriteBatch(arr);
    }

    private static void WriteListedStringLeaf1Value(
        RowGroupWriter rg, IReadOnlyList<Row> rows, Func<Row, bool> present,
        Func<Row, IReadOnlyList<MzPeakReader.CvParam>?> getter,
        Func<MzPeakReader.CvParam, string?> selector)
    {
        using var w = rg.NextColumn().LogicalWriter<Nested<Nested<Nested<string?>?>?[]>?>();
        var arr = new Nested<Nested<Nested<string?>?>?[]>?[rows.Count];
        for (int i = 0; i < rows.Count; i++)
        {
            if (!present(rows[i])) { arr[i] = null; continue; }
            var p = getter(rows[i]);
            var elems = (p is null || p.Count == 0)
                ? Array.Empty<Nested<Nested<string?>?>?>()
                : p.Select(x => (Nested<Nested<string?>?>?)new Nested<Nested<string?>?>(new Nested<string?>(selector(x)))).ToArray();
            arr[i] = new Nested<Nested<Nested<string?>?>?[]>(elems);
        }
        w.WriteBatch(arr);
    }

    private static void WriteListedValueLeaf1Value<T>(
        RowGroupWriter rg, IReadOnlyList<Row> rows, Func<Row, bool> present,
        Func<Row, IReadOnlyList<MzPeakReader.CvParam>?> getter,
        Func<MzPeakReader.CvParam, T?> selector) where T : unmanaged
    {
        using var w = rg.NextColumn().LogicalWriter<Nested<Nested<Nested<T?>?>?[]>?>();
        var arr = new Nested<Nested<Nested<T?>?>?[]>?[rows.Count];
        for (int i = 0; i < rows.Count; i++)
        {
            if (!present(rows[i])) { arr[i] = null; continue; }
            var p = getter(rows[i]);
            var elems = (p is null || p.Count == 0)
                ? Array.Empty<Nested<Nested<T?>?>?>()
                : p.Select(x => (Nested<Nested<T?>?>?)new Nested<Nested<T?>?>(new Nested<T?>(selector(x)))).ToArray();
            arr[i] = new Nested<Nested<Nested<T?>?>?[]>(elems);
        }
        w.WriteBatch(arr);
    }

    private static void WriteListedStringLeaf2(
        RowGroupWriter rg, IReadOnlyList<Row> rows, Func<Row, bool> present,
        Func<Row, IReadOnlyList<MzPeakReader.CvParam>?> getter,
        Func<MzPeakReader.CvParam, string?> selector)
    {
        using var w = rg.NextColumn().LogicalWriter<Nested<Nested<Nested<string?>?[]>?>?>();
        var arr = new Nested<Nested<Nested<string?>?[]>?>?[rows.Count];
        for (int i = 0; i < rows.Count; i++)
        {
            if (!present(rows[i])) { arr[i] = null; continue; }
            var p = getter(rows[i]);
            var elems = (p is null || p.Count == 0)
                ? Array.Empty<Nested<string?>?>()
                : p.Select(x => (Nested<string?>?)new Nested<string?>(selector(x))).ToArray();
            arr[i] = new Nested<Nested<Nested<string?>?[]>?>(new Nested<Nested<string?>?[]>(elems));
        }
        w.WriteBatch(arr);
    }

    private static void WriteListedStringLeaf2Value(
        RowGroupWriter rg, IReadOnlyList<Row> rows, Func<Row, bool> present,
        Func<Row, IReadOnlyList<MzPeakReader.CvParam>?> getter,
        Func<MzPeakReader.CvParam, string?> selector)
    {
        using var w = rg.NextColumn().LogicalWriter<Nested<Nested<Nested<Nested<string?>?>?[]>?>?>();
        var arr = new Nested<Nested<Nested<Nested<string?>?>?[]>?>?[rows.Count];
        for (int i = 0; i < rows.Count; i++)
        {
            if (!present(rows[i])) { arr[i] = null; continue; }
            var p = getter(rows[i]);
            var elems = (p is null || p.Count == 0)
                ? Array.Empty<Nested<Nested<string?>?>?>()
                : p.Select(x => (Nested<Nested<string?>?>?)new Nested<Nested<string?>?>(new Nested<string?>(selector(x)))).ToArray();
            arr[i] = new Nested<Nested<Nested<Nested<string?>?>?[]>?>(new Nested<Nested<Nested<string?>?>?[]>(elems));
        }
        w.WriteBatch(arr);
    }

    private static void WriteListedValueLeaf2Value<T>(
        RowGroupWriter rg, IReadOnlyList<Row> rows, Func<Row, bool> present,
        Func<Row, IReadOnlyList<MzPeakReader.CvParam>?> getter,
        Func<MzPeakReader.CvParam, T?> selector) where T : unmanaged
    {
        using var w = rg.NextColumn().LogicalWriter<Nested<Nested<Nested<Nested<T?>?>?[]>?>?>();
        var arr = new Nested<Nested<Nested<Nested<T?>?>?[]>?>?[rows.Count];
        for (int i = 0; i < rows.Count; i++)
        {
            if (!present(rows[i])) { arr[i] = null; continue; }
            var p = getter(rows[i]);
            var elems = (p is null || p.Count == 0)
                ? Array.Empty<Nested<Nested<T?>?>?>()
                : p.Select(x => (Nested<Nested<T?>?>?)new Nested<Nested<T?>?>(new Nested<T?>(selector(x)))).ToArray();
            arr[i] = new Nested<Nested<Nested<Nested<T?>?>?[]>?>(new Nested<Nested<Nested<T?>?>?[]>(elems));
        }
        w.WriteBatch(arr);
    }

    // ---------------- File-level metadata (KV JSON) + manifest ----------------

    private static void WriteManifest(string path, bool includeChromatograms, bool includeSupplementaryPeaks)
    {
        var files = new List<object>
        {
            new { name = "spectra_metadata.parquet", entity_type = "spectrum", data_kind = "metadata" },
            new { name = "spectra_data.parquet",     entity_type = "spectrum", data_kind = "data arrays" },
        };
        if (includeSupplementaryPeaks)
            files.Add(new { name = "spectra_peaks.parquet", entity_type = "spectrum", data_kind = "peaks" });
        if (includeChromatograms)
        {
            files.Add(new { name = "chromatograms_metadata.parquet", entity_type = "chromatogram", data_kind = "metadata" });
            files.Add(new { name = "chromatograms_data.parquet",     entity_type = "chromatogram", data_kind = "data arrays" });
        }
        var manifest = new { files = files.ToArray(), metadata = new { } };
        File.WriteAllText(path, JsonSerializer.Serialize(manifest, ManifestJsonOpts));
    }

    private static readonly JsonSerializerOptions ManifestJsonOpts = new() { WriteIndented = true };

    private static IReadOnlyDictionary<string, string> BuildKeyValueMetadata(FileMetadata fm, long spectraDataPointCount)
    {
        var opts = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };
        var kv = new Dictionary<string, string>
        {
            ["file_description"]              = JsonSerializer.Serialize(fm.FileDescription, opts),
            ["instrument_configuration_list"] = JsonSerializer.Serialize(fm.InstrumentConfigurations, opts),
            ["data_processing_method_list"]   = JsonSerializer.Serialize(fm.DataProcessingMethods, opts),
            ["software_list"]                 = JsonSerializer.Serialize(fm.Software, opts),
            ["sample_list"]                   = JsonSerializer.Serialize(fm.Samples, opts),
            ["run"]                           = JsonSerializer.Serialize(fm.Run, opts),
            ["spectrum_count"]                = "0",
            ["spectrum_data_point_count"]     = spectraDataPointCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        if (!string.IsNullOrEmpty(fm.DocumentId))
            kv["document_id"] = fm.DocumentId!;
        if (fm.ParamGroups is { Count: > 0 })
            kv["referenceable_param_group_list"] = JsonSerializer.Serialize(fm.ParamGroups, opts);
        return kv;
    }
}
