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

    public class PointAnnotation
    {
        private string label;
        public string Label
        {
            get { return label; }
            set { label = value; }
        }

        private GraphObj extraAnnotation;
        public GraphObj ExtraAnnotation
        {
            get { return extraAnnotation; }
            set { extraAnnotation = value; }
        }

        private FontSpec fontSpec;
        public FontSpec FontSpec
        {
            get { return fontSpec; }
            set { fontSpec = value; }
        }

        public PointAnnotation()
        {
            label = string.Empty;
            extraAnnotation = null;
            fontSpec = new FontSpec();
        }

        public PointAnnotation( string label )
        {
            this.label = label;
            extraAnnotation = null;
            fontSpec = new FontSpec();
        }

        public PointAnnotation( string label, FontSpec fontSpec )
        {
            this.label = label;
            extraAnnotation = null;
            this.fontSpec = fontSpec;
        }
    }

    public interface IMSGraphItemInfo
    {
        /// <summary>
        /// gets the kind of item this is (Chromatogram, Spectrum, or something else);
        /// only items of the same type may be overlayed on a GraphPane
        /// </summary>
        MSGraphItemType GraphItemType { get; }

        /// <summary>
        /// gets the drawing method to use for this graph;
        /// (e.g. line graphs for profile items and stick graphs for centroided items)
        /// </summary>
        MSGraphItemDrawMethod GraphItemDrawMethod { get; }

        /// <summary>
        /// gets the title to show for the graph
        /// </summary>
        string Title { get; }

        /// <summary>
        /// gets the color to draw the graph
        /// </summary>
        Color Color { get; }

        /// <summary>
        /// customize the X axis to use when graphing this item;
        /// when a GraphPane's type changes, this function is used to setup the X axis
        /// </summary>
        void CustomizeXAxis( Axis axis );

        /// <summary>
        /// customize the Y axis to use when graphing this item;
        /// when a GraphPane's type changes, this function is used to setup the Y axis
        /// </summary>
        void CustomizeYAxis( Axis axis );

        /// <summary>
        /// return a string to use as a label when graphing the data point;
        /// if the returned value is null or empty, there is no annotation
        /// </summary>
        PointAnnotation AnnotatePoint( PointPair point );

        /// <summary>
        /// fill in a list of ZedGraph objects to display on the graph;
        /// the list may change depending on the state of the pointList
        /// and the annotations that have already been added
        /// </summary>
        void AddAnnotations( MSPointList pointList, GraphObjList annotations );

        /// <summary>
        /// gets the entire point list of the graph
        /// </summary>
        IPointList Points { get; }
    }
}
