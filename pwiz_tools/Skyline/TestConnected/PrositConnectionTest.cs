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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Prosit.Communication;
using pwiz.Skyline.Model.Prosit.Config;
using pwiz.Skyline.Model.Prosit.Models;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestConnected
{
    [TestClass]
    public class PrositConfigTest : AbstractUnitTest
    {
        /// <summary>
        /// Tests that a connection can successfully be made to the server specified in PrositConfig.GetPrositConfig().
        /// </summary>
        [TestMethod]
        public void TestPrositConnection()
        {
            if (!HasPrositServer())
            {
                return;
            }
            PrositPredictionClient client = PrositPredictionClient.CreateClient(PrositConfig.GetPrositConfig());
            Assert.IsTrue(TestClient(client));
        }

        public bool TestClient(PrositPredictionClient client)
        {
            var pingPep = new Peptide(@"PING");
            var peptide = new PeptideDocNode(pingPep);
            var precursor = new TransitionGroupDocNode(new TransitionGroup(pingPep, Adduct.SINGLY_PROTONATED, IsotopeLabelType.light),
                new TransitionDocNode[0]);
            var input = new PrositIntensityModel.PeptidePrecursorNCE(peptide, precursor, IsotopeLabelType.light, 32);
            var intensityModel = PrositIntensityModel.GetInstance(PrositIntensityModel.Models.First());
            try
            {
                intensityModel.PredictSingle(client, SrmSettingsList.GetDefault(), input, CancellationToken.None);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Tests that the PrositConfig class can round-trip in XML serialization.
        /// Also, outputs the XML serialization text so that you can see what it is supposed to look like.
        /// </summary>
        [TestMethod]
        public void TestPrositConfigSerialization()
        {
            var prositConfig = new PrositConfig()
            {
                Server = "myhost",
                ClientCertificate = @"-----BEGIN CERTIFICATE-----
Stuff
-----END CERTIFICATE-----
",
                ClientKey = @"-----BEGIN RSA PRIVATE KEY-----
Other Stuff
-----END RSA PRIVATE KEY-----
",
                RootCertificate = @"-----BEGIN CERTIFICATE-----
Even More Stuff
-----END CERTIFICATE-----
"

            };
            var xmlSerializer = new XmlSerializer(typeof(PrositConfig));
            var myStream = new MemoryStream();
            xmlSerializer.Serialize(myStream, prositConfig);
            myStream.Seek(0, SeekOrigin.Begin);
            var roundTrip = xmlSerializer.Deserialize(myStream);
            Assert.IsInstanceOfType(roundTrip, typeof(PrositConfig));
            try
            {
                TestContext.WriteLine(Encoding.UTF8.GetString(myStream.ToArray()));
            }
            catch
            {
                // ignore possible TargetInvocationException:NotImplementedException
            }
        }

        [TestMethod]
        public void TestGetPrositConfig()
        {
            var prositConfig = PrositConfig.GetPrositConfig();
            Assert.IsNotNull(prositConfig);
            Assert.IsFalse(string.IsNullOrEmpty(prositConfig.Server));
        }

        /// <summary>
        /// Returns true if Skyline was compiled with a prosit config file that enables connecting to a real server.
        /// </summary>
        public static bool HasPrositServer()
        {
            return !string.IsNullOrEmpty(PrositConfig.GetPrositConfig().RootCertificate);
        }
    }
}
