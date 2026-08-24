namespace Pwiz.Vendor.Bruker.PrmScheduling;

/// <summary>
/// Managed wrapper for <c>PrmMethodInfo</c>. Method-template metadata read
/// from a .prmsqlite file.
/// </summary>
/// <remarks>
/// Property names mirror the original C++/CLI <c>pwiz.CLI.Bruker.PrmScheduling.MethodInfo</c>
/// (lowercase snake_case) so Skyline's existing call sites (e.g.
/// <c>methodInfo.one_over_k0_lower_limit</c>) port across without churn.
/// </remarks>
public sealed class MethodInfo
{
    private PrmMethodInfo _native;

    /// <summary>Wraps the native struct returned from <c>prm_scheduling_get_method_info</c>.</summary>
    internal MethodInfo(PrmMethodInfo native)
    {
        _native = native;
    }

    /// <summary>Default constructor for the seldom case where a caller builds one up.</summary>
    public MethodInfo() { }

    /// <summary>1 / K0 distance required between two targets so the quadrupole tunes cleanly.</summary>
    public double mobility_gap
    {
        get => _native.mobility_gap;
        set => _native.mobility_gap = value;
    }

    /// <summary>Frames per second (constant when the tims ramp is identical across frames).</summary>
    public double frame_rate
    {
        get => _native.frame_rate;
        set => _native.frame_rate = value;
    }

    /// <summary>Lower 1 / K0 measurable by the template method.</summary>
    public double one_over_k0_lower_limit
    {
        get => _native.one_over_k0_lower_limit;
        set => _native.one_over_k0_lower_limit = value;
    }

    /// <summary>Upper 1 / K0 measurable by the template method.</summary>
    public double one_over_k0_upper_limit
    {
        get => _native.one_over_k0_upper_limit;
        set => _native.one_over_k0_upper_limit = value;
    }
}
