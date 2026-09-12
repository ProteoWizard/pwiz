using System.Globalization;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using Pwiz.Analysis.Demux;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Chemistry;

namespace Pwiz.Analysis.Tests.Demux;

/// <summary>
/// Unit tests for the demux subcomponents — port of the relevant cpp tests under
/// <c>pwiz/analysis/demux/</c> (PrecursorMaskCodec, SpectrumPeakExtractor) plus a fresh-written
/// NNLS test (cpp doesn't ship a standalone NNLS test; the algorithm is exercised through
/// <c>SpectrumList_DemuxTest</c>). End-to-end parity tests against cpp's
/// <c>SpectrumList_DemuxTest.data</c> fixtures live in
/// <c>SpectrumProcessing/SpectrumListDemuxTests.cs</c>.
/// </summary>
public static class DemuxTests
{
    // ============================================================================
    //   Nnls — Lawson-Hanson NNLS solver tests
    // ============================================================================

    [TestClass]
    public class NnlsTests
    {
        [TestMethod]
        public void IdentitySystem_ReturnsB()
        {
            // A = I, b = (1, 2, 3). NNLS reduces to plain LS; x = b since b ≥ 0.
            var A = DenseMatrix.OfArray(new[,] { { 1.0, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } });
            var b = DenseVector.OfArray(new[] { 1.0, 2.0, 3.0 });
            var x = Nnls.Solve(A, b);
            Assert.IsNotNull(x);
            AssertVectorEqual(new[] { 1.0, 2.0, 3.0 }, x, 1e-12);
        }

        [TestMethod]
        public void IdentitySystem_NegativeBComponent_ClampsToZero()
        {
            // A = I, b = (1, -2, 3). Unconstrained x would be (1, -2, 3); NNLS clamps the negative to 0.
            // KKT: at x = (1, 0, 3), gradient w = A^T b - A^T A x = (0, -2, 0), max in Z is 0 ≤ ε. Optimal.
            var A = DenseMatrix.OfArray(new[,] { { 1.0, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } });
            var b = DenseVector.OfArray(new[] { 1.0, -2.0, 3.0 });
            var x = Nnls.Solve(A, b);
            Assert.IsNotNull(x);
            AssertVectorEqual(new[] { 1.0, 0.0, 3.0 }, x, 1e-12);
        }

        [TestMethod]
        public void OverdeterminedConsistent_RecoversTrueX()
        {
            // A is 4×2; b = A * (3, 5). NNLS should recover x = (3, 5) exactly.
            var A = DenseMatrix.OfArray(new[,]
            {
                { 1.0, 2.0 },
                { 2.0, 1.0 },
                { 3.0, 0.5 },
                { 0.5, 4.0 },
            });
            var trueX = DenseVector.OfArray(new[] { 3.0, 5.0 });
            var b = A * trueX;
            var x = Nnls.Solve(A, b);
            Assert.IsNotNull(x);
            AssertVectorEqual(new[] { 3.0, 5.0 }, x, 1e-10);
        }

        [TestMethod]
        public void OverdeterminedWithNoise_NonNegativeAndNearTrueX()
        {
            // Same A as above with trueX = (3, 5); add small Gaussian-ish noise to b. NNLS should
            // recover something close to trueX with both components > 0.
            var A = DenseMatrix.OfArray(new[,]
            {
                { 1.0, 2.0 },
                { 2.0, 1.0 },
                { 3.0, 0.5 },
                { 0.5, 4.0 },
            });
            var trueX = DenseVector.OfArray(new[] { 3.0, 5.0 });
            var b = A * trueX;
            b[0] += 0.01; b[1] -= 0.02; b[2] += 0.005; b[3] -= 0.01;
            var x = Nnls.Solve(A, b);
            Assert.IsNotNull(x);
            Assert.IsTrue(x![0] > 0 && x[1] > 0, $"both components should be > 0, got ({x[0]}, {x[1]})");
            Assert.AreEqual(3.0, x[0], 0.05);
            Assert.AreEqual(5.0, x[1], 0.05);
        }

        [TestMethod]
        public void UnconstrainedNegativeOptimum_ReturnsNonNegativeOptimum()
        {
            // A = [[1, 0], [0, 1], [1, 1]], b = (-1, -1, -1). Unconstrained LS solution is
            // negative; NNLS clamps to (0, 0) with residual = b, ‖b‖² = 3.
            var A = DenseMatrix.OfArray(new[,] { { 1.0, 0 }, { 0, 1 }, { 1, 1 } });
            var b = DenseVector.OfArray(new[] { -1.0, -1.0, -1.0 });
            var x = Nnls.Solve(A, b);
            Assert.IsNotNull(x);
            AssertVectorEqual(new[] { 0.0, 0.0 }, x, 1e-12);
        }

        [TestMethod]
        public void IterationCount_TracksLsSolves()
        {
            // 2×2 identity, b > 0 → NNLS adds both columns to P then solves once each. With our
            // implementation that's 2 LS solves total (one per outer iteration after AddToP).
            var A = DenseMatrix.OfArray(new[,] { { 1.0, 0 }, { 0, 1 } });
            var b = DenseVector.OfArray(new[] { 1.0, 2.0 });
            var solver = new Nnls(A);
            Assert.IsTrue(solver.Solve(b));
            Assert.IsTrue(solver.IterationCount >= 1 && solver.IterationCount <= 4,
                $"expected a small iteration count, got {solver.IterationCount}");
        }

        private static void AssertVectorEqual(double[] expected, double[]? actual, double tolerance)
        {
            Assert.IsNotNull(actual);
            Assert.AreEqual(expected.Length, actual!.Length, "vector length mismatch");
            for (int i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], actual[i], tolerance, $"x[{i}]");
        }
    }

    // ============================================================================
    //   PrecursorMaskCodec — port of cpp PrecursorMaskCodecTest.cpp
    // ============================================================================

    [TestClass]
    public class PrecursorMaskCodecTests
    {
        [TestMethod]
        public void NoMs2Scans_Throws()
        {
            var sl = new SpectrumListSimple();
            sl.Spectra.Add(MakeMs1(0, mzs: new[] { 100.0 }));
            sl.Spectra.Add(MakeMs1(1, mzs: new[] { 100.0 }));

            var ex = Assert.ThrowsException<InvalidOperationException>(() => new PrecursorMaskCodec(sl));
            StringAssert.Contains(ex.Message, "No MS2 scans");
        }

        [TestMethod]
        public void Ms2WithEmptyPrecursors_Throws()
        {
            var sl = new SpectrumListSimple();
            sl.Spectra.Add(MakeMs1(0, mzs: new[] { 100.0 }));
            var ms2 = new Spectrum { Index = 1, Id = "scan=2" };
            ms2.Params.Set(CVID.MS_ms_level, 2);
            sl.Spectra.Add(ms2);

            var ex = Assert.ThrowsException<InvalidOperationException>(() => new PrecursorMaskCodec(sl));
            StringAssert.Contains(ex.Message, "missing precursor information");
        }

        [TestMethod]
        public void TooFewSpectra_ThrowsCannotDetermineScheme()
        {
            var sl = new SpectrumListSimple();
            sl.Spectra.Add(MakeMs1(0, mzs: new[] { 100.0 }));
            sl.Spectra.Add(MakeMs2(1, isoCenter: 500.0, halfWidth: 5.0));
            sl.Spectra.Add(MakeMs1(2, mzs: new[] { 100.0 }));

            var ex = Assert.ThrowsException<InvalidOperationException>(() => new PrecursorMaskCodec(sl));
            StringAssert.Contains(ex.Message, "Could not determine demultiplexing scheme");
        }

        [TestMethod]
        public void VaryingPrecursorCount_Throws()
        {
            var sl = new SpectrumListSimple();
            sl.Spectra.Add(MakeMs1(0, mzs: new[] { 100.0 }));
            sl.Spectra.Add(MakeMs2(1, isoCenter: 500.0, halfWidth: 5.0));
            var ms2WithTwo = MakeMs2(2, isoCenter: 510.0, halfWidth: 5.0);
            ms2WithTwo.Precursors.Add(NewPrecursor(520.0, halfWidth: 5.0));
            sl.Spectra.Add(ms2WithTwo);

            var ex = Assert.ThrowsException<InvalidOperationException>(() => new PrecursorMaskCodec(sl));
            StringAssert.Contains(ex.Message, "varying between");
        }

        [TestMethod]
        public void SingleOverlap_InfersExpectedSchemeAndCounts()
        {
            // Mirror cpp's SingleOverlapTest: 5 cycles × 25 MS2 scans/cycle × 2 overlaps × 1 precursor.
            // Expected: PrecursorsPerSpectrum=1, OverlapsPerCycle=2, SpectraPerCycle=50, NumDemuxWindows=51.
            var sl = BuildSingleOverlapSpectrumList(numCycles: 5, scansPerHalf: 25,
                mzStart: 400.0, mzEnd: 600.0);

            var pmc = new PrecursorMaskCodec(sl);
            Assert.AreEqual(1, pmc.PrecursorsPerSpectrum, "PrecursorsPerSpectrum");
            Assert.AreEqual(2, pmc.OverlapsPerCycle, "OverlapsPerCycle");
            Assert.AreEqual(50, pmc.SpectraPerCycle, "SpectraPerCycle");
            Assert.AreEqual(51, pmc.NumDemuxWindows, "NumDemuxWindows");
            Assert.AreEqual(50 * 1 * 2, pmc.DemuxBlockSize, "DemuxBlockSize");
        }

        [TestMethod]
        public void SpectrumToIndices_FindsExpectedSubWindows()
        {
            var sl = BuildSingleOverlapSpectrumList(numCycles: 5, scansPerHalf: 25,
                mzStart: 400.0, mzEnd: 600.0);
            var pmc = new PrecursorMaskCodec(sl);

            Spectrum? ms2 = null;
            for (int i = 0; i < sl.Count; i++)
            {
                var s = sl.GetSpectrum(i);
                if (s.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0) == 2) { ms2 = s; break; }
            }
            Assert.IsNotNull(ms2);

            var indices = new List<int>();
            pmc.SpectrumToIndices(ms2!, indices);
            Assert.AreEqual(pmc.OverlapsPerCycle * pmc.PrecursorsPerSpectrum, indices.Count);
            CollectionAssert.AllItemsAreUnique(indices);
            foreach (var idx in indices)
                Assert.IsTrue(idx >= 0 && idx < pmc.NumDemuxWindows, $"index {idx} out of range");
        }

        [TestMethod]
        public void GetMask_ReturnsZeroExceptAtSpectrumIndices()
        {
            var sl = BuildSingleOverlapSpectrumList(numCycles: 5, scansPerHalf: 25,
                mzStart: 400.0, mzEnd: 600.0);
            var pmc = new PrecursorMaskCodec(sl);

            Spectrum? ms2 = null;
            for (int i = 0; i < sl.Count; i++)
            {
                var s = sl.GetSpectrum(i);
                if (s.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0) == 2) { ms2 = s; break; }
            }

            var indices = new List<int>();
            pmc.SpectrumToIndices(ms2!, indices);
            var mask = pmc.GetMask(ms2!, weight: 3.0);

            Assert.AreEqual(pmc.DemuxBlockSize, mask.Count);
            for (int i = 0; i < mask.Count; i++)
            {
                double expected = indices.Contains(i) ? 3.0 : 0.0;
                Assert.AreEqual(expected, mask[i], 1e-12, $"mask[{i}]");
            }
        }
    }

    // ============================================================================
    //   SpectrumPeakExtractor — port of cpp SpectrumPeakExtractorTest.cpp
    // ============================================================================

    [TestClass]
    public class SpectrumPeakExtractorTests
    {
        [TestMethod]
        public void NumPeaks_ReportsConstructorListLength()
        {
            var ext = new SpectrumPeakExtractor(
                new[] { 100.0, 200.0, 300.0 },
                new MZTolerance(10, MZToleranceUnits.Ppm));
            Assert.AreEqual(3, ext.NumPeaks);
        }

        [TestMethod]
        public void SelfExtraction_ReturnsOriginalIntensities()
        {
            double[] peakMzs = { 100.0, 200.0, 300.0, 500.0 };
            double[] intensities = { 7.0, 13.0, 4.0, 22.0 };

            var spec = MakeSpectrumPlain(peakMzs, intensities);
            var ext = new SpectrumPeakExtractor(peakMzs, new MZTolerance(10, MZToleranceUnits.Ppm));
            var matrix = DenseMatrix.Create(1, ext.NumPeaks, 0);
            ext.Extract(spec, matrix, rowNum: 0);

            for (int i = 0; i < intensities.Length; i++)
                Assert.AreEqual(intensities[i], matrix[0, i], 1e-6, $"col {i}");
        }

        [TestMethod]
        public void SelfExtraction_WithWeight_ScalesRow()
        {
            double[] peakMzs = { 100.0, 200.0 };
            var spec = MakeSpectrumPlain(peakMzs, new[] { 5.0, 10.0 });
            var ext = new SpectrumPeakExtractor(peakMzs, new MZTolerance(10, MZToleranceUnits.Ppm));
            var matrix = DenseMatrix.Create(1, ext.NumPeaks, 0);

            ext.Extract(spec, matrix, rowNum: 0, weight: 3.0);
            Assert.AreEqual(15.0, matrix[0, 0], 1e-9);
            Assert.AreEqual(30.0, matrix[0, 1], 1e-9);
        }

        [TestMethod]
        public void OutOfRangePeaks_AreIgnored()
        {
            double[] peakMzs = { 100.0, 200.0 };
            var spec = MakeSpectrumPlain(
                new[] { 50.0, 100.0, 200.0, 1000.0 },
                new[] {  9.0,   3.0,   4.0,    7.0 });
            var ext = new SpectrumPeakExtractor(peakMzs, new MZTolerance(10, MZToleranceUnits.Ppm));
            var matrix = DenseMatrix.Create(1, ext.NumPeaks, 0);

            ext.Extract(spec, matrix, rowNum: 0);
            Assert.AreEqual(3.0, matrix[0, 0], 1e-9);
            Assert.AreEqual(4.0, matrix[0, 1], 1e-9);
        }

        [TestMethod]
        public void CloselySpacedPeaks_BinIntoSameTarget()
        {
            // Mirror cpp's "closely-spaced peaks" scenario: multiple input peaks within a single
            // tolerance window all sum into that window's bin.
            double[] binMzs = { 2.0, 4.0, 6.0, 8.0, 10.0, 12.0, 14.0, 16.0, 18.0 };
            double[] inputMz = { 0.0, 2.0, 2.000001, 3.999999, 4.0, 4.000001, 6.0, 8.0, 10.0, 12.0, 14.0, 16.0, 18.0 };
            double[] inputIntensity = new double[inputMz.Length];
            for (int i = 0; i < inputIntensity.Length; i++) inputIntensity[i] = 1.0;

            var spec = MakeSpectrumPlain(inputMz, inputIntensity);
            var ext = new SpectrumPeakExtractor(binMzs, new MZTolerance(10, MZToleranceUnits.Ppm));
            var matrix = DenseMatrix.Create(1, ext.NumPeaks, 0);
            ext.Extract(spec, matrix, rowNum: 0);

            // mz=0 is below the lowest bin. mz=2, 2.000001 → bin 0. mz=3.999999, 4, 4.000001 → bin 1.
            // The remaining inputs hit bins 2..8 exactly.
            Assert.AreEqual(2.0, matrix[0, 0], 1e-9, "bin @2");
            Assert.AreEqual(3.0, matrix[0, 1], 1e-9, "bin @4");
            for (int i = 2; i < binMzs.Length; i++)
                Assert.AreEqual(1.0, matrix[0, i], 1e-9, $"bin @{binMzs[i]}");
        }
    }

    // ============================================================================
    //   Shared spectrum builders used by the PrecursorMaskCodec + extractor tests
    // ============================================================================

    private static Spectrum MakeMs1(int index, double[] mzs)
    {
        var s = new Spectrum { Index = index, Id = $"scan={index + 1}" };
        s.Params.Set(CVID.MS_ms_level, 1);
        s.SetMZIntensityArrays(mzs, new double[mzs.Length], CVID.MS_number_of_detector_counts);
        return s;
    }

    private static Spectrum MakeMs2(int index, double isoCenter, double halfWidth)
    {
        var s = new Spectrum { Index = index, Id = $"scan={index + 1}" };
        s.Params.Set(CVID.MS_ms_level, 2);
        s.Precursors.Add(NewPrecursor(isoCenter, halfWidth));
        s.SetMZIntensityArrays(new[] { 200.0 }, new[] { 1.0 }, CVID.MS_number_of_detector_counts);
        return s;
    }

    private static Precursor NewPrecursor(double isoCenter, double halfWidth)
    {
        var p = new Precursor();
        p.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, isoCenter, CVID.MS_m_z);
        p.IsolationWindow.Set(CVID.MS_isolation_window_lower_offset, halfWidth, CVID.MS_m_z);
        p.IsolationWindow.Set(CVID.MS_isolation_window_upper_offset, halfWidth, CVID.MS_m_z);
        p.SelectedIons.Add(new SelectedIon(isoCenter));
        return p;
    }

    private static SpectrumListSimple BuildSingleOverlapSpectrumList(int numCycles, int scansPerHalf,
        double mzStart, double mzEnd)
    {
        var sl = new SpectrumListSimple();
        double width = (mzEnd - mzStart) / scansPerHalf;
        double halfWidth = width / 2.0;
        int idx = 0;
        for (int cycle = 0; cycle < numCycles; cycle++)
        {
            sl.Spectra.Add(MakeMs1(idx++, mzs: new[] { 200.0 }));
            for (int s = 0; s < scansPerHalf; s++)
            {
                double center = mzStart + halfWidth + s * width;
                sl.Spectra.Add(MakeMs2(idx++, isoCenter: center, halfWidth: halfWidth));
            }
            for (int s = 0; s < scansPerHalf; s++)
            {
                double center = mzStart + halfWidth + s * width + halfWidth;
                sl.Spectra.Add(MakeMs2(idx++, isoCenter: center, halfWidth: halfWidth));
            }
        }
        return sl;
    }

    private static Spectrum MakeSpectrumPlain(double[] mz, double[] intensity)
    {
        var s = new Spectrum { Index = 0, Id = "scan=1" };
        s.SetMZIntensityArrays(mz, intensity, CVID.MS_number_of_detector_counts);
        return s;
    }
}
