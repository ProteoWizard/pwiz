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
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using pwiz.CommonMsData.RemoteApi.Ardia;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    [SuppressMessage("ReSharper", "IdentifierTypo")]
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    public class ArdiaMarshalingTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestArdiaJsonUnmarshaling()
        {
            // StagedDocument - Request
            var stagedDocumentRequest = StageDocumentRequest.Create();
            stagedDocumentRequest.AddSingleDocumentPiece();

            Assert.IsNotNull(stagedDocumentRequest.Pieces);
            Assert.AreEqual(1, stagedDocumentRequest.Pieces.Count);
            Assert.AreEqual(StageDocumentRequest.DocumentPieceRequest.SINGLE_DOCUMENT, stagedDocumentRequest.Pieces[0].PieceName);

            // StagedDocument - Response
            var stagedDocumentResponse = JsonConvert.DeserializeObject<StagedDocumentResponse>(SimpleStagedDocumentString);

            Assert.AreEqual("97088887-788079", stagedDocumentResponse.UploadId);

            var piece = stagedDocumentResponse.Pieces[0];
            Assert.IsNotNull(stagedDocumentResponse.Pieces);
            Assert.AreEqual(1, stagedDocumentResponse.Pieces.Count);
            Assert.IsNotNull(piece);
            Assert.AreEqual(StageDocumentRequest.DocumentPieceRequest.SINGLE_DOCUMENT, piece.PieceName);
            Assert.AreEqual("/foobar", piece.PiecePath);

            var presignedUrls = piece.PresignedUrls;
            Assert.IsNotNull(presignedUrls);
            Assert.AreEqual("https://www.example.com/", presignedUrls[0]);
        }

        [TestMethod]
        public void TestArdiaAccountUserConfigSettings()
        {
            var ardiaAccount = new ArdiaAccount("https://ardiaserver.example.com", "ardia_username", "ardia password", "ardia token value");

            var remoteAccountList = new RemoteAccountList { ardiaAccount };

            var stringWriter = new StringWriter();
            var xmlSerializer = new XmlSerializer(typeof(RemoteAccountList));
            xmlSerializer.Serialize(stringWriter, remoteAccountList);
            var accountListXml = stringWriter.ToString();

            // token's plaintext value should not be in serialized XML
            Assert.AreEqual(-1, accountListXml.IndexOf(ardiaAccount.Token, StringComparison.Ordinal));

            var deserializedAccountList = (RemoteAccountList)xmlSerializer.Deserialize(new StringReader(accountListXml));
            Assert.AreEqual(remoteAccountList.Count, deserializedAccountList.Count);
            Assert.AreEqual(ardiaAccount, deserializedAccountList[0]);
            Assert.AreEqual(ardiaAccount.Token, ((ArdiaAccount)deserializedAccountList[0]).Token);
        }

        private static string SimpleStagedDocumentString =
            "{pieces: " +
            "   [" +
            "       {pieceName: \"[SingleDocument]\", piecePath: \"/foobar\", presignedUrls: [\"https://www.example.com/\"]}" +
            "   ], " +
            "   uploadId: \"97088887-788079\"" +
            "}";
    }
}