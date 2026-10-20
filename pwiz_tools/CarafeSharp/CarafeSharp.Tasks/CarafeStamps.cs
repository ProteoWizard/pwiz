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
using System.IO;
using pwiz.BiblioSpec.IO;
using pwiz.CarafeSharp.Proteome;
using pwiz.Common.Pipeline;

namespace pwiz.CarafeSharp.Tasks
{
    /// <summary>
    /// Reads the <see cref="ArtifactStamp"/> inside the artifacts CarafeSharp writes, whatever
    /// the format: a saved model's manifest (<see cref="CarafeModelFile.ReadStamp"/>) and a
    /// library's metadata table (the slot Osprey's blib writer uses too). The TSV library and
    /// the FASTA have no slot a reader of those formats would tolerate, so they carry none, and
    /// the tasks that write only those declare no outputs. Never throws: "I cannot tell"
    /// resolves to recomputing the artifact.
    /// </summary>
    internal static class CarafeStamps
    {
        private const string BLIB_EXT = @".blib";

        public static ArtifactStamp Read(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return null;
            if (path.EndsWith(CarafeModelFile.EXTENSION, StringComparison.OrdinalIgnoreCase))
                return CarafeModelFile.ReadStamp(path);
            if (path.EndsWith(BLIB_EXT, StringComparison.OrdinalIgnoreCase))
                return ReadBlib(path);
            return null;
        }

        private static ArtifactStamp ReadBlib(string path)
        {
            // The one blib writer's metadata reader never throws, and reads the table's old name too.
            return ArtifactStamp.Parse(BlibMetadata.Read(path, ArtifactStamp.PARQUET_KEY));
        }
    }
}
