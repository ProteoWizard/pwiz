/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
                double val_mode = i[0], help_val_mode = i[0];
                int old_counter = 0, new_counter = 0;
                int j = 0;
                for (; j <= i.Length - 1; j++)
                {
                    if (i[j] == help_val_mode)
                        new_counter++;
                    else if (new_counter > old_counter)
                    {
                        val_mode = help_val_mode;
                        old_counter = new_counter;
                        new_counter = 1;
                        help_val_mode = i[j];
                    }
                    else if (new_counter == old_counter)
                    {
                        val_mode = double.NaN;
                        help_val_mode = i[j];
                        new_counter = 1;
                    }
                    else
                    {
                        help_val_mode = i[j];
                        new_counter = 1;
                    }
                }
                if (new_counter > old_counter)
                    val_mode = help_val_mode;
                else if (new_counter == old_counter)
                    val_mode = double.NaN;
                return val_mode;
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
                double help_val_mode = i[0];
                int old_counter = 0, new_counter = 0;
                int j = 0;
                for (; j <= i.Length - 1; j++)
                {
                    if (i[j] == help_val_mode)
                        new_counter++;
                    else if (new_counter > old_counter)
                    {
                        listModes.Clear();
                        listModes.Add(help_val_mode);

                        old_counter = new_counter;
                        new_counter = 1;
                        help_val_mode = i[j];
                    }
                    else if (new_counter == old_counter)
                    {
                        listModes.Add(help_val_mode);

                        help_val_mode = i[j];
                        new_counter = 1;
                    }
                    else
                    {
                        help_val_mode = i[j];
                        new_counter = 1;
                    }
                }
                if (new_counter > old_counter)
                {
                    listModes.Clear();
                    listModes.Add(help_val_mode);
                }
                else if (new_counter == old_counter)
                    listModes.Add(help_val_mode);
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
        /// Calculates the mean average of the set of numbers.
        /// </summary>
        /// <returns>Mean</returns>
        public double Mean()
        {
            try
            {
                double sum = 0;
                for (int i = 0; i <= _list.Length - 1; i++)
                    sum += _list[i];
                return sum/_list.Length;
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
        /// Calculates the variance of the set of numbers.
        /// </summary>
        /// <returns>Variance</returns>
        public double Variance()
        {
            try
            {
                double s = 0;
                for (int i = 0; i <= _list.Length - 1; i++)
                    s += Math.Pow(_list[i], 2);
                return (s - _list.Length*Math.Pow(Mean(), 2))/(_list.Length - 1);
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
                double sum_mul = 0;
                for (int i = 0; i < len; i++)
                    sum_mul += (s1._list[i]*s2._list[i]);
                return (sum_mul - len*s1.Mean()*s2.Mean())/(len - 1);
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
            return new Statistics(residuals.ToArray());
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
                else
                    return ordered[((int) (Math.Ceiling(n*p))) - 1];
            }
            catch (Exception)
            {
                return double.NaN;
            }
        }

        /// <summary>
        /// Calculates a Costa Soares correlation coefficient between this and
        /// another set of numbers. 
        /// </summary>
        /// <param name="s">Second set of numbers</param>
        /// <returns>Correlation coefficient</returns>
        public double CostaSoares(Statistics s)
        {
            List<double> a = new List<double>(_list);
            List<double> b = new List<double>(s._list);

            int limit1 = Length;
            int limit2 = Math.Min(4, limit1);

            // Computing ranks for only top limit1 peaks
            for (int i = 0; i < limit1; i++)
            {
                double biggest_val = -1;
                int biggest_pos = -1;

                for (int j = 0; j < a.Count; j++)
                {
                    if (a[j] >= 0 && a[j] >= biggest_val)
                    {
                        biggest_val = a[j];
                        biggest_pos = j;
                    }
                }

                if (biggest_pos >= 0)
                    a[biggest_pos] = (i + 1) * -1.0;
            }

            for (int i = 0; i < limit1; i++)
            {
                double biggest_val = -1;
                int biggest_pos = -1;

                for (int j = 0; j < b.Count; j++)
                {
                    // Doing only for those peaks for which a[j] has been ranked
                    if (a[j] < 0 && b[j] >= 0 && b[j] >= biggest_val)
                    {
                        biggest_val = b[j];
                        biggest_pos = j;
                    }
                }

                if (biggest_pos >= 0)
                    b[biggest_pos] = (i + 1) * -1.0;
            }

            double total = 0;

            for (int i = 0; i < b.Count; i++)
            {
                // For which ranks have been computed
                if (a[i] < 0 && b[i] < 0 && a[i] >= -1 * limit2 || b[i] >= -1 * limit2)
                {
                    total += (a[i] - b[i])*(a[i] - b[i])*
                             (limit1 + 1 + a[i] + limit1 + 1 + b[i]);
                }
            }

            double n1 = limit1;
            double n2 = n1 * n1;
            double n3 = n1 * n2;
            double n4 = n1 * n3;
            const double n1_1 = 0;
            const double n2_1 = n1_1 * n1_1;
            const double n3_1 = n1_1 * n2_1;
            const double n4_1 = n1_1 * n3_1;
            total *= 6.0 / (n4 + n3 - n2 - n1 - (n4_1 + n3_1 - n2_1 - n1_1));
            total = 1 - total;
            return total;
        }
    }
}
