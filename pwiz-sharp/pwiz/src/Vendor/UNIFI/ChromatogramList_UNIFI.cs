using System.Globalization;
using System.Text.RegularExpressions;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Spectra;

#pragma warning disable CA1707

namespace Pwiz.Vendor.UNIFI;

/// <summary>
/// <see cref="IChromatogramList"/> backed by a <see cref="IUnifiDataSource"/>. C# port of cpp
/// <c>ChromatogramList_UNIFI</c> (ChromatogramList_UNIFI.cpp).
/// </summary>
/// <remarks>
/// UNIFI exposes one TIC + one BPI per scan function (typically Low / High / lockmass), one
/// chromatogram per MRM transition, and one per UV / FLR / IR detector channel. cpp collapses
/// all the per-function TICs into a single <c>id="TIC"</c> chromatogram (by summing their
/// arrays in <c>makeFullFileChromatogram</c>); we mirror that. BPI is currently disabled in
/// cpp ("FIXME: not working the same as TIC" — ChromatogramList_UNIFI.cpp:264) so we skip it
/// too.
/// </remarks>
public sealed class ChromatogramList_UNIFI : ChromatogramListBase
{
    private static readonly Regex FunctionNumberRegex =
        new(@"^(\d+)\:.*", RegexOptions.Compiled);

    private readonly IUnifiDataSource _source;
    private readonly bool _globalChromsAreMs1Only;
    private readonly List<IndexEntry> _index = new();

    /// <summary>DataProcessing emitted as the document's <c>defaultDataProcessingRef</c>.</summary>
    public DataProcessing? Dp { get; set; }

    /// <inheritdoc/>
    public override DataProcessing? DataProcessing => Dp;

    /// <summary>Wraps <paramref name="source"/>. <paramref name="globalChromatogramsAreMs1Only"/>
    /// matches cpp <c>config.globalChromatogramsAreMs1Only</c> — when true, the synthetic TIC
    /// only sums the function-1 chromatograms (cpp ChromatogramList_UNIFI.cpp:108-109).</summary>
    public ChromatogramList_UNIFI(IUnifiDataSource source, bool globalChromatogramsAreMs1Only)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
        _globalChromsAreMs1Only = globalChromatogramsAreMs1Only;
        CreateIndex();
    }

    private sealed class IndexEntry : ChromatogramIdentity
    {
        public CVID ChromatogramType;
        public int ChromatogramInfoIndex;
    }

    /// <inheritdoc/>
    public override int Count => _index.Count;

    /// <inheritdoc/>
    public override ChromatogramIdentity ChromatogramIdentity(int index) => _index[index];

    /// <inheritdoc/>
    public override Chromatogram GetChromatogram(int index, bool getBinaryData = false)
    {
        if (index < 0 || index >= _index.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        var ie = _index[index];

        var c = new Chromatogram { Index = ie.Index, Id = ie.Id };
        c.Params.Set(ie.ChromatogramType);

        switch (ie.ChromatogramType)
        {
            case CVID.MS_TIC_chromatogram:
                MakeFullFileChromatogram(c, "(TIC)", getBinaryData);
                break;

            case CVID.MS_basepeak_chromatogram:
                MakeFullFileChromatogram(c, "(BPC)", getBinaryData);
                break;

            case CVID.MS_SRM_chromatogram:
                FillSrmChromatogram(c, ie.ChromatogramInfoIndex, getBinaryData);
                break;

            case CVID.MS_emission_chromatogram:
                FillSimpleChromatogram(c, ie.ChromatogramInfoIndex, getBinaryData,
                                       intensityUnit: CVID.MS_number_of_detector_counts);
                break;

            case CVID.MS_absorption_chromatogram:
                FillSimpleChromatogram(c, ie.ChromatogramInfoIndex, getBinaryData,
                                       intensityUnit: CVID.UO_absorbance_unit);
                break;

            default:
                throw new InvalidOperationException(
                    $"unhandled chromatogram type {ie.ChromatogramType} at index {index}");
        }

        return c;
    }

    private void MakeFullFileChromatogram(Chromatogram result, string chromatogramTag, bool getBinaryData)
    {
        // cpp ChromatogramList_UNIFI.cpp:87-144: walk the source's chromatogramInfo, take the
        // ones whose name contains the tag (e.g. "(TIC)"), parse out the leading function
        // number (e.g. "1: TOF MS..." → 1), drop "Integrated:" entries, and sum their points
        // into a single multimap keyed by retention time. The result is a chromatogram whose
        // intensity-array length is the number of distinct (rt, function) pairs across all
        // matching channels — i.e. cpp emits ONE TIC per pwiz-document, not one per function.
        var samples = new SortedDictionary<double, List<(int Function, double Intensity)>>();

        var infoList = _source.ChromatogramInfo;
        long sizeOnly = 0;
        for (int i = 0; i < infoList.Count; i++)
        {
            var info = infoList[i];
            if (info.Name.IndexOf(chromatogramTag, StringComparison.Ordinal) < 0) continue;
            if (info.Name.Contains("Integrated", StringComparison.Ordinal)) continue;

            var match = FunctionNumberRegex.Match(info.Name);
            if (!match.Success) continue; // cpp warns once + skips
            int functionNumber = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            if (_globalChromsAreMs1Only && functionNumber != 1) continue;

            var chromatogram = new UnifiChromatogram();
            _source.GetChromatogram(info.Index, chromatogram, getBinaryData);

            if (getBinaryData)
            {
                for (int j = 0; j < chromatogram.ArrayLength; j++)
                {
                    double rt = chromatogram.TimeArray[j];
                    if (!samples.TryGetValue(rt, out var bucket))
                        samples[rt] = bucket = new List<(int, double)>();
                    bucket.Add((functionNumber, chromatogram.IntensityArray[j]));
                }
            }
            else
            {
                sizeOnly += chromatogram.ArrayLength;
            }
        }

        if (getBinaryData)
        {
            int total = samples.Sum(kv => kv.Value.Count);
            var time = new BinaryDataArray();
            time.Set(CVID.MS_time_array, string.Empty, CVID.UO_minute);
            var intensity = new BinaryDataArray();
            intensity.Set(CVID.MS_intensity_array, string.Empty, CVID.MS_number_of_detector_counts);
            // cpp ChromatogramList_UNIFI.cpp:128-141 emits the function number in a parallel
            // int64 array tagged with MS_non_standard_data_array name="function".
            var functionArray = new IntegerDataArray();
            functionArray.Set(CVID.MS_non_standard_data_array, "function", CVID.UO_dimensionless_unit);
            time.Data.Capacity = total;
            intensity.Data.Capacity = total;
            functionArray.Data.Capacity = total;
            foreach (var kv in samples)
            {
                foreach (var (function, value) in kv.Value)
                {
                    time.Data.Add(kv.Key);
                    intensity.Data.Add(value);
                    functionArray.Data.Add(function);
                }
            }
            result.BinaryDataArrays.Add(time);
            result.BinaryDataArrays.Add(intensity);
            result.IntegerDataArrays.Add(functionArray);
            result.DefaultArrayLength = total;
        }
        else
        {
            result.DefaultArrayLength = (int)sizeOnly;
        }
    }

    private void FillSrmChromatogram(Chromatogram result, int infoIndex, bool getBinaryData)
    {
        var chromatogram = new UnifiChromatogram();
        _source.GetChromatogram(infoIndex, chromatogram, getBinaryData);
        result.DefaultArrayLength = chromatogram.ArrayLength;
        if (chromatogram.Polarity != UnifiPolarity.Unknown)
            result.Params.Set(TranslatePolarity(chromatogram.Polarity));

        result.Precursor.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, chromatogram.Q1, CVID.MS_m_z);
        result.Product.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, chromatogram.Q3, CVID.MS_m_z);

        if (getBinaryData) AppendTimeIntensity(result, chromatogram, CVID.MS_number_of_detector_counts);
    }

    private void FillSimpleChromatogram(Chromatogram result, int infoIndex, bool getBinaryData, CVID intensityUnit)
    {
        var chromatogram = new UnifiChromatogram();
        _source.GetChromatogram(infoIndex, chromatogram, getBinaryData);
        result.DefaultArrayLength = chromatogram.ArrayLength;
        if (getBinaryData) AppendTimeIntensity(result, chromatogram, intensityUnit);
    }

    private static void AppendTimeIntensity(Chromatogram result, UnifiChromatogram chromatogram, CVID intensityUnit)
    {
        var time = new BinaryDataArray();
        time.Set(CVID.MS_time_array, string.Empty, CVID.UO_minute);
        var intensity = new BinaryDataArray();
        intensity.Set(CVID.MS_intensity_array, string.Empty, intensityUnit);
        time.Data.AddRange(chromatogram.TimeArray);
        intensity.Data.AddRange(chromatogram.IntensityArray);
        result.BinaryDataArrays.Add(time);
        result.BinaryDataArrays.Add(intensity);
    }

    private static CVID TranslatePolarity(UnifiPolarity p) => p switch
    {
        UnifiPolarity.Positive => CVID.MS_positive_scan,
        UnifiPolarity.Negative => CVID.MS_negative_scan,
        _ => CVID.CVID_Unknown,
    };

    private void CreateIndex()
    {
        // cpp ChromatogramList_UNIFI.cpp:244-323. The synthetic TIC is added once on the first
        // function whose name contains "(TIC)" — even though there are usually 2-3 TIC entries
        // (low CE, high CE, reference). BPI is declared in cpp but commented out (FIXME on
        // line 264 — "not working the same as TIC"); we skip it too.
        bool hasTic = false;
        var infoList = _source.ChromatogramInfo;
        for (int i = 0; i < infoList.Count; i++)
        {
            var info = infoList[i];
            switch (info.Type)
            {
                case ChromatogramType.TIC:
                    if (!hasTic && info.Name.Contains("(TIC)", StringComparison.Ordinal))
                    {
                        hasTic = true;
                        _index.Add(new IndexEntry
                        {
                            Id = "TIC",
                            ChromatogramType = CVID.MS_TIC_chromatogram,
                        });
                    }
                    break;

                case ChromatogramType.MRM:
                {
                    // cpp ChromatogramList_UNIFI.cpp:279-283: prepend "+ " / "- " when the
                    // chromatogram name ends with the polarity char.
                    string id = "SRM SIC " + info.Name;
                    if (info.Name.EndsWith('+'))
                        id = "+ " + id;
                    else if (info.Name.EndsWith('-'))
                        id = "- " + id;
                    _index.Add(new IndexEntry
                    {
                        Id = id,
                        ChromatogramType = CVID.MS_SRM_chromatogram,
                        ChromatogramInfoIndex = i,
                    });
                    break;
                }

                case ChromatogramType.FLR:
                    _index.Add(new IndexEntry
                    {
                        Id = info.Name,
                        ChromatogramType = CVID.MS_emission_chromatogram,
                        ChromatogramInfoIndex = i,
                    });
                    break;

                case ChromatogramType.UV:
                    _index.Add(new IndexEntry
                    {
                        Id = info.Name,
                        ChromatogramType = CVID.MS_absorption_chromatogram,
                        ChromatogramInfoIndex = i,
                    });
                    break;

                default:
                    // cpp falls through silently for unsupported detector types.
                    break;
            }
        }

        for (int i = 0; i < _index.Count; i++) _index[i].Index = i;
    }
}
