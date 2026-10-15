namespace Pwiz.Vendor.Bruker.PrmScheduling;

/// <summary>
/// Managed wrapper for <c>PrmMeasurementMode</c>. Per-frame parameters
/// when targets should be measured under different modes (today this is essentially just
/// TIMS accumulation time; Bruker may add more fields later).
/// </summary>
public sealed class MeasurementMode
{
    internal PrmMeasurementMode Native;

    /// <summary>Construct with default zeroed parameters.</summary>
    public MeasurementMode() { }

    /// <summary>TIMS accumulation time (milliseconds).</summary>
    public double accumulation_time
    {
        get => Native.accumulation_time;
        set => Native.accumulation_time = value;
    }
}
