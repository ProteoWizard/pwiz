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

namespace pwiz.Osprey.Tasks
{
    internal static class BlockReadLog
    {
        /// <summary>
        /// Logs the experimental block-read totals (OSPREY_BLOCK_READ_MB) at a phase boundary;
        /// silent when block reads are off.
        /// </summary>
        public static void LogBlockReads(this PipelineContext ctx, string phase)
        {
            string text = BlockReadStats.Text();
            if (text != null)
                ctx.LogInfo(LogTag.PATH, @"{0}: {1}", phase, text);
        }
    }
}
