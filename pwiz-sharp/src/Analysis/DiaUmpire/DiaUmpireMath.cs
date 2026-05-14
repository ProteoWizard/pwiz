using MathNet.Numerics.Distributions;

// CA1051: structs (Equation, MassDefect) keep cpp's public-field shape so the algorithm
// can do `equation.Mvalue = ...` directly. CA1822: LinearInterpolation.Run / MassDefect.InMassDefectRange
// don't touch instance state today; we still expose them as instance methods because cpp does, and
// because future variants (parameterized smoothing) belong on the instance.
#pragma warning disable CA1051 // Do not declare visible instance fields
#pragma warning disable CA1822 // Member does not access instance data and can be marked as static

namespace Pwiz.Analysis.DiaUmpire;

/// <summary>
/// File-scope floating-point comparison helpers ported from the anonymous namespace in
/// cpp <c>DiaUmpireMath.hpp</c>. Used by the rest of the DIA-Umpire ports for
/// tolerance-based equality / less-than / greater-than against cpp's exact branch behaviour.
/// </summary>
internal static class FpCompare
{
    /// <summary>Relative-equality comparison (do not use with zero).</summary>
    public static bool IsApproximatelyEqual(float a, float b, float tolerance)
    {
        float diff = System.Math.Abs(a - b);
        if (diff <= tolerance) return true;
        return diff < System.Math.Max(System.Math.Abs(a), System.Math.Abs(b)) * tolerance;
    }

    /// <summary>Relative-equality comparison (do not use with zero).</summary>
    public static bool IsApproximatelyEqual(double a, double b, double tolerance)
    {
        double diff = System.Math.Abs(a - b);
        if (diff <= tolerance) return true;
        return diff < System.Math.Max(System.Math.Abs(a), System.Math.Abs(b)) * tolerance;
    }

    /// <summary>Absolute-equality comparison to zero (supply meaningful tolerance).</summary>
    public static bool IsApproximatelyZero(float a, float tolerance) => System.Math.Abs(a) <= tolerance;

    /// <summary>Absolute-equality comparison to zero.</summary>
    public static bool IsApproximatelyZero(double a, double tolerance) => System.Math.Abs(a) <= tolerance;

    /// <summary>
    /// Definitely-less-than that mirrors cpp's odd "tolerance-rejection" branch
    /// (diff &lt; tolerance returns <paramref name="orEqualTo"/>, so default-false means
    /// "no, not less than" for tiny differences — matches cpp <c>isDefinitelyLessThan</c>).
    /// </summary>
    public static bool IsDefinitelyLessThan(float a, float b, float tolerance, bool orEqualTo = false)
    {
        float diff = a - b;
        if (diff < tolerance) return orEqualTo;
        return diff < System.Math.Max(System.Math.Abs(a), System.Math.Abs(b)) * tolerance;
    }

    /// <summary>See <see cref="IsDefinitelyLessThan(float, float, float, bool)"/>.</summary>
    public static bool IsDefinitelyGreaterThan(float a, float b, float tolerance, bool orEqualTo = false)
    {
        float diff = b - a;
        if (diff < tolerance) return orEqualTo;
        return diff < System.Math.Max(System.Math.Abs(a), System.Math.Abs(b)) * tolerance;
    }
}

/// <summary>
/// B-spline smoothing. Port of cpp <c>DiaUmpire::BSpline</c> in <c>DiaUmpireMath.hpp</c>.
/// Original algorithm by Chih-Chiang Tsou.
/// </summary>
public class BSpline
{
    private double[] _t = System.Array.Empty<double>();

    /// <summary>
    /// Generates a smoothed curve from <paramref name="data"/> with <paramref name="ptNum"/>
    /// + 1 samples and degree <paramref name="smoothDegree"/>. If <paramref name="data"/>
    /// has fewer than <paramref name="smoothDegree"/>+1 points, returns it as-is.
    /// </summary>
    public XYPointCollection Run(XYPointCollection data, int ptNum, int smoothDegree, int logId)
    {
        _ = logId; // cpp-only debug knob
        var output = new XYPointCollection();
        int p = smoothDegree;
        int n = data.Data.Count - 1;
        int m = data.Data.Count + p;
        _t = new double[m + p];

        if (data.Data.Count <= p) return data;

        for (int i = 0; i <= n; i++)
        {
            _t[i] = 0;
            _t[m - i] = 1;
        }
        double intv = 1.0 / (m - 2 * p);
        for (int i = 1; i <= m - 1; i++)
            _t[p + i] = _t[p + i - 1] + intv;

        for (int i = 0; i <= ptNum; i++)
        {
            double t = (double)i / ptNum;
            output.AddPoint(GetBSpline(data, t, n, p));
        }
        if (FpCompare.IsDefinitelyLessThan(output.Data[^1].GetX(), data.Data[^1].GetX(), 1e-8f))
            output.AddPoint(data.Data[data.PointCount() - 1]);
        if (FpCompare.IsDefinitelyGreaterThan(output.Data[0].GetX(), data.Data[0].GetX(), 1e-8f))
            output.AddPoint(data.Data[0]);

        return output;
    }

    private XYData GetBSpline(XYPointCollection data, double t, int n, int p)
    {
        var pt = new XYData(0, 0);
        for (int i = 0; i <= n; i++)
        {
            double basis = BSplineBase(i, p, t);
            pt.X = (float)(pt.GetX() + data.Data[i].GetX() * basis);
            pt.Y = (float)(pt.GetY() + data.Data[i].GetY() * basis);
        }
        return pt;
    }

    private double BSplineBase(int i, int p, double t)
    {
        if (p == 0)
        {
            if (_t[i] <= t && t < _t[i + 1] && _t[i] < _t[i + 1]) return 1;
            return 0;
        }

        double c1, c2, tn1 = 0, tn2 = 0;
        if (_t[i + p] - _t[i] == 0) { c1 = 0; }
        else
        {
            tn1 = BSplineBase(i, p - 1, t);
            c1 = (t - _t[i]) / (_t[i + p] - _t[i]);
        }
        if (_t[i + p + 1] - _t[i + 1] == 0) { c2 = 0; }
        else
        {
            tn2 = BSplineBase(i + 1, p - 1, t);
            c2 = (_t[i + p + 1] - t) / (_t[i + p + 1] - _t[i + 1]);
        }
        return c1 * tn1 + c2 * tn2;
    }
}

/// <summary>
/// Linear-interpolation resampler. Port of cpp <c>DiaUmpire::LinearInterpolation</c>.
/// </summary>
public class LinearInterpolation
{
    /// <summary>
    /// Resamples <paramref name="data"/> onto <paramref name="ptNum"/> evenly-spaced X
    /// samples. Gaps with no source point are filled with the midpoint of the nearest two
    /// samples (matches the cpp loop's iterator-rewind trick).
    /// </summary>
    public XYPointCollection Run(XYPointCollection data, int ptNum)
    {
        var smoothData = new XYData[ptNum];
        float intv = (data.Data[data.PointCount() - 1].GetX() - data.Data[0].GetX()) / ptNum;
        float rt = data.Data[0].GetX();
        for (int i = 0; i < ptNum; i++)
            smoothData[i] = new XYData(intv * i + rt, -1);
        int index = 0;
        foreach (var point in data.Data)
        {
            bool found = false;
            for (int i = index; i < ptNum - 1; i++)
            {
                if (smoothData[i].GetX() <= point.GetX() && smoothData[i + 1].GetX() > point.GetX())
                {
                    smoothData[i].Y = point.GetY();
                    index = i;
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                smoothData[ptNum - 1].Y = point.GetY();
                index = ptNum - 1;
            }
        }

        bool gapFound = false;
        int startIdx = 0;
        float startIntensity = smoothData[0].GetY();

        for (int i = 1; i < ptNum; i++)
        {
            if (gapFound && smoothData[i].GetY() != -1)
            {
                int endIdx = i;
                float endIntensity = smoothData[i].GetY();
                smoothData[(startIdx + endIdx) / 2].Y = (startIntensity + endIntensity) / 2;
                i = startIdx;
                gapFound = false;
            }
            if (!gapFound && smoothData[i].GetY() == -1)
            {
                startIdx = i - 1;
                startIntensity = smoothData[i - 1].GetY();
                gapFound = true;
            }
        }
        var ret = new XYPointCollection();
        ret.Data.AddRange(smoothData);
        // Maintain MaxY parity with the AddPoint path.
        foreach (var pt in ret.Data)
            if (pt.Y > ret.MaxY) ret.MaxY = pt.Y;
        return ret;
    }
}

/// <summary>
/// Continuous-wavelet (Mexican Hat) peak detector. Port of cpp
/// <c>DiaUmpire::WaveletMassDetector</c>. Original by Chih-Chiang Tsou; references
/// Tautenhahn, Bottcher &amp; Neumann, BMC Bioinformatics 9, 504 (2008).
/// </summary>
public class WaveletMassDetector
{
    private const int WaveletEsl = -5;
    private const int WaveletEsr = 5;
    private readonly InstrumentParameter _parameter;
    private readonly List<XYData> _dataPoint;
    private readonly double _waveletWindow = 0.3;
    private readonly float[] _mexHat;
    private readonly double _nPoints;
    private readonly double _nPointsHalf;
    private readonly int _d;

    /// <summary>Peak ridges discovered, indexed by scale level (outer) and ridge (inner).</summary>
    public List<List<XYData>?> PeakRidge { get; } = new();

    /// <summary>Constructs the detector and precomputes the wavelet samples.</summary>
    public WaveletMassDetector(InstrumentParameter parameter, List<XYData> dataPoint, int noPoints)
    {
        _parameter = parameter;
        _dataPoint = dataPoint;
        _nPoints = noPoints;

        double wstep = (WaveletEsr - WaveletEsl) / _nPoints;
        _mexHat = new float[(int)_nPoints];

        double waveletIndex = WaveletEsl;
        for (int j = 0; j < _nPoints; j++)
        {
            _mexHat[j] = (float)CwtMexHatReal(waveletIndex, _waveletWindow, 0.0);
            waveletIndex += wstep;
        }

        _nPointsHalf = _nPoints / 2;
        _d = (int)_nPoints / (WaveletEsr - WaveletEsl);
    }

    /// <summary>Runs the multi-scale CWT and populates <see cref="PeakRidge"/>.</summary>
    public void Run()
    {
        int maxScale = (int)(System.Math.Max(
            System.Math.Min(_dataPoint[^1].GetX() - _dataPoint[0].GetX(), _parameter.MaxCurveRTRange), 0.5f)
            * _parameter.NoPeakPerMin / (WaveletEsr + WaveletEsr));

        PeakRidge.Clear();
        for (int i = 0; i < maxScale; i++) PeakRidge.Add(null);

        for (int scaleLevel = 0; scaleLevel < maxScale; scaleLevel++)
        {
            var wavelet = PerformCwt(scaleLevel * 2 + 5);
            PeakRidge[scaleLevel] = new List<XYData>();
            var ridgeList = PeakRidge[scaleLevel]!;

            XYData lastpt = wavelet[0];
            var localmax = new XYData(0, 0);
            XYData startpt = wavelet[0];

            bool increasing = false;
            bool decreasing = false;
            var localmaxint = new XYData(0, 0);

            for (int cwtidx = 1; cwtidx < wavelet.Count; cwtidx++)
            {
                XYData currentPoint = wavelet[cwtidx];
                if (currentPoint.GetY() > lastpt.GetY())
                {
                    if (decreasing)
                    {
                        if (localmax.Y > 0 && (lastpt.GetY() <= startpt.GetY()
                            || System.Math.Abs(lastpt.GetY() - startpt.GetY()) / localmax.GetY() < _parameter.SymThreshold))
                        {
                            ridgeList.Add(localmax);
                            localmax = currentPoint;
                            startpt = lastpt;
                        }
                    }
                    increasing = true;
                    decreasing = false;
                }
                else if (currentPoint.GetY() < lastpt.GetY())
                {
                    if (increasing)
                    {
                        if (localmax.GetY() < lastpt.GetY()) localmax = lastpt;
                    }
                    decreasing = true;
                    increasing = false;
                }
                lastpt = currentPoint;
                if (currentPoint.GetY() > localmaxint.GetY()) localmaxint = currentPoint;
                if (cwtidx == wavelet.Count - 1 && decreasing)
                {
                    if (localmax.Y > 0 && (currentPoint.GetY() <= startpt.GetY()
                        || System.Math.Abs(currentPoint.GetY() - startpt.GetY()) / localmax.GetY() < _parameter.SymThreshold))
                    {
                        ridgeList.Add(localmax);
                    }
                }
            }
        }
    }

    private List<XYData> PerformCwt(int scaleLevel)
    {
        int length = _dataPoint.Count;
        var cwtDataPoints = new XYData[length];

        int aEsl = scaleLevel * WaveletEsl;
        int aEsr = scaleLevel * WaveletEsr;
        int nPointsHalf = (int)_nPointsHalf;
        int nPoints = (int)_nPoints;
        double sqrtScaleLevel = System.Math.Sqrt(scaleLevel);

        for (int dx = 0; dx < length; dx++)
        {
            int t1 = System.Math.Max(0, aEsl + dx);
            int t2 = System.Math.Min(length - 1, aEsr + dx);

            float intensity = 0;
            for (int i = t1; i <= t2; ++i)
            {
                int ind = nPointsHalf + (_d * (i - dx) / scaleLevel);
                ind = System.Math.Clamp(ind, 0, nPoints - 1);
                intensity += _dataPoint[i].Y * _mexHat[ind];
            }
            intensity = (float)(intensity / sqrtScaleLevel);
            if (intensity < 0) intensity = 0;
            cwtDataPoints[dx] = new XYData(_dataPoint[dx].GetX(), intensity);
        }
        return new List<XYData>(cwtDataPoints);
    }

    private static double CwtMexHatReal(double x, double window, double b)
    {
        const double C = 0.8673250705840776; // 2 / (sqrt(3) * pi^(1/4))
        const double Tiny = 1E-200;

        if (window == 0.0) window = Tiny;
        x = (x - b) / window;
        double x2 = x * x;
        return C * (1.0 - x2) * System.Math.Exp(-x2 / 2);
    }
}

/// <summary>
/// Per-mass mass-defect filter for precursor candidates.
/// Port of cpp <c>DiaUmpire::MassDefect</c> in <c>DiaUmpireMath.hpp</c>.
/// </summary>
public struct MassDefect
{
    /// <summary>
    /// True if the mass-defect (mass - floor(mass)) falls within the empirically-derived
    /// upper/lower envelope expanded by <paramref name="d"/>. Constants 0.00052738 etc.
    /// come from the original Java port — see header comment in cpp.
    /// </summary>
    public readonly bool InMassDefectRange(float mass, float d)
    {
        double u = GetMassDefect(0.00052738 * mass + 0.066015 + d);
        double l = GetMassDefect(0.00042565 * mass + 0.00038210 - d);
        double defect = GetMassDefect(mass);
        if (u > l) return defect >= l && defect <= u;
        return defect >= l || defect <= u;
    }

    /// <summary>Mass defect = mass − floor(mass).</summary>
    public static double GetMassDefect(double mass) => mass - System.Math.Floor(mass);
}

/// <summary>
/// Ordinary-least-squares linear regression of an <see cref="XYPointCollection"/>.
/// Port of cpp <c>DiaUmpire::Regression</c> in <c>DiaUmpireMath.hpp</c>.
/// </summary>
public class Regression
{
    /// <summary>Result equation y = mx + b plus per-fit statistics.</summary>
    public struct Equation
    {
        /// <summary>Intercept term (b in y = mx + b).</summary>
        public float Bvalue;
        /// <summary>Slope term (m in y = mx + b).</summary>
        public float Mvalue;
        /// <summary>Residual standard deviation (populated by <see cref="ComputeSD"/>).</summary>
        public float SDvalue;
        /// <summary>R^2 coefficient of determination.</summary>
        public float R2value;
        /// <summary>Sample count.</summary>
        public int NoPoints;
        /// <summary>Pearson correlation coefficient (populated by <see cref="ComputeCorrelationCoff"/>).</summary>
        public float CorrelationCoffe;

        /// <summary>Human-readable equation string, rounded to 3 decimal places.</summary>
        public readonly string GetEquationText() =>
            $"Y=({System.Math.Round(Mvalue * 1000) / 1000})X+{System.Math.Round(Bvalue * 1000) / 1000}";
    }

    /// <summary>The computed equation.</summary>
    public Equation EquationResult;

    /// <summary>Minimum points to compute a valid fit.</summary>
    public int MinPoint { get; set; } = 3;

    private readonly XYPointCollection _pointset;
    private float _sigXY;
    private float _sigX;
    private float _sigY;
    private float _sigX2;
    private float _sigY2;
    private float _sst;
    private float _ssr;
    private float _sxx;
    private float _syy;
    private float _sxy;
    private float _meanY;
    private float _meanX;

    /// <summary>Fits y = mx + b to the supplied points.</summary>
    public Regression(XYPointCollection pointset)
    {
        _pointset = pointset;
        FindEquation();
    }

    /// <summary>True if the point set is large enough for a meaningful regression.</summary>
    public bool Valid() => _pointset.PointCount() >= MinPoint;

    /// <summary>X corresponding to a given Y.</summary>
    public float GetX(float y) => (y - EquationResult.Bvalue) / EquationResult.Mvalue;

    /// <summary>Y corresponding to a given X.</summary>
    public float GetY(float x) => EquationResult.Mvalue * x + EquationResult.Bvalue;

    /// <summary>Coefficient of determination (R^2). Computed lazily on first call.</summary>
    public float GetR2()
    {
        ComputeR2();
        return EquationResult.R2value;
    }

    /// <summary>Pearson correlation coefficient.</summary>
    public void ComputeCorrelationCoff()
    {
        ComputeSXY();
        ComputeSXX();
        ComputeSYY();
        EquationResult.CorrelationCoffe = (float)(_sxy / System.Math.Pow((double)_sxx * _syy, 0.5));
    }

    /// <summary>Residual standard deviation.</summary>
    public void ComputeSD()
    {
        EquationResult.SDvalue = (float)System.Math.Sqrt(
            ((_pointset.PointCount() * _sigY2) - (_sigY * _sigY)
             - EquationResult.Mvalue * ((_pointset.PointCount() * _sigXY) - (_sigX * _sigY))) / _pointset.PointCount());
    }

    private void FindEquation()
    {
        for (int i = 0; i < _pointset.PointCount(); i++)
        {
            var point = _pointset.Data[i];
            _sigXY += point.GetX() * point.GetY();
            _sigX += point.GetX();
            _sigY += point.GetY();
            _sigX2 += point.GetX() * point.GetX();
            _sigY2 += point.GetY() * point.GetY();
        }
        EquationResult.Mvalue = ((_pointset.PointCount() * _sigXY) - (_sigX * _sigY))
                              / ((_pointset.PointCount() * _sigX2) - (_sigX * _sigX));
        EquationResult.Bvalue = (_sigY - EquationResult.Mvalue * _sigX) / _pointset.PointCount();
        EquationResult.NoPoints = _pointset.PointCount();
        _meanY = _sigY / _pointset.PointCount();
        _meanX = _sigX / _pointset.PointCount();
    }

    private void ComputeSXY()
    {
        _sxy = 0;
        for (int i = 0; i < _pointset.PointCount(); i++)
            _sxy += (_pointset.Data[i].GetX() - _meanX) * (_pointset.Data[i].GetY() - _meanY);
    }

    private void ComputeSXX()
    {
        _sxx = 0;
        for (int i = 0; i < _pointset.PointCount(); i++)
            _sxx += (_pointset.Data[i].GetX() - _meanX) * (_pointset.Data[i].GetX() - _meanX);
    }

    private void ComputeSYY()
    {
        _syy = 0;
        for (int i = 0; i < _pointset.PointCount(); i++)
            _syy += (_pointset.Data[i].GetY() - _meanY) * (_pointset.Data[i].GetY() - _meanY);
    }

    private void ComputeR2()
    {
        ComputeSST();
        ComputeSSR();
        EquationResult.R2value = (_sst - _ssr) / _sst;
    }

    private void ComputeSST()
    {
        _sst = 0;
        for (int i = 0; i < _pointset.PointCount(); i++)
            _sst += (_pointset.Data[i].GetY() - _meanY) * (_pointset.Data[i].GetY() - _meanY);
    }

    private void ComputeSSR()
    {
        _ssr = 0;
        for (int i = 0; i < _pointset.PointCount(); i++)
        {
            float pred = GetY(_pointset.Data[i].GetX());
            float resid = _pointset.Data[i].GetY() - pred;
            _ssr += resid * resid;
        }
    }
}

/// <summary>
/// Bin-and-correlate helpers for comparing two XICs. Port of cpp
/// <c>DiaUmpire::PearsonCorr</c> in <c>DiaUmpireMath.hpp</c>.
/// </summary>
/// <remarks>
/// Note: the cpp class is named <c>PearsonCorr</c>, but the math is regression-R^2
/// on linearly-binned intensity pairs — not Pearson's r. Naming preserved for parity.
/// </remarks>
public static class PearsonCorr
{
    /// <summary>
    /// Bins both inputs at 0.01-unit X intervals (100 points per X unit) and computes
    /// regression-R^2 between paired bins. Matches cpp <c>CalcCorrNeighborBin</c>.
    /// </summary>
    public static float CalcCorrNeighborBin(XYPointCollection collectionA, XYPointCollection collectionB)
    {
        int num = (int)((System.Math.Min(collectionA.Data[collectionA.PointCount() - 1].GetX(),
                                          collectionB.Data[collectionB.PointCount() - 1].GetX())
                       - System.Math.Max(collectionA.Data[0].GetX(), collectionB.Data[0].GetX())) * 100);
        float timeInterval = 1f / 100f;

        var arrayA = new float[num];
        var arrayB = new float[num];

        float start = System.Math.Max(collectionA.Data[0].GetX(), collectionB.Data[0].GetX());

        for (int i = 0; i < num - 1; i++)
        {
            float low = start + i * timeInterval;
            float up = start + (i + 1) * timeInterval;

            for (int j = 0; j < collectionA.PointCount(); j++)
            {
                if (collectionA.Data[j].GetX() >= low && collectionA.Data[j].GetX() < up)
                {
                    float intenLow = collectionA.Data[j].GetY() * (1 - (collectionA.Data[j].GetX() - low) / timeInterval);
                    float intenUp = collectionA.Data[j].GetY() * (1 - (up - collectionA.Data[j].GetX()) / timeInterval);
                    if (intenLow > arrayA[i]) arrayA[i] = intenLow;
                    if (intenUp > arrayA[i + 1]) arrayA[i + 1] = intenUp;
                }
                else if (collectionA.Data[j].GetX() > up) break;
            }

            for (int j = 0; j < collectionB.PointCount(); j++)
            {
                if (collectionB.Data[j].GetX() >= low && collectionB.Data[j].GetX() < up)
                {
                    float intenLow = collectionB.Data[j].GetY() * (1 - (collectionB.Data[j].GetX() - low) / timeInterval);
                    float intenUp = collectionB.Data[j].GetY() * (1 - (up - collectionB.Data[j].GetX()) / timeInterval);
                    if (intenLow > arrayB[i]) arrayB[i] = intenLow;
                    if (intenUp > arrayB[i + 1]) arrayB[i + 1] = intenUp;
                }
                else if (collectionB.Data[j].GetX() > up) break;
            }
        }

        var pointset = new XYPointCollection();
        for (int i = 0; i < num; i++)
            if (arrayA[i] > 0 && arrayB[i] > 0)
                pointset.AddPoint(arrayA[i], arrayB[i]);

        float r2 = 0;
        if (pointset.PointCount() > 5)
        {
            var regression = new Regression(pointset);
            if (regression.EquationResult.Mvalue > 0) r2 = regression.GetR2();
        }
        return r2;
    }

    /// <summary>
    /// Bins inputs at <c>2/<paramref name="noPointPerInterval"/></c> intervals, takes
    /// the bin-max for each side, and computes regression-R^2 of the paired bins.
    /// Matches cpp <c>CalcCorr</c>.
    /// </summary>
    public static float CalcCorr(XYPointCollection collectionA, XYPointCollection collectionB, int noPointPerInterval)
    {
        int num = System.Math.Max(collectionA.PointCount(), collectionB.PointCount()) / 2;
        float timeInterval = 2f / noPointPerInterval;
        if (num < 6) return 0;

        var arrayA = new float[num];
        var arrayB = new float[num];

        float start = System.Math.Max(collectionA.Data[0].GetX(), collectionB.Data[0].GetX());

        int i = 0;
        float low = start;
        float up = start + timeInterval;

        for (int j = 0; j < collectionA.PointCount(); j++)
        {
            while (collectionA.Data[j].GetX() > up) { i++; low = up; up = low + timeInterval; }
            if (i >= num) break;
            if (collectionA.Data[j].GetX() >= low && collectionA.Data[j].GetX() < up)
                if (collectionA.Data[j].GetY() > arrayA[i])
                    arrayA[i] = collectionA.Data[j].GetY();
        }
        i = 0;
        low = start;
        up = start + timeInterval;
        for (int j = 0; j < collectionB.PointCount(); j++)
        {
            while (collectionB.Data[j].GetX() > up) { i++; low = up; up = low + timeInterval; }
            if (i >= num) break;
            if (collectionB.Data[j].GetX() >= low && collectionB.Data[j].GetX() < up)
                if (collectionB.Data[j].GetY() > arrayB[i])
                    arrayB[i] = collectionB.Data[j].GetY();
        }

        for (int idx = 1; idx < num - 1; idx++)
        {
            if (arrayA[idx] == 0) arrayA[idx] = (arrayA[idx - 1] + arrayA[idx + 1]) / 2;
            if (arrayB[idx] == 0) arrayB[idx] = (arrayB[idx - 1] + arrayB[idx + 1]) / 2;
        }

        var pointset = new XYPointCollection();
        for (int idx = 0; idx < num; idx++)
            if (arrayA[idx] > 0 && arrayB[idx] > 0)
                pointset.AddPoint(arrayA[idx], arrayB[idx]);

        float r2 = 0;
        if (pointset.PointCount() > 5)
        {
            var regression = new Regression(pointset);
            if (regression.EquationResult.Mvalue > 0) r2 = regression.GetR2();
        }
        return r2;
    }
}

/// <summary>
/// Chi-squared goodness-of-fit probability lookup. Port of cpp
/// <c>DiaUmpire::ChiSquareGOF</c> in <c>DiaUmpireMath.hpp</c>.
/// </summary>
/// <remarks>
/// Cpp uses boost <c>boost::math::chi_squared</c>; we use MathNet
/// <see cref="ChiSquared"/> instances (same CDF math; same float result to ~6 digits).
/// </remarks>
public class ChiSquareGOF
{
    private readonly ChiSquared[] _models;

    /// <summary>Builds a model bank for degrees-of-freedom 1..<paramref name="maxPeak"/>-1.</summary>
    public ChiSquareGOF(int maxPeak)
    {
        _models = new ChiSquared[maxPeak - 1];
        for (int i = 1; i < maxPeak; i++)
            _models[i - 1] = new ChiSquared(i);
    }

    /// <summary>
    /// Returns 1 − chi^2 CDF for the relative-error statistic of <paramref name="observed"/>
    /// vs <paramref name="expected"/>. Degrees of freedom = number of nonzero observed entries
    /// minus 2 (matches cpp's "nopeaks - 2" indexing).
    /// </summary>
    public float GetGoodNessOfFitProb(IReadOnlyList<float> expected, IReadOnlyList<float> observed)
    {
        float gof = 0;
        int noPeaks = 0;
        int n = System.Math.Min(observed.Count, expected.Count);
        for (int i = 0; i < n; i++)
        {
            if (observed[i] > 0)
            {
                float error = expected[i] - observed[i];
                gof += (error * error) / (expected[i] * expected[i]);
                noPeaks++;
            }
        }

        if (float.IsNaN(gof) || noPeaks < 2) return 0;

        float prob = 1 - (float)_models[noPeaks - 2].CumulativeDistribution(gof);
        return prob;
    }
}
