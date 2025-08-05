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

using System;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace pwiz.CommonMsData.RemoteApi.Ardia
{
    public class StorageInfoResponse
    {
        private const long GB = 1024 * 1024 * 1024;

        public static StorageInfoResponse Create(string json)
        {
            return JsonConvert.DeserializeObject<StorageInfoResponse>(json);
        }

        private StorageInfoResponse() { }
        
        public long? TotalSpace { get; set; }
        public long? AvailableFreeSpace { get; set; }
        public bool IsUnlimited { get; private set; }

        public double? AvailableFreeSpaceGb
        {
            get
            {
                if (AvailableFreeSpace != null)
                {
                    var originalNumber = (double)AvailableFreeSpace / GB;
                    var roundedNumber = Math.Round(originalNumber, 2);
                    return roundedNumber;
                }
                else return null;
            }
        }

        public bool HasAvailableStorageFor(long sizeInBytes) => IsUnlimited || sizeInBytes < AvailableFreeSpace;

        public string AvailableFreeSpaceLabel => IsUnlimited
            ? ArdiaResources.FileUpload_AvailableFreeSpace_Unlimited
            : string.Format(ArdiaResources.FileUpload_AvailableFreeSpace_SizeInGB, AvailableFreeSpaceGb);

        [OnDeserialized]
        internal void OnDeserialized(StreamingContext context)
        {
            if (TotalSpace == null && AvailableFreeSpace == null)
            {
                IsUnlimited = true;
            }
        }
    }
}
