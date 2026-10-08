using MathNet.Numerics.LinearAlgebra;
using Pwiz.Analysis.Demux;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Chemistry;

namespace Pwiz.Analysis;

/// <summary>
/// Demultiplexes spectra acquired from MSX or overlapping-DIA experiments by inferring the
/// per-window intensity contributions via non-negative least squares. Port of cpp's
/// <c>SpectrumList_Demux</c> in <c>pwiz/analysis/spectrum_processing/SpectrumList_Demux.cpp</c>.
/// </summary>
public sealed class SpectrumListDemux : SpectrumListWrapper
{
    /// <summary>Available demux algorithm choices.</summary>
    public enum Optimization
    {
        /// <summary>Default — MSX demultiplexing via <see cref="MsxDemultiplexer"/>.</summary>
        None,

        /// <summary>Pure overlap demultiplexing via <see cref="OverlapDemultiplexer"/>; adds
        /// chromatographic interpolation across the demux block.</summary>
        OverlapOnly,
    }

    /// <summary>Tunable parameters.</summary>
    public sealed class Params
    {
        /// <summary>Mass tolerance for MS/MS peak extraction (default 10 ppm).</summary>
        public MZTolerance MassError { get; init; } = new(10, MZToleranceUnits.Ppm);

        /// <summary>Multiplier expanding the demux block size (0 = exactly one cycle's worth of
        /// spectra). When > 0, extends by <c>demuxBlockExtra * SpectraPerCycle</c> rows.</summary>
        public double DemuxBlockExtra { get; init; }

        /// <summary>Maximum NNLS iterations per column (default 50).</summary>
        public int NnlsMaxIter { get; init; } = 50;

        /// <summary>NNLS convergence tolerance (default 1e-10).</summary>
        public double NnlsEps { get; init; } = 1e-10;

        /// <summary>Down-weight nearby spectra by RT distance during signal assembly (only when
        /// <see cref="InterpolateRetentionTime"/> is false).</summary>
        public bool ApplyWeighting { get; init; } = true;

        /// <summary>Rescale per-window contributions so their sum equals the original
        /// (un-demuxed) intensity. Default true; preserves overall intensity conservation.</summary>
        public bool RegularizeSums { get; init; } = true;

        /// <summary>Variable fill times: weights each design-matrix entry by the precursor's
        /// <c>MultiFillTime</c> user param.</summary>
        public bool VariableFill { get; init; }

        /// <summary>Interpolate signal rows to the demuxed spectrum's RT (overlap mode only).</summary>
        public bool InterpolateRetentionTime { get; init; } = true;

        /// <summary>Choice of demultiplexer algorithm.</summary>
        public Optimization Optimization { get; init; } = Optimization.None;

        /// <summary>Tolerance for treating window boundaries as the same point during overlap
        /// inference (passed through to <see cref="PrecursorMaskCodec"/>).</summary>
        public double MinimumWindowSize { get; init; } = 0.2;

        /// <summary>Drop edge isolation segments not covered at the same multiplicity as the
        /// bulk of the cycle.</summary>
        public bool RemoveNonOverlappingEdges { get; init; }
    }

    private readonly DemuxImpl _impl;

    /// <summary>Constructs a demux-wrapped spectrum list.</summary>
    public SpectrumListDemux(ISpectrumList inner, Params? p = null) : base(inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _impl = new DemuxImpl(inner, p ?? new Params(), Inner.DataProcessing);
    }

    /// <inheritdoc/>
    public override int Count => _impl.Count;

    /// <inheritdoc/>
    public override SpectrumIdentity SpectrumIdentity(int index) => _impl.SpectrumIdentity(index);

    /// <inheritdoc/>
    public override Spectrum GetSpectrum(int index, bool getBinaryData = false) =>
        _impl.GetSpectrum(index, getBinaryData);

    /// <inheritdoc/>
    public override DataProcessing? DataProcessing => _impl.DataProcessing;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable",
        Justification = "_inner is a DemuxSpectrumCache wrapping the same underlying ISpectrumList that SpectrumListDemux's base SpectrumListWrapper holds. Both wrappers point at the same disposable; SpectrumListBase.Dispose is idempotent (ISpectrumList.cs:147) so the base wrapper's DisposeCore handles cleanup. Adding a second dispose path here would be redundant.")]
    private sealed class DemuxImpl
    {
        private readonly DemuxSpectrumCache _inner;
        private readonly Params _params;
        private readonly PrecursorMaskCodec _pmc;
        private readonly IDemultiplexer _demux;
        private readonly NnlsSolver _solver;
        private readonly DemuxIndexMapper _indexMapper;

        public DataProcessing? DataProcessing { get; }

        // 1-deep cache: each multiplexed source spectrum produces multiple output sub-spectra
        // and they're requested consecutively. Solving once per source is the main optimization.
        private int _lastSolvedSourceIndex = -1;
        private Matrix<double>? _lastSolution;

        public DemuxImpl(ISpectrumList inner, Params p, DataProcessing? innerDp)
        {
            // Wrap the inner list in a bounded LRU + MS-level cache. PMC, the demultiplexer, the
            // index mapper, and our own per-source GetSpectrum all share this so the ~500-per-
            // source metadata reads (FindNearbySpectra) and ~30-per-source binary reads (mask
            // matrix + interpolation cycles) collapse to 1 unique fetch per spectrum amortized.
            _inner = new DemuxSpectrumCache(inner);
            _params = p;
            _solver = new NnlsSolver(p.NnlsMaxIter, p.NnlsEps);

            var pmcParams = new PrecursorMaskCodec.Params
            {
                VariableFill = p.VariableFill,
                MinimumWindowSize = p.MinimumWindowSize,
                RemoveNonOverlappingEdges = p.RemoveNonOverlappingEdges,
            };
            _pmc = new PrecursorMaskCodec(_inner, pmcParams);

            _demux = p.Optimization switch
            {
                Optimization.OverlapOnly => new OverlapDemultiplexer(new OverlapDemultiplexer.Params
                {
                    InterpolateRetentionTime = p.InterpolateRetentionTime,
                    ApplyWeighting = p.ApplyWeighting,
                    MassError = p.MassError,
                }),
                _ => new MsxDemultiplexer(new MsxDemultiplexer.Params
                {
                    ApplyWeighting = p.ApplyWeighting,
                    MassError = p.MassError,
                    VariableFill = p.VariableFill,
                }),
            };
            _demux.Initialize(_inner, _pmc);

            _indexMapper = new DemuxIndexMapper(_inner, _pmc);

            // Build the data-processing chain; the "PRISM Demultiplexing" UserParam is what
            // SpectrumWorkerThreads keys on in cpp, and the mzML writer surfaces it.
            var dp = new DataProcessing(innerDp?.Id ?? "pwiz_Reader_conversion");
            if (innerDp is not null)
                foreach (var pm in innerDp.ProcessingMethods)
                    dp.ProcessingMethods.Add(pm);
            var method = new ProcessingMethod
            {
                Order = dp.ProcessingMethods.Count,
                Software = dp.ProcessingMethods.FirstOrDefault()?.Software,
            };
            method.Set(CVID.MS_data_processing);
            method.UserParams.Add(new UserParam("PRISM Demultiplexing"));
            dp.ProcessingMethods.Add(method);
            DataProcessing = dp;
        }

        public int Count => _indexMapper.IndexMap.Count;

        public SpectrumIdentity SpectrumIdentity(int index) => _indexMapper.SpectrumIdentities[index];

        public Spectrum GetSpectrum(int index, bool getBinaryData)
        {
            var request = _indexMapper.IndexMap[index];
            if (request.MsLevel != 2)
            {
                // Pass-through for MS1 (and any non-MS2 spectrum).
                var orig = _inner.GetSpectrum(request.SpectrumOriginalIndex, getBinaryData: true);
                orig.Index = index;
                orig.Id = SpectrumIdentity(index).Id;
                return orig;
            }
            return BuildDemuxedSpectrum(index, request);
        }

        private Spectrum BuildDemuxedSpectrum(int outputIndex, DemuxRequestIndex request)
        {
            var refSpectrum = _inner.GetSpectrum(request.SpectrumOriginalIndex, getBinaryData: true);

            Matrix<double> solution;
            if (_lastSolution is not null && _lastSolvedSourceIndex == request.SpectrumOriginalIndex)
            {
                solution = _lastSolution;
            }
            else
            {
                var muxIndices = new List<int>();
                _demux.GetMatrixBlockIndices(request.SpectrumOriginalIndex, muxIndices, _params.DemuxBlockExtra);
                _demux.BuildDeconvBlock(request.SpectrumOriginalIndex, muxIndices, out var masks, out var signal);
                solution = _solver.Solve(masks, signal);
                _lastSolution = solution;
                _lastSolvedSourceIndex = request.SpectrumOriginalIndex;
            }

            // Copy the source spectrum and overwrite the demux-specific fields.
            var demuxed = CopySpectrum(refSpectrum);
            demuxed.Precursors.Clear();

            var deconvIndices = new List<int>();
            _pmc.SpectrumToIndices(refSpectrum, deconvIndices);
            var demuxIsoWindow = _pmc.GetIsolationWindow(deconvIndices[request.DemuxIndex]);

            // Rewrite the precursor's isolation window to point at the demuxed sub-window.
            var originalPrecursor = refSpectrum.Precursors[request.PrecursorIndex];
            var demuxPrecursor = ClonePrecursor(originalPrecursor);
            double lowMz = demuxIsoWindow.LowMz;
            double highMz = demuxIsoWindow.HighMz;
            double offsetMz = (highMz - lowMz) / 2.0;
            double targetMz = lowMz + offsetMz;
            var mzUnits = demuxPrecursor.IsolationWindow.CvParam(CVID.MS_isolation_window_target_m_z).Units;
            demuxPrecursor.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, targetMz, mzUnits);
            demuxPrecursor.IsolationWindow.Set(CVID.MS_isolation_window_lower_offset, offsetMz, mzUnits);
            demuxPrecursor.IsolationWindow.Set(CVID.MS_isolation_window_upper_offset, offsetMz, mzUnits);
            if (demuxPrecursor.SelectedIons.Count > 0)
            {
                demuxPrecursor.SelectedIons[0].Set(CVID.MS_selected_ion_m_z, targetMz, mzUnits);
                // Cpp zeroes the precursor intensity since it's invalidated by splitting; mirror.
                var intensityUnits = demuxPrecursor.SelectedIons[0].CvParam(CVID.MS_peak_intensity).Units;
                demuxPrecursor.SelectedIons[0].Set(CVID.MS_peak_intensity, 0, intensityUnits);
            }
            demuxed.Precursors.Add(demuxPrecursor);

            demuxed.Index = outputIndex;
            demuxed.Id = SpectrumIdentity(outputIndex).Id;

            // Rebuild m/z + intensity arrays from the demux solution.
            bool isProfile = refSpectrum.Params.HasCVParam(CVID.MS_profile_spectrum);
            var refMz = refSpectrum.GetMZArray()?.Data;
            var refInt = refSpectrum.GetIntensityArray()?.Data;
            if (refMz is null || refInt is null) return demuxed;

            // Sum the per-window contributions for every demux index this source spectrum exposes,
            // so we can rescale to preserve total intensity.
            var refDemuxIndices = _demux.SpectrumIndices;
            var summed = new double[solution.ColumnCount];
            foreach (var di in refDemuxIndices)
                for (int j = 0; j < solution.ColumnCount; j++)
                    summed[j] += solution[di, j];

            var rawSolutionIntensities = new double[solution.ColumnCount];
            int targetRow = refDemuxIndices[request.DemuxIndex];
            for (int j = 0; j < solution.ColumnCount; j++)
                rawSolutionIntensities[j] = solution[targetRow, j];

            var newMz = new List<double>();
            var newInt = new List<double>();
            int n = System.Math.Min(System.Math.Min(refMz.Count, refInt.Count), rawSolutionIntensities.Length);
            for (int i = 0; i < n; i++)
            {
                if (rawSolutionIntensities[i] <= 0 && !isProfile) continue;
                if (refInt[i] <= 0 && !isProfile) continue;

                newMz.Add(refMz[i]);
                if (!_params.VariableFill)
                {
                    double newIntensity = summed[i] > 0
                        ? refInt[i] * rawSolutionIntensities[i] / summed[i]
                        : 0.0;
                    newInt.Add(newIntensity);
                }
                else
                {
                    newInt.Add(rawSolutionIntensities[i]);
                }
            }

            demuxed.SetMZIntensityArrays(newMz, newInt, CVID.MS_number_of_detector_counts);
            demuxed.DefaultArrayLength = newMz.Count;
            return demuxed;
        }

        private static Spectrum CopySpectrum(Spectrum src)
        {
            // CVParam / UserParam are reference types and ParamContainer.Set() mutates them in
            // place — so every copy must duplicate the param objects, not just the references.
            var dst = new Spectrum
            {
                Index = src.Index,
                Id = src.Id,
                SpotId = src.SpotId,
                SourceFilePosition = src.SourceFilePosition,
                DefaultArrayLength = src.DefaultArrayLength,
                DataProcessing = src.DataProcessing,
                SourceFile = src.SourceFile,
            };
            CopyParams(src.Params, dst.Params);
            foreach (var pg in src.Params.ParamGroups) dst.Params.ParamGroups.Add(pg);
            foreach (var pre in src.Precursors) dst.Precursors.Add(ClonePrecursor(pre));
            foreach (var prod in src.Products) dst.Products.Add(prod);
            foreach (var arr in src.BinaryDataArrays)
            {
                var copy = new BinaryDataArray();
                CopyParams(arr, copy);
                copy.Data.AddRange(arr.Data);
                dst.BinaryDataArrays.Add(copy);
            }
            foreach (var scan in src.ScanList.Scans)
            {
                var copyScan = new Scan
                {
                    InstrumentConfiguration = scan.InstrumentConfiguration,
                    SourceFile = scan.SourceFile,
                };
                CopyParams(scan.Params, copyScan.Params);
                foreach (var sw in scan.ScanWindows) copyScan.ScanWindows.Add(sw);
                dst.ScanList.Scans.Add(copyScan);
            }
            return dst;
        }

        private static Precursor ClonePrecursor(Precursor src)
        {
            // CVParam is a reference type, and ParamContainer.Set() mutates the existing
            // CVParam's Value/Units fields in place — so we must DEEP-copy each CVParam,
            // otherwise rewriting the demuxed precursor's isolation window also mutates the
            // source's. (Caught by Demux_*Test_PerWindowSumsMatchOriginal tests.)
            var dst = new Precursor
            {
                SourceFile = src.SourceFile,
                ExternalSpectrumId = src.ExternalSpectrumId,
                SpectrumId = src.SpectrumId,
            };
            CopyParams(src.Params, dst.Params);
            CopyParams(src.IsolationWindow, dst.IsolationWindow);
            CopyParams(src.Activation, dst.Activation);
            foreach (var ion in src.SelectedIons)
            {
                var ionCopy = new SelectedIon();
                CopyParams(ion, ionCopy);
                dst.SelectedIons.Add(ionCopy);
            }
            return dst;
        }

        private static void CopyParams(ParamContainer src, ParamContainer dst)
        {
            foreach (var p in src.CVParams)
                dst.CVParams.Add(new CVParam(p.Cvid, p.Value, p.Units));
            foreach (var p in src.UserParams)
                dst.UserParams.Add(new UserParam(p.Name, p.Value, p.Type, p.Units));
        }
    }

    private readonly record struct DemuxRequestIndex(
        int MsLevel,
        int SpectrumOriginalIndex,
        int PrecursorIndex,
        int DemuxIndex);

    private sealed class DemuxIndexMapper
    {
        public List<DemuxRequestIndex> IndexMap { get; } = new();
        public List<SpectrumIdentity> SpectrumIdentities { get; } = new();

        public DemuxIndexMapper(ISpectrumList inner, IPrecursorMaskCodec pmc)
        {
            // Find the first / last non-removed demux windows for clamping isolation slices.
            var removed = pmc.DemuxWindowEdgesRemoved;
            int lowestMzWindow = 0;
            for (; lowestMzWindow < pmc.NumDemuxWindows; lowestMzWindow++)
                if (!removed.Contains(lowestMzWindow)) break;
            int highestMzWindow = pmc.NumDemuxWindows - 1;
            for (; highestMzWindow > 0; highestMzWindow--)
                if (!removed.Contains(highestMzWindow)) break;
            double lowestMz = pmc.GetIsolationWindow(lowestMzWindow).LowMz;
            double highestMz = pmc.GetIsolationWindow(highestMzWindow).HighMz;

            for (int i = 0; i < inner.Count; i++)
            {
                var spec = inner.GetSpectrum(i, getBinaryData: false);
                int msLevel = spec.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0);
                PushSpectrum(spec, msLevel, pmc.PrecursorsPerSpectrum, pmc.OverlapsPerCycle, lowestMz, highestMz);
            }
        }

        private void PushSpectrum(Spectrum spec, int msLevel, int numPrecursors, int numOverlap,
            double lowestMz, double highestMz)
        {
            int spectrumOriginalIndex = spec.Index;
            int numDemuxIndices = numPrecursors * numOverlap;
            if (msLevel != 2 || spec.Precursors.Count == 0) numDemuxIndices = 1;

            double isoLowMz = spec.Precursors.Count == 0 ? lowestMz : DemuxHelpers.PrecursorMzLow(spec.Precursors[0]);
            double isoHighMz = spec.Precursors.Count == 0 ? highestMz : DemuxHelpers.PrecursorMzHigh(spec.Precursors[0]);

            int demuxIndex = isoLowMz < lowestMz ? 1 : 0;
            if (isoHighMz > highestMz) numDemuxIndices--;

            for (; demuxIndex < numDemuxIndices; demuxIndex++)
            {
                int pIndex = numOverlap == 0 ? 0 : demuxIndex / numOverlap;
                IndexMap.Add(new DemuxRequestIndex(msLevel, spectrumOriginalIndex, pIndex, demuxIndex));

                var newId = new SpectrumIdentity
                {
                    Index = SpectrumIdentities.Count,
                    Id = InjectScanId(spec.Id, SpectrumIdentities.Count + 1, demuxIndex),
                    SpotId = spec.SpotId,
                    SourceFilePosition = spec.SourceFilePosition,
                };
                SpectrumIdentities.Add(newId);
            }
        }

        /// <summary>Rewrites a "scan=N otherKey=v" id by appending demux=N + originalScan=N tokens
        /// and updating the scan= value to the new index — mirrors cpp's <c>injectScanId</c>.</summary>
        internal static string InjectScanId(string id, int scanNumber, int demuxIndex)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var token in id.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = token.IndexOf('=');
                if (eq < 0) { sb.Append(token).Append(' '); continue; }
                string key = token[..eq];
                string val = token[(eq + 1)..];
                if (key == "scan")
                {
                    sb.Append("originalScan=").Append(val).Append(' ');
                    sb.Append("demux=").Append(demuxIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(' ');
                    sb.Append("scan=").Append(scanNumber.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(' ');
                }
                else
                {
                    sb.Append(token).Append(' ');
                }
            }
            return sb.ToString().TrimEnd();
        }
    }
}
