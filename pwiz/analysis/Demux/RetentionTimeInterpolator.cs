namespace Pwiz.Analysis.Demux;

/// <summary>How a mux spectrum's binned intensities are carried to the demuxed spectrum's
/// retention time.</summary>
public enum DemuxInterpolation
{
    /// <summary>Natural cubic spline through 3 acquisitions, built by MathNet (cpp's CSpline).
    /// The default.</summary>
    NaturalSpline,

    /// <summary>The same 3-point natural spline in closed form: the same curve without building
    /// a general spline per value, so intensities differ from <see cref="NaturalSpline"/> only
    /// in rounding.</summary>
    NaturalThreePoint,

    /// <summary>Modified Akima through 6 acquisitions, 3 each side of the target: local, with a
    /// smaller apex error than the 3-point natural spline on simulated peaks.</summary>
    Makima,
}

/// <summary>
/// Interpolation of one fragment channel at a single time from a few acquisitions of the same
/// isolation window. The closed-form natural spline and makima follow Michael MacCoss's
/// <c>RtInterpolator</c> in ProteoWizard/pwiz PR #4710.
/// </summary>
internal static class RetentionTimeInterpolator
{
    /// <summary>Acquisitions of the window the method interpolates through.</summary>
    public static int Samples(DemuxInterpolation method) => method == DemuxInterpolation.Makima ? 6 : 3;

    /// <summary>Interpolates at <paramref name="t"/>. <paramref name="times"/> must be strictly
    /// increasing.</summary>
    public static double Interpolate(DemuxInterpolation method, double[] times, double[] values, double t)
    {
        return method switch
        {
            DemuxInterpolation.Makima => InterpolateMakima(times, values, t),
            _ => InterpolateNaturalThreePoint(times, values, t),
        };
    }

    // Natural boundary conditions (zero second derivative at both ends) leave one unknown, the
    // second derivative at the middle point. Outside the points the end segment extends, as
    // MathNet's spline does.
    private static double InterpolateNaturalThreePoint(double[] times, double[] values, double t)
    {
        double x0 = times[0], x1 = times[1], x2 = times[2];
        double y0 = values[0], y1 = values[1], y2 = values[2];
        double h0 = x1 - x0, h1 = x2 - x1;
        double m1 = 3.0 * ((y2 - y1) / h1 - (y1 - y0) / h0) / (h0 + h1);
        return t <= x1
            ? CubicSegment(x0, y0, 0, x1, y1, m1, t)
            : CubicSegment(x1, y1, m1, x2, y2, 0, t);
    }

    // A cubic spline segment from the second derivatives at its ends.
    private static double CubicSegment(double xa, double ya, double ma, double xb, double yb, double mb, double t)
    {
        double h = xb - xa;
        double a = xb - t, b = t - xa;
        return ma * a * a * a / (6 * h) + mb * b * b * b / (6 * h)
               + (ya / h - ma * h / 6) * a + (yb / h - mb * h / 6) * b;
    }

    // Outside the sampled range the nearest end value is held, not extrapolated.
    private static double InterpolateMakima(double[] times, double[] values, double t)
    {
        int count = times.Length;
        if (t <= times[0])
            return values[0];
        if (t >= times[count - 1])
            return values[count - 1];
        int i = 0;
        while (times[i + 1] <= t)
            i++;
        double slopeLeft = MakimaSlope(times, values, i);
        double slopeRight = MakimaSlope(times, values, i + 1);
        return Hermite(times[i], values[i], slopeLeft, times[i + 1], values[i + 1], slopeRight, t);
    }

    private static double Hermite(double x0, double y0, double d0, double x1, double y1, double d1, double t)
    {
        double h = x1 - x0;
        double s = (t - x0) / h;
        double s2 = s * s, s3 = s2 * s;
        return (2 * s3 - 3 * s2 + 1) * y0 + (s3 - 2 * s2 + s) * h * d0
               + (-2 * s3 + 3 * s2) * y1 + (s3 - s2) * h * d1;
    }

    private static double MakimaSlope(double[] times, double[] values, int i)
    {
        double dm2 = ExtendedSecant(times, values, i - 2);
        double dm1 = ExtendedSecant(times, values, i - 1);
        double d0 = ExtendedSecant(times, values, i);
        double dp1 = ExtendedSecant(times, values, i + 1);
        double w1 = System.Math.Abs(dp1 - d0) + 0.5 * System.Math.Abs(dp1 + d0);
        double w2 = System.Math.Abs(dm1 - dm2) + 0.5 * System.Math.Abs(dm1 + dm2);
        if (w1 + w2 <= 0)
            return 0.5 * (dm1 + d0);
        return (w1 * dm1 + w2 * d0) / (w1 + w2);
    }

    // Secant slope of interval j, extended past both ends by Akima's rule: each missing slope
    // is twice its neighbor minus the next one.
    private static double ExtendedSecant(double[] times, double[] values, int j)
    {
        int last = times.Length - 2;
        if (j >= 0 && j <= last)
            return Secant(times, values, j);
        if (j < 0)
        {
            double s0 = Secant(times, values, 0);
            double s1 = Secant(times, values, 1);
            double sm1 = 2 * s0 - s1;
            return j == -1 ? sm1 : 2 * sm1 - s0;
        }
        double sl = Secant(times, values, last);
        double sl1 = Secant(times, values, last - 1);
        double sp1 = 2 * sl - sl1;
        return j == last + 1 ? sp1 : 2 * sp1 - sl;
    }

    private static double Secant(double[] times, double[] values, int j)
    {
        return (values[j + 1] - values[j]) / (times[j + 1] - times[j]);
    }
}
