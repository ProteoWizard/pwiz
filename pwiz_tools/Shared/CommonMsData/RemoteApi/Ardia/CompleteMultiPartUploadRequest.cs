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
using Newtonsoft.Json;

namespace pwiz.CommonMsData.RemoteApi.Ardia
{
    public class CompleteMultiPartUploadRequest
    {
        public static CompleteMultiPartUploadRequest Create(string storagePath, string multiPartId, IList<string> eTags)
        {
            var model = new CompleteMultiPartUploadRequest
            {
                StoragePath = storagePath,
                MultiPartId = multiPartId,
            };

            // NB: partTags are numbered starting at 1, so increment the loop index
            for (var i = 0; i < eTags.Count; i++) 
            {
                model.AddPartTag(i + 1, eTags[i]);
            }

            return model;
        }

        public string StoragePath { get; private set; }
        public string MultiPartId { get; private set; }
        public IList<PartTag> PartTags { get; } = new List<PartTag>();

        public void AddPartTag(int partNumber, string eTag)
        {
            PartTags.Add(PartTag.Create(partNumber, eTag));
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }

        public class PartTag
        {
            public string ETag { get; private set; }
            public int PartNumber { get; private set; }

            internal static PartTag Create(int partNumber, string eTag)
            {
                var model = new PartTag
                {
                    ETag = eTag,
                    PartNumber = partNumber
                };

                return model;
            }

            public override string ToString()
            {
                return $@"{PartNumber}: {ETag}";
            }
        }
    }
}
