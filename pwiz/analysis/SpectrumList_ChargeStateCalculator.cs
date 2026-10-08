using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData.Spectra;

#pragma warning disable CA1707

namespace Pwiz.Analysis;

/// <summary>
/// Predicts charge state for MSn spectrum precursors by inspecting the intensity distribution
/// above vs. below the precursor m/z. Port of <c>pwiz::analysis::SpectrumList_ChargeStateCalculator</c>.
/// </summary>
/// <remarks>
/// <para>For each MSn spectrum: if the fraction of TIC at or below the precursor m/z is at
/// least <c>singleChargeFractionTIC</c>, predict singly-charged. Otherwise predict multiply-
/// charged — either set a single charge state (when <c>min == max</c>), or enumerate possible
/// charges in <c>[min, max]</c>.</para>
/// <para>The cpp version optionally invokes a libsvm-trained model for ETD-activated spectra to
/// pick a specific multiply-charged value. We don't port the SVM path: it serializes a model via
/// <c>boost::archive</c> and pulls in libsvm. The fall-through behavior (enumerate possible
/// charges across <c>[min, max]</c>) is the same as cpp produces when SVM doesn't fire — useful
/// in practice and avoids the heavy dependency.</para>
/// </remarks>
public sealed class SpectrumList_ChargeStateCalculator : SpectrumListWrapper
{
    private readonly bool _override;
    private readonly int _maxCharge;
    private readonly int _minCharge;
    private readonly double _singleChargeFractionTic;
    private readonly int _maxKnownCharge;
    private readonly bool _makeMS2;

    /// <summary>Constructs a charge-state-predicting wrapper.</summary>
    /// <param name="inner">Inner spectrum list.</param>
    /// <param name="overrideExistingChargeState">Replace existing <c>MS_charge_state</c> with the
    /// new prediction when true; otherwise leave existing values intact.</param>
    /// <param name="maxMultipleCharge">Maximum multiply-charged value (default 3).</param>
    /// <param name="minMultipleCharge">Minimum multiply-charged value (default 2).</param>
    /// <param name="intensityFractionBelowPrecursorForSinglyCharged">TIC fraction at or below the
    /// precursor m/z above which a spectrum is treated as singly-charged (default 0.9).</param>
    /// <param name="maxKnownCharge">If &gt; 0, treat existing charges greater than this as junk
    /// and overwrite. Default 0 = no maximum.</param>
    /// <param name="makeMS2">Use the alternative MakeMS2 algorithm (intensity ratio across a
    /// ±20 Da window) for the singly-charged check.</param>
    public SpectrumList_ChargeStateCalculator(
        ISpectrumList inner,
        bool overrideExistingChargeState = true,
        int maxMultipleCharge = 3,
        int minMultipleCharge = 2,
        double intensityFractionBelowPrecursorForSinglyCharged = 0.9,
        int maxKnownCharge = 0,
        bool makeMS2 = false) : base(inner)
    {
        _override = overrideExistingChargeState;
        _maxCharge = maxMultipleCharge;
        _minCharge = minMultipleCharge;
        _singleChargeFractionTic = intensityFractionBelowPrecursorForSinglyCharged;
        _maxKnownCharge = maxKnownCharge;
        _makeMS2 = makeMS2;
    }

    /// <inheritdoc/>
    public override Spectrum GetSpectrum(int index, bool getBinaryData = false)
    {
        // Charge prediction needs binary data unconditionally — cpp always passes true here.
        var spec = Inner.GetSpectrum(index, getBinaryData: true);
        if (!IsPredictable(spec)) return spec;

        var precursor = spec.Precursors[0];
        var ion = precursor.SelectedIons[0];

        // Walk existing charge-related CV params: erase them when overriding (preserving the old
        // value as a UserParam), keep them otherwise. If we find an existing MS_charge_state
        // that we're not overriding, return as-is.
        var existingPossible = new HashSet<int>();
        for (int i = ion.CVParams.Count - 1; i >= 0; i--)
        {
            var cv = ion.CVParams[i];
            if (cv.Cvid != CVID.MS_charge_state && cv.Cvid != CVID.MS_possible_charge_state) continue;

            bool bogusZero = cv.Value == "0";
            bool exceedsMaxKnown = _maxKnownCharge > 0 && _maxKnownCharge < cv.ValueAs<int>();
            if (_override || bogusZero || exceedsMaxKnown)
            {
                ion.UserParams.Add(new UserParam("old charge state", cv.Value));
                ion.CVParams.RemoveAt(i);
            }
            else if (cv.Cvid == CVID.MS_possible_charge_state)
            {
                existingPossible.Add(cv.ValueAs<int>());
            }
            else
            {
                // Existing MS_charge_state we're not overriding — leave the spectrum alone.
                return spec;
            }
        }

        double precursorMz = ion.CvParam(CVID.MS_selected_ion_m_z).ValueAs<double>();

        var mzData = spec.GetMZArray()?.Data;
        var intData = spec.GetIntensityArray()?.Data;
        if (mzData is null || intData is null) return spec;

        bool singleCharge = _makeMS2
            ? IsSinglyChargedMakeMs2(mzData, intData, precursorMz)
            : IsSinglyChargedTicFraction(mzData, intData, precursorMz);

        if (singleCharge)
        {
            if (_override && existingPossible.Count > 0)
                ion.CVParams.RemoveAll(p => p.Cvid == CVID.MS_possible_charge_state);
            ion.CVParams.Add(new CVParam(
                _override || existingPossible.Count == 0 ? CVID.MS_charge_state : CVID.MS_possible_charge_state,
                "1"));
        }
        else if (_maxCharge - _minCharge == 0)
        {
            if (_override && existingPossible.Count > 0)
                ion.CVParams.RemoveAll(p => p.Cvid == CVID.MS_possible_charge_state);
            ion.CVParams.Add(new CVParam(
                _override || existingPossible.Count == 0 ? CVID.MS_charge_state : CVID.MS_possible_charge_state,
                _maxCharge.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
        else
        {
            for (int z = _minCharge; z <= _maxCharge; z++)
                if (!existingPossible.Contains(z))
                    ion.CVParams.Add(new CVParam(CVID.MS_possible_charge_state,
                        z.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
        return spec;
    }

    private static bool IsPredictable(Spectrum spec)
    {
        if (spec.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0) < 2) return false;
        if (spec.DefaultArrayLength == 0) return false;
        if (spec.Precursors.Count == 0 || spec.Precursors[0].SelectedIons.Count == 0) return false;
        return true;
    }

    /// <summary>Cpp default singly-charged check: fraction of TIC at or below precursor m/z is
    /// ≥ <c>_singleChargeFractionTic</c>.</summary>
    private bool IsSinglyChargedTicFraction(IReadOnlyList<double> mz, IReadOnlyList<double> intensity, double precursorMz)
    {
        double tic = 0;
        for (int i = 0; i < intensity.Count; i++) tic += intensity[i];
        if (tic <= 0) return true; // empty TIC → degenerate, treat as singly charged

        // Walk from the right edge inward, summing intensities of points whose m/z >= precursor
        // (cpp's reverse iterator walks until it hits the precursor m/z position). The result is
        // 1 - fractionAtOrBelow, then we invert.
        int precursorIdx = LowerBound(mz, precursorMz);
        if (precursorIdx >= mz.Count) precursorIdx = mz.Count - 1;

        double inverseCutoff = 1.0 - _singleChargeFractionTic;
        double aboveTic = 0;
        for (int i = mz.Count - 1; i > precursorIdx && aboveTic < inverseCutoff; i--)
            aboveTic += intensity[i] / tic;

        double fractionAtOrBelow = 1.0 - aboveTic;
        return fractionAtOrBelow >= _singleChargeFractionTic;
    }

    /// <summary>MakeMS2-style singly-charged check: ratio of intensity above (precursor + 20)
    /// to below (precursor - 20), corrected by the position of the highest-mass observed peak.</summary>
    private bool IsSinglyChargedMakeMs2(IReadOnlyList<double> mz, IReadOnlyList<double> intensity, double precursorMz)
    {
        // sum intensities where m/z < precursor - 20 (cpp: upper_bound returns first > precursor - 20)
        int leftEnd = UpperBound(mz, precursorMz - 20);
        double leftSum = 1e-5;
        for (int i = 0; i < leftEnd; i++) leftSum += intensity[i];

        // sum intensities where m/z >= precursor + 20 (cpp: lower_bound)
        int rightStart = LowerBound(mz, precursorMz + 20);
        if (rightStart >= mz.Count) return true; // no peaks above → singly charged

        double rightSum = 1e-5;
        for (int i = rightStart; i < intensity.Count; i++) rightSum += intensity[i];

        double highMass = mz[mz.Count - 1];
        double correctionFactor = (precursorMz * 2 < highMass || precursorMz <= 0) ? 1 : highMass / precursorMz - 1;
        return rightSum / leftSum < _singleChargeFractionTic * correctionFactor;
    }

    private static int LowerBound(IReadOnlyList<double> sorted, double key)
    {
        int lo = 0, hi = sorted.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) >>> 1;
            if (sorted[mid] < key) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }

    private static int UpperBound(IReadOnlyList<double> sorted, double key)
    {
        int lo = 0, hi = sorted.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) >>> 1;
            if (sorted[mid] <= key) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }
}
