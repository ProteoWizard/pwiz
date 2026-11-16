/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4) <noreply .at. anthropic.com>
 *
 * Based on osprey (https://github.com/MacCossLab/osprey)
 *   by Michael J. MacCoss, MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2026 University of Washington - Seattle, WA
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

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// Spectral library file format.
    /// Maps to osprey-core/src/config.rs LibrarySource variants.
    /// </summary>
    public enum LibraryFormat
    {
        DiannTsv,
        Blib,
        SkylineDocument,
        DiannParquet
    }

    /// <summary>
    /// The user-facing name of a <see cref="LibraryFormat"/>. Skyline's
    /// <c>GetLocalizedString</c> pattern.
    /// </summary>
    public static class LibraryFormatExtension
    {
        private static string[] LOCALIZED_VALUES
        {
            get
            {
                return new[]
                {
                    OspreyCoreResources.LibraryFormatExtension_LOCALIZED_VALUES_DIA_NN_TSV,
                    OspreyCoreResources.LibraryFormatExtension_LOCALIZED_VALUES_BiblioSpec,
                    OspreyCoreResources.LibraryFormatExtension_LOCALIZED_VALUES_Skyline_document,
                    OspreyCoreResources.LibraryFormatExtension_LOCALIZED_VALUES_DIA_NN_Parquet
                };
            }
        }

        public static string GetLocalizedString(this LibraryFormat val)
        {
            return LOCALIZED_VALUES[(int)val];
        }
    }

    /// <summary>
    /// Spectral library source, combining format and file path.
    /// Maps to osprey-core/src/config.rs LibrarySource.
    /// </summary>
    public class LibrarySource
    {
        public const string EXT_BLIB = @".blib";
        public const string EXT_ELIB = @".elib";
        public const string EXT_SKY = @".sky";
        public const string EXT_PARQUET = @".parquet";
        public const string EXT_SPECLIB = @".speclib";

        /// <summary>The detected or specified library format.</summary>
        public LibraryFormat Format { get; }

        /// <summary>Path to the library file.</summary>
        public string Path { get; }

        public LibrarySource(LibraryFormat format, string path)
        {
            Format = format;
            Path = path;
        }

        /// <summary>
        /// Detect library format from file extension.
        /// .blib -> Blib, .parquet -> DiannParquet, .sky -> SkylineDocument, default -> DiannTsv.
        /// An .elib path is rejected: EncyclopeDIA reading was removed, and falling through
        /// to the TSV loader would fail with a confusing parse error instead. So is a DIA-NN
        /// .speclib, whose binary format is undocumented: the message gives the DIA-NN command
        /// that converts it to the .parquet this reads.
        /// </summary>
        public static LibrarySource FromPath(string path)
        {
            string ext = (System.IO.Path.GetExtension(path) ?? string.Empty).ToLowerInvariant();
            switch (ext)
            {
                case EXT_BLIB:
                    return new LibrarySource(LibraryFormat.Blib, path);
                case EXT_ELIB:
                    throw new System.NotSupportedException(string.Format(
                        OspreyCoreResources.LibrarySource_FromPath_EncyclopeDIA__elib_spectral_libraries_are_no_longer_supported__convert_the_library_to_DIA_,
                        EXT_ELIB, TextUtil.EXT_TSV, EXT_BLIB));
                case EXT_SKY:
                    return new LibrarySource(LibraryFormat.SkylineDocument, path);
                case EXT_PARQUET:
                    return new LibrarySource(LibraryFormat.DiannParquet, path);
                case EXT_SPECLIB:
                    throw new System.NotSupportedException(string.Format(
                        OspreyCoreResources.LibrarySource_FromPath_A_DIA_NN__0__library_cannot_be_read__convert_it_to_the_DIA_NN__1__library_format_with_,
                        EXT_SPECLIB, EXT_PARQUET, SpeclibConversionCommand(path)));
                default:
                    return new LibrarySource(LibraryFormat.DiannTsv, path);
            }
        }

        /// <summary>
        /// The DIA-NN 2.x command that converts the binary library <paramref name="speclibPath"/>
        /// to the .parquet beside it, which <see cref="FromPath"/> accepts. DIA-NN's own flags,
        /// not Osprey's.
        /// </summary>
        public static string SpeclibConversionCommand(string speclibPath)
        {
            return string.Format(@"diann --lib ""{0}"" --gen-spec-lib --out-lib ""{1}""",
                speclibPath, System.IO.Path.ChangeExtension(speclibPath, EXT_PARQUET));
        }
    }
}
