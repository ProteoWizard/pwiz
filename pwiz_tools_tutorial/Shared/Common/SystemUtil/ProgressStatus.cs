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

namespace pwiz.Common.SystemUtil
{
    public enum ProgressState { begin, running, complete, cancelled, error }

    public class ProgressStatus
    {
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

        public ProgressStatus(ProgressStatus status)
        {
            ErrorException = status.ErrorException;
            Message = status.Message;
            PercentComplete = status.PercentComplete;
            PercentZoomEnd = status.PercentZoomEnd;
            PercentZoomStart = status.PercentZoomStart;
            Segment = status.Segment;
            SegmentCount = status.SegmentCount;
            State = status.State;
            Id = status.Id;
        }

        public ProgressState State { get; private set; }
        public string Message { get; private set; }
        public int PercentComplete { get; private set; }
        public int PercentZoomStart { get; private set; }
        public int PercentZoomEnd { get; private set; }
        public int SegmentCount { get; private set; }
        public int Segment { get; private set; }
        public Exception ErrorException { get; private set; }
        public object Id { get; private set; }

        /// <summary>
        /// Any innactive state after begin
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
            return (PercentComplete == ZoomedToPercent(percent));
        }

        private int ZoomedToPercent(int percent)
        {
            return PercentZoomStart + percent*(PercentZoomEnd - PercentZoomStart)/100;
        }

        #region Property change methods

        public ProgressStatus ChangePercentComplete(int prop)
        {
            // Handle progress zooming, if a range of progress has been zoomed
            if (PercentZoomEnd != 0)
            {
                prop = prop == 100 ? PercentZoomEnd : ZoomedToPercent(prop);
            }
            // Allow -1 as a way of allowing a looping progress indicator
            if (prop != -1)
                prop = Math.Min(100, Math.Max(0, prop));
            if (prop == PercentComplete)
                return this;

            var status = new ProgressStatus(this) {PercentComplete = prop};
            // Turn off progress zooming, if the end has been reached
            if (prop == PercentZoomEnd)
                status.PercentZoomEnd = 0;
            status.State = (status.PercentComplete == 100
                                ? ProgressState.complete
                                : ProgressState.running);
            return status;
        }

        public ProgressStatus ZoomUntil(int end)
        {
            return new ProgressStatus(this) {PercentZoomStart = PercentComplete, PercentZoomEnd = end};
        }

        public ProgressStatus ChangeSegments(int segment, int segmentCount)
        {
            ProgressStatus status = new ProgressStatus(this);
            if (segmentCount == 0)
                status.PercentZoomStart = status.PercentZoomEnd = 0;
            else
            {
                status.PercentComplete = status.PercentZoomStart = segment*100/segmentCount;
                status.PercentZoomEnd = (segment+1)*100/segmentCount;
            }
            status.SegmentCount = segmentCount;
            status.Segment = segment;
            return status;
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
            return new ProgressStatus(this)
                       {
                           ErrorException = prop,
                           State = ProgressState.error,
                       };
        }

        public ProgressStatus ChangeMessage(string prop)
        {
            return new ProgressStatus(this)
                {
                    Message = prop,
                };
        }

        public ProgressStatus Cancel()
        {
            return new ProgressStatus(this)
                       {
                           State = ProgressState.cancelled,
                       };
        }

        public ProgressStatus Complete()
        {
            return ChangePercentComplete(100);
        }

        #endregion
    }
}