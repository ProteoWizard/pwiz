using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Spectra;
using ThermoFisher.CommonCore.Data.Business;

#pragma warning disable CA1707

namespace Pwiz.Vendor.Thermo;

/// <summary>
/// <see cref="IChromatogramList"/> backed by a Thermo <see cref="ThermoRawFile"/>. Emits the
/// document-level TIC chromatogram (currently the only chromatogram type supported).
/// </summary>
/// <remarks>Port of pwiz::msdata::ChromatogramList_Thermo.</remarks>
public sealed class ChromatogramList_Thermo : ChromatogramListBase
{
    private readonly ThermoRawFile _raw;
    private readonly List<ChromatogramIdentity> _index = new();

    /// <summary>DataProcessing id emitted as the <c>defaultDataProcessingRef</c>. Set by <see cref="Reader_Thermo"/>.</summary>
    public DataProcessing? Dp { get; set; }

    /// <inheritdoc/>
    public override DataProcessing? DataProcessing => Dp;

    /// <summary>Creates a chromatogram list backed by the given Thermo raw file.</summary>
    public ChromatogramList_Thermo(ThermoRawFile raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        _raw = raw;
        // Always emit the TIC chromatogram. SIM/SRM chromatograms would go here for those scan types.
        _index.Add(new ChromatogramIdentity { Index = 0, Id = "TIC" });
    }

    /// <inheritdoc/>
    public override int Count => _index.Count;

    /// <inheritdoc/>
    public override ChromatogramIdentity ChromatogramIdentity(int index) => _index[index];

    /// <inheritdoc/>
    public override Chromatogram GetChromatogram(int index, bool getBinaryData = false)
    {
        var ident = _index[index];
        var chrom = new Chromatogram
        {
            Index = ident.Index,
            Id = ident.Id,
        };
        chrom.Params.Set(CVID.MS_TIC_chromatogram);

        if (getBinaryData && ident.Id == "TIC")
        {
            var settings = new ChromatogramTraceSettings(TraceType.TIC);
            var data = _raw.Raw.GetChromatogramDataEx(new[] { settings }, -1, -1, new MassOptions());
            if (data?.PositionsArray?.Length > 0 && data.PositionsArray[0] is { } times
                && data.IntensitiesArray?[0] is { } intensities)
            {
                chrom.DefaultArrayLength = times.Length;
                chrom.BinaryDataArrays.Add(MakeArray(times, CVID.MS_time_array, CVID.UO_minute));
                chrom.BinaryDataArrays.Add(MakeArray(intensities, CVID.MS_intensity_array, CVID.MS_number_of_detector_counts));

                // Third array: ms level per time point, matches pwiz C++ ChromatogramList_Thermo.
                // Stored as an IntegerDataArray (64-bit integers) — pwiz writes it inside the same
                // <binaryDataArrayList> XML element as the float arrays.
                var msArr = new IntegerDataArray();
                msArr.Set(CVID.MS_non_standard_data_array, "ms level", CVID.UO_dimensionless_unit);
                for (int i = 0; i < times.Length; i++)
                {
                    try
                    {
                        int sn = _raw.Raw.ScanNumberFromRetentionTime(times[i]);
                        msArr.Data.Add(_raw.MsLevel(sn));
                    }
                    catch { msArr.Data.Add(0); }
                }
                chrom.IntegerDataArrays.Add(msArr);
            }
        }
        return chrom;
    }

    private static BinaryDataArray MakeArray(double[] values, CVID kind, CVID units)
    {
        var arr = new BinaryDataArray();
        arr.Set(kind, "", units);
        arr.Data.AddRange(values);
        return arr;
    }
}
