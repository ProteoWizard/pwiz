using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Analysis;

/// <summary>
/// Reorders the spectra of an inner <see cref="ISpectrumList"/> using a key extracted from each
/// spectrum's identity metadata. Port of pwiz <c>SpectrumList_Sorter</c>.
/// </summary>
/// <remarks>
/// The sort is materialized eagerly: the constructor walks the inner list once, computes the
/// sort key for each entry (potentially fetching the spectrum at fast-metadata level), and
/// permutes the index. <c>GetSpectrum(index)</c> resolves through the permutation. The inner
/// list's <see cref="ISpectrumList.SpectrumIdentity(int)"/> is preserved per-spectrum (no copy);
/// only the order changes.
/// </remarks>
public sealed class SpectrumListSorter : SpectrumListWrapper
{
    private readonly int[] _permutation;

    /// <summary>Wraps <paramref name="inner"/>, sorting by the key extracted via <paramref name="keyOf"/>.</summary>
    /// <param name="inner">The list to wrap.</param>
    /// <param name="keyOf">
    /// Extracts the sort key for spectrum at the given index. Receives the inner list and an index;
    /// returns a comparable key.
    /// </param>
    public SpectrumListSorter(ISpectrumList inner, Func<ISpectrumList, int, IComparable> keyOf)
        : base(inner)
    {
        ArgumentNullException.ThrowIfNull(keyOf);
        int n = inner.Count;
        var keys = new IComparable[n];
        var indices = new int[n];
        for (int i = 0; i < n; i++)
        {
            keys[i] = keyOf(inner, i);
            indices[i] = i;
        }
        Array.Sort(indices, (a, b) => keys[a].CompareTo(keys[b]));
        _permutation = indices;
    }

    /// <inheritdoc/>
    public override SpectrumIdentity SpectrumIdentity(int index)
    {
        var orig = Inner.SpectrumIdentity(_permutation[index]);
        // Fix the visible Index to the new position so consumers that key off it
        // (e.g. mzML <spectrum index="...">) see the sorted ordering. Keep the
        // native id intact since that's the vendor-side identifier.
        return new ProxiedSpectrumIdentity
        {
            Index = index,
            Id = orig.Id,
            SpotId = orig.SpotId,
            SourceFilePosition = orig.SourceFilePosition,
        };
    }

    /// <inheritdoc/>
    public override Spectrum GetSpectrum(int index, bool getBinaryData = false)
    {
        var spec = Inner.GetSpectrum(_permutation[index], getBinaryData);
        spec.Index = index;
        return spec;
    }

    private sealed class ProxiedSpectrumIdentity : SpectrumIdentity { }

    /// <summary>Sort key: the first scan's <c>scan_start_time</c>. Mirrors cpp
    /// <c>SpectrumList_SorterPredicate_ScanStartTime</c>.</summary>
    public static IComparable ByScanStartTimeKey(ISpectrumList list, int index)
    {
        var spec = list.GetSpectrum(index, getBinaryData: false);
        if (spec.ScanList.Scans.Count == 0) return double.MaxValue;
        var time = spec.ScanList.Scans[0].CvParam(CVID.MS_scan_start_time);
        return time.IsEmpty ? double.MaxValue : time.TimeInSeconds();
    }
}
