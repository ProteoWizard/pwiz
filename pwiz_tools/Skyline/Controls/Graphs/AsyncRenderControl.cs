/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.Threading;
using System.Windows.Forms;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Controls.Graphs
{
    /// <summary>
    /// Base class for user controls that render to a bitmap asynchronously.
    /// </summary>
    public partial class AsyncRenderControl : UserControl
    {
        private readonly AutoResetEvent _startBackgroundRender = new AutoResetEvent(false);
        private bool _finished;
        private int _rendering;
        private bool _renderPending;
        private Rectangle _renderRect;
        private Rectangle _invalidRect;
        private readonly string _backgroundThreadName;

        //private static readonly Log LOG = new Log<AsyncRenderControl>();

        // Just for Visual Studio designer
        protected AsyncRenderControl()
        {
        }

        protected AsyncRenderControl(string backgroundThreadName)
        {
            InitializeComponent();
            pictureBox.SizeMode = PictureBoxSizeMode.StretchImage;
            _backgroundThreadName = backgroundThreadName;
        }

        /// <summary>
        /// Start background rendering thread.
        /// </summary>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (!DesignMode)
                ActionUtil.RunAsync(BackgroundRender, _backgroundThreadName);
        }

        /// <summary>
        /// Accumulate invalid rectangle.
        /// </summary>
        protected override void OnInvalidated(InvalidateEventArgs e)
        {
            base.OnInvalidated(e);
            _invalidRect = Rectangle.Union(_invalidRect, e.InvalidRect);
        }

        /// <summary>
        /// Start background rendering when necessary.
        /// </summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (IsVisible && !DesignMode)
            {
                // Remember a pending render request if the background thread is already busy.
                _renderPending = true;

                // Atomic test to see if background rendering thread is active.
                if (Interlocked.CompareExchange(ref _rendering, 1, 0) == 0)
                {
                    _renderPending = false;
                    _renderRect = _invalidRect;
                    _invalidRect = Rectangle.Empty;

                    // Allocate or resize offscreen buffer.
                    if (pictureBox.Image == null || pictureBox.Width != Width || pictureBox.Height != Height)
                    {
                        pictureBox.Image = new Bitmap(Width, Height);
                        _renderRect = new Rectangle(0, 0, Width, Height);
                    }

                    // Copy data to render, and start background rendering.
                    CopyState();
                    _startBackgroundRender.Set();
                }
            }
        }

        protected bool IsVisible
        {
            get
            {
                var parent = FormEx.GetParentForm(this);
                return parent != null && parent.Visible;
            }
        }

        /// <summary>
        /// Stop background rendering.
        /// </summary>
        public void Finish()
        {
            _finished = true;
            _startBackgroundRender.Set();
        }

        /// <summary>
        /// Background rendering loop.
        /// </summary>
        private void BackgroundRender()
        {
            // Initialize on the background thread.
            BackgroundInitialize();

            while (true)
            {
                try
                {
                    // Wait for rendering request.
                    _startBackgroundRender.WaitOne();
                    if (_finished)
                        break;

                    // Render and display a new bitmap.
                    Render((Bitmap) pictureBox.Image, _renderRect);
                    Invoke(new Action(() =>
                    {
                        pictureBox.Invalidate(_renderRect);
                        pictureBox.Update();
                    }));

                    // Not rendering now, but restart cycle if we missed a request.
                    _rendering = 0;
                    if (_renderPending)
                        Invalidate();
                }
// ReSharper disable EmptyGeneralCatchClause
                catch (Exception e)
// ReSharper restore EmptyGeneralCatchClause
                {
                    Trace.WriteLine(e);
                    // If we don't catch the exception in this background thread, Visual Studio
                    // kills a unit test without any explanation.  Resharper and TestRunner just
                    // ignore the problem.
                    //LOG.Error("Exception in async renderer", exception);    // Not L10N
                }
            }
        }

        /// <summary>
        /// Override to perform initialization on the background thread.
        /// </summary>
        protected virtual void BackgroundInitialize()
        {
        }

        /// <summary>
        /// Copy graphics on main thread to freeze them for background rendering.
        /// </summary>
        protected virtual void CopyState()
        {
        }

        /// <summary>
        /// Render content to a bitmap.
        /// </summary>
        protected virtual void Render(Bitmap bitmap, Rectangle invalidRect)
        {
        }
    }
}
