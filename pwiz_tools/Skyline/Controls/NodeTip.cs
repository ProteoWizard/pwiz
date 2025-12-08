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
using System.Drawing.Imaging;
using System.Windows.Forms;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls
{
    /// <summary>
    /// Implement to provide custom tool tips for tree nodes.
    /// </summary>
    public interface ITipProvider
    {
        /// <summary>
        /// Return false to disable tips on the implementing node depending
        /// on application state.
        /// </summary>
        bool HasTip { get; }

        /// <summary>
        /// In the process of showing a custom tip, this function is called
        /// multiple times. First, it is called with <see cref="draw"/> set to false,
        /// and a maximum size allowable for the tip client area. The implementing code
        /// is expected to return a desired size for the tip client area.  The caller
        /// may call as many times as necessary with <see cref="draw"/> set to false
        /// in order to negotiate a tip size.  The implementation must not actually
        /// draw on the <see cref="Graphics"/> supplied in these cases.
        ///
        /// Finally, the method will be called once with <see cref="draw"/> set to true
        /// and a maximum size.  The implementation must then use the <see cref="Graphics"/>
        /// supplied to draw its tip with origin (0,0) and within the maximum size.
        /// </summary>
        /// <param name="g">Graphics to use for measuring or drawing the tip</param>
        /// <param name="sizeMax">Maximum size within which the tip must fit</param>
        /// <param name="draw">True if the implementation should paint, or false if it should measure</param>
        /// <returns>The best size for the tip that fits within the maximum specified</returns>
        Size RenderTip(Graphics g, Size sizeMax, bool draw);
    }

    /// <summary>
    /// Implement to enable a control to display tool tips.
    /// </summary>
    public interface ITipDisplayer
    {
        /// <summary>
        /// Gets the bounds of the control in which the tip is displayed.
        /// </summary>
        Rectangle ScreenRect { get; }

        /// <summary>
        /// Indicates if the tip should be displayed.
        /// </summary>
        bool AllowDisplayTip { get; }

        /// <summary>
        /// Gets the screen coordinates of the given rectangle,
        /// which in this case is the node whose tip we are displaying.
        /// </summary>
        Rectangle RectToScreen(Rectangle r);
    }

    public class NodeTip : CustomTip
    {
        public static string FontFace { get { return @"Arial"; } }
        public static float FontSize { get { return 8f; } }

        public static int TipDelayMs { get { return 500; } }

        private ITipProvider _tipProvider;
        private readonly ITipDisplayer _tipDisplayer;
        private Rectangle _rectItem;
        private Timer _timer;
        private readonly MoveThreshold _moveThreshold = new MoveThreshold(5, 5);

        private const int NODE_SPACE_Y = 5;

        public NodeTip(ITipDisplayer tipDisplayer)
        {
            _timer = new Timer { Interval = TipDelayMs };
            _timer.Tick += Timer_Tick;
            _tipDisplayer = tipDisplayer;
        }

        protected override void Dispose(bool disposing)
        {
            if (_timer != null)
            {
                _timer.Dispose();
                _timer = null;
            }
            base.Dispose(disposing);
        }

        public void HideTip()
        {
            SetTipProvider(null, new Rectangle(), new Point());
        }

        public void SetTipProvider(ITipProvider tipProvider, Rectangle rectItem, Point cursorPos)
        {
            if (!_moveThreshold.Moved(cursorPos))
                return;
            _timer.Stop();
            if (Visible)
            {
                AnimateMode animate = (Y < _rectItem.Y ?
                AnimateMode.SlideTopToBottom : AnimateMode.SlideBottomToTop);
                HideAnimate(animate);
            }
            _tipProvider = tipProvider;
            _rectItem = _tipDisplayer.RectToScreen(rectItem);
            _moveThreshold.Location = cursorPos;
            if (tipProvider != null)
                _timer.Start();
        }

        public override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (_tipProvider != null)
            {
                // Render in unrestricted size, since current algorithms may
                // not render completely, if given exactly the ClientSize.
                _tipProvider.RenderTip(e.Graphics, ClientSize, true);
            }
        }

        private void Timer_Tick(Object sender, EventArgs e)
        {
            _timer.Stop();
            if (_tipDisplayer == null || !_tipDisplayer.AllowDisplayTip)
                return;
            try
            {
                DisplayTip();
            }
            catch (Exception exception)
            {
                ExceptionUtil.DisplayOrReportException(this, exception, ControlsResources.NodeTip_Timer_Tick_An_error_occurred_displaying_a_tooltip_);
            }
        }

        private void DisplayTip()
        {
            Rectangle rectScreen = _tipDisplayer.ScreenRect;
            AnimateMode animate = AnimateMode.SlideTopToBottom;

            using (Bitmap bitmap1 = new Bitmap(1, 1, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(bitmap1))
                {
                    Size size = _tipProvider.RenderTip(g, rectScreen.Size, false);
                    int yPos = _rectItem.Y + _rectItem.Height + NODE_SPACE_Y;
                    if (yPos + size.Height > rectScreen.Bottom)
                    {
                        if (rectScreen.Bottom - yPos > _rectItem.Top - NODE_SPACE_Y - rectScreen.Top)
                        {
                            size.Height = rectScreen.Bottom - yPos;

                            // Recalc size based to fit into restricted area.
                            size = _tipProvider.RenderTip(g, size, false);
                        }
                        else
                        {
                            yPos = _rectItem.Top - NODE_SPACE_Y;
                            if (yPos - size.Height < rectScreen.Top)
                            {
                                size.Height = yPos - rectScreen.Top;

                                // Recalc size based to fit into restricted area.
                                size = _tipProvider.RenderTip(g, size, false);
                            }
                            yPos -= size.Height;
                            animate = AnimateMode.SlideBottomToTop;
                        }
                    }
                    Location = new Point(_rectItem.X, yPos);
                    ClientSize = size;
                }
            }

            ShowAnimate(X, Y, animate); // Not really animated anymore, because of GDI handle leak on Windows 10
        }

        #region Test Support

        public ITipProvider Provider => _tipProvider;

        #endregion
    }
}
