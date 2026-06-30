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
    /// Reads the profile spectrum for frame <paramref name="frameId"/>. Skips runs of zero
    /// intensities (keeping boundary points) and returns matched m/z and intensity arrays.
    /// </summary>
    public (double[] Mz, double[] Intensity) ReadProfileSpectrum(long frameId)
    {
        while (true)
        {
            int required = NativeMethods.tsf_read_profile_spectrum_v2(
                _handle, frameId, _profileBuffer, _profileBuffer.Length);
            if (required < 0) ThrowLastError();
            if (required == 0) return (Array.Empty<double>(), Array.Empty<double>());

            if (required <= _profileBuffer.Length)
                return CondenseProfile(frameId, required);

            if (required > 16_777_216)
                throw new InvalidOperationException("TSF profile spectrum exceeded max expected size.");
            _profileBuffer = new uint[required];
        }
    }

    private (double[] Mz, double[] Intensity) CondenseProfile(long frameId, int len)
    {
        // Keep first and last bin, and any non-zero triplet — matches tsfdata_cpp_pwiz.h.
        var idx = new List<double>(Math.Min(len, 4096));
        var ints = new List<double>(Math.Min(len, 4096));
        idx.Add(0); ints.Add(_profileBuffer[0]);
        for (int i = 1; i < len - 1; i++)
        {
            if (_profileBuffer[i] > 0 || _profileBuffer[i - 1] > 0 || _profileBuffer[i + 1] > 0)
            {
                idx.Add(i);
                ints.Add(_profileBuffer[i]);
            }
        }
        idx.Add(len - 1); ints.Add(_profileBuffer[len - 1]);

        var idxArr = idx.ToArray();
        var mz = new double[idxArr.Length];
        uint ok = NativeMethods.tsf_index_to_mz(_handle, frameId, idxArr, mz, (uint)idxArr.Length);
        if (ok == 0) ThrowLastError();
        return (mz, ints.ToArray());
    }

    /// <summary>Sets the internal OpenMP thread count for the TSF half of the DLL.</summary>
    public static void SetNumThreads(uint n) => NativeMethods.tsf_set_num_threads(n);

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
        if (_disposed) return;
        _disposed = true;
        if (_handle != 0) { NativeMethods.tsf_close(_handle); _handle = 0; }
    }
}
