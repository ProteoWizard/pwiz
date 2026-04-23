using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Sources;

namespace Pwiz.Data.MsData.Spectra;

/// <summary>
/// How detailed a spectrum/chromatogram result must be. Higher levels cost more to produce.
/// Port of pwiz::msdata::DetailLevel.
/// </summary>
public enum DetailLevel
{
    /// <summary>Only instantly-available metadata (index, id).</summary>
    InstantMetadata,
    /// <summary>Fast metadata (no I/O beyond what's already cached).</summary>
    FastMetadata,
    /// <summary>Full metadata including default array length (may require reading binary array headers).</summary>
    FullMetadata,
    /// <summary>Full data including decoded binary arrays.</summary>
    FullData,
}

/// <summary>Sentinel for "no index assigned".</summary>
public static class SpectrumConstants
{
    /// <summary>Sentinel for <see cref="SpectrumIdentity.Index"/> / <see cref="ChromatogramIdentity.Index"/> when no index is known.</summary>
    public const int IdentityIndexNone = -1;
}

/// <summary>Identifying metadata for a spectrum. Port of pwiz::msdata::SpectrumIdentity.</summary>
public class SpectrumIdentity
{
    /// <summary>Zero-based position in the containing SpectrumList; −1 if unassigned.</summary>
    public int Index { get; set; } = SpectrumConstants.IdentityIndexNone;

    /// <summary>Vendor-native id for this spectrum.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>MALDI spot id if applicable.</summary>
    public string SpotId { get; set; } = string.Empty;

    /// <summary>Byte offset of this spectrum in the source file (file-backed impls only).</summary>
    public long SourceFilePosition { get; set; } = -1;
}

/// <summary>A single spectrum with metadata and binary-data arrays. Port of pwiz::msdata::Spectrum.</summary>
public sealed class Spectrum : SpectrumIdentity
{
    /// <summary>ParamContainer interface for this spectrum (CVParams, UserParams, group refs).</summary>
    public ParamContainer Params { get; } = new();

    /// <summary>Default length of the binary data arrays below.</summary>
    public int DefaultArrayLength { get; set; }

    /// <summary>Data processing step that produced this spectrum.</summary>
    public DataProcessing? DataProcessing { get; set; }

    /// <summary>Source file (if different from the MSData document's default).</summary>
    public SourceFile? SourceFile { get; set; }

    /// <summary>Scan list (acquisition metadata).</summary>
    public ScanList ScanList { get; set; } = new();

    /// <summary>Precursor ions (for MS/MS).</summary>
    public List<Precursor> Precursors { get; } = new();

    /// <summary>Product ions (for SRM).</summary>
    public List<Product> Products { get; } = new();

    /// <summary>Binary arrays (m/z, intensity, etc.) — doubles.</summary>
    public List<BinaryDataArray> BinaryDataArrays { get; } = new();

    /// <summary>Binary arrays of int64 values (non-mzML-required extensions).</summary>
    public List<IntegerDataArray> IntegerDataArrays { get; } = new();

    /// <summary>True iff all fields (identity, params, arrays) are empty.</summary>
    public bool IsEmpty =>
        Index == SpectrumConstants.IdentityIndexNone
        && string.IsNullOrEmpty(Id)
        && string.IsNullOrEmpty(SpotId)
        && SourceFilePosition == -1
        && Params.IsEmpty
        && DefaultArrayLength == 0
        && DataProcessing is null
        && SourceFile is null
        && ScanList.IsEmpty
        && Precursors.Count == 0
        && Products.Count == 0
        && BinaryDataArrays.Count == 0
        && IntegerDataArrays.Count == 0;

    /// <summary>True iff the spectrum has any populated binary data.</summary>
    public bool HasBinaryData =>
        BinaryDataArrays.Count > 0 || IntegerDataArrays.Count > 0;

    /// <summary>Returns the m/z binary array (<see cref="CVID.MS_m_z_array"/>), or null.</summary>
    public BinaryDataArray? GetMZArray() => GetArrayByCvid(CVID.MS_m_z_array);

    /// <summary>Returns the intensity binary array (<see cref="CVID.MS_intensity_array"/>), or null.</summary>
    public BinaryDataArray? GetIntensityArray() => GetArrayByCvid(CVID.MS_intensity_array);

    /// <summary>Returns the first binary array tagged with <paramref name="arrayType"/>, or null.</summary>
    /// <param name="arrayType">The CV term identifying the array (e.g. <see cref="CVID.MS_m_z_array"/>).</param>
    /// <param name="allowChildTerm">If true, any child of <paramref name="arrayType"/> matches.</param>
    public BinaryDataArray? GetArrayByCvid(CVID arrayType, bool allowChildTerm = false)
    {
        foreach (var arr in BinaryDataArrays)
        {
            if (allowChildTerm ? arr.HasCVParamChild(arrayType) : arr.HasCVParam(arrayType))
                return arr;
        }
        return null;
    }

    /// <summary>Copies m/z and intensity arrays into a flat list of <see cref="MZIntensityPair"/>.</summary>
    public void GetMZIntensityPairs(List<MZIntensityPair> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        output.Clear();
        var mz = GetMZArray();
        var inten = GetIntensityArray();
        if (mz is null || inten is null) return;
        int n = System.Math.Min(mz.Data.Count, inten.Data.Count);
        output.Capacity = System.Math.Max(output.Capacity, n);
        for (int i = 0; i < n; i++)
            output.Add(new MZIntensityPair(mz.Data[i], inten.Data[i]));
    }

    /// <summary>Replaces the m/z and intensity arrays from a list of pairs.</summary>
    public void SetMZIntensityPairs(IReadOnlyList<MZIntensityPair> input, CVID intensityUnits)
    {
        ArgumentNullException.ThrowIfNull(input);
        var mz = new double[input.Count];
        var inten = new double[input.Count];
        for (int i = 0; i < input.Count; i++) { mz[i] = input[i].Mz; inten[i] = input[i].Intensity; }
        SetMZIntensityArrays(mz, inten, intensityUnits);
    }

    /// <summary>Replaces the m/z and intensity arrays. Sizes must match.</summary>
    public void SetMZIntensityArrays(IReadOnlyList<double> mz, IReadOnlyList<double> intensity, CVID intensityUnits)
    {
        ArgumentNullException.ThrowIfNull(mz);
        ArgumentNullException.ThrowIfNull(intensity);
        if (mz.Count != intensity.Count)
            throw new ArgumentException("m/z and intensity arrays must have the same size.");

        var mzArray = EnsureArray(CVID.MS_m_z_array, CVID.MS_m_z);
        var intArray = EnsureArray(CVID.MS_intensity_array, intensityUnits);

        mzArray.Data.Clear();
        mzArray.Data.AddRange(mz);
        intArray.Data.Clear();
        intArray.Data.AddRange(intensity);
        DefaultArrayLength = mz.Count;
    }

    private BinaryDataArray EnsureArray(CVID arrayType, CVID unitCvid)
    {
        var arr = GetArrayByCvid(arrayType);
        if (arr is not null) return arr;
        arr = new BinaryDataArray();
        arr.Set(arrayType, "", unitCvid);
        BinaryDataArrays.Add(arr);
        return arr;
    }
}

/// <summary>Identifying metadata for a chromatogram. Port of pwiz::msdata::ChromatogramIdentity.</summary>
public class ChromatogramIdentity
{
    /// <summary>Zero-based position in the containing ChromatogramList.</summary>
    public int Index { get; set; } = SpectrumConstants.IdentityIndexNone;

    /// <summary>Vendor-native id for this chromatogram.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Byte offset in the source file (file-backed impls only).</summary>
    public long SourceFilePosition { get; set; } = -1;
}

/// <summary>A single chromatogram. Port of pwiz::msdata::Chromatogram.</summary>
public sealed class Chromatogram : ChromatogramIdentity
{
    /// <summary>ParamContainer for this chromatogram.</summary>
    public ParamContainer Params { get; } = new();

    /// <summary>Default length of the binary data arrays.</summary>
    public int DefaultArrayLength { get; set; }

    /// <summary>Data-processing step that produced this chromatogram.</summary>
    public DataProcessing? DataProcessing { get; set; }

    /// <summary>Precursor ion (Q1 settings).</summary>
    public Precursor Precursor { get; set; } = new();

    /// <summary>Product ion (Q3 settings).</summary>
    public Product Product { get; set; } = new();

    /// <summary>Binary arrays (time, intensity).</summary>
    public List<BinaryDataArray> BinaryDataArrays { get; } = new();

    /// <summary>Integer binary arrays.</summary>
    public List<IntegerDataArray> IntegerDataArrays { get; } = new();

    /// <summary>True iff all fields are empty.</summary>
    public bool IsEmpty =>
        Index == SpectrumConstants.IdentityIndexNone
        && string.IsNullOrEmpty(Id)
        && SourceFilePosition == -1
        && Params.IsEmpty
        && DefaultArrayLength == 0
        && DataProcessing is null
        && Precursor.IsEmpty
        && Product.IsEmpty
        && BinaryDataArrays.Count == 0
        && IntegerDataArrays.Count == 0;

    /// <summary>Returns the time binary array (<see cref="CVID.MS_time_array"/>), or null.</summary>
    public BinaryDataArray? GetTimeArray() => GetArrayByCvid(CVID.MS_time_array);

    /// <summary>Returns the intensity binary array (<see cref="CVID.MS_intensity_array"/>), or null.</summary>
    public BinaryDataArray? GetIntensityArray() => GetArrayByCvid(CVID.MS_intensity_array);

    private BinaryDataArray? GetArrayByCvid(CVID arrayType)
    {
        foreach (var arr in BinaryDataArrays)
            if (arr.HasCVParam(arrayType)) return arr;
        return null;
    }

    /// <summary>Copies time and intensity arrays into a flat list of <see cref="TimeIntensityPair"/>.</summary>
    public void GetTimeIntensityPairs(List<TimeIntensityPair> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        output.Clear();
        var t = GetTimeArray();
        var inten = GetIntensityArray();
        if (t is null || inten is null) return;
        int n = System.Math.Min(t.Data.Count, inten.Data.Count);
        output.Capacity = System.Math.Max(output.Capacity, n);
        for (int i = 0; i < n; i++)
            output.Add(new TimeIntensityPair(t.Data[i], inten.Data[i]));
    }
}
