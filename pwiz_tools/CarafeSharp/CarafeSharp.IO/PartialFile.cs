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

using System.IO;

namespace pwiz.CarafeSharp.IO
{
    /// <summary>
    /// An output file written under a sibling temporary name (<see cref="SUFFIX"/> appended)
    /// and moved over its final name only by <see cref="Commit"/>, so a run that fails or is
    /// stopped part way leaves any earlier file at the final name as it was, not a truncated
    /// one that looks complete.
    /// </summary>
    public sealed class PartialFile
    {
        public const string SUFFIX = @".partial";

        private bool _committed;

        /// <summary>Prepares to write <paramref name="path"/>, removing a partial file left by an earlier run.</summary>
        public PartialFile(string path)
        {
            FinalPath = path;
            PartialPath = path + SUFFIX;
            if (File.Exists(PartialPath))
                File.Delete(PartialPath);
        }

        /// <summary>The name the file has once committed.</summary>
        public string FinalPath { get; }

        /// <summary>The name the file is written under.</summary>
        public string PartialPath { get; }

        /// <summary>Replaces any file at <see cref="FinalPath"/> with the finished partial file.</summary>
        public void Commit()
        {
            File.Move(PartialPath, FinalPath, true);
            _committed = true;
        }

        /// <summary>Deletes the partial file unless it was committed.</summary>
        public void Discard()
        {
            if (!_committed && File.Exists(PartialPath))
                File.Delete(PartialPath);
        }
    }
}
