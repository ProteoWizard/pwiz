using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Drawing.Drawing2D;
using ZedGraph;

namespace MSGraph
{
    public enum MSGraphItemType
    {
        Unknown,
        Chromatogram,
        Spectrum
    }

    public enum MSGraphItemDrawMethod
    {
        Line,
        Stick
    }

    public class PointAnnotationFormat
    {
        private string format;

        public PointAnnotationFormat()
        {
            this.format = String.Empty;
        }

        public PointAnnotationFormat( string format )
        {
            StringBuilder fb = new StringBuilder( format );
            fb.Replace( "{x", "{0" );
            fb.Replace( "{y", "{1" );
            this.format = fb.ToString();
            String.Format( this.format, 123, 456 );
        }

        public override string ToString()
        {
            return format;
        }

        public string AnnotatePoint( PointPair point )
        {
            return String.Format( format, point.X, point.Y );
        }
    }

    public interface IMSGraphItemInfo
    {
        // describes the kind of item this is (Chromatogram, Spectrum, or something else);
        // only items of the same type may be overlayed on a GraphPane
        MSGraphItemType GraphItemType { get; }

        // determines the drawing method to use for this graph;
        // (e.g. line graphs for profile items and stick graphs for centroided items)
        MSGraphItemDrawMethod GraphItemDrawMethod { get; }

        // the title to show for the graph
        string Title { get; }

        // the color to draw the graph
        Color Color { get; }

        // customize the X axis to use when graphing this item;
        // when a GraphPane's type changes, this function is used to setup the X axis
        void CustomizeXAxis( Axis axis );

        // customize the Y axis to use when graphing this item;
        // when a GraphPane's type changes, this function is used to setup the Y axis
        void CustomizeYAxis( Axis axis );

        // return a string to use as a label when graphing the data point;
        // if the returned value is null or empty, there is no annotation
        string AnnotatePoint( PointPair point );

        // the entire point list of the graph
        IPointList Points { get; }
    }

    public class MSGraphItem : LineItem
    {
        public MSGraphItem( IMSGraphItemInfo info )
            : base( info.Title )
        {
            points_ = new MSPointList( info.Points );

            item_ = info.GraphItemDrawMethod == MSGraphItemDrawMethod.Line ?
                    new LineItem( info.Title, points_, info.Color, SymbolType.None ) :
                    new StickItem( info.Title, points_, info.Color );
            ( item_ as LineItem ).Line.IsAntiAlias = true;
            graphItemInfo_ = info;
        }

        private CurveItem item_;
        public CurveItem BaseItem
        {
            get { return item_; }
        }

        private MSPointList points_;
        public new MSPointList Points
        {
            get { return points_; }
        }

        private IMSGraphItemInfo graphItemInfo_;
        public IMSGraphItemInfo GraphItemInfo
        {
            get { return graphItemInfo_; }
        }

        #region Forwarding overrides
        public override Axis BaseAxis( GraphPane pane )
        {
            return item_.BaseAxis( pane );
        }

        public override bool Equals( object obj )
        {
            return item_.Equals( obj );
        }

        public override void DrawLegendKey( Graphics g, GraphPane pane, RectangleF rect, float scaleFactor )
        {
            item_.DrawLegendKey( g, pane, rect, scaleFactor );
        }

        public override bool GetCoords( GraphPane pane, int i, out string coords )
        {
            return item_.GetCoords( pane, i, out coords );
        }

        public override int GetHashCode()
        {
            return item_.GetHashCode();
        }

        public override void GetObjectData( System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context )
        {
            item_.GetObjectData( info, context );
        }

        public override void GetRange( out double xMin, out double xMax, out double yMin, out double yMax, bool ignoreInitial, bool isBoundedRanges, GraphPane pane )
        {
            item_.GetRange( out xMin, out xMax, out yMin, out yMax, ignoreInitial, isBoundedRanges, pane );
        }

        public override void MakeUnique( ColorSymbolRotator rotator )
        {
            item_.MakeUnique( rotator );
        }

        public override string ToString()
        {
            return item_.ToString();
        }

        public override Axis ValueAxis( GraphPane pane )
        {
            return item_.ValueAxis( pane );
        }
        #endregion

        public override void Draw( Graphics g, GraphPane pane, int pos, float scaleFactor )
        {
            item_.Draw( g, pane, pos, scaleFactor );
        }
    }
}
