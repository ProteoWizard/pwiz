using System.Globalization;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Instruments;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Spectra;

#pragma warning disable CA1707 // underscored class name mirrors cpp `SpectrumList_UIMF`

namespace Pwiz.Vendor.UIMF;

/// <summary>
/// <see cref="ISpectrumList"/> backed by <see cref="UimfData"/>. C# port of cpp
/// <c>SpectrumList_UIMF</c> (SpectrumList_UIMF.cpp).
/// </summary>
/// <remarks>
/// One logical spectrum per <c>(frame, scan, frameType)</c> row in the SQLite
/// <c>Frame_Scans</c> table — same as cpp's index. Spectrum ids follow the cpp
/// `frame=N scan=N frameType=N` format (SpectrumList_UIMF.cpp:247) so reference mzMLs
/// keyed off id parse identically.
/// </remarks>
public sealed class SpectrumList_UIMF : SpectrumListBase
{
    private readonly UimfData _data;
    private readonly InstrumentConfiguration? _defaultIc;
    private readonly bool _ignoreZeroIntensityPoints;
    private readonly List<IndexEntry> _index = new();

    /// <summary>DataProcessing emitted as the document's <c>defaultDataProcessingRef</c>.</summary>
    public DataProcessing? Dp { get; set; }

    /// <inheritdoc/>
    public override DataProcessing? DataProcessing => Dp;

    /// <summary>Wraps <paramref name="data"/>. <paramref name="ignoreZeroIntensityPoints"/>
    /// matches cpp <c>config.ignoreZeroIntensityPoints</c> — when true, the SDK arrays go
    /// through unchanged; when false, the gap-padding zero-intensity boundaries from cpp
    /// UIMFReader.cpp:252-284 get inserted.</summary>
    public SpectrumList_UIMF(
        UimfData data,
        InstrumentConfiguration? defaultInstrumentConfiguration,
        bool ignoreZeroIntensityPoints)
    {
        ArgumentNullException.ThrowIfNull(data);
        _data = data;
        _defaultIc = defaultInstrumentConfiguration;
        _ignoreZeroIntensityPoints = ignoreZeroIntensityPoints;
        BuildIndex();
    }

    private sealed class IndexEntry : SpectrumIdentity
    {
        public int Frame;
        public int Scan;
        public UimfFrameType FrameType;
    }

    /// <inheritdoc/>
    public override int Count => _index.Count;

    /// <inheritdoc/>
    public override SpectrumIdentity SpectrumIdentity(int index) => _index[index];

    /// <inheritdoc/>
    public override Spectrum GetSpectrum(int index, bool getBinaryData = false)
        => GetSpectrum(index, getBinaryData, doCentroid: false);

    /// <summary>Pulls spectrum at <paramref name="index"/>, mirroring cpp
    /// <c>SpectrumList_UIMF::spectrum</c> (SpectrumList_UIMF.cpp:81-145).</summary>
    public Spectrum GetSpectrum(int index, bool getBinaryData, bool doCentroid)
    {
        if (index < 0 || index >= _index.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        var ie = _index[index];

        var spec = new Spectrum { Index = index, Id = ie.Id };
        var scan = new Scan { InstrumentConfiguration = _defaultIc };
        spec.ScanList.Set(CVID.MS_no_combination);
        spec.ScanList.Scans.Add(scan);

        int msLevel = UimfData.GetMsLevel(ie.FrameType);
        CVID spectrumType = ie.FrameType == UimfFrameType.Calibration
            ? CVID.MS_calibration_spectrum
            : (msLevel == 1 ? CVID.MS_MS1_spectrum : CVID.MS_MSn_spectrum);
        spec.Params.Set(CVID.MS_ms_level, msLevel);
        spec.Params.Set(spectrumType);
        spec.Params.Set(CVID.MS_profile_spectrum);

        scan.Set(CVID.MS_scan_start_time, _data.GetRetentionTimeMinutes(ie.Frame), CVID.UO_minute);
        scan.Set(CVID.MS_ion_mobility_drift_time, _data.GetDriftTimeMilliseconds(ie.Frame, ie.Scan), CVID.UO_millisecond);

        var (lo, hi) = _data.GetScanRange();
        scan.ScanWindows.Add(new ScanWindow(lo, hi, CVID.MS_m_z));

        // cpp SpectrumList_UIMF.cpp:127-134: UIMF data is always DIA-style; for MS2 frames
        // emit a synthetic "selected ion" centered on the scan window so MS2 consumers
        // (mzXML round-trip, MGF) still get a precursor record.
        if (msLevel > 1)
        {
            var precursor = new Precursor();
            var selectedIon = new SelectedIon();
            selectedIon.Set(CVID.MS_selected_ion_m_z, (lo + hi) / 2, CVID.MS_m_z);
            precursor.SelectedIons.Add(selectedIon);
            precursor.Activation.Set(CVID.MS_CID);
            spec.Precursors.Add(precursor);
        }

        if (getBinaryData)
        {
            var (mz, intensity) = _data.GetScan(ie.Frame, ie.Scan, ie.FrameType, _ignoreZeroIntensityPoints);
            spec.DefaultArrayLength = mz.Length;

            var mzArr = new BinaryDataArray();
            mzArr.Set(CVID.MS_m_z_array, string.Empty, CVID.MS_m_z);
            mzArr.Data.AddRange(mz);
            var intensityArr = new BinaryDataArray();
            intensityArr.Set(CVID.MS_intensity_array, string.Empty, CVID.MS_number_of_detector_counts);
            intensityArr.Data.AddRange(intensity);

            spec.BinaryDataArrays.Add(mzArr);
            spec.BinaryDataArrays.Add(intensityArr);
        }
        else
        {
            // Metadata-only request: still need defaultArrayLength to match cpp output.
            // cpp computes it via getScan with the same ignoreZeros flag; we do the same
            // even though the arrays are discarded. Slightly wasteful but keeps the diff
            // honest until a getNonZeroCount-style fast path is plumbed through.
            var (mz, _) = _data.GetScan(ie.Frame, ie.Scan, ie.FrameType, _ignoreZeroIntensityPoints);
            spec.DefaultArrayLength = mz.Length;
        }

        return spec;
    }

    private void BuildIndex()
    {
        // cpp SpectrumList_UIMF.cpp:235-251 walks UIMFReader's index and emits
        // `frame=N scan=N frameType=N` ids in the natural SQLite order.
        var raw = _data.Index;
        for (int i = 0; i < raw.Count; i++)
        {
            var r = raw[i];
            _index.Add(new IndexEntry
            {
                Index = i,
                Frame = r.Frame,
                Scan = r.Scan,
                FrameType = r.FrameType,
                Id = string.Format(CultureInfo.InvariantCulture,
                                   "frame={0} scan={1} frameType={2}",
                                   r.Frame, r.Scan, (int)r.FrameType),
            });
        }
    }
}
