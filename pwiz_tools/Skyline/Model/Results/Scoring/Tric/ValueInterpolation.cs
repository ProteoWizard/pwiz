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

namespace pwiz.Skyline.Model.Results.Scoring.Tric
{
    class ValueInterpolation
    {
        private readonly int[] _sortedBValuesIndexArr;
        private readonly IList<double> _valuesA;
        private readonly IList<double> _valuesB;

        public ValueInterpolation(IList<double> valuesA, IList<double> valuesB)
        {
            _valuesA = valuesA;
            _valuesB = valuesB;
            _sortedBValuesIndexArr = new int[valuesA.Count];
            for (int i = 0; i < valuesA.Count; i++)
            {
                _sortedBValuesIndexArr[i] = i;
            }
            Array.Sort(_sortedBValuesIndexArr, (x, y) => valuesB[x].CompareTo(valuesB[y]));
        }

        public double GetValueAForValueB(double bValue)
        {
            var orderIndex = FindAdjacentIndex(bValue, 0, _valuesB.Count);
            var adjIndex = _sortedBValuesIndexArr[orderIndex];
            if (orderIndex == _valuesB.Count - 1)
                return _valuesA[adjIndex];
            else
            {
                var adjIndexRight = _sortedBValuesIndexArr[orderIndex + 1];
                return Interpolate(bValue, _valuesB[adjIndex], _valuesA[adjIndex],
                    _valuesB[adjIndexRight], _valuesA[adjIndexRight]);
            }
        }

        private double Interpolate(double x, double x1, double y1, double x2, double y2)
        {
            if(x1 == x2)
                return y1;
            return y1 + (y2 - y1)/(x2 - x1)*(x - x1);
        }

        private int FindAdjacentIndex(double value, int start, 
            int length)
        {
            if (length == 1)
            {
                return start;
            }
            else
            {
                int curIndex = start + length/2;
                double difference = _valuesB[_sortedBValuesIndexArr[curIndex]] - value;
                if (difference == 0)
                {
                    return curIndex;
                }
                else if (difference > 0)
                {
                    return FindAdjacentIndex(value, start, curIndex - start);
                }
                else
                {
                    return FindAdjacentIndex(value, curIndex, length - (curIndex - start));
                }
            }
        }
    }
}