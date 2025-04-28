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

using System.Collections.Generic;
using System.IO;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

/*
  (1) How to check whether a file exists in a separate thread? Where to store file states?
        Look at ProducerConsumerWorker

    * rat-plasma.sky
            * Replicates\
                    * Replicate 1\
                        * Sample File 1
                        * Sample File 2
                    * Replicate 2\
                        * Sample File 1
            * Spectral Libraries\
                    * Library NIST 1
                    * Library GPM 2
            * Background Proteome\
                    * Foo.protdb
            * Project Files\
                    * Audit Log
                    * View
                    * Chromatogram Cache

    File type reference, which Brendan shares with people using Skyline.
        https://skyline.ms/wiki/home/software/Skyline/page.view?name=file-types

 */
// ReSharper disable WrongIndentSize
namespace pwiz.Skyline.Model.Files
{
    public enum ImageId
    {
        blank,
        folder,
        file,
        file_missing,
        replicate,
        replicate_sample_file,
        peptide,
        skyline,
        audit_log,
        cache_file,
        view_file
    }

    internal class StaticFolderId : Identity { }

    public abstract class FileNode
    {
        protected FileNode(SrmDocument document, string documentPath, IdentityPath identityPath,
                           ImageId available = ImageId.file, ImageId missing = ImageId.file_missing)
        {
            Document = document;
            DocumentPath = documentPath;
            IdentityPath = identityPath;
            ImageAvailable = available;
            ImageMissing = missing;
        }

        public SrmDocument Document { get; }
        public string DocumentPath { get; }

        public IdentityPath IdentityPath { get; }
        public virtual bool IsBackedByFile => false;
        public abstract Immutable Immutable { get; }
        public abstract string Name { get; }
        public abstract string FilePath { get; }
        public virtual string FileName => Path.GetFileName(FilePath);

        public ImageId ImageAvailable { get; }
        public ImageId ImageMissing { get; }

        public virtual IList<FileNode> Files => ImmutableList.Empty<FileNode>();

        internal bool IsDocumentSavedToDisk()
        {
            return !string.IsNullOrEmpty(DocumentPath);
        }

        // ReSharper disable once LocalizableElement
        public override string ToString() => "FileNode: " + (Name ?? string.Empty);
    }
}
