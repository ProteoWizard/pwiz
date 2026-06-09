using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Analysis.Demux;

/// <summary>
/// Hashes a floating-point m/z value to a 64-bit integer with ±5e-9 effective precision.
/// Port of cpp's <c>IsoWindowHasher</c> (<c>IPrecursorMaskCodec.hpp</c>).
/// </summary>
public static class IsoWindowHasher
{
    private const double Multiplier = 1e8;

    /// <summary>Hashes <paramref name="mz"/> as <c>round(mz * 1e8)</c>. m/z 500.49 → 50049000000.</summary>
    public static long Hash(double mz) => (long)System.Math.Round(mz * Multiplier);

    /// <summary>Inverse of <see cref="Hash"/>.</summary>
    public static double UnHash(long hashed) => hashed / Multiplier;
}

/// <summary>
/// Slim m/z-range container used as a key in demux algorithms. Port of cpp's <c>DemuxWindow</c>.
/// Boundaries are stored as <see cref="IsoWindowHasher"/> hashes so equality and ordering are
/// fuzzy-tolerant (±5e-9 m/z).
/// </summary>
public readonly record struct DemuxWindow(long MzLow, long MzHigh) : IComparable<DemuxWindow>
{
    /// <summary>Constructs a <see cref="DemuxWindow"/> from a precursor's isolation window.</summary>
    public DemuxWindow(Precursor p)
        : this(IsoWindowHasher.Hash(DemuxHelpers.PrecursorTarget(p) - DemuxHelpers.PrecursorLowerOffset(p)),
               IsoWindowHasher.Hash(DemuxHelpers.PrecursorTarget(p) + DemuxHelpers.PrecursorUpperOffset(p))) { }

    /// <summary>True iff <paramref name="inner"/>'s m/z range is fully contained in this one.</summary>
    public bool Contains(DemuxWindow inner) => inner.MzLow >= MzLow && inner.MzHigh <= MzHigh;

    /// <summary>True iff <paramref name="inner"/>'s center m/z lies within this window.</summary>
    public bool ContainsCenter(DemuxWindow inner)
    {
        long center = (long)System.Math.Round(inner.MzLow + (inner.MzHigh - inner.MzLow) / 2.0);
        return center >= MzLow && center <= MzHigh;
    }

    /// <inheritdoc/>
    public int CompareTo(DemuxWindow other) => MzLow.CompareTo(other.MzLow);

#pragma warning disable CS1591 // comparison operators delegate to CompareTo; docs would just restate the operator
    public static bool operator <(DemuxWindow a, DemuxWindow b) => a.CompareTo(b) < 0;
    public static bool operator >(DemuxWindow a, DemuxWindow b) => a.CompareTo(b) > 0;
    public static bool operator <=(DemuxWindow a, DemuxWindow b) => a.CompareTo(b) <= 0;
    public static bool operator >=(DemuxWindow a, DemuxWindow b) => a.CompareTo(b) >= 0;
#pragma warning restore CS1591
}

/// <summary>
/// Full-precision wrapper around a <see cref="DemuxWindow"/> that retains the original
/// floating-point m/z bounds. Port of cpp's <c>IsolationWindow</c> in
/// <c>IPrecursorMaskCodec.hpp</c>. Named with the <c>Demux</c> prefix to disambiguate from
/// <c>Pwiz.Data.MsData.Spectra.IsolationWindow</c>.
/// </summary>
public readonly record struct DemuxIsolationWindow(double LowMz, double HighMz, DemuxWindow Window)
    : IComparable<DemuxIsolationWindow>
{
    /// <summary>Constructs from raw m/z bounds.</summary>
    public DemuxIsolationWindow(double lowMz, double highMz)
        : this(lowMz, highMz, new DemuxWindow(IsoWindowHasher.Hash(lowMz), IsoWindowHasher.Hash(highMz))) { }

    /// <summary>Constructs from a precursor's isolation window CV params.</summary>
    public static DemuxIsolationWindow FromPrecursor(Precursor p) =>
        new(DemuxHelpers.PrecursorMzLow(p), DemuxHelpers.PrecursorMzHigh(p), new DemuxWindow(p));

    /// <inheritdoc/>
    public int CompareTo(DemuxIsolationWindow other) => Window.CompareTo(other.Window);

#pragma warning disable CS1591 // comparison operators delegate to CompareTo; docs would just restate the operator
    public static bool operator <(DemuxIsolationWindow a, DemuxIsolationWindow b) => a.CompareTo(b) < 0;
    public static bool operator >(DemuxIsolationWindow a, DemuxIsolationWindow b) => a.CompareTo(b) > 0;
    public static bool operator <=(DemuxIsolationWindow a, DemuxIsolationWindow b) => a.CompareTo(b) <= 0;
    public static bool operator >=(DemuxIsolationWindow a, DemuxIsolationWindow b) => a.CompareTo(b) >= 0;
#pragma warning restore CS1591
}

/// <summary>
/// Generates and accesses precursor masks for a demultiplexing scheme. Port of cpp's
/// <c>IPrecursorMaskCodec</c>.
/// </summary>
public interface IPrecursorMaskCodec
{
    /// <summary>Returns a design-matrix row describing which precursor isolation windows are
    /// present in <paramref name="spectrum"/>, scaled by <paramref name="weight"/>.</summary>
    Vector<double> GetMask(Spectrum spectrum, double weight = 1.0);

    /// <summary>Same as <see cref="GetMask(Spectrum,double)"/> but writes into row
    /// <paramref name="rowNum"/> of the user-provided matrix (avoids a per-call allocation).</summary>
    void GetMask(Spectrum spectrum, Matrix<double> m, int rowNum, double weight = 1.0);

    /// <summary>Maps the precursor isolation windows of <paramref name="spectrum"/> to indices
    /// into the demux design matrix.</summary>
    void SpectrumToIndices(Spectrum spectrum, List<int> indices);

    /// <summary>The isolation window at design-matrix column <paramref name="i"/>.</summary>
    DemuxIsolationWindow GetIsolationWindow(int i);

    /// <summary>Total number of demux'd precursor windows (= number of design-matrix columns).</summary>
    int NumDemuxWindows { get; }

    /// <summary>Indices of edge windows removed during overlap analysis (<see cref="PrecursorMaskCodec.Params.RemoveNonOverlappingEdges"/>).</summary>
    IReadOnlyList<int> DemuxWindowEdgesRemoved { get; }

    /// <summary>Number of spectra required to cover all precursor isolation windows.</summary>
    int SpectraPerCycle { get; }

    /// <summary>Number of precursors per multiplexed spectrum (constant across the run).</summary>
    int PrecursorsPerSpectrum { get; }

    /// <summary>Number of overlap repeats per cycle (1 for non-overlapped, 2 for split-by-half, etc.).</summary>
    int OverlapsPerCycle { get; }

    /// <summary>Total number of windows the design matrix needs to cover. Equals
    /// <see cref="SpectraPerCycle"/> × <see cref="PrecursorsPerSpectrum"/> × <see cref="OverlapsPerCycle"/>.</summary>
    int DemuxBlockSize { get; }
}

/// <summary>
/// Implementation of <see cref="IPrecursorMaskCodec"/> that handles both overlapping DIA and
/// MSX experiments. Port of cpp's <c>PrecursorMaskCodec</c>.
/// </summary>
public sealed class PrecursorMaskCodec : IPrecursorMaskCodec
{
    /// <summary>Tunable parameters for cycle/overlap inference.</summary>
    public sealed class Params
    {
        /// <summary>Whether the data was acquired with variable fill times (the per-precursor
        /// mask is weighted by <c>MultiFillTime / 1000</c> when true).</summary>
        public bool VariableFill { get; init; }

        /// <summary>Tolerance (in m/z) used to decide whether two window boundaries are "the same".</summary>
        public double MinimumWindowSize { get; init; } = 0.2;

        /// <summary>Drop edge isolation segments not covered at the same multiplicity as the bulk
        /// of the cycle.</summary>
        public bool RemoveNonOverlappingEdges { get; init; }
    }

    private readonly Params _params;
    private readonly List<DemuxIsolationWindow> _isolationWindows = new();
    private readonly List<int> _edgeWindowsToRemove = new();
    private int _spectraPerCycle;
    private int _precursorsPerSpectrum;
    private int _overlapsPerSpectrum;

    /// <inheritdoc/>
    public int NumDemuxWindows => _isolationWindows.Count;

    /// <inheritdoc/>
    public IReadOnlyList<int> DemuxWindowEdgesRemoved => _edgeWindowsToRemove;

    /// <inheritdoc/>
    public int SpectraPerCycle => _spectraPerCycle;

    /// <inheritdoc/>
    public int PrecursorsPerSpectrum => _precursorsPerSpectrum;

    /// <inheritdoc/>
    public int OverlapsPerCycle => _overlapsPerSpectrum;

    /// <inheritdoc/>
    public int DemuxBlockSize => _spectraPerCycle * _precursorsPerSpectrum * _overlapsPerSpectrum;

    /// <summary>Constructs a codec by inferring the demux scheme from <paramref name="spectrumList"/>.</summary>
    public PrecursorMaskCodec(ISpectrumList spectrumList, Params? p = null)
    {
        ArgumentNullException.ThrowIfNull(spectrumList);
        _params = p ?? new Params();
        ReadDemuxScheme(spectrumList);
    }

    /// <inheritdoc/>
    public Vector<double> GetMask(Spectrum spectrum, double weight = 1.0)
    {
        var mask = DenseVector.Create(DemuxBlockSize, 0.0);
        FillMask((i, v) => mask[i] = v, spectrum, weight);
        return mask;
    }

    /// <inheritdoc/>
    public void GetMask(Spectrum spectrum, Matrix<double> m, int rowNum, double weight = 1.0)
    {
        for (int j = 0; j < m.ColumnCount; j++) m[rowNum, j] = 0;
        FillMask((i, v) => m[rowNum, i] = v, spectrum, weight);
    }

    private void FillMask(Action<int, double> setter, Spectrum spectrum, double weight)
    {
        var indices = new List<int>();
        SpectrumToIndices(spectrum, indices);

        if (_params.VariableFill)
        {
            // Per-index weighting from the MultiFillTime user param on each precursor.
            var indexedDemuxWindows = new List<DemuxWindow>(indices.Count);
            foreach (var idx in indices)
                indexedDemuxWindows.Add(_isolationWindows[idx].Window);

            foreach (var p in spectrum.Precursors)
            {
                var precursorWindow = new DemuxWindow(p);
                for (int i = 0; i < indices.Count; i++)
                {
                    if (precursorWindow.ContainsCenter(indexedDemuxWindows[i]))
                    {
                        double fillTime = p.UserParam("MultiFillTime").ValueAs<double>();
                        setter(indices[i], weight * fillTime / 1000.0);
                        break;
                    }
                }
            }
        }
        else
        {
            foreach (var idx in indices) setter(idx, weight);
        }
    }

    /// <inheritdoc/>
    public void SpectrumToIndices(Spectrum spectrum, List<int> indices)
    {
        if (spectrum.Precursors.Count != _precursorsPerSpectrum)
            throw new InvalidOperationException(
                "SpectrumToIndices() Number of precursors in this spectrum differs from the demultiplexing scheme.");

        indices.Clear();
        var overlappingWindows = new List<DemuxWindow>(spectrum.Precursors.Count);
        foreach (var precursor in spectrum.Precursors)
            overlappingWindows.Add(new DemuxWindow(precursor));
        overlappingWindows.Sort();

        int searchLowerBound = 0;
        foreach (var window in overlappingWindows)
        {
            for (int searchIdx = searchLowerBound; searchIdx < _isolationWindows.Count; searchIdx++)
            {
                var candidate = _isolationWindows[searchIdx].Window;
                if (window.MzHigh <= candidate.MzLow) break;
                if (window.ContainsCenter(candidate))
                {
                    indices.Add(searchIdx);
                    searchLowerBound = searchIdx + 1;
                }
            }
        }

        if (indices.Count == 0)
            throw new InvalidOperationException("SpectrumToIndices() found no demux windows for this spectrum.");

        if (indices.Count != _overlapsPerSpectrum * _precursorsPerSpectrum)
            throw new InvalidOperationException(
                "SpectrumToIndices() Number of demultiplexing windows changed. Minimum window size or " +
                "boundary tolerance may be set too low.");
    }

    /// <inheritdoc/>
    public DemuxIsolationWindow GetIsolationWindow(int i) => _isolationWindows[i];

    private void ReadDemuxScheme(ISpectrumList spectrumList)
    {
        IdentifyCycle(spectrumList, _isolationWindows);
        IdentifyOverlap(_isolationWindows);
    }

    private void IdentifyCycle(ISpectrumList spectrumList, List<DemuxIsolationWindow> demuxWindows)
    {
        const string Ms2MissingPrecursorError = "IdentifyCycle() MS2 spectrum is missing precursor information.";

        // First MS2 spectrum sets the expected precursors-per-spectrum count.
        int index = 0;
        Spectrum? firstMs2 = null;
        for (; index < spectrumList.Count; index++)
        {
            var spec = spectrumList.GetSpectrum(index, getBinaryData: false);
            if (spec.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0) != 2) continue;
            if (spec.Precursors.Count == 0) throw new InvalidOperationException(Ms2MissingPrecursorError);
            _precursorsPerSpectrum = spec.Precursors.Count;
            firstMs2 = spec;
            break;
        }
        if (firstMs2 is null)
            throw new InvalidOperationException("IdentifyCycle() No MS2 scans found for this experiment.");

        // Walk forward until the cycle has been observed twice over (mappedAlready > 2 * |precursorMap|).
        // Map key is the rounded iso-center (cpp <c>prec_to_string</c>).
        var precursorMap = new Dictionary<string, Precursor>();
        int mappedAlready = 0;
        for (; index < spectrumList.Count && mappedAlready <= 2 * precursorMap.Count; index++)
        {
            var spec = spectrumList.GetSpectrum(index, getBinaryData: false);
            if (spec.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0) != 2) continue;
            if (spec.Precursors.Count == 0) throw new InvalidOperationException(Ms2MissingPrecursorError);
            if (spec.Precursors.Count != _precursorsPerSpectrum)
                throw new InvalidOperationException(
                    "IdentifyCycle() Number of precursors is varying between MS2 scans. Cannot infer demultiplexing scheme.");

            foreach (var p in spec.Precursors)
            {
                string key = DemuxHelpers.PrecToString(p);
                if (!precursorMap.ContainsKey(key))
                {
                    mappedAlready = 0;
                    precursorMap[key] = p;
                }
                else
                {
                    mappedAlready++;
                }
            }
        }
        if (mappedAlready <= 2 * precursorMap.Count)
            throw new InvalidOperationException(
                "IdentifyCycle() Could not determine demultiplexing scheme. Too few spectra to determine the number of precursor windows.");

        // Sort keys numerically (cpp uses stof comparator).
        var sortedKeys = new List<string>(precursorMap.Keys);
        sortedKeys.Sort((a, b) => double.Parse(a, System.Globalization.CultureInfo.InvariantCulture)
            .CompareTo(double.Parse(b, System.Globalization.CultureInfo.InvariantCulture)));

        demuxWindows.Clear();
        demuxWindows.Capacity = sortedKeys.Count;
        foreach (var key in sortedKeys)
            demuxWindows.Add(DemuxIsolationWindow.FromPrecursor(precursorMap[key]));

        if (_precursorsPerSpectrum == 0)
            throw new InvalidOperationException("IdentifyCycle() Number of precursors per spectrum is 0.");
        _spectraPerCycle = demuxWindows.Count / _precursorsPerSpectrum;
        if (_spectraPerCycle == 0)
            throw new InvalidOperationException("IdentifyCycle() Number of spectra per cycle is 0.");
    }

    private void IdentifyOverlap(List<DemuxIsolationWindow> isolationWindows)
    {
        if (isolationWindows.Count <= 1) return;

        long minimumWindowSize = IsoWindowHasher.Hash(_params.MinimumWindowSize);

        // Collect every distinct boundary m/z (hashed) as a sorted set.
        var demuxBoundaries = new SortedSet<DemuxBoundary>();
        foreach (var w in isolationWindows)
        {
            demuxBoundaries.Add(new DemuxBoundary(w.LowMz));
            demuxBoundaries.Add(new DemuxBoundary(w.HighMz));
        }

        // Merge nearby boundaries within minimumWindowSize. Direct port of cpp's iterator-based
        // walk: each iteration consumes 1 boundary (far-apart case) or 2 boundaries (merge case).
        // Cpp's post-loop push uses the last value of lowMZ, NOT the last array element — they
        // can differ when a merge consumed the final pair.
        var exactBoundaries = new List<DemuxBoundary>();
        var boundaryArray = new DemuxBoundary[demuxBoundaries.Count];
        demuxBoundaries.CopyTo(boundaryArray);
        int low = 0, high = 1;
        while (high < boundaryArray.Length)
        {
            if (boundaryArray[high].MzHash - boundaryArray[low].MzHash > minimumWindowSize)
            {
                exactBoundaries.Add(boundaryArray[low]);
                low++; high++;
            }
            else
            {
                double avg = (boundaryArray[high].Mz + boundaryArray[low].Mz) / 2.0;
                exactBoundaries.Add(new DemuxBoundary(avg));
                low += 2; high += 2;
            }
        }
        // Cpp pushes lowMZ->mz after the loop. lowMZ may be one past the last consumed boundary
        // when the loop ended on a merge that ate the final pair — in that case there's nothing
        // left to add.
        if (low < boundaryArray.Length)
            exactBoundaries.Add(boundaryArray[low]);

        // Build the candidate sub-windows from adjacent boundary pairs.
        var possibleWindows = new List<DemuxIsolationWindow>(exactBoundaries.Count - 1);
        for (int i = 0; i + 1 < exactBoundaries.Count; i++)
            possibleWindows.Add(new DemuxIsolationWindow(exactBoundaries[i].Mz, exactBoundaries[i + 1].Mz));

        // For each precursor isolation window, mark which sub-windows it covers.
        // Use a list-of-equivalence-classes structure (cpp uses std::multiset<IsolationWindow>).
        var usedWindows = new List<DemuxIsolationWindow>();
        foreach (var iso in isolationWindows)
            foreach (var sub in possibleWindows)
                if (iso.Window.ContainsCenter(sub.Window))
                    usedWindows.Add(sub);
        usedWindows.Sort();

        // Walk groups of equal-window entries; the largest group size is the overlap multiplicity.
        var returnWindows = new List<DemuxIsolationWindow>();
        int maxCount = 0;
        int idx = 0;
        while (idx < usedWindows.Count)
        {
            var current = usedWindows[idx];
            int count = 1;
            while (idx + count < usedWindows.Count && usedWindows[idx + count].Window == current.Window) count++;
            if (count > maxCount) maxCount = count;
            if (_params.RemoveNonOverlappingEdges)
            {
                returnWindows.Add(current);
                if (count != maxCount) _edgeWindowsToRemove.Add(returnWindows.Count - 1);
            }
            else
            {
                returnWindows.Add(current);
            }
            idx += count;
        }

        if (_params.RemoveNonOverlappingEdges)
        {
            // Re-scan: now that maxCount is known, redo the edge marking with the final value.
            _edgeWindowsToRemove.Clear();
            int outIdx = 0;
            int srcIdx = 0;
            while (srcIdx < usedWindows.Count)
            {
                int count = 1;
                while (srcIdx + count < usedWindows.Count && usedWindows[srcIdx + count].Window == usedWindows[srcIdx].Window) count++;
                if (count != maxCount)
                {
                    System.Console.Error.WriteLine(
                        $"Dropping non-overlapping edge isolation window: {usedWindows[srcIdx].LowMz}, {usedWindows[srcIdx].HighMz}");
                    _edgeWindowsToRemove.Add(outIdx);
                }
                outIdx++;
                srcIdx += count;
            }
        }

        _overlapsPerSpectrum = maxCount;
        if (_overlapsPerSpectrum == 0)
            throw new InvalidOperationException("IdentifyOverlap() Number of demux windows is 0.");

        isolationWindows.Clear();
        isolationWindows.AddRange(returnWindows);
    }

    /// <summary>Helper struct for splitting / merging isolation-window edges during overlap
    /// analysis. Cpp's <c>PrecursorMaskCodec::DemuxBoundary</c>.</summary>
    private readonly record struct DemuxBoundary(double Mz, long MzHash) : IComparable<DemuxBoundary>
    {
        public DemuxBoundary(double mz) : this(mz, IsoWindowHasher.Hash(mz)) { }
        public int CompareTo(DemuxBoundary other) => MzHash.CompareTo(other.MzHash);
    }
}
