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
using System.Threading;
using MathNet.Numerics.Statistics;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.RetentionTimes
{
    public class CosineGaussian
    {
        private readonly float _stdev;
        private readonly float _A;
        private readonly float _M;
        private readonly float _min;
        private readonly float _max;
        public CosineGaussian(float stdev)
        {
            _stdev = stdev;
            _A = (float)(Math.PI / (8.0 * stdev));
            _M = (float)(Math.PI / (4.0 * stdev));
            _min = -2 * stdev;
            _max = 2 * stdev;
        }

        public double GetDensity(double delta)
        {
            if (delta > _max || delta < _min)
            {
                return 0;
            }

            return _A * Math.Cos(-delta * _M);
        }

        public double Stdev { get { return _stdev; } }
    }

    public class KdeAligner : Aligner
    {
        private readonly int _resolution;
        private double _minX;
        private double _maxX;
        private double _minY;
        private double _maxY;
        private int _maxXi;
        private int _minXi;
        private int _maxYi;
        private int _minYi;
        private int[] _consolidatedXY;
        private int[] _consolidatedYX;
        private double _rmsd;
        private double[] _smoothedY;
        private double[] _xArr;

        public KdeAligner(int origXFileIndex, int origYFileIndex, int resolution = 1000) : base(origXFileIndex, origYFileIndex)
        {
            _resolution = resolution;
        }

        /// <summary>
        /// Constructs a KdeAligner with a specific resolution and stretchFactor
        /// </summary>
        /// <param name="resolution">The number of points in the X and Y axes </param>
        public KdeAligner(int resolution = 1000)
        {
            _resolution = resolution;
        }

        public int Resolution
        {
            get { return _resolution; }
        }

        /// <summary>
        /// When looking for the starting point with the highest intensity,
        /// this value constrains the search to a window in the center whose
        /// size is a fraction of the entire window
        /// </summary>
        public double StartingWindowSizeProportion { get; set; }

        public double GetScaledX(int coordinate)
        {
            return _minX + coordinate * (_maxX - _minX) / _resolution;
        }
        public int GetXCoordinate(double x)
        {
            return (int)Math.Round(_resolution * (x - _minX) / (_maxX - _minX));
        }

        public double GetScaledY(int coordinate)
        {
            return _minY + coordinate * (_maxY - _minY) / _resolution;
        }

        public int GetYCoordinate(double y)
        {
            return (int)Math.Round(_resolution * (y - _minY) / (_maxY - _minY));
        }

        public override void Train(double[] xArr, double[] yArr, CancellationToken token)
        {
            Array.Sort(xArr, yArr);
            GetHistogramAndTrain(xArr, yArr, token);
        }

        private static readonly double BANDWIDTH_TO_STDEV = 2.0 * Math.Sqrt(2.0 * Math.Log(2.0));

        public float[,] GetHistogramAndTrain(double[] xArr, double[] yArr, CancellationToken cancellationToken)
        {
            _xArr = xArr;
            double[] xNormal;
            double[] yNormal;

            GetResolutionNormalizedPoints(xArr, yArr, out xNormal, out yNormal,
                out _minX, out _maxX, out _minY, out _maxY);

            var indStdev = xNormal.StandardDeviation();
            var depStdev = yNormal.StandardDeviation();

            // Silverman's rule of thumb
            var bandWidth = Math.Pow(xArr.Length, -1f / 6f) * (indStdev + depStdev) / 2.0f;
            // division by 2.3548 converts bandwidth (fwhm) to stdev for gaussians
            var stdev = (float)Math.Min(_resolution / 40f, bandWidth / BANDWIDTH_TO_STDEV);

            float[,] stamp = GetCosineGaussianStamp(new CosineGaussian(stdev));

            float[,] histogram = new float[_resolution, _resolution];

            for (int p = 0; p < xNormal.Length; p++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int x = (int)xNormal[p];
                int y = (int)yNormal[p];
                Stamp(histogram, stamp, x, y);
            }

            FindBestXY(histogram, out int bestXi, out int bestYi);
            var points = new LinkedList<Tuple<int, int>>();
            points.AddFirst(new Tuple<int, int>(bestXi, bestYi));
            TraceNorthEast(histogram, bestXi, bestYi, points);
            TraceSouthWest(histogram, bestXi, bestYi, points);
            _consolidatedXY = GetConsolidatedXY(points);
            if (CanCalculateReverseRegression)
            {
                _consolidatedYX = GetConsolidateYX(points);
            }
            _rmsd = 0;
            _smoothedY = new double[xArr.Length];
            for (int i = 0; i < xArr.Length; i++)
            {
                var x = xArr[i];
                var y = yArr[i];
                var pred = GetValue(x);
                _smoothedY[i] = pred;
                var diff = y - pred;
                _rmsd += diff * diff / xArr.Length;
            }
            _rmsd = Math.Sqrt(_rmsd);
            return histogram;
        }


        void FindBestXY(float[,] histogram, out int xBest, out int yBest)
        {
            int startX = (int)Math.Floor(histogram.GetLength(0) * StartingWindowSizeProportion / 2);
            int xCount = (int)Math.Floor(histogram.GetLength(0) * (1 - StartingWindowSizeProportion) / 2);
            int startY = (int)Math.Floor(histogram.GetLength(1) * StartingWindowSizeProportion / 2);
            int yCount = (int)Math.Floor(histogram.GetLength(1) * (1 - StartingWindowSizeProportion) / 2);
            xCount = Math.Max(xCount, 1);
            yCount = Math.Max(yCount, 1);
            xBest = startX;
            yBest = startY;
            double best = double.MinValue;

            for (int i = 0; i < xCount; i++)
            {
                int x = startX + i;
                for (int j = 0; j < yCount; j++)
                {
                    int y = startY + j;
                    float val = histogram[x, y];
                    if (val > best)
                    {
                        best = val;
                        xBest = x;
                        yBest = y;
                    }
                }
            }
        }
        private int[] GetConsolidatedXY(ICollection<Tuple<int, int>> points)
        {
            var consolidatedXY = new int[_resolution];
            using var iterator = points.GetEnumerator();
            bool notFinished = iterator.MoveNext();
            while (notFinished && iterator.Current != null)
            {
                var minY = iterator.Current.Item2;
                var maxY = minY;
                var x = iterator.Current.Item1;
                var lastX = x;
                while ((notFinished = iterator.MoveNext()) && iterator.Current != null)
                {
                    if (iterator.Current.Item1 != lastX)
                        break;
                    maxY = iterator.Current.Item2;
                }
                consolidatedXY[x] = (int)Math.Round((minY + maxY) / 2.0);
            }

            return consolidatedXY;
        }

        private int[] GetConsolidateYX(ICollection<Tuple<int, int>> points)
        {
            var consolidatedYX = new int[_resolution];
            using var iterator = points.GetEnumerator();
            bool notFinished = iterator.MoveNext();
            while (notFinished && iterator.Current != null)
            {
                var minX = iterator.Current.Item1;
                var maxX = minX;
                var y = iterator.Current.Item2;
                var lastY = y;
                while ((notFinished = iterator.MoveNext()) && iterator.Current != null)
                {
                    if (iterator.Current.Item2 != lastY)
                        break;
                    maxX = iterator.Current.Item1;
                }
                consolidatedYX[y] = (int)Math.Round((minX + maxX) / 2.0);
            }

            return consolidatedYX;
        }

        private void TraceNorthEast(float[,] histogram, int bestXi, int bestYi, LinkedList<Tuple<int, int>> points)
        {
            _maxXi = bestXi;
            _maxYi = bestYi;
            int xi = bestXi;
            int yi = bestYi;
            while (true)
            {
                var north = yi + 1 < histogram.GetLength(1) ? histogram[xi, yi + 1] : -1;
                var east = xi + 1 < histogram.GetLength(0) ? histogram[xi + 1, yi] : -1;
                var northeast = north != -1 && east != -1 ? histogram[xi + 1, yi + 1] : -1;
                var max = Math.Max(north, Math.Max(east, northeast));
                Assume.IsFalse(double.IsNaN(max));
                if (max == -1)
                    break;
                if (northeast == max || east == north)
                {
                    xi++;
                    yi++;
                    _maxXi++;
                    _maxYi++;
                }
                else if (east == max)
                {
                    xi++;
                    _maxXi++;
                }
                else if (north == max)
                {
                    yi++;
                    _maxYi++;
                }
                points.AddLast(new Tuple<int, int>(xi, yi));
            }
        }

        private void TraceSouthWest(float[,] histogram, int bestXi, int bestYi, LinkedList<Tuple<int, int>> points)
        {
            _minXi = bestXi;
            _minYi = bestYi;
            var xi = bestXi;
            var yi = bestYi;
            while (true)
            {
                var south = yi - 1 >= 0 ? histogram[xi, yi - 1] : -1;
                var west = xi - 1 >= 0 ? histogram[xi - 1, yi] : -1;
                var southwest = south != -1 && west != -1 ? histogram[xi - 1, yi - 1] : -1;
                var max = Math.Max(south, Math.Max(west, southwest));
                Assume.IsFalse(double.IsNaN(max));
                if (max == -1)
                    break;
                if (southwest == max || south == west)
                {
                    xi--;
                    yi--;
                    _minXi--;
                    _minYi--;
                }
                else if (south == max)
                {
                    yi--;
                    _minYi--;
                }
                else
                {
                    xi--;
                    _minXi--;
                }
                points.AddFirst(new Tuple<int, int>(xi, yi));
            }
        }

        private void Stamp(float[,] histogram, float[,] stamp, int x, int y)
        {
            var stampLength0 = stamp.GetLength(0); // GetLength is more expensive than you'd think
            var stampLength1 = stamp.GetLength(1);
            var histogramLength0 = histogram.GetLength(0);
            var histogramLength1 = histogram.GetLength(1);

            for (int i = x - stampLength0/2; i <= x + stampLength0 / 2; i++)
            {
                if (i < 0)
                    continue;
                if (i >= histogramLength0)
                    break;
                for (int j = y - stampLength1/2; j <= y + stampLength1/2; j++)
                {
                    if (j < 0)
                        continue;
                    if (j >= histogramLength1)
                        break;
                    histogram[i, j] += stamp[i - x + stampLength0 / 2, j - y + stampLength1/2];
                }
            }
        }

        public static float[,] GetCosineGaussianStamp(CosineGaussian cG)
        {
            int stampRadius = (int)Math.Round(2.0f * cG.Stdev);

            var stamp = new float[stampRadius * 2 + 1, stampRadius * 2 + 1];
            for (int i = 0; i < stampRadius * 2 + 1; i++)
            {
                for (int j = 0; j < stampRadius * 2 + 1; j++)
                {
                    double deltaX = i - stampRadius;
                    double deltaY = j - stampRadius;
                    double delta;
                    delta = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
                    var value = (float)cG.GetDensity(delta);
                    if (float.IsNaN(value) || float.IsInfinity(value))
                    {
                        throw new InvalidOperationException(string.Format(@"Invalid value {0} at ({1},{2})",
                            value, i, j));
                    }
                    stamp[i, j] = value;
                }
            }
            return stamp;
        }

        private void GetResolutionNormalizedPoints(double[] indArr, double[] depArr, out double[] normalInd,
            out double[] normalDep, out double minInd, out double maxInd, out double minDep, out double maxDep)
        {
            minInd = double.MaxValue;
            maxInd = double.MinValue;
            minDep = double.MaxValue;
            maxDep = double.MinValue;
            foreach (var x in indArr)
            {
                minInd = Math.Min(minInd, x);
                maxInd = Math.Max(maxInd, x);
            }
            foreach (var y in depArr)
            {
                minDep = Math.Min(minDep, y);
                maxDep = Math.Max(maxDep, y);
            }

            normalInd = new double[indArr.Length];
            normalDep = new double[depArr.Length];
            for (int i = 0; i < normalDep.Length; i++)
            {
                normalInd[i] = IntegerInterpolate(indArr[i], minInd, maxInd, 0, _resolution - 1);
                normalDep[i] = IntegerInterpolate(depArr[i], minDep, maxDep, 0, _resolution - 1);
            }
        }

        private int IntegerInterpolate(double val, double minX, double maxX, int minY, int maxY)
        {
            return (int)Math.Round(Interpolate(val, minX, maxX, minY, maxY));
        }

        private double Interpolate(double val, double minX, double maxX, double minY, double maxY)
        {
            if (val < minX || val > maxX)
                return Double.NaN;
            return minY + (val - minX) / (maxX - minX) * (maxY - minY);
        }

        public override double GetValue(double x)
        {
            return _GetValueFor(x, _minX, _maxX, _minXi, _maxXi, _minY, _maxY, _minYi, _maxYi, _consolidatedXY);
        }

        public override double GetValueReversed(double y)
        {
            if (!CanCalculateReverseRegression)
                throw new Exception(@"KDE has not calculated reverse regression");
            return _GetValueFor(y, _minY, _maxY, _minYi, _maxYi, _minX, _maxX, _minXi, _maxXi, _consolidatedYX);
        }

        private double _GetValueFor(double ind, double minInd, double maxInd, int minIndNormal, int maxIndNormal,
            double minDep, double maxDep, int minDepNormal, int maxDepNormal, int[] arr)
        {
            int dep_i = -1;
            if (ind <= minInd)
                dep_i = minIndNormal;
            else if (ind >= maxInd)
                dep_i = maxIndNormal;
            else
            {
                int ind_i = IntegerInterpolate(ind, minInd, maxInd, minIndNormal, maxIndNormal);
                dep_i = arr[ind_i];
            }
            return Interpolate(dep_i, minDepNormal, maxDepNormal, minDep, maxDep);
        }

        public override double GetRmsd()
        {
            return _rmsd;
        }

        public override void GetSmoothedValues(out double[] xArr, out double[] yArr)
        {
            xArr = _xArr;
            yArr = _smoothedY;
        }
    }
}
