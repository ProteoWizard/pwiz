using System.Diagnostics;

namespace Pwiz.Util.Misc;

/// <summary>Status returned by a progress listener to continue or cancel a long-running operation.</summary>
public enum IterationStatus
{
    /// <summary>Continue the operation.</summary>
    Ok,
    /// <summary>Request cancellation of the operation.</summary>
    Cancel,
}

/// <summary>A progress update delivered to <see cref="IIterationListener"/>s.</summary>
public readonly record struct IterationUpdate
{
    /// <summary>Zero-based index of the current iteration. Never exceeds <see cref="IterationCount"/> - 1 when the count is known.</summary>
    public int IterationIndex { get; }

    /// <summary>Total number of iterations expected, or 0 if unknown.</summary>
    public int IterationCount { get; }

    /// <summary>Optional description of the current step (e.g. "Reading spectrum 1234").</summary>
    public string Message { get; }

    /// <summary>Constructs an update, clamping <paramref name="index"/> when a finite count is given.</summary>
    public IterationUpdate(int index, int count, string? message = null)
    {
        IterationCount = count;
        IterationIndex = count > 0 && index >= count ? count - 1 : index;
        Message = message ?? string.Empty;
    }
}

/// <summary>
/// Interface for receiving progress callbacks from long-running operations.
/// Port of pwiz::util::IterationListener.
/// </summary>
public interface IIterationListener
{
    /// <summary>Called with a progress update; return <see cref="IterationStatus.Cancel"/> to request cancellation.</summary>
    IterationStatus Update(IterationUpdate message);
}

/// <summary>
/// Manages registration of <see cref="IIterationListener"/>s and broadcasts update messages,
/// respecting per-listener iteration-count or time-based throttling.
/// </summary>
/// <remarks>Port of pwiz::util::IterationListenerRegistry.</remarks>
public sealed class IterationListenerRegistry
{
    private readonly List<Subscription> _subs = new();

    /// <summary>Registers <paramref name="listener"/> to receive updates every <paramref name="iterationPeriod"/> iterations.</summary>
    public void AddListener(IIterationListener listener, int iterationPeriod)
    {
        ArgumentNullException.ThrowIfNull(listener);
        ArgumentOutOfRangeException.ThrowIfLessThan(iterationPeriod, 1);
        _subs.Add(new Subscription(listener, iterationPeriod, timeBased: false, TimeSpan.Zero));
    }

    /// <summary>Registers <paramref name="listener"/> to receive updates approximately every <paramref name="timePeriod"/>.</summary>
    public void AddListenerWithTimer(IIterationListener listener, TimeSpan timePeriod)
    {
        ArgumentNullException.ThrowIfNull(listener);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timePeriod, TimeSpan.Zero);
        _subs.Add(new Subscription(listener, 0, timeBased: true, timePeriod));
    }

    /// <summary>Unregisters <paramref name="listener"/>.</summary>
    public void RemoveListener(IIterationListener listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        _subs.RemoveAll(s => ReferenceEquals(s.Listener, listener));
    }

    /// <summary>
    /// Delivers an update to all registered listeners (subject to throttling) and returns the
    /// combined status. If any listener requests cancellation, the result is <see cref="IterationStatus.Cancel"/>.
    /// </summary>
    public IterationStatus Broadcast(IterationUpdate update)
    {
        var result = IterationStatus.Ok;
        foreach (var sub in _subs)
        {
            bool isLast = update.IterationCount > 0 && update.IterationIndex == update.IterationCount - 1;
            bool shouldDeliver;

            if (sub.TimeBased)
            {
                shouldDeliver = isLast || sub.ElapsedSinceLast >= sub.TimePeriod;
            }
            else
            {
                shouldDeliver = isLast || (update.IterationIndex % sub.IterationPeriod == 0);
            }

            if (!shouldDeliver) continue;

            var status = sub.Listener.Update(update);
            sub.ResetClock();
            if (status == IterationStatus.Cancel) result = IterationStatus.Cancel;
        }
        return result;
    }

    private sealed class Subscription
    {
        public IIterationListener Listener { get; }
        public int IterationPeriod { get; }
        public bool TimeBased { get; }
        public TimeSpan TimePeriod { get; }

        private readonly Stopwatch _stopwatch = new();

        public Subscription(IIterationListener listener, int iterationPeriod, bool timeBased, TimeSpan timePeriod)
        {
            Listener = listener;
            IterationPeriod = iterationPeriod;
            TimeBased = timeBased;
            TimePeriod = timePeriod;
            _stopwatch.Start();
        }

        public TimeSpan ElapsedSinceLast => _stopwatch.Elapsed;
        public void ResetClock() => _stopwatch.Restart();
    }
}
