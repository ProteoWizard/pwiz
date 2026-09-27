/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;

namespace pwiz.Osprey.Demux
{
    /// <summary>
    /// How a window's fragment signal is carried from the times it was acquired to the time
    /// a partner window was acquired.
    /// </summary>
    /// <remarks>
    /// Measured on simulated EMG peaks at a half-cycle offset (k=2), mean apex error as a
    /// percentage of peak height:
    /// <list type="bullet">
    /// <item>4 points per FWHM: makima -0.6, PCHIP -2.6, 3-point natural -1.5, linear -4.1.</item>
    /// <item>3 points per FWHM: makima -1.3, PCHIP -4.5, 3-point natural -3.2 (5% of values
    /// negative), linear -6.9.</item>
    /// </list>
    /// A natural spline over the whole series was best without noise but rang under counting
    /// noise, with 11-38% of interpolated values negative, so it is not offered.
    /// </remarks>
    public enum RtInterpolation
    {
        /// <summary>
        /// Modified Akima: local, no peak-shape assumption, small apex bias, and only rare
        /// tiny undershoots. The default.
        /// </summary>
        makima,

        /// <summary>
        /// Fritsch-Carlson monotone cubic Hermite (PCHIP). Never overshoots, but clips the
        /// apex because it forces a zero slope at the sampled maximum.
        /// </summary>
        pchip,

        /// <summary>
        /// Natural cubic spline through the nearest sample and one neighbor on each side:
        /// what pwiz's OverlapDemultiplexer does. Kept to attribute differences against msconvert.
        /// </summary>
        natural_three_point,

        /// <summary>Linear interpolation between the bracketing samples.</summary>
        linear,
    }

    /// <summary>
    /// Local interpolation of one fragment channel's chromatogram at a single time.
    /// </summary>
    public static class RtInterpolator
    {
        /// <summary>
        /// How many samples the method wants on each side of the target time. The caller
        /// passes up to this many, fewer at the ends of the run.
        /// </summary>
        public static int SamplesPerSide(RtInterpolation method)
        {
            switch (method)
            {
                case RtInterpolation.makima:
                    return 3;
                case RtInterpolation.pchip:
                case RtInterpolation.natural_three_point:
                    return 2;
                default:
                    return 1;
            }
        }

        /// <summary>
        /// Interpolates at <paramref name="t"/> from the first <paramref name="count"/> samples,
        /// whose times must be strictly increasing. Outside the sampled range the nearest end
        /// value is held rather than extrapolated.
        /// </summary>
        public static double Interpolate(RtInterpolation method, double[] times, double[] values,
            int count, double t)
        {
            if (count <= 0)
                return 0;
            if (count == 1 || t <= times[0])
                return values[0];
            if (t >= times[count - 1])
                return values[count - 1];

            if (method == RtInterpolation.natural_three_point)
                return InterpolateNaturalThreePoint(times, values, count, t);

            int i = 0;
            while (times[i + 1] <= t)
                i++;
            if (method == RtInterpolation.linear || count == 2)
                return Lerp(times[i], values[i], times[i + 1], values[i + 1], t);

            double slopeLeft, slopeRight;
            if (method == RtInterpolation.pchip)
            {
                slopeLeft = PchipSlope(times, values, count, i);
                slopeRight = PchipSlope(times, values, count, i + 1);
            }
            else
            {
                slopeLeft = MakimaSlope(times, values, count, i);
                slopeRight = MakimaSlope(times, values, count, i + 1);
            }
            return Hermite(times[i], values[i], slopeLeft, times[i + 1], values[i + 1], slopeRight, t);
        }

        private static double InterpolateNaturalThreePoint(double[] times, double[] values, int count,
            double t)
        {
            if (count < 3)
                return Lerp(times[0], values[0], times[1], values[1], t);
            // The sample nearest the target, and one neighbor on each side of it, as pwiz does.
            int nearest = 0;
            for (int j = 1; j < count; j++)
            {
                if (Math.Abs(times[j] - t) < Math.Abs(times[nearest] - t))
                    nearest = j;
            }
            int first = Math.Max(0, Math.Min(nearest - 1, count - 3));

            double x0 = times[first], x1 = times[first + 1], x2 = times[first + 2];
            double y0 = values[first], y1 = values[first + 1], y2 = values[first + 2];
            if (t < x0)
                return y0;
            if (t > x2)
                return y2;
            double h0 = x1 - x0, h1 = x2 - x1;
            // Natural end conditions: zero second derivative at x0 and x2.
            double m1 = 3.0 * ((y2 - y1) / h1 - (y1 - y0) / h0) / (h0 + h1);
            if (t <= x1)
                return CubicSegment(x0, y0, 0, x1, y1, m1, t);
            return CubicSegment(x1, y1, m1, x2, y2, 0, t);
        }

        /// <summary>
        /// Evaluates a cubic spline segment from the second derivatives at its ends.
        /// </summary>
        private static double CubicSegment(double xa, double ya, double ma, double xb, double yb,
            double mb, double t)
        {
            double h = xb - xa;
            double a = xb - t, b = t - xa;
            return ma * a * a * a / (6 * h) + mb * b * b * b / (6 * h)
                   + (ya / h - ma * h / 6) * a + (yb / h - mb * h / 6) * b;
        }

        private static double Lerp(double x0, double y0, double x1, double y1, double t)
        {
            return y0 + (y1 - y0) * (t - x0) / (x1 - x0);
        }

        private static double Hermite(double x0, double y0, double d0, double x1, double y1, double d1,
            double t)
        {
            double h = x1 - x0;
            double s = (t - x0) / h;
            double s2 = s * s, s3 = s2 * s;
            return (2 * s3 - 3 * s2 + 1) * y0 + (s3 - 2 * s2 + s) * h * d0
                   + (-2 * s3 + 3 * s2) * y1 + (s3 - s2) * h * d1;
        }

        private static double Secant(double[] times, double[] values, int j)
        {
            return (values[j + 1] - values[j]) / (times[j + 1] - times[j]);
        }

        /// <summary>
        /// Secant slope of interval <paramref name="j"/>, extended past both ends by Akima's
        /// rule (each missing slope is twice its neighbor minus the next one).
        /// </summary>
        private static double ExtendedSecant(double[] times, double[] values, int count, int j)
        {
            int last = count - 2;
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

        private static double MakimaSlope(double[] times, double[] values, int count, int i)
        {
            double dm2 = ExtendedSecant(times, values, count, i - 2);
            double dm1 = ExtendedSecant(times, values, count, i - 1);
            double d0 = ExtendedSecant(times, values, count, i);
            double dp1 = ExtendedSecant(times, values, count, i + 1);
            double w1 = Math.Abs(dp1 - d0) + 0.5 * Math.Abs(dp1 + d0);
            double w2 = Math.Abs(dm1 - dm2) + 0.5 * Math.Abs(dm1 + dm2);
            if (w1 + w2 <= 0)
                return 0.5 * (dm1 + d0);
            return (w1 * dm1 + w2 * d0) / (w1 + w2);
        }

        private static double PchipSlope(double[] times, double[] values, int count, int i)
        {
            if (i == 0 || i == count - 1)
                return PchipEndSlope(times, values, count, i == 0);
            double hl = times[i] - times[i - 1];
            double hr = times[i + 1] - times[i];
            double dl = Secant(times, values, i - 1);
            double dr = Secant(times, values, i);
            if (dl * dr <= 0)
                return 0;
            double w1 = 2 * hr + hl;
            double w2 = hr + 2 * hl;
            return (w1 + w2) / (w1 / dl + w2 / dr);
        }

        private static double PchipEndSlope(double[] times, double[] values, int count, bool left)
        {
            double h0, h1, d0, d1;
            if (left)
            {
                h0 = times[1] - times[0];
                h1 = times[2] - times[1];
                d0 = Secant(times, values, 0);
                d1 = Secant(times, values, 1);
            }
            else
            {
                h0 = times[count - 1] - times[count - 2];
                h1 = times[count - 2] - times[count - 3];
                d0 = Secant(times, values, count - 2);
                d1 = Secant(times, values, count - 3);
            }
            double d = ((2 * h0 + h1) * d0 - h0 * d1) / (h0 + h1);
            if (Math.Sign(d) != Math.Sign(d0))
                return 0;
            if (Math.Sign(d0) != Math.Sign(d1) && Math.Abs(d) > Math.Abs(3 * d0))
                return 3 * d0;
            return d;
        }
    }
}
