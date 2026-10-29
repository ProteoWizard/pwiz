namespace Pwiz.Vendor.Bruker.PrmScheduling;

/// <summary>
/// Mirrors <c>PrmPasefSchedulingMetrics</c> from <c>prmscheduler.h</c>. Selects which metric
/// <see cref="Scheduler.GetSchedulingMetrics"/> should compute.
/// </summary>
public enum SchedulingMetrics : uint
{
    /// <summary>
    /// Number of frames that have to be measured concurrently in each retention-time segment,
    /// one after the other. <c>x</c> = start/end RT of each segment; <c>y</c> = count.
    /// </summary>
    CONCURRENT_FRAMES = 0,

    /// <summary>Average number of targets per frame for each retention-time segment.</summary>
    TARGETS_PER_FRAME = 1,

    /// <summary>
    /// Average times a target is measured per segment to fill otherwise unused mobility
    /// space: total fragmentations divided by unique targets.
    /// </summary>
    REDUNDANCY_OF_TARGETS = 2,

    /// <summary>Average seconds between two measurements of a target; <c>x</c> is the target id.</summary>
    MEAN_SAMPLING_TIMES = 3,

    /// <summary>Max seconds between two measurements of a target; <c>x</c> is the target id.</summary>
    MAX_SAMPLING_TIMES = 4,
}

/// <summary>
/// Progress / cancel callback for the scheduling algorithm. Return <c>true</c> to cancel.
/// </summary>
public delegate bool ProgressUpdate(double progressPercentage);
