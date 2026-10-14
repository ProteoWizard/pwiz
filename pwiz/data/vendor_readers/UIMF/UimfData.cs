using System.Data.SQLite;
using UIMFLibrary;

namespace Pwiz.Vendor.UIMF;

/// <summary>UIMF frame type, mirroring cpp <c>FrameType</c> in UIMFReader.hpp.</summary>
public enum UimfFrameType
{
    /// <summary>Single-stage MS scan.</summary>
    MS1 = 1,
    /// <summary>Product-ion scan (MSn).</summary>
    MS2 = 2,
    /// <summary>Internal calibration frame (treated as MS1 in pwiz output).</summary>
    Calibration = 3,
    /// <summary>Prescan frame used for instrument tuning (treated as MS1 in pwiz output).</summary>
    Prescan = 4,
}

/// <summary>One <c>(frame, scan, frameType)</c> row from the UIMF SQLite index, as
/// surfaced by cpp <c>UIMFReaderImpl::index_</c> (UIMFReader.cpp:140-152).</summary>
public readonly record struct UimfIndexEntry(int Frame, int Scan, UimfFrameType FrameType);

/// <summary>Per-drift-scan metadata returned by <see cref="UimfData.GetDriftScansForFrame"/>.
/// Mirrors cpp <c>DriftScanInfo</c>.</summary>
public sealed class UimfDriftScanInfo
{
    /// <summary>Frame number this drift scan belongs to.</summary>
    public int FrameNumber { get; init; }
    /// <summary>Frame-level acquisition type (MS1 / MS2 / Calibration / Prescan).</summary>
    public UimfFrameType FrameType { get; init; }
    /// <summary>Scan number within the frame.</summary>
    public int DriftScanNumber { get; init; }
    /// <summary>Drift time in milliseconds.</summary>
    public double DriftTimeMs { get; init; }
    /// <summary>Frame retention time in minutes.</summary>
    public double RetentionTimeMinutes { get; init; }
    /// <summary>Count of non-zero intensity bins in the scan.</summary>
    public int NonZeroCount { get; init; }
    /// <summary>Sum of intensities for the scan.</summary>
    public double Tic { get; init; }
}

/// <summary>
/// C# port of cpp <c>UIMFReaderImpl</c> (UIMFReader.cpp). Thin wrapper around
/// <see cref="DataReader"/> from PNNL's open-source UIMFLibrary; keeps the SQLite-driven
/// index walk separate from the per-spectrum data fetch so the index is built once at
/// open time and the heavyweight DataReader instance is only used when the caller actually
/// asks for a spectrum or the TIC.
/// </summary>
public sealed class UimfData : IDisposable
{
    private readonly DataReader _reader;
    private readonly List<UimfIndexEntry> _index = new();
    private readonly HashSet<UimfFrameType> _frameTypes = new();
    private readonly int _frameCount;
    private readonly int _binsPerFrame;
    private bool _disposed;

    /// <summary>Opens <paramref name="path"/> and prepopulates the
    /// <c>(frame, scan, frameType)</c> index by querying the SQLite tables directly —
    /// same SELECT cpp issues at UIMFReader.cpp:142.</summary>
    public UimfData(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path)) throw new FileNotFoundException("UIMF file not found", path);

        // Build the index via direct SQLite read first — avoids spinning up DataReader
        // just to walk Frame_Scans, and lets us keep the index even after the DataReader
        // would complain about a half-open file. (`SQLiteConnectionStringBuilder` makes
        // the URI read-only so concurrent acquisitions on the same file are safe.)
        var csb = new SQLiteConnectionStringBuilder
        {
            DataSource = path,
            ReadOnly = true,
            Pooling = false,
        };
        using (var conn = new SQLiteConnection(csb.ConnectionString))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT fs.FrameNum, ScanNum, FrameType " +
                "FROM Frame_Scans fs, Frame_Parameters fp " +
                "WHERE fs.FrameNum = fp.FrameNum";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var entry = new UimfIndexEntry(
                    Frame: reader.GetInt32(0),
                    Scan: reader.GetInt32(1),
                    FrameType: (UimfFrameType)reader.GetInt32(2));
                _index.Add(entry);
                _frameTypes.Add(entry.FrameType);
            }
        }

        // DataReader holds an open file handle for the SDK calls; cpp UIMFReader.cpp:154
        // passes useLegacyDecoding=false, so do the same.
        _reader = new DataReader(path, false);
        var gp = _reader.GetGlobalParams();
        _frameCount = gp.NumFrames;
        _binsPerFrame = gp.Bins;
    }

    /// <summary>Flat list of every <c>(frame, scan, frameType)</c> row in the file, in the
    /// natural SQLite order. Mirrors cpp <c>getIndex()</c>.</summary>
    public IReadOnlyList<UimfIndexEntry> Index => _index;

    /// <summary>Distinct set of frame types present in the file. Used by Reader_UIMF to set
    /// MS1/MSn/calibration cvParams in <c>fileDescription.fileContent</c>.</summary>
    public IReadOnlySet<UimfFrameType> FrameTypes => _frameTypes;

    /// <summary>Number of frames reported by <c>GlobalParams.NumFrames</c>.</summary>
    public int FrameCount => _frameCount;

    /// <summary>Total drift bins per frame (cpp's <c>driftScansPerFrame_</c>).</summary>
    public int DriftScansPerFrame => _binsPerFrame;

    /// <summary>True when the file has an ion-mobility axis (essentially always true for
    /// real UIMF data; constant <c>true</c> in cpp). The expression references
    /// <see cref="_binsPerFrame"/> so the analyzer sees instance state and doesn't push
    /// us toward making this static — leaving the property instance-bound matches cpp's
    /// virtual method and keeps room for a future "no-IMS" file shape.</summary>
    public bool HasIonMobility => _binsPerFrame > 0;

    /// <summary>cpp <c>canConvertIonMobilityAndCCS</c> always returns false in the current
    /// implementation; same here. Same instance-state reference rationale as
    /// <see cref="HasIonMobility"/>.</summary>
    public bool CanConvertIonMobilityAndCcs => _binsPerFrame > 0 && false;

    /// <summary>Maps a UIMF frame type to the mzML <c>ms level</c> CV value. cpp
    /// <c>UIMFReader::getMsLevel</c> (UIMFReader.cpp:119-129).</summary>
    public static int GetMsLevel(UimfFrameType frameType) => frameType switch
    {
        UimfFrameType.MS1 => 1,
        UimfFrameType.MS2 => 2,
        UimfFrameType.Calibration => 1,
        UimfFrameType.Prescan => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(frameType), frameType, "unknown UIMF frame type"),
    };

    /// <summary>Constant scan range across the file (UIMFReader.cpp:177-187): low bin → high bin
    /// mapped through <c>DataReader.ConvertBinToMz</c> using frame-1 calibration values.</summary>
    public (double Low, double High) GetScanRange()
    {
        ThrowIfDisposed();
        var fp = _reader.GetFrameParams(1);
        var gp = _reader.GetGlobalParams();
        double low = DataReader.ConvertBinToMZ(fp.CalibrationSlope, fp.CalibrationIntercept,
                                               gp.BinWidth, gp.TOFCorrectionTime, 1);
        double high = DataReader.ConvertBinToMZ(fp.CalibrationSlope, fp.CalibrationIntercept,
                                                gp.BinWidth, gp.TOFCorrectionTime, gp.Bins);
        return (low, high);
    }

    /// <summary>Frame retention time in minutes. cpp UIMFReader.cpp:299-309.</summary>
    public double GetRetentionTimeMinutes(int frame)
    {
        ThrowIfDisposed();
        var fp = _reader.GetFrameParams(frame);
        if (fp.HasParameter(FrameParamKeyType.StartTimeMinutes))
            return fp.GetValueDouble(FrameParamKeyType.StartTimeMinutes);
        return _reader.GetFrameStartTimeMinutesEstimated(frame);
    }

    /// <summary>Drift time in milliseconds for a given (frame, scan) pair.
    /// cpp UIMFReader.cpp:294-297.</summary>
    public double GetDriftTimeMilliseconds(int frame, int scan)
    {
        ThrowIfDisposed();
        return _reader.GetDriftTime(frame, scan, true);
    }

    /// <summary>cpp UIMFReader.cpp:241-292. Fetches the m/z + intensity arrays for a single
    /// (frame, scan, frameType) triple. When <paramref name="ignoreZeroIntensityPoints"/> is
    /// false (the default), zero-intensity boundary points are inserted at gap edges so the
    /// profile-spectrum encoding survives a peak-picking-free round trip — same interpolation
    /// cpp does to backfill the implicit baseline between non-zero bins.</summary>
    public (double[] Masses, double[] Intensities) GetScan(int frame, int scan, UimfFrameType frameType,
        bool ignoreZeroIntensityPoints = false)
    {
        ThrowIfDisposed();

        double[]? rawMz = null;
        int[]? rawIntensity = null;
        _reader.GetSpectrum(frame, (DataReader.FrameType)frameType, scan, out rawMz, out rawIntensity);

        if (rawMz is null || rawMz.Length == 0)
            return (Array.Empty<double>(), Array.Empty<double>());

        if (ignoreZeroIntensityPoints)
        {
            var intens = new double[rawIntensity!.Length];
            for (int i = 0; i < rawIntensity.Length; i++) intens[i] = rawIntensity[i];
            return (rawMz, intens);
        }

        // cpp UIMFReader.cpp:252-284: pad with zero-intensity points at the bin boundaries so
        // an mzML consumer reading this as profile data sees explicit baseline rather than an
        // unbounded peak. Capacity guess is 3× the input length: each non-zero point may
        // contribute itself plus two flanking zeros.
        var mz = new List<double>(rawMz.Length * 3);
        var intensity = new List<double>(rawMz.Length * 3);

        // cpp UIMFReader.cpp:257-282 calls `GetDeltaMz(frame, managedMzArray[i])` passing a
        // double for the second arg. C++/CLI silently narrows it to int (the SDK overload
        // is `GetDeltaMz(int frameNumber, int startBin)`), so the boundary widths use a
        // bin index roughly equal to the truncated m/z. Replicate the truncation byte-for-
        // byte rather than "fix" it to match cpp's reference mzML.
        // Leading boundary
        mz.Add(rawMz[0] - _reader.GetDeltaMz(frame, (int)rawMz[0]));
        intensity.Add(0);
        mz.Add(rawMz[0]);
        intensity.Add(rawIntensity![0]);

        for (int i = 1; i < rawMz.Length; i++)
        {
            double deltaMz = _reader.GetDeltaMz(frame, (int)rawMz[i]);
            // Big enough gap to the previous m/z that we should emit explicit zeros on both
            // sides of the gap. cpp uses `fabs(diff) - deltaMz > 1e-2` as the threshold.
            if (Math.Abs(rawMz[i] - mz[^1]) - deltaMz > 1e-2)
            {
                mz.Add(rawMz[i - 1] + deltaMz);
                intensity.Add(0);
                mz.Add(rawMz[i] - deltaMz);
                intensity.Add(0);
            }
            mz.Add(rawMz[i]);
            intensity.Add(rawIntensity[i]);
        }

        // Trailing boundary
        double last = rawMz[^1];
        mz.Add(last + _reader.GetDeltaMz(frame, (int)last));
        intensity.Add(0);

        return (mz.ToArray(), intensity.ToArray());
    }

    /// <summary>File-level TIC. cpp UIMFReader.cpp:311-322. Walks
    /// <c>DataReader.GetTICByFrame(0,0,0,0)</c> (all-frames sentinel) and pairs each
    /// frame's TIC value with its retention time.</summary>
    public (double[] TimeMinutes, double[] Intensities) GetTic()
    {
        ThrowIfDisposed();
        var times = new List<double>(_frameCount);
        var intensities = new List<double>(_frameCount);
        var ticByFrame = _reader.GetTICByFrame(0, 0, 0, 0);
        foreach (var kv in ticByFrame)
        {
            times.Add(_reader.GetFrameStartTimeMinutesEstimated(kv.Key));
            intensities.Add(kv.Value);
        }
        return (times.ToArray(), intensities.ToArray());
    }

    /// <summary>Acquisition timestamp pulled from <c>GlobalParameters.DateStarted</c>.
    /// cpp UIMFReader.cpp:206-227 parses two known formats and returns a
    /// <c>boost::local_date_time</c>; we surface a UTC <see cref="DateTime"/> with
    /// unspecified kind (the source string itself is timezone-naïve, matching cpp's
    /// "keep time as is" semantics).</summary>
    public DateTime? GetAcquisitionTimeUtc()
    {
        ThrowIfDisposed();
        var raw = _reader.GetGlobalParams().GetValue(GlobalParamKeyType.DateStarted)?.ToString();
        if (string.IsNullOrWhiteSpace(raw)) return null;
        // Two known formats (cpp UIMFReader.cpp:214):
        //   "M/d/yyyy h:mm:ss tt"  — old US-locale stamps
        //   "yyyy-M-d h:mm:ss tt"  — newer ISO-ish form
        string[] formats = { "M/d/yyyy h:mm:ss tt", "yyyy-M-d h:mm:ss tt" };
        if (DateTime.TryParseExact(raw, formats,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out DateTime parsed))
            return parsed;
        return null;
    }

    /// <summary>Per-frame walk of the SDK's <c>GetFrameScans</c> output. Mirrors cpp
    /// <c>getDriftScansForFrame</c>. Used by the spectrum-3D path; the per-spectrum reader
    /// path doesn't need this.</summary>
    public IReadOnlyList<UimfDriftScanInfo> GetDriftScansForFrame(int frame)
    {
        ThrowIfDisposed();
        var frameType = (UimfFrameType)(int)_reader.GetFrameTypeForFrame(frame);
        double rt = GetRetentionTimeMinutes(frame);
        var sdkScans = _reader.GetFrameScans(frame);
        var result = new List<UimfDriftScanInfo>(sdkScans.Count);
        foreach (var sdk in sdkScans)
        {
            result.Add(new UimfDriftScanInfo
            {
                FrameNumber = sdk.Frame,
                FrameType = frameType,
                DriftScanNumber = sdk.Scan,
                DriftTimeMs = sdk.DriftTime,
                RetentionTimeMinutes = rt,
                NonZeroCount = sdk.NonZeroCount,
                Tic = sdk.TIC,
            });
        }
        return result;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _reader?.Dispose();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
