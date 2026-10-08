using System.Globalization;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Instruments;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Spectra;

#pragma warning disable CA1707

namespace Pwiz.Vendor.UNIFI;

/// <summary>
/// <see cref="ISpectrumList"/> backed by a <see cref="IUnifiDataSource"/>. C# port of cpp
/// <c>SpectrumList_UNIFI</c> (SpectrumList_UNIFI.cpp).
/// </summary>
/// <remarks>
/// Spectrum-id format depends on the API + ion-mobility setting (cpp createIndex,
/// SpectrumList_UNIFI.cpp:227-265):
/// <list type="bullet">
///   <item>UNIFI, no IMS or IMS combined: <c>scan=N</c> / <c>merged=N</c> (1-based).</item>
///   <item>waters_connect, no IMS or IMS combined: <c>channelIndex=C scanIndex=N</c> /
///   <c>channelIndex=C merged=N</c>.</item>
/// </list>
/// </remarks>
public sealed class SpectrumList_UNIFI : SpectrumListBase
{
    private readonly IUnifiDataSource _source;
    private readonly InstrumentConfiguration? _defaultIc;
    private readonly bool _combineIonMobilitySpectra;
    private readonly List<IndexEntry> _index = new();

    /// <summary>DataProcessing emitted as the document's <c>defaultDataProcessingRef</c>.</summary>
    public DataProcessing? Dp { get; set; }

    /// <inheritdoc/>
    public override DataProcessing? DataProcessing => Dp;

    /// <summary>Wraps <paramref name="source"/>. <paramref name="combineIonMobilitySpectra"/>
    /// matches cpp <c>config.combineIonMobilitySpectra</c> — drives the id-format choice
    /// AND the per-spectrum scan-number rebasing (cpp SpectrumList_UNIFI.cpp:115-116).</summary>
    public SpectrumList_UNIFI(
        IUnifiDataSource source,
        InstrumentConfiguration? defaultInstrumentConfiguration,
        bool combineIonMobilitySpectra)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
        _defaultIc = defaultInstrumentConfiguration;
        _combineIonMobilitySpectra = combineIonMobilitySpectra;
        CreateIndex();
    }

    private sealed class IndexEntry : SpectrumIdentity
    {
        public int Scan;
        public int ChannelIndex;
    }

    /// <inheritdoc/>
    public override int Count => _index.Count;

    /// <inheritdoc/>
    public override SpectrumIdentity SpectrumIdentity(int index) => _index[index];

    /// <inheritdoc/>
    public override Spectrum GetSpectrum(int index, bool getBinaryData = false)
        => GetSpectrum(index, getBinaryData, doCentroid: false);

    /// <summary>Pulls spectrum at <paramref name="index"/>, mirroring cpp
    /// <c>SpectrumList_UNIFI::spectrum</c> (SpectrumList_UNIFI.cpp:97-224).</summary>
    /// <param name="index">Zero-based spectrum index.</param>
    /// <param name="getBinaryData">Include m/z + intensity arrays in the result.</param>
    /// <param name="doCentroid">Ask the source for centroided arrays. cpp's caller routes this
    /// from <c>msLevelsToCentroid.contains(msLevel)</c>; the same gate applies here.</param>
    public Spectrum GetSpectrum(int index, bool getBinaryData, bool doCentroid)
    {
        if (index < 0 || index >= _index.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        var ie = _index[index];

        var spec = new Spectrum { Index = index, Id = ie.Id };
        var scan = new Scan { InstrumentConfiguration = _defaultIc };
        spec.ScanList.Set(CVID.MS_no_combination);
        spec.ScanList.Scans.Add(scan);

        int msLevel = _source.GetMsLevel(index);
        spec.Params.Set(msLevel == 1 ? CVID.MS_MS1_spectrum : CVID.MS_MSn_spectrum);
        spec.Params.Set(CVID.MS_ms_level, msLevel);

        var unifiSpec = new UnifiSpectrum();
        _source.GetSpectrum(index, unifiSpec, getBinaryData, doCentroid);

        if (unifiSpec.RetentionTime > 0)
            scan.Set(CVID.MS_scan_start_time, unifiSpec.RetentionTime, CVID.UO_minute);

        if (unifiSpec.DriftTime > 0)
            scan.Set(CVID.MS_ion_mobility_drift_time, unifiSpec.DriftTime, CVID.UO_millisecond);

        if (unifiSpec.EnergyLevel != UnifiEnergyLevel.Unknown)
            scan.Set(CVID.MS_preset_scan_configuration,
                     unifiSpec.EnergyLevel == UnifiEnergyLevel.Low ? 1 : 2);

        // cpp SpectrumList_UNIFI.cpp:145: low-energy MSe should always land on MS1.
        if (unifiSpec.EnergyLevel == UnifiEnergyLevel.Low && msLevel != 1)
            throw new InvalidOperationException(
                $"BUG: mismatch between MSe energy level and MS level at spectrum {index}");

        if (unifiSpec.ScanPolarity != UnifiPolarity.Unknown)
            spec.Params.Set(TranslatePolarity(unifiSpec.ScanPolarity));

        scan.ScanWindows.Add(new ScanWindow(
            unifiSpec.ScanRange.Low, unifiSpec.ScanRange.High, CVID.MS_m_z));

        // cpp SpectrumList_UNIFI.cpp:153-159: profile vs centroid tag selection. If the source
        // says continuum AND we're not centroiding, emit profile; otherwise emit centroid (and
        // flip the doCentroid flag in the centroid-but-source-says-continuum case so the
        // "profile lie" trick later in this method runs correctly for SpectrumList_PeakPicker).
        if (unifiSpec.DataIsContinuous && !doCentroid)
        {
            spec.Params.Set(CVID.MS_profile_spectrum);
        }
        else
        {
            spec.Params.Set(CVID.MS_centroid_spectrum);
            doCentroid = unifiSpec.DataIsContinuous;
        }

        if (msLevel > 1)
        {
            // cpp SpectrumList_UNIFI.cpp:162-174: the ion-isolation window is the function's
            // acquisition range (UNIFI doesn't expose per-scan precursor m/z for MSn). Center
            // the isolation window on the scan range and report it as the selected ion m/z.
            double low = unifiSpec.ScanRange.Low;
            double high = unifiSpec.ScanRange.High;
            double centerMz = (high - low) / 2;
            var precursor = new Precursor();
            precursor.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, centerMz, CVID.MS_m_z);
            precursor.IsolationWindow.Set(CVID.MS_isolation_window_lower_offset, centerMz - low, CVID.MS_m_z);
            precursor.IsolationWindow.Set(CVID.MS_isolation_window_upper_offset, high - centerMz, CVID.MS_m_z);
            // cpp assumes beam-type CID — UNIFI instruments are TOFs.
            precursor.Activation.Set(CVID.MS_beam_type_collision_induced_dissociation);
            var selected = new SelectedIon();
            selected.Set(CVID.MS_selected_ion_m_z, centerMz, CVID.MS_m_z);
            precursor.SelectedIons.Add(selected);
            spec.Precursors.Add(precursor);
        }

        spec.DefaultArrayLength = unifiSpec.ArrayLength;

        if (getBinaryData)
        {
            var mz = new BinaryDataArray();
            mz.Set(CVID.MS_m_z_array, string.Empty, CVID.MS_m_z);
            mz.Data.AddRange(unifiSpec.MzArray);
            var intensity = new BinaryDataArray();
            intensity.Set(CVID.MS_intensity_array, string.Empty, CVID.MS_number_of_detector_counts);
            intensity.Data.AddRange(unifiSpec.IntensityArray);
            spec.BinaryDataArrays.Add(mz);
            spec.BinaryDataArrays.Add(intensity);

            if (unifiSpec.DriftTimeArray.Length > 0)
            {
                var mobility = new BinaryDataArray();
                mobility.Set(CVID.MS_raw_ion_mobility_array, string.Empty, CVID.UO_millisecond);
                mobility.Data.AddRange(unifiSpec.DriftTimeArray);
                spec.BinaryDataArrays.Add(mobility);
            }

            // cpp SpectrumList_UNIFI.cpp:213-214: when the source said continuum but the caller
            // asked for centroid (doCentroid was flipped in the branch above), tag the spectrum
            // as profile too so SpectrumList_PeakPicker takes the swap-and-keep-as-centroid path
            // instead of running its algorithm on the SDK's centroid arrays.
            if (doCentroid)
                spec.Params.Set(CVID.MS_profile_spectrum);
        }

        return spec;
    }

    private static CVID TranslatePolarity(UnifiPolarity p) => p switch
    {
        UnifiPolarity.Positive => CVID.MS_positive_scan,
        UnifiPolarity.Negative => CVID.MS_negative_scan,
        _ => CVID.CVID_Unknown,
    };

    private void CreateIndex()
    {
        // cpp SpectrumList_UNIFI.cpp:227-265 — the id format depends on the API family AND the
        // combineIonMobilitySpectra flag. Mirror exactly so output ids match cpp byte-for-byte.
        bool watersConnect = _source.RemoteApi == RemoteApiType.WatersConnect;
        bool combinedIms = _source.HasIonMobilityData && _combineIonMobilitySpectra;
        int n = _source.NumberOfSpectra;

        if (watersConnect)
        {
            string scanKey = combinedIms ? "merged=" : " scanIndex=";
            for (int i = 0; i < n; i++)
            {
                var (channelIndex, scanIndexInChannel) = _source.GetChannelAndScanIndex(i);
                var ie = new IndexEntry
                {
                    Index = _index.Count,
                    Scan = scanIndexInChannel,
                    ChannelIndex = channelIndex,
                    Id = string.Format(CultureInfo.InvariantCulture,
                                       "channelIndex={0}{1}{2}",
                                       channelIndex, scanKey, scanIndexInChannel),
                };
                _index.Add(ie);
            }
        }
        else
        {
            string idKey = combinedIms ? "merged=" : "scan=";
            for (int i = 0; i < n; i++)
            {
                var ie = new IndexEntry
                {
                    Index = _index.Count,
                    Scan = _index.Count + 1, // 1-based scan number
                    Id = idKey + (_index.Count + 1).ToString(CultureInfo.InvariantCulture),
                };
                _index.Add(ie);
            }
        }
    }

    /// <summary>True iff the underlying data source is talking to a waters_connect endpoint
    /// (cpp <c>SpectrumList_UNIFI::isWatersConnect</c>).</summary>
    public bool IsWatersConnect => _source.RemoteApi == RemoteApiType.WatersConnect;
}
