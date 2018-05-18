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

namespace pwiz.Skyline.Model.Results.RemoteApi
{
    public class RemoteItem
    {
        public RemoteItem(MsDataFileUri msDataFileUri, string label, string type, DateTime? lastModified, long fileSizeBytes)
        {
            MsDataFileUri = msDataFileUri;
            Label = label;
            Type = type;
            LastModified = lastModified;
            FileSize = (ulong) fileSizeBytes;
        }

        public MsDataFileUri MsDataFileUri { get; private set; }
        public string Label { get; private set; }
        public string Type { get; private set; }
        public DateTime? LastModified { get; private set; }
        public ulong FileSize { get; private set; }
    }
}
