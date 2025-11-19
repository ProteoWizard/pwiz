using pwiz.Common.Collections;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace pwiz.Skyline.Model
{
    public interface IProgressBar : IDisposable
    {
        bool IsDisposed();
        void UpdateProgress(int progress);
        void UIInvoke(Action act);
    }

    public class ProgressMonitor : IDisposable
    {
        // ReSharper disable once InconsistentlySynchronizedField
        public int MaxProgressRaw { get; private set; }
        public int ReportingIntervalMs { get; private set; }
        public IProgressBar ProgressBar { get; private set; }

        private int _progressRaw;
        private object _updateLock = new object();
        private Timer _reportingTimer;
        private bool _disposed;

        private static ConcurrentDictionary<CancellationToken, ProgressMonitor> _monitors =
            new ConcurrentDictionary<CancellationToken, ProgressMonitor>();

        //this method must be called on the UI thread
        public static IProgressBar RegisterProgressBar(CancellationToken token, int maxProgress, int reportingIntervalMs,
            IProgressBar bar)
        {
            //collection cleanup
            _monitors.ForEach((pair) =>
            {
                if (pair.Value.ProgressBar.IsDisposed())
                    if (_monitors.TryRemove(pair.Key, out var monitor))
                        monitor.Dispose();
            });

            if (token.IsCancellationRequested)
            {
                TerminateProgressBar(token);
                return null;
            }
            else
            {
                if (_monitors.TryGetValue(token, out ProgressMonitor monitor))
                {
                    if (monitor.ProgressBar.IsDisposed())
                        TerminateProgressBar(token);
                    else
                        return monitor.ProgressBar;
                }

                if (maxProgress >= 100)
                {
                    var newMonitor = new ProgressMonitor(maxProgress, reportingIntervalMs, bar);
                    _monitors.TryAdd(token, newMonitor);
                    return bar;
                }
                else return null;
            }
        }

        public static void TerminateProgressBar(CancellationToken token)
        {
            if (_monitors.TryRemove(token, out var monitor))
            {
                monitor.ProgressBar.UIInvoke(() => monitor.Dispose());
            }
        }

        public static void CheckCanceled(CancellationToken token)
        {
            if (token.IsCancellationRequested)
            {
                TerminateProgressBar(token);
                throw new OperationCanceledException();
            }
            else
            {
                // Increment the progress counter
                if (_monitors.TryGetValue(token, out ProgressMonitor monitor))
                {
                    monitor.IncrementProgress();
                }
            }
        }

        private ProgressMonitor(int maxProgress, int reportingIntervalMs, IProgressBar bar)
        {
            MaxProgressRaw = maxProgress;
            ReportingIntervalMs = reportingIntervalMs;
            ProgressBar = bar;
            _progressRaw = 0;

            // Start the timer for periodic progress updates
            _reportingTimer = new Timer(TimerCallback, null, reportingIntervalMs, reportingIntervalMs);
        }

        private void IncrementProgress()
        {
            lock (_updateLock)
            {
                if (_progressRaw < MaxProgressRaw)
                {
                    _progressRaw++;
                }
            }
        }

        private void TimerCallback(object state)
        {
            if (_disposed || ProgressBar.IsDisposed())
                return;

            try
            {
                int currentProgress;
                lock (_updateLock)
                {
                    currentProgress = _progressRaw;
                }

                // Calculate percentage (0-100)
                int progressPercentage = Math.Min(100, (currentProgress * 100) / MaxProgressRaw);

                ProgressBar.UIInvoke(() =>
                {
                    if (!_disposed && !ProgressBar.IsDisposed())
                    {
                        ProgressBar.UpdateProgress(progressPercentage);
                    }
                });
            }
            catch (ObjectDisposedException)
            {
                // Progress bar was disposed, stop the timer
                Dispose();
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _reportingTimer?.Dispose();
                _reportingTimer = null;

                try
                {
                    ProgressBar?.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // Already disposed, ignore
                }
            }
        }
    }
}
