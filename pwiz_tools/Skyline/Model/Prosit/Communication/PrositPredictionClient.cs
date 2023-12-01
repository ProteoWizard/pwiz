/*
 * Original author: Tobias Rohde <tobiasr .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2019 University of Washington - Seattle, WA
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

using Grpc.Core;
using pwiz.Skyline.Model.Prosit.Config;
using Tensorflow.Serving;

namespace pwiz.Skyline.Model.Prosit.Communication
{
    /// <summary>
    /// A simple wrapper for simple construction of
    /// a prediction client through IP.
    /// </summary>
    public class PrositPredictionClient : PredictionService.PredictionServiceClient
    {
        private static PrositPredictionClient _predictionClient;

        public static PrositPredictionClient Current
        {
            get
            {
                if (FakeClient != null)
                    return FakeClient;

                if (_predictionClient != null)
                    return _predictionClient;

                var config = PrositConfig.GetPrositConfig();
                
                // TODO(nicksh): this Channel never gets disposed, but it does not really matter
                // because it only gets created once
                var channel = config.CreateChannel();
                _predictionClient = new PrositPredictionClient(channel, config.Server);
                return _predictionClient;
            }
        }

        /// <summary>
        /// Public static wrapper for creating clients
        /// </summary>
        /// <param name="channel">Channel that the client should use. Caller is responsible for shutting down the channel.</param>
        /// <param name="server">Name of the server</param>
        /// <returns>A client for making predictions with the given server</returns>
        public static PrositPredictionClient CreateClient(Channel channel, string server)
        {
            if (FakeClient != null)
            {
                return FakeClient;
            }
            return new PrositPredictionClient(channel, server);
        }

        /// <summary>
        /// Constructs a new client. Caller is responsible for shutting down the Channel.
        /// </summary>
        public PrositPredictionClient(Channel channel, string server) : base(channel)
        {
            Server = server;
        }

        public string Server { get; }

        // For faking predictions, usually without an actual server but instead
        // a set of cached predictions
        public static PrositPredictionClient FakeClient { get; set; }
    }
}
