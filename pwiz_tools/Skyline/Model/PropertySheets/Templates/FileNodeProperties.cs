/*
 * Original author: Aaron Banse <acbanse .at. acbanse dot com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
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

using System.ComponentModel;
using System.Resources;
using pwiz.Skyline.Model.Files;

namespace pwiz.Skyline.Model.PropertySheets.Templates
{
    public abstract class FileNodeProperties : GlobalizedObject
    {
        protected override ResourceManager GetResourceManager()
        {
            return PropertySheetFileNodeResources.ResourceManager;
        }

        protected FileNodeProperties(FileNode fileNode, string localFilePath)
        {
            FilePath = localFilePath;
            Name = fileNode.Name;
        }

        [Category("FileInfo")] public string FilePath { get; set; }
        [Category("FileInfo")] public string Name { get; set; }
        [Category("FileInfo")] public string FileSize { get; set; }

        // TODO: Does this need to be localized?
        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            var order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
