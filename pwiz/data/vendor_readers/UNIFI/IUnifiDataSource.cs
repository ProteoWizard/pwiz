namespace Pwiz.Vendor.UNIFI;

/// <summary>
/// Read-only view of a UNIFI / waters_connect sample result, abstracting the network +
/// protobuf machinery so <see cref="SpectrumList_UNIFI"/> and <see cref="ChromatogramList_UNIFI"/>
/// can be tested without a live Waters server. C# port of the surface area cpp's
/// <c>SpectrumList_UNIFI</c> + <c>ChromatogramList_UNIFI</c> consume off
/// <c>UnifiData</c> (UnifiData.hpp:185-226).
/// </summary>
/// <remarks>
/// The concrete implementation that talks HTTP / parses protobuf lives in the next slice
/// (it'll be backed by <see cref="UnifiHttpClient"/> + <see cref="WatersConnectHttpClient"/>
/// + <see cref="ParallelDownloadQueue"/>). Tests exercise the spectrum/chromatogram-list
/// translation layer with a hand-built fake.
/// </remarks>
public interface IUnifiDataSource
{
    /// <summary>Which API the source is talking to (drives id format + endpoint shape).</summary>
    RemoteApiType RemoteApi { get; }

    /// <summary>True when the source carries combined-IMS or per-bin drift-time data.</summary>
    bool HasIonMobilityData { get; }

    /// <summary>Total number of spectra exposed at the current
    /// <c>combineIonMobilitySpectra</c> setting.</summary>
    int NumberOfSpectra { get; }

    /// <summary>Channel-level chromatogram catalog. Drives
    /// <see cref="ChromatogramList_UNIFI.CreateIndex"/>.</summary>
    IReadOnlyList<UnifiChromatogramInfo> ChromatogramInfo { get; }

    /// <summary>MS level (1 or 2) for the spectrum at <paramref name="index"/>.</summary>
    int GetMsLevel(int index);

    /// <summary>Returns (channelIndex, scanIndexInChannel) for waters_connect — the legacy
    /// UNIFI source throws because UNIFI spectra are not channel-organized
    /// (UnifiData.cpp:270-273).</summary>
    (int ChannelIndex, int ScanIndexInChannel) GetChannelAndScanIndex(int index);

    /// <summary>Pulls the spectrum at <paramref name="index"/> into <paramref name="spectrum"/>.
    /// When <paramref name="getBinaryData"/> is false the metadata fields (RT, polarity,
    /// energy level, scan range, ...) are filled but the m/z + intensity arrays stay empty.
    /// </summary>
    void GetSpectrum(int index, UnifiSpectrum spectrum, bool getBinaryData, bool doCentroid);

    /// <summary>Pulls the chromatogram at <paramref name="index"/> (an index into
    /// <see cref="ChromatogramInfo"/>) into <paramref name="chromatogram"/>.</summary>
    void GetChromatogram(int index, UnifiChromatogram chromatogram, bool getBinaryData);
}
