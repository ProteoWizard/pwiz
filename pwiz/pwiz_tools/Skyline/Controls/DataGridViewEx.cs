/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
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
using System.Windows.Forms;

namespace pwiz.Skyline.Controls
{
    public class DataGridViewEx : DataGridView
    {
        protected override bool ProcessDataGridViewKey(KeyEventArgs e)
        {
            if (DataGridViewKey != null)
            {
                DataGridViewKey.Invoke(this, e);
                if (e.Handled)
                {
                    return true;
                }
            }
            return base.ProcessDataGridViewKey(e);
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (DataGridViewKey != null)
            {
                var keyEventArgs = new KeyEventArgs(keyData);
                DataGridViewKey.Invoke(this, keyEventArgs);
                if (keyEventArgs.Handled)
                {
                    return true;
                }
            }
            return base.ProcessDialogKey(keyData);
        }

        /// <summary>
        /// DataGridViews somehow manage to get access to keystrokes such as cursor
        /// keys before the textbox with the focus gets them.  In order to allow
        /// other people to process these keys, the DataGridViewKey event is exposed
        /// in this class.
        /// </summary>
        public event EventHandler<KeyEventArgs> DataGridViewKey;

        /// <summary>
        /// If this control is a child of the SkylineWindow (not a popup), then returns the
        /// SkylineWindow.  Otherwise returns null.
        /// </summary>
        protected SkylineWindow FindParentSkylineWindow()
        {
            for (Control control = this; control != null; control = control.Parent)
            {
                var skylineWindow = control as SkylineWindow;
                if (skylineWindow != null)
                {
                    return skylineWindow;
                }
            }
            return null;
        }

        protected override void OnEnter(EventArgs e)
        {
            base.OnEnter(e);
            var skylineWindow = FindParentSkylineWindow();
            if (skylineWindow != null)
            {
                skylineWindow.ClipboardControlGotFocus(this);
            }
            // Fix for Issue 85(nicksh): For some reason ContainerControl.UpdateFocusedControl can
            // get into an infinite loop sometimes.
            // Setting ActiveControl to this prevents the hang from happening, and seems like a safe
            // thing to do here.
            var form = FindForm();
            if (form != null)
            {
                form.ActiveControl = this;
            }
        }

        protected override void OnLeave(EventArgs e)
        {
            base.OnLeave(e);
            var skylineWindow = FindParentSkylineWindow();
            if (skylineWindow != null)
            {
                skylineWindow.ClipboardControlLostFocus(this);
            }
        }

        /// <summary>
        /// Testing method: Sends Ctrl-V to this control.
        /// </summary>
        public void SendPaste()
        {
            OnKeyDown(new KeyEventArgs(Keys.V | Keys.Control));
        }
    }
}
