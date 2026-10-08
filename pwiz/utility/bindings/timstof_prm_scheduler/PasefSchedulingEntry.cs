namespace Pwiz.Vendor.Bruker.PrmScheduling;

/// <summary>
/// Managed wrapper for <c>PrmPasefSchedulingEntry</c>. One scheduling
/// row returned by the scheduler: (frame, target, time segment, measurement mode).
/// </summary>
public sealed class PasefSchedulingEntry
{
    private PrmPasefSchedulingEntry _native;

    /// <summary>Wraps the native struct as returned by the scheduling callback.</summary>
    internal PasefSchedulingEntry(PrmPasefSchedulingEntry native)
    {
        _native = native;
    }

    /// <summary>Construct with default zeroed parameters.</summary>
    public PasefSchedulingEntry() { }

    /// <summary>Frame id (unique only within a single time segment).</summary>
    public uint frame_id
    {
        get => _native.frame_id;
        set => _native.frame_id = value;
    }

    /// <summary>Target id (index into the input target table).</summary>
    public uint target_id
    {
        get => _native.target_id;
        set => _native.target_id = value;
    }

    /// <summary>Time-segment id (index into the time-segments array).</summary>
    public uint time_segment_id
    {
        get => _native.time_segment_id;
        set => _native.time_segment_id = value;
    }

    /// <summary>Measurement-mode id (index into the measurement-modes array).</summary>
    public uint measurement_mode_id
    {
        get => _native.measurement_mode_id;
        set => _native.measurement_mode_id = value;
    }
}
