using System.Globalization;
using Pwiz.Analysis;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData.Instruments;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Spectra;

#pragma warning disable CA1707

namespace Pwiz.Vendor.Shimadzu;

/// <summary>
/// <see cref="ISpectrumList"/> backed by a <see cref="ShimadzuRawData"/>. C# port of cpp
/// <c>SpectrumList_Shimadzu</c> in <c>SpectrumList_Shimadzu.cpp</c>.
/// </summary>
/// <remarks>
/// Initial scope (mirroring cpp): MS1 / MS2 spectra with profile or centroid arrays, polarity,
/// scan window, base peak / TIC, and precursor isolation/charge for MS2. <c>srmAsSpectra</c>
/// promotion includes SRM events in the spectrum list.
/// </remarks>
public sealed class SpectrumList_Shimadzu : SpectrumListBase, IVendorCentroidingSpectrumList
{
    private readonly ShimadzuRawData _raw;
    private readonly bool _ownsRaw;
    private readonly InstrumentConfiguration? _defaultIc;
    private readonly bool _srmAsSpectra;
    private readonly List<IndexEntry> _index = new();

    /// <summary>DataProcessing emitted as the document's <c>defaultDataProcessingRef</c>.</summary>
    public DataProcessing? Dp { get; set; }

    /// <inheritdoc/>
    public override DataProcessing? DataProcessing => Dp;

    /// <summary>Wraps <paramref name="raw"/>. When <paramref name="ownsRaw"/> is true, disposes
    /// the underlying reader on Dispose.</summary>
    public SpectrumList_Shimadzu(ShimadzuRawData raw, bool ownsRaw,
        InstrumentConfiguration? defaultInstrumentConfiguration, bool srmAsSpectra)
    {
        ArgumentNullException.ThrowIfNull(raw);
        _raw = raw;
        _ownsRaw = ownsRaw;
        _defaultIc = defaultInstrumentConfiguration;
        _srmAsSpectra = srmAsSpectra;
        CreateIndex();
    }

    private sealed class IndexEntry : SpectrumIdentity
    {
        public int ScanNumber;
    }

    /// <inheritdoc/>
    public override int Count => _index.Count;

    /// <inheritdoc/>
    public override SpectrumIdentity SpectrumIdentity(int index) => _index[index];

    private void CreateIndex()
    {
        // cpp SpectrumList_Shimadzu.cpp:252-269: scan numbers are 1..ScanCount. The index
        // ID format is "scan=<n>" (boost.spirit.karma "scan=" << int_).
        for (int i = 1; i <= _raw.ScanCount; i++)
        {
            _index.Add(new IndexEntry
            {
                Index = _index.Count,
                Id = "scan=" + i.ToString(CultureInfo.InvariantCulture),
                ScanNumber = i,
            });
        }
    }

    /// <inheritdoc/>
    public override Spectrum GetSpectrum(int index, bool getBinaryData = false)
        => BuildSpectrum(index, getBinaryData, vendorCentroid: false);

    /// <inheritdoc/>
    public string VendorCentroidName => "Shimadzu peak picking";

    /// <inheritdoc/>
    public Spectrum GetCentroidSpectrum(int index, bool getBinaryData)
        => BuildSpectrum(index, getBinaryData, vendorCentroid: true);

    private Spectrum BuildSpectrum(int index, bool getBinaryData, bool vendorCentroid)
    {
        if (index < 0 || index >= _index.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        var ie = _index[index];

        var spec = new Spectrum { Index = index, Id = ie.Id };
        spec.ScanList.Set(CVID.MS_no_combination);
        var scan = new Scan { InstrumentConfiguration = _defaultIc };
        spec.ScanList.Scans.Add(scan);

        var info = _raw.GetSpectrumInfo(ie.ScanNumber);
        spec.Params.Set(CVID.MS_ms_level, info.MsLevel);

        // Centroid path (caller is SpectrumList_PeakPicker via IVendorCentroidingSpectrumList):
        // ask the SDK with profileDesired=false so we receive CentroidList. Otherwise the
        // default path mirrors cpp — prefer profile when available, fall back to centroid.
        var sdkSpec = _raw.GetSpectrumRaw(ie.ScanNumber, profileDesired: !vendorCentroid);
        bool hasProfile = !vendorCentroid && sdkSpec.ProfileList.Count > 0;
        bool isCentroid = !hasProfile;

        if (info.MsLevel == 1)
            spec.Params.Set(CVID.MS_MS1_spectrum);
        else if (info.IsSrm)
            spec.Params.Set(CVID.MS_SRM_spectrum);
        else
            spec.Params.Set(CVID.MS_MSn_spectrum);

        // Polarity.
        switch (info.Polarity)
        {
            case ShimadzuPolarity.Positive: spec.Params.Set(CVID.MS_positive_scan); break;
            case ShimadzuPolarity.Negative: spec.Params.Set(CVID.MS_negative_scan); break;
            default: break;
        }

        // RT — cpp converts scanTime / 1000 (Shimadzu stores ms; raw.GetSpectrumInfo already
        // applies the ms→s scale via TimeMultiplier, so just emit as seconds here).
        if (info.ScanTime > 0 || ie.ScanNumber == 1)
            scan.Set(CVID.MS_scan_start_time, info.ScanTime, CVID.UO_second);

        // Scan window — cpp uses the SDK's MassEventInfo StartMz/EndMz. For full-scan MS the
        // pair is (lowMz, highMz) and orders ascending; for SRM events StartMz is Q1 and
        // EndMz is the per-channel Q3, so it's not necessarily ascending. cpp emits the pair
        // verbatim either way (SpectrumList_Shimadzu.cpp:145-146 — `if (spectrum->getMinX() > 0)
        // scanWindows.push_back(ScanWindow(getMinX(), getMaxX()))`). Mirror that: emit when
        // StartMz is non-zero, regardless of relative ordering, and let the consumer interpret.
        var eventInfo = _raw.TryGetEventInfo((short)info.Event);
        if (eventInfo is not null && eventInfo.StartMz > 0)
        {
            double minX = eventInfo.StartMz * ShimadzuRawData.MassMultiplier;
            double maxX = eventInfo.EndMz * ShimadzuRawData.MassMultiplier;
            scan.ScanWindows.Add(new ScanWindow(minX, maxX, CVID.MS_m_z));
        }

        // Precursor metadata for MS2 — cpp SpectrumList_Shimadzu.cpp:174-203 wires isolation
        // window from getIsolationInfo (Q transmission width) when present, otherwise from the
        // selectedIon m/z directly. Charge comes from the file's DDA precursor table.
        if (info.MsLevel > 1 || sdkSpec.PrecursorMzList.Count > 0)
        {
            double selectedMz = 0;
            if (sdkSpec.PrecursorMzList.Count > 0 && sdkSpec.PrecursorMzList[0] > 0)
                selectedMz = sdkSpec.PrecursorMzList[0] * ShimadzuRawData.MassMultiplier;
            else if (info.PrecursorMz > 0)
                selectedMz = info.PrecursorMz;

            if (selectedMz > 0)
            {
                var precursor = new Precursor();
                precursor.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, selectedMz, CVID.MS_m_z);

                var selected = new SelectedIon();
                selected.Set(CVID.MS_selected_ion_m_z, selectedMz, CVID.MS_m_z);

                if (_raw.TryGetPrecursorInfo(ie.ScanNumber, out _, out int charge) && charge > 0)
                    selected.Set(CVID.MS_charge_state, charge);

                // cpp assumes beam-type CID for QqTOF.
                precursor.Activation.Set(CVID.MS_beam_type_collision_induced_dissociation);

                precursor.SelectedIons.Add(selected);
                spec.Precursors.Add(precursor);
            }
        }

        // Base peak / TIC come from the scan record directly. cpp SpectrumList_Shimadzu.cpp:227-230.
        spec.Params.Set(CVID.MS_base_peak_m_z, sdkSpec.BPMass * ShimadzuRawData.MassMultiplier, CVID.MS_m_z);
        spec.Params.Set(CVID.MS_base_peak_intensity, (double)sdkSpec.BPInt, CVID.MS_number_of_detector_counts);
        spec.Params.Set(CVID.MS_total_ion_current, (double)sdkSpec.TotalInt, CVID.MS_number_of_detector_counts);

        // cpp SpectrumList_Shimadzu.cpp:217 + 243-244: when vendorCentroid=true the spectrum
        // gets BOTH MS_centroid_spectrum (set first, line 217) AND MS_profile_spectrum (set
        // last, line 244 — "let SpectrumList_PeakPicker know this was a profile spectrum").
        // The dual tag is what makes SpectrumList_PeakPicker take its centroid short-circuit
        // branch (which strips the profile lie and returns the SDK's pre-centroided arrays
        // unchanged) instead of falling through to the algorithm path that would re-pick on
        // top of the vendor output. Without both terms the picker's algorithm runs on the
        // vendor centroids, dropping ~70% of the points (it expects a profile-shaped input).
        if (vendorCentroid)
        {
            spec.Params.Set(CVID.MS_centroid_spectrum);
            spec.Params.Set(CVID.MS_profile_spectrum);
        }
        else
        {
            spec.Params.Set(isCentroid ? CVID.MS_centroid_spectrum : CVID.MS_profile_spectrum);
        }

        int totalPoints = isCentroid ? sdkSpec.CentroidList.Count : sdkSpec.ProfileList.Count;
        spec.DefaultArrayLength = totalPoints;

        if (getBinaryData && totalPoints > 0)
        {
            var mz = new double[totalPoints];
            var intensity = new double[totalPoints];
            if (isCentroid)
            {
                for (int i = 0; i < totalPoints; i++)
                {
                    var p = sdkSpec.CentroidList[i];
                    mz[i] = p.Mass * ShimadzuRawData.MassMultiplier;
                    intensity[i] = p.Intensity;
                }
            }
            else
            {
                for (int i = 0; i < totalPoints; i++)
                {
                    var p = sdkSpec.ProfileList[i];
                    mz[i] = p.Mass * ShimadzuRawData.MassMultiplier;
                    intensity[i] = p.Intensity;
                }
            }
            spec.SetMZIntensityArrays(mz, intensity, CVID.MS_number_of_detector_counts);
        }

        return spec;
    }

    /// <inheritdoc/>
    protected override void DisposeCore()
    {
        if (_ownsRaw)
        {
            try { _raw.Dispose(); }
            catch { /* best-effort */ }
        }
    }
}
