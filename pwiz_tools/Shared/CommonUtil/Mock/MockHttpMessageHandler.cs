using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

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
                return await Task.FromResult(matcher.GetResponse());
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
        protected string _response;
        protected HttpStatusCode _statusCode = HttpStatusCode.OK;
        public RequestMatcher(Func<HttpRequestMessage, bool> matcher, string response, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _matcher = matcher;
            _response = response;
            _statusCode = statusCode;
        }

        public bool Match(HttpRequestMessage request)
        {
            return _matcher(request);
        }

        public virtual HttpResponseMessage GetResponse()
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
        public RequestMatcherFile(Func<HttpRequestMessage, bool> matcher, string response) : base(matcher, response)
        {
            if (!File.Exists(response))
                throw new FileNotFoundException(string.Format(@"Response contents file {0} does not exist.", response));
        }

        public override HttpResponseMessage GetResponse()
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(File.ReadAllText(_response))
            };
            return response;
        }
    }

    public class HttpMessageHandlerFactory
    {
        internal HttpMessageHandlerFactory()
        {
            var wcHandler = CreateHandler("wcHandler1");
            wcHandler.AddMatcher(new RequestMatcherFile(req => req.RequestUri.ToString().IndexOf(@"/waters_connect/v1.0/folders") >= 0,
                @"C:\Users\RitaCh\Workspaces\ProteoWiz\pwiz1\pwiz_tools\Skyline\MockHttpData\WCFolders.json"));
            wcHandler.AddMatcher(new RequestMatcherFile(req => req.RequestUri.ToString().IndexOf(@"/waters_connect/v2.0/published-methods") >= 0,
                @"C:\Users\RitaCh\Workspaces\ProteoWiz\pwiz1\pwiz_tools\Skyline\MockHttpData\WCMethods.json"));
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