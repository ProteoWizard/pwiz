using System.Globalization;
using Agilent.MassSpectrometry.DataAnalysis;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData.Instruments;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Data.MsData.Processing;
using AgPolarity = Agilent.MassSpectrometry.DataAnalysis.IonPolarity;
using AgScanType = Agilent.MassSpectrometry.DataAnalysis.MSScanType;

#pragma warning disable CA1707

namespace Pwiz.Vendor.Agilent;

/// <summary>
/// <see cref="ISpectrumList"/> backed by an Agilent <see cref="AgilentRawData"/>. Initial
/// port of pwiz C++ <c>SpectrumList_Agilent</c>: handles Q-TOF / TQ / single-quad / TOF MS
/// scans (<c>Scan</c>, <c>ProductIon</c>, <c>PrecursorIon</c>, <c>NeutralLoss</c>/<c>NeutralGain</c>),
/// optionally <c>SelectedIon</c> / <c>MultipleReaction</c> when <c>simAsSpectra</c> /
/// <c>srmAsSpectra</c> is set on the constructor. <c>TotalIon</c> is always emitted as a chromatogram.
/// </summary>
/// <remarks>
/// Skipped: ion-mobility frames (MIDAC), non-MS UV/DAD spectra, all-ions scan promotion. Each is
/// a follow-up port; current scope covers the bulk of Q-TOF / TQ files.
/// </remarks>
public sealed class SpectrumList_Agilent : SpectrumListBase
{
    private readonly AgilentRawData _raw;
    private readonly bool _ownsRaw;
    private readonly InstrumentConfiguration? _defaultIc;
    private readonly bool _simAsSpectra;
    private readonly bool _srmAsSpectra;
    private readonly List<IndexEntry> _index = new();

    /// <summary>DataProcessing emitted as the document's <c>defaultDataProcessingRef</c>.</summary>
    public DataProcessing? Dp { get; set; }

    /// <inheritdoc/>
    public override DataProcessing? DataProcessing => Dp;

    /// <summary>Wraps <paramref name="raw"/>. When <paramref name="ownsRaw"/> is true, disposes
    /// the underlying reader on Dispose.</summary>
    public SpectrumList_Agilent(AgilentRawData raw, bool ownsRaw,
        InstrumentConfiguration? defaultInstrumentConfiguration,
        bool simAsSpectra, bool srmAsSpectra)
    {
        ArgumentNullException.ThrowIfNull(raw);
        _raw = raw;
        _ownsRaw = ownsRaw;
        _defaultIc = defaultInstrumentConfiguration;
        _simAsSpectra = simAsSpectra;
        _srmAsSpectra = srmAsSpectra;
        CreateIndex();
    }

    private sealed class IndexEntry : SpectrumIdentity
    {
        public int RowNumber;
        public int ScanId;
        public AgScanType ScanType;
        public bool IsNonMs { get; set; }  // true => UV/DAD spectrum, fetched via GetNonMsSpectrumByRow
    }

    /// <inheritdoc/>
    public override int Count => _index.Count;

    /// <inheritdoc/>
    public override SpectrumIdentity SpectrumIdentity(int index) => _index[index];

    private void CreateIndex()
    {
        long total = _raw.TotalScansPresent;
        for (int row = 0; row < total; row++)
        {
            IMSScanRecord rec;
            try { rec = _raw.GetScanRecord(row); }
            catch { continue; }
            var scanType = rec.MSScanType;

            // Chromatogram-centric scan types are emitted as chromatograms unless explicitly
            // promoted. TotalIon never becomes a spectrum (cpp same).
            if (scanType == AgScanType.TotalIon) continue;
            if (scanType == AgScanType.SelectedIon && !_simAsSpectra) continue;
            if (scanType == AgScanType.MultipleReaction && !_srmAsSpectra) continue;

            _index.Add(new IndexEntry
            {
                Index = _index.Count,
                Id = "scanId=" + rec.ScanID.ToString(CultureInfo.InvariantCulture),
                RowNumber = row,
                ScanId = rec.ScanID,
                ScanType = scanType,
            });
        }

        // Append non-MS (UV/DAD) spectra after the MS run. cpp does the same in
        // SpectrumList_Agilent::createIndex; ids look like `merged=<index> row=<rowNumber>`
        // where rowNumber is the 0-based offset into the DAD time grid.
        int nonMsCount = _raw.GetNonMsScanCount();
        for (int row = 0; row < nonMsCount; row++)
        {
            int idx = _index.Count;
            _index.Add(new IndexEntry
            {
                Index = idx,
                Id = $"merged={idx} row={row}",
                RowNumber = row,
                ScanId = -1,
                ScanType = AgScanType.Unspecified,
                IsNonMs = true,
            });
        }
    }

    /// <inheritdoc/>
    public override Spectrum GetSpectrum(int index, bool getBinaryData = false)
    {
        if (index < 0 || index >= _index.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        var ie = _index[index];

        if (ie.IsNonMs) return BuildNonMsSpectrum(index, ie, getBinaryData);

        var rec = _raw.GetScanRecord(ie.RowNumber);

        var spec = new Spectrum
        {
            Index = index,
            Id = ie.Id,
        };

        spec.ScanList.Set(CVID.MS_no_combination);
        var scan = new Scan
        {
            InstrumentConfiguration = _defaultIc,
        };
        spec.ScanList.Scans.Add(scan);

        int msLevel = TranslateAsMsLevel(ie.ScanType, rec.MSLevel);
        var spectrumType = TranslateAsSpectrumType(ie.ScanType);

        // Polarity.
        if (rec.IonPolarity == AgPolarity.Positive) spec.Params.Set(CVID.MS_positive_scan);
        else if (rec.IonPolarity == AgPolarity.Negative) spec.Params.Set(CVID.MS_negative_scan);

        // Run-level base peak / TIC / RT come from the scan record (faster than the full spec).
        // cpp routes base-peak-intensity / TIC through float arrays
        // (BinaryData<float> getBpcIntensities / getTicIntensities), so the rendered cvParam
        // value picks up boost lexical_cast<string>(float) precision = 6 significant figures.
        // Cast to float here to mirror — without this, the SDK's full-precision double
        // produces 12-digit values that miss the cpp reference by per-spectrum string diff.
        spec.Params.Set(CVID.MS_base_peak_m_z, rec.BasePeakMZ, CVID.MS_m_z);
        spec.Params.Set(CVID.MS_base_peak_intensity, (float)rec.BasePeakIntensity, CVID.MS_number_of_detector_counts);
        spec.Params.Set(CVID.MS_total_ion_current, (float)rec.Tic, CVID.MS_number_of_detector_counts);
        scan.Set(CVID.MS_scan_start_time, rec.RetentionTime, CVID.UO_minute);

        // Precursor: cpp uses MZOfInterest from the ScanRecord plus full spectrum metadata for
        // charge / intensity. For msLevel > 1 product/precursor scans we wire the isolation
        // window target from MZOfInterest.
        double mzOfInterest = rec.MZOfInterest;
        bool isNeutralLossOrGain = ie.ScanType == AgScanType.NeutralLoss || ie.ScanType == AgScanType.NeutralGain;
        Precursor? precursor = null;
        if (msLevel > 1 && mzOfInterest > 0)
        {
            precursor = new Precursor();
            if (ie.ScanType == AgScanType.PrecursorIon)
            {
                var product = new Product();
                product.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, mzOfInterest, CVID.MS_m_z);
                spec.Products.Add(product);
            }
            else if (isNeutralLossOrGain)
            {
                scan.Set(CVID.MS_analyzer_scan_offset, mzOfInterest, CVID.MS_m_z);
            }
            else
            {
                precursor.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, mzOfInterest, CVID.MS_m_z);
                var selected = new SelectedIon();
                selected.Set(CVID.MS_selected_ion_m_z, mzOfInterest, CVID.MS_m_z);
                precursor.SelectedIons.Add(selected);
            }
            CVID activation = TranslateAsActivation(_raw.DeviceType);
            precursor.Activation.Set(activation == CVID.CVID_Unknown ? CVID.MS_CID : activation);
            precursor.Activation.Set(CVID.MS_collision_energy, rec.CollisionEnergy, CVID.UO_electronvolt);
            spec.Precursors.Add(precursor);
        }

        spec.Params.Set(CVID.MS_ms_level, msLevel);
        spec.Params.Set(spectrumType);

        // Pull the full spectrum to get peaks + scan-window range. Cpp prefers profile when
        // available unless centroiding is requested; we follow the same default.
        bool preferProfile = _raw.HasProfileData;
        IBDASpecData? specData;
        try { specData = _raw.GetSpectrumByRow(ie.RowNumber, preferProfile); }
        catch { specData = null; }

        if (specData is not null)
        {
            var range = specData.MeasuredMassRange;
            if (range is not null)
                scan.ScanWindows.Add(new ScanWindow(range.Start, range.End, CVID.MS_m_z));

            // Profile-vs-centroid CV. mzML wants exactly one of these per spectrum.
            bool isProfile = specData.MSStorageMode == MSStorageMode.ProfileSpectrum;
            spec.Params.Set(isProfile ? CVID.MS_profile_spectrum : CVID.MS_centroid_spectrum);

            // cpp SpectrumList_Agilent.cpp:339-340: when the SDK reports centroided storage on
            // a non-IM scan, tag the scan window so consumers know the lower/upper limits came
            // from the centroided peak range (specData.MeasuredMassRange) rather than the
            // configured acquisition window. mzML doesn't have a CVID for this so cpp uses a
            // bare userParam.
            if (!isProfile && scan.ScanWindows.Count > 0)
                scan.ScanWindows[^1].UserParams.Add(new UserParam("centroided min/max"));

            // cpp SpectrumList_Agilent.cpp:375-387: for NL/NG (and all-ions MS2) the precursor
            // needs a broad isolation window covering the whole scan mass range plus a sentinel
            // selected ion at the midpoint — mzML's selectedIonList has minOccurs=1, so omitting
            // it produces an invalid file. The "real" precursor info lives in MS_analyzer_scan_offset
            // (set earlier on the scan) for NL/NG, and there's no specific precursor m/z, so cpp
            // synthesizes a placeholder mid-window ion to satisfy the schema.
            if (precursor is not null && isNeutralLossOrGain && range is not null)
            {
                double width = (range.End - range.Start) * 0.5;
                double targetMz = range.Start + width;
                precursor.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, targetMz, CVID.MS_m_z);
                precursor.IsolationWindow.Set(CVID.MS_isolation_window_lower_offset, width, CVID.MS_m_z);
                precursor.IsolationWindow.Set(CVID.MS_isolation_window_upper_offset, width, CVID.MS_m_z);
                var sentinel = new SelectedIon();
                sentinel.Set(CVID.MS_selected_ion_m_z, targetMz, CVID.MS_m_z);
                precursor.SelectedIons.Add(sentinel);
            }

            // Fill in precursor charge + intensity from the full spectrum data when present.
            if (precursor is not null && precursor.SelectedIons.Count > 0)
            {
                int chargeOut = 0;
                if (specData.GetPrecursorCharge(out chargeOut) && chargeOut > 0)
                    precursor.SelectedIons[0].Set(CVID.MS_charge_state, chargeOut);
                double intensityOut = 0;
                if (specData.GetPrecursorIntensity(out intensityOut) && intensityOut > 0)
                    precursor.SelectedIons[0].Set(CVID.MS_peak_intensity, intensityOut, CVID.MS_number_of_detector_counts);
                int parentScanId = specData.ParentScanId;
                if (parentScanId > 0)
                    precursor.SpectrumId = "scanId=" + parentScanId.ToString(CultureInfo.InvariantCulture);
            }

            int n = specData.TotalDataPoints;
            spec.DefaultArrayLength = n;
            if (getBinaryData && n > 0)
            {
                double[] x = specData.XArray ?? Array.Empty<double>();
                float[] y = specData.YArray ?? Array.Empty<float>();
                int len = Math.Min(Math.Min(x.Length, y.Length), n);
                if (isProfile && len >= 3)
                    (x, y) = TrimProfileZeros(x, y, len);
                else
                    y = SliceFloat(y, len);
                var doubleY = new double[y.Length];
                for (int i = 0; i < y.Length; i++) doubleY[i] = y[i];
                spec.SetMZIntensityArrays(SliceDouble(x, y.Length), doubleY, CVID.MS_number_of_detector_counts);

                // cpp SpectrumList_Agilent.cpp:514-533: lowest/highest observed m/z come from
                // the first / last non-zero-intensity points in the trimmed data. The "first
                // 10" window for lowest is a perf shortcut from cpp — Agilent profile data
                // begins with framing zeros, so the first non-zero typically lies in the first
                // few samples; capping at 10 avoids scanning the whole array. Highest scans
                // backward from the end. With centroided data (or post-trim profile), the
                // first/last samples are already non-zero so the loops short-circuit.
                if (x.Length > 0 && y.Length > 0)
                {
                    int loBound = Math.Min(10, y.Length);
                    for (int i = 0; i < loBound; i++)
                    {
                        if (y[i] > 0)
                        {
                            spec.Params.Set(CVID.MS_lowest_observed_m_z, x[i], CVID.MS_m_z);
                            break;
                        }
                    }
                    for (int i = y.Length - 1; i >= 0; i--)
                    {
                        if (y[i] > 0)
                        {
                            spec.Params.Set(CVID.MS_highest_observed_m_z, x[i], CVID.MS_m_z);
                            break;
                        }
                    }
                }
            }
        }
        else
        {
            spec.Params.Set(CVID.MS_centroid_spectrum);
        }

        return spec;
    }

    /// <summary>
    /// Builds a UV/DAD spectrum from <see cref="AgilentRawData.GetNonMsSpectrumByRow"/>.
    /// Mirrors cpp <c>SpectrumList_Agilent::getNonMsSpectrum</c>: tags MS_EMR_spectrum +
    /// MS_profile_spectrum, emits wavelength + intensity arrays, computes
    /// MS_lowest/highest_observed_wavelength from the first / last non-zero intensity points.
    /// </summary>
    private Spectrum BuildNonMsSpectrum(int index, IndexEntry ie, bool getBinaryData)
    {
        var spec = new Spectrum
        {
            Index = index,
            Id = ie.Id,
        };
        spec.Params.Set(CVID.MS_EMR_spectrum);
        spec.Params.Set(CVID.MS_profile_spectrum);

        spec.ScanList.Set(CVID.MS_no_combination);
        var scan = new Scan { InstrumentConfiguration = _defaultIc };
        spec.ScanList.Scans.Add(scan);

        var sd = _raw.GetNonMsSpectrumByRow(ie.RowNumber);
        if (sd is null)
        {
            spec.DefaultArrayLength = 0;
            return spec;
        }

        try
        {
            var rt = sd.AcquiredTimeRange;
            if (rt is not null && rt.Length > 0)
                scan.Set(CVID.MS_scan_start_time, rt[0].Start, CVID.UO_minute);
        }
        catch { /* non-fatal */ }

        double[] wavelengths = sd.XArray ?? Array.Empty<double>();
        float[] yArray = sd.YArray ?? Array.Empty<float>();
        int n = Math.Min(wavelengths.Length, yArray.Length);
        spec.DefaultArrayLength = n;

        // lowest/highest observed wavelength: first/last non-zero intensity points. cpp only
        // checks the first 10 / last n for the lower bound in a quirk we mirror exactly.
        if (n > 0)
        {
            int scanLow = Math.Min(10, n);
            for (int i = 0; i < scanLow; i++)
            {
                if (yArray[i] != 0f)
                {
                    spec.Params.Set(CVID.MS_lowest_observed_wavelength, wavelengths[i], CVID.UO_nanometer);
                    break;
                }
            }
            for (int i = n - 1; i > 0; i--)
            {
                if (yArray[i] != 0f)
                {
                    spec.Params.Set(CVID.MS_highest_observed_wavelength, wavelengths[i], CVID.UO_nanometer);
                    break;
                }
            }
        }

        if (getBinaryData && n > 0)
        {
            var w = new BinaryDataArray();
            w.Set(CVID.MS_wavelength_array, "", CVID.UO_nanometer);
            w.Data.Capacity = n;
            for (int i = 0; i < n; i++) w.Data.Add(wavelengths[i]);
            var y = new BinaryDataArray();
            y.Set(CVID.MS_intensity_array, "", CVID.MS_number_of_detector_counts);
            y.Data.Capacity = n;
            for (int i = 0; i < n; i++) y.Data.Add(yArray[i]);
            spec.BinaryDataArrays.Add(w);
            spec.BinaryDataArrays.Add(y);
        }

        return spec;
    }

    /// <summary>
    /// Mirrors the cpp Agilent profile-zero-filter: profile spectra return mostly-zero pairs;
    /// keep only points adjacent to at least one non-zero, plus the outermost zeros so consumers
    /// see the m/z bounds. Returns trimmed arrays sized to the kept count.
    /// </summary>
    private static (double[] mz, float[] intensity) TrimProfileZeros(double[] x, float[] y, int n)
    {
        var mz = new double[n];
        var inten = new float[n];
        int idx = 0;
        int last = n;
        // Find first non-zero.
        int i = 0;
        while (i < last && y[i] == 0f) i++;
        if (i >= last) return (Array.Empty<double>(), Array.Empty<float>());
        // Emit one preceding zero to anchor the lower bound.
        if (i > 0) { mz[idx] = x[i - 1]; inten[idx] = 0f; idx++; }
        mz[idx] = x[i]; inten[idx] = y[i]; idx++;
        i++;
        while (i < last)
        {
            if (y[i] != 0f) { mz[idx] = x[i]; inten[idx] = y[i]; idx++; i++; continue; }
            // emit one zero adjacent to a non-zero, then skip over the zero run
            mz[idx] = x[i]; inten[idx] = 0f; idx++;
            int z = i + 1;
            while (z < last && y[z] == 0f) z++;
            if (z < last)
            {
                if (z > i + 1) { mz[idx] = x[z - 1]; inten[idx] = 0f; idx++; }
                mz[idx] = x[z]; inten[idx] = y[z]; idx++;
                i = z + 1;
            }
            else break;
        }
        Array.Resize(ref mz, idx);
        Array.Resize(ref inten, idx);
        return (mz, inten);
    }

    private static double[] SliceDouble(double[] src, int len)
    {
        if (len == src.Length) return src;
        var dst = new double[len];
        Array.Copy(src, dst, len);
        return dst;
    }

    private static float[] SliceFloat(float[] src, int len)
    {
        if (len == src.Length) return src;
        var dst = new float[len];
        Array.Copy(src, dst, len);
        return dst;
    }

    /// <summary>Maps Agilent MSScanType to mzML spectrum-type CVID. Mirrors cpp <c>translateAsSpectrumType</c>.</summary>
    public static CVID TranslateAsSpectrumType(AgScanType t) => t switch
    {
        AgScanType.Scan => CVID.MS_MS1_spectrum,
        AgScanType.ProductIon => CVID.MS_MSn_spectrum,
        AgScanType.PrecursorIon => CVID.MS_precursor_ion_spectrum,
        AgScanType.SelectedIon => CVID.MS_SIM_spectrum,
        AgScanType.TotalIon => CVID.MS_SIM_spectrum,
        AgScanType.MultipleReaction => CVID.MS_SRM_spectrum,
        AgScanType.NeutralLoss => CVID.MS_constant_neutral_loss_spectrum,
        AgScanType.NeutralGain => CVID.MS_constant_neutral_gain_spectrum,
        _ => CVID.MS_MS1_spectrum,
    };

    /// <summary>
    /// Maps Agilent MSScanType + scan-record MSLevel to a mzML ms_level integer. Cpp
    /// <c>translateAsMSLevel</c> returns 1 for SIM/TIC and -1 for PrecursorIon (which mzML can't
    /// represent — we coerce to 2 since pwiz cpp's caller does the same effectively).
    /// </summary>
    public static int TranslateAsMsLevel(AgScanType t, MSLevel recordLevel) => t switch
    {
        AgScanType.Scan => 1,
        AgScanType.SelectedIon => 1,
        AgScanType.TotalIon => 1,
        AgScanType.ProductIon => 2,
        AgScanType.MultipleReaction => 2,
        AgScanType.NeutralLoss => 2,
        AgScanType.NeutralGain => 2,
        AgScanType.PrecursorIon => 2,
        _ => recordLevel == MSLevel.MSMS ? 2 : 1,
    };

    /// <summary>Mirrors cpp <c>translateAsActivationType</c>.</summary>
    public static CVID TranslateAsActivation(DeviceType deviceType) => deviceType switch
    {
        DeviceType.IonTrap => CVID.MS_trap_type_collision_induced_dissociation,
        DeviceType.TandemQuadrupole or DeviceType.Quadrupole or DeviceType.QuadrupoleTimeOfFlight
            => CVID.MS_beam_type_collision_induced_dissociation,
        DeviceType.TimeOfFlight => CVID.MS_in_source_collision_induced_dissociation,
        _ => CVID.MS_CID,
    };

    /// <inheritdoc/>
    protected override void DisposeCore()
    {
        if (_ownsRaw) _raw.Dispose();
        base.DisposeCore();
    }
}
