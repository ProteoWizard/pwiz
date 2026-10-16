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

using System.Collections.Generic;

namespace pwiz.CarafeSharp.Training
{
    /// <summary>The training exports <see cref="TrainingExportLocator.Find(string, IReadOnlyList{string})"/> chose, and the runs they are for.</summary>
    public sealed class TrainingExportSelection
    {
        public TrainingExportSelection(IReadOnlyList<string> exports, IReadOnlyDictionary<string, string> runs, IReadOnlyList<string> warnings)
        {
            Exports = exports;
            Runs = runs;
            Warnings = warnings;
        }

        /// <summary>The export files, in ordinal path order.</summary>
        public IReadOnlyList<string> Exports { get; }

        /// <summary>
        /// The runs <c>-ms</c> named, by file stem, each with the path Carafe keys its meta.json
        /// entry by: the <c>-ms</c> entry as typed, or for a run found in an <c>-ms</c> folder,
        /// that folder as typed, a directory separator and the run's file name. Empty without
        /// <c>-ms</c>.
        /// </summary>
        public IReadOnlyDictionary<string, string> Runs { get; }

        /// <summary>Exports of different Osprey searches named by different <c>-i</c> inputs.</summary>
        public IReadOnlyList<string> Warnings { get; }
    }
}
