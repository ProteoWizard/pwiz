//============================================================================
//ZedGraph Class Library - A Flexible Line Graph/Bar Graph Library in C#
//Copyright © 2004  John Champion
//
//This library is free software; you can redistribute it and/or
//modify it under the terms of the GNU Lesser General Public
//License as published by the Free Software Foundation; either
//version 2.1 of the License, or (at your option) any later version.
//
//This library is distributed in the hope that it will be useful,
//but WITHOUT ANY WARRANTY; without even the implied warranty of
//MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
//Lesser General Public License for more details.
//
//You should have received a copy of the GNU Lesser General Public
//License along with this library; if not, write to the Free Software
//Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
//=============================================================================

/*
 * Original author: Rita Chupalov <ritach .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using System.Drawing;
using System.Linq;
using Newtonsoft.Json;

namespace ZedGraph
{
    public class LabelLayout
    {
        private GraphPane _graph;
        private int _cellSize;
        private Random _randGenerator = new Random(123);
        private PointF _chartOffset;
        private Dictionary<TextObj, LabeledPoint> _labeledPoints = new Dictionary<TextObj, LabeledPoint>();

        public Dictionary<TextObj, LabeledPoint> LabeledPoints => _labeledPoints;

        public List<LabeledPoint.PointLayout> PointsLayout
        {
            get
            {
                return _labeledPoints.Select(lp => new LabeledPoint.PointLayout(lp.Value)).ToList();
            }
        }

        public LabelLayout(GraphPane graph, int cellSize)
        {
            _graph = graph;
            _cellSize = cellSize;
            var chartRect = _graph.Chart.Rect;
            _chartOffset = new PointF(chartRect.Location.X, chartRect.Location.Y);
            FillDensityGrid();
        }

        private class GridCell
        {
            public PointF _location;
            public RectangleF _bounds;
            public float _density;
            // public PointF _gradient;
            public static Dictionary<Color, Brush> _brushes = new Dictionary<Color, Brush>();
        }

        // First index row, second index line
        private GridCell[][] _densityGrid;
        private Size _densityGridSize;

        private void FillDensityGrid()
        {
            var chartOffset = new Size((int)_chartOffset.X, (int)_chartOffset.Y);
            _densityGridSize.Width = ((int)_graph.Chart.Rect.Width) / _cellSize + 1;
            _densityGridSize.Height = ((int)_graph.Chart.Rect.Height) / _cellSize + 1;
            _densityGrid = new GridCell[_densityGridSize.Height][];
            for (var i = 0; i < _densityGridSize.Height; i++)
            {
                _densityGrid[i] = new GridCell[_densityGridSize.Width];
                for (var j = 0; j < _densityGridSize.Width; j++)
                {
                    var location = new Point(j * _cellSize, i * _cellSize) + chartOffset;
                    _densityGrid[i][j] = new GridCell()
                    {
                        _location = location,
                        _bounds = new RectangleF(location, new SizeF(_cellSize, _cellSize)),
                    };
                }
            }

            foreach (var line in _graph.CurveList.OfType<LineItem>().Where(c => c.Symbol.Type != SymbolType.None))
            {
                for (var i = 0; i < line.Points.Count; i++)
                {
                    if (!line.GetCoords(this._graph, i, out var coords))
                    {
                        continue;
                    }

                    var sides = Array.ConvertAll(coords.Split(','), int.Parse);
                    var markerRect = new Rectangle(sides[0], sides[1], sides[2] - sides[0],
                        sides[3] - sides[1]);

                    foreach (var cell in GetRectangleCells(markerRect))
                    {
                        var intersect = RectangleF.Intersect(markerRect, cell._bounds);
                        if (intersect != Rectangle.Empty)
                        {
                            cell._density += intersect.Height * intersect.Width;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Calculates goal function for a labeled point and a suggested label position.
        /// All coordinates are in screen pixels.
        /// </summary>
        /// <param name="pt">Center of the label box, in pixels</param>
        /// <param name="targetPoint">point being labeled.</param>
        /// <param name="labelSize"> in pixels </param>
        /// <returns>goal function value.</returns>
        private float GoalFuncion(PointF pt, PointF targetPoint, SizeF labelSize)
        {
            var pathCellCoord = CellIndexesFromXY(targetPoint);
            if (!IndexesWithinGrid(pathCellCoord))
                return 10000; //return really large value if the target point is outside of the density grid
            if (!IndexesWithinGrid(CellIndexesFromXY(pt)))
                return 10000;
            // Distance to the label is measured from the center or right/left ends, whichever is closer.
            var distPoints = new[]
                { pt - new SizeF(labelSize.Width / 2, 0), pt, pt + new SizeF(labelSize.Width / 2, 0) };
            var dist = distPoints.Min(p =>
            {
                var diff = new SizeF(p.X - targetPoint.X, p.Y - targetPoint.Y);
                return diff.Width * diff.Width + diff.Height * diff.Height;
            });
            var rect = new RectangleF(pt.X - labelSize.Width / 2, pt.Y - labelSize.Height / 2, labelSize.Width,
                labelSize.Height);
            var totalOverlap = 0.0;
            foreach (var cell in GetRectangleCells(rect))
            {
                var intersect = RectangleF.Intersect(rect, cell._bounds);
                totalOverlap += 1.0 * intersect.Height * intersect.Width / (_cellSize * _cellSize) * cell._density;
            }

            // penalize this point if there is more points between it and its label
            // we find the cells between the two points by traversing the vector intersection
            // with the grid
            var lv = new VectorF(targetPoint, pt); //line vector
            var deltaX = Math.Abs(_cellSize / lv.X);
            var deltaY = Math.Abs(_cellSize / lv.Y);
            var rxList = new List<float>();
            if (lv.X != 0)
            {
                if (lv.X > 0)
                    rxList.Add((float)Math.Ceiling(lv.Start.X / deltaX) - lv.Start.X / deltaX);
                else
                    rxList.Add(lv.Start.X / deltaX - (float)Math.Floor(lv.Start.X / deltaX));
                while ((rxList.Last() + deltaX) < 1)
                    rxList.Add(rxList.Last() + deltaX);
            }

            var ryList = new List<float>();
            if (lv.Y != 0)
            {
                if (lv.Y > 0)
                    ryList.Add((float)Math.Ceiling(lv.Start.Y / deltaY) - lv.Start.Y / deltaY);
                else
                    ryList.Add(lv.Start.Y / deltaY - (float)Math.Floor(lv.Start.Y / deltaY));
                while ((ryList.Last() + deltaY) < 1)
                    ryList.Add(ryList.Last() + deltaY);
            }

            var rList = rxList.Select(r => new KeyValuePair<float, string>(r, "x")).ToList();
            rList.AddRange(ryList.Select(r => new KeyValuePair<float, string>(r, "y")));
            var pathDensity = CellFromPoint(pathCellCoord)._density;
            foreach (var kv in rList.OrderBy(kv => kv.Key))
            {
                if ("x".Equals(kv.Value))
                    pathCellCoord.X += lv.X > 0 ? 1 : -1;
                if ("y".Equals(kv.Value))
                    pathCellCoord.Y += lv.Y > 0 ? 1 : -1;
                if (IndexesWithinGrid(
                        pathCellCoord)) // it can occasionally get out of boundaries, but we can safely ignore it.
                    pathDensity += CellFromPoint(pathCellCoord)._density;
            }

            // calculate the crossover penalty. For each previously labeled point we find if there is an intersection 
            // between vector V from this point q to the suggested label position and the vector U from the previously
            // labeled point p to its label position. We do this by solving the equation p + rR = q + sV where r and s
            // are parameters. If r and s both are <= 1, then the vectors intersect.
            // For each crossover we penalize the goal function by some large number because we really do not want crossovers to happen.
            var penalty = 0.0;
            var thisVector = new VectorF(targetPoint, pt);
            foreach (var point in _labeledPoints)
            {
                if (point.Value.LabelVector.Start.Equals(targetPoint))
                    break;
                if (thisVector.DoIntersect(point.Value.LabelVector))
                    penalty += 2000;
            }

            return (float)((0.025 * dist + totalOverlap) + penalty + 0.2 * pathDensity);
        }

        private IEnumerable<GridCell> GetRectangleCells(RectangleF rect)
        {
            rect.Offset(new PointF(-_chartOffset.X, -_chartOffset.Y));
            for (int i = (int)Math.Max(Math.Floor(rect.Left / _cellSize), 0);
                 i <= Math.Min(rect.Right / _cellSize, _densityGridSize.Width - 1);
                 i++)
            {
                for (int j = (int)Math.Max(Math.Floor(rect.Y / _cellSize), 0);
                     j <= Math.Min(rect.Bottom / _cellSize, _densityGridSize.Height - 1);
                     j++)
                {
                    yield return _densityGrid[j][i];
                }
            }
        }

        /// <summary>
        /// Density grid accessor
        /// </summary>
        /// <param name="pt"></param>
        private GridCell CellFromPoint(Point pt)
        {
            return _densityGrid[pt.Y][pt.X];
        }

        private Point CellIndexesFromXY(PointF pt)
        {
            return new Point((int)((pt.X - _chartOffset.X) / _cellSize), (int)((pt.Y - _chartOffset.Y) / _cellSize));
        }

        private bool IndexesWithinGrid(Point pt)
        {
            return pt.X >= 0 && pt.X < _densityGridSize.Width && pt.Y >= 0 && pt.Y < _densityGridSize.Height;
        }

        private int GetRandom(float range)
        {
            return (int)((_randGenerator.NextDouble() - 0.5) * (_randGenerator.NextDouble() - 0.5) * range * 1.5);
        }

        private static Rectangle ToRectangle(RectangleF rect)
        {
            return new Rectangle((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
        }

        /**
         * Algorighm overview:
         * Divide the graph into a grid with cell size equals the label height (the smallest dimension).
         * Each cell is assigned the average density of the occupied pixels and a vector of the density gradient.
         * For each label to place the cells in it's vicinity are searched for the least density.
         * Then the found cell is used to do a random search around it in general direction of the density gradient
         * using the target function. The target function takes into account area of overlap, and location (direction and distance)
         * of the label relative to it's data point.
         *  The algorighm works in screen coordinates (pixels). There is no need to use user coordinates here.
         *  Returns true if the label has been successfully placed, false otherwise.
         */
        public bool PlaceLabel(LabeledPoint labPoint, Graphics g)
        {
            var labelRect = _graph.GetRectScreen(labPoint.Label, g);
            var targetPoint = _graph.TransformCoord(labPoint.Point.X, labPoint.Point.Y, CoordType.AxisXYScale);
            var labelLength = (int)Math.Ceiling(1.0 * labelRect.Width / _cellSize);

            var pointCell = new Point((int)((targetPoint.X - _chartOffset.X) / _cellSize),
                (int)((targetPoint.Y - _chartOffset.Y) / _cellSize));
            if (!new Rectangle(Point.Empty, _densityGridSize).Contains(pointCell))
                return false;
            var goal = float.MaxValue;
            var goalCell = Point.Empty;
            var gridRect = new Rectangle(Point.Empty, _densityGridSize);
            var points = new List<Point>();
            for (var count = 80; count > 0; count--)
            {
                var randomGridPoint = pointCell +
                                      new Size(GetRandom(_densityGridSize.Width), GetRandom(_densityGridSize.Height));

                //the label shouldn't overlap the data point and must be within the grid limits
                if (randomGridPoint.Y == pointCell.Y && randomGridPoint.X > pointCell.X - labelLength &&
                    randomGridPoint.X < pointCell.X
                    || !gridRect.Contains(randomGridPoint))
                    continue;
                if (points.Contains(randomGridPoint))
                {
                    count++; // avoid computing goal function for points already checked
                    continue;
                }

                points.Add(randomGridPoint);
                var goalEstimate = GoalFuncion(CellFromPoint(randomGridPoint)._location, targetPoint, labelRect.Size);
                if (goalEstimate < goal)
                {
                    goal = goalEstimate;
                    goalCell = randomGridPoint;
                }
            }

            var roughGoal = goal;
            // Search the cell neighborhood for a better position
            var goalPoint = _densityGrid[goalCell.Y][goalCell.X]._location;
            for (var count = 15; count > 0; count--)
            {
                var p = goalPoint + new Size(GetRandom(_cellSize * 2), GetRandom(_cellSize * 2));
                if (!_graph.Chart.Rect.Contains(p))
                    continue;
                var goalEstimate1 = GoalFuncion(p, targetPoint, labelRect.Size);
                if (goalEstimate1 < goal)
                {
                    goal = goalEstimate1;
                    goalPoint = p;
                }
            }

            var labelLocation = new PointF(goalPoint.X, goalPoint.Y + labelRect.Height / 2);
            _graph.ReverseTransform(new PointF(labelLocation.X, labelLocation.Y), out var x, out var y);

            labPoint.Label.Location.X = x;
            labPoint.Label.Location.Y = y;

            // update density grid to prevent overlaps
            var newScreenRectangle = _graph.GetRectScreen(labPoint.Label, g);
            var newLabelRectangle = ToRectangle(newScreenRectangle);

            foreach (var cell in GetRectangleCells(newLabelRectangle))
            {
                var cellOverlap = RectangleF.Intersect(newLabelRectangle, cell._bounds);
                var densityIncrement = cellOverlap.Height * cellOverlap.Width;
                cell._density += 2.0f * densityIncrement;
            }

            labPoint.LabelVector = new VectorF(targetPoint, goalPoint);
            _labeledPoints[labPoint.Label] = labPoint;
            return true;
        }

        /// <summary>
        /// Places the label at the specified coordinates and updates the density grid so that
        /// the future calls to PlaceLabel take avoid overlaps and crossovers with this label
        /// </summary>
        /// <param name="labPoint">Point to add. It is assumed that this LabeledPoint object already has
        /// Label and Point components </param>
        /// <param name="newLabelLocation">new label position</param>
        public void AddLabel(LabeledPoint labPoint, PointF newLabelLocation)
        {
            labPoint.Label.Location.X = newLabelLocation.X;
            labPoint.Label.Location.Y = newLabelLocation.Y;
            using (var g = Graphics.FromHwnd(IntPtr.Zero))
            {
                var newScreenRectangle = _graph.GetRectScreen(labPoint.Label, g);
                var newLabelRectangle = ToRectangle(newScreenRectangle);

                foreach (var cell in GetRectangleCells(newLabelRectangle))
                {
                    var cellOverlap = RectangleF.Intersect(newLabelRectangle, cell._bounds);
                    var densityIncrement = cellOverlap.Height * cellOverlap.Width;
                    cell._density += 2.0f * densityIncrement;
                }

                var targetPoint = _graph.TransformCoord(labPoint.Point.X, labPoint.Point.Y, CoordType.AxisXYScale);
                var goalPoint = _graph.TransformCoord(labPoint.Label.Location.X, labPoint.Label.Location.Y, CoordType.AxisXYScale);
                labPoint.LabelVector = new VectorF(targetPoint, goalPoint);
                _labeledPoints[labPoint.Label] = labPoint;
            }
        }

        public void DrawConnector(LabeledPoint labPoint, Graphics g)
        {
            var endSize = CalculateConnectorSize(labPoint, g, _graph);

            var line = new LineObj(labPoint.Label.FontSpec.FontColor, labPoint.Point.X, labPoint.Point.Y,
                labPoint.Point.X + endSize.Width, labPoint.Point.Y + endSize.Height){IsClippedToChartRect = true};
            labPoint.Connector = line;
            _graph.GraphObjList.Add(line);
        }

        public void UpdateConnector(LabeledPoint labPoint, Graphics g)
        {
            var endSize = CalculateConnectorSize(labPoint, g, _graph);
            var loc = labPoint.Connector.Location;
            var newLocation = new Location(loc.X, loc.Y, endSize.Width, endSize.Height, CoordType.AxisXYScale,
                AlignH.Left, AlignV.Top);
            labPoint.Connector.Location = newLocation;
        }

        // Used to recalculate label connector attachment point during drag
        public static SizeF CalculateConnectorSize(LabeledPoint labPoint, Graphics g, GraphPane graph)
        {
            var rect = graph.GetRectScreen(labPoint.Label, g);
            InflateRectangle(ref rect, -1);
            var targetPoint = graph.TransformCoord(labPoint.Point.X, labPoint.Point.Y, CoordType.AxisXYScale);

            var diag1 = new VectorF(rect.Location, new PointF(rect.X + rect.Width, rect.Y + rect.Height));
            var diag2 = new VectorF(new PointF(rect.X, rect.Y + rect.Height), new PointF(rect.Right, rect.Y));
            var center = new PointF(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2);

            var labelVector = new VectorF(new PointF(targetPoint.X, targetPoint.Y), center);

            PointF endPoint;
            // If this multiple >0 then the connector line approaches the label from top or bottom, otherwise from right or left.
            if (VectorF.VectorDeterminant(labelVector, diag1) * VectorF.VectorDeterminant(labelVector, diag2) > 0)
                endPoint = new PointF(center.X - rect.Height * labelVector.X / (2 * Math.Abs(labelVector.Y)),
                    rect.Top + (labelVector.Y < 0 ? rect.Height : 0));
            else
                endPoint = new PointF(rect.Left + (labelVector.X > 0 ? 0 : rect.Width),
                    center.Y - rect.Width * labelVector.Y / (2 * Math.Abs(labelVector.X)));

            graph.ReverseTransform(new PointF(endPoint.X, endPoint.Y), out var x, out var y);
            return new SizeF((float)(x - labPoint.Point.X), (float)(y - labPoint.Point.Y));
        }

        public LabeledPoint FindById(object id)
        {
            var res = _labeledPoints.ToList().FindAll(lpt => lpt.Value.UniqueID.Equals(id)).Select(pair => pair.Value).ToList();
            if (res.Any())
                return res.First();
            else
                return null;
        }

        public bool IsPointVisible(PointPair point)
        {
            var chartRect = new RectangleF((float)_graph.XAxis.Scale.Min, (float)_graph.YAxis.Scale.Min,
                (float)(_graph.XAxis.Scale.Max - _graph.XAxis.Scale.Min),
                (float)(_graph.YAxis.Scale.Max - _graph.YAxis.Scale.Min));
            return chartRect.Contains(new PointF((float)point.X, (float)point.Y));
        }

        public static void InflateRectangle(ref RectangleF rect, float size)
        {
            var size2 = size * 2;
            rect.Location += new SizeF(-size, size);
            rect.Width += size2;
            rect.Height += size2;
        }
    }

    public class LabeledPoint
    {
        private LineObj _connector;
        private TextObj _label;

        public LabeledPoint(bool isSelected, object uniqueId)
        {
            IsSelected = isSelected;
            UniqueID = uniqueId;
        }

        public PointPair Point { get; set; }

        public TextObj Label
        {
            get => _label;
            set
            {
                _label = value;
                LabelPosition = _label.Location.TopLeft;
                _label.FontSpec.BoxExpansion = 2;
            }
        }

        public LineObj Connector
        {
            get => _connector;
            set
            {
                _connector = value;
                ConnectorLoc = new Location(_connector.Location);
            }
        }

        public VectorF LabelVector { get; set; }
        public CurveItem Curve { get; set; }

        public Location ConnectorLoc { get; private set; }
        public PointF LabelPosition { get; private set; }
        public bool IsSelected { get; private set; }
        public object UniqueID { get; private set; }

        // This method is used to memorize starting positions during drags
        public void UpdatePositions()
        {
            LabelPosition = _label.Location.TopLeft;
            ConnectorLoc = new Location(_connector.Location);
        }

        public void UpdateLabelLocation(double x, double y, GraphPane graph)
        {
            Label.Location.X = x;
            Label.Location.Y = y;
            using (var g = Graphics.FromHwnd(IntPtr.Zero))
            {
                var newConnectorEnd = LabelLayout.CalculateConnectorSize(this, g, graph);
                Connector.Location.Width = newConnectorEnd.Width;
                Connector.Location.Height = newConnectorEnd.Height;
            }
        }

        // Connector class to pass layout information to the document persistence code
        [JsonObject]
        public class PointLayout
        {
            public string Identity { get; set; }
            public PointF PointLocation { get; set; }
            public PointF LabelLocation { get; set; }

            public PointLayout()
            {
            }

            public PointLayout(LabeledPoint labPoint)
            {
                Identity = labPoint.UniqueID.ToString();
                PointLocation = new PointF((float)labPoint.Point.X, (float)labPoint.Point.Y);
                LabelLocation = new PointF((float)labPoint.Label.Location.X, (float)labPoint.Label.Location.Y);
            }
        }
    }

    public class VectorF
    {
        public PointF Start;
        public PointF End;
        private SizeF _vectorDiff;

        public VectorF(PointF start, PointF end)
        {
            Start = start;
            End = end;
            _vectorDiff = new SizeF(end.X - start.X, end.Y - start.Y);
        }

        public float X => _vectorDiff.Width;
        public float Y => _vectorDiff.Height;

        // calculates a determinant of 2x2 matrix with v1 and v2 as colums
        public static float VectorDeterminant(VectorF v1, VectorF v2) { return v1.X * v2.Y - v1.Y * v2.X; }

        public bool DoIntersect(VectorF other)
        {
            var det = VectorDeterminant(this, other);
            if (det == 0)
                return false;   // the vectors are parallel, no intersections
            var pointOffset = new VectorF(this.Start, other.Start);
            var r = VectorDeterminant(pointOffset, other) / det;
            var s = -VectorDeterminant(this, pointOffset) / det;
            return ((r >= 0 && r <= 1) && (s >= 0 && s <= 1));
        }

        public override string ToString()
        {
            return string.Format("Start: {0}, End: {1}, Vect: {2}, {3}", Start, End, X, Y);
        }
    }
}