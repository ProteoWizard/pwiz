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
    /// Names of the command-line arguments that code below the Osprey executable names in its
    /// messages. The argument declarations live in the executable's OspreyCommandArgs, which
    /// Osprey.Tasks and Osprey.IO cannot reference, so those declarations take their names
    /// from these constants and a message built with <see cref="Text(string)"/> cannot drift
    /// from what the parser accepts.
    /// </summary>
    public static class OspreyArgNames
    {
        /// <summary>The long-form prefix, the same as the shared framework's ArgumentBase.ARG_PREFIX.</summary>
        public const string PREFIX = @"--";

        public const string INPUT = @"input";
        public const string LIBRARY = @"library";
        public const string OUTPUT = @"output";
        public const string OUTPUT_DIR = @"output-dir";
        public const string CACHE_DIR = @"cache-dir";
        public const string FDR_METHOD = @"fdr-method";
        public const string DECOYS_IN_LIBRARY = @"decoys-in-library";
        public const string RESOLUTION = @"resolution";
        public const string FRAGMENT_TOLERANCE = @"fragment-tolerance";
        public const string DECOY_PAIRING_MANIFEST = @"decoy-pairing-manifest";
        public const string TASK = @"task";
        public const string MODEL_DIAGNOSTICS = @"model-diagnostics";
        public const string PARALLEL_FILES = @"parallel-files";
        public const string VERBOSE = @"verbose";

        /// <summary>The argument as it is typed: <c>--name</c>.</summary>
        public static string Text(string name)
        {
            return PREFIX + name;
        }

        /// <summary>
        /// The argument with a value, as it is typed and as the generated help shows it:
        /// <c>--name value</c>.
        /// </summary>
        public static string Text(string name, string value)
        {
            return Text(name) + @" " + value;
        }

        /// <summary>The command-line text that selects a task: <c>--task Name</c>.</summary>
        public static string TaskText(string taskName)
        {
            return Text(TASK, taskName);
        }
    }
}
