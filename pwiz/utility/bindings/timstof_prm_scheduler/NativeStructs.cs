using System.Runtime.InteropServices;

namespace Pwiz.Vendor.Bruker.PrmScheduling;

/// <summary>
/// Mirrors <c>PrmMethodInfo</c> from <c>prmscheduler.h</c>. Parameters exported or generated
/// from a template acquisition method.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct PrmMethodInfo
{
    /// <summary>1 / K0 distance required between two targets so the quadrupole tunes cleanly.</summary>
    public double mobility_gap;

    /// <summary>Frames per second (constant when the tims ramp is identical across frames).</summary>
    public double frame_rate;

    /// <summary>Lower 1 / K0 measurable by the template method.</summary>
    public double one_over_k0_lower_limit;

    /// <summary>Upper 1 / K0 measurable by the template method.</summary>
    public double one_over_k0_upper_limit;
}

/// <summary>
/// Mirrors <c>PrmAdditionalMeasurementParameters</c> from <c>prmscheduler.h</c>. User parameters
/// added on top of the target list.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct PrmAdditionalMeasurementParameters
{
    /// <summary>Use the default PASEF collision energies (ramped over mobility).</summary>
    [MarshalAs(UnmanagedType.U1)]
    public bool default_pasef_collision_energies;

    /// <summary>Seconds between two MS1 frames when MS1 and MS2 compete for time.</summary>
    public double ms1_repetition_time;
}

/// <summary>
/// Mirrors <c>PrmCollisionEnergyRamp</c> from <c>prmscheduler.h</c>. Parameters for a direct-injection
/// CE-ramp experiment.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct PrmCollisionEnergyRamp
{
    /// <summary>Whether to use CE ramping at all.</summary>
    [MarshalAs(UnmanagedType.U1)]
    public bool do_collision_energy_ramping;

    /// <summary>Lowest CE in the ramp.</summary>
    public double min_collision_energy;

    /// <summary>Highest CE in the ramp.</summary>
    public double max_collision_energy;

    /// <summary>Number of steps in the ramp.</summary>
    public uint number_of_steps;

    /// <summary>Seconds spent at each step (minimum is one second).</summary>
    public double time_per_step;
}

/// <summary>
/// Mirrors <c>PrmMeasurementMode</c> from <c>prmscheduler.h</c>. Per-frame parameters when
/// targets should be measured under different modes.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct PrmMeasurementMode
{
    /// <summary>TIMS accumulation time (milliseconds).</summary>
    public double accumulation_time;
}

/// <summary>
/// Mirrors <c>PrmTimeSegments</c> from <c>prmscheduler.h</c>. A retention-time interval over
/// which the same set of frames is repeated.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct PrmTimeSegments
{
    /// <summary>Begin of this RT segment (seconds).</summary>
    public double time_in_seconds_begin;

    /// <summary>End of this RT segment (seconds).</summary>
    public double time_in_seconds_end;
}

/// <summary>
/// Mirrors <c>PrmInputTarget</c> from <c>prmscheduler.h</c>. A single PRM target row.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct PrmInputTarget
{
    /// <summary>Isolation m/z; the quadrupole tunes to this for fragmentation.</summary>
    public double isolation_mz;

    /// <summary>Total 3-dB isolation window width (m/z units).</summary>
    public double isolation_width;

    /// <summary>Lower 1 / K0 of the target's mobility range.</summary>
    public double one_over_k0_lower_limit;

    /// <summary>Upper 1 / K0 of the target's mobility range.</summary>
    public double one_over_k0_upper_limit;

    /// <summary>Begin of the RT window for this target (seconds).</summary>
    public double time_in_seconds_begin;

    /// <summary>End of the RT window for this target (seconds).</summary>
    public double time_in_seconds_end;

    /// <summary>Collision energy (eV); negative means "let the method decide".</summary>
    public double collision_energy;

    /// <summary>1 / K0 of the target apex.</summary>
    public double one_over_k0;

    /// <summary>Monoisotopic m/z.</summary>
    public double monoisotopic_mz;

    /// <summary>RT apex of the target (seconds).</summary>
    public double time_in_seconds;

    /// <summary>Charge state.</summary>
    public int charge;
}

/// <summary>
/// Mirrors <c>PrmPasefSchedulingEntry</c> from <c>prmscheduler.h</c>. One scheduling row:
/// (frame, target, time segment, measurement mode).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct PrmPasefSchedulingEntry
{
    /// <summary>Frame id (unique only within a single time segment).</summary>
    public uint frame_id;

    /// <summary>Target id (index into the input target table).</summary>
    public uint target_id;

    /// <summary>Time-segment id (index into the time-segments array).</summary>
    public uint time_segment_id;

    /// <summary>Measurement-mode id (index into the measurement-modes array).</summary>
    public uint measurement_mode_id;
}

/// <summary>
/// Mirrors <c>PrmVisualizationDataPoint</c> from <c>prmscheduler.h</c>. (x, y) value pair for
/// scheduling-metric visualization.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct PrmVisualizationDataPoint
{
    /// <summary>X value (typically RT in minutes, sometimes a target id).</summary>
    public double x;

    /// <summary>Y value (e.g. number of concurrent frames).</summary>
    public double y;
}

/// <summary>
/// Mirrors <c>PrmRetentionTimeStandard</c> from <c>prmscheduler.h</c>. Properties of an
/// RT-standard target (fragment ions live in <see cref="PrmRetentionTimeStandardFragment"/>).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct PrmRetentionTimeStandard
{
    /// <summary>Target id (index into the input target table) used by this standard.</summary>
    public uint target_id;

    /// <summary>Summed-intensity threshold across the fragment m/z ranges.</summary>
    public double intensity_threshold;

    /// <summary>Reference retention time (seconds) for this standard.</summary>
    public double reference_time_in_seconds;
}

/// <summary>
/// Mirrors <c>PrmRetentionTimeStandardFragment</c> from <c>prmscheduler.h</c>. Per-fragment
/// m/z + relative intensity for an RT standard.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct PrmRetentionTimeStandardFragment
{
    /// <summary>RT-standard id (order in which it was added, starting at 0).</summary>
    public uint retention_time_standard_id;

    /// <summary>Fragment m/z (might be monoisotopic or not).</summary>
    public double mz;

    /// <summary>Relative intensity (percent of the largest fragment of this standard).</summary>
    public double relative_intensity_percentage;
}
