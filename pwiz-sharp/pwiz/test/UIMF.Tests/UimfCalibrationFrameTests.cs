using System.Data.SQLite;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Readers;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Vendor.UIMF.Tests;

/// <summary>
/// ReaderConfig.IgnoreCalibrationScans on a UIMF file that actually has a calibration frame.
/// </summary>
/// <remarks>
/// The only UIMF fixture has none, which is also the gap cpp's Reader_UIMF_Test leaves open. It is
/// a legacy-schema file, where Frame_Parameters.FrameType is the one place both the index query and
/// the SDK's GetFrameTypeForFrame read a frame's type from, so relabeling one frame in a copy of it
/// produces a file with a genuine calibration frame.
/// </remarks>
[TestClass]
public class UimfCalibrationFrameTests
{
    private const int CALIBRATION_FRAME = 8; // An MS1 frame in the fixture, with 6 scan rows

    [TestMethod]
    public void CalibrationFramesLeaveSpectraTicAndFileContent()
    {
        string? root = FindTestDataRoot();
        if (root is null) { Assert.Inconclusive("UIMF test data tree not found."); return; }
        string fixture = Path.Combine(root, "BSA_10ugml_CID.UIMF");
        if (!File.Exists(fixture)) { Assert.Inconclusive($"{fixture} not present."); return; }

        string dir = Path.Combine(Path.GetTempPath(), $"uimf-calibration-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "BSA_calibration.uimf");
        File.Copy(fixture, path);
        try
        {
            RelabelAsCalibration(path, CALIBRATION_FRAME);
            VerifyLists(path);
            VerifyFileContent(path);
        }
        finally
        {
            TryDeleteDirectory(dir);
        }
    }

    private static void VerifyLists(string path)
    {
        using var data = new UimfData(path);
        Assert.IsTrue(data.FrameTypes.Contains(UimfFrameType.Calibration), "relabeling did not take");
        int calibrationRows = data.Index.Count(e => e.FrameType == UimfFrameType.Calibration);
        Assert.AreEqual(6, calibrationRows);

        // Without the flag the calibration frame is presented, and labeled as such
        var kept = new SpectrumList_UIMF(data, null, ignoreZeroIntensityPoints: true);
        Assert.AreEqual(data.Index.Count, kept.Count);
        Assert.IsFalse(kept.CalibrationSpectraAreOmitted);
        int firstCalibration = Enumerable.Range(0, kept.Count)
            .First(i => kept.SpectrumIdentity(i).Id.StartsWith($"frame={CALIBRATION_FRAME} ", StringComparison.Ordinal));
        Assert.IsTrue(kept.GetSpectrum(firstCalibration).HasCVParam(CVID.MS_calibration_spectrum));

        // With it, the frame is gone and what remains is numbered without a gap
        var omitted = new SpectrumList_UIMF(data, null, ignoreZeroIntensityPoints: true, ignoreCalibrationScans: true);
        Assert.AreEqual(data.Index.Count - calibrationRows, omitted.Count);
        Assert.IsTrue(omitted.CalibrationSpectraAreOmitted);
        for (int i = 0; i < omitted.Count; i++)
        {
            var identity = omitted.SpectrumIdentity(i);
            Assert.AreEqual(i, identity.Index);
            Assert.IsFalse(identity.Id.StartsWith($"frame={CALIBRATION_FRAME} ", StringComparison.Ordinal), identity.Id);
        }
        // The spectrum that took the calibration frame's first slot is the one after it, read correctly
        var successor = omitted.GetSpectrum(firstCalibration, true);
        Assert.AreEqual(kept.SpectrumIdentity(firstCalibration + calibrationRows).Id, successor.Id);
        Assert.AreEqual(firstCalibration, successor.Index);
        Assert.IsFalse(successor.HasCVParam(CVID.MS_calibration_spectrum));

        // The TIC loses the calibration frame's point too, with or without its arrays
        var ticKept = new ChromatogramList_UIMF(data).GetChromatogram(0, true);
        var ticOmitted = new ChromatogramList_UIMF(data, ignoreCalibrationScans: true);
        Assert.AreEqual(data.FrameCount, ticKept.DefaultArrayLength);
        Assert.AreEqual(data.FrameCount - 1, ticOmitted.GetChromatogram(0, true).DefaultArrayLength);
        Assert.AreEqual(data.FrameCount - 1, ticOmitted.GetChromatogram(0, false).DefaultArrayLength);
    }

    /// <summary>
    /// fileContent must declare calibration spectra only when the list will present them.
    /// </summary>
    private static void VerifyFileContent(string path)
    {
        foreach (bool ignore in new[] { false, true })
        {
            var msd = new MSData();
            new Reader_UIMF().Read(path, msd, new ReaderConfig { IgnoreCalibrationScans = ignore });
            Assert.AreEqual(!ignore, msd.FileDescription.FileContent.HasCVParam(CVID.MS_calibration_spectrum),
                $"IgnoreCalibrationScans={ignore}");
            Assert.AreEqual(ignore, msd.Run.SpectrumList!.CalibrationSpectraAreOmitted,
                $"IgnoreCalibrationScans={ignore}");
        }
    }

    private static void RelabelAsCalibration(string path, int frame)
    {
        var csb = new SQLiteConnectionStringBuilder { DataSource = path, Pooling = false };
        using var conn = new SQLiteConnection(csb.ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Frame_Parameters SET FrameType = @type WHERE FrameNum = @frame";
        cmd.Parameters.AddWithValue("@type", (int)UimfFrameType.Calibration);
        cmd.Parameters.AddWithValue("@frame", frame);
        Assert.AreEqual(1, cmd.ExecuteNonQuery());
    }

    /// <summary>
    /// Reader_UIMF hands the UimfData it opens to lists that do not dispose it, so the file can
    /// still be held when the test ends. Leaving a temp copy behind is not worth failing over.
    /// </summary>
    private static void TryDeleteDirectory(string dir)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        try
        {
            Directory.Delete(dir, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string? FindTestDataRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string candidate = Path.Combine(dir, "pwiz", "data", "vendor_readers", "UIMF",
                "Reader_UIMF_Test.data");
            if (Directory.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }
}
