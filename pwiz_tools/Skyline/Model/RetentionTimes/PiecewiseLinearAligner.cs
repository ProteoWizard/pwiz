/*
 * Original author: Max Horowitz-Gelb <maxhg .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.RetentionTimes
{
    /// <summary>
    /// Aligner abstract class that defines a template for how Alligners should work for the Tric Algorithm
    /// In order to traverse the MST
    /// </summary>
    public abstract class PiecewiseLinearAligner
    {
        //The index of the the run that the independent values came from
        protected int _origXRun;
        //The index of the the run that the x values came from
        protected int _origYRun;

        protected double[] _xArr;
        protected double[] _yArr;
        protected double[] _yPred;
        protected double _rmsd;
        private bool _trained;

        /// <summary>
        /// Generic Constructor for an Aligner. Sets values and trains aligner
        /// </summary>
        /// <param name="xArr">Array of independent retention times for alignment</param>
        /// <param name="yArr">Array of x retention times for alignment</param>
        /// <param name="origXRun">Index of the run from which the independent values come from</param>
        /// <param name="origYRun">Index of the run from which the dependent values come from</param>
        protected PiecewiseLinearAligner(double[] xArr, double[] yArr, int origXRun, int origYRun)
        {
            if (yArr.Length != xArr.Length)
            {
                throw new ArgumentException("Length of independent and x arrays do not match.");    // Not L10N: developer only error
            }
            
            _origXRun = origXRun;
            _origYRun = origYRun;

            _xArr = xArr;
            _yArr = yArr;
            _trained = false;
        }

        public void Train()
        {
            Array.Sort(_xArr,_yArr);
            _yPred = GetSmoothedValues(_xArr, _yArr);
            _trained = true;
            _rmsd = GetRmsd();
            RegressionFunction = new PiecewiseLinearRegressionFunction(_xArr,_yPred,_rmsd);
        }

        protected abstract double[] GetSmoothedValues(double[] xArr, double[] yArr);

        public PiecewiseLinearRegressionFunction RegressionFunction { get; private set; }


        public Statistics getTransformedY()
        {
            if (!_trained)
                return null;
            return new Statistics(_yPred);
        }


        public double GetRmsd()
        {
            if (_yPred == null)
                return -1;
            
            var sum = 0.0;
            for (int i = 0; i < _yPred.Length; i++)
            {
                sum += (_yPred[i] - _yArr[i])*(_yPred[i] - _yArr[i]);
            }
            return Math.Sqrt(sum/_yPred.Length);
        }
    }

    public abstract class AlignerFactory
    {
        protected Dictionary<string, object> _extraParams;

        public AlignerFactory(Dictionary<String, object> extraParams)
        {
            _extraParams = extraParams;
        }
        public abstract PiecewiseLinearAligner GetInstance(double[] xArr, double[] yArr, int xRun , int yRun);
    }
}
