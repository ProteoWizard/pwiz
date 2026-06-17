using System.IO.Compression;
using System.Text.RegularExpressions;
using ParquetSharp;

namespace Pwiz.Data.MsData.MzPeak;

/// <summary>
/// Phase 2 / 2.5 / 3 / 4 mzPeak reader prototype — synchronous, no async runtime touched.
///
/// 2: scalar spectrum metadata (id, time, ms_level, profile/centroid CURIE, scan_start_time, filter_string).
/// 2.5: nested CvParam lists (spectrum.parameters, scan.parameters), isolation_window /
///      activation / selected_ion scalars.
/// 3: per-spectrum random-access binary data — mz + intensity arrays — from
///    spectra_data.parquet (profile) or spectra_peaks.parquet (centroid).
/// 4a: file-level metadata via Parquet KV JSON (file_description, instrument
///     configurations, software, samples, run info).
/// 4b: chromatograms (count, description, time/intensity arrays) — same column
///     dispatcher pattern as spectra, separate parquet files.
///
/// Deferred to later phases: aux_arrays (rep=2), nested parameters under
/// isolation_window / activation / scan_windows (rep=2), multi-precursor spectra,
/// row-group-lazy reads (current code eager-loads the whole metadata table +
/// point-layout files), writer.
/// </summary>
public sealed class MzPeakReader : IDisposable
{
    public sealed record SpectrumDescription(
        ulong Index,
        string Id,
        double Time,
        int? MsLevel,
        bool IsProfile,
        bool IsCentroid,
        string? RepresentationCurie,
        ScanInfo? Scan,
        IReadOnlyList<PrecursorInfo> Precursors,
        IReadOnlyList<CvParam> Parameters,
        // Phase 7 — MSData parity additions
        int? ScanPolarity = null,
        string? SpectrumTypeCurie = null,
        long? NumberOfDataPoints = null,
        long? NumberOfPeaks = null,
        double? BasePeakMz = null,
        double? BasePeakIntensity = null,
        double? TotalIonCurrent = null,
        double? LowestObservedMz = null,
        double? HighestObservedMz = null,
        string? DataProcessingRef = null,
        int? NumberOfAuxiliaryArrays = null,
        IReadOnlyList<double>? MzDeltaModel = null,
        // scanList combination method CURIE + referenceableParamGroup ids referenced by the spectrum.
        string? ScanCombinationCurie = null,
        IReadOnlyList<string>? ParamGroupRefs = null,
        IReadOnlyList<AuxiliaryArrayData>? AuxArrays = null,
        // Value-array type/unit CURIEs when the value array isn't m/z (UV/DAD wavelength, …).
        string? ValueArrayCurie = null,
        string? ValueArrayUnitCurie = null);

    public sealed record ScanInfo(
        double? StartTime,
        string? FilterString,
        uint? InstrumentConfigurationRef,
        double? IonInjectionTime,
        IReadOnlyList<CvParam> Parameters,
        IReadOnlyList<ScanWindow> ScanWindows,
        // Phase 7
        ulong? SourceIndex = null,
        double? IonMobilityValue = null,
        string? IonMobilityTypeCurie = null,
        long? PresetScanConfiguration = null,
        // JSON of per-scan-window free-form params (parallel to ScanWindows).
        string? ScanWindowParamsJson = null,
        // JSON of scanList scans beyond this one (combined ion-mobility spectra).
        string? ExtraScansJson = null,
        // scan[0] spectrumRef (source spectrum for a combined scan), if any.
        string? SpectrumRef = null);

    public sealed record PrecursorInfo(
        string? PrecursorId,
        IsolationWindow? IsolationWindow,
        Activation? Activation,
        SelectedIonInfo? SelectedIon,
        // Phase 7
        ulong? SourceIndex = null,
        ulong? PrecursorIndex = null);

    public sealed record IsolationWindow(
        double? TargetMz,
        double? LowerOffset,
        double? UpperOffset,
        IReadOnlyList<CvParam> Parameters);

    public sealed record Activation(
        double? CollisionEnergy,
        string? DissociationMethod,
        IReadOnlyList<CvParam> Parameters);

    public sealed record SelectedIonInfo(
        double? Mz,
        double? PeakIntensity,
        long? ChargeState,
        IReadOnlyList<CvParam> Parameters,
        // Phase 7
        ulong? SourceIndex = null,
        ulong? PrecursorIndex = null,
        double? IonMobilityValue = null,
        string? IonMobilityTypeCurie = null);

    /// <summary>
    /// One scan window — the m/z range the instrument was set to scan over for this
    /// scan. cpp/mzML allows multiple scan windows per scan; the demo has exactly one
    /// per scan. The nested rep=2 parameters inside each scan window are deferred
    /// (would require LogicalReader&lt;T?[][]&gt;); the scalar lower/upper limits are
    /// what matters for most consumers.
    /// </summary>
    public sealed record ScanWindow(double? LowerLimit, double? UpperLimit);

    /// <summary>
    /// A controlled-vocabulary parameter. Exactly one of String/Integer/Float/Boolean
    /// is non-null per the spec's value-union encoding; the C# side surfaces them
    /// through the discriminated <see cref="Value"/> accessor.
    /// </summary>
    public sealed record CvParam(
        string? Name,
        string? Accession,
        string? ValueString,
        long? ValueInteger,
        double? ValueFloat,
        bool? ValueBoolean,
        string? Unit,
        // xsd datatype hint for userParams (e.g. "xsd:float"); null/empty for CV params.
        string? Type = null)
    {
        public object? Value =>
            (object?)ValueString ?? (object?)ValueInteger ?? (object?)ValueFloat ?? (object?)ValueBoolean;
    }

    /// <summary>
    /// Mz + intensity arrays for one spectrum. The mzPeak writer puts every spectrum's
    /// canonical data — profile signal for profile spectra, peak data for centroid
    /// spectra — in spectra_data.parquet; spectra_peaks.parquet is supplementary
    /// (additional centroid peaks for profile spectra that were peak-picked).
    /// </summary>
    public sealed record BinaryArrays(double[] Mz, float[] Intensity);

    private const string CurieProfileSpectrum = "MS:1000128";
    private const string CurieCentroidSpectrum = "MS:1000127";

    // CV column name pattern. Captures the trailing "_unit_(MS|UO)_NNNN" suffix when present
    // — informational only; the unit is also implicit in the column name and not load-bearing
    // for Phase 2/2.5 access. Also accepts an optional Parquet list-element trailer:
    // Arrow / real mzPeak files use ".list.element" embedded in the path (handled by the
    // (?:\.<sub>)* group), while ParquetSharp's writer adds a trailing ".list.item" to
    // declare the leaf inside a list group. Accept either trailer so files written by
    // both stacks dispatch to the same column.
    private static readonly Regex CvColumnRegex = new(
        @"^(?<entity>[a-z_]+)(?:\.(?<sub>[a-z_]+))*\." +
        @"MS_(?<acc>\d{7})_(?<snake>[a-z_]+?)" +
        @"(?:_unit_(?<unitsrc>MS|UO)_(?<unitacc>\d{7}))?" +
        @"(?:\.list\.(?:item|element))?$",
        RegexOptions.Compiled);

    // Source of the parquet entries: read in place from the .mzpeak ZIP (Stored entries) when
    // possible, else extracted to a temp dir. Exactly one of _archive / _tempDir is set.
    private readonly MzPeakArchive? _archive;
    private readonly string? _tempDir;

    // Lazy spectra-metadata: the parquet reader stays open; columns are allocated full-height at
    // construction but each row group's slice is decoded only on first access (EnsureGroupLoaded),
    // so opening a file doesn't pay to read every metadata column. The two linking columns
    // (spectrum.index, precursor.source_index) are read eagerly because the fan-out map needs them.
    private readonly ParquetFileReader _metaReader;
    private readonly Dictionary<string, int> _metaCols;
    private readonly int _numMetaGroups;
    private readonly int[] _rgRowOffset;          // cumulative row offsets, length _numMetaGroups + 1
    private readonly bool[] _metaGroupLoaded;
    private readonly object _metaLock = new();

    // Scalar columns (Phase 2) — allocated full-height, filled lazily per row group. Schema marks
    // everything nullable (maxDef=2 — optional parent struct + optional column), so storage is `T?[]`.
    private readonly ulong?[] _index;
    private readonly string?[] _id;
    private readonly double?[] _time;
    private readonly int?[] _msLevel;
    private readonly string?[] _representationCurie;
    private readonly double?[] _scanStartTime;
    private readonly string?[] _filterString;
    private readonly uint?[] _instrumentConfigRef;
    private readonly double?[] _ionInjectionTime;

    // Phase 2.5 — list columns (spectrum.parameters, scan.parameters). Each row holds
    // a nullable array of leaf values; zipping the parallel arrays reconstructs the
    // CvParam list. ParquetSharp returns null for "row had no list at all" vs an empty
    // array for "list was present but empty"; we collapse both to empty list at read.
    private readonly string?[]?[] _spectrumParamsName;
    private readonly string?[]?[] _spectrumParamsAccession;
    private readonly string?[]?[] _spectrumParamsValueString;
    private readonly long?[]?[] _spectrumParamsValueInteger;
    private readonly double?[]?[] _spectrumParamsValueFloat;
    private readonly bool?[]?[] _spectrumParamsValueBoolean;
    private readonly string?[]?[] _spectrumParamsUnit;
    private readonly string?[]?[] _spectrumParamsType;

    private readonly string?[]?[] _scanParamsName;
    private readonly string?[]?[] _scanParamsAccession;
    private readonly string?[]?[] _scanParamsValueString;
    private readonly long?[]?[] _scanParamsValueInteger;
    private readonly double?[]?[] _scanParamsValueFloat;
    private readonly bool?[]?[] _scanParamsValueBoolean;
    private readonly string?[]?[] _scanParamsUnit;
    private readonly string?[]?[] _scanParamsType;

    // Precursor scalars (Phase 2.5).
    private readonly string?[] _precursorId;
    private readonly double?[] _isolationTargetMz;
    private readonly double?[] _isolationLowerOffset;
    private readonly double?[] _isolationUpperOffset;
    private readonly double?[] _collisionEnergy;
    private readonly string?[] _dissociationMethod;
    private readonly double?[] _selectedIonMz;
    private readonly double?[] _selectedIonPeakIntensity;
    private readonly long?[] _selectedIonChargeState;

    // Phase 5 — nested parameter lists. Each entity's params are seven parallel
    // arrays of nullable lists per row (one list per spectrum row). Naming pattern
    // matches the columnar leaf: <entity>.parameters.list.element.{name,accession,
    // value.{string,integer,float,boolean},unit}.
    private readonly string?[]?[] _isoParamsName;
    private readonly string?[]?[] _isoParamsAccession;
    private readonly string?[]?[] _isoParamsValueString;
    private readonly long?[]?[] _isoParamsValueInteger;
    private readonly double?[]?[] _isoParamsValueFloat;
    private readonly bool?[]?[] _isoParamsValueBoolean;
    private readonly string?[]?[] _isoParamsUnit;
    private readonly string?[]?[] _isoParamsType;

    private readonly string?[]?[] _actParamsName;
    private readonly string?[]?[] _actParamsAccession;
    private readonly string?[]?[] _actParamsValueString;
    private readonly long?[]?[] _actParamsValueInteger;
    private readonly double?[]?[] _actParamsValueFloat;
    private readonly bool?[]?[] _actParamsValueBoolean;
    private readonly string?[]?[] _actParamsUnit;
    private readonly string?[]?[] _actParamsType;

    private readonly string?[]?[] _siParamsName;
    private readonly string?[]?[] _siParamsAccession;
    private readonly string?[]?[] _siParamsValueString;
    private readonly long?[]?[] _siParamsValueInteger;
    private readonly double?[]?[] _siParamsValueFloat;
    private readonly bool?[]?[] _siParamsValueBoolean;
    private readonly string?[]?[] _siParamsUnit;
    private readonly string?[]?[] _siParamsType;

    // Scan windows — per-spectrum list of (lower, upper). The element columns
    // are themselves rep=1 (one list of doubles per spectrum row, where each
    // element corresponds to one scan window).
    private readonly string?[] _scanWindowParamsJson;
    private readonly string?[] _extraScansJson;
    private readonly string?[] _scanSpectrumRef;
    private readonly double?[]?[] _scanWindowLower;  // MS:1000501
    private readonly double?[]?[] _scanWindowUpper;  // MS:1000500

    // Phase 7 — MSData parity scalar storage.
    private readonly int?[] _scanPolarity;
    private readonly string?[] _spectrumTypeCurie;
    private readonly long?[] _numberOfDataPoints;
    private readonly long?[] _numberOfPeaks;
    private readonly double?[] _basePeakMz;
    private readonly double?[] _basePeakIntensity;
    private readonly double?[] _totalIonCurrent;
    private readonly double?[] _lowestObservedMz;
    private readonly double?[] _highestObservedMz;
    private readonly string?[] _spectrumDataProcessingRef;
    private readonly int?[] _numberOfAuxiliaryArrays;
    private readonly double?[]?[] _mzDeltaModel;
    private readonly string?[] _scanCombinationCurie;
    private readonly string?[]?[] _paramGroupRefs;
    private readonly string?[] _spectrumAuxArraysJson;
    private readonly string?[] _valueArrayType;   // non-null when the value array isn't m/z (UV wavelength, …)
    private readonly string?[] _valueArrayUnit;

    private readonly ulong?[] _scanSourceIndex;
    private readonly double?[] _scanIonMobilityValue;
    private readonly string?[] _scanIonMobilityType;
    private readonly long?[] _scanPresetConfiguration;

    private readonly ulong?[] _precursorSourceIndex;
    private readonly ulong?[] _precursorPrecursorIndex;

    private readonly ulong?[] _selectedIonSourceIndex;
    private readonly ulong?[] _selectedIonPrecursorIndex;
    private readonly double?[] _selectedIonIonMobilityValue;
    private readonly string?[] _selectedIonIonMobilityType;

    // Phase 3 — binary data. Eager-load the point-layout files, bucket by spectrum_index,
    // Binary data is read lazily, one row group at a time, with an LRU cache (see LazyPointLayout) —
    // open stays cheap and memory is bounded to a few row groups instead of the whole file.
    private readonly LazyPointLayout _dataLayer;  // spectra_data.parquet (canonical)
    private readonly LazyPointLayout _peaksLayer; // spectra_peaks.parquet (supplementary)

    // UV/DAD wavelength spectra. mzPeak.NET stores these in dedicated wavelength_spectra_* entries
    // (the value array is wavelength, not m/z) rather than inline in spectra_metadata. When present
    // they're surfaced after the MS spectra: logical index >= _msSpectrumCount routes here.
    private readonly WavelengthSpectra? _wavelength;
    private int _msSpectrumCount;

    // Phase 4b — chromatograms. Same point-layout convention as spectra; eager-load
    // (time, intensity) by chromatogram_index. Demo file: 3431 chromatogram points
    // across 105 chromatograms.
    private readonly ulong?[] _chromIndex;
    private readonly string?[] _chromId;
    private readonly string?[] _chromTypeCurie;        // MS:1000625 / MS:1000235 / MS:1000810 / ...
    private readonly string?[] _chromDataProcessingRef;
    private readonly string?[] _chromTimeUnitCurie;    // unit of the time array (UO:0000010 / UO:0000031)
    private readonly string?[] _chromIntensityUnitCurie; // unit of the intensity array (counts / % / pascal / …)
    private readonly string?[]?[] _chromParamsName;
    private readonly string?[]?[] _chromParamsAccession;
    private readonly string?[]?[] _chromParamsValueString;
    private readonly long?[]?[] _chromParamsValueInteger;
    private readonly double?[]?[] _chromParamsValueFloat;
    private readonly bool?[]?[] _chromParamsValueBoolean;
    private readonly string?[]?[] _chromParamsUnit;
    private readonly string?[]?[] _chromParamsType;
    private readonly string?[] _chromAuxArraysJson;
    private readonly LazyPointLayout _chromData;

    /// <summary>Chromatogram metadata + data.</summary>
    public sealed record ChromatogramDescription(
        ulong Index,
        string Id,
        string? ChromatogramTypeCurie,
        string? DataProcessingRef,
        string? TimeUnitCurie = null,
        string? IntensityUnitCurie = null,
        IReadOnlyList<CvParam>? Parameters = null,
        IReadOnlyList<AuxiliaryArrayData>? AuxArrays = null);

    /// <summary>Time + intensity arrays for one chromatogram.</summary>
    public sealed record ChromatogramArrays(double[] Time, float[] Intensity);

    /// <summary>File-level metadata (the Parquet KV JSON blocks). Lazily parsed
    /// the first time it's accessed.</summary>
    public FileMetadata FileMetadata => _fileMetadata.Value;
    private readonly Lazy<FileMetadata> _fileMetadata;

    public int SpectrumCount { get; private set; }
    public int ChromatogramCount { get; }

    // Multi-precursor fan-out: every parquet row contributes to one spectrum
    // but a given spectrum may span N rows (one per precursor). _primaryRowOf
    // maps a logical spectrum index → the row carrying its spectrum/scan-level
    // fields (the row where spectrum.index is non-null). _precursorRowsBySpec
    // lists every row whose precursor.source_index matches that spectrum.
    private readonly int[] _primaryRowOfSpectrum;
    private readonly int[][] _precursorRowsBySpectrum;

    public MzPeakReader(string path)
    {
        // Prefer reading parquet entries in place from the ZIP; extract only if that isn't possible.
        _archive = MzPeakArchive.TryOpen(path);
        _tempDir = _archive is null ? ExtractZip(path) : null;

        _metaReader = OpenParquet("spectra_metadata.parquet")!;
        _metaCols = BuildColumnIndex(_metaReader.FileMetaData.Schema);
        _numMetaGroups = _metaReader.FileMetaData.NumRowGroups;
        _rgRowOffset = new int[_numMetaGroups + 1];
        for (int g = 0; g < _numMetaGroups; g++)
        {
            using var rgr = _metaReader.RowGroup(g);
            _rgRowOffset[g + 1] = _rgRowOffset[g] + checked((int)rgr.MetaData.NumRows);
        }
        int totalRows = _rgRowOffset[_numMetaGroups];
        SpectrumCount = totalRows;
        _metaGroupLoaded = new bool[_numMetaGroups];

        // Allocate the full-height column arrays once; row-group slices are filled lazily on first
        // access (EnsureGroupLoaded), so opening the file decodes no per-spectrum columns.
        _index = new ulong?[totalRows];
        _id = new string?[totalRows];
        _time = new double?[totalRows];
        _msLevel = new int?[totalRows];
        _representationCurie = new string?[totalRows];
        _scanStartTime = new double?[totalRows];
        _filterString = new string?[totalRows];
        _instrumentConfigRef = new uint?[totalRows];
        _ionInjectionTime = new double?[totalRows];
        _spectrumParamsName = new string?[totalRows][];
        _spectrumParamsAccession = new string?[totalRows][];
        _spectrumParamsValueString = new string?[totalRows][];
        _spectrumParamsValueInteger = new long?[totalRows][];
        _spectrumParamsValueFloat = new double?[totalRows][];
        _spectrumParamsValueBoolean = new bool?[totalRows][];
        _spectrumParamsUnit = new string?[totalRows][];
        _spectrumParamsType = new string?[totalRows][];
        _scanParamsName = new string?[totalRows][];
        _scanParamsAccession = new string?[totalRows][];
        _scanParamsValueString = new string?[totalRows][];
        _scanParamsValueInteger = new long?[totalRows][];
        _scanParamsValueFloat = new double?[totalRows][];
        _scanParamsValueBoolean = new bool?[totalRows][];
        _scanParamsUnit = new string?[totalRows][];
        _scanParamsType = new string?[totalRows][];
        _precursorId = new string?[totalRows];
        _isolationTargetMz = new double?[totalRows];
        _isolationLowerOffset = new double?[totalRows];
        _isolationUpperOffset = new double?[totalRows];
        _collisionEnergy = new double?[totalRows];
        _dissociationMethod = new string?[totalRows];
        _selectedIonMz = new double?[totalRows];
        _selectedIonPeakIntensity = new double?[totalRows];
        _selectedIonChargeState = new long?[totalRows];
        _isoParamsName = new string?[totalRows][];
        _isoParamsAccession = new string?[totalRows][];
        _isoParamsValueString = new string?[totalRows][];
        _isoParamsValueInteger = new long?[totalRows][];
        _isoParamsValueFloat = new double?[totalRows][];
        _isoParamsValueBoolean = new bool?[totalRows][];
        _isoParamsUnit = new string?[totalRows][];
        _isoParamsType = new string?[totalRows][];
        _actParamsName = new string?[totalRows][];
        _actParamsAccession = new string?[totalRows][];
        _actParamsValueString = new string?[totalRows][];
        _actParamsValueInteger = new long?[totalRows][];
        _actParamsValueFloat = new double?[totalRows][];
        _actParamsValueBoolean = new bool?[totalRows][];
        _actParamsUnit = new string?[totalRows][];
        _actParamsType = new string?[totalRows][];
        _siParamsName = new string?[totalRows][];
        _siParamsAccession = new string?[totalRows][];
        _siParamsValueString = new string?[totalRows][];
        _siParamsValueInteger = new long?[totalRows][];
        _siParamsValueFloat = new double?[totalRows][];
        _siParamsValueBoolean = new bool?[totalRows][];
        _siParamsUnit = new string?[totalRows][];
        _siParamsType = new string?[totalRows][];
        _scanWindowLower = new double?[totalRows][];
        _scanWindowUpper = new double?[totalRows][];
        _scanWindowParamsJson = new string?[totalRows];
        _extraScansJson = new string?[totalRows];
        _scanSpectrumRef = new string?[totalRows];
        _scanPolarity = new int?[totalRows];
        _spectrumTypeCurie = new string?[totalRows];
        _numberOfDataPoints = new long?[totalRows];
        _numberOfPeaks = new long?[totalRows];
        _basePeakMz = new double?[totalRows];
        _basePeakIntensity = new double?[totalRows];
        _totalIonCurrent = new double?[totalRows];
        _lowestObservedMz = new double?[totalRows];
        _highestObservedMz = new double?[totalRows];
        _spectrumDataProcessingRef = new string?[totalRows];
        _numberOfAuxiliaryArrays = new int?[totalRows];
        _mzDeltaModel = new double?[totalRows][];
        _scanCombinationCurie = new string?[totalRows];
        _paramGroupRefs = new string?[totalRows][];
        _spectrumAuxArraysJson = new string?[totalRows];
        _valueArrayType = new string?[totalRows];
        _valueArrayUnit = new string?[totalRows];
        _scanSourceIndex = new ulong?[totalRows];
        _scanIonMobilityValue = new double?[totalRows];
        _scanIonMobilityType = new string?[totalRows];
        _scanPresetConfiguration = new long?[totalRows];
        _precursorSourceIndex = new ulong?[totalRows];
        _precursorPrecursorIndex = new ulong?[totalRows];
        _selectedIonSourceIndex = new ulong?[totalRows];
        _selectedIonPrecursorIndex = new ulong?[totalRows];
        _selectedIonIonMobilityValue = new double?[totalRows];
        _selectedIonIonMobilityType = new string?[totalRows];

        // Read eagerly: the two linking columns (fan-out map) and id (SpectrumIdentity list, built
        // at open). Everything else is decoded lazily per row group on first GetSpectrumDescription.
        ReadColumnAllGroups(_index, (rg, n) => ReadNullableValue<ulong>(rg, _metaCols, "spectrum.index", n));
        ReadColumnAllGroups(_precursorSourceIndex, (rg, n) => ReadNullableValue<ulong>(rg, _metaCols, "precursor.source_index", n));
        ReadColumnAllGroups(_id, (rg, n) => ReadNullableString(rg, _metaCols, "spectrum.id", n));

        // Multi-precursor fan-out. A spectrum with N precursors writes N parquet
        // rows: row 0 carries spectrum + scan + precursor[0] + selected_ion[0];
        // rows 1..N-1 carry only precursor[k] + selected_ion[k] (spectrum/scan
        // groups are null). Primary rows are those where spectrum.index is set.
        int rowCount = SpectrumCount;
        var primary = new List<int>(rowCount);
        for (int r = 0; r < rowCount; r++) if (_index[r].HasValue) primary.Add(r);
        _primaryRowOfSpectrum = primary.ToArray();
        SpectrumCount = _primaryRowOfSpectrum.Length;

        var precGroups = new Dictionary<ulong, List<int>>();
        for (int r = 0; r < rowCount; r++)
        {
            if (_precursorSourceIndex[r] is ulong s)
            {
                if (!precGroups.TryGetValue(s, out var list)) precGroups[s] = list = new List<int>();
                list.Add(r);
            }
        }
        _precursorRowsBySpectrum = new int[SpectrumCount][];
        for (int i = 0; i < SpectrumCount; i++)
        {
            var sIdx = _index[_primaryRowOfSpectrum[i]]!.Value;
            _precursorRowsBySpectrum[i] = precGroups.TryGetValue(sIdx, out var rs) ? rs.ToArray() : Array.Empty<int>();
        }

        // Phase 3 — open the lazy point-layout readers (reads only the row-group range KV).
        _dataLayer = new LazyPointLayout(OpenParquet("spectra_data.parquet"));
        _peaksLayer = new LazyPointLayout(OpenParquet("spectra_peaks.parquet"));

        // Phase 4a — file-level metadata is lazily parsed from the same parquet
        // file's KV (the writer puts it there once per file). Snapshot the KV now
        // so we don't have to reopen the file when callers ask for it.
        var kvSnapshot = _metaReader.FileMetaData.KeyValueMetadata.ToDictionary(p => p.Key, p => p.Value);
        _fileMetadata = new Lazy<FileMetadata>(() => FileMetadataDeserializer.Parse(kvSnapshot));

        // Phase 4b — chromatograms. Same column-dispatcher pattern as spectra,
        // separate parquet files. Phase 4b reads scalar columns (id, type CURIE,
        // data_processing_ref) + point-layout (time + intensity); precursor /
        // selected_ion under chromatograms are deferred.
        if (HasEntry("chromatograms_metadata.parquet"))
        {
            using var chromReader = OpenParquet("chromatograms_metadata.parquet")!;
            ChromatogramCount = checked((int)chromReader.FileMetaData.NumRows);
            var chromCols = BuildColumnIndex(chromReader.FileMetaData.Schema);
            using var chromRg = chromReader.RowGroup(0);
            _chromIndex = ReadNullableValue<ulong>(chromRg, chromCols, "chromatogram.index", ChromatogramCount);
            _chromId = ReadNullableString(chromRg, chromCols, "chromatogram.id", ChromatogramCount);
            _chromTypeCurie = ReadNullableString(chromRg, chromCols, "MS:1000626", ChromatogramCount);
            _chromDataProcessingRef = ReadNullableString(chromRg, chromCols, "chromatogram.data_processing_ref", ChromatogramCount);
            _chromTimeUnitCurie = ReadNullableString(chromRg, chromCols, "MS:1000595", ChromatogramCount);
            _chromIntensityUnitCurie = ReadNullableString(chromRg, chromCols, "MS:1000515", ChromatogramCount);
            _chromParamsName = ReadNullableStringList(chromRg, chromCols, "chromatogram.parameters.list.element.name", ChromatogramCount);
            _chromParamsAccession = ReadNullableStringList(chromRg, chromCols, "chromatogram.parameters.list.element.accession", ChromatogramCount);
            _chromParamsValueString = ReadNullableStringList(chromRg, chromCols, "chromatogram.parameters.list.element.value.string", ChromatogramCount);
            _chromParamsValueInteger = ReadNullableValueList<long>(chromRg, chromCols, "chromatogram.parameters.list.element.value.integer", ChromatogramCount);
            _chromParamsValueFloat = ReadNullableValueList<double>(chromRg, chromCols, "chromatogram.parameters.list.element.value.float", ChromatogramCount);
            _chromParamsValueBoolean = ReadNullableValueList<bool>(chromRg, chromCols, "chromatogram.parameters.list.element.value.boolean", ChromatogramCount);
            _chromParamsUnit = ReadNullableStringList(chromRg, chromCols, "chromatogram.parameters.list.element.unit", ChromatogramCount);
            _chromParamsType = ReadNullableStringList(chromRg, chromCols, "chromatogram.parameters.list.element.type", ChromatogramCount);
            _chromAuxArraysJson = ReadNullableString(chromRg, chromCols, "chromatogram.auxiliary_arrays", ChromatogramCount);
        }
        else
        {
            ChromatogramCount = 0;
            _chromIndex = Array.Empty<ulong?>();
            _chromId = Array.Empty<string?>();
            _chromTypeCurie = Array.Empty<string?>();
            _chromDataProcessingRef = Array.Empty<string?>();
            _chromTimeUnitCurie = Array.Empty<string?>();
            _chromIntensityUnitCurie = Array.Empty<string?>();
            _chromParamsName = Array.Empty<string?[]?>();
            _chromParamsAccession = Array.Empty<string?[]?>();
            _chromParamsValueString = Array.Empty<string?[]?>();
            _chromParamsValueInteger = Array.Empty<long?[]?>();
            _chromParamsValueFloat = Array.Empty<double?[]?>();
            _chromParamsValueBoolean = Array.Empty<bool?[]?>();
            _chromParamsUnit = Array.Empty<string?[]?>();
            _chromParamsType = Array.Empty<string?[]?>();
            _chromAuxArraysJson = Array.Empty<string?>();
        }
        _chromData = new LazyPointLayout(OpenParquet("chromatograms_data.parquet"));

        // UV/DAD wavelength spectra live in separate parquet entries (mzPeak.NET). Load them as a
        // secondary spectrum table appended after the MS spectra.
        _msSpectrumCount = SpectrumCount;
        if (HasEntry("wavelength_spectra_metadata.parquet"))
        {
            _wavelength = new WavelengthSpectra(
                OpenParquet("wavelength_spectra_metadata.parquet")!,
                OpenParquet("wavelength_spectra_data.parquet"));
            SpectrumCount = _msSpectrumCount + _wavelength.Count;
        }
    }

    /// <summary>Open a parquet entry — in place from the ZIP archive, or from the extracted temp dir.</summary>
    private ParquetFileReader? OpenParquet(string name)
    {
        if (_archive is not null) return _archive.OpenParquet(name);
        string p = System.IO.Path.Combine(_tempDir!, name);
        return File.Exists(p) ? new ParquetFileReader(p) : null;
    }

    private bool HasEntry(string name) =>
        _archive?.HasEntry(name) ?? File.Exists(System.IO.Path.Combine(_tempDir!, name));

    /// <summary>Get a chromatogram description by row index.</summary>
    public ChromatogramDescription GetChromatogramDescription(int rowIndex)
    {
        if ((uint)rowIndex >= (uint)ChromatogramCount)
            throw new ArgumentOutOfRangeException(nameof(rowIndex));
        return new ChromatogramDescription(
            Index: _chromIndex[rowIndex] ?? 0,
            Id: _chromId[rowIndex] ?? string.Empty,
            ChromatogramTypeCurie: _chromTypeCurie[rowIndex],
            DataProcessingRef: _chromDataProcessingRef[rowIndex],
            TimeUnitCurie: _chromTimeUnitCurie[rowIndex],
            IntensityUnitCurie: _chromIntensityUnitCurie[rowIndex],
            Parameters: ZipParams(
                _chromParamsName[rowIndex], _chromParamsAccession[rowIndex],
                _chromParamsValueString[rowIndex], _chromParamsValueInteger[rowIndex],
                _chromParamsValueFloat[rowIndex], _chromParamsValueBoolean[rowIndex],
                _chromParamsUnit[rowIndex], _chromParamsType[rowIndex]),
            AuxArrays: AuxiliaryArrays.Parse(_chromAuxArraysJson[rowIndex]));
    }

    /// <summary>Get a chromatogram's (time, intensity) arrays. Returns null when
    /// no point rows reference this chromatogram in chromatograms_data.parquet.</summary>
    public ChromatogramArrays? GetChromatogramData(int rowIndex)
    {
        if ((uint)rowIndex >= (uint)ChromatogramCount)
            throw new ArgumentOutOfRangeException(nameof(rowIndex));
        // Key on the stored chromatogram.index (see GetSpectrumData for the rationale).
        long key = (long)(_chromIndex[rowIndex] ?? (ulong)rowIndex);
        if (_chromData.Get(key) is { } d) return new ChromatogramArrays(d.Value, d.Intensity);
        return null;
    }

    private void ReadColumnAllGroups<T>(T[] dest, Func<RowGroupReader, int, T[]> read)
    {
        for (int g = 0; g < _numMetaGroups; g++)
        {
            using var rg = _metaReader.RowGroup(g);
            int n = _rgRowOffset[g + 1] - _rgRowOffset[g];
            Array.Copy(read(rg, n), 0, dest, _rgRowOffset[g], n);
        }
    }

    private static void CopyInto<T>(T[] dest, T[] src, int off) => Array.Copy(src, 0, dest, off, src.Length);

    private int GroupOfRow(int row)
    {
        int lo = 0, hi = _numMetaGroups - 1;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) >> 1;
            if (_rgRowOffset[mid] <= row) lo = mid; else hi = mid - 1;
        }
        return lo;
    }

    private void EnsureGroupLoaded(int g)
    {
        if (_metaGroupLoaded[g]) return;
        lock (_metaLock)
        {
            if (_metaGroupLoaded[g]) return;
            LoadMetaGroup(g);
            _metaGroupLoaded[g] = true;
        }
    }

    /// <summary>Decode every spectra-metadata column for one row group into its slice of the full arrays.</summary>
    private void LoadMetaGroup(int g)
    {
        using var rg = _metaReader.RowGroup(g);
        int n = _rgRowOffset[g + 1] - _rgRowOffset[g];
        int off = _rgRowOffset[g];

        CopyInto(_id, ReadNullableString(rg, _metaCols, "spectrum.id", n), off);
        CopyInto(_time, ReadNullableValue<double>(rg, _metaCols, "spectrum.time", n), off);
        CopyInto(_msLevel, ReadNullableInt8(rg, _metaCols, "MS:1000511", n), off);
        CopyInto(_representationCurie, ReadNullableString(rg, _metaCols, "MS:1000525", n), off);
        CopyInto(_scanStartTime, ReadNullableValue<double>(rg, _metaCols, "MS:1000016", n), off);
        CopyInto(_filterString, ReadNullableString(rg, _metaCols, "MS:1000512", n), off);
        CopyInto(_instrumentConfigRef, ReadNullableValue<uint>(rg, _metaCols, "scan.instrument_configuration_ref", n), off);
        CopyInto(_ionInjectionTime, ReadNullableValue<double>(rg, _metaCols, "MS:1000927", n), off);

        CopyInto(_spectrumParamsName, ReadNullableStringList(rg, _metaCols, "spectrum.parameters.list.element.name", n), off);
        CopyInto(_spectrumParamsAccession, ReadNullableStringList(rg, _metaCols, "spectrum.parameters.list.element.accession", n), off);
        CopyInto(_spectrumParamsValueString, ReadNullableStringList(rg, _metaCols, "spectrum.parameters.list.element.value.string", n), off);
        CopyInto(_spectrumParamsValueInteger, ReadNullableValueList<long>(rg, _metaCols, "spectrum.parameters.list.element.value.integer", n), off);
        CopyInto(_spectrumParamsValueFloat, ReadNullableValueList<double>(rg, _metaCols, "spectrum.parameters.list.element.value.float", n), off);
        CopyInto(_spectrumParamsValueBoolean, ReadNullableValueList<bool>(rg, _metaCols, "spectrum.parameters.list.element.value.boolean", n), off);
        CopyInto(_spectrumParamsUnit, ReadNullableStringList(rg, _metaCols, "spectrum.parameters.list.element.unit", n), off);
        CopyInto(_spectrumParamsType, ReadNullableStringList(rg, _metaCols, "spectrum.parameters.list.element.type", n), off);

        CopyInto(_scanParamsName, ReadNullableStringList(rg, _metaCols, "scan.parameters.list.element.name", n), off);
        CopyInto(_scanParamsAccession, ReadNullableStringList(rg, _metaCols, "scan.parameters.list.element.accession", n), off);
        CopyInto(_scanParamsValueString, ReadNullableStringList(rg, _metaCols, "scan.parameters.list.element.value.string", n), off);
        CopyInto(_scanParamsValueInteger, ReadNullableValueList<long>(rg, _metaCols, "scan.parameters.list.element.value.integer", n), off);
        CopyInto(_scanParamsValueFloat, ReadNullableValueList<double>(rg, _metaCols, "scan.parameters.list.element.value.float", n), off);
        CopyInto(_scanParamsValueBoolean, ReadNullableValueList<bool>(rg, _metaCols, "scan.parameters.list.element.value.boolean", n), off);
        CopyInto(_scanParamsUnit, ReadNullableStringList(rg, _metaCols, "scan.parameters.list.element.unit", n), off);
        CopyInto(_scanParamsType, ReadNullableStringList(rg, _metaCols, "scan.parameters.list.element.type", n), off);

        CopyInto(_precursorId, ReadNullableString(rg, _metaCols, "precursor.precursor_id", n), off);
        CopyInto(_isolationTargetMz, ReadNullableValue<double>(rg, _metaCols, "MS:1000827", n), off);
        CopyInto(_isolationLowerOffset, ReadNullableValue<double>(rg, _metaCols, "MS:1000828", n), off);
        CopyInto(_isolationUpperOffset, ReadNullableValue<double>(rg, _metaCols, "MS:1000829", n), off);
        CopyInto(_collisionEnergy, ReadNullableValue<double>(rg, _metaCols, "MS:1000045", n), off);
        CopyInto(_dissociationMethod, ReadNullableString(rg, _metaCols, "MS:1000044", n), off);
        CopyInto(_selectedIonMz, ReadNullableValue<double>(rg, _metaCols, "MS:1000744", n), off);
        CopyInto(_selectedIonPeakIntensity, ReadNullableValue<double>(rg, _metaCols, "MS:1000042", n), off);
        CopyInto(_selectedIonChargeState, ReadNullableValue<long>(rg, _metaCols, "MS:1000041", n), off);

        CopyInto(_isoParamsName, ReadNullableStringList(rg, _metaCols, "precursor.isolation_window.parameters.list.element.name", n), off);
        CopyInto(_isoParamsAccession, ReadNullableStringList(rg, _metaCols, "precursor.isolation_window.parameters.list.element.accession", n), off);
        CopyInto(_isoParamsValueString, ReadNullableStringList(rg, _metaCols, "precursor.isolation_window.parameters.list.element.value.string", n), off);
        CopyInto(_isoParamsValueInteger, ReadNullableValueList<long>(rg, _metaCols, "precursor.isolation_window.parameters.list.element.value.integer", n), off);
        CopyInto(_isoParamsValueFloat, ReadNullableValueList<double>(rg, _metaCols, "precursor.isolation_window.parameters.list.element.value.float", n), off);
        CopyInto(_isoParamsValueBoolean, ReadNullableValueList<bool>(rg, _metaCols, "precursor.isolation_window.parameters.list.element.value.boolean", n), off);
        CopyInto(_isoParamsUnit, ReadNullableStringList(rg, _metaCols, "precursor.isolation_window.parameters.list.element.unit", n), off);
        CopyInto(_isoParamsType, ReadNullableStringList(rg, _metaCols, "precursor.isolation_window.parameters.list.element.type", n), off);

        CopyInto(_actParamsName, ReadNullableStringList(rg, _metaCols, "precursor.activation.parameters.list.element.name", n), off);
        CopyInto(_actParamsAccession, ReadNullableStringList(rg, _metaCols, "precursor.activation.parameters.list.element.accession", n), off);
        CopyInto(_actParamsValueString, ReadNullableStringList(rg, _metaCols, "precursor.activation.parameters.list.element.value.string", n), off);
        CopyInto(_actParamsValueInteger, ReadNullableValueList<long>(rg, _metaCols, "precursor.activation.parameters.list.element.value.integer", n), off);
        CopyInto(_actParamsValueFloat, ReadNullableValueList<double>(rg, _metaCols, "precursor.activation.parameters.list.element.value.float", n), off);
        CopyInto(_actParamsValueBoolean, ReadNullableValueList<bool>(rg, _metaCols, "precursor.activation.parameters.list.element.value.boolean", n), off);
        CopyInto(_actParamsUnit, ReadNullableStringList(rg, _metaCols, "precursor.activation.parameters.list.element.unit", n), off);
        CopyInto(_actParamsType, ReadNullableStringList(rg, _metaCols, "precursor.activation.parameters.list.element.type", n), off);

        CopyInto(_siParamsName, ReadNullableStringList(rg, _metaCols, "selected_ion.parameters.list.element.name", n), off);
        CopyInto(_siParamsAccession, ReadNullableStringList(rg, _metaCols, "selected_ion.parameters.list.element.accession", n), off);
        CopyInto(_siParamsValueString, ReadNullableStringList(rg, _metaCols, "selected_ion.parameters.list.element.value.string", n), off);
        CopyInto(_siParamsValueInteger, ReadNullableValueList<long>(rg, _metaCols, "selected_ion.parameters.list.element.value.integer", n), off);
        CopyInto(_siParamsValueFloat, ReadNullableValueList<double>(rg, _metaCols, "selected_ion.parameters.list.element.value.float", n), off);
        CopyInto(_siParamsValueBoolean, ReadNullableValueList<bool>(rg, _metaCols, "selected_ion.parameters.list.element.value.boolean", n), off);
        CopyInto(_siParamsUnit, ReadNullableStringList(rg, _metaCols, "selected_ion.parameters.list.element.unit", n), off);
        CopyInto(_siParamsType, ReadNullableStringList(rg, _metaCols, "selected_ion.parameters.list.element.type", n), off);

        CopyInto(_scanWindowLower, ReadNullableValueList<double>(rg, _metaCols, "MS:1000501", n), off);
        CopyInto(_scanWindowUpper, ReadNullableValueList<double>(rg, _metaCols, "MS:1000500", n), off);
        CopyInto(_scanWindowParamsJson, ReadNullableString(rg, _metaCols, "scan.scan_window_params", n), off);
        CopyInto(_extraScansJson, ReadNullableString(rg, _metaCols, "scan.extra_scans", n), off);
        CopyInto(_scanSpectrumRef, ReadNullableString(rg, _metaCols, "scan.spectrum_ref", n), off);

        CopyInto(_scanPolarity, ReadNullableInt8(rg, _metaCols, "MS:1000465", n), off);
        CopyInto(_spectrumTypeCurie, ReadNullableString(rg, _metaCols, "MS:1000559", n), off);
        CopyInto(_numberOfDataPoints, ReadNullableValue<long>(rg, _metaCols, "MS:1003060", n), off);
        CopyInto(_numberOfPeaks, ReadNullableValue<long>(rg, _metaCols, "MS:1003059", n), off);
        CopyInto(_basePeakMz, ReadNullableValue<double>(rg, _metaCols, "MS:1000504", n), off);
        CopyInto(_basePeakIntensity, ReadNullableValue<double>(rg, _metaCols, "MS:1000505", n), off);
        CopyInto(_totalIonCurrent, ReadNullableValue<double>(rg, _metaCols, "MS:1000285", n), off);
        CopyInto(_lowestObservedMz, ReadNullableValue<double>(rg, _metaCols, "MS:1000528", n), off);
        CopyInto(_highestObservedMz, ReadNullableValue<double>(rg, _metaCols, "MS:1000527", n), off);
        CopyInto(_spectrumDataProcessingRef, ReadNullableString(rg, _metaCols, "spectrum.data_processing_ref", n), off);
        CopyInto(_numberOfAuxiliaryArrays, ReadNullableValueAsInt(rg, _metaCols, "spectrum.number_of_auxiliary_arrays", n), off);

        CopyInto(_mzDeltaModel, ReadNullableValueList<double>(rg, _metaCols, "spectrum.mz_delta_model.list.element", n), off);
        CopyInto(_scanCombinationCurie, ReadNullableString(rg, _metaCols, "MS:1000570", n), off);
        CopyInto(_paramGroupRefs, ReadNullableStringList(rg, _metaCols, "spectrum.param_group_refs.list.element", n), off);
        CopyInto(_spectrumAuxArraysJson, ReadNullableString(rg, _metaCols, "spectrum.auxiliary_arrays", n), off);
        CopyInto(_valueArrayType, ReadNullableString(rg, _metaCols, "spectrum.value_array_type", n), off);
        CopyInto(_valueArrayUnit, ReadNullableString(rg, _metaCols, "spectrum.value_array_unit", n), off);

        CopyInto(_scanSourceIndex, ReadNullableValue<ulong>(rg, _metaCols, "scan.source_index", n), off);
        CopyInto(_scanIonMobilityValue, ReadNullableValue<double>(rg, _metaCols, "scan.ion_mobility_value", n), off);
        CopyInto(_scanIonMobilityType, ReadNullableString(rg, _metaCols, "scan.ion_mobility_type", n), off);
        CopyInto(_scanPresetConfiguration, ReadNullableValue<long>(rg, _metaCols, "MS:1000616", n), off);

        CopyInto(_precursorPrecursorIndex, ReadNullableValue<ulong>(rg, _metaCols, "precursor.precursor_index", n), off);
        CopyInto(_selectedIonSourceIndex, ReadNullableValue<ulong>(rg, _metaCols, "selected_ion.source_index", n), off);
        CopyInto(_selectedIonPrecursorIndex, ReadNullableValue<ulong>(rg, _metaCols, "selected_ion.precursor_index", n), off);
        CopyInto(_selectedIonIonMobilityValue, ReadNullableValue<double>(rg, _metaCols, "selected_ion.ion_mobility_value", n), off);
        CopyInto(_selectedIonIonMobilityType, ReadNullableString(rg, _metaCols, "selected_ion.ion_mobility_type", n), off);
    }

    /// <summary>The spectrum's id without forcing a lazy metadata-group load (id is read eagerly at open).</summary>
    public string GetSpectrumId(int spectrumIndex)
    {
        if ((uint)spectrumIndex >= (uint)SpectrumCount)
            throw new ArgumentOutOfRangeException(nameof(spectrumIndex));
        if (spectrumIndex >= _msSpectrumCount)
            return _wavelength!.GetId(spectrumIndex - _msSpectrumCount);
        return _id[_primaryRowOfSpectrum[spectrumIndex]] ?? string.Empty;
    }

    public SpectrumDescription GetSpectrumDescription(int spectrumIndex)
    {
        if ((uint)spectrumIndex >= (uint)SpectrumCount)
            throw new ArgumentOutOfRangeException(nameof(spectrumIndex));
        if (spectrumIndex >= _msSpectrumCount)
            return _wavelength!.GetDescription(spectrumIndex - _msSpectrumCount);

        int row = _primaryRowOfSpectrum[spectrumIndex];

        // Lazily decode the row group(s) holding this spectrum's rows before reading columns. A
        // spectrum's fan-out rows are kept in one group by the writer, so this is typically one group.
        EnsureGroupLoaded(GroupOfRow(row));
        foreach (var pr in _precursorRowsBySpectrum[spectrumIndex]) EnsureGroupLoaded(GroupOfRow(pr));

        var curie = _representationCurie[row];
        var spectrumParams = ZipParams(
            _spectrumParamsName[row], _spectrumParamsAccession[row],
            _spectrumParamsValueString[row], _spectrumParamsValueInteger[row],
            _spectrumParamsValueFloat[row], _spectrumParamsValueBoolean[row],
            _spectrumParamsUnit[row], _spectrumParamsType[row]);
        var scanParams = ZipParams(
            _scanParamsName[row], _scanParamsAccession[row],
            _scanParamsValueString[row], _scanParamsValueInteger[row],
            _scanParamsValueFloat[row], _scanParamsValueBoolean[row],
            _scanParamsUnit[row], _scanParamsType[row]);

        var scanWindows = ZipScanWindows(_scanWindowLower[row], _scanWindowUpper[row]);

        ScanInfo? scan = (_scanStartTime[row] is null
                && _filterString[row] is null
                && _instrumentConfigRef[row] is null
                && _ionInjectionTime[row] is null
                && scanParams.Count == 0
                && scanWindows.Count == 0
                && _extraScansJson[row] is null
                && _scanSpectrumRef[row] is null)
            ? null
            : new ScanInfo(
                StartTime: _scanStartTime[row],
                FilterString: _filterString[row],
                InstrumentConfigurationRef: _instrumentConfigRef[row],
                IonInjectionTime: _ionInjectionTime[row],
                Parameters: scanParams,
                ScanWindows: scanWindows,
                SourceIndex: _scanSourceIndex[row],
                IonMobilityValue: _scanIonMobilityValue[row],
                IonMobilityTypeCurie: _scanIonMobilityType[row],
                PresetScanConfiguration: _scanPresetConfiguration[row],
                ScanWindowParamsJson: _scanWindowParamsJson[row],
                ExtraScansJson: _extraScansJson[row],
                SpectrumRef: _scanSpectrumRef[row]);

        // Gather every precursor row belonging to this spectrum. The fan-out
        // writer guarantees one parquet row per precursor; rows that don't
        // populate any precursor field (precursor.source_index null) aren't
        // listed here, so empty Precursors == "spectrum has no precursors".
        var precursors = new List<PrecursorInfo>(_precursorRowsBySpectrum[spectrumIndex].Length);
        foreach (var pRow in _precursorRowsBySpectrum[spectrumIndex])
        {
            var isoParams = ZipParams(
                _isoParamsName[pRow], _isoParamsAccession[pRow],
                _isoParamsValueString[pRow], _isoParamsValueInteger[pRow],
                _isoParamsValueFloat[pRow], _isoParamsValueBoolean[pRow],
                _isoParamsUnit[pRow], _isoParamsType[pRow]);
            var actParams = ZipParams(
                _actParamsName[pRow], _actParamsAccession[pRow],
                _actParamsValueString[pRow], _actParamsValueInteger[pRow],
                _actParamsValueFloat[pRow], _actParamsValueBoolean[pRow],
                _actParamsUnit[pRow], _actParamsType[pRow]);
            var siParams = ZipParams(
                _siParamsName[pRow], _siParamsAccession[pRow],
                _siParamsValueString[pRow], _siParamsValueInteger[pRow],
                _siParamsValueFloat[pRow], _siParamsValueBoolean[pRow],
                _siParamsUnit[pRow], _siParamsType[pRow]);

            var isolation = (_isolationTargetMz[pRow], _isolationLowerOffset[pRow], _isolationUpperOffset[pRow], isoParams.Count) switch
            {
                (null, null, null, 0) => null,
                _ => new IsolationWindow(_isolationTargetMz[pRow], _isolationLowerOffset[pRow], _isolationUpperOffset[pRow], isoParams),
            };
            var activation = (_collisionEnergy[pRow], _dissociationMethod[pRow], actParams.Count) switch
            {
                (null, null, 0) => null,
                _ => new Activation(_collisionEnergy[pRow], _dissociationMethod[pRow], actParams),
            };
            var selectedIon = (_selectedIonMz[pRow], _selectedIonPeakIntensity[pRow], _selectedIonChargeState[pRow], siParams.Count) switch
            {
                (null, null, null, 0) => null,
                _ => new SelectedIonInfo(
                    Mz: _selectedIonMz[pRow],
                    PeakIntensity: _selectedIonPeakIntensity[pRow],
                    ChargeState: _selectedIonChargeState[pRow],
                    Parameters: siParams,
                    SourceIndex: _selectedIonSourceIndex[pRow],
                    PrecursorIndex: _selectedIonPrecursorIndex[pRow],
                    IonMobilityValue: _selectedIonIonMobilityValue[pRow],
                    IonMobilityTypeCurie: _selectedIonIonMobilityType[pRow]),
            };

            if (_precursorId[pRow] is null && isolation is null && activation is null && selectedIon is null)
                continue;

            precursors.Add(new PrecursorInfo(
                PrecursorId: _precursorId[pRow],
                IsolationWindow: isolation,
                Activation: activation,
                SelectedIon: selectedIon,
                SourceIndex: _precursorSourceIndex[pRow],
                PrecursorIndex: _precursorPrecursorIndex[pRow]));
        }

        IReadOnlyList<double>? mzDeltaModel = null;
        if (_mzDeltaModel[row] is { } modelArr && modelArr.Length > 0)
            mzDeltaModel = modelArr.Where(d => d.HasValue).Select(d => d!.Value).ToArray();

        IReadOnlyList<string>? paramGroupRefs = null;
        if (_paramGroupRefs[row] is { } refArr && refArr.Length > 0)
            paramGroupRefs = refArr.Where(s => s is not null).Select(s => s!).ToArray();

        return new SpectrumDescription(
            Index: _index[row] ?? 0,
            Id: _id[row] ?? string.Empty,
            Time: _time[row] ?? 0.0,
            MsLevel: _msLevel[row],
            IsProfile: curie == CurieProfileSpectrum,
            IsCentroid: curie == CurieCentroidSpectrum,
            RepresentationCurie: curie,
            Scan: scan,
            Precursors: precursors,
            Parameters: spectrumParams,
            ScanPolarity: _scanPolarity[row],
            SpectrumTypeCurie: _spectrumTypeCurie[row],
            NumberOfDataPoints: _numberOfDataPoints[row],
            NumberOfPeaks: _numberOfPeaks[row],
            BasePeakMz: _basePeakMz[row],
            BasePeakIntensity: _basePeakIntensity[row],
            TotalIonCurrent: _totalIonCurrent[row],
            LowestObservedMz: _lowestObservedMz[row],
            HighestObservedMz: _highestObservedMz[row],
            DataProcessingRef: _spectrumDataProcessingRef[row],
            NumberOfAuxiliaryArrays: _numberOfAuxiliaryArrays[row],
            MzDeltaModel: mzDeltaModel,
            ScanCombinationCurie: _scanCombinationCurie[row],
            ParamGroupRefs: paramGroupRefs,
            AuxArrays: AuxiliaryArrays.Parse(_spectrumAuxArraysJson[row]),
            ValueArrayCurie: _valueArrayType[row],
            ValueArrayUnitCurie: _valueArrayUnit[row]);
    }

    /// <summary>
    /// Returns the canonical mz/intensity arrays for a spectrum. Falls back to the
    /// supplementary peaks layer only when the data layer has no rows for this
    /// spectrum (shouldn't happen for a valid file). Whether the data is profile
    /// or centroid is told by the spectrum description's CV classification, not
    /// by which file the data lived in.
    /// </summary>
    public BinaryArrays? GetSpectrumData(int spectrumIndex)
    {
        if ((uint)spectrumIndex >= (uint)SpectrumCount)
            throw new ArgumentOutOfRangeException(nameof(spectrumIndex));
        if (spectrumIndex >= _msSpectrumCount)
            return _wavelength!.GetData(spectrumIndex - _msSpectrumCount);

        // The point layer keys on the spectrum's stored spectrum.index, which equals the logical
        // position for pwiz-written files but need not for cross-stack / filtered lists — so look up
        // by the stored index, not the row position.
        long key = SpectrumDataKey(spectrumIndex);
        if (_dataLayer.Get(key) is { } d) return new BinaryArrays(d.Value, d.Intensity);
        if (_peaksLayer.Get(key) is { } p) return new BinaryArrays(p.Value, p.Intensity);
        return null;
    }

    private long SpectrumDataKey(int spectrumIndex) =>
        (long)(_index[_primaryRowOfSpectrum[spectrumIndex]] ?? (ulong)spectrumIndex);

    /// <summary>
    /// Returns the supplementary centroid-peak layer for a spectrum (typically only
    /// populated for profile spectra that were peak-picked alongside the profile signal).
    /// Returns null when no supplementary peaks were stored for this spectrum.
    /// </summary>
    public BinaryArrays? GetSupplementaryPeaks(int spectrumIndex)
    {
        if ((uint)spectrumIndex >= (uint)SpectrumCount)
            throw new ArgumentOutOfRangeException(nameof(spectrumIndex));
        // Wavelength spectra have no supplementary peaks layer.
        if (spectrumIndex >= _msSpectrumCount) return null;
        if (_peaksLayer.Get(SpectrumDataKey(spectrumIndex)) is { } p) return new BinaryArrays(p.Value, p.Intensity);
        return null;
    }

    public void Dispose()
    {
        // Close the open parquet readers (and their archive sub-streams) before removing any temp dir.
        _metaReader?.Dispose();
        _dataLayer?.Dispose();
        _peaksLayer?.Dispose();
        _chromData?.Dispose();
        _wavelength?.Dispose();
        _archive?.Dispose();
        if (_tempDir is not null)
            try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort */ }
    }

    // ----------------------- helpers -----------------------

    private static string ExtractZip(string archivePath)
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"mzpeak-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        ZipFile.ExtractToDirectory(archivePath, dir);
        return dir;
    }

    private static readonly Regex ListItemTrailerRegex = new(@"\.list\.(item|element)$", RegexOptions.Compiled);

    private static Dictionary<string, int> BuildColumnIndex(SchemaDescriptor schema)
    {
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < schema.NumColumns; i++)
        {
            var col = schema.Column(i);
            var dotPath = col.Path.ToDotString();
            map[dotPath] = i;

            // Also register the path with any trailing list-element decoration stripped,
            // so callers that look up the Arrow-style path get the same column when the
            // file was written by ParquetSharp (which appends `.list.item`). Real mzPeak
            // files use `.list.element` already embedded mid-path; their leaf path has
            // no trailing decoration, so this branch is a no-op for them.
            var stripped = ListItemTrailerRegex.Replace(dotPath, "");
            if (!map.ContainsKey(stripped)) map[stripped] = i;

            var match = CvColumnRegex.Match(dotPath);
            if (match.Success)
            {
                var accession = $"MS:{match.Groups["acc"].Value}";
                if (!map.ContainsKey(accession))
                    map[accession] = i;
            }
        }
        return map;
    }

    // Lenient column reads — Phase 6 needs the reader to tolerate writers that
    // emit a subset of the spec's columns (e.g. our minimal Phase 6 writer
    // skips nested list columns). Missing columns return empty arrays of the
    // requested length so downstream code can treat absent == empty uniformly.
    // A column that IS present but whose physical type none of the adaptive
    // candidates can decode throws instead — silently returning empties there
    // would masquerade as an absent column and hide real data loss.
    private static T?[] ReadNullableValue<T>(RowGroupReader rg, Dictionary<string, int> indices, string key, int rowCount) where T : unmanaged
    {
        if (!indices.TryGetValue(key, out int col)) return new T?[rowCount];

        // Fast path: the column's element type matches T exactly (always true for pwiz-written files).
        if (TryRead<T, T>(rg, col, rowCount, v => v, out var exact)) return exact;

        // Adaptive path: another stack (e.g. mzPeak.NET) wrote a narrower/different physical type —
        // double scalars as float32, int64 scalars as int32, etc. Read the actual type and convert.
        if (typeof(T) == typeof(double))
        {
            if (TryRead<float, T>(rg, col, rowCount, v => (T)(object)(double)v, out var r)) return r;
        }
        else if (typeof(T) == typeof(long))
        {
            if (TryRead<int, T>(rg, col, rowCount, v => (T)(object)(long)v, out var r)) return r;
            // mzPeak.NET writes the count fields (number_of_data_points / number_of_peaks) as unsigned.
            if (TryRead<ulong, T>(rg, col, rowCount, v => (T)(object)(long)v, out var r2)) return r2;
            if (TryRead<uint, T>(rg, col, rowCount, v => (T)(object)(long)v, out var r3)) return r3;
        }
        else if (typeof(T) == typeof(ulong))
        {
            if (TryRead<long, T>(rg, col, rowCount, v => (T)(object)(ulong)v, out var r)) return r;
            if (TryRead<uint, T>(rg, col, rowCount, v => (T)(object)(ulong)v, out var r2)) return r2;
            if (TryRead<int, T>(rg, col, rowCount, v => (T)(object)(ulong)v, out var r3)) return r3;
        }
        else if (typeof(T) == typeof(uint))
        {
            if (TryRead<int, T>(rg, col, rowCount, v => (T)(object)(uint)v, out var r)) return r;
            if (TryRead<long, T>(rg, col, rowCount, v => (T)(object)(uint)v, out var r2)) return r2;
        }
        throw UnsupportedColumnType(rg, col, key, typeof(T));
    }

    /// <summary>Read a column as nullable <typeparamref name="TNat"/> and convert each value to T; false on type mismatch.</summary>
    private static bool TryRead<TNat, T>(RowGroupReader rg, int col, int rowCount, Func<TNat, T> conv, out T?[] result)
        where TNat : unmanaged where T : unmanaged
    {
        try
        {
            using var cr = rg.Column(col).LogicalReader<TNat?>();
            var buf = new TNat?[rowCount];
            cr.ReadBatch(buf.AsSpan());
            var outBuf = new T?[rowCount];
            for (int i = 0; i < rowCount; i++)
                if (buf[i] is TNat v) outBuf[i] = conv(v);
            result = outBuf;
            return true;
        }
        catch (InvalidCastException)
        {
            result = null!;
            return false;
        }
    }

    /// <summary>Error for a column that exists but whose physical type none of the adaptive read
    /// candidates could decode. Failing loudly here (rather than returning empty/NaN arrays that
    /// look like an absent column) surfaces an unsupported file instead of silently losing data.</summary>
    private static Exception UnsupportedColumnType(RowGroupReader rg, int col, string label, Type requested)
    {
        string physical;
        try { using var cc = rg.MetaData.GetColumnChunkMetaData(col); physical = cc.Type.ToString(); }
        catch { physical = "unknown"; }
        return new NotSupportedException(
            $"mzPeak column '{label}' has physical type {physical}, which the reader cannot convert to " +
            $"{requested.Name}. The file may use a type or encoding from another writer that this reader does not support.");
    }

    private static string?[] ReadNullableString(RowGroupReader rg, Dictionary<string, int> indices, string key, int rowCount)
    {
        if (!indices.TryGetValue(key, out int col)) return new string?[rowCount];
        using var cr = rg.Column(col).LogicalReader<string?>();
        var buf = new string?[rowCount];
        cr.ReadBatch(buf.AsSpan());
        return buf;
    }

    private static int?[] ReadNullableInt8(RowGroupReader rg, Dictionary<string, int> indices, string key, int rowCount)
    {
        if (!indices.TryGetValue(key, out int col)) return new int?[rowCount];
        // 8-bit columns may be written signed (our writer, needed for scan_polarity ±1) or unsigned
        // (mzPeak.NET writes ms_level as uint8). Widen either to int via the shared adaptive reader.
        if (TryRead<sbyte, int>(rg, col, rowCount, v => v, out var r)) return r;
        if (TryRead<byte, int>(rg, col, rowCount, v => v, out var r2)) return r2;
        throw UnsupportedColumnType(rg, col, key, typeof(int));
    }

    /// <summary>Reads a small-integer column as nullable int, tolerating the actual physical width/signedness.</summary>
    private static int?[] ReadNullableValueAsInt(RowGroupReader rg, Dictionary<string, int> indices, string key, int rowCount)
    {
        if (!indices.TryGetValue(key, out int col)) return new int?[rowCount];
        // Fast path: our writer uses Int32. Other stacks may write UInt32 (mzPeak.NET's
        // number_of_auxiliary_arrays) or narrower widths — read the actual type and widen.
        if (TryRead<int, int>(rg, col, rowCount, v => v, out var r)) return r;
        if (TryRead<uint, int>(rg, col, rowCount, v => (int)v, out var r2)) return r2;
        if (TryRead<short, int>(rg, col, rowCount, v => v, out var r3)) return r3;
        if (TryRead<ushort, int>(rg, col, rowCount, v => v, out var r4)) return r4;
        throw UnsupportedColumnType(rg, col, key, typeof(int));
    }

    private static string?[]?[] ReadNullableStringList(RowGroupReader rg, Dictionary<string, int> indices, string key, int rowCount)
    {
        if (!indices.TryGetValue(key, out int col)) return new string?[rowCount][];
        using var cr = rg.Column(col).LogicalReader<string?[]>();
        var buf = new string?[rowCount][];
        cr.ReadBatch(buf.AsSpan());
        return buf;
    }

    private static T?[]?[] ReadNullableValueList<T>(RowGroupReader rg, Dictionary<string, int> indices, string key, int rowCount) where T : unmanaged
    {
        if (!indices.TryGetValue(key, out int col)) return new T?[rowCount][];
        try
        {
            using var cr = rg.Column(col).LogicalReader<T?[]>();
            var buf = new T?[rowCount][];
            cr.ReadBatch(buf.AsSpan());
            return buf;
        }
        catch (InvalidCastException) when (typeof(T) == typeof(double))
        {
            // float32 list column written by another stack; widen each element to double.
            using var cr = rg.Column(col).LogicalReader<float?[]>();
            var fbuf = new float?[rowCount][];
            cr.ReadBatch(fbuf.AsSpan());
            var buf = new T?[rowCount][];
            for (int i = 0; i < rowCount; i++)
            {
                if (fbuf[i] is not { } fa) continue;
                var outArr = new T?[fa.Length];
                for (int j = 0; j < fa.Length; j++)
                    if (fa[j] is float f) outArr[j] = (T)(object)(double)f;
                buf[i] = outArr;
            }
            return buf;
        }
    }

    /// <summary>
    /// Reconstruct a list of <see cref="CvParam"/> by zipping the seven parallel leaf-column
    /// arrays for one row. The shortest non-null array's length wins (defensive against
    /// writer-side parallel-array length drift); in practice all arrays are the same length
    /// per the mzPeak spec.
    /// </summary>
    private static IReadOnlyList<CvParam> ZipParams(
        string?[]? name, string?[]? accession,
        string?[]? valueString, long?[]? valueInteger,
        double?[]? valueFloat, bool?[]? valueBoolean,
        string?[]? unit, string?[]? type = null)
    {
        int n = MinNonNullLength(name, accession, valueString, valueInteger, valueFloat, valueBoolean, unit);
        if (n == 0) return Array.Empty<CvParam>();
        var list = new CvParam[n];
        for (int i = 0; i < n; i++)
        {
            list[i] = new CvParam(
                Name: name?[i], Accession: accession?[i],
                ValueString: valueString?[i], ValueInteger: valueInteger?[i],
                ValueFloat: valueFloat?[i], ValueBoolean: valueBoolean?[i],
                Unit: unit?[i], Type: type?[i]);
        }
        return list;
    }

    private static IReadOnlyList<ScanWindow> ZipScanWindows(double?[]? lower, double?[]? upper)
    {
        int n = MinNonNullLength(lower, upper);
        if (n == 0) return Array.Empty<ScanWindow>();
        var list = new ScanWindow[n];
        for (int i = 0; i < n; i++)
            list[i] = new ScanWindow(lower?[i], upper?[i]);
        return list;
    }

    private static int MinNonNullLength(params Array?[] arrays)
    {
        int min = int.MaxValue;
        bool any = false;
        foreach (var a in arrays)
        {
            if (a is null) continue;
            any = true;
            if (a.Length < min) min = a.Length;
        }
        return any ? min : 0;
    }

    /// <summary>
    /// UV/DAD wavelength spectra read from the dedicated <c>wavelength_spectra_metadata.parquet</c> /
    /// <c>wavelength_spectra_data.parquet</c> entries some writers (mzPeak.NET) use instead of inlining
    /// them in <c>spectra_metadata</c>. The schema mirrors a normal spectrum table except the value
    /// array is wavelength (nm) rather than m/z, and there are no ms_level/polarity/precursor columns.
    /// These tables are secondary and typically far smaller than the MS table, so columns are read
    /// eagerly at open; binary data still flows through a lazy point layout. Logical spectrum indices
    /// at or above the MS spectrum count route here (index − msCount → local row).
    /// </summary>
    private sealed class WavelengthSpectra : IDisposable
    {
        private const string WavelengthArrayCurie = "MS:1000617"; // wavelength array
        private const string NanometerCurie = "UO:0000018";       // nanometer

        public int Count { get; }

        private readonly ulong?[] _index;
        private readonly string?[] _id, _representationCurie, _typeCurie, _filterString, _dpRef, _auxJson, _scanIonMobType;
        private readonly double?[] _time, _scanStartTime, _ionInjTime, _ionMobValue,
            _lowestWl, _highestWl, _lambdaMax, _basePeakIntensity, _tic;
        private readonly long?[] _numDataPoints, _presetScanConfig;
        private readonly uint?[] _instrConfigRef;
        private readonly int?[] _numAuxArrays;
        private readonly ulong?[] _scanSourceIndex;

        private readonly string?[]?[] _spName, _spAcc, _spValStr, _spUnit, _spType;
        private readonly long?[]?[] _spValInt;
        private readonly double?[]?[] _spValFloat;
        private readonly bool?[]?[] _spValBool;

        private readonly string?[]?[] _scName, _scAcc, _scValStr, _scUnit, _scType;
        private readonly long?[]?[] _scValInt;
        private readonly double?[]?[] _scValFloat;
        private readonly bool?[]?[] _scValBool;

        private readonly LazyPointLayout _data;

        public WavelengthSpectra(ParquetFileReader meta, ParquetFileReader? data)
        {
            using (meta)
            {
                var cols = BuildColumnIndex(meta.FileMetaData.Schema);
                int groups = meta.FileMetaData.NumRowGroups;
                var offs = new int[groups + 1];
                for (int g = 0; g < groups; g++)
                {
                    using var rg = meta.RowGroup(g);
                    offs[g + 1] = offs[g] + checked((int)rg.MetaData.NumRows);
                }
                int n = offs[groups];
                Count = n;

                _index = new ulong?[n]; _id = new string?[n]; _representationCurie = new string?[n];
                _typeCurie = new string?[n]; _filterString = new string?[n]; _dpRef = new string?[n];
                _auxJson = new string?[n]; _scanIonMobType = new string?[n];
                _time = new double?[n]; _scanStartTime = new double?[n]; _ionInjTime = new double?[n];
                _ionMobValue = new double?[n]; _lowestWl = new double?[n]; _highestWl = new double?[n];
                _lambdaMax = new double?[n]; _basePeakIntensity = new double?[n]; _tic = new double?[n];
                _numDataPoints = new long?[n]; _presetScanConfig = new long?[n];
                _instrConfigRef = new uint?[n]; _numAuxArrays = new int?[n]; _scanSourceIndex = new ulong?[n];
                _spName = new string?[n][]; _spAcc = new string?[n][]; _spValStr = new string?[n][];
                _spUnit = new string?[n][]; _spType = new string?[n][]; _spValInt = new long?[n][];
                _spValFloat = new double?[n][]; _spValBool = new bool?[n][];
                _scName = new string?[n][]; _scAcc = new string?[n][]; _scValStr = new string?[n][];
                _scUnit = new string?[n][]; _scType = new string?[n][]; _scValInt = new long?[n][];
                _scValFloat = new double?[n][]; _scValBool = new bool?[n][];

                for (int g = 0; g < groups; g++)
                {
                    using var rg = meta.RowGroup(g);
                    int len = offs[g + 1] - offs[g], o = offs[g];
                    CopyInto(_index, ReadNullableValue<ulong>(rg, cols, "spectrum.index", len), o);
                    CopyInto(_id, ReadNullableString(rg, cols, "spectrum.id", len), o);
                    CopyInto(_time, ReadNullableValue<double>(rg, cols, "spectrum.time", len), o);
                    CopyInto(_representationCurie, ReadNullableString(rg, cols, "MS:1000525", len), o);
                    CopyInto(_typeCurie, ReadNullableString(rg, cols, "MS:1000559", len), o);
                    CopyInto(_lowestWl, ReadNullableValue<double>(rg, cols, "MS:1000619", len), o);
                    CopyInto(_highestWl, ReadNullableValue<double>(rg, cols, "MS:1000618", len), o);
                    CopyInto(_lambdaMax, ReadNullableValue<double>(rg, cols, "MS:1003812", len), o);
                    CopyInto(_basePeakIntensity, ReadNullableValue<double>(rg, cols, "MS:1000505", len), o);
                    CopyInto(_tic, ReadNullableValue<double>(rg, cols, "MS:1000285", len), o);
                    CopyInto(_numDataPoints, ReadNullableValue<long>(rg, cols, "MS:1003060", len), o);
                    CopyInto(_dpRef, ReadNullableString(rg, cols, "spectrum.data_processing_ref", len), o);
                    CopyInto(_numAuxArrays, ReadNullableValueAsInt(rg, cols, "spectrum.number_of_auxiliary_arrays", len), o);
                    CopyInto(_auxJson, ReadNullableString(rg, cols, "spectrum.auxiliary_arrays", len), o);

                    CopyInto(_scanSourceIndex, ReadNullableValue<ulong>(rg, cols, "scan.source_index", len), o);
                    CopyInto(_scanStartTime, ReadNullableValue<double>(rg, cols, "MS:1000016", len), o);
                    CopyInto(_filterString, ReadNullableString(rg, cols, "MS:1000512", len), o);
                    CopyInto(_ionInjTime, ReadNullableValue<double>(rg, cols, "MS:1000927", len), o);
                    CopyInto(_presetScanConfig, ReadNullableValue<long>(rg, cols, "MS:1000616", len), o);
                    CopyInto(_instrConfigRef, ReadNullableValue<uint>(rg, cols, "scan.instrument_configuration_ref", len), o);
                    CopyInto(_ionMobValue, ReadNullableValue<double>(rg, cols, "scan.ion_mobility_value", len), o);
                    CopyInto(_scanIonMobType, ReadNullableString(rg, cols, "scan.ion_mobility_type", len), o);

                    CopyInto(_spName, ReadNullableStringList(rg, cols, "spectrum.parameters.list.element.name", len), o);
                    CopyInto(_spAcc, ReadNullableStringList(rg, cols, "spectrum.parameters.list.element.accession", len), o);
                    CopyInto(_spValStr, ReadNullableStringList(rg, cols, "spectrum.parameters.list.element.value.string", len), o);
                    CopyInto(_spValInt, ReadNullableValueList<long>(rg, cols, "spectrum.parameters.list.element.value.integer", len), o);
                    CopyInto(_spValFloat, ReadNullableValueList<double>(rg, cols, "spectrum.parameters.list.element.value.float", len), o);
                    CopyInto(_spValBool, ReadNullableValueList<bool>(rg, cols, "spectrum.parameters.list.element.value.boolean", len), o);
                    CopyInto(_spUnit, ReadNullableStringList(rg, cols, "spectrum.parameters.list.element.unit", len), o);
                    CopyInto(_spType, ReadNullableStringList(rg, cols, "spectrum.parameters.list.element.type", len), o);

                    CopyInto(_scName, ReadNullableStringList(rg, cols, "scan.parameters.list.element.name", len), o);
                    CopyInto(_scAcc, ReadNullableStringList(rg, cols, "scan.parameters.list.element.accession", len), o);
                    CopyInto(_scValStr, ReadNullableStringList(rg, cols, "scan.parameters.list.element.value.string", len), o);
                    CopyInto(_scValInt, ReadNullableValueList<long>(rg, cols, "scan.parameters.list.element.value.integer", len), o);
                    CopyInto(_scValFloat, ReadNullableValueList<double>(rg, cols, "scan.parameters.list.element.value.float", len), o);
                    CopyInto(_scValBool, ReadNullableValueList<bool>(rg, cols, "scan.parameters.list.element.value.boolean", len), o);
                    CopyInto(_scUnit, ReadNullableStringList(rg, cols, "scan.parameters.list.element.unit", len), o);
                    CopyInto(_scType, ReadNullableStringList(rg, cols, "scan.parameters.list.element.type", len), o);
                }
            }
            _data = new LazyPointLayout(data);
        }

        public string GetId(int i) => _id[i] ?? string.Empty;

        public BinaryArrays? GetData(int i)
        {
            long key = (long)(_index[i] ?? (ulong)i);
            return _data.Get(key) is { } d ? new BinaryArrays(d.Value, d.Intensity) : null;
        }

        public SpectrumDescription GetDescription(int i)
        {
            var parameters = new List<CvParam>(ZipParams(
                _spName[i], _spAcc[i], _spValStr[i], _spValInt[i], _spValFloat[i], _spValBool[i], _spUnit[i], _spType[i]));

            // The typed wavelength scalars carry their own CV terms (distinct from the m/z ones), so
            // surface them as params for the translation layer rather than reusing the m/z accessors.
            if (!string.IsNullOrEmpty(_typeCurie[i]))
                parameters.Add(new CvParam("spectrum type", _typeCurie[i], "", null, null, null, null));
            AddScalar(parameters, "MS:1000619", "lowest observed wavelength", _lowestWl[i], NanometerCurie);
            AddScalar(parameters, "MS:1000618", "highest observed wavelength", _highestWl[i], NanometerCurie);
            AddScalar(parameters, "MS:1003812", "lambda max", _lambdaMax[i], NanometerCurie);
            AddScalar(parameters, "MS:1000505", "base peak intensity", _basePeakIntensity[i], "MS:1000131");
            AddScalar(parameters, "MS:1000285", "total ion current", _tic[i], null);

            var scanParams = ZipParams(
                _scName[i], _scAcc[i], _scValStr[i], _scValInt[i], _scValFloat[i], _scValBool[i], _scUnit[i], _scType[i]);

            ScanInfo? scan = (_scanStartTime[i] is null && _filterString[i] is null && _instrConfigRef[i] is null
                    && _ionInjTime[i] is null && _scanSourceIndex[i] is null && scanParams.Count == 0)
                ? null
                : new ScanInfo(
                    StartTime: _scanStartTime[i],
                    FilterString: _filterString[i],
                    InstrumentConfigurationRef: _instrConfigRef[i],
                    IonInjectionTime: _ionInjTime[i],
                    Parameters: scanParams,
                    ScanWindows: Array.Empty<ScanWindow>(),
                    SourceIndex: _scanSourceIndex[i],
                    IonMobilityValue: _ionMobValue[i],
                    IonMobilityTypeCurie: _scanIonMobType[i],
                    PresetScanConfiguration: _presetScanConfig[i]);

            var curie = _representationCurie[i];
            return new SpectrumDescription(
                Index: _index[i] ?? (ulong)i,
                Id: _id[i] ?? string.Empty,
                Time: _time[i] ?? 0.0,
                MsLevel: null,
                IsProfile: curie == CurieProfileSpectrum,
                IsCentroid: curie == CurieCentroidSpectrum,
                RepresentationCurie: curie,
                Scan: scan,
                Precursors: Array.Empty<PrecursorInfo>(),
                Parameters: parameters,
                SpectrumTypeCurie: _typeCurie[i],
                NumberOfDataPoints: _numDataPoints[i],
                DataProcessingRef: _dpRef[i],
                NumberOfAuxiliaryArrays: _numAuxArrays[i],
                AuxArrays: AuxiliaryArrays.Parse(_auxJson[i]),
                ValueArrayCurie: WavelengthArrayCurie,
                ValueArrayUnitCurie: NanometerCurie);
        }

        private static void AddScalar(List<CvParam> list, string accession, string name, double? value, string? unit)
        {
            if (value is double d)
                list.Add(new CvParam(name, accession, null, null, d, null, unit));
        }

        public void Dispose() => _data.Dispose();
    }

    /// <summary>
    /// Lazily reads a point-layout parquet — (parent_index, value, intensity) — one row group at a
    /// time, caching the most-recently-touched groups (LRU). The writer splits row groups on
    /// parent (spectrum/chromatogram) boundaries and records each group's parent-index range in the
    /// <c>point_row_group_ranges</c> KV, so <see cref="Get"/> reads only the single group covering
    /// the requested parent. Open is cheap (no point data read) and memory is bounded to a few
    /// row groups instead of the whole file. Reads are guarded by a lock so concurrent callers are
    /// safe; the underlying parquet reader stays open until <see cref="Dispose"/>.
    /// </summary>
    private sealed class LazyPointLayout : IDisposable
    {
        // Must match MzPeakWriter.PointRowGroupRangesKey.
        private const string PointRowGroupRangesKey = "point_row_group_ranges";
        private const int MaxCachedGroups = 3;
        private readonly ParquetFileReader? _reader;
        private readonly (long First, long Last)[] _ranges;
        private readonly int _colIdx, _colVal, _colInt;
        private readonly object _lock = new();
        private readonly Dictionary<int, Dictionary<long, (double[] Value, float[] Intensity)>> _cache = new();
        private readonly LinkedList<int> _lru = new();

        public LazyPointLayout(ParquetFileReader? reader)
        {
            _ranges = Array.Empty<(long, long)>();
            _colIdx = _colVal = _colInt = -1;
            if (reader is null) return;

            _reader = reader;
            var schema = _reader.FileMetaData.Schema;
            for (int i = 0; i < schema.NumColumns; i++)
            {
                var p = schema.Column(i).Path.ToDotString();
                if (p.EndsWith(".intensity", StringComparison.Ordinal)) _colInt = i;
                else if (p.EndsWith("_index", StringComparison.Ordinal)) _colIdx = i;
                else if (p.EndsWith(".mz", StringComparison.Ordinal) || p.EndsWith(".time", StringComparison.Ordinal)
                      || p.EndsWith(".wavelength", StringComparison.Ordinal)) _colVal = i;
            }

            var kv = _reader.FileMetaData.KeyValueMetadata;
            if (kv.TryGetValue(PointRowGroupRangesKey, out var json))
            {
                // pwiz-written files: the writer splits row groups on parent-index boundaries and
                // records the authoritative [first,last] per group, so groups never overlap.
                var raw = System.Text.Json.JsonSerializer.Deserialize<long[][]>(json);
                if (raw is not null) _ranges = raw.Select(r => (r[0], r[1])).ToArray();
            }
            else if (_colIdx >= 0)
            {
                // Files without the range KV (single-row-group pwiz files, and cross-stack files such
                // as mzPeak.NET). Derive each row group's parent-index [min,max] range. Prefer the
                // Parquet column-chunk statistics (free — already in the footer); only fall back to
                // reading the index column when a group has no usable stats. Unlike the KV path these
                // ranges can OVERLAP (a foreign writer may split a spectrum's points across groups), so
                // Get() merges every group whose range covers the key rather than taking the first.
                int groups = _reader.FileMetaData.NumRowGroups;
                var derived = new List<(long, long)>(groups);
                for (int g = 0; g < groups; g++)
                {
                    using var rgr = _reader.RowGroup(g);
                    int rows = checked((int)rgr.MetaData.NumRows);
                    if (rows == 0) { derived.Add((-1, -1)); continue; }

                    using var cc = rgr.MetaData.GetColumnChunkMetaData(_colIdx);
                    if (TryStatsRange(cc.Statistics, out long smin, out long smax))
                    {
                        derived.Add((smin, smax));
                        continue;
                    }

                    var idx = new ulong?[rows];
                    using (var c = rgr.Column(_colIdx).LogicalReader<ulong?>()) c.ReadBatch(idx.AsSpan());
                    long min = long.MaxValue, max = long.MinValue;
                    foreach (var u in idx) if (u is { } v) { if ((long)v < min) min = (long)v; if ((long)v > max) max = (long)v; }
                    derived.Add(min <= max ? (min, max) : (-1, -1));
                }
                _ranges = derived.ToArray();
            }
        }

        /// <summary>Extract a row group's [min,max] for the index column from Parquet statistics, if set.
        /// Index columns are integers; the concrete <c>Statistics&lt;T&gt;</c> varies by physical width and
        /// signedness, so match each. Returns false when stats are absent (caller reads the column).</summary>
        private static bool TryStatsRange(Statistics? stats, out long min, out long max)
        {
            min = 0; max = 0;
            if (stats is null || !stats.HasMinMax) return false;
            switch (stats)
            {
                case Statistics<int> s: min = s.Min; max = s.Max; return true;
                case Statistics<long> s: min = s.Min; max = s.Max; return true;
                case Statistics<uint> s: min = s.Min; max = s.Max; return true;
                case Statistics<ulong> s: min = (long)s.Min; max = (long)s.Max; return true;
                default: return false;
            }
        }

        public (double[] Value, float[] Intensity)? Get(long parentIndex)
        {
            if (_reader is null || _colIdx < 0 || _colVal < 0 || _colInt < 0) return null;

            // Collect every row group whose [min,max] covers the key. With the KV path these ranges
            // are disjoint partitions so this is exactly one group; with derived (possibly overlapping)
            // ranges a spectrum's points may live in several, and we concatenate them in group order.
            double[]? value = null;
            float[]? intensity = null;
            for (int i = 0; i < _ranges.Length; i++)
            {
                if (parentIndex < _ranges[i].First || parentIndex > _ranges[i].Last) continue;
                lock (_lock)
                {
                    var group = EnsureLoaded(i);
                    if (!group.TryGetValue(parentIndex, out var v)) continue;
                    if (value is null) { value = v.Value; intensity = v.Intensity; }
                    else { value = Concat(value, v.Value); intensity = Concat(intensity!, v.Intensity); }
                }
            }
            return value is null ? null : (value, intensity!);
        }

        private static T[] Concat<T>(T[] a, T[] b)
        {
            var r = new T[a.Length + b.Length];
            Array.Copy(a, 0, r, 0, a.Length);
            Array.Copy(b, 0, r, a.Length, b.Length);
            return r;
        }

        private Dictionary<long, (double[] Value, float[] Intensity)> EnsureLoaded(int rgIdx)
        {
            if (_cache.TryGetValue(rgIdx, out var cached))
            {
                _lru.Remove(rgIdx);
                _lru.AddFirst(rgIdx);
                return cached;
            }
            var group = LoadGroup(rgIdx);
            _cache[rgIdx] = group;
            _lru.AddFirst(rgIdx);
            while (_lru.Count > MaxCachedGroups)
            {
                int evict = _lru.Last!.Value;
                _lru.RemoveLast();
                _cache.Remove(evict);
            }
            return group;
        }

        private Dictionary<long, (double[] Value, float[] Intensity)> LoadGroup(int rgIdx)
        {
            using var rg = _reader!.RowGroup(rgIdx);
            int rowCount = checked((int)rg.MetaData.NumRows);
            // Adaptive reads: cross-stack writers vary the physical type of these columns (mzPeak.NET
            // stores the wavelength value array as float32; index widths differ too).
            var idx = ReadIndexColumn(rg, _colIdx, rowCount);
            var val = ReadDoubleColumn(rg, _colVal, rowCount);
            var inten = ReadFloatColumn(rg, _colInt, rowCount);

            var counts = new Dictionary<long, int>();
            for (int r = 0; r < rowCount; r++)
                if (idx[r] is { } u) counts[(long)u] = counts.GetValueOrDefault((long)u) + 1;

            var outv = new Dictionary<long, (double[] Value, float[] Intensity)>(counts.Count);
            var cursor = new Dictionary<long, int>(counts.Count);
            foreach (var kvp in counts)
            {
                outv[kvp.Key] = (new double[kvp.Value], new float[kvp.Value]);
                cursor[kvp.Key] = 0;
            }
            for (int r = 0; r < rowCount; r++)
            {
                if (idx[r] is not { } u) continue;
                long s = (long)u;
                var (vv, ii) = outv[s];
                int c = cursor[s]++;
                vv[c] = val[r] ?? double.NaN;
                ii[c] = inten[r] ?? float.NaN;
            }
            return outv;
        }

        // Column reads tolerant of the physical type a foreign stack chose. Each tries the canonical
        // type first (pwiz's own files) then falls back to the other plausible width/precision, reusing
        // the enclosing reader's adaptive primitive. A present column with no matching candidate throws
        // (rather than yielding NaN points that look like real data).
        private static ulong?[] ReadIndexColumn(RowGroupReader rg, int col, int n)
        {
            if (TryRead<ulong, ulong>(rg, col, n, v => v, out var r)) return r;
            if (TryRead<long, ulong>(rg, col, n, v => (ulong)v, out var r2)) return r2;
            if (TryRead<uint, ulong>(rg, col, n, v => v, out var r3)) return r3;
            if (TryRead<int, ulong>(rg, col, n, v => (ulong)v, out var r4)) return r4;
            throw UnsupportedColumnType(rg, col, "point index", typeof(ulong));
        }

        private static double?[] ReadDoubleColumn(RowGroupReader rg, int col, int n)
        {
            if (TryRead<double, double>(rg, col, n, v => v, out var r)) return r;
            if (TryRead<float, double>(rg, col, n, v => v, out var r2)) return r2;
            throw UnsupportedColumnType(rg, col, "point value", typeof(double));
        }

        private static float?[] ReadFloatColumn(RowGroupReader rg, int col, int n)
        {
            if (TryRead<float, float>(rg, col, n, v => v, out var r)) return r;
            if (TryRead<double, float>(rg, col, n, v => (float)v, out var r2)) return r2;
            throw UnsupportedColumnType(rg, col, "point intensity", typeof(float));
        }

        public void Dispose() => _reader?.Dispose();
    }
}
