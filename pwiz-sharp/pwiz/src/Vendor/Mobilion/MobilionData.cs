using System.Diagnostics.CodeAnalysis;
using System.Text;
using static Pwiz.Vendor.Mobilion.MobilionShimNative;

namespace Pwiz.Vendor.Mobilion;

/// <summary>MBI metadata key constants used by Reader_Mobilion. Mirrors
/// <c>MBISDK::MBIAttr::GlobalKey</c> and <c>FrameKey</c> from
/// <c>MBIConstants.h</c>; the SDK passes these as plain string keys to
/// <c>Metadata::ReadString/ReadDouble</c>, so we keep them as constants on
/// our side rather than wrapping the namespace.</summary>
[SuppressMessage("Naming", "CA1707",
    Justification = "Constant names mirror the SDK's MBIAttr enum identifiers (FRM_POLARITY, ACQ_MS_MODEL, etc.) so the cpp port stays grep-able.")]
public static class MobilionAttr
{
    /// <summary>Frame-level: scan polarity ("Positive" / "Negative").
    /// cpp <c>MBIAttr::FrameKey::FRM_POLARITY</c> in MBIConstants.h:117.</summary>
    public const string FRM_POLARITY = "frm-polarity";

    /// <summary>Global: instrument model string. cpp <c>GlobalKey::ACQ_MS_MODEL</c>.</summary>
    public const string ACQ_MS_MODEL = "acq-ms-model";

    /// <summary>Global: acquisition software version. cpp <c>GlobalKey::ACQ_SOFTWARE_VERSION</c>.</summary>
    public const string ACQ_SOFTWARE_VERSION = "acq-software-version";

    /// <summary>Global: acquisition timestamp ("yyyy-MM-dd HH:mm:ss[.fffff]").
    /// cpp <c>GlobalKey::ACQ_TIMESTAMP</c>.</summary>
    public const string ACQ_TIMESTAMP = "acq-timestamp";

    /// <summary>Global: ADC mass-spec range (used as scan-window upper bound).
    /// cpp <c>GlobalKey::ADC_MASS_SPEC_RANGE</c>.</summary>
    public const string ADC_MASS_SPEC_RANGE = "adc-mass-spec-range";
}

/// <summary>
/// Managed wrapper around <c>MBIFile</c> (the SDK's top-level handle). C# port of cpp
/// <c>MBIFileWrapper</c> (Reader_Mobilion_Detail.hpp:42-60); owns the native
/// <see cref="MobilionShimNative"/> handle and exposes the small subset of MBISDK
/// the reader actually needs.
/// </summary>
/// <remarks>
/// HDF5 isn't thread-safe — cpp guards with a process-wide mutex
/// (Reader_Mobilion_Detail.cpp:55). We hold the same lock for the lifetime of an
/// <see cref="MobilionData"/> instance: <see cref="Open"/> acquires, <see cref="Dispose"/>
/// releases. Concurrent <c>MobilionData</c> instances therefore serialize through the
/// lock, matching cpp behavior.
/// </remarks>
public sealed class MobilionData : IDisposable
{
    private static readonly object Hdf5Lock = new();

    private IntPtr _handle;
    private bool _holdsLock;
    private bool _disposed;

    private MobilionData(IntPtr handle)
    {
        _handle = handle;
    }

    /// <summary>Opens <paramref name="path"/> and calls
    /// <c>MBIFile::Init</c>. Throws <see cref="InvalidOperationException"/> with the
    /// shim's last-error message on any SDK failure.</summary>
    public static MobilionData Open(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path)) throw new FileNotFoundException("MBI file not found", path);

        Monitor.Enter(Hdf5Lock);
        IntPtr h = IntPtr.Zero;
        try
        {
            h = mbi_file_open(path);
            if (h == IntPtr.Zero)
                throw new InvalidOperationException(
                    "MBIFile constructor failed: " + MbiLastErrorMessage());

            int rc = mbi_file_init(h);
            if (rc != MBI_OK)
                throw new InvalidOperationException(
                    "MBIFile::Init failed: " + MbiLastErrorMessage());
        }
        catch
        {
            if (h != IntPtr.Zero) mbi_file_free(h);
            Monitor.Exit(Hdf5Lock);
            throw;
        }
        var data = new MobilionData(h) { _holdsLock = true };
        return data;
    }

    /// <summary>Native <c>MBIFile::NumFrames</c>.</summary>
    public int NumFrames
    {
        get
        {
            ThrowIfDisposed();
            int n = mbi_file_num_frames(_handle);
            if (n < 0) throw new InvalidOperationException("MBIFile::NumFrames failed: " + MbiLastErrorMessage());
            return n;
        }
    }

    /// <summary>Loads frame <paramref name="frameIndex1Based"/> (1-based to match the
    /// SDK's <c>MBIFile::GetFrame</c>) and returns a managed wrapper. Caller disposes
    /// the returned <see cref="MobilionFrame"/> to release the native shared_ptr.</summary>
    public MobilionFrame GetFrame(int frameIndex1Based)
    {
        ThrowIfDisposed();
        IntPtr h = mbi_file_get_frame(_handle, frameIndex1Based);
        if (h == IntPtr.Zero)
            throw new InvalidOperationException(
                $"MBIFile::GetFrame({frameIndex1Based}) failed: " + MbiLastErrorMessage());
        return new MobilionFrame(h);
    }

    /// <summary>Reads a string-valued global metadata entry (e.g.
    /// <see cref="MobilionAttr.ACQ_TIMESTAMP"/>). Returns <c>null</c> if the key isn't
    /// present or the SDK returned an empty string.</summary>
    public string? ReadGlobalString(string key)
    {
        ThrowIfDisposed();
        return ReadStringTwoCall(key, (string k, byte[]? b, int n, out int req)
            => mbi_file_global_read_string(_handle, k, b, n, out req));
    }

    /// <summary>Reads a double-valued global metadata entry (e.g.
    /// <see cref="MobilionAttr.ADC_MASS_SPEC_RANGE"/>). Returns <c>null</c> if the SDK
    /// reports the key is missing.</summary>
    public double? ReadGlobalDouble(string key)
    {
        ThrowIfDisposed();
        int rc = mbi_file_global_read_double(_handle, key, out double val);
        return rc == MBI_OK ? val : (double?)null;
    }

    /// <summary>cpp <c>SpectrumList_Mobilion::canConvertIonMobilityAndCCS</c>: probes
    /// <c>EyeOnCcsCalibration::GetAtSurf</c> and treats success as "yes".</summary>
    public bool CanConvertIonMobilityAndCcs()
    {
        ThrowIfDisposed();
        return mbi_file_can_convert_ccs(_handle) == 1;
    }

    /// <summary>cpp <c>SpectrumList_Mobilion::ionMobilityToCCS</c>.
    /// <paramref name="absMzCharge"/> = abs(m/z * charge).</summary>
    public double ArrivalTimeToCcs(double driftTime, double absMzCharge)
    {
        ThrowIfDisposed();
        return mbi_file_arrival_time_to_ccs(_handle, driftTime, absMzCharge);
    }

    /// <summary>cpp <c>SpectrumList_Mobilion::ccsToIonMobility</c>. Returns NaN when
    /// <paramref name="ccs"/> falls outside <c>[GetCCSMinimum, GetCCSMaximum]</c>,
    /// matching cpp's bounds check.</summary>
    public double CcsToArrivalTime(double ccs, double absMzCharge)
    {
        ThrowIfDisposed();
        double lo = mbi_file_ccs_min(_handle);
        double hi = mbi_file_ccs_max(_handle);
        if (ccs < lo || ccs > hi) return double.NaN;
        return mbi_file_ccs_to_arrival_time(_handle, ccs, absMzCharge);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_handle != IntPtr.Zero)
        {
            try { mbi_file_close(_handle); } catch { /* best-effort */ }
            mbi_file_free(_handle);
            _handle = IntPtr.Zero;
        }
        if (_holdsLock)
        {
            _holdsLock = false;
            Monitor.Exit(Hdf5Lock);
        }
    }

    internal static string? ReadStringTwoCall(string key, ReadStringDelegate read)
    {
        int rc = read(key, null, 0, out int required);
        if (rc == MBI_ERR_NO_DATA) return null;
        if (rc != MBI_OK && rc != MBI_ERR_NOT_ENOUGH_SPACE)
            throw new InvalidOperationException($"MBI metadata read of '{key}' failed: " + MbiLastErrorMessage());
        if (required <= 1) return string.Empty;
        var buf = new byte[required];
        rc = read(key, buf, required, out _);
        if (rc != MBI_OK)
            throw new InvalidOperationException($"MBI metadata fill of '{key}' failed: " + MbiLastErrorMessage());
        // shim NUL-terminates; trim before decoding.
        int len = Array.IndexOf(buf, (byte)0);
        if (len < 0) len = buf.Length;
        return Encoding.UTF8.GetString(buf, 0, len);
    }

    internal delegate int ReadStringDelegate(string key, byte[]? buf, int bufSize, out int required);

    private void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(_disposed, this);
}

/// <summary>Managed wrapper around an <c>MBISDK::Frame</c> shared pointer. Disposes
/// the native handle on <see cref="Dispose"/>; methods throw on use after dispose.</summary>
public sealed class MobilionFrame : IDisposable
{
    private IntPtr _handle;
    private bool _disposed;

    internal MobilionFrame(IntPtr handle) { _handle = handle; }

    /// <summary>cpp <c>Frame::GetCE(0)</c>: cpp uses index 0 as a probe to detect
    /// "fragmentation enabled" (a non-zero CE on the first sub-event ⇒ MS2 frame).</summary>
    public double GetCe(long index = 0)
    {
        ThrowIfDisposed();
        return mbi_frame_get_ce_at(_handle, index);
    }

    /// <summary>cpp <c>Frame::GetCollisionEnergy</c>.</summary>
    public double CollisionEnergy
    {
        get { ThrowIfDisposed(); return mbi_frame_collision_energy(_handle); }
    }

    /// <summary>cpp <c>Frame::Time</c> in seconds.</summary>
    public double TimeSeconds
    {
        get { ThrowIfDisposed(); return mbi_frame_time(_handle); }
    }

    /// <summary>cpp <c>Frame::GetFrameTotalIntensity</c>.</summary>
    public long TotalIntensity
    {
        get { ThrowIfDisposed(); return mbi_frame_total_intensity(_handle); }
    }

    /// <summary>cpp <c>Frame::GetArrivalBinTimeOffset</c> for a given drift bin.</summary>
    public double GetArrivalBinTimeOffsetMilliseconds(nuint binIndex)
    {
        ThrowIfDisposed();
        return mbi_frame_arrival_bin_time_offset(_handle, binIndex);
    }

    /// <summary>Batch drift-time lookup. Writes <paramref name="scanIndices"/>.Length values
    /// into <paramref name="outDrift"/>; both must already be allocated to the same length.
    /// One P/Invoke regardless of N — the SDK has no batch API, so the shim loops internally,
    /// but a single managed→native transition still beats N transitions for the
    /// thousand-cell COO data combine-IMS spectra hand back.</summary>
    public void ArrivalBinTimeOffsetsBatch(long[] scanIndices, double[] outDrift)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(scanIndices);
        ArgumentNullException.ThrowIfNull(outDrift);
        if (outDrift.Length < scanIndices.Length)
            throw new ArgumentException("outDrift buffer is shorter than scanIndices.", nameof(outDrift));
        if (scanIndices.Length == 0) return;
        int rc = mbi_frame_arrival_bin_time_offsets_batch(_handle, scanIndices, scanIndices.Length, outDrift);
        if (rc != MBI_OK)
            throw new InvalidOperationException("Frame::GetArrivalBinTimeOffset (batch) failed: " + MbiLastErrorMessage());
    }

    /// <summary>Reads a string-valued frame metadata entry.</summary>
    public string? ReadFrameString(string key)
    {
        ThrowIfDisposed();
        return MobilionData.ReadStringTwoCall(key, (string k, byte[]? b, int n, out int req)
            => mbi_frame_metadata_read_string(_handle, k, b, n, out req));
    }

    /// <summary>cpp <c>Frame::GetNonZeroScanIndices</c>.</summary>
    public long[] GetNonZeroScanIndices()
    {
        ThrowIfDisposed();
        int rc = mbi_frame_get_nonzero_scan_indices(_handle, null, 0, out int needed);
        if (rc != MBI_OK && rc != MBI_ERR_NOT_ENOUGH_SPACE)
            throw new InvalidOperationException("Frame::GetNonZeroScanIndices size query failed: " + MbiLastErrorMessage());
        if (needed == 0) return Array.Empty<long>();
        var buf = new long[needed];
        rc = mbi_frame_get_nonzero_scan_indices(_handle, buf, needed, out _);
        if (rc != MBI_OK)
            throw new InvalidOperationException("Frame::GetNonZeroScanIndices fill failed: " + MbiLastErrorMessage());
        return buf;
    }

    /// <summary>cpp <c>Frame::GetScanDataMzIndexedSparse</c>. Output arrays are parallel.
    /// The shim always passes <c>padWithZeroes=false</c> to the SDK; gap-edge zero
    /// padding is added client-side in <see cref="SpectrumList_Mobilion"/> to match
    /// cpp pwiz's per-scan output exactly.</summary>
    public (double[] Mz, long[] Intensity) GetScanDataMzIndexedSparse(nuint scanIndex)
    {
        ThrowIfDisposed();
        int rc = mbi_frame_get_scan_data_mz_sparse(_handle, scanIndex, null, null, 0, out int needed);
        if (rc != MBI_OK && rc != MBI_ERR_NOT_ENOUGH_SPACE)
            throw new InvalidOperationException("Frame::GetScanDataMzIndexedSparse size query failed: " + MbiLastErrorMessage());
        if (needed == 0) return (Array.Empty<double>(), Array.Empty<long>());
        var mz = new double[needed];
        var intens = new long[needed];
        rc = mbi_frame_get_scan_data_mz_sparse(_handle, scanIndex, mz, intens, needed, out _);
        if (rc != MBI_OK)
            throw new InvalidOperationException("Frame::GetScanDataMzIndexedSparse fill failed: " + MbiLastErrorMessage());
        return (mz, intens);
    }

    /// <summary>cpp <c>Frame::GetScanDataToFIndexedSparse</c>. Output arrays are parallel.
    /// See <see cref="GetScanDataMzIndexedSparse"/> for the pad-with-zeroes contract.</summary>
    public (long[] TofIndex, long[] Intensity) GetScanDataTofIndexedSparse(nuint scanIndex)
    {
        ThrowIfDisposed();
        int rc = mbi_frame_get_scan_data_tof_sparse(_handle, scanIndex, null, null, 0, out int needed);
        if (rc != MBI_OK && rc != MBI_ERR_NOT_ENOUGH_SPACE)
            throw new InvalidOperationException("Frame::GetScanDataToFIndexedSparse size query failed: " + MbiLastErrorMessage());
        if (needed == 0) return (Array.Empty<long>(), Array.Empty<long>());
        var tof = new long[needed];
        var intens = new long[needed];
        rc = mbi_frame_get_scan_data_tof_sparse(_handle, scanIndex, tof, intens, needed, out _);
        if (rc != MBI_OK)
            throw new InvalidOperationException("Frame::GetScanDataToFIndexedSparse fill failed: " + MbiLastErrorMessage());
        return (tof, intens);
    }

    /// <summary>cpp <c>Frame::GetFrameDataAsCOOArray</c>: sparse cube of (intensity,
    /// scanIdx, tofIdx) triples for the whole frame. Used by the combine-IMS path.
    /// See <see cref="GetScanDataMzIndexedSparse"/> for the pad-with-zeroes contract.</summary>
    public (long[] Data, long[] RowScan, long[] ColTof) GetCooArray()
    {
        ThrowIfDisposed();
        int rc = mbi_frame_get_coo_array(_handle, null, null, null, 0, out int needed);
        if (rc != MBI_OK && rc != MBI_ERR_NOT_ENOUGH_SPACE)
            throw new InvalidOperationException("Frame::GetFrameDataAsCOOArray size query failed: " + MbiLastErrorMessage());
        if (needed == 0) return (Array.Empty<long>(), Array.Empty<long>(), Array.Empty<long>());
        var data = new long[needed];
        var row = new long[needed];
        var col = new long[needed];
        rc = mbi_frame_get_coo_array(_handle, data, row, col, needed, out _);
        if (rc != MBI_OK)
            throw new InvalidOperationException("Frame::GetFrameDataAsCOOArray fill failed: " + MbiLastErrorMessage());
        return (data, row, col);
    }

    /// <summary>cpp <c>frame.GetCalibration().IndexToMz(tofIndex)</c>.</summary>
    public double IndexToMz(long tofIndex)
    {
        ThrowIfDisposed();
        return mbi_frame_index_to_mz(_handle, tofIndex);
    }

    /// <summary>Batch <c>IndexToMz</c> via <c>TofCalibration::IndexToMzBuffer</c>. Writes
    /// <paramref name="tofIndices"/>.Length m/z values into <paramref name="outMz"/>; both
    /// arrays must already be allocated to the same length. One P/Invoke per call regardless
    /// of N — used by the combine-IMS / per-scan paths to avoid the previous per-point
    /// P/Invoke storm into <see cref="IndexToMz"/>.</summary>
    public void IndexToMzBatch(long[] tofIndices, double[] outMz)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(tofIndices);
        ArgumentNullException.ThrowIfNull(outMz);
        if (outMz.Length < tofIndices.Length)
            throw new ArgumentException("outMz buffer is shorter than tofIndices.", nameof(outMz));
        if (tofIndices.Length == 0) return;
        int rc = mbi_frame_index_to_mz_batch(_handle, tofIndices, tofIndices.Length, outMz);
        if (rc != MBI_OK)
            throw new InvalidOperationException("Frame::IndexToMzBuffer failed: " + MbiLastErrorMessage());
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_handle != IntPtr.Zero)
        {
            mbi_frame_free(_handle);
            _handle = IntPtr.Zero;
        }
    }

    private void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(_disposed, this);
}
