using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;

namespace Pwiz.Data.IdentData;

/// <summary>Definition of a per-fragment measurement (m/z, intensity, etc.). Port of
/// <c>pwiz::identdata::Measure</c>.</summary>
public sealed class Measure : IdentifiableParamContainer { }

/// <summary>Per-ion array of measure values; references the <see cref="Measure"/> defining the
/// measurement type. Port of <c>FragmentArray</c>.</summary>
public sealed class FragmentArray
{
    /// <summary>Per-ion values.</summary>
    public List<double> Values { get; } = new();

    /// <summary>The measure these values describe.</summary>
    public Measure? MeasurePtr { get; set; }

    /// <summary>True when no measure or values are recorded.</summary>
    public bool IsEmpty => Values.Count == 0 && MeasurePtr is null;
}

/// <summary>An ion type and its observed indices (e.g. b3, b7, b8). Cpp models this as a
/// <c>CVParam</c> subclass; we keep the same shape with an explicit <see cref="Type"/> CVParam.
/// Port of <c>IonType</c>.</summary>
public sealed class IonType
{
    /// <summary>Ion-type CV term (e.g. <c>MS_frag__b_ion</c>).</summary>
    public CVParam Type { get; set; } = new(CVID.CVID_Unknown);

    /// <summary>Indices of the observed ions (1-based positions in the fragmented peptide).</summary>
    public List<int> Index { get; } = new();

    /// <summary>Charge state of the ions in this group.</summary>
    public int Charge { get; set; }

    /// <summary>Per-measure value arrays for the ions in this group.</summary>
    public List<FragmentArray> FragmentArray { get; } = new();

    /// <summary>True when neither the type nor any ion has been recorded.</summary>
    public bool IsEmpty => Type.IsEmpty && Index.Count == 0 && Charge == 0 && FragmentArray.Count == 0;
}
