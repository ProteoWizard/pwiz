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

using System;
using pwiz.Osprey.Core;

namespace pwiz.Osprey
{
    /// <summary>
    /// The developer stop of <see cref="OspreyEnvironment.StopAfterTasks"/>: counts the tasks a run
    /// actually runs, across every driver in the process - the library phases and each analysis's
    /// stages - and ends the run once the count is reached. A driver calls <see cref="TaskDone"/>
    /// after a task has returned and its outputs are committed, so the stop leaves exactly what
    /// a crash between two tasks would, and the next run resumes from it.
    /// </summary>
    internal static class TaskBudget
    {
        private static int _ran;

        /// <summary>Starts the count for one command line (an in-process test runs many).</summary>
        public static void Reset()
        {
            _ran = 0;
        }

        /// <summary>
        /// Counts <paramref name="taskName"/>, which has run, and throws <see cref="TaskBudgetSpentException"/>
        /// when it is the last one the budget allows. A task that failed (<paramref name="exitCode"/>
        /// not zero) is not counted: its failure ends the run as it would without the budget, rather
        /// than being reported as a clean stop.
        /// </summary>
        public static void TaskDone(string taskName, int exitCode)
        {
            int limit = OspreyEnvironment.StopAfterTasks;
            if (limit <= 0 || exitCode != 0)
                return;
            if (++_ran >= limit)
                throw new TaskBudgetSpentException(taskName, _ran);
        }
    }

    /// <summary>
    /// The run reached <see cref="OspreyEnvironment.StopAfterTasks"/>: not a failure, and never
    /// reported as one. The drivers' callers let it pass, and Program ends the run with exit code 0.
    /// </summary>
    internal sealed class TaskBudgetSpentException : Exception
    {
        public TaskBudgetSpentException(string taskName, int tasksRun)
            : base(string.Format(@"stopped after {0} task(s) run, the last {1}", tasksRun, taskName))
        {
            TaskName = taskName;
            TasksRun = tasksRun;
        }

        public string TaskName { get; }

        public int TasksRun { get; }
    }
}
