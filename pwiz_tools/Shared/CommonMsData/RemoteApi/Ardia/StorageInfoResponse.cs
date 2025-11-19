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

using System.Runtime.Serialization;
using Newtonsoft.Json;
using pwiz.Common.SystemUtil;

namespace pwiz.CommonMsData.RemoteApi.Ardia
{
    public class StorageInfoResponse
    {
        public static StorageInfoResponse Create(string json)
        {
            return JsonConvert.DeserializeObject<StorageInfoResponse>(json);
        }

        private StorageInfoResponse() { }
        
        public long? TotalSpace { get; set; }
        public long? AvailableFreeSpace { get; set; }
        public bool IsUnlimited { get; private set; }

        public bool HasAvailableStorageFor(long sizeInBytes) => IsUnlimited || sizeInBytes < AvailableFreeSpace;

        public string AvailableFreeSpaceLabel => IsUnlimited ? ArdiaResources.FileUpload_AvailableFreeSpace_Unlimited : LimitedSpaceLabel();

        public string LimitedSpaceLabel()
        {
            var sizeString = AvailableBytesToString(AvailableFreeSpace);
            return string.Format(ArdiaResources.FileUpload_AvailableFreeSpace_WithSize, sizeString);
        }

        public static string AvailableBytesToString(long? bytes)
        {
            if (bytes == null || bytes < 0)
                return string.Empty;

            // Omit decimal point if available space is measured in B or KB
            var format = bytes < 1024 * 1024 ? @"fs0" : @"fs2";

            var formatProvider = new FileSizeFormatProvider();
            var sizeString = formatProvider.Format(format, bytes, formatProvider);

            return sizeString;
        }

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
