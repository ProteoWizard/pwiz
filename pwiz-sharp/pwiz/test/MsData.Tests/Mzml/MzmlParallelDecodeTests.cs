using System.IO;
using Pwiz.Data.MsData.Mzml;
using Pwiz.Data.MsData.Readers;

namespace Pwiz.Data.MsData.Tests.Mzml;

/// <summary>
/// Pins the one property <see cref="SpectrumList_Mzml.DecodeThreads"/> rests on: decoding
/// binary arrays on a thread pool must produce exactly the values the sequential path
/// produces, in the same order.
///
/// That holds because decoding is a pure function of (base64 payload, encoder config) -
/// <c>BinaryDataEncoder</c> carries nothing but a readonly config and its helpers are
/// static - but "it should be pure" is an argument, and this is the check. Exact equality,
/// not a tolerance: the same bytes through the same decoder must give the same doubles, and
/// any tolerance here would hide the reordering bug this test exists to catch.
/// </summary>
[TestClass]
public class MzmlParallelDecodeTests
{
    private static string? FindCppTestDataDir()
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string candidate = Path.Combine(dir, "pwiz", "analysis", "spectrum_processing",
                "SpectrumList_DiaUmpireTest.data");
            if (Directory.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    [TestMethod]
    public void ParallelDecode_ProducesIdenticalSpectraToSequential()
    {
        string? dataDir = FindCppTestDataDir();
        if (dataDir is null) { Assert.Inconclusive("cpp test data dir not found"); return; }
        string path = Path.Combine(dataDir,
            "Hoofnagle_10xDil_SWATH_01-20130327_Hoofnagle_10xDil_SWATH_1_01-diaumpire.mzML");
        if (!File.Exists(path)) { Assert.Inconclusive($"fixture missing: {path}"); return; }

        var sequential = ReadAll(path, threads: 1);
        var parallel = ReadAll(path, threads: 8);

        Assert.IsTrue(sequential.Count > 1, "fixture produced too few spectra to be a real check");
        Assert.AreEqual(sequential.Count, parallel.Count, "spectrum count differs");

        for (int i = 0; i < sequential.Count; i++)
        {
            var (seqId, seqMz, seqIntensity) = sequential[i];
            var (parId, parMz, parIntensity) = parallel[i];

            // Identity first: a batch served out of order would show up here rather than as
            // a confusing array mismatch.
            Assert.AreEqual(seqId, parId, $"spectrum {i} id differs");
            Assert.AreEqual(seqMz.Length, parMz.Length, $"spectrum {i} m/z length differs");
            Assert.AreEqual(seqIntensity.Length, parIntensity.Length,
                $"spectrum {i} intensity length differs");

            for (int j = 0; j < seqMz.Length; j++)
                Assert.AreEqual(seqMz[j], parMz[j], 0.0, $"spectrum {i} m/z[{j}] differs");
            for (int j = 0; j < seqIntensity.Length; j++)
                Assert.AreEqual(seqIntensity[j], parIntensity[j], 0.0,
                    $"spectrum {i} intensity[{j}] differs");
        }
    }

    /// <summary>
    /// Reads every spectrum with binary data at the given thread setting. Restores the
    /// previous setting so the test cannot leak a global into whatever runs next.
    /// </summary>
    private static List<(string Id, double[] Mz, double[] Intensity)> ReadAll(string path, int threads)
    {
        int previous = SpectrumList_Mzml.DecodeThreads;
        SpectrumList_Mzml.DecodeThreads = threads;
        try
        {
            var result = new List<(string, double[], double[])>();
            using var msd = new MSData();
            ReaderList.Default.Read(path, msd, 0, new ReaderConfig());
            var list = msd.Run.SpectrumList;
            Assert.IsNotNull(list, "fixture has no spectrum list");

            for (int i = 0; i < list.Count; i++)
            {
                var spectrum = list.GetSpectrum(i, getBinaryData: true);
                result.Add((
                    spectrum.Id,
                    spectrum.GetMZArray()?.Data.ToArray() ?? Array.Empty<double>(),
                    spectrum.GetIntensityArray()?.Data.ToArray() ?? Array.Empty<double>()));
            }
            return result;
        }
        finally
        {
            SpectrumList_Mzml.DecodeThreads = previous;
        }
    }
}
