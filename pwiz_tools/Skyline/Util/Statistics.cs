using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace pwiz.Skyline.Util
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
        
        public Statistics(IEnumerable<double> list)
        {
            _list = list.ToArray();
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
        /// Calculates the mode (most frequently occurring number) in the set.
        /// </summary>
        /// <returns>Mode of the set of numbers</returns>
        public double Mode()
        {
            try
            {
                var i = new double[_list.Length];
                _list.CopyTo(i, 0);
                Array.Sort(i);
                double valMode = i[0], helpValMode = i[0];
                int oldCounter = 0, newCounter = 0;
                int j = 0;
                for (; j <= i.Length - 1; j++)
                {
                    if (i[j] == helpValMode)
                        newCounter++;
                    else if (newCounter > oldCounter)
                    {
                        valMode = helpValMode;
                        oldCounter = newCounter;
                        newCounter = 1;
                        helpValMode = i[j];
                    }
                    else if (newCounter == oldCounter)
                    {
                        valMode = double.NaN;
                        helpValMode = i[j];
                        newCounter = 1;
                    }
                    else
                    {
                        helpValMode = i[j];
                        newCounter = 1;
                    }
                }
                if (newCounter > oldCounter)
                    valMode = helpValMode;
                else if (newCounter == oldCounter)
                    valMode = double.NaN;
                return valMode;
            }
            catch (Exception)
            {
                return double.NaN;
            }
        }

        /// <summary>
        /// Calculates the mode or modes (most frequently occurring number(s)) in the set.
        /// </summary>
        /// <returns>List of the modes the set of numbers</returns>
        public double[] Modes()
        {
            try
            {
                var i = new double[_list.Length];
                _list.CopyTo(i, 0);
                Array.Sort(i);
                List<double> listModes = new List<double>();
                double helpValMode = i[0];
                int oldCounter = 0, newCounter = 0;
                int j = 0;
                for (; j <= i.Length - 1; j++)
                {
                    if (i[j] == helpValMode)
                        newCounter++;
                    else if (newCounter > oldCounter)
                    {
                        listModes.Clear();
                        listModes.Add(helpValMode);

                        oldCounter = newCounter;
                        newCounter = 1;
                        helpValMode = i[j];
                    }
                    else if (newCounter == oldCounter)
                    {
                        listModes.Add(helpValMode);

                        helpValMode = i[j];
                        newCounter = 1;
                    }
                    else
                    {
                        helpValMode = i[j];
                        newCounter = 1;
                    }
                }
                if (newCounter > oldCounter)
                {
                    listModes.Clear();
                    listModes.Add(helpValMode);
                }
                else if (newCounter == oldCounter)
                    listModes.Add(helpValMode);
                return listModes.ToArray();
            }
            catch (Exception)
            {
                return new double[0];
            }
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

        /// <summary>
        /// Sum of all the values in a set of numbers.
        /// </summary>
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
                return Sum()/Length;
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
        public double IQ()
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
            return (minimum + maximum)/2;
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
                    normalized[i] = _list[i]/sum;
            }
            catch (Exception)
            {
                for (int i = 0; i < normalized.Length; i++)
                    normalized[i] = double.NaN;
            }
            return new Statistics(normalized);
        }

        /// <summary>
        /// Base statistical value used in calculating variance and standard deviations.
        /// </summary>
        private double SumOfSquares()
        {
            double s = 0;
            foreach (double value in _list)
                s += Math.Pow(value, 2);
            return s;
        }

        /// <summary>
        /// Base statistical value used in calculating variance and standard deviations.
        /// <para>
        /// Simple "Naive" algorithm has inherent numeric instability.  See:
        /// http://en.wikipedia.org/wiki/Algorithms_for_calculating_variance
        /// </para>
        /// </summary>
        private double VarianceTotal()
        {
//            return (SumOfSquares() - _list.Length * Math.Pow(Mean(), 2));            
            double mean = Mean();

            double s = 0;
            double sc = 0;
            foreach (double value in _list)
            {
                double diff = value - mean;
                s += diff*diff;
                sc += diff;
            }
            return (s - (sc*sc)/_list.Length);
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
                return (s/weights.Mean() - _list.Length*Math.Pow(Mean(weights), 2)) / (_list.Length - 1);
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

        /// <summary>
        /// The standard error of the set of numbers.
        /// </summary>
        public double StdErr()
        {
            return StdDev()/Math.Sqrt(_list.Length);
        }

        /// <summary>
        /// The standard error of the set of numbers around a weighted mean.
        /// </summary>
        /// <param name="weights">The weights</param>
        /// <returns>Stadard error around weighted mean</returns>
        public double StdErr(Statistics weights)
        {
            return StdDev(weights)/Math.Sqrt(_list.Length);
        }

        /// <summary>
        /// Calculate the YULE index for the set of numbers.
        /// </summary>
        /// <returns>YULE index</returns>
        public double YULE()
        {
            try
            {
                return ((Q3() - Median()) - (Median() - Q1()))/(Q3() - Q1());
            }
            catch (Exception)
            {
                return double.NaN;
            }
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
                    return (member - Mean())/StdDev();
                
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
                    sumMul += (s1._list[i]*s2._list[i]);
                return (sumMul - len*s1.Mean()*s2.Mean())/(len - 1);
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
                return Covariance(s1, s2)/(s1.StdDev()*s2.StdDev());
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
            return y.Mean() - Beta(y, x)*x.Mean();
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
                residuals.Add(y._list[i] - (a*x._list[i] + b));
            return new Statistics(residuals);
        }

        /// <summary>
        /// Standard deviation of Y for a linear regression.
        /// <para>
        /// Described at:
        /// http://www.chem.utoronto.ca/coursenotes/analsci/StatsTutorial/ErrRegr.html
        /// </para>
        /// </summary>
        /// <param name="y">Y values</param>
        /// <param name="x">X values</param>
        /// <returns>The standard deviation in the y values for the linear regression</returns>
        private static double StdDevY(Statistics y, Statistics x)
        {
            double s = 0;
            Statistics residuals = Residuals(y, x);
            foreach (double value in residuals._list)
                s += Math.Pow(value, 2);
            return Math.Sqrt(s / (residuals._list.Length - 2));
        }

        /// <summary>
        /// Calculates the standard error of the alpha (y-intercept) coefficient for a
        /// linear regression function using the current set of numbers as Y values
        /// and another set as X values.
        /// </summary>
        /// <param name="x">X values</param>
        /// <returns>Standard error of alpha</returns>
        public double StdErrAlpha(Statistics x)
        {
            return StdErrAlpha(this, x);
        }

        /// <summary>
        /// Standard error for the Alpha (y-intercept) coefficient of a linear
        /// regression.
        /// <para>
        /// Described at:
        /// http://www.chem.utoronto.ca/coursenotes/analsci/StatsTutorial/ErrRegr.html
        /// </para>
        /// </summary>
        /// <param name="y">Y values</param>
        /// <param name="x">X values</param>
        public static double StdErrAlpha(Statistics y, Statistics x)
        {
            try
            {
                return StdDevY(y, x)*Math.Sqrt(x.SumOfSquares()/(x._list.Length*x.VarianceTotal()));
            }
            catch (Exception)
            {
                return double.NaN;
            }
        }

        /// <summary>
        /// Calculates the standard error of the beta (slope) coefficient for a
        /// linear regression function using the current set of numbers as Y values
        /// and another set as X values.
        /// </summary>
        /// <param name="x">X values</param>
        /// <returns>Standard error of beta</returns>
        public double StdErrBeta(Statistics x)
        {
            return StdErrBeta(this, x);
        }

        /// <summary>
        /// Standard error for the Beta (slope) coefficient of a linear
        /// regression.
        /// <para>
        /// Described at:
        /// http://www.chem.utoronto.ca/coursenotes/analsci/StatsTutorial/ErrRegr.html
        /// </para>
        /// </summary>
        /// <param name="y">Y values</param>
        /// <param name="x">X values</param>
        public static double StdErrBeta(Statistics y, Statistics x)
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
        /// Calculates a percentile based on the SAS Method 5 (default in SAS).
        /// (See http://www.haiweb.org/medicineprices/manual/quartiles_iTSS.pdf)
        /// </summary>
        /// <param name="p">Percentile in decimal form (e.g. 0.25)</param>
        /// <returns>Data value such that p percent are below the value</returns>
        public double PercentileSAS5(double p)
        {
            try
            {
                int n = _list.Length;
                var ordered = new double[n];
                _list.CopyTo(ordered, 0);
                Array.Sort(ordered);

                if (Math.Ceiling(n*p) == n*p)
                    return (ordered[(int) (n*p - 1)] + ordered[(int) (n*p)])/2;
                
                return ordered[((int) (Math.Ceiling(n*p))) - 1];
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
            listNormal1 = listNormal1.ConvertAll(Math.Sqrt);
            var stat1 = new Statistics(listNormal1);
            var listNormal2 = new List<double>(s._list);
            listNormal2 = listNormal2.ConvertAll(Math.Sqrt);
            var stat2 = new Statistics(listNormal2);

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

                sumCross += left*right;
                sumLeft += left*left;
                sumRight += right*right;
            }

            // Avoid dividing by zero
            if (sumLeft == 0 || sumRight == 0)
                return sumLeft == 0 && sumRight == 0 ? 1 : 0;

            return sumCross/Math.Sqrt(sumLeft*sumRight);
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
                ranks[listValueIndices[i].Value] = i+1;
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
            return new Statistics(listNewValues).Rank();
        }
    }
}
