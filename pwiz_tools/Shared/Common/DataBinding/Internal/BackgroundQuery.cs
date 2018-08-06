/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using System.Threading;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding.Internal
{
    internal class BackgroundQuery : AbstractQuery
    {
        private CancellationToken _rootCancellationToken;
        private CancellationTokenSource _cancellationTokenSource;

        public BackgroundQuery(RowSourceWrapper rowSource, IQueryRequest queryRequest)
        {
            RowSource = rowSource;
            QueryRequest = queryRequest;
            _rootCancellationToken = QueryRequest.CancellationToken;
        }

        public RowSourceWrapper RowSource { get; private set; }
        public IQueryRequest QueryRequest { get; private set; }

        public void Start()
        {
            lock (this)
            {
                if (null != _cancellationTokenSource)
                {
                    throw new InvalidOperationException();
                }
                RowSource.RowSourceChanged += RowSourceChanged;
                _rootCancellationToken.Register(() => RowSource.RowSourceChanged -= RowSourceChanged);
                EnsureRunning();
            }
        }

        private void RowSourceChanged()
        {
            Reset();
        }

        private void Reset()
        {
            lock (this)
            {
                Stop();
                EnsureRunning();
            }
        }
        public void Stop()
        {
            lock (this)
            {
                if (null != _cancellationTokenSource)
                {
                    _cancellationTokenSource.Cancel();
                    _cancellationTokenSource = null;
                }
            }
        }
        public void EnsureRunning()
        {
            lock (this)
            {
                if (_cancellationTokenSource == null)
                {
                    _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(new[]{QueryRequest.CancellationToken});
                    var token = _cancellationTokenSource.Token;
                    var thread = new Thread(() => Run(token)) {Name = "Background Query"};
                    thread.Start();
                }
            }
        }
        private void Run(CancellationToken cancellationToken)
        {
            LocalizationHelper.InitThread();
            try
            {
                using (QueryRequest.QueryLock.GetReadLock())
                {
                    var queryResults = QueryResults.Empty
                        .SetParameters(QueryRequest.QueryParameters)
                        .SetSourceRows(RowSource.ListRowItems());
                    queryResults = RunAll(cancellationToken, queryResults);
                    QueryRequest.SetFinalQueryResults(queryResults);
                }
            }
            catch (OperationCanceledException)
            {

            }
            catch (Exception e)
            {
                if (!(e.InnerException is OperationCanceledException))
                {
                    QueryRequest.OnUnhandledException(e);
                }
            }
        }
    }
}
