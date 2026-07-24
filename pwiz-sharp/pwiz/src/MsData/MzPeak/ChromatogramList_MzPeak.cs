using System;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Data.MsData.MzPeak;

/// <summary>
/// Lazy <see cref="IChromatogramList"/> over an <see cref="MzPeakReader"/>.
/// Sibling of <see cref="SpectrumList_MzPeak"/>. The mzPeak chromatogram model
/// is much simpler than the spectrum model — id, type CURIE, and the
/// (time, intensity) point arrays — so the translation here is short.
/// </summary>
internal sealed class ChromatogramList_MzPeak : ChromatogramListBase
{
    private readonly MzPeakReader _reader;
    private readonly bool _ownsReader;
    private readonly DataProcessing? _dp;
    private readonly ChromatogramIdentity[] _identities;

    public ChromatogramList_MzPeak(MzPeakReader reader, DataProcessing? dp, bool ownsReader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        _reader = reader;
        _ownsReader = ownsReader;
        _dp = dp;
        _identities = new ChromatogramIdentity[reader.ChromatogramCount];
        for (int i = 0; i < reader.ChromatogramCount; i++)
        {
            var desc = reader.GetChromatogramDescription(i);
            _identities[i] = new ChromatogramIdentity { Index = i, Id = desc.Id };
        }
    }

    public override int Count => _identities.Length;

    public override ChromatogramIdentity ChromatogramIdentity(int index) => _identities[index];

    public override DataProcessing? DataProcessing => _dp;

    public override Chromatogram GetChromatogram(int index, bool getBinaryData = false)
    {
        if ((uint)index >= (uint)_identities.Length)
            throw new ArgumentOutOfRangeException(nameof(index));

        var desc = _reader.GetChromatogramDescription(index);
        var chrom = new Chromatogram
        {
            Index = index,
            Id = desc.Id,
        };

        // Chromatogram type CURIE → CV term. mzPeak stores e.g. MS:1000235
        // (total ion current chromatogram). Set both the bag-level type
        // tag and any chromatogram-type-specific defaults the cpp readers emit.
        var typeCvid = CvidFromCurie(desc.ChromatogramTypeCurie);
        if (typeCvid != CVID.CVID_Unknown)
            chrom.Params.Set(typeCvid);

        if (desc.Parameters is not null)
            ApplyParams(chrom.Params, desc.Parameters);

        if (getBinaryData)
        {
            // pwiz/mzML chromatograms always carry a time + intensity array pair, even when empty;
            // emit them unconditionally so 0-point chromatograms round-trip with the right shape.
            var data = _reader.GetChromatogramData(index);
            var time = data?.Time ?? Array.Empty<double>();
            var intensitySrc = data?.Intensity ?? Array.Empty<float>();
            var intensityDouble = new double[intensitySrc.Length];
            for (int i = 0; i < intensitySrc.Length; i++) intensityDouble[i] = intensitySrc[i];
            var timeUnit = CvidFromCurie(desc.TimeUnitCurie);
            if (timeUnit == CVID.CVID_Unknown) timeUnit = CVID.UO_minute;
            var intensityUnit = CvidFromCurie(desc.IntensityUnitCurie);
            if (intensityUnit == CVID.CVID_Unknown) intensityUnit = CVID.MS_number_of_detector_counts;
            SetTimeIntensityArrays(chrom, time, intensityDouble, timeUnit, intensityUnit);
            MzPeakAuxArrays.Apply(desc.AuxArrays, chrom.BinaryDataArrays, chrom.IntegerDataArrays);
        }

        return chrom;
    }

    protected override void DisposeCore()
    {
        if (_ownsReader) _reader.Dispose();
    }

    /// <summary>
    /// pwiz's <see cref="Chromatogram"/> doesn't expose a SetTimeIntensityArrays
    /// helper (asymmetric with <see cref="Spectrum.SetMZIntensityArrays"/>), so
    /// build the two BinaryDataArrays explicitly. Time uses MS_minute (matching
    /// the cpp <c>Chromatogram::set_time_intensity_arrays</c> default).
    /// </summary>
    private static void SetTimeIntensityArrays(Chromatogram chrom, double[] time, double[] intensity, CVID timeUnit, CVID intensityUnit)
    {
        var timeArr = new BinaryDataArray();
        timeArr.Set(CVID.MS_time_array, "", timeUnit);
        timeArr.Data.AddRange(time);
        chrom.BinaryDataArrays.Add(timeArr);

        var intArr = new BinaryDataArray();
        intArr.Set(CVID.MS_intensity_array, "", intensityUnit);
        intArr.Data.AddRange(intensity);
        chrom.BinaryDataArrays.Add(intArr);

        chrom.DefaultArrayLength = time.Length;
    }

    private static CVID CvidFromCurie(string? curie)
    {
        if (string.IsNullOrEmpty(curie)) return CVID.CVID_Unknown;
        return CvLookup.CvTermInfo(curie).Cvid;
    }

    /// <summary>Apply free-form chromatogram params (CV → CVParam, else UserParam, keeping type).</summary>
    private static void ApplyParams(Pwiz.Data.Common.Params.ParamContainer target, System.Collections.Generic.IReadOnlyList<MzPeakReader.CvParam> src)
    {
        foreach (var p in src)
        {
            var cvid = CvidFromCurie(p.Accession);
            var unitCvid = string.IsNullOrEmpty(p.Unit) ? CVID.CVID_Unknown : CvidFromCurie(p.Unit);
            string value = p.ValueString
                ?? p.ValueInteger?.ToString(System.Globalization.CultureInfo.InvariantCulture)
                ?? p.ValueFloat?.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                ?? (p.ValueBoolean is bool b ? (b ? "true" : "false") : string.Empty);

            if (cvid != CVID.CVID_Unknown)
                target.Set(cvid, value, unitCvid);
            else
                target.UserParams.Add(new Pwiz.Data.Common.Params.UserParam(
                    p.Name ?? string.Empty, value, type: p.Type ?? string.Empty, units: unitCvid));
        }
    }
}

/// <summary>
/// Rebuilds auxiliary (non-canonical) binary/integer data arrays from their round-tripped
/// <see cref="AuxiliaryArrayData"/> records onto a spectrum or chromatogram. Shared by
/// <see cref="SpectrumList_MzPeak"/> and <see cref="ChromatogramList_MzPeak"/>.
/// </summary>
internal static class MzPeakAuxArrays
{
    public static void Apply(
        System.Collections.Generic.IReadOnlyList<AuxiliaryArrayData>? aux,
        System.Collections.Generic.List<BinaryDataArray> binaryTarget,
        System.Collections.Generic.List<IntegerDataArray> integerTarget)
    {
        if (aux is null) return;
        foreach (var a in aux)
        {
            if (a.IsInteger)
            {
                var arr = new IntegerDataArray();
                ApplyMzPeakParams(arr, a.Params);
                if (a.IntValues is not null) arr.Data.AddRange(a.IntValues);
                integerTarget.Add(arr);
            }
            else
            {
                var arr = new BinaryDataArray();
                ApplyMzPeakParams(arr, a.Params);
                if (a.DoubleValues is not null) arr.Data.AddRange(a.DoubleValues);
                binaryTarget.Add(arr);
            }
        }
    }

    internal static void ApplyMzPeakParams(Pwiz.Data.Common.Params.ParamContainer target,
        System.Collections.Generic.IReadOnlyList<MzPeakCvParam> src)
    {
        foreach (var p in src)
        {
            var cvid = string.IsNullOrEmpty(p.Accession) ? CVID.CVID_Unknown : CvLookup.CvTermInfo(p.Accession!).Cvid;
            var unit = string.IsNullOrEmpty(p.Unit) ? CVID.CVID_Unknown : CvLookup.CvTermInfo(p.Unit!).Cvid;
            string value = p.Value switch
            {
                null => string.Empty,
                string s => s,
                bool b => b ? "true" : "false",
                long l => l.ToString(System.Globalization.CultureInfo.InvariantCulture),
                int i => i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                double d => d.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                _ => p.Value.ToString() ?? string.Empty,
            };
            if (cvid != CVID.CVID_Unknown)
                target.Set(cvid, value, unit);
            else
                target.UserParams.Add(new Pwiz.Data.Common.Params.UserParam(p.Name ?? string.Empty, value, type: string.Empty, units: unit));
        }
    }
}
