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

namespace pwiz.OspreySharp.Core
{
    /// <summary>
    /// Spectral library file format.
    /// Maps to osprey-core/src/config.rs LibrarySource variants.
    /// </summary>
    public enum LibraryFormat
    {
        DiannTsv,
        Blib,
        Elib,
        SkylineDocument
    }

    /// <summary>
    /// Spectral library source, combining format and file path.
    /// Maps to osprey-core/src/config.rs LibrarySource.
    /// </summary>
    public class LibrarySource
    {
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
        /// .blib -> Blib, .elib -> Elib, .sky -> SkylineDocument, default -> DiannTsv.
        /// </summary>
        public static LibrarySource FromPath(string path)
        {
            string ext = (System.IO.Path.GetExtension(path) ?? string.Empty).ToLowerInvariant();
            switch (ext)
            {
                case ".blib":
                    return new LibrarySource(LibraryFormat.Blib, path);
                case ".elib":
                    return new LibrarySource(LibraryFormat.Elib, path);
                case ".sky":
                    return new LibrarySource(LibraryFormat.SkylineDocument, path);
                default:
                    return new LibrarySource(LibraryFormat.DiannTsv, path);
            }
        }
    }
}
