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
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Serialization;
using Grpc.Core;
using pwiz.Skyline.Model.Koina.Communication;

namespace pwiz.Skyline.Model.Koina.Config
{
    /// <summary>
    /// Configuration of a Koina server.
    /// </summary>
    public class KoinaConfig
    {
        public string Server { get; set; }
        public bool RequireSsl { get; set; }
        public string RootCertificate { get; set; }
        public string ClientCertificate { get; set; }
        public string ClientKey { get; set; }

        public Channel CreateChannel()
        {
            return new Channel(Server, GetChannelCredentials());
        }

        private const string BEGIN_CERTIFICATE = @"-----BEGIN CERTIFICATE-----";
        private const string END_CERTIFICATE = @"-----END CERTIFICATE-----";
        public ChannelCredentials GetChannelCredentials()
        {
            if (string.IsNullOrEmpty(RootCertificate))
            {
                if (RequireSsl)
                {
                    // use all certificates from system's root store
                    using var certStore = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
                    certStore.Open(OpenFlags.ReadOnly);
                    var rootCertificates = new StringBuilder();
                    foreach (var rootCert in certStore.Certificates)
                        rootCertificates.AppendLine(string.Join(Environment.NewLine,
                            BEGIN_CERTIFICATE,
                            Convert.ToBase64String(rootCert.RawData, Base64FormattingOptions.InsertLineBreaks),
                            END_CERTIFICATE));
                    RootCertificate = rootCertificates.ToString();
                }
                else
                    return ChannelCredentials.Insecure;
            }

            if (string.IsNullOrEmpty(ClientCertificate))
            {
                return new SslCredentials(RootCertificate);
            }
            return new SslCredentials(RootCertificate, new KeyCertificatePair(ClientCertificate, ClientKey));
        }


        /// <summary>
        /// Read the configuration from the KoinaConfig.xml embedded resource
        /// </summary>
        public static KoinaConfig GetKoinaConfig()
        {
            using (var stream = typeof(KoinaConfig).Assembly
                .GetManifestResourceStream(typeof(KoinaConfig), "KoinaConfig.xml"))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException(@"Unable to read KoinaConfig.xml");
                }
                var xmlSerializer = new XmlSerializer(typeof(KoinaConfig));
                var koinaConfig = (KoinaConfig) xmlSerializer.Deserialize(stream);
                koinaConfig.RequireSsl = true;
                return koinaConfig;
            }
        }

        public void CallWithClient(Action<KoinaPredictionClient> action)
        {
            var channel = CreateChannel();
            try
            {
                var client = new KoinaPredictionClient(channel, Server);
                action(client);
            }
            finally
            {
                channel.ShutdownAsync().Wait();
            }
        }
    }
}
