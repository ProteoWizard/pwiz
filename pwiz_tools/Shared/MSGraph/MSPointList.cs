//
// $Id: MSPointList.cs 1599 2009-12-04 01:35:39Z brendanx $
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//

using System;
using System.Collections.Generic;
using ZedGraph;

namespace pwiz.MSGraph
{
    public class MSPointList : IPointList, IEnumerable<PointPair>
    {
        private readonly PointPairList _fullPointList;
        private readonly PointPairList _scaledPointList;
        private readonly List<int> _scaledMaxIndexList;
        private int _scaledWidth;
        private double _scaledMin;
        private double _scaledMax;
        private double _scaleRange;
        private double _scaleFactor;

        public MSPointList( IPointList sourcePointList )
        {
            _fullPointList = new PointPairList( sourcePointList );
            _scaledPointList = new PointPairList();
            _scaledMaxIndexList = new List<int>();
        }

        // returns the entire point list downsampled for a given width (in pixels)
        public void SetScale( int width )
        {
            SetScale( width, 0, Double.MaxValue );
        }

        // returns a range (subset) of the point list downsampled for a given width (in pixels)
        public void SetScale( Scale scale, int width )
        {
            SetScale( width, scale.Min, scale.Max );
        }

        // returns a range (subset) of the point list downsampled for a given width (in pixels)
        public void SetScale( int width, double min, double max )
        {
            if( _fullPointList.Count == 0 )
                return;

            min = Math.Max( min, _fullPointList[0].X );
            max = Math.Min( max, _fullPointList[_fullPointList.Count - 1].X );

            if( _scaledWidth == width && _scaledMin == min && _scaledMax == max )
                return;

            double min1 = double.MinValue;
            double max1 = double.MaxValue;
            for (int i = 0; i < _fullPointList.Count; ++i)
            {
                double x = _fullPointList[i].X;
                if (x < min && x > min1)
                    min1 = x;
                if (x > max && x < max1)
                    max1 = x;
            }
            if (min1 > double.MinValue)
                min = min1;
            if (max1 < double.MaxValue)
                max = max1;

            _scaledWidth = width;
            _scaledMin = min;
            _scaledMax = max;
            _scaleRange = max - min;
            if( _scaleRange == 0 )
                return;
            _scaleFactor = width / _scaleRange;

            // store 4 points for each bin (entry, min, max, exit)
            _scaledPointList.Clear();
            _scaledPointList.Capacity = width * 4;

            // store just the index of the max point for each bin
            _scaledMaxIndexList.Clear();
            _scaledMaxIndexList.Capacity = width;
            for( int i = 0; i < width; ++i )
                _scaledMaxIndexList.Add( -1 );

            int lastBin = -1;
            int curBinEntryIndex = -1;
            int curBinMinIndex = -1;
            int curBinMaxIndex = -1;
            int curBinExitIndex = -1;
            for( int i = 0; i < _fullPointList.Count; ++i )
            {
                PointPair point = _fullPointList[i];

                if( point.X < min )
                    continue;

                if( point.X > max )
                    break;

                int curBin = Math.Max( 1, (int) Math.Round( _scaleFactor * ( point.X - min ) ) );
                if( curBin > lastBin ) // new bin, insert points of last bin
                {
                    if( lastBin > -1 )
                    {
                        _scaledPointList.Add( _fullPointList[curBinEntryIndex] );
                        if( curBinEntryIndex != curBinMinIndex ||
                            curBinEntryIndex != curBinMaxIndex )
                        {
                            if( curBinMinIndex != curBinMaxIndex )
                            {
                                if( _fullPointList[curBinMinIndex].X < _fullPointList[curBinMaxIndex].X )
                                {
                                    if( curBinMinIndex != curBinEntryIndex )
                                        _scaledPointList.Add( _fullPointList[curBinMinIndex] );
                                    if (curBinMaxIndex != curBinEntryIndex)
                                        _scaledPointList.Add( _fullPointList[curBinMaxIndex] );
                                }
                                else
                                {
                                    if (curBinMaxIndex != curBinEntryIndex)
                                        _scaledPointList.Add( _fullPointList[curBinMaxIndex] );
                                    if (curBinMinIndex != curBinEntryIndex)
                                        _scaledPointList.Add( _fullPointList[curBinMinIndex] );
                                }
                            }
                            else if( curBinMinIndex != curBinEntryIndex )
                                _scaledPointList.Add( _fullPointList[curBinMinIndex] );
                        }
                        if( curBinEntryIndex != curBinMaxIndex &&
                            curBinMinIndex != curBinMaxIndex &&
                            curBinMaxIndex != curBinExitIndex )
                            _scaledPointList.Add( _fullPointList[curBinExitIndex] );
                        if( _fullPointList[curBinMaxIndex].Y != 0 )
                            _scaledMaxIndexList[lastBin - 1] = curBinMaxIndex;
                    }
                    lastBin = curBin;
                    curBinEntryIndex = i;
                    curBinMinIndex = i;
                    curBinMaxIndex = i;
                } else // same bin, set exit point
                {
                    curBinExitIndex = i;
                    if( point.Y > _fullPointList[curBinMaxIndex].Y )
                        curBinMaxIndex = i;
                    else if( point.Y < _fullPointList[curBinMinIndex].Y )
                        curBinMinIndex = i;
                }
            }

            if( lastBin > -1 )
            {
                _scaledPointList.Add( _fullPointList[curBinEntryIndex] );
                if( curBinEntryIndex != curBinMinIndex )
                    _scaledPointList.Add( _fullPointList[curBinMinIndex] );
                if( curBinEntryIndex != curBinMaxIndex &&
                    curBinMinIndex != curBinMaxIndex )
                    _scaledPointList.Add( _fullPointList[curBinMaxIndex] );
                if( curBinEntryIndex != curBinMaxIndex &&
                    curBinMinIndex != curBinMaxIndex &&
                    curBinMaxIndex != curBinExitIndex )
                    _scaledPointList.Add( _fullPointList[curBinExitIndex] );
                if( _fullPointList[curBinMaxIndex].Y != 0 && lastBin > 0 )
                    _scaledMaxIndexList[lastBin-1] = curBinMaxIndex;
            }
        }

        public int Count
        {
            get
            {
                if( ScaledCount > 0 )
                    return ScaledCount;
                else
                    return FullCount;
            }
        }

        public int FullCount
        {
            get { return _fullPointList.Count; }
        }

        public int ScaledCount
        {
            get { return _scaledPointList.Count; }
        }

        public int MaxCount
        {
            get { return _scaledMaxIndexList.Count; }
        }

        public PointPairList FullList { get { return _fullPointList; } }
        public PointPairList ScaledList { get { return _scaledPointList; } }
        public List<int> ScaledMaxIndexList { get { return _scaledMaxIndexList; } }

        /// <summary>
        /// Returns the index of the point in the full list with the largest X value less than or equal to 'x'; returns -1 if no such value is in the list
        /// </summary>
        public int FullLowerBound (double x)
        {
            return GetLowerBound(_fullPointList, x);
        }

        /// <summary>
        /// Returns the index of the point in the scaled list with the largest X value less than or equal to 'x'; returns -1 if no such value is in the list
        /// </summary>
        public int ScaledLowerBound( double x )
        {
            return GetLowerBound(_scaledPointList, x);
        }

        private int GetLowerBound(PointPairList pointList, double x)
        {
            if (pointList.Count == 0 || pointList[0].X > x)
                return -1;

            int min = 0;
            int max = pointList.Count;
            while (max - 1 > min)
            {
                int i = (max + min) / 2;
                if (pointList[i].X <= x)
                    min = i;
                else
                    max = i;
            }
            return min;
        }

        /// <summary>
        /// Returns the index of the point with the largest X value less than or equal to 'x'; returns -1 if no such value is in the list
        /// </summary>
        public int LowerBound( double x ) {return ScaledCount > 0 ? ScaledLowerBound(x) : FullLowerBound(x);}

        public int GetNearestMaxIndexToBin( int bin )
        {
            if( _scaledMaxIndexList[bin] >= 0 )
                return _scaledMaxIndexList[bin];

            int i=1;
            while( bin + i < _scaledMaxIndexList.Count && bin - i >= 0 )
            {
                if( bin + i < _scaledMaxIndexList.Count && _scaledMaxIndexList[bin + i] >= 0 )
                    return _scaledMaxIndexList[bin + i];
                if( bin - i >= 0 && _scaledMaxIndexList[bin - i] >= 0 )
                    return _scaledMaxIndexList[bin - i];
                ++i;
            }
            return -1;
        }

        public PointPair this[int index]
        {
            get
            {
                if( ScaledCount > 0 )
                    return _scaledPointList[index];
                else
                    return _fullPointList[index];
            }
        }

        public object Clone()
        {
            return this;
        }

        public IEnumerator<PointPair> GetEnumerator()
        {
            if( ScaledCount > 0 )
                return _scaledPointList.GetEnumerator();
            else
                return _fullPointList.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            if( ScaledCount > 0 )
                return _scaledPointList.GetEnumerator();
            else
                return _fullPointList.GetEnumerator();
        }
    }
}
