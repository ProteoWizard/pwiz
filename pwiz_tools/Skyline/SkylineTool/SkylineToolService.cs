/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.ServiceModel;

namespace SkylineTool
{
    /// <summary>
    /// SkylineToolService is a class created within the Skyline process to service
    /// requests from interactive tools that it started.
    /// </summary>
    public class SkylineToolService : IDisposable
    {
        private const int MaxMessageLength = 200000;
        private readonly ServiceHost _host;

        public SkylineToolService(Type serviceClassType, string connectionName)
        {
            _host = new ServiceHost(serviceClassType);
            var address = GetAddress(connectionName);
            var binding = new NetNamedPipeBinding
            {
                MaxReceivedMessageSize = MaxMessageLength,
                ReceiveTimeout = TimeSpan.MaxValue
            };
            _host.AddServiceEndpoint(typeof (ISkylineTool), binding, address);
            _host.Open();
        }

        public void Dispose()
        {
            _host.Close();
        }

        /// <summary>
        /// Get a service address given a connection name to the Skyline process.
        /// </summary>
        public static string GetAddress(string connectionName)
        {
            return "net.pipe://localhost/SkylineToolService-" + connectionName; // Not L10N
        }
    }
}
