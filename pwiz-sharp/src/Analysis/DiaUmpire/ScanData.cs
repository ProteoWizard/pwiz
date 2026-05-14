// XYZData/XYData expose public fields because they mirror cpp DiaUmpire structs that the
// algorithm assigns to by-name dozens of times (pt.x = ..., pt.y = ...). Wrapping them in
// auto-properties would force a partial-mutation dance (var pt = list[i]; pt.X = ...; list[i] = pt;)
// at every call site, hiding cpp parity. Same intent as the [SuppressMessage] on InstrumentParameter
// for CA1707.
// Class names ending in "Collection" (XYPointCollection, ScanCollection) match cpp class names
// 1:1; renaming them would obscure the port mapping for future maintainers.
#pragma warning disable CA1051 // Do not declare visible instance fields
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix

namespace Pwiz.Analysis.DiaUmpire;

/// <summary>
/// 3D point used by the DIA-Umpire peak-curve representation.
/// Port of cpp <c>DiaUmpire::XYZData</c> in <c>ScanData.hpp</c>.
/// </summary>
/// <remarks>
/// In cpp this is a union (rt/x, mz/y, intensity/z). C# can't union floats cleanly,
/// so the three fields are kept plain — semantic aliases are exposed via the Rt/Mz/Intensity
/// properties so call sites that read "rt" / "mz" stay readable.
/// </remarks>
public struct XYZData : System.IEquatable<XYZData>
{
    /// <summary>Primary X coordinate (also accessed as <see cref="Rt"/>).</summary>
    public float X;
    /// <summary>Primary Y coordinate (also accessed as <see cref="Mz"/>).</summary>
    public float Y;
    /// <summary>Primary Z coordinate (also accessed as <see cref="Intensity"/>).</summary>
    public float Z;

    /// <summary>Retention time alias for <see cref="X"/>.</summary>
    public float Rt { readonly get => X; set => X = value; }
    /// <summary>m/z alias for <see cref="Y"/>.</summary>
    public float Mz { readonly get => Y; set => Y = value; }
    /// <summary>Intensity alias for <see cref="Z"/>.</summary>
    public float Intensity { readonly get => Z; set => Z = value; }

    /// <summary>Constructs an XYZ point.</summary>
    public XYZData(float x, float y, float z) { X = x; Y = y; Z = z; }

    /// <summary>Returns X (cpp parity).</summary>
    public readonly float GetX() => X;
    /// <summary>Returns Y (cpp parity).</summary>
    public readonly float GetY() => Y;
    /// <summary>Returns Z (cpp parity).</summary>
    public readonly float GetZ() => Z;

    /// <inheritdoc/>
    public readonly bool Equals(XYZData other) => X == other.X && Y == other.Y && Z == other.Z;
    /// <inheritdoc/>
    public override readonly bool Equals(object? obj) => obj is XYZData o && Equals(o);
    /// <inheritdoc/>
    public override readonly int GetHashCode() => System.HashCode.Combine(X, Y, Z);
    /// <summary>Equality operator.</summary>
    public static bool operator ==(XYZData a, XYZData b) => a.Equals(b);
    /// <summary>Inequality operator.</summary>
    public static bool operator !=(XYZData a, XYZData b) => !a.Equals(b);
}

/// <summary>
/// 2D point (m/z + intensity, or rt + intensity depending on context) used throughout
/// the DIA-Umpire algorithm. Port of cpp <c>DiaUmpire::XYData</c> in <c>ScanData.hpp</c>.
/// </summary>
public struct XYData : System.IEquatable<XYData>, System.IComparable<XYData>
{
    /// <summary>Primary X coordinate (also accessed as <see cref="Mz"/>).</summary>
    public float X;
    /// <summary>Primary Y coordinate (also accessed as <see cref="Intensity"/>).</summary>
    public float Y;

    /// <summary>m/z alias for <see cref="X"/>.</summary>
    public float Mz { readonly get => X; set => X = value; }
    /// <summary>Intensity alias for <see cref="Y"/>.</summary>
    public float Intensity { readonly get => Y; set => Y = value; }

    /// <summary>Constructs an XY point.</summary>
    public XYData(float x, float y) { X = x; Y = y; }

    /// <summary>Returns X (cpp parity).</summary>
    public readonly float GetX() => X;
    /// <summary>Returns Y (cpp parity).</summary>
    public readonly float GetY() => Y;

    /// <inheritdoc/>
    public readonly bool Equals(XYData other) => X == other.X && Y == other.Y;
    /// <inheritdoc/>
    public override readonly bool Equals(object? obj) => obj is XYData o && Equals(o);
    /// <inheritdoc/>
    public override readonly int GetHashCode() => System.HashCode.Combine(X, Y);
    /// <inheritdoc/>
    public readonly int CompareTo(XYData other) => X == other.X ? Y.CompareTo(other.Y) : X.CompareTo(other.X);
    /// <summary>Equality operator.</summary>
    public static bool operator ==(XYData a, XYData b) => a.Equals(b);
    /// <summary>Inequality operator.</summary>
    public static bool operator !=(XYData a, XYData b) => !a.Equals(b);
    /// <summary>Less-than operator.</summary>
    public static bool operator <(XYData a, XYData b) => a.CompareTo(b) < 0;
    /// <summary>Greater-than operator.</summary>
    public static bool operator >(XYData a, XYData b) => a.CompareTo(b) > 0;
    /// <summary>Less-or-equal operator.</summary>
    public static bool operator <=(XYData a, XYData b) => a.CompareTo(b) <= 0;
    /// <summary>Greater-or-equal operator.</summary>
    public static bool operator >=(XYData a, XYData b) => a.CompareTo(b) >= 0;
}

/// <summary>
/// X-sorted collection of <see cref="XYData"/> points, with binary-search lookups.
/// Port of cpp <c>DiaUmpire::XYPointCollection</c> in <c>ScanData.hpp</c>.
/// </summary>
public class XYPointCollection
{
    /// <summary>Maximum Y value seen so far; maintained incrementally by <see cref="AddPoint(float,float)"/>.</summary>
    public float MaxY { get; set; }

    /// <summary>Underlying point list (m/z- or rt-sorted by convention; callers must maintain order).</summary>
    public List<XYData> Data { get; } = new();

    /// <summary>Number of points.</summary>
    public int PointCount() => Data.Count;

    /// <summary>Sum of X over all points.</summary>
    public float GetSumX()
    {
        float sum = 0;
        foreach (var point in Data) sum += point.GetX();
        return sum;
    }

    /// <summary>Sum of Y over all points.</summary>
    public float GetSumY()
    {
        float sum = 0;
        foreach (var point in Data) sum += point.GetY();
        return sum;
    }

    /// <summary>Appends a point and updates <see cref="MaxY"/>.</summary>
    public void AddPoint(float x, float y)
    {
        Data.Add(new XYData(x, y));
        if (MaxY < y) MaxY = y;
    }

    /// <summary>Appends a point and updates <see cref="MaxY"/>.</summary>
    public void AddPoint(XYData point)
    {
        Data.Add(point);
        if (MaxY < point.GetY()) MaxY = point.GetY();
    }

    /// <summary>
    /// Adds <c>(x, y)</c> if no point within <paramref name="ppm"/> of x already exists.
    /// When a close match does exist, the call is a no-op on the stored data — only
    /// <see cref="MaxY"/> is updated. This is the cpp parity behavior: cpp does
    /// <c>XYData pt = Data.at(idx); ... pt.y = y;</c> which mutates a local value copy
    /// (XYData is a struct), so the assignment is silently dropped. The method name
    /// ("KeepMax") doesn't describe what actually happens — keep parity, not the name.
    /// </summary>
    public void AddPointKeepMaxIfCloseValueExisted(float x, float y, float ppm)
    {
        bool insert = true;
        if (Data.Count > 0)
        {
            int idx = GetClosestIndexOfX(x);
            var pt = Data[idx];
            if (InstrumentParameter.CalcPPM(pt.GetX(), x) < ppm)
            {
                insert = false;
                // cpp branches here on `y < pt.getY()` and mutates a local-copy `pt`;
                // the mutation is dropped at scope exit (cpp parity bug). We don't write
                // back either — keep the dead branch out of the C# code entirely so the
                // intent matches what actually happens.
                if (MaxY < y) MaxY = y;
            }
        }
        if (insert) AddPoint(x, y);
    }

    /// <summary>
    /// Adds <c>(x, y)</c>; if a point with the exact x already exists, the existing point's
    /// value is replaced when y is smaller. Cpp parity.
    /// </summary>
    public void AddPointKeepMaxIfValueExisted(float x, float y)
    {
        bool insert = true;
        if (Data.Count > 0)
        {
            int idx = GetClosestIndexOfX(x);
            var pt = Data[idx];
            if (pt.GetX() == x)
            {
                insert = false;
                // cpp parity: same value-copy-mutation-dropped quirk as
                // AddPointKeepMaxIfCloseValueExisted. Existing point stays untouched;
                // only MaxY updates.
                if (MaxY < y) MaxY = y;
            }
        }
        if (insert) AddPoint(x, y);
    }

    /// <summary>
    /// Centroiding using a local-maximum window; gap is the m/z value divided by resolution
    /// at the current m/z (so the window shrinks with mass).
    /// </summary>
    public void CentroidingByLocalMaximum(int resolution, float minMz)
    {
        if (Data.Count == 0) return;
        int oldCount = Data.Count;
        var dataTemp = new List<XYData>(Data);
        int startIndex = BinarySearchHigherIn(dataTemp, minMz);
        var pt = dataTemp[startIndex];
        float maxIntensity = pt.GetY();
        float maxMz = pt.GetX();
        float gap = pt.GetX() / resolution;
        Data.Clear();
        MaxY = 0;
        for (int i = startIndex + 1; i < oldCount; i++)
        {
            var pti = dataTemp[i];
            if (pti.GetX() - maxMz < gap)
            {
                if (pti.GetY() > maxIntensity)
                {
                    maxIntensity = pti.GetY();
                    maxMz = pti.GetX();
                    gap = pti.GetX() / resolution;
                }
            }
            else
            {
                AddPoint(maxMz, maxIntensity);
                maxIntensity = pti.GetY();
                maxMz = pti.GetX();
                gap = pti.GetX() / resolution;
            }
        }
    }

    /// <summary>Index of the highest point with X &lt;= x (or 0 if x is below the first point).</summary>
    public int GetLowerIndexOfX(float x) => BinarySearchLower(x);

    /// <summary>Index of the lowest point with X &gt;= x (or last index if x is above all points).</summary>
    public int GetHigherIndexOfX(float x) => BinarySearchHigher(x);

    /// <summary>Index of the point whose X is closest to x.</summary>
    public int GetClosestIndexOfX(float x) => BinarySearchClosest(x);

    /// <summary>The point at the index returned by <see cref="GetLowerIndexOfX"/>.</summary>
    public XYData GetPointByXLower(float x) => Data[GetLowerIndexOfX(x)];

    /// <summary>The point at the index returned by <see cref="GetClosestIndexOfX"/>.</summary>
    public XYData GetPointByXClosest(float x) => Data[GetClosestIndexOfX(x)];

    /// <summary>The point at the index returned by <see cref="GetHigherIndexOfX"/>.</summary>
    public XYData GetPointByXHigher(float x) => Data[GetHigherIndexOfX(x)];

    /// <summary>Returns a new collection containing only the points with X in <c>[xlower, xupper]</c>.</summary>
    public XYPointCollection GetSubSetByXRange(float xLower, float xUpper)
    {
        if (PointCount() == 0) return new XYPointCollection();

        var n = new XYPointCollection();
        int start = GetLowerIndexOfX(xLower);
        if (start < 0) start = 0;

        for (int i = start; i < PointCount(); i++)
        {
            float x = Data[i].GetX();
            if (x >= xLower && x <= xUpper)
                n.AddPoint(x, Data[i].GetY());
            else if (x > xUpper)
                break;
        }
        return n;
    }

    /// <summary>The point at <paramref name="index"/>.</summary>
    public XYData Get(int index) => Data[index];

    /// <summary>Number of points (alias for <see cref="PointCount"/>).</summary>
    public int Size() => Data.Count;

    /// <summary>Binary search for the rightmost X &lt;= value.</summary>
    public int BinarySearchLower(float value) => BinarySearchLowerIn(Data, value);

    /// <summary>Binary search for the leftmost X &gt;= value.</summary>
    public int BinarySearchHigher(float value) => BinarySearchHigherIn(Data, value);

    /// <summary>Binary search for the X closest to value.</summary>
    public int BinarySearchClosest(float value) => BinarySearchClosestIn(Data, value);

    private static int BinarySearchHigherIn(List<XYData> data, float value)
    {
        if (data.Count == 0) return 0;
        int lower = 0;
        int upper = data.Count - 1;

        if (value - data[upper].GetX() >= 0) return upper;
        if (value - data[0].GetX() <= 0) return 0;

        while (lower <= upper)
        {
            int middle = (lower + upper) / 2;
            float diff = value - data[middle].GetX();
            if (diff == 0)
            {
                while (middle - 1 >= 0 && data[middle - 1].GetX() == value) middle--;
                return middle;
            }
            if (diff < 0) upper = middle - 1;
            else lower = middle + 1;
        }
        if (lower > data.Count - 1) return data.Count - 1;
        while (lower < data.Count - 1 && data[lower].GetX() <= value) lower++;
        return lower;
    }

    private static int BinarySearchLowerIn(List<XYData> data, float value)
    {
        if (data.Count == 0) return 0;
        int lower = 0;
        int upper = data.Count - 1;

        if (value - data[upper].GetX() >= 0) return upper;
        if (value - data[0].GetX() <= 0) return 0;

        while (lower <= upper)
        {
            int middle = (lower + upper) / 2;
            float diff = value - data[middle].GetX();
            if (diff == 0)
            {
                while (middle - 1 >= 0 && data[middle - 1].GetX() == value) middle--;
                return middle;
            }
            if (diff < 0) upper = middle - 1;
            else lower = middle + 1;
        }
        if (upper < 0) return 0;
        while (upper > 0 && data[upper].GetX() >= value) upper--;
        return upper;
    }

    private static int BinarySearchClosestIn(List<XYData> data, float value)
    {
        if (data.Count == 0) return 0;
        int lower = 0;
        int upper = data.Count - 1;

        if (value - data[upper].GetX() >= 0) return upper;
        if (value - data[0].GetX() <= 0) return 0;

        while (lower <= upper)
        {
            int middle = (lower + upper) / 2;
            float diff = value - data[middle].GetX();
            if (diff == 0) return middle;
            if (diff < 0) upper = middle - 1;
            else lower = middle + 1;
        }

        if (System.Math.Abs(value - data[lower].GetX()) > System.Math.Abs(value - data[upper].GetX()))
            return upper;
        return lower;
    }
}

/// <summary>
/// Single mass-spectrum scan with peak data and acquisition metadata.
/// Port of cpp <c>DiaUmpire::ScanData</c> in <c>ScanData.hpp</c>.
/// </summary>
public class ScanData : XYPointCollection
{
    private float _totIonCurrent;

    /// <summary>Scan number (often the index from the source).</summary>
    public int ScanNum { get; set; }
    /// <summary>MS level (1 for precursor, 2 for fragment).</summary>
    public int MsLevel { get; set; }
    /// <summary>Retention time, in minutes.</summary>
    public float RetentionTime { get; set; }
    /// <summary>Lower m/z bound of the scan.</summary>
    public float StartMz { get; set; }
    /// <summary>Upper m/z bound of the scan.</summary>
    public float EndMz { get; set; }
    /// <summary>m/z of the base peak.</summary>
    public float BasePeakMz { get; set; }
    /// <summary>Intensity of the base peak.</summary>
    public float BasePeakIntensity { get; set; }
    /// <summary>Selected precursor m/z (MS2 only).</summary>
    public float PrecursorMz { get; set; }
    /// <summary>Selected precursor charge state.</summary>
    public int PrecursorCharge { get; set; }
    /// <summary>Activation/dissociation method, e.g. "CID".</summary>
    public string ActivationMethod { get; set; } = string.Empty;
    /// <summary>Selected precursor intensity.</summary>
    public float PrecursorIntensity { get; set; }
    /// <summary>Free-form scan type label.</summary>
    public string Scantype { get; set; } = string.Empty;
    /// <summary>Precision of the m/z encoding (cpp parity field; not used internally).</summary>
    public int Precision { get; set; }
    /// <summary>Compression scheme of the binary array (cpp parity field; not used internally).</summary>
    public string CompressionType { get; set; } = string.Empty;
    /// <summary>True if peaks are centroided.</summary>
    public bool Centroided { get; set; }
    /// <summary>Precursor scan number for MS2 scans.</summary>
    public int PrecursorScanNum { get; set; }
    /// <summary>Cached peak count.</summary>
    public int PeaksCountString { get; set; }
    /// <summary>Background level used for noise filtering.</summary>
    public float Background { get; set; }
    /// <summary>MGF title field.</summary>
    public string MGFTitle { get; set; } = string.Empty;
    /// <summary>Top-N peak scan (populated by <see cref="GenerateTopPeakScanData"/>).</summary>
    public ScanData? TopPeakScan { get; set; }
    /// <summary>Isolation window width.</summary>
    public float WindowWideness { get; set; }
    /// <summary>Isolation window target m/z.</summary>
    public float IsolationWindowTargetMz { get; set; }
    /// <summary>Lower offset (positive) from the isolation target m/z.</summary>
    public float IsolationWindowLoffset { get; set; }
    /// <summary>Upper offset (positive) from the isolation target m/z.</summary>
    public float IsolationWindowRoffset { get; set; }

    /// <summary>Calculates the precursor neutral mass.</summary>
    public float PrecursorMass()
    {
        const float Proton = 1.00727646677f;
        return PrecursorCharge * (PrecursorMz - Proton);
    }

    /// <summary>Centroids the spectrum then marks it centroided.</summary>
    public void Centroiding(int resolution, float minMz)
    {
        CentroidingByLocalMaximum(resolution, minMz);
        Centroided = true;
    }

    /// <summary>Finds the highest peak within <paramref name="ppm"/> of <paramref name="targetMz"/>, or null.</summary>
    public XYData? GetHighestPeakInMzWindow(float targetMz, float ppm)
    {
        float lowMz = InstrumentParameter.GetMzByPPM(targetMz, 1, ppm);
        int startIdx = GetLowerIndexOfX(lowMz);
        XYData? closestPeak = null;
        for (int idx = startIdx; idx < Data.Count; idx++)
        {
            var peak = Data[idx];
            if (InstrumentParameter.CalcPPM(targetMz, peak.GetX()) <= ppm)
            {
                if (closestPeak is null || peak.GetY() > closestPeak.Value.GetY())
                    closestPeak = peak;
            }
            else if (peak.GetX() > targetMz)
            {
                break;
            }
        }
        return closestPeak;
    }

    /// <summary>
    /// Populates <see cref="TopPeakScan"/> with the top-N peaks by intensity, with
    /// the X and Y of the source flipped (matches cpp <c>AddPoint(peak.getY(), peak.getX())</c>).
    /// </summary>
    public void GenerateTopPeakScanData(int topPeaks)
    {
        Data.Sort((a, b) => b.Y.CompareTo(a.Y));
        TopPeakScan = new ScanData();
        for (int i = 0; TopPeakScan.PointCount() < topPeaks && i < Data.Count; ++i)
        {
            var peak = Data[i];
            TopPeakScan.AddPoint(peak.GetY(), peak.GetX());
        }
        Data.Sort((a, b) => a.X.CompareTo(b.X));
    }

    /// <summary>
    /// Normalises Y values so the maximum is 1. Note (cpp parity): the cpp version writes
    /// to a local <c>XYData pt = Data.at(i)</c> copy and so doesn't actually mutate the list.
    /// We preserve that no-op behaviour to keep parity with cpp output. Marked as deprecated
    /// internally; we may revisit when phase 5 validates against cpp.
    /// </summary>
    public void Normalization()
    {
        if (MaxY != 0)
        {
            for (int i = 0; i < PointCount(); i++)
            {
                var pt = Data[i];
                _ = pt.GetY() / MaxY; // cpp parity: side-effect-free, same as cpp's local-copy.
            }
        }
    }

    /// <summary>Drops all points with Y &lt;= <see cref="Background"/>.</summary>
    public void RemoveSignalBelowBG()
    {
        for (int i = Data.Count - 1; i >= 0; --i)
            if (Data[i].GetY() <= Background)
                Data.RemoveAt(i);
        Data.TrimExcess();
    }

    /// <summary>Cached total ion current; computed lazily on first call.</summary>
    public float TotIonCurrent()
    {
        if (_totIonCurrent == 0)
            for (int i = 0; i < PointCount(); i++)
                _totIonCurrent += Data[i].GetY();
        return _totIonCurrent;
    }

    /// <summary>Sets the cached TIC value.</summary>
    public void SetTotIonCurrent(float ticValue) => _totIonCurrent = ticValue;

    /// <summary>
    /// Returns the intensity of the Nth-highest peak (0-indexed), or -1 if there are fewer
    /// than 10 peaks. Matches cpp's slightly odd "> 10" guard.
    /// </summary>
    public float GetTopNIntensity(int n)
    {
        Data.Sort((a, b) => b.Y.CompareTo(a.Y));
        float topN = Data.Count > 10 ? Data[n].Y : -1f;
        Data.Sort((a, b) => a.X.CompareTo(b.X));
        return topN;
    }

    /// <summary>
    /// Full preprocessing chain (centroid + background estimation + denoise).
    /// Deisotoping is unimplemented in cpp; we preserve the no-op.
    /// </summary>
    public void Preprocessing(InstrumentParameter parameter)
    {
        if (!Centroided) Centroiding(parameter.Resolution, parameter.MinMZ);

        if (parameter.EstimateBG)
        {
            AdjacentPeakHistogram();
        }
        else
        {
            if (MsLevel == 1) Background = parameter.MinMSIntensity;
            if (MsLevel == 2) Background = parameter.MinMSMSIntensity;
        }

        if (parameter.Denoise) RemoveSignalBelowBG();
    }

    /// <summary>
    /// Background estimator that sweeps a threshold upward until the count of adjacent peaks
    /// at common isotope distances (1.00, 0.50, 0.33, 0.25) dominates the count of "noise"
    /// peaks (closely-spaced). Cpp parity.
    /// </summary>
    public void AdjacentPeakHistogram()
    {
        if (PointCount() < 10) return;

        const float Ratio = 2;

        var intList = new float[Data.Count];
        for (int i = 0; i < Data.Count; i++) intList[i] = Data[i].GetY();
        System.Array.Sort(intList);

        float upper = intList[(int)(intList.Length * 0.7f)];
        float lower = intList[0];

        if (upper <= lower + 0.001f) return;

        float bk = 0;
        float interval = (upper - lower) / 20;

        for (bk = lower; bk < upper; bk += interval)
        {
            int count1 = 0, count2 = 0, count3 = 0, count4 = 0, noise = 0;
            int preIdx = -1;
            for (int i = 1; i < Data.Count; i++)
            {
                if (Data[i].GetY() > bk)
                {
                    if (preIdx != -1)
                    {
                        float dist = Data[i].GetX() - Data[preIdx].GetX();
                        if (dist > 0.95f && dist < 1.05f && Data[preIdx].GetY() > Data[i].GetY()) count1++;
                        else if (dist > 0.45f && dist < 0.55f && Data[preIdx].GetY() > Data[i].GetY()) count2++;
                        else if (dist > 0.3f && dist < 0.36f && Data[preIdx].GetY() > Data[i].GetY()) count3++;
                        else if (dist > 0.24f && dist < 0.26f && Data[preIdx].GetY() > Data[i].GetY()) count4++;
                        else if (dist < 0.23f) noise++;
                    }
                    preIdx = i;
                }
            }
            if (noise < (count1 + count2 + count3 + count4) * Ratio) break;
        }
        if (bk > 0)
        {
            Background = bk;
            RemoveSignalBelowBG();
        }
    }
}

/// <summary>
/// Map of scan-number → <see cref="ScanData"/> with MS1/MS2 indices and a few cached
/// summary views (TIC, elution-time→scan-no map). Port of cpp <c>DiaUmpire::ScanCollection</c>
/// in <c>ScanData.hpp</c>.
/// </summary>
public class ScanCollection
{
    private int _numScan;
    private int _numScanLevel1;
    private int _numScanLevel2;
    private int _startScan = 1_000_000;
    private int _endScan;
    private int _numPeaks; // across all scans
    private float _minPrecursorInt = float.MaxValue;
    private XYPointCollection? _tic;
    private readonly List<int> _ms1ScanIndex = new();
    private readonly List<int> _ms2ScanIndex = new();

    /// <summary>Resolution from the cpp ctor; not currently used internally.</summary>
    public int Resolution { get; }

    /// <summary>All scans keyed by scan number (cpp uses std::map; ordered).</summary>
    public SortedDictionary<int, ScanData> ScanHashMap { get; } = new();

    /// <summary>Map from retention time to scan number (cpp uses std::map; ordered).</summary>
    public SortedDictionary<float, int> ElutionTimeToScanNoMap { get; } = new();

    /// <summary>Creates an empty collection.</summary>
    public ScanCollection(int resolution = 0)
    {
        Resolution = resolution;
        Clear();
    }

    /// <summary>Empties all internal state.</summary>
    public void Clear()
    {
        _ms1ScanIndex.Clear();
        _ms2ScanIndex.Clear();
        ScanHashMap.Clear();
        ElutionTimeToScanNoMap.Clear();
        _numScan = 0;
        _numScanLevel1 = 0;
        _numScanLevel2 = 0;
        _numPeaks = 0;
        _tic = null;
    }

    /// <summary>Sorts the MS1 and MS2 index vectors.</summary>
    public void SortIndices()
    {
        _ms1ScanIndex.Sort();
        _ms2ScanIndex.Sort();
    }

    /// <summary>Number of scans.</summary>
    public int Size() => ScanHashMap.Count;

    /// <summary>Number of peaks across all scans.</summary>
    public int GetNumPeaks() => _numPeaks;

    /// <summary>Number of MS1 scans.</summary>
    public int NumScanLevel1 => _numScanLevel1;

    /// <summary>Number of MS2 scans.</summary>
    public int NumScanLevel2 => _numScanLevel2;

    /// <summary>Lowest observed precursor intensity (MS2).</summary>
    public float MinPrecursorInt => _minPrecursorInt;

    /// <summary>Scan number indices at the requested MS level.</summary>
    public IReadOnlyList<int> GetScanNoArray(int msLevel) => msLevel switch
    {
        1 => _ms1ScanIndex,
        2 => _ms2ScanIndex,
        _ => throw new System.ArgumentException("unsupported ms level", nameof(msLevel)),
    };

    /// <summary>Copy of the MS2 scan-number list (cpp parity name).</summary>
    public List<int> GetMS2DescendingArray() => new(_ms2ScanIndex);

    /// <summary>
    /// Adds an already-populated <see cref="ScanData"/> to the collection. Differs slightly
    /// from cpp, which builds the ScanData from a pwiz <c>SpectrumPtr</c> inline — the
    /// adapter that goes from a pwiz-sharp <c>Spectrum</c> to a <see cref="ScanData"/>
    /// lives in the (forthcoming) <c>SpectrumList_DiaUmpire</c> wrapper. Keeping the data-only
    /// path lets the unit tests build a ScanCollection without an MSData dependency.
    /// </summary>
    public ScanData AddScan(ScanData scan)
    {
        if (ScanHashMap.TryGetValue(scan.ScanNum, out var existing))
            return existing;

        ScanHashMap[scan.ScanNum] = scan;
        _numPeaks += scan.PointCount();

        if (scan.MsLevel == 1) { _numScanLevel1++; _ms1ScanIndex.Add(scan.ScanNum); }
        else if (scan.MsLevel == 2)
        {
            _numScanLevel2++;
            _ms2ScanIndex.Add(scan.ScanNum);
            if (scan.PrecursorIntensity > 0)
                _minPrecursorInt = System.Math.Min(_minPrecursorInt, scan.PrecursorIntensity);
        }
        _numScan++;

        if (scan.ScanNum >= _endScan) _endScan = scan.ScanNum;
        if (scan.ScanNum <= _startScan) _startScan = scan.ScanNum;

        return scan;
    }

    /// <summary>Returns the nearest preceding MS1 scan to <paramref name="scanNo"/>, or null.</summary>
    public ScanData? GetParentMSScan(int scanNo)
    {
        if (!ScanHashMap.ContainsKey(scanNo)) return null;

        // SortedDictionary doesn't have a "find iterator and walk back" — manually scan keys.
        ScanData? lastMs1 = null;
        foreach (var kv in ScanHashMap)
        {
            if (kv.Key >= scanNo) break;
            if (kv.Value.MsLevel == 1) lastMs1 = kv.Value;
        }
        return lastMs1;
    }

    /// <summary>Returns the scan at <paramref name="scanNo"/> or null.</summary>
    public ScanData? GetScan(int scanNo) =>
        ScanHashMap.TryGetValue(scanNo, out var s) ? s : null;

    /// <summary>True if a scan with the given number has been added.</summary>
    public bool ScanAdded(int scanNo) => ScanHashMap.ContainsKey(scanNo);

    /// <summary>Centroids every scan in the collection.</summary>
    public void CentoridingAllScans(int resolution, float miniIntF)
    {
        foreach (var kv in ScanHashMap) kv.Value.Centroiding(resolution, miniIntF);
    }

    /// <summary>Returns the scan number whose elution time is &gt;= <paramref name="rt"/>, falling back to the last.</summary>
    public int GetScanNoByRT(float rt)
    {
        // SortedDictionary.lower_bound equivalent: find first key >= rt.
        foreach (var kv in ElutionTimeToScanNoMap)
            if (kv.Key >= rt) return kv.Value;
        return ElutionTimeToScanNoMap.Count > 0
            ? ElutionTimeToScanNoMap.Reverse().First().Value
            : 0;
    }

    /// <summary>Cached TIC chromatogram (built on first access).</summary>
    public XYPointCollection GetTIC()
    {
        if (_tic is not null && _tic.Data.Count > 0) return _tic;
        _tic = new XYPointCollection();
        foreach (var kv in ScanHashMap)
            _tic.AddPoint(kv.Value.RetentionTime, kv.Value.TotIonCurrent());
        return _tic;
    }

    /// <summary>Sets background on all scans at the given MS level and drops sub-threshold peaks.</summary>
    public void RemoveBackground(int msLevel, float background)
    {
        foreach (var kv in ScanHashMap)
        {
            var scan = kv.Value;
            if (scan.MsLevel == msLevel)
            {
                scan.Background = background;
                scan.RemoveSignalBelowBG();
            }
        }
    }
}
