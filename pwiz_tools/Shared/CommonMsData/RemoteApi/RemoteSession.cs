/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using System.Net.Http;
using System.Threading;
using pwiz.Common.SystemUtil;
using pwiz.CommonMsData.RemoteApi.Ardia;
using pwiz.CommonMsData.RemoteApi.Unifi;
using pwiz.CommonMsData.RemoteApi.WatersConnect;

namespace pwiz.CommonMsData.RemoteApi
{
    public interface IRemoteAccountUserInteraction
    {
        /// <summary>
        /// Called by a RemoteSession when a user needs to interactively login to their account.
        /// The implementation must decide what to do for the given type of RemoteAccount.
        /// </summary>
        /// <param name="account">The account before user is logged in.</param>
        /// <returns>A functor used to create an authenticated HttpClient.</returns>
        public Func<HttpClient> UserLogin(RemoteAccount account);
    }

    public abstract class RemoteSession : IDisposable
    {
        private readonly object _lock = new object();
        private readonly IDictionary<RequestKey, RemoteResponse> _responses
            = new Dictionary<RequestKey, RemoteResponse>();
        private readonly HashSet<RequestKey> _fetchRequests
            = new HashSet<RequestKey>();
        protected readonly CancellationTokenSource _cancellationTokenSource 
            = new CancellationTokenSource();

        protected RemoteSession(RemoteAccount account)
        {
            Account = account;
        }
        protected void FireContentsAvailable()
        {
            var contentsAvailable = ContentsAvailable;
            if (contentsAvailable != null)
            {
                contentsAvailable();
            }
        }

        public event Action ContentsAvailable;
        
        public virtual void Dispose()
        {
            _cancellationTokenSource.Cancel();
        }

        public RemoteAccount Account { get; private set; }
        public static IRemoteAccountUserInteraction RemoteAccountUserInteraction { get; set; }
        public abstract IEnumerable<RemoteItem> ListContents(MsDataFileUri parentUrl);

        public abstract bool AsyncFetchContents(RemoteUrl remoteUrl, out RemoteServerException remoteException);

        protected bool AsyncFetch<T>(Uri requestUri, Func<Uri, T> fetcher, out RemoteServerException remoteException)
        {
            if (null == requestUri)
            {
                remoteException = null;
                return true;
            }
            lock (_lock)
            {
                RemoteResponse response;
                var key = new RequestKey(typeof(T), requestUri);
                if (_responses.TryGetValue(key, out response))
                {
                    remoteException = response.Exception;
                    return true;
                }
                remoteException = null;
                if (!_fetchRequests.Add(key))
                {
                    return false;
                }
            }
            CommonActionUtil.RunAsync(()=>FetchAndStore(requestUri, fetcher));
            return false;
        }

        protected void RetryFetch<T>(Uri requestUri, Func<Uri, T> fetcher)
        {
            if (requestUri == null)
            {
                return;
            }
            var key = new RequestKey(typeof(T), requestUri);
            lock (_lock)
            {
                
                if (_fetchRequests.Contains(key))
                {
                    if (_responses.TryGetValue(key, out var response))
                    {
                        if (response.Exception == null)
                        {
                            // Already have the response data.
                            return;
                        }
                        else
                            _responses.Remove(key);
                    }
                    else 
                        return; // The request is already in progress.
                }
                _fetchRequests.Remove(key);
                AsyncFetch(requestUri, fetcher, out _);
            }

        }

        private void FetchAndStore<T>(Uri requestUri, Func<Uri, T> fetcher)
        {
            var key = new RequestKey(typeof(T), requestUri);
            try
            {
                var data = fetcher(requestUri);
                lock (_lock)
                {
                    _responses[key] = new RemoteResponse(data, null);
                }
            }
            catch (Exception exception)
            {
                RemoteServerException remoteException = exception as RemoteServerException;
                // ReSharper disable once ConvertIfStatementToNullCoalescingExpression
                if (null == remoteException)
                {
                    remoteException = new RemoteServerException(
                        CommonMsDataResources.RemoteSession_FetchContents_There_was_an_error_communicating_with_the_server__
                        + exception.Message, exception);
                }
                lock (_lock)
                {
                    _responses[key] = new RemoteResponse(null, remoteException);
                }
            }
            FireContentsAvailable();
        }

        protected bool TryGetData<T>(Uri requestUri, out T data)
        {
            if (requestUri == null)
            {
                data = default(T);
                return false;
            }
            lock (_lock)
            {
                RemoteResponse remoteResponse;
                if (!_responses.TryGetValue(new RequestKey(typeof(T), requestUri), out remoteResponse))
                {
                    data = default(T);
                    return false;
                }
                if (remoteResponse.Exception != null)
                {
                    data = default(T);
                    return false;
                }
                data = (T) remoteResponse.Data;
                return true;
            }
        }

        public static RemoteSession CreateSession(RemoteAccount remoteAccount)
        {
            return remoteAccount switch
            {
                UnifiAccount unifiAccount => new UnifiSession(unifiAccount),
                WatersConnectAccount wcAccount => new WatersConnectSession(wcAccount),
                ArdiaAccount ardiaAccount => new ArdiaSession(ardiaAccount),
                _ => throw new ArgumentException(nameof(remoteAccount))
            };
        }

        public abstract void RetryFetchContents(RemoteUrl remoteUrl);

        protected class RemoteResponse
        {
            public RemoteResponse(object data, RemoteServerException exception)
            {
                Data = data;
                Exception = exception;
            }

            public object Data { get; private set; }
            public RemoteServerException Exception { get; private set; }
        }

        protected struct RequestKey
        {
            public RequestKey(Type type, Uri uri) : this()
            {
                Type = type;
                Uri = uri;
            }

            public Type Type { get; private set; }
            public Uri Uri { get; private set; }
        }
    }

    public class ProgressableStreamContents
    {

    }
}
