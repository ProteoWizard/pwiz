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

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;
using pwiz.Osprey.Tasks;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// One task's config, built the way <c>Program.Main</c> builds it: the task looked up by
    /// name in a task set and SELECTED together with the pipeline it runs, through the same
    /// <see cref="OspreyConfig.SelectTask"/> the CLI goes through. Three test classes each
    /// used to hand-copy Main's flag assignments, and one of the copies named a task Main did
    /// not, building a config the CLI cannot produce; a config that stands for a task is
    /// built here or not at all.
    /// </summary>
    internal static class TaskConfigs
    {
        /// <summary>
        /// A config selecting the named task from a fresh task set. The pipeline it runs
        /// travels on the config, so a test that needs a <see cref="PipelineContext"/> over
        /// the same instances builds it with <see cref="ContextFor"/>.
        /// </summary>
        public static OspreyConfig ForTask(string taskName)
        {
            return ForTask(OspreyTasks.Create(), taskName);
        }

        public static OspreyConfig ForTask(OspreyTasks tasks, string taskName)
        {
            var task = tasks.FindByName(taskName);
            Assert.IsNotNull(task, string.Format(@"no task named '{0}'", taskName));
            var config = new OspreyConfig();
            config.SelectTask(task, tasks.PipelineFor(task));
            return config;
        }

        /// <summary>
        /// A no-selection config over the canonical pipeline: the straight-through run.
        /// </summary>
        public static OspreyConfig StraightThrough()
        {
            var config = new OspreyConfig();
            config.SelectTask(null, OspreyTasks.Create().Pipeline);
            return config;
        }

        /// <summary>
        /// A pipeline context over the stages <paramref name="config"/> carries - the
        /// instances its selection was resolved against, so a stage asking "am I the
        /// selection?" by reference gets the run's answer. A bare config that never went
        /// through <see cref="OspreyConfig.SelectTask"/> gets a fresh canonical pipeline.
        /// </summary>
        public static PipelineContext ContextFor(OspreyConfig config)
        {
            var stages = config.Pipeline?.Cast<OspreyTask>() ?? OspreyTasks.Create().Pipeline;
            return new PipelineContext(config, stages, null, null, null);
        }
    }
}
