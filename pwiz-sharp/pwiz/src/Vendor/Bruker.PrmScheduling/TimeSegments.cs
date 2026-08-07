namespace Pwiz.Vendor.Bruker.PrmScheduling;

/// <summary>
/// Managed wrapper for <c>PrmTimeSegments</c>. A retention-time interval
/// over which the same set of frames is repeated.
/// </summary>
public sealed class TimeSegments
{
    private PrmTimeSegments _native;

    /// <summary>Wraps the native struct as returned by the scheduling callback.</summary>
    internal TimeSegments(PrmTimeSegments native)
    {
        _native = native;
    }

    /// <summary>Construct with default zeroed parameters.</summary>
    public TimeSegments() { }

    /// <summary>Begin of this RT segment (seconds).</summary>
    public double time_in_seconds_begin
    {
        get => _native.time_in_seconds_begin;
        set => _native.time_in_seconds_begin = value;
    }

    /// <summary>End of this RT segment (seconds).</summary>
    public double time_in_seconds_end
    {
        get => _native.time_in_seconds_end;
        set => _native.time_in_seconds_end = value;
    }
}
