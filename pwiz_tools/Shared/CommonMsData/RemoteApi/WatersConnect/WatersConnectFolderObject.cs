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
using pwiz.Common.SystemUtil;

namespace pwiz.CommonMsData.RemoteApi.WatersConnect
{
    public class WatersConnectFolderObject : WatersConnectObject
    {
        public string Path { get; private set; }
        public bool CanRead { get; private set; }
        public bool CanWrite { get; private set; }
        public string ParentId { get; private set; }

        public WatersConnectFolderObject(JObject jobject, string parentId, bool isSampleSet)
        {
            Assume.IsNotNull(jobject);
            // ReSharper disable LocalizableElement
            Name = GetProperty(jobject, "name");
            if (isSampleSet)
            {
                Id = GetProperty(jobject, "sampleSetId");
                CanRead = true;
                CanWrite = false;
            }
            else
            {
                Id = GetProperty(jobject, "id");
                Path = GetProperty(jobject, "path");
                CanRead = jobject["accessType"]["read"]?.Value<bool>() ?? false;
                CanWrite = jobject["accessType"]["write"]?.Value<bool>() ?? false;
            }
            // ReSharper restore LocalizableElement
            ParentId = parentId;
        }

        public override WatersConnectUrl ToUrl(WatersConnectUrl currentConnectUrl)
        {
            return (WatersConnectUrl) currentConnectUrl
                .ChangeType(WatersConnectUrl.ItemType.folder)
                .ChangeFolderOrSampleSetId(Id)
                .ChangePathPartsOnly(Path.Split('/'));
        }
    }
}
