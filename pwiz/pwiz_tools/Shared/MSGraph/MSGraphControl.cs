//
// $Id: MSGraphControl.cs 1599 2009-12-04 01:35:39Z brendanx $
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
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;
using ZedGraph;

namespace pwiz.MSGraph
{
    public partial class MSGraphControl : ZedGraphControl
    {
        public MSGraphControl()
        {
            InitializeComponent();

            GraphPane = new MSGraphPane(); // replace default ZedGraph.GraphPane

            IsZoomOnMouseCenter = false;
            IsEnableVZoom = false;
            IsEnableVPan = false;
            IsEnableHEdit = false;
            IsEnableVEdit = false;

            EditButtons = MouseButtons.Left;
            EditModifierKeys = Keys.None;

            unzoomButtons_ = new MouseButtonClicks( MouseButtons.Middle );
            unzoomButtons2_ = new MouseButtonClicks( MouseButtons.None );
            unzoomAllButtons_ = new MouseButtonClicks( MouseButtons.Left, 2 );
            unzoomAllButtons2_ = new MouseButtonClicks( MouseButtons.None );

            ZoomEvent += new ZoomEventHandler( MSGraphControl_ZoomEvent );
            MouseMoveEvent += new ZedMouseEventHandler( MSGraphControl_MouseMoveEvent );
            MouseClick += new MouseEventHandler( MSGraphControl_MouseClick );
            MouseDoubleClick += new MouseEventHandler( MSGraphControl_MouseClick );
            Resize += new EventHandler( MSGraphControl_Resize );
        }

        #region On-the-fly rescaling of graph items when panning
        bool MSGraphControl_MouseMoveEvent( ZedGraphControl sender, MouseEventArgs e )
        {
            if( e.Button == MouseButtons.None )
                return false;

            Point pos = MousePosition;
            pos = PointToClient( pos );
            MSGraphPane pane = MasterPane.FindChartRect( new PointF( (float) pos.X, (float) pos.Y ) ) as MSGraphPane;
            if( pane == null )
                pos = PointToClient( new Point( ContextMenuStrip.Left, ContextMenuStrip.Top ) );
            pane = MasterPane.FindChartRect( new PointF( (float) pos.X, (float) pos.Y ) ) as MSGraphPane;
            if( pane == null )
                return false;

            if( ( IsEnableHPan ) &&
                ( ( e.Button == PanButtons && Control.ModifierKeys == PanModifierKeys ) ||
                ( e.Button == PanButtons2 && Control.ModifierKeys == PanModifierKeys2 ) ) )
            {
                Graphics g = CreateGraphics();
                pane.SetScale(g);
            }
            return false;
        }
        #endregion

        #region Additional mouse events (Unzoom and UnzoomAll)
        public class MouseButtonClicks
        {
            private MouseButtons buttons;
            private int clicks;

            public MouseButtonClicks( MouseButtons buttons )
            {
                this.buttons = buttons;
                this.clicks = 1;
            }

            public MouseButtonClicks( string value )
            {
                string[] tokens = value.Split( ",".ToCharArray() );
                if( tokens.Length != 2 )
                    throw new FormatException( "format string must have 2 tokens" );

                switch( tokens[0] )
                {
                    case "None":
                        buttons = MouseButtons.None;
                        break;
                    case "Left":
                        buttons = MouseButtons.Left;
                        break;
                    case "Middle":
                        buttons = MouseButtons.Middle;
                        break;
                    case "Right":
                        buttons = MouseButtons.Right;
                        break;
                    case "XButton1":
                        buttons = MouseButtons.XButton1;
                        break;
                    case "XButton2":
                        buttons = MouseButtons.XButton2;
                        break;
                    default:
                        throw new FormatException( "first format string token must be one of (None,Left,Middle,Right,XButton1,XButton2)" );
                }

                if( !Int32.TryParse( tokens[1], out clicks ) )
                    throw new FormatException( "second format string token must be an integer specifying the number of button clicks" );
            }

            public MouseButtonClicks( MouseButtons buttons, int clicks )
            {
                this.buttons = buttons;
                this.clicks = clicks;
            }

            public bool MatchesEvent( MouseEventArgs e )
            {
                return ( buttons == e.Button && clicks == e.Clicks );
            }

        }

        void MSGraphControl_MouseClick( object sender, MouseEventArgs e )
        {
            MSGraphPane pane = MasterPane.FindChartRect( e.Location ) as MSGraphPane;

            if( pane != null && ( IsEnableHZoom || IsEnableVZoom ) )
            {
                if( ( unzoomButtons_.MatchesEvent( e ) && Control.ModifierKeys == unzoomModifierKeys_ ) ||
                    ( unzoomButtons2_.MatchesEvent( e ) && Control.ModifierKeys == unzoomModifierKeys2_ ) )
                {
                    if( IsSynchronizeXAxes )
                    {
                        foreach( MSGraphPane syncPane in MasterPane.PaneList )
                            syncPane.ZoomStack.Pop( syncPane );
                    } else
                        pane.ZoomStack.Pop( pane );

                } else if( unzoomAllButtons_.MatchesEvent( e ) ||
                           unzoomAllButtons2_.MatchesEvent( e ) )
                {
                    if( IsSynchronizeXAxes )
                    {
                        foreach( MSGraphPane syncPane in MasterPane.PaneList )
                            syncPane.ZoomStack.PopAll( syncPane );
                    } else
                        pane.ZoomStack.PopAll( pane );

                } else
                    return;

                Graphics g = CreateGraphics();
                if( IsSynchronizeXAxes )
                {
                    foreach( MSGraphPane syncPane in MasterPane.PaneList )
                        syncPane.SetScale(g);
                } else
                {
                    pane.SetScale(g);
                }

                Refresh();
            }
        }

        private MouseButtonClicks unzoomButtons_;
        private MouseButtonClicks unzoomButtons2_;

        [NotifyParentProperty( true ),
        Bindable( true ),
        Category( "Display" ),
        DefaultValue( "Middle,1" ),
        Description( "Determines which mouse button is used as the primary for unzooming" )]
        public MouseButtonClicks UnzoomButtons
        {
            get { return unzoomButtons_; }
            set { unzoomButtons_ = value; }
        }

        [Description( "Determines which mouse button is used as the secondary for unzooming" ),
        NotifyParentProperty( true ),
        Bindable( true ),
        Category( "Display" ),
        DefaultValue( "None,0" )]
        public MouseButtonClicks UnzoomButtons2
        {
            get { return unzoomButtons2_; }
            set { unzoomButtons2_ = value; }
        }

        private MouseButtonClicks unzoomAllButtons_;
        private MouseButtonClicks unzoomAllButtons2_;

        [Description( "Determines which mouse button is used as the secondary for undoing all zoom/pan operations" ),
        NotifyParentProperty( true ),        Bindable( true ),
        Category( "Display" ),
        DefaultValue( "Left,1" )]
        public MouseButtonClicks UnzoomAllButtons
        {
            get { return unzoomAllButtons_; }
            set { unzoomAllButtons_ = value; }
        }

        [Description( "Determines which mouse button is used as the secondary for undoing all zoom/pan operations" ),
        NotifyParentProperty( true ),
        Bindable( true ),
        Category( "Display" ),
        DefaultValue( "None,0" )]
        public MouseButtonClicks UnzoomAllButtons2
        {
            get { return unzoomAllButtons2_; }
            set { unzoomAllButtons2_ = value; }
        }

        Keys unzoomModifierKeys_;
        Keys unzoomModifierKeys2_;

        [NotifyParentProperty( true ),
        Bindable( true ),
        Description( "Determines which modifier key used as the primary for zooming" ),
        Category( "Display" ),
        DefaultValue( Keys.None )]
        public Keys UnzoomModifierKeys
        {
            get { return unzoomModifierKeys_; }
            set { unzoomModifierKeys_ = value; }
        }

        [Category( "Display" ),
        NotifyParentProperty( true ),
        Bindable( true ),
        Description( "Determines which modifier key used as the secondary for zooming" ),
        DefaultValue( Keys.None )]
        public Keys UnzoomModifierKeys2
        {
            get { return unzoomModifierKeys2_; }
            set { unzoomModifierKeys2_ = value; }
        }
        #endregion

        #region Rescaling of graph items after zoom or resize events
        void MSGraphControl_ZoomEvent( ZedGraphControl sender, ZoomState oldState, ZoomState newState )
        {
            Point pos = MousePosition;
            pos = PointToClient( pos );
            MSGraphPane pane = MasterPane.FindChartRect( new PointF( (float) pos.X, (float) pos.Y ) ) as MSGraphPane;
            if( pane == null )
                pos = PointToClient( new Point( ContextMenuStrip.Left, ContextMenuStrip.Top ) );
            pane = MasterPane.FindChartRect( new PointF( (float) pos.X, (float) pos.Y ) ) as MSGraphPane;
            if( pane == null )
                return;

            Graphics g = CreateGraphics();
            pane.SetScale(g);

            if( IsSynchronizeXAxes )
            {
                foreach( MSGraphPane syncPane in MasterPane.PaneList )
                {
                    if( syncPane == pane )
                        continue;

                    syncPane.SetScale(g);
                }
            }

            Refresh();
        }

        void MSGraphControl_Resize( object sender, EventArgs e )
        {
            Graphics g = CreateGraphics();
            foreach( GraphPane pane in MasterPane.PaneList )
                if( pane is MSGraphPane )
                    ( pane as MSGraphPane ).SetScale(g);
            Refresh();
        }
        #endregion

        #region MS graph management functions
        private CurveItem makeMSGraphItem(IMSGraphItemInfo item)
        {
            CurveItem newCurve = item.GraphItemDrawMethod == MSGraphItemDrawMethod.Line ?
                new LineItem( item.Title, new MSPointList( item.Points ), item.Color, SymbolType.None ) :
                new StickItem( item.Title, new MSPointList( item.Points ), item.Color );

            if( item.GraphItemDrawMethod == MSGraphItemDrawMethod.Line )
            {
                ( newCurve as LineItem ).Line.IsAntiAlias = true;
            }

            IMSGraphItemExtended extended = item as IMSGraphItemExtended;
            if (extended != null)
                extended.CustomizeCurve(newCurve);

            newCurve.Tag = item;
            return newCurve;
        }

        public CurveItem AddGraphItem( MSGraphPane pane, IMSGraphItemInfo item )
        {
            return AddGraphItem(pane, item, true);
        }

        public CurveItem AddGraphItem( MSGraphPane pane, IMSGraphItemInfo item, bool setScale )
        {
            if( item.GraphItemType != pane.CurrentItemType )
            {
                pane.CurveList.Clear();
                pane.CurrentItemType = item.GraphItemType;
                pane.ZoomStack.PopAll( pane );
                item.CustomizeXAxis( pane.XAxis );
                item.CustomizeYAxis( pane.YAxis );
            }

            CurveItem newItem = makeMSGraphItem( item );
            pane.CurveList.Add( newItem );
            // If you are adding multiple graph items, it is quickest to set the scale
            // once at the end.
            if (setScale)
                pane.SetScale( CreateGraphics() );
            return newItem;
        }
        #endregion

        [
            Bindable(false), 
            Browsable(false), 
            DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)
        ]
        public new MSGraphPane GraphPane
        {
            get
            {
                // Just return the first GraphPane in the list
                lock( this )
                {
                    if( MasterPane != null && MasterPane.PaneList.Count > 0 )
                        if( MasterPane.PaneList[0] is MSGraphPane )
                            return MasterPane.PaneList[0] as MSGraphPane;
                        else
                            throw new Exception( "invalid graph pane type" );
                    else
                        return null;
                }
            }

            set
            {
                lock( this )
                {
                    //Clear the list, and replace it with the specified Graphpane
                    if( MasterPane != null )
                    {
                        MasterPane.PaneList.Clear();
                        MasterPane.Add( value as GraphPane );
                    }
                }
            }
        }
    }
}