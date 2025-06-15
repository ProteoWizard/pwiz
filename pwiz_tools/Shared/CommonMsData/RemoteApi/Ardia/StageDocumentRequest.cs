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

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace pwiz.CommonMsData.RemoteApi.Ardia
{
    // Ardia API StageDocument and Document models
    //     https://api.hyperbridge.cmdtest.thermofisher.com/document/api/swagger/index.html
    //
    public class StageDocumentRequest
    {
        public static StageDocumentRequest Create()
        {
            return new StageDocumentRequest();
        }

        private StageDocumentRequest() { }

        public IList<DocumentPieceRequest> Pieces { get; } = new List<DocumentPieceRequest>();

        public void AddPiece(string name)
        {
            var piece = new DocumentPieceRequest
            {
                PieceName = name
            };
            Pieces.Add(piece);
        }

        // CONSIDER: if SingleDocument, disallow adding additional pieces
        public void AddSingleDocumentPiece()
        {
            AddPiece(DocumentPieceRequest.SINGLE_DOCUMENT);
        }

        public class DocumentPieceRequest
        {
            public const string SINGLE_DOCUMENT = "[SingleDocument]";

            public string PieceName { get; internal set; }
        }
    }

    public class StagedDocumentResponse
    {
        public string UploadId { get; set; }
        public IList<DocumentPieceResponse> Pieces { get; set; }

        public class DocumentPieceResponse
        {
            public string PieceName { get; set; }
            public string PiecePath { get; set; }
            [SuppressMessage("ReSharper", "IdentifierTypo")]
            public IList<string> PresignedUrls { get; set; } // CONSIDER: URI rather than string
        }
    }
}
