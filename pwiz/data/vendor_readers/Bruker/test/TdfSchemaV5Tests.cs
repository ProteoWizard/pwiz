using System.Data.SQLite;
using Pwiz.Vendor.Bruker;

namespace Pwiz.Vendor.Bruker.Tests;

/// <summary>
/// TDF schema v5 (TDF-SDK 5.0) moved MS/MS metadata into a <c>Fragmentation*</c> table family
/// and kept the old names - <c>PasefFrameMsMsInfo</c>, <c>DiaFrameMsMsInfo</c>,
/// <c>DiaFrameMsMsWindows</c>, <c>PrmFrameMsMsInfo</c> - as views. Acquisition-mode detection
/// must accept a view wherever it accepted a table. These tests build a minimal
/// <c>analysis.tdf</c> where those names are views; no SDK or data file is needed, so they run
/// on a CI leg built without vendor archives.
/// </summary>
[TestClass]
public class TdfSchemaV5Tests
{
    [TestMethod]
    public void TdfMetadata_DetectsAcquisitionModeFromViews()
    {
        // (MS/MS view to create, expected PASEF, expected diaPASEF, expected prm-PASEF)
        var cases = new (string View, bool Pasef, bool Dia, bool Prm)[]
        {
            ("PasefFrameMsMsInfo", true, false, false),
            ("DiaFrameMsMsInfo", false, true, false),
            ("PrmFrameMsMsInfo", false, false, true),
        };

        foreach (var c in cases)
        {
            string dotD = CreateV5StyleTdf(c.View);
            try
            {
                using var metadata = new TdfMetadata(dotD);
                Assert.AreEqual(c.Pasef, metadata.HasPasefData, $"{c.View}: HasPasefData");
                Assert.AreEqual(c.Dia, metadata.HasDiaPasefData, $"{c.View}: HasDiaPasefData");
                Assert.AreEqual(c.Prm, metadata.HasPrmPasefData, $"{c.View}: HasPrmPasefData");

                if (c.Dia)
                {
                    var groups = metadata.EnumerateDiaWindowGroups().ToList();
                    Assert.AreEqual(2, groups.Count, "diaPASEF window rows read through the DiaFrameMsMsWindows view");
                    Assert.AreEqual(500.0, groups[0].IsolationMz);
                    Assert.AreEqual(700.0, groups[1].IsolationMz);
                }
            }
            finally
            {
                SQLiteConnection.ClearAllPools();
                Directory.Delete(dotD, recursive: true);
            }
        }
    }

    /// <summary>
    /// Writes a .d directory whose <c>analysis.tdf</c> has the tables <see cref="TdfMetadata"/>'s
    /// constructor reads, plus <paramref name="msmsView"/> (and, for diaPASEF, the windows view)
    /// as a view over a backing table, the way schema v5 defines them.
    /// </summary>
    private static string CreateV5StyleTdf(string msmsView)
    {
        string dotD = Path.Combine(Path.GetTempPath(), $"TdfSchemaV5_{msmsView}_{Guid.NewGuid():N}.d");
        Directory.CreateDirectory(dotD);
        string tdf = Path.Combine(dotD, "analysis.tdf");

        using var conn = new SQLiteConnection($"Data Source={tdf}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            CREATE TABLE GlobalMetadata (Key TEXT PRIMARY KEY, Value TEXT);
            INSERT INTO GlobalMetadata VALUES ('SchemaVersionMajor', '5');
            CREATE TABLE Frames (Id INTEGER PRIMARY KEY, NumScans INTEGER, TimsCalibration INTEGER);
            INSERT INTO Frames VALUES (1, 100, 1), (2, 100, 1);
            CREATE TABLE FragmentationFrameToExperiment (Frame INTEGER, Experiment INTEGER);
            INSERT INTO FragmentationFrameToExperiment VALUES (2, 1);
            CREATE VIEW {msmsView} AS SELECT Frame, Experiment AS WindowGroup FROM FragmentationFrameToExperiment;
            CREATE TABLE FragmentationIsolationWindow (
                Experiment INTEGER, ScanNumBegin INTEGER, ScanNumEnd INTEGER,
                IsolationMz REAL, IsolationWidth REAL, CollisionEnergy REAL);
            INSERT INTO FragmentationIsolationWindow VALUES (1, 0, 50, 500, 25, 30), (1, 50, 100, 700, 25, 35);
            CREATE VIEW DiaFrameMsMsWindows AS
                SELECT Experiment AS WindowGroup, ScanNumBegin, ScanNumEnd, IsolationMz, IsolationWidth, CollisionEnergy
                FROM FragmentationIsolationWindow;";
        cmd.ExecuteNonQuery();
        return dotD;
    }
}
