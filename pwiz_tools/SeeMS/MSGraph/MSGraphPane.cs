using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Drawing.Drawing2D;
using ZedGraph;

namespace MSGraph
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

            Line.Default.IsOptimizedDraw = true;

            currentItemType_ = MSGraphItemType.Unknown;
            pointAnnotations_ = new GraphObjList();
        }

        protected MSGraphItemType currentItemType_;
        public MSGraphItemType CurrentItemType
        {
            get { return currentItemType_; }
            set { currentItemType_ = value; }
        }

        public void SetScale()
        {
            SetScale( null );
        }

        public void SetScale( Graphics g )
        {
            int bins = 0;
            if( g == null )
                bins = (int) Chart.Rect.Width;
            else
                bins = (int) CalcChartRect( g ).Width;
            if( bins < 1 )
                return;

            foreach( CurveItem curve in CurveList )
                if( curve is MSGraphItem )
                {
                    if( !IsZoomed ) //pane.XAxis.Scale.MinAuto && pane.XAxis.Scale.MaxAuto )
                        ( curve as MSGraphItem ).Points.SetScale( bins );
                    else
                        ( curve as MSGraphItem ).Points.SetScale( XAxis.Scale, bins );
                }

            AxisChange();
        }

        public override void Draw( Graphics g )
        {
            drawLabels( g );
            base.Draw( g );
        }

        protected GraphObjList pointAnnotations_;
        private void drawLabels( Graphics g )
        {
            foreach( GraphObj pa in pointAnnotations_ )
                this.GraphObjList.Remove( pa );

            Axis xAxis = this.XAxis;
            Axis yAxis = this.YAxis;

            yAxis.Scale.MinAuto = false;
            yAxis.Scale.Min = 0;

            // setup axes scales to enable the Transform method
            xAxis.Scale.SetupScaleData( this, xAxis );
            yAxis.Scale.SetupScaleData( this, yAxis );

            if( this.Chart.Rect.Width < 1 || this.Chart.Rect.Height < 1 )
                return;

            Region textBoundsRegion;
            Region chartRegion = new Region( this.Chart.Rect );
            Region clipRegion = new Region();
            clipRegion.MakeEmpty();
            g.SetClip( this.Rect, CombineMode.Replace );
            g.SetClip( chartRegion, CombineMode.Exclude );
            /*Bitmap clipBmp = new Bitmap( Convert.ToInt32( pane.Rect.Width ), Convert.ToInt32( pane.Rect.Height ) );
            Graphics clipG = Graphics.FromImage( clipBmp );
            clipG.Clear( Color.White );
            clipG.FillRegion( new SolidBrush( Color.Black ), g.Clip );
            clipBmp.Save( "C:\\clip.bmp" );*/


            // some dummy labels for very fast clipping
            string baseLabel = "";
            foreach( MSGraphItem item in this.CurveList )
            {
                PointAnnotation annotation = item.GraphItemInfo.AnnotatePoint( new PointPair( 0, 0 ) );
                if( annotation != null &&
                    annotation.Label != null &&
                    annotation.Label.Length > baseLabel.Length )
                    baseLabel = annotation.Label;
            }

            TextObj baseTextObj = new TextObj( baseLabel, 0, 0 );
            baseTextObj.FontSpec.Border.IsVisible = false;
            baseTextObj.FontSpec.Fill.IsVisible = false;
            PointF[] pts = baseTextObj.FontSpec.GetBox( g, baseLabel, 0, 0,
                                AlignH.Center, AlignV.Bottom, 1.0f, new SizeF() );
            float baseLabelWidth = (float) Math.Round( (double) ( pts[1].X - pts[0].X ) );
            float baseLabelHeight = (float) Math.Round( (double) ( pts[2].Y - pts[0].Y ) );
            baseLabelWidth = (float) xAxis.Scale.ReverseTransform( xAxis.Scale.Transform( 0 ) + baseLabelWidth );
            baseLabelHeight = (float) yAxis.Scale.ReverseTransform( yAxis.Scale.Transform( 0 ) - baseLabelHeight );
            float labelLengthToWidthRatio = baseLabelWidth / (float) baseLabel.Length;

            float xAxisPixel = yAxis.Scale.Transform( 0 );

            // add automatic labels for MSGraphItems
            foreach( MSGraphItem item in CurveList )
            {
                if( item.GraphItemInfo.ToString().Length == 0 )
                    continue;

                PointPairList fullList = item.Points.FullList;
                List<int> maxIndexList = item.Points.ScaledMaxIndexList;
                for( int i = 0; i < maxIndexList.Count; ++i )
                {
                    if( maxIndexList[i] < 0 )
                        continue;
                    PointPair pt = fullList[maxIndexList[i]];

                    if( pt.X < xAxis.Scale.Min || pt.Y > yAxis.Scale.Max || pt.Y < yAxis.Scale.Min )
                        continue;
                    if( pt.X > xAxis.Scale.Max )
                        break;

                    float yPixel = yAxis.Scale.Transform( pt.Y );

                    // labelled points must be at least 3 pixels off the X axis
                    if( xAxisPixel - yPixel < 3 )
                        continue;

                    PointAnnotation annotation = item.GraphItemInfo.AnnotatePoint( pt );
                    if( annotation == null )
                        continue;

                    if( annotation.ExtraAnnotation != null )
                    {
                        this.GraphObjList.Add( annotation.ExtraAnnotation );
                        pointAnnotations_.Add( annotation.ExtraAnnotation );
                    }

                    if( annotation.Label == null || annotation.Label == String.Empty )
                        continue;

                    float pointLabelWidth = labelLengthToWidthRatio * annotation.Label.Length;

                    // do fast check for overlap against all MSGraphItems
                    double labelY = yAxis.Scale.ReverseTransform( yPixel - 5 );
                    bool overlap = false;
                    foreach( MSGraphItem item2 in CurveList )
                    {
                        int nearestMaxIndex = item2.Points.GetNearestMaxIndexToBin( i );
                        if( nearestMaxIndex < 0 )
                            continue;
                        RectangleF r = new RectangleF( (float) pt.X - pointLabelWidth / 2,
                                                       (float) labelY - baseLabelHeight,
                                                       pointLabelWidth,
                                                       baseLabelHeight );
                        overlap = detectLabelCurveOverlap( this, item2.Points.FullList, nearestMaxIndex, item2.BaseItem is StickItem, r );
                        if( overlap )
                            break;
                    }

                    if( overlap )
                        continue;

                    TextObj text = new TextObj( annotation.Label, pt.X, labelY,
                                                CoordType.AxisXYScale, AlignH.Center, AlignV.Bottom );
                    text.ZOrder = ZOrder.A_InFront;
                    //text.IsClippedToChartRect = true;
                    text.FontSpec = annotation.FontSpec;

                    if( !detectLabelOverlap( this, g, text, out textBoundsRegion, item.Points, maxIndexList[i], item.BaseItem is StickItem ) )
                    {
                        this.GraphObjList.Add( text );
                        pointAnnotations_.Add( text );


                        clipRegion.Union( textBoundsRegion );
                        g.SetClip( clipRegion, CombineMode.Replace );
                    }
                }

                if( item.GraphItemInfo.NonPointAnnotations != null )
                {
                    GraphObjList.AddRange( item.GraphItemInfo.NonPointAnnotations );
                    pointAnnotations_.AddRange( item.GraphItemInfo.NonPointAnnotations );
                }
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
                for( int k = pointIndex - 1; k > 0; --k )
                {
                    PointPair p = points[k];

                    if( points[k + 1].X < rL )
                        break;

                    if( p.Y > rB )
                        return true;
                }

                for( int k = pointIndex + 1; k < points.Count; ++k )
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
            Point[] textBoundsPoints = new Point[]
						{
							new Point( Convert.ToInt32(textBoundsPointStrings[0]), Convert.ToInt32(textBoundsPointStrings[1])),
							new Point( Convert.ToInt32(textBoundsPointStrings[2]), Convert.ToInt32(textBoundsPointStrings[3])),
							new Point( Convert.ToInt32(textBoundsPointStrings[4]), Convert.ToInt32(textBoundsPointStrings[5])),
							new Point( Convert.ToInt32(textBoundsPointStrings[6]), Convert.ToInt32(textBoundsPointStrings[7]))
						};
            byte[] textBoundsPointTypes = new byte[]
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

            for( int j = 0; j < textBoundsRectangles.Length; ++j )
            {
                RectangleF r = textBoundsRectangles[j];

                if( g.Clip.IsVisible( r ) )
                    return true;
            }
            return false;
        }
    }
}
