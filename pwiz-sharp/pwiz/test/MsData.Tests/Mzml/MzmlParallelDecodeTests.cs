using System.IO;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Mzml;
using Pwiz.Data.MsData.Readers;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Data.MsData.Tests.Mzml;

/// <summary>
/// Pins the one property <see cref="ReaderConfig.MzmlDecodeThreads"/> rests on: decoding
/// binary arrays on a thread pool must produce exactly the values the sequential path
/// produces, in the same order.
///
/// That holds because decoding is a pure function of (base64 payload, encoder config) -
/// <c>BinaryDataEncoder</c> carries nothing but a readonly config and its helpers are
/// static - but "it should be pure" is an argument, and this is the check. Exact equality,
/// not a tolerance: any tolerance here would hide the reordering bug the test exists to
/// catch.
///
/// The peak fixture is <c>tiny.pwiz.1.1.mzML</c>, which the test project already copies to
/// the output directory, so this cannot silently skip on a machine lacking the cpp test
/// tree, and it is indexed so the lazy list is genuinely used. It carries no INTEGER binary
/// array, so the integer half of the deferral code gets a written fixture of its own below.
/// </summary>
[TestClass]
public class MzmlParallelDecodeTests
{
    private static string FixturePath()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "example_data", "tiny.pwiz.1.1.mzML");
        Assert.IsTrue(File.Exists(path), $"fixture missing: {path}");
        return path;
    }

    [TestMethod]
    public void ParallelDecode_ProducesIdenticalSpectraToSequential()
    {
        string path = FixturePath();

        var sequential = ReadAll(path, threads: 1);
        var parallel = ReadAll(path, threads: 8);

        Assert.AreEqual(sequential.Count, parallel.Count, "spectrum count differs");
        Assert.IsTrue(sequential.Count > 1, "fixture produced too few spectra to be a real check");

        // Without this the comparison below can pass while comparing nothing: if every array
        // came back empty, each length assert would be AreEqual(0, 0) and each loop body
        // would execute zero times. Assert up front that there is real data to compare.
        int totalValues = 0;
        foreach (var s in sequential)
            totalValues += s.Mz.Length + s.Intensity.Length + s.Integers.Length;
        Assert.IsTrue(totalValues > 0, "fixture decoded no binary data, so this test proves nothing");

        for (int i = 0; i < sequential.Count; i++)
        {
            var seq = sequential[i];
            var par = parallel[i];

            // Identity first: a batch served out of order shows up here rather than as a
            // confusing array mismatch.
            Assert.AreEqual(seq.Id, par.Id, $"spectrum {i} id differs");
            AssertArraysEqual(seq.Mz, par.Mz, i, "m/z");
            AssertArraysEqual(seq.Intensity, par.Intensity, i, "intensity");

            Assert.AreEqual(seq.Integers.Length, par.Integers.Length, $"spectrum {i} integer length");
            for (int j = 0; j < seq.Integers.Length; j++)
                Assert.AreEqual(seq.Integers[j], par.Integers[j], $"spectrum {i} integer[{j}] differs");
        }
    }

    /// <summary>
    /// Covers the half of the deferral code the peak arrays never reach:
    /// <c>PendingDecode.IntegerArray</c>. No shipped mzML fixture carries an integer binary
    /// array - the assertion that first went looking for one failed - so this writes an
    /// indexed mzML that does, then reads it back both ways.
    /// </summary>
    [TestMethod]
    public void ParallelDecode_IntegerArraysMatchSequential()
    {
        var msd = new MSData();
        var simple = new SpectrumListSimple();
        for (int i = 0; i < 8; i++)
        {
            var spectrum = new Spectrum { Index = i, Id = $"scan={i + 1}" };
            spectrum.Params.Set(CVID.MS_ms_level, 1);
            var mz = new BinaryDataArray();
            mz.Params.Set(CVID.MS_m_z_array);
            var intensity = new BinaryDataArray();
            intensity.Params.Set(CVID.MS_intensity_array);
            var counts = new IntegerDataArray();
            counts.Params.Set(CVID.MS_non_standard_data_array, "drift bin");
            for (int j = 0; j < 32; j++)
            {
                mz.Data.Add(100.0 + i + (j * 0.25));
                intensity.Data.Add(1000.0 + j);
                counts.Data.Add((i * 100L) + j);
            }
            spectrum.BinaryDataArrays.Add(mz);
            spectrum.BinaryDataArrays.Add(intensity);
            spectrum.IntegerDataArrays.Add(counts);
            simple.Spectra.Add(spectrum);
        }
        msd.Run.SpectrumList = simple;

        string path = Path.Combine(Path.GetTempPath(),
            $"pwiz-parallel-decode-{Guid.NewGuid():N}.mzML");
        try
        {
            File.WriteAllText(path, new MzmlWriter().Write(msd));

            var sequential = ReadAll(path, threads: 1);
            var parallel = ReadAll(path, threads: 8);

            int integerValues = 0;
            foreach (var spectrum in sequential)
                integerValues += spectrum.Integers.Length;
            Assert.IsTrue(integerValues > 0, "written fixture carried no integer array");

            Assert.AreEqual(sequential.Count, parallel.Count);
            for (int i = 0; i < sequential.Count; i++)
            {
                Assert.AreEqual(sequential[i].Integers.Length, parallel[i].Integers.Length,
                    $"spectrum {i} integer length differs");
                for (int j = 0; j < sequential[i].Integers.Length; j++)
                    Assert.AreEqual(sequential[i].Integers[j], parallel[i].Integers[j],
                        $"spectrum {i} integer[{j}] differs");
            }
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    /// <summary>
    /// Read-ahead must not change what a caller reading BACKWARD sees. That path skips the
    /// batch entirely, so this pins that the fallback still returns correct spectra rather
    /// than whatever a stale batch happens to hold.
    /// </summary>
    [TestMethod]
    public void ParallelDecode_ReverseOrderMatchesSequential()
    {
        string path = FixturePath();
        var expected = ReadAll(path, threads: 1);

        {
            using var msd = new MSData();
            ReaderList.Default.Read(path, msd, 0,
                new ReaderConfig { MzmlDecodeThreads = 8 });
            var list = msd.Run.SpectrumList;
            Assert.IsNotNull(list);

            for (int i = expected.Count - 1; i >= 0; i--)
            {
                var spectrum = list.GetSpectrum(i, getBinaryData: true);
                Assert.AreEqual(expected[i].Id, spectrum.Id, $"spectrum {i} id differs in reverse order");
                AssertArraysEqual(expected[i].Mz, ToArray(spectrum.GetMZArray()), i, "m/z (reverse)");
            }
        }
    }

    private static void AssertArraysEqual(double[] expected, double[] actual, int index, string what)
    {
        Assert.AreEqual(expected.Length, actual.Length, $"spectrum {index} {what} length differs");
        for (int j = 0; j < expected.Length; j++)
            Assert.AreEqual(expected[j], actual[j], 0.0, $"spectrum {index} {what}[{j}] differs");
    }

    private static double[] ToArray(BinaryDataArray? array) =>
        array is null ? Array.Empty<double>() : array.Data.ToArray();

    /// <summary>
    /// Reads every spectrum with binary data at the given thread setting. The setting rides
    /// on the ReaderConfig for this read alone, so nothing leaks into another test.
    /// </summary>
    private static List<(string Id, double[] Mz, double[] Intensity, long[] Integers)> ReadAll(
        string path, int threads)
    {
        {
            var result = new List<(string, double[], double[], long[])>();
            using var msd = new MSData();
            ReaderList.Default.Read(path, msd, 0,
                new ReaderConfig { MzmlDecodeThreads = threads });
            var list = msd.Run.SpectrumList;
            Assert.IsNotNull(list, "fixture has no spectrum list");

            // The batching lives in SpectrumList_Mzml; an eager SpectrumListSimple fallback
            // would make every assertion below pass without the new code running at all.
            Assert.IsInstanceOfType<SpectrumList_Mzml>(list,
                "expected the lazy indexed reader - the parallel path would not be exercised otherwise");

            // Prove the ReaderConfig value actually arrived. Without this the comparison
            // below passes whether or not the setting is plumbed through, because an
            // unwired setting leaves BOTH runs sequential and therefore identical.
            Assert.AreEqual(Math.Min(threads, Environment.ProcessorCount),
                ((SpectrumList_Mzml)list).DecodeThreadsInEffect,
                "MzmlDecodeThreads did not reach the spectrum list");

            for (int i = 0; i < list.Count; i++)
            {
                var spectrum = list.GetSpectrum(i, getBinaryData: true);
                var integers = new List<long>();
                foreach (var arr in spectrum.IntegerDataArrays)
                    integers.AddRange(arr.Data);

                result.Add((
                    spectrum.Id,
                    ToArray(spectrum.GetMZArray()),
                    ToArray(spectrum.GetIntensityArray()),
                    integers.ToArray()));
            }
            return result;
        }
    }
}
