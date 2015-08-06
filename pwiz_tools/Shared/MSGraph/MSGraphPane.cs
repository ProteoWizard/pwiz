//
// $Id: MSGraphPane.cs 1599 2009-12-04 01:35:39Z brendanx $
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
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using ZedGraph;

namespace pwiz.MSGraph
{
    public class MSGraphPane : GraphPane
    {
        private OverlapDetector _overlapDetector;
        private readonly LabelBoundsCache _labelBoundsCache;

        public MSGraphPane(LabelBoundsCache labelBoundsCache = null)
        {
            Title.IsVisible = false;
            Chart.Border.IsVisible = false;
            Y2Axis.IsVisible = false;
            X2Axis.IsVisible = false;
            XAxis.MajorTic.IsOpposite = false;
            YAxis.MajorTic.IsOpposite = false;
            XAxis.MinorTic.IsOpposite = false;
            YAxis.MinorTic.IsOpposite = false;
            IsFontsScaled = false;
            YAxis.Scale.MaxGrace = 0.1;
            YAxis.MajorGrid.IsZeroLine = false; // Hide the y=0 line
            LockYAxisAtZero = true;

            _currentItemType = MSGraphItemType.unknown;
            _pointAnnotations = new GraphObjList();
            _manualLabels = new Dictionary<TextObj, RectangleF>();
            _labelBoundsCache = labelBoundsCache ?? new LabelBoundsCache();
        }

        public MSGraphPane(MSGraphPane other)
            : base(other)
        {
            _currentItemType = other._currentItemType;
            _pointAnnotations = new GraphObjList();
            _manualLabels = new Dictionary<TextObj, RectangleF>();
            _labelBoundsCache = other._labelBoundsCache;
        }

        public bool AllowCurveOverlap { get; set; }
        public bool AllowLabelOverlap { get; set; }
        public bool LockYAxisAtZero { get; set; }

        protected MSGraphItemType _currentItemType;
        public MSGraphItemType CurrentItemType
        {
            get { return _currentItemType; }
            set { _currentItemType = value; }
        }

        public virtual void SetScale( Graphics g )
        {
            int bins = (int) CalcChartRect( g ).Width;
            if( bins < 1 )
                return;

            foreach( CurveItem curve in CurveList )
                if( curve.Points is MSPointList )
                {
                    if( XAxis.Scale.MinAuto && XAxis.Scale.MaxAuto )
                        ( curve.Points as MSPointList ).SetScale( bins );
                    else
                        ( curve.Points as MSPointList ).SetScale( XAxis.Scale, bins );
                }

            AxisChange();
        }

        /// <summary>
        /// Find the closest curve/point to the cursor.
        /// </summary>
        /// <param name="curveList">List of curves to check.</param>
        /// <param name="maxDistance">Maximum distance from curve allowed.</param>
        /// <param name="pt">Cursor coordinates.</param>
        /// <param name="closestCurve">Returns the closest curve (or null if none is close enough).</param>
        /// <param name="closestPoint">Returns the closest point on the curve.</param>
        public void FindClosestCurve(IEnumerable<CurveItem> curveList, PointF pt, int maxDistance,
            out CurveItem closestCurve, out PointF closestPoint)
        {
            // Determine boundaries of point search in graph coordinates.
            double xLo, xHi, y;
            ReverseTransform(new PointF(pt.X - maxDistance, 0), out xLo, out y);
            ReverseTransform(new PointF(pt.X + maxDistance, 0), out xHi, out y);

            closestCurve = null;
            closestPoint = new PointF();
            double closestDistanceSquared = maxDistance * maxDistance;

            // Iterate through each curve, finding the closest one within the distance limit (closestDistanceSquared).
            foreach (var curve in curveList)
                FindClosestPoint(curve, xLo, xHi, pt, ref closestCurve, ref closestPoint, ref closestDistanceSquared);
        }

        /// <summary>
        /// Find the closest point to the cursor of the given curve.
        /// </summary>
        /// <param name="curve">Curve to check.</param>
        /// <param name="xLo">Lowest x value to search (in axis coordinates).</param>
        /// <param name="xHi">Highest x value to search (in axis coordinates).</param>
        /// <param name="pt">Cursor coordinates.</param>
        /// <param name="closestCurve">Records which curve is the closest during multiple calls.</param>
        /// <param name="closestPoint">Records the closest point over multiple calls.</param>
        /// <param name="closestDistanceSquared">Records the closest squared distance over multiple calls.</param>
        private void FindClosestPoint(CurveItem curve, double xLo, double xHi, PointF pt,
            ref CurveItem closestCurve, ref PointF closestPoint, ref double closestDistanceSquared)
        {
            if (curve.NPts == 0)
                return;
            var points = curve.Points as MSPointList;
            if (points == null)
                return;
            int minIndex = Math.Max(0, points.LowerBound(xLo) - 1);
            int maxIndex = Math.Min(curve.NPts, points.LowerBound(xHi) + 2);
            for (int i = minIndex; i < maxIndex - 1; i++)
            {
                // Transform line segment to UI coordinates.
                var pt0 = GeneralTransform(points[i].X, points[i].Y, CoordType.AxisXYScale);
                var pt1 = GeneralTransform(points[i + 1].X, points[i + 1].Y, CoordType.AxisXYScale);

                // Choose axis of greatest change for projection,
                // and make it the x coordinate.
                bool swapped;
                PointF pts;
                if (Math.Abs(pt0.Y - pt1.Y) > Math.Abs(pt0.X - pt1.X))
                {
                    swapped = true;
                    pts = new PointF(pt.Y, pt.X);
                    pt0 = new PointF(pt0.Y, pt0.X);
                    pt1 = new PointF(pt1.Y, pt1.X);
                    // Make sure first coordinate of pt0 is less than pt1
                    if (pt0.X >= pt1.X)
                    {
                        var ptTmp = pt0;
                        pt0 = pt1;
                        pt1 = ptTmp;
                    }
                }
                else
                {
                    swapped = false;
                    pts = pt;
                }

                // If within the extent of the line segment, project along lesser axis onto the line segment.
                // Otherwise, choose the closest endpoint.
                PointF projectedPoint =
                    (pts.X < pt0.X) ? pt0 :
                    (pts.X > pt1.X) ? pt1 :
                    new PointF(pts.X, pt0.Y + (pt1.Y - pt0.Y) * (pts.X - pt0.X) / (pt1.X - pt0.X));
                double distanceSquared = GetDistanceSquared(pts, projectedPoint);
                if (closestDistanceSquared > distanceSquared)
                {
                    closestDistanceSquared = distanceSquared;
                    closestCurve = curve;
                    if (swapped)
                    {
                        closestPoint.X = projectedPoint.Y;
                        closestPoint.Y = projectedPoint.X;
                    }
                    else
                        closestPoint = projectedPoint;
                }
            }
        }

        private static double GetDistanceSquared(PointF p0, PointF p1)
        {
            double xDiff = p0.X - p1.X;
            double yDiff = p0.Y - p1.Y;
            return (xDiff * xDiff + yDiff * yDiff);
        }

        public override void Draw(Graphics g)
        {
            drawLabels( g );
            base.Draw( g );
        }

        protected readonly GraphObjList _pointAnnotations;
        protected readonly Dictionary<TextObj, RectangleF> _manualLabels;

        /// <summary>
        /// Get all annotation label text, for testing purposes
        /// </summary>
        public IEnumerable<string> GetAnnotationLabelStrings()
        {
            foreach (CurveItem item in CurveList)
            {
                var info = item.Tag as IMSGraphItemExtended;
                if (info == null || string.IsNullOrEmpty(info.ToString()))
                    continue;

                var points = item.Points as MSPointList;
                if (points == null)
                    continue;

                var listAnnotations = new GraphObjList();
                info.AddPreCurveAnnotations(this, null, points, listAnnotations);
                foreach (var annotation in listAnnotations)
                {
                    var textObj = annotation as TextObj;
                    if (textObj != null)
                        yield return textObj.Text;
                }

                foreach (var pt in points.FullList)
                {
                    var annotation = info.AnnotatePoint(pt);
                    if (annotation != null)
                        yield return annotation.Label;
                }
            }
        }

        private void drawLabels( Graphics g )
        {
            foreach( GraphObj pa in _pointAnnotations )
                GraphObjList.Remove( pa );
            _pointAnnotations.Clear();
            _manualLabels.Clear();

            Axis xAxis = XAxis;
            Axis yAxis = YAxis;

            yAxis.Scale.MinAuto = false;
            if (LockYAxisAtZero)
                yAxis.Scale.Min = 0;

            // ensure that the chart rectangle is the right size
            AxisChange(g);
            // then setup axes scales to enable the Transform method
            xAxis.Scale.SetupScaleData(this, xAxis);
            yAxis.Scale.SetupScaleData( this, yAxis );

            if( Chart.Rect.Width < 1 || Chart.Rect.Height < 1 )
                return;

            _overlapDetector = AllowLabelOverlap ? null : new OverlapDetector();
            Region chartRegion = new Region( Chart.Rect );
            Region clipRegion = new Region();
            clipRegion.MakeEmpty();
            var previousClip = g.Clip.Clone();
            g.SetClip( Rect, CombineMode.Replace );
            g.SetClip( chartRegion, CombineMode.Exclude );

            /*Bitmap clipBmp = new Bitmap(Convert.ToInt32(Chart.Rect.Width), Convert.ToInt32(Chart.Rect.Height));
            Graphics clipG = Graphics.FromImage(clipBmp);
            clipG.Clear(Color.White);
            clipG.FillRegion(new SolidBrush(Color.Black), g.Clip);
            clipBmp.Save("C:\\clip.bmp");*/


            // some dummy labels for very fast clipping
            string baseLabel = "0"; // Not L10N
            foreach( CurveItem item in CurveList )
            {
                IMSGraphItemInfo info = item.Tag as IMSGraphItemInfo;
                if( info != null )
                {
                    PointAnnotation annotation = info.AnnotatePoint( new PointPair( 0, 0 ) );
                    if( annotation != null &&
                        annotation.Label != null &&
                        annotation.Label.Length > baseLabel.Length )
                        baseLabel = annotation.Label;
                }
            }

            TextObj baseTextObj = new TextObj( baseLabel, 0, 0 );
            baseTextObj.FontSpec.Border.IsVisible = false;
            baseTextObj.FontSpec.Fill.IsVisible = false;
            PointF[] pts = baseTextObj.FontSpec.GetBox( g, baseLabel, 0, 0,
                                AlignH.Center, AlignV.Bottom, 1.0f, new SizeF() );
            float baseLabelWidth = pts[1].X - pts[0].X;
            float baseLabelHeight = pts[2].Y - pts[0].Y;
            baseLabelWidth = (float) xAxis.Scale.ReverseTransform( xAxis.Scale.Transform( 0 ) + baseLabelWidth );
            baseLabelHeight = (float) yAxis.Scale.ReverseTransform( yAxis.Scale.Transform( 0 ) - baseLabelHeight );
            float labelLengthToWidthRatio = baseLabelWidth / baseLabel.Length;

            float xAxisPixel = yAxis.Scale.Transform( 0 );

            double xMin = xAxis.Scale.Min;
            double xMax = xAxis.Scale.Max;
            double yMin = yAxis.Scale.Min;
            double yMax = yAxis.Scale.Max;

            // add manual annotations with TextObj priority over curve annotations
            foreach (CurveItem item in CurveList)
            {
                var info = item.Tag as IMSGraphItemExtended;
                MSPointList points = item.Points as MSPointList;
                if (info == null || points == null)
                    continue;

                info.AddPreCurveAnnotations(this, g, points, _pointAnnotations);
                AddAnnotations(g);
            }

            // add automatic labels for MSGraphItems
            foreach( CurveItem item in CurveList )
            {
                IMSGraphItemInfo info = item.Tag as IMSGraphItemInfo;
                MSPointList points = item.Points as MSPointList;
                if( info == null || points == null )
                    continue;

                if( info.ToString().Length == 0 )
                    continue;

                PointPairList fullList = points.FullList;
                List<int> maxIndexList = points.ScaledMaxIndexList;
                for( int i = 0; i < maxIndexList.Count; ++i )
                {
                    if( maxIndexList[i] < 0 )
                        continue;
                    PointPair pt = fullList[maxIndexList[i]];

                    if( pt.X < xMin || pt.Y > yMax || pt.Y < yMin )
                        continue;
                    if( pt.X > xMax )
                        break;

                    float yPixel = yAxis.Scale.Transform( pt.Y );

                    // labelled points must be at least 3 pixels off the X axis
                    if( xAxisPixel - yPixel < 3 )
                        continue;

                    PointAnnotation annotation = info.AnnotatePoint( pt );
                    if( annotation == null )
                        continue;

                    if( annotation.ExtraAnnotation != null )
                    {
                        GraphObjList.Add( annotation.ExtraAnnotation );
                        _pointAnnotations.Add( annotation.ExtraAnnotation );
                    }

                    if( string.IsNullOrEmpty(annotation.Label) )
                        continue;

                    float pointLabelWidth = labelLengthToWidthRatio * annotation.Label.Split('\n').Max(o => o.Length);

                    double labelY = yAxis.Scale.ReverseTransform(yPixel - 5);

                    if (!AllowCurveOverlap)
                    {
                        // do fast check for overlap against all MSGraphItems
                        bool overlap = false;
                        foreach (CurveItem item2 in CurveList)
                        {
                            MSPointList points2 = item2.Points as MSPointList;
                            if (points2 != null)
                            {
                                int nearestMaxIndex = points2.GetNearestMaxIndexToBin(i);
                                if (nearestMaxIndex < 0)
                                    continue;
                                RectangleF r = new RectangleF((float)pt.X - pointLabelWidth / 2,
                                                               (float)labelY - baseLabelHeight,
                                                               pointLabelWidth,
                                                               baseLabelHeight);
                                overlap = detectLabelCurveOverlap(this, points2.FullList, nearestMaxIndex, item2 is StickItem, r);
                                if (overlap)
                                    break;
                            }
                        }

                        if (overlap)
                            continue;
                    }

                    TextObj text = new TextObj( annotation.Label, pt.X, labelY,
                                                CoordType.AxisXYScale, AlignH.Center, AlignV.Bottom )
                    {
                        ZOrder = ZOrder.A_InFront,
                        FontSpec = annotation.FontSpec,
                        IsClippedToChartRect = true
                    };

                    var textRect = _labelBoundsCache.GetLabelBounds(text, this, g);
                    bool overlap2 = _overlapDetector != null && _overlapDetector.Overlaps(textRect);
                    _manualLabels[text] = textRect;
                    if (!overlap2)
                    {
                        _pointAnnotations.Add(text);
                        AddAnnotations(g);
                    }
                }
            }

            // add manual annotations
            foreach( CurveItem item in CurveList )
            {
                IMSGraphItemInfo info = item.Tag as IMSGraphItemInfo;
                MSPointList points = item.Points as MSPointList;
                if( info == null || points == null )
                    continue;

                info.AddAnnotations( this, g,  points, _pointAnnotations );
                AddAnnotations(g);
            }

            autoScaleForManualLabels(g);
            g.Clip = previousClip;
        }

        private bool isXChartFractionObject(GraphObj obj)
        {
            return obj.Location.CoordinateFrame == CoordType.XChartFractionYPaneFraction ||
                   obj.Location.CoordinateFrame == CoordType.ChartFraction ||
                   obj.Location.CoordinateFrame == CoordType.XChartFractionYScale ||
                   obj.Location.CoordinateFrame == CoordType.XChartFractionY2Scale;
        }
        
        private bool IsYChartFractionObject(GraphObj obj)
        {
            return obj.Location.CoordinateFrame == CoordType.XPaneFractionYChartFraction ||
                   obj.Location.CoordinateFrame == CoordType.ChartFraction ||
                   obj.Location.CoordinateFrame == CoordType.XScaleYChartFraction;
        }

        private void AddAnnotations(Graphics g)
        {
            foreach (GraphObj obj in _pointAnnotations)
            {
                TextObj text = obj as TextObj;
                if (text != null)
                {
                    if (isXChartFractionObject(text) && (text.Location.X < XAxis.Scale.Min || text.Location.X > XAxis.Scale.Max))
                        continue;

                    var textRect = _labelBoundsCache.GetLabelBounds(text, this, g);
                    if (_overlapDetector == null || !_overlapDetector.Overlaps(textRect))
                        _manualLabels[text] = textRect;
                }
                else if (!GraphObjList.Contains(obj)) // always add non-text annotations
                    GraphObjList.Add(obj);
            }
        }

        /// <summary>
        /// We know which labels are overlapping the data points, but we need to make sure the labels that will be
        /// displayed can fit in the scale using the Min/MaxGrace properties and adjusting the label fractions if appropriate:
        /// TextObjs with CoordType.AxisXYScale coordinates will move proportionally with Min/MaxGrace, but ChartFraction
        /// coordinates will not.
        /// </summary>
        protected void autoScaleForManualLabels(Graphics g)
        {
            Axis yAxis = YAxis;
            bool maxAuto = yAxis.Scale.MaxAuto;
            try
            {
                if (maxAuto)
                {
                    AxisChange(g);
                }

                double yMaxRequired = 0;
                foreach (var kvp in _manualLabels)
                {
                    TextObj text = kvp.Key;
                    if (text.Location.X < XAxis.Scale.Min || text.Location.X > XAxis.Scale.Max)
                        continue;

                    if (!YAxis.Scale.IsLog && !IsYChartFractionObject(text))
                    {
                        double axisHeight = YAxis.Scale.Max - YAxis.Scale.Min;

                        PointF[] pts = text.FontSpec.GetBox(g, text.Text, 0, 0, text.Location.AlignH, text.Location.AlignV, 1.0f,
                            new SizeF());
                        float pixelShift = 0;
                        var rectPeak = _labelBoundsCache.GetLabelBounds(text, this, g);
                        foreach (var id in _manualLabels.Keys.Where(IsYChartFractionObject))
                        {
                            var rectID = _labelBoundsCache.GetLabelBounds(id, this, g);

                            if (rectID.Left  < rectPeak.Right && rectID.Right > rectID.Left)
                            {
                                pixelShift = Math.Max(rectID.Height, pixelShift);
                            }
                        }
                        double labelHeight = 
                            Math.Abs(yAxis.Scale.ReverseTransform(pts[0].Y - pixelShift) - yAxis.Scale.ReverseTransform(pts[2].Y));
                        if (labelHeight < axisHeight / 2)
                        {
                            // Ensure that the YAxis will have enough space to show the label.
                            // Only do this if the labelHeight is going to take up less than half the space on the graph, because
                            // otherwise the graph will be shrunk too much to have any useful information.

                            // When calculating the scaling required, take into account that the height of the label
                            // itself will not shrink when we shrink the YAxis.
                            var labelYMaxRequired = (text.Location.Y - labelHeight*YAxis.Scale.Min/axisHeight)/
                                                    (1 - labelHeight/axisHeight) + pixelShift;
                            yMaxRequired = Math.Max(yMaxRequired, labelYMaxRequired);
                        }
                    }

                    if (!GraphObjList.Any(
                            o =>
                                (o is TextObj) && (o as TextObj).Location == text.Location &&
                                (o as TextObj).Text == text.Text))
                    {
                        if (_pointAnnotations.Contains(text))
                            GraphObjList.Add(text);
                    }
                }

                if (maxAuto && yMaxRequired > 0)
                {
                    yAxis.Scale.Max = Math.Max(yAxis.Scale.Max, yMaxRequired);
                }
            }
            finally
            {
                // Reset the value of MaxAuto since it may have been changed to false when Scale.Max was changed.
                yAxis.Scale.MaxAuto = maxAuto;
            }
        }

        protected bool detectLabelCurveOverlap( GraphPane pane, IPointList points, int pointIndex, bool pointsAreSticks, RectangleF labelBounds )
        {
            double rL = labelBounds.Left;// pane.XAxis.Scale.ReverseTransform( labelBounds.Left );
            //double rT = pane.YAxis.Scale.ReverseTransform( labelBounds.Top );
            double rR = labelBounds.Right;//pane.XAxis.Scale.ReverseTransform( labelBounds.Right );
            double rB = labelBounds.Bottom;//pane.YAxis.Scale.ReverseTransform( labelBounds.Bottom );

            // labels cannot overlap the Y axis
            if( rL < pane.XAxis.Scale.Min )
                return true;

            if( pointsAreSticks )
            {
                for( int k = 0; k < points.Count; ++k )
                {
                    PointPair p = points[k];

                    if( p.X > rR )
                        break;

                    if( p.X < rL )
                        continue;

                    if( p.Y > rB )
                        return true;
                }
            } else
            {
                // find all points in the X range of the rectangle
                // also add the points immediately to the left and right
                // of these points, find the local maximum
                // an overlap happens if maximum > rB
                for( int k = pointIndex; k > 0; --k )
                {
                    PointPair p = points[k];

                    if( k + 1 < points.Count && points[k + 1].X < rL )
                        break;
                    if( p.Y > rB )
                        return true;
                }

                // accessing points.Count in the loop condition showed up in a profiler
                for( int k = pointIndex + 1, len = points.Count; k < len; ++k )
                {
                    PointPair p = points[k];

                    if( points[k - 1].X > rR )
                        break;

                    if( p.Y > rB )
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Detect overlaps between rectangles.
        /// </summary>
        private class OverlapDetector
        {
            private readonly List<RectangleF> _rectangles = new List<RectangleF>();

            public bool Overlaps(RectangleF rectangle)
            {
                // At this point, we just do an O(n^2) intersection test between each
                // pair of rectangles.  Obviously, we could do something much smarter
                // if this became a performance bottleneck, but for now the number of
                // rectangles is fairly small.
                for (int i = _rectangles.Count-1; i >= 0; i--)
                {
                    if (rectangle.IntersectsWith(_rectangles[i]))
                        return true;
                }

                // Add this non-overlapping rectangle to the list of rectangles, where
                // it will suppress subsequent rectangles that interesect it.
                _rectangles.Add(rectangle);
                return false;
            }
        }
    }
}
