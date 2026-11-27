/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
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
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Quasar.Core.Output;
using pwiz.Quasar.IO;

namespace pwiz.Quasar.Test
{
    /// <summary>
    /// The clusters sidecar (spec Stage 4): windows written as separate row groups, from several
    /// threads, read back with every column and the footer metadata.
    /// </summary>
    [TestClass]
    public class ClustersSidecarTest
    {
        [TestMethod]
        public void TestClustersSidecarRoundTrip()
        {
            string path = Path.Combine(Path.GetTempPath(), @"quasar-sidecar-" + Guid.NewGuid().ToString(@"N") + @".parquet");
            try
            {
                var windows = Enumerable.Range(0, 4).Select(w => Enumerable.Range(0, 5 + w).Select(i =>
                    new ClusterMemberRow(w, 10 + i % 2, @"run=r window=" + w + @" cluster=" + (10 + i % 2), i,
                        300.0 + 10.0 * i + w, i % 3, i == 0, 100.0 * (i + 1), 1.0 / (i + 1), 9.6 + i, 20.0 + w, 19.9 + w,
                        20.1 + w)).ToList()).ToList();
                using (var writer = new ClustersSidecarWriter(path, new Dictionary<string, string> { [@"tool"] = @"Quasar" }))
                {
                    System.Threading.Tasks.Parallel.ForEach(windows, rows => writer.Write(rows));
                    writer.Write(new List<ClusterMemberRow>());
                }

                var read = ClustersSidecarWriter.Read(path, out var metadata);
                Assert.AreEqual(@"Quasar", metadata[@"tool"]);
                Assert.AreEqual(ClustersSidecarWriter.FORMAT_VERSION, metadata[ClustersSidecarWriter.KEY_FORMAT_VERSION]);
                var expected = windows.SelectMany(w => w).OrderBy(r => r.Window).ThenBy(r => r.TraceId).ToList();
                var actual = read.OrderBy(r => r.Window).ThenBy(r => r.TraceId).ToList();
                Assert.AreEqual(expected.Count, actual.Count);
                for (int i = 0; i < expected.Count; i++)
                {
                    var e = expected[i];
                    var a = actual[i];
                    Assert.AreEqual(e.Cluster, a.Cluster);
                    Assert.AreEqual(e.SpectrumId, a.SpectrumId);
                    Assert.AreEqual(e.Mz, a.Mz);
                    Assert.AreEqual(e.Charge, a.Charge);
                    Assert.AreEqual(e.IsAnchor, a.IsAnchor);
                    Assert.AreEqual(e.Area, a.Area);
                    Assert.AreEqual(e.SharedFraction, a.SharedFraction);
                    Assert.AreEqual(e.DeltaChiSquare, a.DeltaChiSquare);
                    Assert.AreEqual(e.ApexRt, a.ApexRt);
                    Assert.AreEqual(e.StartRt, a.StartRt);
                    Assert.AreEqual(e.EndRt, a.EndRt);
                }
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
