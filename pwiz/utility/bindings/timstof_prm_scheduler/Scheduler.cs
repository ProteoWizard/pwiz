using System.Runtime.InteropServices;
using System.Text;

namespace Pwiz.Vendor.Bruker.PrmScheduling;

/// <summary>
/// High-level handle owner for a .prmsqlite scheduling session. Open with the constructor,
/// add targets, run scheduling, then dispose to free the native handle. Mirrors the legacy
/// C++/CLI <c>pwiz.CLI.Bruker.PrmScheduling.Scheduler</c> surface so Skyline call sites
/// (<see cref="Scheduler"/>, <c>using (var s = new Scheduler(...))</c>, etc.) port across.
/// </summary>
/// <remarks>
/// Callback delegates passed to native are kept alive by storing them in instance fields
/// while the call runs. The native API is synchronous from the managed side: every call
/// returns before its delegate is collectable.
/// </remarks>
public sealed class Scheduler : IDisposable
{
    private ulong _handle;
    private bool _disposed;

    /// <summary>
    /// Open the .prmsqlite file. Throws <see cref="InvalidOperationException"/> if the native
    /// open fails (the message is the thread-local last-error string).
    /// </summary>
    /// <param name="schedulingFileName">UTF-8 path to a .prmsqlite (extensionless / wrong-ext is
    /// normalized to .prmsqlite by the native side).</param>
    public Scheduler(string schedulingFileName)
    {
        ArgumentNullException.ThrowIfNull(schedulingFileName);
        _handle = NativeMethods.prm_scheduling_file_open(schedulingFileName);
        if (_handle == 0)
            throw new InvalidOperationException(GetLastErrorString());
    }

    /// <summary>Closes the native handle. Idempotent.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_handle != 0)
        {
            NativeMethods.prm_scheduling_file_close(_handle);
            _handle = 0;
        }
        GC.SuppressFinalize(this);
    }

    /// <summary>Finalizer; calls <see cref="Dispose"/>. Native handle is process-global so
    /// failing to dispose risks leaks across long-lived hosts.</summary>
    ~Scheduler()
    {
        if (_handle != 0)
            NativeMethods.prm_scheduling_file_close(_handle);
    }

    /// <summary>Underlying native handle (zero after dispose).</summary>
    internal ulong Handle => _handle;

    /// <summary>Last error string from the native side (thread-local).</summary>
    public static string GetLastErrorString()
    {
        // First call with a zero-length buffer returns the required size; allocate and refetch.
        var initial = new byte[1024];
        uint size = NativeMethods.prm_scheduling_get_last_error_string(initial, (uint)initial.Length);
        if (size == 0) return string.Empty;
        if (size <= initial.Length)
        {
            // Trim the trailing NUL (the native side reports size INCLUDING the terminator).
            int len = (int)size - 1;
            if (len < 0) len = 0;
            return Encoding.UTF8.GetString(initial, 0, len);
        }
        // Buffer too small; allocate and try again.
        var buf = new byte[size];
        size = NativeMethods.prm_scheduling_get_last_error_string(buf, (uint)buf.Length);
        int len2 = (int)size - 1;
        if (len2 < 0) len2 = 0;
        return Encoding.UTF8.GetString(buf, 0, len2);
    }

    /// <summary>
    /// Get <see cref="MethodInfo"/> for the open template. Native API uses a callback that may
    /// emit zero or more entries; we collect them into a list.
    /// </summary>
    public MethodInfoList GetPrmMethodInfo()
    {
        var list = new MethodInfoList();
        NativeMethods.PrmMethodInfoFunction cb = (ref PrmMethodInfo native, IntPtr _) =>
        {
            list.Add(new MethodInfo(native));
        };
        // Return value is the success flag (1 on success, 0 on error). The list is empty in
        // the error case and the caller can interpret that however they like; the legacy
        // C++/CLI binding ignored the flag here, so we preserve that.
        _ = NativeMethods.prm_scheduling_get_method_info(_handle, cb, IntPtr.Zero);
        GC.KeepAlive(cb);
        return list;
    }

    /// <summary>Set the additional measurement parameters (MS1 repetition time, etc.).</summary>
    public void SetAdditionalMeasurementParameters(AdditionalMeasurementParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        if (NativeMethods.prm_scheduling_set_additional_measurement_parameters(_handle, ref parameters.Native) == 0)
            throw new InvalidOperationException(GetLastErrorString());
    }

    /// <summary>Set the collision-energy-ramp parameters (direct-injection mode).</summary>
    public void SetCollisionEnergyRampParameters(CollisionEnergyRamp parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        if (NativeMethods.prm_scheduling_set_collision_energy_ramp_parameters(_handle, ref parameters.Native) == 0)
            throw new InvalidOperationException(GetLastErrorString());
    }

    /// <summary>Add a single input target.</summary>
    public void AddInputTarget(InputTarget inputTarget, string? externalId, string? description)
    {
        ArgumentNullException.ThrowIfNull(inputTarget);
        if (NativeMethods.prm_add_input_target(_handle, ref inputTarget.Native, externalId ?? string.Empty, description ?? string.Empty) == 0)
            throw new InvalidOperationException(GetLastErrorString());
    }

    /// <summary>Add a single measurement mode (optional).</summary>
    public void AddMeasurementMode(MeasurementMode measurementMode, string? externalId)
    {
        ArgumentNullException.ThrowIfNull(measurementMode);
        if (NativeMethods.prm_add_measurement_mode(_handle, ref measurementMode.Native, externalId ?? string.Empty) == 0)
            throw new InvalidOperationException(GetLastErrorString());
    }

    /// <summary>
    /// Run the scheduling algorithm, then fill <paramref name="timeSegmentList"/> and
    /// <paramref name="schedulingEntryList"/> with the results. The
    /// <paramref name="progressUpdate"/> delegate is invoked periodically; return <c>true</c> to
    /// cancel.
    /// </summary>
    public void GetScheduling(TimeSegmentList timeSegmentList, SchedulingEntryList schedulingEntryList, ProgressUpdate progressUpdate)
    {
        ArgumentNullException.ThrowIfNull(timeSegmentList);
        ArgumentNullException.ThrowIfNull(schedulingEntryList);
        ArgumentNullException.ThrowIfNull(progressUpdate);

        NativeMethods.PrmProgressCancelFunction progressCb = (pct, _) => progressUpdate(pct);

        if (NativeMethods.prm_scheduling_prm_targets(_handle, progressCb, IntPtr.Zero) == 0)
        {
            var err = GetLastErrorString();
            // The legacy C++/CLI Scheduler swallows "user request" cancellations and just
            // returns empty lists. Preserve that behaviour.
            if (!err.Contains("user request", StringComparison.Ordinal))
            {
                GC.KeepAlive(progressCb);
                throw new InvalidOperationException(err);
            }
            GC.KeepAlive(progressCb);
            return;
        }
        GC.KeepAlive(progressCb);

        // Capture the entries via callbacks.
        timeSegmentList.Clear();
        schedulingEntryList.Clear();

        NativeMethods.PrmPasefSchedulingEntryFunction entriesCb = (num, ptr, _) =>
        {
            for (uint i = 0; i < num; i++)
            {
                var entry = Marshal.PtrToStructure<PrmPasefSchedulingEntry>(
                    IntPtr.Add(ptr, (int)(i * Marshal.SizeOf<PrmPasefSchedulingEntry>())));
                schedulingEntryList.Add(new PasefSchedulingEntry(entry));
            }
        };

        NativeMethods.PrmTimeSegmentsFunction segsCb = (num, ptr, _) =>
        {
            for (uint i = 0; i < num; i++)
            {
                var seg = Marshal.PtrToStructure<PrmTimeSegments>(
                    IntPtr.Add(ptr, (int)(i * Marshal.SizeOf<PrmTimeSegments>())));
                timeSegmentList.Add(new TimeSegments(seg));
            }
        };

        if (NativeMethods.prm_get_scheduling(_handle, entriesCb, segsCb, IntPtr.Zero, IntPtr.Zero) == 0)
        {
            GC.KeepAlive(entriesCb);
            GC.KeepAlive(segsCb);
            throw new InvalidOperationException(GetLastErrorString());
        }
        GC.KeepAlive(entriesCb);
        GC.KeepAlive(segsCb);
    }

    /// <summary>
    /// Compute one of the visualization metrics over the current scheduling results.
    /// </summary>
    public DataPointList GetSchedulingMetrics(SchedulingMetrics metric)
    {
        uint numPoints = NativeMethods.prm_calculate_visualization(_handle, (uint)metric);
        if (numPoints == 0)
            throw new InvalidOperationException(GetLastErrorString());

        var list = new DataPointList();
        NativeMethods.PrmVisualizationPointsFunction cb = (num, ptr, _) =>
        {
            for (uint i = 0; i < num; i++)
            {
                var pt = Marshal.PtrToStructure<PrmVisualizationDataPoint>(
                    IntPtr.Add(ptr, (int)(i * Marshal.SizeOf<PrmVisualizationDataPoint>())));
                list.Add(new VisualizationDataPoint(pt));
            }
        };

        if (NativeMethods.prm_get_visualization(_handle, cb, IntPtr.Zero) == 0)
        {
            GC.KeepAlive(cb);
            throw new InvalidOperationException(GetLastErrorString());
        }
        GC.KeepAlive(cb);
        return list;
    }

    /// <summary>Persist the scheduling results to the open .prmsqlite.</summary>
    public void WriteScheduling()
    {
        if (NativeMethods.prm_write_scheduling(_handle) == 0)
            throw new InvalidOperationException(GetLastErrorString());
    }
}
