using pwiz.BiblioSpec.IO;

namespace Pwiz.Tools.BiblioSpec.Tests;

/// <summary>
/// Round-trip confidence checks across the Phase 1 surface (BlibMaker
/// writer + LibReader reader) on the same .blib file in a single test
/// run, no cpp tools required. The cpp golden tests come later in
/// Phase 4 once BlibBuild.exe and BlibFilter.exe are ported.
/// </summary>
[TestClass]
public class RoundTrip
{
    /// <summary>
    /// Appending (BlibBuild without -o, Skyline's Append action) adds to the library in place
    /// through the writer: the existing rows stay, a protein already there is reused rather than
    /// repeated, the revision goes up by one, and the indexes dropped for the bulk insert are
    /// back exactly once.
    /// </summary>
    [TestMethod]
    public void Append_KeepsExistingRowsAndBumpsRevision()
    {
        string path = Path.Combine(Path.GetTempPath(),
            $"pwiz-sharp-bibliospec-{Guid.NewGuid():N}.blib");
        try
        {
            using (var maker = new BlibMaker())
            {
                maker.Overwrite = true;
                maker.SetLibName(path);
                maker.Init();
                var fileId = maker.AddFile("a.ms2", -1, "a.ssl", WorkflowType.Dda);
                maker.InsertSpectrum(Spectrum("PEPTIDEK", fileId, "P1"));
                maker.Commit();
            }
            using (var maker = new BlibMaker())
            {
                maker.SetLibName(path);
                maker.Init();
                Assert.IsFalse(maker.Overwrite, "An existing library is appended to unless overwrite is asked for.");
                Assert.AreEqual(1, maker.SpectrumCount, "The appended library's rows should be there to add to.");
                var fileId = maker.AddFile("b.ms2", -1, "b.ssl", WorkflowType.Dda);
                maker.InsertSpectrum(Spectrum("SAMPLERK", fileId, "P1", "P2"));
                maker.Commit();
            }

            using var conn = SqliteRoutine.Open(path, readOnly: true);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT numSpecs, majorVersion FROM LibInfo";
            using (var rdr = cmd.ExecuteReader())
            {
                Assert.IsTrue(rdr.Read());
                Assert.AreEqual(2, rdr.GetInt32(0));
                Assert.AreEqual(2, rdr.GetInt32(1), "Each build of the library is a revision.");
            }
            Assert.AreEqual(2, SqliteRoutine.TableRows(conn, "SpectrumSourceFiles"));
            Assert.AreEqual(2, SqliteRoutine.TableRows(conn, "Proteins"), "P1 is one protein however many builds map to it.");
            Assert.AreEqual(3, SqliteRoutine.TableRows(conn, "RefSpectraProteins"));
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name IN ('idxPeptide', 'idxRefIdProteins')";
            Assert.AreEqual(2L, cmd.ExecuteScalar(), "The indexes are back, once each.");
        }
        finally
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            try { if (File.Exists(path)) File.Delete(path); }
            catch (IOException) { /* best-effort temp-file cleanup */ }
        }
    }

    private static BlibSpectrumRecord Spectrum(string peptide, int fileId, params string[] proteins)
    {
        return new BlibSpectrumRecord
        {
            PeptideSeq = peptide,
            PeptideModSeq = peptide,
            PrecursorMz = 500.25,
            PrecursorCharge = 2,
            NumPeaks = 2,
            MzBlob = BlibWriter.CompressMzs(new[] { 200.1, 300.2 }),
            IntensityBlob = BlibWriter.CompressIntensities(new[] { 1.0f, 0.5f }),
            FileId = fileId,
            Proteins = proteins,
        };
    }

    /// <summary>
    /// A library carries the command line that made it: BlibBuild and BlibFilter record theirs
    /// in LibMetadata under their own keys, as Osprey and CarafeSharp do, and the library is
    /// not on disk until Commit moves the writer's temp over its name.
    /// </summary>
    [TestMethod]
    public void Commit_RecordsCommandLineInLibMetadata()
    {
        string path = Path.Combine(Path.GetTempPath(),
            $"pwiz-sharp-bibliospec-{Guid.NewGuid():N}.blib");
        try
        {
            using (var builder = new BlibBuilder())
            {
                builder.SetLibName(path);
                builder.Init();
                Assert.IsFalse(File.Exists(path), "The library should not be at its final name before Commit.");
                builder.RecordCommandLine("BlibBuild", BlibBuilder.CommandMetadataKey, new[] { "-o", "my lib.blib" });
                builder.Commit();
            }

            Assert.AreEqual("BlibBuild -o \"my lib.blib\"", BlibMetadata.Read(path, BlibBuilder.CommandMetadataKey));
            Assert.IsNull(BlibMetadata.Read(path, BlibFilter.CommandMetadataKey));
            // A redundant library has no RetentionTimes table, as BiblioSpec has always built it.
            using var conn = SqliteRoutine.Open(path, readOnly: true);
            Assert.AreEqual(1, SqliteRoutine.TableRows(conn, "LibMetadata"),
                "LibMetadata should hold the one command line.");
            Assert.IsFalse(SqliteRoutine.TableExists(conn, "RetentionTimes"));
        }
        finally
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            try { if (File.Exists(path)) File.Delete(path); }
            catch (IOException) { /* best-effort temp-file cleanup */ }
        }
    }

    /// <summary>
    /// Create an empty .blib, write the schema, commit, then open with
    /// LibReader and confirm the library is queryable and reports zero
    /// spectra. Smoke check that BlibMaker + LibReader agree on schema.
    /// </summary>
    [TestMethod]
    public void EmptyLibrary_RoundTrip_ReportsZeroSpectra()
    {
        string path = Path.Combine(Path.GetTempPath(),
            $"pwiz-sharp-bibliospec-{Guid.NewGuid():N}.blib");
        try
        {
            using (var maker = new BlibMaker())
            {
                maker.Overwrite = true;
                maker.SetLibName(path);
                maker.Init();
                maker.Commit();
            }

            Assert.IsTrue(File.Exists(path), "BlibMaker did not produce the file.");
            Assert.IsTrue(new FileInfo(path).Length > 0, "BlibMaker wrote an empty file.");

            using var reader = new LibReader(path);
            Assert.AreEqual(0, reader.CountAllSpec(),
                "Freshly-initialized library should have no RefSpectra rows.");
            Assert.AreEqual(0, reader.GetAllRefSpec().Count,
                "GetAllRefSpec on an empty library should return an empty list.");
            Assert.IsNull(reader.GetRefSpec(1),
                "GetRefSpec for a non-existent id should return null.");
        }
        finally
        {
            // System.Data.SQLite on Windows can hold a file lock for a brief
            // window after connection dispose; force-flush handles before delete.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            try { if (File.Exists(path)) File.Delete(path); }
            catch (IOException) { /* best-effort temp-file cleanup */ }
        }
    }

    /// <summary>
    /// Verify the schema BlibMaker wrote actually has every table BlibSchema
    /// declares. If a future schema change drops a table from one side without
    /// the other, this test catches it before LibReader's SELECTs blow up at
    /// runtime against real data.
    /// </summary>
    [TestMethod]
    public void Init_CreatesEveryDeclaredTable()
    {
        string path = Path.Combine(Path.GetTempPath(),
            $"pwiz-sharp-bibliospec-{Guid.NewGuid():N}.blib");
        try
        {
            using (var maker = new BlibMaker())
            {
                maker.Overwrite = true;
                maker.SetLibName(path);
                maker.Init();
                maker.Commit();
            }

            // Re-open as a plain SQLite connection and list the tables.
            using var conn = SqliteRoutine.Open(path, readOnly: true);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name";
            var found = new HashSet<string>(StringComparer.Ordinal);
            using (var rdr = cmd.ExecuteReader())
                while (rdr.Read()) found.Add(rdr.GetString(0));

            // Tables that Init() guarantees exist. Names line up with
            // BlibSchema's Create* constants.
            string[] expected =
            {
                "LibInfo",
                "RefSpectra",
                "Modifications",
                "RefSpectraPeaks",
                "Proteins",
                "RefSpectraProteins",
                "RefSpectraPeakAnnotations",
                "SpectrumSourceFiles",
                "ScoreTypes",
                "IonMobilityTypes",
            };
            foreach (var name in expected)
                Assert.IsTrue(found.Contains(name),
                    $"BlibMaker.Init() did not create table '{name}'. Tables present: "
                    + string.Join(", ", found));
        }
        finally
        {
            // System.Data.SQLite on Windows can hold a file lock for a brief
            // window after connection dispose; force-flush handles before delete.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            try { if (File.Exists(path)) File.Delete(path); }
            catch (IOException) { /* best-effort temp-file cleanup */ }
        }
    }

    /// <summary>
    /// Confirm Init+Commit stamps a valid schema version in LibInfo.
    /// minorVersion holds the schema version (cpp parity: it's overloaded —
    /// see BlibMaker.h:49); majorVersion is bumped on every commit and
    /// represents the library revision count (not schema version).
    /// </summary>
    [TestMethod]
    public void Init_StampsCurrentSchemaVersionInLibInfo()
    {
        string path = Path.Combine(Path.GetTempPath(),
            $"pwiz-sharp-bibliospec-{Guid.NewGuid():N}.blib");
        try
        {
            using (var maker = new BlibMaker())
            {
                maker.Overwrite = true;
                maker.SetLibName(path);
                maker.Init();
                maker.Commit();
            }

            using var conn = SqliteRoutine.Open(path, readOnly: true);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT majorVersion, minorVersion FROM LibInfo";
            using var rdr = cmd.ExecuteReader();
            Assert.IsTrue(rdr.Read(), "LibInfo had no rows.");
            Assert.IsTrue(rdr.GetInt32(0) >= 1,
                "majorVersion should be >= 1 after Commit (it's the library revision count, bumped on every commit).");
            Assert.AreEqual(BlibSchema.CurrentMinorVersion, rdr.GetInt32(1),
                "LibInfo.minorVersion does not match BlibSchema.CurrentMinorVersion (this is the actual schema version).");
        }
        finally
        {
            // System.Data.SQLite on Windows can hold a file lock for a brief
            // window after connection dispose; force-flush handles before delete.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            try { if (File.Exists(path)) File.Delete(path); }
            catch (IOException) { /* best-effort temp-file cleanup */ }
        }
    }
}
