using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Analysis;

/// <summary>
/// Sums adjacent MS2 sub-scans whose precursor m/z, scan time, and (when present) ion mobility
/// are within tolerance. Port of pwiz cpp's <c>SpectrumList_ScanSummer</c>
/// (<c>pwiz/analysis/spectrum_processing/SpectrumList_ScanSummer.cpp</c>).
/// </summary>
/// <remarks>
/// Pass intended for Waters DDA and Bruker PASEF where the same precursor is scanned several
/// times in close succession. The wrapper builds its grouping eagerly in the constructor so
/// <see cref="Count"/> is stable; <see cref="GetSpectrum"/> on a grouped index loads every
/// member, naively merges m/z bins (deduping within 1e-2 Da and 1e-6 Da windows), and emits the
/// median scan time / precursor m/z / inverse reduced ion mobility.
/// </remarks>
public sealed class SpectrumListScanSummer : SpectrumListWrapper
{
    private readonly double _precursorTol;
    private readonly double _scanTimeTol;
    private readonly double _ionMobilityTol;
    private readonly bool _sumMs1;

    private readonly List<SpectrumIdentity> _identities = new();
    /// <summary>Group entry for each output index. Always non-null — MS1 / single-shot
    /// spectra get a group containing exactly one inner index.</summary>
    private readonly List<PrecursorGroup> _groups = new();
    private readonly DataProcessing _dp;

    /// <inheritdoc/>
    public override DataProcessing? DataProcessing => _dp;

    /// <summary>Constructs the summer eagerly.</summary>
    /// <param name="inner">Source spectrum list.</param>
    /// <param name="precursorTol">m/z window for grouping precursors (cpp default 0.05).</param>
    /// <param name="scanTimeTol">Scan-time window in seconds (cpp default 10).</param>
    /// <param name="ionMobilityTol">Ion-mobility window (cpp default 0.01); 0 disables IM-based grouping.</param>
    /// <param name="sumMs1">Sum MS1 spectra too (default false — pass-through for MS1).</param>
    public SpectrumListScanSummer(ISpectrumList inner, double precursorTol = 0.05,
        double scanTimeTol = 10, double ionMobilityTol = 0.01, bool sumMs1 = false) : base(inner)
    {
        _precursorTol = precursorTol;
        _scanTimeTol = scanTimeTol;
        _ionMobilityTol = ionMobilityTol;
        _sumMs1 = sumMs1;

        // Index pass: load every spectrum's metadata-only view, classify into groups.
        // precursorList is sorted-by-precursor-m/z so we can binary-search a candidate range.
        var precursorList = new SortedDictionary<double, List<PrecursorGroup>>();
        for (int i = 0; i < inner.Count; i++)
        {
            var spec = inner.GetSpectrum(i, getBinaryData: false);
            double precursorMz = GetPrecursorMz(spec);
            var identity = inner.SpectrumIdentity(i);

            if (precursorMz == 0.0)
            {
                // MS1 — emitted as its own output entry, never grouped with another scan.
                // Wrapped in a 1-member group so the GetSpectrum path is uniform; sumMs1=true
                // then reaches the sort+bin code with a single member (no cross-scan sum).
                var single = new PrecursorGroup();
                single.PrecursorMzs.Add(0);
                single.ScanTimes.Add(0);
                single.IonMobilities.Add(0);
                single.InnerIndices.Add(i);
                AppendOutputEntry(identity, single);
                continue;
            }

            double scanTime = spec.ScanList.Scans.Count > 0
                ? spec.ScanList.Scans[0].Params.CvParamValueOrDefault(CVID.MS_scan_start_time, 0.0)
                : 0.0;
            double ionMobility = spec.ScanList.Scans.Count > 0
                ? spec.ScanList.Scans[0].Params.CvParamValueOrDefault(CVID.MS_inverse_reduced_ion_mobility, 0.0)
                : 0.0;

            // Search candidate groups whose precursor m/z is within ±precursorTol.
            var candidates = new List<PrecursorGroup>();
            foreach (var (key, list) in precursorList)
            {
                if (key < precursorMz - _precursorTol) continue;
                if (key > precursorMz + _precursorTol) break;
                candidates.AddRange(list);
            }

            PrecursorGroup? matched = null;
            foreach (var candidate in candidates)
            {
                double timeDiff = System.Math.Abs(scanTime - candidate.ScanTimes[0]);
                double imDiff = System.Math.Abs(ionMobility - candidate.IonMobilities[0]);
                if ((_scanTimeTol == 0 || timeDiff < _scanTimeTol)
                    && (_ionMobilityTol == 0 || imDiff < _ionMobilityTol))
                {
                    matched = candidate;
                    break;
                }
            }

            if (matched is not null)
            {
                matched.PrecursorMzs.Add(precursorMz);
                matched.ScanTimes.Add(scanTime);
                matched.IonMobilities.Add(ionMobility);
                matched.InnerIndices.Add(i);
            }
            else
            {
                var group = new PrecursorGroup();
                group.PrecursorMzs.Add(precursorMz);
                group.ScanTimes.Add(scanTime);
                group.IonMobilities.Add(ionMobility);
                group.InnerIndices.Add(i);

                AppendOutputEntry(identity, group);

                if (!precursorList.TryGetValue(precursorMz, out var bucket))
                    precursorList[precursorMz] = bucket = new List<PrecursorGroup>();
                bucket.Add(group);
            }
        }

        // Build a fresh DataProcessing chain that documents the summing step.
        var innerDp = inner.DataProcessing;
        _dp = new DataProcessing(innerDp?.Id ?? "pwiz_Reader_conversion");
        if (innerDp is not null)
            foreach (var pm in innerDp.ProcessingMethods)
                _dp.ProcessingMethods.Add(pm);
        var method = new ProcessingMethod
        {
            Order = _dp.ProcessingMethods.Count,
            Software = _dp.ProcessingMethods.FirstOrDefault()?.Software,
        };
        method.UserParams.Add(new UserParam("summing of spectra from the same precursor adjacent in time and/or mobility space"));
        method.UserParams.Add(new UserParam("m/z tolerance", _precursorTol.ToString("R", System.Globalization.CultureInfo.InvariantCulture), "xsd:double"));
        method.UserParams.Add(new UserParam("scan time tolerance", _scanTimeTol.ToString("R", System.Globalization.CultureInfo.InvariantCulture), "xsd:double"));
        method.UserParams.Add(new UserParam("ion mobility tolerance", _ionMobilityTol.ToString("R", System.Globalization.CultureInfo.InvariantCulture), "xsd:double"));
        method.UserParams.Add(new UserParam("sumMS1", _sumMs1 ? "true" : "false", "xsd:boolean"));
        _dp.ProcessingMethods.Add(method);
    }

    private void AppendOutputEntry(SpectrumIdentity inner, PrecursorGroup group)
    {
        // Output identity must reference the new index, not the inner one.
        var rewritten = new SpectrumIdentity
        {
            Index = _identities.Count,
            Id = inner.Id,
            SpotId = inner.SpotId,
            SourceFilePosition = inner.SourceFilePosition,
        };
        _identities.Add(rewritten);
        _groups.Add(group);
    }

    /// <inheritdoc/>
    public override int Count => _identities.Count;

    /// <inheritdoc/>
    public override SpectrumIdentity SpectrumIdentity(int index) => _identities[index];

    /// <inheritdoc/>
    public override Spectrum GetSpectrum(int index, bool getBinaryData = false)
    {
        var group = _groups[index];
        var summed = Inner.GetSpectrum(group.InnerIndices[0], getBinaryData: true);
        int msLevel = summed.Params.CvParamValueOrDefault(CVID.MS_ms_level, 1);

        if (msLevel > 1 || _sumMs1)
        {
            if (msLevel > 1)
            {
                // Patch the spectrum's CV params to the group medians (cpp:387-394).
                // For a 1-member group the medians equal the single value (no-op, but harmless).
                if (summed.ScanList.Scans.Count > 0)
                {
                    var scan = summed.ScanList.Scans[0];
                    ReplaceCvValue(scan.Params, CVID.MS_scan_start_time, Median(group.ScanTimes));
                    ReplaceCvValue(scan.Params, CVID.MS_inverse_reduced_ion_mobility, Median(group.IonMobilities));
                    // Drop other scan entries — only the first survives.
                    if (summed.ScanList.Scans.Count > 1)
                        summed.ScanList.Scans.RemoveRange(1, summed.ScanList.Scans.Count - 1);
                }
                if (summed.Precursors.Count > 0 && summed.Precursors[0].SelectedIons.Count > 0)
                    ReplaceCvValue(summed.Precursors[0].SelectedIons[0].Params, CVID.MS_selected_ion_m_z,
                        Median(group.PrecursorMzs));
            }

            // Drop auxiliary parallel arrays whose length matches the m/z array — summing
            // mutates the m/z array's length and the per-peak correspondence is lost.
            // Mirrors cpp's SpectrumList_ScanSummer.cpp:404-406 pre-sum cleanup.
            int mzLen = summed.GetMZArray()?.Data.Count ?? 0;
            for (int i = summed.BinaryDataArrays.Count - 1; i >= 2; i--)
            {
                if (summed.BinaryDataArrays[i].Data.Count == mzLen)
                    summed.BinaryDataArrays.RemoveAt(i);
            }

            // Sort + bin within 1e-6 Da, then accumulate cross-scan peaks (no-op for single-member group).
            SumGroupArrays(summed, group);
        }

        summed.Index = index;
        summed.DataProcessing = _dp;
        return summed;
    }

    private void SumGroupArrays(Spectrum summed, PrecursorGroup group)
    {
        var mzArr = summed.GetMZArray();
        var intArr = summed.GetIntensityArray();
        if (mzArr is null || intArr is null) return;

        var mz = new List<double>(mzArr.Data);
        var inten = new List<double>(intArr.Data);

        // Sort the seed arrays together by m/z then dedupe peaks within 1e-6 Da (matches cpp).
        SortPair(mz, inten);
        if (mz.Count > 1)
        {
            var binnedMz = new List<double>(mz.Count) { mz[0] };
            var binnedInt = new List<double>(mz.Count) { inten[0] };
            for (int i = 1; i < mz.Count; i++)
            {
                if (System.Math.Abs(binnedMz[^1] - mz[i]) < 1e-6)
                    binnedInt[^1] += inten[i];
                else
                {
                    binnedMz.Add(mz[i]);
                    binnedInt.Add(inten[i]);
                }
            }
            mz = binnedMz;
            inten = binnedInt;
        }

        // Accumulate the rest of the group (skip the first member — already loaded above).
        for (int gi = 1; gi < group.InnerIndices.Count; gi++)
        {
            var member = Inner.GetSpectrum(group.InnerIndices[gi], getBinaryData: true);
            var memMz = member.GetMZArray();
            var memInt = member.GetIntensityArray();
            if (memMz is null || memInt is null) continue;

            int n = System.Math.Min(memMz.Data.Count, memInt.Data.Count);
            for (int j = 0; j < n; j++)
            {
                double targetMz = memMz.Data[j];
                int idx = LowerBound(mz, targetMz - 1e-2);
                if (idx == mz.Count)
                {
                    mz.Add(targetMz);
                    inten.Add(memInt.Data[j]);
                }
                else if (System.Math.Abs(mz[idx] - targetMz) > 1e-2)
                {
                    mz.Insert(idx, targetMz);
                    inten.Insert(idx, memInt.Data[j]);
                }
                else
                {
                    inten[idx] += memInt.Data[j];
                }
            }
        }

        CVID intensityUnits = CVID.MS_number_of_detector_counts;
        foreach (var p in intArr.CVParams)
            if (p.Units != CVID.CVID_Unknown) { intensityUnits = p.Units; break; }

        summed.SetMZIntensityArrays(mz, inten, intensityUnits);
    }

    private static void SortPair(List<double> mz, List<double> inten)
    {
        int n = mz.Count;
        var order = new int[n];
        for (int i = 0; i < n; i++) order[i] = i;
        Array.Sort(order, (a, b) => mz[a].CompareTo(mz[b]));
        var sortedMz = new double[n];
        var sortedIn = new double[n];
        for (int i = 0; i < n; i++) { sortedMz[i] = mz[order[i]]; sortedIn[i] = inten[order[i]]; }
        for (int i = 0; i < n; i++) { mz[i] = sortedMz[i]; inten[i] = sortedIn[i]; }
    }

    private static int LowerBound(List<double> sorted, double target)
    {
        int lo = 0, hi = sorted.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) >>> 1;
            if (sorted[mid] < target) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }

    private static void ReplaceCvValue(ParamContainer container, CVID cvid, double value)
    {
        for (int i = 0; i < container.CVParams.Count; i++)
        {
            if (container.CVParams[i].Cvid == cvid)
            {
                var existing = container.CVParams[i];
                container.CVParams[i] = new CVParam(existing.Cvid,
                    value.ToString("R", System.Globalization.CultureInfo.InvariantCulture), existing.Units);
                return;
            }
        }
    }

    private static double Median(List<double> values)
    {
        if (values.Count == 0) return 0;
        var copy = new List<double>(values);
        copy.Sort();
        int mid = copy.Count / 2;
        return copy.Count % 2 == 0 ? (copy[mid] + copy[mid - 1]) / 2 : copy[mid];
    }

    private static double GetPrecursorMz(Spectrum spectrum)
    {
        foreach (var precursor in spectrum.Precursors)
            foreach (var ion in precursor.SelectedIons)
            {
                var p = ion.CvParam(CVID.MS_selected_ion_m_z);
                if (p.Cvid != CVID.CVID_Unknown && double.TryParse(p.Value,
                        System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v))
                    return v;
            }
        return 0;
    }

    private sealed class PrecursorGroup
    {
        public List<double> PrecursorMzs { get; } = new();
        public List<double> ScanTimes { get; } = new();
        public List<double> IonMobilities { get; } = new();
        public List<int> InnerIndices { get; } = new();
    }
}
