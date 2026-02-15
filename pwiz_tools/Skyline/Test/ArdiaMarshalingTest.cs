/*
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
using System.IO;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CommonMsData.RemoteApi.Ardia;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class ArdiaMarshalingTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestArdiaAccountUserConfigSettings()
        {
            var ardiaAccount = new ArdiaAccount("https://ardiaserver.example.com", "ardia_username", "ardia password", EncryptedToken.FromString("ardia token value"));

            var remoteAccountList = new RemoteAccountList { ardiaAccount };

            var stringWriter = new StringWriter();
            var xmlSerializer = new XmlSerializer(typeof(RemoteAccountList));
            xmlSerializer.Serialize(stringWriter, remoteAccountList);
            var accountListXml = stringWriter.ToString();

            // token's plaintext value should not be in serialized XML
            Assert.AreEqual(-1, accountListXml.IndexOf(ardiaAccount.Token.Decrypted, StringComparison.Ordinal), $"{accountListXml}");

            var deserializedAccountList = (RemoteAccountList)xmlSerializer.Deserialize(new StringReader(accountListXml));
            Assert.AreEqual(remoteAccountList.Count, deserializedAccountList.Count);
            Assert.AreEqual(ardiaAccount, deserializedAccountList[0]);
            Assert.AreEqual(ardiaAccount.Token.Encrypted, ((ArdiaAccount)deserializedAccountList[0]).Token.Encrypted);
        }
    }
}