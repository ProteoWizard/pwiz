namespace Pwiz.Vendor.Bruker.PrmScheduling;

/// <summary>
/// Managed wrapper for <c>PrmAdditionalMeasurementParameters</c>. User
/// parameters added on top of the target list.
/// </summary>
public sealed class AdditionalMeasurementParameters
{
    internal PrmAdditionalMeasurementParameters Native;

    /// <summary>Construct with default zeroed parameters.</summary>
    public AdditionalMeasurementParameters() { }

    /// <summary>Use the default PASEF collision energies (ramped over mobility).</summary>
    public bool default_pasef_collision_energies
    {
        get => Native.default_pasef_collision_energies;
        set => Native.default_pasef_collision_energies = value;
    }

    /// <summary>Seconds between two MS1 frames when MS1 and MS2 compete for time.</summary>
    public double ms1_repetition_time
    {
        get => Native.ms1_repetition_time;
        set => Native.ms1_repetition_time = value;
    }
}
