namespace Pwiz.Vendor.Bruker.PrmScheduling;

/// <summary>
/// List of <see cref="MethodInfo"/>. Returned by <see cref="Scheduler.GetPrmMethodInfo"/>;
/// usually contains a single entry (the template), but the legacy API exposed a list and
/// Skyline's <c>FirstOrDefault</c> call site depends on the list shape.
/// </summary>
public sealed class MethodInfoList : List<MethodInfo>
{
    /// <summary>Construct an empty list.</summary>
    public MethodInfoList() { }

    /// <summary>Construct from an existing sequence.</summary>
    public MethodInfoList(IEnumerable<MethodInfo> collection) : base(collection) { }
}

/// <summary>
/// List of <see cref="PasefSchedulingEntry"/>. Filled in by <see cref="Scheduler.GetScheduling"/>.
/// </summary>
public sealed class SchedulingEntryList : List<PasefSchedulingEntry>
{
    /// <summary>Construct an empty list.</summary>
    public SchedulingEntryList() { }

    /// <summary>Construct from an existing sequence.</summary>
    public SchedulingEntryList(IEnumerable<PasefSchedulingEntry> collection) : base(collection) { }
}

/// <summary>
/// List of <see cref="TimeSegments"/>. Filled in by <see cref="Scheduler.GetScheduling"/>.
/// </summary>
public sealed class TimeSegmentList : List<TimeSegments>
{
    /// <summary>Construct an empty list.</summary>
    public TimeSegmentList() { }

    /// <summary>Construct from an existing sequence.</summary>
    public TimeSegmentList(IEnumerable<TimeSegments> collection) : base(collection) { }
}

/// <summary>
/// List of <see cref="VisualizationDataPoint"/>. Returned by
/// <see cref="Scheduler.GetSchedulingMetrics"/>.
/// </summary>
public sealed class DataPointList : List<VisualizationDataPoint>
{
    /// <summary>Construct an empty list.</summary>
    public DataPointList() { }

    /// <summary>Construct from an existing sequence.</summary>
    public DataPointList(IEnumerable<VisualizationDataPoint> collection) : base(collection) { }
}
