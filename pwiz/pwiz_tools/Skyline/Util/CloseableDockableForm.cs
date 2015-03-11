/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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
using System.Windows.Forms;

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// Dockable Form with the following characteristics:
    /// 1. HideOnClose is true.  This works around a problem where focus does not correctly go to the Skyline window when
    /// a floating dockable form is closed.
    /// 2. When this form is hidden, it closes itself
    /// 3. When this form is closed, it disposes itself
    /// 4. If the owner of this form is closed, this form closes itself
    /// </summary>
    public class CloseableDockableForm : DockableFormEx
    {
        private Form _owner;
        public CloseableDockableForm()
        {
            HideOnClose = true;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _owner = Owner;
            if (null != _owner)
            {
                _owner.FormClosed += OwnerFormClosed;
            }
        }

        private void OwnerFormClosed(object sender, EventArgs args)
        {
            Close();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (!Visible && IsHandleCreated)
            {
                BeginInvoke(new Action(Close));
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            if (null != _owner)
            {
                _owner.FormClosed -= OwnerFormClosed;
                _owner = null;
            }
            Dispose();
        }
    }
}
