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
using System;
using Newtonsoft.Json.Linq;

namespace pwiz.CommonMsData.RemoteApi.WatersConnect
{
    public class WatersConnectFileObject : WatersConnectObject
    {
        public WatersConnectFileObject(JObject jobject)
        {
            // ReSharper disable LocalizableElement
            Id = GetProperty(jobject, "id");
            Name = GetProperty(jobject, "skylineName");
            //Type = GetProperty(jobject, "type");
            CreatedAt = GetDateProperty(jobject, "dateCreated");
            ModifiedAt = GetDateProperty(jobject, "dateModified");
            //AcquisitionDateTime = GetDateProperty(jobject, "acquisitionStartDateTime");
            // ReSharper restore LocalizableElement
        }

        //public string Type { get; private set; }
        public DateTime? CreatedAt { get; private set; }
        public DateTime? ModifiedAt { get; private set; }
        public DateTime? AcquisitionDateTime { get; private set; }
    }
}
