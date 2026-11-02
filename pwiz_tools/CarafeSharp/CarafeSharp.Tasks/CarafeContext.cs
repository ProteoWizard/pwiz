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

using System.Collections.Generic;
using System.IO;
using System.Reflection;
using pwiz.CarafeSharp.Proteome;
using pwiz.Common.Pipeline;

namespace pwiz.CarafeSharp.Tasks
{
    /// <summary>
    /// The shared <see cref="TaskContext"/> with what CarafeSharp's tasks read: the parsed
    /// command line and the writer the stages log their prose to. Its stamps carry
    /// <see cref="BuildVersion"/>, and <see cref="CarafeStamps.Read"/> finds the stamp inside the
    /// artifacts CarafeSharp writes.
    /// </summary>
    public sealed class CarafeContext : TaskContext
    {
        /// <summary>
        /// The build version every stamp carries (<see cref="TaskContext.Version"/>): the informational
        /// version, which the SDK stamps with the commit (<c>26.1.1.0+&lt;hash&gt;</c>), so an artifact from
        /// another build is never current. The bare assembly version is constant between commits
        /// locally and would let a rebuilt predictor reuse a stale library.
        /// </summary>
        public static readonly string BuildVersion =
            typeof(CarafeContext).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ??
            typeof(CarafeContext).Assembly.GetName().Version?.ToString() ?? @"0.0.0.0";

        public CarafeContext(CarafeCommandLine commandLine, IEnumerable<PipelineTask> tasks, TextWriter output, TextWriter error)
            : base(tasks, BuildVersion, CarafeStamps.Read, output.WriteLine, error.WriteLine, error.WriteLine)
        {
            CommandLine = commandLine;
            Output = output;
        }

        /// <summary>
        /// The context of a host that runs CarafeSharp's tasks inside its own command (Osprey's
        /// library build), which has no CarafeSharp command line: the stamps and the stamp reader
        /// are the same, so a library either front end writes is current to the other.
        /// </summary>
        public CarafeContext(IEnumerable<PipelineTask> tasks, TextWriter output, TextWriter error)
            : this(null, tasks, output, error)
        {
        }

        /// <summary>The command line the run was given, with the settings of the mode it selects; null for a host's run.</summary>
        public CarafeCommandLine CommandLine { get; }

        /// <summary>Where the stages write their prose (standard output, or the log file).</summary>
        public TextWriter Output { get; }
    }
}
