namespace Pwiz.Vendor.Bruker.PrmScheduling;

/// <summary>
/// Managed wrapper for <c>PrmInputTarget</c>. A single PRM target row
/// added to the scheduler via <see cref="Scheduler.AddInputTarget"/>.
/// </summary>
/// <remarks>
/// Property names mirror the original C++/CLI <c>pwiz.CLI.Bruker.PrmScheduling.InputTarget</c>
/// (lowercase snake_case) so Skyline's existing call sites (e.g.
/// <c>target.isolation_mz = ...</c>) port across without churn.
/// </remarks>
public sealed class InputTarget
{
    internal PrmInputTarget Native;

    /// <summary>Construct with default zeroed parameters.</summary>
    public InputTarget() { }

    /// <summary>Isolation m/z; the quadrupole tunes to this for fragmentation.</summary>
    public double isolation_mz
    {
        get => Native.isolation_mz;
        set => Native.isolation_mz = value;
    }

    /// <summary>Total 3-dB isolation window width (m/z units).</summary>
    public double isolation_width
    {
        get => Native.isolation_width;
        set => Native.isolation_width = value;
    }

    /// <summary>Lower 1 / K0 of the target's mobility range.</summary>
    public double one_over_k0_lower_limit
    {
        get => Native.one_over_k0_lower_limit;
        set => Native.one_over_k0_lower_limit = value;
    }

    /// <summary>Upper 1 / K0 of the target's mobility range.</summary>
    public double one_over_k0_upper_limit
    {
        get => Native.one_over_k0_upper_limit;
        set => Native.one_over_k0_upper_limit = value;
    }

    /// <summary>Begin of the RT window for this target (seconds).</summary>
    public double time_in_seconds_begin
    {
        get => Native.time_in_seconds_begin;
        set => Native.time_in_seconds_begin = value;
    }

    /// <summary>End of the RT window for this target (seconds).</summary>
    public double time_in_seconds_end
    {
        get => Native.time_in_seconds_end;
        set => Native.time_in_seconds_end = value;
    }

    /// <summary>Collision energy (eV); negative means "let the method decide".</summary>
    public double collision_energy
    {
        get => Native.collision_energy;
        set => Native.collision_energy = value;
    }

    /// <summary>1 / K0 of the target apex.</summary>
    public double one_over_k0
    {
        get => Native.one_over_k0;
        set => Native.one_over_k0 = value;
    }

    /// <summary>Monoisotopic m/z.</summary>
    public double monoisotopic_mz
    {
        get => Native.monoisotopic_mz;
        set => Native.monoisotopic_mz = value;
    }

    /// <summary>RT apex of the target (seconds).</summary>
    public double time_in_seconds
    {
        get => Native.time_in_seconds;
        set => Native.time_in_seconds = value;
    }

    /// <summary>Charge state.</summary>
    public int charge
    {
        get => Native.charge;
        set => Native.charge = value;
    }
}
