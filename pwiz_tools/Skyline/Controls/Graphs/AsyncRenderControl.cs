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
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using pwiz.Skyline.Util;
using Timer = System.Windows.Forms.Timer;

namespace pwiz.Skyline.Controls.Graphs
{
    /// <summary>
    /// Base class for user controls that render to a bitmap asynchronously.
    /// </summary>
    public partial class AsyncRenderControl : UserControl
    {
        private Bitmap _renderedBitmap;
        private Timer _updateTimer;
        private int _renderWidth;
        private int _renderHeight;
        private int _lastWidth;
        private int _lastHeight;
        private readonly Thread _backgroundRenderThread;
        private readonly AutoResetEvent _startBackgroundRender = new AutoResetEvent(false);

        private static readonly Log LOG = new Log<AsyncRenderControl>();

        public AsyncRenderControl()
        {
        }

        protected AsyncRenderControl(int framesPerSecond)
        {
            InitializeComponent();
            pictureBox.SizeMode = PictureBoxSizeMode.StretchImage;

            // Keep VS designer from crashing.
            if (Program.MainWindow == null)
                return;

            // NOTE: We create our a timer manually instead of using a designer component
            // because we want the timer to continue ticking even when the window is
            // closed.  Some subclasses may need to render even without a window to
            // process data that would otherwise consume a lot of memory.
            _updateTimer = new Timer { Interval = 1000/framesPerSecond };
            _updateTimer.Tick += (s, e) =>
                {
                    if (_renderedBitmap != null && !ReferenceEquals(pictureBox.Image, _renderedBitmap))
                        pictureBox.Image = _renderedBitmap;
                    StartBackgroundRendering();
                };
            _updateTimer.Start();

            _backgroundRenderThread = new Thread(BackgroundRender)
                {
                    Name = "AllChromatograms background render",    // Not L10N
                    IsBackground = true
                };
            _backgroundRenderThread.Start();
        }

        protected bool IsVisible
        {
            get { return FormEx.GetParentForm(this).Visible; }
        }

        /// <summary>
        /// Stop background rendering.
        /// </summary>
        public void Finish()
        {
            _updateTimer.Dispose();
            _updateTimer = null;
            _startBackgroundRender.Set();
        }

        /// <summary>
        /// Start rendering the next frame.
        /// </summary>
        protected void StartBackgroundRendering()
        {
            lock (this)
            {
                _renderWidth = Width;
                _renderHeight = Height;
            }
            pictureBox.Width = _renderWidth;
            pictureBox.Height = _renderHeight;
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
                    if (_updateTimer == null)
                        return;

                    // If size changed, force a new frame to be rendered.
                    bool forceRender = (_renderedBitmap == null);
                    lock (this)
                    {
                        forceRender |= (_lastWidth != _renderWidth || _lastHeight != _renderHeight);
                        _lastWidth = _renderWidth;
                        _lastHeight = _renderHeight;
                    }

                    // Call derived class to render the bitmap.
                    Render(_lastWidth, _lastHeight, forceRender && IsVisible, ref _renderedBitmap);
                }
                catch (Exception exception)
                {
                    // If we don't catch the exception in this background thread, Visual Studio
                    // kills a unit test without any explanation.  Resharper and TestRunner just
                    // ignore the problem.
                    LOG.Error("Exception in async renderer", exception);    // Not L10N
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
        /// <param name="width">New bitmap width.</param>
        /// <param name="height">New bitmap height.</param>
        /// <param name="forceRender">True if render is required.</param>
        /// <param name="bitmap">Bitmap which is created if new content needs to be displayed.</param>
        protected virtual void Render(int width, int height, bool forceRender, ref Bitmap bitmap)
        {
            // This is a default implementation that displays a red rectangle.  We don't
            // expect anyone to use this, but it's here to provide a reference implementation.
            // For example, no bitmap needs to be rendered if forceRender is false and nothing
            // else has changed (this example is static).
            if (forceRender)
            {
                bitmap = new Bitmap(width, height);
                using (var graphics = Graphics.FromImage(bitmap))
                    graphics.FillRectangle(Brushes.Red, 0, 0, bitmap.Width, bitmap.Height);
            }
        }
    }
}
