/*
 * Original author: Tahmina Jahan <tabaker .at. u.washington.edu>,
 *                  UWPR, Department of Genome Sciences, UW
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
using System.Windows.Forms;

namespace pwiz.Skyline.Controls
{
    public class WizardPages : TabControl
    {
        public WizardPages()
        {
            TabStop = false;    // Make sure the TabControl is not a tab stop
        }

        protected override void WndProc(ref Message m)
        {
            // Hide tabs by trapping the TCM_ADJUSTRECT message
            if (m.Msg == 0x1328 && !DesignMode) m.Result = (IntPtr)1;
            else base.WndProc(ref m);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // If the user presses Ctrl+Tab or Ctrl+Shift+Tab, 
            // just return true to swallow the keystroke, or
            // these key combinations will rotate through the tabs.
            if (keyData == (Keys.Control | Keys.Tab) ||
                keyData == (Keys.Control | Keys.Shift | Keys.Tab))
            {
                return true; // Keystroke is handled; don't pass to base.
            }

            // Otherwise, let the base class handle it
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }

    public class WizardPageControl : UserControl
    {
        /// <summary>
        /// Allow controls in a wizard to preempt close requests by the wizard's form. Returns true iff the close request should continue, false if it should be canceled.
        /// </summary>
        public virtual bool CanWizardClose()
        {
            return true;
        }
    }
}



