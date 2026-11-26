/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using pwiz.Common.DataBinding.Layout;

namespace pwiz.Common.DataBinding.Internal
{
    /// <summary>
    /// Runs a report's query to completion on the calling thread: the sorts, filters, pivots and
    /// clustering a grid's BindingListSource applies, for exporting a report where there is no grid.
    /// </summary>
    public class SynchronousQuery : AbstractQuery
    {
        public static ReportResults GetReportResults(CancellationToken cancellationToken, ViewInfo viewInfo,
            ViewLayout layout, IRowSource rowSource)
        {
            var parameters = QueryParameters.Empty.ChangeViewInfo(viewInfo);
            if (layout != null)
            {
                parameters = parameters.ChangeTransformStack(new TransformStack(layout.RowTransforms, 0))
                    .ChangeIsClusteringRequested(layout.ClusterSpec);
            }
            var queryResults = QueryResults.Empty
                .SetParameters(parameters)
                .SetSourceRows(new RowSourceWrapper(rowSource).ListRowItems());
            queryResults = new SynchronousQuery().RunAll(cancellationToken, queryResults);
            return queryResults.TransformResults.PivotedRows;
        }
    }
}
