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
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
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

        /// <summary>
        /// Returns true if the given original document is no longer the active
        /// document for this operation.
        /// </summary>
        bool IsDocumentChanged(SrmDocument docOrig);

        /// <summary>
        /// Shows a dialog box on the right thread, parented to the progress form
        /// </summary>
        DialogResult ShowDialog(Func<IWin32Window, DialogResult> show);
    }

    /// <summary>
    /// Exposes <see cref="IProgressMonitor"/> for an action that requires the interface,
    /// given a <see cref="ILongWaitBroker"/>.
    /// </summary>
    public sealed class ProgressWaitBroker : IProgressMonitor
    {
        private readonly Action<IProgressMonitor> _performWork;
        private ILongWaitBroker _broker;

        public ProgressWaitBroker(Action<IProgressMonitor> performWork)
        {
            _performWork = performWork;
            Status = new ProgressStatus("");
        }

        public void PerformWork(ILongWaitBroker broker)
        {
            _broker = broker;
            _performWork(this);
        }

        public ProgressStatus Status { get; private set; }

        public bool IsCanceled
        {
            get { return _broker.IsCanceled; }
        }

        public void UpdateProgress(ProgressStatus status)
        {
            _broker.ProgressValue = status.PercentComplete;
            _broker.Message = status.Message;
            Status = status;
        }
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

    /// <summary>
    /// Helpers for doing web stuff.
    /// </summary>
    public sealed class WebHelpers
    {
        public static void OpenLink(IWin32Window parent, string link)
        {
            try
            {
                Process.Start(link);
            }
            catch (Exception)
            {
                AlertLinkDlg.Show(parent, "Could not open web browser to show link:", link, link, false);
            }
        }
    }
}
