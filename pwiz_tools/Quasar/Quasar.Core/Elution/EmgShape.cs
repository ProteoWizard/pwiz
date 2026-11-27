/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on datum (https://github.com/maccoss/datum)
 *   by Michael J. MacCoss, MacCoss Lab, Department of Genome Sciences, UW
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
using System.Collections.Concurrent;

namespace pwiz.Quasar.Core.Elution
{
    /// <summary>
    /// The shape of an exponentially modified Gaussian (EMG) elution profile: a Gaussian of
    /// center <see cref="Mu"/> and width <see cref="Sigma"/> convolved with a one-sided exponential
    /// of time constant <see cref="Tau"/> (larger tau, longer right tail). The density integrates
    /// to 1, so a fitted amplitude is the peak area.
    /// </summary>
    /// <remarks>
    /// Evaluated with the numerically stable factorization datum uses (Kalambet et al. 2011). With
    /// u = (t - mu) / sigma, k = sigma / tau and z = (k - u) / sqrt(2), the density is
    /// exp(-u^2 / 2) * erfcx(z) / (2 tau) for z &gt;= 0, and exp(k^2 / 2 - u k) * erfc(z) / (2 tau)
    /// otherwise.
    /// </remarks>
    public sealed class EmgShape
    {
        /// <summary>The parameter vector order <see cref="Model"/> expects: area, mu, sigma, tau.</summary>
        public const int PARAMETER_COUNT = 4;

        // The mode and the height crossings depend on sigma, tau (and the fraction) but not on mu,
        // and finding them takes hundreds of density evaluations, while every template fit,
        // segment and support count asks for them. They are computed once per shape at mu = 0
        // and offset by mu.
        private static readonly ConcurrentDictionary<(double Sigma, double Tau), double> MODE_OFFSETS =
            new ConcurrentDictionary<(double Sigma, double Tau), double>();
        private static readonly ConcurrentDictionary<(double Sigma, double Tau, double Fraction), (double Left, double Right)> CROSSING_OFFSETS =
            new ConcurrentDictionary<(double Sigma, double Tau, double Fraction), (double Left, double Right)>();

        /// <summary>
        /// The EMG with area <c>parameters[0]</c> and shape (mu, sigma, tau) =
        /// <c>parameters[1..3]</c>, evaluated at <paramref name="t"/>. Negative sigma or tau are
        /// read as their absolute values, so an unconstrained fitter can step through zero.
        /// </summary>
        public static double Model(double[] parameters, double t)
        {
            return Evaluate(parameters[0], parameters[1], parameters[2], parameters[3], t);
        }

        /// <summary><see cref="Model"/> without the parameter array.</summary>
        public static double Evaluate(double amplitude, double mu, double sigmaParameter, double tauParameter, double t)
        {
            double sigma = Math.Abs(sigmaParameter);
            double tau = Math.Abs(tauParameter);
            if (sigma < 1e-9 || tau < 1e-9)
                return 0.0;

            double u = (t - mu) / sigma;
            double k = sigma / tau;
            double z = (k - u) / Math.Sqrt(2.0);
            double raw = z >= 0.0
                ? Math.Exp(-0.5 * u * u) * ErrorFunctions.Erfcx(z)
                : Math.Exp(0.5 * k * k - u * k) * ErrorFunctions.Erfc(z);
            return amplitude * raw / (2.0 * tau);
        }

        public EmgShape(double mu, double sigma, double tau)
        {
            Mu = mu;
            Sigma = sigma;
            Tau = tau;
        }

        /// <summary>Center of the Gaussian component (the mode sits slightly to its right).</summary>
        public double Mu { get; }

        /// <summary>Standard deviation of the Gaussian component.</summary>
        public double Sigma { get; }

        /// <summary>Time constant of the exponential tail.</summary>
        public double Tau { get; }

        /// <summary>The unit-area density at <paramref name="t"/>.</summary>
        public double Density(double t)
        {
            return Evaluate(1.0, Mu, Sigma, Tau, t);
        }

        /// <summary>
        /// The time of the density's maximum: the apex. For a tailed peak it sits right of
        /// <see cref="Mu"/>, by up to sigma as tau grows. Found by golden-section search, which is
        /// exact to well below a scan because the density is unimodal.
        /// </summary>
        public double Mode
        {
            get { return Mu + MODE_OFFSETS.GetOrAdd((Sigma, Tau), key => new EmgShape(0.0, key.Sigma, key.Tau).SearchMode()); }
        }

        /// <summary>This shape moved so that its <see cref="Mode"/> is at <paramref name="apex"/>.</summary>
        public EmgShape WithModeAt(double apex)
        {
            return new EmgShape(Mu + apex - Mode, Sigma, Tau);
        }

        /// <summary>
        /// The times where the density falls to <paramref name="fraction"/> of its peak height on
        /// the leading and trailing sides of the mode. For a tailed peak the trailing crossing is
        /// farther from the mode than the leading one. Found on a fine grid of the noise-free model,
        /// which spans the leading Gaussian shoulder and a trailing extent that grows with tau and
        /// with how far down the threshold sits.
        /// </summary>
        public (double Left, double Right) HeightCrossings(double fraction)
        {
            var (left, right) = CROSSING_OFFSETS.GetOrAdd((Sigma, Tau, fraction),
                key => new EmgShape(0.0, key.Sigma, key.Tau).GridCrossings(key.Fraction));
            return (Mu + left, Mu + right);
        }

        public override string ToString()
        {
            return $@"EMG[mu={Mu}, sigma={Sigma}, tau={Tau}]";
        }

        /// <summary>The golden-section search for the mode, on this shape as it is.</summary>
        private double SearchMode()
        {
            double lo = Mu - Sigma;
            double hi = Mu + 2.0 * Sigma + Tau;
            const double invPhi = 0.6180339887498949;
            double a = hi - invPhi * (hi - lo);
            double b = lo + invPhi * (hi - lo);
            double fa = Density(a);
            double fb = Density(b);
            while (hi - lo > 1e-9 * Math.Max(1.0, Sigma))
            {
                if (fa < fb)
                {
                    lo = a;
                    a = b;
                    fa = fb;
                    b = lo + invPhi * (hi - lo);
                    fb = Density(b);
                }
                else
                {
                    hi = b;
                    b = a;
                    fb = fa;
                    a = hi - invPhi * (hi - lo);
                    fa = Density(a);
                }
            }

            return 0.5 * (lo + hi);
        }

        /// <summary>The height crossings found on a 1000-step grid of this shape as it is (datum's method).</summary>
        private (double Left, double Right) GridCrossings(double fraction)
        {
            double lo = Mu - 8.0 * Sigma;
            double hi = Mu + 8.0 * Sigma + Tau * (Math.Max(0.0, -Math.Log(fraction)) + 2.0);
            const int steps = 1000;
            double dt = (hi - lo) / steps;

            double peakHeight = 0.0;
            int peakIdx = 0;
            var g = new double[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                g[i] = Density(lo + i * dt);
                if (g[i] > peakHeight)
                {
                    peakHeight = g[i];
                    peakIdx = i;
                }
            }

            double target = fraction * peakHeight;

            double left = lo;
            for (int i = peakIdx; i > 0; i--)
            {
                if (g[i] >= target && g[i - 1] < target)
                {
                    double f = (target - g[i - 1]) / (g[i] - g[i - 1]);
                    left = lo + (i - 1 + f) * dt;
                    break;
                }
            }

            double right = hi;
            for (int i = peakIdx; i < steps; i++)
            {
                if (g[i] >= target && g[i + 1] < target)
                {
                    double f = (g[i] - target) / (g[i] - g[i + 1]);
                    right = lo + (i + f) * dt;
                    break;
                }
            }

            return (left, right);
        }
    }
}
