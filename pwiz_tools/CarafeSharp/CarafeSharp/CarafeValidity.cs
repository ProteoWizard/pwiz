/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using pwiz.CarafeSharp.Proteome;
using pwiz.Common.SystemUtil;

namespace pwiz.CarafeSharp
{
    /// <summary>
    /// The validity key of a CarafeSharp task: a hash of its settings and the identity (name,
    /// length, last write) of every file it reads, so an output is reused only when nothing
    /// that went into it has changed. A task computes it once, before any setting is touched
    /// by the run, and hands the resulting stamp to the writers.
    /// </summary>
    internal static class CarafeValidity
    {
        public static string Key(string settingsName, object settings, IEnumerable<string> inputs)
        {
            var key = new StringBuilder();
            key.Append(settingsName).Append('=').Append(Sha256(JsonSerializer.Serialize(settings, settings.GetType())));
            foreach (string input in inputs)
            {
                if (!string.IsNullOrEmpty(input))
                    key.Append(';').Append(Identity(input));
            }
            return key.ToString();
        }

        /// <summary>
        /// What distinguishes one version of a file from another without reading it: its name,
        /// length and last write time; for a folder, the same for each file directly in it except
        /// what CarafeSharp writes there itself (a library, a saved model, a temp file), since a run
        /// commonly predicts into the folder it reads its models from and must not change its own key.
        /// </summary>
        private static string Identity(string path)
        {
            if (string.IsNullOrEmpty(path))
                return @"none";
            if (Directory.Exists(path))
            {
                var files = Directory.GetFiles(path);
                Array.Sort(files, StringComparer.Ordinal);
                var identities = new StringBuilder(new DirectoryInfo(path).Name).Append('[');
                foreach (string file in files)
                {
                    if (!IsOwnOutput(file))
                        identities.Append(Identity(file)).Append(',');
                }
                return identities.Append(']').ToString();
            }
            var info = new FileInfo(path);
            if (!info.Exists)
                return info.Name + @":missing";
            return info.Name + @":" + info.Length.ToString(CultureInfo.InvariantCulture) + @":" +
                   info.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture);
        }

        private static bool IsOwnOutput(string file)
        {
            string name = Path.GetFileName(file);
            return name.StartsWith(SimpleFileSaver.TEMP_PREFIX, StringComparison.Ordinal) ||
                   name.EndsWith(@".blib", StringComparison.OrdinalIgnoreCase) ||
                   name.EndsWith(@".tsv", StringComparison.OrdinalIgnoreCase) ||
                   name.EndsWith(CarafeModelFile.EXTENSION, StringComparison.OrdinalIgnoreCase);
        }

        private static string Sha256(string text)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
        }
    }
}
