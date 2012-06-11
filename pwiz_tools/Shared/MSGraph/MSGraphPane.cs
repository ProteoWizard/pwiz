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
        public MSGraphPane()
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

            _currentItemType = MSGraphItemType.unknown;
            _pointAnnotations = new GraphObjList();
        }

        public bool AllowCurveOverlap { get; set; }

        protected MSGraphItemType _currentItemType;
        public MSGraphItemType CurrentItemType
        {
            get { return _currentItemType; }
            set { _currentItemType = value; }
        }

        public void SetScale()
        {
            SetScale( null );
        }

        public void SetScale( Graphics g )
        {
            int bins;
            if( g == null )
                bins = (int) Chart.Rect.Width;
            else
                bins = (int) CalcChartRect( g ).Width;
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

        public override void Draw( Graphics g )
        {
            drawLabels( g );
            base.Draw( g );
        }

        protected GraphObjList _pointAnnotations;
        private void drawLabels( Graphics g )
        {
            foreach( GraphObj pa in _pointAnnotations )
                GraphObjList.Remove( pa );
            _pointAnnotations.Clear();

            Axis xAxis = XAxis;
            Axis yAxis = YAxis;

            yAxis.Scale.MinAuto = false;
            yAxis.Scale.Min = 0;

            // setup axes scales to enable the Transform method
            xAxis.Scale.SetupScaleData( this, xAxis );
            yAxis.Scale.SetupScaleData( this, yAxis );

            if( Chart.Rect.Width < 1 || Chart.Rect.Height < 1 )
                return;

            Region textBoundsRegion;
            Region chartRegion = new Region( Chart.Rect );
            Region clipRegion = new Region();
            clipRegion.MakeEmpty();
            g.SetClip( Rect, CombineMode.Replace );
            g.SetClip( chartRegion, CombineMode.Exclude );

            /*Bitmap clipBmp = new Bitmap(Convert.ToInt32(Chart.Rect.Width), Convert.ToInt32(Chart.Rect.Height));
            Graphics clipG = Graphics.FromImage(clipBmp);
            clipG.Clear(Color.White);
            clipG.FillRegion(new SolidBrush(Color.Black), g.Clip);
            clipBmp.Save("C:\\clip.bmp");*/


            // some dummy labels for very fast clipping
            string baseLabel = "0";
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
            float baseLabelWidth = (float) Math.Round( pts[1].X - pts[0].X );
            float baseLabelHeight = (float) Math.Round( pts[2].Y - pts[0].Y );
            baseLabelWidth = (float) xAxis.Scale.ReverseTransform( xAxis.Scale.Transform( 0 ) + baseLabelWidth );
            baseLabelHeight = (float) yAxis.Scale.ReverseTransform( yAxis.Scale.Transform( 0 ) - baseLabelHeight );
            float labelLengthToWidthRatio = baseLabelWidth / baseLabel.Length;

            float xAxisPixel = yAxis.Scale.Transform( 0 );

            double xMin = xAxis.Scale.Min;
            double xMax = xAxis.Scale.Max;
            double yMin = yAxis.Scale.Min;
            double yMax = yAxis.Scale.Max;

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

                    float pointLabelWidth = labelLengthToWidthRatio * annotation.Label.Length;

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
                                           FontSpec = annotation.FontSpec
                                       };
                    //text.IsClippedToChartRect = true;

                    if( !detectLabelOverlap( this, g, text, out textBoundsRegion, item.Points, maxIndexList[i], item is StickItem ) )
                    {
                        GraphObjList.Add( text );
                        _pointAnnotations.Add( text );


                        clipRegion.Union( textBoundsRegion );
                        g.SetClip( clipRegion, CombineMode.Replace );
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
                foreach (GraphObj obj in _pointAnnotations)
                {
                    if (!GraphObjList.Contains(obj))
                    {
                        TextObj text = obj as TextObj;
                        if (text != null)
                        {
                            if (detectLabelOverlap(this, g, text, out textBoundsRegion, item.Points, -1, item is StickItem))
                                continue;

                            clipRegion.Union(textBoundsRegion);
                            g.SetClip(clipRegion, CombineMode.Replace);
                        }

                        GraphObjList.Add(obj);
                    }
                }
                /*GraphObjList objsToRemove = new GraphObjList();
                foreach( GraphObj obj in GraphObjList )
                    if( !pointAnnotations_.Contains( obj ) )
                        objsToRemove.Add( obj );
                foreach( GraphObj obj in objsToRemove )
                    GraphObjList.Remove( obj );*/
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

        protected bool detectLabelOverlap( GraphPane pane, Graphics g, TextObj text, out Region textBoundsRegion, IPointList points, int pointIndex, bool pointsAreSticks )
        {
            string shape, coords;
            text.GetCoords( pane, g, 1.0f, out shape, out coords );
            if( shape != "poly" ) throw new InvalidOperationException( "shape must be 'poly'" );
            string[] textBoundsPointStrings = coords.Split( ",".ToCharArray() );
            if( textBoundsPointStrings.Length != 9 ) throw new InvalidOperationException( "coords length must be 8" );
            Point[] textBoundsPoints = new[]
                        {
                            new Point( Convert.ToInt32(textBoundsPointStrings[0]), Convert.ToInt32(textBoundsPointStrings[1])),
                            new Point( Convert.ToInt32(textBoundsPointStrings[2]), Convert.ToInt32(textBoundsPointStrings[3])),
                            new Point( Convert.ToInt32(textBoundsPointStrings[4]), Convert.ToInt32(textBoundsPointStrings[5])),
                            new Point( Convert.ToInt32(textBoundsPointStrings[6]), Convert.ToInt32(textBoundsPointStrings[7]))
                        };
            byte[] textBoundsPointTypes = new[]
                        {
                            (byte) PathPointType.Start,
                            (byte) PathPointType.Line,
                            (byte) PathPointType.Line,
                            (byte) PathPointType.Line
                        };
            GraphicsPath textBoundsPath = new GraphicsPath( textBoundsPoints, textBoundsPointTypes );
            textBoundsPath.CloseFigure();
            textBoundsRegion = new Region( textBoundsPath );
            textBoundsRegion.Intersect( pane.Chart.Rect );
            RectangleF[] textBoundsRectangles = textBoundsRegion.GetRegionScans( g.Transform );

            return textBoundsRectangles.Any(t => IsVisibleToClip(g, t));
        }

        /// <summary>
        /// Fixes an issue with <see cref="Region.IsVisible(RectangleF)"/> where it
        /// sometimes returns true with complex regions when it should return false.
        /// <para>
        /// A reference on this issue can be found at:
        /// https://connect.microsoft.com/VisualStudio/feedback/ViewFeedback.aspx?FeedbackID=160358
        /// </para>
        /// </summary>
        /// <param name="g">Source of <see cref="Graphics.Clip"/> region to check against</param>
        /// <param name="rect">The rectagle to check for intersection with the clip region</param>
        /// <returns>Tree if the rectangle intersects with the region</returns>
        private static bool IsVisibleToClip( Graphics g, RectangleF rect )
        {
            foreach( RectangleF rectClip in g.Clip.GetRegionScans( g.Transform ) )
            {
                if( rectClip.IntersectsWith( rect ) )
                    return true;
            }
            return false;
        }
    }
}
