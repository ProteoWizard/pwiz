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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;

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
namespace pwiz.Skyline.Controls.FilesTree
{
    public enum FileState
    {
        available,
        missing,
        unknown
    }

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
        // ReSharper disable once LocalizableElement
        private static string FILE_PATH_NOT_SET = "# local path #";

        protected FileNode(SrmDocument document, string documentPath, IdentityPath identityPath,
                           ImageId available = ImageId.file, ImageId missing = ImageId.file_missing)
        {
            Document = document;
            DocumentPath = documentPath;
            IdentityPath = identityPath;
            ImageAvailable = available;
            ImageMissing = missing;

            FileState = FileState.unknown;

            LocalFilePath = FILE_PATH_NOT_SET;
        }

        // Initialize the path to a local file. This tries exactly once to find a file locally
        // matching the name of a file from the model.
        //
        // Note, this accesses the file system (possibly more than once) so should
        // not be executed on the UI thread.
        public void InitLocalFile()
        {
            if (IsBackedByFile && ReferenceEquals(LocalFilePath, FILE_PATH_NOT_SET))
            {
                LocalFilePath = LookForFileInPotentialLocations(DocumentPath, FileName);
                FileState = LocalFilePath != null ? FileState.available : FileState.missing;
            }
        }

        public SrmDocument Document { get; }
        public string DocumentPath { get; set; }
        public IdentityPath IdentityPath { get; }
        public abstract Immutable Immutable { get; }

        public abstract string Name { get; }
        public abstract string FilePath { get; }
        public virtual string FileName => Path.GetFileName(FilePath);

        public ImageId ImageAvailable { get; }
        public ImageId ImageMissing { get; }
        public FileState FileState { get; set; }

        public virtual bool IsBackedByFile => false;

        public virtual string LocalFilePath { get; private set; }

        public virtual IList<FileNode> Files => new List<FileNode>();
        public virtual bool HasFiles() => Files != null && Files.Count > 0;

        ///
        /// LOOK FOR A FILE ON DISK
        ///
        /// SkylineFiles uses this approach to locate file paths found in SrmSettings. It starts with
        /// the given path but those paths may be set on others machines. If not available locally, use
        /// <see cref="PathEx.FindExistingRelativeFile"/> to search for the file locally.
        ///
        /// <param name="relativeFilePath">Usually the SrmDocument path.</param>
        /// <param name="fileName"></param>
        ///
        /// TODO: is this the same way Skyline finds replicate sample files? Ex: Chromatogram.GetExistingDataFilePath
        internal string LookForFileInPotentialLocations(string relativeFilePath, string fileName)
        {
            if (File.Exists(fileName) || Directory.Exists(fileName))
                return fileName;
            else
                return PathEx.FindExistingRelativeFile(relativeFilePath, fileName);
        }
    }
}
