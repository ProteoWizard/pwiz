/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.7) <noreply .at. anthropic.com>
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// Per-(output, task) sidecar that records "this task produced
    /// this output with validity key K." A small JSON file written
    /// next to each task output at path
    /// <c>&lt;output&gt;.&lt;TaskName&gt;.osprey.task</c>.
    ///
    /// Naming includes the task name so that two tasks writing to the
    /// same output path do not trample each other's validity records.
    /// (Historically PerFileScoringTask wrote <c>.scores.parquet</c> and
    /// PerFileRescoreTask overwrote it in place; Stage 6 now writes a
    /// separate <c>.scores-reconciled.parquet</c>, so the two tasks no
    /// longer share an output -- but the task-name disambiguation remains
    /// for any future same-path producers.) Each task checks its own
    /// sidecar; downstream consumers don't need to know which task wrote
    /// last.
    ///
    /// On the next pipeline invocation, the driver reads each
    /// output's sidecar before running the producing task; when
    /// every output has a sidecar whose <c>validity_key</c> matches
    /// the current task's <see cref="OspreyTask.ValidityKey"/>, the
    /// task is skipped. This is the resume-on-restart mechanism: a
    /// crashed run re-invokes the same CLI and the driver
    /// fast-forwards through whatever was completed before the
    /// crash.
    ///
    /// JSON format (stable):
    /// <code>
    /// {
    ///   "task": "PerFileScoring",
    ///   "version": "26.6.0",
    ///   "validity_key": "search=abc...;library=def...",
    ///   "inputs": ["/path/to/file.mzML", "/path/to/library.tsv"]
    /// }
    /// </code>
    ///
    /// Hand-readable on purpose. No binary header; no length prefix.
    /// Survives format evolution because parsers ignore unknown
    /// top-level keys.
    /// </summary>
    public static class TaskValiditySidecar
    {
        /// <summary>
        /// Sidecar path for an (output, task) pair:
        /// <c>output + "." + taskName + ".osprey.task"</c>. Lives next
        /// to the output so a directory listing groups them. Including
        /// the task name disambiguates the per-task sidecars for tasks
        /// that share an output path.
        /// </summary>
        public static string PathFor(string outputPath, string taskName)
        {
            if (string.IsNullOrEmpty(outputPath))
                throw new ArgumentException(@"outputPath required", nameof(outputPath));
            if (string.IsNullOrEmpty(taskName))
                throw new ArgumentException(@"taskName required", nameof(taskName));
            return outputPath + @"." + taskName + @".osprey.task";
        }

        /// <summary>
        /// Write a sidecar declaring that <paramref name="outputPath"/>
        /// was produced by task <paramref name="taskName"/> with
        /// validity key <paramref name="validityKey"/>. The sidecar is
        /// written next to the output, replacing any existing sidecar.
        /// Caller is responsible for serializing concurrent writes to
        /// the same path (callers in Osprey drive per-file work
        /// from one task at a time).
        /// </summary>
        public static void Write(string outputPath, string taskName, string version,
            string validityKey, IEnumerable<string> inputs)
        {
            if (string.IsNullOrEmpty(outputPath))
                throw new ArgumentException(@"outputPath required", nameof(outputPath));
            if (string.IsNullOrEmpty(taskName))
                throw new ArgumentException(@"taskName required", nameof(taskName));
            if (validityKey == null)
                throw new ArgumentNullException(nameof(validityKey));

            var sb = new StringBuilder();
            sb.Append('{').Append('\n');
            AppendField(sb, @"task", taskName, true);
            AppendField(sb, @"version", version ?? string.Empty, true);
            AppendField(sb, @"validity_key", validityKey, true);

            sb.Append(@"  ""inputs"": [");
            bool first = true;
            if (inputs != null)
            {
                foreach (var input in inputs)
                {
                    if (!first) sb.Append(',');
                    sb.Append('\n').Append(@"    ").Append(JsonString(input ?? string.Empty));
                    first = false;
                }
            }
            if (!first) sb.Append('\n').Append(@"  ");
            sb.Append(']').Append('\n');

            sb.Append('}').Append('\n');

            using (var saver = new FileSaver(PathFor(outputPath, taskName)))
            {
                File.WriteAllText(saver.SafeName, sb.ToString());
                saver.Commit();
            }
        }

        /// <summary>
        /// Read a sidecar and return whether its <c>validity_key</c>
        /// matches <paramref name="expectedValidityKey"/>. Returns
        /// <c>false</c> if the sidecar is missing, malformed, or
        /// records a different key. Never throws on read failure —
        /// "I can't tell, must re-run" is the conservative answer.
        /// </summary>
        public static bool IsValid(string outputPath, string taskName, string expectedValidityKey)
        {
            if (string.IsNullOrEmpty(outputPath) || string.IsNullOrEmpty(taskName) || expectedValidityKey == null)
                return false;
            string sidecarPath = PathFor(outputPath, taskName);
            if (!File.Exists(sidecarPath))
                return false;
            string content;
            try
            {
                content = File.ReadAllText(sidecarPath);
            }
            catch
            {
                return false;
            }
            string recorded = ExtractStringField(content, @"validity_key");
            return string.Equals(recorded, expectedValidityKey, StringComparison.Ordinal);
        }

        /// <summary>
        /// Delete the sidecar for an (output, task) pair if it exists.
        /// Called when a task starts running (we don't want a stale
        /// sidecar surviving if the new Run crashes mid-write).
        /// </summary>
        public static void Delete(string outputPath, string taskName)
        {
            if (string.IsNullOrEmpty(outputPath) || string.IsNullOrEmpty(taskName))
                return;
            string sidecarPath = PathFor(outputPath, taskName);
            if (File.Exists(sidecarPath))
            {
                try { File.Delete(sidecarPath); } catch { /* best-effort */ }
            }
        }

        // --- minimal JSON support: enough for the four keys above ---

        private static void AppendField(StringBuilder sb, string key, string value, bool trailingComma)
        {
            sb.Append(@"  ").Append(JsonString(key)).Append(@": ").Append(JsonString(value));
            if (trailingComma) sb.Append(',');
            sb.Append('\n');
        }

        private static string JsonString(string s)
        {
            if (s == null) return @"""""";
            var sb = new StringBuilder(s.Length + 2);
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append(@"\"""); break;
                    case '\\': sb.Append(@"\\"); break;
                    case '\b': sb.Append(@"\b"); break;
                    case '\f': sb.Append(@"\f"); break;
                    case '\n': sb.Append(@"\n"); break;
                    case '\r': sb.Append(@"\r"); break;
                    case '\t': sb.Append(@"\t"); break;
                    default:
                        if (c < 0x20)
                            sb.AppendFormat(@"\u{0:X4}", (int)c);
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        /// <summary>
        /// Pull the string value for <c>"&lt;fieldName&gt;": "&lt;value&gt;"</c>
        /// out of <paramref name="json"/>. Naive single-line parse — the
        /// sidecar writer only ever emits string-valued fields on one
        /// line, so a regex-class search is enough. Returns null when
        /// the field is missing or unparseable.
        /// </summary>
        private static string ExtractStringField(string json, string fieldName)
        {
            if (string.IsNullOrEmpty(json)) return null;
            string needle = @"""" + fieldName + @"""";
            int keyIdx = json.IndexOf(needle, StringComparison.Ordinal);
            if (keyIdx < 0) return null;
            int colonIdx = json.IndexOf(':', keyIdx + needle.Length);
            if (colonIdx < 0) return null;
            int quoteOpen = json.IndexOf('"', colonIdx + 1);
            if (quoteOpen < 0) return null;
            var sb = new StringBuilder();
            for (int i = quoteOpen + 1; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '\\' && i + 1 < json.Length)
                {
                    char next = json[++i];
                    switch (next)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 < json.Length &&
                                int.TryParse(json.Substring(i + 1, 4),
                                    System.Globalization.NumberStyles.HexNumber,
                                    System.Globalization.CultureInfo.InvariantCulture, out int code))
                            {
                                sb.Append((char)code);
                                i += 4;
                            }
                            break;
                        default: sb.Append(next); break;
                    }
                }
                else if (c == '"')
                {
                    return sb.ToString();
                }
                else
                {
                    sb.Append(c);
                }
            }
            return null;  // unterminated
        }
    }
}
