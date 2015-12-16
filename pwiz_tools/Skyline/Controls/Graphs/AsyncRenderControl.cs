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
using Timer = System.Windows.Forms.Timer;

namespace pwiz.Skyline.Controls.Graphs
{
    /// <summary>
    /// Base class for user controls that render to a bitmap asynchronously.
    /// </summary>
    public partial class AsyncRenderControl : UserControl
    {
        private readonly RenderContext _context = new RenderContext();
        private readonly AutoResetEvent _startBackgroundRender = new AutoResetEvent(false);
        private readonly string _backgroundThreadName;

        //private static readonly Log LOG = new Log<AsyncRenderControl>();

        protected AsyncRenderControl()
            : this("Background render") // Not L10N
        {
        }

        protected AsyncRenderControl(string backgroundThreadName)
        {
            InitializeComponent();
            _backgroundThreadName = backgroundThreadName;
            FrameMilliseconds = 100;
        }

        protected override void OnLoad(EventArgs e)
        {
            // Keep VS designer from crashing.
            if (Program.MainWindow == null)
                return;

            pictureBox.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox.Width = Width;
            pictureBox.Height = Height;
            pictureBox.Image = new Bitmap(Width, Height);

            lock (_context)
            {
                _context._renderBitmap = new Bitmap(Width, Height);
                _context._fullFrame = true;
                
                // NOTE: We create our a timer manually instead of using a designer component
                // because we want the timer to continue ticking even when the window is
                // closed.  Some subclasses may need to render even without a window to
                // process data that would otherwise consume a lot of memory.
                _context._updateTimer = new Timer { Interval = 100 };
                _context._updateTimer.Tick += (s, e1) =>
                {
                    lock (_context)
                    {
                        // Don't render if window is not visible.
                        if (!IsVisible)
                        {
                            _context._fullFrame = true;
                            _context._invalidRect.Width = 0;
                            _context._updateTimer.Interval = 500;
                            return;
                        }

                        // For full frame, swap bitmap buffers.
                        if (_context._invalidRect.Width == Width)
                        {
                            var swap = (Bitmap) pictureBox.Image;
                            pictureBox.Image = _context._renderBitmap;
                            pictureBox.Update();
                            _context._renderBitmap = swap;
                        }

                        // For partial frame, copy pixels from rendering buffer.
                        else if (_context._invalidRect.Width > 0)
                        {
                            using (var graphics = Graphics.FromImage(pictureBox.Image))
                            {
                                graphics.DrawImage(
                                    _context._renderBitmap,
                                    _context._invalidRect,
                                    _context._invalidRect, 
                                    GraphicsUnit.Pixel);
                            }
                            pictureBox.Invalidate(_context._invalidRect);
                            pictureBox.Update();
                        }

                        _context._invalidRect.Width = 0;
                        _context._updateTimer.Interval = FrameMilliseconds;
                    }
                    StartBackgroundRendering();
                };
                _context._updateTimer.Start();
            }

            ActionUtil.RunAsync(BackgroundRender, _backgroundThreadName);
            StartBackgroundRendering();
        }

        protected int FrameMilliseconds { get; set; }

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
            lock (_context)
            {
                if (_context._updateTimer != null)
                {
                    _context._updateTimer.Dispose();
                    _context._updateTimer = null;
                }
            }
            _startBackgroundRender.Set();
        }

        /// <summary>
        /// Start rendering the next frame.
        /// </summary>
        protected void StartBackgroundRendering()
        {
            if (IsVisible)
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
                    // Wait for rendering request (someone calls StartBackgroundRendering).
                    _startBackgroundRender.WaitOne();

                    lock (_context)
                    {
                        if (_context._updateTimer == null)
                            return;

                        _context._invalidRect = Render(_context._renderBitmap, _context._fullFrame);
                        _context._fullFrame = false;
                    }
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
        /// Render content to a bitmap.  Subclasses override this method to render their
        /// particular content.  The bitmap does not need to be re-rendered if nothing
        /// has changed since the last Render.
        /// </summary>
        /// <param name="bitmap">Destination bitmap.</param>
        /// <param name="fullFrame">True to force full frame rendering.</param>
        protected virtual Rectangle Render(Bitmap bitmap, bool fullFrame)
        {
            // This is a default implementation that displays a red rectangle.  We don't
            // expect anyone to use this, but it's here to provide a reference implementation.

            // No bitmap needs to be rendered if fullFrame is false and nothing
            // else has changed (this example is static).
            if (!fullFrame)
                return Rectangle.Empty;

            var renderedRect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            using (var graphics = Graphics.FromImage(bitmap))
                graphics.FillRectangle(Brushes.Red, renderedRect);
            return renderedRect;
        }

        private class RenderContext
        {
            public Bitmap _renderBitmap;
            public Rectangle _invalidRect;
            public Timer _updateTimer;
            public bool _fullFrame;
        }
    }
}
