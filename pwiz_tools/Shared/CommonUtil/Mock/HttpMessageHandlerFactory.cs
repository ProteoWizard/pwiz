/*
 * Original author: Rita Chupalov <ritach .at. uw.edu>
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5) <noreply .at. anthropic.com>
 *
 * Copyright 2025 University of Washington - Seattle, WA
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

namespace pwiz.Common.Mock
{
    /// <summary>
    /// Factory class for injecting HttpMessageHandlers for testing purposes.
    /// </summary>
    public class HttpMessageHandlerFactory
    {
        internal HttpMessageHandlerFactory()
        {
            // Can create a handler here for quick testing. Otherwise, they should be created by the test code.
        }

        private readonly Dictionary<string, HttpMessageHandler> _handlers = new Dictionary<string, HttpMessageHandler>();

        public void CreateReplaceHandler(string handlerName, HttpMessageHandler handler)
        {
            _handlers[handlerName] = handler;
        }

        /// <summary>
        /// Returns the handler registered under the given name, or null if none is registered.
        /// Callers that cache HttpClient pipelines (e.g. through IHttpClientFactory) should use
        /// this to detect a registered test handler and build their client on it directly, so a
        /// replacement installed by <see cref="CreateReplaceHandler"/> takes effect immediately
        /// instead of being masked by a pooled pipeline built around a previous handler.
        /// </summary>
        public HttpMessageHandler GetRegisteredHandler(string handlerName)
        {
            return _handlers.TryGetValue(handlerName, out var handler) ? handler : null;
        }

        public HttpMessageHandler getMessageHandler(string handlerName, Func<HttpMessageHandler> defaultHandlerFactory = null)
        {
            if (_handlers.TryGetValue(handlerName, out var handler))
                return handler;
            else if (defaultHandlerFactory != null)
            {
                var defaultHandler = defaultHandlerFactory();
                if (defaultHandler != null)
                    return defaultHandler;
            }
            return new HttpClientHandler();
        }
    }
}
