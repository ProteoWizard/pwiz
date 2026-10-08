// PeakRidge / PeakCurveRegion structs keep cpp's public-field shape so the algorithm's
// dozens of in-place mutations (ridge.RT = ..., ridge.intensity = ..., region.x = ...) read
// 1:1 against the cpp source. Same justification as the file-scope pragmas in ScanData.cs.
#pragma warning disable CA1051 // Do not declare visible instance fields

namespace Pwiz.Analysis.DiaUmpire;

/// <summary>
/// Single m/z trace peak curve. Holds the raw peak list, a smoothed XIC, and (after
/// <see cref="DetectPeakRegion"/>) one or more apex regions. Port of cpp
/// <c>DiaUmpire::PeakCurve</c> in <c>PeakCurve.hpp</c>. Original algorithm by Chih-Chiang Tsou.
/// </summary>
public class PeakCurve
{
    /// <summary>Per-ridge state used by CWT-based region detection.</summary>
    public struct PeakRidge : System.IComparable<PeakRidge>, System.IEquatable<PeakRidge>
    {
        /// <summary>Retention time of the ridge maximum.</summary>
        public float RT;
        /// <summary>Lowest CWT scale at which this ridge survived.</summary>
        public int LowScale;
        /// <summary>Number of consecutive CWT scales over which the ridge persisted.</summary>
        public int ContinuousLevel;
        /// <summary>Smoothed intensity at the ridge RT.</summary>
        public float Intensity;

        /// <inheritdoc/>
        public readonly int CompareTo(PeakRidge other) => RT.CompareTo(other.RT);
        /// <inheritdoc/>
        public readonly bool Equals(PeakRidge other) =>
            RT == other.RT && LowScale == other.LowScale && ContinuousLevel == other.ContinuousLevel && Intensity == other.Intensity;
        /// <inheritdoc/>
        public override readonly bool Equals(object? obj) => obj is PeakRidge r && Equals(r);
        /// <inheritdoc/>
        public override readonly int GetHashCode() => System.HashCode.Combine(RT, LowScale, ContinuousLevel, Intensity);
        /// <summary>Equality operator.</summary>
        public static bool operator ==(PeakRidge a, PeakRidge b) => a.Equals(b);
        /// <summary>Inequality operator.</summary>
        public static bool operator !=(PeakRidge a, PeakRidge b) => !a.Equals(b);
        /// <summary>Less-than operator (RT-based, matches cpp).</summary>
        public static bool operator <(PeakRidge a, PeakRidge b) => a.CompareTo(b) < 0;
        /// <summary>Greater-than operator.</summary>
        public static bool operator >(PeakRidge a, PeakRidge b) => a.CompareTo(b) > 0;
        /// <summary>Less-or-equal operator.</summary>
        public static bool operator <=(PeakRidge a, PeakRidge b) => a.CompareTo(b) <= 0;
        /// <summary>Greater-or-equal operator.</summary>
        public static bool operator >=(PeakRidge a, PeakRidge b) => a.CompareTo(b) >= 0;
    }

    private readonly List<XYZData> _peakList = new();
    private XYPointCollection _smoothData = new();
    private float _totalIntMzF;
    private float _totalIntF;
    private readonly List<XYZData> _peakRegionList = new();
    private readonly List<List<float>> _noRidgeRegion = new();
    private float _snr = -1;
    private float _baseLine = -1;
    private float _noiseLevel = -1;

    /// <summary>Index for diagnostics and cluster cross-referencing.</summary>
    public int Index { get; set; }
    /// <summary>First scan number contributing to this curve.</summary>
    public int StartScan { get; set; } = -1;
    /// <summary>Last scan number contributing to this curve.</summary>
    public int EndScan { get; set; } = -1;
    /// <summary>MS level (1 = precursor, 2 = fragment).</summary>
    public int MsLevel { get; set; }
    /// <summary>Intensity-weighted target m/z of the curve.</summary>
    public float TargetMz { get; set; }
    /// <summary>Apex retention time.</summary>
    public float ApexRT { get; set; } = -1;
    /// <summary>Apex intensity.</summary>
    public float ApexInt { get; set; }
    /// <summary>Minimum intensity seen (used by raw-SNR calc).</summary>
    public float MinIntF { get; set; } = float.PositiveInfinity;
    /// <summary>Maximum correlation seen during MS1↔MS2 pairing.</summary>
    public float MaxCorr { get; set; }
    /// <summary>State flag used by the cluster-building stage.</summary>
    public bool CheckState { get; set; }
    /// <summary>Sum of correlation scores from competing clusters that pulled this curve.</summary>
    public float ConflictCorr { get; set; }
    /// <summary>True if this curve has already been assigned to a stronger neighbouring cluster.</summary>
    public bool Grouped { get; set; }
    /// <summary>Charge states that have grouped this curve.</summary>
    public HashSet<int> ChargeGrouped { get; } = new();
    /// <summary>Variance of m/z across raw peaks (populated by <see cref="CalculateMzVar"/>).</summary>
    public float MzVar { get; set; } = -1;
    /// <summary>Retention times of the per-region split ridges.</summary>
    public List<float> RegionRidge { get; set; } = new();
    /// <summary>The instrument parameters this curve is using.</summary>
    public InstrumentParameter Parameter { get; }

    /// <summary>Constructs an empty peak curve.</summary>
    public PeakCurve(InstrumentParameter parameter) { Parameter = parameter; }

    /// <summary>Generates the smoothed XIC using B-spline smoothing.</summary>
    public void DoBspline()
    {
        foreach (var point in _peakList)
            _smoothData.AddPoint(new XYData(point.GetX(), point.GetZ()));
        var bspline = new BSpline();
        _smoothData = bspline.Run(_smoothData,
            (int)System.Math.Max((int)System.Math.Round(RTWidth() * Parameter.NoPeakPerMin), _peakList.Count),
            2, 0);
    }

    /// <summary>Generates the smoothed XIC using linear interpolation (alternative to <see cref="DoBspline"/>).</summary>
    public void DoInterpolation()
    {
        foreach (var point in _peakList)
            _smoothData.AddPoint(new XYData(point.GetX(), point.GetZ()));
        var interp = new LinearInterpolation();
        _smoothData = interp.Run(_smoothData,
            (int)System.Math.Max((int)System.Math.Round(RTWidth() * Parameter.NoPeakPerMin), _peakList.Count));
    }

    /// <summary>Accumulates a conflict-correlation contribution from a competing cluster.</summary>
    public void AddConflictScore(float corr) => ConflictCorr += corr;

    /// <summary>Apex / min intensity ratio (raw-data SNR).</summary>
    public float GetRawSNR() => ApexInt / MinIntF;

    /// <summary>
    /// Runs the CWT-based peak-region detector against the smoothed XIC and populates
    /// <see cref="GetPeakRegionList"/>. Empty if the curve has fewer than 1 estimated point.
    /// </summary>
    public void DetectPeakRegion()
    {
        var peakRidgeList = new List<PeakRidge>();
        _peakRegionList.Clear();
        _noRidgeRegion.Clear();
        if (RTWidth() * Parameter.NoPeakPerMin < 1) return;

        var peakArrayList = new List<XYData>(_smoothData.Data);
        var detector = new WaveletMassDetector(Parameter, peakArrayList,
            (int)(RTWidth() * Parameter.NoPeakPerMin));
        detector.Run();

        int maxScale = detector.PeakRidge.Count - 1;
        const float NoValuePlaceholder = float.PositiveInfinity;

        for (int i = maxScale; i >= 0; i--)
        {
            var peakRidgeArrayPtr = detector.PeakRidge[i];
            if (peakRidgeArrayPtr is null) { maxScale = i; continue; }
            if (peakRidgeArrayPtr.Count == 0) continue;

            var peakRidgeArray = peakRidgeArrayPtr;
            var disMatrix = new float[peakRidgeList.Count, peakRidgeArray.Count];
            for (int k = 0; k < peakRidgeList.Count; k++)
                for (int l = 0; l < peakRidgeArray.Count; l++)
                    disMatrix[k, l] = System.Math.Abs(peakRidgeList[k].RT - peakRidgeArray[l].GetX());

            bool conti = true;
            var removedRidgeList = new List<int>();
            while (conti)
            {
                float closest = NoValuePlaceholder;
                int existingRidgeIdx = -1;
                int peakRidgeIdx = -1;
                for (int k = 0; k < peakRidgeList.Count; k++)
                    for (int l = 0; l < peakRidgeArray.Count; l++)
                        if (disMatrix[k, l] < closest)
                        {
                            closest = disMatrix[k, l];
                            existingRidgeIdx = k;
                            peakRidgeIdx = l;
                        }

                if (closest != NoValuePlaceholder && closest <= Parameter.MinRTRange)
                {
                    PeakRidge ridge = peakRidgeList[existingRidgeIdx];
                    peakRidgeList.RemoveAt(existingRidgeIdx);
                    ridge.LowScale = i;
                    ridge.ContinuousLevel++;
                    XYData nearestRidge = peakRidgeArray[peakRidgeIdx];
                    ridge.RT = nearestRidge.GetX();
                    peakRidgeList.Add(ridge);
                    peakRidgeList.Sort();
                    removedRidgeList.Add(peakRidgeIdx);
                    for (int k = 0; k < peakRidgeList.Count; k++)
                        disMatrix[k, peakRidgeIdx] = NoValuePlaceholder;
                    for (int l = 0; l < peakRidgeArray.Count; l++)
                        disMatrix[existingRidgeIdx, l] = NoValuePlaceholder;
                }
                else
                {
                    conti = false;
                }
            }

            removedRidgeList.Sort((a, b) => b.CompareTo(a));
            foreach (int removeRidge in removedRidgeList)
                peakRidgeArray.RemoveAt(removeRidge);

            var removeList = new List<int>();
            for (int k = 0; k < peakRidgeList.Count; k++)
            {
                var existRidge = peakRidgeList[k];
                if (existRidge.LowScale - i > 2 && existRidge.ContinuousLevel < maxScale / 2)
                    removeList.Add(k);
            }
            removeList.Sort((a, b) => b.CompareTo(a));
            foreach (int idx in removeList) peakRidgeList.RemoveAt(idx);

            if (i > maxScale / 2)
            {
                foreach (var ridge in peakRidgeArray)
                {
                    var newRidge = new PeakRidge
                    {
                        RT = ridge.GetX(),
                        LowScale = i,
                        ContinuousLevel = 1,
                    };
                    newRidge.Intensity = _smoothData.GetPointByXClosest(newRidge.RT).GetY();
                    peakRidgeList.Add(newRidge);
                    peakRidgeList.Sort();
                }
            }
            peakRidgeArray.Clear();
        }

        if (peakRidgeList.Count <= 1)
        {
            _peakRegionList.Add(new XYZData(_smoothData.Data[0].GetX(), ApexRT,
                _smoothData.Data[_smoothData.PointCount() - 1].GetX()));
            _noRidgeRegion.Add(new List<float> { ApexRT });
        }

        if (peakRidgeList.Count > 1)
        {
            var valleyPoints = new XYData[peakRidgeList.Count + 1];
            valleyPoints[0] = _smoothData.Data[0];
            PeakRidge currentRidge = peakRidgeList[0];
            var localmin = new XYData(-1, NoValuePlaceholder);
            int startIdx = _smoothData.GetLowerIndexOfX(currentRidge.RT);

            for (int j = 1; j < peakRidgeList.Count; j++)
            {
                PeakRidge nextRidge = peakRidgeList[j];
                for (int p = startIdx; p < _smoothData.Data.Count; p++)
                {
                    var point = _smoothData.Data[p];
                    if (point.GetX() > currentRidge.RT && point.GetX() < nextRidge.RT)
                        if (localmin.GetY() > point.GetY()) localmin = point;
                    if (point.GetX() >= nextRidge.RT) { startIdx = p; break; }
                }
                valleyPoints[j] = localmin;
                localmin = new XYData(-1, NoValuePlaceholder);
                currentRidge = nextRidge;
            }
            valleyPoints[peakRidgeList.Count] = _smoothData.Data[_smoothData.PointCount() - 1];

            startIdx = 0;
            for (int p = 0; p < peakRidgeList.Count; p++)
            {
                PeakRidge ridge = peakRidgeList[p];
                for (int j = startIdx; j < _smoothData.Data.Count; j++)
                {
                    var point = _smoothData.Data[j];
                    if (point.GetX() < valleyPoints[p + 1].GetX())
                    {
                        if (ridge.Intensity < point.GetY())
                        {
                            ridge.Intensity = point.GetY();
                            ridge.RT = point.GetX();
                        }
                    }
                    else { startIdx = j; break; }
                }
                peakRidgeList[p] = ridge;
            }

            var splitPoints = new bool[peakRidgeList.Count - 1];
            FindSplitPoint(peakRidgeList, 0, peakRidgeList.Count - 1, valleyPoints, splitPoints);

            var ridgeRTs = new List<float>();
            startIdx = 0;
            PeakRidge maxRidge = peakRidgeList[0];

            for (int p = 0; p < peakRidgeList.Count - 1; p++)
            {
                ridgeRTs.Add(peakRidgeList[p].RT);
                if (peakRidgeList[p].Intensity > maxRidge.Intensity) maxRidge = peakRidgeList[p];
                if (splitPoints[p])
                {
                    _peakRegionList.Add(new XYZData(valleyPoints[startIdx].GetX(), maxRidge.RT,
                        valleyPoints[p + 1].GetX()));
                    _noRidgeRegion.Add(new List<float>(ridgeRTs));
                    maxRidge = peakRidgeList[p + 1];
                    ridgeRTs.Clear();
                    startIdx = p + 1;
                }
            }
            ridgeRTs.Add(peakRidgeList[^1].RT);
            if (peakRidgeList[^1].Intensity > maxRidge.Intensity) maxRidge = peakRidgeList[^1];
            _peakRegionList.Add(new XYZData(valleyPoints[startIdx].GetX(), maxRidge.RT,
                valleyPoints[peakRidgeList.Count].GetX()));
            _noRidgeRegion.Add(new List<float>(ridgeRTs));
        }
    }

    /// <summary>Returns separate <see cref="PeakCurve"/> instances per detected region.</summary>
    /// <param name="sn">Unused; preserved for cpp signature parity.</param>
    public List<PeakCurve> SeparatePeakByRegion(float sn)
    {
        _ = sn;
        var temp = new List<PeakCurve>();

        for (int i = 0; i < GetPeakRegionList().Count; i++)
        {
            var pc = new PeakCurve(Parameter)
            {
                Index = Index,
                RegionRidge = _noRidgeRegion[i],
                MsLevel = MsLevel,
            };
            temp.Add(pc);
            XYZData region = GetPeakRegionList()[i];
            if (region.GetZ() - region.GetX() > Parameter.MaxCurveRTRange)
            {
                int leftIdx = GetSmoothedList().GetLowerIndexOfX(region.GetX());
                int rightIdx = GetSmoothedList().GetHigherIndexOfX(region.GetZ());
                XYData left = GetSmoothedList().Data[leftIdx];
                XYData right = GetSmoothedList().Data[rightIdx];
                while ((right.GetX() - left.GetX()) > Parameter.MaxCurveRTRange)
                {
                    if (right.GetX() - region.GetY() <= Parameter.MaxCurveRTRange / 4) leftIdx++;
                    else if (region.GetY() - left.GetX() <= Parameter.MaxCurveRTRange / 4) rightIdx--;
                    else if (left.GetY() < right.GetY()) leftIdx++;
                    else rightIdx--;
                    left = GetSmoothedList().Data[leftIdx];
                    right = GetSmoothedList().Data[rightIdx];
                }
                region.X = left.GetX();
                region.Z = right.GetX();
                _peakRegionList[i] = region;
            }
        }

        for (int i = 0; i < GetPeakList().Count; i++)
        {
            XYZData peak = GetPeakList()[i];
            for (int j = 0; j < GetPeakRegionList().Count; j++)
            {
                XYZData region = GetPeakRegionList()[j];
                if (FpCompare.IsDefinitelyGreaterThan(peak.GetX(), region.GetX(), 1e-8f, true)
                    && FpCompare.IsDefinitelyLessThan(peak.GetX(), region.GetZ(), 1e-8f, true))
                {
                    temp[j].AddPeak(peak);
                    break;
                }
            }
        }

        for (int i = 0; i < GetSmoothedList().Data.Count; i++)
        {
            XYData peak = GetSmoothedList().Data[i];
            for (int j = 0; j < GetPeakRegionList().Count; j++)
            {
                XYZData region = GetPeakRegionList()[j];
                if (FpCompare.IsDefinitelyGreaterThan(peak.GetX(), region.GetX(), 1e-8f, true)
                    && FpCompare.IsDefinitelyLessThan(peak.GetX(), region.GetZ(), 1e-8f, true))
                {
                    temp[j].GetSmoothedList().Data.Add(peak);
                    break;
                }
            }
        }

        var ret = new List<PeakCurve>();
        foreach (var peak in temp)
            if (peak._peakList.Count > 2)
                ret.Add(peak);
        return ret;
    }

    /// <summary>Intensity of the first raw peak.</summary>
    public float StartInt() => _peakList[0].GetZ();

    /// <summary>Retention time of the first smoothed point (or second raw point if no smoothed data).</summary>
    public float StartRT()
    {
        if (_smoothData.Data.Count > 0) return _smoothData.Data[0].GetX();
        return _peakList[1].GetX();
    }

    /// <summary>Maximum intensity within an RT slice of the smoothed XIC.</summary>
    public float GetMaxIntensityByRegionRange(float startRT, float endRT)
    {
        float max = 0;
        foreach (var pt in GetSmoothedList().Data)
            if (pt.GetX() >= startRT && pt.GetX() <= endRT && pt.GetY() > max)
                max = pt.GetY();
        return max;
    }

    /// <summary>SNR placeholder — cpp implementation only caches ApexInt; we preserve that.</summary>
    public float GetSNR()
    {
        if (_snr == -1) _snr = ApexInt;
        return _snr;
    }

    /// <summary>Baseline estimate (lowest 10% of smoothed intensities, averaged).</summary>
    public float GetBaseLine()
    {
        if (_baseLine == -1)
        {
            CalculateBaseLine();
            if (_baseLine == 0) _baseLine = 1;
        }
        return _baseLine;
    }

    /// <summary>Noise level (currently set to <see cref="GetBaseLine"/> by cpp; same here).</summary>
    public float GetNoiseLevel()
    {
        if (_noiseLevel == -1) CalculateBaseLine();
        return _noiseLevel;
    }

    /// <summary>RT of the second-to-last raw peak (cpp parity — note this is NOT the last).</summary>
    public float EndRT() => _peakList[^2].GetX();

    /// <summary>RT of the very last raw peak.</summary>
    public float LastScanRT() => _peakList[^1].GetX();

    /// <summary>Returns a copy of the smoothed XIC as an <see cref="XYPointCollection"/>.</summary>
    public XYPointCollection GetPeakCollection()
    {
        var pt = new XYPointCollection();
        foreach (var p in _smoothData.Data) pt.AddPoint(p.GetX(), p.GetY());
        return pt;
    }

    /// <summary>Smoothed XIC clipped to <c>[startRT, endRT]</c>.</summary>
    public XYPointCollection GetSmoothPeakCollection(float startRT, float endRT)
    {
        var pt = new XYPointCollection();
        foreach (var p in _smoothData.Data)
        {
            if (p.GetX() > endRT) break;
            if (p.GetX() >= startRT && p.GetX() <= endRT) pt.AddPoint(p.GetX(), p.GetY());
        }
        return pt;
    }

    /// <summary>Maximum smoothed intensity within an RT slice.</summary>
    public float DetermineIntByRTRange(float startRT, float endRT)
    {
        float intensity = 0;
        foreach (var pt in GetSmoothedList().Data)
            if (pt.GetX() >= startRT && pt.GetX() <= endRT && pt.GetY() > intensity)
                intensity = pt.GetY();
        return intensity;
    }

    /// <summary>Width of the curve in RT (preferring raw peaks, falling back to smoothed).</summary>
    public float RTWidth()
    {
        float width = 0;
        if (_peakList.Count > 0)
            width = _peakList[^1].GetX() - _peakList[0].GetX();
        else if (_smoothData.PointCount() > 0)
            width = _smoothData.Data[_smoothData.PointCount() - 1].GetX() - _smoothData.Data[0].GetX();

        if (width < 0)
            throw new System.InvalidOperationException(
                "[DiaUmpire::PeakCurve::RTWidth] peak times out of order");
        return width;
    }

    /// <summary>Raw peak list (rt, mz, intensity).</summary>
    public List<XYZData> GetPeakList() => _peakList;

    /// <summary>Smoothed XIC.</summary>
    public XYPointCollection GetSmoothedList() => _smoothData;

    /// <summary>Detected peak regions; populated after <see cref="DetectPeakRegion"/>.</summary>
    public List<XYZData> GetPeakRegionList() => _peakRegionList;

    /// <summary>Drops all stored peak/smooth/region data (memory reclaim).</summary>
    public void ReleasePeakData()
    {
        _peakList.Clear();
        _smoothData.Data.Clear();
        _peakRegionList.Clear();
    }

    /// <summary>Drops raw peak + region data, preserving the smoothed XIC.</summary>
    public void ReleaseRawPeak()
    {
        _peakList.Clear();
        _peakList.TrimExcess();
        _peakRegionList.Clear();
        _peakRegionList.TrimExcess();
    }

    /// <summary>Appends a raw peak; updates apex/min and the running intensity-weighted m/z.</summary>
    public void AddPeak(XYZData xyzPoint)
    {
        if (_peakList.Count > 0 && xyzPoint.GetX() < _peakList[^1].GetX())
            throw new System.InvalidOperationException(
                $"[DiaUmpire::PeakCurve::AddPeak] scan time is not monotonically increasing: new time " +
                $"{xyzPoint.GetX()} < last added time {_peakList[^1].GetX()}");

        _peakList.Add(xyzPoint);
        _totalIntMzF += xyzPoint.GetY() * xyzPoint.GetZ() * xyzPoint.GetZ();
        _totalIntF += xyzPoint.GetZ() * xyzPoint.GetZ();
        if (xyzPoint.GetZ() > ApexInt)
        {
            ApexInt = xyzPoint.GetZ();
            ApexRT = xyzPoint.GetX();
        }
        if (xyzPoint.GetZ() < MinIntF) MinIntF = xyzPoint.GetZ();
        TargetMz = _totalIntMzF / _totalIntF;
    }

    /// <summary>Computes the variance of m/z values over the raw peaks (relative to <see cref="TargetMz"/>).</summary>
    public void CalculateMzVar()
    {
        MzVar = 0;
        for (int j = 0; j < _peakList.Count; j++)
            MzVar += (_peakList[j].GetX() - TargetMz) * (_peakList[j].GetX() - TargetMz);
        MzVar /= _peakList.Count;
    }

    private void CalculateBaseLine()
    {
        _baseLine = 0;
        var intensityQueue = new Queue<float>();
        foreach (var point in _smoothData.Data) intensityQueue.Enqueue(point.GetY());

        if (intensityQueue.Count > 10)
        {
            int tenth = intensityQueue.Count / 10;
            for (int i = 0; i < tenth; i++) _baseLine += intensityQueue.Dequeue();
            _baseLine /= tenth;
        }
        else if (intensityQueue.Count > 0)
        {
            _baseLine = intensityQueue.Peek();
        }
        _noiseLevel = _baseLine;
    }

    private void FindSplitPoint(List<PeakRidge> peakRidgeList, int left, int right,
        XYData[] valleyPoints, bool[] splitPoints)
    {
        for (int i = left; i < right; i++)
        {
            if (ValidSplitPoint(peakRidgeList, left, right, i, valleyPoints))
            {
                splitPoints[i] = true;
                FindSplitPoint(peakRidgeList, left, i, valleyPoints, splitPoints);
                FindSplitPoint(peakRidgeList, i + 1, right, valleyPoints, splitPoints);
                break;
            }
        }
    }

    private bool ValidSplitPoint(List<PeakRidge> peakRidgeList, int left, int right, int cut,
        XYData[] valleyPoints)
    {
        PeakRidge leftRidge = peakRidgeList[left];
        PeakRidge rightRidge = peakRidgeList[cut + 1];

        for (int i = left; i <= cut; i++)
            if (peakRidgeList[i].Intensity > leftRidge.Intensity) leftRidge = peakRidgeList[i];
        for (int i = cut + 1; i <= right; i++)
            if (peakRidgeList[i].Intensity > rightRidge.Intensity) rightRidge = peakRidgeList[i];

        float leftValley = System.Math.Abs(valleyPoints[left].GetY() - valleyPoints[cut + 1].GetY())
                         / leftRidge.Intensity;
        float rightValley = System.Math.Abs(valleyPoints[cut + 1].GetY() - valleyPoints[right + 1].GetY())
                          / rightRidge.Intensity;

        return leftValley < Parameter.SymThreshold && rightValley < Parameter.SymThreshold;
    }
}

/// <summary>
/// Cross-correlation between two <see cref="PeakCurve"/> instances.
/// Port of cpp <c>DiaUmpire::PeakCurveCorrCalc</c>.
/// </summary>
public static class PeakCurveCorrCalc
{
    /// <summary>Correlation over the RT overlap of two curves.</summary>
    public static float CalPeakCorr(PeakCurve peakA, PeakCurve peakB, int noPointPerMin)
    {
        float startRT = System.Math.Max(peakA.StartRT(), peakB.StartRT());
        float endRT = System.Math.Min(peakA.EndRT(), peakB.EndRT());
        var pa = peakA.GetSmoothPeakCollection(startRT, endRT);
        var pb = peakB.GetSmoothPeakCollection(startRT, endRT);
        if (pa.Data.Count > 0 && pb.Data.Count > 0)
            return PearsonCorr.CalcCorr(pa, pb, noPointPerMin);
        return 0;
    }

    /// <summary>Correlation across two specific region pairs.</summary>
    public static float CalPeakCorrOverlap(PeakCurve peakA, PeakCurve peakB,
        int aStart, int aEnd, int bStart, int bEnd, int noPeakPerMin)
    {
        float startRT = System.Math.Max(peakA.GetPeakRegionList()[aStart].GetX(),
                                         peakB.GetPeakRegionList()[bStart].GetX());
        float endRT = System.Math.Min(peakA.GetPeakRegionList()[aEnd].GetZ(),
                                       peakB.GetPeakRegionList()[bEnd].GetZ());
        var pa = peakA.GetSmoothPeakCollection(startRT, endRT);
        var pb = peakB.GetSmoothPeakCollection(startRT, endRT);
        if (pa.Data.Count > 0 && pb.Data.Count > 0)
            return PearsonCorr.CalcCorr(pa, pb, noPeakPerMin);
        return 0;
    }
}
