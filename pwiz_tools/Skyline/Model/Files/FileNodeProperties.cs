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
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.Files
{
    public class FileNodeProperties : GlobalizedObject
    {
        protected override ResourceManager GetResourceManager()
        {
            return PropertySheetFileNodeResources.ResourceManager;
        }

        public FileNodeProperties(FileNode model, string localFilePath)
        {
            FilePath = localFilePath;
            Name = model.Name;

            if (model.IsBackedByFile && System.IO.File.Exists(FilePath))
            {
                var fileInfo = new System.IO.FileInfo(FilePath);
                if (fileInfo.Exists)
                    FileSize = new FileSize(fileInfo.Length).ToString();
            }
        }

        [Category("FileInfo")] public string FilePath { get; set; }
        [Category("FileInfo")] public string FileSize { get; set; }

        // Name is editable for some FileNode types, such as Replicate, so allow override
        // TODO: this needs to be virtual so derived classes can assign their own attributes to it,
        // but that gives the warning "Virtual member call in constructor" from "Name = model.Name;".
        // does not throw errors but should be considered.
        // As long as derived Name has no custom logic, it should be safe.
        [Category("FileInfo")] public virtual string Name { get; set; }
    }
}
