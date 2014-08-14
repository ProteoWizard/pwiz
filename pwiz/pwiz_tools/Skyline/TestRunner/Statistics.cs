using System;

namespace TestRunner
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
        
        /// <summary>
        /// Count of the numbers in the set.
        /// </summary>
        public int Length
        {
            get { return _list.Length; }
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
        /// Calculates the stadard deviation (sqrt(variance)) of the set
        /// of numbers.
        /// </summary>
        /// <returns>Standard deviation</returns>
        public double StdDev()
        {
            return Math.Sqrt(Variance());
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
    }
}
