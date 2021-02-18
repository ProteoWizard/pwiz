using System;
using System.Threading;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    /// <summary>
    /// Manages running a job on a background thread to calculate results that will eventually be displayed in a ZedGraphControl.
    ///
    /// Displays a PaneProgressBar in the graph while the background task is running. 
    /// </summary>
    /// <typeparam name="TInput">The input data which is fed to the background task. When the input data changes, the background task is restarted.</typeparam>
    /// <typeparam name="TResults">The data which gets calculated by the background task</typeparam>
    public abstract class GraphDataCalculator<TInput, TResults>
    {
        private TInput _input;
        private Tuple<CancellationTokenSource, PaneProgressBar> _progressTuple;


        public GraphDataCalculator(CancellationToken parentCancellationToken, ZedGraphControl zedGraphControl)
        {
            ParentCancellationToken = parentCancellationToken;
            ZedGraphControl = zedGraphControl;
        }

        public CancellationToken ParentCancellationToken { get; }

        public ZedGraphControl ZedGraphControl { get; private set; }

        public virtual GraphPane GraphPane
        {
            get { return ZedGraphControl.GraphPane; }
        }

        public TInput Input
        {
            get
            {
                return _input;
            }
            set
            {
                if (Equals(Input, value))
                {
                    return;
                }
                _input = value;
                RestartCalculatorTask();
            }
        }

        public TResults Results { get; private set; }

        public void RestartCalculatorTask()
        {
            if (_progressTuple != null)
            {
                _progressTuple.Item1.Cancel();
                _progressTuple.Item2.Dispose();
                _progressTuple = null;
            }

            Results = default(TResults);
            var input = _input;
            if (Equals(input, default(TInput)))
            {
                return;
            }
            if (ParentCancellationToken.IsCancellationRequested)
            {
                return;
            }
            var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(ParentCancellationToken);
            var paneProgressBar = new PaneProgressBar(ZedGraphControl, GraphPane);
            paneProgressBar.UpdateProgressUI(0);
            _progressTuple = Tuple.Create(cancellationTokenSource, paneProgressBar);
            var cancellationToken = cancellationTokenSource.Token;
            ActionUtil.RunAsync(() =>
            {
                var results = CalculateResults(input, cancellationToken);
                if (Equals(results, default(TResults)))
                {
                    return;
                }
                BeginInvoke(cancellationToken, ()=> {
                    Assume.IsTrue(ReferenceEquals(_progressTuple.Item1, cancellationTokenSource));
                    _progressTuple.Item2.Dispose();
                    _progressTuple = null;
                    Results = results;
                    ResultsAvailable();
                });
            });
        }

        public void UpdateProgress(CancellationToken cancellationToken, int progressValue)
        {
            BeginInvoke(cancellationToken, ()=> _progressTuple?.Item2.UpdateProgressUI(progressValue));
        }

        protected Action<int> UpdateProgressAction(CancellationToken cancellationToken)
        {
            return progressValue => UpdateProgress(cancellationToken, progressValue);
        }

        protected abstract TResults CalculateResults(TInput input, CancellationToken cancellationToken);

        protected abstract void ResultsAvailable();

        protected void BeginInvoke(CancellationToken cancellationToken, Action action)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            CommonActionUtil.SafeBeginInvoke(ZedGraphControl, ()=>
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    action();
                }
            });
        }
    }
}
