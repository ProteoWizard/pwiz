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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using pwiz.Skyline.Model.Results.RemoteApi.Ardia;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class RemoteArdiaTest : AbstractUnitTest
    {
        [TestMethod]
        public void RemoteArdiaJsonUnmarshalingTest()
        {
            // StageDocument - Request
            var stagedDocumentRequest = ArdiaStageDocumentRequest.Create();
            stagedDocumentRequest.AddSingleDocumentPiece();

            Assert.IsNotNull(stagedDocumentRequest.Pieces);
            Assert.AreEqual(1, stagedDocumentRequest.Pieces.Count);
            Assert.AreEqual(ArdiaStageDocumentPieceRequest.SINGLE_DOCUMENT, stagedDocumentRequest.Pieces[0].PieceName);

            // StageDocument - Response
            var stagedDocumentResponse = JsonConvert.DeserializeObject<ArdiaStagedDocumentResponse>(SimpleStagedDocumentString);

            Assert.AreEqual("97088887-788079", stagedDocumentResponse.UploadId);

            var piece = stagedDocumentResponse.Pieces[0];
            Assert.IsNotNull(stagedDocumentResponse.Pieces);
            Assert.AreEqual(1, stagedDocumentResponse.Pieces.Count);
            Assert.IsNotNull(piece);
            Assert.AreEqual(ArdiaStageDocumentPieceRequest.SINGLE_DOCUMENT, piece.PieceName);
            Assert.AreEqual("/foobar", piece.PiecePath);

            var presignedUrls = piece.PresignedUrls;
            Assert.IsNotNull(presignedUrls);
            Assert.AreEqual("https://www.example.com/", presignedUrls[0]);
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