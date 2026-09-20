/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
 *
 * Based on osprey (https://github.com/MacCossLab/osprey)
 *   by Michael J. MacCoss, MacCoss Lab, Department of Genome Sciences, UW
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;
using pwiz.Osprey.Tasks;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// One task's config, built the way <c>Program.Main</c> builds it: the task looked up by
    /// name in the run's task list and SELECTED, so it sets the flags it implies through the
    /// same <see cref="OspreyConfig.SelectTask"/> the CLI goes through. Three test classes
    /// each used to hand-copy Main's flag assignments, and one of the copies named a task
    /// Main did not, building a config the CLI cannot produce; deriving the flags from the
    /// task here means a test cannot pin a membership no real run has.
    /// </summary>
    internal static class TaskConfigs
    {
        /// <summary>
        /// A config selecting the named task from a fresh task list. For a test that also
        /// builds a pipeline, use the overload that takes the list, so the selection and the
        /// pipeline share instances the way a run does.
        /// </summary>
        public static OspreyConfig ForTask(string taskName)
        {
            return ForTask(OspreyTasks.CreateAll(), taskName);
        }

        public static OspreyConfig ForTask(IReadOnlyList<OspreyTask> allTasks, string taskName)
        {
            var task = OspreyTasks.FindByName(allTasks, taskName);
            Assert.IsNotNull(task, string.Format(@"no task named '{0}'", taskName));
            var config = new OspreyConfig();
            config.SelectTask(task);
            return config;
        }
    }
}
