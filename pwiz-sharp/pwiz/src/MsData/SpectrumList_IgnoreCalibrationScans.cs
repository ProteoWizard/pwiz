using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Processing;

namespace Pwiz.Data.MsData.Spectra;

/// <summary>
/// Hides spectra labeled "calibration spectrum" (MS:1000928), which describe a calibration source
/// rather than the injected sample.
/// </summary>
/// <remarks>
/// <para>This is <see cref="Readers.ReaderConfig.IgnoreCalibrationScans"/> for formats that carry the
/// label rather than a vendor notion of a calibration function. Readers that know which scans are
/// calibration without reading them - SpectrumList_Waters by lockmass function, SpectrumList_UIMF by
/// frame type - leave them out of their index instead, which is cheaper; this exists for lists that
/// can only tell by looking, mzML above all.</para>
/// <para>Port of <c>pwiz::msdata::SpectrumList_IgnoreCalibrationScans</c>. It lives here rather than
/// beside the other wrappers in Pwiz.Analysis because the mzML, mzMLb and mz5 readers install it,
/// and those cannot reference that assembly - so it derives from <see cref="SpectrumListBase"/> and
/// holds its inner list itself.</para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
    Justification = "Matches cpp pwiz::msdata::SpectrumList_IgnoreCalibrationScans class name; pwiz-sharp convention preserves cpp class names verbatim.")]
public sealed class SpectrumList_IgnoreCalibrationScans : SpectrumListBase
{
    private readonly ISpectrumList _inner;
    private readonly int[] _indexMap; // our index -> inner index, ascending
    private readonly SpectrumIdentity[] _identities; // inner identities, renumbered to be contiguous

    /// <summary>
    /// Wraps <paramref name="inner"/> if <paramref name="msd"/> says it contains calibration spectra
    /// AND at least one spectrum turns out to carry the term; returns <paramref name="inner"/>
    /// untouched otherwise.
    /// </summary>
    /// <remarks>
    /// <para>Both conditions matter, for different reasons. The fileContent declaration keeps the
    /// scan off files with nothing to remove - nearly all of them - which is what makes this
    /// affordable for callers such as Skyline that set IgnoreCalibrationScans for everything they
    /// open. And returning <paramref name="inner"/> when the scan finds nothing keeps a wrapper that
    /// removed nothing from claiming, through <see cref="CalibrationSpectraAreOmitted"/>, that it
    /// did: a file may declare the term in fileContent and carry it on no spectrum at all (see
    /// ProteoWizard #4499), and a consumer told "already handled" would then stand down over spectra
    /// that are all still present.</para>
    /// <para>A file whose fileContent does not admit to its calibration spectra keeps them.</para>
    /// <para>cpp resolves the cheapest detail level that populates the term through
    /// <c>min_level_accepted</c> before scanning. pwiz-sharp has no such probe, and needs none for the
    /// lists this is installed on: mzML, mzMLb and mz5 serve every level below
    /// <see cref="DetailLevel.FullData"/> as the same metadata-only read, which carries the
    /// spectrum's cvParams and decodes no arrays.</para>
    /// </remarks>
    public static ISpectrumList? Create(ISpectrumList? inner, MSData msd)
    {
        ArgumentNullException.ThrowIfNull(msd);
        if (inner is null)
            return inner;
        if (!msd.FileDescription.FileContent.HasCVParam(CVID.MS_calibration_spectrum))
            return inner;

        var indexMap = new List<int>(inner.Count);
        for (int i = 0; i < inner.Count; i++)
        {
            if (!IsCalibrationSpectrum(inner, i))
                indexMap.Add(i);
        }

        // Nothing carried the term - the fileContent declaration was false, or no spectrum could be
        // read. Either way hand back the list we were given, so CalibrationSpectraAreOmitted cannot
        // claim to have removed something, and behavior is what it was without this wrapper.
        if (indexMap.Count == inner.Count)
            return inner;

        return new SpectrumList_IgnoreCalibrationScans(inner, indexMap.ToArray());
    }

    /// <summary>
    /// Applies <see cref="Create"/> to <paramref name="msd"/>'s spectrum list, and drops the
    /// fileContent declaration along with the spectra.
    /// </summary>
    /// <remarks>
    /// Reconciling fileContent is not tidiness. It is what the declaration means: msconvert writes it
    /// back out verbatim, so leaving it would emit a file advertising calibration spectra it does not
    /// contain - and since that declaration is exactly what decides whether a file is worth scanning,
    /// every later read of the output would pay a full pass to find nothing. Port of cpp's
    /// <c>applyIgnoreCalibrationScans</c> in DefaultReaderList.cpp.
    /// </remarks>
    public static void Apply(MSData msd)
    {
        ArgumentNullException.ThrowIfNull(msd);
        var inner = msd.Run.SpectrumList;
        var filtered = Create(inner, msd);
        if (ReferenceEquals(filtered, inner))
            return; // Nothing was hidden, so the declaration is either absent or still true

        msd.Run.SpectrumList = filtered;
        msd.FileDescription.FileContent.CVParams.RemoveAll(p => p.Cvid == CVID.MS_calibration_spectrum);
    }

    private SpectrumList_IgnoreCalibrationScans(ISpectrumList inner, int[] indexMap)
    {
        _inner = inner;
        _indexMap = indexMap;
        _identities = new SpectrumIdentity[indexMap.Length];
        for (int i = 0; i < indexMap.Length; i++)
        {
            // Copy rather than renumber in place: SpectrumIdentity is a reference type, and the
            // inner list's instance is still the inner list's.
            var orig = inner.SpectrumIdentity(indexMap[i]);
            _identities[i] = new SpectrumIdentity
            {
                Index = i,
                Id = orig.Id,
                SpotId = orig.SpotId,
                SourceFilePosition = orig.SourceFilePosition,
            };
        }
    }

    /// <inheritdoc/>
    public override int Count => _indexMap.Length;

    /// <inheritdoc/>
    public override DataProcessing? DataProcessing => _inner.DataProcessing;

    /// <summary>
    /// True because this list removed something - <see cref="Create"/> does not build one otherwise.
    /// </summary>
    public override bool CalibrationSpectraAreOmitted => true;

    /// <inheritdoc/>
    public override SpectrumIdentity SpectrumIdentity(int index)
    {
        if ((uint)index >= (uint)_identities.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        return _identities[index];
    }

    /// <inheritdoc/>
    public override Spectrum GetSpectrum(int index, bool getBinaryData = false) =>
        GetSpectrum(index, getBinaryData ? DetailLevel.FullData : DetailLevel.FullMetadata);

    /// <inheritdoc/>
    public override Spectrum GetSpectrum(int index, DetailLevel detailLevel)
    {
        if ((uint)index >= (uint)_indexMap.Length)
            throw new ArgumentOutOfRangeException(nameof(index));

        // Copy before renumbering. Some lists hand back their stored instance - SpectrumListSimple
        // does - so assigning Index in place would renumber the inner list's spectrum, not ours.
        var spectrum = _inner.GetSpectrum(_indexMap[index], detailLevel).ShallowCopy();
        spectrum.Index = index; // The inner list numbered this one counting the spectra we hide
        return spectrum;
    }

    /// <summary>
    /// Remapped through the inner list, which usually keeps an id hash; the base implementation
    /// would fall back to a linear scan.
    /// </summary>
    public override int Find(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return FromInnerIndex(_inner.Find(id));
    }

    /// <inheritdoc/>
    public override int FindAbbreviated(string abbreviatedId, char delimiter = '.')
    {
        ArgumentNullException.ThrowIfNull(abbreviatedId);
        return FromInnerIndex(_inner.FindAbbreviated(abbreviatedId, delimiter));
    }

    /// <summary>Releases the inner list, which this wrapper owns.</summary>
    protected override void DisposeCore()
    {
        _inner.Dispose();
        base.DisposeCore();
    }

    private static bool IsCalibrationSpectrum(ISpectrumList inner, int index)
    {
        try
        {
            return inner.GetSpectrum(index, DetailLevel.FullMetadata)?.HasCVParam(CVID.MS_calibration_spectrum) ?? false;
        }
        catch (Exception)
        {
            // A spectrum that cannot be read cannot be identified as calibration data, and refusing
            // to open the file over it would be a worse answer than the one the caller asked for.
            // Keep it and let whatever reads it next report the problem.
            return false;
        }
    }

    /// <summary>Inner index to our index, or <see cref="Count"/> if the inner list did not
    /// recognize it or it is one of the spectra being hidden.</summary>
    private int FromInnerIndex(int innerIndex)
    {
        if (innerIndex < 0 || innerIndex >= _inner.Count)
            return Count;
        int position = Array.BinarySearch(_indexMap, innerIndex);
        return position >= 0 ? position : Count;
    }
}
