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
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// <c>&lt;stem&gt;.training.parquet</c> as a file: every column round-trips (NaN included),
    /// the per-ion blobs keep their slot count, the footer carries the format version and the
    /// caller's keys, a run with nothing to export still writes a readable zero-row file, and
    /// docs/22-training-export.md - the contract CarafeSharp reads - names every column.
    /// </summary>
    [TestClass]
    public class TrainingExportParquetTest
    {
        [TestMethod]
        public void TestTrainingExportParquet()
        {
            string dir = Path.Combine(Path.GetTempPath(), @"osprey_train_" + Path.GetRandomFileName());
            Directory.CreateDirectory(dir);
            try
            {
                AssertRoundTrip(Path.Combine(dir, @"a.training.parquet"), false);
                AssertRoundTrip(Path.Combine(dir, @"b.training.parquet"), true);
                AssertZeroRowFile(Path.Combine(dir, @"empty.training.parquet"));
                AssertPathBesideTheRunsProducts(dir);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
            AssertEveryColumnIsDocumented();
        }

        private static void AssertRoundTrip(string path, bool withXics)
        {
            var records = new List<TrainingRecord> { Record(2, 8, withXics), Record(9, 12, withXics) };
            var footer = new Dictionary<string, string> { { @"osprey.file_name", @"run1" } };
            TrainingExportParquet.Write(path, records, footer, withXics);
            var read = TrainingExportParquet.Read(path, out var metadata);

            Assert.AreEqual(TrainingExportParquet.FORMAT_VERSION.ToString(CultureInfo.InvariantCulture),
                metadata[TrainingExportParquet.KEY_FORMAT_VERSION]);
            Assert.AreEqual(@"run1", metadata[@"osprey.file_name"]);
            Assert.AreEqual(records.Count, read.Count);
            for (int i = 0; i < records.Count; i++)
            {
                var expected = records[i];
                var actual = read[i];
                // Every property, by reflection, so a column added to the record and forgotten
                // in the writer (or read back into the wrong field) fails here.
                foreach (var property in typeof(TrainingRecord).GetProperties())
                {
                    object e = property.GetValue(expected);
                    object a = property.GetValue(actual);
                    if (e is Array ea)
                    {
                        if (!withXics && (property.Name == nameof(TrainingRecord.XicRts) ||
                                          property.Name == nameof(TrainingRecord.XicIntensities)))
                        {
                            Assert.IsNull(a, property.Name + @" is written only with the XIC matrix");
                            continue;
                        }
                        var aa = (Array)a;
                        Assert.IsNotNull(aa, property.Name);
                        CollectionAssert.AreEqual(ea, aa, property.Name);
                    }
                    else
                    {
                        Assert.AreEqual(e, a, property.Name);
                    }
                }
                Assert.AreEqual(expected.NSlots, actual.IonMz.Length, @"a per-ion blob holds one value per slot");
                Assert.AreEqual(expected.NSlots, actual.PolishOutlierZ.Length);
                Assert.AreEqual(expected.NSlots, actual.NFiniteScans.Length);
            }
        }

        private static void AssertZeroRowFile(string path)
        {
            TrainingExportParquet.Write(path, new List<TrainingRecord>(), new Dictionary<string, string>
            {
                { @"osprey.training_export.rows", @"0" }
            }, false);
            Assert.IsTrue(File.Exists(path), @"a run with nothing to export still gets its file");
            var read = TrainingExportParquet.Read(path, out var metadata);
            Assert.AreEqual(0, read.Count);
            Assert.AreEqual(@"0", metadata[@"osprey.training_export.rows"]);
            Assert.IsTrue(metadata.ContainsKey(TrainingExportParquet.KEY_FORMAT_VERSION));
        }

        private static void AssertPathBesideTheRunsProducts(string dir)
        {
            string saved = ArtifactPaths.OutputDir;
            try
            {
                ArtifactPaths.OutputDir = dir;
                Assert.AreEqual(Path.Combine(dir, @"run1.training.parquet"),
                    TrainingExportParquet.PathFor(Path.Combine(dir, @"data", @"run1.mzML")));
            }
            finally
            {
                ArtifactPaths.OutputDir = saved;
            }
        }

        /// <summary>
        /// The schema document is the contract, so every column the writer emits must appear in
        /// it as a code span. A column added to the writer without its documentation fails here.
        /// </summary>
        private static void AssertEveryColumnIsDocumented()
        {
            string doc = File.ReadAllText(Path.Combine(OspreySourceRoot(), @"docs", @"22-training-export.md"));
            foreach (string column in TrainingExportParquet.ColumnNames(true))
                StringAssert.Contains(doc, @"`" + column + @"`", column + @" must be documented in docs/22-training-export.md");
        }

        private static TrainingRecord Record(uint entryId, int length, bool withXics)
        {
            int n = FragmentLadder.SlotCount(length);
            float[] F(float seed) => Enumerable.Range(0, n).Select(i => i % 5 == 0 ? float.NaN : seed + i).ToArray();
            var record = new TrainingRecord
            {
                EntryId = entryId,
                IsDecoy = false,
                IsEntrapment = entryId % 2 == 1,
                PeptideKind = entryId % 2 == 1 ? @"p_target" : @"target",
                Sequence = new string('A', length - 1) + @"K",
                ModifiedSequence = new string('A', length - 1) + @"K",
                ModPositions = entryId % 2 == 1 ? new[] { 2 } : Array.Empty<int>(),
                ModMasses = entryId % 2 == 1 ? new[] { 57.021464 } : Array.Empty<double>(),
                ModUnimodIds = entryId % 2 == 1 ? new[] { 4 } : Array.Empty<int>(),
                Charge = 2,
                PrecursorMz = 456.789,
                LibraryRt = 12.5,
                ProteinIds = entryId % 2 == 1 ? @"P1;P2" : null,
                FileName = @"run1",
                ScanNumber = 1234,
                ApexRt = 10.5,
                StartRt = 10.2,
                EndRt = 10.8,
                NPeakScans = 3,
                IsolationLower = 454,
                IsolationUpper = 458,
                BoundsArea = 1e6,
                CoelutionSum = 2.5,
                Score = 3.25,
                RunPrecursorQ = 0.001,
                RunPeptideQ = 0.002,
                ExperimentPrecursorQ = 0.0005,
                ExperimentPeptideQ = double.NaN,
                ExperimentProteinQ = 0.01,
                Pep = 0.02,
                ApexTic = 5e6,
                ExplainedIntensity = 0.3,
                NSlots = n,
                NIonsApplicable = n,
                NIonsObserved = n / 2,
                MpFitted = true,
                MpConverged = entryId % 2 == 0,
                MpIterations = 4,
                MpOverall = 9.5,
                MpCosine = 0.987654321,
                MpCosineParity = true,
                MpResidualMad = 0.05,
                MpNCore = 6,
                MpNFragmentsUsed = 6,
                BoundaryStartRatioMedian = 0.1,
                BoundaryEndRatioMedian = 0.2,
                NCoelutingClaimants = 3,
                NSameApexClaimants = 1,
                DdcNeighborCount = 0,
                IonMz = Enumerable.Range(0, n).Select(i => 100.0 + i * 0.5).ToArray(),
                IonFlags = Enumerable.Range(0, n).Select(i => (byte)(i % 256)).ToArray(),
                ApexIntensity = F(1),
                ApexMzError = F(2),
                LibraryRelIntensity = F(3),
                NFiniteScans = Enumerable.Range(0, n).Select(i => (ushort)i).ToArray(),
                XicStart = F(4),
                XicEnd = F(5),
                XicMax = F(6),
                CorrPolish = F(7),
                CorrReference = F(8),
                PolishRowEffect = F(9),
                PolishR2 = F(10),
                PolishPosResidMax = F(11),
                PolishApexResidual = F(12),
                PolishOutlierZ = F(13),
                PolishApexRatio = F(14),
                PolishRelIntensity = F(15),
                SharedApexN = Enumerable.Range(0, n).Select(i => (byte)(i % 3)).ToArray(),
                SharedCoeluteN = Enumerable.Range(0, n).Select(i => (byte)(i % 4)).ToArray(),
                MinClaimantQ = F(16),
            };
            if (withXics)
            {
                record.XicRts = new[] { 10.2, 10.5, 10.8 };
                record.XicIntensities = Enumerable.Range(0, n * 3).Select(i => (float)i).ToArray();
            }
            return record;
        }

        private static string OspreySourceRoot()
        {
            string dir = Path.GetDirectoryName(typeof(TrainingExportParquetTest).Assembly.Location);
            while (!string.IsNullOrEmpty(dir))
            {
                if (File.Exists(Path.Combine(dir, @"Osprey.sln")))
                    return dir;
                dir = Path.GetDirectoryName(dir);
            }
            Assert.Fail(@"Osprey source root not found above the test assembly");
            return null;
        }
    }
}
