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

using Newtonsoft.Json;

namespace pwiz.CommonMsData.RemoteApi.Ardia
{
    // Ardia API StageDocument and Document models
    //     https://api.hyperbridge.cmdtest.thermofisher.com/document/api/swagger/index.html
    //
    public class CreateDocumentRequest
    {
        public string UploadId { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public long Size { get; set; }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }
    }

    public class CreateDocumentResponse
    {
        public string DocumentId { get; set; }
        public string RLink { get; set; }

        public static CreateDocumentResponse FromJson(string json)
        {
            return JsonConvert.DeserializeObject<CreateDocumentResponse>(json);
        }
    }
}
