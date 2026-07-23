using System.Globalization;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Instruments;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Spectra;

#pragma warning disable CA1707  // underscored class name mirrors cpp `SpectrumList_Mobilion`

namespace Pwiz.Vendor.Mobilion;

/// <summary>
/// <see cref="ISpectrumList"/> backed by <see cref="MobilionData"/>. C# port of cpp
/// <c>SpectrumList_Mobilion</c> (SpectrumList_Mobilion.cpp).
/// </summary>
/// <remarks>
/// Two index modes match cpp:
/// <list type="bullet">
///   <item>Default: one logical spectrum per <c>(frame, drift-scan)</c> in
///   <see cref="MobilionFrame.GetNonZeroScanIndices"/>. Spectrum id is
///   <c>frame=N scan=M</c> (cpp SpectrumList_Mobilion.cpp:406-407, with
///   <c>scan</c> 1-based for output).</item>
///   <item><c>combineIonMobilitySpectra</c>: one logical spectrum per frame, id is
///   <c>merged=N frame=M</c> (cpp SpectrumList_Mobilion.cpp:390-391). The
///   per-bin drift dimension lands in a parallel <c>raw ion mobility</c>
///   binary array.</item>
/// </list>
/// </remarks>
public sealed class SpectrumList_Mobilion : SpectrumListBase, IIonMobilitySpectrumList, IIonMobilityCcsConversion
{
    /// <inheritdoc cref="IIonMobilitySpectrumList.IonMobilityUnits"/>
    /// <remarks>Mobilion is an IMS-only instrument — IM is always drift time in ms.</remarks>
    public IonMobilityUnits IonMobilityUnits => IonMobilityUnits.DriftTimeMsec;

    /// <inheritdoc cref="IIonMobilitySpectrumList.HasCombinedIonMobility"/>
    /// <remarks>True iff <c>combineIonMobilitySpectra</c> was enabled (per-frame 3-array mode).</remarks>
    public bool HasCombinedIonMobility => _combineIonMobilitySpectra;

    /// <inheritdoc/>
    public bool IsWatersSonar => false;

    /// <inheritdoc/>
    /// <remarks>Probes <c>EyeOnCcsCalibration::GetAtSurf</c> via
    /// <see cref="MobilionData.CanConvertIonMobilityAndCcs"/>.</remarks>
    public bool CanConvertIonMobilityAndCcs => _data.CanConvertIonMobilityAndCcs();

    /// <inheritdoc/>
    /// <remarks>cpp <c>ionMobilityToCCS(im, mz, charge)</c> -> sharp
    /// <see cref="MobilionData.ArrivalTimeToCcs"/><c>(driftTime, absMzCharge)</c> where
    /// <c>absMzCharge = |mz * charge|</c> (cpp SpectrumList_Mobilion.cpp uses the same
    /// transform before calling the SDK).</remarks>
    public double IonMobilityToCcs(double ionMobility, double mz, int charge) =>
        _data.ArrivalTimeToCcs(ionMobility, System.Math.Abs(mz * charge));

    /// <inheritdoc/>
    public double CcsToIonMobility(double ccs, double mz, int charge) =>
        _data.CcsToArrivalTime(ccs, System.Math.Abs(mz * charge));

    private readonly MobilionData _data;
    private readonly InstrumentConfiguration? _defaultIc;
    private readonly bool _combineIonMobilitySpectra;
    private readonly bool _ignoreZeroIntensityPoints;
    private readonly double _scanWindowUpperMz;
    private readonly List<IndexEntry> _index = new();

    /// <summary>DataProcessing emitted as the document's <c>defaultDataProcessingRef</c>.</summary>
    public DataProcessing? Dp { get; set; }

    /// <inheritdoc/>
    public override DataProcessing? DataProcessing => Dp;

    /// <summary>Wraps <paramref name="data"/>. <paramref name="combineIonMobilitySpectra"/>
    /// matches cpp <c>config.combineIonMobilitySpectra</c>; <paramref name="ignoreZeroIntensityPoints"/>
    /// matches the cpp config flag of the same name and toggles the gap-padding-zero
    /// boundary insertion in the per-spectrum data fetch.</summary>
    public SpectrumList_Mobilion(
        MobilionData data,
        InstrumentConfiguration? defaultInstrumentConfiguration,
        bool combineIonMobilitySpectra,
        bool ignoreZeroIntensityPoints,
        double scanWindowUpperMz)
    {
        ArgumentNullException.ThrowIfNull(data);
        _data = data;
        _defaultIc = defaultInstrumentConfiguration;
        _combineIonMobilitySpectra = combineIonMobilitySpectra;
        _ignoreZeroIntensityPoints = ignoreZeroIntensityPoints;
        _scanWindowUpperMz = scanWindowUpperMz;
        BuildIndex();
    }

    private sealed class IndexEntry : SpectrumIdentity
    {
        public int Frame;
        public long ScanWithinFrame;  // -1 for combined-IMS rows
    }

    /// <inheritdoc/>
    /// <remarks>Disposes the underlying <see cref="MobilionData"/> so the .mbi file's
    /// HDF5 handle is released — the harness's post-dispose rename probe checks for
    /// this. <see cref="MobilionData.Dispose"/> is idempotent, so the parallel call
    /// from <see cref="ChromatogramList_Mobilion"/> is safe.</remarks>
    protected override void DisposeCore() => _data.Dispose();

    /// <inheritdoc/>
    public override int Count => _index.Count;

    /// <inheritdoc/>
    public override SpectrumIdentity SpectrumIdentity(int index) => _index[index];

    /// <inheritdoc/>
    public override Spectrum GetSpectrum(int index, bool getBinaryData = false)
        => GetSpectrum(index, getBinaryData, doCentroid: false);

    /// <summary>Pulls spectrum at <paramref name="index"/>, mirroring cpp
    /// <c>SpectrumList_Mobilion::spectrum</c> (SpectrumList_Mobilion.cpp:77-256).</summary>
    public Spectrum GetSpectrum(int index, bool getBinaryData, bool doCentroid)
    {
        if (index < 0 || index >= _index.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        var ie = _index[index];

        var spec = new Spectrum { Index = index, Id = ie.Id };
        var scan = new Scan { InstrumentConfiguration = _defaultIc };
        spec.ScanList.Set(CVID.MS_no_combination);
        spec.ScanList.Scans.Add(scan);

        using var frame = _data.GetFrame(ie.Frame);

        // cpp SpectrumList_Mobilion.cpp:103: MS2 if Frame::GetCE(0) > 0.
        int msLevel = frame.GetCe(0) > 0 ? 2 : 1;
        spec.Params.Set(msLevel == 2 ? CVID.MS_MSn_spectrum : CVID.MS_MS1_spectrum);
        spec.Params.Set(CVID.MS_ms_level, msLevel);

        // cpp SpectrumList_Mobilion.cpp:109-110: scan_start_time in seconds (UO_second).
        scan.Set(CVID.MS_scan_start_time, frame.TimeSeconds, CVID.UO_second);

        // Per-frame metadata used both for polarity and as profile-spectrum tag.
        string? polarity = frame.ReadFrameString(MobilionAttr.FRM_POLARITY);
        var polarityCv = TranslatePolarity(polarity);
        if (polarityCv != CVID.CVID_Unknown) spec.Params.Set(polarityCv);
        spec.Params.Set(CVID.MS_profile_spectrum);

        spec.Params.Set(CVID.MS_total_ion_current, (double)frame.TotalIntensity);

        // cpp SpectrumList_Mobilion.cpp:128-129: scan window from
        // [0, GlobalKey.ADC_MASS_SPEC_RANGE]. We hoist that double once at reader-open
        // time and stash on the list to avoid re-querying per spectrum.
        scan.ScanWindows.Add(new ScanWindow(0, _scanWindowUpperMz, CVID.MS_m_z));

        if (!_combineIonMobilitySpectra)
        {
            scan.Set(CVID.MS_ion_mobility_drift_time,
                     frame.GetArrivalBinTimeOffsetMilliseconds((nuint)ie.ScanWithinFrame),
                     CVID.UO_millisecond);
        }

        if (msLevel > 1)
        {
            // cpp SpectrumList_Mobilion.cpp:140-156: synthetic precursor centered on the
            // scan window (no per-MS2-spectrum precursor metadata in MBI). Activation =
            // beam-type CID (Agilent QTOF). cpp picks midMZ = (maxMZ + minMZ) / 2 with
            // minMZ pinned to 0, so the offsets are also (maxMZ - midMZ) on both sides.
            double mid = _scanWindowUpperMz / 2;
            var precursor = new Precursor();
            precursor.IsolationWindow.Set(CVID.MS_isolation_window_upper_offset, _scanWindowUpperMz - mid, CVID.MS_m_z);
            precursor.IsolationWindow.Set(CVID.MS_isolation_window_lower_offset, _scanWindowUpperMz - mid, CVID.MS_m_z);
            precursor.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, mid, CVID.MS_m_z);
            precursor.Activation.Set(CVID.MS_beam_type_collision_induced_dissociation);
            double ce = frame.CollisionEnergy;
            if (ce > 0) precursor.Activation.Set(CVID.MS_collision_energy, ce, CVID.UO_electronvolt);
            precursor.SelectedIons.Add(new SelectedIon(mid));
            spec.Precursors.Add(precursor);
        }

        if (!getBinaryData)
        {
            // cpp SpectrumList_Mobilion.cpp:159-160 returns early without binary data;
            // defaultArrayLength still needs to reflect the data shape so writers can
            // allocate. Compute it without materializing the arrays — the SDK's
            // sparse fetches all hand back the count as their first action, so this
            // is cheap. Per-bin path: count = nonzero-cell count for the scan; combined
            // path: count = COO data size (with optional 3× boundary inflation).
            spec.DefaultArrayLength = ComputeDefaultArrayLength(frame, ie);
            return spec;
        }

        if (_combineIonMobilitySpectra)
        {
            var (mz, intensity, drift) = GetCombinedSpectrumData(frame);
            spec.DefaultArrayLength = mz.Length;
            AddBinary(spec, mz, intensity);
            var mobilityArr = new BinaryDataArray();
            mobilityArr.Set(CVID.MS_raw_ion_mobility_array, string.Empty, CVID.UO_millisecond);
            mobilityArr.Data.AddRange(drift);
            spec.BinaryDataArrays.Add(mobilityArr);
        }
        else
        {
            var (mz, intensity) = GetPerScanSpectrumData(frame, (nuint)ie.ScanWithinFrame);
            spec.DefaultArrayLength = mz.Length;
            AddBinary(spec, mz, intensity);
        }

        return spec;
    }

    private (double[] Mz, double[] Intensity) GetPerScanSpectrumData(MobilionFrame frame, nuint scanIndex)
    {
        // cpp SpectrumList_Mobilion.cpp:182-253 has two paths:
        //   ignoreZeros=true  → use mz-indexed-sparse (already at full m/z resolution).
        //   ignoreZeros=false → use TOF-indexed-sparse + IndexToMz, then pad with
        //                       zero-intensity boundaries at TOF gap edges.
        if (_ignoreZeroIntensityPoints)
        {
            var (mz, intens) = frame.GetScanDataMzIndexedSparse(scanIndex);
            var intensD = new double[intens.Length];
            for (int i = 0; i < intens.Length; i++) intensD[i] = intens[i];
            return (mz, intensD);
        }

        var (tof, intensities) = frame.GetScanDataTofIndexedSparse(scanIndex);
        if (intensities.Length == 0)
            return (Array.Empty<double>(), Array.Empty<double>());

        // First pass: count gaps so we can size the output exactly. (gap test mirrors
        // cpp's size_t-subtraction wrap; see GetCombinedSpectrumData for the rationale.)
        int gaps = 0;
        for (int i = 1; i < intensities.Length; i++)
            if ((ulong)tof[i] - (ulong)tof[i - 1] > 1UL) gaps++;
        int actualPoints = 2 + intensities.Length + 2 * gaps;

        // Build TOF-index + intensity arrays for the expanded output, then convert all
        // TOF indices to m/z in one batch P/Invoke. That replaces O(actualPoints)
        // per-point calls into mbi_frame_index_to_mz; on combine-IMS spectra the
        // batched form is the difference between thousands of native transitions and
        // one. Per-scan paths are smaller but go through the same code path for
        // uniformity.
        var tofs = new long[actualPoints];
        var intOut = new double[actualPoints];
        int p = 0;
        // Leading zero + first sample. cpp SpectrumList_Mobilion.cpp:216-221.
        tofs[p] = tof[0] - 1; intOut[p] = 0; p++;
        tofs[p] = tof[0];     intOut[p] = intensities[0]; p++;
        for (int i = 1; i < intensities.Length; i++)
        {
            if ((ulong)tof[i] - (ulong)tof[i - 1] > 1UL)
            {
                tofs[p] = tof[i - 1] + 1; intOut[p] = 0; p++;
                tofs[p] = tof[i] - 1;     intOut[p] = 0; p++;
            }
            tofs[p] = tof[i]; intOut[p] = intensities[i]; p++;
        }
        // Trailing zero. cpp:243-245.
        tofs[p] = tof[^1] + 1; intOut[p] = 0; p++;

        var mzOut = new double[actualPoints];
        frame.IndexToMzBatch(tofs, mzOut);
        return (mzOut, intOut);
    }

    private (double[] Mz, double[] Intensity, double[] Drift) GetCombinedSpectrumData(MobilionFrame frame)
    {
        // cpp SpectrumList_Mobilion.cpp:297-373 — same boundary-padding logic as
        // per-scan but operating on the whole-frame COO array, with a parallel drift
        // axis built from row indices (scan number → drift time).
        var (data, rowScan, colTof) = frame.GetCooArray();
        if (data.Length == 0)
            return (Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>());

        if (_ignoreZeroIntensityPoints)
        {
            var mz = new double[data.Length];
            var drift = new double[data.Length];
            var intens = new double[data.Length];
            // Two batch P/Invokes replace 2N per-point calls (was the dominant cost
            // of combine-IMS spectra). long→double intensity copy stays in managed.
            frame.IndexToMzBatch(colTof, mz);
            frame.ArrivalBinTimeOffsetsBatch(rowScan, drift);
            for (int i = 0; i < data.Length; i++) intens[i] = data[i];
            return (mz, intens, drift);
        }

        // First pass: count gaps and compute exact output length. cpp uses
        // `tofSampleIndices[i] - tofSampleIndices[i - 1] > 1` on a vector<size_t>;
        // size_t subtraction wraps on underflow, so when COO iteration crosses a
        // scan boundary (TOF resets as the row index increments), cpp counts that as
        // a gap and emits a zero-padding pair. Signed long subtraction would be
        // negative there and miss the boundary. Mirror cpp's wrap semantics with
        // unsigned arithmetic so the gap count matches the reference mzML's expansion.
        int gaps = 0;
        for (int i = 1; i < data.Length; i++)
            if ((ulong)colTof[i] - (ulong)colTof[i - 1] > 1UL) gaps++;
        int actualPoints = 2 + data.Length + 2 * gaps;

        // Pre-size the per-point input arrays for the two batch P/Invokes. We fill
        // intensity inline (managed-only); m/z and drift come back from the SDK.
        var tofs = new long[actualPoints];
        var scans = new long[actualPoints];
        var intOut = new double[actualPoints];
        int p = 0;
        // Leading zero + first sample. cpp:332-339.
        tofs[p] = colTof[0] - 1; scans[p] = rowScan[0]; intOut[p] = 0;        p++;
        tofs[p] = colTof[0];     scans[p] = rowScan[0]; intOut[p] = data[0];  p++;
        for (int i = 1; i < data.Length; i++)
        {
            if ((ulong)colTof[i] - (ulong)colTof[i - 1] > 1UL)
            {
                tofs[p] = colTof[i - 1] + 1; scans[p] = rowScan[i - 1]; intOut[p] = 0; p++;
                tofs[p] = colTof[i] - 1;     scans[p] = rowScan[i];     intOut[p] = 0; p++;
            }
            tofs[p] = colTof[i]; scans[p] = rowScan[i]; intOut[p] = data[i]; p++;
        }
        // Trailing zero. cpp:365-367.
        tofs[p] = colTof[^1] + 1; scans[p] = rowScan[^1]; intOut[p] = 0; p++;

        var mzOut = new double[actualPoints];
        var driftOut = new double[actualPoints];
        frame.IndexToMzBatch(tofs, mzOut);
        frame.ArrivalBinTimeOffsetsBatch(scans, driftOut);
        return (mzOut, intOut, driftOut);
    }

    private int ComputeDefaultArrayLength(MobilionFrame frame, IndexEntry ie)
    {
        // The cheapest way to learn the size up-front is to fetch the arrays the same
        // way getBinaryData=true would and use .Length. The SDK's cost is in the
        // HDF5 read (data is already loaded by the time GetFrame returned), so this
        // is cheap. We could collapse to a faster nnz-count for the ignoreZeros case
        // later if a profile shows it's hot.
        if (_combineIonMobilitySpectra)
        {
            var (mz, _, _) = GetCombinedSpectrumData(frame);
            return mz.Length;
        }
        else
        {
            var (mz, _) = GetPerScanSpectrumData(frame, (nuint)ie.ScanWithinFrame);
            return mz.Length;
        }
    }

    private static void AddBinary(Spectrum spec, double[] mz, double[] intensity)
    {
        var mzArr = new BinaryDataArray();
        mzArr.Set(CVID.MS_m_z_array, string.Empty, CVID.MS_m_z);
        mzArr.Data.AddRange(mz);
        var intensityArr = new BinaryDataArray();
        intensityArr.Set(CVID.MS_intensity_array, string.Empty, CVID.MS_number_of_detector_counts);
        intensityArr.Data.AddRange(intensity);
        spec.BinaryDataArrays.Add(mzArr);
        spec.BinaryDataArrays.Add(intensityArr);
    }

    private static CVID TranslatePolarity(string? polarity) => polarity switch
    {
        not null when polarity.Equals("Positive", StringComparison.OrdinalIgnoreCase) => CVID.MS_positive_scan,
        not null when polarity.Equals("Negative", StringComparison.OrdinalIgnoreCase) => CVID.MS_negative_scan,
        _ => CVID.CVID_Unknown,
    };

    private void BuildIndex()
    {
        // cpp SpectrumList_Mobilion.cpp:376-413.
        int frames = _data.NumFrames;
        for (int frame = 1; frame <= frames; frame++)
        {
            if (_combineIonMobilitySpectra)
            {
                _index.Add(new IndexEntry
                {
                    Index = _index.Count,
                    Frame = frame,
                    ScanWithinFrame = -1,
                    Id = string.Format(CultureInfo.InvariantCulture,
                                       "merged={0} frame={1}",
                                       _index.Count + 1, frame),
                });
            }
            else
            {
                using var f = _data.GetFrame(frame);
                foreach (long scan in f.GetNonZeroScanIndices())
                {
                    _index.Add(new IndexEntry
                    {
                        Index = _index.Count,
                        Frame = frame,
                        ScanWithinFrame = scan,
                        Id = string.Format(CultureInfo.InvariantCulture,
                                           "frame={0} scan={1}",
                                           frame, scan + 1),
                    });
                }
            }
        }
    }
}
