using System;
using System.Collections.Generic;
using HDF.PInvoke;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;

#pragma warning disable CA1806 // HDF5 close() ints
#pragma warning disable CS1591 // internal helpers

namespace Pwiz.Data.MsData.Mz5;

/// <summary>
/// <see cref="ISpectrumList"/> backed by an mz5 file. Each row of the
/// <c>SpectrumMetaData</c> dataset becomes one spectrum; m/z + intensity
/// arrays are sliced lazily from the global <c>SpectrumMZ</c> +
/// <c>SpectrumIntensity</c> datasets via the per-spectrum end-offset
/// recorded in <c>SpectrumIndex</c>.
/// </summary>
/// <remarks>
/// Port of pwiz cpp's <c>SpectrumList_mz5</c>. Three datasets get loaded
/// eagerly at construction (metadata + binary-array params + offset index);
/// the m/z + intensity values are sliced on demand inside
/// <see cref="GetSpectrum(int, bool)"/>. Nested scan / precursor / product
/// lists ride along on each <c>SpectrumMZ5</c> row as HDF5 vlen pointers —
/// we walk them to populate the standard pwiz-sharp <see cref="Spectrum"/>
/// fields. Param-list slicing reuses <see cref="Mz5ReferenceRead"/>'s
/// param-table state via an internal accessor.
/// </remarks>
public sealed class Mz5SpectrumList : SpectrumListBase
{
    private readonly Mz5Connection _conn;
    private readonly Mz5ReferenceRead _refs;
    private readonly SpectrumMZ5[] _meta;
    private readonly BinaryDataMZ5[] _binaryParams;
    private readonly ulong[] _spectrumEndOffsets;
    private readonly Dictionary<string, int> _idMap = new(StringComparer.Ordinal);

    // Per-row decoded id / spotId (vlen-string-converted once at construction
    // so SpectrumIdentity stays cheap; reclaim-after-construction releases
    // the HDF5-allocated string memory).
    private readonly SpectrumIdentity[] _identities;

    internal Mz5SpectrumList(Mz5Connection conn, Mz5ReferenceRead refs)
    {
        _conn = conn;
        _conn.AddRef();
        _refs = refs;

        if (!conn.Has(Mz5Datasets.SpectrumMetaData))
        {
            _meta = Array.Empty<SpectrumMZ5>();
            _binaryParams = Array.Empty<BinaryDataMZ5>();
            _spectrumEndOffsets = Array.Empty<ulong>();
            _identities = Array.Empty<SpectrumIdentity>();
            return;
        }

        long metaT = SpectrumMZ5.CreateType();
        long binT = BinaryDataMZ5.CreateType();
        try
        {
            _meta = conn.ReadFull<SpectrumMZ5>(Mz5Datasets.SpectrumMetaData, metaT);
            _binaryParams = conn.Has(Mz5Datasets.SpectrumBinaryMetaData)
                ? conn.ReadFull<BinaryDataMZ5>(Mz5Datasets.SpectrumBinaryMetaData, binT)
                : Array.Empty<BinaryDataMZ5>();

            // SpectrumIndex: one offset per spectrum (end-exclusive into
            // SpectrumMZ + SpectrumIntensity). The on-disk type is the
            // writer's native unsigned long — 4 bytes on Windows, 8 on
            // Linux — so we ask HDF5 for fixed 64-bit on read and let it
            // widen for us. (cpp does a manual 32-bit-wraparound fixup;
            // HDF5's type-conversion is the portable equivalent.)
            _spectrumEndOffsets = conn.Has(Mz5Datasets.SpectrumIndex)
                ? ReadIndex(conn, Mz5Datasets.SpectrumIndex, _meta.Length)
                : new ulong[_meta.Length];

            // Build identities + id→index map.
            _identities = new SpectrumIdentity[_meta.Length];
            for (int i = 0; i < _meta.Length; i++)
            {
                _identities[i] = new SpectrumIdentity
                {
                    Index = (int)_meta[i].Index,
                    Id = Mz5StringMarshal.FromVlen(_meta[i].Id),
                    SpotId = Mz5StringMarshal.FromVlen(_meta[i].SpotID),
                };
                if (!string.IsNullOrEmpty(_identities[i].Id))
                    _idMap[_identities[i].Id] = i;
            }
        }
        catch
        {
            // Reclaim anything we read before the throw.
            if (_meta is not null && _meta.Length > 0) conn.VlenReclaim(_meta, metaT);
            throw;
        }
        finally
        {
            H5T.close(binT);
            H5T.close(metaT);
        }
        // NOTE: we keep _meta + _binaryParams alive (vlen pointers and all)
        // for the lifetime of this list. Dispose reclaims them.
    }

    /// <inheritdoc/>
    public override int Count => _meta.Length;

    /// <inheritdoc/>
    public override SpectrumIdentity SpectrumIdentity(int index)
    {
        if ((uint)index >= (uint)_identities.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        return _identities[index];
    }

    /// <inheritdoc/>
    public override int Find(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return _idMap.TryGetValue(id, out int i) ? i : Count;
    }

    /// <inheritdoc/>
    public override Spectrum GetSpectrum(int index, bool getBinaryData = false)
    {
        if ((uint)index >= (uint)_meta.Length)
            throw new ArgumentOutOfRangeException(nameof(index));

        var meta = _meta[index];
        var spec = new Spectrum
        {
            Index = (int)meta.Index,
            Id = _identities[index].Id,
            SpotId = _identities[index].SpotId,
        };
        _refs.FillParamContainerInternal(spec.Params, meta.ParamList);

        // Unpack scanList (params + nested Scans + each Scan's params + scanWindowList).
        // Required for retentionTime and ion-mobility — cpp parity with Datastructures_mz5.cpp.
        _refs.FillParamContainerInternal(spec.ScanList, meta.ScanList.ParamList);
        FillScans(spec, meta.ScanList.ScanList);

        // Unpack precursor list (params + activation + isolationWindow + selectedIons[]).
        // Required for precursor m/z and charge — cpp PrecursorMZ5::fillPrecursor.
        FillPrecursors(spec, meta.PrecursorList);

        // Slice m/z + intensity from the global arrays.
        ulong start = index == 0 ? 0UL : _spectrumEndOffsets[index - 1];
        ulong end = _spectrumEndOffsets[index];
        int arrayLen = checked((int)(end - start));
        spec.DefaultArrayLength = arrayLen;

        if (getBinaryData && arrayLen > 0
            && _conn.Has(Mz5Datasets.SpectrumMZ)
            && _conn.Has(Mz5Datasets.SpectrumIntensity))
        {
            double[] mz = _conn.ReadDoubles(Mz5Datasets.SpectrumMZ, start, (ulong)arrayLen);
            double[] intensity = _conn.ReadDoubles(Mz5Datasets.SpectrumIntensity, start, (ulong)arrayLen);
            // Reverse delta-encoding if the writer used it. cpp's Translator_mz5
            // does the same on read: each stored mz[i] is the delta from the
            // running sum of all previous mz; the cumulative-sum walk recovers
            // the original m/z values.
            if (_conn.DeltaMz)
            {
                double running = 0;
                for (int i = 0; i < mz.Length; i++) { mz[i] += running; running = mz[i]; }
            }
            spec.SetMZIntensityArrays(mz, intensity, CVID.MS_number_of_detector_counts);

            // If we have per-spectrum binary-array param lists, copy their cv
            // params onto the freshly-built m/z + intensity BinaryDataArrays
            // (encoding precision / compression / array-type, etc.).
            if (index < _binaryParams.Length)
            {
                var bin = _binaryParams[index];
                if (spec.BinaryDataArrays.Count >= 2)
                {
                    _refs.FillParamContainerInternal(spec.BinaryDataArrays[0], bin.XParamList);
                    _refs.FillParamContainerInternal(spec.BinaryDataArrays[1], bin.YParamList);
                }
            }
        }
        return spec;
    }

    /// <inheritdoc/>
    protected override void DisposeCore()
    {
        // Reclaim the HDF5-allocated vlen strings hanging off the metadata
        // we cached at construction.
        if (_meta.Length > 0)
        {
            long metaT = SpectrumMZ5.CreateType();
            try { _conn.VlenReclaim(_meta, metaT); }
            finally { H5T.close(metaT); }
        }
        if (_binaryParams.Length > 0)
        {
            long binT = BinaryDataMZ5.CreateType();
            try { _conn.VlenReclaim(_binaryParams, binT); }
            finally { H5T.close(binT); }
        }
        _conn.Dispose();
    }

    /// <summary>Walk an Hvl pointing at a typed array of POD structs.
    /// Marshals each row to a managed struct via <see cref="System.Runtime.InteropServices.Marshal.PtrToStructure{T}(IntPtr)"/>.</summary>
    private static T[] ReadHvlArray<T>(Hvl hvl) where T : struct
    {
        ulong n = (ulong)hvl.Length;
        if (n == 0 || hvl.Data == IntPtr.Zero) return Array.Empty<T>();
        var rows = new T[n];
        int eltSize = System.Runtime.InteropServices.Marshal.SizeOf<T>();
        unsafe
        {
            byte* src = (byte*)hvl.Data;
            for (ulong i = 0; i < n; i++)
            {
                rows[i] = System.Runtime.InteropServices.Marshal.PtrToStructure<T>(
                    (IntPtr)(src + (long)i * eltSize));
            }
        }
        return rows;
    }

    /// <summary>Populate <see cref="Spectrum.ScanList"/>'s nested
    /// <see cref="Pwiz.Data.MsData.Spectra.Scan"/> entries from an mz5 <c>ScansMZ5.scanList</c> Hvl.
    /// cpp parity: ScansMZ5::fill + ScanMZ5::fillScan.</summary>
    private void FillScans(Spectrum spec, Hvl scansHvl)
    {
        var rows = ReadHvlArray<ScanMZ5>(scansHvl);
        foreach (var row in rows)
        {
            var scan = new Pwiz.Data.MsData.Spectra.Scan();
            _refs.FillParamContainerInternal(scan, row.ParamList);

            // Each ScanWindowList entry is itself a ParamListMZ5 inside a ParamListsMZ5 vlen.
            var windowRows = ReadHvlArray<ParamListMZ5>(row.ScanWindowList);
            foreach (var w in windowRows)
            {
                var sw = new Pwiz.Data.MsData.Spectra.ScanWindow();
                _refs.FillParamContainerInternal(sw, w);
                scan.ScanWindows.Add(sw);
            }
            spec.ScanList.Scans.Add(scan);
        }
    }

    /// <summary>Populate <see cref="Spectrum.Precursors"/> from an mz5
    /// <c>SpectrumMZ5.precursorList</c> Hvl. cpp parity: PrecursorMZ5::fillPrecursor.</summary>
    private void FillPrecursors(Spectrum spec, Hvl precursorsHvl)
    {
        var rows = ReadHvlArray<PrecursorMZ5>(precursorsHvl);
        foreach (var row in rows)
        {
            var p = new Pwiz.Data.MsData.Spectra.Precursor();
            _refs.FillParamContainerInternal(p, row.ParamList);
            _refs.FillParamContainerInternal(p.Activation, row.Activation);
            _refs.FillParamContainerInternal(p.IsolationWindow, row.IsolationWindow);

            var ionRows = ReadHvlArray<ParamListMZ5>(row.SelectedIonList);
            foreach (var ion in ionRows)
            {
                var si = new Pwiz.Data.MsData.Spectra.SelectedIon();
                _refs.FillParamContainerInternal(si, ion);
                p.SelectedIons.Add(si);
            }
            spec.Precursors.Add(p);
        }
    }

    private static ulong[] ReadIndex(Mz5Connection conn, Mz5Datasets ds, int expectedCount)
        => ReadUlongIndex(conn, ds, expectedCount);

    /// <summary>Shared cumulative-offset-index reader for both SpectrumIndex
    /// and ChromatogramIndex. Both datasets are typed as the writer's native
    /// unsigned long (4 bytes on Win, 8 on Linux); we request NATIVE_UINT64
    /// so HDF5 widens 32-bit stored values transparently. Internal so
    /// <see cref="Mz5ChromatogramList"/> can reuse it.</summary>
    internal static ulong[] ReadUlongIndex(Mz5Connection conn, Mz5Datasets ds, int expectedCount)
    {
        string name = Mz5Configuration.DatasetName(ds);
        long dsId = H5D.open(conn.FileId, name);
        if (dsId < 0)
            throw new System.IO.IOException($"Could not open dataset '{name}'");
        long spaceId = H5D.get_space(dsId);
        try
        {
            var dims = new ulong[1];
            H5S.get_simple_extent_dims(spaceId, dims, null);
            var buf = new ulong[Math.Max((int)dims[0], expectedCount)];
            if (dims[0] > 0)
            {
                unsafe
                {
                    fixed (ulong* p = buf)
                    {
                        if (H5D.read(dsId, H5T.NATIVE_UINT64, H5S.ALL, H5S.ALL,
                                     H5P.DEFAULT, (IntPtr)p) < 0)
                            throw new System.IO.IOException($"H5D.read failed for '{name}'");
                    }
                }
            }
            return buf;
        }
        finally
        {
            H5S.close(spaceId);
            H5D.close(dsId);
        }
    }
}
