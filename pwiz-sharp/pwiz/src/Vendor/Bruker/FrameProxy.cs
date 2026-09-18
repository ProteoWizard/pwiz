using System.Runtime.InteropServices;

namespace Pwiz.Vendor.Bruker;

/// <summary>
/// Thin view over the raw buffer returned by <c>tims_read_scans_v2</c>. Owned by the parent
/// <see cref="TimsBinaryData"/>; do not cache across calls. Port of
/// <c>timsdata::FrameProxy</c> from <c>timsdata_cpp_pwiz.h</c>.
/// </summary>
/// <remarks>
/// Buffer layout (N = number of scans requested):
/// <list type="bullet">
///   <item>N × uint32: peaks per scan</item>
///   <item>N pairs of (index[], intensity[]) uint32 arrays of varying length</item>
/// </list>
/// m/z conversion via <see cref="TimsBinaryData.IndexToMz"/> is deferred — <see cref="GetScanMzs"/> only
/// works after <c>ReadScans(..., performMzConversion: true)</c>.
/// </remarks>
public sealed class FrameProxy
{
    private readonly uint[] _pData;
    private int _numScans;
    private uint[] _scanOffsets = Array.Empty<uint>(); // prefix sums into index/intensity regions
    internal double[] MzIndicesAsDoubles = Array.Empty<double>();
    internal double[] MzValues = Array.Empty<double>();

    internal FrameProxy(uint[] pData) { _pData = pData; }

    /// <summary>Update after a successful <c>tims_read_scans_v2</c>.</summary>
    internal void Update(int numScans)
    {
        _numScans = numScans;
        if (_scanOffsets.Length < numScans + 1)
            _scanOffsets = new uint[numScans + 1];
        _scanOffsets[0] = 0;
        for (int i = 0; i < numScans; i++)
            _scanOffsets[i + 1] = _scanOffsets[i] + _pData[i];
    }

    /// <summary>Number of scans in this proxy (equal to scan_end − scan_begin on the last read).</summary>
    public int NumScans => _numScans;

    /// <summary>Total number of peaks across all scans in this frame proxy.</summary>
    public int TotalNumPeaks => _numScans == 0 ? 0 : (int)_scanOffsets[_numScans];

    /// <summary>Peak count of scan <paramref name="scan"/>.</summary>
    public int NumPeaks(int scan)
    {
        ThrowIfInvalid(scan);
        return (int)_pData[scan];
    }

    /// <summary>m/z values for scan <paramref name="scan"/> (only populated when read with m/z conversion).</summary>
    public ReadOnlySpan<double> GetScanMzs(int scan)
    {
        ThrowIfInvalid(scan);
        return MzValues.AsSpan((int)_scanOffsets[scan], (int)_pData[scan]);
    }

    /// <summary>Raw m/z indices (uint32) for scan <paramref name="scan"/>.</summary>
    public ReadOnlySpan<uint> GetScanIndices(int scan) => MakeRange(scan, offset: 0);

    /// <summary>Raw intensities (uint32) for scan <paramref name="scan"/>.</summary>
    public ReadOnlySpan<uint> GetScanIntensities(int scan) => MakeRange(scan, offset: _pData[scan]);

    private ReadOnlySpan<uint> MakeRange(int scan, uint offset)
    {
        ThrowIfInvalid(scan);
        int start = _numScans + 2 * (int)_scanOffsets[scan] + (int)offset;
        return _pData.AsSpan(start, (int)_pData[scan]);
    }

    private void ThrowIfInvalid(int scan)
    {
        if ((uint)scan >= (uint)_numScans)
            throw new ArgumentOutOfRangeException(nameof(scan), $"Scan number out of range [0,{_numScans}).");
    }

    internal uint[] RawBuffer => _pData;
}
