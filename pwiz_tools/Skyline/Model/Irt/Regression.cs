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
using pwiz.Common.DataAnalysis;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Irt
{
    public interface IIrtRegression : IRegressionFunction
    {
        IIrtRegression ChangePoints(double[] x, double[] y);
        double Correlation { get; }
        string DisplayEquation { get; }
    }

    public static class IrtRegression
    {
        public static bool TryGet<TRegression>(IList<double> listIndependent, IList<double> listDependent, int minPoints,
            out IIrtRegression regression, IList<Tuple<double, double>> removedValues = null)
            where TRegression : IIrtRegression, new()
        {
            regression = null;
            removedValues?.Clear();
            if (listIndependent.Count != listDependent.Count || listIndependent.Count < minPoints)
                return false;

            var listX = new List<double>(listIndependent);
            var listY = new List<double>(listDependent);

            while (true)
            {
                regression = new TRegression().ChangePoints(listX.ToArray(), listY.ToArray());
                if (regression.Correlation >= RCalcIrt.MIN_IRT_TO_TIME_CORRELATION || listX.Count <= minPoints)
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

            return regression.Correlation >= RCalcIrt.MIN_IRT_TO_TIME_CORRELATION;
        }

        public static double RSquared(IRegressionFunction regression, IEnumerable<double> xValues, IList<double> yValues)
        {
            var yMean = new Statistics(yValues).Mean();
            var totalSumOfSquares = yValues.Sum(y => (y - yMean) * (y - yMean));
            var sumOfSquaresOfResiduals = xValues.Select((x, i) => Math.Pow(yValues[i] - regression.GetY(x), 2)).Sum();
            return 1 - sumOfSquaresOfResiduals / totalSumOfSquares;
        }
    }

    public class LogRegression : IIrtRegression
    {
        public LogRegression()
        {
            Slope = double.NaN;
            Intercept = double.NaN;
            Correlation = double.NaN;
        }

        public LogRegression(IList<double> xValues, IList<double> yValues)
        {
            var statIndependent = new Statistics(xValues.Select(x => Math.Log(x)));
            var statDependent = new Statistics(yValues);
            Slope = statDependent.Slope(statIndependent);
            Intercept = statDependent.Intercept(statIndependent);
            Correlation = IrtRegression.RSquared(this, xValues, yValues);
        }

        public double GetY(double x)
        {
            return Intercept + Slope * Math.Log(x);
        }
        public double Slope { get; }
        public double Intercept { get; }
        public double Correlation { get; }
        public string DisplayEquation => string.Format(@"iRT = {0:F3} + log(RT) * {1:F3}", Intercept, Slope);

        public IIrtRegression ChangePoints(double[] x, double[] y)
        {
            return new LogRegression(x, y);
        }

        public string GetRegressionDescription(double r, double window)
        {
            throw new NotImplementedException();
        }

        public void GetCurve(RetentionTimeStatistics statistics, out double[] hydroScores, out double[] predictions)
        {
            throw new NotImplementedException();
        }
    }

    public class LoessRegression : IIrtRegression
    {
        public LoessRegression()
        {
            _linearFit = new RegressionLine();
            _xMin = double.MinValue;
            _loess = null;
            Correlation = double.NaN;
        }

        public LoessRegression(double[] x, double[] y)
        {
            _linearFit = new RegressionLine(x, y);
            var statX = new Statistics(x);
            _xMin = statX.Min();
            _xMax = statX.Max();
            _loess = new LoessAligner(0.4);
            _loess.Train(x, y, CustomCancellationToken.NONE);
            Correlation = IrtRegression.RSquared(this, x, y);
        }

        public double GetY(double x)
        {
            return _xMin <= x && x <= _xMax ? _loess.GetValue(x) : _linearFit.GetY(x);
        }

        public double Slope => double.NaN;
        public double Intercept => double.NaN;
        public double Correlation { get; }
        public string DisplayEquation => Resources.DisplayEquation_N_A;

        private readonly RegressionLine _linearFit;
        private readonly double _xMin;
        private readonly double _xMax;
        private readonly LoessAligner _loess;

        public IIrtRegression ChangePoints(double[] x, double[] y)
        {
            return new LoessRegression(x, y);
        }

        public string GetRegressionDescription(double r, double window)
        {
            throw new NotImplementedException();
        }

        public void GetCurve(RetentionTimeStatistics statistics, out double[] hydroScores, out double[] predictions)
        {
            throw new NotImplementedException();
        }
    }
}
