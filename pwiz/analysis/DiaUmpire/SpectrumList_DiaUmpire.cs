using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Instruments;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Misc;

namespace Pwiz.Analysis.DiaUmpire;

/// <summary>
/// <see cref="ISpectrumList"/> that materializes DIA-Umpire pseudo-MS/MS spectra
/// from the wrapped <see cref="DiaUmpire"/> run. Each output spectrum at index
/// <c>i</c> corresponds to <c>DiaUmpire.PseudoMsMsKeys[i]</c> — the spill file
/// referenced by that key is consulted to actually pull the spectrum's params /
/// peaks / scan list.
/// </summary>
/// <remarks>
/// <para>Port of cpp <c>SpectrumList_DiaUmpire</c> (<c>SpectrumList_DiaUmpire.{hpp,cpp}</c>).
/// cpp uses an MRU cache of <c>MSDataFile</c> instances keyed by the spill file
/// pointer; pwiz-sharp does true per-spectrum random-access reads from a compact
/// custom binary spill format, with no MSData materialization. Each
/// <see cref="GetSpectrum"/> seeks to the requested record, reconstructs the
/// static spectrum scaffolding (ms_level, MSn, centroid, scan list, precursor)
/// from the persisted record plus the source MSData's instrument configuration,
/// and returns the new <see cref="Spectrum"/>. The id + index are overwritten
/// from the <see cref="PseudoMsMsKey"/> so callers see the DiaUmpire numbering,
/// not the per-window spill numbering. Dispose this wrapper (or the underlying
/// <see cref="DiaUmpire"/>) to delete the per-window spill temp files.</para>
///
/// <para>The DIA-Umpire algorithm is run lazily on first access (Count,
/// GetSpectrum, etc.) rather than in the constructor. This lets the construction
/// stack (SpectrumListFactory.Wrap, ParseDiaUmpire, our ctor, …) unwind first
/// so the input <see cref="ISpectrumList"/> is held only by us — then once we've
/// finished consuming it inside DIA-Umpire's internal Run, our reference can
/// be cleared and the input freed. Constructor-time eager run would keep the
/// input pinned via stack roots in every frame above us.</para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
    Justification = "Matches cpp pwiz::analysis::SpectrumList_DiaUmpire class name; pwiz-sharp convention preserves cpp filter class names verbatim")]
public sealed class SpectrumList_DiaUmpire : SpectrumListBase
{
    private readonly DataProcessing _dp;
    private readonly InstrumentConfiguration? _defaultInstrumentConfig;

    // Lazy-Run state. _pendingInner / _pendingMsd / _pendingConfig / _pendingIlr hold
    // construction args until EnsureRun is called for the first time, at which point
    // we hand them to DiaUmpire and immediately null them so the GC can reclaim the
    // input SpectrumList once DiaUmpire's internal Sl reference is cleared mid-Run.
    private DiaUmpire? _dia;
    private ISpectrumList? _pendingInner;
    private MSData? _pendingMsd;
    private Config? _pendingConfig;
    private IterationListenerRegistry? _pendingIlr;
    private readonly object _runLock = new();

    /// <summary>
    /// Wraps an inner <see cref="ISpectrumList"/> in a DIA-Umpire run and exposes
    /// the resulting pseudo-MS/MS spectra. The DIA-Umpire algorithm itself is run
    /// lazily on first access (see remarks); only the <see cref="DataProcessing"/>
    /// chain is built eagerly here.
    /// </summary>
    public SpectrumList_DiaUmpire(MSData msd, ISpectrumList inner, Config config,
                                  IterationListenerRegistry? ilr = null)
    {
        System.ArgumentNullException.ThrowIfNull(msd);
        System.ArgumentNullException.ThrowIfNull(inner);
        System.ArgumentNullException.ThrowIfNull(config);

        _defaultInstrumentConfig = msd.InstrumentConfigurations.Count > 0
            ? msd.InstrumentConfigurations[0] : null;
        _pendingMsd = msd;
        _pendingInner = inner;
        _pendingConfig = config;
        _pendingIlr = ilr;

        // Build the DataProcessing chain eagerly — SpectrumListFactory.Wrap reads
        // .DataProcessing right after our ctor returns to promote new methods to
        // msd.DataProcessings. cpp copies the inner DP and appends a method
        // describing the DIA-Umpire run + parameter map; we do the same,
        // attributing the new method to the first existing software entry (cpp
        // uses inner's processingMethods[0].softwarePtr).
        _dp = inner.DataProcessing is null
            ? new DataProcessing("pwiz_Reader_DiaUmpire_processing")
            : new DataProcessing(inner.DataProcessing.Id);
        if (inner.DataProcessing is not null)
            foreach (var m in inner.DataProcessing.ProcessingMethods)
                _dp.ProcessingMethods.Add(m);

        var method = new ProcessingMethod
        {
            Order = _dp.ProcessingMethods.Count,
            Software = _dp.ProcessingMethods.Count == 0
                ? null
                : _dp.ProcessingMethods[0].Software,
        };
        method.UserParams.Add(new UserParam(
            "Pseudo-spectra generated by DIA-Umpire demultiplexing"));
        foreach (var kvp in config.InstrumentParameters.GetParameterMap())
            method.UserParams.Add(new UserParam(kvp.Key, kvp.Value));
        if (config.DiaTargetWindowScheme == TargetWindowScheme.SwathVariable)
        {
            var windowStr = string.Join(" ",
                config.DiaVariableWindows.Select(w => $"{w.MzRange.Begin}-{w.MzRange.End}"));
            method.UserParams.Add(new UserParam("VariableWindows", windowStr));
        }
        _dp.ProcessingMethods.Add(method);
    }

    private DiaUmpire EnsureRun()
    {
        if (_dia is not null) return _dia;
        lock (_runLock)
        {
            if (_dia is not null) return _dia;
            // Snap parameters into locals, null the fields BEFORE running so the
            // wrapper itself no longer holds the input. DiaUmpire's Impl.Sl will
            // be the sole reference to the input until DIA-Umpire releases it
            // mid-Run after both ScanCollections are built.
            var msd = _pendingMsd!;
            var inner = _pendingInner!;
            var config = _pendingConfig!;
            var ilr = _pendingIlr;
            _pendingMsd = null;
            _pendingInner = null;
            _pendingConfig = null;
            _pendingIlr = null;
            var dia = new DiaUmpire(msd, inner, config, ilr);
            _dia = dia;
            return dia;
        }
    }

    /// <inheritdoc/>
    public override int Count => EnsureRun().PseudoMsMsKeys.Count;

    /// <inheritdoc/>
    public override DataProcessing? DataProcessing => _dp;

    /// <summary>Underlying DIA-Umpire run. Triggers lazy execution if not already run.</summary>
    public DiaUmpire DiaUmpire => EnsureRun();

    /// <inheritdoc/>
    public override SpectrumIdentity SpectrumIdentity(int index)
    {
        var dia = EnsureRun();
        if ((uint)index >= (uint)dia.PseudoMsMsKeys.Count)
            throw new System.ArgumentOutOfRangeException(nameof(index));
        return dia.PseudoMsMsKeys[index];
    }

    /// <inheritdoc/>
    public override int Find(string id)
    {
        System.ArgumentNullException.ThrowIfNull(id);
        var dia = EnsureRun();
        for (int i = 0; i < dia.PseudoMsMsKeys.Count; i++)
            if (dia.PseudoMsMsKeys[i].Id == id) return i;
        return dia.PseudoMsMsKeys.Count;
    }

    /// <inheritdoc/>
    public override Spectrum GetSpectrum(int index, bool getBinaryData = false)
    {
        var dia = EnsureRun();
        if ((uint)index >= (uint)dia.PseudoMsMsKeys.Count)
            throw new System.ArgumentOutOfRangeException(nameof(index));

        var key = dia.PseudoMsMsKeys[index];
        var spill = (SpillFile)key.SpillFileToken!;
        var rec = spill.ReadRecord(key.SpillFileIndex, getBinaryData);

        // Reconstruct the spectrum scaffolding from the persisted record. Static
        // CV params + per-spectrum fields recreate exactly what the in-memory
        // version produced — keep this in sync with DiaUmpire.Impl's pseudo-MS/MS
        // emission loop (it builds the same shape, then we tear it down to a
        // record at end-of-window and rebuild it here at read time).
        var s = new Spectrum
        {
            Id = key.Id,
            Index = key.Index,
        };
        s.Params.Set(CVID.MS_ms_level, 2);
        s.Params.Set(CVID.MS_MSn_spectrum);
        s.Params.Set(CVID.MS_centroid_spectrum);
        s.Params.UserParams.Add(new UserParam(
            "DIA-Umpire quality level",
            rec.QualityLevel.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "xsd:positiveInteger"));

        var scan = new Scan();
        scan.Set(CVID.MS_scan_start_time, rec.ScanRtMinutes, CVID.UO_minute);
        if (_defaultInstrumentConfig is not null)
            scan.InstrumentConfiguration = _defaultInstrumentConfig;
        s.ScanList.Scans.Add(scan);

        s.Precursors.Add(new Precursor(rec.TargetMz, rec.PrecursorIntensity, rec.Charge,
                                       CVID.MS_number_of_detector_counts));

        if (getBinaryData)
            s.SetMZIntensityArrays(rec.MzArray, rec.IntensityArray, CVID.MS_number_of_detector_counts);

        return s;
    }

    /// <summary>Disposes the underlying <see cref="DiaUmpire"/> (if it has been
    /// started), deleting all per-window spill temp files. Otherwise drops the
    /// captured construction args without ever running.</summary>
    protected override void DisposeCore()
    {
        lock (_runLock)
        {
            _dia?.Dispose();
            _pendingMsd = null;
            _pendingInner = null;
            _pendingConfig = null;
            _pendingIlr = null;
        }
    }
}
