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
        private readonly Color _backColor = Color.LightYellow; // color of tootlip box background (MS tooltip yellow);

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

                    SIZE size1;
                    POINT point1;
                    POINT point2;

                    IntPtr ptr1 = User32.GetDC(IntPtr.Zero);
                    IntPtr ptr2 = Gdi32.CreateCompatibleDC(ptr1);
                    IntPtr ptr3 = bmp.GetHbitmap(Color.FromArgb(0));
                    IntPtr ptr4 = Gdi32.SelectObject(ptr2, ptr3);
                    size1.cx = size.Width;
                    size1.cy = size.Height;
                    point1.x = point.X;
                    point1.y = point.Y;
                    point2.x = 0;
                    point2.y = 0;
                    BLENDFUNCTION blendfunction1 = new BLENDFUNCTION
                        {
                            BlendOp = 0,
                            BlendFlags = 0,
                            SourceConstantAlpha = alpha,
                            AlphaFormat = 1
                        };
                    User32.UpdateLayeredWindow(Handle, ptr1, ref point1, ref size1, ptr2, ref point2, 0, ref blendfunction1, 2);
                    Gdi32.SelectObject(ptr2, ptr4);
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
			uint flag = AnimateWindow.AW_CENTER;
			switch (mode)
			{
				case AnimateMode.Blend:
					Show(x, y, true);
					return;
				case AnimateMode.ExpandCollapse:
					flag = AnimateWindow.AW_CENTER;
					break;
				case AnimateMode.SlideLeftToRight:
					flag = (AnimateWindow.AW_HOR_POSITIVE | AnimateWindow.AW_SLIDE);
					break;
				case AnimateMode.SlideRightToLeft:
					flag = (AnimateWindow.AW_HOR_NEGATIVE | AnimateWindow.AW_SLIDE);
					break;
                case AnimateMode.SlideTopToBottom:
                    flag = (AnimateWindow.AW_VER_POSITIVE | AnimateWindow.AW_SLIDE);
                    break;
                case AnimateMode.SlideBottomToTop:
					flag = (AnimateWindow.AW_VER_NEGATIVE | AnimateWindow.AW_SLIDE);
					break;
				case AnimateMode.RollLeftToRight:
					flag = (AnimateWindow.AW_HOR_POSITIVE);
					break;
				case AnimateMode.RollRightToLeft:
					flag = (AnimateWindow.AW_HOR_NEGATIVE);
					break;
				case AnimateMode.RollBottomToTop:
					flag = (AnimateWindow.AW_VER_POSITIVE);
					break;
				case AnimateMode.RollTopToBottom:
					flag = (AnimateWindow.AW_VER_NEGATIVE);
					break;
			}
			if (_supportsLayered)
			{
                if (Handle == IntPtr.Zero)
                    CreateHandle(CreateParams);
                UpdateLayeredWindow();
				User32.AnimateWindow(Handle, 100, flag);
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
			uint flag = AnimateWindow.AW_CENTER;
			switch (mode)
			{
				case AnimateMode.Blend:
					HideWindowWithAnimation();
					return;
				case AnimateMode.ExpandCollapse:
					flag = AnimateWindow.AW_CENTER;
					break;
				case AnimateMode.SlideLeftToRight:
					flag = (AnimateWindow.AW_HOR_POSITIVE | AnimateWindow.AW_SLIDE);
					break;
				case AnimateMode.SlideRightToLeft:
					flag = (AnimateWindow.AW_HOR_NEGATIVE | AnimateWindow.AW_SLIDE);
					break;
				case AnimateMode.SlideTopToBottom:
					flag = (AnimateWindow.AW_VER_POSITIVE | AnimateWindow.AW_SLIDE);
					break;
                case AnimateMode.SlideBottomToTop:
                    flag = (AnimateWindow.AW_VER_NEGATIVE | AnimateWindow.AW_SLIDE);
                    break;
                case AnimateMode.RollLeftToRight:
					flag = (AnimateWindow.AW_HOR_POSITIVE);
					break;
				case AnimateMode.RollRightToLeft:
					flag = (AnimateWindow.AW_HOR_NEGATIVE);
					break;
				case AnimateMode.RollBottomToTop:
					flag = (AnimateWindow.AW_VER_POSITIVE);
					break;
				case AnimateMode.RollTopToBottom:
					flag = (AnimateWindow.AW_VER_NEGATIVE);
					break;
			}
			flag |= AnimateWindow.AW_HIDE;
			if (_supportsLayered)
			{
				UpdateLayeredWindow();
				User32.AnimateWindow(Handle, 100, flag);
			}
			Hide();
		}

		#endregion

		#region == Mouse ==

        public Point PointToClient(Point ptScreen)
        {
            POINT pnt;
            pnt.x = ptScreen.X;
            pnt.y = ptScreen.Y;
            User32.ScreenToClient(Handle, ref pnt);
            return new Point(pnt.x, pnt.y);
        }

        public Point PointToScreen(Point ptClient)
        {
            POINT pnt;
            pnt.x = ptClient.X;
            pnt.y = ptClient.Y;
            User32.ClientToScreen(Handle, ref pnt);
            return new Point(pnt.x, pnt.y);
        }

/*
        private POINT MousePositionToClient(POINT point)
		{
			POINT point1;
			point1.x = point.x;
			point1.y = point.y;
			User32.ScreenToClient(Handle, ref point1);
			return point1;
		}
*/

/*
		private POINT MousePositionToScreen(POINT point)
		{
			POINT point1;
			point1.x = point.x;
			point1.y = point.y;
			User32.ClientToScreen(Handle, ref point1);
			return point1;
		}
*/

/*
		private POINT MousePositionToScreen(Message msg)
		{
			POINT point1;
			point1.x = (short) (((int) msg.LParam) & 0xffff);
			point1.y = (short) ((((int) msg.LParam) & -65536) >> 0x10);
			if ((((msg.Msg != 0xa2) && (msg.Msg != 0xa8)) && ((msg.Msg != 0xa5) && (msg.Msg != 0xac))) && (((msg.Msg != 0xa1) && (msg.Msg != 0xa7)) && ((msg.Msg != 0xa4) && (msg.Msg != 0xab))))
			{
				User32.ClientToScreen(msg.HWnd, ref point1);
			}
			return point1;
		}
*/

// ReSharper disable UnusedParameter.Local
		private static void PerformWmMouseDown(ref Message m)
// ReSharper restore UnusedParameter.Local
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

// ReSharper disable UnusedParameter.Local
        private static void PerformWmMouseMove(ref Message m)
// ReSharper restore UnusedParameter.Local
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
//			POINT point1;
//			Point p = Control.MousePosition;
//			point1.x = p.X;
//			point1.y = p.Y;
//			point1 = MousePositionToClient(point1);

			m.Result = (IntPtr) (-1);
			return true;
		}

        private void PerformWmNcPaint(ref Message m)
        {
            IntPtr hWnd = m.HWnd;
            IntPtr hRgn = m.WParam;

            Rectangle bounds = Bounds;
            bounds.Offset(-bounds.Left, -bounds.Top);

            const uint flags = (uint)(DCX.DCX_WINDOW |
                                      DCX.DCX_INTERSECTRGN);
            IntPtr hDC = User32.GetDCEx(hWnd, hRgn, flags);
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
            if (m.WParam == User32.FALSE)
            {
                RECT rect1 = (RECT) m.GetLParam(typeof(RECT));
                Rectangle rectProposed = rect1.Rectangle;
                OnNcCalcSize(ref rectProposed);
                rect1 = RECT.FromRectangle(rectProposed);
                Marshal.StructureToPtr(rect1, m.LParam, false);
                m.Result = IntPtr.Zero;
            }
            else if (m.WParam == User32.TRUE)
            {
                NCCALCSIZE_PARAMS ncParams = (NCCALCSIZE_PARAMS)
                    m.GetLParam(typeof(NCCALCSIZE_PARAMS));
                Rectangle rectProposed = ncParams.rectProposed.Rectangle;
                OnNcCalcSize(ref rectProposed);
                ncParams.rectProposed = RECT.FromRectangle(rectProposed);
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
            PAINTSTRUCT ps = new PAINTSTRUCT();
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
			int num1 = m.Msg;
			switch (num1)
			{
                case (int) WinMsg.WM_PAINT:
                {
                    PerformWmPaint(ref m);
                    return;
                }
                case (int) WinMsg.WM_ERASEBKGND:
		        {
                    m.Result = IntPtr.Zero;
                    return;
		        }
                case (int) WinMsg.WM_SETCURSOR:
				{
					PerformWmSetCursor(ref m);
					return;
				}
				case (int) WinMsg.WM_MOUSEACTIVATE:
				{
					PerformWmMouseActivate(ref m);
					return;
				}
                case (int) WinMsg.WM_CALCSIZE:
                {
                    PerformWmNcCalcSize(ref m);
                    return;
                }
                case (int) WinMsg.WM_NCHITTEST:
				{
					if (!PerformWmNcHitTest(ref m))
					{
						WndProc(ref m);
					}
					return;
				}
                case (int) WinMsg.WM_NCPAINT:
                {
                    PerformWmNcPaint(ref m);
                    m.Result = IntPtr.Zero;
                    return;
                }
                case (int) WinMsg.WM_MOUSEMOVE:
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
				case (int) WinMsg.WM_LBUTTONDOWN:
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
				case (int) WinMsg.WM_LBUTTONUP:
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
				case (int) WinMsg.WM_MOUSELEAVE:
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
					int num1 = 20;
					if ((X == x) && (Y == y))
					{
						num1 |= 2;
					}
					if ((Width == width) && (Height == height))
					{
						num1 |= 1;
					}
					User32.SetWindowPos(Handle, IntPtr.Zero, x, y, width, height, (uint)num1);
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
		    RECT rect1 = new RECT();
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
			RECT rect1 = new RECT();
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
					value.HandleDestroyed -= CustomTip_HandleDestroyed;
				}

				_parent = value;
				if (value != null)
				{
					value.HandleDestroyed += CustomTip_HandleDestroyed;
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
                    RECT rect = new RECT();
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
                    throw new ArgumentException("Alpha must be between 0 and 255"); // Not L10N
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

	#region #  Win32  #

    // ReSharper disable InconsistentNaming
    internal struct PAINTSTRUCT
	{
// ReSharper disable UnusedField.Compiler
		public IntPtr hdc;
		public int fErase;
		public Rectangle rcPaint;
		public int fRestore;
		public int fIncUpdate;
		public int Reserved1;
		public int Reserved2;
		public int Reserved3;
		public int Reserved4;
		public int Reserved5;
		public int Reserved6;
		public int Reserved7;
		public int Reserved8;
// ReSharper restore UnusedField.Compiler
    }
	[StructLayout(LayoutKind.Sequential)]
	internal struct POINT
	{
		public int x;
		public int y;

        public Point Point
        {
            get
            {
                return new Point(x, y);
            }
        }
	}
	[StructLayout(LayoutKind.Sequential)]
	internal struct RECT
	{
// ReSharper disable FieldCanBeMadeReadOnly.Global
		public int left;
		public int top;
		public int right;
		public int bottom;
// ReSharper restore FieldCanBeMadeReadOnly.Global

        public RECT(int left, int top, int right, int bottom)
        {
            this.left = left;
            this.top = top;
            this.right = right;
            this.bottom = bottom;
        }

        public Rectangle Rectangle
        {
            get
            {
                return new Rectangle(left, top, right - left, bottom - top);
            }
        }

        public static RECT FromRectangle(Rectangle rect)
        {
            return new RECT(rect.Left, rect.Top, rect.Right, rect.Bottom);
        }
    }
	[StructLayout(LayoutKind.Sequential)]
	internal struct SIZE
	{
		public int cx;
		public int cy;

        public Size Size
        {
            get
            {
                return new Size(cx, cy);
            }
        }
	}
    [StructLayout(LayoutKind.Sequential)]
    internal struct NCCALCSIZE_PARAMS
    {
        public RECT rectProposed;
        public RECT rectWndBefore;
        public RECT rectClientBefore;
        public IntPtr lppos;
    }
    [StructLayout(LayoutKind.Sequential)]
    //[CLSCompliant(false)]
	internal struct TRACKMOUSEEVENTS
	{
// ReSharper disable FieldCanBeMadeReadOnly.Global
		public uint cbSize;
		public uint dwFlags;
		public IntPtr hWnd;
		public uint dwHoverTime;
// ReSharper restore FieldCanBeMadeReadOnly.Global
    }
	[StructLayout(LayoutKind.Sequential, Pack=1)]
	internal struct BLENDFUNCTION
	{
		public byte BlendOp;
		public byte BlendFlags;
		public byte SourceConstantAlpha;
		public byte AlphaFormat;
	}
    [StructLayout(LayoutKind.Sequential)]
    //[CLSCompliant(false)]
    public struct LOGBRUSH
    {
// ReSharper disable FieldCanBeMadeReadOnly.Global
        public uint lbStyle;
        public uint lbColor;
        public uint lbHatch;
// ReSharper restore FieldCanBeMadeReadOnly.Global
    }
// ReSharper disable once ClassNeverInstantiated.Global
    internal class AnimateWindow
	{
		private AnimateWindow() 
		{
		}

        public const int AW_HOR_POSITIVE = 0x1;
        public const int AW_HOR_NEGATIVE = 0x2;
        public const int AW_VER_POSITIVE = 0x4;
        public const int AW_VER_NEGATIVE = 0x8;
        public const int AW_CENTER = 0x10;
        public const int AW_HIDE = 0x10000;
        public const int AW_ACTIVATE = 0x20000;
        public const int AW_SLIDE = 0x40000;
        public const int AW_BLEND = 0x80000;
	}
	public enum AnimateMode
	{
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
	}
    internal enum WinMsg
    {
        WM_PAINT = 0x000F,
        WM_ERASEBKGND = 0x0014,
        WM_SETCURSOR = 0x0020,
		WM_MOUSEACTIVATE = 0x0021,
        WM_CALCSIZE = 0x0083,
        WM_NCHITTEST = 0x0084,
        WM_NCPAINT = 0x0085,
        WM_CHAR = 0x0102,
        WM_TIMER = 0x0113,
        WM_MOUSEMOVE = 0x0200, 
		WM_LBUTTONDOWN = 0x0201,
		WM_LBUTTONUP = 0x0202,
		WM_MOUSELEAVE = 0x02A3
    }
	internal static class User32
	{
        public static IntPtr FALSE = new IntPtr(0);
        public static IntPtr TRUE = new IntPtr(1);

        // Methods
	    // Not L10N
		[DllImport("User32.dll", CharSet=CharSet.Auto)]
		internal static extern bool AnimateWindow(IntPtr hWnd, uint dwTime, uint dwFlags);
		[DllImport("User32.dll", CharSet=CharSet.Auto)]
		internal static extern IntPtr BeginPaint(IntPtr hWnd, ref PAINTSTRUCT ps);
		[DllImport("User32.dll", CharSet=CharSet.Auto)]
		internal static extern bool ClientToScreen(IntPtr hWnd, ref POINT pt);
		[DllImport("User32.dll", CharSet=CharSet.Auto)]
		internal static extern bool DrawFocusRect(IntPtr hWnd, ref RECT rect);
		[DllImport("User32.dll", CharSet=CharSet.Auto)]
		internal static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT ps);
		[DllImport("User32.dll", CharSet=CharSet.Auto)]
		internal static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("User32.dll", CharSet = CharSet.Auto)]
        internal static extern IntPtr GetWindowDC(IntPtr hWnd);
        [DllImport("User32.dll", CharSet = CharSet.Auto)]
        internal static extern IntPtr GetDCEx(IntPtr hWnd, IntPtr hRgn, uint dwFlags);
        [DllImport("User32.dll", CharSet = CharSet.Auto)]
		internal static extern IntPtr GetFocus();
		[DllImport("User32.dll", CharSet=CharSet.Auto)]
		internal static extern ushort GetKeyState(int virtKey);
		[DllImport("User32.dll", CharSet=CharSet.Auto)]
		internal static extern IntPtr GetParent(IntPtr hWnd);
		[DllImport("user32.dll", CharSet=CharSet.Auto, ExactSpelling=true)]
		public static extern bool GetClientRect(IntPtr hWnd, [In, Out] ref RECT rect);
		[DllImport("User32.dll", CharSet=CharSet.Auto)]
		internal static extern int GetWindowLong(IntPtr hWnd, int nIndex);
		[DllImport("User32.dll", CharSet=CharSet.Auto)]
		internal static extern IntPtr GetWindow(IntPtr hWnd, int cmd);
		[DllImport("User32.dll", CharSet=CharSet.Auto)]
		internal static extern bool GetWindowRect(IntPtr hWnd, ref RECT rect);
		[DllImport("User32.dll", CharSet=CharSet.Auto)]
		internal static extern bool HideCaret(IntPtr hWnd);
		[DllImport("User32.dll", CharSet=CharSet.Auto)]
		internal static extern bool InvalidateRect(IntPtr hWnd, ref RECT rect, bool erase);
		[DllImport("User32.dll", CharSet=CharSet.Auto)]
		internal static extern IntPtr LoadCursor(IntPtr hInstance, uint cursor);
		[DllImport("user32.dll", CharSet=CharSet.Auto, ExactSpelling=true)]
		public static extern int MapWindowPoints(IntPtr hWndFrom, IntPtr hWndTo, [In, Out] ref RECT rect, int cPoints);
		[DllImport("User32.dll", CharSet=CharSet.Auto)]
		internal static extern bool MoveWindow(IntPtr hWnd, int x, int y, int width, int height, bool repaint);
		[DllImport("User32.dll", CharSet=CharSet.Auto)]
		internal static extern bool PostMessage(IntPtr hWnd, int Msg, uint wParam, uint lParam);
		[DllImport("User32.dll", CharSet=CharSet.Auto)]
		internal static extern bool ReleaseCapture();
		[DllImport("User32.dll", CharSet=CharSet.Auto)]
		internal static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
		[DllImport("User32.dll", CharSet=CharSet.Auto)]
		internal static extern bool ScreenToClient(IntPtr hWnd, ref POINT pt);
		[DllImport("User32.dll", CharSet=CharSet.Auto)]
		internal static extern uint SendMessage(IntPtr hWnd, int Msg, uint wParam, uint lParam);
		[DllImport("User32.dll", CharSet=CharSet.Auto)]
		internal static extern IntPtr SetCursor(IntPtr hCursor);
		[DllImport("User32.dll", CharSet=CharSet.Auto)]
		internal static extern IntPtr SetFocus(IntPtr hWnd);
		[DllImport("User32.dll", CharSet=CharSet.Auto)]
		internal static extern int SetWindowLong(IntPtr hWnd, int nIndex, int newLong);
		[DllImport("User32.dll", CharSet=CharSet.Auto)]
		internal static extern int SetWindowPos(IntPtr hWnd, IntPtr hWndAfter, int X, int Y, int Width, int Height, uint flags);
		[DllImport("User32.dll", CharSet=CharSet.Auto)]
		internal static extern bool SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool redraw);
		[DllImport("User32.dll", CharSet=CharSet.Auto)]
		internal static extern bool ShowCaret(IntPtr hWnd);
		[DllImport("User32.dll", CharSet=CharSet.Auto)]
		internal static extern bool SetCapture(IntPtr hWnd);
		[DllImport("User32.dll", CharSet=CharSet.Auto)]
		internal static extern int ShowWindow(IntPtr hWnd, short cmdShow);
		[DllImport("User32.dll", CharSet=CharSet.Auto)]
		internal static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref int bRetValue, uint fWinINI);
		[DllImport("User32.dll", CharSet=CharSet.Auto)]
		internal static extern bool TrackMouseEvent(ref TRACKMOUSEEVENTS tme);
		[DllImport("User32.dll", CharSet=CharSet.Auto)]
		internal static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pprSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
		[DllImport("User32.dll", CharSet=CharSet.Auto)]
		internal static extern bool UpdateWindow(IntPtr hwnd);
		[DllImport("User32.dll", CharSet=CharSet.Auto)]
		internal static extern bool WaitMessage();
		[DllImport("user32.dll", CharSet=CharSet.Auto, ExactSpelling=true)]
		public static extern bool AdjustWindowRectEx(ref RECT lpRect, int dwStyle, bool bMenu, int dwExStyle);
        [DllImport("User32.dll", CharSet = CharSet.Auto)]
        internal static extern bool IsWindowVisible(IntPtr hwnd);

        public static Control GetFocusedControl()
        {
            Control focusedControl = null;
            // To get hold of the focused control:
            IntPtr focusedHandle = GetFocus();
            if (focusedHandle != IntPtr.Zero)
            {
                // If the focused Control is not a .Net control, then this will return null.
                focusedControl = Control.FromHandle(focusedHandle);
            }
            return focusedControl;
        }
    }
    [Flags]
    internal enum DCX
    {
        DCX_WINDOW = 0x0001,
        DCX_CACHE = 0x0002,
        DCX_CLIPSIBLINGS = 0x0010,
        DCX_INTERSECTRGN = 0x0080
    }
	internal static class Gdi32
	{
		// Methods
	    // Not L10N
		[DllImport("gdi32.dll", CharSet=CharSet.Auto)]
		internal static extern int CombineRgn(IntPtr dest, IntPtr src1, IntPtr src2, int flags);
		[DllImport("gdi32.dll", CharSet=CharSet.Auto)]
		internal static extern IntPtr CreateBrushIndirect(ref LOGBRUSH brush);
		[DllImport("gdi32.dll", CharSet=CharSet.Auto)]
		internal static extern IntPtr CreateCompatibleDC(IntPtr hDC);
		[DllImport("gdi32.dll", CharSet=CharSet.Auto)]
		internal static extern IntPtr CreateRectRgnIndirect(ref RECT rect);
		[DllImport("gdi32.dll", CharSet=CharSet.Auto)]
		internal static extern bool DeleteDC(IntPtr hDC);
		[DllImport("gdi32.dll", CharSet=CharSet.Auto)]
		internal static extern IntPtr DeleteObject(IntPtr hObject);
		[DllImport("gdi32.dll", CharSet=CharSet.Auto)]
		internal static extern int GetClipBox(IntPtr hDC, ref RECT rectBox);
		[DllImport("gdi32.dll", CharSet=CharSet.Auto)]
		internal static extern bool PatBlt(IntPtr hDC, int x, int y, int width, int height, uint flags);
		[DllImport("gdi32.dll", CharSet=CharSet.Auto)]
		internal static extern int SelectClipRgn(IntPtr hDC, IntPtr hRgn);
		[DllImport("gdi32.dll", CharSet=CharSet.Auto)]
		internal static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
	}
    // ReSharper restore InconsistentNaming

	#endregion
}
