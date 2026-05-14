using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using HDF.PInvoke;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Readers;
using Pwiz.Data.MsData.Spectra;

#pragma warning disable CA1806 // HDF5 close() ints

namespace Pwiz.Data.MsData.Tests;

/// <summary>
/// Identify-only smoke tests for <see cref="Mz5ReaderAdapter"/>. Full Read
/// support is WIP (Mz5ReferenceRead walker not yet ported); these tests
/// validate that the file-open + version-check path correctly recognizes
/// cpp-msconvert-produced mz5 files.
/// </summary>
[TestClass]
public class Mz5IdentifyTests
{
    [TestMethod]
    public void Identify_CppWrittenMz5_RecognizedAsMz5Format()
    {
        string? msconvert = FindCppMsconvert();
        if (msconvert is null)
        {
            Assert.Inconclusive("cpp msconvert.exe not found; build pwiz cpp first.");
            return;
        }
        string sourceMzML = FindSmallSourceMzML();
        if (!File.Exists(sourceMzML))
        {
            Assert.Inconclusive($"source mzML fixture not found: {sourceMzML}");
            return;
        }

        string outDir = Path.Combine(Path.GetTempPath(),
            $"mz5-identify-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outDir);
        try
        {
            // Run cpp msconvert to produce a real mz5 file.
            var psi = new ProcessStartInfo(msconvert)
            {
                Arguments = $"\"{sourceMzML}\" --mz5 -o \"{outDir}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi)!;
            proc.WaitForExit(120_000);
            Assert.AreEqual(0, proc.ExitCode, "cpp msconvert --mz5 exited non-zero");

            string cppOut = Directory.EnumerateFiles(outDir, "*.mz5").FirstOrDefault()
                ?? throw new InvalidOperationException("cpp msconvert produced no .mz5 file");

            // Identify path: HDF5 magic + FileInformation dataset present + version match.
            byte[] head = new byte[32];
            using (var fs = File.OpenRead(cppOut))
                fs.Read(head, 0, head.Length);
            string headStr = new(Array.ConvertAll(head, b => (char)b));
            var reader = new Mz5ReaderAdapter();
            Assert.AreEqual(CVID.MS_mz5_format, reader.Identify(cppOut, headStr));
        }
        finally
        {
            try { Directory.Delete(outDir, recursive: true); } catch { }
        }
    }

    [TestMethod]
    public void Read_CppWrittenMz5_PopulatesDocumentLevelMetadata()
    {
        // Round-trip an mzML through cpp msconvert --mz5, then read the mz5
        // through our adapter and confirm the document-level metadata
        // (CVs, source files, software, instrument config, run id) survived.
        // Spectrum / chromatogram lists aren't covered yet — they're the
        // pending Mz5ReferenceRead extension.
        string? msconvert = FindCppMsconvert();
        if (msconvert is null) { Assert.Inconclusive("cpp msconvert.exe not found"); return; }
        string sourceMzML = FindSmallSourceMzML();
        if (!File.Exists(sourceMzML)) { Assert.Inconclusive($"source not found: {sourceMzML}"); return; }

        string outDir = Path.Combine(Path.GetTempPath(), $"mz5-read-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outDir);
        try
        {
            var psi = new ProcessStartInfo(msconvert)
            {
                Arguments = $"\"{sourceMzML}\" --mz5 -o \"{outDir}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi)!;
            proc.WaitForExit(120_000);
            Assert.AreEqual(0, proc.ExitCode, "cpp msconvert --mz5 failed");

            string mz5Path = Directory.EnumerateFiles(outDir, "*.mz5").First();

            // Also read the same source via plain mzML adapter for a reference.
            var refMsd = new MSData();
            new MzmlReaderAdapter().Read(sourceMzML, refMsd);

            var mz5Msd = new MSData();
            new Mz5ReaderAdapter().Read(mz5Path, mz5Msd);

            // CV list
            Assert.IsTrue(mz5Msd.CVs.Count >= 2,
                $"expected CV list to have MS + UO, got {mz5Msd.CVs.Count}");
            Assert.IsTrue(mz5Msd.CVs.Any(cv => cv.Id == "MS"));
            Assert.IsTrue(mz5Msd.CVs.Any(cv => cv.Id == "UO"));

            // Source files (the cpp msconvert adds its own conversion source
            // entry, so we expect at least the original).
            Assert.IsTrue(mz5Msd.FileDescription.SourceFiles.Count >= 1,
                "no source files in mz5 output");

            // Software (must include 'pwiz' for the conversion record)
            Assert.IsTrue(mz5Msd.Software.Any(sw => sw.Id.StartsWith("pwiz", StringComparison.Ordinal)),
                "no pwiz software entry");

            // Run id should be non-empty and match the reference's run id
            Assert.IsFalse(string.IsNullOrEmpty(mz5Msd.Run.Id), "Run.Id is empty");
            Assert.AreEqual(refMsd.Run.Id, mz5Msd.Run.Id,
                "Run.Id mismatch between source mzML and round-tripped mz5");

            // Spectrum list: counts match, first spectrum's m/z + intensity
            // arrays match the source (cpp wrote double-precision by default).
            Assert.IsNotNull(mz5Msd.Run.SpectrumList, "Run.SpectrumList is null");
            Assert.AreEqual(refMsd.Run.SpectrumList!.Count, mz5Msd.Run.SpectrumList!.Count,
                "spectrum count mismatch");

            var refSpec = refMsd.Run.SpectrumList.GetSpectrum(0, getBinaryData: true);
            var mz5Spec = mz5Msd.Run.SpectrumList.GetSpectrum(0, getBinaryData: true);
            Assert.AreEqual(refSpec.Id, mz5Spec.Id, "spectrum[0].Id mismatch");

            var refPairs = new List<MZIntensityPair>();
            refSpec.GetMZIntensityPairs(refPairs);
            var mz5Pairs = new List<MZIntensityPair>();
            mz5Spec.GetMZIntensityPairs(mz5Pairs);
            Assert.AreEqual(refPairs.Count, mz5Pairs.Count, "peak count mismatch");
            for (int i = 0; i < refPairs.Count; i++)
            {
                Assert.AreEqual(refPairs[i].Mz, mz5Pairs[i].Mz, 1e-9,
                    $"m/z mismatch at peak {i}");
                Assert.AreEqual(refPairs[i].Intensity, mz5Pairs[i].Intensity, 1e-9,
                    $"intensity mismatch at peak {i}");
            }

            // Chromatogram list: counts match, first chromatogram's time +
            // intensity arrays match the source.
            int refChromCount = refMsd.Run.ChromatogramList?.Count ?? 0;
            int mz5ChromCount = mz5Msd.Run.ChromatogramList?.Count ?? 0;
            Assert.AreEqual(refChromCount, mz5ChromCount, "chromatogram count mismatch");
            if (refChromCount > 0)
            {
                var refChrom = refMsd.Run.ChromatogramList!.GetChromatogram(0, getBinaryData: true);
                var mz5Chrom = mz5Msd.Run.ChromatogramList!.GetChromatogram(0, getBinaryData: true);
                Assert.AreEqual(refChrom.Id, mz5Chrom.Id, "chromatogram[0].Id mismatch");

                var refTime = refChrom.GetTimeArray()?.Data;
                var mz5Time = mz5Chrom.GetTimeArray()?.Data;
                Assert.IsNotNull(refTime); Assert.IsNotNull(mz5Time);
                Assert.AreEqual(refTime.Count, mz5Time.Count, "chromatogram time-array count mismatch");
                for (int i = 0; i < Math.Min(refTime.Count, 10); i++)
                    Assert.AreEqual(refTime[i], mz5Time[i], 1e-9, $"time[{i}] mismatch");

                var refInt = refChrom.GetIntensityArray()?.Data;
                var mz5Int = mz5Chrom.GetIntensityArray()?.Data;
                Assert.IsNotNull(refInt); Assert.IsNotNull(mz5Int);
                Assert.AreEqual(refInt.Count, mz5Int.Count, "chromatogram intensity-array count mismatch");
                for (int i = 0; i < Math.Min(refInt.Count, 10); i++)
                    Assert.AreEqual(refInt[i], mz5Int[i], 1e-9, $"intensity[{i}] mismatch");
            }
        }
        finally { try { Directory.Delete(outDir, recursive: true); } catch { } }
    }

    [TestMethod]
    public void Identify_HdfFileWithoutFileInformation_ReturnsUnknown()
    {
        // Plain HDF5 file with no mz5 FileInformation dataset; Mz5ReaderAdapter
        // must NOT claim it (mzMLb shares the magic bytes, so we'd otherwise
        // mis-identify any HDF5 file as mz5).
        string path = Path.Combine(Path.GetTempPath(), $"plain-{Guid.NewGuid():N}.h5");
        H5E.set_auto(H5E.DEFAULT, null, IntPtr.Zero);
        long f = H5F.create(path, H5F.ACC_TRUNC);
        H5F.close(f);
        try
        {
            byte[] head = new byte[32];
            using (var fs = File.OpenRead(path))
                fs.Read(head, 0, head.Length);
            string headStr = new(Array.ConvertAll(head, b => (char)b));
            var reader = new Mz5ReaderAdapter();
            Assert.AreEqual(CVID.CVID_Unknown, reader.Identify(path, headStr));
        }
        finally { File.Delete(path); }
    }

    private static string? FindCppMsconvert()
    {
        foreach (var rel in new[]
        {
            "build-nt-x86/msvc-release-x86_64/msconvert.exe",
            "build-nt-x86/msvc-release/msconvert.exe",
        })
        {
            string p = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", rel));
            if (File.Exists(p)) return p;
        }
        return null;
    }

    private static string FindSmallSourceMzML() =>
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..",
            "pwiz", "data", "vendor_readers", "Bruker",
            "Reader_Bruker_Test.data",
            "20percLaser_100fold_1_0_H6_MS-ms1-centroid.mzML"));
}
