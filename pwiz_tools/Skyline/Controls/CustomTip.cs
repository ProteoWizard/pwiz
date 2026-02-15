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
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using pwiz.Common.SystemUtil.PInvoke;

namespace pwiz.Skyline.Controls
{
    /// <summary>
    /// Represents a floating native window with shadow, transparency and the ability to 
    /// move and be resized with the mouse.
    /// </summary>
    public class CustomTip : NativeWindow, IDisposable
    {
        private const int SHADOW_LENGTH = 4;

        private bool _isMouseIn;
        private bool _onMouseMove;
        private bool _onMouseDown;
        private bool _onMouseUp;
        private bool _captured;
        private bool _disposed;
        private bool _hasShadow = true;
        private Point _location = new Point(0,0);
        private Size _size = new Size(200, 100);
        private byte _alpha = 0xff;
        private Control _parent;
        private Point _lastMouseDown = Point.Empty;
        private readonly bool _supportsLayered;
        private readonly Color _borderColor = Color.Black;
        private readonly Color _backColor = Color.LightYellow; // color of tooltip box background (MS tooltip yellow);

        public CustomTip()
        {
            // The layered window support was causing a GDI handle leak
            _supportsLayered = false; // OSFeature.Feature.GetVersionPresent(OSFeature.LayeredWindows) != null;
        }

        ~CustomTip()
        {
            Dispose(false);
        }

        #region #  Methods  #

        #region == Painting ==

        /// <summary>
        /// Paints the shadow for the window.
        /// </summary>
        /// <param name="e">A <see cref="PaintEventArgs"/> containing the event data.</param>
        protected void PaintShadow(NcPaintEventArgs e)
        {
            if (_hasShadow)
            {
                Color color1 = Color.FromArgb(0x30, 0, 0, 0);
                Color color2 = Color.FromArgb(0, 0, 0, 0);
                using (GraphicsPath path1 = new GraphicsPath())
                {
                    Rectangle rectShadow = GetShadowRectangle(e.Bounds);
                    GraphicsContainer container1 = e.Graphics.BeginContainer();
                    path1.StartFigure();
                    path1.AddRectangle(rectShadow);
                    path1.CloseFigure();
                    using (PathGradientBrush brush2 = new PathGradientBrush(path1))
                    {
                        brush2.CenterColor = color1;
                        brush2.CenterPoint = new Point(rectShadow.X + rectShadow.Width / 2,
                            rectShadow.Y + rectShadow.Height / 2);
                        ColorBlend cb = new ColorBlend
                        {
                            Colors = new[] {color1, color2},
                            Positions = new[] {0.0f, 1.0f}
                        };
                        brush2.InterpolationColors = cb;
                        using (Region region = e.Graphics.Clip)
                        {
                            //region.Exclude(ClientRectangle);
                            e.Graphics.Clip = region;
                            e.Graphics.FillRectangle(brush2, rectShadow);
                        }
                    }
                    e.Graphics.EndContainer(container1);
                }
            }
        }

        protected void PaintBorder(NcPaintEventArgs e)
        {
            Rectangle rectBorder = GetBorderRectangle(e.Bounds);

            using (SolidBrush br = new SolidBrush(Color.FromArgb(255, _borderColor)))
            {
                Rectangle rectFill = new Rectangle(rectBorder.Left, rectBorder.Top, rectBorder.Width, 1);
                e.Graphics.FillRectangle(br, rectFill);
                rectFill = new Rectangle(rectBorder.Left, rectBorder.Bottom - 1, rectBorder.Width, 1);
                e.Graphics.FillRectangle(br, rectFill);
                rectFill = new Rectangle(rectBorder.Left, rectBorder.Top + 1, 1, rectBorder.Height - 2);
                e.Graphics.FillRectangle(br, rectFill);
                rectFill = new Rectangle(rectBorder.Right - 1, rectBorder.Top + 1, 1, rectBorder.Height - 2);
                e.Graphics.FillRectangle(br, rectFill);
            }            
        }

        /// <summary>
        /// Performs the painting of the window.
        /// </summary>
        /// <param name="e">A <see cref="PaintEventArgs"/> containing the event data.</param>
        protected virtual void PaintClient(PaintEventArgs e)
        {
            using (SolidBrush br = new SolidBrush(Color.FromArgb(255, _backColor)))
            {
                e.Graphics.FillRectangle(br, ClientRectangle);
            }
        }

        /// <summary>
        /// Raises the <see cref="Paint"/> event.
        /// </summary>
        /// <param name="e">A <see cref="PaintEventArgs"/> containing the event data.</param>
        public virtual void OnPaint(PaintEventArgs e)
        {
            PaintClient(e);
            if (Paint != null)
            {
                Paint(this, e);
            }
        }

        #endregion

        #region == Updating ==

        protected internal void Invalidate()
        {
            UpdateLayeredWindow();
        }

        private void UpdateLayeredWindow()
        {
            UpdateLayeredWindow(_location, _size, _alpha);
        }

        private void UpdateLayeredWindowAnimate()
        {
            UpdateLayeredWindow(true);
        }
        
        private void UpdateLayeredWindow(bool animate)
        {
            if (animate)
            {
                for (int num1 = 128; num1 < _alpha; num1 = num1 << 1)
                {
                    UpdateLayeredWindow(_location, _size, (byte) num1);
                }
            }
            UpdateLayeredWindow(_location, _size, _alpha);
        }

        private void UpdateLayeredWindow(byte alpha)
        {
            UpdateLayeredWindow(_location, _size, alpha);
        }

        private void UpdateLayeredWindow(Point point, Size size, byte alpha)
        {
            using (Bitmap bmp = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    // Paint the window on the bitmap
                    Rectangle bounds = new Rectangle(0, 0, size.Width, size.Height);
                    // First non-client area
                    OnNCPaint(new NcPaintEventArgs(g, bounds));
                    // Adjust graphics and paint client area
                    using (Region region = g.Clip)
                    {
                        Rectangle rectClient = GetClientRectangle(bounds);
                        region.Intersect(rectClient);
                        g.Clip = region;
                        g.TranslateTransform(rectClient.Left, rectClient.Top);

                        OnPaint(new PaintEventArgs(g, GetClientRectangle(bounds)));
                    }

                    PInvokeCommon.SIZE size1;
                    User32.POINT point1;
                    User32.POINT point2;

                    IntPtr ptr1 = User32.GetDC(IntPtr.Zero);
                    IntPtr ptr2 = Gdi32.CreateCompatibleDC(ptr1);
                    IntPtr ptr3 = bmp.GetHbitmap(Color.FromArgb(0));
                    try
                    {
                        IntPtr ptr4 = Gdi32.SelectObject(ptr2, ptr3);
                        size1.cx = size.Width;
                        size1.cy = size.Height;
                        point1.x = point.X;
                        point1.y = point.Y;
                        point2.x = 0;
                        point2.y = 0;
                        User32.BLENDFUNCTION blendfunction1 = new User32.BLENDFUNCTION
                        {
                            BlendOp = 0,
                            BlendFlags = 0,
                            SourceConstantAlpha = alpha,
                            AlphaFormat = 1
                        };
                        User32.UpdateLayeredWindow(Handle, ptr1, ref point1, ref size1, ptr2, ref point2, 0, ref blendfunction1, 2);
                        Gdi32.SelectObject(ptr2, ptr4);
                    }
                    finally
                    {
                        Gdi32.DeleteObject(ptr3);
                    }
                    User32.ReleaseDC(IntPtr.Zero, ptr1);
                    Gdi32.DeleteDC(ptr2);
                }
            }
        }
        
        #endregion

        #region == Show / Hide ==
        
        /// <summary>
        /// Shows the window.
        /// </summary>
        /// <remarks>
        /// Showing is done with animation.
        /// </remarks>
        public virtual void Show()
        {
            Show(X, Y);
        }

        /// <summary>
        /// Shows the window at the specified location.
        /// </summary>
        /// <param name="x">The horizontal coordinate.</param>
        /// <param name="y">The vertical coordinate.</param>
        /// <remarks>
        /// Showing is done with animation.
        /// </remarks>
        public virtual void Show(int x, int y)
        {
            Show(x, y, true);
        }

        /// <summary>
        /// Shows the window at the specified location.
        /// </summary>
        /// <param name="x">The horizontal coordinate.</param>
        /// <param name="y">The vertical coordinate.</param>
        /// <param name="animate"><b>true</b> if the showing should be done with animation; otherwise, <b>false</b>.</param>
        public virtual void Show(int x, int y, bool animate)
        {
            _location = new Point(x, y);
            if (Handle == IntPtr.Zero)
                CreateHandle(CreateParams);

            if (_supportsLayered)
            {
                if (animate)
                {
                    User32.ShowWindow(Handle, 4);
                    Thread thread1 = new Thread(UpdateLayeredWindowAnimate)
                        {IsBackground = true};
                    thread1.Start();
                }
                else
                {
                    UpdateLayeredWindow();
                }
            }
            User32.ShowWindow(Handle, 4);
        }

        /// <summary>
        /// Shows the window with a specific animation.
        /// </summary>
        /// <param name="x">The horizontal coordinate.</param>
        /// <param name="y">The vertical coordinate.</param>
        /// <param name="mode">An <see cref="AnimateMode"/> parameter.</param>
        public virtual void ShowAnimate(int x, int y, AnimateMode mode)
        {
            var flag = User32.AnimateWindowFlags.CENTER;
            switch (mode)
            {
                case AnimateMode.Blend:
                    Show(x, y, true);
                    return;
                case AnimateMode.ExpandCollapse:
                    flag = User32.AnimateWindowFlags.CENTER;
                    break;
                case AnimateMode.SlideLeftToRight:
                    flag = (User32.AnimateWindowFlags.HORIZONTAL_POSITIVE | User32.AnimateWindowFlags.SLIDE);
                    break;
                case AnimateMode.SlideRightToLeft:
                    flag = (User32.AnimateWindowFlags.HORIZONTAL_NEGATIVE | User32.AnimateWindowFlags.SLIDE);
                    break;
                case AnimateMode.SlideTopToBottom:
                    flag = (User32.AnimateWindowFlags.VERTICAL_POSITIVE | User32.AnimateWindowFlags.SLIDE);
                    break;
                case AnimateMode.SlideBottomToTop:
                    flag = (User32.AnimateWindowFlags.VERTICAL_NEGATIVE | User32.AnimateWindowFlags.SLIDE);
                    break;
                case AnimateMode.RollLeftToRight:
                    flag = (User32.AnimateWindowFlags.HORIZONTAL_POSITIVE);
                    break;
                case AnimateMode.RollRightToLeft:
                    flag = (User32.AnimateWindowFlags.HORIZONTAL_NEGATIVE);
                    break;
                case AnimateMode.RollBottomToTop:
                    flag = (User32.AnimateWindowFlags.VERTICAL_POSITIVE);
                    break;
                case AnimateMode.RollTopToBottom:
                    flag = (User32.AnimateWindowFlags.VERTICAL_NEGATIVE);
                    break;
            }
            if (_supportsLayered)
            {
                if (Handle == IntPtr.Zero)
                    CreateHandle(CreateParams);
                UpdateLayeredWindow();
                User32.AnimateWindow(Handle, 100, (int)flag);
            }
            else
            {
                Show(x, y);
            }
        }

        /// <summary>
        /// Hides the window.
        /// </summary>
        public virtual void Hide()
        {
            Capture = false;
            if (Handle != IntPtr.Zero)
            {
                User32.ShowWindow(Handle, 0);
                ReleaseHandle();
            }
        }

        private void HideWindowWithAnimation()
        {
            if (_supportsLayered)
            {
                for (int num1 = 0xff; num1 > 128; num1 = num1 >> 1)
                {
                    UpdateLayeredWindow(_location, _size, (byte)num1);
                }
            }
            Hide();
        }

        /// <summary>
        /// Hides the window with a specific animation.
        /// </summary>
        /// <param name="mode">An <see cref="AnimateMode"/> parameter.</param>
        public virtual void HideAnimate(AnimateMode mode)
        {
            var flag = User32.AnimateWindowFlags.CENTER;
            switch (mode)
            {
                case AnimateMode.Blend:
                    HideWindowWithAnimation();
                    return;
                case AnimateMode.ExpandCollapse:
                    flag = User32.AnimateWindowFlags.CENTER;
                    break;
                case AnimateMode.SlideLeftToRight:
                    flag = (User32.AnimateWindowFlags.HORIZONTAL_POSITIVE | User32.AnimateWindowFlags.SLIDE);
                    break;
                case AnimateMode.SlideRightToLeft:
                    flag = (User32.AnimateWindowFlags.HORIZONTAL_NEGATIVE | User32.AnimateWindowFlags.SLIDE);
                    break;
                case AnimateMode.SlideTopToBottom:
                    flag = (User32.AnimateWindowFlags.VERTICAL_POSITIVE | User32.AnimateWindowFlags.SLIDE);
                    break;
                case AnimateMode.SlideBottomToTop:
                    flag = (User32.AnimateWindowFlags.VERTICAL_NEGATIVE | User32.AnimateWindowFlags.SLIDE);
                    break;
                case AnimateMode.RollLeftToRight:
                    flag = (User32.AnimateWindowFlags.HORIZONTAL_POSITIVE);
                    break;
                case AnimateMode.RollRightToLeft:
                    flag = (User32.AnimateWindowFlags.HORIZONTAL_NEGATIVE);
                    break;
                case AnimateMode.RollBottomToTop:
                    flag = (User32.AnimateWindowFlags.VERTICAL_POSITIVE);
                    break;
                case AnimateMode.RollTopToBottom:
                    flag = (User32.AnimateWindowFlags.VERTICAL_NEGATIVE);
                    break;
            }
            flag |= User32.AnimateWindowFlags.HIDE;
            if (_supportsLayered)
            {
                UpdateLayeredWindow();
                User32.AnimateWindow(Handle, 100, (int)flag);
            }
            Hide();
        }

        #endregion

        #region == Mouse ==

        public Point PointToClient(Point ptScreen)
        {
            User32.POINT pnt;
            pnt.x = ptScreen.X;
            pnt.y = ptScreen.Y;
            User32.ScreenToClient(Handle, ref pnt);
            return new Point(pnt.x, pnt.y);
        }

        public Point PointToScreen(Point ptClient)
        {
            User32.POINT pnt;
            pnt.x = ptClient.X;
            pnt.y = ptClient.Y;
            User32.ClientToScreen(Handle, ref pnt);
            return new Point(pnt.x, pnt.y);
        }

        private static void PerformWmMouseDown(ref Message m)
        {
        }

        protected virtual void OnMouseDown(MouseEventArgs e)
        {
            if (MouseDown != null)
            {
                MouseDown(this, e);
            }
            _onMouseDown = true;
        }

        private static void PerformWmMouseMove(ref Message m)
        {
        }

        protected virtual void OnMouseMove(MouseEventArgs e)
        {
            if (MouseMove != null)
            {
                MouseMove(this, e);
            }

            _onMouseMove = true;
        }

// ReSharper disable UnusedParameter.Local
        private static void PerformWmMouseUp(ref Message m)
// ReSharper restore UnusedParameter.Local
        {
        }

        protected virtual void OnMouseUp(MouseEventArgs e)
        {
            if (MouseUp != null)
            {
                MouseUp(this, e);
            }
            _onMouseUp = true;
        }

        private static void PerformWmMouseActivate(ref Message m)
        {
            m.Result = (IntPtr) 3;
        }

        protected virtual void OnMouseEnter()
        {
            if (MouseEnter != null)
            {
                MouseEnter(this, EventArgs.Empty);
            }
        }
        protected virtual void OnMouseLeave()
        {
            if (MouseLeave != null)
            {
                MouseLeave(this, EventArgs.Empty);
            }
        }

        #endregion

        #region == Other messages ==

        private static bool PerformWmNcHitTest(ref Message m)
        {
//          POINT point1;
//          Point p = Control.MousePosition;
//          point1.x = p.X;
//          point1.y = p.Y;
//          point1 = MousePositionToClient(point1);

            m.Result = (IntPtr) (-1);
            return true;
        }

        private void PerformWmNcPaint(ref Message m)
        {
            IntPtr hWnd = m.HWnd;
            IntPtr hRgn = m.WParam;

            Rectangle bounds = Bounds;
            bounds.Offset(-bounds.Left, -bounds.Top);

            const DCX flags = DCX.DCX_WINDOW | DCX.DCX_INTERSECTRGN;
            IntPtr hDC = User32.GetDCEx(hWnd, hRgn, (uint)flags);
            if (hDC == IntPtr.Zero)
            {
                hDC = User32.GetWindowDC(hWnd);
                if (hDC == IntPtr.Zero)
                    return;
            }

            using (Graphics g = Graphics.FromHdc(hDC))
            {
                OnNCPaint(new NcPaintEventArgs(g, bounds));
            }
        }

        protected virtual void OnNCPaint(NcPaintEventArgs e)
        {
            PaintShadow(e);
            PaintBorder(e);
        }

        private void PerformWmNcCalcSize(ref Message m)
        {
            if (m.WParam == User32.False)
            {
                var rect1 = (User32.RECT) m.GetLParam(typeof(User32.RECT));
                var rectProposed = rect1.Rectangle;
                OnNcCalcSize(ref rectProposed);
                rect1 = User32.RECT.FromRectangle(rectProposed);
                Marshal.StructureToPtr(rect1, m.LParam, false);
                m.Result = IntPtr.Zero;
            }
            else if (m.WParam == User32.True)
            {
                var ncParams = (NCCALCSIZE_PARAMS)
                    m.GetLParam(typeof(NCCALCSIZE_PARAMS));
                var rectProposed = ncParams.rectProposed.Rectangle;
                OnNcCalcSize(ref rectProposed);
                ncParams.rectProposed = User32.RECT.FromRectangle(rectProposed);
                Marshal.StructureToPtr(ncParams, m.LParam, false);
                m.Result = IntPtr.Zero;
            }
        }

        protected virtual void OnNcCalcSize(ref Rectangle rectProposed)
        {
            rectProposed = GetClientRectangle(rectProposed);
        }
        
// ReSharper disable UnusedParameter.Local
        private static void PerformWmSetCursor(ref Message m)
// ReSharper restore UnusedParameter.Local
        {
        }

        private void PerformWmPaint(ref Message m)
        {
            var ps = new User32.PAINTSTRUCT();
            Rectangle rectClient = ClientRectangle;
            IntPtr hDC = User32.BeginPaint(m.HWnd, ref ps);
            using (Graphics g = Graphics.FromHdc(hDC))
            {
                using (Bitmap bitmap1 = new Bitmap(rectClient.Width, rectClient.Height))
                {
                    using (Graphics graphics2 = Graphics.FromImage(bitmap1))
                    {
                        OnPaint(new PaintEventArgs(graphics2, rectClient));
                    }
                    g.DrawImageUnscaled(bitmap1, 0, 0);
                }
            }
            User32.EndPaint(m.HWnd, ref ps);
        }

        protected override void WndProc(ref Message m)
        {
            var msgID = (User32.WinMessageType)m.Msg;

            switch (msgID)
            {
                case User32.WinMessageType.WM_PAINT:
                {
                    PerformWmPaint(ref m);
                    return;
                }
                case User32.WinMessageType.WM_ERASEBKGND:
                {
                    m.Result = IntPtr.Zero;
                    return;
                }
                case User32.WinMessageType.WM_SETCURSOR:
                {
                    PerformWmSetCursor(ref m);
                    return;
                }
                case User32.WinMessageType.WM_MOUSEACTIVATE:
                {
                    PerformWmMouseActivate(ref m);
                    return;
                }
                case User32.WinMessageType.WM_CALCSIZE:
                {
                    PerformWmNcCalcSize(ref m);
                    return;
                }
                case User32.WinMessageType.WM_NCHITTEST:
                {
                    if (!PerformWmNcHitTest(ref m))
                    {
                        WndProc(ref m);
                    }
                    return;
                }
                case User32.WinMessageType.WM_NCPAINT:
                {
                    PerformWmNcPaint(ref m);
                    m.Result = IntPtr.Zero;
                    return;
                }
                case User32.WinMessageType.WM_MOUSEMOVE:
                {
                    if (!_isMouseIn)
                    {
                        OnMouseEnter();
                        _isMouseIn = true;
                    }
                    Point p6 = new Point(m.LParam.ToInt32());
                    OnMouseMove(new MouseEventArgs(Control.MouseButtons, 1, p6.X, p6.Y, 0));
                    if (_onMouseMove)
                    {
                        PerformWmMouseMove(ref m);
                        _onMouseMove = false;
                    }
                    break;
                }
                case User32.WinMessageType.WM_LBUTTONDOWN:
                {
                    _lastMouseDown = new Point(m.LParam.ToInt32());
                    OnMouseDown(new MouseEventArgs(Control.MouseButtons, 1, _lastMouseDown.X, _lastMouseDown.Y, 0));
                    if (_onMouseDown)
                    {
                        PerformWmMouseDown(ref m);
                        _onMouseDown = false;
                    }

                    return;
                }
                case User32.WinMessageType.WM_LBUTTONUP:
                {
                    Point p = new Point(m.LParam.ToInt32());
                    OnMouseUp(new MouseEventArgs(Control.MouseButtons, 1, p.X, p.Y, 0));
                    if (_onMouseUp)
                    {
                        PerformWmMouseUp(ref m);
                        _onMouseUp = false;
                    }
                    return;
                }
                case User32.WinMessageType.WM_MOUSELEAVE:
                {
                    if (_isMouseIn)
                    {
                        OnMouseLeave();
                        _isMouseIn = false;
                    }
                    break;
                }
            }

            base.WndProc(ref m);
        }

        #endregion

        #region == Event Methods ==

        protected virtual void OnLocationChanged(EventArgs e)
        {
            OnMove(EventArgs.Empty);
            if (LocationChanged != null)
            {
                LocationChanged(this, e);
            }
        }

        protected virtual void OnSizeChanged(EventArgs e)
        {
            OnResize(EventArgs.Empty);
            if (SizeChanged != null)
            {
                SizeChanged(this, e);
            }
        }

        protected virtual void OnMove(EventArgs e)
        {
            if (Move != null)
            {
                Move(this, e);
            }
        }

        protected virtual void OnResize(EventArgs e)
        {
            if (Resize != null)
            {
                Resize(this, e);
            }
        }

        #endregion

        #region == Size and Location ==

        protected virtual void SetBoundsCore(int x, int y, int width, int height)
        {
            if (width < (11+11+4+4))
                width = 11+11+4+4;

            if (((X != x) || (Y != y)) || ((Width != width) || (Height != height)))
            {
                if (Handle != IntPtr.Zero)
                {
                    var flags = User32.SetWindowPosFlags.NOACTIVATE | User32.SetWindowPosFlags.NOZORDER;
                    if ((X == x) && (Y == y))
                    {
                        flags |= User32.SetWindowPosFlags.NOMOVE;
                    }
                    if ((Width == width) && (Height == height))
                    {
                        flags |= User32.SetWindowPosFlags.NOSIZE;
                    }
                    User32.SetWindowPos(Handle, IntPtr.Zero, x, y, width, height, flags);
                }
                else
                {
                    UpdateBounds(x, y, width, height);
                }
            }
        }

/*
        private void UpdateBounds()
        {
            var rect1 = new User32.RECT();
            User32.GetWindowRect(Handle, ref rect1);
            if (User32.GetParent(Handle)!=IntPtr.Zero)
            {
                User32.MapWindowPoints(IntPtr.Zero, User32.GetParent(Handle), ref rect1, 2);
            }
            UpdateBounds(rect1.left, rect1.top, rect1.right - rect1.left, rect1.bottom - rect1.top);
        }
*/

        private void UpdateBounds(int x, int y, int width, int height)
        {
            var rect1 = new User32.RECT();
            CreateParams params1 = CreateParams;
            User32.AdjustWindowRectEx(ref rect1, params1.Style, false, params1.ExStyle);

            bool locationChanged = (X != x) || (Y != y);
            bool sizeChanged = (((Width != width) || (Height != height)));
            _size = new Size(width, height);
            _location = new Point(x, y);
            if (locationChanged)
            {
                OnLocationChanged(EventArgs.Empty);
            }
            if (sizeChanged)
            {
                OnSizeChanged(EventArgs.Empty);
            }
        }

        #endregion

        #region == Various ==

        private void CustomTip_HandleDestroyed(object sender, EventArgs e)
        {
            _parent.HandleDestroyed -= CustomTip_HandleDestroyed;
            Hide();
        }

        public void Destroy()
        {
            Hide();
            Dispose();
        }
        
        #endregion

        #endregion

        #region #  Properties  #

        protected virtual CreateParams CreateParams
        {
            get
            {
                CreateParams params1 = new CreateParams();
                Size size1 = _size;
                Point point1 = _location;
                params1.X = _location.X;
                params1.Y = _location.Y;
                params1.Height = size1.Height;
                params1.Width = size1.Width;
                params1.Parent = _parent != null ? _parent.Handle : IntPtr.Zero;
                params1.Style = -2147483648;
                params1.ExStyle = 0x88;      // WS_EX_TOOLWINDOW | WS_EX_TOPMOST
                if (_supportsLayered)
                {
                    params1.ExStyle += 0x80000; // WS_EX_LAYERED
                }
                _size = size1;
                _location = point1;
                return params1;
            }
        }

        public Control Parent
        {
            get { return _parent; }
            set
            {
                if (value == _parent)
                    return;

                if (_parent != null)
                {
                    _parent.HandleDestroyed -= CustomTip_HandleDestroyed;
                }

                _parent = value;
                if (_parent != null)
                {
                    _parent.HandleDestroyed += CustomTip_HandleDestroyed;
                }
            }
        }

        public Rectangle Bounds
        {
            get
            {
                return new Rectangle(_location, _size);
            }
            set
            {
                _location = value.Location;
                _size = value.Size;
                if (Handle != IntPtr.Zero)
                {
                    SetBoundsCore(_location.X, _location.Y, _size.Width, _size.Height);
                    var rect = new User32.RECT();
                    User32.GetWindowRect(Handle, ref rect);
                    Rectangle rectangle = rect.Rectangle;
                    _location = rectangle.Location;
                    _size = rectangle.Size;
                }
            }
        }

        public virtual Size Size
        {
            get { return _size; }
            set
            {
                Bounds = new Rectangle(Location, value);
            }
        }

        public virtual Point Location
        {
            get { return _location; }
            set
            {
                Bounds = new Rectangle(value, Size);
            }
        }

        public int Height
        {
            get { return _size.Height; }
            set
            {
                Size = new Size(_size.Width, value);
            }
        }

        public int Width
        {
            get { return _size.Width; }
            set
            {
                Size = new Size(value, _size.Height);
            }
        }

        public int X
        {
            get { return _location.X; }
            set
            {
                Location = new Point(value, _location.Y);
            }
        }

        public int Y
        {
            get { return _location.Y; }
            set
            {
                Location = new Point(_location.X, value);
            }
        }

        public Rectangle ClientRectangle
        {
            get
            {
                Rectangle rectClient = GetClientRectangle(Bounds);
                rectClient.Offset(-rectClient.Left, -rectClient.Top);
                return rectClient;
            }
        }

        public Size ClientSize
        {
            get { return ClientRectangle.Size; }
            set
            {
                Size = new Size(value.Width + SHADOW_LENGTH + 1,
                    value.Height + SHADOW_LENGTH + 1);
            }
        }

        protected static Rectangle GetClientRectangle(Rectangle bounds)
        {
            Rectangle rectClient = GetBorderRectangle(bounds);
            rectClient.Inflate(-1, -1); // Remove border
            return rectClient;
        }

        protected static Rectangle GetBorderRectangle(Rectangle bounds)
        {
            return new Rectangle(bounds.Left, bounds.Top,
                bounds.Width - SHADOW_LENGTH, bounds.Height - SHADOW_LENGTH);
        }

        protected Rectangle GetShadowRectangle(Rectangle bounds)
        {
            if (!_hasShadow)
                return Rectangle.Empty;

            Rectangle rectShadow = GetBorderRectangle(bounds);
            rectShadow.Offset(SHADOW_LENGTH, SHADOW_LENGTH);
            return rectShadow;
        }

        public bool Visible
        {
            get { return User32.IsWindowVisible(Handle); }
            set
            {
                if (value != Visible)
                {
                    if (value)
                        Show();
                    else
                        Hide();
                }
            }
        }

        public bool HasShadow
        {
            get { return _hasShadow; }
            set
            {
                _hasShadow = value;
                Invalidate();
            }
        }

        public int Alpha
        {
            get { return _alpha; }
            set
            {
                if (_alpha == value) return;
                if (value < 0 || value > 255)
                {
                    throw new ArgumentException(@"Alpha must be between 0 and 255");
                }
                _alpha = (byte)value;
                UpdateLayeredWindow(_alpha);
            }
        }

        public bool Capture
        {
            get { return _captured; }
            set
            {
                if (_captured != value)
                {
                    if (value)
                        User32.SetCapture(Handle);
                    else
                        User32.ReleaseCapture();
                    _captured = value;
                }
            }
        }

        #endregion

        #region #  Events  #

// ReSharper disable EventNeverSubscribedTo.Global
        public event PaintEventHandler Paint;
        public event EventHandler SizeChanged;
        public event EventHandler LocationChanged;
        public event EventHandler Move;
        public event EventHandler Resize;
        public event MouseEventHandler MouseDown;
        public event MouseEventHandler MouseUp;
        public event MouseEventHandler MouseMove;
        public event EventHandler MouseEnter;
        public event EventHandler MouseLeave;
// ReSharper restore EventNeverSubscribedTo.Global

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    if (_parent != null)
                    {
                        _parent.HandleDestroyed -= CustomTip_HandleDestroyed;
                    }
                }
                DestroyHandle();
                _disposed = true;
            }
        }

        #endregion
    }

    public class NcPaintEventArgs
    {
        public NcPaintEventArgs(Graphics g, Rectangle bounds)
        {
            Graphics = g;
            Bounds = bounds;
        }

        public Graphics Graphics { get; private set; }
        public Rectangle Bounds { get; private set; }
    }

    #region Win32

    // ReSharper disable once InconsistentNaming IdentifierTypo
    [StructLayout(LayoutKind.Sequential)]
    internal struct NCCALCSIZE_PARAMS
    {
        public User32.RECT rectProposed;
        public User32.RECT rectWndBefore;
        public User32.RECT rectClientBefore;
        public IntPtr lppos;
    }

    [Flags]
    public enum AnimateMode : uint
    {
        // ReSharper disable InconsistentNaming
        SlideRightToLeft,
        SlideLeftToRight,
        SlideTopToBottom,
        SlideBottomToTop,
        RollRightToLeft,
        RollLeftToRight,
        RollTopToBottom,
        RollBottomToTop,
        Blend,
        ExpandCollapse
        // ReSharper restore InconsistentNaming
    }

    [Flags]
    internal enum DCX : uint
    {
        DCX_WINDOW = 0x0001,
        DCX_CACHE = 0x0002,
        // ReSharper disable IdentifierTypo
        DCX_CLIPSIBLINGS = 0x0010,
        DCX_INTERSECTRGN = 0x0080
        // ReSharper restore IdentifierTypo
    }

    #endregion
}
