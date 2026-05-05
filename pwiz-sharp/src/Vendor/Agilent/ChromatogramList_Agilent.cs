#if !NO_VENDOR_SUPPORT
using Agilent.MassSpectrometry.DataAnalysis;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Spectra;

#pragma warning disable CA1707

namespace Pwiz.Vendor.Agilent;

/// <summary>
/// <see cref="IChromatogramList"/> for Agilent <c>.d</c> directories. C# port of pwiz cpp
/// <c>ChromatogramList_Agilent</c>: emits a run-level TIC plus one absorption / pressure /
/// flow-rate / etc. chromatogram per non-MS signal exposed by the file's
/// <c>INonmsDataReader</c>.
/// </summary>
/// <remarks>
/// Initial port covers TIC + the simplest signal-chromatogram path (UV/DAD absorption).
/// SRM / SIM transition chromatograms are a follow-up; cpp emits one chromatogram per
/// transition driven by <c>MassHunterData::getTransitions()</c>.
/// </remarks>
public sealed class ChromatogramList_Agilent : ChromatogramListBase
{
    private readonly AgilentRawData _raw;
    private readonly bool _ownsRaw;
    private readonly bool _globalChromsAreMs1Only;
    private readonly List<IndexEntry> _index = new();

    /// <summary>DataProcessing emitted as the document's <c>defaultDataProcessingRef</c>.</summary>
    public DataProcessing? Dp { get; set; }

    /// <inheritdoc/>
    public override DataProcessing? DataProcessing => Dp;

    /// <summary>Wraps <paramref name="raw"/>; <paramref name="ownsRaw"/> selects whether
    /// disposing the list disposes the raw data handle.</summary>
    public ChromatogramList_Agilent(AgilentRawData raw, bool ownsRaw, bool globalChromatogramsAreMs1Only)
    {
        ArgumentNullException.ThrowIfNull(raw);
        _raw = raw;
        _ownsRaw = ownsRaw;
        _globalChromsAreMs1Only = globalChromatogramsAreMs1Only;
        CreateIndex();
    }

    private enum ChromKind { Tic, AbsorptionSignal, Srm, Sim }

    private sealed class IndexEntry : ChromatogramIdentity
    {
        public ChromKind Kind;
        public CVID ChromatogramType;
        public string? DeviceNameAndOrdinal; // null for TIC / SRM / SIM
        public string? SignalName;           // null for TIC / SRM / SIM
        public IDeviceInfo? Device;          // null for TIC / SRM / SIM
        public int TransitionIndex = -1;     // index into AgilentRawData.Transitions for SRM/SIM
    }

    /// <inheritdoc/>
    public override int Count => _index.Count;

    /// <inheritdoc/>
    public override ChromatogramIdentity ChromatogramIdentity(int index) => _index[index];

    private void CreateIndex()
    {
        // TIC always comes first. cpp does the same; the run-level TIC is supported on every
        // Agilent file type with MS data.
        _index.Add(new IndexEntry { Index = 0, Id = "TIC", Kind = ChromKind.Tic, ChromatogramType = CVID.MS_TIC_chromatogram });

        // SRM / SIM transitions next (cpp ChromatogramList_Agilent.cpp:266-294 builds the same
        // order: TIC, then one chromatogram per transition from MassHunterData::getTransitions).
        // Id format mirrors cpp's boost.spirit.karma generator output:
        //   SRM: "<polarity>SRM SIC Q1=<q1> Q3=<q3> start=<t> end=<t>"
        //   SIM: "SIM SIC Q1=<q1> start=<t> end=<t>"
        var transitions = _raw.Transitions;
        for (int i = 0; i < transitions.Count; i++)
        {
            var t = transitions[i];
            string id;
            CVID type;
            if (t.Type == AgTransitionType.Mrm)
            {
                string pol = t.Polarity switch
                {
                    AgTransitionPolarity.Positive => "+ ",
                    AgTransitionPolarity.Negative => "- ",
                    _ => string.Empty,
                };
                id = $"{pol}SRM SIC Q1={PwizFloat.ToKarmaNoSci(t.Q1)} Q3={PwizFloat.ToKarmaNoSci(t.Q3)} start={PwizFloat.ToKarmaNoSci(t.TimeStart)} end={PwizFloat.ToKarmaNoSci(t.TimeEnd)}";
                type = CVID.MS_SRM_chromatogram;
            }
            else
            {
                id = $"SIM SIC Q1={PwizFloat.ToKarmaNoSci(t.Q1)} start={PwizFloat.ToKarmaNoSci(t.TimeStart)} end={PwizFloat.ToKarmaNoSci(t.TimeEnd)}";
                type = CVID.MS_SIM_chromatogram;
            }
            _index.Add(new IndexEntry
            {
                Index = _index.Count,
                Id = id,
                Kind = t.Type == AgTransitionType.Mrm ? ChromKind.Srm : ChromKind.Sim,
                ChromatogramType = type,
                TransitionIndex = i,
            });
        }

        // Non-MS signals: UV/DAD/pressure/flow chromatograms. cpp pairs each signal's
        // ISignalInfo (used to fetch the data) with the corresponding SignalDescription pulled
        // from FileInformation.GetSignalTable (which has the human-readable description that
        // ends up in the chromatogram id, e.g. "DAD1 A: Sig=272,16 Ref=360,100"). We currently
        // honor only the absorption-chromatogram bucket (UV/DAD); other device kinds (pumps,
        // columns) return CVID_Unknown and are skipped until a fixture exercises them.
        try
        {
            var nonMs = _raw.NonMsDataReader;
            var fileInfo = _raw.FileInformation;
            if (nonMs is null || fileInfo is null) return;
            var devices = nonMs.GetNonmsDevices();
            if (devices is null) return;

            foreach (var device in devices)
            {
                CVID chromType = TranslateAsChromatogramType(device.DeviceType);
                if (chromType != CVID.MS_absorption_chromatogram) continue;

                string deviceNameAndOrdinal = device.DeviceName + device.OrdinalNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
                ISignalInfo[]? signalInfos;
                try { signalInfos = nonMs.GetSignalInfo(device, StoredDataType.Chromatograms); }
                catch { continue; }
                if (signalInfos is null) continue;

                // Pull descriptions keyed by SignalName from the signal table — cpp does the
                // same. Best-effort: if the table read fails, descriptions stay empty.
                var descriptionsBySignalName = new Dictionary<string, string>(StringComparer.Ordinal);
                try
                {
                    var table = fileInfo.GetSignalTable(deviceNameAndOrdinal, StoredDataType.Chromatograms);
                    if (table is not null)
                    {
                        foreach (System.Data.DataRow row in table.Rows)
                        {
                            string signalName = row["SignalName"]?.ToString() ?? string.Empty;
                            string description = row["SignalDescription"]?.ToString() ?? string.Empty;
                            if (signalName.Length > 0)
                                descriptionsBySignalName[signalName] = description;
                        }
                    }
                }
                catch { /* signal table missing — fall back to empty descriptions */ }

                foreach (var sig in signalInfos)
                {
                    // cpp's id is `signal.deviceName + " " + signal.signalName`, where
                    // `signal.deviceName` is the deviceNameAndOrdinal (e.g. "DAD1"), not the
                    // bare device name. Match that exactly for chromatogram-id parity.
                    string id = deviceNameAndOrdinal + " " + sig.SignalName;
                    if (descriptionsBySignalName.TryGetValue(sig.SignalName, out var desc) && desc.Length > 0)
                        id += ": " + desc;

                    _index.Add(new IndexEntry
                    {
                        Index = _index.Count,
                        Id = id,
                        Kind = ChromKind.AbsorptionSignal,
                        ChromatogramType = chromType,
                        DeviceNameAndOrdinal = deviceNameAndOrdinal,
                        SignalName = sig.SignalName,
                        Device = device,
                    });
                }
            }
        }
        catch { /* SDK quirks shouldn't take down the whole index */ }
    }

    /// <summary>Mirrors a subset of cpp <c>translateAsChromatogramType</c>. Currently only
    /// returns <see cref="CVID.MS_absorption_chromatogram"/> for UV/DAD-style devices; other
    /// device kinds (pumps, columns) return <see cref="CVID.CVID_Unknown"/> and are skipped
    /// by the indexer until a fixture exercises them.</summary>
    private static CVID TranslateAsChromatogramType(DeviceType d) => d switch
    {
        DeviceType.DiodeArrayDetector
            or DeviceType.MultiWavelengthDetector
            or DeviceType.VariableWavelengthDetector
            or DeviceType.FluorescenceDetector
            or DeviceType.ElectronCaptureDetector
            or DeviceType.RefractiveIndexDetector
            or DeviceType.EvaporativeLightScatteringDetector
            or DeviceType.AnalogDigitalConverter
            or DeviceType.FlameIonizationDetector
            or DeviceType.ThermalConductivityDetector
            => CVID.MS_absorption_chromatogram,
        _ => CVID.CVID_Unknown,
    };

    /// <inheritdoc/>
    public override Chromatogram GetChromatogram(int index, bool getBinaryData = false)
    {
        if (index < 0 || index >= _index.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        var ie = _index[index];

        var c = new Chromatogram
        {
            Index = index,
            Id = ie.Id,
        };
        c.Params.Set(ie.ChromatogramType);

        switch (ie.Kind)
        {
            case ChromKind.Tic:
                FillTic(c, getBinaryData);
                break;

            case ChromKind.AbsorptionSignal:
                FillSignal(c, ie, getBinaryData);
                break;

            case ChromKind.Srm:
            case ChromKind.Sim:
                FillTransition(c, ie, getBinaryData);
                break;
        }

        return c;
    }

    private void FillTransition(Chromatogram c, IndexEntry ie, bool getBinaryData)
    {
        if (ie.TransitionIndex < 0)
        {
            c.DefaultArrayLength = 0;
            return;
        }
        var transitions = _raw.Transitions;
        if (ie.TransitionIndex >= transitions.Count)
        {
            c.DefaultArrayLength = 0;
            return;
        }
        var t = transitions[ie.TransitionIndex];

        // Polarity tag mirrors cpp ChromatogramList_Agilent.cpp:139-141 / :174-176.
        if (t.Polarity == AgTransitionPolarity.Positive) c.Params.Set(CVID.MS_positive_scan);
        else if (t.Polarity == AgTransitionPolarity.Negative) c.Params.Set(CVID.MS_negative_scan);

        // Precursor / product isolation windows. SRM has both Q1 and Q3 bounds; SIM has Q1
        // only. cpp also emits MS_CID + collision energy on the precursor activation, but the
        // collision energy comes from the chromatogram object; we'll wire that in if/when
        // a fixture surfaces a non-zero CE diff.
        c.Precursor.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, t.Q1, CVID.MS_m_z);
        if (ie.Kind == ChromKind.Srm)
        {
            c.Precursor.Activation.Set(CVID.MS_CID);
            // cpp ChromatogramList_Agilent.cpp:144 — collision energy reads as a unit-less
            // scalar from the SDK (no unit cvParam attached). Match that.
            c.Precursor.Activation.Set(CVID.MS_collision_energy, t.CollisionEnergy);
            c.Product.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, t.Q3, CVID.MS_m_z);
        }

        // Binary data — pull from the cached chromatogram the SDK returned during transition
        // discovery. cpp also caches by reference (member array) for the same 50x perf reason.
        var chrom = _raw.GetTransitionChromatogram(ie.TransitionIndex);
        if (chrom is null)
        {
            c.DefaultArrayLength = 0;
            return;
        }
        var x = chrom.XArray ?? Array.Empty<double>();
        var y = chrom.YArray ?? Array.Empty<float>();
        int n = Math.Min(x.Length, y.Length);
        c.DefaultArrayLength = n;
        if (getBinaryData && n > 0)
        {
            var times = new BinaryDataArray();
            times.Set(CVID.MS_time_array, "", CVID.UO_minute);
            for (int i = 0; i < n; i++) times.Data.Add(x[i]);
            var inten = new BinaryDataArray();
            inten.Set(CVID.MS_intensity_array, "", CVID.MS_number_of_detector_counts);
            for (int i = 0; i < n; i++) inten.Data.Add(y[i]);
            c.BinaryDataArrays.Add(times);
            c.BinaryDataArrays.Add(inten);
        }
    }

    private void FillTic(Chromatogram c, bool getBinaryData)
    {
        // Build TIC by walking scan records (RetentionTime, TIC). cpp does the same in
        // MassHunterDataImpl ctor: SDK's GetTIC() returns an IBDAChromData but populating it
        // is awkward and the per-scan-record path mirrors what cpp emits exactly.
        int total = (int)_raw.TotalScansPresent;
        bool onlyMs1 = _globalChromsAreMs1Only;
        var times = new List<double>(total);
        var intensities = new List<double>(total);
        var msLevels = new List<long>(total);
        for (int i = 0; i < total; i++)
        {
            IMSScanRecord rec;
            try { rec = _raw.GetScanRecord(i); }
            catch { continue; }
            int level = rec.MSLevel == MSLevel.MSMS ? 2 : 1;
            if (onlyMs1 && level != 1) continue;
            times.Add(rec.RetentionTime);
            intensities.Add(rec.Tic);
            msLevels.Add(level);
        }
        c.DefaultArrayLength = times.Count;
        if (getBinaryData && times.Count > 0)
        {
            var t = new BinaryDataArray();
            t.Set(CVID.MS_time_array, "", CVID.UO_minute);
            t.Data.AddRange(times);
            var y = new BinaryDataArray();
            y.Set(CVID.MS_intensity_array, "", CVID.MS_number_of_detector_counts);
            y.Data.AddRange(intensities);
            c.BinaryDataArrays.Add(t);
            c.BinaryDataArrays.Add(y);

            // ms-level integer array — cpp emits this on the TIC so consumers can filter by
            // MS level without re-walking the scan list.
            var ms = new IntegerDataArray();
            ms.Set(CVID.MS_non_standard_data_array, "ms level", CVID.UO_dimensionless_unit);
            ms.Data.AddRange(msLevels);
            c.IntegerDataArrays.Add(ms);
        }
    }

    private void FillSignal(Chromatogram c, IndexEntry ie, bool getBinaryData)
    {
        if (ie.Device is null || ie.SignalName is null)
        {
            c.DefaultArrayLength = 0;
            return;
        }
        var nonMs = _raw.NonMsDataReader;
        if (nonMs is null) { c.DefaultArrayLength = 0; return; }

        // KNOWN GAP: nonMs.GetSignal(ISignalInfo) returns null on the .NET 8 / Cecil-patched
        // SDK path even after re-fetching the IDeviceInfo + ISignalInfo on each call. cpp
        // (running on .NET Framework 4.8 from C++/CLI) gets the actual chromatogram data
        // through the same call, so the difference is somewhere in the CLR-host integration —
        // not in the BeginInvoke surface we already patched (we re-inventoried the SDK and
        // confirmed there are no other Begin/End sites). Until that's diagnosed, the absorption
        // chromatogram is emitted with the correct id but zero-length arrays. Leaves a 1-diff
        // remainder vs cpp's reference mzML for fixtures that include UV/DAD chromatograms.
        IBDAChromData? signalData = null;
        try
        {
            var devices = nonMs.GetNonmsDevices();
            IDeviceInfo? device = null;
            if (devices is not null)
            {
                foreach (var d in devices)
                {
                    string nameAndOrdinal = d.DeviceName + d.OrdinalNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    if (string.Equals(nameAndOrdinal, ie.DeviceNameAndOrdinal, StringComparison.Ordinal)) { device = d; break; }
                }
            }
            if (device is null) { c.DefaultArrayLength = 0; return; }

            var infos = nonMs.GetSignalInfo(device, StoredDataType.Chromatograms);
            ISignalInfo? match = null;
            if (infos is not null)
            {
                foreach (var s in infos)
                {
                    if (string.Equals(s.SignalName, ie.SignalName, StringComparison.Ordinal)) { match = s; break; }
                }
            }
            if (match is not null) signalData = nonMs.GetSignal(match);
        }
        catch { c.DefaultArrayLength = 0; return; }
        if (signalData is null) { c.DefaultArrayLength = 0; return; }

        double[] times = signalData.XArray ?? Array.Empty<double>();
        float[] yArray = signalData.YArray ?? Array.Empty<float>();
        int n = Math.Min(times.Length, yArray.Length);
        c.DefaultArrayLength = n;
        if (getBinaryData && n > 0)
        {
            // Absorption chromatograms use absorbance unit (mAU). Pressure / flow conversions
            // (which need bar -> Pa and mL/min -> uL/min scaling per cpp) are added when those
            // device kinds land.
            EmitTimeIntensityArrays(c, times, yArray, n, CVID.UO_absorbance_unit);
        }
    }

    private static void EmitTimeIntensityArrays(Chromatogram c, double[] times, float[] yArray, int n, CVID intensityUnit)
    {
        var t = new BinaryDataArray();
        t.Set(CVID.MS_time_array, "", CVID.UO_minute);
        var y = new BinaryDataArray();
        y.Set(CVID.MS_intensity_array, "", intensityUnit);
        for (int i = 0; i < n; i++) t.Data.Add(times[i]);
        for (int i = 0; i < n; i++) y.Data.Add(yArray[i]);
        c.BinaryDataArrays.Add(t);
        c.BinaryDataArrays.Add(y);
    }

    /// <inheritdoc/>
    protected override void DisposeCore()
    {
        if (_ownsRaw) _raw.Dispose();
        base.DisposeCore();
    }
}
#endif
