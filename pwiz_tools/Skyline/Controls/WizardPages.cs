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
using System.Drawing;
using System.Windows.Forms;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls
{
    public class WizardPages : TabControl
    {
        /// <summary>
        /// Adds a page body created at runtime to a wizard tab page, below the page's
        /// title header and inset by a border, anchored to grow with the page.
        /// </summary>
        /// <param name="pageControl">The page body</param>
        /// <param name="tabPage">The wizard page to add it to</param>
        /// <param name="border">Left, right and bottom inset, in 96-DPI pixels</param>
        /// <param name="header">Height reserved above the body for the page title, in 96-DPI pixels</param>
        /// <remarks>
        /// The page bodies are added after InitializeComponent, when the form has already
        /// auto-scaled, so these offsets must be scaled here or the body rides up under the
        /// title header on a high-DPI display.
        /// </remarks>
        public static void AddPageControl(UserControl pageControl, TabPage tabPage, int border, int header)
        {
            border = DpiUtil.Scale(tabPage, border);
            header = DpiUtil.Scale(tabPage, header);
            pageControl.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            pageControl.Location = new Point(border, header);
            pageControl.Width = tabPage.Width - border * 2;
            pageControl.Height = tabPage.Height - header - border;
            tabPage.Controls.Add(pageControl);
        }

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



