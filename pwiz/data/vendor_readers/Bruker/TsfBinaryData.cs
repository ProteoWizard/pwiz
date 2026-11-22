using System.Runtime.InteropServices;
using System.Text;

namespace Pwiz.Vendor.Bruker;

/// <summary>
/// Managed wrapper around the TSF half of Bruker's <c>timsdata.dll</c>. Opens a
/// <c>.d</c> analysis directory whose metadata lives in <c>analysis.tsf</c>, and reads
/// per-frame line or profile spectra via <c>tsf_read_line_spectrum_v2</c> /
/// <c>tsf_read_profile_spectrum_v2</c>.
/// </summary>
/// <remarks>
/// Port of the C++ header-only wrapper in <c>tsfdata_cpp_pwiz.h</c>. TSF is the
/// non-mobility timsTOF format — one spectrum per frame, no TIMS scan dimension.
/// </remarks>
public sealed class TsfBinaryData : IDisposable
{
    private ulong _handle;
    private double[] _indexBuffer;
    private float[] _intensityBuffer;
    private uint[] _profileBuffer;
    private double[] _condensedIndexBuffer = new double[128];
    private long _profileFrameId;
    private int _profileLength;
    private int _condensedCount;
    private double[] _mzBuffer;
    private bool _disposed;

    /// <summary>Underlying native handle. Non-zero once the analysis is open.</summary>
    public ulong Handle => _handle;

    /// <summary>Absolute path of the opened <c>.d</c> directory.</summary>
    public string AnalysisDirectory { get; }

    /// <summary>True if the analysis has a recalibrated state available from DataAnalysis.</summary>
    public bool HasRecalibratedState => NativeMethods.tsf_has_recalibrated_state(_handle) != 0;

    /// <summary>Opens <paramref name="analysisDirectory"/> using the given calibration state.</summary>
    public TsfBinaryData(string analysisDirectory, bool useRecalibratedState = true)
    {
        ArgumentNullException.ThrowIfNull(analysisDirectory);
        if (!Directory.Exists(analysisDirectory))
            throw new DirectoryNotFoundException($"Bruker .d directory not found: {analysisDirectory}");
        AnalysisDirectory = Path.GetFullPath(analysisDirectory);

        NativeInit.EnsureCompatibleCrt();
        _handle = NativeMethods.tsf_open(AnalysisDirectory, useRecalibratedState ? 1u : 0u);
        if (_handle == 0) ThrowLastError();

        _indexBuffer = new double[128];
        _intensityBuffer = new float[128];
        _profileBuffer = new uint[128];
        _mzBuffer = new double[128];
    }

    /// <summary>
    /// Reads the centroided (line) spectrum for frame <paramref name="frameId"/>. Returns the
    /// m/z values (converted from raw indices) and intensities. Arrays have matching length.
    /// </summary>
    /// <remarks>Not thread-safe: one reader per handle at a time.</remarks>
    public (double[] Mz, double[] Intensity) ReadLineSpectrum(long frameId)
    {
        while (true)
        {
            int required = NativeMethods.tsf_read_line_spectrum_v2(
                _handle, frameId, _indexBuffer, _intensityBuffer, _indexBuffer.Length);
            if (required < 0) ThrowLastError();
            if (required == 0) return (Array.Empty<double>(), Array.Empty<double>());

            if (required <= _indexBuffer.Length)
            {
                if (_mzBuffer.Length < required) _mzBuffer = new double[required];
                var mzIn = new double[required];
                Array.Copy(_indexBuffer, mzIn, required);
                var mzOut = new double[required];
                uint ok = NativeMethods.tsf_index_to_mz(_handle, frameId, mzIn, mzOut, (uint)required);
                if (ok == 0) ThrowLastError();

                var intOut = new double[required];
                for (int i = 0; i < required; i++) intOut[i] = _intensityBuffer[i];
                return (mzOut, intOut);
            }

            if (required > 16_777_216)
                throw new InvalidOperationException("TSF line spectrum exceeded max expected size.");
            _indexBuffer = new double[required];
            _intensityBuffer = new float[required];
        }
    }

    /// <summary>
    /// Reads the profile spectrum for frame <paramref name="frameId"/> into this reader and
    /// returns how many points it condenses to, or 0 for an empty frame. Hand storage of that
    /// length to <see cref="CopyProfileSpectrum"/> to receive the points.
    /// </summary>
    public int ReadProfileSpectrum(long frameId)
    {
        while (true)
        {
            int required = NativeMethods.tsf_read_profile_spectrum_v2(
                _handle, frameId, _profileBuffer, _profileBuffer.Length);
            if (required < 0) ThrowLastError();
            if (required <= _profileBuffer.Length)
            {
                _profileFrameId = frameId;
                _profileLength = required;
                _condensedCount = required == 0 ? 0 : CountCondensedProfile(required);
                return _condensedCount;
            }

            if (required > 16_777_216)
                throw new InvalidOperationException("TSF profile spectrum exceeded max expected size.");
            _profileBuffer = new uint[required];
        }
    }

    // Keeps the first and last bin, and every bin in a non-zero triplet; matches tsfdata_cpp_pwiz.h
    private int CountCondensedProfile(int len)
    {
        var profile = _profileBuffer;
        int count = 2;
        for (int i = 1; i < len - 1; i++)
        {
            if (profile[i] > 0 || profile[i - 1] > 0 || profile[i + 1] > 0)
                count++;
        }
        return count;
    }

    /// <summary>
    /// Writes the profile spectrum last read by <see cref="ReadProfileSpectrum"/>, condensed, into
    /// <paramref name="mz"/> and <paramref name="intensity"/>, which must be the length it returned.
    /// </summary>
    public void CopyProfileSpectrum(Span<double> mz, Span<double> intensity)
    {
        int count = mz.Length;
        if (count == 0 || count != intensity.Length || count != _condensedCount)
            throw new ArgumentException("Storage must match the condensed profile spectrum length.");
        if (_condensedIndexBuffer.Length < count)
            _condensedIndexBuffer = new double[count];

        var profile = _profileBuffer;
        var index = _condensedIndexBuffer;
        int len = _profileLength;
        int write = 0;
        index[write] = 0;
        intensity[write++] = profile[0];
        for (int i = 1; i < len - 1; i++)
        {
            if (profile[i] > 0 || profile[i - 1] > 0 || profile[i + 1] > 0)
            {
                index[write] = i;
                intensity[write++] = profile[i];
            }
        }
        index[write] = len - 1;
        intensity[write] = profile[len - 1];

        uint ok = NativeMethods.tsf_index_to_mz(_handle, _profileFrameId, index, ref MemoryMarshal.GetReference(mz), (uint)count);
        if (ok == 0) ThrowLastError();
    }

    /// <summary>Sets the internal OpenMP thread count for the TSF half of the DLL.</summary>
    public static void SetNumThreads(uint n)
    {
        NativeInit.EnsureCompatibleCrt();
        NativeMethods.tsf_set_num_threads(n);
    }

    private static void ThrowLastError()
    {
        uint len = NativeMethods.tsf_get_last_error_string(null, 0);
        if (len == 0) throw new InvalidOperationException("tsfdata call failed with no error message.");
        var buf = new byte[len];
        _ = NativeMethods.tsf_get_last_error_string(buf, len);
        int n = buf.Length;
        while (n > 0 && buf[n - 1] == 0) n--;
        throw new InvalidOperationException("tsfdata: " + Encoding.UTF8.GetString(buf, 0, n));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Finalizer safety net: <c>tsf_open</c> returns a raw native handle; without this a
    /// <see cref="TsfBinaryData"/> dropped without <see cref="Dispose()"/> would leak the
    /// <c>analysis.tsf</c> handle until process exit (see <see cref="TimsBinaryData"/>).
    /// </summary>
    ~TsfBinaryData()
    {
        Dispose(false);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;
        if (_handle != 0) { NativeMethods.tsf_close(_handle); _handle = 0; }
    }
}
