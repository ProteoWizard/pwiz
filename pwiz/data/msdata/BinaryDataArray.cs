using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData.Processing;

namespace Pwiz.Data.MsData.Spectra;

/// <summary>A binary array of doubles (e.g. m/z or intensity). Port of pwiz::msdata::BinaryDataArray.</summary>
public sealed class BinaryDataArray : ParamContainer
{
    /// <summary>Data-processing step that produced this array.</summary>
    public DataProcessing? DataProcessing { get; set; }

    /// <summary>The binary payload as doubles.</summary>
    public List<double> Data { get; } = new();

    /// <summary>
    /// Removes the payload without copying it: returns <see cref="Data"/>'s own backing array,
    /// whose first <paramref name="count"/> elements are the values and which may be longer, and
    /// leaves <see cref="Data"/> empty. A combined ion mobility payload can hold a million or more
    /// values, so handing it over saves a large-object-heap allocation and copy per spectrum.
    /// </summary>
    public double[] DetachData(out int count)
    {
        var data = Data;
        count = data.Count;
        if (count == 0)
        {
            data.Capacity = 0;
            return Array.Empty<double>();
        }
        var result = ListAccessor<double>.Items(data) ?? data.ToArray();
        data.Clear();
        data.Capacity = 0;  // drop the list's reference to the returned array
        return result;
    }

    /// <inheritdoc/>
    public override bool IsEmpty => DataProcessing is null && Data.Count == 0 && base.IsEmpty;
}

/// <summary>
/// Reaches <see cref="List{T}"/>'s backing array through its private <c>_items</c> field, or
/// returns null if a runtime names it differently. Reflection rather than an UnsafeAccessor
/// extern: an extern method has no IL, and the coverage profiler on the build agents fails the
/// test run when it cannot instrument one. The lookup happens once per spectrum array, so its
/// cost is negligible next to the copy it saves.
/// </summary>
internal static class ListAccessor<T>
{
    private static readonly System.Reflection.FieldInfo? ItemsField = typeof(List<T>).GetField("_items",
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

    public static T[]? Items(List<T> list) => ItemsField?.GetValue(list) as T[];
}

/// <summary>A binary array of 64-bit integers. Port of pwiz::msdata::IntegerDataArray.</summary>
public sealed class IntegerDataArray : ParamContainer
{
    /// <summary>Data-processing step that produced this array.</summary>
    public DataProcessing? DataProcessing { get; set; }

    /// <summary>The binary payload as int64s.</summary>
    public List<long> Data { get; } = new();

    /// <inheritdoc/>
    public override bool IsEmpty => DataProcessing is null && Data.Count == 0 && base.IsEmpty;
}

/// <summary>One data point in a mass spectrum (m/z + intensity).</summary>
public readonly record struct MZIntensityPair(double Mz, double Intensity);

/// <summary>One data point in a chromatogram (time + intensity).</summary>
public readonly record struct TimeIntensityPair(double Time, double Intensity);
