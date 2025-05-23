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

// Ardia API StageDocument and Document models
//     https://api.hyperbridge.cmdtest.thermofisher.com/document/api/swagger/index.html
//
namespace pwiz.Skyline.Model.Results.RemoteApi.Ardia
{
    public class ArdiaStageDocumentRequest
    {
        public static ArdiaStageDocumentRequest Create()
        {
            return new ArdiaStageDocumentRequest();
        }

        private ArdiaStageDocumentRequest() { }

        public IList<ArdiaStageDocumentPieceRequest> Pieces { get; } = new List<ArdiaStageDocumentPieceRequest>();

        public void AddPiece(string name)
        {
            var piece = new ArdiaStageDocumentPieceRequest
            {
                PieceName = name
            };
            Pieces.Add(piece);
        }

        // CONSIDER: if SingleDocument, disallow adding additional pieces
        public void AddSingleDocumentPiece()
        {
            AddPiece(ArdiaStageDocumentPieceRequest.SINGLE_DOCUMENT);
        }
    }

    public class ArdiaStageDocumentPieceRequest
    {
        internal const string SINGLE_DOCUMENT = "[SingleDocument]";

        public string PieceName { get; internal set; }
    }

    public class ArdiaStagedDocumentResponse
    {
        public string UploadId { get; set; }
        public IList<ArdiaStagedDocumentPieceResponse> Pieces { get; set; }
    }

    public class ArdiaStagedDocumentPieceResponse
    {
        public string PieceName { get; set; }
        public string PiecePath { get; set; }
        [SuppressMessage("ReSharper", "IdentifierTypo")]
        public IList<string> PresignedUrls { get; set; } // CONSIDER: URI rather than string
    }

    public class ArdiaDocumentRequest
    {
        public string UploadId { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public int Size { get; set; }
    }

    public class ArdiaDocumentResponse
    {
        public string DocumentId { get; set; }
        [SuppressMessage("ReSharper", "IdentifierTypo")]
        public IList<string> PresignedUrls { get; set; } // CONSIDER: URI rather than string
    }
}