namespace Pwiz.Vendor.Bruker.PrmScheduling;

/// <summary>
/// Managed wrapper for <c>PrmCollisionEnergyRamp</c>. Parameters for a
/// direct-injection collision-energy-ramp experiment.
/// </summary>
public sealed class CollisionEnergyRamp
{
    internal PrmCollisionEnergyRamp Native;

    /// <summary>Construct with default zeroed parameters.</summary>
    public CollisionEnergyRamp() { }

    /// <summary>Whether to use CE ramping at all.</summary>
    public bool do_collision_energy_ramping
    {
        get => Native.do_collision_energy_ramping;
        set => Native.do_collision_energy_ramping = value;
    }

    /// <summary>Lowest CE in the ramp.</summary>
    public double min_collision_energy
    {
        get => Native.min_collision_energy;
        set => Native.min_collision_energy = value;
    }

    /// <summary>Highest CE in the ramp.</summary>
    public double max_collision_energy
    {
        get => Native.max_collision_energy;
        set => Native.max_collision_energy = value;
    }

    /// <summary>Number of steps in the ramp.</summary>
    public uint number_of_steps
    {
        get => Native.number_of_steps;
        set => Native.number_of_steps = value;
    }

    /// <summary>Seconds spent at each step (minimum one second).</summary>
    public double time_per_step
    {
        get => Native.time_per_step;
        set => Native.time_per_step = value;
    }
}
