using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Distributions;

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// Simple statistics utility class based on the CodeProject article:
    /// http://www.codeproject.com/KB/cs/csstatistics.aspx
    /// </summary>
    public class Statistics
    {
        private readonly double[] _list;

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
        /// Count of the numbers in the set.
        /// </summary>
        public int Length
        {
            get { return _list.Length; }
        }

        /// <summary>
        /// Creates a copy of the internal list and sorts it.  O(n*log(n)) operation.
        /// </summary>
        /// <returns>A sorted copy of the list of numbers in this object</returns>
        private double[] OrderedList()
        {
            var ordered = CopyList();
            Array.Sort(ordered);
            return ordered;
        }

        private double[] CopyList()
        {
            var listCopy = new double[_list.Length];
            _list.CopyTo(listCopy, 0);
            return listCopy;
        }

        private KeyValuePair<double, int>[] OrderedIndexedList(bool desc = false)
        {
            var ordered = _list.Select((v, i) => new KeyValuePair<double, int>(v, i)).ToArray();
            if (desc)
                Array.Sort(ordered, (p1, p2) => Comparer<double>.Default.Compare(p2.Key, p1.Key));
            else
                Array.Sort(ordered, (p1, p2) => Comparer<double>.Default.Compare(p1.Key, p2.Key));
            return ordered;
        }

        /// <summary>
        /// Calculates the mode (most frequently occurring number) in the set.
        /// </summary>
        /// <returns>Mode of the set of numbers</returns>
        public double Mode()
        {
            var modes = Modes();
            if (modes.Length == 0 || modes.Length > 1)
                return double.NaN;
            return modes[0];
        }

        /// <summary>
        /// Calculates the mode or modes (most frequently occurring number(s)) in the set.
        /// </summary>
        /// <returns>List of the modes the set of numbers</returns>
        public double[] Modes()
        {
            try
            {
                var dictValCount = new Dictionary<double, int>();
                int maxCount = 1;
                foreach (double value in _list)
                {
                    int count;
                    if (!dictValCount.TryGetValue(value, out count))
                        dictValCount.Add(value, count = 1);
                    else
                        dictValCount[value] = ++count;

                    if (count > maxCount)
                        maxCount = count;
                }
                return (from vc in dictValCount
                        where vc.Value == maxCount
                        select vc.Key).ToArray();
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
            // ReSharper disable LoopCanBePartlyConvertedToQuery
            double sum = 0;
            foreach (double d in _list) // using LINQ Sum() is about 10x slower than foreach
            {
                sum += d;
            }
            // ReSharper restore LoopCanBePartlyConvertedToQuery
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
        /// Calculates the variance of the set of numbers as a
        /// sample of a larger population.
        /// </summary>
        /// <returns>Variance estimate for population</returns>
        public double VarianceS()
        {
            return Variance();
        }

        /// <summary>
        /// Calculates the variance of the set of numbers as the
        /// entire population.
        /// </summary>
        /// <returns>Variance of population</returns>
        public double VarianceP()
        {
            try
            {
                return VarianceTotal() / _list.Length;
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
            if (_list.Length < 2)
                return 0;

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
        /// of numbers as a sample of an entire population.  Variance uses n-1.
        /// </summary>
        /// <returns>Standard deviation</returns>
        public double StdDevS()
        {
            return StdDev();
        }

        /// <summary>
        /// Calculates the stadard deviation (sqrt(variance)) of the set
        /// of numbers as an entire population.  Variance uses n.
        /// </summary>
        /// <returns>Standard deviation</returns>
        public double StdDevP()
        {
            return Math.Sqrt(VarianceP());
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
        /// Computes the index standard of a given value for the set of numbers.
        /// </summary>
        /// <param name="value">An arbitrary value</param>
        /// <returns>Index standard</returns>
        public double Z(double value)
        {
            return Z(value, Mean(), StdDev());
        }

        /// <summary>
        /// Computes the index standard of a given set of values for the set of numbers.
        /// </summary>
        /// <param name="s">Another set of numbers</param>
        /// <returns>Index standard for each number in the new set</returns>
        public Statistics Z(Statistics s)
        {
            double mean = Mean();
            double stdev = StdDev();
            return new Statistics(s._list.Select(v => Z(v, mean, stdev)));
        }

        /// <summary>
        /// Calculates a z-score (decimal number of standard deviations from the mean)
        /// for a single value, based on a mean and standard deviation.
        /// </summary>
        public static double Z(double value, double mean, double stdev)
        {
            try
            {
                return (value - mean) / stdev;
            }
            catch (Exception)
            {
                return double.NaN;
            }
        }

        /// <summary>
        /// Density function for a Student's T distribution for a specified number
        /// of degrees of freedom.
        /// </summary>
        public static double DT(double value, double df, double location = 0, double scale = 1)
        {
            var tdist = new StudentT(location, scale, df);
            return tdist.Density(value);
        }

        /// <summary>
        /// Distribution function for Student's T distribution for a specified z-score
        /// and a number of degrees of freedom.
        /// </summary>
        public static double PT(double z, double df)
        {
            var tdist = new StudentT(0, 1, df);
            return tdist.CumulativeDistribution(z);
        }

        /// <summary>
        /// Quantile function for Student's T distribution for a specified probability
        /// and a number of degrees of freedom.
        /// </summary>
        public static double QT(double p, double df)
        {
            return QDist(p, prob => PT(prob, df));
        }

        /// <summary>
        /// Density function for a Normal distribution for a specified mean and standard deviation
        /// </summary>
        public static double DNorm(double value, double mean = 0, double stdev = 1)
        {
            return (1/(Math.Sqrt(2*Math.PI)*stdev))*Math.Pow(Math.E, -Math.Pow(value - mean, 2)/(2*Math.Pow(stdev, 2)));
        }

        /// <summary>
        /// Quantile function for a Normal distribution for a specified probability
        /// </summary>
        public static double QNorm(double p)
        {
            return QDist(p, PNorm);
        }

        /// <summary>
        /// Quantile function for any distribution for which a distribution function
        /// exists.  Binary search method is used to find the specified quantile using
        /// the distribution function.
        /// </summary>
        private static double QDist(double p, Func<double, double> pDist)
        {
            double left = -40, right = 40;
            while (Math.Abs((left - right) / left) > 1E-15)
            {
                double middle = (left + right) / 2;
                double middleP = pDist(middle);
                if (middleP < p)
                    left = middle;
                else
                    right = middle;
            }
            return Math.Round(left, 10);
        }

        private const double Z_MAX = 6.0;
        
        /// <summary>
        /// Adapted from a polynomial approximation in:  Ibbetson D, Algorithm 209
        /// Collected Algorithms of the CACM 1963 p. 616
        /// </summary>
        /// <param name="z">Z score</param>
        /// <returns>P value for the given z-score</returns>
        public static double PNorm(double z)
        {
            double y = z != 0.0
                ? 0.5 * Math.Abs(z)
                : 0.0;

            double x;
            if (y > (Z_MAX * 0.5))
            {
                x = 1.0;
            }
            else if (y < 1.0)
            {
                double w = y * y;
                x = ((((((((0.000124818987 * w
                         - 0.001075204047) * w + 0.005198775019) * w
                         - 0.019198292004) * w + 0.059054035642) * w
                         - 0.151968751364) * w + 0.319152932694) * w
                         - 0.531923007300) * w + 0.797884560593) * y * 2.0;
            }
            else
            {
                y -= 2.0;
                x = (((((((((((((-0.000045255659 * y
                               + 0.000152529290) * y - 0.000019538132) * y
                               - 0.000676904986) * y + 0.001390604284) * y
                               - 0.000794620820) * y - 0.002034254874) * y
                               + 0.006549791214) * y - 0.010557625006) * y
                               + 0.011630447319) * y - 0.009279453341) * y
                               + 0.005353579108) * y - 0.002141268741) * y
                               + 0.000535310849) * y + 0.999936657524;
            }

            return z > 0.0 ? ((x + 1.0) * 0.5) : ((1.0 - x) * 0.5);
        }

        /// <summary>
        /// Computes the p value of a given value using the set of numbers as a null distribution
        /// which is assumed to be Gaussian (normal).
        /// </summary>
        /// <param name="value">An arbitrary value</param>
        /// <returns>P value</returns>
        public double PvalueNorm(double value)
        {
            return 1 - PNorm(Z(value));
        }

        /// <summary>
        /// Computes the p values of a given target distribution using the set of numbers as a null distribution
        /// which is assumed to be Gaussian (normal).
        /// </summary>
        /// <param name="s">Another set of numbers</param>
        /// <returns>Index standard for each number in the new set</returns>
        public double[] PvaluesNorm(Statistics s)
        {
            return Z(s)._list.Select(v => 1 - PNorm(v)).ToArray();
        }

        /// <summary>
        /// Computes the p value of a given value using the set of numbers as the actual
        /// null distribution with no assumptions of distribution shape.
        /// </summary>
        /// <param name="value">An arbitrary value</param>
        /// <returns>P value</returns>
        public double PvalueNull(double value)
        {
            return PvalueNull(value, OrderedList());
        }

        /// <summary>
        /// Computes the p values of a given target distribution using the set of numbers as the actual
        /// null distribution with no assumptions of distribution shape.
        /// </summary>
        /// <param name="s">Another set of numbers</param>
        /// <returns>Index standard for each number in the new set</returns>
        public double[] PvaluesNull(Statistics s)
        {
            var ordered = OrderedList();
            return s._list.Select(v => PvalueNull(v, ordered)).ToArray();
        }

        private static double PvalueNull(double value, double[] ordered)
        {
            int i = Array.BinarySearch(ordered, value);
            if (i < 0)
                i = ~i;
            int n = ordered.Length;
            return ((double) i)/n;
        }

        /// <summary>
        /// Returns a single point Pi-zero (proportion of features that are truly null)
        /// estimation, given a specific p value cut-off, where values in the list are
        /// assumed to be well behaved (randomly distributed for false-postives) p values.
        /// See Storey and Tibshirani 2003
        /// </summary>
        /// <param name="lambda">P value cut-off</param>
        /// <returns>Pi-zero estimation</returns>
        public double PiZero(double lambda)
        {
            int aboveLambda = 0;
            for (int i = 0; i < _list.Length; i++)
            {
                if (_list[i] > lambda)
                    aboveLambda++;
            }
            return aboveLambda/(_list.Length*(1 - lambda));
        }

        /// <summary>
        /// Returns an array of pi-zero values, given a list of lambdas in sorted order
        /// </summary>
        public double[] PiZeros(double[] lambdas)
        {
            var aboveLambdaCounts = new int[lambdas.Length];
            for (int i = 0; i < _list.Length; i++)
            {
                for (int j = 0; j < lambdas.Length; j++)
                {
                    if (_list[i] <= lambdas[j])
                        break;
                     aboveLambdaCounts[j]++;
                }
            }
            var piZeros = new double[lambdas.Length];
            for (int j = 0; j < lambdas.Length; j++)
            {
                piZeros[j] = aboveLambdaCounts[j]/(_list.Length*(1 - lambdas[j]));
            }
            return piZeros;
        }

        private const int RANDOM_CYCLE_COUNT = 100;
        private const int RANDOM_DRAWS_MAX = 1000;

        public double PiZero(double? lambda = null)
        {
            if (lambda.HasValue)
                return PiZero(lambda.Value);

            lambda = CalcPiZeroLambda();
            return Math.Max(0.0, Math.Min(1.0, PiZero(lambda)));
        }

        public double CalcPiZeroLambda()
        {
            // As in Storey and Tibshirani 2003 calculate Pi-zero across a range of
            // p value cut-offs.
            var lambdas = PiZeroLambdas.ToArray();
            var piZeros = PiZeros(lambdas);
            double minPi0 = piZeros.Min();

            // Because the spline fitting described in Storey and Tibshirani 2003
            // is non-trivial to implement in C#, the method in use in Percolator
            // is used instead.

            // Find the lambda level closest to the minimum with enough precision
            // by testing sets of p values drawn at random from the current set.
            double[] arrayMse = new double[lambdas.Length];
            int numDraw = Math.Min(Length, RANDOM_DRAWS_MAX);
            var rand = new Random(0);   // Use a fixed random seed value for reproducible results
            for (int r = 0; r < RANDOM_CYCLE_COUNT; r++)
            {
                // Create an array of p-values randomly drawn from the current set
                var statBoot = new Statistics(RandomDraw(rand).Take(numDraw));
                piZeros = statBoot.PiZeros(lambdas);
                for (int i = 0; i < lambdas.Length; ++i)
                {
                    double pi0Boot = piZeros[i];
                    // Estimated mean-squared error.
                    arrayMse[i] += (pi0Boot - minPi0) * (pi0Boot - minPi0);
                }
            }

            // Use the original estimate for the lambda that produced
            // the minimum mean-squared error for the random draw iterations
            int iMin = arrayMse.IndexOf(v => v == arrayMse.Min());
            return lambdas[iMin];
        }

        private IEnumerable<double> RandomDraw(Random rand)
        {
            int n = Length;
            for (int i = 0; i < n; i++)
                yield return _list[rand.Next(n-1)];
        }

        public static IEnumerable<double> PiZeroLambdas
        {
            get
            {
                for (int i = 1; i <= 95; i++)
                    yield return i*0.01;
            }
        }

        /// <summary>
        /// Returns q values for a set of p values, optionally using a p value cut-off
        /// used for calculating Pi-zero.
        /// See Storey and Tibshirani 2003
        /// CONSIDER: Use spline fitting described in the paper instead of a supplied
        ///           cut-off.
        /// </summary>
        /// <returns>New statistics containing q values for the p values in this set</returns>
        public double[] Qvalues(double? lambda = null, double minPiZero = 0)
        {
            double pi0 = Math.Max(PiZero(lambda), minPiZero);
            var ordered = OrderedIndexedList();
            int n = _list.Length;
            var qlist = new double[n];
            for (int i = 0; i < n; i++)
            {
                double pVal = ordered[i].Key;
                int iOrig = ordered[i].Value;
                double qVal = pi0*n*pVal / (i + 1);
                qlist[iOrig] = qVal;
            }
            // Enforce that q values are monotonically increasing and never greater than 1
            double last = 1.0;
            for (int i = n - 1; i >= 0; i--)
            {
                int iOrig = ordered[i].Value;
                double qVal = Math.Min(qlist[iOrig], last);
                qlist[iOrig] = qVal;
                last = qVal;
            }
            return qlist;
        }

        public double[] QvaluesNorm(Statistics s)
        {
            return new Statistics(PvaluesNorm(s)).Qvalues();
        }

        public double[] QvaluesNull(Statistics s)
        {
            return new Statistics(PvaluesNull(s)).Qvalues();
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
        /// Calculates the b term (y-intercept) of the linear
        /// regression function (y = a*x + b) using the current set of numbers as Y values
        /// and another set as X values.
        /// </summary>
        /// <param name="x">X values</param>
        /// <returns>The b coefficient of y = a*x + b</returns>
        public double BTerm2(Statistics x)
        {
            return BTerm2(this, x);
        }

        /// <summary>
        /// Calculates the b term (y-intercept) of the linear
        /// regression function (y = a*x + b) given the Y and X values.
        /// </summary>
        /// <param name="y">Y values</param>
        /// <param name="x">X values</param>
        /// <returns>The b coefficient of y = a*x + b</returns>
        public static double BTerm2(Statistics y, Statistics x)
        {
            return y.Mean() - ATerm2(y, x)*x.Mean();
        }

        /// <summary>
        /// Calculates the y-intercept (b term) of the linear
        /// regression function (y = a*x + b) using the current set of numbers as Y values
        /// and another set as X values.
        /// </summary>
        /// <param name="x">X values</param>
        /// <returns>The y-intercept</returns>
        public double Intercept(Statistics x)
        {
            return BTerm2(x);
        }

        /// <summary>
        /// Calculates the y-intercept (Beta coefficient) of the linear
        /// regression function (y = a*x + b) given the Y and X values.
        /// </summary>
        /// <param name="y">Y values</param>
        /// <param name="x">X values</param>
        /// <returns>The y-intercept</returns>
        public static double Intercept(Statistics y, Statistics x)
        {
            return BTerm2(y, x);
        }

        /// <summary>
        /// Calculates the a term (slope) of the linear regression function (y = a*x + b)
        /// using the current set of numbers as Y values and another set
        /// as X values.
        /// </summary>
        /// <param name="x">X values</param>
        /// <returns>The a term of y = a*x + b</returns>
        public double ATerm2(Statistics x)
        {
            return ATerm2(this, x);
        }

        /// <summary>
        /// Calculates the a term (slope) of the linear regression function (y = a*x + b)
        /// given the Y and X values.
        /// </summary>
        /// <param name="y">Y values</param>
        /// <param name="x">X values</param>
        /// <returns>The a term of y = a*x + b</returns>
        public static double ATerm2(Statistics y, Statistics x)
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
        /// Calculates the slope (a term) of the linear regression function (y = a*x + b)
        /// using the current set of numbers as Y values and another set
        /// as X values.
        /// </summary>
        /// <param name="x">X values</param>
        /// <returns>The slope</returns>
        public double Slope(Statistics x)
        {
            return ATerm2(x);
        }

        /// <summary>
        /// Calculates the slope (a term) of the linear regression function (y = a*x + b)
        /// given the Y and X values.
        /// </summary>
        /// <param name="y">Y values</param>
        /// <param name="x">X values</param>
        /// <returns>The slope</returns>
        public static double Slope(Statistics y, Statistics x)
        {
            return ATerm2(y, x);
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
            double a = ATerm2(y, x);
            double b = BTerm2(y, x);

            List<double> residuals = new List<double>();
            for (int i = 0; i < x.Length; i++)
                residuals.Add(y._list[i] - (a*x._list[i] + b));
            return new Statistics(residuals);
        }


        /// <summary>
        /// Calculates the a term of the quadratic regression function (y = a*x^2 + b*x + c)
        /// using the current set of numbers as Y values and another set
        /// as X values.
        /// see http://www.codeproject.com/Articles/63170/Least-Squares-Regression-for-Quadratic-Curve-Fitti
        /// </summary>
        /// <param name="x">X values</param>
        /// <returns>The a term of y = a*x^2 + b*x + c</returns>
        public double ATerm3(Statistics x)
        {
            return ATerm3(this, x);
        }

        /// <summary>
        /// Calculates the a term of the quadratic regression function (y = a*x^2 + b*x + c)
        /// given the Y and X values.
        /// </summary>
        /// <param name="y">Y values</param>
        /// <param name="x">X values</param>
        /// <returns>The a term of y = a*x^2 + b*x + c</returns>
        public static double ATerm3(Statistics y, Statistics x)
        {
            if (x.Length < 3)
                throw new InvalidOperationException("Insufficient pairs of co-ordinates"); // Not L10N

            //notation sjk to mean the sum of x_i^j*y_i^k. 
            double s40 = x._list.Sum(v => v*v*v*v); //sum of x^4
            double s30 = x._list.Sum(v => v*v*v); //sum of x^3
            double s20 = x._list.Sum(v => v*v); //sum of x^2
            double s10 = x.Sum();  //sum of x
            double s00 = x.Length;
            //sum of x^0 * y^0  ie 1 * number of entries

            double s21 = x._list.Select((v, i) => v*v*y._list[i]).Sum(); //sum of x^2*y
            double s11 = x._list.Select((v, i) => v*y._list[i]).Sum();  //sum of x*y
            double s01 = y.Sum();   //sum of y

            //a = Da/D
            return (s21 * (s20 * s00 - s10 * s10) -
                    s11 * (s30 * s00 - s10 * s20) +
                    s01 * (s30 * s10 - s20 * s20))
                    /
                    (s40 * (s20 * s00 - s10 * s10) -
                     s30 * (s30 * s00 - s10 * s20) +
                     s20 * (s30 * s10 - s20 * s20));
        }

        /// <summary>
        /// Calculates the b term of the quadratic regression function (y = a*x^2 + b*x + c)
        /// using the current set of numbers as Y values and another set
        /// as X values.
        /// </summary>
        /// <param name="x">X values</param>
        /// <returns>The b term of y = a*x^2 + b*x + c</returns>
        public double BTerm3(Statistics x)
        {
            return BTerm3(this, x);
        }

        /// <summary>
        /// Calculates the c term of the quadratic regression function (y = a*x^2 + b*x + c)
        /// given the Y and X values.
        /// </summary>
        /// <param name="y">Y values</param>
        /// <param name="x">X values</param>
        /// <returns>The c term of y = a*x^2 + b*x + c</returns>
        public static double BTerm3(Statistics y, Statistics x)
        {
            if (x.Length < 3)
                throw new InvalidOperationException("Insufficient pairs of co-ordinates"); // Not L10N

            //notation sjk to mean the sum of x_i^j*y_i^k.
            double s40 = x._list.Sum(v => v*v*v*v); //sum of x^4
            double s30 = x._list.Sum(v => v*v*v); //sum of x^3
            double s20 = x._list.Sum(v => v*v); //sum of x^2
            double s10 = x.Sum();  //sum of x
            double s00 = x.Length;
            //sum of x^0 * y^0  ie 1 * number of entries

            double s21 = x._list.Select((v, i) => v*v*y._list[i]).Sum(); //sum of x^2*y
            double s11 = x._list.Select((v, i) => v*y._list[i]).Sum();  //sum of x*y
            double s01 = y.Sum();   //sum of y

            //b = Db/D
            return (s40 * (s11 * s00 - s01 * s10) -
                    s30 * (s21 * s00 - s01 * s20) +
                    s20 * (s21 * s10 - s11 * s20))
                    /
                    (s40 * (s20 * s00 - s10 * s10) -
                     s30 * (s30 * s00 - s10 * s20) +
                     s20 * (s30 * s10 - s20 * s20));
        }

        /// <summary>
        /// Calculates the c term of the quadratic regression function (y = a*x^2 + b*x + c)
        /// using the current set of numbers as Y values and another set
        /// as X values.
        /// </summary>
        /// <param name="x">X values</param>
        /// <returns>The c term of y = a*x^2 + b*x + c</returns>
        public double CTerm3(Statistics x)
        {
            return CTerm3(this, x);
        }

        /// <summary>
        /// Calculates the c term of the quadratic regression function (y = a*x^2 + b*x + c)
        /// given the Y and X values.
        /// </summary>
        /// <param name="y">Y values</param>
        /// <param name="x">X values</param>
        /// <returns>The c term of y = a*x^2 + b*x + c</returns>
        public static double CTerm3(Statistics y, Statistics x)
        {
            if (x.Length < 3)
                throw new InvalidOperationException("Insufficient pairs of co-ordinates"); // Not L10N

            //notation sjk to mean the sum of x_i^j*y_i^k.
            double s40 = x._list.Sum(v => v*v*v*v); //sum of x^4
            double s30 = x._list.Sum(v => v*v*v); //sum of x^3
            double s20 = x._list.Sum(v => v*v); //sum of x^2
            double s10 = x.Sum();  //sum of x
            double s00 = x.Length;
            //sum of x^0 * y^0  ie 1 * number of entries

            double s21 = x._list.Select((v, i) => v*v*y._list[i]).Sum(); //sum of x^2*y
            double s11 = x._list.Select((v, i) => v*y._list[i]).Sum();  //sum of x*y
            double s01 = y.Sum();   //sum of y

            //c = Dc/D
            return (s40 * (s20 * s01 - s10 * s11) -
                    s30 * (s30 * s01 - s10 * s21) +
                    s20 * (s30 * s11 - s20 * s21))
                    /
                    (s40 * (s20 * s00 - s10 * s10) -
                     s30 * (s30 * s00 - s10 * s20) +
                     s20 * (s30 * s10 - s20 * s20));
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
        /// Calculates the standard error of the b term (y-intercept) for a
        /// linear regression function y = a*x + b using the current set of numbers as Y values
        /// and another set as X values.
        /// </summary>
        /// <param name="x">X values</param>
        /// <returns>Standard error of a term</returns>
        public double StdErrBTerm2(Statistics x)
        {
            return StdErrBTerm2(this, x);
        }

        /// <summary>
        /// Standard error for the Alpha (y-intercept) coefficient of a linear
        /// regression function y = a*x + b.
        /// <para>
        /// Described at:
        /// http://www.chem.utoronto.ca/coursenotes/analsci/StatsTutorial/ErrRegr.html
        /// </para>
        /// </summary>
        /// <param name="y">Y values</param>
        /// <param name="x">X values</param>
        public static double StdErrBTerm2(Statistics y, Statistics x)
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
        /// Calculates the standard error of the a term (slope) for a
        /// linear regression function y = a*x + b using the current set of numbers as Y values
        /// and another set as X values.
        /// </summary>
        /// <param name="x">X values</param>
        /// <returns>Standard error of a term</returns>
        public double StdErrATerm2(Statistics x)
        {
            return StdErrATerm2(this, x);
        }

        /// <summary>
        /// Standard error for the a term (slope) of a linear
        /// regression function y = a*x + b.
        /// <para>
        /// Described at:
        /// http://www.chem.utoronto.ca/coursenotes/analsci/StatsTutorial/ErrRegr.html
        /// </para>
        /// </summary>
        /// <param name="y">Y values</param>
        /// <param name="x">X values</param>
        public static double StdErrATerm2(Statistics y, Statistics x)
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
        /// Calculates a percentile using <see cref="QPercentile"/>.
        /// </summary>
        /// <param name="p">Percentile in decimal form (e.g. 0.25)</param>
        /// <returns>Data value such that p percent are below the value</returns>
        public double Percentile(double p)
        {
            return QPercentile(p);
        }

        /// <summary>
        /// Calculates a percentile based on the Excel method.
        /// (See http://www.haiweb.org/medicineprices/manual/quartiles_iTSS.pdf)
        /// This method is currently used for all other statistical calculations
        /// that rely on percentiles (e.g. Q1, Q2, IQ, etc.)
        /// </summary>
        /// <param name="p">Percentile in decimal form (e.g. 0.25)</param>
        /// <returns>Data value such that p percent are below the value</returns>
        public double PercentileExcelSorted(double p)
        {
            try
            {
                var ordered = OrderedList();
                double pos = (ordered.Length - 1) * p;
                int j = (int)pos;
                double g = pos - j;
                if (g == 0)
                    return ordered[j];

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
                var ordered = OrderedList();
                int n = ordered.Length;
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
        /// Quick percentile function that gives percentiles in linear time.
        /// (See http://www.i-programmer.info/babbages-bag/505-quick-median.html?start=1)
        /// Calculates a percentile based on the Excel method.
        /// (See http://www.haiweb.org/medicineprices/manual/quartiles_iTSS.pdf)
        /// For highest performance, this is a static function that takes a list
        /// in which it modifies the order of the values, to avoid extra allocation.
        /// </summary>
        /// <param name="p">Percentile in decimal form (e.g. 0.25)</param>
        /// <returns>Data value such that p percent are below the value</returns>
        public double QPercentile(double p)
        {
            try
            {
                var list = CopyList();
                double pos = (list.Length - 1) * p;
                int j = (int)pos;
                double value = QNthItem(list, j);
                double g = pos - j;
                if (g == 0)
                    return value;
                double value2 = QNthItem(list, j + 1);
                return (1 - g) * value + g * value2;
            }
            catch (Exception)
            {
                return double.NaN;
            }
        }

        public double QNthItem(int elementIndex)
        {
            return QNthItem(CopyList(), elementIndex);
        }

        /// <summary>
        /// Linear time function for finding the n-th item in a list. This function changes the order of the elements in the list.
        /// </summary>
        public static double QNthItem(IList<double> list, int elementIndex)
        {
            int left = 0;
            int right = list.Count - 1;
            while (left < right)
            {
                double value = list[elementIndex];
                int splitLeft = left, splitRight = right;
                Split(list, value, ref splitLeft, ref splitRight);
                if (splitRight < elementIndex)
                    left = splitLeft;
                if (elementIndex < splitLeft)
                    right = splitRight;
            }
            return list[elementIndex];
        }

        private static void Split(IList<double> list, double value, ref int left, ref int right)
        {
            // Left and right scan until the pointers cross
            do
            {
                while (list[left] < value)
                    left++;
                while (value < list[right])
                    right--;

                if (left <= right)
                {
                    double temp = list[left];
                    list[left] = list[right];
                    list[right] = temp;

                    left++;
                    right--;
                }
            } while (left <= right);
        }

        /// <summary>
        /// Calculates the dot-product or cos(angle) between two vectors,
        /// using the square roots of the values in the vectors.
        /// </summary>
        /// <param name="s">The other vector</param>
        /// <returns>Dot-Product of square roots of values in vectors</returns>
        public double AngleSqrt(Statistics s)
        {
            var stat1 = new Statistics(_list.Select(Math.Sqrt));
            var stat2 = new Statistics(s._list.Select(Math.Sqrt));

            return stat1.Angle(stat2);
        }

        /// <summary>
        /// Calculates the normalized contrast angle dot-product or 1 - angle/90 between two vectors,
        /// using the square roots of the values in the vectors.
        /// </summary>
        /// <param name="s">The other vector</param>
        /// <returns>Normalized contrast angle dot-product of square roots of values in vectors</returns>
        public double NormalizedContrastAngleSqrt(Statistics s)
        {
            var stat1 = new Statistics(_list.Select(Math.Sqrt));
            var stat2 = new Statistics(s._list.Select(Math.Sqrt));

            return stat1.NormalizedContrastAngle(stat2);
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
        /// Calculates the normalized contrast angle dot-product or 1 - angle/90 between two vectors,
        /// with both normalized to a unit vector first.
        /// </summary>
        /// <param name="s">The other vector</param>
        /// <returns>Normalized contrast angle dot-product of normalized vectors</returns>
        public double NormalizedContrastAngleUnitVector(Statistics s)
        {
            var stat1 = NormalizeUnit();
            var stat2 = s.NormalizeUnit();

            return stat1.NormalizedContrastAngle(stat2);
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
            if (Length != s.Length)
                return double.NaN;

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

            // Rounding error can cause values slightly larger than 1.
            return Math.Min(1.0, sumCross/Math.Sqrt(sumLeft*sumRight));
        }

        /// <summary>
        /// Calculates a dot-product or 1 - angle/90 between two vectors,
        /// which is more sensetive for small vectors than cos(angle).
        /// </summary>
        /// <param name="s">The other vector</param>
        /// <returns>Normalized contrast angle dot-product</returns>
        public double NormalizedContrastAngle(Statistics s)
        {
            // Acos returns the angle in radians, where Pi == 180 degrees
            return AngleToNormalizedContrastAngle(Angle(s));
        }

        /// <summary>
        /// Convert from dot-product of the form cos(angle) to 1 - angle/90
        /// </summary>
        public static double AngleToNormalizedContrastAngle(double angle)
        {
            return 1 - Math.Acos(angle)*2/Math.PI;
        }

        /// <summary>
        /// Convert from dot-product of the form 1 - angle/90 to cos(angle)
        /// </summary>
        public static double NormalizedContrastAngleToAngle(double normalAngle)
        {
            return Math.Cos((1 - normalAngle)*Math.PI/2);
        }

        /// <summary>
        /// Calculates the rank orders of the values in the list.
        /// </summary>
        /// <returns>Rank order</returns>
        public int[] Rank(bool desc = false)
        {
            // Sort into rank order, storing original indices
            var ordered = OrderedIndexedList(desc);
            int n = _list.Length;
            // Record the sorted rank order
            var ranks = new int[n];
            for (int i = 0; i < n; i++)
                ranks[ordered[i].Value] = i+1;
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
            if (Length != s.Length)
                return double.NaN;

            int n = Length;

            int[] a = Rank(true);
            int[] b = s.Rank(true);

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
            return new Statistics(listNewValues).Rank(true);
        }

        public double[] Standardize()
        {
            // subtract the mean and divide by the standard deviation
            double mean = Mean();
            double std = StdDevP();

            var listNew = new double[_list.Length];
            for (int i = 0; i < _list.Length; i++)
                listNew[i] = (_list[i] - mean)/std;
            return listNew;
        }

        public Dictionary<int, double> CrossCorrelation(Statistics s, bool normalize)
        {
            if (Length != s.Length)
                return null;

            double mean1 = Mean();
            double mean2 = s.Mean();
            double invdenominator = 1; // 1/denominator for normalization
            int length = Length; // cache this - profiling shows a surprising cost for repeated access
            var result = new Dictionary<int, double>(1 + (2 * length));

            // Normalized cross-correlation = subtract the mean and divide by the standard deviation
            if (normalize)
            {
                double sqsum1 = 0;
                double sqsum2 = 0;
                foreach (double v in _list)
                  sqsum1 += (v - mean1)*(v - mean1);
                foreach (double v in s._list)
                  sqsum2 += (v - mean2)*(v - mean2);
                // sigma_1 * sigma_2 * n
                double denominator = Math.Sqrt(sqsum1*sqsum2); // find the demominator
                if (denominator > 0)
                {
                    invdenominator = 1.0/denominator; // for speed, we'll multiply by invdenominator rather than divide by denominator
                }
                else
                {
                    // all datapoints are zero
                    for (int delay = -length; delay <= length; delay++)
                    {
                        result.Add(delay,0);
                    }
                    return result;
                }
            }

            for (int delay = -length; delay <= length; delay++)
            {
                double sxy = 0;
                int upper = Math.Min(length, length - delay); // i and i+delay must both be in range(0,length)
                for (int i = Math.Max(0, -delay); i < upper; i++)  // i and i+delay must both be in range(0,length)
                {
                    if (normalize)
                        sxy += (_list[i] - mean1) * (s._list[i + delay] - mean2);
                    else
                        sxy += (_list[i]) * (s._list[i + delay]);
                }

                result.Add(delay, sxy * invdenominator); 
            }
            return result;
        }
    }
}
