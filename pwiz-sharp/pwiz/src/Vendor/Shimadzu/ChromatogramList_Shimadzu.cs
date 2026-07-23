#if !NO_VENDOR_SUPPORT
using System.Globalization;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Spectra;

#pragma warning disable CA1707

namespace Pwiz.Vendor.Shimadzu;

/// <summary>
/// <see cref="IChromatogramList"/> for Shimadzu <c>.lcd</c> files. C# port of cpp
/// <c>ChromatogramList_Shimadzu</c>: file-level TIC plus one chromatogram per SRM transition
/// (suppressed when <c>srmAsSpectra</c> is set on the reader config).
/// </summary>
public sealed class ChromatogramList_Shimadzu : ChromatogramListBase
{
    private readonly ShimadzuRawData _raw;
    private readonly bool _ownsRaw;
    private readonly bool _srmAsSpectra;
    private readonly bool _globalChromsAreMs1Only;
    private readonly List<IndexEntry> _index = new();

    /// <summary>DataProcessing emitted as the document's <c>defaultDataProcessingRef</c>.</summary>
    public DataProcessing? Dp { get; set; }

    /// <inheritdoc/>
    public override DataProcessing? DataProcessing => Dp;

    /// <summary>Wraps <paramref name="raw"/>; <paramref name="ownsRaw"/> selects whether
    /// disposing the list disposes the raw data handle.</summary>
    public ChromatogramList_Shimadzu(ShimadzuRawData raw, bool ownsRaw,
        bool srmAsSpectra, bool globalChromatogramsAreMs1Only)
    {
        ArgumentNullException.ThrowIfNull(raw);
        _raw = raw;
        _ownsRaw = ownsRaw;
        _srmAsSpectra = srmAsSpectra;
        _globalChromsAreMs1Only = globalChromatogramsAreMs1Only;
        CreateIndex();
    }

    private enum ChromKind { Tic, Srm }

    private sealed class IndexEntry : ChromatogramIdentity
    {
        public ChromKind Kind;
        public CVID ChromatogramType;
        public ShimadzuTransition? Transition;
    }

    /// <inheritdoc/>
    public override int Count => _index.Count;

    /// <inheritdoc/>
    public override ChromatogramIdentity ChromatogramIdentity(int index) => _index[index];

    private void CreateIndex()
    {
        // TIC always comes first (cpp ChromatogramList_Shimadzu.cpp:160-173). Some LabSolutions
        // versions don't expose TIC; cpp tolerates that with a try/catch — we emit the entry
        // unconditionally and let GetChromatogram return an empty array if the SDK fails.
        _index.Add(new IndexEntry
        {
            Index = 0,
            Id = "TIC",
            Kind = ChromKind.Tic,
            ChromatogramType = CVID.MS_TIC_chromatogram,
        });

        if (_srmAsSpectra) return;

        foreach (var t in _raw.Transitions)
        {
            string polarityPrefix = t.Polarity == ShimadzuPolarity.Negative ? "- " : string.Empty;
            string id = string.Format(CultureInfo.InvariantCulture,
                "{0}SRM SIC Q1={1} Q3={2} Channel={3} Event={4} Segment={5} CE={6}",
                polarityPrefix,
                PwizFloat.ToPrintfG10(t.Q1),
                PwizFloat.ToPrintfG10(t.Q3),
                t.Channel,
                t.Event,
                t.Segment,
                PwizFloat.ToPrintfG10(t.CollisionEnergy));
            _index.Add(new IndexEntry
            {
                Index = _index.Count,
                Id = id,
                Kind = ChromKind.Srm,
                ChromatogramType = CVID.MS_SRM_chromatogram,
                Transition = t,
            });
        }
    }

    /// <inheritdoc/>
    public override Chromatogram GetChromatogram(int index, bool getBinaryData = false)
    {
        if (index < 0 || index >= _index.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        var ie = _index[index];

        var c = new Chromatogram { Index = ie.Index, Id = ie.Id };
        c.Params.Set(ie.ChromatogramType);

        switch (ie.Kind)
        {
            case ChromKind.Tic:
            {
                try
                {
                    var (x, y) = _raw.GetTic(_globalChromsAreMs1Only);
                    c.DefaultArrayLength = x.Length;
                    if (getBinaryData) FillTimeIntensityArrays(c, x, y);
                }
                catch { /* some LabSolutions versions don't expose TIC; cpp tolerates */ }
                break;
            }
            case ChromKind.Srm when ie.Transition is not null:
            {
                var t = ie.Transition;
                c.Precursor.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, t.Q1, CVID.MS_m_z);
                c.Precursor.Activation.Set(CVID.MS_CID);
                c.Precursor.Activation.Set(CVID.MS_collision_energy, t.CollisionEnergy, CVID.UO_electronvolt);
                c.Params.Set(t.Polarity == ShimadzuPolarity.Positive ? CVID.MS_positive_scan : CVID.MS_negative_scan);
                c.Product.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, t.Q3, CVID.MS_m_z);

                var (x, y) = _raw.GetSrmChromatogram(t);
                c.DefaultArrayLength = x.Length;
                if (getBinaryData) FillTimeIntensityArrays(c, x, y);
                break;
            }
        }

        return c;
    }

    private static void FillTimeIntensityArrays(Chromatogram c, double[] x, double[] y)
    {
        var times = new BinaryDataArray();
        times.Set(CVID.MS_time_array, string.Empty, CVID.UO_second);
        var intens = new BinaryDataArray();
        intens.Set(CVID.MS_intensity_array, string.Empty, CVID.MS_number_of_detector_counts);
        int n = Math.Min(x.Length, y.Length);
        for (int i = 0; i < n; i++) { times.Data.Add(x[i]); intens.Data.Add(y[i]); }
        c.BinaryDataArrays.Add(times);
        c.BinaryDataArrays.Add(intens);
    }

    /// <inheritdoc/>
    protected override void DisposeCore()
    {
        if (_ownsRaw)
        {
            try { _raw.Dispose(); }
            catch { /* best-effort */ }
        }
    }
}
#endif
