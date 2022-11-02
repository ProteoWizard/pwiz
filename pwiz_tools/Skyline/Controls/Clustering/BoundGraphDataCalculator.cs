/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using System.Threading;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Controls.Graphs;
using ZedGraph;

namespace pwiz.Skyline.Controls.Clustering
{
    /// <summary>
    /// GraphDataCalculator which gets its input data from a DataSchema.
    /// When working with data from a DataSchema on a background thread, you must always
    /// make sure to have called <see cref="QueryLock.GetReadLock"/>.
    /// </summary>
    public abstract class BoundGraphDataCalculator<TInput, TResults> : GraphDataCalculator<TInput, TResults> where TInput : DataSchemaInput
    {
        protected BoundGraphDataCalculator(CancellationToken parentCancellationToken, ZedGraphControl zedGraphControl) :
            base(parentCancellationToken, zedGraphControl, zedGraphControl.GraphPane)
        {
        }

        protected sealed override TResults CalculateResults(TInput input, CancellationToken cancellationToken)
        {
            using (input.QueryLock.GetReadLock())
            {
                var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, input.QueryLock.CancellationToken);
                {
                    return CalculateDataBoundResults(input, cancellationTokenSource.Token);
                }
            }
        }

        protected abstract TResults CalculateDataBoundResults(TInput input, CancellationToken cancellationToken);
    }

    public class DataSchemaInput
    {
        public DataSchemaInput(DataSchema dataSchema)
        {
            DataSchema = dataSchema;
        }

        public DataSchema DataSchema { get; }

        public QueryLock QueryLock
        {
            get { return DataSchema.QueryLock; }
        }

        public DataSchemaLocalizer DataSchemaLocalizer
        {
            get { return DataSchema.DataSchemaLocalizer; }
        }
    }
}
