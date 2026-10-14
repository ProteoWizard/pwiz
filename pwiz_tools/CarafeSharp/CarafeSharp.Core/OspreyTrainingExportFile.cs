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

namespace pwiz.CarafeSharp.Core
{
    /// <summary>
    /// The name of Osprey's training export, <c>&lt;stem&gt;.training.parquet</c>, which the
    /// command line recognizes (CarafeSharp.Proteome) and the reader opens (CarafeSharp.IO).
    /// Declared here, below both, so the two cannot disagree.
    /// </summary>
    public static class OspreyTrainingExportFile
    {
        public const string SUFFIX = @".training.parquet";

        /// <summary>True when <paramref name="path"/> names a training export by its suffix.</summary>
        public static bool IsExport(string path)
        {
            return path.EndsWith(SUFFIX, StringComparison.OrdinalIgnoreCase);
        }
    }
}
