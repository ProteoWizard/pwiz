using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using MSGraph;
using ZedGraph;

namespace seems
{
	public partial class GraphForm : DockableForm, IDataView
	{
        #region IDataView Members
        public IList<ManagedDataSource> Sources
        {
            get
            {
                List<ManagedDataSource> sources = new List<ManagedDataSource>();
                for( int i = 0; i < paneList.Count; ++i )
                {
                    Pane logicalPane = paneList[i];

                    foreach( GraphItem item in logicalPane )
                        sources.Add( item.Source );
                }
                return sources;
            }
        }

        public IList<GraphItem> DataItems
        {
            get
            {
                List<GraphItem> graphItems = new List<GraphItem>();
                for( int i = 0; i < paneList.Count; ++i )
                {
                    Pane logicalPane = paneList[i];

                    foreach( GraphItem item in logicalPane )
                        graphItems.Add( item );
                }
                return graphItems;
            }
        }
        #endregion

		public MSGraph.MSGraphControl ZedGraphControl { get { return msGraphControl; } }

        MSGraphPane focusedPane = null;
        CurveItem focusedItem = null;

        /// <summary>
        /// Occurs when the FocusedItem property changes;
        /// usually caused by a left click near a different MSGraphItem
        /// </summary>
        public event EventHandler ItemGotFocus;

        private void OnItemGotFocus( GraphForm graphForm, EventArgs eventArgs )
        {
            if( ItemGotFocus != null )
                ItemGotFocus( graphForm, eventArgs );
        }

        private void setFocusedItem( CurveItem item )
        {
            if( item != focusedItem )
            {
                focusedItem = item;
                OnItemGotFocus( this, EventArgs.Empty );
            }
        }

        /// <summary>
        /// Gets the MSGraphPane that was last focused on within the MSGraphControl
        /// </summary>
        public MSGraphPane FocusedPane { get { return focusedPane; } }

        /// <summary>
        /// If FocusedPane has a single item, it will return that;
        /// If the last left mouse click was less than ZedGraph.GraphPane.Default.NearestTol
        /// from a point, it will return the item containing that point;
        /// Otherwise returns the first item in the FocusedPane
        /// </summary>
        public CurveItem FocusedItem { get { return focusedItem; } }

        private PaneList paneList;
        public PaneList PaneList
        {
            get { return paneList; }
            set
            {
                paneList = value;
                Refresh();
            }
        }

        private ZedGraph.PaneLayout paneLayout;
        public ZedGraph.PaneLayout PaneListLayout
        {
            get { return paneLayout; }
            set
            {
                ZedGraph.PaneLayout oldLayout = paneLayout;
                paneLayout = value;
                if( oldLayout != paneLayout )
                    Refresh();
            }
        }

		public GraphForm()
		{
			InitializeComponent();

            paneList = new PaneList();
            paneLayout = PaneLayout.SingleColumn;

            msGraphControl.MasterPane.InnerPaneGap = 1;
            msGraphControl.MouseDownEvent += new ZedGraphControl.ZedMouseEventHandler( msGraphControl_MouseDownEvent );
            msGraphControl.MouseMoveEvent += new ZedGraphControl.ZedMouseEventHandler( msGraphControl_MouseMoveEvent );

            msGraphControl.ZoomButtons = MouseButtons.Left;
            msGraphControl.ZoomModifierKeys = Keys.None;
            msGraphControl.ZoomButtons2 = MouseButtons.None;

            msGraphControl.UnzoomButtons = new MSGraphControl.MouseButtonClicks( MouseButtons.Middle );
            msGraphControl.UnzoomModifierKeys = Keys.None;
            msGraphControl.UnzoomButtons2 = new MSGraphControl.MouseButtonClicks( MouseButtons.None );

            msGraphControl.UnzoomAllButtons = new MSGraphControl.MouseButtonClicks( MouseButtons.Left, 2 );
            msGraphControl.UnzoomAllButtons2 = new MSGraphControl.MouseButtonClicks( MouseButtons.None );

            msGraphControl.PanButtons = MouseButtons.Left;
            msGraphControl.PanModifierKeys = Keys.Control;
            msGraphControl.PanButtons2 = MouseButtons.None;

            msGraphControl.ContextMenuBuilder += new MSGraphControl.ContextMenuBuilderEventHandler( GraphForm_ContextMenuBuilder );
		}

        bool msGraphControl_MouseMoveEvent( ZedGraphControl sender, MouseEventArgs e )
        {
            MSGraphPane hoverPane = sender.MasterPane.FindPane( e.Location ) as MSGraphPane;
            CurveItem nearestCurve;
            int nearestIndex;

            //change the cursor if the mouse is sufficiently close to a point
            if( hoverPane.FindNearestPoint( e.Location, out nearestCurve, out nearestIndex ) )
            {
                msGraphControl.Cursor = Cursors.SizeAll;
            } else
            {
                msGraphControl.Cursor = Cursors.Default;
            }
            return false;
        }

        bool msGraphControl_MouseDownEvent( ZedGraphControl sender, MouseEventArgs e )
        {
            // keep track of MSGraphItem nearest the last left click
            Point pos = MousePosition;
            focusedPane = sender.MasterPane.FindPane( e.Location ) as MSGraphPane;
            CurveItem nearestCurve; int nearestIndex;
            focusedPane.FindNearestPoint( e.Location, out nearestCurve, out nearestIndex );
            if( nearestCurve == null )
                setFocusedItem( sender.MasterPane[0].CurveList[0] );
            else
                setFocusedItem( nearestCurve );
            return false;
        }

        void GraphForm_ContextMenuBuilder( ZedGraphControl sender,
                                           ContextMenuStrip menuStrip,
                                           Point mousePt,
                                           MSGraphControl.ContextMenuObjectState objState )
        {
            if( sender.MasterPane.PaneList.Count > 1 )
            {
                ToolStripMenuItem layoutMenu = new ToolStripMenuItem( "Stack Layout", null,
                        new ToolStripItem[]
                    {
                        new ToolStripMenuItem("Single Column", null, GraphForm_StackLayoutSingleColumn),
                        new ToolStripMenuItem("Single Row", null, GraphForm_StackLayoutSingleRow),
                        new ToolStripMenuItem("Grid", null, GraphForm_StackLayoutGrid)
                    }
                    );
                menuStrip.Items.Add( layoutMenu );

                ToolStripMenuItem syncMenuItem = new ToolStripMenuItem( "Synchronize Zoom/Pan", null, GraphForm_SyncZoomPan );
                syncMenuItem.Checked = msGraphControl.IsSynchronizeXAxes;
                menuStrip.Items.Add( syncMenuItem );
            }
        }

        void GraphForm_StackLayoutSingleColumn( object sender, EventArgs e )
        {
            PaneListLayout = PaneLayout.SingleColumn;
        }

        void GraphForm_StackLayoutSingleRow( object sender, EventArgs e )
        {
            PaneListLayout = PaneLayout.SingleRow;
        }

        void GraphForm_StackLayoutGrid( object sender, EventArgs e )
        {
            PaneListLayout = PaneLayout.ForceSquare;
        }

        void GraphForm_SyncZoomPan( object sender, EventArgs e )
        {
            msGraphControl.IsSynchronizeXAxes = !msGraphControl.IsSynchronizeXAxes;
        }

        public override void Refresh()
        {
            MasterPane mp = msGraphControl.MasterPane;
            
            if( mp.PaneList.Count != paneList.Count )
            {
                mp.PaneList.Clear();
                foreach( Pane logicalPane in paneList )
                {
                    MSGraphPane pane = new MSGraphPane();
                    pane.IsFontsScaled = false;
                    mp.Add( pane );
                }
                //mp.SetLayout( msGraphControl.CreateGraphics(), paneLayout );
            } else
            {
                for( int i=0; i < paneList.Count; ++i )
                {
                    MSGraphPane pane = mp.PaneList[i] as MSGraphPane;
                    pane.CurveList.Clear();
                    pane.GraphObjList.Clear();
                }
            }

            for( int i = 0; i < paneList.Count; ++i )
            {
                Pane logicalPane = paneList[i];
                MSGraphPane pane = mp.PaneList[i] as MSGraphPane;
                pane.IsFontsScaled = false;

                foreach( GraphItem item in logicalPane )
                {
                    msGraphControl.AddGraphItem( pane, item );
                }

                if( mp.PaneList.Count > 1 )
                {
                    //if( i < paneList.Count - 1 )
                    {
                        pane.XAxis.Title.IsVisible = false;
                        pane.XAxis.Scale.IsVisible = false;
                        pane.Margin.Bottom = 0;
                        pane.Margin.Top = 2;
                    }/* else
                    {
                        pane.XAxis.Title.IsVisible = true;
                        pane.XAxis.Scale.IsVisible = true;
                    }*/
                    pane.YAxis.Title.IsVisible = false;
                    pane.YAxis.Scale.IsVisible = false;
                    pane.YAxis.Scale.SetupScaleData( pane, pane.YAxis );
                } else
                {
                    pane.XAxis.IsVisible = true;
                    pane.XAxis.Title.IsVisible = true;
                    pane.XAxis.Scale.IsVisible = true;
                    pane.YAxis.Title.IsVisible = true;
                    pane.YAxis.Scale.IsVisible = true;
                }

                if( logicalPane.Count == 1 )
                {
                    pane.Legend.IsVisible = false;
                } else
                {
                    pane.Legend.IsVisible = true;
                    pane.Legend.Position = ZedGraph.LegendPos.TopCenter;

                    ZedGraph.ColorSymbolRotator rotator = new ColorSymbolRotator();
                    foreach( CurveItem item in pane.CurveList )
                    {
                        item.Color = rotator.NextColor;
                    }
                }

                if( paneList.Count > 0 && paneList[0].Count > 0 )
                    this.Text = this.TabText = paneList[0][0].Id;

                if( !pane.IsZoomed )
                    msGraphControl.RestoreScale( pane );
                else
                    pane.AxisChange();
            }

            mp.SetLayout( msGraphControl.CreateGraphics(), paneLayout );

            /*if( isOverlay )
            {
                pane.Legend.IsVisible = true;
                pane.Legend.Position = ZedGraph.LegendPos.TopCenter;
                for( int i = 0; i < pane.CurveList.Count; ++i )
                {
                    pane.CurveList[i].Color = overlayColors[i];
                    ( pane.CurveList[i] as ZedGraph.LineItem ).Line.Width = 2;
                }
            } else
            {
                pane.Legend.IsVisible = false;
                currentGraphItem = chromatogram;
            }*/

            //msGraphControl.RestoreScale( pane );
            //msGraphControl.ZoomOutAll( pane );

            /*bool isScaleAuto = !pane.IsZoomed;

            if( isScaleAuto )
                pointList.SetScale( bins, pointList[0].X, pointList[pointList.Count - 1].X );
            else
                pointList.SetScale( bins, pane.XAxis.Scale.Min, pane.XAxis.Scale.Max );*/

            // String.Format( "{0} - {1}", currentDataSource.Name, chromatogram.Id )

            if( mp.PaneList.Count > 0 &&
                ( focusedPane == null ||
                  !mp.PaneList.Contains( focusedPane ) ) )
                focusedPane = mp.PaneList[0] as MSGraphPane;

            if( mp.PaneList.Count > 0 &&
                mp.PaneList[0].CurveList.Count > 0 &&
                ( focusedItem == null ||
                  !focusedPane.CurveList.Contains( focusedItem ) ) )
                setFocusedItem( mp.PaneList[0].CurveList[0] );

            msGraphControl.Refresh();
        }


		private Color[] overlayColors = new Color[]
		{
			Color.Red, Color.Blue, Color.Green, Color.Purple, Color.Brown,
			Color.Magenta, Color.Cyan, Color.LightGreen, Color.Beige,
			Color.DarkRed, Color.DarkBlue, Color.DarkGreen, Color.DeepPink
		};
#if false
        #region old label code
        // create a map of point X coordinates to point indexes in descending order by the Y coordinate
		// for each point, create a text label of the X and/or Y value (user configurable)
		// check that the label will not overlap any other labels (how to check against data lines as well?)
		// if no overlap, add the label and mask the 
		private ZedGraph.GraphObjList dataLabels = new ZedGraph.GraphObjList();
        public void SetDataLabelsVisible( bool visible )
        {
            // set direct references
            foreach( MSGraphPane pane in msGraphControl.MasterPane.PaneList )
            {
                ZedGraph.Axis xAxis = pane.XAxis;
                ZedGraph.Axis yAxis = pane.YAxis;

                if( CurrentGraphItem == null )
                    return;

                //msGraphControl.GraphPane.GraphObjList.Clear();
                foreach( ZedGraph.GraphObj dataLabel in dataLabels )
                    msGraphControl.GraphPane.GraphObjList.Remove( dataLabel );

                if( visible )
                {
                    if( CurrentGraphItem.IsChromatogram )
                    {
                        if( !( ChromatogramAnnotationSettings.ShowPointTimes ||
                            ChromatogramAnnotationSettings.ShowPointIntensities ||
                            ChromatogramAnnotationSettings.ShowUnmatchedAnnotations ||
                            ChromatogramAnnotationSettings.ShowMatchedAnnotations ) )
                            return;
                    } else
                    {
                        if( !( ScanAnnotationSettings.ShowPointMZs ||
                            ScanAnnotationSettings.ShowPointIntensities ||
                            ScanAnnotationSettings.ShowUnmatchedAnnotations ||
                            ScanAnnotationSettings.ShowMatchedAnnotations ) )
                            return;
                    }

                    yAxis.Scale.MinAuto = false;
                    yAxis.Scale.Min = 0;

                    // setup axes scales to enable the Transform method
                    xAxis.Scale.SetupScaleData( pane, xAxis );
                    yAxis.Scale.SetupScaleData( pane, yAxis );

                    Graphics g = msGraphControl.CreateGraphics();
                    System.Drawing.Bitmap gmap;

                    if( pane.Chart.Rect.Width > 0 && pane.Chart.Rect.Height > 0 )
                    {
                        try
                        {
                            //pane.Draw( g );
                            pane.CurveList.Draw( g, pane, 1.0f );
                            gmap = new Bitmap( Convert.ToInt32( pane.Rect.Width ), Convert.ToInt32( pane.Rect.Height ) );
                            msGraphControl.DrawToBitmap( gmap, Rectangle.Round( pane.Rect ) );
                        } catch
                        {
                            return;
                        }
                    } else
                        return;

                    Region textBoundsRegion;
                    Region chartRegion = new Region( pane.Chart.Rect );
                    Region clipRegion = new Region();
                    clipRegion.MakeEmpty();
                    g.SetClip( msGraphControl.MasterPane.Rect, CombineMode.Replace );
                    g.SetClip( chartRegion, CombineMode.Exclude );
                    /*Bitmap clipBmp = new Bitmap( Convert.ToInt32( pane.Rect.Width ), Convert.ToInt32( pane.Rect.Height ) );
                    Graphics clipG = Graphics.FromImage( clipBmp );
                    clipG.Clear( Color.White );
                    clipG.FillRegion( new SolidBrush( Color.Black ), g.Clip );
                    clipBmp.Save( "C:\\clip.bmp" );*/

                    PointDataMap<SeemsPointAnnotation> matchedAnnotations = new PointDataMap<SeemsPointAnnotation>();

                    // add precursor label(s) for tandem mass spectra
                    if( CurrentGraphItem.IsMassSpectrum )
                    {
                        MassSpectrum scanItem = (MassSpectrum) CurrentGraphItem;
                        pwiz.CLI.msdata.PrecursorList precursorList = scanItem.Element.spectrumDescription.precursors;
                        for( int i = 0; i < precursorList.Count; ++i )
                        {
                            pwiz.CLI.msdata.Precursor precursor = precursorList[i];
                            pwiz.CLI.msdata.SelectedIonList selectedIons = precursor.selectedIons;
                            if( selectedIons.Count == 0 )
                                continue;
                            double precursorMz = (double) selectedIons[0].cvParam( pwiz.CLI.msdata.CVID.MS_m_z ).value;
                            pwiz.CLI.msdata.CVParam precursorChargeParam = selectedIons[0].cvParam( pwiz.CLI.msdata.CVID.MS_charge_state );
                            int precursorCharge = 0;
                            if( precursorChargeParam.cvid != pwiz.CLI.msdata.CVID.CVID_Unknown )
                                precursorCharge = (int) selectedIons[0].cvParam( pwiz.CLI.msdata.CVID.MS_charge_state ).value;

                            float stickLength = ( yAxis.MajorTic.Size * 5 ) / pane.Chart.Rect.Height;
                            ZedGraph.LineObj stickOverlay = new ZedGraph.LineObj( precursorMz, 1, precursorMz, 1 + stickLength );
                            stickOverlay.Location.CoordinateFrame = ZedGraph.CoordType.XScaleYChartFraction;
                            stickOverlay.Line.Width = 3;
                            stickOverlay.Line.Style = DashStyle.Dot;
                            stickOverlay.Line.Color = Color.Green;
                            pane.GraphObjList.Add( stickOverlay );
                            dataLabels.Add( stickOverlay );

                            // Create a text label from the X data value
                            string precursorLabel;
                            if( precursorCharge > 0 )
                                precursorLabel = String.Format( "{0}\n(+{1} precursor)", precursorMz.ToString( "f3" ), precursorCharge );
                            else
                                precursorLabel = String.Format( "{0}\n(precursor of unknown charge)", precursorMz.ToString( "f3" ) );
                            ZedGraph.TextObj text = new ZedGraph.TextObj( precursorLabel, precursorMz, 1 + stickLength,
                                ZedGraph.CoordType.XScaleYChartFraction, ZedGraph.AlignH.Center, ZedGraph.AlignV.Top );
                            text.ZOrder = ZedGraph.ZOrder.A_InFront;
                            text.FontSpec.FontColor = stickOverlay.Line.Color;
                            text.FontSpec.Border.IsVisible = false;
                            text.FontSpec.Fill.IsVisible = false;
                            //text.FontSpec.Fill = new Fill( Color.FromArgb( 100, Color.White ) );
                            text.FontSpec.Angle = 0;

                            if( !detectLabelOverlap( pane, g, gmap, text, out textBoundsRegion ) )
                            {
                                pane.GraphObjList.Add( text );
                                clipRegion.Union( textBoundsRegion );
                                //g.SetClip( chartRegion, CombineMode.Replace );
                                g.SetClip( clipRegion, CombineMode.Replace );
                                //clipG.Clear( Color.White );
                                //clipG.FillRegion( new SolidBrush( Color.Black ), g.Clip );
                                //clipBmp.Save( "C:\\clip.bmp" );
                                dataLabels.Add( text );
                                //text.Draw( g, pane, 1.0f );
                                //msGraphControl.DrawToBitmap( gmap, Rectangle.Round( pane.Rect ) );
                            }
                        }
                    }

                    // add automatic labels
                    foreach( ZedGraph.CurveItem curve in pane.CurveList )
                    {
                        if( !( curve.Points is PointList ) )
                            continue;

                        PointList pointList = (PointList) curve.Points;
                        for( int i = 0; i < pointList.MaxCount; i++ )
                        {
                            ZedGraph.PointPair pt = pointList.GetPointAtIndex( pointList.ScaledMaxIndexList[i] );
                            if( pt.X < xAxis.Scale.Min || pt.Y > yAxis.Scale.Max || pt.Y < yAxis.Scale.Min )
                                continue;
                            if( pt.X > xAxis.Scale.Max )
                                break;

                            StringBuilder pointLabel = new StringBuilder();
                            Color pointColor = curve.Color;

                            // Add annotation
                            double annotationX = 0.0;
                            SeemsPointAnnotation annotation = null;
                            if( CurrentGraphItem.IsMassSpectrum )
                            {
                                PointDataMap<SeemsPointAnnotation>.MapPair annotationPair = ScanAnnotationSettings.PointAnnotations.FindNear( pt.X, ScanAnnotationSettings.MatchTolerance );
                                if( annotationPair != null )
                                {
                                    annotationX = annotationPair.Key;
                                    annotation = annotationPair.Value;
                                    matchedAnnotations.Add( annotationPair );
                                }
                            } else
                            {
                                PointDataMap<SeemsPointAnnotation>.MapPair annotationPair = ChromatogramAnnotationSettings.PointAnnotations.FindNear( pt.X, ChromatogramAnnotationSettings.MatchTolerance );
                                if( annotationPair != null )
                                {
                                    annotationX = annotationPair.Key;
                                    annotation = annotationPair.Value;
                                    matchedAnnotations.Add( annotationPair );
                                }
                            }

                            bool showMatchedAnnotations = false;
                            if( CurrentGraphItem.IsMassSpectrum )
                                showMatchedAnnotations = ScanAnnotationSettings.ShowMatchedAnnotations;
                            else
                                showMatchedAnnotations = ChromatogramAnnotationSettings.ShowMatchedAnnotations;

                            if( showMatchedAnnotations && annotation != null )
                            {
                                pointLabel.AppendLine( annotation.Label );
                                pointColor = annotation.Color;
                                //if( curve is ZedGraph.StickItem )
                                {
                                    ZedGraph.LineObj stickOverlay = new ZedGraph.LineObj( annotationX, 0, annotationX, pt.Y );
                                    //stickOverlay.IsClippedToChartRect = true;
                                    stickOverlay.Location.CoordinateFrame = ZedGraph.CoordType.AxisXYScale;
                                    stickOverlay.Line.Width = annotation.Width;
                                    stickOverlay.Line.Color = pointColor;
                                    msGraphControl.GraphPane.GraphObjList.Add( stickOverlay );
                                    dataLabels.Add( stickOverlay );
                                    //( (ZedGraph.StickItem) curve ).Color = pointColor;
                                    //( (ZedGraph.StickItem) curve ).Line.Width = annotation.Width;
                                }
                            }

                            if( CurrentGraphItem.IsMassSpectrum )
                            {
                                if( ScanAnnotationSettings.ShowPointMZs )
                                    pointLabel.AppendLine( pt.X.ToString( "f2" ) );
                                if( ScanAnnotationSettings.ShowPointIntensities )
                                    pointLabel.AppendLine( pt.Y.ToString( "f2" ) );
                            } else
                            {
                                if( ChromatogramAnnotationSettings.ShowPointTimes )
                                    pointLabel.AppendLine( pt.X.ToString( "f2" ) );
                                if( ChromatogramAnnotationSettings.ShowPointIntensities )
                                    pointLabel.AppendLine( pt.Y.ToString( "f2" ) );
                            }

                            string pointLabelString = pointLabel.ToString();
                            if( pointLabelString.Length == 0 )
                                continue;

                            // Create a text label from the X data value
                            ZedGraph.TextObj text = new ZedGraph.TextObj( pointLabelString, pt.X, yAxis.Scale.ReverseTransform( yAxis.Scale.Transform( pt.Y ) - 5 ),
                                ZedGraph.CoordType.AxisXYScale, ZedGraph.AlignH.Center, ZedGraph.AlignV.Bottom );
                            text.ZOrder = ZedGraph.ZOrder.A_InFront;
                            //text.IsClippedToChartRect = true;
                            text.FontSpec.FontColor = pointColor;
                            // Hide the border and the fill
                            text.FontSpec.Border.IsVisible = false;
                            text.FontSpec.Fill.IsVisible = false;
                            //text.FontSpec.Fill = new Fill( Color.FromArgb( 100, Color.White ) );
                            // Rotate the text to 90 degrees
                            text.FontSpec.Angle = 0;

                            if( !detectLabelOverlap( pane, g, gmap, text, out textBoundsRegion ) )
                            {
                                pane.GraphObjList.Add( text );
                                clipRegion.Union( textBoundsRegion );
                                //g.SetClip( chartRegion, CombineMode.Replace );
                                g.SetClip( clipRegion, CombineMode.Replace );
                                //clipG.Clear( Color.White );
                                //clipG.FillRegion( new SolidBrush( Color.Black ), g.Clip );
                                //clipBmp.Save( "C:\\clip.bmp" );
                                dataLabels.Add( text );
                                //text.Draw( g, pane, 1.0f );
                                //msGraphControl.DrawToBitmap( gmap, Rectangle.Round( pane.Rect ) );
                            }
                        }
                    }

                    bool showUnmatchedAnnotations = false;
                    if( CurrentGraphItem.IsMassSpectrum )
                        showUnmatchedAnnotations = ScanAnnotationSettings.ShowUnmatchedAnnotations;
                    else
                        showUnmatchedAnnotations = ChromatogramAnnotationSettings.ShowUnmatchedAnnotations;

                    if( showUnmatchedAnnotations )
                    {
                        PointDataMap<SeemsPointAnnotation> annotations = CurrentPointAnnotations;

                        foreach( PointDataMap<SeemsPointAnnotation>.MapPair annotationPair in annotations )
                        {
                            if( matchedAnnotations.Contains( annotationPair ) )
                                continue;

                            float stickLength = ( yAxis.MajorTic.Size * 2 ) / pane.Chart.Rect.Height;
                            ZedGraph.LineObj stickOverlay = new ZedGraph.LineObj( annotationPair.Key, 1, annotationPair.Key, 1 + stickLength );
                            //stickOverlay.IsClippedToChartRect = true;
                            stickOverlay.Location.CoordinateFrame = ZedGraph.CoordType.XScaleYChartFraction;
                            stickOverlay.Line.Width = annotationPair.Value.Width;
                            stickOverlay.Line.Color = annotationPair.Value.Color;
                            msGraphControl.GraphPane.GraphObjList.Add( stickOverlay );
                            dataLabels.Add( stickOverlay );
                        }
                    }
                    g.Dispose();
                } else
                {
                    // remove labels
                }
            }
        }

		private bool detectLabelOverlap( ZedGraph.GraphPane pane, Graphics g, Bitmap gmap, ZedGraph.TextObj text, out Region textBoundsRegion )
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
				if( g.Clip.IsVisible( textBoundsRectangles[j] ) )
					return true;

				for( float y = textBoundsRectangles[j].Top; y <= textBoundsRectangles[j].Bottom ; ++y )
					for( float x = textBoundsRectangles[j].Left; x <= textBoundsRectangles[j].Right; ++x )
						if( gmap.GetPixel( Convert.ToInt32( x ), Convert.ToInt32( y ) ).ToArgb() != pane.Chart.Fill.Color.ToArgb() )
							return true;
			}
			return false;
        }
        #endregion
#endif

		private void GraphForm_ResizeBegin( object sender, EventArgs e )
		{
			SuspendLayout();
			msGraphControl.Visible = false;
		}

		private void GraphForm_ResizeEnd( object sender, EventArgs e )
		{
			ResumeLayout();
			msGraphControl.Visible = true;
			Refresh();
		}
    }

    public class Pane : List<GraphItem>
    {
    }

    public class PaneList : List<Pane>
    {
    }
}