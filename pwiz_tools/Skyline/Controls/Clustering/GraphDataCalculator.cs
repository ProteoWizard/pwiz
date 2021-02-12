using System;
using System.Threading;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using ZedGraph;

namespace pwiz.Skyline.Controls.Clustering
{
    /// <summary>
    /// Manages running a job on a background thread to calculate results that will eventually be displayed in a ZedGraphControl.
    ///
    /// Displays a PaneProgressBar in the graph while the background task is running. 
    /// </summary>
    /// <typeparam name="TInput">The input data which is fed to the background task. When the input data changes, the background task is restarted.</typeparam>
    /// <typeparam name="TOutput">The data which gets calculated by the background task</typeparam>
    public abstract class GraphDataCalculator<TInput, TOutput>
    {
        private TInput _input;
        private Tuple<CancellationTokenSource, PaneProgressBar> _progressTuple;


        public GraphDataCalculator(ZedGraphControl zedGraphControl)
        {
            ZedGraphControl = zedGraphControl;
        }

        public ZedGraphControl ZedGraphControl { get; private set; }

        public virtual GraphPane GraphPane
        {
            get { return ZedGraphControl.GraphPane; }
        }

        public virtual string LoadingMessage
        {
            get
            {
                return "Calculating...";
            }
        }

        public TInput Input
        {
            get
            {
                return _input;
            }
            set
            {
                _input = value;
                RestartCalculatorTask();
            }
        }

        public void RestartCalculatorTask()
        {
            if (_progressTuple != null)
            {
                _progressTuple.Item1.Cancel();
                _progressTuple.Item2.Dispose();
                _progressTuple = null;
            }
            var input = _input;
            if (Equals(input, default(TInput)))
            {
                return;
            }

            var cancellationTokenSource = new CancellationTokenSource();
            GraphPane.Title.IsVisible = true;
            GraphPane.Title.Text = LoadingMessage;
            var paneProgressBar = new PaneProgressBar(ZedGraphControl, GraphPane);
            paneProgressBar.UpdateProgressUI(0);
            _progressTuple = Tuple.Create(cancellationTokenSource, paneProgressBar);
            var cancellationToken = cancellationTokenSource.Token;
            ActionUtil.RunAsync(() =>
            {
                var output = ComputeOutput(input, cancellationToken);
                if (cancellationToken.IsCancellationRequested || Equals(output, default(TOutput)))
                {
                    return;
                }

                CommonActionUtil.SafeBeginInvoke(ZedGraphControl, () =>
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    Assume.IsTrue(ReferenceEquals(_progressTuple.Item1, cancellationTokenSource));
                    _progressTuple.Item2.Dispose();
                    _progressTuple = null;
                    SetOutput(output);
                });
            });
        }

        public void UpdateProgress(CancellationToken cancellationToken, int progressValue)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                // If the task has been cancelled, then we must ignore the progress update
                // since the PaneProgressBar that it was associated with was already destroyed
                return;
            }

            CommonActionUtil.SafeBeginInvoke(ZedGraphControl, () =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                _progressTuple?.Item2.UpdateProgressUI(progressValue);
            });
        }

        protected Action<int> UpdateProgressAction(CancellationToken cancellationToken)
        {
            return progressValue => UpdateProgress(cancellationToken, progressValue);
        }

        protected abstract TOutput ComputeOutput(TInput input, CancellationToken cancellationToken);

        protected abstract void SetOutput(TOutput output);
    }
}
