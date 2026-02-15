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
using System.Windows.Forms;
using pwiz.Common.SystemUtil.PInvoke;
using TestRunnerLib.PInvoke;

namespace SkylineTester
{
    // From http://bartdesmet.net/blogs/bart/archive/2006/10/05/4495.aspx

    public partial class WindowThumbnail : UserControl
    {
        public static Form MainWindow;

        private IntPtr _thumb;

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
                DwmapiTest.DwmUnregisterThumbnail(_thumb);
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
            DwmapiTest.DwmRegisterThumbnail(MainWindow.Handle, window, out newThumb);
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

            PInvokeCommon.SIZE size;
            DwmapiTest.DwmQueryThumbnailSourceSize(_thumb, out size);

            var props = new DwmapiTest.THUMBNAIL_PROPERTIES
            {
                dwFlags = (int)(DwmapiTest.TNPFlags.VISIBLE | DwmapiTest.TNPFlags.RECTDESTINATION | DwmapiTest.TNPFlags.OPACITY),
                fVisible = true,
                opacity = 255,
                rcDestination = new User32.RECT(
                    locationOnForm.X,
                    locationOnForm.Y,
                    locationOnForm.X + Width,
                    locationOnForm.Y + Height)
            };

            if (size.cx < Width)
                props.rcDestination.right = props.rcDestination.left + size.cx;
            if (size.cy < Height)
                props.rcDestination.bottom = props.rcDestination.top + size.cy;

            DwmapiTest.DwmUpdateThumbnailProperties(_thumb, ref props);
        }

        private IntPtr FindWindow(int processId)
        {
            const ulong targetWindow = User32Test.WS_BORDER | User32Test.WS_VISIBLE;

            IntPtr window = IntPtr.Zero;

            User32Test.EnumWindows(
                delegate(IntPtr wnd, IntPtr param)
                {
                    uint id;
                    User32.GetWindowThreadProcessId(wnd, out id);

                    if ((int)id == processId &&
                        (User32Test.GetWindowLongA(wnd, User32Test.GWL_STYLE) & targetWindow) == targetWindow)
                    {
                        window = wnd;
                        return false;
                    }
                    return true;
                },
                (IntPtr)processId);

            return window;
        }
    }
}
