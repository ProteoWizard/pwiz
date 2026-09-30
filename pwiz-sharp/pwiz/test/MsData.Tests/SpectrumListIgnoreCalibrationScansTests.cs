using System.IO;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Mzml;
using Pwiz.Data.MsData.Readers;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Data.MsData.Tests;

/// <summary>
/// Port of cpp's SpectrumList_IgnoreCalibrationScansTest, plus the reader wiring that cpp covers
/// through DefaultReaderList.
/// </summary>
[TestClass]
public class SpectrumListIgnoreCalibrationScansTests
{
    [TestMethod]
    public void RemovesCalibrationSpectra()
    {
        var msd = BuildMSData(declareInFileContent: true);
        var inner = msd.Run.SpectrumList!;
        var sl = SpectrumList_IgnoreCalibrationScans.Create(inner, msd)!;

        Assert.AreNotSame(inner, sl); // It wrapped
        Assert.AreEqual(3, sl.Count);
        Assert.IsTrue(sl.CalibrationSpectraAreOmitted);

        // The survivors keep their ids, and are renumbered so nothing sees a gap
        var expected = new[] { "scan=1", "scan=3", "scan=4" };
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.AreEqual(expected[i], sl.SpectrumIdentity(i).Id);
            Assert.AreEqual(i, sl.SpectrumIdentity(i).Index);
            Assert.AreEqual(expected[i], sl.GetSpectrum(i).Id);
            Assert.AreEqual(i, sl.GetSpectrum(i).Index);
        }

        // The mapping has to reach the right inner spectrum, not merely one with the right id
        Assert.AreEqual(0, sl.GetSpectrum(0).DefaultArrayLength);
        Assert.AreEqual(2, sl.GetSpectrum(1).DefaultArrayLength);
        Assert.AreEqual(3, sl.GetSpectrum(2).DefaultArrayLength);

        // Find has to follow the renumbering too
        Assert.AreEqual(1, sl.Find("scan=3"));
        Assert.AreEqual(3, sl.Find("scan=2")); // Gone, so "not found" is Count
        Assert.AreEqual(3, sl.Find("scan=99"));

        // Fetching through the wrapper must not renumber the inner list's own spectra.
        // SpectrumListSimple hands back the element itself, so assigning Index in place would
        // corrupt it - and every assertion above would still pass, since none looks at inner.
        Assert.AreEqual(2, inner.GetSpectrum(2).Index);
        Assert.AreEqual(2, inner.SpectrumIdentity(2).Index);
        Assert.AreEqual("scan=3", inner.SpectrumIdentity(2).Id);
    }

    /// <summary>
    /// A file may declare calibration spectra in fileContent and carry the term on no spectrum at
    /// all - msconvert with a spectrum-dropping filter writes exactly that (ProteoWizard #4499).
    /// Wrapping one would leave a list reporting, through CalibrationSpectraAreOmitted, that it
    /// removed calibration spectra while every spectrum is still present. Skyline stands down from
    /// its own lockspray heuristic when a list says so, so it would stop filtering scans nothing had
    /// removed.
    /// </summary>
    [TestMethod]
    public void LeavesDeclaredButUntaggedFilesAlone()
    {
        var msd = BuildMSData(declareInFileContent: true, tagSpectrum: false);
        var inner = msd.Run.SpectrumList!;
        var sl = SpectrumList_IgnoreCalibrationScans.Create(inner, msd)!;

        Assert.AreSame(inner, sl);
        Assert.AreEqual(4, sl.Count);
        Assert.IsFalse(sl.CalibrationSpectraAreOmitted);
    }

    /// <summary>
    /// Scanning every spectrum to find out there was nothing to remove is the cost the fileContent
    /// gate avoids, so a file that does not declare calibration spectra must come back untouched -
    /// and unread - rather than merely equal.
    /// </summary>
    [TestMethod]
    public void LeavesUndeclaredFilesAlone()
    {
        var msd = BuildMSData(declareInFileContent: false);
        var inner = new DetailLevelRecordingSpectrumList(((SpectrumListSimple)msd.Run.SpectrumList!).Spectra);
        var sl = SpectrumList_IgnoreCalibrationScans.Create(inner, msd)!;

        Assert.AreSame(inner, sl);
        Assert.AreEqual(4, sl.Count);
        Assert.IsFalse(sl.CalibrationSpectraAreOmitted);
        Assert.AreEqual(0, inner.Requested.Count);
    }

    /// <summary>
    /// The scan must not decode anyone's binary arrays to answer a metadata question. cpp pins the
    /// cheapest level through min_level_accepted; the lists this is installed on here serve every
    /// level below FullData as the same metadata-only read, so what matters is never reaching it.
    /// </summary>
    [TestMethod]
    public void ScanNeverAsksForBinaryData()
    {
        var msd = BuildMSData(declareInFileContent: true);
        var inner = new DetailLevelRecordingSpectrumList(((SpectrumListSimple)msd.Run.SpectrumList!).Spectra);
        var sl = SpectrumList_IgnoreCalibrationScans.Create(inner, msd)!;

        // It still works: the tagged spectrum is gone
        Assert.AreEqual(3, sl.Count);
        Assert.IsTrue(sl.CalibrationSpectraAreOmitted);

        Assert.AreEqual(4, inner.Requested.Count);
        foreach (var detailLevel in inner.Requested)
            Assert.IsTrue(detailLevel < DetailLevel.FullData, detailLevel.ToString());
    }

    /// <summary>
    /// Through the mzML reader: the spectrum is hidden only when the config asks for it, and the
    /// fileContent declaration goes with it so that a file written from the result does not
    /// advertise calibration spectra it no longer contains.
    /// </summary>
    [TestMethod]
    public void MzmlReaderHonorsIgnoreCalibrationScans()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ignore-calibration-{Guid.NewGuid():N}.mzML");
        try
        {
            File.WriteAllText(path, new MzmlWriter().Write(BuildMSData(declareInFileContent: true)));

            var kept = ReadMzml(path, ignoreCalibrationScans: false);
            Assert.AreEqual(4, kept.Run.SpectrumList!.Count);
            Assert.IsFalse(kept.Run.SpectrumList.CalibrationSpectraAreOmitted);
            Assert.IsTrue(kept.FileDescription.FileContent.HasCVParam(CVID.MS_calibration_spectrum));
            kept.Run.SpectrumList.Dispose();

            var omitted = ReadMzml(path, ignoreCalibrationScans: true);
            var sl = omitted.Run.SpectrumList!;
            Assert.AreEqual(3, sl.Count);
            Assert.IsTrue(sl.CalibrationSpectraAreOmitted);
            Assert.AreEqual("scan=3", sl.GetSpectrum(1, true).Id);
            Assert.IsFalse(omitted.FileDescription.FileContent.HasCVParam(CVID.MS_calibration_spectrum));
            Assert.IsTrue(omitted.FileDescription.FileContent.HasCVParam(CVID.MS_MS1_spectrum));

            // And the declaration stays gone on the way back out
            StringAssert.DoesNotMatch(new MzmlWriter().Write(omitted),
                new System.Text.RegularExpressions.Regex("MS:1000928"));
            sl.Dispose();
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static MSData ReadMzml(string path, bool ignoreCalibrationScans)
    {
        var msd = new MSData();
        new MzmlReaderAdapter().Read(path, msd, new ReaderConfig { IgnoreCalibrationScans = ignoreCalibrationScans });
        return msd;
    }

    /// <summary>
    /// A lockspray scan sits second, so a wrapper that forgot to remap would still line up at
    /// index 0 and only go wrong later.
    /// </summary>
    private static MSData BuildMSData(bool declareInFileContent, bool tagSpectrum = true)
    {
        var msd = new MSData { Id = "calibration" };
        msd.CVs.AddRange(MSData.DefaultCVList);
        var list = new SpectrumListSimple();
        for (int i = 0; i < 4; i++)
        {
            var spectrum = new Spectrum
            {
                Index = i,
                Id = "scan=" + (i + 1),
                DefaultArrayLength = i, // Something per-spectrum to prove the right one is fetched
            };
            spectrum.Params.Set(CVID.MS_ms_level, 1);
            spectrum.Params.Set(i == 1 && tagSpectrum ? CVID.MS_calibration_spectrum : CVID.MS_MS1_spectrum);
            list.Spectra.Add(spectrum);
        }
        msd.Run.Id = "run";
        msd.Run.SpectrumList = list;

        msd.FileDescription.FileContent.Set(CVID.MS_MS1_spectrum);
        if (declareInFileContent)
            msd.FileDescription.FileContent.Set(CVID.MS_calibration_spectrum);
        return msd;
    }

    /// <summary>
    /// Records every detail level it is asked for. SpectrumListSimple ignores detail level
    /// altogether, so without a list like this the tests above pass whatever level the scan uses.
    /// </summary>
    private sealed class DetailLevelRecordingSpectrumList : SpectrumListBase
    {
        private readonly List<Spectrum> _spectra;

        public DetailLevelRecordingSpectrumList(List<Spectrum> spectra)
        {
            _spectra = spectra;
        }

        public List<DetailLevel> Requested { get; } = new();

        public override int Count => _spectra.Count;

        public override SpectrumIdentity SpectrumIdentity(int index) => _spectra[index];

        public override Spectrum GetSpectrum(int index, bool getBinaryData = false) =>
            GetSpectrum(index, getBinaryData ? DetailLevel.FullData : DetailLevel.FullMetadata);

        public override Spectrum GetSpectrum(int index, DetailLevel detailLevel)
        {
            Requested.Add(detailLevel);
            return _spectra[index];
        }
    }
}
