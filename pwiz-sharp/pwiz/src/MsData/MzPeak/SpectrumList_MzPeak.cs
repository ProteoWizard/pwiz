using System;
using System.Collections.Generic;
using System.Linq;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Data.MsData.MzPeak;

/// <summary>
/// Lazy <see cref="ISpectrumList"/> over an <see cref="MzPeakReader"/>. The
/// reader already buffers every metadata column in memory at open time, so
/// "lazy" here means "translate columns to <see cref="Spectrum"/> objects on
/// demand" — there's no extra parquet I/O per call. Binary data
/// (m/z + intensity arrays) is still pulled from the per-spectrum buckets the
/// reader built up at construction.
/// </summary>
internal sealed class SpectrumList_MzPeak : SpectrumListBase
{
    private readonly MzPeakReader _reader;
    private readonly bool _ownsReader;
    private readonly DataProcessing? _dp;
    private readonly SpectrumIdentity[] _identities;
    // referenceableParamGroups referenced by spectra, keyed by id, plus the set of CVIDs each
    // provides (so params a referenced group already carries aren't also inlined as direct params).
    private readonly Dictionary<string, ParamGroup> _paramGroupsById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<CVID>> _paramGroupCvids = new(StringComparer.Ordinal);

    public SpectrumList_MzPeak(MzPeakReader reader, DataProcessing? dp, bool ownsReader,
        IReadOnlyList<ParamGroup>? paramGroups = null)
    {
        ArgumentNullException.ThrowIfNull(reader);
        _reader = reader;
        _ownsReader = ownsReader;
        _dp = dp;
        if (paramGroups is not null)
            foreach (var pg in paramGroups)
            {
                _paramGroupsById[pg.Id] = pg;
                _paramGroupCvids[pg.Id] = pg.CVParams.Select(cv => cv.Cvid).ToHashSet();
            }
        _identities = new SpectrumIdentity[reader.SpectrumCount];
        for (int i = 0; i < reader.SpectrumCount; i++)
        {
            // Use the lightweight id accessor — building identities must not force a full lazy
            // metadata-group load for every spectrum at open.
            _identities[i] = new SpectrumIdentity { Index = i, Id = reader.GetSpectrumId(i) };
        }
    }

    public override int Count => _identities.Length;

    public override SpectrumIdentity SpectrumIdentity(int index) => _identities[index];

    public override DataProcessing? DataProcessing => _dp;

    public override Spectrum GetSpectrum(int index, bool getBinaryData = false)
    {
        if ((uint)index >= (uint)_identities.Length)
            throw new ArgumentOutOfRangeException(nameof(index));

        var desc = _reader.GetSpectrumDescription(index);
        var spectrum = new Spectrum
        {
            Index = index,
            Id = desc.Id,
        };

        TranslateSpectrumLevel(desc, spectrum);
        TranslateScan(desc, spectrum);
        TranslatePrecursors(desc, spectrum);
        ApplyScanCombination(desc, spectrum);
        ApplyParamGroupRefs(desc, spectrum);

        if (getBinaryData)
        {
            // pwiz/mzML spectra always carry an m/z + intensity array pair, even when empty
            // (a 0-point spectrum still serializes two zero-length arrays). Emit them
            // unconditionally so empty spectra round-trip with the right array shape.
            var data = _reader.GetSpectrumData(index);
            var mz = data?.Mz ?? Array.Empty<double>();
            var intensity = data?.Intensity ?? Array.Empty<float>();
            // mzPeak stores intensities as float for size; pwiz's binary arrays are double —
            // widen here. Unit is "detector counts" matching what every other reader emits when
            // the vendor doesn't tag a more specific unit.
            var intensityDouble = new double[intensity.Length];
            for (int i = 0; i < intensity.Length; i++) intensityDouble[i] = intensity[i];
            if (desc.ValueArrayCurie is null)
            {
                // Common case: the value array is m/z.
                spectrum.SetMZIntensityArrays(mz, intensityDouble, CVID.MS_number_of_detector_counts);
            }
            else
            {
                // Non-m/z value array (e.g. UV/DAD wavelength): rebuild with its real type + unit.
                var valueCvid = CvidFromCurie(desc.ValueArrayCurie);
                var valueUnit = CvidFromCurie(desc.ValueArrayUnitCurie);
                var valueArr = new Pwiz.Data.MsData.Spectra.BinaryDataArray();
                valueArr.Set(valueCvid, "", valueUnit);
                valueArr.Data.AddRange(mz);
                spectrum.BinaryDataArrays.Add(valueArr);
                var intArr = new Pwiz.Data.MsData.Spectra.BinaryDataArray();
                intArr.Set(CVID.MS_intensity_array, "", CVID.MS_number_of_detector_counts);
                intArr.Data.AddRange(intensityDouble);
                spectrum.BinaryDataArrays.Add(intArr);
                spectrum.DefaultArrayLength = mz.Length;
            }
            MzPeakAuxArrays.Apply(desc.AuxArrays, spectrum.BinaryDataArrays, spectrum.IntegerDataArrays);
        }
        else if (desc.NumberOfDataPoints is long n)
        {
            // Caller asked for metadata-only — keep the array-length hint so
            // downstream code can decide how much to allocate later.
            spectrum.DefaultArrayLength = checked((int)n);
        }

        return spectrum;
    }

    protected override void DisposeCore()
    {
        if (_ownsReader) _reader.Dispose();
    }

    // ===== Translation: mzPeak columns → pwiz MSData params =====

    private static void TranslateSpectrumLevel(MzPeakReader.SpectrumDescription desc, Spectrum spectrum)
    {
        // Representation: profile vs centroid. cpp writers emit one of these
        // unconditionally; we mirror that so downstream code that branches on
        // HasCVParam(MS_centroid_spectrum) sees something sensible.
        if (desc.IsProfile) spectrum.Params.Set(CVID.MS_profile_spectrum);
        else if (desc.IsCentroid) spectrum.Params.Set(CVID.MS_centroid_spectrum);

        if (desc.MsLevel is int msLevel) spectrum.Params.Set(CVID.MS_ms_level, msLevel);

        // mzPeak's scan_polarity column is encoded as MSData's Int8 +1/-1
        // (per CV MS:1000465). pwiz emits the polarity CV terms directly.
        switch (desc.ScanPolarity)
        {
            case 1: spectrum.Params.Set(CVID.MS_positive_scan); break;
            case -1: spectrum.Params.Set(CVID.MS_negative_scan); break;
        }

        if (desc.BasePeakMz is double bpmz) spectrum.Params.Set(CVID.MS_base_peak_m_z, bpmz, CVID.MS_m_z);
        if (desc.BasePeakIntensity is double bpi)
            spectrum.Params.Set(CVID.MS_base_peak_intensity, bpi, CVID.MS_number_of_detector_counts);
        if (desc.TotalIonCurrent is double tic)
            // mzML emits total ion current unitless (matching pwiz cpp output); don't synthesize a unit.
            spectrum.Params.Set(CVID.MS_total_ion_current, tic);
        if (desc.LowestObservedMz is double lmz)
            spectrum.Params.Set(CVID.MS_lowest_observed_m_z, lmz, CVID.MS_m_z);
        if (desc.HighestObservedMz is double hmz)
            spectrum.Params.Set(CVID.MS_highest_observed_m_z, hmz, CVID.MS_m_z);

        ApplyParams(spectrum.Params, desc.Parameters);
    }

    private static void TranslateScan(MzPeakReader.SpectrumDescription desc, Spectrum spectrum)
    {
        if (desc.Scan is not { } src) return;

        var scan = new Scan();
        if (!string.IsNullOrEmpty(src.SpectrumRef)) scan.SpectrumId = src.SpectrumRef!;
        if (src.StartTime is double st)
            scan.Set(CVID.MS_scan_start_time, st, CVID.UO_minute);
        if (!string.IsNullOrEmpty(src.FilterString))
            scan.Set(CVID.MS_filter_string, src.FilterString!);
        if (src.IonInjectionTime is double iit)
            scan.Set(CVID.MS_ion_injection_time, iit, CVID.UO_millisecond);
        if (src.PresetScanConfiguration is long psc)
            scan.Set(CVID.MS_preset_scan_configuration, psc);

        // Ion mobility — the type CURIE tells us which CV term to write the
        // value under (drift time, reverse drift time, FAIMS compensation V, …).
        // Translate by accession; unknown types are emitted as a user param so
        // round-trip survives even when the CV table doesn't know the term.
        if (src.IonMobilityValue is double im)
        {
            var imCvid = CvidFromCurie(src.IonMobilityTypeCurie);
            if (imCvid != CVID.CVID_Unknown)
                scan.Set(imCvid, im);
            else
                scan.UserParams.Add(new Pwiz.Data.Common.Params.UserParam(
                    "ion mobility value", im.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        var windowParams = ScanWindowParams.Parse(src.ScanWindowParamsJson);
        for (int wi = 0; wi < src.ScanWindows.Count; wi++)
        {
            var win = src.ScanWindows[wi];
            if (win.LowerLimit is double lo && win.UpperLimit is double hi)
            {
                var sw = new ScanWindow(lo, hi, CVID.MS_m_z);
                if (wi < windowParams.Count) MzPeakAuxArrays.ApplyMzPeakParams(sw, windowParams[wi]);
                scan.ScanWindows.Add(sw);
            }
        }

        ApplyParams(scan, src.Parameters);

        spectrum.ScanList.Scans.Add(scan);

        // Append scanList scans beyond the first (combined ion-mobility spectra: one per mobility bin).
        foreach (var extra in ExtraScans.Parse(src.ExtraScansJson))
        {
            var es = new Scan();
            if (!string.IsNullOrEmpty(extra.SpectrumId)) es.SpectrumId = extra.SpectrumId!;
            MzPeakAuxArrays.ApplyMzPeakParams(es, extra.Params);
            foreach (var win in extra.ScanWindows)
            {
                var sw = new ScanWindow();
                MzPeakAuxArrays.ApplyMzPeakParams(sw, win);
                es.ScanWindows.Add(sw);
            }
            spectrum.ScanList.Scans.Add(es);
        }
    }

    /// <summary>
    /// Restore the scanList combination method (MS:1000570 children). pwiz/mzML always carries
    /// one on the scanList; we emit whatever the writer captured.
    /// </summary>
    private static void ApplyScanCombination(MzPeakReader.SpectrumDescription desc, Spectrum spectrum)
    {
        var cvid = CvidFromCurie(desc.ScanCombinationCurie);
        if (cvid != CVID.CVID_Unknown)
            spectrum.ScanList.Params.Set(cvid);
    }

    /// <summary>
    /// Re-attach the referenceableParamGroups this spectrum referenced. Any CV term a referenced
    /// group already provides is dropped from the spectrum's direct params so it isn't duplicated
    /// (the writer inlines polarity / representation from typed columns even when the group carried
    /// them).
    /// </summary>
    private void ApplyParamGroupRefs(MzPeakReader.SpectrumDescription desc, Spectrum spectrum)
    {
        if (desc.ParamGroupRefs is null) return;
        foreach (var refId in desc.ParamGroupRefs)
        {
            if (!_paramGroupsById.TryGetValue(refId, out var pg)) continue;
            spectrum.Params.ParamGroups.Add(pg);
            if (_paramGroupCvids.TryGetValue(refId, out var cvids))
                spectrum.Params.CVParams.RemoveAll(cv => cvids.Contains(cv.Cvid));
        }
    }

    private static void TranslatePrecursors(MzPeakReader.SpectrumDescription desc, Spectrum spectrum)
    {
        foreach (var p in desc.Precursors)
        {
            var precursor = new Precursor();
            if (!string.IsNullOrEmpty(p.PrecursorId))
                precursor.SpectrumId = p.PrecursorId!;

            if (p.IsolationWindow is { } iso)
            {
                if (iso.TargetMz is double t) precursor.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, t, CVID.MS_m_z);
                if (iso.LowerOffset is double lo) precursor.IsolationWindow.Set(CVID.MS_isolation_window_lower_offset, lo, CVID.MS_m_z);
                if (iso.UpperOffset is double hi) precursor.IsolationWindow.Set(CVID.MS_isolation_window_upper_offset, hi, CVID.MS_m_z);
                ApplyParams(precursor.IsolationWindow, iso.Parameters);
            }

            if (p.Activation is { } act)
            {
                if (act.CollisionEnergy is double ce)
                    precursor.Activation.Set(CVID.MS_collision_energy, ce, CVID.UO_electronvolt);
                var dissCvid = CvidFromCurie(act.DissociationMethod);
                if (dissCvid != CVID.CVID_Unknown)
                    precursor.Activation.Set(dissCvid);
                ApplyParams(precursor.Activation, act.Parameters);
            }

            if (p.SelectedIon is { } si)
            {
                var selectedIon = new SelectedIon();
                if (si.Mz is double mz)
                    selectedIon.Set(CVID.MS_selected_ion_m_z, mz, CVID.MS_m_z);
                if (si.PeakIntensity is double pi)
                    // Selected-ion peak intensity is emitted unitless in mzML; don't synthesize a unit.
                    selectedIon.Set(CVID.MS_peak_intensity, pi);
                if (si.ChargeState is long cs)
                    selectedIon.Set(CVID.MS_charge_state, cs);
                ApplyParams(selectedIon, si.Parameters);
                precursor.SelectedIons.Add(selectedIon);
            }

            spectrum.Precursors.Add(precursor);
        }
    }

    /// <summary>
    /// Apply a list of free-form <see cref="MzPeakReader.CvParam"/>s onto a
    /// pwiz <see cref="ParamContainer"/>. CV-tagged params (accession matches a
    /// known CVID) become CVParams; anything else becomes a UserParam so it
    /// round-trips even if the term is unknown to this build.
    /// </summary>
    private static void ApplyParams(ParamContainer target, IReadOnlyList<MzPeakReader.CvParam> src)
    {
        foreach (var p in src)
        {
            var cvid = CvidFromCurie(p.Accession);
            var unitCvid = CVID.CVID_Unknown;
            if (!string.IsNullOrEmpty(p.Unit)) unitCvid = CvidFromCurie(p.Unit);
            string value = ScalarToString(p);

            if (cvid != CVID.CVID_Unknown)
            {
                target.Set(cvid, value, unitCvid);
            }
            else
            {
                target.UserParams.Add(new Pwiz.Data.Common.Params.UserParam(
                    p.Name ?? string.Empty, value, type: p.Type ?? string.Empty, units: unitCvid));
            }
        }
    }

    private static string ScalarToString(MzPeakReader.CvParam p)
    {
        if (p.ValueString is not null) return p.ValueString;
        if (p.ValueInteger is long li) return li.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (p.ValueFloat is double d) return d.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        if (p.ValueBoolean is bool b) return b ? "true" : "false";
        return string.Empty;
    }

    private static CVID CvidFromCurie(string? curie)
    {
        if (string.IsNullOrEmpty(curie)) return CVID.CVID_Unknown;
        return CvLookup.CvTermInfo(curie).Cvid;
    }
}
