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
using System.Text;
using ZedGraph;

namespace pwiz.MSGraph
{
    public class MSPointList : IPointList, IEnumerable<PointPair>
    {
        private PointPairList fullPointList;
        private PointPairList scaledPointList;
        private List<int> scaledMaxIndexList;
        private int scaledWidth;
        private double scaledMin;
        private double scaledMax;
        private double scaleRange;
        private double scaleFactor;
        private int scaledMinIndex;
        private int scaledMaxIndex;

        public MSPointList( IPointList sourcePointList )
        {
            fullPointList = new ZedGraph.PointPairList( sourcePointList );
            scaledPointList = new ZedGraph.PointPairList();
            scaledMaxIndexList = new List<int>();
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
            if( fullPointList.Count == 0 )
                return;

            min = Math.Max( min, fullPointList[0].X );
            max = Math.Min( max, fullPointList[fullPointList.Count - 1].X );

            if( scaledWidth == width && scaledMin == min && scaledMax == max )
                return;

            scaledWidth = width;
            scaledMin = min;
            scaledMax = max;
            scaleRange = max - min;
            if( scaleRange == 0 )
                return;
            scaleFactor = width / scaleRange;

            // store 4 points for each bin (entry, min, max, exit)
            scaledPointList.Clear();
            scaledPointList.Capacity = width * 4;

            // store just the index of the max point for each bin
            scaledMaxIndexList.Clear();
            scaledMaxIndexList.Capacity = width;
            for( int i = 0; i < width; ++i )
                scaledMaxIndexList.Add( -1 );

            int lastBin = -1;
            int curBinEntryIndex = -1;
            int curBinMinIndex = -1;
            int curBinMaxIndex = -1;
            int curBinExitIndex = -1;
            for( int i = 0; i < fullPointList.Count; ++i )
            {
                PointPair point = fullPointList[i];

                if( point.X < min )
                    continue;

                if( point.X > max )
                    break;

                int curBin = Math.Max( 1, (int) Math.Round( scaleFactor * ( point.X - min ) ) );
                if( curBin > lastBin ) // new bin, insert points of last bin
                {
                    if( lastBin > -1 )
                    {
                        scaledMinIndex = curBinMinIndex;
                        scaledPointList.Add( fullPointList[curBinEntryIndex] );
                        if( curBinEntryIndex != curBinMinIndex )
                            scaledPointList.Add( fullPointList[curBinMinIndex] );
                        if( curBinEntryIndex != curBinMaxIndex &&
                            curBinMinIndex != curBinMaxIndex )
                            scaledPointList.Add( fullPointList[curBinMaxIndex] );
                        if( curBinEntryIndex != curBinMaxIndex &&
                            curBinMinIndex != curBinMaxIndex &&
                            curBinMaxIndex != curBinExitIndex )
                            scaledPointList.Add( fullPointList[curBinExitIndex] );
                        if( fullPointList[curBinMaxIndex].Y != 0 )
                            scaledMaxIndexList[lastBin - 1] = curBinMaxIndex;
                    }
                    lastBin = curBin;
                    curBinEntryIndex = i;
                    curBinMinIndex = i;
                    curBinMaxIndex = i;
                } else // same bin, set exit point
                {
                    curBinExitIndex = i;
                    if( point.Y > fullPointList[curBinMaxIndex].Y )
                        scaledMaxIndex = curBinMaxIndex = i;
                    else if( point.Y < fullPointList[curBinMinIndex].Y )
                        curBinMinIndex = i;
                }
            }

            if( lastBin > -1 )
            {
                scaledMinIndex = curBinMinIndex;
                scaledPointList.Add( fullPointList[curBinEntryIndex] );
                if( curBinEntryIndex != curBinMinIndex )
                    scaledPointList.Add( fullPointList[curBinMinIndex] );
                if( curBinEntryIndex != curBinMaxIndex &&
                    curBinMinIndex != curBinMaxIndex )
                    scaledPointList.Add( fullPointList[curBinMaxIndex] );
                if( curBinEntryIndex != curBinMaxIndex &&
                    curBinMinIndex != curBinMaxIndex &&
                    curBinMaxIndex != curBinExitIndex )
                    scaledPointList.Add( fullPointList[curBinExitIndex] );
                if( fullPointList[curBinMaxIndex].Y != 0 && lastBin > 0 )
                    scaledMaxIndexList[lastBin-1] = curBinMaxIndex;
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
            get { return fullPointList.Count; }
        }

        public int ScaledCount
        {
            get { return scaledPointList.Count; }
        }

        public int MaxCount
        {
            get { return scaledMaxIndexList.Count; }
        }

        public PointPairList FullList { get { return fullPointList; } }
        public PointPairList ScaledList { get { return scaledPointList; } }
        public List<int> ScaledMaxIndexList { get { return scaledMaxIndexList; } }

        /// <summary>
        /// Returns the index of the point in the scaled list with the lowest X value greater than or equal to 'x'; returns -1 if no such value is in the list
        /// </summary>
        public int LowerBound( double x )
        {
            if( scaledPointList.Count == 0 ||
                scaledPointList[scaledPointList.Count - 1].X < x )
                return -1;

            int min = 0;
            int max = scaledPointList.Count;
            int best = max - 1;
            while( true )
            {
                int i = ( max + min ) / 2;
                if( scaledPointList[i].X < x )
                {
                    if( min == i )
                        return ( max == scaledPointList.Count ? -1 : max );
                    min = i;
                } else
                {
                    best = i;
                    max = i;
                    if( i == 0 )
                        break;
                }
            }
            return best;
        }

        public int GetNearestMaxIndexToBin( int bin )
        {
            if( scaledMaxIndexList[bin] >= 0 )
                return scaledMaxIndexList[bin];

            int i=1;
            while( bin + i < scaledMaxIndexList.Count && bin - i >= 0 )
            {
                if( bin + i < scaledMaxIndexList.Count && scaledMaxIndexList[bin + i] >= 0 )
                    return scaledMaxIndexList[bin + i];
                if( bin - i >= 0 && scaledMaxIndexList[bin - i] >= 0 )
                    return scaledMaxIndexList[bin - i];
                ++i;
            }
            return -1;
        }

        public PointPair this[int index]
        {
            get
            {
                if( ScaledCount > 0 )
                    return scaledPointList[index];
                else
                    return fullPointList[index];
            }
        }

        public object Clone()
        {
            return this;
        }

        public IEnumerator<PointPair> GetEnumerator()
        {
            if( ScaledCount > 0 )
                return scaledPointList.GetEnumerator();
            else
                return fullPointList.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            if( ScaledCount > 0 )
                return scaledPointList.GetEnumerator();
            else
                return fullPointList.GetEnumerator();
        }
    }
}
