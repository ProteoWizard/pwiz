/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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
using System.Collections.Generic;

namespace SkylineNightly
{
    // ReSharper disable LocalizableElement

    /// <summary>
    /// Which branch a nightly run builds and tests. The release and integration branches are
    /// whatever TeamCity's VCS roots for them point at, so this never names an actual branch.
    /// </summary>
    public enum Branch { master, release, integration }

    /// <summary>
    /// What a nightly run does with its hours. Each type is one job, so that no machine has to do
    /// everything in one night: standard cycles the suite, leak repeats the leak checking pass over
    /// every test, and perf concentrates on the perf tests. standard_leak is the run every machine
    /// did before the split - pass 0, pass 1 and pass 2 in one night - and is not offered in the
    /// form: a machine scheduled with a pre-split argument keeps running exactly what it ran until
    /// it is reconfigured.
    /// </summary>
    public enum RunType { standard, leak, perf, stress, standard_leak }

    /// <summary>
    /// A branch and a run type: everything the scheduled task needs to say about one nightly run.
    /// Written as branch/type in the task argument, e.g. "master/standard release/leak".
    /// </summary>
    public class RunSpec
    {
        public const int STANDARD_DURATION_HOURS = 9;
        public const int LONG_DURATION_HOURS = 12;
        public const int STRESS_DURATION_HOURS = 168; // Let it go as long as a week

        /// <summary>
        /// The pre-split task arguments, which the shim keeps passing until a machine is reconfigured.
        /// A pre-split standard run is the combined run, so that no machine loses leak checking before
        /// the leak machines exist. A pre-split perf run is the new perf run, because the leak checking
        /// it used to do in pass 1 depended on an attribute that no longer exists.
        /// </summary>
        private static readonly Dictionary<string, RunSpec> LEGACY_ARGUMENTS = new Dictionary<string, RunSpec>
        {
            { "trunk", new RunSpec(Branch.master, RunType.standard_leak) },
            { "perf", new RunSpec(Branch.master, RunType.perf) },
            { "release", new RunSpec(Branch.release, RunType.standard_leak) },
            { "release_perf", new RunSpec(Branch.release, RunType.perf) },
            { "integration", new RunSpec(Branch.integration, RunType.standard_leak) },
            { "integration_perf", new RunSpec(Branch.integration, RunType.perf) },
            { "stress", new RunSpec(Branch.master, RunType.stress) },
        };

        public static RunSpec Parse(string argument)
        {
            if (LEGACY_ARGUMENTS.TryGetValue(argument.ToLowerInvariant(), out var legacy))
                return legacy;

            var parts = argument.Split('/');
            if (parts.Length != 2)
                throw new ArgumentException(string.Format("Expected a run as branch/type, such as master/standard, but got '{0}'", argument));
            return new RunSpec((Branch)Enum.Parse(typeof(Branch), parts[0], true),
                (RunType)Enum.Parse(typeof(RunType), parts[1], true));
        }

        public RunSpec(Branch branch, RunType runType)
        {
            Branch = branch;
            RunType = runType;
        }

        public Branch Branch { get; }
        public RunType RunType { get; }

        public bool IsPerf => RunType == RunType.perf;
        public bool IsStress => RunType == RunType.stress;

        /// <summary>
        /// Whether this run takes the longer of the two nightly slots. A machine may schedule one
        /// long run and one standard run per day, never two long runs, which would leave no margin
        /// in 24 hours for updates or a person at the keyboard.
        /// </summary>
        public bool IsLong => RunType == RunType.leak || RunType == RunType.perf;

        public TimeSpan TargetDuration
        {
            get
            {
                switch (RunType)
                {
                    case RunType.stress:
                        return TimeSpan.FromHours(STRESS_DURATION_HOURS);
                    case RunType.leak:
                    case RunType.perf:
                        return TimeSpan.FromHours(LONG_DURATION_HOURS);
                    default:
                        return TimeSpan.FromHours(STANDARD_DURATION_HOURS);
                }
            }
        }

        /// <summary>
        /// The name the pre-split SkylineNightly gave this run, which still names the working
        /// directory and the log file so that nothing on disk moves when a machine is reconfigured -
        /// the log parser finds the branch in the clone command by the directory name, and the
        /// scheduled cleanup of the previous run's directory finds it by the same name.
        /// </summary>
        public string ShortName
        {
            get
            {
                var branchName = Branch == Branch.master ? "trunk" : Branch.ToString();
                switch (RunType)
                {
                    case RunType.perf:
                        return Branch == Branch.master ? "perf" : branchName + "_perf";
                    case RunType.leak:
                        return Branch == Branch.master ? "leak" : branchName + "_leak";
                    case RunType.stress:
                        return Branch == Branch.master ? "stress" : branchName + "_stress";
                    default:
                        return branchName;
                }
            }
        }

        /// <summary>
        /// The scheduled task argument for this run.
        /// </summary>
        public override string ToString()
        {
            return Branch + "/" + RunType;
        }

        public override bool Equals(object obj)
        {
            return obj is RunSpec other && Branch == other.Branch && RunType == other.RunType;
        }

        public override int GetHashCode()
        {
            return ((int)Branch * 397) ^ (int)RunType;
        }
    }
}
