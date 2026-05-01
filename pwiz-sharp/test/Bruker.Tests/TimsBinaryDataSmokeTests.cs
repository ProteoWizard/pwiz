using Pwiz.Vendor.Bruker;

namespace Pwiz.Vendor.Bruker.Tests;

/// <summary>
/// Smoke tests for the native <c>timsdata.dll</c> P/Invoke wrappers below the
/// <see cref="Reader_Bruker"/> layer. Tests are grouped by phase / wrapper class:
/// <list type="bullet">
///   <item><description>TimsBinaryData (Phase A): native handle + scan-read smoke</description></item>
///   <item><description>TdfMetadata (Phase B): TDF SQLite metadata, exercised against diaPASEF.d</description></item>
///   <item><description>TsfMetadata + TsfBinaryData (Phase D): TSF (timsTOF MALDI) variant</description></item>
///   <item><description>Reader_Bruker dispatch over diaPASEF.d (Phase C): identify + per-scan + combineIMS</description></item>
/// </list>
/// Each method aggregates several related assertions for a single fixture so that a failure
/// in one wrapper doesn't fan out to many MSTest entries.
/// </summary>
[TestClass]
public class TimsBinaryDataSmokeTests
{
    /// <summary>Injected by MSTest; lets us surface per-test diagnostic output.</summary>
    public TestContext? TestContext { get; set; }

    private static string? FindTestD()
    {
        // Walk up from the test output directory looking for the Bruker test fixture.
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string candidate = Path.Combine(dir, "pwiz", "data", "vendor_readers", "Bruker",
                "Reader_Bruker_Test.data", "diaPASEF.d");
            if (Directory.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    private static string? FindTestTsf(string fixtureLeaf = "20percLaser_100fold_1_0_H6_MS.d")
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string candidate = Path.Combine(dir, "pwiz", "data", "vendor_readers", "Bruker",
                "Reader_Bruker_Test.data", fixtureLeaf);
            if (Directory.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    [TestMethod]
    public void TimsBinaryData_OpenScanReadAndPureMath()
    {
        // Bogus path surfaces as DirectoryNotFoundException (wrapper translates the native
        // open-failure code). Pure-math CCS round-trip needs no fixture and is covered here
        // alongside the open-handle smoke so failures cluster on the same MSTest entry.
        Assert.ThrowsException<DirectoryNotFoundException>(
            () => new TimsBinaryData("C:/definitely/not/a/real/path.d"));

        // Mason-Schamp CCS ↔ 1/K0 round-trip: pure math, no native handle.
        const double ook0 = 0.85;
        const int charge = 2;
        const double mz = 600.0;
        double ccs = TimsBinaryData.OneOverK0ToCcs(ook0, charge, mz);
        double back = TimsBinaryData.CcsToOneOverK0(ccs, charge, mz);
        Assert.AreEqual(ook0, back, 1e-6, "CCS/(1/K0) round-trip should be lossless");

        string? path = FindTestD();
        if (path is null) { Assert.Inconclusive("diaPASEF.d not found in test data tree."); return; }

        using var tims = new TimsBinaryData(path);
        Assert.AreNotEqual(0UL, tims.Handle, "open should yield a non-zero native handle");

        // Frame ids are 1-based in timsdata.
        var frame = tims.ReadScans(frameId: 1, scanBegin: 0, scanEnd: 10, performMzConversion: true);
        Assert.AreEqual(10, frame.NumScans);
        Assert.IsTrue(frame.TotalNumPeaks >= 0);

        // When there ARE peaks, m/z values should be sorted within each scan.
        for (int s = 0; s < frame.NumScans; s++)
        {
            var mzArr = frame.GetScanMzs(s);
            for (int i = 1; i < mzArr.Length; i++)
                Assert.IsTrue(mzArr[i] >= mzArr[i - 1], $"scan {s}: m/z not sorted at index {i}");
        }
    }

    [TestMethod]
    public void TdfMetadata_DiaPasef_GlobalMetadataAndFrameEnumeration()
    {
        string? path = FindTestD();
        if (path is null) { Assert.Inconclusive("diaPASEF.d not found in test data tree."); return; }

        using var tdf = new TdfMetadata(path);

        // Global metadata is dumped to TestContext to aid diagnosis when tests below fail.
        foreach (var kv in tdf.GlobalMetadata)
            TestContext?.WriteLine($"{kv.Key} = {kv.Value}");
        Assert.IsTrue(tdf.GlobalMetadata.Count > 0, "GlobalMetadata table should not be empty");

        // Structural assertions on the diaPASEF fixture.
        Assert.IsTrue(tdf.FrameCount > 0);
        Assert.IsTrue(tdf.MaxNumScans > 0);
        Assert.IsTrue(tdf.CalibrationCount >= 1);
        Assert.IsTrue(tdf.HasDiaPasefData || tdf.HasPasefData,
            "Expected diaPASEF.d to have PASEF or DIA-PASEF tables.");

        var (low, high) = tdf.MzAcquisitionRange;
        Assert.IsTrue(low > 0 && high > low, $"acquisition m/z range looks bogus: low={low} high={high}");

        // EnumerateFrames returns ids in ascending order, RT ≥ 0, and the count matches FrameCount.
        long prev = 0;
        int count = 0;
        foreach (var fr in tdf.EnumerateFrames())
        {
            Assert.IsTrue(fr.FrameId > prev, $"Frames not in id order: {prev} then {fr.FrameId}");
            Assert.IsTrue(fr.RetentionTimeSeconds >= 0);
            Assert.IsTrue(fr.NumScans > 0);
            prev = fr.FrameId;
            count++;
        }
        Assert.AreEqual(tdf.FrameCount, count);

        // Cross-check TdfMetadata with TimsBinaryData on the first frame: NumPeaks reported by
        // the metadata table should match TotalNumPeaks returned from tims_read_scans_v2.
        using var tims = new TimsBinaryData(path);
        var first = tdf.EnumerateFrames().First();
        var binFrame = tims.ReadScans(first.FrameId, 0, (uint)first.NumScans, performMzConversion: true);
        Assert.AreEqual(first.NumScans, binFrame.NumScans);
        Assert.AreEqual((int)first.NumPeaks, binFrame.TotalNumPeaks);
    }

    [TestMethod]
    public void TsfMetadata_AndBinaryData_Smoke()
    {
        string? path = FindTestTsf();
        if (path is null) { Assert.Inconclusive("TSF fixture not found."); return; }

        using var tsf = new TsfMetadata(path);
        Assert.IsTrue(tsf.GlobalMetadata.Count > 0);
        Assert.IsTrue(tsf.FrameCount > 0);
        Assert.IsTrue(tsf.HasLineSpectra);

        var firstMeta = tsf.EnumerateFrames().First();
        Assert.IsTrue(firstMeta.NumPeaks > 0);

        // Pulling actual line-spectrum peaks goes through TsfBinaryData. m/z and intensity
        // arrays must be the same length and m/z must be non-decreasing.
        using var bin = new TsfBinaryData(path);
        var (mz, intensity) = bin.ReadLineSpectrum(firstMeta.FrameId);
        Assert.AreEqual(mz.Length, intensity.Length);
        Assert.IsTrue(mz.Length > 0);
        for (int i = 1; i < mz.Length; i++)
            Assert.IsTrue(mz[i] >= mz[i - 1], $"m/z not sorted at {i}: {mz[i - 1]} then {mz[i]}");
    }

    [TestMethod]
    public void Tsf_SchemaDump_Diagnostic()
    {
        // Diagnostic-only: schema/peek dump for TSF fixtures, used during reader development to
        // validate Frames-table assumptions. Kept on its own MSTest entry so it can be skipped
        // (it produces a lot of TestContext output and never fails on its own).
        string? tsf = FindTestTsf();
        if (tsf is null) { Assert.Inconclusive("TSF fixture not found."); return; }
        string tsfFile = Path.Combine(tsf, "analysis.tsf");
        using var conn = new System.Data.SQLite.SQLiteConnection($"Data Source={tsfFile};Read Only=True");
        conn.Open();

        TestContext?.WriteLine("=== Frames schema ===");
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT sql FROM sqlite_master WHERE type='table' AND name='Frames'";
            TestContext?.WriteLine(cmd.ExecuteScalar()?.ToString());
        }

        TestContext?.WriteLine("=== Frames row count ===");
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM Frames";
            TestContext?.WriteLine(cmd.ExecuteScalar()?.ToString());
        }

        TestContext?.WriteLine("=== First 3 frames ===");
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT * FROM Frames LIMIT 3";
            using var reader = cmd.ExecuteReader();
            for (int i = 0; i < reader.FieldCount; i++) TestContext?.Write(reader.GetName(i) + "\t");
            TestContext?.WriteLine("");
            while (reader.Read())
            {
                for (int i = 0; i < reader.FieldCount; i++)
                    TestContext?.Write((reader.IsDBNull(i) ? "NULL" : reader.GetValue(i).ToString()) + "\t");
                TestContext?.WriteLine("");
            }
        }

        TestContext?.WriteLine("=== GlobalMetadata ===");
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT Key, Value FROM GlobalMetadata";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                TestContext?.WriteLine($"{reader.GetString(0)} = {reader.GetValue(1)}");
        }
    }

    [TestMethod]
    public void Reader_Bruker_DiaPasef_IdentifyAndPerScanRead()
    {
        // Reader_Bruker dispatch over the diaPASEF.d fixture (the per-scan / chromatogram /
        // combineIMS path that ReaderBrukerTests.Reader_Bruker_HelaPasefTdf does NOT cover).
        // We aggregate identify + per-scan read + chromatogram emission into one method since
        // they share a single Read invocation.
        string? path = FindTestD();
        if (path is null) { Assert.Inconclusive("diaPASEF.d not found."); return; }

        var reader = new Reader_Bruker();
        Assert.AreEqual(Pwiz.Data.Common.Cv.CVID.MS_Bruker_TDF_format, reader.Identify(path, head: null));

        var msd = new Pwiz.Data.MsData.MSData();
        new Reader_Bruker().Read(path, msd);

        // Per-scan mode: one spectrum per (frame, TIMS scan). diaPASEF.d has 5 frames × 927
        // scans → a bit over 4500 spectra.
        Assert.AreEqual("diaPASEF", msd.Id);
        Assert.IsNotNull(msd.Run.SpectrumList);
        Assert.IsTrue(msd.Run.SpectrumList.Count > 4000);
        var spec0 = msd.Run.SpectrumList.GetSpectrum(0);
        StringAssert.StartsWith(spec0.Id, "frame=1 scan=1", StringComparison.Ordinal);

        // Two chromatograms (TIC + BPC), with monotonic times and an MS-level integer array.
        Assert.IsNotNull(msd.Run.ChromatogramList);
        Assert.AreEqual(2, msd.Run.ChromatogramList.Count);
        Assert.AreEqual("TIC", msd.Run.ChromatogramList.ChromatogramIdentity(0).Id);
        Assert.AreEqual("BPC", msd.Run.ChromatogramList.ChromatogramIdentity(1).Id);

        var tic = msd.Run.ChromatogramList.GetChromatogram(0, getBinaryData: true);
        Assert.IsTrue(tic.DefaultArrayLength > 0);
        Assert.AreEqual(tic.DefaultArrayLength, tic.BinaryDataArrays[0].Data.Count);
        var times = tic.BinaryDataArrays[0].Data;
        for (int i = 1; i < times.Count; i++)
            Assert.IsTrue(times[i] >= times[i - 1], $"Times not sorted at index {i}");
        Assert.AreEqual(1, tic.IntegerDataArrays.Count);
        Assert.IsTrue(tic.IntegerDataArrays[0].HasCVParam(Pwiz.Data.Common.Cv.CVID.MS_non_standard_data_array));
    }

    [TestMethod]
    public void Reader_Bruker_DiaPasef_CombineIonMobility_MergesByPasefWindow()
    {
        // Distinct from the per-scan read above because Reader_Bruker.CombineIonMobilitySpectra
        // changes the spectrum-list shape entirely (different count, different ids).
        string? path = FindTestD();
        if (path is null) { Assert.Inconclusive("diaPASEF.d not found."); return; }

        var msd = new Pwiz.Data.MsData.MSData();
        new Reader_Bruker { CombineIonMobilitySpectra = true }.Read(path, msd);

        Assert.IsNotNull(msd.Run.SpectrumList);
        // diaPASEF.d has 5 frames: 1 MS1 + 4 MS2. Combined mode emits one spectrum per MS1
        // frame and one per DIA-PASEF isolation window (2 windows per MS2 frame here), so
        // 1 + 4*2 = 9 spectra. Per-scan-aware combined emission was ported from
        // TimsData.cpp:699-711.
        Assert.AreEqual(9, msd.Run.SpectrumList.Count);
        var first = msd.Run.SpectrumList.GetSpectrum(0, getBinaryData: true);
        // pwiz cpp SpectrumList_Bruker.cpp:381 native id is just "merged=N"; per-scan
        // membership is carried by scanList <scan spectrumRef="..."> entries.
        Assert.AreEqual("merged=0", first.Id);
        Assert.IsTrue(first.DefaultArrayLength > 100, "combined frame should have a lot of peaks");
    }

    [TestMethod]
    public void Reader_Bruker_TsfFixture_IdentifyAndRead()
    {
        // TSF fixture (no IMS, MALDI) — covers the TSF dispatch path through Reader_Bruker that
        // ReaderBrukerTests.Reader_Bruker_MaldiTsf hits at the harness layer. Here we just check
        // identify-by-CV and the basic Read shape (id, spectrum count, first-spectrum ids).
        string? path = FindTestTsf();
        if (path is null) { Assert.Inconclusive("TSF fixture not found."); return; }

        Assert.AreEqual(Pwiz.Data.Common.Cv.CVID.MS_Bruker_TSF_format,
            new Reader_Bruker().Identify(path, head: null));

        var msd = new Pwiz.Data.MsData.MSData();
        new Reader_Bruker().Read(path, msd);

        Assert.AreEqual("20percLaser_100fold_1_0_H6_MS", msd.Id);
        Assert.IsNotNull(msd.Run.SpectrumList);
        Assert.IsTrue(msd.Run.SpectrumList.Count > 0);

        var spec = msd.Run.SpectrumList.GetSpectrum(0, getBinaryData: true);
        StringAssert.StartsWith(spec.Id, "frame=1", StringComparison.Ordinal);
        Assert.IsTrue(spec.DefaultArrayLength > 0);
    }
}
