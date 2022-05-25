/*
 * Original author: Kaipo Tamura <kaipot .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Threading;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Irt
{
    public class IrtRegressionType : LabeledValues<string>
    {
        public static readonly IrtRegressionType LINEAR = new IrtRegressionType(@"linear", () => Resources.IrtRegressionType_Linear);
        public static readonly IrtRegressionType LOGARITHMIC = new IrtRegressionType(@"logarithmic", () => Resources.IrtRegressionType_Logarithmic);
        public static readonly IrtRegressionType LOWESS = new IrtRegressionType(@"lowess", () => Resources.IrtRegressionType_Lowess);

        public static readonly IrtRegressionType DEFAULT = LINEAR;
        public static IEnumerable<IrtRegressionType> ALL => new[] {LINEAR, LOGARITHMIC, LOWESS};

        public IrtRegressionType(string name, Func<string> getLabelFunc) : base(name, getLabelFunc)
        {
        }

        public override string ToString()
        {
            return Label;
        }

        public static IrtRegressionType FromName(string name)
        {
            return ALL.FirstOrDefault(t => t.Name.Equals(name)) ?? DEFAULT;
        }
    }

    public interface IIrtRegression : IRegressionFunction
    {
        IIrtRegression ChangePoints(double[] x, double[] y);
        double[] XValues { get; }
        double[] YValues { get; }
        string DisplayEquation { get; }
        bool IrtIndependent { get; }
    }

    public static class IrtRegression
    {
        public static bool TryGet(IIrtRegression existing, IList<double> listIndependent, IList<double> listDependent,
            int minPoints, out IIrtRegression regression, IList<Tuple<double, double>> removedValues = null)
        {
            regression = null;
            removedValues?.Clear();
            if (listIndependent.Count != listDependent.Count || listIndependent.Count < minPoints)
                return false;

            var listX = new List<double>(listIndependent);
            var listY = new List<double>(listDependent);

            while (true)
            {
                regression = existing.ChangePoints(listX.ToArray(), listY.ToArray());
                if (Accept(regression, minPoints) || listX.Count <= minPoints)
                    break;

                var furthest = 0;
                var maxDistance = 0.0;
                for (var i = 0; i < listY.Count; i++)
                {
                    var distance = Math.Abs(regression.GetY(listX[i]) - listY[i]);
                    if (distance > maxDistance)
                    {
                        furthest = i;
                        maxDistance = distance;
                    }
                }

                removedValues?.Add(new Tuple<double, double>(listX[furthest], listY[furthest]));
                listX.RemoveAt(furthest);
                listY.RemoveAt(furthest);
            }

            return Accept(regression, minPoints);
        }

        public static bool TryGet<TRegression>(IList<double> listIndependent, IList<double> listDependent,
            int minPoints, out IIrtRegression regression, IList<Tuple<double, double>> removedValues = null)
            where TRegression : IIrtRegression, new()
        {
            return TryGet(new TRegression(), listIndependent, listDependent,
                minPoints, out regression, removedValues);
        }

        public static bool Accept(IIrtRegression regression, int minPoints)
        {
            return regression.XValues != null && regression.XValues.Length >= minPoints && R(regression) >= RCalcIrt.MIN_IRT_TO_TIME_CORRELATION;
        }

        public static double R(IIrtRegression regression)
        {
            if (regression?.XValues == null || regression.YValues == null || regression.XValues.Length == 0 || regression.YValues.Length == 0)
                return double.NaN;
            var yMean = new Statistics(regression.YValues).Mean();
            var totalSumOfSquares = regression.YValues.Sum(y => (y - yMean) * (y - yMean));
            var sumOfSquaresOfResiduals = regression.XValues.Select((x, i) => Math.Pow(regression.YValues[i] - regression.GetY(x), 2)).Sum();
            return Math.Sqrt(1 - sumOfSquaresOfResiduals / totalSumOfSquares);
        }

        public static void GetCurve(IIrtRegression regression, RetentionTimeStatistics statistics, out double[] hydroScores, out double[] predictions)
        {
            var minHydro = double.MaxValue;
            var maxHydro = double.MinValue;
            foreach (var hydroScore in statistics.ListHydroScores)
            {
                minHydro = Math.Min(minHydro, hydroScore);
                maxHydro = Math.Max(maxHydro, hydroScore);
            }
            var sortedX = regression.XValues.OrderBy(x => x).ToList();
            if (minHydro < sortedX[0])
                sortedX.Insert(0, minHydro);
            if (maxHydro > sortedX[sortedX.Count - 1])
                sortedX.Add(maxHydro);
            hydroScores = sortedX.ToArray();
            predictions = sortedX.Select(regression.GetY).ToArray();
        }
    }

    public class LogRegression : IIrtRegression
    {
        public LogRegression(bool irtIndependent = false)
        {
            Slope = double.NaN;
            Intercept = double.NaN;
            XValues = new double[0];
            YValues = new double[0];
            IrtIndependent = irtIndependent;
        }

        public LogRegression(IList<double> xValues, IList<double> yValues, bool irtIndependent = false)
        {
            var xFiltered = new List<double>(irtIndependent ? xValues : yValues);
            var yFiltered = new List<double>(irtIndependent ? yValues : xValues);
            for (var i = xFiltered.Count - 1; i >= 0; i--)
            {
                if (xFiltered[i] <= 0)
                {
                    xFiltered.RemoveAt(i);
                    yFiltered.RemoveAt(i);
                }
            }
            var statIndependent = new Statistics(xFiltered.Select(x => Math.Log(x)));
            var statDependent = new Statistics(yFiltered);
            Slope = statDependent.Slope(statIndependent);
            Intercept = statDependent.Intercept(statIndependent);
            XValues = xValues.ToArray();
            YValues = yValues.ToArray();
            IrtIndependent = irtIndependent;
        }

        public double GetY(double x)
        {
            return IrtIndependent
                ? x > 0 ? Intercept + Slope * Math.Log(x) : 0
                : Math.Exp((x - Intercept) / Slope);
        }
        public double Slope { get; }
        public double Intercept { get; }

        public string DisplayEquation => IrtIndependent
            ? string.Format(@"{0} = {1:F3} * log({2}) {3} {4:F3}",
                Resources.IIrtRegression_DisplayEquation_Measured_RT, Slope, Resources.IIrtRegression_DisplayEquation_iRT, Intercept <= 0 ? '-' : '+', Math.Abs(Intercept))
            : string.Format(@"{0} = e^(({1} {2} {3:F3}) / {4:F3})",
                Resources.IIrtRegression_DisplayEquation_iRT, Resources.IIrtRegression_DisplayEquation_Measured_RT, Intercept <= 0 ? '+' : '-', Math.Abs(Intercept), Slope);

        public bool IrtIndependent { get; }

        public IIrtRegression ChangePoints(double[] x, double[] y)
        {
            return new LogRegression(x, y, IrtIndependent);
        }
        public double[] XValues { get; }
        public double[] YValues { get; }

        public string GetRegressionDescription(double r, double window)
        {
            return string.Format(@"r = {0}", Math.Round(r, RetentionTimeRegression.ThresholdPrecision));
        }

        public void GetCurve(RetentionTimeStatistics statistics, out double[] hydroScores, out double[] predictions)
        {
            IrtRegression.GetCurve(this, statistics, out hydroScores, out predictions);
        }
    }

    public class LoessRegression : IIrtRegression
    {
        public LoessRegression(bool irtIndependent = false)
        {
            _linearFit = new RegressionLine();
            _xMin = double.MinValue;
            _loess = null;
            _token = default(CancellationToken);
            XValues = new double[0];
            YValues = new double[0];
            IrtIndependent = irtIndependent;
        }

        public LoessRegression(double[] x, double[] y, bool irtIndependent = false, CancellationToken token = default(CancellationToken))
        {
            _linearFit = new RegressionLine(x, y);
            _xMin = x.Min();
            _xMax = x.Max();
            _loess = new LoessAligner(0.4);
            _token = token;
            _loess.Train(x, y, _token);
            XValues = x;
            YValues = y;
        }

        public double GetY(double x)
        {
            return _xMin <= x && x <= _xMax ? _loess.GetValue(x) : _linearFit.GetY(x);
        }

        public double Slope => double.NaN;
        public double Intercept => double.NaN;
        public string DisplayEquation => Resources.DisplayEquation_N_A;
        public bool IrtIndependent { get; }

        private readonly RegressionLine _linearFit;
        private readonly double _xMin;
        private readonly double _xMax;
        private readonly LoessAligner _loess;
        private readonly CancellationToken _token;

        public IIrtRegression ChangePoints(double[] x, double[] y)
        {
            return new LoessRegression(x, y, IrtIndependent, _token);
        }
        public double[] XValues { get; }
        public double[] YValues { get; }

        public double Rmsd => _loess.GetRmsd();

        public string GetRegressionDescription(double r, double window)
        {
            return string.Format(@"rmsd = {0}", Math.Round(Rmsd, 4));
        }

        public void GetCurve(RetentionTimeStatistics statistics, out double[] hydroScores, out double[] predictions)
        {
            IrtRegression.GetCurve(this, statistics, out hydroScores, out predictions);
        }
    }
}
