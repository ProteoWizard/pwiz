/*
 * Original author: Rita Chupalov <ritach .at. uw.edu>,
   *                  MacCoss Lab, Department of Genome Sciences, UW
 * Co-authored: OpenAI Codex
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
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    internal sealed class LabelLayoutRunner
    {
        private const int LABEL_LAYOUT_PROGRESS_INTERVAL_MS = 200;
        private const int LABEL_LAYOUT_DEBOUNCE_MS = 100;
        private const int MAX_LABELS_PER_CELL = 2;
        private const double MAX_LABEL_AREA_RATIO = 0.3;

        private BackgroundWorker _labelLayoutWorker;
        private CancellationTokenSource _labelLayoutCts;
        private IProgressStatus _labelLayoutStatus;
        private int _labelLayoutRequestId;
        private int _labelLayoutLastProgressMs;
        private System.Windows.Forms.Timer _labelLayoutDebounceTimer;
        private LabelLayoutRequest _pendingRequest;

        private sealed class LabelLayoutWorkItem
        {
            public int RequestId;
            public GraphPane Pane;
            public LabelLayout LabelLayout;
            public List<LabeledPoint> VisiblePoints;
            public List<LabeledPoint.PointLayout> SavedLayout;
            public CancellationTokenSource Cancellation;
        }

        private sealed class LabelLayoutWorkResult
        {
            public LabelLayout LabelLayout;
            public LabelLayout.LayoutResult Result;
        }

        private sealed class LabelLayoutRequest
        {
            public int RequestId;
            public GraphPane Pane;
            public IList<LabeledPoint> LabeledPoints;
            public IList<LabeledPoint.PointLayout> SavedLayout;
            public Action<List<LabeledPoint.PointLayout>> SaveLayout;
            public Action Invalidate;
            public Func<bool> IsDisposed;
            public IProgressMonitor ProgressMonitor;
            public string StatusMessage;
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

        private sealed class SampleInfo
        {
            public LabeledPoint Point { get; }
            public uint Hash { get; }

            public SampleInfo(LabeledPoint point, uint hash)
            {
                Point = point;
                Hash = hash;
            }
        }

        private static uint StableHash(string text)
        {
            unchecked
            {
                const uint fnvOffset = 2166136261;
                const uint fnvPrime = 16777619;
                uint hash = fnvOffset;
                if (!string.IsNullOrEmpty(text))
                {
                    foreach (var ch in text)
                    {
                        hash ^= ch;
                        hash *= fnvPrime;
                    }
                }
                return hash;
            }
        }

        private static string GetSampleKey(LabeledPoint point)
        {
            var text = point.Label?.Text ?? string.Empty;
            return text;
        }

        private static List<LabeledPoint> SamplePointsByDensityGrid(LabelLayout labelLayout, GraphPane pane, List<LabeledPoint> points)
        {
            if (points == null || points.Count == 0)
                return points;

            if (labelLayout == null)
                return points;

            // Calculate point positions in the density grid and find the maximum hash for normalization.
            var pointCells = new List<Tuple<SampleInfo, Point>>();
            var alwaysKeep = new HashSet<LabeledPoint>();
            var maxHash = uint.MinValue;
            foreach (var point in points)
            {
                var pix = new PointF(pane.XAxis.Scale.Transform(point.Point.X),
                    pane.YAxis.Scale.Transform(point.Point.Y));
                var cellIndexPoint = labelLayout.CellIndexesFromXY(pix);
                if (!labelLayout.IndexesWithinGrid(cellIndexPoint))
                {
                    alwaysKeep.Add(point);
                    continue;
                }
                pointCells.Add(new Tuple<SampleInfo, Point>(new SampleInfo(point, StableHash(GetSampleKey(point))), cellIndexPoint));
                if (pointCells.Last().Item1.Hash > maxHash)
                    maxHash = pointCells.Last().Item1.Hash;
            }

            var graphArea = pane.Chart.Rect.Height * pane.Chart.Rect.Width;
            var areaSamplingRate = graphArea / points.Average(p => p.LabelArea) * MAX_LABEL_AREA_RATIO / pointCells.Count;
            areaSamplingRate = Math.Min(1.0, areaSamplingRate);

            var keep = new HashSet<LabeledPoint>(alwaysKeep);
            foreach (var cellEntry in pointCells)
            {
                var cellpoPointCount = labelLayout.CellFromPoint(cellEntry.Item2)._pointCount;
                var cutoff = cellpoPointCount > MAX_LABELS_PER_CELL ? MAX_LABELS_PER_CELL/ (double)cellpoPointCount : 1.0;

                if (cellEntry.Item1.Point.IsSelected || (double)cellEntry.Item1.Hash / maxHash <= (cutoff * areaSamplingRate))
                {
                    keep.Add(cellEntry.Item1.Point);
                }
            }

            if (keep.Count == points.Count)
                return points;

            var result = new List<LabeledPoint>();
            foreach (var point in points)
            {
                if (keep.Contains(point))
                    result.Add(point);
            }
            return result;
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

            CancelInFlight(progressMonitor);

            // DigitalRune docking panel sometimes is resized with negative chart width, which crashes the layout algorithm.
            if (!labeledPoints.Any() || pane.Chart.Rect.Width <= 0 || pane.Chart.Rect.Height <= 0)
                return;

            _pendingRequest = new LabelLayoutRequest
            {
                RequestId = requestId,
                Pane = pane,
                LabeledPoints = labeledPoints,
                SavedLayout = savedLayout,
                SaveLayout = saveLayout,
                Invalidate = invalidate,
                IsDisposed = isDisposed,
                ProgressMonitor = progressMonitor,
                StatusMessage = statusMessage
            };

            // ZedGraph tends to fire multiple Zoom events for a single mouse wheel movement, which causes multiple redundant layout runs. Use a debounce timer to avoid this.
            EnsureDebounceTimer();
            _labelLayoutDebounceTimer.Stop();
            _labelLayoutDebounceTimer.Start();
        }

        private void EnsureDebounceTimer()
        {
            if (_labelLayoutDebounceTimer != null)
                return;

            _labelLayoutDebounceTimer = new System.Windows.Forms.Timer { Interval = LABEL_LAYOUT_DEBOUNCE_MS };
            _labelLayoutDebounceTimer.Tick += (s, e) =>
            {
                _labelLayoutDebounceTimer.Stop();
                var request = _pendingRequest;
                _pendingRequest = null;
                if (request == null)
                    return;

                if (_labelLayoutWorker != null && _labelLayoutWorker.IsBusy)
                {
                    _pendingRequest = request;
                    _labelLayoutDebounceTimer.Start();
                    return;
                }

                StartDebounced(request);
            };
        }

        /// <summary>
        /// Starts the label layout algorithm asynchronously after the debounce timer has elapsed. This method should not be called directly; use Start() instead.
        /// </summary>
        private void StartDebounced(LabelLayoutRequest request)
        {
            if (request == null)
                return;

            var pane = request.Pane;
            var labeledPoints = request.LabeledPoints;
            if (pane == null || labeledPoints == null)
                return;
            if (request.IsDisposed != null && request.IsDisposed())
                return;

            // DigitalRune docking panel sometimes is resized with negative chart width, which crashes the layout algorithm.
            if (!labeledPoints.Any() || pane.Chart.Rect.Width <= 0 || pane.Chart.Rect.Height <= 0)
                return;

            // Need this to make sure the coordinate transforms work correctly.
            pane.XAxis.Scale.SetupScaleData(pane, pane.XAxis);
            pane.YAxis.Scale.SetupScaleData(pane, pane.YAxis);

            var chartRect = new RectangleF((float)pane.XAxis.Scale.Min, (float)pane.YAxis.Scale.Min,
                (float)(pane.XAxis.Scale.Max - pane.XAxis.Scale.Min),
                (float)(pane.YAxis.Scale.Max - pane.YAxis.Scale.Min));
            var candidates = new List<LabeledPoint>();
            foreach (var labeledPoint in labeledPoints)
            {
                if (chartRect.Contains(new PointF((float)labeledPoint.Point.X, (float)labeledPoint.Point.Y)))
                {
                    candidates.Add(labeledPoint);
                }
                else
                {
                    labeledPoint.Label.IsVisible = false;
                    pane.GraphObjList.Remove(labeledPoint.Connector);
                }
            }

            if (!candidates.Any())
                return;

            LabelLayout samplingLayout = null;
            List<LabeledPoint> visiblePoints;
            using (var g = Graphics.FromHwnd(IntPtr.Zero))
            {
                var rects = candidates.ToDictionary(lp => lp, p => pane.GetRectScreen(p.Label, g));
                rects.ForEach(kv => kv.Key.LabelArea = kv.Value.Height * kv.Value.Width);   // Make sure the areas are calculated for the sampling algorithm to work correctly.
                if (!rects.Values.Any(h => h.Height > 0))
                    return;
                var minLabelHeight = rects.Values.Select(r => r.Height).ToList().FindAll(h => h > 0).Min();
                // If there are a lot of labels, do a quick sampling to improve performance of the full layout.
                samplingLayout = new LabelLayout(pane, (int)Math.Ceiling(minLabelHeight));
                visiblePoints = SamplePointsByDensityGrid(samplingLayout, pane, candidates);
            }
            var visibleSet = new HashSet<LabeledPoint>(visiblePoints);
            foreach (var labeledPoint in candidates)
            {
                if (visibleSet.Contains(labeledPoint))
                {
                    labeledPoint.Label.IsDraggable = true;
                    labeledPoint.Label.IsVisible = true;
                }
                else
                {
                    labeledPoint.Label.IsVisible = false;
                    pane.GraphObjList.Remove(labeledPoint.Connector);
                }
            }

            if (!visiblePoints.Any())
                return;

            var statusMessage = request.StatusMessage;
            var progressMonitor = request.ProgressMonitor;
            IProgressStatus status = new ProgressStatus(string.IsNullOrEmpty(statusMessage)
                ? GraphsResources.LabelLayoutRunner_Start_Label_layout
                : statusMessage);
            _labelLayoutStatus = status;
            progressMonitor?.UpdateProgress(status);
            _labelLayoutLastProgressMs = 0;

            var workerCancellation = new CancellationTokenSource();
            _labelLayoutCts = workerCancellation;

            var requestId = request.RequestId;
            var saveLayout = request.SaveLayout;
            var invalidate = request.Invalidate;
            var isDisposed = request.IsDisposed;

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
                    var labelLayout = workItem.LabelLayout ?? new LabelLayout(workItem.Pane, (int)Math.Ceiling(minLabelHeight));
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
                LabelLayout = samplingLayout,
                VisiblePoints = visiblePoints,
                SavedLayout = request.SavedLayout != null ? new List<LabeledPoint.PointLayout>(request.SavedLayout) : new List<LabeledPoint.PointLayout>(),
                Cancellation = workerCancellation
            });
        }

        private void CancelInFlight(IProgressMonitor progressMonitor = null)
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

        public void Cancel(IProgressMonitor progressMonitor = null)
        {
            CancelInFlight(progressMonitor);
            _pendingRequest = null;
            _labelLayoutDebounceTimer?.Stop();
        }
    }
}
