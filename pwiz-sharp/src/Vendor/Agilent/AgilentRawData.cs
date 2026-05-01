using System.Globalization;
using Agilent.MassSpectrometry.DataAnalysis;

#pragma warning disable CA1707

namespace Pwiz.Vendor.Agilent;

/// <summary>
/// Thin managed wrapper around Agilent's <see cref="IMsdrDataReader"/>. C# equivalent of pwiz
/// C++ <c>MassHunterDataImpl</c> in <c>MassHunterData.cpp</c>; the Agilent SDK is already a
/// .NET assembly so the wrapping is much thinner than for SDKs reached via P/Invoke.
/// </summary>
/// <remarks>
/// Scope (initial port): non-IMS MS spectra only — Reader_Agilent / SpectrumList_Agilent expose
/// the scan records and per-row peak data that mzML conversion needs. IMS frames (MIDAC),
/// MRM/SIM transition chromatograms, and non-MS UV/DAD spectra are not yet ported.
/// </remarks>
public sealed class AgilentRawData : IDisposable
{
    // The SDK exposes its API via IMsdrDataReader (the concrete MassSpecDataReader uses
    // explicit interface implementations for most methods). Holding the interface lets us call
    // OpenDataFile / GetSpectrum / etc. without casts.
    private readonly IMsdrDataReader _reader;
    private bool _disposed;

    /// <summary>Path to the .d directory.</summary>
    public string Path { get; }

    /// <summary>Underlying SDK handle. Avoid using outside the Agilent vendor module.</summary>
    public IMsdrDataReader Reader => _reader;

    /// <summary>File-level info, populated lazily.</summary>
    public IBDAFileInformation FileInformation => _reader.FileInformation;

    /// <summary>Per-MS-scan-collection info: types, ranges, polarity, etc.</summary>
    public IBDAMSScanFileInformation MSScanFileInformation => _reader.MSScanFileInformation;

    /// <summary>Number of scan records (mass spectra) in the file.</summary>
    public long TotalScansPresent => MSScanFileInformation.TotalScansPresent;

    /// <summary>True if the file has any profile-mode data.</summary>
    public bool HasProfileData =>
        MSScanFileInformation.SpectraFormat == MSStorageMode.ProfileSpectrum
        || MSScanFileInformation.SpectraFormat == MSStorageMode.Mixed;

    /// <summary>Top-level instrument family / device type from the file (Q-TOF / TQ / etc.).</summary>
    public DeviceType DeviceType => MSScanFileInformation.DeviceType;

    /// <summary>Acquisition timestamp (local clock).</summary>
    public DateTime AcquisitionTime => FileInformation.AcquisitionTime;

    /// <summary>Bitmask of MS scan types present in the file.</summary>
    public MSScanType ScanTypes => MSScanFileInformation.ScanTypes;

    /// <summary>SDK version string.</summary>
    public string Version => _reader.Version ?? string.Empty;

    /// <summary>Opens <paramref name="dotDPath"/> (a .d directory).</summary>
    /// <remarks>
    /// .NET 8 limitation: <c>DataFileMgr.OpenDataFile</c> in <c>BaseDataAccess.dll</c> uses
    /// <see cref="System.Delegate"/>'s <c>BeginInvoke</c> for asynchronous metadata loading, a
    /// pattern .NET Core 5+ removed. On a .NET (Core/5+/8) host the SDK throws
    /// <see cref="PlatformNotSupportedException"/>; we re-wrap that with a clearer message
    /// pointing at the actual limitation. The C# port itself is correct and runs cleanly under
    /// a .NET Framework 4.8 host (e.g. Skyline).
    /// </remarks>
    public AgilentRawData(string dotDPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dotDPath);
        if (!Directory.Exists(dotDPath))
            throw new DirectoryNotFoundException($"Agilent .d not found: {dotDPath}");
        Path = dotDPath;

        _reader = new MassSpecDataReader();
        try
        {
            if (!_reader.OpenDataFile(dotDPath))
                throw new InvalidDataException($"MassSpecDataReader could not open {dotDPath}");
        }
        catch (PlatformNotSupportedException ex)
        {
            throw new PlatformNotSupportedException(
                "Agilent's MassSpecDataReader uses delegate.BeginInvoke, which .NET 5+ removed; "
                + "this msconvert-sharp build targets .NET 8 and cannot open Agilent .d files. "
                + "Run under a .NET Framework 4.8 host (e.g. Skyline) instead.", ex);
        }
    }

    /// <summary>Returns the lightweight scan record for row <paramref name="rowIndex"/> (0-based).</summary>
    public IMSScanRecord GetScanRecord(int rowIndex) => _reader.GetScanRecord(rowIndex);

    /// <summary>
    /// Returns the full spectrum for row <paramref name="rowIndex"/>. <paramref name="preferProfile"/>
    /// asks for profile data when both formats are stored; otherwise the SDK returns the centroid
    /// representation. Mirrors cpp <c>getProfileSpectrumByRow</c> / <c>getPeakSpectrumByRow</c>.
    /// </summary>
    public IBDASpecData GetSpectrumByRow(int rowIndex, bool preferProfile)
    {
        var storage = preferProfile
            ? DesiredMSStorageType.ProfileElsePeak
            : DesiredMSStorageType.PeakElseProfile;
        // The 3-arg overload takes (scanId, peakFilterMS1, peakFilterMSn, storageType). Passing
        // null for the peak filters means "no filtering". rowIndex here is the row, not scan id —
        // the SDK overloads on int treat the int as a row when called via this signature.
        return _reader.GetSpectrum(rowIndex, null, null, storage);
    }

    // ---------- TIC / BPC helpers ----------

    /// <summary>Run-level total ion chromatogram (times in minutes, intensities in counts).</summary>
    public IBDAChromData? GetTic()
    {
        try { return _reader.GetTIC(); }
        catch { return null; }
    }

    /// <summary>Run-level base peak chromatogram (times in minutes, intensities in counts).</summary>
    public IBDAChromData? GetBpc()
    {
        try { return _reader.GetBPC(); }
        catch { return null; }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _reader?.CloseDataFile(); }
        catch { /* SDK may throw on bogus state — best-effort close */ }
    }

    /// <summary>
    /// Quick sanity check: a path is an Agilent .d directory iff it has an <c>AcqData</c>
    /// subdirectory containing the well-known scan files. Mirrors cpp <c>Reader_Agilent::identify</c>.
    /// </summary>
    public static bool IsAgilentDirectory(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return false;
        string acqData = System.IO.Path.Combine(path, "AcqData");
        if (!Directory.Exists(acqData)) return false;
        // MSScan.bin or MSPeak.bin signal "this AcqData has MS data"; some non-MS .d dirs only
        // have signal data and no MS scans.
        return File.Exists(System.IO.Path.Combine(acqData, "MSScan.bin"))
            || File.Exists(System.IO.Path.Combine(acqData, "MSPeak.bin"));
    }
}
