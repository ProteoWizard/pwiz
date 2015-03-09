/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SkylineTester
{
    // From http://bartdesmet.net/blogs/bart/archive/2006/10/05/4495.aspx

    public partial class WindowThumbnail : UserControl
    {
        public static Form MainWindow;

        public WindowThumbnail()
        {
            InitializeComponent();
        }

        private int _processId;

        public int ProcessId
        {
            get { return _processId; }
            set
            {
                _processId = value;
                if (_processId == 0)
                    UnregisterThumb();
                else
                    RegisterThumb();
            }
        }

        private void UnregisterThumb()
        {
            if (_thumb != IntPtr.Zero)
            {
                DwmUnregisterThumbnail(_thumb);
                _thumb = IntPtr.Zero;
            }
        }
        
        private void RegisterThumb()
        {
            var window = FindWindow(_processId);
            if (window == IntPtr.Zero)
            {
                UnregisterThumb();
                return;
            }

            IntPtr newThumb;
            DwmRegisterThumbnail(MainWindow.Handle, window, out newThumb);
            if (newThumb == IntPtr.Zero)
            {
                UnregisterThumb();
                return;
            }
            if (_thumb != newThumb)
            {
                UnregisterThumb();
                _thumb = newThumb;
            }

            Point locationOnForm = MainWindow.PointToClient(Parent.PointToScreen(Location));

            PSIZE size;
            DwmQueryThumbnailSourceSize(_thumb, out size);

            DWM_THUMBNAIL_PROPERTIES props = new DWM_THUMBNAIL_PROPERTIES
            {
                dwFlags = DWM_TNP_VISIBLE | DWM_TNP_RECTDESTINATION | DWM_TNP_OPACITY,
                fVisible = true,
                opacity = 255,
                rcDestination = new Rect(
                    locationOnForm.X,
                    locationOnForm.Y,
                    locationOnForm.X + Width,
                    locationOnForm.Y + Height)
            };

            if (size.x < Width)
                props.rcDestination.Right = props.rcDestination.Left + size.x;
            if (size.y < Height)
                props.rcDestination.Bottom = props.rcDestination.Top + size.y;

            DwmUpdateThumbnailProperties(_thumb, ref props);
        }

        private IntPtr FindWindow(int processId)
        {
            IntPtr window = IntPtr.Zero;

            EnumWindows(
                delegate(IntPtr wnd, IntPtr param)
                {
                    uint id;
                    GetWindowThreadProcessId(wnd, out id);

                    if ((int)id == processId &&
                        (GetWindowLongA(wnd, GWL_STYLE) & TARGETWINDOW) == TARGETWINDOW)
                    {
                        window = wnd;
                        return false;
                    }
                    return true;
                },
                (IntPtr)processId);

            return window;
        }

        private const int GWL_STYLE = -16;

        private const ulong WS_VISIBLE = 0x10000000L;
        private const ulong WS_BORDER = 0x00800000L;
        private const ulong TARGETWINDOW = WS_BORDER | WS_VISIBLE;

        private IntPtr _thumb;

        private const int DWM_TNP_VISIBLE = 0x8;
        private const int DWM_TNP_OPACITY = 0x4;
        private const int DWM_TNP_RECTDESTINATION = 0x1;

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern ulong GetWindowLongA(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("dwmapi.dll")]
        static extern int DwmRegisterThumbnail(IntPtr dest, IntPtr src, out IntPtr thumb);

        [DllImport("dwmapi.dll")]
        static extern int DwmUnregisterThumbnail(IntPtr thumb);

        [DllImport("dwmapi.dll")]
        static extern int DwmQueryThumbnailSourceSize(IntPtr thumb, out PSIZE size);

        [DllImport("dwmapi.dll")]
        static extern int DwmUpdateThumbnailProperties(IntPtr hThumb, ref DWM_THUMBNAIL_PROPERTIES props);

        [StructLayout(LayoutKind.Sequential)]
        internal struct DWM_THUMBNAIL_PROPERTIES
        {
            public int dwFlags;
            public Rect rcDestination;
            public Rect rcSource;
            public byte opacity;
            public bool fVisible;
            public readonly bool fSourceClientAreaOnly;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Rect
        {
            internal Rect(int left, int top, int right, int bottom)
            {
                Left = left;
                Top = top;
                Right = right;
                Bottom = bottom;
            }

            public readonly int Left;
            public readonly int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct PSIZE
        {
            public readonly int x;
            public readonly int y;
        }


    }
}
