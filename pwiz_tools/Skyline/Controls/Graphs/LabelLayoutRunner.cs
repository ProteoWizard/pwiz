/*
 * Original author: OpenAI Codex
 *
 * Copyright 2026 University of Washington - Seattle, WA
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using pwiz.Common.SystemUtil;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    internal sealed class LabelLayoutRunner
    {
        private const int LABEL_LAYOUT_PROGRESS_INTERVAL_MS = 200;

        private BackgroundWorker _labelLayoutWorker;
        private CancellationTokenSource _labelLayoutCts;
        private IProgressStatus _labelLayoutStatus;
        private int _labelLayoutRequestId;
        private int _labelLayoutLastProgressMs;

        private sealed class LabelLayoutWorkItem
        {
            public int RequestId;
            public GraphPane Pane;
            public List<LabeledPoint> VisiblePoints;
            public List<LabeledPoint.PointLayout> SavedLayout;
            public CancellationTokenSource Cancellation;
        }

        private sealed class LabelLayoutWorkResult
        {
            public LabelLayout LabelLayout;
            public LabelLayout.LayoutResult Result;
        }

        private sealed class WorkerProgress : IProgress<int>
        {
            private readonly BackgroundWorker _worker;
            private readonly int _requestId;
            private readonly CancellationTokenSource _cancellation;

            public WorkerProgress(BackgroundWorker worker, int requestId, CancellationTokenSource cancellation)
            {
                _worker = worker;
                _requestId = requestId;
                _cancellation = cancellation;
            }

            public void Report(int value)
            {
                if (_worker.CancellationPending || _cancellation.IsCancellationRequested)
                    return;
                try
                {
                    _worker.ReportProgress(value, _requestId);
                }
                catch (InvalidOperationException)
                {
                    // Ignore race if worker has already completed.
                }
            }
        }

        public void Start(GraphPane pane, IList<LabeledPoint> labeledPoints, IList<LabeledPoint.PointLayout> savedLayout,
            Action<List<LabeledPoint.PointLayout>> saveLayout, Action invalidate, Func<bool> isDisposed,
            IProgressMonitor progressMonitor, string statusMessage)
        {
            if (pane == null || labeledPoints == null)
                return;
            if (isDisposed != null && isDisposed())
                return;

            _labelLayoutRequestId++;
            var requestId = _labelLayoutRequestId;

            Cancel(progressMonitor);

            // DigitalRune docking panel sometimes is resized with negative chart width, which crashes the layout algorithm.
            if (!labeledPoints.Any() || pane.Chart.Rect.Width <= 0 || pane.Chart.Rect.Height <= 0)
                return;

            // Need this to make sure the coordinate transforms work correctly.
            pane.XAxis.Scale.SetupScaleData(pane, pane.XAxis);
            pane.YAxis.Scale.SetupScaleData(pane, pane.YAxis);

            var chartRect = new RectangleF((float)pane.XAxis.Scale.Min, (float)pane.YAxis.Scale.Min,
                (float)(pane.XAxis.Scale.Max - pane.XAxis.Scale.Min),
                (float)(pane.YAxis.Scale.Max - pane.YAxis.Scale.Min));
            var visiblePoints = new List<LabeledPoint>();
            foreach (var labeledPoint in labeledPoints)
            {
                if (chartRect.Contains(new PointF((float)labeledPoint.Point.X, (float)labeledPoint.Point.Y)))
                {
                    labeledPoint.Label.IsDraggable = true;
                    labeledPoint.Label.IsVisible = true;
                    visiblePoints.Add(labeledPoint);
                }
                else
                {
                    labeledPoint.Label.IsVisible = false;
                    pane.GraphObjList.Remove(labeledPoint.Connector);
                }
            }

            if (!visiblePoints.Any())
                return;

            IProgressStatus status = new ProgressStatus(string.IsNullOrEmpty(statusMessage) ? GraphsResources.LabelLayoutRunner_Start_Label_layout : statusMessage);
            _labelLayoutStatus = status;
            progressMonitor?.UpdateProgress(status);
            _labelLayoutLastProgressMs = 0;

            var workerCancellation = new CancellationTokenSource();
            _labelLayoutCts = workerCancellation;

            var worker = new BackgroundWorker { WorkerReportsProgress = true, WorkerSupportsCancellation = true };
            _labelLayoutWorker = worker;

            worker.DoWork += (s, e) =>
            {
                var workItem = (LabelLayoutWorkItem)e.Argument;
                var bw = (BackgroundWorker)s;
                using (var g = Graphics.FromHwnd(IntPtr.Zero))
                {
                    var heights = workItem.VisiblePoints.Select(p => workItem.Pane.GetRectScreen(p.Label, g).Height).ToList();
                    if (!heights.Any(h => h > 0))
                    {
                        e.Result = new LabelLayoutWorkResult();
                        return;
                    }
                    var minLabelHeight = heights.FindAll(h => h > 0).Min();
                    var labelLayout = new LabelLayout(workItem.Pane, (int)Math.Ceiling(minLabelHeight));
                    var progress = new WorkerProgress(bw, workItem.RequestId, workItem.Cancellation);

                    var result = labelLayout.ComputePlacementsSimulatedAnnealing(workItem.VisiblePoints, g, workItem.SavedLayout,
                        workItem.Cancellation.Token, progress);
                    if (bw.CancellationPending || workItem.Cancellation.IsCancellationRequested)
                    {
                        e.Cancel = true;
                        return;
                    }

                    e.Result = new LabelLayoutWorkResult
                    {
                        LabelLayout = labelLayout,
                        Result = result
                    };
                }
            };

            worker.ProgressChanged += (s, e) =>
            {
                if (!(e.UserState is int id) || id != _labelLayoutRequestId)
                    return;
                var now = Environment.TickCount;
                if (e.ProgressPercentage < 100 &&
                    _labelLayoutLastProgressMs != 0 &&
                    unchecked(now - _labelLayoutLastProgressMs) < LABEL_LAYOUT_PROGRESS_INTERVAL_MS)
                    return;
                _labelLayoutLastProgressMs = now;
                status = status.ChangePercentComplete(e.ProgressPercentage);
                _labelLayoutStatus = status;
                progressMonitor?.UpdateProgress(status);
            };

            worker.RunWorkerCompleted += (s, e) =>
            {
                if (requestId != _labelLayoutRequestId)
                {
                    workerCancellation.Dispose();
                    return;
                }
                if (isDisposed != null && isDisposed())
                {
                    workerCancellation.Dispose();
                    return;
                }

                if (e.Cancelled || workerCancellation.IsCancellationRequested)
                {
                    status = status.Cancel();
                }
                else if (e.Error != null)
                {
                    status = status.ChangeErrorException(e.Error);
                }
                else
                {
                    var result = e.Result as LabelLayoutWorkResult;
                    if (result?.Result != null)
                    {
                        using (var g = Graphics.FromHwnd(IntPtr.Zero))
                        {
                            if (pane.ApplyLabelLayout(result.LabelLayout, result.Result, g))
                            {
                                saveLayout?.Invoke(pane.Layout?.PointsLayout);
                                invalidate?.Invoke();
                            }
                        }
                    }
                    status = status.ChangePercentComplete(100);
                }

                _labelLayoutStatus = status;
                progressMonitor?.UpdateProgress(status);
                workerCancellation.Dispose();
            };

            worker.RunWorkerAsync(new LabelLayoutWorkItem
            {
                RequestId = requestId,
                Pane = pane,
                VisiblePoints = visiblePoints,
                SavedLayout = savedLayout != null ? new List<LabeledPoint.PointLayout>(savedLayout) : new List<LabeledPoint.PointLayout>(),
                Cancellation = workerCancellation
            });
        }

        public void Cancel(IProgressMonitor progressMonitor = null)
        {
            if (_labelLayoutWorker != null && _labelLayoutWorker.IsBusy)
                _labelLayoutWorker.CancelAsync();
            if (_labelLayoutCts != null)
            {
                try
                {
                    _labelLayoutCts.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // Ignore race if worker already disposed its CTS.
                }
            }
            if (_labelLayoutStatus != null)
            {
                _labelLayoutStatus = _labelLayoutStatus.Cancel();
                progressMonitor?.UpdateProgress(_labelLayoutStatus);
            }
            if (_labelLayoutWorker == null || !_labelLayoutWorker.IsBusy)
            {
                _labelLayoutCts?.Dispose();
                _labelLayoutCts = null;
            }
        }
    }
}
