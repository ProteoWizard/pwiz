using System;

namespace Pwiz.Data.MsData.MzMlb;

/// <summary>
/// External source of binary array data referenced by mzML cvParams.
/// </summary>
/// <remarks>
/// In an mzMLb file the mzML XML's <c>&lt;binary&gt;</c> elements are empty;
/// the actual peak / intensity / time arrays live in separate HDF5 datasets
/// referenced by <c>MS_external_HDF5_dataset</c> +
/// <c>MS_external_offset</c> + <c>MS_external_array_length</c> cvParams.
/// <c>MzmlReader</c> takes an optional implementation of this interface
/// and routes those references through it; for plain mzML files, the source
/// is null and the reader falls back to inline base64.
/// <para>HDF5 performs type conversion at read time, so callers can request
/// the type they want (double / float / long) regardless of how the array is
/// stored on disk.</para>
/// </remarks>
public interface IExternalBinarySource
{
    /// <summary>Read <c>buf.Length</c> doubles from <paramref name="dataset"/>
    /// starting at element-index <paramref name="offset"/>. Returns the number
    /// of elements actually read (may be less than requested if the dataset
    /// is shorter than offset + buf.Length).</summary>
    int ReadDoubles(string dataset, long offset, Span<double> buf);

    /// <summary>Read <c>buf.Length</c> 64-bit integers from
    /// <paramref name="dataset"/> starting at element-index
    /// <paramref name="offset"/>. Returns the number of elements actually read.</summary>
    int ReadInt64(string dataset, long offset, Span<long> buf);

    /// <summary>Read <c>buf.Length</c> raw bytes from
    /// <paramref name="dataset"/> starting at byte-offset
    /// <paramref name="offset"/>. Used to pull a numpress-encoded opaque
    /// blob back from the HDF5 storage before handing it to the
    /// BinaryDataEncoder for decoding.</summary>
    int ReadBytes(string dataset, long offset, Span<byte> buf);
}

/// <summary>
/// External sink for binary array data referenced by mzML cvParams.
/// </summary>
/// <remarks>
/// Inverse of <see cref="IExternalBinarySource"/>: used by <c>MzmlWriter</c>
/// when serializing an mzMLb file. The writer hands raw <c>double</c> /
/// <c>long</c> arrays to the sink, which stores them in named HDF5 datasets
/// and returns the element-offset where each chunk landed. The writer then
/// emits the <c>MS_external_HDF5_dataset</c> + <c>MS_external_offset</c> +
/// <c>MS_external_array_length</c> cvParams (and an empty <c>&lt;binary/&gt;</c>
/// element) instead of base64-encoding the array inline.
/// <para>The cpp writer concatenates arrays by type (e.g. all m/z arrays from
/// all spectra into <c>spectrum_MS_1000514_double</c>) so the dataset names
/// double as a per-array-type partitioning scheme.</para>
/// </remarks>
public interface IExternalBinarySink
{
    /// <summary>Append <paramref name="data"/> to <paramref name="dataset"/>
    /// (creating the dataset on first reference). Returns the 0-based
    /// element-index where this chunk starts within the dataset.</summary>
    long AppendDoubles(string dataset, ReadOnlySpan<double> data);

    /// <summary>Append <paramref name="data"/> to <paramref name="dataset"/>
    /// as 32-bit floats. Used when the per-array precision config selects
    /// MS_32_bit_float for this array type — by convention the dataset name
    /// then carries a "_float" suffix. Returns the 0-based element-index
    /// where this chunk starts.</summary>
    long AppendFloats(string dataset, ReadOnlySpan<float> data);

    /// <summary>Append <paramref name="data"/> to <paramref name="dataset"/>.
    /// Returns the 0-based element-index where this chunk starts.</summary>
    long AppendInt64(string dataset, ReadOnlySpan<long> data);

    /// <summary>Append <paramref name="data"/> to <paramref name="dataset"/>
    /// as 32-bit integers. By convention the dataset name carries an
    /// "_int32" suffix. Returns the 0-based element-index where this chunk
    /// starts.</summary>
    long AppendInt32(string dataset, ReadOnlySpan<int> data);

    /// <summary>Append <paramref name="data"/> to <paramref name="dataset"/>
    /// as raw bytes (HDF5 NATIVE_UINT8 type). Used for numpress-encoded
    /// binary arrays where the on-disk representation is an opaque byte
    /// blob and decoding happens entirely in the BinaryDataEncoder layer.
    /// Returns the 0-based byte-offset where this chunk starts.</summary>
    long AppendBytes(string dataset, ReadOnlySpan<byte> data);
}
