namespace Pwiz.Vendor.Bruker.PrmScheduling;

/// <summary>
/// Managed wrapper for <c>PrmVisualizationDataPoint</c>. (x, y) value pair
/// returned by <see cref="Scheduler.GetSchedulingMetrics"/>.
/// </summary>
public sealed class VisualizationDataPoint
{
    private PrmVisualizationDataPoint _native;

    /// <summary>Wraps the native struct as returned by the visualization callback.</summary>
    internal VisualizationDataPoint(PrmVisualizationDataPoint native)
    {
        _native = native;
    }

    /// <summary>Construct with default zeroed parameters.</summary>
    public VisualizationDataPoint() { }

    /// <summary>X value (typically RT in minutes, sometimes a target id).</summary>
    public double x
    {
        get => _native.x;
        set => _native.x = value;
    }

    /// <summary>Y value (e.g. number of concurrent frames).</summary>
    public double y
    {
        get => _native.y;
        set => _native.y = value;
    }
}
