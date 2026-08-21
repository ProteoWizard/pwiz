using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Spectra;

#pragma warning disable CA1707

namespace Pwiz.Vendor.Bruker;

/// <summary>
/// <see cref="ISpectrumList"/> backed by any Bruker <c>.d</c> directory — a thin wrapper over
/// <see cref="IBrukerData"/> that knows nothing about TDF vs TSF vs future formats. All
/// format-specific spectrum construction lives in the <see cref="IBrukerData"/> implementation.
/// </summary>
/// <remarks>
/// Port of pwiz::msdata::SpectrumList_Bruker. The vendor-agnostic split mirrors the C++ split
/// between <c>SpectrumList_Bruker</c> and <c>CompassData</c>.
/// </remarks>
public sealed class SpectrumList_Bruker : SpectrumListBase, IVendorCentroidingSpectrumList,
    IIonMobilitySpectrumList, IIonMobilityCcsConversion
{
    private readonly IBrukerData _data;
    private readonly IReadOnlyList<BrukerIndexEntry> _index;
    private readonly bool _owns;
    private readonly bool _combineIonMobilitySpectra;
    private readonly bool _passEntireDiaPasefFrame;
    private readonly bool _includeIsolationArrays;

    /// <summary>Which Bruker sub-format this list is backed by.</summary>
    public BrukerFormat Format => _data.Format;

    /// <summary>True iff the analysis stores ion-mobility data — cpp parity:
    /// <c>format_ == Reader_Bruker_Format_TDF</c>. Only the TDF (TIMS) format
    /// carries IM; BAF / TSF / Yep / Fid don't.</summary>
    public bool HasIonMobility => _data.Format == BrukerFormat.Tdf;

    /// <inheritdoc cref="IIonMobilitySpectrumList.IonMobilityUnits"/>
    /// <remarks>Bruker TIMS reports IM as inverse reduced ion mobility (1/K0) in V·s/cm².</remarks>
    public IonMobilityUnits IonMobilityUnits =>
        HasIonMobility ? IonMobilityUnits.InverseReducedIonMobilityVsecPerCm2 : IonMobilityUnits.None;

    /// <inheritdoc cref="IIonMobilitySpectrumList.HasCombinedIonMobility"/>
    /// <remarks>True iff IMS data is present AND <c>combineIonMobilitySpectra</c> was enabled
    /// (3-array per-frame mode).</remarks>
    public bool HasCombinedIonMobility => HasIonMobility && _combineIonMobilitySpectra;

    /// <inheritdoc/>
    public bool IsWatersSonar => false;

    /// <inheritdoc/>
    /// <remarks>TIMS CCS conversion is a closed-form expression in
    /// <see cref="TimsBinaryData.OneOverK0ToCcs"/>; it's always available once IM data is
    /// present (no separate calibration step like Waters .cal).</remarks>
    public bool CanConvertIonMobilityAndCcs => HasIonMobility;

    /// <inheritdoc/>
    /// <remarks>cpp signature is <c>(im, mz, charge)</c>; sharp's <see cref="TimsBinaryData.OneOverK0ToCcs"/>
    /// takes <c>(oneOverK0, charge, mz)</c> — reorder the args here so the public surface
    /// stays cpp-consistent.</remarks>
    public double IonMobilityToCcs(double ionMobility, double mz, int charge) =>
        TimsBinaryData.OneOverK0ToCcs(ionMobility, charge, mz);

    /// <inheritdoc/>
    public double CcsToIonMobility(double ccs, double mz, int charge) =>
        TimsBinaryData.CcsToOneOverK0(ccs, charge, mz);

    /// <inheritdoc/>
    public string VendorCentroidName => "Bruker/Agilent/CompassXtract peak picking";

    /// <inheritdoc/>
    public Spectrum GetCentroidSpectrum(int index, bool getBinaryData) =>
        BuildSpectrum(index, getBinaryData, preferCentroid: true);

    /// <summary>DataProcessing emitted as the <c>defaultDataProcessingRef</c>.</summary>
    public DataProcessing? Dp { get; set; }

    /// <inheritdoc/>
    public override DataProcessing? DataProcessing => Dp;

    private readonly bool _sortAndJitter;

    /// <summary>
    /// Creates a spectrum list over the given Bruker analysis handle. Index is built eagerly so
    /// the caller sees a stable <see cref="Count"/> immediately.
    /// </summary>
    public SpectrumList_Bruker(
        IBrukerData data,
        bool owns = true,
        bool combineIonMobilitySpectra = false,
        int preferOnlyMsLevel = 0,
        bool sortAndJitter = false,
        bool passEntireDiaPasefFrame = false,
        bool includeIsolationArrays = false)
    {
        ArgumentNullException.ThrowIfNull(data);
        _data = data;
        _owns = owns;
        _combineIonMobilitySpectra = combineIonMobilitySpectra;
        _sortAndJitter = sortAndJitter;
        _passEntireDiaPasefFrame = passEntireDiaPasefFrame;
        _includeIsolationArrays = includeIsolationArrays;
        _index = data.BuildSpectrumIndex(combineIonMobilitySpectra, preferOnlyMsLevel, _passEntireDiaPasefFrame);
    }

    /// <inheritdoc/>
    public override int Count => _index.Count;

    /// <inheritdoc/>
    public override SpectrumIdentity SpectrumIdentity(int index) => _index[index];

    /// <summary>
    /// Returns the MS level of the spectrum at <paramref name="index"/> without loading binary
    /// data. Used by <see cref="ChromatogramList_Bruker"/> to populate the per-point
    /// <c>ms level</c> integer-array on TIC/BPC chromatograms.
    /// </summary>
    public int GetMsLevelByIndex(int index) =>
        index >= 0 && index < _index.Count ? _index[index].MsLevel : 0;

    /// <inheritdoc/>
    public override Spectrum GetSpectrum(int index, bool getBinaryData = false) =>
        BuildSpectrum(index, getBinaryData, preferCentroid: false);

    private Spectrum BuildSpectrum(int index, bool getBinaryData, bool preferCentroid)
    {
        var entry = _index[index];
        var spec = new Spectrum { Index = index, Id = entry.Id };
        _data.FillSpectrum(spec, entry, getBinaryData, preferCentroid, _sortAndJitter, _includeIsolationArrays);
        return spec;
    }

    /// <inheritdoc/>
    protected override void DisposeCore()
    {
        if (_owns) _data.Dispose();
        base.DisposeCore();
    }
}
