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

        /// <summary>
        /// Make an HTTP GET request to a remote API.
        ///
        /// This handles caching, networking, and async. First, it checks whether a response already exists for this URI + response type.
        /// If so, a C# object of type <list type="T"></list> is returned from the cache. If not, an <see cref="Action"/> starts to 
        /// make the HTTP request asynchronously and the response is stored in the cache of responses.
        ///
        /// <see cref="ContentsAvailable"/> is called if an async request was made to fetch the contents. 
        /// </summary>
        /// <typeparam name="T">Type created when un-marshaling the response.</typeparam>
        /// <param name="requestUri">URI of the remote API to call</param>
        /// <param name="fetcher">Function that makes an HTTP request and handles the response. The fetcher configures the request
        ///                       (verb, auth headers / cookies) and handles the response (response code, parsing the body, etc).
        ///                       The fetcher also un-marshals the response into strongly typed objects.</param>
        /// <param name="remoteException">Exception that occurs processing the response. For example: authentication or un-marshaling issues.</param>
        /// <returns>True if the response exists in the cache and false otherwise. Remote requests to previously unfetched URLs will return false.</returns>
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

        /// <summary>
        /// Call a remote API, cache the results, and fire the <see cref="ContentsAvailable"/> event.
        /// </summary>
        /// <typeparam name="T">Type created when un-marshaling the response.</typeparam>
        /// <param name="requestUri">URI of the remote API to call</param>
        /// <param name="fetcher">Function that makes the request and handles the response. Often, the response body is JSON
        ///                       that is un-marshaled into one or more strongly typed objects. </param>
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

        public bool HasResultsFor<T>(Uri remoteUrl)
        {
            if (remoteUrl == null)
            {
                return false;
            }
            else
            {
                lock (_lock)
                {
                    var requestKey = new RequestKey(typeof(T), remoteUrl);
                    return _responses.ContainsKey(requestKey);
                }
            }
        }

        public bool ClearResultsFor<T>(Uri remoteUrl)
        {
            if (remoteUrl == null)
            {
                return false;
            }
            else
            {
                lock (_lock)
                {
                    var requestKey = new RequestKey(typeof(T), remoteUrl);

                    _fetchRequests.Remove(requestKey);
                    return _responses.Remove(requestKey);
                }
            }
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

        private struct RequestKey : IEquatable<RequestKey>
        {
            public RequestKey(Type type, Uri uri) : this()
            {
                Type = type;
                Uri = uri;
            }

            public Type Type { get; private set; }
            public Uri Uri { get; private set; }


            public bool Equals(RequestKey other)
            {
                return Type == other.Type && Equals(Uri, other.Uri);
            }

            public override bool Equals(object obj)
            {
                return obj is RequestKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((Type != null ? Type.GetHashCode() : 0) * 397) ^ (Uri != null ? Uri.GetHashCode() : 0);
                }
            }
        }
    }

    public class ProgressableStreamContents
    {

    }
}
