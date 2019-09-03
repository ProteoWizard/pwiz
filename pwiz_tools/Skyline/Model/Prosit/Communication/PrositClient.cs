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
using pwiz.Skyline.Properties;
using Tensorflow.Serving;

namespace pwiz.Skyline.Model.Prosit.Communication
{
    /// <summary>
    /// A simple wrapper for simple construction of
    /// a prediction client through IP.
    /// </summary>
    public class PrositPredictionClient : PredictionService.PredictionServiceClient
    {
        private static PredictionService.PredictionServiceClient _debugPredictionClient;

        public static PredictionService.PredictionServiceClient Current
        {
            get
            {
                if (_debugPredictionClient != null)
                    return _debugPredictionClient;

                if (string.IsNullOrEmpty(Settings.Default.PrositServer))
                    throw new PrositException(PrositResources.PrositPredictionClient_Current_No_Prosit_server_set);

                return new PrositPredictionClient(Settings.Default.PrositServer);
            }
            set { _debugPredictionClient = value; }
        }

        public PrositPredictionClient(string server)
            : base(new Channel(server, ChannelCredentials.Insecure))
        {
        }
    }
}
