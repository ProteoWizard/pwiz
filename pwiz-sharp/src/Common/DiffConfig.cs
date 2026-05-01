namespace Pwiz.Data.Common.Diff;

/// <summary>
/// Configuration for <see cref="Diff"/> comparisons.
/// </summary>
/// <remarks>Port of pwiz/data::BaseDiffConfig and pwiz::msdata::DiffConfig.</remarks>
public class DiffConfig
{
    /// <summary>Precision threshold for floating-point differences (default 1e-6).</summary>
    public double Precision { get; set; } = 1e-6;

    /// <summary>If true, stop after finding the first difference (faster, incomplete).</summary>
    public bool PartialDiffOk { get; set; }

    /// <summary>If true, ignore version number mismatches in id strings.</summary>
    public bool IgnoreVersions { get; set; }

    /// <summary>Ignore document-level metadata (FileDescription, Software, DataProcessing, etc.) — diff only spectra/chromatograms.</summary>
    public bool IgnoreMetadata { get; set; }

    /// <summary>Ignore ChromatogramList entirely.</summary>
    public bool IgnoreChromatograms { get; set; }

    /// <summary>When comparing spectra/chromatograms, ignore extra binary arrays that exist in one side but not the other.</summary>
    public bool IgnoreExtraBinaryDataArrays { get; set; }

    /// <summary>Ignore per-spectrum Index/Id identity (useful when e.g. FID nativeID translation loses native ids).</summary>
    public bool IgnoreIdentity { get; set; }

    /// <summary>Ignore the <c>dataProcessingRef</c> field and the contents of the DataProcessings list.</summary>
    public bool IgnoreDataProcessing { get; set; }

    /// <summary>Maximum number of differences to include in the human-readable report (default 50).</summary>
    public int MaxDifferencesToReport { get; set; } = 50;
}
