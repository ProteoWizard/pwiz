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
using pwiz.Common.Collections;

namespace pwiz.Skyline.Model.DocSettings
{
    /// <summary>
    /// Enum representing Skyline file types.
    /// https://skyline.ms/wiki/home/software/Skyline/page.view?name=file-types
    /// </summary>
    public enum FileType
    {
        background_proteome,        // .protdb
        ion_mobility_library,       // .imsdb
        optimization_library,       // .optdb
        peptide_library,            // library type (cache type, if any) - .blib (.slc), .lib, .clib, .hlf (.slc), .msp (.slc), .sptxt (.splc), .elib (.elibc)
        replicate,                  // virtual file containing replicate_sample_file(s)
        replicate_sample,           // .raw, .mzml, .wiff, more (see @DataSourceUtil)
        retention_score_calculator, // .irtdb
        sky,                        // .sky    
        sky_audit_log,              // .skyl
        sky_chromatogram_cache,     // .skyd
        sky_view,                   // .sky.view

        folder,
        folder_replicates,
        folder_peptide_libraries,
        folder_background_proteome,
        folder_retention_score_calculator,
        folder_ion_mobility_library,
        folder_optimization_library,
        folder_project_files
    }

    public interface IFileBase
    {
        Identity Id { get; }
        FileType Type { get; }
        string Name { get; }
    }

    public interface IFileModel : IFileBase
    {
        string FilePath { get; }
    }

    // CONSIDER: separating folder type from the FileType enum
    public interface IFileGroupModel : IFileBase
    {
        IList<IFileModel> Files { get; }
        IList<IFileGroupModel> Folders { get; }
        IList<IFileBase> FilesAndFolders { get; }
    }

    public interface IFileProvider
    {
        IDictionary<FileType, IFileGroupModel> Files { get; }
    }

    // TODO: build incrementally with a factory to simplify ensuring immutability and non-null files / folders
    public class BasicFileGroupModel : IFileGroupModel
    {
        private class BasicFileGroupModelId : Identity { }

        public BasicFileGroupModel(FileType type, string name, IList<IFileGroupModel> folders) : this(type, name, null, folders) { }

        public BasicFileGroupModel(FileType type, string name, IList<IFileModel> files) : this(type, name, files, null) { }

        public BasicFileGroupModel(FileType type, string name, IFileModel file) : this(type, name, ImmutableList.Singleton(file), null) { }

        public BasicFileGroupModel(FileType type, string name, IList<IFileModel> files, IList<IFileGroupModel> folders) {
            Id = new BasicFileGroupModelId();

            Type = type;
            Name = name;
            Files = ImmutableList.ValueOf(files);
            Folders = ImmutableList.ValueOf(folders);

            FilesAndFolders = new List<IFileBase>();

            if (Files != null)
                FilesAndFolders.AddRange(Files);
            
            if (Folders != null)
                FilesAndFolders.AddRange(Folders);
            
            FilesAndFolders = ImmutableList.ValueOf(FilesAndFolders);
        }

        public Identity Id { get; }
        public FileType Type { get; }
        public string Name { get; }
        public IList<IFileModel> Files { get; }
        public IList<IFileGroupModel> Folders { get; }
        public IList<IFileBase> FilesAndFolders { get; }

        public bool HasFiles()
        {
            return Files != null && Files.Count > 0;
        }

        public bool HasFolders()
        {
            return Folders != null && Folders.Count > 0;
        }

        public bool HasFilesOrFolders()
        {
            return HasFiles() || HasFolders();
        }
    }
}