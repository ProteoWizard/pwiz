/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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

namespace pwiz.MSGraph
{
    /// <summary>
    /// Optimize display of heat map data using a recursive quad-tree subdivision of the
    /// data. This allows us to identify the highest intensity sample that falls within
    /// any cell of the quad-tree. Cells are recursively refined to display at any resolution/
    /// fidelity the user wants.
    /// </summary>
    public class HeatMapData
    {
        private readonly Cell _cell;

        /// <summary>
        /// Construct the quad-tree from a given list of 3D data points.
        /// </summary>
        public HeatMapData(List<Point3D> points)
        {
            _cell = new Cell(points);
        }

        /// <summary>
        /// Return the point with the maximum intensity in the whole list.
        /// </summary>
        public Point3D MaxPoint
        {
            get { return _cell.MaxPoint; }
        }

        /// <summary>
        /// Return a list of points that falls within the given x and y range for a cell size
        /// that has been recursively refined to be smaller than the given cell dimensions.
        /// </summary>
        public List<Point3D> GetPoints(double xMin, double xMax, double yMin, double yMax, double cellWidth, double cellHeight)
        {
            var points = new List<Point3D>(5000);
            _cell.GetPoints((float) xMin, (float) xMax, (float) yMin, (float) yMax, (float) cellWidth, (float) cellHeight, points);
            return points;
        }

        /// <summary>
        /// A cell within the quad-tree data structure.
        /// </summary>
        private class Cell
        {
            private readonly List<Point3D> _points;
            private readonly float _xMin;
            private readonly float _xMax;
            private readonly float _yMin;
            private readonly float _yMax;
            private readonly Point3D _maxPoint;
            private Cell[] _cells;

            /// <summary>
            /// Construct a cell that contains the given list of data points.
            /// </summary>
            public Cell(List<Point3D> points)
            {
                _points = points;
                _xMin = float.MaxValue;
                _xMax = float.MinValue;
                _yMin = float.MaxValue;
                _yMax = float.MinValue;
                foreach (var point in _points)
                {
                    if (point.Z <= 0)
                        continue;
                    if (_maxPoint == null || _maxPoint.Z < point.Z)
                        _maxPoint = point;

                    _xMin = Math.Min(_xMin, point.X);
                    _xMax = Math.Max(_xMax, point.X);
                    _yMin = Math.Min(_yMin, point.Y);
                    _yMax = Math.Max(_yMax, point.Y);
                }
            }

            /// <summary>
            /// Return the point within the cell with maximum intensity.
            /// </summary>
            public Point3D MaxPoint
            {
                get { return _maxPoint; }
            }

            /// <summary>
            /// Add to a list of points any that fall within the given x and y range for a cell size
            /// that has been recursively refined to be smaller than the given cell dimensions.
            /// </summary>
            public void GetPoints(float xMin, float xMax, float yMin, float yMax, float cellWidth, float cellHeight,
                ICollection<Point3D> returnedPoints)
            {
                // Add no points if the range does not intersect this cell's boundaries.
                if (xMin > _xMax ||
                    xMax < _xMin ||
                    yMin > _yMax ||
                    yMax < _yMin)
                    return;

                // If this cell is the same size or smaller than the required dimensions,
                // add the maximum intensity point to the list.
                if (cellWidth > _xMax - _xMin && cellHeight > _yMax - _yMin)
                {
                    returnedPoints.Add(_maxPoint);
                    return;
                }

                if (_cells == null)
                    _cells = CreateCells();

                // Add the maximum intensity points from each of the 4 cells inside this cell (and recurse to
                // the right cell size).
                for (int i = 0; i < 4; i++)
                {
                    if (_cells[i] != null)
                        _cells[i].GetPoints(xMin, xMax, yMin, yMax, cellWidth, cellHeight, returnedPoints);
                }
            }

            /// <summary>
            /// Create up to 4 cells within this cell.
            /// </summary>
            private Cell[] CreateCells()
            {
                float xMid = (_xMin + _xMax)/2;
                float yMid = (_yMin + _yMax)/2;
                var pointLists = new List<Point3D>[4];
                for (int i = 0; i < 4; i++)
                    pointLists[i] = new List<Point3D>(_points.Count/4);

                // Divide points among four quadrants.
                foreach (var point in _points)
                {
                    int index = (point.X > xMid) ? 1 : 0;
                    if (point.Y > yMid)
                        index += 2;
                    pointLists[index].Add(point);
                }

                // Create cell for each non-empty quadrant.
                var cells = new Cell[4];
                for (int i = 0; i < 4; i++)
                {
                    if (pointLists[i].Count > 0)
                    {
                        var newCell = new Cell(pointLists[i]);
                        // Make sure there was a maximum point, or the boundaries
                        // will end up float.MinValue and float.MaxValue
                        if (newCell.MaxPoint != null)
                            cells[i] = newCell;
                    }
                }

                return cells;
            }
        }
    }
}
