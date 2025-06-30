using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace pwiz.Common.Mock
{
    public class MockHttpMessageHandler : HttpMessageHandler
    {
        private List<RequestMatcher> _matchers = new List<RequestMatcher>();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var matcher = _matchers.FirstOrDefault(matcher => matcher.Match(request));
            if (matcher != null)
            {
                return await Task.FromResult(matcher.GetResponse(request));
            }
            else
            {
                var response = new HttpResponseMessage(HttpStatusCode.NotFound);
                return await Task.FromResult(response);
            }
        }

        public void AddMatcher(RequestMatcher matcher)
        {
            _matchers.Add(matcher);
        }
    }

    public class RequestMatcher
    {
        protected Func<HttpRequestMessage, bool> _matcher;
        protected HttpStatusCode _statusCode;
        public RequestMatcher(Func<HttpRequestMessage, bool> matcher, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _matcher = matcher;
            _statusCode = statusCode;
        }

        public bool Match(HttpRequestMessage request)
        {
            return _matcher(request);
        }

        public virtual HttpResponseMessage GetResponse(HttpRequestMessage request)
        {
            var response = new HttpResponseMessage(_statusCode);
            return response;
        }
    }

    public class RequestMatcherString : RequestMatcher
    {
        protected string _response;
        public RequestMatcherString(Func<HttpRequestMessage, bool> matcher, string response, HttpStatusCode statusCode = HttpStatusCode.OK)
            : base(matcher, statusCode)
        {
            _response = response ?? throw new ArgumentNullException(nameof(response), @"Response cannot be null");
        }
        public override HttpResponseMessage GetResponse(HttpRequestMessage request)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_response)
            };
            return response;
        }
    }

    public class RequestMatcherFile : RequestMatcher
    {
        private string _responseFile;

        public RequestMatcherFile(Func<HttpRequestMessage, bool> matcher, string responseFile, HttpStatusCode statusCode = HttpStatusCode.OK) : base(matcher, statusCode)
        {
            if (!File.Exists(responseFile))
                throw new FileNotFoundException(string.Format(@"Response contents file {0} does not exist.", responseFile));
            _responseFile = responseFile;
        }

        public override HttpResponseMessage GetResponse(HttpRequestMessage request)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(File.ReadAllText(_responseFile))
            };
            return response;
        }
    }

    public class RequestMatcherFunction : RequestMatcher
    {
        private Func<HttpRequestMessage, string> _responseFunction;
        public RequestMatcherFunction(Func<HttpRequestMessage, bool> matcher, Func<HttpRequestMessage, string> responseFunction, HttpStatusCode statusCode = HttpStatusCode.OK)
            : base(matcher, statusCode)
        {
            _responseFunction = responseFunction ?? throw new ArgumentNullException(nameof(responseFunction), @"Response function cannot be null");
        }
        public override HttpResponseMessage GetResponse(HttpRequestMessage request)
        {
            return new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseFunction.Invoke(request))
            };
        }
    }

    public class HttpMessageHandlerFactory
    {
        internal HttpMessageHandlerFactory()
        {
            // Can create a handler here for quick testing. Otherwise, they should be created by the test code.
        }

        private readonly Dictionary<string, MockHttpMessageHandler> _handlers = new Dictionary<string, MockHttpMessageHandler>();

        public MockHttpMessageHandler CreateHandler(string handlerName)
        {
            if (_handlers.ContainsKey(handlerName))
                throw new ArgumentException(string.Format(@"Handler {0} already exists", handlerName));
            var handler = new MockHttpMessageHandler();
            _handlers[handlerName] = handler;
            return handler;
        }

        public HttpMessageHandler getMessageHandler(string handlerName, HttpMessageHandler defaultHandler = null)
        {
            if (_handlers.TryGetValue(handlerName, out var handler))
                return handler;
            else if (defaultHandler != null)
                return defaultHandler;
            else
                return new HttpClientHandler();
        }
    }
}