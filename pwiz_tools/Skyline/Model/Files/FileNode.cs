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
        folder_missing,
        file,
        file_missing,
        replicate,
        replicate_missing,
        replicate_sample_file,
        peptide_library,
        skyline,
        audit_log,
        cache_file,
        view_file
    }

    internal class StaticFolderId : Identity { }

    public abstract class FileNode
    {
        public enum MoveType { move_to, move_last }

        protected FileNode(string documentFilePath, IdentityPath identityPath)
        {
            IdentityPath = identityPath;
            DocumentPath = documentFilePath;
        }

        internal string DocumentPath { get; }

        public IdentityPath IdentityPath { get; }
        public virtual bool IsBackedByFile => false;
        public virtual bool RequiresSavedDocument => false;

        public abstract string Name { get; }
        public abstract string FilePath { get; }
        public virtual string FileName => Path.GetFileName(FilePath);

        public virtual ImageId ImageAvailable => ImageId.file; 
        public virtual ImageId ImageMissing => ImageId.file_missing;

        public virtual IList<FileNode> Files => ImmutableList.Empty<FileNode>();

        public override string ToString() => @$"{GetType().Name}: " + (Name ?? string.Empty);

        /// <summary>
        /// Use this to decide whether the file represented by this model is ready to be monitored. A model may not be ready if:
        ///     (1) it does not represent a local file
        ///     OR
        ///     (2) it represents a local file but is not ready for initialization. Example: a file may exist in memory, even
        ///         displayed in UI  (FilesTree) but is not written to disk yet. So attempts to monitor will fail until the
        ///         file is written to disk. This happens for a newly created Skyline document and associated .skyl/.view
        ///         files until the Skyline document is saved for the first time.
        /// </summary>
        /// <returns>true if ready to initialize the local file, false otherwise</returns>
        public bool ShouldInitializeLocalFile()
        {
            // Nothing to initialize if not backed by a file.
            if(!IsBackedByFile) 
                return false;

            // Files like .skyl / .view are only saved to disk once a Skyline document is saved.
            if (RequiresSavedDocument && !IsDocumentSaved())
                return false;

            // This model represents a file expected to be available locally and can be initialized.
            return true;
        }

        // If DocumentFilePath is null, document is not saved to disk
        public static bool IsDocumentSaved(string documentFilePath)
        {
            return !string.IsNullOrEmpty(documentFilePath);
        }

        internal bool IsDocumentSaved()
        {
            return IsDocumentSaved(DocumentPath);
        }
    }
}
