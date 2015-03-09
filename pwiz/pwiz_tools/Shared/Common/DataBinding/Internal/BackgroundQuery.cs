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
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding.Internal
{
    internal class BackgroundQuery : AbstractQuery
    {
        private CancellationToken _rootCancellationToken;
        private CancellationTokenSource _cancellationTokenSource;

        public BackgroundQuery(IRowSourceWrapper rowSource, TaskScheduler backgroundTaskScheduler, IQueryRequest queryRequest)
        {
            RowSource = rowSource;
            BackgroundTaskScheduler = backgroundTaskScheduler;
            QueryRequest = queryRequest;
            _rootCancellationToken = QueryRequest.CancellationToken;
        }

        public IRowSourceWrapper RowSource { get; private set; }
        public IQueryRequest QueryRequest { get; private set; }
        public TaskScheduler BackgroundTaskScheduler { get; private set; }
        public IList<RowItem> SourceRowItems { get; private set; }

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
                SourceRowItems = ImmutableList.ValueOf(RowSource.ListRowItems());
                EnsureRunning();
            }
        }

        private void RowSourceChanged(object sender, ListChangedEventArgs eventArgs)
        {
            Reset();
        }

        private void Reset()
        {
            lock (this)
            {
                Stop();
                SourceRowItems = ImmutableList.ValueOf(RowSource.ListRowItems());
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
                    Task.Factory.StartNew(() => Run(token), token);
                }
            }
        }
        private void Run(CancellationToken cancellationToken)
        {
            LocalizationHelper.InitThread();
            try
            {
                var queryResults = QueryRequest.InitialQueryResults
                    .SetSourceRows(SourceRowItems);
                queryResults = RunAll(new Pivoter.TickCounter(cancellationToken), queryResults);
                QueryRequest.SetFinalQueryResults(queryResults);
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
