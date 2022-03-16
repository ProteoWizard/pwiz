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
using System.Linq;
using System.Threading;
using pwiz.Common.DataAnalysis;

namespace pwiz.Skyline.Model.RetentionTimes
{
    public class LoessAligner : Aligner
    {
        private double _maxX;
        private double _minX;
        private double _maxY;
        private double _minY;
        private double _rmsd;

        private double[] _xArr;
        private double[] _yArrSmoothed;

        private double[] _yArrOrig;
        private double[] _reverseXArr;
        private double[] _reverseYArr;

        private readonly double _bandwidth;
        private readonly int _robustIters;

        public LoessAligner(int origXFileIndex, int origYFileIndex, 
            double bandwidth = LoessInterpolator.DEFAULT_BANDWIDTH, int robustIters = LoessInterpolator.DEFAULT_ROBUSTNESS_ITERS)
            : base(origXFileIndex, origYFileIndex)
        {
            _bandwidth = bandwidth;
            _robustIters = robustIters;
        }

        public LoessAligner(double bandwidth = LoessInterpolator.DEFAULT_BANDWIDTH, int robustIters = LoessInterpolator.DEFAULT_ROBUSTNESS_ITERS)
        {
            _bandwidth = bandwidth;
            _robustIters = robustIters;
        }

        public override void Train(double[] xArr, double[] yArr, CancellationToken token) 
        {
            //Calculate lowess
            Array.Sort(xArr, yArr);
            double[] lowessArr;
            if (xArr.Length > 2)
            {
                LoessInterpolator interpolator = new LoessInterpolator(Math.Max(_bandwidth, 2.0 / xArr.Length), _robustIters);
                lowessArr = interpolator.Smooth(xArr, yArr, token);
            }
            else
            {
                lowessArr = yArr;
            }

            _minX = xArr[0];
            _maxX = xArr[xArr.Length - 1];

            _minY = lowessArr.Min();
            _maxY = lowessArr.Max();

            _xArr = xArr;
            _yArrOrig = yArr;
            _yArrSmoothed = lowessArr;

            var sum = 0.0;
            for (int i = 0; i < _yArrOrig.Length; i++)
            {
                var e = _yArrOrig[i] - lowessArr[i];
                sum += (e * e)/_yArrOrig.Length;
            }
            _rmsd = Math.Sqrt(sum);

            if (CanCalculateReverseRegression)
            {
                //We must copy arrays and sort twice since
                //X and Y arrays are not necessarily monotonically increasing
                _reverseXArr = new double[_xArr.Length];
                _reverseYArr = new double[_yArrSmoothed.Length];

                Array.Copy(_xArr, _reverseYArr, _xArr.Length);
                Array.Copy(_yArrSmoothed, _reverseXArr, _yArrSmoothed.Length);

                Array.Sort(_reverseXArr, _reverseYArr);
            }
        }

        public override double GetValue(double x)
        {
            return GetValueFor(_xArr, _yArrSmoothed, x, _minX, _maxX);        
        }

        public override double GetValueReversed(double y)
        {
            return GetValueFor(_reverseXArr, _reverseYArr, y, _minY, _maxY);
        }

        private double GetValueFor(double[] indArr, double[] depArr, double value, double min, double max)
        {
            if (value < min)
            {
                return GetValueForExtremeLeft(indArr, depArr, value, min);
            }
            else if (value > max)
            {
                return GetValueForExtremeRight(indArr, depArr, value, max);
            }
            else
            {
                var adjacent = GetAdjacentIndex(value, indArr, 0, indArr.Length);
                if (value == indArr[adjacent])
                {
                    return depArr[adjacent];
                }
                else if (value < indArr[adjacent])
                {
                    var left = adjacent - 1;
                    while (indArr[adjacent] == indArr[left])
                    {
                        left--;
                        if (left < 0)
                        {
                            break;
                        }
                    }
                    if (left < 0)
                    {
                        return GetValueForExtremeLeft(indArr, depArr, value, min);
                    }
                    return interpolate(indArr[left], depArr[left],  indArr[adjacent], depArr[adjacent],
                        value);
                }
                else
                {
                    var right = adjacent + 1;
                    while (indArr[adjacent] == indArr[right])
                    {
                        right++;
                        if (right >= indArr.Length)
                            break;
                    }
                    if (right >= indArr.Length)
                    {
                        return GetValueForExtremeRight(indArr,depArr,value,max);
                    }
                    return interpolate(indArr[adjacent], depArr[adjacent], indArr[right], depArr[right],
                        value);
                }
            }
        }

        private static double GetValueForExtremeLeft(double[] indArr, double[] depArr, double value, double min)
        {
            var slope = -1.0;
            for (var i = 1; i < indArr.Length && slope < 0; i ++)
            {
                if (indArr[i] == min)
                {
                    continue;
                }
                slope = (depArr[i] - depArr[0])/(indArr[i] - min);
            }
            return depArr[0] - slope*(min - value);
        }

        private static double GetValueForExtremeRight(double[] indArr, double[] depArr, double value, double max)
        {
            var slope = -1.0;
            for (int i = indArr.Length - 2; i >= 0 && slope < 0; i --)
            {
                if (indArr[i] == max)
                {
                    continue;        
                }
                slope = (depArr[depArr.Length - 1] - depArr[i])/(max - indArr[i]);
            }
            return depArr[depArr.Length - 1] + slope*(value - max);
        }

        private double interpolate(double lx, double ly, double rx, double ry, double x)
        {
            return ly + (x - lx)*(ry - ly)/(rx - lx);
        }

        private int GetAdjacentIndex(double value, double[] arr, int start, int length)
        {
            if (length <= 2)
            {
                return start;
            }
            else
            {
                int mid = start + length/2;
                if (value == arr[mid])
                {
                    return mid;
                }
                else if (value > arr[mid])
                {
                    return GetAdjacentIndex(value, arr, mid + 1, length - length/2 - 1);
                }
                else
                {
                    return GetAdjacentIndex(value, arr, start, length/2);
                }
            }
        }

        public override double GetRmsd()
        {
            return _rmsd;
        }

        public override void GetSmoothedValues(out double[] xArr, out double[] yArr)
        {
            xArr = _xArr;
            yArr = _yArrSmoothed;
        }
    }

   
}