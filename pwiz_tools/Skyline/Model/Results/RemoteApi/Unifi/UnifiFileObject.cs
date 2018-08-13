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
using Newtonsoft.Json.Linq;

namespace pwiz.Skyline.Model.Results.RemoteApi.Unifi
{
    public class UnifiFileObject : UnifiObject
    {
        public UnifiFileObject(JObject jobject)
        {
            // ReSharper disable NonLocalizedString
            Id = GetProperty(jobject, "id");
            Name = GetProperty(jobject, "name");
            Type = GetProperty(jobject, "type");
            IdInFolder = GetIntegerProperty(jobject, "idInFolder");
            CreatedAt = GetDateProperty(jobject, "createdAt");
            ModifiedAt = GetDateProperty(jobject, "modifiedAt");
            // ReSharper restore NonLocalizedString
        }

        public string Type { get; private set; }
        public int? IdInFolder { get; private set; }
        public DateTime? CreatedAt { get; private set; }
        public DateTime? ModifiedAt { get; private set; }
    }
}
