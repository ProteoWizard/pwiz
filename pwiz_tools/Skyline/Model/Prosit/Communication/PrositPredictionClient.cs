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

using System;
using Grpc.Core;
using pwiz.Skyline.Model.Prosit.Config;
using Tensorflow.Serving;

namespace pwiz.Skyline.Model.Prosit.Communication
{
    /// <summary>
    /// A simple wrapper for simple construction of
    /// a prediction client through IP.
    /// </summary>
    public class PrositPredictionClient : PredictionService.PredictionServiceClient, IDisposable
    {
        private static PrositPredictionClient _predictionClient;
        private Channel _channel;

        public static PrositPredictionClient Current
        {
            get
            {
                if (FakeClient != null)
                    return FakeClient;

                if (_predictionClient != null)
                    return _predictionClient;

                return _predictionClient = new PrositPredictionClient(PrositConfig.GetPrositConfig());
            }
        }

        /// <summary>
        /// Public static wrapper for creating clients
        /// </summary>
        /// <param name="prositConfig">Configuration parameters</param>
        /// <returns>A client for making predictions with the given server</returns>
        public static PrositPredictionClient CreateClient(PrositConfig prositConfig)
        {
            if (FakeClient != null)
                return FakeClient;

            return _predictionClient?.Server == prositConfig.Server
                ? _predictionClient
                : new PrositPredictionClient(prositConfig);
        }

        protected PrositPredictionClient(PrositConfig prositConfig)
            : this(prositConfig.CreateChannel())
        {
            Server = prositConfig.Server;
        }

        private PrositPredictionClient(Channel channel) : base(channel)
        {
            _channel = channel;
        }

        public string Server { get; }
        
        // For faking predictions, usually without an actual server but instead
        // a set of cached predictions
        public static PrositPredictionClient FakeClient { get; set; }

        void IDisposable.Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _channel?.ShutdownAsync().Wait();
                _channel = null;
            }
        }
    }
}
