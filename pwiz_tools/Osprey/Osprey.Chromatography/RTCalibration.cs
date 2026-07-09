/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4) <noreply .at. anthropic.com>
 *
 * Based on osprey (https://github.com/MacCossLab/osprey)
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
using System.Collections.Generic;
using System.Linq;

namespace pwiz.Osprey.Chromatography
{
    /// <summary>
    /// RT calibration configuration.
    /// Maps to RTCalibratorConfig in osprey-chromatography/src/calibration/rt.rs.
    /// </summary>
    public class RTCalibratorConfig
    {
        /// <summary>Bandwidth parameter for LOESS (fraction of data for each local fit).</summary>
        public double Bandwidth { get; set; }

        /// <summary>Polynomial degree for local fits (0 = constant, 1 = linear).</summary>
        public int Degree { get; set; }

        /// <summary>Minimum number of points required for calibration.</summary>
        public int MinPoints { get; set; }

        /// <summary>Number of robustness iterations (0 = no robustness weighting).</summary>
        public int RobustnessIterations { get; set; }

        /// <summary>
        /// Fraction of points to retain after outlier removal (0.8 = keep best 80%).
        /// Set to 1.0 to disable outlier removal.
        /// </summary>
        public double OutlierRetention { get; set; }

        /// <summary>
        /// When true (default), residuals are recomputed from the current fit
        /// each iteration, which is the classical Cleveland (1979) robust
        /// LOESS algorithm. When false, robustness iterations reuse absolute
        /// residuals from the initial fit throughout the loop (legacy
        /// behavior). Default matches Rust calibration_ml.rs v26.3.1 and
        /// later; override via OSPREY_LOESS_CLASSICAL_ROBUST=0 in both tools
        /// together when validating against legacy behavior.
        /// </summary>
        public bool ClassicalRobustIterations { get; set; }

        /// <summary>
        /// Fit a single global least-squares line instead of a LOESS curve, and
        /// report the calibration as <see cref="RTCalibrationMethod.Linear"/>.
        /// LOESS bandwidth is a *fraction* of the points, so its local window
        /// (<c>Bandwidth * n</c>) thins out as n falls; below ~100 points the
        /// window no longer supports a locally varying fit and a line is the
        /// better-conditioned estimator. <see cref="Bandwidth"/>,
        /// <see cref="Degree"/> and <see cref="RobustnessIterations"/> are ignored
        /// when this is set. See issue #4401.
        /// </summary>
        public bool LinearFit { get; set; }

        /// <summary>Create default configuration.</summary>
        public RTCalibratorConfig()
        {
            Bandwidth = 0.3;
            Degree = 1;
            MinPoints = 20;
            RobustnessIterations = 2;
            OutlierRetention = 0.8;
            ClassicalRobustIterations = true;
            LinearFit = false;
        }
    }

    /// <summary>
    /// RT Calibrator using LOESS regression.
    /// Maps to RTCalibrator in osprey-chromatography/src/calibration/rt.rs.
    /// </summary>
    public class RTCalibrator
    {
        private readonly RTCalibratorConfig _config;

        /// <summary>Create a new RT calibrator with default settings.</summary>
        public RTCalibrator() : this(new RTCalibratorConfig()) { }

        /// <summary>Create a new RT calibrator with custom settings.</summary>
        public RTCalibrator(RTCalibratorConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// Fit a calibration curve from (library_rt, measured_rt) pairs.
        /// </summary>
        /// <param name="libraryRts">Library retention times (iRT or predicted).</param>
        /// <param name="measuredRts">Corresponding measured retention times.</param>
        /// <returns>A fitted RTCalibration for predicting measured RT from library RT.</returns>
        public RTCalibration Fit(double[] libraryRts, double[] measuredRts)
        {
            if (libraryRts == null || measuredRts == null)
                throw new ArgumentNullException(libraryRts == null ? "libraryRts" : "measuredRts");
            if (libraryRts.Length != measuredRts.Length)
                throw new ArgumentException(string.Format(
                    "RT arrays must have same length: {0} vs {1}",
                    libraryRts.Length, measuredRts.Length));
            if (libraryRts.Length < _config.MinPoints)
                throw new ArgumentException(string.Format(
                    "Need at least {0} calibration points, got {1}",
                    _config.MinPoints, libraryRts.Length));

            // Sort by library RT (stable). Rust's slice::sort_by is stable; C#
            // Array.Sort with a Comparison<T> is introsort, which is UNSTABLE
            // and reorders duplicate keys. Multi-charge peptides share a library
            // RT, so an unstable sort drops a different y-value into position
            // for each duplicate and the subsequent LOESS fit diverges from
            // Rust. LINQ OrderBy is stable and matches Rust.
            int n = libraryRts.Length;
            // Sort by (libraryRt, measuredRt) so the outer x/y arrays match
            // the inner sort inside LoessRegression.Fit (which also uses
            // (x, y) for duplicate-x determinism). Without the secondary
            // key, outer y[i] and inner fitted[i] can end up corresponding
            // to different data points at duplicate-x positions, producing
            // swapped abs_residuals[i] in the calibration model.
            int[] order = Enumerable.Range(0, n)
                .OrderBy(i => libraryRts[i])
                .ThenBy(i => measuredRts[i])
                .ToArray();

            double[] x = new double[n];
            double[] y = new double[n];
            for (int i = 0; i < n; i++)
            {
                x[i] = libraryRts[order[i]];
                y[i] = measuredRts[order[i]];
            }

            // A global least-squares line for point sets too thin to support a
            // locally varying fit (see RTCalibratorConfig.LinearFit). The fitted
            // values are evaluated at the same x, so every downstream consumer --
            // Predict, InversePredict, the .calibration.json model params, resume
            // and the HPC merge -- is identical to the LOESS path.
            if (_config.LinearFit)
            {
                double[] linearFitted = FitRobustLine(x, y);
                return BuildCalibration(x, y, linearFitted, RTCalibrationMethod.Linear);
            }

            // Fit LOESS with robustness iterations
            LoessModel model = LoessRegression.Fit(x, y, _config.Bandwidth,
                _config.Degree, _config.RobustnessIterations,
                _config.ClassicalRobustIterations);
            double[] fitted = model.FittedY;

            // Outlier removal: remove worst fraction, refit
            if (_config.OutlierRetention < 1.0)
            {
                double[] absResid = new double[n];
                for (int i = 0; i < n; i++)
                    absResid[i] = Math.Abs(y[i] - fitted[i]);

                double threshold = LoessRegression.PercentileValue(absResid, _config.OutlierRetention);

                List<double> xFiltered = new List<double>();
                List<double> yFiltered = new List<double>();
                for (int i = 0; i < n; i++)
                {
                    if (absResid[i] <= threshold + 1e-12)
                    {
                        xFiltered.Add(x[i]);
                        yFiltered.Add(y[i]);
                    }
                }

                if (xFiltered.Count >= _config.MinPoints)
                {
                    x = xFiltered.ToArray();
                    y = yFiltered.ToArray();
                    n = x.Length;

                    // Refit on clean data (no robustness weights needed)
                    model = LoessRegression.Fit(x, y, _config.Bandwidth, _config.Degree, 0);
                    fitted = model.FittedY;
                }
            }

            return BuildCalibration(x, y, fitted, RTCalibrationMethod.LOESS);
        }

        /// <summary>
        /// Compute residual statistics from the fitted values and wrap them in an
        /// <see cref="RTCalibration"/>. Shared by the LOESS and linear paths.
        /// </summary>
        private static RTCalibration BuildCalibration(
            double[] x, double[] y, double[] fitted, RTCalibrationMethod method)
        {
            int n = x.Length;
            double[] residuals = new double[n];
            double[] absResiduals = new double[n];
            for (int i = 0; i < n; i++)
            {
                residuals[i] = y[i] - fitted[i];
                absResiduals[i] = Math.Abs(residuals[i]);
            }
            double residualSD = LoessRegression.StdDev(residuals);

            return new RTCalibration(x, y, fitted, absResiduals, residualSD, method);
        }

        /// <summary>
        /// Theil-Sen robust line through (x, y), evaluated at each x: the slope is the
        /// median of the pairwise slopes over all distinct-x pairs, and the intercept
        /// the median of <c>y - slope*x</c>.
        ///
        /// The linear tier fits point sets of a few dozen peptides. Each one cleared
        /// LDA + 1% FDR + S/N, but at that count the FDR estimate is granular, so one
        /// or two false positives can survive -- and ordinary least squares lets a
        /// single such point, if it sits at an RT extreme, lever the slope across the
        /// whole gradient. Theil-Sen has a ~29% breakdown point, so it shrugs those
        /// off. O(n^2) is trivial here (n &lt; ~100 gives under 5,000 pairs).
        ///
        /// A degenerate x (every library RT equal, so no distinct-x pair exists)
        /// yields a horizontal line at median(y) rather than dividing by zero.
        /// See issue #4401.
        /// </summary>
        private static double[] FitRobustLine(double[] x, double[] y)
        {
            int n = x.Length;
            var slopes = new List<double>(n * (n - 1) / 2);
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    double dx = x[j] - x[i];
                    if (dx != 0.0)
                        slopes.Add((y[j] - y[i]) / dx);
                }
            }

            double slope = slopes.Count > 0 ? Median(slopes) : 0.0;

            var offsets = new List<double>(n);
            for (int i = 0; i < n; i++)
                offsets.Add(y[i] - slope * x[i]);
            double intercept = Median(offsets);

            double[] fitted = new double[n];
            for (int i = 0; i < n; i++)
                fitted[i] = intercept + slope * x[i];
            return fitted;
        }

        /// <summary>
        /// Median of the values, averaging the two central elements on an even count.
        /// Sorts a copy, so the caller's list order is preserved.
        /// </summary>
        private static double Median(List<double> values)
        {
            var sorted = values.ToArray();
            Array.Sort(sorted); // Array.Sort OK: single primitive array sorted for a median; equal values are interchangeable so tie order cannot change the result
            int mid = sorted.Length / 2;
            return sorted.Length % 2 == 0
                ? (sorted[mid - 1] + sorted[mid]) / 2.0
                : sorted[mid];
        }
    }

    /// <summary>
    /// Fitted RT calibration curve. Predicts measured RT from library RT using
    /// interpolation on LOESS-fitted values.
    /// Maps to RTCalibration in osprey-chromatography/src/calibration/rt.rs.
    /// </summary>
    public class RTCalibration
    {
        private readonly double[] _libraryRts;
        private readonly double[] _measuredRts;
        private readonly double[] _fittedValues;
        private readonly double[] _absResiduals;
        private readonly double _residualSD;
        private readonly LoessModel _model;

        internal RTCalibration(double[] libraryRts, double[] measuredRts,
            double[] fittedValues, double[] absResiduals,
            double residualSD, RTCalibrationMethod method)
        {
            _libraryRts = libraryRts;
            _measuredRts = measuredRts;
            _fittedValues = fittedValues;
            _absResiduals = absResiduals;
            _residualSD = residualSD;
            _model = new LoessModel(libraryRts, fittedValues);
            Method = method;
        }

        /// <summary>
        /// How the fitted values were produced. Prediction is knot interpolation in
        /// every case, so this is reporting metadata (it reaches
        /// <c>.calibration.json</c>'s <c>rt_calibration.method</c>), not behaviour.
        /// A calibration rebuilt by <see cref="FromModelParams"/> reports
        /// <see cref="RTCalibrationMethod.LOESS"/> because the saved model is just
        /// the knots and no longer records how they were fitted.
        /// </summary>
        public RTCalibrationMethod Method { get; private set; }

        /// <summary>Library retention times (sorted).</summary>
        public double[] LibraryRts { get { return _libraryRts; } }

        /// <summary>Fitted values at calibration points.</summary>
        public double[] FittedValues { get { return _fittedValues; } }

        /// <summary>Absolute residuals at each calibration point.</summary>
        public double[] AbsResiduals { get { return _absResiduals; } }

        /// <summary>Residual standard deviation.</summary>
        public double ResidualSD { get { return _residualSD; } }

        /// <summary>Predict measured RT from library RT.</summary>
        public double Predict(double libraryRt)
        {
            return _model.Predict(libraryRt);
        }

        /// <summary>
        /// Inverse predict: convert measured RT back to library RT space.
        /// </summary>
        public double InversePredict(double measuredRt)
        {
            return _model.InversePredict(measuredRt);
        }

        /// <summary>
        /// Get local RT tolerance at a given library RT.
        /// Interpolates smoothed absolute residuals and applies a multiplier.
        /// </summary>
        /// <param name="libraryRt">Library retention time to query.</param>
        /// <param name="factor">Multiplier for local residual (typically 3.0).</param>
        /// <param name="minTolerance">Minimum tolerance floor in minutes.</param>
        /// <returns>Local RT tolerance in minutes.</returns>
        public double LocalTolerance(double libraryRt, double factor, double minTolerance)
        {
            double localResidual = InterpolateAbsResidual(libraryRt);
            return Math.Max(localResidual * factor, minTolerance);
        }

        /// <summary>Get calibration quality statistics.</summary>
        public RTCalibrationStats Stats()
        {
            int n = _libraryRts.Length;
            double[] residuals = new double[n];
            for (int i = 0; i < n; i++)
                residuals[i] = _measuredRts[i] - _fittedValues[i];

            double meanResidual = 0;
            if (n > 0)
            {
                double sum = 0;
                for (int i = 0; i < n; i++)
                    sum += residuals[i];
                meanResidual = sum / n;
            }

            double maxResidual = 0;
            for (int i = 0; i < n; i++)
                maxResidual = Math.Max(maxResidual, Math.Abs(residuals[i]));

            // Compute R-squared
            double yMean = 0;
            for (int i = 0; i < n; i++)
                yMean += _measuredRts[i];
            yMean /= n;

            double ssTot = 0, ssRes = 0;
            for (int i = 0; i < n; i++)
            {
                ssTot += (_measuredRts[i] - yMean) * (_measuredRts[i] - yMean);
                ssRes += residuals[i] * residuals[i];
            }
            double rSquared = ssTot > 1e-10 ? 1.0 - ssRes / ssTot : 0.0;

            double p20AbsResidual = LoessRegression.PercentileValue(_absResiduals, 0.20);
            double p80AbsResidual = LoessRegression.PercentileValue(_absResiduals, 0.80);
            double mad = LoessRegression.PercentileValue(_absResiduals, 0.50);

            return new RTCalibrationStats
            {
                NPoints = n,
                ResidualSD = _residualSD,
                MeanResidual = meanResidual,
                MaxResidual = maxResidual,
                RSquared = rSquared,
                P20AbsResidual = p20AbsResidual,
                P80AbsResidual = p80AbsResidual,
                MAD = mad
            };
        }

        /// <summary>
        /// The final RT search-window half-width (minutes) actually used by the
        /// main search for a given robust-spread MAD: <c>3 * MAD * 1.4826</c>
        /// clamped to <c>[minTolerance, maxTolerance]</c>. This is the single
        /// definition shared by the scoring path (<c>ScoringPipeline</c>), the
        /// persisted calibration JSON (<c>RTCalibrationJson</c>), and the console
        /// calibration summary, so all three report the same number. MAD*1.4826
        /// approximates a robust SD; 3x covers ~99.7% of a normal spread.
        /// </summary>
        public static double SearchWindowHalfWidth(double mad, double minTolerance, double maxTolerance)
        {
            return Math.Max(minTolerance, Math.Min(maxTolerance, SearchWindowRaw(mad)));
        }

        /// <summary>
        /// <see cref="SearchWindowHalfWidth(double,double,double)"/> with the floor
        /// widened for a calibration fitted from few points, via
        /// <see cref="EffectiveMinRtTolerance"/>.
        /// </summary>
        public static double SearchWindowHalfWidth(double mad, int nPoints,
            double minTolerance, double maxTolerance, int minCalibrationPoints)
        {
            return SearchWindowHalfWidth(mad,
                EffectiveMinRtTolerance(nPoints, minTolerance, maxTolerance, minCalibrationPoints),
                maxTolerance);
        }

        /// <summary>
        /// The minimum RT search-window half-width appropriate for a calibration
        /// fitted from <paramref name="nPoints"/> points.
        ///
        /// The configured <c>MinRtTolerance</c> (0.5 min) is the floor that suits a
        /// *well-estimated* MAD -- one measured from at least
        /// <paramref name="minCalibrationPoints"/> confident peptides. The sampling
        /// error of a scale estimate shrinks like <c>1/sqrt(n)</c>, so from a thin
        /// point set the MAD can come out small by luck, and clamping to 0.5 min
        /// would pair a tight window with a fit that does not deserve one. Widen the
        /// floor by <c>sqrt(minCalibrationPoints / n)</c> to compensate: the fit
        /// still tightens the window, but only as far as its own precision supports.
        ///
        /// Reduces exactly to <paramref name="minTolerance"/> at
        /// <c>n &gt;= minCalibrationPoints</c>, so a healthy calibration is unaffected.
        /// Never exceeds <paramref name="maxTolerance"/>. See issue #4401.
        /// </summary>
        public static double EffectiveMinRtTolerance(int nPoints, double minTolerance,
            double maxTolerance, int minCalibrationPoints)
        {
            if (nPoints <= 0 || nPoints >= minCalibrationPoints)
                return minTolerance;

            double inflated = minTolerance * Math.Sqrt((double)minCalibrationPoints / nPoints);
            return Math.Min(inflated, maxTolerance);
        }

        /// <summary>
        /// The unclamped RT search tolerance (minutes) for a given robust-spread
        /// MAD: <c>3 * MAD * 1.4826</c>, before the <c>[minTolerance, maxTolerance]</c>
        /// clamp in <see cref="SearchWindowHalfWidth(double,double,double)"/> is
        /// applied. Shared so the
        /// console summary can report the computed tolerance alongside the clamped
        /// one (e.g. when the fit is tighter than the floor).
        /// </summary>
        public static double SearchWindowRaw(double mad)
        {
            double robustSd = mad * 1.4826;
            return robustSd * 3.0;
        }

        /// <summary>Get the library RT range used for calibration.</summary>
        public void LibraryRtRange(out double min, out double max)
        {
            if (_libraryRts.Length == 0)
            {
                min = 0; max = 0;
                return;
            }
            min = _libraryRts[0];
            max = _libraryRts[_libraryRts.Length - 1];
        }

        /// <summary>Check if a library RT is within the calibration range.</summary>
        public bool IsWithinRange(double libraryRt)
        {
            if (_libraryRts.Length == 0)
                return false;
            return libraryRt >= _libraryRts[0] && libraryRt <= _libraryRts[_libraryRts.Length - 1];
        }

        /// <summary>
        /// The library-to-mzML RT mapping derived from the two RT *ranges*
        /// (<c>slope * libraryRt + intercept</c>), as a predict-only calibration.
        ///
        /// This is what the search should centre its RT window on when calibration
        /// fails. Without it the search falls back to the raw library RT -- fine when
        /// the two RT scales already agree (the mapping is then the identity and this
        /// changes nothing), but badly wrong when they do not, e.g. a Carafe library
        /// whose Tr_recalibrated is in seconds against a minutes-keyed mzML.
        ///
        /// Carries no residuals, so it must not be used to derive a search tolerance
        /// or reported as a successful calibration -- the caller keeps using the
        /// fallback tolerance and records calibration_successful=false. Returns null
        /// for a degenerate library RT range. See issue #4401.
        /// </summary>
        public static RTCalibration FromLinearMapping(
            double libMinRt, double libMaxRt, double slope, double intercept)
        {
            if (!(libMaxRt > libMinRt))
                return null;

            double[] libraryRts = { libMinRt, libMaxRt };
            double[] fitted = { intercept + slope * libMinRt, intercept + slope * libMaxRt };
            double[] absResiduals = { 0.0, 0.0 };
            return new RTCalibration(libraryRts, fitted, fitted, absResiduals, 0.0,
                RTCalibrationMethod.Linear);
        }

        /// <summary>
        /// Reconstruct an RTCalibration from saved model parameters.
        /// </summary>
        public static RTCalibration FromModelParams(double[] libraryRts, double[] fittedRts,
            double[] absResiduals, double residualSD)
        {
            if (libraryRts == null || fittedRts == null)
                throw new ArgumentNullException(libraryRts == null ? "libraryRts" : "fittedRts");
            if (libraryRts.Length != fittedRts.Length)
                throw new ArgumentException("libraryRts and fittedRts must have same length");
            if (libraryRts.Length == 0)
                throw new ArgumentException("Model params have no calibration points");

            // Handle backwards compatibility: if absResiduals not present, use uniform residualSD
            double[] residuals = absResiduals != null && absResiduals.Length == libraryRts.Length
                ? absResiduals
                : CreateUniformArray(libraryRts.Length, residualSD);

            return new RTCalibration(libraryRts, fittedRts, fittedRts, residuals,
                residualSD, RTCalibrationMethod.LOESS);
        }

        private double InterpolateAbsResidual(double libraryRt)
        {
            int n = _libraryRts.Length;
            if (n == 0)
                return _residualSD;

            int idx = Array.BinarySearch(_libraryRts, libraryRt);
            if (idx < 0)
                idx = ~idx;

            if (idx == 0)
                return SmoothedAbsResidual(0);
            if (idx >= n)
                return SmoothedAbsResidual(n - 1);

            double x0 = _libraryRts[idx - 1];
            double x1 = _libraryRts[idx];
            double r0 = SmoothedAbsResidual(idx - 1);
            double r1 = SmoothedAbsResidual(idx);

            if (Math.Abs(x1 - x0) < 1e-12)
                return (r0 + r1) / 2.0;

            double t = (libraryRt - x0) / (x1 - x0);
            return r0 + t * (r1 - r0);
        }

        private double SmoothedAbsResidual(int idx)
        {
            int start = Math.Max(0, idx - 2);
            int end = Math.Min(idx + 3, _absResiduals.Length);
            double sum = 0;
            for (int i = start; i < end; i++)
                sum += _absResiduals[i];
            return sum / (end - start);
        }

        private static double[] CreateUniformArray(int length, double value)
        {
            double[] arr = new double[length];
            for (int i = 0; i < length; i++)
                arr[i] = value;
            return arr;
        }
    }

    /// <summary>
    /// Statistics for RT calibration quality.
    /// Maps to RTCalibrationStats in osprey-chromatography/src/calibration/rt.rs.
    /// </summary>
    public class RTCalibrationStats
    {
        /// <summary>Number of calibration points.</summary>
        public int NPoints { get; set; }

        /// <summary>Residual standard deviation.</summary>
        public double ResidualSD { get; set; }

        /// <summary>Mean residual (should be near 0).</summary>
        public double MeanResidual { get; set; }

        /// <summary>Maximum absolute residual.</summary>
        public double MaxResidual { get; set; }

        /// <summary>R-squared (coefficient of determination).</summary>
        public double RSquared { get; set; }

        /// <summary>20th percentile of absolute residuals.</summary>
        public double P20AbsResidual { get; set; }

        /// <summary>80th percentile of absolute residuals.</summary>
        public double P80AbsResidual { get; set; }

        /// <summary>Median absolute deviation (MAD) of residuals.</summary>
        public double MAD { get; set; }
    }

    /// <summary>
    /// Stratified sampler for RT calibration discovery.
    /// Distributes samples evenly across RT bins for representative calibration.
    /// Maps to RTStratifiedSampler in osprey-chromatography/src/calibration/rt.rs.
    /// </summary>
    public class RTStratifiedSampler
    {
        private int _nBins;
        private int _peptidesPerBin;
        private int _minCalibrationPoints;

        /// <summary>Create a new stratified sampler with default settings.</summary>
        public RTStratifiedSampler()
        {
            _nBins = 10;
            _peptidesPerBin = 100;
            _minCalibrationPoints = 50;
        }

        /// <summary>Number of RT bins for stratification.</summary>
        public int NBins
        {
            get { return _nBins; }
            set { _nBins = value; }
        }

        /// <summary>Target peptides per bin.</summary>
        public int PeptidesPerBin
        {
            get { return _peptidesPerBin; }
            set { _peptidesPerBin = value; }
        }

        /// <summary>Minimum total calibration points.</summary>
        public int MinCalibrationPoints
        {
            get { return _minCalibrationPoints; }
            set { _minCalibrationPoints = value; }
        }

        /// <summary>
        /// Sample library entries stratified by RT.
        /// Returns indices of entries to use for calibration discovery.
        /// </summary>
        public int[] Sample(double[] retentionTimes)
        {
            if (retentionTimes == null || retentionTimes.Length == 0)
                return new int[0];

            double minRt = retentionTimes[0], maxRt = retentionTimes[0];
            for (int i = 1; i < retentionTimes.Length; i++)
            {
                if (retentionTimes[i] < minRt)
                    minRt = retentionTimes[i];
                if (retentionTimes[i] > maxRt)
                    maxRt = retentionTimes[i];
            }

            if (maxRt - minRt < 1e-10)
            {
                int count = Math.Min(retentionTimes.Length, _peptidesPerBin);
                int[] result = new int[count];
                for (int i = 0; i < count; i++)
                    result[i] = i;
                return result;
            }

            double binWidth = (maxRt - minRt) / _nBins;

            // Assign entries to bins
            List<int>[] bins = new List<int>[_nBins];
            for (int i = 0; i < _nBins; i++)
                bins[i] = new List<int>();

            for (int i = 0; i < retentionTimes.Length; i++)
            {
                int bin = (int)((retentionTimes[i] - minRt) / binWidth);
                bin = Math.Min(bin, _nBins - 1);
                bins[bin].Add(i);
            }

            // Sample from each bin
            List<int> sampled = new List<int>();
            for (int b = 0; b < _nBins; b++)
            {
                int nTake = Math.Min(bins[b].Count, _peptidesPerBin);
                for (int i = 0; i < nTake; i++)
                    sampled.Add(bins[b][i]);
            }

            return sampled.ToArray();
        }
    }
}
