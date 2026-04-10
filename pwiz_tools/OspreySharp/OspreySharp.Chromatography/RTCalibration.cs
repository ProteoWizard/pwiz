using System;
using System.Collections.Generic;

namespace pwiz.OspreySharp.Chromatography
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

        /// <summary>Create default configuration.</summary>
        public RTCalibratorConfig()
        {
            Bandwidth = 0.3;
            Degree = 1;
            MinPoints = 20;
            RobustnessIterations = 2;
            OutlierRetention = 0.8;
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

            // Sort by library RT
            int n = libraryRts.Length;
            int[] order = new int[n];
            for (int i = 0; i < n; i++)
                order[i] = i;
            Array.Sort(order, (a, b) => libraryRts[a].CompareTo(libraryRts[b]));

            double[] x = new double[n];
            double[] y = new double[n];
            for (int i = 0; i < n; i++)
            {
                x[i] = libraryRts[order[i]];
                y[i] = measuredRts[order[i]];
            }

            // Fit LOESS with robustness iterations
            LoessModel model = LoessRegression.Fit(x, y, _config.Bandwidth,
                _config.Degree, _config.RobustnessIterations);
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

            // Compute residuals and stats
            double[] residuals = new double[n];
            double[] absResiduals = new double[n];
            for (int i = 0; i < n; i++)
            {
                residuals[i] = y[i] - fitted[i];
                absResiduals[i] = Math.Abs(residuals[i]);
            }
            double residualSD = LoessRegression.StdDev(residuals);

            return new RTCalibration(x, y, fitted, absResiduals, _config.Bandwidth,
                _config.Degree, residualSD);
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
        private readonly double _bandwidth;
        private readonly int _degree;
        private readonly double _residualSD;
        private readonly LoessModel _model;

        internal RTCalibration(double[] libraryRts, double[] measuredRts,
            double[] fittedValues, double[] absResiduals,
            double bandwidth, int degree, double residualSD)
        {
            _libraryRts = libraryRts;
            _measuredRts = measuredRts;
            _fittedValues = fittedValues;
            _absResiduals = absResiduals;
            _bandwidth = bandwidth;
            _degree = degree;
            _residualSD = residualSD;
            _model = new LoessModel(libraryRts, fittedValues);
        }

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
                0.3, 1, residualSD);
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
                if (retentionTimes[i] < minRt) minRt = retentionTimes[i];
                if (retentionTimes[i] > maxRt) maxRt = retentionTimes[i];
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
