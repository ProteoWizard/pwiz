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
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace pwiz.Skyline.Model.Results.RemoteApi.Ardia
{
    /// <summary>
    /// Ardia API document model
    ///     https://api.hyperbridge.cmdtest.thermofisher.com/document/api/swagger/index.html
    /// </summary>
    public class ArdiaStageDocumentRequest : ArdiaObject
    {
        private List<ArdiaStageDocumentPieceRequest> _pieces = new List<ArdiaStageDocumentPieceRequest>();

        private ArdiaStageDocumentRequest() { }

        public IList<ArdiaStageDocumentPieceRequest> Pieces => _pieces;

        public void AddPiece(string name = ArdiaStageDocumentPieceRequest.SINGLE_DOCUMENT)
        {
            var piece = new ArdiaStageDocumentPieceRequest
            {
                PieceName = name
            };
            _pieces.Add(piece);
        }

        public static ArdiaStageDocumentRequest Create()
        {
            return new ArdiaStageDocumentRequest();
        }
    }

    public class ArdiaStageDocumentPieceRequest : ArdiaObject
    {
        public const string SINGLE_DOCUMENT = "[SingleDocument]";

        internal ArdiaStageDocumentPieceRequest() { }

        public string PieceName { get; internal set; }
    }

    public class ArdiaStagedDocumentResponse : ArdiaObject
    {
        public ArdiaStagedDocumentResponse(JObject json)
        {
            // ReSharper disable LocalizableElement
            UploadId = GetProperty(json, "uploadId");

            // CONSIDER: ArdiaObject is an Immutable but has mutable properties (does not use ChangeProperty)?
            // Converting JArray to List of objects - https://gist.github.com/deostroll/9969331
            var jArray = json["pieces"].Value<JArray>();
            Pieces = jArray.ToObject<List<ArdiaStagedDocumentPieceResponse>>();
        }

        public string UploadId { get; private set; }
        public IList<ArdiaStagedDocumentPieceResponse> Pieces { get; private set; }
    }

    public class ArdiaStagedDocumentPieceResponse : ArdiaObject
    {
        public ArdiaStagedDocumentPieceResponse() {}

        public ArdiaStagedDocumentPieceResponse(JObject json)
        {
            // ReSharper disable LocalizableElement
            PieceName = GetProperty(json, "pieceName");
            PiecePath = GetProperty(json, "piecePath");
            
            var arrayStr = GetProperty(json, "presignedUrls");
            PresignedUrls = JsonConvert.DeserializeObject<string[]>(arrayStr).ToList();
        }

        public string PieceName { get; set; }
        public string PiecePath { get; set; }
        public IList<string> PresignedUrls { get; set; } // CONSIDER: URI instead of string
    }
}