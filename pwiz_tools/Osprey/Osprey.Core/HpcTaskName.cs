/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// The one spelling of each <see cref="HpcTask"/>'s name. That spelling is the
    /// <c>--task</c> token, the task's <c>Name</c> in <c>[TASK]</c> log lines, the key the
    /// validity sidecar stamps into a <c>.osprey.task</c> file, and the label the tests
    /// assert on - four readers of one contract, so it cannot live in any one of them.
    /// Not <c>HpcTask.ToString()</c>: the members keep C# casing (<c>FirstPassFdr</c>) where
    /// the name keeps the acronym (<c>FirstPassFDR</c>), and <c>PerFileRescore</c> is named
    /// <c>PerFileRescoring</c>.
    /// </summary>
    public static class HpcTaskName
    {
        public const string SPECTRA_CACHE = "SpectraCache";
        public const string PER_FILE_SCORING = "PerFileScoring";
        public const string FIRST_PASS_FDR = "FirstPassFDR";
        public const string PER_FILE_RESCORING = "PerFileRescoring";
        public const string SECOND_PASS_FDR = "SecondPassFDR";
        public const string MODEL_DIAGNOSTICS = "ModelDiagnostics";

        /// <summary>Every name, in pipeline order - the order <c>--help</c> lists them.</summary>
        public static readonly string[] ALL =
        {
            SPECTRA_CACHE, PER_FILE_SCORING, FIRST_PASS_FDR, PER_FILE_RESCORING, SECOND_PASS_FDR, MODEL_DIAGNOSTICS
        };

        public static string Of(HpcTask task)
        {
            switch (task)
            {
                case HpcTask.SpectraCache: return SPECTRA_CACHE;
                case HpcTask.PerFileScoring: return PER_FILE_SCORING;
                case HpcTask.FirstPassFdr: return FIRST_PASS_FDR;
                case HpcTask.PerFileRescore: return PER_FILE_RESCORING;
                case HpcTask.SecondPassFdr: return SECOND_PASS_FDR;
                case HpcTask.ModelDiagnostics: return MODEL_DIAGNOSTICS;
                default: throw new ArgumentOutOfRangeException(nameof(task), task, null);
            }
        }

        /// <summary>
        /// The task for a <c>--task</c> token, matched case-insensitively as the selector
        /// always has been (<c>--task firstpassfdr</c> selects <see cref="HpcTask.FirstPassFdr"/>).
        /// </summary>
        public static bool TryParse(string name, out HpcTask task)
        {
            foreach (HpcTask candidate in Enum.GetValues(typeof(HpcTask)))
            {
                if (string.Equals(name, Of(candidate), StringComparison.OrdinalIgnoreCase))
                {
                    task = candidate;
                    return true;
                }
            }
            task = default;
            return false;
        }
    }
}
