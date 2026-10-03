namespace Pwiz.Data.MsData.Mz5;

/// <summary>
/// The fixed set of named HDF5 datasets that make up an mz5 file. Port of
/// <c>pwiz::msdata::mz5::Configuration_mz5::MZ5DataSets</c>.
/// </summary>
/// <remarks>
/// Each enum value maps to a string dataset name in the HDF5 file via
/// <see cref="Mz5Configuration.DatasetName"/>. The dataset names are part of
/// the mz5 file format and must match the cpp writer exactly.
/// </remarks>
public enum Mz5Datasets
{
    /// <summary>Top-level controlled-vocabulary list.</summary>
    ControlledVocabulary,
    /// <summary>FileContent param-list.</summary>
    FileContent,
    /// <summary>Contact param-lists.</summary>
    Contact,
    /// <summary>All CV references used in the file (prefix / accession / definition table).</summary>
    CVReference,
    /// <summary>All CVParam values, normalized.</summary>
    CVParam,
    /// <summary>All UserParam values.</summary>
    UserParam,
    /// <summary>RefParam: per-occurrence references into the CVParam / UserParam tables.</summary>
    RefParam,
    /// <summary>Referenceable parameter groups.</summary>
    ParamGroups,
    /// <summary>Source files.</summary>
    SourceFiles,
    /// <summary>Samples.</summary>
    Samples,
    /// <summary>Software entries.</summary>
    Software,
    /// <summary>Scan-setting param-lists.</summary>
    ScanSetting,
    /// <summary>Instrument configurations.</summary>
    InstrumentConfiguration,
    /// <summary>Data-processing entries.</summary>
    DataProcessing,
    /// <summary>Per-run metadata.</summary>
    Run,
    /// <summary>Per-spectrum metadata (id, index, msLevel cvParam refs, etc.).</summary>
    SpectrumMetaData,
    /// <summary>Per-spectrum binary-data-array metadata.</summary>
    SpectrumBinaryMetaData,
    /// <summary>Index into <see cref="SpectrumMZ"/> + <see cref="SpectrumIntensity"/>; k-th entry is the offset to the end of the k-th spectrum.</summary>
    SpectrumIndex,
    /// <summary>Concatenated m/z arrays for all spectra.</summary>
    SpectrumMZ,
    /// <summary>Concatenated intensity arrays for all spectra.</summary>
    SpectrumIntensity,
    /// <summary>Per-chromatogram metadata.</summary>
    ChromatogramMetaData,
    /// <summary>Per-chromatogram binary-data-array metadata.</summary>
    ChromatogramBinaryMetaData,
    /// <summary>Index into <see cref="ChromatogramTime"/> + <see cref="ChromatogramIntensity"/>.</summary>
    ChromatogramIndex,
    /// <summary>Concatenated time arrays for all chromatograms.</summary>
    ChromatogramTime,
    /// <summary>Concatenated chromatogram-intensity arrays.</summary>
    ChromatogramIntensity,
    /// <summary>Version / configuration scalar dataset.</summary>
    FileInformation,
}
