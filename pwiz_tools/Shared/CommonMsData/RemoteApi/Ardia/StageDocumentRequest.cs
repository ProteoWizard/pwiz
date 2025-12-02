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
using pwiz.Common.SystemUtil;

// Ardia API StageDocument and Document models
//     https://api.ardia-core-int.cmdtest.thermofisher.com/document/api/swagger/index.html
namespace pwiz.CommonMsData.RemoteApi.Ardia
{
    [SuppressMessage("ReSharper", "IdentifierTypo")]
    public class StageDocumentRequest
    {
        public const string DEFAULT_PIECE_NAME = "[SingleDocument]";

        /// <summary>
        /// </summary>
        /// <param name="fileSize">size in bytes of local file to upload</param>
        /// <param name="maxPartSizeMBs">max size of a part in MB</param>
        /// <returns></returns>
        public static StageDocumentRequest Create(long fileSize, int maxPartSizeMBs)
        {
            Assume.IsTrue(ArdiaClient.IsValidPartSize(maxPartSizeMBs));

            var model = new StageDocumentRequest();
            if (fileSize < maxPartSizeMBs * 1024 * 1024)
            {
                model.AddPiece();
            }
            else
            {
                model.AddPiece(true, fileSize, maxPartSizeMBs);
            }

            return model;
        }

        private StageDocumentRequest() { }

        public IList<DocumentPieceRequest> Pieces { get; } = new List<DocumentPieceRequest>();

        private void AddPiece()
        {
            Pieces.Add(new DocumentPieceRequest());
        }

        private void AddPiece(bool isMultiPart, long fileSize, long maxPartSize)
        {
            Pieces.Add(new DocumentPieceRequest(isMultiPart, fileSize, maxPartSize));
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }

        public class DocumentPieceRequest
        {
            internal DocumentPieceRequest() { }

            internal DocumentPieceRequest(bool isMultiPart, long size, long maxPartSize)
            {
                IsMultiPart = isMultiPart;
                Size = size;
                PartSize = maxPartSize;
            }

            public string PieceName => DEFAULT_PIECE_NAME;

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public bool IsMultiPart { get; internal set; }

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)] 
            public long Size { get; internal set; }

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public long PartSize { get; internal set; }
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

            // public string PiecePath { get; set; }

            public string StoragePath { get; set; }

            public string MultiPartId { get; set; }

            public IList<string> PresignedUrls { get; set; }
        }
    }
}
