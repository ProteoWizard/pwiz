using System;
using System.Collections.Generic;
using HDF.PInvoke;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;

#pragma warning disable CA1806 // HDF5 close() ints
#pragma warning disable CS1591 // internal helpers

namespace Pwiz.Data.MsData.Mz5;

/// <summary>
/// <see cref="IChromatogramList"/> backed by an mz5 file. Each row of the
/// <c>ChromatogramMetaData</c> dataset becomes one chromatogram; time +
/// intensity arrays are sliced lazily from the global <c>ChromatogramTime</c>
/// + <c>ChromatogramIntensity</c> datasets via the per-chromatogram
/// end-offset recorded in <c>ChromatogramIndex</c>.
/// </summary>
/// <remarks>
/// Sibling of <see cref="Mz5SpectrumList"/>; same lazy-slice pattern.
/// Chromatograms don't carry the delta-encoding optimization that m/z arrays
/// do, so no translator is needed on the time array (cpp's Translator_mz5
/// only handles m/z + intensity, and only m/z is non-trivial).
/// </remarks>
public sealed class Mz5ChromatogramList : ChromatogramListBase
{
    private readonly Mz5Connection _conn;
    private readonly Mz5ReferenceRead _refs;
    private readonly ChromatogramMZ5[] _meta;
    private readonly BinaryDataMZ5[] _binaryParams;
    private readonly ulong[] _endOffsets;
    private readonly Dictionary<string, int> _idMap = new(StringComparer.Ordinal);
    private readonly ChromatogramIdentity[] _identities;

    internal Mz5ChromatogramList(Mz5Connection conn, Mz5ReferenceRead refs)
    {
        _conn = conn;
        _conn.AddRef();
        _refs = refs;

        if (!conn.Has(Mz5Datasets.ChromatogramMetaData))
        {
            _meta = Array.Empty<ChromatogramMZ5>();
            _binaryParams = Array.Empty<BinaryDataMZ5>();
            _endOffsets = Array.Empty<ulong>();
            _identities = Array.Empty<ChromatogramIdentity>();
            return;
        }

        long metaT = ChromatogramMZ5.CreateType();
        long binT = BinaryDataMZ5.CreateType();
        try
        {
            _meta = conn.ReadFull<ChromatogramMZ5>(Mz5Datasets.ChromatogramMetaData, metaT);
            _binaryParams = conn.Has(Mz5Datasets.ChromatogramBinaryMetaData)
                ? conn.ReadFull<BinaryDataMZ5>(Mz5Datasets.ChromatogramBinaryMetaData, binT)
                : Array.Empty<BinaryDataMZ5>();
            _endOffsets = conn.Has(Mz5Datasets.ChromatogramIndex)
                ? Mz5SpectrumList.ReadUlongIndex(conn, Mz5Datasets.ChromatogramIndex, _meta.Length)
                : new ulong[_meta.Length];

            _identities = new ChromatogramIdentity[_meta.Length];
            for (int i = 0; i < _meta.Length; i++)
            {
                _identities[i] = new ChromatogramIdentity
                {
                    Index = (int)_meta[i].Index,
                    Id = Mz5StringMarshal.FromVlen(_meta[i].Id),
                };
                if (!string.IsNullOrEmpty(_identities[i].Id))
                    _idMap[_identities[i].Id] = i;
            }
        }
        catch
        {
            if (_meta is { Length: > 0 }) conn.VlenReclaim(_meta, metaT);
            throw;
        }
        finally
        {
            H5T.close(binT);
            H5T.close(metaT);
        }
    }

    /// <inheritdoc/>
    public override int Count => _meta.Length;

    /// <inheritdoc/>
    public override ChromatogramIdentity ChromatogramIdentity(int index)
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
    public override Chromatogram GetChromatogram(int index, bool getBinaryData = false)
    {
        if ((uint)index >= (uint)_meta.Length)
            throw new ArgumentOutOfRangeException(nameof(index));

        var meta = _meta[index];
        var chrom = new Chromatogram
        {
            Index = (int)meta.Index,
            Id = _identities[index].Id,
        };
        _refs.FillParamContainerInternal(chrom.Params, meta.ParamList);

        ulong start = index == 0 ? 0UL : _endOffsets[index - 1];
        ulong end = _endOffsets[index];
        int len = checked((int)(end - start));
        chrom.DefaultArrayLength = len;

        if (getBinaryData && len > 0
            && _conn.Has(Mz5Datasets.ChromatogramTime)
            && _conn.Has(Mz5Datasets.ChromatogramIntensity))
        {
            double[] time = _conn.ReadDoubles(Mz5Datasets.ChromatogramTime, start, (ulong)len);
            double[] intensity = _conn.ReadDoubles(Mz5Datasets.ChromatogramIntensity, start, (ulong)len);
            // No translator step: cpp Translator_mz5 only special-cases m/z.
            var timeArr = new Pwiz.Data.MsData.Spectra.BinaryDataArray();
            timeArr.Params.Set(CVID.MS_time_array);
            timeArr.Data.AddRange(time);
            var intArr = new Pwiz.Data.MsData.Spectra.BinaryDataArray();
            intArr.Params.Set(CVID.MS_intensity_array);
            intArr.Data.AddRange(intensity);
            chrom.BinaryDataArrays.Add(timeArr);
            chrom.BinaryDataArrays.Add(intArr);

            if (index < _binaryParams.Length)
            {
                var bin = _binaryParams[index];
                _refs.FillParamContainerInternal(timeArr, bin.XParamList);
                _refs.FillParamContainerInternal(intArr, bin.YParamList);
            }
        }
        return chrom;
    }

    /// <inheritdoc/>
    protected override void DisposeCore()
    {
        if (_meta.Length > 0)
        {
            long metaT = ChromatogramMZ5.CreateType();
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
}
