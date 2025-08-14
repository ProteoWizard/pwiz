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

        protected FileNode(IDocumentContainer documentContainer,
                           IdentityPath identityPath,
                           ImageId available = ImageId.file,
                           ImageId missing = ImageId.file_missing)
        {
            DocumentContainer = documentContainer;
            IdentityPath = identityPath;
            ImageAvailable = available;
            ImageMissing = missing;
        }

        internal IDocumentContainer DocumentContainer { get; }
        internal SrmDocument Document => DocumentContainer.Document;
        internal string DocumentPath => DocumentContainer.DocumentFilePath;

        public IdentityPath IdentityPath { get; }
        public virtual bool IsBackedByFile => false;
        public virtual bool RequiresSavedSkylineDocument => false;

        public abstract Immutable Immutable { get; }
        public abstract string Name { get; }
        public abstract string FilePath { get; }
        public virtual string FileName => Path.GetFileName(FilePath);

        public ImageId ImageAvailable { get; }
        public ImageId ImageMissing { get; }

        public virtual IList<FileNode> Files => ImmutableList.Empty<FileNode>();

        public override string ToString() => @"FileNode: " + (Name ?? string.Empty);

        // All implementers should override
        public virtual GlobalizedObject GetProperties()
        {
            return null;
        }

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

            // Files like .skyl / .view only exist once a Skyline document is saved.
            if (RequiresSavedSkylineDocument && !IsDocumentSavedToDisk())
                return false;

            // This model represents a file expected to be available locally and can be initialized.
            return true;
        }

        public virtual bool ModelEquals(FileNode nodeDoc)
        {
            if (nodeDoc == null) return false;
            if (GetType() != nodeDoc.GetType()) return false;

            return ReferenceEquals(Immutable, nodeDoc.Immutable);
        }

        public static bool IsDocumentSavedToDisk(string documentFilePath)
        {
            return !string.IsNullOrEmpty(documentFilePath);
        }

        internal bool IsDocumentSavedToDisk()
        {
            return IsDocumentSavedToDisk(DocumentPath);
        }

        internal static IDocumentContainer CreateDocumentContainer(SrmDocument document, string documentFilePath)
        {
            var documentContainer = new MemoryDocumentContainer
            {
                DocumentFilePath = documentFilePath
            };
            // CONSIDER: does docOriginal need to be set differently?
            documentContainer.SetDocument(document, documentContainer.Document);

            return documentContainer;
        }
    }
}
