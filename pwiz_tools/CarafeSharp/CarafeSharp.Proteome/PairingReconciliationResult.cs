/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) src/main/java/db/PairingManifestReconciler.java
 *   (Result)
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

namespace pwiz.CarafeSharp.Proteome
{
    /// <summary>Summary counts of a <see cref="PairingManifestReconciler"/> run.</summary>
    public sealed class PairingReconciliationResult
    {
        public int LibraryPeptides { get; set; }
        public int GroupsIn { get; set; }
        public int GroupsKept { get; set; }
        public int GroupsDropped { get; set; }
        public int RowsIn { get; set; }
        public int RowsKept { get; set; }
        public int RowsDropped { get; set; }

        /// <summary>Library peptides with no manifest row. Expected 0; more is a warning.</summary>
        public int LibraryPeptidesNotInManifest { get; set; }

        /// <summary>Kept groups whose target survived but whose decoy did not.</summary>
        public int KeptTargetsWithoutDecoy { get; set; }
    }
}
