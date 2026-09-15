using System.Runtime.InteropServices;

namespace Pwiz.Vendor.Bruker.PrmScheduling;

/// <summary>
/// P/Invoke surface for Bruker's <c>prmscheduler.dll</c> — the C "mini-API" used to
/// compute PRM-PASEF target scheduling against a .prmsqlite template. Structure and
/// signatures match <c>prmscheduler.h</c> from Bruker's timsTOF SDK.
/// </summary>
/// <remarks>
/// The x64 <c>prmscheduler.dll</c> is shipped from the pwiz repo at
/// <c>pwiz/utility/bindings/CLI/timstof_prm_scheduler/x64/</c> and is copied next to the
/// output assembly by <c>Bruker.PrmScheduling.csproj</c>'s Content item.
/// <para>
/// All scheduling functions take a <c>ulong</c> handle returned by
/// <see cref="prm_scheduling_file_open"/> and return <c>1</c> on success / <c>0</c> on
/// failure; the thread-local last-error string is fetched via
/// <see cref="prm_scheduling_get_last_error_string"/>. Strings cross the boundary as
/// UTF-8 (<c>const char*</c>); booleans cross as one-byte values.
/// </para>
/// </remarks>
internal static class NativeMethods
{
    /// <summary>DLL name. Shipped from the pwiz repo (see csproj Content item); the OS
    /// loader resolves <c>prmscheduler.dll</c> from the assembly's output directory.</summary>
    public const string PrmSchedulerDll = "prmscheduler";

    /// <summary>
    /// Callback delegate: receives a single <see cref="PrmMethodInfo"/> by pointer.
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void PrmMethodInfoFunction(
        ref PrmMethodInfo prm_method_info,
        IntPtr user_data);

    /// <summary>
    /// Callback delegate: receives an array of <see cref="PrmTimeSegments"/>.
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void PrmTimeSegmentsFunction(
        uint num_entries,
        IntPtr prm_time_segments,
        IntPtr user_data);

    /// <summary>
    /// Callback delegate: receives an array of <see cref="PrmPasefSchedulingEntry"/>.
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void PrmPasefSchedulingEntryFunction(
        uint num_entries,
        IntPtr prm_pasef_scheduling_entries,
        IntPtr user_data);

    /// <summary>
    /// Callback delegate: receives an array of <see cref="PrmVisualizationDataPoint"/>.
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void PrmVisualizationPointsFunction(
        uint num_entries,
        IntPtr data_points,
        IntPtr user_data);

    /// <summary>
    /// Progress / cancel callback: return <c>true</c> to cancel calculation.
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    public delegate bool PrmProgressCancelFunction(
        double progress_percentage,
        IntPtr user_data_progress);

    /// <summary>Open the prmsqlite file and prepare for scheduling. Returns 0 on error.</summary>
    [DllImport(PrmSchedulerDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern ulong prm_scheduling_file_open(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string scheduling_file_name);

    /// <summary>Close the data set + free memory. Passing 0 is a no-op.</summary>
    [DllImport(PrmSchedulerDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void prm_scheduling_file_close(ulong handle);

    /// <summary>Copy the last error string into <paramref name="buf"/>. Returns required length.</summary>
    [DllImport(PrmSchedulerDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern uint prm_scheduling_get_last_error_string(
        byte[] buf,
        uint actual_buffer_size);

    /// <summary>Get the <see cref="PrmMethodInfo"/> data via a callback. Returns 0 on error.</summary>
    [DllImport(PrmSchedulerDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern uint prm_scheduling_get_method_info(
        ulong handle,
        PrmMethodInfoFunction callback,
        IntPtr user_data);

    /// <summary>Set additional measurement parameters for scheduling. Returns 0 on error.</summary>
    [DllImport(PrmSchedulerDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern uint prm_scheduling_set_additional_measurement_parameters(
        ulong handle,
        ref PrmAdditionalMeasurementParameters parameters);

    /// <summary>Set collision-energy-ramp parameters for direct-injection mode. Returns 0 on error.</summary>
    [DllImport(PrmSchedulerDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern uint prm_scheduling_set_collision_energy_ramp_parameters(
        ulong handle,
        ref PrmCollisionEnergyRamp parameters);

    /// <summary>Add a single input target. Returns 0 on error.</summary>
    [DllImport(PrmSchedulerDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern uint prm_add_input_target(
        ulong handle,
        ref PrmInputTarget prm_input_target,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? external_id,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? description);

    /// <summary>Add a single measurement mode. Returns 0 on error.</summary>
    [DllImport(PrmSchedulerDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern uint prm_add_measurement_mode(
        ulong handle,
        ref PrmMeasurementMode prm_measurement_mode,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? external_id);

    /// <summary>Run the scheduling algorithm with a progress/cancel callback. Returns 0 on error/cancel.</summary>
    [DllImport(PrmSchedulerDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern uint prm_scheduling_prm_targets(
        ulong handle,
        PrmProgressCancelFunction callback_progress_cancel,
        IntPtr user_data_progress);

    /// <summary>Get the number of scheduling entries (call after <see cref="prm_scheduling_prm_targets"/>).</summary>
    [DllImport(PrmSchedulerDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern uint prm_get_num_scheduling_entries(ulong handle);

    /// <summary>Get the number of time segments (call after <see cref="prm_scheduling_prm_targets"/>).</summary>
    [DllImport(PrmSchedulerDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern uint prm_get_num_time_segments(ulong handle);

    /// <summary>Receive the scheduling entries + time segments via callbacks. Returns 0 on error.</summary>
    [DllImport(PrmSchedulerDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern uint prm_get_scheduling(
        ulong handle,
        PrmPasefSchedulingEntryFunction callback_scheduling_entries,
        PrmTimeSegmentsFunction callback_time_segments,
        IntPtr user_data_scheduling_entry,
        IntPtr user_data_time_segments_entry);

    /// <summary>Persist the scheduling results to the open .prmsqlite. Returns 0 on error.</summary>
    [DllImport(PrmSchedulerDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern uint prm_write_scheduling(ulong handle);

    /// <summary>Compute a visualization metric and return the data-point count (0 on error).</summary>
    [DllImport(PrmSchedulerDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern uint prm_calculate_visualization(
        ulong handle,
        uint pasef_scheduling_metric);

    /// <summary>Receive the visualization data points via a callback. Returns 0 on error.</summary>
    [DllImport(PrmSchedulerDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern uint prm_get_visualization(
        ulong handle,
        PrmVisualizationPointsFunction callback_visualization_points,
        IntPtr user_data_visualization_points);

    /// <summary>Add a retention-time standard. Returns 0 on error.</summary>
    [DllImport(PrmSchedulerDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern uint prm_add_retention_time_standard(
        ulong handle,
        ref PrmRetentionTimeStandard retention_time_standard);

    /// <summary>Add a fragment ion for a retention-time standard. Returns 0 on error.</summary>
    [DllImport(PrmSchedulerDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern uint prm_add_retention_time_standard_fragment(
        ulong handle,
        ref PrmRetentionTimeStandardFragment retention_time_standard_fragment);
}
