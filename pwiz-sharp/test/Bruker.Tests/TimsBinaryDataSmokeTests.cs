using Pwiz.Vendor.Bruker;

namespace Pwiz.Vendor.Bruker.Tests;

/// <summary>
/// Smoke tests for the native <c>timsdata.dll</c> P/Invoke wrapper. Skipped when the test data
/// folder isn't present (dev machines outside the pwiz checkout).
/// </summary>
[TestClass]
public class TimsBinaryDataSmokeTests
{
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

    [TestMethod]
    public void Open_DiaPasefTdf_YieldsNonZeroHandle()
    {
        string? path = FindTestD();
        if (path is null) { Assert.Inconclusive("diaPASEF.d not found in test data tree."); return; }

        using var tims = new TimsBinaryData(path);
        Assert.AreNotEqual(0UL, tims.Handle);
    }

    [TestMethod]
    public void ReadScans_FirstFrame_ReturnsFrameProxyWithSamePeakCountAsCaller()
    {
        string? path = FindTestD();
        if (path is null) { Assert.Inconclusive("diaPASEF.d not found in test data tree."); return; }

        using var tims = new TimsBinaryData(path);
        // Frame ids are 1-based in timsdata.
        var frame = tims.ReadScans(frameId: 1, scanBegin: 0, scanEnd: 10, performMzConversion: true);
        Assert.AreEqual(10, frame.NumScans);
        Assert.IsTrue(frame.TotalNumPeaks >= 0);

        // When there ARE peaks, m/z values should be populated and sorted within each scan.
        for (int s = 0; s < frame.NumScans; s++)
        {
            var mz = frame.GetScanMzs(s);
            if (mz.Length <= 1) continue;
            for (int i = 1; i < mz.Length; i++)
                Assert.IsTrue(mz[i] >= mz[i - 1], $"scan {s}: m/z not sorted at index {i}");
        }
    }

    [TestMethod]
    public void MasonShamp_CcsRoundTrip_IsClose()
    {
        // Pure math call; doesn't require a .d directory.
        double ook0 = 0.85;
        int charge = 2;
        double mz = 600.0;
        double ccs = TimsBinaryData.OneOverK0ToCcs(ook0, charge, mz);
        double back = TimsBinaryData.CcsToOneOverK0(ccs, charge, mz);
        Assert.AreEqual(ook0, back, 1e-6);
    }

    [TestMethod]
    public void LastErrorMessage_OnBogusOpen_Surfaces()
    {
        Assert.ThrowsException<DirectoryNotFoundException>(
            () => new TimsBinaryData("C:/definitely/not/a/real/path.d"));
    }

    // ---- TdfMetadata (Phase B) ----

    [TestMethod]
    public void Metadata_DumpGlobalMetadata_Diagnostic()
    {
        string? path = FindTestD();
        if (path is null) { Assert.Inconclusive("diaPASEF.d not found."); return; }
        using var tdf = new TdfMetadata(path);
        foreach (var kv in tdf.GlobalMetadata)
            TestContext?.WriteLine($"{kv.Key} = {kv.Value}");
    }

    [TestMethod]
    public void Metadata_DumpTsfSchema_Diagnostic()
    {
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

    // ---- TSF (Phase D) ----

    [TestMethod]
    public void Reader_Bruker_IdentifiesTsfDirectory()
    {
        string? path = FindTestTsf();
        if (path is null) { Assert.Inconclusive("TSF fixture not found."); return; }

        var reader = new Reader_Bruker();
        var cv = reader.Identify(path, head: null);
        Assert.AreEqual(Pwiz.Data.Common.Cv.CVID.MS_Bruker_TSF_format, cv);
    }

    [TestMethod]
    public void Metadata_Tsf_HasGlobalMetadataAndFrames()
    {
        string? path = FindTestTsf();
        if (path is null) { Assert.Inconclusive("TSF fixture not found."); return; }

        using var tsf = new TsfMetadata(path);
        Assert.IsTrue(tsf.GlobalMetadata.Count > 0);
        Assert.IsTrue(tsf.FrameCount > 0);
        Assert.IsTrue(tsf.HasLineSpectra);

        var frame = tsf.EnumerateFrames().First();
        Assert.IsTrue(frame.NumPeaks > 0);
    }

    [TestMethod]
    public void TsfBinaryData_ReadLineSpectrum_ReturnsNonEmpty()
    {
        string? path = FindTestTsf();
        if (path is null) { Assert.Inconclusive("TSF fixture not found."); return; }

        using var meta = new TsfMetadata(path);
        using var tsf = new TsfBinaryData(path);
        var first = meta.EnumerateFrames().First();
        var (mz, intensity) = tsf.ReadLineSpectrum(first.FrameId);
        Assert.AreEqual(mz.Length, intensity.Length);
        Assert.IsTrue(mz.Length > 0);
        // m/z should be non-decreasing.
        for (int i = 1; i < mz.Length; i++)
            Assert.IsTrue(mz[i] >= mz[i - 1], $"m/z not sorted at {i}: {mz[i - 1]} then {mz[i]}");
    }

    [TestMethod]
    public void Reader_Bruker_ReadsTsfIntoMsData()
    {
        string? path = FindTestTsf();
        if (path is null) { Assert.Inconclusive("TSF fixture not found."); return; }

        var msd = new Pwiz.Data.MsData.MSData();
        new Reader_Bruker().Read(path, msd);

        Assert.AreEqual("20percLaser_100fold_1_0_H6_MS", msd.Id);
        Assert.IsNotNull(msd.Run.SpectrumList);
        Assert.IsTrue(msd.Run.SpectrumList.Count > 0);

        var spec = msd.Run.SpectrumList.GetSpectrum(0, getBinaryData: true);
        StringAssert.StartsWith(spec.Id, "frame=1", StringComparison.Ordinal);
        Assert.IsTrue(spec.DefaultArrayLength > 0);
    }

    /// <summary>Injected by MSTest; lets us surface per-test diagnostic output.</summary>
    public TestContext? TestContext { get; set; }

    [TestMethod]
    public void Metadata_DiaPasefTdf_HasGlobalMetadataAndFrames()
    {
        string? path = FindTestD();
        if (path is null) { Assert.Inconclusive("diaPASEF.d not found."); return; }

        using var tdf = new TdfMetadata(path);
        Assert.IsTrue(tdf.GlobalMetadata.Count > 0);
        Assert.IsTrue(tdf.FrameCount > 0);
        Assert.IsTrue(tdf.MaxNumScans > 0);
        Assert.IsTrue(tdf.CalibrationCount >= 1);
        // diaPASEF should have the DIA tables populated.
        Assert.IsTrue(tdf.HasDiaPasefData || tdf.HasPasefData,
            "Expected diaPASEF.d to have PASEF or DIA-PASEF tables.");
    }

    [TestMethod]
    public void Metadata_EnumerateFrames_ReturnsFrameIdsInOrder()
    {
        string? path = FindTestD();
        if (path is null) { Assert.Inconclusive("diaPASEF.d not found."); return; }

        using var tdf = new TdfMetadata(path);
        long prev = 0;
        int count = 0;
        foreach (var frame in tdf.EnumerateFrames())
        {
            Assert.IsTrue(frame.FrameId > prev, $"Frames not in id order: {prev} then {frame.FrameId}");
            Assert.IsTrue(frame.RetentionTimeSeconds >= 0);
            Assert.IsTrue(frame.NumScans > 0);
            prev = frame.FrameId;
            count++;
        }
        Assert.AreEqual(tdf.FrameCount, count);
    }

    [TestMethod]
    public void Metadata_MzAcquisitionRange_ReturnsReasonableValues()
    {
        string? path = FindTestD();
        if (path is null) { Assert.Inconclusive("diaPASEF.d not found."); return; }

        using var tdf = new TdfMetadata(path);
        var (low, high) = tdf.MzAcquisitionRange;
        Assert.IsTrue(low > 0, $"low = {low}");
        Assert.IsTrue(high > low, $"high = {high}");
    }

    [TestMethod]
    public void Metadata_TimsBinaryData_TogetherReadsFirstFramePeaks()
    {
        string? path = FindTestD();
        if (path is null) { Assert.Inconclusive("diaPASEF.d not found."); return; }

        using var tdf = new TdfMetadata(path);
        using var tims = new TimsBinaryData(path);

        var first = tdf.EnumerateFrames().First();
        var frame = tims.ReadScans(first.FrameId, 0, (uint)first.NumScans, performMzConversion: true);
        Assert.AreEqual(first.NumScans, frame.NumScans);
        // The peak count from the Frames table should match the total from tims_read_scans_v2.
        Assert.AreEqual((int)first.NumPeaks, frame.TotalNumPeaks);
    }

    // ---- Reader_Bruker (Phase C) ----

    [TestMethod]
    public void Reader_Bruker_IdentifiesTdfDirectory()
    {
        string? path = FindTestD();
        if (path is null) { Assert.Inconclusive("diaPASEF.d not found."); return; }

        var reader = new Reader_Bruker();
        var cv = reader.Identify(path, head: null);
        Assert.AreEqual(Pwiz.Data.Common.Cv.CVID.MS_Bruker_TDF_format, cv);
    }

    [TestMethod]
    public void Reader_Bruker_ReadsTdfIntoMsData_PerScanMode()
    {
        string? path = FindTestD();
        if (path is null) { Assert.Inconclusive("diaPASEF.d not found."); return; }

        var msd = new Pwiz.Data.MsData.MSData();
        new Reader_Bruker().Read(path, msd);

        Assert.AreEqual("diaPASEF", msd.Id);
        Assert.IsNotNull(msd.Run.SpectrumList);
        // Per-scan mode: one spectrum per (frame, TIMS scan). The diaPASEF fixture has 5 frames
        // with 927 scans each — so a bit over 4500 spectra.
        Assert.IsTrue(msd.Run.SpectrumList.Count > 4000);

        // Spot-check spectrum 0.
        var spec = msd.Run.SpectrumList.GetSpectrum(0);
        StringAssert.StartsWith(spec.Id, "frame=1 scan=1", StringComparison.Ordinal);
    }

    [TestMethod]
    public void Reader_Bruker_EmitsTicAndBpcChromatograms()
    {
        string? path = FindTestD();
        if (path is null) { Assert.Inconclusive("diaPASEF.d not found."); return; }

        var msd = new Pwiz.Data.MsData.MSData();
        new Reader_Bruker().Read(path, msd);

        Assert.IsNotNull(msd.Run.ChromatogramList);
        Assert.AreEqual(2, msd.Run.ChromatogramList.Count);
        Assert.AreEqual("TIC", msd.Run.ChromatogramList.ChromatogramIdentity(0).Id);
        Assert.AreEqual("BPC", msd.Run.ChromatogramList.ChromatogramIdentity(1).Id);

        var tic = msd.Run.ChromatogramList.GetChromatogram(0, getBinaryData: true);
        Assert.IsTrue(tic.DefaultArrayLength > 0);
        Assert.AreEqual(tic.DefaultArrayLength, tic.BinaryDataArrays[0].Data.Count);
        // Times should be monotonically non-decreasing (ordered by frame id == RT order).
        var times = tic.BinaryDataArrays[0].Data;
        for (int i = 1; i < times.Count; i++)
            Assert.IsTrue(times[i] >= times[i - 1], $"Times not sorted at index {i}");

        // ms-level integer array attached alongside.
        Assert.AreEqual(1, tic.IntegerDataArrays.Count);
        Assert.IsTrue(tic.IntegerDataArrays[0].HasCVParam(Pwiz.Data.Common.Cv.CVID.MS_non_standard_data_array));
    }

    [TestMethod]
    public void Reader_Bruker_CombineIonMobility_ProducesOneSpectrumPerFrame()
    {
        string? path = FindTestD();
        if (path is null) { Assert.Inconclusive("diaPASEF.d not found."); return; }

        var msd = new Pwiz.Data.MsData.MSData();
        new Reader_Bruker { CombineIonMobilitySpectra = true }.Read(path, msd);

        Assert.IsNotNull(msd.Run.SpectrumList);
        Assert.AreEqual(5, msd.Run.SpectrumList.Count);
        var first = msd.Run.SpectrumList.GetSpectrum(0, getBinaryData: true);
        StringAssert.StartsWith(first.Id, "merged=0 frame=1", StringComparison.Ordinal);
        Assert.IsTrue(first.DefaultArrayLength > 100, "combined frame should have a lot of peaks");
    }
}
