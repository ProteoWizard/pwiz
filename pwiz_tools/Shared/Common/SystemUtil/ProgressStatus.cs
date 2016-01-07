/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Threading;

namespace pwiz.Common.SystemUtil
{
// ReSharper disable InconsistentNaming
    public enum ProgressState { begin, running, complete, cancelled, error }
// ReSharper restore InconsistentNaming

    public class ProgressStatus : Immutable
    {
        /// <summary>
        /// Increments a counter, and returns the percent complete before incrementing if it
        /// is different from the incremented percent complete. Important to reporting percent
        /// complete progress changes only once during parallel operations.
        /// </summary>
        public static int? ThreadsafeIncementPercent(ref int currentCount, int? totalCount)
        {
            Interlocked.Increment(ref currentCount);
            if (totalCount.HasValue)
            {
                int percentIncremented = currentCount*100/totalCount.Value;
                int percentBefore = (currentCount - 1)*100/totalCount.Value;
                if (percentIncremented != percentBefore)
                    return percentBefore;
            }
            return null;
        }

        /// <summary>
        /// Initial constructor for progress status of a long operation.  Starts
        /// in <see cref="pwiz.Common.SystemUtil.ProgressState.begin"/>.
        /// </summary>
        public ProgressStatus(string message)
        {
            State = ProgressState.begin;
            Message = message;
            Id = new object();
        }

        public ProgressState State { get; private set; }
        public string Message { get; private set; }
        public string WarningMessage { get; private set; }
        public int PercentComplete { get; private set; }
        public int ZoomedPercentComplete { get; private set; }
        public int PercentZoomStart { get; private set; }
        public int PercentZoomEnd { get; private set; }
        public int SegmentCount { get; private set; }
        public int Segment { get; private set; }
        public Exception ErrorException { get; private set; }
        public object Id { get; private set; }

        /// <summary>
        /// Any inactive state after begin
        /// </summary>
        public bool IsFinal
        {
            get { return (State != ProgressState.begin && State != ProgressState.running); }
        }

        /// <summary>
        /// Completed successfully
        /// </summary>
        public bool IsComplete { get { return State == ProgressState.complete; } }

        /// <summary>
        /// Encountered an error
        /// </summary>
        public bool IsError { get { return State == ProgressState.error; } }

        /// <summary>
        /// Canceled by user action
        /// </summary>
        public bool IsCanceled { get { return State == ProgressState.cancelled; } }

        /// <summary>
        /// Initial status state
        /// </summary>
        public bool IsBegin { get { return State == ProgressState.begin; } }

        public bool IsPercentComplete(int percent)
        {
            if (PercentZoomEnd == 0)
                return (PercentComplete == percent);
            return (ZoomedPercentComplete == percent);
        }

        private int ZoomedToPercent(int percent)
        {
            return PercentZoomStart + percent*(PercentZoomEnd - PercentZoomStart)/100;
        }

        #region Property change methods

        public ProgressStatus ChangePercentComplete(int percent)
        {
            var zoomedPercentComplete = percent;

            // Handle progress zooming, if a range of progress has been zoomed
            if (PercentZoomEnd != 0)
            {
                percent = percent == 100 ? PercentZoomEnd : ZoomedToPercent(percent);
            }
            // Allow -1 as a way of allowing a looping progress indicator
            if (percent != -1)
                percent = Math.Min(100, Math.Max(0, percent));
            // If this percent complete value has already been set, then do nothing.
            if (IsPercentComplete(percent))
                return this;

            return ChangeProp(ImClone(this), s =>
                {
                    s.PercentComplete = percent;
                    s.ZoomedPercentComplete = zoomedPercentComplete;
                    // Turn off progress zooming, if the end has been reached
                    if (percent == PercentZoomEnd)
                        s.PercentZoomEnd = 0;
                    s.State = (s.PercentComplete == 100
                                        ? ProgressState.complete
                                        : ProgressState.running);
                });
        }

        public ProgressStatus ZoomUntil(int end)
        {
            return ChangeProp(ImClone(this), s =>
                {
                    s.PercentZoomStart = PercentComplete;
                    s.PercentZoomEnd = end;
                });
        }

        public ProgressStatus ChangeSegments(int segment, int segmentCount)
        {
            return ChangeProp(ImClone(this), s =>
                {
                    if (segmentCount == 0)
                        s.PercentZoomStart = s.PercentZoomEnd = 0;
                    else
                    {
                        s.PercentComplete = s.PercentZoomStart = segment*100/segmentCount;
                        s.PercentZoomEnd = (segment + 1)*100/segmentCount;
                    }
                    s.SegmentCount = segmentCount;
                    s.Segment = segment;
                });
        }

        public ProgressStatus NextSegment()
        {
            int segment = Segment + 1;
            if (segment >= SegmentCount)
                return this;
            return ChangeSegments(segment, SegmentCount);
        }

        public ProgressStatus ChangeErrorException(Exception prop)
        {
            return ChangeProp(ImClone(this), s =>
                {
                    s.ErrorException = prop;
                    s.State = ProgressState.error;
                });
        }

        public ProgressStatus ChangeMessage(string prop)
        {
            return ChangeProp(ImClone(this), s => s.Message = prop);
        }

        public ProgressStatus ChangeWarningMessage(string prop)
        {
            return ChangeProp(ImClone(this), s => s.WarningMessage = prop);
        }

        public ProgressStatus Cancel()
        {
            return ChangeProp(ImClone(this), s => s.State = ProgressState.cancelled);
        }

        public ProgressStatus Complete()
        {
            return ChangePercentComplete(100);
        }

        public ProgressStatus UpdatePercentCompleteProgress(IProgressMonitor progressMonitor,
            long currentCount, long totalCount)
        {
            if (progressMonitor.IsCanceled)
                throw new OperationCanceledException();
            int percentComplete = (int) (100 * currentCount / totalCount);
            if (percentComplete == PercentComplete)
                return this;
            var statusNew = ChangePercentComplete(percentComplete);
            progressMonitor.UpdateProgress(statusNew);
            return statusNew;
        }

        #endregion
    }
}