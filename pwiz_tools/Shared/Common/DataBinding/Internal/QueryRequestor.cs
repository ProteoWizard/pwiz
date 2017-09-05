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
using System.Threading.Tasks;

namespace pwiz.Common.DataBinding.Internal
{
    internal class QueryRequestor : IDisposable
    {
        private readonly BindingListView _bindingListView;
        private Request _request;
        private QueryParameters _queryParameters;
        public QueryRequestor(BindingListView bindingListView)
        {
            _bindingListView = bindingListView;
            // ReSharper disable once PossiblyMistakenUseOfParamsMethod
            _queryParameters = QueryParameters.Empty;
        }
        public TaskScheduler EventTaskScheduler { get { return _bindingListView.EventTaskScheduler; } }
        public QueryParameters QueryParameters
        {
            get { return _queryParameters; }
            set
            {
                if (Equals(_queryParameters, value))
                {
                    return;
                }
                _queryParameters = value;
                Requery();
            }
        }
        public IRowSource RowSource
        {
            get { return _bindingListView.RowSource; }
        }

        public QueryResults QueryResults
        {
            get
            {
                return _request == null ? QueryResults.Empty.SetParameters(QueryParameters) : _request.LiveQueryResults;
            }
        }

        public void Dispose()
        {
            _queryParameters = null;
            var request = Interlocked.Exchange(ref _request, null);
            if (request != null)
            {
                request.Dispose();
            }
        }
        public void Requery()
        {
            if (null != _request)
            {
                _request.Dispose();
                _request = null;
            }
            if (null == QueryParameters || null == QueryParameters.ViewInfo)
            {
                return;
            }
            _request = new Request(this, _bindingListView.QueryLock);
            new RowSourceWrapper(RowSource).StartQuery(_request);
        }

        class Request : IQueryRequest, IDisposable
        {
            private readonly CancellationTokenSource _cancellationTokenSource;
            private readonly QueryRequestor _queryRequestor;
            public Request(QueryRequestor queryRequestor, QueryLock queryLock)
            {
                _queryRequestor = queryRequestor;
                QueryParameters = _queryRequestor.QueryParameters;
                // ReSharper disable PossiblyMistakenUseOfParamsMethod
                _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(queryLock.CancellationToken);
                // ReSharper restore PossiblyMistakenUseOfParamsMethod
                QueryLock = queryLock;
            }

            public TaskScheduler EventTaskScheduler { get { return _queryRequestor._bindingListView.EventTaskScheduler; } }
            public CancellationToken CancellationToken { get { return _cancellationTokenSource.Token; } }
            public QueryParameters QueryParameters { get; private set; }
            public QueryResults InitialQueryResults
            {
                get { return QueryResults.Empty.SetParameters(QueryParameters); }
            }

            public QueryLock QueryLock { get; private set; }

            public void SetFinalQueryResults(QueryResults newResults)
            {
                var action = new Action(() =>
                    {
                        try
                        {
                            LiveQueryResults = newResults;
                            _queryRequestor._bindingListView.UpdateResults();
                        }
                        catch (Exception exception)
                        {
                            OnUnhandledException(exception);
                        }
                    });
                if (null == EventTaskScheduler)
                {
                    action.Invoke();
                }
                else
                {
                    Task.Factory.StartNew(action, _cancellationTokenSource.Token, TaskCreationOptions.None, EventTaskScheduler);
                }
            }

            public QueryResults LiveQueryResults { get; private set; }
            public void Dispose()
            {
                _cancellationTokenSource.Cancel();
            }

            public void OnUnhandledException(Exception exception)
            {
                _queryRequestor._bindingListView.OnUnhandledException(exception);
            }
        }
    }
}
