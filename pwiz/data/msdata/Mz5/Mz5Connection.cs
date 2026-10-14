using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using HDF.PInvoke;

#pragma warning disable CA1806 // HDF5 close() ints — see MzMlbConnection.cs
#pragma warning disable CS1591 // missing XML doc on internals; struct-level docs cover the API

namespace Pwiz.Data.MsData.Mz5;

/// <summary>
/// Read-only HDF5 file wrapper for mz5 files. Port of
/// <c>pwiz::msdata::mz5::Connection_mz5</c> (read paths only).
/// </summary>
/// <remarks>
/// mz5 stores its data in ~25 named HDF5 datasets (see <see cref="Mz5Datasets"/>).
/// Each dataset holds either:
/// <list type="bullet">
///   <item>An array of one of the compound types from <see cref="Mz5Types"/>
///         (metadata tables: SourceFiles, Samples, Software, SpectrumMetaData,
///         CVParam, UserParam, etc.).</item>
///   <item>A flat array of native primitives (SpectrumMZ / SpectrumIntensity /
///         ChromatogramTime / ChromatogramIntensity are <c>double[]</c> in this
///         file's writer config; SpectrumIndex / ChromatogramIndex are
///         <c>ulong[]</c>).</item>
/// </list>
/// The reader-side flow:
/// <list type="number">
///   <item>Open the file, list which datasets are present.</item>
///   <item>Read each metadata table fully into a managed <c>T[]</c>.</item>
///   <item>For per-spectrum / per-chromatogram binary arrays, slice the global
///         m/z / intensity / time / intensity datasets via the index table.</item>
/// </list>
/// </remarks>
public sealed class Mz5Connection : IDisposable
{
    private long _fileId = -1L;
    private bool _disposed;
    // Ref-count of co-owners (Mz5SpectrumList + Mz5ChromatogramList). The reader
    // adapter initializes the count to 1 (its own hold); each list increments
    // in its ctor and decrements in DisposeCore. The connection actually closes
    // only when the count reaches 0. Lets either list be disposed in any order
    // without breaking the other's lazy reads.
    private int _refCount = 1;
    // Cached presence of each dataset, populated on open. mz5 files don't carry
    // every dataset (a chromatogram-only file omits SpectrumMetaData etc.); the
    // reader walks Run -> defined-datasets and skips missing branches.
    private readonly HashSet<Mz5Datasets> _present = new();

    /// <summary>True iff the writer applied delta-encoding to m/z arrays
    /// (FileInformation.deltaMZ flag). Reader must reverse-translate via
    /// running sum.</summary>
    public bool DeltaMz { get; private set; }

    /// <summary>True iff the writer applied log-translation to intensity
    /// arrays. Reader must reverse-translate (cpp Translator_mz5 leaves this
    /// unimplemented; we follow suit — store the flag for completeness).</summary>
    public bool TranslateIntensity { get; private set; }

    /// <summary>Open the file for reading. Throws if it isn't a valid mz5 file
    /// (HDF5 magic check + presence of the <c>FileInformation</c> dataset).</summary>
    public Mz5Connection(string filename)
    {
        if (!File.Exists(filename))
            throw new FileNotFoundException("mz5 file not found", filename);

        H5E.set_auto(H5E.DEFAULT, null, IntPtr.Zero);
        _fileId = H5F.open(filename, H5F.ACC_RDONLY);
        if (_fileId < 0)
            throw new IOException($"Could not open mz5 file: {filename}");

        try
        {
            ScanDatasets();
            if (!_present.Contains(Mz5Datasets.FileInformation))
                throw new IOException("File is HDF5 but missing FileInformation dataset; not an mz5 file.");
            ValidateVersion();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>True iff the dataset is present in this file. Used by the
    /// reference reader to skip optional branches (a chromatogram-only file
    /// has no SpectrumMetaData, etc.).</summary>
    public bool Has(Mz5Datasets ds) => _present.Contains(ds);

    /// <summary>Reads the complete contents of <paramref name="ds"/> as an
    /// array of <typeparamref name="T"/> records (one per row). The caller
    /// must supply the matching HDF5 compound type. The returned array's
    /// vlen-string IntPtr fields point at HDF5-allocated memory; call
    /// <see cref="VlenReclaim{T}"/> on the array before dropping references
    /// or HDF5 will leak.</summary>
    public T[] ReadFull<T>(Mz5Datasets ds, long compoundType) where T : unmanaged
    {
        EnsureOpen();
        string name = Mz5Configuration.DatasetName(ds);
        long dsId = H5D.open(_fileId, name);
        if (dsId < 0)
            throw new IOException($"Could not open dataset '{name}'");
        long spaceId = H5D.get_space(dsId);
        try
        {
            var dims = new ulong[1];
            H5S.get_simple_extent_dims(spaceId, dims, null);
            T[] buf = new T[dims[0]];
            if (dims[0] > 0)
            {
                unsafe
                {
                    fixed (T* p = buf)
                    {
                        if (H5D.read(dsId, compoundType, H5S.ALL, H5S.ALL,
                                     H5P.DEFAULT, (IntPtr)p) < 0)
                            throw new IOException($"H5D.read failed for '{name}'");
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

    /// <summary>Reads a 1-D range of native primitives from a dataset. Used
    /// for the binary array slices: pass <see cref="Mz5Datasets.SpectrumMZ"/>
    /// / <see cref="Mz5Datasets.SpectrumIntensity"/> /
    /// <see cref="Mz5Datasets.ChromatogramTime"/> /
    /// <see cref="Mz5Datasets.ChromatogramIntensity"/> + the element range
    /// from the matching index dataset. HDF5 promotes float datasets to
    /// double on the fly via the <c>H5T.NATIVE_DOUBLE</c> request type.</summary>
    public double[] ReadDoubles(Mz5Datasets ds, ulong start, ulong count)
    {
        EnsureOpen();
        if (count == 0) return Array.Empty<double>();
        string name = Mz5Configuration.DatasetName(ds);
        long dsId = H5D.open(_fileId, name);
        if (dsId < 0)
            throw new IOException($"Could not open dataset '{name}'");
        long spaceId = H5D.get_space(dsId);
        try
        {
            ulong[] startArr = { start };
            ulong[] countArr = { count };
            H5S.select_hyperslab(spaceId, H5S.seloper_t.SET, startArr, null, countArr, null);
            long mspace = H5S.create_simple(1, countArr, countArr);
            try
            {
                double[] buf = new double[count];
                unsafe
                {
                    fixed (double* p = buf)
                    {
                        if (H5D.read(dsId, H5T.NATIVE_DOUBLE, mspace, spaceId,
                                     H5P.DEFAULT, (IntPtr)p) < 0)
                            throw new IOException($"H5D.read range failed for '{name}'");
                    }
                }
                return buf;
            }
            finally
            {
                H5S.close(mspace);
            }
        }
        finally
        {
            H5S.close(spaceId);
            H5D.close(dsId);
        }
    }

    /// <summary>Reclaim HDF5-allocated memory hanging off vlen fields in
    /// <paramref name="records"/>. Must be called once you're done copying
    /// out the vlen strings / lists, before the array goes out of scope.</summary>
    public void VlenReclaim<T>(T[] records, long compoundType) where T : unmanaged
    {
        EnsureOpen();
        if (records.Length == 0) return;
        ulong[] dims = { (ulong)records.Length };
        long space = H5S.create_simple(1, dims, dims);
        try
        {
            unsafe
            {
                fixed (T* p = records)
                {
                    H5D.vlen_reclaim(compoundType, space, H5P.DEFAULT, (IntPtr)p);
                }
            }
        }
        finally
        {
            H5S.close(space);
        }
    }

    /// <summary>HDF5 file handle. Used by <see cref="Mz5SpectrumList"/> for
    /// per-row hyperslab reads on the SpectrumIndex / SpectrumMZ /
    /// SpectrumIntensity datasets without going through the typed Read*
    /// helpers (which assume "read all").</summary>
    internal long FileId => _fileId;

    /// <summary>Increment the share-count. Each lazy list (SpectrumList,
    /// ChromatogramList) calls this in its ctor so the connection survives
    /// until both lists are disposed.</summary>
    internal void AddRef() => System.Threading.Interlocked.Increment(ref _refCount);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        if (System.Threading.Interlocked.Decrement(ref _refCount) > 0) return;
        _disposed = true;
        if (_fileId >= 0) { H5F.close(_fileId); _fileId = -1L; }
    }

    private void EnsureOpen() => ObjectDisposedException.ThrowIf(_disposed, this);

    private void ScanDatasets()
    {
        foreach (Mz5Datasets ds in Enum.GetValues<Mz5Datasets>())
        {
            string name = Mz5Configuration.DatasetName(ds);
            if (H5L.exists(_fileId, name) > 0)
                _present.Add(ds);
        }
    }

    private void ValidateVersion()
    {
        long t = FileInformationMZ5.CreateType();
        try
        {
            var info = ReadFull<FileInformationMZ5>(Mz5Datasets.FileInformation, t);
            if (info.Length == 0)
                throw new IOException("FileInformation dataset is empty.");
            if (info[0].MajorVersion != Mz5Configuration.MajorVersion)
                throw new IOException(
                    $"Unsupported mz5 major version: {info[0].MajorVersion}.{info[0].MinorVersion}, " +
                    $"expected {Mz5Configuration.MajorVersion}.x");
            DeltaMz = info[0].DeltaMz != 0;
            TranslateIntensity = info[0].TranslateInten != 0;
        }
        finally
        {
            H5T.close(t);
        }
    }
}
