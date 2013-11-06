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
using pwiz.Common.SystemUtil;
using pwiz.MSGraph;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    /// <summary>
    /// An MSGraphPane that render asynchronously to avoid hiccups in the UI.
    /// </summary>
    public class AsyncMSGraphPane : MSGraphPane
    {
        private readonly Control _parent;
        private readonly AutoResetEvent _startBackgroundRender = new AutoResetEvent(false);
        private Bitmap _bitmap;
        private bool _hasNewBitmap;
        private GraphPane _renderPane;

        public AsyncMSGraphPane(Control parent)
        {
            _parent = parent;

            // Start a new background rendering thread.
            var backgroundRenderThread = new Thread(BackgroundRender)
            {
                Name = "Background render thread",  // Not L10N
                IsBackground = true
            };
            LocalizationHelper.InitThread(backgroundRenderThread);
            backgroundRenderThread.Start();
        }

        /// <summary>
        /// Draw method which initiates asynchronous rendering.
        /// </summary>
        public override void Draw(Graphics g)
        {
            // If we have a bitmap, display it, possibly scaling to fit the size of the pane.
            if (_bitmap != null)
            {
                g.DrawImage(_bitmap, Rect.X, Rect.Y, Rect.Width, Rect.Height);
                if (_hasNewBitmap && _bitmap.Width == Rect.Width && _bitmap.Height == Rect.Height)
                    return;
            }

            // If the bitmap is out of date, start rendering if it isn't already busy.
            if (_renderPane == null)
            {
                // Possibly expensive: create a separate copy of this graph pane, so changes can
                // happen while the background thread is busy rendering the current pane.
                _renderPane = new MSGraphPane(this);
                _startBackgroundRender.Set();
            }
        }

        /// <summary>
        /// Background rendering loop.
        /// </summary>
        private void BackgroundRender()
        {
            while (true)
            {
                try
                {
                    // Wait for rendering request.
                    _startBackgroundRender.WaitOne();
                    if (_renderPane == null)
                        return;

                    // Allocate new bitmap and render to it.
                    var newBitmap = new Bitmap((int) _renderPane.Rect.Width, (int) _renderPane.Rect.Height);
                    using (var g = Graphics.FromImage(newBitmap))
                    {
                        _renderPane.Draw(g);
                    }

                    // Call Draw method on the UI thread to display the new bitmap.
                    _parent.Invoke(new Action(() =>
                    {
                        _renderPane = null;     // discard pane for rendering
                        _bitmap = newBitmap;    // discard old bitmap and set new one
                        _hasNewBitmap = true;   // notify Draw to only display the bitmap
                        _parent.Refresh();
                        _hasNewBitmap = false;
                    }));
                }
                catch (Exception)
                {
                    _renderPane = null;
                    //LOG.Error("Exception in async renderer", exception);    // Not L10N
                }
            }
        }
    }
}
