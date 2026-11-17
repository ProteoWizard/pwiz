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

using System.Data.SQLite;
using pwiz.BiblioSpec.IO;

namespace Pwiz.Tools.BiblioSpec.Tests;

/// <summary>
/// The port's <see cref="LibReader"/>, on the shared <see cref="BlibReader"/>, keeps the cpp
/// view of a library that BlibSearch and BlibToMs2 output: which spectra each entry point
/// returns, what it sets the flanking residues and a missing retention time to, and the
/// modified sequence rebuilt from the Modifications table.
/// </summary>
[TestClass]
public class LibReaderTests
{
    [TestMethod]
    public void LibReader_KeepsTheCppViewOfTheLibrary()
    {
        string path = Path.Combine(Path.GetTempPath(), $"pwiz-sharp-libreader-{Guid.NewGuid():N}.blib");
        try
        {
            var mzs = Enumerable.Range(0, 64).Select(i => 200.0 + i * 1.5).ToArray();
            var intensities = Enumerable.Range(0, 64).Select(i => (float)(1000 - i * 10)).ToArray();
            long modifiedId, plainId, precursorOnlyId;
            using (var writer = new BlibWriter(path, "libreader-test.blib"))
            {
                long file = writer.AddSourceFile("a.mzML", null, null, BlibWriter.WORKFLOW_TYPE_DDA);
                writer.BeginBatch();
                // Compressed blobs, a retention time, flanking residues, and modifications at one
                // position twice and past the end of the sequence once (cpp sums and clamps).
                modifiedId = writer.AddSpectrum(new BlibSpectrumRecord
                {
                    PeptideSeq = "PEPTIDEK",
                    PeptideModSeq = "PEPT[+80.0]IDEK",
                    PrecursorMz = 500.25,
                    PrecursorCharge = 2,
                    PrevAA = "K",
                    NextAA = "R",
                    Copies = 3,
                    RetentionTime = 12.5,
                    FileId = file,
                    MzBlob = BlibWriter.CompressMzs(mzs),
                    IntensityBlob = BlibWriter.CompressIntensities(intensities),
                    NumPeaks = mzs.Length,
                    Modifications = new[] { new BlibModification(4, 79.966331), new BlibModification(4, 0.033669), new BlibModification(99, 15.994915) },
                });
                // Raw blobs and no retention time.
                plainId = writer.AddSpectrum(new BlibSpectrumRecord
                {
                    PeptideSeq = "ANOTHERK",
                    PeptideModSeq = "ANOTHERK",
                    PrecursorMz = 450.0,
                    PrecursorCharge = 3,
                    FileId = file,
                    MzBlob = BlibWriter.EncodeMzs(new[] { 100.0, 120.0 }, false),
                    IntensityBlob = BlibWriter.EncodeIntensities(new[] { 1f, 0.5f }, false),
                    NumPeaks = 2,
                });
                // A precursor-only entry: no peaks row, which cpp's inner join never returned.
                precursorOnlyId = writer.AddSpectrum(new BlibSpectrumRecord
                {
                    PeptideSeq = "SAMPLER",
                    PeptideModSeq = "SAMPLER",
                    PrecursorMz = 400.2,
                    PrecursorCharge = 2,
                    FileId = file,
                });
                writer.CommitBatch();
                writer.Complete();
            }

            using (var reader = new LibReader(path))
            {
                Assert.AreEqual(3, reader.CountAllSpec());

                // The m/z window of BlibSearch: numPeaks > minPeaks, flanking residues unread,
                // a NULL retention time as 0, the precursor-only row unseen.
                var window = reader.GetSpecInMzRange(400.0, 500.25, 1);
                CollectionAssert.AreEqual(new[] { (int)modifiedId, (int)plainId }, window.Select(s => s.LibSpecId).ToArray());
                var modified = window[0];
                Assert.AreEqual("PEPTIDEK", modified.Sequence);
                Assert.AreEqual("PEPT[+80.0]IDEK", modified.ModifiedSequence);
                Assert.AreEqual(500.25, modified.Mz);
                Assert.AreEqual(2, modified.Charge);
                Assert.AreEqual(3, modified.Copies);
                Assert.AreEqual(12.5, modified.RetentionTime);
                Assert.AreEqual(string.Empty, modified.PrevAa);
                Assert.AreEqual(string.Empty, modified.NextAa);
                Assert.AreEqual(mzs.Length, modified.RawPeaks.Count);
                Assert.AreEqual(mzs[63], modified.RawPeaks[63].Mz);
                Assert.AreEqual(intensities[63], modified.RawPeaks[63].Intensity);
                Assert.AreEqual(0.0, window[1].RetentionTime);
                CollectionAssert.AreEqual(new[] { 100.0, 120.0 }, window[1].RawPeaks.Select(p => p.Mz).ToArray());
                Assert.AreEqual(1, reader.GetSpecInMzRange(400.0, 500.25, 2).Count);
                Assert.AreEqual(0, reader.GetSpecInMzRange(400.0, 400.2, -1).Count);

                // The experimental filters: one charge, or a range; flanking residues "-".
                reader.LowMz = 0;
                reader.HighMz = 1000;
                reader.Charge = 3;
                var byCharge = reader.GetAllRefSpec();
                Assert.AreEqual(1, byCharge.Count);
                Assert.AreEqual((int)plainId, byCharge[0].LibSpecId);
                Assert.AreEqual("-", byCharge[0].PrevAa);
                Assert.AreEqual("-", byCharge[0].NextAa);
                reader.Charge = -1;
                reader.LowCharge = 2;
                reader.HighCharge = 2;
                Assert.AreEqual((int)modifiedId, reader.GetAllRefSpec().Single().LibSpecId);

                // By id: the stored modified sequence without a precision; null for the
                // precursor-only row and for an id the library lacks.
                var byId = reader.GetRefSpec((int)modifiedId);
                Assert.IsNotNull(byId);
                Assert.AreEqual("PEPT[+80.0]IDEK", byId.ModifiedSequence);
                Assert.AreEqual("-", byId.PrevAa);
                Assert.IsNull(reader.GetRefSpec((int)precursorOnlyId));
                Assert.IsNull(reader.GetRefSpec(99));

                // The enumeration of BlibToMs2, in id order, ending on a clobbered spectrum.
                Assert.IsTrue(reader.GetNextSpectrum(out var first));
                Assert.AreEqual((int)modifiedId, first.LibSpecId);
                Assert.AreEqual("-", first.PrevAa);
                Assert.IsTrue(reader.GetNextSpectrum(out var second));
                Assert.AreEqual((int)plainId, second.LibSpecId);
                Assert.IsFalse(reader.GetNextSpectrum(out var last));
                Assert.AreEqual(string.Empty, last.Sequence);
                Assert.AreEqual(0, last.RawPeaks.Count);
            }

            // With a precision, the modified sequence is rebuilt from Modifications: the masses
            // at one position summed, the position past the end clamped to it.
            using (var reader = new LibReader(path, 2))
            {
                Assert.AreEqual("PEPT[+80.00]IDEK[+15.99]", reader.GetRefSpec((int)modifiedId)!.ModifiedSequence);
            }

            // A library without the peaks table is refused at open, where cpp's prepare failed.
            using (var connection = SqliteRoutine.Open(path))
            using (var drop = new SQLiteCommand("DROP TABLE RefSpectraPeaks", connection))
            {
                drop.ExecuteNonQuery();
            }
            Assert.ThrowsException<BlibException>(() => new LibReader(path));
        }
        finally
        {
            SQLiteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (IOException)
            {
                // Best-effort temp-file cleanup.
            }
        }
    }
}
