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
using pwiz.Skyline.FileUI;

namespace pwiz.Skyline.Model.Results.RemoteApi.WatersConnect
{
    public class WatersConnectFolderObject : WatersConnectObject
    {
        public string Path { get; private set; }

        /// <summary>
        /// In some places set true based on if "accessType" "read" access is true
        /// </summary>
        public bool HasSampleSets { get; private set; }
        public string ParentId { get; private set; }

        /// <summary>
        /// Set true based on if "accessType" "read" access is true and isSampleSet in ctor call is false
        /// </summary>
        public bool AccessTypeReadTrue { get; private set; }

        /// <summary>
        /// Set true based on if "accessType" "write" access is true and isSampleSet in ctor call is false
        /// </summary>
        public bool AccessTypeWriteTrue { get; private set; }

        public string Children { get; private set; }

        public WatersConnectFolderObject(JObject jobject, string parentId, bool isSampleSet)
        {
            // ReSharper disable LocalizableElement
            Name = GetProperty(jobject, "name");
            Children = GetProperty(jobject, "children");
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
                AccessTypeReadTrue = jobject["accessType"]["read"].Value<bool>();
                AccessTypeWriteTrue = jobject["accessType"]["write"].Value<bool>();
            }
            // ReSharper restore LocalizableElement
            ParentId = parentId;
        }

        public override WatersConnectUrl ToUrl(WatersConnectUrl currentConnectUrl)
        {
            return (WatersConnectUrl)currentConnectUrl
                .ChangeType(WatersConnectUrl.ItemType.folder_child_folders_acquisition_methods)
                .ChangeFolderOrSampleSetId(Id)
                .ChangePathParts(UrlPath.Split(Path));
        }
    }
}
