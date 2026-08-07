using System.IO;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.MzMlb;
using Pwiz.Data.MsData.Readers;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Data.MsData.Tests;

/// <summary>
/// Verifies that sharp-written mzMLb files are lazily-readable: MzMlbWriter must
/// emit the <c>mzML_spectrumIndex</c> + <c>mzML_spectrumIndex_idRef</c> HDF5 datasets
/// (matching cpp's Serializer_mzML mzMLb-detection path) so that on re-open,
/// MzMlbReaderAdapter takes the SpectrumList_Mzml lazy path and a one-spectrum
/// lookup doesn't need to parse the whole spectrum list.
/// </summary>
[TestClass]
public class MzMlbWriterLazyRoundTripTest
{
    [TestMethod]
    public void SharpWrittenMzMlb_ContainsSpectrumIndexDataset_AndReadsLazy()
    {
        var msd = new MSData { Id = "lazy-roundtrip" };
        msd.CVs.AddRange(MSData.DefaultCVList);
        msd.Run.Id = msd.Id;
        var sl = new SpectrumListSimple();
        for (int i = 0; i < 5; i++)
        {
            var s = new Spectrum { Index = i, Id = $"scan={i + 1}", DefaultArrayLength = 3 };
            s.Params.Set(CVID.MS_ms_level, 1);
            s.Params.Set(CVID.MS_centroid_spectrum);
            s.SetMZIntensityArrays(new[] { 100.0, 200.0, 300.0 },
                                   new[] { 10.0 + i, 20.0 + i, 30.0 + i },
                                   CVID.MS_number_of_detector_counts);
            sl.Spectra.Add(s);
        }
        msd.Run.SpectrumList = sl;

        string path = Path.Combine(Path.GetTempPath(), $"lazy-mzmlb-{System.Guid.NewGuid():N}.mzMLb");
        try
        {
            new MzMlbWriter().Write(msd, path);

            // The lazy-index HDF5 datasets must be present.
            using (var conn = MzMlbConnection.OpenForRead(path))
            {
                Assert.IsTrue(conn.Exists("mzML_spectrumIndex"),
                    "sharp-written mzMLb missing 'mzML_spectrumIndex' dataset — lazy reader will fall back to eager");
                Assert.IsTrue(conn.Exists("mzML_spectrumIndex_idRef"),
                    "sharp-written mzMLb missing 'mzML_spectrumIndex_idRef' dataset");
                Assert.AreEqual(6L, conn.GetDatasetElementCount("mzML_spectrumIndex"),
                    "expected N+1 offsets for N=5 spectra");
            }

            // Re-read via the adapter; verify the lazy SpectrumList_Mzml is installed and
            // spectra round-trip.
            using var rt = new MSData();
            new MzMlbReaderAdapter().Read(path, rt);
            Assert.IsInstanceOfType(rt.Run.SpectrumList, typeof(Pwiz.Data.MsData.Mzml.SpectrumList_Mzml),
                "lazy path didn't fire — sharp-written mzMLb should use SpectrumList_Mzml on re-open");
            Assert.AreEqual(5, rt.Run.SpectrumList.Count);

            // Spot-check one spectrum reads correctly.
            var read2 = rt.Run.SpectrumList.GetSpectrum(2, getBinaryData: true);
            Assert.AreEqual("scan=3", read2.Id);
            var mzs = new List<MZIntensityPair>();
            read2.GetMZIntensityPairs(mzs);
            Assert.AreEqual(3, mzs.Count);
            Assert.AreEqual(100.0, mzs[0].Mz, 1e-9);
            Assert.AreEqual(12.0, mzs[0].Intensity, 1e-9);
        }
        finally { try { File.Delete(path); } catch { } }
    }
}
