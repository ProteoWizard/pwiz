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

namespace pwiz.CommonMsData.RemoteApi.Ardia
{
    internal class UploadPartsResponse
    {
        internal static UploadPartsResponse Create()
        {
            return new UploadPartsResponse();
        }

        internal UploadPartsResponse()
        {
            Parts = new List<UploadPartResponse>();
        }

        internal IList<UploadPartResponse> Parts { get; }
        internal long UploadedBytes => Parts.Sum(part => part.BytesUploaded);
    }

    internal class UploadPartResponse
    {
        internal static UploadPartResponse Create(long bytesUploaded, string eTag)
        {
            return new UploadPartResponse
            {
                BytesUploaded = bytesUploaded,
                ETag = eTag
            };
        }

        public long BytesUploaded { get; private set; }
        public string ETag { get; private set; }
    }
}
