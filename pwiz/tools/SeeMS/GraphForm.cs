using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;
using System.Windows.Forms;

namespace seems
{
	public partial class GraphForm : Form
	{
		private bool isLoaded = false;

		public bool IsLoaded
		{
			get { return isLoaded; }
			set { isLoaded = value; }
		}

		private DataSource currentDataSource = null;
		public DataSource DataSource { get { return currentDataSource; } }

		private GraphItem currentGraphItem = null;
		public GraphItem CurrentGraphItem { get { return (GraphItem) currentGraphItem; } }

		private int currentGraphItemIndex = -1;
		public int CurrentGraphItemIndex { get { return currentGraphItemIndex; } }

		private ChromatogramAnnotationSettings chromatogramAnnotationSettings = new ChromatogramAnnotationSettings();
		public ChromatogramAnnotationSettings ChromatogramAnnotationSettings { get { return chromatogramAnnotationSettings; } }

		private ScanAnnotationSettings scanAnnotationSettings = new ScanAnnotationSettings();
		public ScanAnnotationSettings ScanAnnotationSettings { get { return scanAnnotationSettings; } }

		public string CurrentSourceFilepath { get { return currentDataSource.CurrentFilepath; } set { currentDataSource.SetInputFile( value ); } }

		public PointDataMap<SeemsPointAnnotation> CurrentPointAnnotations
		{
			get
			{
				if( CurrentGraphItem.IsMassSpectrum )
					return scanAnnotationSettings.PointAnnotations;
				else
					return chromatogramAnnotationSettings.PointAnnotations;
			}
		}

		public ZedGraph.ZedGraphControl ZedGraphControl { get { return zedGraphControl1; } }

		private seems SeemsMdiParent { get { return (seems) MdiParent; } }

		public GraphForm()
		{
			InitializeComponent();

			this.Load += GraphForm_Load;

			zedGraphControl1.GraphPane.Title.IsVisible = false;
			zedGraphControl1.GraphPane.Chart.Border.IsVisible = false;
			zedGraphControl1.GraphPane.Y2Axis.IsVisible = false;
			zedGraphControl1.GraphPane.X2Axis.IsVisible = false;
			zedGraphControl1.GraphPane.XAxis.MajorTic.IsOpposite = false;
			zedGraphControl1.GraphPane.YAxis.MajorTic.IsOpposite = false;
			zedGraphControl1.GraphPane.XAxis.MinorTic.IsOpposite = false;
			zedGraphControl1.GraphPane.YAxis.MinorTic.IsOpposite = false;
			zedGraphControl1.GraphPane.IsFontsScaled = false;
			zedGraphControl1.IsZoomOnMouseCenter = false;
			zedGraphControl1.GraphPane.YAxis.Scale.MaxGrace = 0.1;
			zedGraphControl1.IsEnableVZoom = false;
			zedGraphControl1.PanButtons = MouseButtons.Left;
			zedGraphControl1.PanModifierKeys = Keys.Control;
			zedGraphControl1.PanButtons2 = MouseButtons.None;
			zedGraphControl1.IsEnableHEdit = false;
			zedGraphControl1.IsEnableVEdit = false;
			zedGraphControl1.EditButtons = MouseButtons.Left;
			zedGraphControl1.EditModifierKeys = Keys.None;
		}

		public static GraphForm CreateNewWindow( Form mdiParent, bool giveFocus )
		{
			// create a new window with this data source
			GraphForm graphForm = new GraphForm();
			graphForm.MdiParent = mdiParent;
			graphForm.Show();
			if( giveFocus )
				graphForm.Activate();

			return graphForm;
		}

		private void GraphForm_Load( object sender, EventArgs e )
		{
			this.StartPosition = FormStartPosition.Manual;
			this.Location = Properties.Settings.Default.LastGraphFormLocation;
			this.Size = Properties.Settings.Default.LastGraphFormSize;
			this.WindowState = Properties.Settings.Default.LastGraphFormWindowState;
			isLoaded = true;
		}

		public void updateGraph()
		{
			updateGraph( false );
		}

		public void updateGraph( bool clearOverlays )
		{
			if( clearOverlays )
				currentOverlays.Clear();

			showData( currentGraphItemIndex, false );
			DataSource primaryDataSource = currentDataSource;
			foreach( RefPair<DataSource, int> overlayDataPair in currentOverlays )
			{
				currentDataSource = overlayDataPair.first;
				showData( overlayDataPair.second, true );
			}
			currentDataSource = primaryDataSource;
		}

		public void ShowData( DataSource dataSource, int dataIndex )
		{
			currentOverlays.Clear();
			currentDataSource = dataSource;
			currentGraphItemIndex = dataIndex;
			showData( dataIndex, false );
		}

		List<RefPair<DataSource, int>> currentOverlays = new List<RefPair<DataSource, int>>();
		public void ShowDataOverlay( DataSource dataSource, int dataIndex )
		{
			currentOverlays.Add( new RefPair<DataSource, int>( dataSource, dataIndex ) );
			currentDataSource = dataSource;
			currentGraphItemIndex = dataIndex;
			showData( dataIndex, true );
		}

		private Color[] overlayColors = new Color[]
		{
			Color.Red, Color.Blue, Color.Green, Color.Purple, Color.Brown,
			Color.Magenta, Color.Cyan, Color.LightGreen, Color.Beige,
			Color.DarkRed, Color.DarkBlue, Color.DarkGreen, Color.DeepPink
		};

		private GraphItem showData( int dataIndex, bool isOverlay )
		{
			GraphItem newGraphItem = currentDataSource.CurrentGraphItems[dataIndex];
			ZedGraph.GraphPane pane = zedGraphControl1.GraphPane;

			if( isOverlay && pane.CurveList.Count > overlayColors.Length )
				MessageBox.Show( "SeeMS only supports up to " + overlayColors.Length + " simultaneous overlays.", "Too many overlays", MessageBoxButtons.OK, MessageBoxIcon.Stop );

			// set form title
			if( !isOverlay )
				Text = String.Format( "{0} - {1}", currentDataSource.Name, newGraphItem.Id );
			else
				Text += "," + newGraphItem.Id;

			if( !isOverlay )
				pane.CurveList.Clear();

			if( currentGraphItem != null && newGraphItem.IsMassSpectrum != currentGraphItem.IsMassSpectrum )
			{
				zedGraphControl1.RestoreScale( pane );
				zedGraphControl1.ZoomOutAll( pane );
			}
			bool isScaleAuto = !pane.IsZoomed;

			//pane.GraphObjList.Clear();

			if( newGraphItem is MassSpectrum )
			{
				MassSpectrum scan = (MassSpectrum) newGraphItem;

				// the header does not have the data points
				pane.YAxis.Title.Text = "Intensity";
				pane.XAxis.Title.Text = "m/z";

				PointList pointList = scan.PointList;
				if( pointList.FullCount > 0 )
				{
					int bins = (int) pane.CalcChartRect( zedGraphControl1.CreateGraphics() ).Width;
					if( isScaleAuto )
						pointList.SetScale( bins, pointList[0].X, pointList[pointList.Count - 1].X );
					else
						pointList.SetScale( bins, pane.XAxis.Scale.Min, pane.XAxis.Scale.Max );

					if( scan.Source.DoCentroiding )
					{
						ZedGraph.StickItem stick = pane.AddStick( scan.Id, pointList, Color.Gray );
						stick.Symbol.IsVisible = false;
						stick.Line.Width = 1;
					} else
						pane.AddCurve( newGraphItem.Id, pointList, Color.Gray, ZedGraph.SymbolType.None );
				}
			} else
			{
				Chromatogram chromatogram = (Chromatogram) newGraphItem;

				PointList pointList = chromatogram.PointList;
				if( pointList.FullCount > 0 )
				{
					int bins = (int) pane.CalcChartRect( zedGraphControl1.CreateGraphics() ).Width;

					if( isScaleAuto )
						pointList.SetScale( bins, pointList[0].X, pointList[pointList.Count - 1].X );
					else
						pointList.SetScale( bins, pane.XAxis.Scale.Min, pane.XAxis.Scale.Max );
					pane.YAxis.Title.Text = "Total Intensity";
					pane.XAxis.Title.Text = "Retention Time (in minutes)";
					pane.AddCurve( newGraphItem.Id, pointList, Color.Gray, ZedGraph.SymbolType.None );
				}
			}
			pane.AxisChange();

			if( isOverlay )
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
				currentGraphItem = newGraphItem;
			}

			SetDataLabelsVisible( true );
			zedGraphControl1.Refresh();
			return newGraphItem;
		}

		// create a map of point X coordinates to point indexes in descending order by the Y coordinate
		// for each point, create a text label of the X and/or Y value (user configurable)
		// check that the label will not overlap any other labels (how to check against data lines as well?)
		// if no overlap, add the label and mask the 
		private ZedGraph.GraphObjList dataLabels = new ZedGraph.GraphObjList();
		public void SetDataLabelsVisible( bool visible )
		{
			// set direct references
			ZedGraph.GraphPane pane = zedGraphControl1.GraphPane;
			ZedGraph.Axis xAxis = pane.XAxis;
			ZedGraph.Axis yAxis = pane.YAxis;

			if( CurrentGraphItem == null )
				return;

			//zedGraphControl1.GraphPane.GraphObjList.Clear();
			foreach( ZedGraph.GraphObj dataLabel in dataLabels )
				zedGraphControl1.GraphPane.GraphObjList.Remove( dataLabel );

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

				Graphics g = zedGraphControl1.CreateGraphics();
				System.Drawing.Bitmap gmap;

				try
				{
					//pane.Draw( g );
					pane.CurveList.Draw( g, pane, 1.0f );
					gmap = new Bitmap( Convert.ToInt32( pane.Rect.Width ), Convert.ToInt32( pane.Rect.Height ) );
					zedGraphControl1.DrawToBitmap( gmap, Rectangle.Round( pane.Rect ) );
				} catch
				{
					return;
				}

				Region textBoundsRegion;
				Region chartRegion = new Region( pane.Chart.Rect );
				Region clipRegion = new Region();
				clipRegion.MakeEmpty();
				g.SetClip( zedGraphControl1.MasterPane.Rect, CombineMode.Replace );
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
							//zedGraphControl1.DrawToBitmap( gmap, Rectangle.Round( pane.Rect ) );
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
								zedGraphControl1.GraphPane.GraphObjList.Add( stickOverlay );
								dataLabels.Add(stickOverlay);
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
							//zedGraphControl1.DrawToBitmap( gmap, Rectangle.Round( pane.Rect ) );
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
						zedGraphControl1.GraphPane.GraphObjList.Add( stickOverlay );
						dataLabels.Add( stickOverlay );
					}
				}
				g.Dispose();
			} else
			{
				// remove labels
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

		private void zedGraphControl1_ZoomEvent( ZedGraph.ZedGraphControl sender, ZedGraph.ZoomState oldState, ZedGraph.ZoomState newState )
		{
			ZedGraph.GraphPane pane = zedGraphControl1.GraphPane;
			int bins = (int) pane.CalcChartRect( zedGraphControl1.CreateGraphics() ).Width;
			foreach( ZedGraph.CurveItem curve in pane.CurveList )
				if( curve.Points is PointList )
					( curve.Points as PointList ).SetScale( bins, pane.XAxis.Scale.Min, pane.XAxis.Scale.Max );
			pane.AxisChange();
			SetDataLabelsVisible( true );
			Refresh();
		}

		private void zedGraphControl1_Resize( object sender, EventArgs e )
		{
			if( WindowState == FormWindowState.Minimized )
				return;

			ZedGraph.GraphPane pane = zedGraphControl1.GraphPane;
			int bins = (int) pane.CalcChartRect( zedGraphControl1.CreateGraphics() ).Width;
			foreach( ZedGraph.CurveItem curve in pane.CurveList )
				if( curve.Points is PointList )
					( curve.Points as PointList ).SetScale( bins, pane.XAxis.Scale.Min, pane.XAxis.Scale.Max );
			SetDataLabelsVisible( true );
		}

		private void GraphForm_MouseClick( object sender, MouseEventArgs e )
		{
			if( e.Button == MouseButtons.Middle )
				zedGraphControl1.ZoomOut( zedGraphControl1.GraphPane );
		}

		private void GraphForm_ResizeBegin( object sender, EventArgs e )
		{
			SuspendLayout();
			zedGraphControl1.Visible = false;
		}

		private void GraphForm_ResizeEnd( object sender, EventArgs e )
		{
			ResumeLayout();
			zedGraphControl1.Visible = true;
			Refresh();
		}
	}
}