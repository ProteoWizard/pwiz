/* 
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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

using System.Collections.Generic;
using System.Linq;
using ZedGraph;
using pwiz.Common.Collections;
using pwiz.MSGraph;

namespace pwiz.Common.Graph
{
    public class GraphPaneData
    {
        public static GraphPaneData GetGraphPaneData(GraphPane graphPane)
        {
            // Check if this is a HeatMapGraphPane with custom clipboard data
            var customData = TryGetHeatMapData(graphPane);
            if (customData != null)
            {
                return customData;
            }

            var dataFrames = new List<DataFrame>();
            var rowHeaderToListIndex = new Dictionary<IColumnGroup, int>();
            foreach (var curveItem in graphPane.CurveList)
            {
                var curveHandler = CurveDataHandlers.FindCurveHandler(curveItem);
                if (curveHandler == null)
                {
                    continue;
                }
                var curveData = new DataFrameBuilder(graphPane, curveItem);
                curveData = curveHandler.FilterToZoomedRange(curveData);
                curveData = curveHandler.CreateDataFrame(curveData);
                var dataFrame = curveData.DataFrame;
                if (dataFrame == null)
                {
                    continue;
                }
                var rowHeader = dataFrame.RowHeader;
                int existingIndex;
                if (rowHeader != null && rowHeaderToListIndex.TryGetValue(rowHeader, out existingIndex))
                {
                    DataFrame previousFrame = dataFrames[existingIndex];
                    DataFrame mergedFrame;
                    if (null == previousFrame.Title)
                    {
                        mergedFrame = previousFrame.AddColumn(dataFrame.RemoveRowHeader());
                    }
                    else
                    {
                        mergedFrame = new DataFrame(null, rowHeader)
                            .AddColumn(previousFrame.RemoveRowHeader())
                            .AddColumn(dataFrame.RemoveRowHeader());
                    }
                    dataFrames[existingIndex] = mergedFrame;
                }
                else
                {
                    if (rowHeader != null)
                    {
                        rowHeaderToListIndex.Add(rowHeader, dataFrames.Count);
                    }
                    dataFrames.Add(dataFrame);
                }
            }
            if (dataFrames.Count == 0)
            {
                return null;
            }
            return new GraphPaneData(graphPane.Title.Text, dataFrames);
        }

        public GraphPaneData(string title, IEnumerable<DataFrame> dataFrames)
        {
            Title = title;
            DataFrames = ImmutableList.ValueOf(dataFrames);
        }

        public string Title { get; private set; }
        public IList<DataFrame> DataFrames { get; private set; }

        /// <summary>
        /// Check if graphPane is a HeatMapGraphPane and extract its data directly.
        /// </summary>
        private static GraphPaneData TryGetHeatMapData(GraphPane graphPane)
        {
            var heatMapPane = graphPane as HeatMapGraphPane;
            if (heatMapPane == null)
            {
                return null;
            }

            var result = heatMapPane.GetHeatMapDataForClipboard();
            if (result == null)
            {
                return null;
            }

            var xAxisTitle = result.Item1;
            var yAxisTitle = result.Item2;
            var points = result.Item3.ToList();

            if (points.Count == 0)
            {
                return null;
            }

            // Create DataFrame with 3 columns: m/z, ion mobility, intensity
            var mzColumn = new DataColumn<double>(xAxisTitle ?? @"m/z", points.Select(p => (double)p.X).ToList());
            var ionMobilityColumn = new DataColumn<double>(yAxisTitle ?? @"Ion Mobility", points.Select(p => (double)p.Y).ToList());
            var intensityColumn = new DataColumn<double>(@"Intensity", points.Select(p => (double)p.Z).ToList());

            var dataFrame = new DataFrame(null, points.Count)
                .SetRowHeaders(mzColumn)
                .AddColumn(ionMobilityColumn)
                .AddColumn(intensityColumn);

            return new GraphPaneData(graphPane.Title?.Text, new[] { dataFrame });
        }
    }
}
