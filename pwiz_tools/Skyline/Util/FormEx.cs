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
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;

namespace pwiz.Skyline.Util
{
    public class FormEx : Form
	{
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            SetLocation(this);
        }

        public void CheckDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException("Form disposed");
            }
        }

        public static void SetLocation(Form form)
        {
            // Avoid breaking forms in Visual Studio designer
            if (Program.MainWindow != null)
            {
                // move offscreen if main window is offscreen
                var mainWindowBounds = Program.MainWindow.DesktopBounds;
                if (!IntersectsAnyScreen(mainWindowBounds))
                {
                    form.StartPosition = FormStartPosition.Manual;
                    form.Location = mainWindowBounds.Location;
                }
            }
        }

        private static bool IntersectsAnyScreen(Rectangle rectangle)
        {
            return Screen.AllScreens.Any(screen => screen.WorkingArea.IntersectsWith(rectangle));
        }

        public static Form GetParentForm(Control control)
        {
            for (; ; )
            {
                var parent = control.Parent;
                if (parent == null)
                    return null;
                var parentForm = parent as Form;
                if (parentForm != null)
                    return parentForm;
                control = parent;
            }
        }

        public virtual void CancelDialog()
        {
            CancelButton.PerformClick();
        }
    }

    public class DockableFormEx : DockableForm
    {
        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            if (ParentForm != null)
            {
                FormEx.SetLocation(ParentForm);
            }
        }

        public void CheckDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException("Form disposed");
            }
        }
    }
}
