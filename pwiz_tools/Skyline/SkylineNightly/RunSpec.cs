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
using SkylineNightly.Properties;

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
    /// form: a machine scheduled with a pre-split argument keeps running the run it ran (pass 1
    /// now covering the tests the NoLeakTesting attribute used to skip) until it is reconfigured.
    /// </summary>
    public enum RunType { standard, leak, perf, standard_leak }

    /// <summary>
    /// A branch and a run type: everything the scheduled task needs to say about one nightly run.
    /// Written as branch/type in the task argument, e.g. "master/standard release/leak".
    /// </summary>
    public class RunSpec
    {
        public const int STANDARD_DURATION_HOURS = 9;
        public const int LONG_DURATION_HOURS = 12;

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
        };

        /// <summary>
        /// The runs the form last saved: Run1 and, if there is one, Run2. Settings from before the
        /// split (mode1 and mode2, pre-split names) are read when Run1 has never been saved, and are
        /// never written, so that a machine put back on the pre-split SkylineNightly still opens its
        /// form and runs what it ran.
        /// </summary>
        public static RunSpec[] GetSavedRuns()
        {
            var settings = Settings.Default;
            bool saved = !string.IsNullOrEmpty(settings.Run1);
            var run1 = Parse(saved ? settings.Run1 : settings.mode1);
            var run2Argument = saved ? settings.Run2 : settings.mode2;
            return string.IsNullOrEmpty(run2Argument) ? new[] { run1 } : new[] { run1, Parse(run2Argument) };
        }

        public static void SaveRuns(RunSpec run1, RunSpec run2)
        {
            Settings.Default.Run1 = run1.ToString();
            Settings.Default.Run2 = run2?.ToString() ?? string.Empty;
        }

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

        /// <summary>
        /// Whether this run takes the longer of the two nightly slots. A machine may schedule one
        /// long run and one standard run per day, never two long runs, which would leave no margin
        /// in 24 hours for updates or a person at the keyboard.
        /// </summary>
        public bool IsLong => RunType == RunType.leak || RunType == RunType.perf;

        public TimeSpan TargetDuration => TimeSpan.FromHours(IsLong ? LONG_DURATION_HOURS : STANDARD_DURATION_HOURS);

        /// <summary>
        /// A name for this run, distinct for every distinct run, which names the working directory
        /// and the log file. A run the pre-split SkylineNightly could schedule keeps the name it had
        /// then (trunk, perf, release, ...), so nothing on disk moves until a machine is reconfigured;
        /// the log parser finds the branch in the clone command by the directory name either way.
        /// </summary>
        public string ShortName
        {
            get
            {
                foreach (var legacy in LEGACY_ARGUMENTS)
                {
                    if (Equals(legacy.Value, this))
                        return legacy.Key;
                }
                if (RunType == RunType.standard && Branch == Branch.master)
                    return Branch.ToString();
                return Branch + "_" + RunType;
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
