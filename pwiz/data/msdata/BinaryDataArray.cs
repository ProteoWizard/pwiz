using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData.Processing;

namespace Pwiz.Data.MsData.Spectra;

/// <summary>A binary array of doubles (e.g. m/z or intensity). Port of pwiz::msdata::BinaryDataArray.</summary>
public sealed class BinaryDataArray : ParamContainer
{
    /// <summary>Data-processing step that produced this array.</summary>
    public DataProcessing? DataProcessing { get; set; }

    /// <summary>The binary payload as doubles.</summary>
    public List<double> Data { get; } = new();

    /// <inheritdoc/>
    public override bool IsEmpty => DataProcessing is null && Data.Count == 0 && base.IsEmpty;
}

/// <summary>A binary array of 64-bit integers. Port of pwiz::msdata::IntegerDataArray.</summary>
public sealed class IntegerDataArray : ParamContainer
{
    /// <summary>Data-processing step that produced this array.</summary>
    public DataProcessing? DataProcessing { get; set; }

    /// <summary>The binary payload as int64s.</summary>
    public List<long> Data { get; } = new();

    /// <inheritdoc/>
    public override bool IsEmpty => DataProcessing is null && Data.Count == 0 && base.IsEmpty;
}

/// <summary>One data point in a mass spectrum (m/z + intensity).</summary>
public readonly record struct MZIntensityPair(double Mz, double Intensity);

/// <summary>One data point in a chromatogram (time + intensity).</summary>
public readonly record struct TimeIntensityPair(double Time, double Intensity);
