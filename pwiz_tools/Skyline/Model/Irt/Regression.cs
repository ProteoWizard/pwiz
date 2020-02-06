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
using pwiz.Common.Collections;
using pwiz.Common.DataAnalysis;
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
            var sortedX = regression.XValues.OrderBy(x => x).ToArray();
            var addMin = minHydro < sortedX[0];
            var addMax = maxHydro > sortedX[sortedX.Length - 1];
            var points = sortedX.Length + (addMax ? 1 : 0) + (addMin ? 1 : 0);
            hydroScores = new double[points];
            predictions = new double[points];
            var offset = 0;
            if (addMin)
            {
                hydroScores[0] = minHydro;
                predictions[0] = regression.GetY(minHydro);
                offset = 1;
            }
            if (addMax)
            {
                hydroScores[hydroScores.Length - 1] = maxHydro;
                predictions[predictions.Length - 1] = regression.GetY(maxHydro);
            }
            for (var i = 0; i < sortedX.Length; i++)
            {
                hydroScores[offset + i] = sortedX[i];
                predictions[offset + i] = regression.GetY(sortedX[i]);
            }
        }
    }

    public class LogRegression : IIrtRegression
    {
        public LogRegression(bool invert = false)
        {
            Slope = double.NaN;
            Intercept = double.NaN;
            XValues = new double[0];
            YValues = new double[0];
            _invert = invert;
        }

        public LogRegression(IList<double> xValues, IList<double> yValues, bool invert = false)
        {
            var xFiltered = new List<double>(!invert ? xValues : yValues);
            var yFiltered = new List<double>(!invert ? yValues : xValues);
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
            _invert = invert;
        }

        public double GetY(double x)
        {
            return !_invert
                ? x > 0 ? Intercept + Slope * Math.Log(x) : 0
                : Math.Exp((x - Intercept) / Slope);
        }
        public double Slope { get; }
        public double Intercept { get; }

        public string DisplayEquation => !_invert
            ? string.Format(@"y = {0:F3} + log(x) * {1:F3}", Intercept, Slope)
            : string.Format(@"y = e^((x {0} {1:F3}) / {2:F3})", Intercept <= 0 ? '+' : '-', Math.Abs(Intercept), Slope);

        public IIrtRegression ChangePoints(double[] x, double[] y)
        {
            return new LogRegression(x, y, this is LogRegression regression && regression._invert);
        }
        public double[] XValues { get; }
        public double[] YValues { get; }
        private readonly bool _invert;

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
        public LoessRegression()
        {
            _linearFit = new RegressionLine();
            _xMin = double.MinValue;
            _loess = null;
            XValues = new double[0];
            YValues = new double[0];
        }

        public LoessRegression(double[] x, double[] y, CustomCancellationToken token = null)
        {
            _linearFit = new RegressionLine(x, y);
            _xMin = x.Min();
            _xMax = x.Max();
            _loess = new LoessAligner(0.4);
            _loess.Train(x, y, token ?? CustomCancellationToken.NONE);
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

        private readonly RegressionLine _linearFit;
        private readonly double _xMin;
        private readonly double _xMax;
        private readonly LoessAligner _loess;

        public IIrtRegression ChangePoints(double[] x, double[] y)
        {
            return new LoessRegression(x, y);
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
