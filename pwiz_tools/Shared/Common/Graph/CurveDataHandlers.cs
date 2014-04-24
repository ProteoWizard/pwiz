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
using System.Linq;
using ZedGraph;

namespace pwiz.Common.Graph
{
    /// <summary>
    /// Attribute which can be placed on a <see cref="CurveItem"/> class
    /// to indicate which <see cref="ICurveDataHandler"/> to use
    /// for curves of that type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class CurveDataHandlerAttribute : Attribute
    {
        public CurveDataHandlerAttribute(Type type)
        {
            Class = type;
        }
        public Type Class { get; set; }
    }

    public static class CurveDataHandlers
    {
        /// <summary>
        /// Returns a new instance of a <see cref="ICurveDataHandler"/> appropriate for the 
        /// <paramref name="curveItem"/>.
        /// </summary>
        public static ICurveDataHandler FindCurveHandler(CurveItem curveItem)
        {
            var handlerClass = curveItem.GetType().GetCustomAttributes(typeof (CurveDataHandlerAttribute), true)
                                        .Cast<CurveDataHandlerAttribute>().Select(attribute=>attribute.Class)
                                        .FirstOrDefault() ??
                               ((curveItem is HiLowBarItem) ? typeof (HiLowBarDataHandler) : typeof (CurveDataHandler));
            var constructorInfo = handlerClass.GetConstructor(new Type[0]);
            if (constructorInfo != null)
                return (ICurveDataHandler) constructorInfo.Invoke(new object[0]);
            return null;
        }
    }
    
    public interface ICurveDataHandler
    {
        /// <summary>
        /// Alters the <see cref="DataFrameBuilder.Points"/> so that, if
        /// the graph has been zoomed in, only points in the visible 
        /// range are included.
        /// </summary>
        DataFrameBuilder FilterToZoomedRange(DataFrameBuilder dataFrameBuilder);
        /// <summary>
        /// Sets the <see cref="DataFrameBuilder.DataFrame"/> to a DataFrame
        /// that has a title and the columns of data from the CurveItem.
        /// </summary>
        DataFrameBuilder CreateDataFrame(DataFrameBuilder dataFrameBuilder);
    }

    /// <summary>
    /// CurveHandler for a <see cref="HiLowBarItem"/>.  
    /// </summary>
    public class HiLowBarDataHandler : CurveDataHandler
    {
        protected override DataFrameBuilder AddColumns(DataFrameBuilder dataFrameBuilder)
        {
            dataFrameBuilder = AddColumnForAxis(dataFrameBuilder, dataFrameBuilder.BaseAxis);
            // Group the "High" and "Low" columns together, and put the title from the Axis on top of them
            var dataFrame = new DataFrame(dataFrameBuilder.ValueAxis.Title.Text, dataFrameBuilder.Points.Count);
            dataFrame = dataFrame.AddColumn(GetColumnForAxis(dataFrameBuilder, dataFrameBuilder.ValueAxis).SetTitle("High")); // Not L10N
            dataFrame = dataFrame.AddColumn(GetZAxisColumn(dataFrameBuilder.Points).SetTitle("Low")); // Not L10N

            dataFrameBuilder = dataFrameBuilder.SetDataFrame(dataFrameBuilder.DataFrame.AddColumn(dataFrame));
            return dataFrameBuilder;
        }
    }
}
