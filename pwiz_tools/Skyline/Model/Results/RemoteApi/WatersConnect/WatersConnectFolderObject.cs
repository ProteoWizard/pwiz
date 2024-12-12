/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com>
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using Newtonsoft.Json.Linq;

namespace pwiz.Skyline.Model.Results.RemoteApi.WatersConnect
{
    public class WatersConnectFolderObject : WatersConnectObject
    {
        public string Path { get; private set; }
        public bool HasSampleSets { get; private set; }
        public string ParentId { get; private set; }

        public WatersConnectFolderObject(JObject jobject, string parentId, bool isSampleSet)
        {
            // ReSharper disable LocalizableElement
            Name = GetProperty(jobject, "name");
            if (isSampleSet)
            {
                Id = GetProperty(jobject, "sampleSetId");
                HasSampleSets = true;
            }
            else
            {
                Id = GetProperty(jobject, "id");
                Path = GetProperty(jobject, "path");
                HasSampleSets = jobject["accessType"]["read"].Value<bool>();
            }
            // ReSharper restore LocalizableElement
            ParentId = parentId;
        }
    }
}
