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
using pwiz.Skyline.Util;

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

    class StaticFolderId : Identity { }

    public interface IFileModel
    {
        Identity Id { get; }
        FileType Type { get; }
        string Name { get; }

        string FilePath { get; }

        IList<IFileModel> Files { get; }
    }

    public interface IFileProvider
    {
        IList<IFileModel> Files { get; }
    }

    public class FolderModel : IFileModel
    {
        public FolderModel(Identity id, FileType type, IFileModel file) :
            this(id, type, new SingletonList<IFileModel>(file)) { }

        public FolderModel(Identity id, FileType type, IList<IFileModel> files)
        {
            Id = id;
            Type = type;
            Files = ImmutableList.ValueOf(files);
        }

        public Identity Id { get; }
        public FileType Type { get; }
        public string Name => string.Empty;
        public string FilePath => string.Empty;

        public IList<IFileModel> Files { get; }

        public bool HasFiles()
        {
            return Files != null && Files.Count > 0;
        }
    }
}