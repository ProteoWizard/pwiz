using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Spectra;

#pragma warning disable CA1707 // underscored class name mirrors cpp `ChromatogramList_Mobilion`

namespace Pwiz.Vendor.Mobilion;

/// <summary>
/// <see cref="IChromatogramList"/> backed by <see cref="MobilionData"/>. C# port of cpp
/// <c>ChromatogramList_Mobilion</c> (ChromatogramList_Mobilion.cpp).
/// </summary>
/// <remarks>
/// Single TIC chromatogram with id "TIC". cpp also attaches a parallel integer
/// <c>ms level</c> data array (ChromatogramList_Mobilion.cpp:124-126) to flag whether
/// each frame is MS1 or MS2 — emit the same so downstream filters that key off the
/// auxiliary array continue to work.
/// </remarks>
public sealed class ChromatogramList_Mobilion : ChromatogramListBase
{
    private readonly MobilionData _data;

    /// <summary>DataProcessing emitted as the document's <c>defaultDataProcessingRef</c>.</summary>
    public DataProcessing? Dp { get; set; }

    /// <inheritdoc/>
    public override DataProcessing? DataProcessing => Dp;

    /// <summary>Wraps <paramref name="data"/>.</summary>
    public ChromatogramList_Mobilion(MobilionData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        _data = data;
    }

    /// <inheritdoc/>
    /// <remarks>Disposes the underlying <see cref="MobilionData"/> as a safety net for
    /// callers that take a <see cref="ChromatogramList_Mobilion"/> reference but never
    /// touch the SpectrumList. <see cref="MobilionData.Dispose()"/> is idempotent.</remarks>
    protected override void DisposeCore() => _data.Dispose();

    /// <inheritdoc/>
    public override int Count => 1;

    /// <inheritdoc/>
    public override ChromatogramIdentity ChromatogramIdentity(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(index, 0);
        return new ChromatogramIdentityImpl { Index = 0, Id = "TIC" };
    }

    private sealed class ChromatogramIdentityImpl : ChromatogramIdentity { }

    /// <inheritdoc/>
    public override Chromatogram GetChromatogram(int index, bool getBinaryData = false)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(index, 0);

        var c = new Chromatogram { Index = 0, Id = "TIC" };
        c.Params.Set(CVID.MS_TIC_chromatogram);

        int frames = _data.NumFrames;
        c.DefaultArrayLength = frames;

        if (!getBinaryData) return c;

        var time = new double[frames];
        var intensity = new double[frames];
        var msLevel = new long[frames];

        // cpp ChromatogramList_Mobilion.cpp:139-147: loop frames 1..N, take Time() in
        // minutes (cpp emits UO_minute on the array — the SDK's Frame::Time returns
        // seconds, so cpp implicitly assumes seconds==minutes? No — cpp passes
        // Frame::Time directly through into a UO_minute-tagged array, which is a unit
        // mislabeling on the cpp side. Mirror it: same numeric output, same UO_minute
        // tag. If/when cpp gets fixed, change here too.
        for (int i = 1; i <= frames; i++)
        {
            using var frame = _data.GetFrame(i);
            int j = i - 1;
            time[j] = frame.TimeSeconds;
            intensity[j] = frame.TotalIntensity;
            msLevel[j] = frame.GetCe(0) > 0 ? 2 : 1;
        }

        var timeArr = new BinaryDataArray();
        timeArr.Set(CVID.MS_time_array, string.Empty, CVID.UO_minute);
        timeArr.Data.AddRange(time);

        var intensityArr = new BinaryDataArray();
        intensityArr.Set(CVID.MS_intensity_array, string.Empty, CVID.MS_number_of_detector_counts);
        intensityArr.Data.AddRange(intensity);

        var msLevelArr = new IntegerDataArray();
        msLevelArr.Set(CVID.MS_non_standard_data_array, "ms level", CVID.UO_dimensionless_unit);
        msLevelArr.Data.AddRange(msLevel);

        c.BinaryDataArrays.Add(timeArr);
        c.BinaryDataArrays.Add(intensityArr);
        c.IntegerDataArrays.Add(msLevelArr);

        return c;
    }
}
