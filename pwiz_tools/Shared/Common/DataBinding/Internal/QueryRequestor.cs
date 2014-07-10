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
using System.Collections;
using System.Threading;
using System.Threading.Tasks;

namespace pwiz.Common.DataBinding.Internal
{
    internal class QueryRequestor : IDisposable
    {
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly BindingListView _bindingListView;
        private Request _request;
        private QueryParameters _queryParameters;
        private IRowSourceWrapper _rowSourceWrapper;
        public QueryRequestor(BindingListView bindingListView)
        {
            _bindingListView = bindingListView;
            // ReSharper disable once PossiblyMistakenUseOfParamsMethod
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(bindingListView.CancellationToken);
            _queryParameters = QueryParameters.Empty;
            _rowSourceWrapper = RowSourceWrapper.Empty;
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
        public IEnumerable RowSource
        {
            get { return _rowSourceWrapper.WrappedRowSource; }
            set
            {
                if (ReferenceEquals(value, RowSource))
                {
                    return;
                }
                _rowSourceWrapper = WrapRowSource(value);
                Requery();
            }
        }

        public void SetRowsAndParameters(IEnumerable rowSource, QueryParameters queryParameters)
        {
            if (ReferenceEquals(RowSource, rowSource) && Equals(QueryParameters, queryParameters))
            {
                return;
            }
            _rowSourceWrapper = WrapRowSource(rowSource);
            _queryParameters = queryParameters;
            Requery();
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
            _cancellationTokenSource.Cancel();
            _queryParameters = null;
            _rowSourceWrapper = null;
            _request = null;
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
            _request = new Request(this);
            _rowSourceWrapper.StartQuery(_request);
        }

        private IRowSourceWrapper WrapRowSource(IEnumerable items)
        {
            if (null == EventTaskScheduler)
            {
                return new RowSourceWrapper(items ?? new object[0]);
            }
            return RowSourceWrappers.Wrap(items);
        }

        class Request : IQueryRequest, IDisposable
        {
            private readonly CancellationTokenSource _cancellationTokenSource;
            private readonly QueryRequestor _queryRequestor;
            public Request(QueryRequestor queryRequestor)
            {
                _queryRequestor = queryRequestor;
                QueryParameters = _queryRequestor.QueryParameters;
                // ReSharper disable PossiblyMistakenUseOfParamsMethod
                _cancellationTokenSource =
                    CancellationTokenSource.CreateLinkedTokenSource(queryRequestor._cancellationTokenSource.Token);
                // ReSharper restore PossiblyMistakenUseOfParamsMethod
            }

            public TaskScheduler EventTaskScheduler { get { return _queryRequestor._bindingListView.EventTaskScheduler; } }
            public CancellationToken CancellationToken { get { return _cancellationTokenSource.Token; } }
            public QueryParameters QueryParameters { get; private set; }
            public QueryResults InitialQueryResults
            {
                get { return QueryResults.Empty.SetParameters(QueryParameters); }
            }

            public void SetFinalQueryResults(QueryResults newResults)
            {
                var action = new Action(() =>
                    {
                        try
                        {
                            LiveQueryResults = _queryRequestor._rowSourceWrapper.MakeLive(newResults);
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
