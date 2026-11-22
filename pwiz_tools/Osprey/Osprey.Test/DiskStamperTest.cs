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
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// <c>--diskstamp</c>: the <see cref="LogTag.DISK"/> line perfviz reads, the stamper that writes
    /// it from a probe, and the rule that a probe which fails writes nothing and throws nothing.
    /// The timer is not exercised by waiting on it: the stamper is built without one and sampled
    /// directly, which is all the timer does. Every stamper here is a local instance with its own
    /// log and probe - none touches <see cref="DiskStamper.Current"/> or any other process-wide
    /// state - so the test is safe beside any other in a parallel run.
    /// </summary>
    [TestClass]
    public class DiskStamperTest
    {
        private const long GB = 1024L * 1024 * 1024;

        [TestMethod]
        public void TestDiskStamper()
        {
            AssertLineRoundTrips();
            AssertStamperSamples();
            AssertFailedProbeIsSilent();
            AssertConcurrentLinesStayWhole();
            AssertVolumeRoot();
            AssertDriveProbe();
            AssertDiskStampDirectory();
        }

        /// <summary>
        /// The text is invariant - a decimal point under fr-FR too - signed, and reads back as it
        /// was written; anything else does not parse.
        /// </summary>
        private static void AssertLineRoundTrips()
        {
            using (new CultureScope(CultureInfo.GetCultureInfo(@"fr-FR")))
            {
                string grown = DiskStamper.FormatLine(12 * GB + GB / 4, 812 * GB + GB / 2, @"D:");
                AssertParses(grown, 12.25, 812.5, @"D:");
                StringAssert.Contains(grown, @"+12.25");
                AssertParses(DiskStamper.FormatLine(-GB / 2, GB, @"D:"), -0.5, 1, @"D:");
                AssertParses(DiskStamper.FormatLine(0, 0, @"/"), 0, 0, @"/");
                // A mount point with spaces is the last field and reads back whole.
                AssertParses(DiskStamper.FormatLine(GB, GB, @"/mnt/run data"), 1, 1, @"/mnt/run data");
            }
            Assert.IsFalse(DiskStamper.TryParseLine(null, out _, out _, out _));
            Assert.IsFalse(DiskStamper.TryParseLine(string.Empty, out _, out _, out _));
            Assert.IsFalse(DiskStamper.TryParseLine(@"Demo:done (1.0s)", out _, out _, out _));
        }

        /// <summary>
        /// The start line is a change of zero from the first reading; later lines measure from it;
        /// Dispose writes the end line and nothing follows it.
        /// </summary>
        private static void AssertStamperSamples()
        {
            var lines = new List<string>();
            var drive = new FakeDrive(100 * GB, 900 * GB);
            var stamper = new DiskStamper(OspreyLog.FromDelegate(lines.Add), drive.Probe, @"D:", Timeout.Infinite);
            AssertDiskLines(lines, 0.0);
            drive.Grow(3 * GB);
            stamper.Sample();
            AssertDiskLines(lines, 0.0, 3.0);
            drive.Grow(-4 * GB);
            stamper.Dispose();
            AssertDiskLines(lines, 0.0, 3.0, -1.0);
            AssertParses(StripTag(lines[1]), 3.0, 897.0, @"D:");
            stamper.Sample();
            stamper.Dispose();
            Assert.AreEqual(3, lines.Count);
        }

        /// <summary>
        /// A probe that cannot read the drive, or that throws, writes no line and fails nothing,
        /// and the first reading that succeeds is the baseline.
        /// </summary>
        private static void AssertFailedProbeIsSilent()
        {
            var lines = new List<string>();
            var drive = new FakeDrive(10 * GB, 5 * GB) { Unreadable = true };
            var stamper = new DiskStamper(OspreyLog.FromDelegate(lines.Add), drive.Probe, @"D:", Timeout.Infinite);
            Assert.AreEqual(0, lines.Count);
            drive.Unreadable = false;
            drive.Throws = true;
            stamper.Sample();
            Assert.AreEqual(0, lines.Count);
            drive.Throws = false;
            stamper.Sample();
            drive.Grow(2 * GB);
            stamper.Sample();
            AssertDiskLines(lines, 0.0, 2.0);
            drive.Throws = true;
            stamper.Dispose();
            Assert.AreEqual(2, lines.Count);

            // A log that throws is a missed sample too.
            var failingLog = OspreyLog.FromDelegate(_ => throw new IOException());
            new DiskStamper(failingLog, new FakeDrive(GB, GB).Probe, @"D:", Timeout.Infinite).Dispose();
        }

        /// <summary>
        /// The timer thread writes while the pipeline does. Through the
        /// <see cref="CommandStatusWriter"/> Program gives the stamper - the writer the pipeline's
        /// lines reach too - every line arrives whole: no interleaving, nothing lost, nothing thrown.
        /// A 1 ms timer stands in for the minute one, and the main thread keeps writing until the
        /// timer has written several lines, so the two writers really overlap.
        /// </summary>
        private static void AssertConcurrentLinesStayWhole()
        {
            const int timerLines = 5;
            var sink = new StringWriter();
            var drive = new FakeDrive(GB, GB);
            int mainLines = 0;
            using (var writer = new CommandStatusWriter(sink))
            {
                var stamper = new DiskStamper(OspreyLog.FromDelegate(line => writer.WriteLine(line)),
                    drive.Probe, @"D:", 1);
                var timeout = Stopwatch.StartNew();
                while (drive.ProbeCount < timerLines + 1 && timeout.Elapsed < TimeSpan.FromSeconds(30))
                    writer.WriteLine(@"main " + mainLines++);
                stamper.Dispose();
            }
            var lines = sink.ToString().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var diskLines = lines.Where(line => !line.StartsWith(@"main ")).ToList();
            CollectionAssert.AreEqual(Enumerable.Range(0, mainLines).Select(i => @"main " + i).ToList(),
                lines.Where(line => line.StartsWith(@"main ")).ToList());
            // The start line, the timer's, and the end line.
            Assert.IsTrue(diskLines.Count >= timerLines + 2, diskLines.Count.ToString(CultureInfo.InvariantCulture));
            AssertDiskLines(diskLines, diskLines.Select(_ => 0.0).ToArray());
        }

        /// <summary>
        /// The volume is the mount whose root is the longest whole-segment prefix of the path, so a
        /// run under a mounted volume measures that volume and not the root it hangs from.
        /// </summary>
        private static void AssertVolumeRoot()
        {
            var unixRoots = new[] { @"/", @"/mnt/data", @"/mnt/data2", @"/home" };
            Assert.AreEqual(@"/mnt/data", DiskStamper.FindVolumeRoot(@"/mnt/data/runs/x", unixRoots, false));
            Assert.AreEqual(@"/mnt/data", DiskStamper.FindVolumeRoot(@"/mnt/data", unixRoots, false));
            Assert.AreEqual(@"/mnt/data2", DiskStamper.FindVolumeRoot(@"/mnt/data2/x", unixRoots, false));
            Assert.AreEqual(@"/", DiskStamper.FindVolumeRoot(@"/mnt/database/x", unixRoots, false));
            Assert.AreEqual(@"/", DiskStamper.FindVolumeRoot(@"/MNT/DATA/x", unixRoots, false));
            // Backslash separates segments only on Windows.
            if (Path.DirectorySeparatorChar == '\\')
            {
                var windowsRoots = new[] { @"C:\", @"D:\", @"D:\mounts\fast\" };
                Assert.AreEqual(@"D:\mounts\fast\", DiskStamper.FindVolumeRoot(@"d:\Mounts\Fast\run", windowsRoots, true));
                Assert.AreEqual(@"D:\", DiskStamper.FindVolumeRoot(@"D:\test\run", windowsRoots, true));
                Assert.IsNull(DiskStamper.FindVolumeRoot(@"E:\x", windowsRoots, true));
            }
            Assert.IsNotNull(DiskStamper.GetVolumeRoot(string.Empty));
        }

        /// <summary>
        /// The real probe reads the drive this test runs on, and returns nothing for a root it
        /// cannot query.
        /// </summary>
        private static void AssertDriveProbe()
        {
            string root = DiskStamper.GetVolumeRoot(Path.GetTempPath());
            Assert.IsNotNull(root);
            var reading = DiskStamper.ProbeDrive(root);
            Assert.IsNotNull(reading);
            Assert.IsTrue(reading.Value.UsedBytes > 0);
            Assert.IsTrue(reading.Value.FreeBytes >= 0);
            Assert.IsFalse(DiskStamper.ProbeDrive(null).HasValue);
            Assert.IsFalse(DiskStamper.ProbeDrive(string.Empty).HasValue);
            Assert.IsFalse(DiskStamper.ProbeDrive(@"\\no-such-server\no-such-share").HasValue);
        }

        /// <summary>
        /// The directory is where the first input's artifacts go, as the artifact resolution says;
        /// with no inputs, --output-dir, else -o's folder, else the current directory.
        /// </summary>
        private static void AssertDiskStampDirectory()
        {
            string outputDir = Path.Combine(Path.GetTempPath(), @"runs", @"x");
            string blib = Path.Combine(Path.GetTempPath(), @"out", @"result.blib");
            string input = Path.Combine(Path.GetTempPath(), @"data", @"a.mzML");
            var asked = new List<string>();
            string Resolve(string path)
            {
                asked.Add(path);
                return outputDir;
            }
            var withInputs = new OspreyConfig { OutputBlib = blib, InputFiles = new List<string> { input, @"b.mzML" } };
            Assert.AreEqual(outputDir, Program.GetDiskStampDirectory(withInputs, Resolve));
            CollectionAssert.AreEqual(new[] { input }, asked);
            Assert.AreEqual(outputDir, Program.GetDiskStampDirectory(new OspreyConfig { OutputDir = outputDir, OutputBlib = blib }, Resolve));
            Assert.AreEqual(Path.GetDirectoryName(blib), Program.GetDiskStampDirectory(new OspreyConfig { OutputBlib = blib }, Resolve));
            Assert.AreEqual(string.Empty, Program.GetDiskStampDirectory(new OspreyConfig(), Resolve));
            Assert.AreEqual(1, asked.Count);
        }

        /// <summary>Every line carries the tag and the expected used-space change, in order.</summary>
        private static void AssertDiskLines(IList<string> lines, params double[] usedDeltaGb)
        {
            Assert.AreEqual(usedDeltaGb.Length, lines.Count);
            for (int i = 0; i < lines.Count; i++)
            {
                Assert.IsTrue(DiskStamper.TryParseLine(StripTag(lines[i]), out double delta, out _, out _), lines[i]);
                Assert.AreEqual(usedDeltaGb[i], delta, 1e-9, lines[i]);
            }
        }

        private static string StripTag(string line)
        {
            string prefix = LogTag.DISK.Format(string.Empty);
            StringAssert.StartsWith(line, prefix);
            return line.Substring(prefix.Length);
        }

        private static void AssertParses(string text, double usedDeltaGb, double freeGb, string drive)
        {
            Assert.IsTrue(DiskStamper.TryParseLine(text, out double parsedDelta, out double parsedFree, out string parsedDrive), text);
            Assert.AreEqual(usedDeltaGb, parsedDelta, 1e-9, text);
            Assert.AreEqual(freeGb, parsedFree, 1e-9, text);
            Assert.AreEqual(drive, parsedDrive, text);
        }

        /// <summary>A drive whose readings a test sets, and which can fail either way a real one can.</summary>
        private sealed class FakeDrive
        {
            private long _usedBytes;
            private long _freeBytes;
            private int _probeCount;

            public FakeDrive(long usedBytes, long freeBytes)
            {
                _usedBytes = usedBytes;
                _freeBytes = freeBytes;
            }

            /// <summary>The probe returns no reading.</summary>
            public bool Unreadable { get; set; }

            /// <summary>The probe throws.</summary>
            public bool Throws { get; set; }

            /// <summary>How many times the probe has been asked, from any thread.</summary>
            public int ProbeCount => Volatile.Read(ref _probeCount);

            public void Grow(long bytes)
            {
                _usedBytes += bytes;
                _freeBytes -= bytes;
            }

            public (long UsedBytes, long FreeBytes)? Probe()
            {
                Interlocked.Increment(ref _probeCount);
                if (Throws)
                    throw new IOException();
                if (Unreadable)
                    return null;
                return (_usedBytes, _freeBytes);
            }
        }
    }
}
