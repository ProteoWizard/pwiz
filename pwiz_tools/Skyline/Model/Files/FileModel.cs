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
using pwiz.Skyline.Model.DocSettings;

/*
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

    File type reference, shared when people ask about Skyline files.
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
        view_file,
        prot_db,
        ims_db,
        opt_db,
        irt_calculator
    }

    internal class StaticFolderId : Identity { }

    /// <summary>
    /// Interface for FileModel types that support renaming through the FilesTree UI.
    /// </summary>
    public interface IFileRenameable
    {
        /// <summary>
        /// Validates whether the new name is acceptable.
        /// </summary>
        /// <param name="document">The current document</param>
        /// <param name="newName">The proposed new name</param>
        /// <param name="errorMessage">Error message to display if validation fails</param>
        /// <returns>true if the name is valid, false otherwise</returns>
        bool ValidateNewName(SrmDocument document, string newName, out string errorMessage);

        /// <summary>
        /// Performs the rename operation.
        /// </summary>
        /// <param name="document">The current document</param>
        /// <param name="monitor">Progress monitor for the operation</param>
        /// <param name="newName">The new name to apply</param>
        /// <returns>Modified document with the rename applied</returns>
        ModifiedDocument PerformRename(SrmDocument document, SrmSettingsChangeMonitor monitor, string newName);

        /// <summary>
        /// Gets the audit log message resource string for this rename operation.
        /// </summary>
        string AuditLogMessageResource { get; }
    }

    public abstract class FileModel
    {
        public enum MoveType { move_to, move_last }

        /// <summary>
        /// Format string for combining a file type description with its resource name.
        /// Example: "Background Proteome" + "Rat mini" → "Background Proteome - Rat mini"
        /// This creates a display name that shows both what type of resource it is and its specific name.
        /// </summary>
        public const string FOLDER_FILE_NAME_FORMAT = "{0} - {1}";

        protected FileModel(string documentFilePath, IdentityPath identityPath)
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

        /// <summary>
        /// Gets the file type text to display as a prefix (e.g., "Background Proteome", "iRT Calculator").
        /// Override in derived classes to provide the type-specific text.
        /// Returns empty string by default for files without a type prefix.
        /// </summary>
        protected virtual string FileTypeText => string.Empty;

        /// <summary>
        /// Gets whether to show file names instead of resource names in the display text.
        /// By default, uses the global SkylineFile.ShowFileNames setting.
        /// Can be overridden for custom behavior.
        /// </summary>
        protected bool ShowFileName => SkylineFile.ShowFileNames;

        /// <summary>
        /// Computes the display text for a file model from its components.
        /// This static method allows tests to verify expected display text without depending on instance state.
        /// </summary>
        /// <param name="fileTypeText">The file type prefix (e.g., "Background Proteome"), or empty string for no prefix</param>
        /// <param name="name">The resource/friendly name (e.g., "Rat mini")</param>
        /// <param name="filePath">The full file path (e.g., "C:\data\Rat_mini.protdb")</param>
        /// <param name="showFileName">true to show file name from path, false to show resource name</param>
        /// <returns>The formatted display text</returns>
        public static string GetDisplayText(string fileTypeText, string name, string filePath, bool showFileName)
        {
            var displayName = showFileName ? Path.GetFileName(filePath) : name;
            // If type text is empty, just return the display name
            if (string.IsNullOrEmpty(fileTypeText))
                return string.IsNullOrEmpty(displayName) ? name : displayName;

            // If display name is empty, just return the type prefix alone
            return string.IsNullOrEmpty(displayName)
                ? fileTypeText
                : string.Format(FOLDER_FILE_NAME_FORMAT, fileTypeText, displayName);
        }

        /// <summary>
        /// Gets the formatted display text for this file model shown in the tree view.
        /// Combines FileTypeText with either the resource name or file name based on ShowFileName setting.
        /// Can be overridden for custom formatting behavior.
        /// </summary>
        public string DisplayText => GetDisplayText(FileTypeText, Name, FilePath, ShowFileName);

        public virtual ImageId ImageAvailable => ImageId.file;
        public virtual ImageId ImageMissing => ImageId.file_missing;

        public virtual IList<FileModel> Files => ImmutableList.Empty<FileModel>();

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
