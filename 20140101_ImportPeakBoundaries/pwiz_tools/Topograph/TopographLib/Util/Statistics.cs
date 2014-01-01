using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace pwiz.Topograph.Util
{
    /// <summary>
    /// Simple statistics utility class based on the CodeProject article:
    /// http://www.codeproject.com/KB/cs/csstatistics.aspx
    /// </summary>
    public class Statistics
    {
        private double[] _list;

        /// <summary>
        /// Constructor for statistics on a set of numbers.
        /// </summary>
        /// <param name="list">The set of numbers</param>
        public Statistics(params double[] list)
        {
            _list = list;
        }

        /// <summary>
        /// Change the set of numbers for which statistics are to be computed.
        /// </summary>
        /// <param name="list">New set of numbers</param>
        public void Update(params double[] list)
        {
            _list = list;
        }

        /// <summary>
        /// Count of the numbers in the set.
        /// </summary>
        public int Length
        {
            get { return _list.Length; }
        }

        /// <summary>
        /// Determines the minimum value in the set of numbers.
        /// </summary>
        /// <returns>Minimum value</returns>
        public double Min()
        {
            double minimum = double.PositiveInfinity;
            for (int i = 0; i <= _list.Length - 1; i++)
                if (_list[i] < minimum) minimum = _list[i];
            return minimum;
        }

        /// <summary>
        /// Determines the maximum value in the set of numbers.
        /// </summary>
        /// <returns>Maximum value</returns>
        public double Max()
        {
            double maximum = double.NegativeInfinity;
            for (int i = 0; i <= _list.Length - 1; i++)
                if (_list[i] > maximum) maximum = _list[i];
            return maximum;
        }

        /// <summary>
        /// Determines the first quartile value of the set of numbers.
        /// </summary>
        /// <returns>First quartile value</returns>
        public double Q1()
        {
            return Percentile(0.25);
        }

        /// <summary>
        /// Determines the median value of the set of numbers.
        /// </summary>
        /// <returns>Median value</returns>
        public double Median()
        {
            return Percentile(0.5);
        }

        /// <summary>
        /// Determines the third quatile value of the set of numbers.
        /// </summary>
        /// <returns>Third quartile value</returns>
        public double Q3()
        {
            return Percentile(0.75);
        }

        public double Sum()
        {
            double sum = 0;
            foreach (double value in _list)
                sum += value;
            return sum;
        }

        /// <summary>
        /// Calculates the mean average of the set of numbers.
        /// </summary>
        /// <returns>Mean</returns>
        public double Mean()
        {
            try
            {
                return Sum() / Length;
            }
            catch (Exception)
            {
                return double.NaN;
            }
        }

        /// <summary>
        /// Calculates a weighted mean average of the set of numbers.
        /// See:
        /// http://en.wikipedia.org/wiki/Weighted_mean
        /// </summary>
        /// <param name="weights">The weights</param>
        /// <returns>Weighted mean</returns>
        public double Mean(Statistics weights)
        {
            try
            {
                double sum = 0;
                for (int i = 0; i < _list.Length; i++)
                    sum += _list[i] * weights._list[i];
                return sum / weights.Sum();
            }
            catch (Exception)
            {
                return double.NaN;
            }
        }

        public double Median(Statistics weights)
        {
            try
            {
                var dict = new Dictionary<double, double>();
                for (int i = 0; i < _list.Length; i++)
                {
                    double value = _list[i];
                    double weight;
                    dict.TryGetValue(value, out weight);
                    weight += weights._list[i];
                    dict[value] = weight;
                }
                var keys = dict.Keys.ToArray();
                Array.Sort(keys);
                double total = weights.Sum();
                double sum = 0;
                for (int i = 0; i < keys.Length; i++)
                {
                    sum += dict[keys[i]];
                    if (sum >= total / 2)
                    {
                        return keys[i];
                    }
                }
                return double.NaN;
            }
            catch
            {
                return double.NaN;
            }
        }

        /// <summary>
        /// Calculates range (max - min) of the set of numbers.
        /// </summary>
        /// <returns>Range</returns>
        public double Range()
        {
            double minimum = Min();
            double maximum = Max();
            return (maximum - minimum);
        }

        /// <summary>
        /// Calculates the inter-quartile range (Q3 - Q1) of the set of numbers.
        /// </summary>
        /// <returns>Inter-quartile range</returns>
        public double InterQuartileRange()
        {
            return Q3() - Q1();
        }

        /// <summary>
        /// Calculates the mid-point of the range (min + max) / 2 of the
        /// set of numbers.
        /// </summary>
        /// <returns>Mid-point of range</returns>
        public double MiddleOfRange()
        {
            double minimum = Min();
            double maximum = Max();
            return (minimum + maximum) / 2;
        }

        /// <summary>
        /// Normalizes a the set of numbers to a unit vector.
        /// </summary>
        /// <returns>Normalized numbers</returns>
        public Statistics NormalizeUnit()
        {
            double[] normalized = new double[Length];
            try
            {
                double sum = Sum();
                for (int i = 0; i < normalized.Length; i++)
                    normalized[i] = _list[i] / sum;
            }
            catch (Exception)
            {
                for (int i = 0; i < normalized.Length; i++)
                    normalized[i] = double.NaN;
            }
            return new Statistics(normalized);
        }

        private double SumOfSquares()
        {
            double s = 0;
            foreach (double value in _list)
                s += Math.Pow(value, 2);
            return s;
        }

        private double VarianceTotal()
        {
            // Sometimes due to rounding errors, a zero variance will result in a very small negative value.
            return Math.Max(0, SumOfSquares() - _list.Length * Math.Pow(Mean(), 2));
        }

        /// <summary>
        /// Calculates the variance of the set of numbers.
        /// </summary>
        /// <returns>Variance</returns>
        public double Variance()
        {
            try
            {
                return VarianceTotal() / (_list.Length - 1);
            }
            catch (Exception)
            {
                return double.NaN;
            }
        }

        /// <summary>
        /// Calculates the variance for a set of numbers from a weighted mean.
        /// See:
        /// http://en.wikipedia.org/wiki/Weighted_mean
        /// </summary>
        /// <param name="weights">The weights</param>
        /// <returns>Variance from weighted mean</returns>
        public double Variance(Statistics weights)
        {
            try
            {
                double s = 0;
                for (int i = 0; i < _list.Length; i++)
                    s += weights._list[i] * Math.Pow(_list[i], 2);
                return (s / weights.Mean() - _list.Length * Math.Pow(Mean(weights), 2)) / (_list.Length - 1);
            }
            catch (Exception)
            {
                return double.NaN;
            }
        }

        /// <summary>
        /// Calculates the stadard deviation (sqrt(variance)) of the set
        /// of numbers.
        /// </summary>
        /// <returns>Standard deviation</returns>
        public double StdDev()
        {
            return Math.Sqrt(Variance());
        }

        /// <summary>
        /// Calculates the stadard deviation (sqrt(variance)) of the set
        /// of numbers from a weighted mean.
        /// </summary>
        /// <param name="weights">The weights</param>
        /// <returns>Standard deviation from weighted mean</returns>
        public double StdDev(Statistics weights)
        {
            return Math.Sqrt(Variance(weights));
        }

        public double StdErr()
        {
            return StdDev() / Math.Sqrt(_list.Length);
        }

        public double StdErr(Statistics weights)
        {
            return StdDev(weights) / Math.Sqrt(_list.Length);
        }

        /// <summary>
        /// Computes the index standard of a given member of the set of numbers.
        /// </summary>
        /// <param name="member">A member of the set</param>
        /// <returns>Index standard</returns>
        public double Z(double member)
        {
            try
            {
                if (_list.Contains(member))
                    return (member - Mean()) / StdDev();
                else
                    return double.NaN;
            }
            catch (Exception)
            {
                return double.NaN;
            }
        }

        /// <summary>
        /// Calculates the covariance between this and another set of numbers.
        /// </summary>
        /// <param name="s">Second set of numbers</param>
        /// <returns>Covariance</returns>
        public double Covariance(Statistics s)
        {
            return Covariance(this, s);
        }

        /// <summary>
        /// Calculates the covariance between two sets of numbers.
        /// </summary>
        /// <param name="s1">First set of numbers</param>
        /// <param name="s2">Second set of numbers</param>
        /// <returns></returns>
        public static double Covariance(Statistics s1, Statistics s2)
        {
            try
            {
                if (s1.Length != s2.Length)
                    return double.NaN;

                int len = s1.Length;
                double sumMul = 0;
                for (int i = 0; i < len; i++)
                    sumMul += (s1._list[i] * s2._list[i]);
                return (sumMul - len * s1.Mean() * s2.Mean()) / (len - 1);
            }
            catch (Exception)
            {
                return double.NaN;
            }
        }

        /// <summary>
        /// Calculates the correlation coefficient between this and
        /// another set of numbers. 
        /// </summary>
        /// <param name="s">Second set of numbers</param>
        /// <returns>Correlation coefficient</returns>
        public double R(Statistics s)
        {
            return R(this, s);
        }

        /// <summary>
        /// Calculates the correlation coefficient between two sets
        /// of numbers. 
        /// </summary>
        /// <param name="s1">First set of numbers</param>
        /// <param name="s2">Second set of numbers</param>
        /// <returns>Correlation coefficient</returns>
        public static double R(Statistics s1, Statistics s2)
        {
            try
            {
                return Covariance(s1, s2) / (s1.StdDev() * s2.StdDev());
            }
            catch (Exception)
            {
                return double.NaN;
            }
        }

        /// <summary>
        /// Calculates the Alpha coefficient (y-intercept) of the linear
        /// regression function using the current set of numbers as Y values
        /// and another set as X values.
        /// </summary>
        /// <param name="x">X values</param>
        /// <returns>The Alpha coefficient</returns>
        public double Alpha(Statistics x)
        {
            return Alpha(this, x);
        }

        /// <summary>
        /// Calculates the Alpha coefficient (y-intercept) of the linear
        /// regression function given the Y and X values.
        /// </summary>
        /// <param name="y">Y values</param>
        /// <param name="x">X values</param>
        /// <returns>The Alpha coefficient</returns>
        public static double Alpha(Statistics y, Statistics x)
        {
            return y.Mean() - Beta(y, x) * x.Mean();
        }

        /// <summary>
        /// Calculates the y-intercept (Alpha coefficient) of the linear
        /// regression function using the current set of numbers as Y values
        /// and another set as X values.
        /// </summary>
        /// <param name="x">X values</param>
        /// <returns>The y-intercept</returns>
        public double Intercept(Statistics x)
        {
            return Alpha(x);
        }

        /// <summary>
        /// Calculates the y-intercept (Alpha coefficient) of the linear
        /// regression function given the Y and X values.
        /// </summary>
        /// <param name="y">Y values</param>
        /// <param name="x">X values</param>
        /// <returns>The y-intercept</returns>
        public static double Intercept(Statistics y, Statistics x)
        {
            return Alpha(y, x);
        }

        /// <summary>
        /// Calculates the Beta coefficient (slope) of the linear regression function
        /// using the current set of numbers as Y values and another set
        /// as X values.
        /// </summary>
        /// <param name="x">X values</param>
        /// <returns>The Beta coefficient</returns>
        public double Beta(Statistics x)
        {
            return Beta(this, x);
        }

        /// <summary>
        /// Calculates the Beta coefficient (slope) of the linear regression function
        /// given the Y and X values.
        /// </summary>
        /// <param name="y">Y values</param>
        /// <param name="x">X values</param>
        /// <returns>The Beta coefficient</returns>
        public static double Beta(Statistics y, Statistics x)
        {
            try
            {
                return Covariance(y, x) / (Math.Pow(x.StdDev(), 2));
            }
            catch (Exception)
            {
                return double.NaN;
            }
        }

        public double SlopeWithoutIntercept(Statistics x)
        {
            double dotProduct = 0;
            for (int i = 0; i < Length; i++)
            {
                dotProduct += _list[i]*x._list[i];
            }
            return dotProduct/x.SumOfSquares();
        }


        /// <summary>
        /// Calculates the slope (Beta coefficient) of the linear regression function
        /// using the current set of numbers as Y values and another set
        /// as X values.
        /// </summary>
        /// <param name="x">X values</param>
        /// <returns>The slope</returns>
        public double Slope(Statistics x)
        {
            return Beta(x);
        }

        /// <summary>
        /// Calculates the slope (Beta coefficient) of the linear regression function
        /// given the Y and X values.
        /// </summary>
        /// <param name="y">Y values</param>
        /// <param name="x">X values</param>
        /// <returns>The slope</returns>
        public static double Slope(Statistics y, Statistics x)
        {
            return Beta(y, x);
        }

        /// <summary>
        /// Calculates the residuals of the linear regression function
        /// using the current set of numbers as Y values and another set
        /// as X values.
        /// </summary>
        /// <param name="x">X values</param>
        /// <returns>A set of residuals</returns>
        public Statistics Residuals(Statistics x)
        {
            return Residuals(this, x);
        }

        /// <summary>
        /// Calculates the residuals of the linear regression function
        /// given the Y and X values.
        /// </summary>
        /// <param name="y">Y values</param>
        /// <param name="x">X values</param>
        /// <returns>A set of residuals</returns>
        public static Statistics Residuals(Statistics y, Statistics x)
        {
            double a = Beta(y, x);
            double b = Alpha(y, x);

            List<double> residuals = new List<double>();
            for (int i = 0; i < x.Length; i++)
                residuals.Add(y._list[i] - (a * x._list[i] + b));
            return new Statistics(residuals.ToArray());
        }

        public static Statistics ResidualsWithoutIntercept(Statistics y, Statistics x)
        {
            double slope = y.SlopeWithoutIntercept(x);
            var residuals = new List<double>();
            for (int i = 0; i < x.Length; i++)
            {
                residuals.Add(y._list[i] - slope*x._list[i]);
            }
            return new Statistics(residuals.ToArray());
        }

        private static double StdDevY(Statistics y, Statistics x)
        {
            double s = 0;
            Statistics residuals = Residuals(y, x);
            foreach (double value in residuals._list)
                s += Math.Pow(value, 2);
            return Math.Sqrt(s / (residuals._list.Length - 2));
        }

        private static double StdDevYWithoutIntercept(Statistics y, Statistics x)
        {
            Statistics residuals = ResidualsWithoutIntercept(y, x);
            return Math.Sqrt(residuals.SumOfSquares()/(residuals._list.Length - 2));
        }

        public static double StdDevA(Statistics y, Statistics x)
        {
            try
            {
                return StdDevY(y, x) * Math.Sqrt(x.SumOfSquares() / (x._list.Length * x.VarianceTotal()));
            }
            catch (Exception)
            {
                return double.NaN;
            }
        }

        public static double StdDevB(Statistics y, Statistics x)
        {
            try
            {
                return StdDevY(y, x) / Math.Sqrt(x.VarianceTotal());
            }
            catch (Exception)
            {
                return double.NaN;
            }
        }

        public static double StdDevSlopeWithoutIntercept(Statistics y, Statistics x)
        {
            try
            {
                return StdDevYWithoutIntercept(y, x)/Math.Sqrt(x.SumOfSquares());
            }
            catch
            {
                return double.NaN;
            }
        }

        /// <summary>
        /// Calculates a percentile using <see cref="PercentileExcel"/>.
        /// </summary>
        /// <param name="p">Percentile in decimal form (e.g. 0.25)</param>
        /// <returns>Data value such that p percent are below the value</returns>
        public double Percentile(double p)
        {
            return PercentileExcel(p);
        }

        /// <summary>
        /// Calculates a percentile based on the Excel method.
        /// (See http://www.haiweb.org/medicineprices/manual/quartiles_iTSS.pdf)
        /// This method is currently used for all other statistical calculations
        /// that rely on percentiles (e.g. Q1, Q2, IQ, etc.)
        /// </summary>
        /// <param name="p">Percentile in decimal form (e.g. 0.25)</param>
        /// <returns>Data value such that p percent are below the value</returns>
        public double PercentileExcel(double p)
        {
            if (_list.Length == 0)
            {
                return double.NaN;
            }
            if (_list.Length == 1)
            {
                return _list[0];
            }
            try
            {
                int n = _list.Length;
                var ordered = new double[n];
                _list.CopyTo(ordered, 0);
                Array.Sort(ordered);

                double pos = (n - 1) * p;
                int j = (int)pos;
                double g = pos - j;

                return (1 - g) * ordered[j] + g * ordered[j + 1];
            }
            catch (Exception)
            {
                return double.NaN;
            }
        }

        /// <summary>
        /// Calculates the dot-product or cos(angle) between two vectors,
        /// using the square roots of the values in the vectors.
        /// </summary>
        /// <param name="s">The other vector</param>
        /// <returns>Dot-Product of square roots of values in vectors</returns>
        public double AngleSqrt(Statistics s)
        {
            var listNormal1 = new List<double>(_list);
            listNormal1 = listNormal1.ConvertAll(val => Math.Sqrt(val));
            var stat1 = new Statistics(listNormal1.ToArray());
            var listNormal2 = new List<double>(s._list);
            listNormal2 = listNormal2.ConvertAll(val => Math.Sqrt(val));
            var stat2 = new Statistics(listNormal2.ToArray());

            return stat1.Angle(stat2);
        }

        /// <summary>
        /// Calculates the dot-product or cos(angle) between two vectors,
        /// with both normalized to a unit vector first.
        /// </summary>
        /// <param name="s">The other vector</param>
        /// <returns>Dot-Product of normalized vectors</returns>
        public double AngleUnitVector(Statistics s)
        {
            var stat1 = NormalizeUnit();
            var stat2 = s.NormalizeUnit();

            return stat1.Angle(stat2);
        }

        /// <summary>
        /// Calculates the dot-product or cos(angle) between two vectors.
        /// See:
        /// http://en.wikipedia.org/wiki/Dot_product
        /// </summary>
        /// <param name="s">The other vector</param>
        /// <returns>Dot-Product</returns>
        public double Angle(Statistics s)
        {
            Debug.Assert(Length == s.Length);
            double sumCross = 0;
            double sumLeft = 0;
            double sumRight = 0;

            for (int i = 0, len = Length; i < len; i++)
            {
                double left = _list[i];
                double right = s._list[i];

                sumCross += left * right;
                sumLeft += left * left;
                sumRight += right * right;
            }

            return sumCross / Math.Sqrt(sumLeft * sumRight);
        }

        /// <summary>
        /// Calculates the rank orders of the values in the list.
        /// </summary>
        /// <returns>Rank order</returns>
        public int[] Rank()
        {
            // Sort into rank order, storing original indices
            var listValueIndices = new List<KeyValuePair<double, int>>();
            for (int i = 0; i < _list.Length; i++)
                listValueIndices.Add(new KeyValuePair<double, int>(_list[i], i));
            listValueIndices.Sort((p1, p2) => Comparer<double>.Default.Compare(p2.Key, p1.Key));
            // Record the sorted rank order
            var ranks = new int[_list.Length];
            for (int i = 0; i < _list.Length; i++)
                ranks[listValueIndices[i].Value] = i + 1;
            return ranks;
        }

        /// <summary>
        /// Calculates a Costa Soares correlation coefficient between this and
        /// another set of numbers. 
        /// </summary>
        /// <param name="s">Second set of numbers</param>
        /// <returns>Correlation coefficient</returns>
        public double CostaSoares(Statistics s)
        {
            return CostaSoares(s, int.MaxValue);
        }

        /// <summary>
        /// Calculates a Costa Soares correlation coefficient between this and
        /// another set of numbers. 
        /// </summary>
        /// <param name="s">Second set of numbers</param>
        /// <param name="limitRank">Exclude pairs where both rank below this limit</param>
        /// <returns>Correlation coefficient</returns>
        public double CostaSoares(Statistics s, int limitRank)
        {
            Debug.Assert(Length == s.Length);

            int n = Length;

            int[] a = Rank();
            int[] b = s.Rank();

            a = FixZeroRanks(a, s, b);
            b = s.FixZeroRanks(b, this, a);

            double total = 0;

            for (int i = 0; i < n; i++)
            {
                if (a[i] <= limitRank || b[i] <= limitRank)
                    total += Math.Pow(a[i] - b[i], 2) * ((n - a[i] + 1) + (n - b[i] + 1));
            }

            double n2 = n * n;
            double n3 = n * n2;
            double n4 = n * n3;
            total *= 6.0 / (n4 + n3 - n2 - n);
            total = 1 - total;

            return total;
        }

        private int[] FixZeroRanks(int[] ranks, Statistics sOther, int[] ranksOther)
        {
            if (!_list.Contains(0))
                return ranks;

            var listNewValues = new List<double>();
            foreach (int rank in ranks)
                listNewValues.Add(rank);

            var listRankOtherIndices = new List<KeyValuePair<int, int>>();
            for (int i = 0; i < _list.Length; i++)
            {
                // Look for zero scores
                if (_list[i] == 0)
                {
                    // If the other is also zero, just match the rankings.
                    // Otherwise, save this index for to determine its new rank.
                    if (sOther._list[i] == 0)
                        listNewValues[i] = ranksOther[i];
                    else
                        listRankOtherIndices.Add(new KeyValuePair<int, int>(ranksOther[i], i));
                }
            }
            // Sort by the rank in the other set
            listRankOtherIndices.Sort((p1, p2) => Comparer<int>.Default.Compare(p1.Key, p2.Key));
            // Make the highest ranked in the other set have the lowest rank in this set
            int rankNew = Length + listRankOtherIndices.Count;
            foreach (var pair in listRankOtherIndices)
                listNewValues[pair.Value] = rankNew--;

            // Finally convert ranks to values by reversing numeric order
            for (int i = 0; i < listNewValues.Count; i++)
                listNewValues[i] = -listNewValues[i];
            // And re-rank
            return new Statistics(listNewValues.ToArray()).Rank();
        }
        ///<summary>
        ///This subroutine determines the best fit line by minimizing the sum of the squares
        ///of the perpendicular distances of the points to the line.
        ///This was initially reported by Kermack and Haldane (1950) Biometrika, 37, 30.
        ///However I found it in York, D. (1966) Canadian Journal of Physics, vol 44, p 1079.
        ///</summary>
        public static LinearRegression LinearRegressionWithErrorsInBothCoordinates(Statistics a, Statistics b)
        {
            double meanA = a.Mean();
            double meanB = b.Mean();
            double sA2 = 0;
            double sB2 = 0;
            double sAb = 0;

            for (int i = 0; i < a.Length; i++)
            {
                double dA = a._list[i] - meanA;
                double dB = b._list[i] - meanB;

                sA2 += dA * dA;
                sB2 += dB * dB;
                sAb += dA * dB;
            }
            LinearRegression result = new LinearRegression();
            if (sA2 > 0 && sB2 > 0 && sAb > 0)
            {
                result.Correlation = sAb / Math.Sqrt(sA2 * sB2);
                result.Slope = (sB2 - sA2 + Math.Sqrt((sB2 - sA2) * (sB2 - sA2)
                                               + 4 * (sAb * sAb))) / 2 / sAb;
                result.Intercept = meanB - result.Slope * meanA;
                if (result.Correlation < 1)
                {
                    result.SlopeError = (result.Slope / result.Correlation) * Math.Sqrt((1 - (result.Correlation * result.Correlation)) / a.Length);
                }
                else
                {
                    result.SlopeError = 0;
                }
            }
            else
            {
                result.Correlation = 0;
                result.Slope = 0;
                result.SlopeError = 0;
                result.Intercept = 0;
            }
            return result;
        }
    }
}
