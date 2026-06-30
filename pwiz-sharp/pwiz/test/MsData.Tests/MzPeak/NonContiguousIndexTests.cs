using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pwiz.Data.MsData.MzPeak;

namespace Pwiz.Data.MsData.Tests;

/// <summary>
/// Regression guard for the data-lookup key: the binary point layer is keyed on the spectrum's
/// stored <c>spectrum.index</c>, NOT its logical row position. The two coincide for pwiz-written
/// files (the writer numbers spectra 0..N-1), so the rest of the suite never exercises the
/// difference. Here we deliberately write non-contiguous indices (7, 42, 100) and confirm each
/// logical spectrum still gets its own peaks back — catching any regression to position-keyed lookup.
/// </summary>
[TestClass]
public class NonContiguousIndexTests
{
    [TestMethod]
    public void GetSpectrumData_KeysOnStoredIndex_NotRowPosition()
    {
        var spectra = new[]
        {
            new MzPeakWriter.SpectrumToWrite(
                Index: 7, Id: "scan=7", Time: 0.1, MsLevel: 1, IsProfile: true,
                Mz: new[] { 100.0, 101.0 }, Intensity: new[] { 10f, 11f }),
            new MzPeakWriter.SpectrumToWrite(
                Index: 42, Id: "scan=42", Time: 0.2, MsLevel: 1, IsProfile: true,
                Mz: new[] { 200.0, 201.0, 202.0 }, Intensity: new[] { 20f, 21f, 22f }),
            new MzPeakWriter.SpectrumToWrite(
                Index: 100, Id: "scan=100", Time: 0.3, MsLevel: 1, IsProfile: true,
                Mz: new[] { 300.0 }, Intensity: new[] { 30f }),
        };

        var fm = new FileMetadata(
            new FileDescription(Array.Empty<MzPeakCvParam>(), Array.Empty<SourceFile>()),
            Array.Empty<InstrumentConfiguration>(),
            Array.Empty<DataProcessingMethod>(),
            Array.Empty<SoftwareInfo>(),
            Array.Empty<SampleInfo>(),
            new RunInfo("run", null, null, null, null, Array.Empty<MzPeakCvParam>()),
            SpectrumCount: spectra.Length, SpectrumDataPointCount: 6);

        string path = Path.Combine(Path.GetTempPath(), $"mzpeak-noncontig-{Guid.NewGuid():N}.mzpeak");
        try
        {
            MzPeakWriter.Write(path, spectra, fm);
            using var reader = new MzPeakReader(path);

            Assert.AreEqual(3, reader.SpectrumCount);
            // Stored indices survive.
            Assert.AreEqual(7ul, reader.GetSpectrumDescription(0).Index);
            Assert.AreEqual(42ul, reader.GetSpectrumDescription(1).Index);
            Assert.AreEqual(100ul, reader.GetSpectrumDescription(2).Index);

            // Each logical spectrum gets ITS peaks (keyed by stored index, not row position).
            CollectionAssert.AreEqual(new[] { 100.0, 101.0 }, reader.GetSpectrumData(0)!.Mz);
            CollectionAssert.AreEqual(new[] { 200.0, 201.0, 202.0 }, reader.GetSpectrumData(1)!.Mz);
            CollectionAssert.AreEqual(new[] { 300.0 }, reader.GetSpectrumData(2)!.Mz);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
