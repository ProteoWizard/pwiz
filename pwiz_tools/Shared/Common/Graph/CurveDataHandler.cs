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
using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.SystemUtil;
using ZedGraph;

namespace pwiz.Common.Graph
{
    /// <summary>
    /// Default implementation of <see cref="ICurveDataHandler"/> which tries 
    /// to deal gracefully with curves of all types.
    /// One can override the way that a curve's data is rendered into text on
    /// the clipboard by placing a <see cref="CurveDataHandlerAttribute"/>
    /// over the class definition, or by modifying the logic in
    /// <see cref="CurveDataHandlers.FindCurveHandler"/>.
    /// </summary>
    public class CurveDataHandler : ICurveDataHandler
    {
        #region ICurveDataHandler members
        public virtual DataFrameBuilder CreateDataFrame(DataFrameBuilder dataFrameBuilder)
        {
            dataFrameBuilder = dataFrameBuilder.SetDataFrame(new DataFrame(dataFrameBuilder.CurveItem.Label.Text, dataFrameBuilder.Points.Count));
            dataFrameBuilder = AddColumns(dataFrameBuilder);
            return dataFrameBuilder;
        }

        /// <summary>
        /// Filters the <see cref="DataFrameBuilder.Points"/> down to just those that are 
        /// visible if the graph has been zoomed in.
        /// This method only filters based on the value on the Base Axis 
        /// (i.e. the X-Axis for most curves).
        /// </summary>
        public virtual DataFrameBuilder FilterToZoomedRange(DataFrameBuilder dataFrameBuilder)
        {
            var graphPane = dataFrameBuilder.GraphPane;
            var pointList = dataFrameBuilder.Points;
            var baseAxis = dataFrameBuilder.CurveItem.BaseAxis(graphPane);
            if (baseAxis.Scale.IsAnyOrdinal)
            {
                // If the Scale is either Ordinal or Text, then don't
                // filter the PointList, because the point values 
                // are derived from their position in the list.
                return dataFrameBuilder;
            }
            Func<PointPair, double> valueOfPointFunc = ValueOfPointFuncForAxis(dataFrameBuilder, baseAxis);
            if (null == valueOfPointFunc)
            {
                return dataFrameBuilder;
            }

            double min = baseAxis.Scale.Min;
            double max = baseAxis.Scale.Max;
            var pointPairList = new PointPairList();
            for (int i = 0; i < pointList.Count; i++)
            {
                var pointPair = pointList[i];
                double value = valueOfPointFunc(pointPair);
                if (value < min || value > max)
                {
                    continue;
                }
                pointPairList.Add(pointPair);
            }
            return dataFrameBuilder.SetPoints(pointPairList);
        }
        #endregion

        /// <summary>
        /// Adds columns to the <see cref="DataFrameBuilder.DataFrame"/> for all
        /// of the data in the <see cref="DataFrameBuilder.Points"/>.
        /// </summary>
        protected virtual DataFrameBuilder AddColumns(DataFrameBuilder dataFrameBuilder)
        {
            dataFrameBuilder = AddColumnForAxis(dataFrameBuilder, dataFrameBuilder.BaseAxis);
            dataFrameBuilder = AddColumnForAxis(dataFrameBuilder, dataFrameBuilder.ValueAxis);
            if (HasZAxis(dataFrameBuilder))
            {
                dataFrameBuilder = dataFrameBuilder.AddColumn(GetZAxisColumn(dataFrameBuilder.Points));
            }
            dataFrameBuilder = dataFrameBuilder.AddColumn(GetTagColumn(dataFrameBuilder.Points));
            return dataFrameBuilder;
        }


        /// <summary>
        /// Adds the data for the <paramref name="axis"/> to the <see cref="DataFrameBuilder.DataFrame"/>.
        /// If <paramref name="axis"/> is the <see cref="CurveItem.BaseAxis"/> then the column
        /// is added as the <see cref="DataFrame.RowHeader"/>, otherwise it is added
        /// to <see cref="DataFrame.ColumnGroups"/>.
        /// The X-Axis is usually the base axis, but for bar graphs that display horizontally, 
        /// the Y-Axis is the base axis.
        /// </summary>
        protected virtual DataFrameBuilder AddColumnForAxis(DataFrameBuilder dataFrameBuilder, Axis axis)
        {
            var column = GetColumnForAxis(dataFrameBuilder, axis);
            if (column == null)
            {
                return dataFrameBuilder;
            }
            var dataFrame = dataFrameBuilder.DataFrame;
            if (dataFrame.RowHeader == null && ReferenceEquals(axis, dataFrameBuilder.BaseAxis))
            {
                dataFrame = dataFrame.SetRowHeaders(column);
            }
            else
            {
                dataFrame = dataFrame.AddColumn(column);
            }
            return dataFrameBuilder.SetDataFrame(dataFrame);
        }

        /// <summary>
        /// Returns a <see cref="DataColumn"/> containing the values on the <paramref name="axis"/>.
        /// If <see cref="Scale.IsText"/> is true for the <see cref="Axis.Scale"/>,
        /// then the DataColumn will contain string values.
        /// </summary>
        protected virtual DataColumn GetColumnForAxis(DataFrameBuilder dataFrameBuilder, Axis axis)
        {
            if (axis == null)
            {
                return null;
            }
            if (axis.Scale.IsText)
            {
                var textValues = new string[dataFrameBuilder.Points.Count];
                Array.Copy(axis.Scale.TextLabels, 0, textValues, 0, Math.Min(textValues.Length, axis.Scale.TextLabels.Length));
                return new DataColumn<string>(axis.Title.Text, textValues);
            }
            if (axis.Scale.IsOrdinal)
            {
                return new DataColumn<int>(axis.Title.Text, Enumerable.Range(0, dataFrameBuilder.Points.Count));
            }
            var values = new double[dataFrameBuilder.Points.Count];
            var valueOfPoint = ValueOfPointFuncForAxis(dataFrameBuilder, axis);
            if (valueOfPoint != null)
            {
                for (int i = 0; i < dataFrameBuilder.Points.Count; i++)
                {
                    values[i] = valueOfPoint(dataFrameBuilder.Points[i]);
                }
            }
            if (values.Any(value=>PointPairBase.Missing == value))
            {
                var valuesWithNull = values.Select(value => PointPairBase.Missing == value ? (double?) null : value);
                return new DataColumn<double?>(axis.Title.Text, valuesWithNull);
            }
            return new DataColumn<double>(axis.Title.Text, values);
        }

        /// <summary>
        /// Returns true if any of the values in <see cref="PointPair.Z"/> are nonzero.
        /// </summary>
        protected bool HasZAxis(DataFrameBuilder dataFrameBuilder)
        {
            return AsEnumerable(dataFrameBuilder.Points).Any(point => 0 != point.Z);
        }

        /// <summary>
        /// Returns a column with the data values in <see cref="PointPair.Z"/>.
        /// </summary>
        protected virtual DataColumn GetZAxisColumn(IPointList points)
        {
            var values = new double?[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                var point = points[i];
                if (point.IsMissing)
                {
                    values[i] = null;
                }
                else
                {
                    values[i] = point.Z;
                }
            }
            if (values.Contains(null))
            {
                return new DataColumn<double?>(@"Z", values);
            }
            return new DataColumn<double>(@"Z", values.Cast<double>());
        }
        
        /// <summary>
        /// If any of the <see cref="PointPair.Tag"/> properties on the point is a string,
        /// then this method returns a <see cref="DataColumn"/> containing those string values.
        /// If none of the tags are strings, then this method returns null.
        /// 
        /// A <see cref="ZedGraphControl"/> will show the tag as a tooltip if it is
        /// a string and <see cref="ZedGraphControl.IsShowPointValues"/> is true.
        /// </summary>
        protected virtual DataColumn GetTagColumn(IPointList points)
        {
            var values = AsEnumerable(points).Select(point => point.Tag as string).ToArray();
            if (values.All(value=>null == value))
            {
                return null;
            }
            return new DataColumn<string>(@"Label", values);
        }

        /// <summary>
        /// Determines whether <paramref name="axis"/> is the X-Axis or the Y-Axis,
        /// and returns a function that returns either <see cref="PointPair.X"/> or 
        /// <see cref="PointPair.Y"/>.
        /// Returns null if the axis is neither.
        /// </summary>
        protected virtual Func<PointPair, double> ValueOfPointFuncForAxis(DataFrameBuilder dataFrameBuilder, Axis axis)
        {
            if (axis is XAxis || axis is X2Axis || ReferenceEquals(axis, dataFrameBuilder.XAxis))
            {
                return point => point.X;
            }
            if (axis is YAxis || axis is Y2Axis || ReferenceEquals(axis, dataFrameBuilder.YAxis))
            {
                return point => point.Y;
            }
            Messages.WriteAsyncDebugMessage(@"Could not determine type of axis {0}", axis);
            return null;
        }

        public static IEnumerable<PointPair> AsEnumerable(IPointList pointList)
        {
            for (int i = 0; i < pointList.Count; i++)
            {
                yield return pointList[i];
            }
        }
    }
}
