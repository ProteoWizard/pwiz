/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using System.IO;

namespace pwiz.CarafeSharp.Models
{
    /// <summary>
    /// The Chronologer retention-time model files: the weights from searlelab/chronologer and the encoding
    /// from searlelab/jchronologer, committed in <c>models/chronologer-20220601193755</c> and copied next to
    /// the executable. Both are pinned by SHA-256, as a different file changes every prediction.
    /// </summary>
    public sealed class ChronologerFiles
    {
        /// <summary>
        /// The Chronologer these files are, as its weights are named. A saved Chronologer records the version it was
        /// fine-tuned from, and a newer Chronologer gets its own folder and pins.
        /// </summary>
        public const string VERSION = @"20220601193755";

        /// <summary>Where the build places the committed files, relative to the executable.</summary>
        public const string BUNDLED_RELATIVE_DIRECTORY = @"models/chronologer-" + VERSION;

        public const string WEIGHTS_FILE = @"Chronologer_" + VERSION + @".pt";
        public const string ENCODING_FILE = @"Chronologer_" + VERSION + @".preprocessing.json";

        public const string PINNED_WEIGHTS_SHA256 = @"1a500c246b49a1a23643bce7f2df86d5a107359bf0ec34365531c73431b6c0b3";
        public const string PINNED_ENCODING_SHA256 = @"ae67c1343b3b1603eedc3b1df8193dfb3c286655d9d16cbc64e790c48112f967";

        /// <summary>Environment variable naming a folder holding both files, overriding the default location.</summary>
        public const string PATH_VARIABLE = @"CARAFESHARP_CHRONOLOGER_MODEL";

        /// <summary>
        /// Opens the files in <paramref name="directory"/>, or in <see cref="DefaultDirectory"/> when it is null.
        /// </summary>
        public static ChronologerFiles Open(string directory = null)
        {
            directory = directory ?? DefaultDirectory;
            string weights = Path.Combine(directory, WEIGHTS_FILE);
            string encoding = Path.Combine(directory, ENCODING_FILE);
            CheckPinned(weights, PINNED_WEIGHTS_SHA256);
            CheckPinned(encoding, PINNED_ENCODING_SHA256);
            return new ChronologerFiles(weights, encoding);
        }

        /// <summary>
        /// <c>%CARAFESHARP_CHRONOLOGER_MODEL%</c> when it is set, else the committed copy beside the executable.
        /// A set variable wins even when it names a missing folder, so a mistyped path fails in
        /// <see cref="Open"/> instead of silently falling back.
        /// </summary>
        public static string DefaultDirectory
        {
            get
            {
                string fromEnvironment = Environment.GetEnvironmentVariable(PATH_VARIABLE);
                return !string.IsNullOrEmpty(fromEnvironment)
                    ? fromEnvironment
                    : Path.Combine(AppContext.BaseDirectory, BUNDLED_RELATIVE_DIRECTORY);
            }
        }

        private ChronologerFiles(string weightsPath, string encodingPath)
        {
            WeightsPath = weightsPath;
            EncodingPath = encodingPath;
        }

        public string WeightsPath { get; }

        public string EncodingPath { get; }

        private static void CheckPinned(string path, string pinnedSha256)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(string.Format(
                    @"Chronologer model file not found at {0}. CarafeSharp ships it in {1} beside the executable; {2} can name another folder holding the pinned files.",
                    path, BUNDLED_RELATIVE_DIRECTORY, PATH_VARIABLE), path);
            }
            string sha256 = PretrainedModels.ComputeSha256(path);
            if (!string.Equals(sha256, pinnedSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(string.Format(
                    @"{0} has SHA-256 {1}, not the pinned {2}. A different Chronologer file changes every prediction.",
                    path, sha256, pinnedSha256));
            }
        }
    }
}
