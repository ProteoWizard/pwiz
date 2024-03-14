/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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

using System.Collections.Generic;
using System.IO;

namespace AssortResources
{
    public class ResourceFile
    {
        public static ResourceFile FromPath(string path)
        {
            var resourceIdentifiers = ResourceIdentifiers.FromPath(path);
            var languages = new Dictionary<string, ResourceIdentifiers>();
            var baseName = Path.GetFileNameWithoutExtension(path) + ".";
            foreach (var filePath in Directory.EnumerateFiles(Path.GetDirectoryName(path) ?? string.Empty, "*.resx"))
            {
                var fileName = Path.GetFileName(filePath);
                if (fileName.StartsWith(baseName) && fileName.Length > baseName.Length + 5)
                {
                    string language = fileName.Substring(baseName.Length, fileName.Length - baseName.Length - 5);
                    languages.Add(language, ResourceIdentifiers.FromPath(filePath));
                }
            }

            return new ResourceFile(resourceIdentifiers, languages);
        }

        public ResourceFile(ResourceIdentifiers resourceIdentifiers,
            Dictionary<string, ResourceIdentifiers> languages)
        {
            ResourceIdentifiers = resourceIdentifiers;
            Languages = languages;
        }

        public string FilePath
        {
            get { return ResourceIdentifiers.FilePath; }
        }
        public ResourceIdentifiers ResourceIdentifiers { get; }
        public Dictionary<string, ResourceIdentifiers> Languages { get; }
    }
}
