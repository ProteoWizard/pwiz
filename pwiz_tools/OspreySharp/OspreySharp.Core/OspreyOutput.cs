/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
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
using System.IO;

namespace pwiz.OspreySharp.Core
{
    /// <summary>
    /// Process-wide output sink for OspreySharp logging that originates below the exe
    /// layer (e.g. the FDR and IO assemblies, which cannot see <c>Program._out</c>).
    /// <c>Program</c> points <c>Out</c> at its single
    /// <c>CommandStatusWriter</c> so every line picks up the <c>--timestamp</c> /
    /// <c>--memstamp</c> prefixes and <c>--log-file</c> redirection. Defaults to stderr
    /// for standalone / test use.
    ///
    /// Typed as the BCL <see cref="TextWriter"/> so Core needs no PortableUtil reference,
    /// and deliberately NOT the diagnostics module's hook (that stays <c>-d</c>-only).
    /// This is a stepping stone: long operations should ultimately report through an
    /// IProgressMonitor / ProgressStatus rather than writing lines directly.
    /// </summary>
    public static class OspreyOutput
    {
        public static TextWriter Out { get; set; } = Console.Error;

        /// <summary>
        /// When false (default), the machine-parseable [COUNT]/[TIMING]/[STAGE-WALL] lines
        /// are suppressed so the human log stays clean (each has a human-readable plain twin
        /// that remains). The --perf-stats flag sets this true so the perf tools
        /// (Test-PerfGate.ps1, Measure-Pipeline.ps1, Osprey-workflow.html) get the tagged lines.
        /// </summary>
        public static bool PerfStats { get; set; }

        /// <summary>
        /// True if a line is a machine-parseable stat line (leading [COUNT]/[TIMING]/[STAGE-WALL],
        /// ignoring leading spaces) gated by <see cref="PerfStats"/>.
        /// </summary>
        public static bool IsStatLine(string line)
        {
            if (string.IsNullOrEmpty(line))
                return false;
            int i = 0;
            while (i < line.Length && line[i] == ' ')
                i++;
            return string.CompareOrdinal(line, i, "[COUNT]", 0, 7) == 0
                || string.CompareOrdinal(line, i, "[TIMING]", 0, 8) == 0
                || string.CompareOrdinal(line, i, "[STAGE-WALL]", 0, 12) == 0;
        }
    }

    /// <summary>
    /// Wraps an inner <see cref="TextWriter"/> and drops machine-parseable stat lines
    /// (<see cref="OspreyOutput.IsStatLine"/>) unless <see cref="OspreyOutput.PerfStats"/> is
    /// set, forwarding everything else unchanged. <c>Program</c> wraps its
    /// CommandStatusWriter in one of these so the default human log is clean while
    /// --perf-stats restores the tagged lines for the perf tools. Inherited WriteLine(format,
    /// args) overloads route through <see cref="WriteLine(string)"/>, so they are filtered too.
    /// </summary>
    public sealed class StatFilteringTextWriter : TextWriter
    {
        private readonly TextWriter _inner;

        public StatFilteringTextWriter(TextWriter inner)
        {
            _inner = inner;
        }

        public override System.Text.Encoding Encoding
        {
            get { return _inner.Encoding; }
        }

        public override void Flush()
        {
            _inner.Flush();
        }

        public override void Write(char value)
        {
            _inner.Write(value);
        }

        public override void Write(string value)
        {
            _inner.Write(value);
        }

        public override void WriteLine(string value)
        {
            if (!OspreyOutput.PerfStats && OspreyOutput.IsStatLine(value))
                return;
            _inner.WriteLine(value);
        }
    }
}
