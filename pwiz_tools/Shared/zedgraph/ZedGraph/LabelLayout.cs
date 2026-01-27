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
using System.Globalization;
using System.IO;
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
        private const float CROSSOVER_PENALTY = 5000f;
        private const float LABEL_OVERLAP_PENALTY = 5000f;
        private const float TARGET_OVERLAP_PENALTY = 300f;
        private const float CONNECTOR_LABEL_OVERLAP_PENALTY = 1500f;
        private const float DISTANCE_SCALE = 10000f;
        private LayoutSignature? _lastSignature;

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
            public Point _indices;
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
                        _indices = new Point(j, i)
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

        private bool GetPointMarkerRectangle(PointF pt, out RectangleF rect)
        {
            rect = RectangleF.Empty;
            foreach (var line in _graph.CurveList.OfType<LineItem>().Where(c => c.Symbol.Type != SymbolType.None))
            {
                for (var i = 0; i < line.Points.Count; i++)
                {
                    var screenPt = _graph.TransformCoord(line.Points[i].X, line.Points[i].Y, CoordType.AxisXYScale);
                    if (Math.Abs(screenPt.X - pt.X) < 1 && Math.Abs(screenPt.Y - pt.Y) < 1 )

                    {
                        if (!line.GetCoords(this._graph, i, out var coords))
                        {
                            continue;
                        }
                        var sides = Array.ConvertAll(coords.Split(','), int.Parse);
                        rect = new Rectangle(sides[0], sides[1], sides[2] - sides[0], sides[3] - sides[1]);
                        return true;
                    }                }
            }
            return false;
        }

        /// <summary>
        /// Calculates goal function for a labeled point and a suggested label position. This does not include point-to-point
        /// pairwise interactions, only the cost of this label placement itself.
        /// All coordinates are in screen pixels.
        /// </summary>
        /// <param name="pt">Center of the label box, in pixels</param>
        /// <param name="targetPoint">data point being labeled.</param>
        /// <param name="labelSize"> in pixels </param>
        /// <param name="targetMarkerRect"> enclosing rectangle of the target point marker. We want to avoid
        /// overlaps with it as much as possible.</param>
        /// <returns>goal function value.</returns>
        private float EvaluateLabelBaseCost(PointF pt, PointF targetPoint, SizeF labelSize, RectangleF targetMarkerRect)
        {
            var pathCellCoord = CellIndexesFromXY(targetPoint);
            if (!IndexesWithinGrid(pathCellCoord))
                return 10000; //return really large value if the target point is outside of the density grid
            if (!IndexesWithinGrid(CellIndexesFromXY(pt)))
                return 10000;
            // Distance to the label is measured from the center or right/left ends, whichever is closer.
            var distPoints = new[]
                { pt - new SizeF(labelSize.Width / 2, 0), pt, pt + new SizeF(labelSize.Width / 2, 0) };
            var graphWidth = _graph.Chart.Rect.Width;
            var graphHeight = _graph.Chart.Rect.Height;
            var dist = distPoints.Min(p =>
            {
                var diff = new SizeF(p.X - targetPoint.X, p.Y - targetPoint.Y);
                // calculate the distance relative to the chart size to make sure it works 
                // for both large and small graphs
                return diff.Width * diff.Width / (graphWidth * graphWidth) + diff.Height * diff.Height / (graphHeight * graphHeight);
            });
            var rect = new RectangleF(pt.X - labelSize.Width / 2, pt.Y, labelSize.Width,
                labelSize.Height);
            var totalOverlap = 0.0;
            foreach (var cell in GetRectangleCells(rect))
            {
                var intersect = RectangleF.Intersect(rect, cell._bounds);
                totalOverlap += 1.0 * intersect.Height * intersect.Width / (_cellSize * _cellSize) * cell._density;
            }

            // overlap with the target point is bad, we should penalize it heavily
            if (RectangleF.Intersect(rect, targetMarkerRect) != RectangleF.Empty)
                totalOverlap += TARGET_OVERLAP_PENALTY;
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

            var visibleArea = RectArea(RectangleF.Intersect(rect, _graph.Chart.Rect));
            var clipPenalty = 0.0f;
            if (visibleArea > 0)
                clipPenalty = (1 - visibleArea / RectArea(rect)) * 500.0f;

            return (float)((DISTANCE_SCALE * dist + totalOverlap) + 0.2 * pathDensity) + clipPenalty;
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

        /// <summary>
        /// Uniform distribution searches the available area more efficiently.
        /// </summary>
        private int GetRandom(float range)
        {
            return (int)((_randGenerator.NextDouble() - 0.5) * range * 0.75);
        }

        private static Rectangle ToRectangle(RectangleF rect)
        {
            return new Rectangle((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
        }

        private float RectArea(RectangleF rect) { return rect.Width * rect.Height; }
        private static RectangleF RectFromTopCenter(PointF topCenter, SizeF labelSize)
        {
            return new RectangleF(topCenter.X - labelSize.Width / 2, topCenter.Y, labelSize.Width, labelSize.Height);
        }

        private RectangleF AllowedRect(SizeF labelSize)
        {
            var chartRect = _graph.Chart.Rect;
            var left = chartRect.Left + labelSize.Width / 2;
            var top = chartRect.Top;
            var width = Math.Max(0, chartRect.Width - labelSize.Width);
            var height = Math.Max(0, chartRect.Height - labelSize.Height);
            return new RectangleF(left, top, width, height);
        }

        private PointF ClampToAllowed(PointF candidate, SizeF labelSize)
        {
            var allowed = AllowedRect(labelSize);
            var x = Math.Min(allowed.Right, Math.Max(allowed.Left, candidate.X));
            var y = Math.Min(allowed.Bottom, Math.Max(allowed.Top, candidate.Y));
            return new PointF(x, y);
        }

        private PointF ScreenToLabelLocation(PointF topCenter, SizeF labelSize)
        {
            _graph.ReverseTransform(new PointF(topCenter.X, topCenter.Y + labelSize.Height / 2), out var x, out var y);
            return new PointF((float)x, (float)y);
        }

        private PointF GetTopCenter(LabeledPoint labPoint, Graphics g)
        {
            var rect = _graph.GetRectScreen(labPoint.Label, g);
            return new PointF(rect.Left + rect.Width / 2, rect.Top);
        }

        private Dictionary<LabeledPoint, PointF> CopyPlacement(Dictionary<LabeledPoint, PointF> source)
        {
            return source.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        /// <summary>
        /// Calculates part of the cost for a single label placement due to its own parameters, without considering
        /// interactions with other labels.
        /// </summary>
        /// <param name="point">Point to estimate</param>
        /// <param name="sizes">List of the label sizes</param>
        /// <param name="targets">Point locations</param>
        /// <param name="targetMarkers">Point marker rectangles</param>
        /// <param name="placements">Label top-center positions</param>
        /// <returns></returns>
        private float BaseCost(LabeledPoint point, IDictionary<LabeledPoint, SizeF> sizes,
            IDictionary<LabeledPoint, PointF> targets, IDictionary<LabeledPoint, RectangleF> targetMarkers,
            IDictionary<LabeledPoint, PointF> placements)
        {
            return EvaluateLabelBaseCost(placements[point], targets[point], sizes[point], targetMarkers[point]);
        }

        /// <summary>
        /// Calculates pairwise cost between two labeled points given their label placements.
        /// </summary>
        private float PairCost(LabeledPoint p1, LabeledPoint p2, IDictionary<LabeledPoint, SizeF> sizes,
            IDictionary<LabeledPoint, PointF> targets, IDictionary<LabeledPoint, PointF> placements)
        {
            var size1 = sizes[p1];
            var size2 = sizes[p2];
            var rect1 = new RectangleF(placements[p1].X - size1.Width / 2, placements[p1].Y, size1.Width, size1.Height);
            var rect2 = new RectangleF(placements[p2].X - size2.Width / 2, placements[p2].Y, size2.Width, size2.Height);

            var start1 = targets[p1];
            var end1 = placements[p1];
            var start2 = targets[p2];
            var end2 = placements[p2];

            // Expanded bounding boxes that include both label rectangle and connector endpoints
            var minAx = Math.Min(Math.Min(rect1.Left, rect1.Right), Math.Min(start1.X, end1.X));
            var maxAx = Math.Max(Math.Max(rect1.Left, rect1.Right), Math.Max(start1.X, end1.X));
            var minAy = Math.Min(Math.Min(rect1.Top, rect1.Bottom), Math.Min(start1.Y, end1.Y));
            var maxAy = Math.Max(Math.Max(rect1.Top, rect1.Bottom), Math.Max(start1.Y, end1.Y));

            var minBx = Math.Min(Math.Min(rect2.Left, rect2.Right), Math.Min(start2.X, end2.X));
            var maxBx = Math.Max(Math.Max(rect2.Left, rect2.Right), Math.Max(start2.X, end2.X));
            var minBy = Math.Min(Math.Min(rect2.Top, rect2.Bottom), Math.Min(start2.Y, end2.Y));
            var maxBy = Math.Max(Math.Max(rect2.Top, rect2.Bottom), Math.Max(start2.Y, end2.Y));

            float cost = 0;
            if (maxAx >= minBx && maxBx >= minAx && maxAy >= minBy && maxBy >= minAy)
            {
                // Calculate penalty for label overlap
                var intersect = RectangleF.Intersect(rect1, rect2);
                if (!intersect.IsEmpty)
                {
                    var intersectArea = intersect.Width * intersect.Height;
                    var rect1Area = rect1.Width * rect1.Height;
                    var rect2Area = rect2.Width * rect2.Height;
                    cost += LABEL_OVERLAP_PENALTY * intersectArea / Math.Max(1, Math.Min(rect1Area, rect2Area));
                }
                // calculate the crossover penalty. For each previously labeled point we find if there is an intersection 
                // between vector V from this point q to the suggested label position and the vector U from the previously
                // labeled point p to its label position. We do this by solving the equation p + rR = q + sV where r and s
                // are parameters. If r and s both are <= 1, then the vectors intersect.
                // For each crossover we penalize the goal function by some large number because we really do not want crossovers to happen.
                // penalize the goal if the label is completely or partially outside of the chart area
                var v1 = new VectorF(start1, end1);
                var v2 = new VectorF(start2, end2);
                if (v1.DoIntersect(v2))
                    cost += CROSSOVER_PENALTY;
            }

            // Penalize when a connector crosses the other label's rectangle
            if (SegmentIntersectsRect(start1, end1, rect2))
                cost += CONNECTOR_LABEL_OVERLAP_PENALTY;
            if (SegmentIntersectsRect(start2, end2, rect1))
                cost += CONNECTOR_LABEL_OVERLAP_PENALTY;

            return cost;
        }

        /// <summary>
        /// Places all labels using a simulated annealing search. Saved layout entries are treated as fixed.
        /// </summary>
        public void PlaceLabelsSimulatedAnnealing(List<LabeledPoint> points, Graphics g, List<LabeledPoint.PointLayout> savedLayout = null)
        {
            if (!points.Any())
                return;
            // This logic avoids recomputing the layout if the points have not changed since last time
            // ZedGraph code has a tendency to call AxisChange multiple times for each zoom.
            var signature = ComputeSignature(points);
            if (_lastSignature.HasValue && _lastSignature.Value.Equals(signature))
                return;

            _labeledPoints.Clear();

            var labelSizes = points.ToDictionary(p => p, p => _graph.GetRectScreen(p.Label, g).Size);
            if (labelSizes.Values.Any(sz => sz.Height <= 0 || sz.Width <= 0))
                return;

            var targetPoints = points.ToDictionary(p => p, p => _graph.TransformCoord(p.Point.X, p.Point.Y, CoordType.AxisXYScale));
            var targetMarkers = new Dictionary<LabeledPoint, RectangleF>();
            foreach (var point in points)
            {
                GetPointMarkerRectangle(targetPoints[point], out var rect);
                targetMarkers[point] = rect;
            }

            var savedLookup = savedLayout ?? new List<LabeledPoint.PointLayout>();
            var placements = new Dictionary<LabeledPoint, PointF>();    // label top-center positions
            var movablePoints = new List<LabeledPoint>();
            var avgLabelLength = labelSizes.Values.Any() ? labelSizes.Values.Average(sz => sz.Width) : _cellSize;
            // Place saved points first, build the list of points that have to be optimized
            foreach (var point in points)
            {
                var savedPoint = savedLookup.FirstOrDefault(p => point.Point.Equals(p.PointLocation));
                var size = labelSizes[point];
                if (size.Height > _graph.Chart.Rect.Height || size.Width / 2 > _graph.Chart.Rect.Width)
                    continue;

                if (savedPoint != null)
                {
                    point.Label.Location.X = savedPoint.LabelLocation.X;
                    point.Label.Location.Y = savedPoint.LabelLocation.Y;
                    var topCenter = GetTopCenter(point, g);
                    placements[point] = ClampToAllowed(topCenter, size);
                }
                else
                {
                    var target = targetPoints[point];
                    // Start near the point with a random offset on the order of the average label length
                    var offsetMag = avgLabelLength * .6f;
                    var initial = new PointF(target.X + GetRandom(offsetMag), target.Y - size.Height - 2 + GetRandom(offsetMag));
                    placements[point] = ClampToAllowed(initial, size);
                    movablePoints.Add(point);
                }
            }

            if (placements.Count == 0)
                return;
            var pointList = placements.Keys.ToList();
            var baseCosts = pointList.ToDictionary(p => p,
                p => BaseCost(p, labelSizes, targetPoints, targetMarkers, placements));
            // Precompute pairwise costs
            var pairCosts = pointList.ToDictionary(p => p, p => new Dictionary<LabeledPoint, float>());
            float pairSum = 0;
            for (var i = 0; i < pointList.Count; i++)
            {
                for (var j = i + 1; j < pointList.Count; j++)
                {
                    var cost = PairCost(pointList[i], pointList[j], labelSizes, targetPoints, placements);
                    pairCosts[pointList[i]][pointList[j]] = cost;
                    pairCosts[pointList[j]][pointList[i]] = cost;
                    pairSum += cost;
                }
            }

            float currentCost = baseCosts.Values.Sum() + pairSum;

            var startTemp = 7.0f;
            var minTemp = 0.1f;
            // Linear cooling
            var maxIterations = Math.Max(700, points.Count * 50);
            var cooling = (startTemp - minTemp) / maxIterations;
            var acceptanceScale = 1.0f * points.Count;
            var bestPlacement = CopyPlacement(placements);
            var bestCost = currentCost;
#if DEBUG
            // Log the annealing process for debugging
            var logPath = Path.Combine(Environment.CurrentDirectory, $"LabelLayoutAnneal.csv");
            if (File.Exists(logPath))
                File.Delete(logPath);
            using (var log = new StreamWriter(logPath))
            {
                log.WriteLine("iteration,point_count,temperature,cost, delta, jump");
#endif
                for (var iter = 0; iter < maxIterations; iter++)
                {
                    var temp = startTemp - cooling * iter;
                    if (temp < minTemp || !movablePoints.Any())
                        break;

                    var point = movablePoints[_randGenerator.Next(movablePoints.Count)];
                    var step = _cellSize * (0.5f + temp);
                    var proposed = placements[point] + new SizeF(GetRandom(step), GetRandom(step));
                    proposed = ClampToAllowed(proposed, labelSizes[point]);
                    var currentPos = placements[point];

                    // Remove old contributions for this point
                    var removedBase = baseCosts[point];
                    var removedPair = pairCosts[point].Values.Sum();
                    var removedCost = removedBase + removedPair;
                    placements[point] = proposed;

                    var newBase = BaseCost(point, labelSizes, targetPoints, targetMarkers, placements);
                    var oldPairs = new Dictionary<LabeledPoint, float>(pairCosts[point]);
                    float newPairSum = 0;
                    foreach (var other in pointList)
                    {
                        if (ReferenceEquals(other, point))
                            continue;
                        var cost = PairCost(point, other, labelSizes, targetPoints, placements);
                        pairCosts[point][other] = cost;
                        pairCosts[other][point] = cost;
                        newPairSum += cost;
                    }
                    var addedCost = newBase + newPairSum;

                    var newCost = currentCost - removedCost + addedCost;
                    var delta = newCost - currentCost;

                    var accept = delta < 0 || Math.Exp(-delta / Math.Max(temp, 0.0001f)/acceptanceScale) > _randGenerator.NextDouble();
                    if (accept)
                    {
                        baseCosts[point] = newBase;
                        currentCost = newCost;
                        if (newCost < bestCost)
                        {
                            bestCost = newCost;
                            bestPlacement = CopyPlacement(placements);
                        }
                    }
                    else
                    {
                        placements[point] = currentPos;
                        foreach (var kvp in oldPairs)
                        {
                            pairCosts[point][kvp.Key] = kvp.Value;
                            pairCosts[kvp.Key][point] = kvp.Value;
                        }
                    }
#if DEBUG
                    var jump = accept && delta > 0 ? 1 : 0;
                    log.WriteLine($"{iter},{points.Count.ToString()},{temp.ToString()},{bestCost.ToString()}, {delta.ToString()}, {jump}");
                }
#endif
            }
            placements = bestPlacement;
            _labeledPoints.Clear();
            foreach (var kv in placements)
            {
                var point = kv.Key;
                var labelSize = labelSizes[point];
                var labelLocation = ScreenToLabelLocation(kv.Value, labelSize);
                AddLabel(point, labelLocation);
            }

            _lastSignature = signature;
        }

        /// <summary>
        /// Calculates the total goal function for the current layout (all labels).
        /// </summary>
        public float CalculateTotalCost(Graphics g)
        {
            if (!_labeledPoints.Any())
                return 0;

            var points = _labeledPoints.Values.ToList();
            var labelSizes = points.ToDictionary(p => p, p => _graph.GetRectScreen(p.Label, g).Size);
            var targetPoints = points.ToDictionary(p => p, p => _graph.TransformCoord(p.Point.X, p.Point.Y, CoordType.AxisXYScale));
            var targetMarkers = new Dictionary<LabeledPoint, RectangleF>();
            foreach (var p in points)
            {
                GetPointMarkerRectangle(targetPoints[p], out var rect);
                targetMarkers[p] = rect;
            }

            var placements = points.ToDictionary(p => p, p => GetTopCenter(p, g));

            double total = 0;
            foreach (var p in points)
                total += BaseCost(p, labelSizes, targetPoints, targetMarkers, placements);

            for (int i = 0; i < points.Count; i++)
            {
                for (int j = i + 1; j < points.Count; j++)
                    total += PairCost(points[i], points[j], labelSizes, targetPoints, placements);
            }

            return (float)total;
        }

        /// <summary>
        /// Places the label at the specified coordinates and updates the density grid so that
        /// future placement calls avoid overlaps and crossovers with this label
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

        private static bool SegmentIntersectsRect(PointF a, PointF b, RectangleF rect)
        {
            // Quick reject on bounding boxes
            var minX = Math.Min(a.X, b.X);
            var maxX = Math.Max(a.X, b.X);
            var minY = Math.Min(a.Y, b.Y);
            var maxY = Math.Max(a.Y, b.Y);
            if (maxX < rect.Left || minX > rect.Right || maxY < rect.Top || minY > rect.Bottom)
                return false;

            // If either endpoint inside rect, it's an overlap
            if (rect.Contains(a) || rect.Contains(b))
                return true;

            var tl = rect.Location;
            var tr = new PointF(rect.Right, rect.Top);
            var bl = new PointF(rect.Left, rect.Bottom);
            var br = new PointF(rect.Right, rect.Bottom);

            var seg = new VectorF(a, b);
            return seg.DoIntersect(new VectorF(tl, tr)) ||
                   seg.DoIntersect(new VectorF(tr, br)) ||
                   seg.DoIntersect(new VectorF(br, bl)) ||
                   seg.DoIntersect(new VectorF(bl, tl));
        }

        private struct LayoutSignature
        {
            public int PointCount;
            public double Checksum;
            public RectangleF ChartRect;
            public double XMin;
            public double XMax;
            public double YMin;
            public double YMax;

            public override bool Equals(object obj)
            {
                if (!(obj is LayoutSignature other))
                    return false;

                return PointCount == other.PointCount &&
                       Math.Abs(Checksum - other.Checksum) < 0.001 &&
                       ChartRect.Equals(other.ChartRect) &&
                       Math.Abs(XMin - other.XMin) < 0.0001 &&
                       Math.Abs(XMax - other.XMax) < 0.0001 &&
                       Math.Abs(YMin - other.YMin) < 0.0001 &&
                       Math.Abs(YMax - other.YMax) < 0.0001;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = PointCount;
                    hash = (hash * 397) ^ Checksum.GetHashCode();
                    hash = (hash * 397) ^ ChartRect.GetHashCode();
                    hash = (hash * 397) ^ XMin.GetHashCode();
                    hash = (hash * 397) ^ XMax.GetHashCode();
                    hash = (hash * 397) ^ YMin.GetHashCode();
                    hash = (hash * 397) ^ YMax.GetHashCode();
                    return hash;
                }
            }
        }

        private LayoutSignature ComputeSignature(IEnumerable<LabeledPoint> points)
        {
            double checksum = 0;
            int count = 0;
            foreach (var p in points)
            {
                checksum += p.Point.X * 31.0 + p.Point.Y;
                count++;
            }

            var chartRect = _graph.Chart.Rect;
            return new LayoutSignature
            {
                PointCount = count,
                Checksum = checksum,
                ChartRect = chartRect,
                XMin = _graph.XAxis.Scale.Min,
                XMax = _graph.XAxis.Scale.Max,
                YMin = _graph.YAxis.Scale.Min,
                YMax = _graph.YAxis.Scale.Max
            };
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
