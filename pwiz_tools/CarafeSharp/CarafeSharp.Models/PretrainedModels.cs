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
using System.Security.Cryptography;

namespace pwiz.CarafeSharp.Models
{
    /// <summary>
    /// The AlphaPeptDeep pretrained weights, <c>pretrained_models.zip</c> from the MannLabs
    /// "pre-trained-models" release. That release URL is unversioned, which is how two
    /// Carafe runs came to predict from different weights, so CarafeSharp pins the exact
    /// archive by SHA-256 and refuses any other unless told to accept it.
    /// </summary>
    public sealed class PretrainedModels
    {
        public const string DOWNLOAD_URL = @"https://github.com/MannLabs/alphapeptdeep/releases/download/pre-trained-models/pretrained_models.zip";

        /// <summary>The v1 archive Carafe uses (25,614,761 bytes, generic/ms2.pth dated 2022-10-28).</summary>
        public const string PINNED_SHA256 = @"75e6037db3280a513d0f6010a21dba4e8ea47a8d67127f38c77fb1f9a7d408eb";

        public const string MS2_ENTRY = @"generic/ms2.pth";
        public const string RT_ENTRY = @"generic/rt.pth";
        public const string CCS_ENTRY = @"generic/ccs.pth";

        /// <summary>Environment variable naming the archive, overriding the default location.</summary>
        public const string PATH_VARIABLE = @"CARAFESHARP_PRETRAINED_MODELS";

        /// <summary>
        /// Opens the archive at <paramref name="zipPath"/>, or at the default location when it is
        /// null: <c>%CARAFESHARP_PRETRAINED_MODELS%</c>, else peptdeep's own
        /// <c>~/peptdeep/pretrained_models/pretrained_models.zip</c>, which Carafe shares.
        /// </summary>
        public static PretrainedModels Open(string zipPath = null, bool allowUnpinned = false)
        {
            zipPath = zipPath ?? DefaultPath;
            if (!File.Exists(zipPath))
            {
                throw new FileNotFoundException(string.Format(
                    @"AlphaPeptDeep pretrained models not found at {0}. Download {1} there, or pass its location.",
                    zipPath, DOWNLOAD_URL), zipPath);
            }
            string sha256 = ComputeSha256(zipPath);
            if (!allowUnpinned && !string.Equals(sha256, PINNED_SHA256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(string.Format(
                    @"{0} has SHA-256 {1}, not the pinned {2}. A different pretrained archive changes every prediction.",
                    zipPath, sha256, PINNED_SHA256));
            }
            return new PretrainedModels(zipPath, sha256);
        }

        public static string DefaultPath
        {
            get
            {
                string fromEnvironment = Environment.GetEnvironmentVariable(PATH_VARIABLE);
                if (!string.IsNullOrEmpty(fromEnvironment))
                    return fromEnvironment;
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    @"peptdeep", @"pretrained_models", @"pretrained_models.zip");
            }
        }

        private PretrainedModels(string zipPath, string sha256)
        {
            ZipPath = zipPath;
            Sha256 = sha256;
        }

        public string ZipPath { get; }

        public string Sha256 { get; }

        private static string ComputeSha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
        }
    }
}
