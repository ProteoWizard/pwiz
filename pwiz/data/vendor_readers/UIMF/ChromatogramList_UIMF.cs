using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Spectra;

#pragma warning disable CA1707 // underscored class name mirrors cpp `ChromatogramList_UIMF`

namespace Pwiz.Vendor.UIMF;

/// <summary>
/// <see cref="IChromatogramList"/> backed by <see cref="UimfData"/>. C# port of cpp
/// <c>ChromatogramList_UIMF</c> (ChromatogramList_UIMF.cpp).
/// </summary>
/// <remarks>
/// One file-level TIC. cpp emits exactly one TIC chromatogram with id "TIC"
/// (ChromatogramList_UIMF.cpp:130-139) and stubs out SRM/SIM as TODOs; we mirror that.
/// </remarks>
public sealed class ChromatogramList_UIMF : ChromatogramListBase
{
    private readonly UimfData _data;

    /// <summary>DataProcessing emitted as the document's <c>defaultDataProcessingRef</c>.</summary>
    public DataProcessing? Dp { get; set; }

    /// <inheritdoc/>
    public override DataProcessing? DataProcessing => Dp;

    /// <summary>Wraps <paramref name="data"/>.</summary>
    public ChromatogramList_UIMF(UimfData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        _data = data;
    }

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

        if (getBinaryData)
        {
            var (timeMin, intensities) = _data.GetTic();
            c.DefaultArrayLength = timeMin.Length;

            var timeArr = new BinaryDataArray();
            timeArr.Set(CVID.MS_time_array, string.Empty, CVID.UO_minute);
            timeArr.Data.AddRange(timeMin);
            var intensityArr = new BinaryDataArray();
            intensityArr.Set(CVID.MS_intensity_array, string.Empty, CVID.MS_number_of_detector_counts);
            intensityArr.Data.AddRange(intensities);
            c.BinaryDataArrays.Add(timeArr);
            c.BinaryDataArrays.Add(intensityArr);
        }
        else
        {
            // cpp ChromatogramList_UIMF.cpp:113-114: when only metadata is asked for, set
            // defaultArrayLength to the frame count without materializing the arrays.
            c.DefaultArrayLength = _data.FrameCount;
        }

        return c;
    }
}
