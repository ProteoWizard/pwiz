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
using System.Globalization;

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// A <c>[TAG]</c> prefix on a log line. Each tag knows its own spelling and when it is
    /// emitted, and <see cref="OspreyLog.Write"/> is the one place that applies both, so no code
    /// writes a tag prefix by hand and no tag can reach the default log by being left off a list.
    ///
    /// <para>Two kinds. MACHINE tags (<see cref="COUNT"/>, <see cref="TIMING"/>, <see cref="BENCH"/>,
    /// <see cref="STAGE_WALL"/>, <see cref="PATH"/>, <see cref="TRAIN"/>, <see cref="TASK"/>,
    /// <see cref="Mem"/>) carry text that scripts and tests read: it is never translated, and it
    /// is the only part of the log a consumer may key off. All but TASK are gated. CATEGORY tags
    /// (<see cref="WARN"/>, <see cref="ERROR"/>, <see cref="MODEL_DIAGNOSTICS"/>, <see cref="BISECT"/>
    /// and the rest) are always emitted and label the prose that follows them; the tag stays ASCII
    /// but the prose is written for a person and may be reworded or localized in any change
    /// (pwiz_tools/Osprey/docs/20-command-line.md, "Log format").</para>
    /// </summary>
    public sealed class LogTag
    {
        /// <summary>A count a script or test asserts. <c>--perf-stats</c>.</summary>
        public static readonly LogTag COUNT = new LogTag(@"COUNT", IsPerfStats);
        /// <summary>A timing inside a stage. <c>--perf-stats</c>.</summary>
        public static readonly LogTag TIMING = new LogTag(@"TIMING", IsPerfStats);
        /// <summary>A benchmarking control or measurement. <c>--perf-stats</c>.</summary>
        public static readonly LogTag BENCH = new LogTag(@"BENCH", IsPerfStats);
        /// <summary>A stage's wall-clock time, read by the perf gate. <c>--perf-stats</c>.</summary>
        public static readonly LogTag STAGE_WALL = new LogTag(@"STAGE-WALL", IsPerfStats);
        /// <summary>Which code route a run took, keyed by a <see cref="LogKey"/> route. <c>--perf-stats</c>.</summary>
        public static readonly LogTag PATH = new LogTag(@"PATH", IsPerfStats);
        /// <summary>Which population a model trained on. <c>--perf-stats</c>.</summary>
        public static readonly LogTag TRAIN = new LogTag(@"TRAIN", IsPerfStats);
        /// <summary>
        /// A task's start, skip and finish. Always emitted: it is the section header a person
        /// reads as well as the key the task-cache assertions read, and the task names are the
        /// documented <c>--task</c> values.
        /// </summary>
        public static readonly LogTag TASK = new LogTag(@"TASK", Always);

        // Category tags: always emitted, followed by prose.
        /// <summary>A warning.</summary>
        public static readonly LogTag WARN = new LogTag(@"WARN", Always);
        /// <summary>An error.</summary>
        public static readonly LogTag ERROR = new LogTag(@"ERROR", Always);
        /// <summary>The <c>--model-diagnostics</c> report and its data.</summary>
        public static readonly LogTag MODEL_DIAGNOSTICS = new LogTag(@"MODEL-DIAGNOSTICS", Always);
        /// <summary>Entrapment pairing under <c>--fdrbench</c>.</summary>
        public static readonly LogTag ENTRAPMENT = new LogTag(@"ENTRAPMENT", Always);
        /// <summary>A bisection dump or stop, reached only through <c>-d</c> or an <c>OSPREY_DUMP_*</c> / <c>OSPREY_DIAG_*</c> setting.</summary>
        public static readonly LogTag BISECT = new LogTag(@"BISECT", Always);
        /// <summary>A diagnostic dump reached only through <c>-d</c>.</summary>
        public static readonly LogTag DIAG = new LogTag(@"DIAG", Always);
        /// <summary>FDR bookkeeping.</summary>
        public static readonly LogTag FDR = new LogTag(@"FDR", Always);
        /// <summary>Library load timing.</summary>
        public static readonly LogTag LIB_LOAD = new LogTag(@"LIB-LOAD", Always);
        /// <summary>Byproducts released at a task boundary.</summary>
        public static readonly LogTag DROP = new LogTag(@"DROP", Always);

        /// <summary>
        /// A memory probe, <c>[MEM label]</c>. Emitted only under <c>OSPREY_LOG_MEMORY</c>, which
        /// is a developer setting: how memory is managed is never user text.
        /// </summary>
        public static LogTag Mem(string label)
        {
            return new LogTag(@"MEM " + label, () => OspreyEnvironment.LogMemory);
        }

        private readonly Func<bool> _isEnabled;

        private LogTag(string name, Func<bool> isEnabled)
        {
            Name = name;
            _isEnabled = isEnabled;
        }

        /// <summary>The text between the brackets.</summary>
        public string Name { get; }

        /// <summary>True when a line with this tag is written in the current run.</summary>
        public bool IsEnabled => _isEnabled();

        /// <summary>The line as written: <c>[Name] text</c>.</summary>
        public string Format(string text)
        {
            return @"[" + Name + @"] " + text;
        }

        public override string ToString()
        {
            return @"[" + Name + @"]";
        }

        private static bool IsPerfStats()
        {
            return OspreyOutput.PerfStats;
        }

        private static bool Always()
        {
            return true;
        }
    }

    /// <summary>
    /// A log sink that can carry a <see cref="LogTag"/>. Code that writes a tagged line takes
    /// one of these rather than an <c>Action&lt;string&gt;</c>, because a plain string delegate
    /// has nowhere to put the tag. <c>PipelineContext</c> implements it; code holding only a
    /// delegate wraps it with <see cref="OspreyLog.FromDelegate"/>, and code below the task layer
    /// with no sink at all uses <see cref="OspreyLog.Out"/>.
    /// </summary>
    public interface IOspreyLog
    {
        /// <summary>Write a line of prose.</summary>
        void LogInfo(string message);

        /// <summary>
        /// Write a machine-channel line. Implementations pass it to <see cref="OspreyLog.Write"/>,
        /// which decides whether it is emitted.
        /// </summary>
        void LogInfo(LogTag tag, string text);
    }

    /// <summary>
    /// The one place that decides whether a tagged line is emitted, plus the adapters that let
    /// every kind of sink reach it.
    /// </summary>
    public static class OspreyLog
    {
        /// <summary>
        /// Emit <paramref name="text"/> under <paramref name="tag"/> to <paramref name="sink"/>
        /// when the tag is enabled for this run. Every tagged line goes through here.
        /// </summary>
        public static void Write(Action<string> sink, LogTag tag, string text)
        {
            if (sink != null && tag.IsEnabled)
                sink(tag.Format(text));
        }

        /// <summary>
        /// Wrap a prose delegate so it can take tagged lines. Null in, null out, so an optional
        /// delegate stays optional.
        /// </summary>
        public static IOspreyLog FromDelegate(Action<string> logInfo)
        {
            return logInfo == null ? null : new DelegateLog(logInfo);
        }

        /// <summary>
        /// A sink that writes to <see cref="OspreyOutput.Out"/>, resolved on each line so a
        /// per-file scope entered after this was taken still captures the output.
        /// </summary>
        public static IOspreyLog Out { get; } = new DelegateLog(line => OspreyOutput.Out.WriteLine(line));

        /// <summary>A sink that discards everything, for an optional log left unset.</summary>
        public static IOspreyLog None { get; } = new DelegateLog(_ => { });

        private sealed class DelegateLog : IOspreyLog
        {
            private readonly Action<string> _logInfo;

            public DelegateLog(Action<string> logInfo)
            {
                _logInfo = logInfo;
            }

            public void LogInfo(string message)
            {
                _logInfo(message);
            }

            public void LogInfo(LogTag tag, string text)
            {
                Write(_logInfo, tag, text);
            }
        }
    }

    /// <summary>
    /// The keys after a <see cref="LogTag.PATH"/> or <see cref="LogTag.COUNT"/> tag that a script
    /// asserts on. They are a contract with <c>pwiz_tools/Osprey/regression.ps1</c> and the
    /// scripts that read the log: adding a key is free, renaming one means updating its consumers
    /// in the same change. Values are formatted with the invariant culture and no group
    /// separators, so a consumer parses them the same way under any UI language.
    /// </summary>
    public static class LogKey
    {
        // [PATH] route keys.
        /// <summary>Program startup, after the settings banner and before any route is chosen.</summary>
        public const string ROUTE_STARTUP = @"startup";
        /// <summary>A first-pass scoring of one file from its spectra (value <c>i/n</c>).</summary>
        public const string ROUTE_SCORE_FILE = @"score-file";
        /// <summary>A second-pass rescoring of one file from its spectra (value <c>i/n</c>).</summary>
        public const string ROUTE_RESCORE_FILE = @"rescore-file";
        /// <summary>Which first-pass FDR input FirstPassFDR used on a rehydrate.</summary>
        public const string ROUTE_FIRST_PASS_FDR = @"first-pass-fdr";
        /// <summary>How the rescore task hydrated its runs.</summary>
        public const string ROUTE_RESCORE_HYDRATE = @"rescore-hydrate";
        /// <summary>How the second-pass join visits runs.</summary>
        public const string ROUTE_SECOND_PASS_JOIN = @"second-pass-join";
        /// <summary>The O(files x entries) all-runs reconciliation bundle was built or refused.</summary>
        public const string ROUTE_ALL_RUNS_BUNDLE = @"all-runs-bundle";
        /// <summary>The frozen-competition second-pass fold and how many worker answers it read.</summary>
        public const string ROUTE_SECOND_PASS_FOLD = @"second-pass-fold";
        /// <summary>The whole-run survivor pool was materialized at once.</summary>
        public const string ROUTE_SURVIVOR_POOL = @"survivor-pool";
        /// <summary>The pass-2 q-value mode in force.</summary>
        public const string ROUTE_PASS2_QVALUE = @"pass2-qvalue";
        /// <summary>The experiment-wide aggregation in force.</summary>
        public const string ROUTE_EXPERIMENT_AGG = @"experiment-agg";
        /// <summary>How the model-diagnostics report was produced.</summary>
        public const string ROUTE_MODEL_DIAGNOSTICS = @"model-diagnostics";
        /// <summary>Protein-level FDR ran in this process.</summary>
        public const string ROUTE_PROTEIN_FDR = @"protein-fdr";
        /// <summary>
        /// Scored entries were loaded from the per-file score parquets: <c>resident</c> (every
        /// file's stubs held at once) or <c>lean</c> (calibration and footer counts only).
        /// </summary>
        public const string ROUTE_SCORED_ENTRIES = @"scored-entries";
        /// <summary>The resident pre-compaction first-pass pool was held (O(files)).</summary>
        public const string ROUTE_PRE_COMPACTION_POOL = @"pre-compaction-pool";

        // [COUNT] keys.
        /// <summary>Library fragment spectra released after an FDR stage.</summary>
        public const string COUNT_LIBRARY_FRAGMENTS_RELEASED = @"library-fragments-released";
        /// <summary>Library fragment spectra never allocated because the retained set was read first.</summary>
        public const string COUNT_LIBRARY_FRAGMENTS_SKIPPED = @"library-fragments-skipped-at-load";
        /// <summary>The analysis-wide retained summary FirstPassFDR wrote.</summary>
        public const string COUNT_RETAINED_SUMMARY_WRITTEN = @"retained-summary-written";
        /// <summary>Precursor candidates scored across all files when first-pass scoring ends.</summary>
        public const string COUNT_SCORED_CANDIDATES = @"scored-candidates";

        // Values of the release scope= field.
        /// <summary>The release after first-pass FDR, keeping what rescore and gap-fill need.</summary>
        public const string SCOPE_RESCORE_GAP_FILL = @"rescore-gap-fill";
        /// <summary>The release after second-pass FDR, keeping the first-pass retained set.</summary>
        public const string SCOPE_RETAINED_SUMMARY = @"retained-summary";

        /// <summary>
        /// The text after the tag for a keyed line: <c>key: value</c>, with
        /// <paramref name="valueFormat"/> formatted in the invariant culture.
        /// </summary>
        public static string Format(string key, string valueFormat, params object[] args)
        {
            return key + @": " + string.Format(CultureInfo.InvariantCulture, valueFormat, args);
        }
    }
}
