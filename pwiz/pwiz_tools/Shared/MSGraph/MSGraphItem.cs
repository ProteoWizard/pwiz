//
// $Id: MSGraphItem.cs 1599 2009-12-04 01:35:39Z brendanx $
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

using System.Drawing;
using ZedGraph;

namespace pwiz.MSGraph
{
    public enum MSGraphItemType
    {
        unknown,
        chromatogram,
        spectrum
    }

    public enum MSGraphItemDrawMethod
    {
        line,
        stick,
        fill
    }

    public class PointAnnotation
    {
        public string Label { get; set; }

        public GraphObj ExtraAnnotation { get; set; }

        public FontSpec FontSpec { get; set; }

        public PointAnnotation()
        {
            Label = string.Empty;
            ExtraAnnotation = null;
            FontSpec = new FontSpec();
        }

        public PointAnnotation( string label )
        {
            Label = label;
            ExtraAnnotation = null;
            FontSpec = new FontSpec();
        }

        public PointAnnotation( string label, FontSpec fontSpec )
        {
            Label = label;
            ExtraAnnotation = null;
            FontSpec = fontSpec;
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
        /// gets the width of graph lines
        /// </summary>
        float LineWidth { get; }

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
        void AddAnnotations(MSGraphPane graphPane, Graphics g, MSPointList pointList, GraphObjList annotations);

        /// <summary>
        /// gets the entire point list of the graph
        /// </summary>
        IPointList Points { get; }
    }

    public interface IMSGraphItemExtended : IMSGraphItemInfo
    {
        /// <summary>
        /// Allow the graph item finer control of display properties on the
        /// underlying <see cref="CurveItem"/> created from it.
        /// </summary>
        /// <param name="curveItem"></param>
        void CustomizeCurve(CurveItem curveItem);

        /// <summary>
        /// fill in a list of ZedGraph objects to display on the graph that get considered
        /// before the annotations added by <see cref="IMSGraphItemInfo.AnnotatePoint"/>;
        /// the list may change depending on the state of the pointList
        /// and the annotations that have already been added
        /// </summary>
        void AddPreCurveAnnotations(MSGraphPane graphPane, Graphics g, MSPointList pointList, GraphObjList annotations);
    }
}
