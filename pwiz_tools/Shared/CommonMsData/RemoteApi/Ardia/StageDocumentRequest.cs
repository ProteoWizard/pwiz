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
using Newtonsoft.Json;

// Ardia API StageDocument and Document models
//     https://api.hyperbridge.cmdtest.thermofisher.com/document/api/swagger/index.html
//
namespace pwiz.CommonMsData.RemoteApi.Ardia
{
    [SuppressMessage("ReSharper", "IdentifierTypo")]
    public class StageDocumentRequest
    {
        public const string SINGLE_DOCUMENT = "[SingleDocument]";

        public static StageDocumentRequest Create()
        {
            return new StageDocumentRequest();
        }

        public static StageDocumentRequest CreateSinglePieceDocument()
        {
            var document = Create();
            document.AddSingleDocumentPiece();
            return document;
        }

        private StageDocumentRequest() { }

        public IList<DocumentPieceRequest> Pieces { get; } = new List<DocumentPieceRequest>();

        // CONSIDER: if SingleDocument, disallow adding additional pieces
        public void AddSingleDocumentPiece()
        {
            AddPiece(SINGLE_DOCUMENT);
        }

        private void AddPiece(string name)
        {
            var piece = new DocumentPieceRequest
            {
                PieceName = name
            };

            Pieces.Add(piece);
        }

        public void AddPiece(string name, long partSize, long size, bool isMultiPart)
        {
            var piece = new DocumentPieceRequest
            {
                PieceName = name,
                PartSize = partSize,
                Size = size,
                IsMultiPart = isMultiPart
            };
            Pieces.Add(piece);
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }

        public class DocumentPieceRequest
        {
            internal DocumentPieceRequest() { }

            public string PieceName { get; internal set; }
            
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public long PartSize { get; internal set; }
            
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)] 
            public long Size { get; internal set; }
            
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)] 
            public bool IsMultiPart { get; internal set; }
        }
    }

    [SuppressMessage("ReSharper", "IdentifierTypo")]
    public class StagedDocumentResponse
    {
        public string UploadId { get; set; }

        public IList<DocumentPieceResponse> Pieces { get; set; }

        public static StagedDocumentResponse FromJson(string json)
        {
            return JsonConvert.DeserializeObject<StagedDocumentResponse>(json);
        }

        public class DocumentPieceResponse
        {
            public string PieceName { get; set; }

            public string PiecePath { get; set; }

            public string StoragePath { get; set; }

            public string MultiPartId { get; set; }

            public IList<string> PresignedUrls { get; set; }
        }
    }
}
