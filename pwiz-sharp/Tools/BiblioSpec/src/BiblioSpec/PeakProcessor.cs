// Port of pwiz_tools/BiblioSpec/src/PeakProcess.{h,cpp}

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// Pre-processes a spectrum's peak list before scoring: bins peaks, optionally removes peaks
/// around the precursor m/z, takes the top-N most intense peaks, and normalises intensities
/// as <c>sqrt(intensity) * mz^2</c>.
/// </summary>
/// <remarks>
/// <para>Port of <c>BiblioSpec::PeakProcessor</c>. The cpp class takes a
/// <c>boost::program_options::variables_map</c>; we instead expose the same five knobs as
/// settable properties so each tool can wire its own CLI to them.</para>
/// <para>cpp pipeline (PeakProcess.cpp:69):</para>
/// <list type="number">
///   <item>Bin peaks (sum intensities into bins).</item>
///   <item>If <see cref="ClearPrecursor"/>, drop peaks within [-20, +5] m/z of the precursor
///         (PeakProcess.cpp:138).</item>
///   <item>If <see cref="NoiseFirst"/> (the cpp default): top-N peaks → norm-mz. Otherwise:
///         norm-mz → top-N.</item>
///   <item>Result lands in <see cref="Spectrum.ProcessedPeaks"/>.</item>
/// </list>
/// </remarks>
public sealed class PeakProcessor
{
    /// <summary>cpp <c>isClearPrecursor_</c>. Default true.</summary>
    public bool ClearPrecursor { get; set; } = true;

    /// <summary>cpp <c>noiseFirst_</c>. Default true (remove noise before normalising).</summary>
    public bool NoiseFirst { get; set; } = true;

    /// <summary>cpp <c>numTopPeaks_</c>. Default 100.</summary>
    public int NumTopPeaks { get; set; } = 100;

    /// <summary>cpp <c>binSize_</c>. Default 1.0.</summary>
    public double BinSize { get; set; } = 1.0;

    /// <summary>cpp <c>binOffset_</c>. Default 0.0.</summary>
    public double BinOffset { get; set; }

    /// <summary>
    /// Run the cpp <see cref="ProcessPeaks(Spectrum)"/> pipeline on <paramref name="spec"/>:
    /// bin, optional precursor-clear, top-N + normMz.
    /// </summary>
    /// <remarks>cpp PeakProcess.cpp:69 <c>processPeaks(Spectrum*)</c>.</remarks>
    public void ProcessPeaks(Spectrum spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        // Bin raw peaks. cpp parity: PeakProcess.cpp:72-75.
        var raw = new List<PeakT>(spec.RawPeaks);
        var binned = new List<PeakT>();
        var totalIntensity = BinPeaks(raw, binned);
        spec.TotalIonCurrentRaw = totalIntensity;

        // cpp PeakProcess.cpp:77-79.
        if (ClearPrecursor)
            RemovePrecursorPeaks(binned, spec.Mz);

        var signal = new List<PeakT>();
        var normalized = new List<PeakT>();

        // cpp PeakProcess.cpp:85-104.
        if (NoiseFirst)
        {
            TopNPeaks(binned, signal, NumTopPeaks);
            NormMz(signal, normalized);
            spec.SetProcessedPeaks(normalized);
        }
        else
        {
            NormMz(binned, normalized);
            TopNPeaks(normalized, signal, NumTopPeaks);
            spec.SetProcessedPeaks(signal);
        }
    }

    /// <summary>
    /// cpp <c>removePrecursorPeaks</c> at PeakProcess.cpp:138 — drop peaks in
    /// [precursorMz - 20, precursorMz + 5].
    /// </summary>
    /// <remarks>cpp quirk: the <c>first</c> / <c>last</c> iterators are expanded by one before
    /// erase (cpp:160-161), so a peak just below / just above the window is also dropped. Preserved.</remarks>
    public static void RemovePrecursorPeaks(List<PeakT> peaks, double mz)
    {
        ArgumentNullException.ThrowIfNull(peaks);

        const double minDelta = -20.0;
        const double maxDelta = +5.0;
        var loMz = mz + minDelta;
        var hiMz = mz + maxDelta;

        // cpp uses lower_bound + upper_bound vs (mz, 0) comparing by mz then intensity.
        // We do the same via simple linear scan since peak lists are small.
        // first = lower_bound by mz; last = upper_bound by mz.
        // cpp parity: PeakProcess.cpp:153-162 — both iterators expanded outward by one.
        var firstIdx = LowerBoundByMz(peaks, loMz);
        var lastIdx = UpperBoundByMz(peaks, hiMz);

        if (firstIdx > 0) firstIdx--;
        if (lastIdx < peaks.Count) lastIdx++;

        if (firstIdx >= lastIdx) return;
        peaks.RemoveRange(firstIdx, lastIdx - firstIdx);
    }

    private static int LowerBoundByMz(List<PeakT> peaks, double mz)
    {
        // First index i such that peaks[i].Mz >= mz (assuming the list is mz-sorted).
        int lo = 0, hi = peaks.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) >>> 1;
            if (peaks[mid].Mz < mz) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }

    private static int UpperBoundByMz(List<PeakT> peaks, double mz)
    {
        // First index i such that peaks[i].Mz > mz.
        int lo = 0, hi = peaks.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) >>> 1;
            if (peaks[mid].Mz <= mz) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }

    /// <summary>
    /// cpp <c>binPeaks</c> at PeakProcess.cpp:179 — sum intensities into bins, replacing each
    /// peak's mz with the bin index. Returns the sum of all raw intensities.
    /// </summary>
    /// <remarks>cpp quirk: PeakProcess.cpp:202 computes total intensity by adding the RAW
    /// peak's intensity (not the bin-summed value), so multiple peaks in one bin contribute
    /// their summed intensity once via the bin AND each separately to <c>totalIntensity</c>.
    /// Wait — re-reading: each input peak adds <c>peaks.at(i).intensity</c> exactly once to
    /// the running total. So the total IS the sum of raw intensities. Preserved.</remarks>
    public double BinPeaks(List<PeakT> peaks, List<PeakT> results)
    {
        ArgumentNullException.ThrowIfNull(peaks);
        ArgumentNullException.ThrowIfNull(results);
        results.Clear();

        if (peaks.Count == 0) return 0;

        double totalIntensity = 0;

        // cpp PeakProcess.cpp:192-198.
        var tmp = new PeakT
        {
            Mz = GetBin(peaks[0].Mz),
            Intensity = peaks[0].Intensity,
        };
        results.Add(tmp);
        totalIntensity = tmp.Intensity;

        for (var i = 1; i < peaks.Count; i++)
        {
            tmp.Mz = GetBin(peaks[i].Mz);
            tmp.Intensity = peaks[i].Intensity;
            totalIntensity += peaks[i].Intensity;

            if (tmp.Mz == results[^1].Mz)
            {
                var back = results[^1];
                back.Intensity += peaks[i].Intensity;
                results[^1] = back;
            }
            else
            {
                results.Add(tmp);
            }
        }

        return totalIntensity;
    }

    /// <summary>
    /// cpp <c>getBin</c> at PeakProcess.cpp:218 — <c>(int)((mz - binOffset) / binSize)</c>.
    /// </summary>
    public double GetBin(double mz)
    {
        if (BinSize == 0) return mz;
        return (int)((mz - BinOffset) / BinSize);
    }

    /// <summary>
    /// cpp <c>normMz</c> at PeakProcess.cpp:235 — replaces each peak's intensity with
    /// <c>sqrt(intensity) * mz^2</c> and returns the sum of the new intensities.
    /// </summary>
    /// <remarks>cpp quirk: PeakProcess.cpp:243 mutates the input peaks in place
    /// (<c>peaks.at(i).intensity = ...</c>). We mirror that.</remarks>
    public static double NormMz(List<PeakT> peaks, List<PeakT> results)
    {
        ArgumentNullException.ThrowIfNull(peaks);
        ArgumentNullException.ThrowIfNull(results);
        results.Clear();
        double total = 0;
        for (var i = 0; i < peaks.Count; i++)
        {
            var p = peaks[i];
            var newInt = Math.Sqrt(p.Intensity) * p.Mz * p.Mz;
            p.Intensity = (float)newInt;
            peaks[i] = p;
            results.Add(p);
            total += newInt;
        }
        return total;
    }

    /// <summary>
    /// cpp <c>topNpeaks</c> at PeakProcess.cpp:254 — sort by intensity desc, take first N,
    /// re-sort BOTH input and results by mz.
    /// </summary>
    /// <remarks>cpp quirk: PeakProcess.cpp:266-267 re-sorts the input vector too, not just
    /// results. We do the same — the caller relies on it.</remarks>
    public static void TopNPeaks(List<PeakT> peaks, List<PeakT> results, int n)
    {
        ArgumentNullException.ThrowIfNull(peaks);
        ArgumentNullException.ThrowIfNull(results);
        results.Clear();

        peaks.Sort(CompareByIntensityDesc);
        var take = Math.Min(n, peaks.Count);
        for (var i = 0; i < take; i++)
            results.Add(peaks[i]);

        peaks.Sort(CompareByMzAsc);
        results.Sort(CompareByMzAsc);
    }

    // cpp comparators at PeakProcess.cpp:325 / :336.
    private static int CompareByMzAsc(PeakT a, PeakT b)
    {
        if (a.Mz < b.Mz) return -1;
        if (a.Mz > b.Mz) return 1;
        if (a.Intensity < b.Intensity) return -1;
        if (a.Intensity > b.Intensity) return 1;
        return 0;
    }

    private static int CompareByIntensityDesc(PeakT a, PeakT b)
    {
        if (a.Intensity > b.Intensity) return -1;
        if (a.Intensity < b.Intensity) return 1;
        if (a.Mz > b.Mz) return -1;
        if (a.Mz < b.Mz) return 1;
        return 0;
    }
}
