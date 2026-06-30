using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pwiz.Data.MsData.MzPeak;

namespace Pwiz.Data.MsData.Tests;

/// <summary>
/// End-to-end round-trip tests: synthesise a 3-spectrum / 3-chromatogram file
/// (one MS2 with two co-isolated precursors), write it, read it back, and
/// verify every field the writer touched matches what the reader surfaces.
/// One shared fixture file is built in <see cref="ClassInit"/>; each
/// [TestMethod] covers a distinct slice (scan info, multi-precursor, mz_delta_model,
/// chromatograms, ...) so a failure points straight at the broken column group.
/// </summary>
[TestClass]
public class RoundTripTests
{
    private static string s_outputPath = string.Empty;
    private static MzPeakReader s_reader = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext _)
    {
        s_outputPath = Path.Combine(Path.GetTempPath(), $"mzpeak-roundtrip-{Guid.NewGuid():N}.mzpeak");
        var spectra = BuildSyntheticSpectra();
        var chroms = BuildSyntheticChromatograms();
        MzPeakWriter.Write(s_outputPath, spectra, BuildSyntheticFileMetadata(), chroms);
        s_reader = new MzPeakReader(s_outputPath);
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        s_reader?.Dispose();
        if (File.Exists(s_outputPath)) File.Delete(s_outputPath);
    }

    [TestMethod]
    public void SpectrumCount_ExcludesPrecursorFanOut()
    {
        // 3 logical spectra; spectrum 1 fans into 2 parquet rows for its two
        // precursors, but the reader collapses that back to a single spectrum.
        Assert.AreEqual(3, CountDistinctSpectra(s_reader));
    }

    [TestMethod]
    public void Spectrum0_CoreFields_RoundTrip()
    {
        var d = s_reader.GetSpectrumDescription(0);
        Assert.AreEqual("synthetic=1 scan=1", d.Id);
        Assert.AreEqual(0.0050, d.Time, 1e-9);
        Assert.AreEqual(1, d.MsLevel);
        Assert.IsTrue(d.IsProfile);
        Assert.IsFalse(d.IsCentroid);
        Assert.AreEqual(0, d.Precursors.Count);
    }

    [TestMethod]
    public void Spectrum0_ScanFields_RoundTrip()
    {
        var scan = s_reader.GetSpectrumDescription(0).Scan;
        Assert.IsNotNull(scan);
        Assert.AreEqual(0.0050, scan!.StartTime!.Value, 1e-9);
        Assert.AreEqual("FTMS + p ESI Full ms", scan.FilterString);
        Assert.AreEqual(0u, scan.InstrumentConfigurationRef);
        Assert.AreEqual(68.227, scan.IonInjectionTime!.Value, 1e-9);
        Assert.AreEqual(1.234, scan.IonMobilityValue!.Value, 1e-9);
        Assert.AreEqual("MS:1002476", scan.IonMobilityTypeCurie);
        Assert.AreEqual(2L, scan.PresetScanConfiguration);
    }

    [TestMethod]
    public void Spectrum0_ScanWindows_RoundTrip()
    {
        var windows = s_reader.GetSpectrumDescription(0).Scan!.ScanWindows;
        Assert.AreEqual(2, windows.Count);
        Assert.AreEqual(200.0, windows[0].LowerLimit);
        Assert.AreEqual(600.0, windows[0].UpperLimit);
        Assert.AreEqual(800.0, windows[1].LowerLimit);
        Assert.AreEqual(2000.0, windows[1].UpperLimit);
    }

    [TestMethod]
    public void Spectrum0_MsDataParityFields_RoundTrip()
    {
        var d = s_reader.GetSpectrumDescription(0);
        Assert.AreEqual(1, d.ScanPolarity);
        Assert.AreEqual(400.0, d.BasePeakMz);
        Assert.AreEqual(5e3, d.BasePeakIntensity);
        Assert.AreEqual(15e3, d.TotalIonCurrent);
        Assert.AreEqual(100.0, d.LowestObservedMz);
        Assert.AreEqual(500.0, d.HighestObservedMz);
        Assert.AreEqual("DP01", d.DataProcessingRef);
        Assert.AreEqual(5L, d.NumberOfDataPoints, "auto-populated from Mz.Length");
    }

    [TestMethod]
    public void Spectrum0_MzDeltaModel_RoundTrip()
    {
        var model = s_reader.GetSpectrumDescription(0).MzDeltaModel;
        Assert.IsNotNull(model);
        CollectionAssert.AreEqual(new[] { 0.001, 0.002, 0.003 }, model!.ToArray());
    }

    [TestMethod]
    public void Spectrum1_MultiPrecursor_FansOutToSeparateRows()
    {
        var d = s_reader.GetSpectrumDescription(1);
        Assert.AreEqual(2, d.Precursors.Count);

        var p0 = d.Precursors[0];
        var p1 = d.Precursors[1];

        // Both precursors share source_index == spectrum.index but carry
        // distinct precursor_index values 0 and 1.
        Assert.AreEqual(1ul, p0.SourceIndex);
        Assert.AreEqual(1ul, p1.SourceIndex);
        Assert.AreEqual(0ul, p0.PrecursorIndex);
        Assert.AreEqual(1ul, p1.PrecursorIndex);

        Assert.AreEqual("synthetic=1 scan=2", p0.PrecursorId);
        Assert.AreEqual(810.79, p0.IsolationWindow!.TargetMz);
        Assert.AreEqual(35.0, p0.Activation!.CollisionEnergy);
        Assert.AreEqual(810.7894, p0.SelectedIon!.Mz);

        Assert.AreEqual("synthetic=1 scan=2#p2", p1.PrecursorId);
        Assert.AreEqual(542.21, p1.IsolationWindow!.TargetMz);
        Assert.AreEqual(28.0, p1.Activation!.CollisionEnergy);
        Assert.AreEqual(542.2105, p1.SelectedIon!.Mz);
        Assert.AreEqual(3L, p1.SelectedIon.ChargeState);
    }

    [TestMethod]
    public void Spectrum1_SpectrumParameters_PolymorphicValues_RoundTrip()
    {
        var ps = s_reader.GetSpectrumDescription(1).Parameters;
        Assert.AreEqual(4, ps.Count);
        Assert.AreEqual("hello", ps.First(p => p.Accession == "MS:9000001").ValueString);
        Assert.AreEqual(42L, ps.First(p => p.Accession == "MS:9000002").ValueInteger);
        Assert.AreEqual(3.14159, ps.First(p => p.Accession == "MS:9000003").ValueFloat!.Value, 1e-9);
        Assert.AreEqual(true, ps.First(p => p.Accession == "MS:9000004").ValueBoolean);
        Assert.AreEqual("UO:0000010", ps.First(p => p.Accession == "MS:9000003").Unit);
    }

    [TestMethod]
    public void SpectrumData_PointArrays_RoundTrip()
    {
        var d0 = s_reader.GetSpectrumData(0);
        Assert.IsNotNull(d0);
        CollectionAssert.AreEqual(new[] { 100.0, 200.0, 300.0, 400.0, 500.0 }, d0!.Mz);

        var d1 = s_reader.GetSpectrumData(1);
        Assert.IsNotNull(d1);
        Assert.AreEqual(3, d1!.Mz.Length);
    }

    [TestMethod]
    public void SupplementaryPeaks_RoundTrip()
    {
        var p = s_reader.GetSupplementaryPeaks(0);
        Assert.IsNotNull(p);
        CollectionAssert.AreEqual(new[] { 200.1, 300.2, 400.3 }, p!.Mz);

        // Spectrum 1 didn't write supplementary peaks.
        Assert.IsNull(s_reader.GetSupplementaryPeaks(1));
    }

    [TestMethod]
    public void Chromatograms_RoundTrip()
    {
        Assert.AreEqual(3, s_reader.ChromatogramCount);
        var tic = s_reader.GetChromatogramDescription(0);
        Assert.AreEqual("TIC", tic.Id);
        Assert.AreEqual("MS:1000235", tic.ChromatogramTypeCurie);

        var ticData = s_reader.GetChromatogramData(0);
        Assert.IsNotNull(ticData);
        Assert.AreEqual(3, ticData!.Time.Length);
    }

    // ===== Synthetic fixture builders =====

    /// <summary>
    /// Count distinct spectra by their logical index. The reader currently
    /// exposes <c>SpectrumCount</c> as the number of primary metadata rows
    /// (one per spectrum) — this asserts that the fan-out doesn't inflate it.
    /// </summary>
    private static int CountDistinctSpectra(MzPeakReader r)
    {
        var seen = new HashSet<ulong>();
        for (int i = 0; i < r.SpectrumCount; i++) seen.Add(r.GetSpectrumDescription(i).Index);
        return seen.Count;
    }

    private static MzPeakWriter.SpectrumToWrite[] BuildSyntheticSpectra() => new[]
    {
        new MzPeakWriter.SpectrumToWrite(
            Index: 0, Id: "synthetic=1 scan=1", Time: 0.0050, MsLevel: 1, IsProfile: true,
            Mz: new[] { 100.0, 200.0, 300.0, 400.0, 500.0 },
            Intensity: new[] { 1e3f, 2e3f, 3e3f, 5e3f, 4e3f },
            ScanStartTime: 0.0050, FilterString: "FTMS + p ESI Full ms",
            InstrumentConfigurationRef: 0, IonInjectionTime: 68.227,
            ScanWindowLowerLimits: new double?[] { 200.0, 800.0 },
            ScanWindowUpperLimits: new double?[] { 600.0, 2000.0 },
            SupplementaryPeaksMz: new[] { 200.1, 300.2, 400.3 },
            SupplementaryPeaksIntensity: new[] { 1.9e3f, 2.9e3f, 4.9e3f },
            ScanPolarity: 1,
            BasePeakMz: 400.0, BasePeakIntensity: 5e3,
            TotalIonCurrent: 15e3,
            LowestObservedMz: 100.0, HighestObservedMz: 500.0,
            SpectrumDataProcessingRef: "DP01",
            ScanIonMobilityValue: 1.234, ScanIonMobilityTypeCurie: "MS:1002476",
            PresetScanConfiguration: 2,
            MzDeltaModel: new double?[] { 0.001, 0.002, 0.003 }),
        new MzPeakWriter.SpectrumToWrite(
            Index: 1, Id: "synthetic=1 scan=2", Time: 0.0100, MsLevel: 2, IsProfile: false,
            Mz: new[] { 150.5, 250.5, 350.5 },
            Intensity: new[] { 100f, 250f, 80f },
            ScanStartTime: 0.0100, FilterString: "ITMS + c ESI d Full ms2",
            InstrumentConfigurationRef: 0, IonInjectionTime: 7.99,
            SpectrumParameters: new MzPeakReader.CvParam[]
            {
                new("custom string", "MS:9000001", ValueString: "hello", null, null, null, Unit: null),
                new("custom int",    "MS:9000002", null, ValueInteger: 42, null, null, null),
                new("custom float",  "MS:9000003", null, null, ValueFloat: 3.14159, null, "UO:0000010"),
                new("custom bool",   "MS:9000004", null, null, null, ValueBoolean: true, null),
            },
            ScanParameters: new MzPeakReader.CvParam[]
            {
                new("scan param 1", "MS:9000010", "alpha", null, null, null, null),
            },
            Precursors: new[]
            {
                new MzPeakWriter.PrecursorToWrite(
                    PrecursorId: "synthetic=1 scan=2",
                    IsolationTargetMz: 810.79, IsolationLowerOffset: 2.0, IsolationUpperOffset: 2.0,
                    CollisionEnergy: 35.0, DissociationMethodCurie: "MS:1000133",
                    SelectedIonMz: 810.7894, SelectedIonPeakIntensity: 1234.5, SelectedIonChargeState: 2),
                new MzPeakWriter.PrecursorToWrite(
                    PrecursorId: "synthetic=1 scan=2#p2",
                    IsolationTargetMz: 542.21, IsolationLowerOffset: 1.5, IsolationUpperOffset: 1.5,
                    CollisionEnergy: 28.0, DissociationMethodCurie: "MS:1000133",
                    SelectedIonMz: 542.2105, SelectedIonPeakIntensity: 875.0, SelectedIonChargeState: 3),
            }),
        new MzPeakWriter.SpectrumToWrite(
            Index: 2, Id: "synthetic=1 scan=3", Time: 0.0150, MsLevel: 1, IsProfile: true,
            Mz: new[] { 110.0, 210.0, 310.0, 410.0 },
            Intensity: new[] { 5e2f, 1.5e3f, 2.5e3f, 1e3f },
            ScanStartTime: 0.0150),
    };

    private static MzPeakWriter.ChromatogramToWrite[] BuildSyntheticChromatograms() => new[]
    {
        new MzPeakWriter.ChromatogramToWrite(
            Index: 0, Id: "TIC", ChromatogramTypeCurie: "MS:1000235", DataProcessingRef: "DP01",
            Time: new[] { 0.001, 0.002, 0.003 },
            Intensity: new[] { 1e5f, 2e5f, 1.5e5f }),
        new MzPeakWriter.ChromatogramToWrite(
            Index: 1, Id: "BPC", ChromatogramTypeCurie: "MS:1000628", DataProcessingRef: "DP01",
            Time: new[] { 0.001, 0.002, 0.003 },
            Intensity: new[] { 5e4f, 1e5f, 8e4f }),
        new MzPeakWriter.ChromatogramToWrite(
            Index: 2, Id: "SIM 200.0", ChromatogramTypeCurie: "MS:1000626", DataProcessingRef: "DP01",
            Time: new[] { 0.001, 0.002 },
            Intensity: new[] { 100f, 200f }),
    };

    private static FileMetadata BuildSyntheticFileMetadata() => new(
        FileDescription: new FileDescription(
            Contents: Array.Empty<MzPeakCvParam>(),
            SourceFiles: Array.Empty<SourceFile>()),
        InstrumentConfigurations: Array.Empty<InstrumentConfiguration>(),
        DataProcessingMethods: Array.Empty<DataProcessingMethod>(),
        Software: Array.Empty<SoftwareInfo>(),
        Samples: Array.Empty<SampleInfo>(),
        Run: new RunInfo(
            Id: "synthetic-run",
            DefaultDataProcessingId: "DP01",
            DefaultInstrumentId: 0,
            DefaultSourceFileId: null,
            StartTime: null,
            Parameters: Array.Empty<MzPeakCvParam>()),
        SpectrumCount: 3,
        SpectrumDataPointCount: 12);
}
