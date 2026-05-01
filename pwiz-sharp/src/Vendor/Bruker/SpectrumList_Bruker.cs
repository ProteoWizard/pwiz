using Pwiz.Analysis;
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
public sealed class SpectrumList_Bruker : SpectrumListBase, IVendorCentroidingSpectrumList
{
    private readonly IBrukerData _data;
    private readonly IReadOnlyList<BrukerIndexEntry> _index;
    private readonly bool _owns;

    /// <summary>Which Bruker sub-format this list is backed by.</summary>
    public BrukerFormat Format => _data.Format;

    /// <inheritdoc/>
    public string VendorCentroidName => "Bruker/Agilent/CompassXtract peak picking";

    /// <inheritdoc/>
    public Spectrum GetCentroidSpectrum(int index, bool getBinaryData) =>
        BuildSpectrum(index, getBinaryData, preferCentroid: true);

    /// <summary>DataProcessing emitted as the <c>defaultDataProcessingRef</c>.</summary>
    public DataProcessing? Dp { get; set; }

    /// <inheritdoc/>
    public override DataProcessing? DataProcessing => Dp;

    /// <summary>
    /// Creates a spectrum list over the given Bruker analysis handle. Index is built eagerly so
    /// the caller sees a stable <see cref="Count"/> immediately.
    /// </summary>
    private readonly bool _sortAndJitter;

    public SpectrumList_Bruker(
        IBrukerData data,
        bool owns = true,
        bool combineIonMobilitySpectra = false,
        int preferOnlyMsLevel = 0,
        bool sortAndJitter = false)
    {
        ArgumentNullException.ThrowIfNull(data);
        _data = data;
        _owns = owns;
        _sortAndJitter = sortAndJitter;
        _index = data.BuildSpectrumIndex(combineIonMobilitySpectra, preferOnlyMsLevel);
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
        _data.FillSpectrum(spec, entry, getBinaryData, preferCentroid, _sortAndJitter);
        return spec;
    }

    /// <inheritdoc/>
    protected override void DisposeCore()
    {
        if (_owns) _data.Dispose();
        base.DisposeCore();
    }
}
