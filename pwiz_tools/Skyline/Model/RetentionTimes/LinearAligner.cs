/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.Threading;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.RetentionTimes
{
    public class LinearAligner : Aligner
    {
        private RegressionLine _regressionLine;
        private RegressionLine _reverseRegressionLine;
        private double _rmsd;

        public LinearAligner(int origXFileIndex, int origYFileIndex)
            : base(origXFileIndex, origYFileIndex)
        {
        }

        public override void Train(double[] xArr, double[] yArr, CancellationToken token)
        {
            var statX = new Statistics(xArr);
            var statY = new Statistics(yArr);
            _regressionLine = new RegressionLine(statX.Slope(statY), statX.Intercept(statY));
            _reverseRegressionLine = new RegressionLine(statY.Slope(statX), statY.Intercept(statX));
            _rmsd = 0;
            for (int i = 0; i < xArr.Length; i++)
            {
                var pred = GetValue(xArr[i]);
                var diff = pred - yArr[i];
                _rmsd += diff * diff / xArr.Length;
            }
            _rmsd = Math.Sqrt(_rmsd);
        }

        public override double GetValue(double x)
        {
            return _regressionLine.GetY(x);
        }

        public override double GetValueReversed(double y)
        {
            return _reverseRegressionLine.GetY(y);
        }

        public override double GetRmsd()
        {
            return _rmsd;
        }

        public override void GetSmoothedValues(out double[] xArr, out double[] yArr)
        {
            //Should never be used since is only used for graphs
            throw new NotImplementedException(); 
        }
    }
}
