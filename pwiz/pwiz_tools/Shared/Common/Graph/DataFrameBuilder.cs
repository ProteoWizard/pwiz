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
using ZedGraph;
using pwiz.MSGraph;

namespace pwiz.Common.Graph
{
    /// <summary>
    /// Holds state while a <see cref="ICurveDataHandler"/> is building 
    /// up the <see cref="DataFrame" /> from the data found in a 
    /// <see cref="CurveItem"/>.
    /// </summary>
    public class DataFrameBuilder
    {
        public DataFrameBuilder(GraphPane graphPane, CurveItem curveItem)
        {
            GraphPane = graphPane;
            CurveItem = curveItem;
            var msPointList = curveItem.Points as MSPointList;
            if (msPointList != null)
            {
                Points = msPointList.FullList;
            }
            else
            {
                Points = curveItem.Points;
            }
            XAxis = curveItem.GetXAxis(graphPane);
            YAxis = curveItem.GetYAxis(graphPane);
            BaseAxis = curveItem.BaseAxis(graphPane);
            ValueAxis = curveItem.ValueAxis(graphPane);
        }
        private DataFrameBuilder(DataFrameBuilder dataFrameBuilder)
        {
            GraphPane = dataFrameBuilder.GraphPane;
            CurveItem = dataFrameBuilder.CurveItem;
            Points = dataFrameBuilder.Points;
            XAxis = dataFrameBuilder.XAxis;
            YAxis = dataFrameBuilder.YAxis;
            BaseAxis = dataFrameBuilder.BaseAxis;
            ValueAxis = dataFrameBuilder.ValueAxis;
            DataFrame = dataFrameBuilder.DataFrame;
        }

        public GraphPane GraphPane { get; private set; }
        public CurveItem CurveItem { get; private set; }
        public IPointList Points { get; private set; }
        public DataFrameBuilder SetPoints(IPointList newPoints)
        {
            return new DataFrameBuilder(this) {Points = newPoints};
        }
        public Axis XAxis { get; private set; }
        public Axis YAxis { get; private set; }
        public Axis BaseAxis { get; private set; }
        public Axis ValueAxis { get; private set; }
        public DataFrame DataFrame { get; private set; }
        public DataFrameBuilder SetDataFrame(DataFrame dataFrame)
        {
            return new DataFrameBuilder(this) { DataFrame = dataFrame};
        }
        public DataFrameBuilder AddColumn(IColumnGroup column)
        {
            return SetDataFrame(DataFrame.AddColumn(column));
        }
    }
}