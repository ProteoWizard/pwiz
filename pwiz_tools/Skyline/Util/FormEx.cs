/*
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
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using pwiz.Common.Controls;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Util
{
    public class FormEx : Form, IFormView
    {
        public static bool ShowFormNames { get; set; }

        private const int TIMEOUT_SECONDS = 10;
        private static readonly List<FormEx> _undisposedForms = new List<FormEx>();

        /// <summary>
        /// Sealed to keep ReSharper happy, because we set it in constructors
        /// </summary>
        public sealed override string Text
        {
            get { return base.Text; }
            set { base.Text = value; }
        }

        public new void Show()
        {
            // If you really need this, then you have to use ShowParentless (not yet created), or windows may leak handles.
            throw new InvalidOperationException("Not supported.");  // Not L10N
        }

        public new DialogResult ShowDialog()
        {
            // If you really need this, then you have to use ShowParentlessDialog, or windows may leak handles.
            throw new InvalidOperationException("Not supported.");  // Not L10N
        }

        public DialogResult ShowParentlessDialog()
        {
            // WINDOWS 10 UPDATE HACK: Because Windows 10 update version 1803 causes unparented non-ShowInTaskbar windows to leak GDI and User handles
            ShowInTaskbar = ShowInTaskbar || Program.FunctionalTest;

            return base.ShowDialog();
        }

        public DialogResult ShowWithTimeout(IWin32Window parent, string message)
        {
            Assume.IsNotNull(parent);   // Problems if the parent is null

            if (Program.FunctionalTest && Program.PauseSeconds == 0 && !Debugger.IsAttached)
            {
                bool timeout = false;
                var timeoutTimer = new Timer { Interval = TIMEOUT_SECONDS * 1000 };
                timeoutTimer.Tick += (sender, args) =>
                {
                    timeoutTimer.Stop();
                    if (!timeout)
                    {
                        timeout = true;
                        Close();
                    }
                };
                timeoutTimer.Start();

                var result = ShowDialog(parent);
                timeoutTimer.Stop();
                if (timeout)
                    throw new TimeoutException(
                        string.Format("{0} not closed for {1} seconds. Message = {2}", // Not L10N
                            GetType(),
                            TIMEOUT_SECONDS,
                            message));
                return result;
            }

            return ShowDialog(parent);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // For unit testing, move window offscreen.
            if (Program.SkylineOffscreen)
                SetOffscreen(this);

            if (Program.FunctionalTest)
            {
                // Track undisposed forms.
                lock (_undisposedForms)
                {
                    _undisposedForms.Add(this);
                }
            }

            if (ShowFormNames)
            {
                string textAppend = "  (" + GetType().Name + ")"; // Not L10N
                Text += textAppend;
            }
        }

        protected override bool ShowWithoutActivation
        {
            get { return Program.FunctionalTest || Program.SkylineOffscreen; }
        }

        protected override void Dispose(bool disposing)
        {
            if (Program.FunctionalTest && disposing)
            {
                lock (_undisposedForms)
                {
                    _undisposedForms.Remove(this);
                }
            }

            try
            {
                base.Dispose(disposing);
            }
            catch (InvalidOperationException x)
            {
                var message = TextUtil.LineSeparate(
                    string.Format("Exception thrown attempting to dispose {0}", GetType()), // Not L10N
                    x.Message,
                    "Exception caught at: " + new StackTrace()); // Not L10N
                throw new InvalidOperationException(message, x);
            }
        }

        public void CheckDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException("Form disposed"); // Not L10N
            }
        }

        public static void CheckAllFormsDisposed()
        {
            lock (_undisposedForms)
            {
                if (_undisposedForms.Count != 0)
                {
                    var formType = _undisposedForms[0].GetType().Name;
                    _undisposedForms.Clear();
                    throw new ApplicationException(formType + " was not disposed"); // Not L10N
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

        public static Form GetParentForm(Control control)
        {
            for (; ; )
            {
                var parent = control.Parent;
                if (parent == null)
                    return FormUtil.FindTopLevelOpenForm();
                var parentForm = parent as Form;
                if (parentForm != null)
                    return parentForm;
                control = parent;
            }
        }

        public void ForceOnScreen()
        {
            ForceOnScreen(this);
        }

        public static void ForceOnScreen(Form form)
        {
            var location = form.Location;
            location.X = Math.Max(GetScreen(form.Left, form.Top).WorkingArea.Left,
                Math.Min(location.X, GetScreen(form.Right, form.Top).WorkingArea.Right - form.Size.Width));
            location.Y = Math.Max(GetScreen(form.Left, form.Top).WorkingArea.Top,
                Math.Min(location.Y, GetScreen(form.Left, form.Bottom).WorkingArea.Bottom - form.Size.Height));
            form.Location = location;
        }

        private static Screen GetScreen(int x, int y)
        {
            return Screen.FromPoint(new Point(x, y));
        }

        public virtual void CancelDialog()
        {
            CancelButton.PerformClick();
        }

        public virtual string DetailedMessage { get { return null; } }
    }
}
