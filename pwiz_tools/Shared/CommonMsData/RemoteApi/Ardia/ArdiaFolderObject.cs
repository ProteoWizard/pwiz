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
using Newtonsoft.Json.Linq;

namespace pwiz.CommonMsData.RemoteApi.Ardia
{
    public class ArdiaFolderObject : ArdiaObject
    {
        public string Path { get; private set; }
        public bool HasChildren { get; private set; }
        public string ParentId { get; private set; }

        /// <summary>
        /// Sequences are a special type of folder that contain one or more RAW files;
        /// their contents are enumerated by a different endpoint than regular folders, but we still basically treat them like folders.
        /// </summary>
        public string SequenceKey { get; private set; }

        public ArdiaFolderObject(JObject jobject, string parentId)
        {
            // ReSharper disable LocalizableElement
            Id = GetProperty(jobject, "id");
            Name = GetProperty(jobject, "name");
            Path = GetProperty(jobject, "fullPathName");
            SequenceKey = GetProperty(jobject, "resourceKey");
            HasChildren = bool.Parse(GetProperty(jobject, "hasChildren"));
            ParentId = parentId;
        }
    }
}
