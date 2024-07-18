/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using NHibernate.Mapping.Attributes;

namespace ResourcesOrganizer.DataModel
{
    [Class(Lazy = false, Table="InvariantResource")]
    public class InvariantResource : Entity<InvariantResource>
    {
        [Property]
        public string? Name { get; set; }
        [Property]
        public string? Comment { get; set; }
        [Property]
        public string? File { get; set; }
        [Property]
        public string? Type { get; set; }
        [Property]
        public string? Value { get; set; }
        [Property]
        public string? MimeType { get; set; }
        [Property]
        public string? XmlSpace { get; set; }

        public ResourcesModel.InvariantResourceKey GetKey()
        {
            var key = new ResourcesModel.InvariantResourceKey
            {
                Name = Name,
                Type = Type,
                MimeType = MimeType,
                Value = Value!,
                Comment = Comment
            };
            if (!key.IsLocalizableText)
            {
                key = key with { File = File };
            }

            return key;
        }
    }
}
