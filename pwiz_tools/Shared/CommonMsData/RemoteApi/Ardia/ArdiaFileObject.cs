/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using System.Linq;
using Newtonsoft.Json.Linq;

namespace pwiz.CommonMsData.RemoteApi.Ardia
{
    public class ArdiaFileObject : ArdiaObject
    {
        public ArdiaFileObject(JObject injectionObject)
        {
            // ReSharper disable LocalizableElement

            // injections should only have one raw file
            var rawFile = injectionObject["rawFiles"].OfType<JObject>().First();
            Id = GetProperty(injectionObject, "id");
            Name = GetProperty(injectionObject, "name");
            Type = GetProperty(rawFile, "type");
            StorageId = GetProperty(rawFile, "storageId");
            Size = GetLongProperty(rawFile, "size");
            CreatedAt = GetDateProperty(injectionObject, "injectionTime");
            ModifiedAt = GetDateProperty(injectionObject, "updatedOn");
            // ReSharper restore LocalizableElement
        }

        public string Type { get; private set; }
        public string StorageId { get; private set; }
        public long? Size { get; private set; }
        public DateTime? CreatedAt { get; private set; }
        public DateTime? ModifiedAt { get; private set; }
    }
}
