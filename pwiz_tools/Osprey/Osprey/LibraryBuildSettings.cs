/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
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

using pwiz.CarafeSharp.Proteome;

namespace pwiz.Osprey
{
    /// <summary>
    /// What the command line says the one command (<c>--fasta</c>) predicts and tunes, as
    /// CarafeSharp's settings, which the config in Osprey.Core cannot hold: the executable's
    /// workflow takes them beside the config.
    /// </summary>
    internal sealed class LibraryBuildSettings
    {
        public LibraryBuildSettings(LibrarySettings library, TrainingSettings training)
        {
            Library = library;
            Training = training;
        }

        /// <summary>
        /// The library predicted with the pretrained models or <c>--model</c>: the one the output is
        /// searched with, or with <c>--training-input</c> the one the tuning runs are searched with.
        /// </summary>
        public LibrarySettings Library { get; }

        /// <summary>
        /// The tuning on the <c>--training-input</c> runs' exports, whose <see cref="TrainingSettings.Library"/>
        /// is the library the output is searched with; null without <c>--training-input</c>.
        /// </summary>
        public TrainingSettings Training { get; }
    }
}
