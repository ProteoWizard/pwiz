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
        replicates,                 // virtual file - replicate from .sky
        replicate_file,             // .raw, .mzml, .wiff, more (see @DataSourceUtil)
        retention_score_calculator, // .irtdb
        sky,                        // .sky    
        sky_audit_log,              // .skyl
        sky_chromatogram_cache,     // .skyd
        sky_view                    // .sky.view
    }

    public interface IFileBase
    {
        Identity Id { get; }
        
        FileType Type { get; }

        string Name { get; }
    }

    public interface IFileGroupModel : IFileBase
    {
        IEnumerable<IFileModel> Files { get; }
    }

    public interface IFileModel : IFileBase
    {
        string FilePath { get; }
    }
}