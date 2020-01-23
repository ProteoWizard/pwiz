/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using System.Xml.Serialization;
using Grpc.Core;

namespace pwiz.Skyline.Model.Prosit.Config
{
    /// <summary>
    /// Configuration of a Prosit server.
    /// </summary>
    public class PrositConfig
    {
        public string Server { get; set; }
        public string RootCertificate { get; set; }
        public string ClientCertificate { get; set; }
        public string ClientKey { get; set; }

        public Channel CreateChannel()
        {
            return new Channel(Server, GetChannelCredentials());
        }

        public ChannelCredentials GetChannelCredentials()
        {
            if (string.IsNullOrEmpty(RootCertificate))
            {
                return ChannelCredentials.Insecure;
            }

            if (string.IsNullOrEmpty(ClientCertificate))
            {
                return new SslCredentials(RootCertificate);
            }
            return new SslCredentials(RootCertificate, new KeyCertificatePair(ClientCertificate, ClientKey));
        }


        /// <summary>
        /// Read the configuration from the PrositConfig.xml embedded resource
        /// </summary>
        public static PrositConfig GetPrositConfig()
        {
            using (var stream = typeof(PrositConfig).Assembly
                .GetManifestResourceStream(typeof(PrositConfig), "PrositConfig.xml"))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException(@"Unable to read PrositConfig.xml");
                }
                var xmlSerializer = new XmlSerializer(typeof(PrositConfig));
                return (PrositConfig) xmlSerializer.Deserialize(stream);
            }
        }
    }
}
