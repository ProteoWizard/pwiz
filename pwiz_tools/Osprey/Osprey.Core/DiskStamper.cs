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
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// <c>--diskstamp</c>: writes a <see cref="LogTag.DISK"/> line with the change in used space on
    /// the output directory's drive since the run started, and the drive's free space - once at
    /// start (a change of zero), every <see cref="SAMPLE_INTERVAL_MS"/> from a timer thread, at each
    /// task boundary (<see cref="SampleCurrent"/>) and once at the end (<see cref="Dispose"/>).
    ///
    /// <para>The measure is the DRIVE's used space, not the size of the run's files, so it is cheap
    /// (one volume query, no directory walk) and counts hard-linked inputs at what they really cost.
    /// It is exact only while nothing else writes to the same drive, which a controlled perf run
    /// arranges.</para>
    ///
    /// <para>It must never disturb the run: a probe that fails writes nothing, and nothing here
    /// throws to its caller or to the timer thread, where an exception would end the process.</para>
    /// </summary>
    public sealed class DiskStamper : IDisposable
    {
        /// <summary>How often the timer samples the drive.</summary>
        public const int SAMPLE_INTERVAL_MS = 60 * 1000;

        private const double BYTES_PER_GB = 1024.0 * 1024.0 * 1024.0;

        // The text after the [DISK] tag. A contract with ai/scripts/perfviz.py and perfviz.html:
        // the used change is signed, both values are GB with two decimals in the invariant culture.
        private const string LINE_FORMAT = @"used: {0:+0.00;-0.00;+0.00} GB since start, {1:0.00} GB free on {2}";
        private static readonly Regex LINE_REGEX = new Regex(
            @"^used: ([+-]\d+\.\d+) GB since start, (\d+\.\d+) GB free on (\S+)$", RegexOptions.CultureInvariant);

        /// <summary>
        /// The stamper of the run in progress, or null when <c>--diskstamp</c> is off. Set by
        /// <see cref="Start"/> and cleared by <see cref="Dispose"/>.
        /// </summary>
        public static DiskStamper Current { get; private set; }

        /// <summary>
        /// Start stamping the drive that holds <paramref name="directory"/> to
        /// <paramref name="log"/>, writing the start line, and make the stamper
        /// <see cref="Current"/>. Disposing it writes the end line and stops the timer.
        /// </summary>
        public static DiskStamper Start(string directory, IOspreyLog log)
        {
            string root = GetDriveRoot(directory);
            var stamper = new DiskStamper(log, () => ProbeDrive(root), FormatDrive(root), SAMPLE_INTERVAL_MS);
            Current = stamper;
            return stamper;
        }

        /// <summary>A line for the run in progress, at a task boundary; nothing when stamping is off.</summary>
        public static void SampleCurrent()
        {
            Current?.Sample();
        }

        /// <summary>
        /// The text after the tag: the used-space change and the free space, in bytes, on
        /// <paramref name="drive"/>.
        /// </summary>
        public static string FormatLine(long usedDeltaBytes, long freeBytes, string drive)
        {
            return string.Format(CultureInfo.InvariantCulture, LINE_FORMAT,
                usedDeltaBytes / BYTES_PER_GB, freeBytes / BYTES_PER_GB, drive);
        }

        /// <summary>
        /// Read the text after the tag back, as <see cref="FormatLine"/> wrote it. False for any
        /// other text.
        /// </summary>
        public static bool TryParseLine(string text, out double usedDeltaGb, out double freeGb, out string drive)
        {
            usedDeltaGb = freeGb = 0;
            drive = null;
            var match = text == null ? null : LINE_REGEX.Match(text);
            if (match == null || !match.Success)
                return false;
            usedDeltaGb = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            freeGb = double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            drive = match.Groups[3].Value;
            return true;
        }

        private readonly object _lock = new object();
        private readonly IOspreyLog _log;
        private readonly Func<(long UsedBytes, long FreeBytes)?> _probe;
        private readonly string _drive;
        private readonly Timer _timer;
        private long? _startUsedBytes;
        private bool _disposed;

        /// <summary>
        /// A stamper over any <paramref name="probe"/>, which returns the drive's used and free
        /// bytes, or null when it cannot. Writes the start line. A
        /// <paramref name="intervalMs"/> of <see cref="Timeout.Infinite"/> runs no timer, so a test
        /// drives the samples itself.
        /// </summary>
        internal DiskStamper(IOspreyLog log, Func<(long UsedBytes, long FreeBytes)?> probe, string drive, int intervalMs)
        {
            _log = log;
            _probe = probe;
            _drive = drive;
            Sample();
            if (intervalMs != Timeout.Infinite)
                _timer = new Timer(_ => Sample(), null, intervalMs, intervalMs);
        }

        /// <summary>
        /// Write one line now. A failed probe writes nothing. The first successful probe is the
        /// baseline every later change is measured from.
        /// </summary>
        public void Sample()
        {
            lock (_lock)
            {
                if (_disposed)
                    return;
                WriteSample();
            }
        }

        /// <summary>Stop the timer, write the end line, and clear <see cref="Current"/>.</summary>
        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed)
                    return;
                // A timer callback already waiting on the lock sees _disposed and writes nothing,
                // so no line can follow the end line, or reach a log that has been closed.
                _disposed = true;
                _timer?.Dispose();
                WriteSample();
            }
            if (ReferenceEquals(Current, this))
                Current = null;
        }

        /// <summary>
        /// The root of the drive that holds <paramref name="directory"/> (the current directory
        /// when it is empty), or null when it has none.
        /// </summary>
        internal static string GetDriveRoot(string directory)
        {
            try
            {
                return Path.GetPathRoot(Path.GetFullPath(string.IsNullOrEmpty(directory) ? @"." : directory));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The used and free bytes of the drive at <paramref name="root"/>, or null when it cannot be read.</summary>
        internal static (long UsedBytes, long FreeBytes)? ProbeDrive(string root)
        {
            if (string.IsNullOrEmpty(root))
                return null;
            try
            {
                var drive = new DriveInfo(root);
                return (drive.TotalSize - drive.TotalFreeSpace, drive.AvailableFreeSpace);
            }
            catch (Exception)
            {
                // A UNC share, a drive gone away, a permission refused: no line, never a failure.
                return null;
            }
        }

        private void WriteSample()
        {
            try
            {
                var reading = _probe();
                if (!reading.HasValue)
                    return;
                if (!_startUsedBytes.HasValue)
                    _startUsedBytes = reading.Value.UsedBytes;
                _log.LogInfo(LogTag.DISK, FormatLine(reading.Value.UsedBytes - _startUsedBytes.Value,
                    reading.Value.FreeBytes, _drive));
            }
            catch (Exception)
            {
                // A probe or a log write that throws is a missed sample, not a failed run.
            }
        }

        /// <summary>The drive as the line names it: <c>D:</c> for <c>D:\</c>, the root itself otherwise.</summary>
        private static string FormatDrive(string root)
        {
            if (string.IsNullOrEmpty(root))
                return @"?";
            string trimmed = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return trimmed.Length > 0 ? trimmed : root;
        }
    }
}
