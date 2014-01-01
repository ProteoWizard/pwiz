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
using ZedGraph;
using pwiz.Common.Collections;

namespace pwiz.Common.Graph
{
    public class GraphPaneData
    {
        public static GraphPaneData GetGraphPaneData(GraphPane graphPane)
        {
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
    }
}
