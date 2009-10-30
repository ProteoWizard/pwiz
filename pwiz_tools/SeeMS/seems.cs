//
// $Id$
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
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.Threading;
using System.IO;
using System.Collections.Specialized;
using System.Xml;
using System.Text.RegularExpressions;
using pwiz.CLI;
using pwiz.CLI.msdata;
using JWC;
using Microsoft.Win32;
using DigitalRune.Windows.Docking;
using SpyTools;

namespace seems
{
	public partial class seemsForm : Form
	{
        public static string Version = "0.5";
        public static string LastModified = "2/10/2009";

		private bool isLoaded = false;
		private OpenDataSourceDialog browseToFileDialog;
		private Manager manager;

		private MruStripMenu recentFilesMenu;
		private string seemsRegistryLocation = "Software\\SeeMS";
		private RegistryKey seemsRegistryKey;

        public Manager Manager { get { return manager; } }
		public DockPanel DockPanel { get { return dockPanel; } }
		public ToolStrip ToolStrip1 { get { return toolStrip1; } }
		public StatusStrip StatusStrip1 { get { return statusStrip1; } }
		public ToolStripStatusLabel StatusLabel { get { return toolStripStatusLabel1; } }
		public ToolStripProgressBar StatusProgressBar { get { return toolStripProgressBar1; } }
		public GraphForm CurrentGraphForm { get { return ( ActiveMdiChild is GraphForm ? (GraphForm) ActiveMdiChild : null ); } }

        public ToolStripButton DataProcessingButton { get { return dataProcessingButton; } }
		public ToolStripButton AnnotationButton { get { return annotationButton; } }

        private static Form eventLog = new Form();
        private static TextBox eventTextBox = new TextBox();
        public static Form EventLog { get { return eventLog; } }
        public static void LogEvent( object sender, EventArgs e )
        {
            if( eventTextBox.IsDisposed )
                return;

            eventTextBox.AppendText( String.Format( "sender: {0}  args: {1}\r\n", sender, e ) );
            eventTextBox.SelectionStart = eventTextBox.Text.Length;
        }

        public static void LogSpyEvent( object sender, SpyTools.SpyEventArgs e )
        {
            if( eventTextBox.IsDisposed )
                return;

            DateTime now = DateTime.Now;
            eventTextBox.AppendText(
                String.Format( "({3}:{4}:{5}:{6}) {0}: On{1}: {2}\r\n",
                sender.GetType(), e.EventName, e.EventArgs,
                now.Hour, now.Minute, now.Second, now.Millisecond ) );
            eventTextBox.SelectionStart = eventTextBox.Text.Length;
        }

        SpyTools.EventSpy spy;
		public seemsForm( string[] args )
		{
			InitializeComponent();

            ToolStripManager.RenderMode = ToolStripManagerRenderMode.Professional;
            DockPanelManager.RenderMode = DockPanelRenderMode.VisualStyles;

            eventLog.Size = new Size( 1024, 768 );
            eventTextBox.Dock = DockStyle.Fill;
            eventTextBox.Multiline = true;
            eventTextBox.ScrollBars = ScrollBars.Vertical;
            eventTextBox.ReadOnly = true;
            eventLog.Controls.Add( eventTextBox );

			this.Load += seems_Load;
			this.Resize += seems_Resize;
			this.LocationChanged += seems_LocationChanged;

			seemsRegistryKey = Registry.CurrentUser.OpenSubKey( seemsRegistryLocation );
			if( seemsRegistryKey != null )
				seemsRegistryKey.Close();

			recentFilesMenu = new MruStripMenu( recentFilesFileMenuItem, new MruStripMenu.ClickedHandler( recentFilesFileMenuItem_Click ), seemsRegistryLocation + "\\Recent File List", true );

			dockPanel.DocumentStyle = DigitalRune.Windows.Docking.DocumentStyle.DockingMdi;

            //spy = new SpyTools.EventSpy( "DockPanel", dockPanel );
            //spy.SpyEvent += new SpyTools.SpyEventHandler( LogSpyEvent );

            browseToFileDialog = new OpenDataSourceDialog();
			browseToFileDialog.InitialDirectory = "C:\\";

			manager = new Manager(this);

            if( args.Length > 0 )
                manager.ParseArgs( args );
		}

		private void seems_Load( object sender, EventArgs e )
		{
			this.StartPosition = FormStartPosition.Manual;
			this.Location = Properties.Settings.Default.MainFormLocation;
			this.Size = Properties.Settings.Default.MainFormSize;
			this.WindowState = Properties.Settings.Default.MainFormWindowState;
			isLoaded = true;
		}

		private void seems_LocationChanged( object sender, EventArgs e )
		{
			if( isLoaded && this.WindowState == FormWindowState.Normal )
				Properties.Settings.Default.MainFormLocation = this.Location;
		}

		private void seems_FormClosing( object sender, FormClosingEventArgs e )
		{
            Properties.Settings.Default.MainFormLocation = this.Location;
            Properties.Settings.Default.MainFormSize = this.Size;
            Properties.Settings.Default.MainFormWindowState = this.WindowState;
			Properties.Settings.Default.Save();

			/*foreach( DataSourceMap.MapPair sourceItr in dataSources )
				if( sourceItr.Value != null &&
					sourceItr.Value.first != null &&
					sourceItr.Value.first.MSDataFile != null )
					sourceItr.Value.first.MSDataFile.Dispose();*/
		}

		public void setFileControls( bool enabled )
		{
			// this is no longer relevant i think
		}

		public void setScanControls( bool enabled )
		{
			// if on MS1, enable mass fingerprint
			// if on MS2+, enable fragmentation
			// if on MS that is centroided in the file, disable peak processing
			// if on SRM, disable annotation
		}

		private void openFile( string filepath )
		{
            // update recent files list
            recentFilesMenu.AddFile( filepath, Path.GetFileName( filepath ) );
            recentFilesMenu.SaveToRegistry();

			manager.OpenFile(filepath);
		}

		private delegate void SetStatusLabelCallback( string status );
		public void SetStatusLabel( string status )
		{
			if( toolStrip1.InvokeRequired )
			{
				SetStatusLabelCallback d = new SetStatusLabelCallback( SetStatusLabel );
				Invoke( d, new object[] { status } );
			} else
            {
                if( toolStrip1.IsDisposed )
                    return;

				if( status.Length > 0 )
				{
					toolStripStatusLabel1.Text = status;
					toolStripStatusLabel1.Visible = true;
				} else
					toolStripStatusLabel1.Visible = false;
				toolStrip1.Refresh();
			}
		}

		private delegate void SetProgressPercentageCallback( int percentage );
		public void SetProgressPercentage( int percentage )
		{
			if( toolStrip1.InvokeRequired )
			{
				SetProgressPercentageCallback d = new SetProgressPercentageCallback( SetProgressPercentage );
				Invoke( d, new object[] { percentage } );
			} else
            {
                if( toolStrip1.IsDisposed )
                    return;

				switch( percentage )
				{
					case 0:
						toolStripProgressBar1.Visible = true;
						toolStripProgressBar1.Minimum = 0;
						toolStripProgressBar1.Maximum = 100;
						toolStripProgressBar1.Value = 0;
						break;
					case 100:
						toolStripProgressBar1.Visible = false;
						break;
					default:
						toolStripProgressBar1.Value = percentage;
						break;
				}
				toolStrip1.Refresh();
			}
		}

		private void openFile_StatusReport( object sender, StatusReportEventArgs e )
		{
			SetStatusLabel( e.Status );
		}

		private void openFile_ProgressReport( object sender, ProgressReportEventArgs e )
		{
			SetProgressPercentage( e.Percentage );
		}

		private void openFile_Click( object sender, EventArgs e )
        {
			if( browseToFileDialog.ShowDialog() == DialogResult.OK )
			{
                foreach( string dataSource in browseToFileDialog.DataSources )
                    openFile( dataSource );
			}
		}

		private void cascadeWindowMenuItem_Click( object sender, EventArgs e )
		{
			LayoutMdi( MdiLayout.Cascade );
		}

		private void tileHorizontalWindowMenuItem_Click( object sender, EventArgs e )
		{
			LayoutMdi( MdiLayout.TileHorizontal );
		}

		private void tileVerticalWindowMenuItem_Click( object sender, EventArgs e )
		{
			LayoutMdi( MdiLayout.TileVertical );
		}

		private void arrangeIconsWindowMenuItem_Click( object sender, EventArgs e )
		{
			LayoutMdi( MdiLayout.ArrangeIcons );
		}

		private void closeAllWindowMenuItem_Click( object sender, EventArgs e )
		{
			foreach( Form f in MdiChildren )
				f.Close();
		}

		private void recentFilesFileMenuItem_Click( int index, string filepath )
		{
			openFile( filepath );
		}

		private void exitFileMenuItem_Click( object sender, EventArgs e )
		{
			Application.Exit();
		}

		private void aboutHelpMenuItem_Click( object sender, EventArgs e )
		{
            AboutForm form = new AboutForm();
            form.ShowDialog();
		}

		// workaround for MDI Window list bug
		private void windowToolStripMenuItem_DropDownOpening( object sender, EventArgs e )
		{
			if( ActiveMdiChild != null )
			{
				Form activeMdiChild = ActiveMdiChild;
				ActivateMdiChild( null );
				ActivateMdiChild( activeMdiChild );
			}
		}

		private void toolStripPanel1_Layout( object sender, LayoutEventArgs e )
		{
			DockPanel.Location = new Point(0, toolStripPanel1.Height);
			DockPanel.Height = ClientSize.Height - toolStripPanel2.Height - toolStripPanel1.Height;
			DockPanel.Width = ClientSize.Width;
		}

		private void toolStripPanel2_Layout( object sender, LayoutEventArgs e )
		{
			DockPanel.Location = new Point( 0, toolStripPanel1.Height );
			DockPanel.Height = ClientSize.Height - toolStripPanel2.Height - toolStripPanel1.Height;
			DockPanel.Width = ClientSize.Width;
		}

		private void seems_ResizeBegin( object sender, EventArgs e )
		{
			if( CurrentGraphForm != null && CurrentGraphForm.WindowState == FormWindowState.Maximized )
			{
				CurrentGraphForm.SuspendLayout();
				CurrentGraphForm.ZedGraphControl.Visible = false;
			}
		}

		private void seems_Resize( object sender, EventArgs e )
		{
			DockPanel.Location = new Point( 0, toolStripPanel1.Height );
			DockPanel.Height = ClientSize.Height - toolStripPanel2.Height - toolStripPanel1.Height;
			DockPanel.Width = ClientSize.Width;
		}

		private void seems_ResizeEnd( object sender, EventArgs e )
		{
			if( isLoaded && this.WindowState != FormWindowState.Minimized )
			{
				if( this.WindowState == FormWindowState.Normal )
					Properties.Settings.Default.MainFormSize = this.Size;
				Properties.Settings.Default.MainFormWindowState = this.WindowState;
			}

			if( CurrentGraphForm != null && CurrentGraphForm.WindowState == FormWindowState.Maximized )
			{
				CurrentGraphForm.ResumeLayout();
				CurrentGraphForm.ZedGraphControl.Visible = true;
				CurrentGraphForm.Refresh();
			}
		}

        #region Integration stuff
		Point integratePeaksMouseDownLocation;
		Point integratePeaksMouseUpLocation;
		ZedGraph.LineItem integratePeaksLine;
		List<ZedGraph.PolyObj> integratePeaksAreas = new List<ZedGraph.PolyObj>();

		private void setPeakIntegrationMode( bool enabled )
		{
			if( peakIntegrationMode.Checked )
			{
				integratePeaksToolStripButton.Image = Properties.Resources.PeakIntegralActive;
				CurrentGraphForm.ZedGraphControl.IsEnableHZoom = false;
				CurrentGraphForm.ZedGraphControl.MouseDownEvent += new ZedGraph.ZedGraphControl.ZedMouseEventHandler( ZedGraphControl_MouseDownEvent );
				CurrentGraphForm.ZedGraphControl.MouseUpEvent += new ZedGraph.ZedGraphControl.ZedMouseEventHandler( ZedGraphControl_MouseUpEvent );
				CurrentGraphForm.ZedGraphControl.MouseMoveEvent += new ZedGraph.ZedGraphControl.ZedMouseEventHandler( ZedGraphControl_MouseMoveEvent );
			} else
			{
				integratePeaksToolStripButton.Image = Properties.Resources.PeakIntegral;
				CurrentGraphForm.ZedGraphControl.IsEnableHZoom = true;
				CurrentGraphForm.ZedGraphControl.MouseDownEvent -= new ZedGraph.ZedGraphControl.ZedMouseEventHandler( ZedGraphControl_MouseDownEvent );
				CurrentGraphForm.ZedGraphControl.MouseUpEvent -= new ZedGraph.ZedGraphControl.ZedMouseEventHandler( ZedGraphControl_MouseUpEvent );
				CurrentGraphForm.ZedGraphControl.MouseMoveEvent -= new ZedGraph.ZedGraphControl.ZedMouseEventHandler( ZedGraphControl_MouseMoveEvent );
			}
		}

		private void integratePeaksButton_Click( object sender, EventArgs e )
		{
			peakIntegrationMode.Checked = !peakIntegrationMode.Checked;
			setPeakIntegrationMode( peakIntegrationMode.Checked );
        }

        bool ZedGraphControl_MouseMoveEvent( ZedGraph.ZedGraphControl sender, MouseEventArgs e )
        {
            if( CurrentGraphForm == null || !peakIntegrationMode.Checked || !CurrentGraphForm.ZedGraphControl.Focused )
                return false;

            if( integratePeaksLine != null && e.Button == MouseButtons.Left )
            {
                double x = CurrentGraphForm.ZedGraphControl.GraphPane.XAxis.Scale.ReverseTransform( (float) e.X );
                double y = CurrentGraphForm.ZedGraphControl.GraphPane.YAxis.Scale.ReverseTransform( (float) e.Y );
                //CurrentGraphForm.ZedGraphControl.GraphPane.GraphObjList.Remove( integratePeaksLine );
                //integratePeaksLine = new ZedGraph.LineItem( integratePeaksLine.Location.X1, integratePeaksLine.Location.Y1, x, y );
                //integratePeaksLine.Location.CoordinateFrame = ZedGraph.CoordType.AxisXYScale;
                //CurrentGraphForm.ZedGraphControl.GraphPane.GraphObjList.Add( integratePeaksLine );
                integratePeaksLine.Points[1].X = Math.Min( CurrentGraphForm.ZedGraphControl.GraphPane.XAxis.Scale.Max, Math.Max( CurrentGraphForm.ZedGraphControl.GraphPane.XAxis.Scale.Min, x ) );
                integratePeaksLine.Points[1].Y = Math.Min( CurrentGraphForm.ZedGraphControl.GraphPane.YAxis.Scale.Max, Math.Max( CurrentGraphForm.ZedGraphControl.GraphPane.YAxis.Scale.Min, y ) );
                /*
                if( CurrentGraphForm.ZedGraphControl.GraphPane.CurveList[0].Points is PointList )
                {
                    foreach( ZedGraph.PolyObj obj in integratePeaksAreas )
                        CurrentGraphForm.ZedGraphControl.GraphPane.GraphObjList.Remove( obj );
                    integratePeaksAreas.Clear();

                    PointList pointList = (PointList) CurrentGraphForm.ZedGraphControl.GraphPane.CurveList[0].Points;
                    if( pointList.ScaledCount == 0 )
                        return false;

                    double x1, y1, x2, y2;
                    if( integratePeaksLine.Points[0].X > integratePeaksLine.Points[1].X )
                    {
                        x1 = integratePeaksLine.Points[1].X;
                        y1 = integratePeaksLine.Points[1].Y;
                        x2 = integratePeaksLine.Points[0].X;
                        y2 = integratePeaksLine.Points[0].Y;
                    } else
                    {
                        x1 = integratePeaksLine.Points[0].X;
                        y1 = integratePeaksLine.Points[0].Y;
                        x2 = integratePeaksLine.Points[1].X;
                        y2 = integratePeaksLine.Points[1].Y;
                    }

                    int lowerBoundIndex = pointList.LowerBound( x1 );
                    int upperBoundIndex = pointList.LowerBound( x2 );
                    if( upperBoundIndex < 0 )
                        upperBoundIndex = pointList.ScaledCount - 1;

                    double totalIntegratedArea = 0.0;
                    int totalAreaPoints = 0;
                    int totalAreaCount = 0;

                    // integration line can be in any of these states:

                    // * entirely to the left of the curve:
                    //		- no integration
                    // * entirely to the right of the curve:
                    //		- no integration
                    // * entirely between two consecutive points:
                    //		- integration is entirely interpolated based on data slope
                    // * starts to the left of the curve and ends between two consecutive points:
                    //		- start integration at the X value of the first data point
                    //		- end integration at the line's right X value
                    // * starts between two consecutive points and ends to the right of the curve:
                    //		- start integration at the line's left X value
                    //		- end integration at the X value of the last data point
                    // * starts between two consecutive points and ends between two different consecutive points:
                    //		- start integration at the line's left X value
                    //		- end integration at the line's right X value

                    // * entirely above the curve:
                    //		- no integration
                    // * starts above the curve and ends below the curve:
                    //		- start integration at the first intersection of the line with the curve
                    //		- end integration at the X value where the line ends
                    // * starts below the curve and ends above the curve:
                    //		- start integration at the X value where the line starts
                    //		- end integration at the last intersection of the line with the curve
                    // * entirely below the curve:
                    //		- start and end integration at the X values of the line

                    // * the Y value of the line's start point is less than 0 and the end point's is not:
                    //		- a special area point must be added before the first area's bottom left point
                    //		- add it at the intersection of the line with the X axis
                    // * the Y value of the line's end point is less than 0 and the start point's is not:
                    //		- a special area point must be added after the last area's bottom right point
                    //		- add it at the intersection of the line with the X axis
                    // * the Y values of both the start and end points of the line are less than 0:
                    //		- no special points are necessary

                    if( lowerBoundIndex >= 0 && x2 > x1 )
                    {
                        // calculate the linear function for integration line
                        double integratePeaksLineA = ( y2 - y1 ) / ( x2 - x1 );
                        double integratePeaksLineB = y1 - ( integratePeaksLineA * x1 );

                        // restrict the X range of the integration line to the minimum and maximum X values of the curve
                        // interpolate the Y value of the integration line at those X values
                        double leftInterpolatedX = Math.Max( pointList.ScaledList[0].X, x1 );
                        double leftInterpolatedY = Math.Max( 0, integratePeaksLineA * leftInterpolatedX + integratePeaksLineB );
                        double rightInterpolatedX = Math.Min( pointList.ScaledList[pointList.ScaledCount - 1].X, x2 );
                        double rightInterpolatedY = Math.Max( 0, integratePeaksLineA * rightInterpolatedX + integratePeaksLineB );

                        List<ZedGraph.PointD> areaPoints = new List<ZedGraph.PointD>();
                        //List<List<ZedGraph.PointD>> areaTrapezoids = new List<List<ZedGraph.PointD>>();
                        for( int i = Math.Max( 1, lowerBoundIndex ); i <= upperBoundIndex; ++i )
                        {
                            ZedGraph.PointPair rightPoint = pointList.ScaledList[i];
                            ZedGraph.PointPair leftPoint = pointList.ScaledList[i - 1];

                            // interpolate the Y value of the integration line at the previous and current points' X value
                            double lastPointLineY = integratePeaksLineA * leftPoint.X + integratePeaksLineB;
                            double curPointLineY = integratePeaksLineA * rightPoint.X + integratePeaksLineB;

                            // calculate the linear function between the previous and current points
                            double dataA = ( rightPoint.Y - leftPoint.Y ) / ( rightPoint.X - leftPoint.X );
                            double dataB = rightPoint.Y - ( dataA * rightPoint.X );

                            double leftInterpolatedPointY = dataA * leftInterpolatedX + dataB;
                            double rightInterpolatedPointY = dataA * rightInterpolatedX + dataB;

                            bool leftInterpolatedPointIsAboveLine = ( leftInterpolatedPointY >= leftInterpolatedY );
                            bool rightInterpolatedPointIsAboveLine = ( rightInterpolatedPointY >= rightInterpolatedY );

                            bool leftPointIsAboveLine = ( leftPoint.Y > lastPointLineY );
                            bool rightPointIsAboveLine = ( rightPoint.Y > curPointLineY );

                            bool leftPointIsLowerBound = ( i == 1 || i == lowerBoundIndex );
                            bool rightPointIsUpperBound = ( i == upperBoundIndex );

                            if( !leftInterpolatedPointIsAboveLine && !rightInterpolatedPointIsAboveLine ||
                                !leftPointIsAboveLine && !rightPointIsAboveLine )
                                continue;

                            bool needIntersection = ( leftInterpolatedPointIsAboveLine != rightInterpolatedPointIsAboveLine );

                            bool areaIsEmpty = ( areaPoints.Count == 0 );

                            if( rightPointIsAboveLine || leftPointIsAboveLine )
                            {
                                if( areaIsEmpty ) // start a new area
                                {
                                    if( leftPointIsLowerBound && leftInterpolatedPointIsAboveLine ) // interpolate the point on the curve above the line
                                    {
                                        if( y1 <= 0 && y2 > 0 )
                                        {
                                            double croppedBottomX = -integratePeaksLineB / integratePeaksLineA;
                                            if( croppedBottomX != leftInterpolatedX )
                                                areaPoints.Add( new ZedGraph.PointD( croppedBottomX, 0 ) );
                                        }
                                        areaPoints.Add( new ZedGraph.PointD( leftInterpolatedX, leftInterpolatedY ) ); // bottom left
                                        areaPoints.Add( new ZedGraph.PointD( leftInterpolatedX, leftInterpolatedPointY ) ); // top left

                                    } else if( needIntersection ) // interpolate the intersection of line and curve
                                    {
                                        double intersectX = ( dataB - integratePeaksLineB ) / ( integratePeaksLineA - dataA );
                                        double intersectY = dataA * intersectX + dataB;

                                        areaPoints.Add( new ZedGraph.PointD( intersectX, intersectY ) );
                                    }
                                }

                                if( rightPointIsUpperBound ) // end at the upper bound and add current area to the area list
                                {
                                    if( rightInterpolatedPointIsAboveLine )
                                    {
                                        // add a new point to the current area
                                        //areaPoints.Add( new ZedGraph.PointD( pointList.ScaledList[i].X, pointList.ScaledList[i].Y ) );

                                        areaPoints.Add( new ZedGraph.PointD( rightInterpolatedX, rightInterpolatedPointY ) ); // top right
                                        areaPoints.Add( new ZedGraph.PointD( rightInterpolatedX, rightInterpolatedY ) ); // bottom right
                                        if( y2 <= 0 && y1 > 0 ) // add another point if line extends below X axis
                                        {
                                            double croppedBottomX = -integratePeaksLineB / integratePeaksLineA;
                                            if( croppedBottomX != rightInterpolatedX )
                                                areaPoints.Add( new ZedGraph.PointD( croppedBottomX, 0 ) );
                                        }
                                    } else if( needIntersection ) // interpolate the intersection of line and curve
                                    {
                                        double intersectX = ( dataB - integratePeaksLineB ) / ( integratePeaksLineA - dataA );
                                        double intersectY = dataA * intersectX + dataB;

                                        areaPoints.Add( new ZedGraph.PointD( intersectX, intersectY ) );
                                    }


                                    if( areaPoints.Count == 0 )
                                        continue;

                                    ZedGraph.PolyObj integratePeaksArea = new ZedGraph.PolyObj( areaPoints.ToArray(), Color.Black, Color.FromArgb( 127, Color.Cyan ), Color.FromArgb( 127, Color.Cyan ) );
                                    integratePeaksArea.Location.CoordinateFrame = ZedGraph.CoordType.AxisXYScale;
                                    //integratePeaksArea.IsClosedFigure = true;
                                    areaPoints.Add( areaPoints[0] );
                                    integratePeaksAreas.Add( integratePeaksArea );

                                    double currentIntegratedArea = 0.0;
                                    for( int k, j = 0; j < areaPoints.Count; ++j )
                                    {
                                        k = ( j + 1 ) % areaPoints.Count;
                                        currentIntegratedArea += areaPoints[j].X * areaPoints[k].Y;
                                        currentIntegratedArea -= areaPoints[j].Y * areaPoints[k].X;
                                    }
                                    totalIntegratedArea += Math.Abs( currentIntegratedArea / 2.0 );
                                    totalAreaPoints += areaPoints.Count - 1;
                                    ++totalAreaCount;
                                    areaPoints.Clear();
                                } else
                                {
                                    // add a new top right point to the current area
                                    areaPoints.Add( new ZedGraph.PointD( pointList.ScaledList[i].X, pointList.ScaledList[i].Y ) );
                                }

                            }

                            if( !rightPointIsAboveLine && !rightPointIsUpperBound )// close the current area and add it to the area list
                            {
                                double intersectX = ( dataB - integratePeaksLineB ) / ( integratePeaksLineA - dataA );
                                double intersectY = dataA * intersectX + dataB;

                                areaPoints.Add( new ZedGraph.PointD( intersectX, intersectY ) );

                                if( areaPoints.Count == 0 )
                                    continue;

                                ZedGraph.PolyObj integratePeaksArea = new ZedGraph.PolyObj( areaPoints.ToArray(), Color.Black, Color.FromArgb( 127, Color.Cyan ), Color.FromArgb( 127, Color.Cyan ) );
                                integratePeaksArea.Location.CoordinateFrame = ZedGraph.CoordType.AxisXYScale;
                                //integratePeaksArea.IsClosedFigure = true;
                                areaPoints.Add( areaPoints[0] );
                                integratePeaksAreas.Add( integratePeaksArea );
                                double currentIntegratedArea = 0.0;
                                for( int k, j = 0; j < areaPoints.Count; ++j )
                                {
                                    k = ( j + 1 ) % areaPoints.Count;
                                    currentIntegratedArea += areaPoints[j].X * areaPoints[k].Y;
                                    currentIntegratedArea -= areaPoints[j].Y * areaPoints[k].X;
                                }
                                totalIntegratedArea += Math.Abs( currentIntegratedArea / 2.0 );
                                totalAreaPoints += areaPoints.Count - 1;
                                ++totalAreaCount;
                                areaPoints.Clear();
                            }
                        }

                        foreach( ZedGraph.PolyObj obj in integratePeaksAreas )
                            CurrentGraphForm.ZedGraphControl.GraphPane.GraphObjList.Add( obj );

#if false
                        if( areaPoints.Count > 2 )
                        {
                            List<ZedGraph.PointD> areaTrapezoid = new List<ZedGraph.PointD>();
                            areaTrapezoid.Add( areaPoints[areaPoints.Count - 3] ); // top left
                            areaTrapezoid.Add( areaPoints[areaPoints.Count - 2] ); // top right
                            areaTrapezoid.Add( areaPoints[areaPoints.Count - 1] ); // bottom right

                            // bottom left
                            double bottomLeftY = Math.Max( 0, integratePeaksLineA * areaPoints[areaPoints.Count - 3].X + integratePeaksLineB );
                            areaTrapezoid.Add( new ZedGraph.PointD( areaPoints[areaPoints.Count - 3].X, bottomLeftY ) );

                            areaTrapezoids.Add( areaTrapezoid );
                        }
                        foreach( List<ZedGraph.PointD> trapezoidPoints in areaTrapezoids )
                        {
                            ZedGraph.PolyObj trapezoid = new ZedGraph.PolyObj( trapezoidPoints.ToArray(), Color.Black, Color.Green, Color.Green );
                            trapezoid.Location.CoordinateFrame = ZedGraph.CoordType.AxisXYScale;
                            trapezoid.IsClosedFigure = true;
                            integratePeaksAreas.Add( trapezoid );
                        }
#endif
                        //CurrentGraphForm.ZedGraphControl.GraphPane.Title.Text = totalIntegratedArea.ToString("f0") + " " + totalAreaPoints + " " + totalAreaCount;
                        //CurrentGraphForm.ZedGraphControl.GraphPane.Title.IsVisible = true;
                        CurrentGraphForm.CurrentGraphItem.TotalIntegratedArea = totalIntegratedArea;
                    }
                }*/
                CurrentGraphForm.ZedGraphControl.Refresh();
                return false;
            }
            return true;
        }

        bool ZedGraphControl_MouseUpEvent( ZedGraph.ZedGraphControl sender, MouseEventArgs e )
        {
            if( CurrentGraphForm == null || !peakIntegrationMode.Checked || !CurrentGraphForm.ZedGraphControl.Focused )
                return false;

            int x0 = (int) Math.Round( CurrentGraphForm.ZedGraphControl.GraphPane.XAxis.Scale.Transform( integratePeaksLine.Points[0].X ) );
            int y0 = (int) Math.Round( CurrentGraphForm.ZedGraphControl.GraphPane.YAxis.Scale.Transform( integratePeaksLine.Points[0].Y ) );
            int x1 = (int) Math.Round( CurrentGraphForm.ZedGraphControl.GraphPane.XAxis.Scale.Transform( integratePeaksLine.Points[1].X ) );
            int y1 = (int) Math.Round( CurrentGraphForm.ZedGraphControl.GraphPane.YAxis.Scale.Transform( integratePeaksLine.Points[1].Y ) );
            integratePeaksMouseDownLocation.X = x0;
            integratePeaksMouseDownLocation.Y = y0;
            integratePeaksMouseUpLocation.X = x1;
            integratePeaksMouseUpLocation.Y = y1;
            return false;
        }

        bool ZedGraphControl_MouseDownEvent( ZedGraph.ZedGraphControl sender, MouseEventArgs e )
        {
            if( CurrentGraphForm == null || !peakIntegrationMode.Checked || !CurrentGraphForm.ZedGraphControl.Focused )
                return false;

            if( e.Button == MouseButtons.Left )
            {
                if( integratePeaksLine != null )
                {
                    double distanceToMouseDownLocation = Math.Sqrt( Math.Pow( e.Location.X - integratePeaksMouseDownLocation.X, 2.0 ) +
                                                        Math.Pow( e.Location.Y - integratePeaksMouseDownLocation.Y, 2.0 ) );
                    if( distanceToMouseDownLocation < 5.0 )
                    {
                        ZedGraph.PointPair tmp = integratePeaksLine.Points[1].Clone();
                        integratePeaksLine.Points[1].X = integratePeaksLine.Points[0].X;
                        integratePeaksLine.Points[1].Y = integratePeaksLine.Points[0].Y;
                        integratePeaksLine.Points[0].X = tmp.X;
                        integratePeaksLine.Points[0].Y = tmp.Y;
                        Point tmp2 = integratePeaksMouseUpLocation;
                        integratePeaksMouseUpLocation = integratePeaksMouseDownLocation;
                        integratePeaksMouseDownLocation = tmp2;
                        return false;
                    } else
                    {
                        double distanceToMouseUpLocation = Math.Sqrt( Math.Pow( e.Location.X - integratePeaksMouseUpLocation.X, 2.0 ) +
                                                        Math.Pow( e.Location.Y - integratePeaksMouseUpLocation.Y, 2.0 ) );
                        if( distanceToMouseUpLocation >= 5.0 )
                        {
                            // clear existing line and start a new one
                            CurrentGraphForm.ZedGraphControl.GraphPane.CurveList.Remove( integratePeaksLine );
                            integratePeaksLine = null;
                            foreach( ZedGraph.PolyObj obj in integratePeaksAreas )
                                CurrentGraphForm.ZedGraphControl.GraphPane.GraphObjList.Remove( obj );
                            integratePeaksAreas.Clear();
                            CurrentGraphForm.ZedGraphControl.Refresh();
                        } else
                            return false;
                    }
                }

                integratePeaksMouseDownLocation = e.Location;
                double x = CurrentGraphForm.ZedGraphControl.GraphPane.XAxis.Scale.ReverseTransform( (float) e.X );
                double y = CurrentGraphForm.ZedGraphControl.GraphPane.YAxis.Scale.ReverseTransform( (float) e.Y );
                x = Math.Min( CurrentGraphForm.ZedGraphControl.GraphPane.XAxis.Scale.Max, Math.Max( CurrentGraphForm.ZedGraphControl.GraphPane.XAxis.Scale.Min, x ) );
                y = Math.Min( CurrentGraphForm.ZedGraphControl.GraphPane.YAxis.Scale.Max, Math.Max( CurrentGraphForm.ZedGraphControl.GraphPane.YAxis.Scale.Min, y ) );
                integratePeaksLine = new ZedGraph.LineItem( "", new double[] { x, x }, new double[] { y, y }, Color.Black, ZedGraph.SymbolType.None );
                integratePeaksLine.Line.IsAntiAlias = true;
                integratePeaksLine.Symbol.Type = ZedGraph.SymbolType.Square;
                integratePeaksLine.Symbol.IsVisible = true;
                integratePeaksLine.Symbol.Border.Width = 2;
                integratePeaksLine.Symbol.Border.Color = Color.Black;
                //integratePeaksLine.Location.CoordinateFrame = ZedGraph.CoordType.AxisXYScale;
                CurrentGraphForm.ZedGraphControl.GraphPane.CurveList.Add( integratePeaksLine );
            }
            return false;
        }

        private void clearCurrentIntegration_Click( object sender, EventArgs e )
        {
            if( integratePeaksLine != null )
            {
                // clear existing line and start a new one
                CurrentGraphForm.ZedGraphControl.GraphPane.CurveList.Remove( integratePeaksLine );
                integratePeaksLine = null;
                foreach( ZedGraph.PolyObj obj in integratePeaksAreas )
                    CurrentGraphForm.ZedGraphControl.GraphPane.GraphObjList.Remove( obj );
                integratePeaksAreas.Clear();
                CurrentGraphForm.ZedGraphControl.Refresh();
            }

            //CurrentGraphForm.CurrentGraphItem.TotalIntegratedArea = 0.0;
        }

        private void clearAllIntegrationsToolStripMenuItem_Click( object sender, EventArgs e )
        {
            if( integratePeaksLine != null )
            {
                // clear existing line and start a new one
                CurrentGraphForm.ZedGraphControl.GraphPane.CurveList.Remove( integratePeaksLine );
                integratePeaksLine = null;
                foreach( ZedGraph.PolyObj obj in integratePeaksAreas )
                    CurrentGraphForm.ZedGraphControl.GraphPane.GraphObjList.Remove( obj );
                integratePeaksAreas.Clear();
                CurrentGraphForm.ZedGraphControl.Refresh();
            }
        }

        private void exportAllIntegrationsToolStripMenuItem_Click( object sender, EventArgs e )
        {
            manager.ExportIntegration();
        }
        #endregion

        private void dataProcessingButton_Click( object sender, EventArgs e )
        {
            manager.ShowDataProcessing();
        }

        private void previewAsMzMLToolStripMenuItem_Click( object sender, EventArgs e )
        {
            manager.ShowCurrentSourceAsMzML();
        }

        private void annotationButton_Click( object sender, EventArgs e )
        {
            manager.ShowAnnotationForm();
        }

        private void eventLogToolStripMenuItem_Click( object sender, EventArgs e )
        {
            eventLog.Show();
        }
	}
}