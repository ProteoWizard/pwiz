/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) main.java.ai.AIGear
 *   (generate_spectral_library(Map), generate_spectral_library_parquet)
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

using System;

namespace pwiz.CarafeSharp.Proteome
{
    /// <summary>
    /// The library files an <c>-lf_type</c> asks for, decided as Carafe decides it. With
    /// <c>-fast</c>: a value containing <c>skyline</c> writes a .blib and, when it is a comma
    /// list, a TSV for each other element (each overwriting the last, so the last one wins);
    /// <c>mzSpecLib</c> is not ported; anything else writes a TSV in that notation. Without
    /// <c>-fast</c> Carafe always writes a TSV, in the DIA-NN or EncyclopeDIA notation or else
    /// the generic one, so <c>Skyline</c> gives a generic-notation TSV and no .blib. CarafeSharp
    /// adds <c>blib</c>, read like <c>Skyline</c> with <c>-fast</c> but with or without it.
    /// </summary>
    public sealed class LibraryOutputs
    {
        public const string BLIB_FORMAT = @"blib";
        public const string SKYLINE_FORMAT = @"Skyline";

        public static LibraryOutputs FromFormat(string libraryFormat, bool fast)
        {
            var outputs = new LibraryOutputs();
            bool blib = libraryFormat.IndexOf(BLIB_FORMAT, StringComparison.OrdinalIgnoreCase) >= 0;
            if (!fast && !blib)
            {
                outputs.SetTsv(libraryFormat);
                if (ContainsSkyline(libraryFormat))
                    outputs.Warning = @"WARNING: without -fast Carafe writes -lf_type " + libraryFormat + @" as a generic TSV, not a .blib.";
                return outputs;
            }
            if (blib || ContainsSkyline(libraryFormat))
            {
                outputs.WritesBlib = true;
                if (libraryFormat.IndexOf(',') >= 0)
                {
                    foreach (string format in libraryFormat.Split(','))
                    {
                        if (!string.Equals(format, SKYLINE_FORMAT, StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(format, BLIB_FORMAT, StringComparison.OrdinalIgnoreCase))
                        {
                            outputs.SetTsv(format);
                        }
                    }
                }
                return outputs;
            }
            if (string.Equals(libraryFormat, @"mzSpecLib", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException(@"-lf_type mzSpecLib is not supported by CarafeSharp");
            outputs.SetTsv(libraryFormat);
            return outputs;
        }

        private LibraryOutputs()
        {
        }

        public bool WritesTsv { get; private set; }

        /// <summary>The TSV ModifiedPeptide notation, when <see cref="WritesTsv"/>.</summary>
        public ModifiedPeptideStyle TsvStyle { get; private set; }

        public bool WritesBlib { get; private set; }

        /// <summary>A warning to print about how the format was read, or null.</summary>
        public string Warning { get; private set; }

        private void SetTsv(string format)
        {
            WritesTsv = true;
            TsvStyle = ModifiedPeptideNotation.GetStyle(format);
        }

        private static bool ContainsSkyline(string format)
        {
            return format.IndexOf(@"skyline", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
