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
using System.Drawing.Drawing2D;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using pwiz.MSGraph;
using ZedGraph;

using System.Diagnostics;
using System.Linq;

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

		public pwiz.MSGraph.MSGraphControl ZedGraphControl { get { return msGraphControl; } }

        MSGraphPane focusedPane = null;
        CurveItem focusedItem = null;
        Manager manager = null;

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

        /// <summary>
        /// If true, the legend will always be shown in all panes in this GraphForm.
        /// If false, the legend will never be shown in any pane in this GraphForm.
        /// If null, the legend will be shown if there is more than one but less than 10 items in a pane.
        /// </summary>
        public bool? ShowPaneLegends { get; set; }

		public GraphForm(Manager manager)
		{
			InitializeComponent();

            this.manager = manager;
            paneList = new PaneList();
            paneLayout = PaneLayout.SingleColumn;

		    msGraphControl.BorderStyle = BorderStyle.None;

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

            msGraphControl.GraphPane.YAxis.ScaleFormatEvent += YAxis_ScaleFormatEvent;

            ContextMenuStrip dummyMenu = new ContextMenuStrip();
            dummyMenu.Opening += new CancelEventHandler( foo_Opening );
            TabPageContextMenuStrip = dummyMenu;
		}

        void foo_Opening( object sender, CancelEventArgs e )
        {
            // close the active form when the tab page strip is right-clicked
            Close();
        }

        bool msGraphControl_MouseMoveEvent( ZedGraphControl sender, MouseEventArgs e )
        {
            MSGraphPane hoverPane = sender.MasterPane.FindPane( e.Location ) as MSGraphPane;
            if( hoverPane == null )
                return false;

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
            if( focusedPane == null )
                return false;

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

            menuStrip.Items.Add(new ToolStripMenuItem("Show Data Processing", Properties.Resources.DataProcessing, GraphForm_ShowDataProcessing));
            menuStrip.Items.Add(new ToolStripMenuItem("Show Annotation", Properties.Resources.Annotation, GraphForm_ShowAnnotation));
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
            if (msGraphControl.IsSynchronizeXAxes)
            {
                msGraphControl.ZoomPane(msGraphControl.MasterPane.PaneList[0], 1, msGraphControl.GraphPane.Chart.Rect.Location, false);
                msGraphControl.ApplyToAllPanes(msGraphControl.MasterPane.PaneList[0]);
            }
            Refresh();
        }

        void GraphForm_ShowDataProcessing (object sender, EventArgs e)
        {
            manager.ShowDataProcessing();
        }

        void GraphForm_ShowAnnotation (object sender, EventArgs e)
        {
            manager.ShowAnnotationForm();
        }

        public override void Refresh()
        {
            MasterPane mp = msGraphControl.MasterPane;
            mp.Border.IsVisible = false;
            //pane.Chart.Border.IsVisible = false;
            
            if( mp.PaneList.Count != paneList.Count )
            {
                mp.PaneList.Clear();
                foreach( Pane logicalPane in paneList )
                {
                    MSGraphPane pane = new MSGraphPane();
                    pane.Border.IsVisible = false;
                    pane.IsFontsScaled = false;
                    pane.YAxis.ScaleFormatEvent += YAxis_ScaleFormatEvent;
                    mp.Add( pane );
                }
                //mp.SetLayout( msGraphControl.CreateGraphics(), paneLayout );
            } else
            {
                for( int i=0; i < paneList.Count; ++i )
                {
                    MSGraphPane pane = mp.PaneList[i] as MSGraphPane;
                    pane.Border.IsVisible = false;
                    pane.CurveList.Clear();
                    pane.GraphObjList.Clear();
                }
            }

            for( int i = 0; i < paneList.Count; ++i )
            {
                Pane logicalPane = paneList[i];
                MSGraphPane pane = mp.PaneList[i] as MSGraphPane;
                pane.IsFontsScaled = false;
                pane.Border.IsVisible = false;

                bool needSourceNamePrefix = logicalPane.Select(o => o.Source).Distinct().Count() > 1;
                int maxAutoLegendItems = needSourceNamePrefix ? 5 : 10;

                foreach( GraphItem item in logicalPane.Take(logicalPane.Count-1) )
                {
                    //item.AddSourceToId = needSourceNamePrefix;
                    msGraphControl.AddGraphItem( pane, item, false );
                }
                //logicalPane.Last().AddSourceToId = needSourceNamePrefix;
                msGraphControl.AddGraphItem(pane, logicalPane.Last(), true);

                if( mp.PaneList.Count > 1 )
                {
                    if(msGraphControl.IsSynchronizeXAxes && i < paneList.Count - 1 )
                    {
                        pane.XAxis.Title.IsVisible = false;
                        pane.XAxis.Scale.IsVisible = false;
                        pane.Margin.Bottom = 0;
                        //pane.Margin.Top = 0;
                    } else
                    {
                        //pane.Margin.Top = 0;
                        pane.XAxis.Title.IsVisible = true;
                        pane.XAxis.Scale.IsVisible = true;
                    }
                    pane.YAxis.Title.IsVisible = true;
                    pane.YAxis.Scale.IsVisible = true;
                    pane.YAxis.Title.Text = String.Join(", ", logicalPane.Select(o => o.Title)) + "\n" + pane.YAxis.Title.Text.Split('\n').Last();
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
                    pane.Legend.IsVisible = ShowPaneLegends ?? (logicalPane.Count < maxAutoLegendItems);
                    pane.Legend.Position = ZedGraph.LegendPos.TopCenter;

                    ZedGraph.ColorSymbolRotator rotator = new ColorSymbolRotator();
                    foreach( CurveItem item in pane.CurveList )
                    {
                        item.Color = rotator.NextColor;
                    }
                }

                if (paneList.Count > 0 && paneList[0].Count > 0)
                {
                    this.Text = paneList[0][0].Id;
                    if (paneList[0][0].IsMassSpectrum)
                        this.TabText = (paneList[0][0] as MassSpectrum).AbbreviatedId;
                    else
                        this.TabText = this.Text;
                }

                if (pane.XAxis.Scale.MaxAuto)
                {
                    using (Graphics g = msGraphControl.CreateGraphics())
                    {
                        if (msGraphControl.IsSynchronizeXAxes || msGraphControl.IsSynchronizeYAxes)
                        {
                            foreach (GraphPane p in msGraphControl.MasterPane.PaneList)
                            {
                                p.XAxis.ResetAutoScale(p, g);
                                p.X2Axis.ResetAutoScale(p, g);
                                foreach (YAxis axis in p.YAxisList)
                                    axis.ResetAutoScale(p, g);
                                foreach (Y2Axis axis in p.Y2AxisList)
                                    axis.ResetAutoScale(p, g);
                            }
                        }
                        else
                        {
                            pane.XAxis.ResetAutoScale(pane, g);
                            pane.X2Axis.ResetAutoScale(pane, g);
                            foreach (YAxis axis in pane.YAxisList)
                                axis.ResetAutoScale(pane, g);
                            foreach (Y2Axis axis in pane.Y2AxisList)
                                axis.ResetAutoScale(pane, g);
                        }
                    }
                    //msGraphControl.RestoreScale(pane);
                }
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
                  //!focusedPane.CurveList.Contains( focusedItem ) ) ) // somehow focusedItem.Tag can be the same as one of focusedPane.CurveList's Tags, but Contains returns false
                  !focusedPane.CurveList.Any(o => o.Tag == focusedItem.Tag)))
                setFocusedItem( mp.PaneList[0].CurveList[0] );

            msGraphControl.Refresh();
        }

        private string YAxis_ScaleFormatEvent(GraphPane pane, Axis axis, double val, int index)
        {
            return val.ToString("0.#e+0");
        }

		private Color[] overlayColors = new Color[]
		{
			Color.Red, Color.Blue, Color.Green, Color.Purple, Color.Brown,
			Color.Magenta, Color.Cyan, Color.LightGreen, Color.Beige,
			Color.DarkRed, Color.DarkBlue, Color.DarkGreen, Color.DeepPink
		};

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

    /// <summary>
    /// A list of GraphItems.
    /// </summary>
    public class Pane : List<GraphItem>
    {
    }

    /// <summary>
    /// A list of GraphForm.Panes.
    /// </summary>
    public class PaneList : List<Pane>
    {
    }

    public static class Extensions
    {
        /// <summary>
        /// Converts the integer X and Y coordinates into a floating-point PointF.
        /// </summary>
        public static PointF ToPointF( this Point point )
        {
            return new PointF( (float) point.X, (float) point.Y );
        }
    }
}