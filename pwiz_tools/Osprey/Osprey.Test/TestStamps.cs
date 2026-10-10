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

using pwiz.Osprey.Core;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Validity stamps for tests that write an artifact only to read it back. Every artifact
    /// writer requires a stamp; these tests are not about validity, so any well-formed one does.
    /// </summary>
    internal static class TestStamps
    {
        public const string TASK = @"TestTask";
        public const string KEY = @"search=test;library=test";

        /// <summary>A stamp from this build, for task <see cref="TASK"/> and key <see cref="KEY"/>.</summary>
        public static ArtifactStamp Any => ArtifactStamp.ForCurrentBuild(TASK, KEY);
    }
}
