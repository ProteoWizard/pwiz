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

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Lib;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Times the load of the <c>.blib</c> libraries named by the <c>SKYLINE_BLIB_TIMING</c>
    /// environment variable (semicolon-separated paths), with the peak working set and the
    /// time to read a sample of spectra, and appends a line per library to the file named by
    /// <c>SKYLINE_BLIB_TIMING_OUT</c>. Skipped when the variable is unset, so the ordinary test
    /// run pays nothing: this is a measurement harness for the library load, which Skyline
    /// reads without a cache of its own since 26.1.
    /// </summary>
    [TestClass]
    public class BlibLoadTimingTest : AbstractUnitTest
    {
        private const string ENV_LIBRARIES = @"SKYLINE_BLIB_TIMING";
        private const string ENV_OUTPUT = @"SKYLINE_BLIB_TIMING_OUT";
        private const int SPECTRA_SAMPLE = 1000;

        [TestMethod]
        public void TestBlibLoadTiming()
        {
            string libraries = Environment.GetEnvironmentVariable(ENV_LIBRARIES);
            if (string.IsNullOrEmpty(libraries))
                return;
            string output = Environment.GetEnvironmentVariable(ENV_OUTPUT);
            foreach (string path in libraries.Split(';').Where(File.Exists))
                Report(output, TimeLibrary(path));
        }

        private static string TimeLibrary(string path)
        {
            var spec = new BiblioSpecLiteSpec(Path.GetFileNameWithoutExtension(path), path);
            var monitor = new DefaultFileLoadMonitor(new SilentProgressMonitor());
            var loadTime = Stopwatch.StartNew();
            var library = BiblioSpecLiteLibrary.Load(spec, monitor);
            loadTime.Stop();
            Assert.IsNotNull(library);
            long peakWorkingSet = Process.GetCurrentProcess().PeakWorkingSet64;
            var keys = library.Keys.ToList();
            int step = Math.Max(1, keys.Count / SPECTRA_SAMPLE);
            var spectrumTime = Stopwatch.StartNew();
            int spectraRead = 0;
            for (int i = 0; i < keys.Count; i += step)
            {
                if (library.TryLoadSpectrum(keys[i], out _))
                    spectraRead++;
            }
            spectrumTime.Stop();
            foreach (var stream in library.ReadStreams)
                stream.CloseStream();
            return string.Join("\t", DateTime.Now.ToString(@"yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                Path.GetFileName(path), Invariant(@"load_s={0:F2}", loadTime.Elapsed.TotalSeconds),
                Invariant(@"peak_ws_mb={0}", peakWorkingSet / (1024 * 1024)), Invariant(@"entries={0}", keys.Count),
                Invariant(@"spectra_read={0}", spectraRead), Invariant(@"spectra_s={0:F2}", spectrumTime.Elapsed.TotalSeconds),
                Invariant(@"threads={0}", ParallelEx.GetThreadCount(4)));
        }

        private static string Invariant(string format, object value)
        {
            return string.Format(CultureInfo.InvariantCulture, format, value);
        }

        private static void Report(string output, string line)
        {
            Console.WriteLine(line);
            if (!string.IsNullOrEmpty(output))
                File.AppendAllText(output, line + Environment.NewLine);
        }
    }
}
