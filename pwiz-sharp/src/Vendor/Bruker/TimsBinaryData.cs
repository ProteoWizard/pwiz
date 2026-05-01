using System.Runtime.InteropServices;
using System.Text;

namespace Pwiz.Vendor.Bruker;

/// <summary>
/// Managed wrapper around Bruker's native <c>timsdata.dll</c>. Opens a timsTOF <c>.d</c>
/// analysis directory, reads frames via <see cref="ReadScans"/>, and converts between
/// index/m/z and scan-number/1-over-K0 domains.
/// </summary>
/// <remarks>
/// Port of the header-only C++ wrapper in <c>timsdata_cpp_pwiz.h</c> (which itself wraps the
/// C API declared in <c>timsdata.h</c>). The binary .tdf_bin file and .tdf SQLite metadata live
/// inside the supplied analysis directory. Reading metadata (frames table, precursors, etc.)
/// is done separately via <c>Microsoft.Data.Sqlite</c>; this class is just the binary-frame
/// accessor.
/// </remarks>
public sealed class TimsBinaryData : IDisposable
{
    private ulong _handle;
    private readonly FrameProxy _currentFrame;
    private uint[] _buffer;
    private GCHandle _bufferHandle;
    private bool _disposed;

    /// <summary>Underlying native handle. Non-zero once the analysis is open.</summary>
    public ulong Handle => _handle;

    /// <summary>Absolute path of the opened <c>.d</c> directory.</summary>
    public string AnalysisDirectory { get; }

    /// <summary>The last <see cref="FrameProxy"/> populated by <see cref="ReadScans"/>.</summary>
    public FrameProxy CurrentFrame => _currentFrame;

    /// <summary>True if the analysis has a recalibrated state available from DataAnalysis.</summary>
    public bool HasRecalibratedState => NativeMethods.tims_has_recalibrated_state(_handle) != 0;

    /// <summary>Opens <paramref name="analysisDirectory"/> using the specified calibration state.</summary>
    public TimsBinaryData(string analysisDirectory, bool useRecalibratedState = false,
        PressureCompensationStrategy pressureCompensation = PressureCompensationStrategy.None)
    {
        ArgumentNullException.ThrowIfNull(analysisDirectory);
        if (!Directory.Exists(analysisDirectory))
            throw new DirectoryNotFoundException($"Bruker .d directory not found: {analysisDirectory}");
        AnalysisDirectory = Path.GetFullPath(analysisDirectory);

        _handle = pressureCompensation == PressureCompensationStrategy.None
            ? NativeMethods.tims_open(AnalysisDirectory, useRecalibratedState ? 1u : 0u)
            : NativeMethods.tims_open_v2(AnalysisDirectory, useRecalibratedState ? 1u : 0u, pressureCompensation);
        if (_handle == 0) ThrowLastError();

        _buffer = new uint[128];
        _bufferHandle = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
        _currentFrame = new FrameProxy(_buffer);
    }

    /// <summary>
    /// Reads scans <c>[scanBegin, scanEnd)</c> from frame <paramref name="frameId"/> into the
    /// wrapped buffer, and returns a proxy view. When <paramref name="performMzConversion"/> is
    /// true, m/z values are computed from the raw indices and exposed via <see cref="FrameProxy.GetScanMzs"/>.
    /// </summary>
    /// <remarks>Not thread-safe: one reader per handle at a time.</remarks>
    public FrameProxy ReadScans(long frameId, uint scanBegin, uint scanEnd, bool performMzConversion)
    {
        if (scanEnd < scanBegin) throw new ArgumentException("scanEnd must be >= scanBegin.");
        int numScans = (int)(scanEnd - scanBegin);

        while (true)
        {
            uint requiredLen = NativeMethods.tims_read_scans_v2(
                _handle, frameId, scanBegin, scanEnd,
                _bufferHandle.AddrOfPinnedObject(),
                (uint)(4 * _buffer.Length));
            if (requiredLen == 0) ThrowLastError();

            if (4 * _buffer.Length > requiredLen)
            {
                if (requiredLen < 4 * numScans)
                    throw new InvalidOperationException("Returned buffer smaller than expected peak-count header.");
                _currentFrame.Update(numScans);
                if (performMzConversion) ConvertIndicesToMz(frameId, numScans);
                return _currentFrame;
            }

            if (requiredLen > 167_772_160)
                throw new InvalidOperationException("timsdata requested an unreasonably large frame buffer.");

            GrowBuffer((int)(requiredLen / 4 + 1));
        }
    }

    private void GrowBuffer(int newCapacity)
    {
        _bufferHandle.Free();
        _buffer = new uint[newCapacity];
        _bufferHandle = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
        // FrameProxy holds a reference to the OLD buffer; we rebuild it on next Update() using the
        // freshly allocated array. The next ReadScans call that succeeds will re-invoke Update.
        typeof(FrameProxy)
            .GetField("_pData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(_currentFrame, _buffer);
    }

    private void ConvertIndicesToMz(long frameId, int numScans)
    {
        int totalPeaks = _currentFrame.TotalNumPeaks;
        if (_currentFrame.MzIndicesAsDoubles.Length < totalPeaks)
            _currentFrame.MzIndicesAsDoubles = new double[totalPeaks];
        int dst = 0;
        for (int i = 0; i < numScans; i++)
        {
            var indices = _currentFrame.GetScanIndices(i);
            for (int j = 0; j < indices.Length; j++)
                _currentFrame.MzIndicesAsDoubles[dst++] = indices[j];
        }
        if (_currentFrame.MzValues.Length < totalPeaks)
            _currentFrame.MzValues = new double[totalPeaks];
        if (totalPeaks > 0)
            IndexToMz(frameId, _currentFrame.MzIndicesAsDoubles.AsSpan(0, totalPeaks),
                      _currentFrame.MzValues.AsSpan(0, totalPeaks));
    }

    /// <summary>Converts raw indices to m/z for <paramref name="frameId"/>.</summary>
    public void IndexToMz(long frameId, ReadOnlySpan<double> indices, Span<double> mz)
    {
        if (indices.Length == 0) return;
        if (indices.Length != mz.Length)
            throw new ArgumentException("indices and mz spans must have the same length.");
        double[] inBuf = indices.ToArray();
        double[] outBuf = new double[indices.Length];
        uint ok = NativeMethods.tims_index_to_mz(_handle, frameId, inBuf, outBuf, (uint)indices.Length);
        if (ok == 0) ThrowLastError();
        outBuf.CopyTo(mz);
    }

    /// <summary>Converts scan numbers (possibly non-integer) to 1/K0 values for a frame.</summary>
    public double[] ScanNumberToOneOverK0(long frameId, double[] scanNumbers)
    {
        var outBuf = new double[scanNumbers.Length];
        if (scanNumbers.Length == 0) return outBuf;
        uint ok = NativeMethods.tims_scannum_to_oneoverk0(_handle, frameId, scanNumbers, outBuf, (uint)scanNumbers.Length);
        if (ok == 0) ThrowLastError();
        return outBuf;
    }

    /// <summary>Mason-Shamp conversion: 1/K0 → CCS (Å²).</summary>
    public static double OneOverK0ToCcs(double oneOverK0, int charge, double mz) =>
        NativeMethods.tims_oneoverk0_to_ccs_for_mz(oneOverK0, charge, mz);

    /// <summary>Mason-Shamp conversion: CCS (Å²) → 1/K0.</summary>
    public static double CcsToOneOverK0(double ccs, int charge, double mz) =>
        NativeMethods.tims_ccs_to_oneoverk0_for_mz(ccs, charge, mz);

    /// <summary>Sets the internal OpenMP thread count.</summary>
    public static void SetNumThreads(uint n) => NativeMethods.tims_set_num_threads(n);

    /// <summary>Fetches the last-error string from the thread-local slot inside timsdata.dll.</summary>
    private static void ThrowLastError()
    {
        uint len = NativeMethods.tims_get_last_error_string(null, 0);
        if (len == 0) throw new InvalidOperationException("timsdata call failed with no error message.");
        var buf = new byte[len];
        _ = NativeMethods.tims_get_last_error_string(buf, len);
        // Strip the trailing null.
        int n = buf.Length;
        while (n > 0 && buf[n - 1] == 0) n--;
        throw new InvalidOperationException("timsdata.dll: " + Encoding.UTF8.GetString(buf, 0, n));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_handle != 0) { NativeMethods.tims_close(_handle); _handle = 0; }
        if (_bufferHandle.IsAllocated) _bufferHandle.Free();
    }
}
