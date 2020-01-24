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
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Irt
{
    public class LogRegression : IRegressionFunction
    {
        private readonly double _a;
        private readonly double _b;

        public LogRegression(double a, double b)
        {
            _a = a;
            _b = b;
        }

        public double GetY(double x)
        {
            return _a + _b * Math.Log(x);
        }
        public double Slope => throw new NotSupportedException();
        public double Intercept => throw new NotSupportedException();

        public string GetRegressionDescription(double r, double window)
        {
            throw new NotImplementedException();
        }

        public void GetCurve(RetentionTimeStatistics statistics, out double[] hydroScores, out double[] predictions)
        {
            throw new NotImplementedException();
        }

        public static bool TryGet(IList<double> listIndependent, IList<double> listDependent, int minPoints, out IRegressionFunction regression, IList<Tuple<double, double>> removedValues = null)
        {
            regression = null;
            removedValues?.Clear();
            if (listIndependent.Count != listDependent.Count || listIndependent.Count < minPoints)
                return false;

            var listX = new List<double>(listIndependent);
            var listY = new List<double>(listDependent);

            double correlation;
            while (true)
            {
                var statIndependent = new Statistics(listX.Select(x => Math.Log(x)));
                var statDependent = new Statistics(listY);
                regression = new LogRegression(statDependent.Slope(statIndependent), statDependent.Intercept(statIndependent));
                correlation = statDependent.R(statIndependent);

                if (correlation >= RCalcIrt.MIN_IRT_TO_TIME_CORRELATION || listX.Count <= minPoints)
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

            return correlation >= RCalcIrt.MIN_IRT_TO_TIME_CORRELATION;
        }
    }
}
