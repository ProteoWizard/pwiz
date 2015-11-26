/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using System.ComponentModel;
using System.Threading;

namespace pwiz.Common.DataBinding.Internal
{
    internal class ForegroundQuery : AbstractQuery
    {
        private CancellationToken _cancellationToken;

        public ForegroundQuery(IRowSourceWrapper rowSource, IQueryRequest queryRequest)
        {
            RowSource = rowSource;
            QueryRequest = queryRequest;
        }

        public IRowSourceWrapper RowSource { get; private set; }
        public IQueryRequest QueryRequest { get; private set; }

        public void Start()
        {
            _cancellationToken = QueryRequest.CancellationToken;
            RowSource.RowSourceChanged += RowSourceChanged;
            _cancellationToken.Register(() => RowSource.RowSourceChanged -= RowSourceChanged);
            Run();
        }

        private void RowSourceChanged(object sender, ListChangedEventArgs listChangedEventArgs)
        {
            Run();
        }

        private void Run()
        {
            try
            {
                var tickCounter = new Pivoter.TickCounter(_cancellationToken, 10000000);
                var queryResults = QueryResults.Empty
                    .SetParameters(QueryRequest.QueryParameters)
                    .SetSourceRows(RowSource.ListRowItems());

                queryResults = RunAll(tickCounter, queryResults);
                QueryRequest.SetFinalQueryResults(queryResults);
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
            catch (Exception ex)
            {
                QueryRequest.OnUnhandledException(ex);
            }
        }
    }
}
