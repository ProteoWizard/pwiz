/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Drawing;
using ZedGraph;
using pwiz.Common.Graph;

namespace pwiz.Skyline.Controls.Graphs
{
    /// <summary>
    /// LineItem with an error bar at each symbol indicating mean and standard deviation.
    /// </summary>
    [CurveDataHandler(typeof(LineErrorBarDataHandler))]
    public class LineErrorBarItem : LineItem
    {
        public LineErrorBarItem(String label, 
                               double[] xValues, double[] yValues, double[] errorValues,
                               Color color, Color errorColor) 
            : this(label, MeanErrorBarItem.MakePointPairList(xValues, yValues, errorValues), color, errorColor)
        {
        }

        public LineErrorBarItem(String label, IPointList pointPairList, Color color, Color errorColor)
            : base(label, pointPairList, color, SymbolType.None)
        {
            _line = new Line { Width = 2, Color = color, IsAntiAlias = true };
            _symbol = new SymbolErrorBar(color, errorColor);
        }
    }

    /// <summary>
    /// A point symbol with an error bar based on the <see cref="ErrorTag"/> member of the <see cref="PointPair"/>.
    /// Only y-axis scaled error bars are currently supported.
    /// </summary>
    public class SymbolErrorBar : Symbol
    {
        private const float PIX_TERM_WIDTH = 5;

        public SymbolErrorBar(Color color, Color errorColor)
        {
            // Pens scale better in EMF files
            ErrorPen = new Pen(errorColor);

            Type = SymbolType.Circle;
            Size = 4;
            Border = new Border(color, 0);
            Fill = new Fill(color);
        }

        public Pen ErrorPen { get; private set; }

        protected override object TransformTag(GraphPane pane, object tag, double x, double y, Scale xScale, Scale yScale, bool isOverrideOrdinal, int pointIndex)
        {
            var errorTag = tag as ErrorTag;
            if (errorTag == null || errorTag.Error == 0 || errorTag.Error == PointPairBase.Missing)
                return null;
            double top = y + errorTag.Error/2;
            double bottom = top - errorTag.Error;
            int pixY = (int) yScale.Transform(isOverrideOrdinal, pointIndex, y);
            return new TransformedErrorTag(
                pixY - (int)yScale.Transform(isOverrideOrdinal, pointIndex, top),
                pixY - (int)yScale.Transform(isOverrideOrdinal, pointIndex, bottom),
                (int)(xScale.Transform(1)-xScale.Transform(0)));
        }

        protected override void DrawTag(Graphics g, object tag)
        {
            var errorTag = tag as TransformedErrorTag;
            if (errorTag == null)
                return;

            // Draw the bar
            if (errorTag.PixWidth >= 3)
            {
                // Draw whiskers
                // Line
                g.DrawLine(ErrorPen, 0, errorTag.PixTop, 0, errorTag.PixBottom);
                if (errorTag.PixWidth >= PIX_TERM_WIDTH)
                {
                    // Ends
                    float pixLeft = -(float) Math.Round(PIX_TERM_WIDTH/2);
                    float pixRight = pixLeft + PIX_TERM_WIDTH - 1;
                    g.DrawLine(ErrorPen, pixLeft, errorTag.PixTop, pixRight, errorTag.PixTop);
                    g.DrawLine(ErrorPen, pixLeft, errorTag.PixBottom, pixRight, errorTag.PixBottom);
                }
            }
        }
    }

    internal class TransformedErrorTag
    {
        public TransformedErrorTag(int pixTop, int pixBottom, int pixWidth)
        {
            PixTop = pixTop;
            PixBottom = pixBottom;
            PixWidth = pixWidth;
        }

        public int PixTop { get; private set; }
        public int PixBottom { get; private set; }
        public int PixWidth { get; private set; }
    }

    internal class LineErrorBarDataHandler : CurveDataHandler
    {
        protected override DataFrameBuilder AddColumns(DataFrameBuilder dataFrameBuilder)
        {
            dataFrameBuilder = AddColumnForAxis(dataFrameBuilder, dataFrameBuilder.BaseAxis);
            dataFrameBuilder = AddColumnForAxis(dataFrameBuilder, dataFrameBuilder.ValueAxis);
            var points = dataFrameBuilder.Points;
            var errors = new double?[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                var point = points[i];
                if (point.IsMissing)
                {
                    continue;
                }
                var errorTag = point.Tag as ErrorTag;
                if (errorTag != null)
                {
                    errors[i] = errorTag.Error;
                }
            }
            dataFrameBuilder = dataFrameBuilder.AddColumn(new DataColumn<double?>(@"StdErr", errors));
            return dataFrameBuilder;
        }
    }
}
