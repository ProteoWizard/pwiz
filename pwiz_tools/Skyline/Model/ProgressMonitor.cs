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


    public class ProgressMonitor
    {
        public int ProgressRaw => _progressRaw;
        public int MaxProgressRaw { get; private set; }
        public int ReportingStep { get; private set; }
        public IProgressBar ProgressBar { get; private set; }
        public int Step => _step;

        private int _progressSteps;
        private int _step;
        private int _progressRaw;
        private object _updateAndCheckLock = new object();

        private static ConcurrentDictionary<CancellationToken, ProgressMonitor> _monitors =
            new ConcurrentDictionary<CancellationToken, ProgressMonitor>();

        //this method must be called on the UI thread
        public static IProgressBar RegisterProgressBar(CancellationToken token, int maxProgress, int reportingStep,
            IProgressBar bar)
        {
            //collection cleanup
            _monitors.ForEach((pair) =>
            {
                if (pair.Value.ProgressBar.IsDisposed())
                    if (_monitors.TryRemove(pair.Key, out var monitor))
                        monitor.ProgressBar.Dispose();
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
                    _monitors.TryAdd(token, new ProgressMonitor(maxProgress, reportingStep, bar));
                    return bar;
                }
                else return null;
            }
        }

        public static void TerminateProgressBar(CancellationToken token)
        {
            if (_monitors.TryRemove(token, out var monitor))
            {
                monitor.ProgressBar.UIInvoke(monitor.ProgressBar.Dispose);
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
                //update the progress bar
                if (_monitors.TryGetValue(token, out ProgressMonitor monitor))
                {
                    if (monitor.Step > 0 && monitor.UpdateAndCheck())
                        monitor.UpdateProgressBar();
                }
            }
        }

        private ProgressMonitor(int maxProgress, int reportingStep, IProgressBar bar)
        {
            MaxProgressRaw = maxProgress;
            ReportingStep = reportingStep;
            ProgressBar = bar;

            _step = MaxProgressRaw / 100 * ReportingStep;
        }

        public bool UpdateAndCheck()
        {
            lock (_updateAndCheckLock)
            {
                var result = (_progressRaw == _progressSteps && ProgressRaw <= MaxProgressRaw);
                _progressRaw++;
                return result;
            }
        }

        public void UpdateProgressBar()
        {
            try
            {
                ProgressBar.UIInvoke(() =>
                    {
                        ProgressBar.UpdateProgress(_progressSteps / _step);
                        _progressSteps += _step;
                    }
                );
            }
            //It is possible that the graph is disposed by another thread during
            //  the Invoke() call. This is normal and this exception does not require
            //  any processing.
            catch (ObjectDisposedException) { }
        }
    }
}
