﻿/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.GUI;

namespace pwiz.Common.SystemUtil
{
    public class CommonFormEx : Form, IFormView
    {
        public static bool TestMode
        {
            get { return CommonApplicationSettings.FunctionalTest; }
        }
        public static bool PauseMode
        {
            get { return CommonApplicationSettings.PauseSeconds != 0; }
        }
        public static bool Offscreen
        {
            get { return CommonApplicationSettings.Offscreen; }
        }
        public static bool ShowFormNames
        {
            get { return CommonApplicationSettings.ShowFormNames; }
        }

        private static readonly List<CommonFormEx> _undisposedForms = new List<CommonFormEx>();

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // For unit testing, move window offscreen.
            if (Offscreen)
                SetOffscreen(this);

            // Track undisposed forms.
            if (TestMode)
            {
                lock (_undisposedForms)
                {
                    _undisposedForms.Add(this);
                }
            }

// ReSharper disable LocalizableElement
            if (ShowFormNames)
                Text += "  (" + GetType().Name + ")"; 
// ReSharper restore LocalizableElement
        }

        protected override bool ShowWithoutActivation
        {
            // Avoid activating forms during test mode or when off screen, but not when
            // pausing to show Skyline as it normally functions.
            get { return (TestMode || Offscreen) && !PauseMode; }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (TestMode && disposing)
            {
                lock (_undisposedForms)
                {
                    _undisposedForms.Remove(this);
                }
            }
        }

        public static void CheckAllFormsDisposed()
        {
            lock (_undisposedForms)
            {
                if (_undisposedForms.Count != 0)
                {
                    var formType = _undisposedForms[0].GetType();
                    string exceptionMessage = formType + @" was not disposed";
                    string message = _undisposedForms.OfType<CommonAlertDlg>().FirstOrDefault()?.Message;
                    if (message != null)
                        exceptionMessage = CommonTextUtil.LineSeparate(exceptionMessage, @"message: " + message);
                    _undisposedForms.Clear();
                    throw new ApplicationException(exceptionMessage);
                }
            }
        }

        public static void SetOffscreen(Form form)
        {
            form.StartPosition = FormStartPosition.Manual;
            form.Location = GetOffscreenPoint();
        }

        public static Point GetOffscreenPoint()
        {
            var offscreenPoint = new Point(0, 0);
            foreach (var screen in Screen.AllScreens)
            {
                offscreenPoint.X = Math.Min(offscreenPoint.X, screen.Bounds.Right);
                offscreenPoint.Y = Math.Min(offscreenPoint.Y, screen.Bounds.Bottom);
            }
            return offscreenPoint - Screen.PrimaryScreen.Bounds.Size;    // position one screen away to top left
        }
        public void CheckDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(@"Form disposed");
            }
        }

        public virtual string DetailedMessage { get { return null; } }

        public DialogResult ShowParentlessDialog()
        {
            // Parentless dialogs should always be shown in the taskbar because:
            // 1. If not shown in the taskbar it will be a leak in Windows 10
            // 2. It is difficult to switch to Skyline if there is a modal dialog which is not in the taskbar
            ShowInTaskbar = true;

            return ShowDialog();
        }
    }
}
