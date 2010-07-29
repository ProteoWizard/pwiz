/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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
using System.Drawing;
using System.Windows.Forms;
using pwiz.Skyline.Model;

namespace pwiz.Skyline.Util
{
    public sealed class LongOp : IDisposable
    {
        private readonly Control _control;
        private readonly Cursor _cursor;

        public LongOp(Control control)
            : this(control, Cursors.Arrow)
        {            
        }

        public LongOp(Control control, Cursor cursor)
        {
            _control = control;
            _cursor = cursor;
            _control.Cursor = Cursors.WaitCursor;
        }

        public void Dispose()
        {
            _control.Cursor = _cursor;
        }
    }

    /// <summary>
    /// For controls that do not allow completely turning off updates
    /// that cause painting during large operations.
    /// <para>
    /// The TreeView control often updates its scrollbar during long
    /// operations.  BeginUpdate keeps the client area from painting,
    /// but apparently not the scrollbar.  Certainly would be better
    /// if the TreeView could be stopped from sending all the paint
    /// and scrollbar update messages, but at least covering it with
    /// this control keeps it from flashing during operations like
    /// clearing the tree, and collapsing many nodes at once.</para>
    /// </summary>
    public sealed class CoverControl : Control
    {
        public CoverControl(Control cover)
            : base(cover.Parent, null)
        {
            SetStyle(ControlStyles.UserPaint, true);
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            SetStyle(ControlStyles.DoubleBuffer, true);

            Bounds = cover.Bounds;

            BringToFront();
            Show();
        }
    }

    /// <summary>
    /// Broker communication between a long operation and the user,
    /// usually used with <see cref="pwiz.Skyline.Controls.LongWaitDlg"/>.
    /// </summary>
    public interface ILongWaitBroker
    {
        /// <summary>
        /// True if the use has canceled the long operation
        /// </summary>
        bool IsCanceled { get; }

        /// <summary>
        /// Percent complete in the progress indicator shown to the user
        /// </summary>
        int ProgressValue { get; set; }

        /// <summary>
        /// Message shown to the user
        /// </summary>
        string Message { set; }
    }

    /// <summary>
    /// Use this interface to provide a progress sink for a long operation,
    /// and let the operation know, if it has been cancelled.  Usually used
    /// with status bar progress for background operations.
    /// </summary>
    public interface IProgressMonitor
    {
        /// <summary>
        /// True if the load has been cancelled.
        /// </summary>
        bool IsCanceled { get; }

        /// <summary>
        /// Reports updated <see cref="ProgressStatus"/> on a library load.
        /// </summary>
        /// <param name="status">The new status</param>
        void UpdateProgress(ProgressStatus status);        
    }

    public enum ProgressState { begin, running, complete, cancelled, error }

    public class ProgressStatusId : Identity
    {        
    }

    public class ProgressStatus : Immutable
    {
        /// <summary>
        /// Initial constructor for progress status of a long operation.  Starts
        /// in <see cref="ProgressState.begin"/>.
        /// </summary>
        public ProgressStatus(string message)
        {
            Id = new ProgressStatusId();
            State = ProgressState.begin;
            Message = message;
        }

        public ProgressStatusId Id { get; private set; }
        public ProgressState State { get; private set; }
        public string Message { get; private set; }
        public int PercentComplete { get; private set; }
        public int PercentZoomStart { get; private set; }
        public int PercentZoomEnd { get; private set; }
        public int SegmentCount { get; private set; }
        public int Segment { get; private set; }
        public Exception ErrorException { get; private set; }

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
                if (prop == 100)
                    prop = PercentZoomEnd;
                else
                    prop = ZoomedToPercent(prop);
            }
            prop = Math.Min(100, Math.Max(0, prop));
            if (prop == PercentComplete)
                return this;

            var status = ImClone(this);
            status.PercentComplete = prop;
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
            var status = ImClone(this);
            status.PercentZoomStart = PercentComplete;
            status.PercentZoomEnd = end;
            return status;
        }

        public ProgressStatus ChangeSegments(int segment, int segmentCount)
        {
            ProgressStatus status = ImClone(this);
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
            return ChangeProp(ImClone(this), (im, v) =>
                { im.ErrorException = v; im.State = ProgressState.error; }, prop);
        }

        public ProgressStatus ChangeMessage(string prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.Message = v, prop);
        }

        public ProgressStatus Cancel()
        {
            return ChangeProp(ImClone(this), (im, v) => im.State = v, ProgressState.cancelled);
        }

        public ProgressStatus Complete()
        {
            return ChangePercentComplete(100);
        }

        #endregion
    }

    public class ProgressUpdateEventArgs : EventArgs
    {
        public ProgressUpdateEventArgs(ProgressStatus progress)
        {
            Progress = progress;
        }

        public ProgressStatus Progress { get; private set; }
    }

    public interface IClipboardDataProvider
    {
        DataObject ProvideData();
    }

    public interface IUpdatable
    {
        /// <summary>
        /// Update this UI element.  Updates are performed on a time
        /// event to avoid blocking the UI thread.
        /// </summary>
        void UpdateUI();        
    }

    public sealed class MoveThreshold
    {
        public MoveThreshold(int width, int height)
        {
            Threshold = new Size(width, height);
        }

        public Size Threshold { get; set; }
        public Point? Location { get; set; }

        public bool Moved(Point locationNew)
        {
            return !Location.HasValue ||
                Math.Abs(Location.Value.X - locationNew.X) > Threshold.Width ||
                Math.Abs(Location.Value.Y - locationNew.Y) > Threshold.Height;            
        }
    }

    /// <summary>
    /// Simple class for custom drawing of text ranges
    /// </summary>
    public sealed class TextSequence
    {
        public String Text { get; set; }
        public Font Font { get; set; }
        public Color Color { get; set; }
        public int Position { get; set; }
        public int Width { get; set; }
        public bool IsPlainText { get; set; }
    }
}
