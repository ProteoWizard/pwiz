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

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// The <c>--task</c> values, which are also the task names each artifact's validity stamp
    /// records. The tasks themselves live in Osprey.Tasks, which Osprey.IO cannot
    /// reference, so each task takes its name from these constants and a message in any
    /// assembly that tells the user which task to run, or which files to delete, names it
    /// exactly as the parser accepts it. Messages pass these as format arguments: a user
    /// types them, so they must never be inside translated text.
    /// </summary>
    public static class OspreyTaskNames
    {
        public const string SPECTRA_CACHE = @"SpectraCache";
        public const string PER_FILE_SCORING = @"PerFileScoring";
        public const string FIRST_PASS_FDR = @"FirstPassFDR";
        public const string PER_FILE_RESCORING = @"PerFileRescoring";
        public const string SECOND_PASS_FDR = @"SecondPassFDR";
        public const string MODEL_DIAGNOSTICS = @"ModelDiagnostics";
    }
}
