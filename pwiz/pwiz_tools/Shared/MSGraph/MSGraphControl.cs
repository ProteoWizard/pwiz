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
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ZedGraph;

namespace pwiz.MSGraph
{
    public partial class MSGraphControl : ZedGraphControl
    {
        public MSGraphControl()
        {
            InitializeComponent();

            GraphPane = new MSGraphPane(new LabelBoundsCache()); // replace default ZedGraph.GraphPane

            IsZoomOnMouseCenter = true;
            IsEnableVZoom = false;
            IsEnableVPan = false;
            IsEnableHEdit = false;
            IsEnableVEdit = false;

            EditButtons = MouseButtons.Left;
            EditModifierKeys = Keys.None;

            _unzoomButtons = new MouseButtonClicks( MouseButtons.Middle );
            _unzoomButtons2 = new MouseButtonClicks( MouseButtons.None );
            _unzoomAllButtons = new MouseButtonClicks( MouseButtons.Left, 2 );
            _unzoomAllButtons2 = new MouseButtonClicks( MouseButtons.None );

            ZoomEvent += MSGraphControl_ZoomEvent;
            MouseMoveEvent += MSGraphControl_MouseMoveEvent;
            MouseClick += MSGraphControl_MouseClick;
            MouseDoubleClick += MSGraphControl_MouseClick;
            Resize += MSGraphControl_Resize;
        }

        #region On-the-fly rescaling of graph items when panning
        bool MSGraphControl_MouseMoveEvent( ZedGraphControl sender, MouseEventArgs e )
        {
            if( e.Button == MouseButtons.None )
                return false;
            if( ( IsEnableHPan ) &&
                ( ( e.Button == PanButtons && ModifierKeys == PanModifierKeys ) ||
                ( e.Button == PanButtons2 && ModifierKeys == PanModifierKeys2 ) ) )
            {
                Graphics g = CreateGraphics();
                foreach (var pane in MasterPane.PaneList.OfType<MSGraphPane>())
                {
                    pane.SetScale(g);
                }
            }
            return false;
        }
        #endregion

        #region Additional mouse events (Unzoom and UnzoomAll)
        public class MouseButtonClicks
        {
            private readonly MouseButtons _buttons;
            private readonly int _clicks;

            public MouseButtonClicks( MouseButtons buttons )
            {
                _buttons = buttons;
                _clicks = 1;
            }

            public MouseButtonClicks( string value )
            {
                string[] tokens = value.Split( ",".ToCharArray() ); // Not L10N
                if( tokens.Length != 2 )
                    throw new FormatException( "format string must have 2 tokens" ); // Not L10N

                // ReSharper disable NonLocalizedString
                switch( tokens[0] )
                {
                    case "None":
                        _buttons = MouseButtons.None;
                        break;
                    case "Left":
                        _buttons = MouseButtons.Left;
                        break;
                    case "Middle":
                        _buttons = MouseButtons.Middle;
                        break;
                    case "Right":
                        _buttons = MouseButtons.Right;
                        break;
                    case "XButton1":
                        _buttons = MouseButtons.XButton1;
                        break;
                    case "XButton2":
                        _buttons = MouseButtons.XButton2;
                        break;
                    default:
                        throw new FormatException("first format string token must be one of (None,Left,Middle,Right,XButton1,XButton2)"); // Not L10N
                }
                // ReSharper restore NonLocalizedString

                if( !Int32.TryParse( tokens[1], out _clicks ) )
                    throw new FormatException("second format string token must be an integer specifying the number of button clicks"); // Not L10N
            }

            public MouseButtonClicks( MouseButtons buttons, int clicks )
            {
                _buttons = buttons;
                _clicks = clicks;
            }

            public bool MatchesEvent( MouseEventArgs e )
            {
                return ( _buttons == e.Button && _clicks == e.Clicks );
            }

        }

        void MSGraphControl_MouseClick( object sender, MouseEventArgs e )
        {
            MSGraphPane pane = MasterPane.FindChartRect( e.Location ) as MSGraphPane;

            if( pane != null && ( IsEnableHZoom || IsEnableVZoom ) )
            {
                if( ( _unzoomButtons.MatchesEvent( e ) && ModifierKeys == _unzoomModifierKeys ) ||
                    ( _unzoomButtons2.MatchesEvent( e ) && ModifierKeys == _unzoomModifierKeys2 ) )
                {
                    if( IsSynchronizeXAxes )
                    {
                        foreach( MSGraphPane syncPane in MasterPane.PaneList )
                            syncPane.ZoomStack.Pop( syncPane );
                    } else
                        pane.ZoomStack.Pop( pane );

                } else if( _unzoomAllButtons.MatchesEvent( e ) ||
                           _unzoomAllButtons2.MatchesEvent( e ) )
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

        private MouseButtonClicks _unzoomButtons;
        private MouseButtonClicks _unzoomButtons2;

        [NotifyParentProperty( true ),
        Bindable( true ),
        Category( "Display" ),
        DefaultValue( "Middle,1" ),
        Description( "Determines which mouse button is used as the primary for unzooming" )]
        public MouseButtonClicks UnzoomButtons
        {
            get { return _unzoomButtons; }
            set { _unzoomButtons = value; }
        }

        [Description( "Determines which mouse button is used as the secondary for unzooming" ),
        NotifyParentProperty( true ),
        Bindable( true ),
        Category( "Display" ),
        DefaultValue( "None,0" )]
        public MouseButtonClicks UnzoomButtons2
        {
            get { return _unzoomButtons2; }
            set { _unzoomButtons2 = value; }
        }

        private MouseButtonClicks _unzoomAllButtons;
        private MouseButtonClicks _unzoomAllButtons2;

        [Description( "Determines which mouse button is used as the secondary for undoing all zoom/pan operations" ),
        NotifyParentProperty( true ),        Bindable( true ),
        Category( "Display" ),
        DefaultValue( "Left,1" )]
        public MouseButtonClicks UnzoomAllButtons
        {
            get { return _unzoomAllButtons; }
            set { _unzoomAllButtons = value; }
        }

        [Description( "Determines which mouse button is used as the secondary for undoing all zoom/pan operations" ),
        NotifyParentProperty( true ),
        Bindable( true ),
        Category( "Display" ),
        DefaultValue( "None,0" )]
        public MouseButtonClicks UnzoomAllButtons2
        {
            get { return _unzoomAllButtons2; }
            set { _unzoomAllButtons2 = value; }
        }

        Keys _unzoomModifierKeys;
        Keys _unzoomModifierKeys2;

        [NotifyParentProperty( true ),
        Bindable( true ),
        Description( "Determines which modifier key used as the primary for zooming" ),
        Category( "Display" ),
        DefaultValue( Keys.None )]
        public Keys UnzoomModifierKeys
        {
            get { return _unzoomModifierKeys; }
            set { _unzoomModifierKeys = value; }
        }

        [Category( "Display" ),
        NotifyParentProperty( true ),
        Bindable( true ),
        Description( "Determines which modifier key used as the secondary for zooming" ),
        DefaultValue( Keys.None )]
        public Keys UnzoomModifierKeys2
        {
            get { return _unzoomModifierKeys2; }
            set { _unzoomModifierKeys2 = value; }
        }
        #endregion

        #region Rescaling of graph items after zoom or resize events
        void MSGraphControl_ZoomEvent( ZedGraphControl sender, ZoomState oldState, ZoomState newState, PointF mousePosition )
        {
            MSGraphPane pane = MasterPane.FindChartRect(mousePosition) as MSGraphPane;
            if( pane == null )
                mousePosition = PointToClient(new Point(ContextMenuStrip.Left, ContextMenuStrip.Top));
            pane = MasterPane.FindChartRect( mousePosition ) as MSGraphPane;
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
        private static CurveItem makeMSGraphItem(IMSGraphItemInfo item)
        {
            CurveItem newCurve = item.GraphItemDrawMethod == MSGraphItemDrawMethod.stick ?
                new StickItem( item.Title, new MSPointList( item.Points ), item.Color, item.LineWidth ) :
                new LineItem( item.Title, new MSPointList( item.Points ), item.Color, SymbolType.None );

            if( item.GraphItemDrawMethod != MSGraphItemDrawMethod.stick )
            {
                var line = ((LineItem) newCurve).Line;
                line.IsAntiAlias = true;
                if (item.GraphItemDrawMethod == MSGraphItemDrawMethod.fill)
                {
                    line.Fill = new Fill(item.Color);
                    line.Color = Color.FromArgb(200, 140, 140, 200);
                }
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
                            throw new Exception( "invalid graph pane type" ); // Not L10N
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
                        MasterPane.Add( value );
                    }
                }
            }
        }
    }
}