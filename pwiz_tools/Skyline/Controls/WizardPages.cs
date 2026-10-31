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
using pwiz.Common.SystemUtil.PInvoke;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls
{
    public class WizardPages : TabControl
    {
        public WizardPages()
        {
            TabStop = false;    // Make sure the TabControl is not a tab stop
        }

        /// <summary>
        /// Moves the focus to the first control on the new page only when the focus was already inside
        /// this control. TabControl.SelectedIndex treats every change made in code as a user switching
        /// tabs and always moves the focus onto the new page, so choosing a page from a control outside
        /// it, such as a radio button or the Next button, would lose that control's focus.
        /// </summary>
        public new int SelectedIndex
        {
            get { return base.SelectedIndex; }
            set
            {
                if (!IsHandleCreated || ContainsFocus || value == base.SelectedIndex)
                {
                    base.SelectedIndex = value;
                    return;
                }
                // Changing the selection with the native message and raising the change ourselves
                // leaves TabControl's private UI-selection flag clear, so it shows the page without
                // moving the focus
                User32.SendMessage(Handle, User32.WinMessageType.TCM_SETCURSEL, value, IntPtr.Zero);
                OnSelectedIndexChanged(EventArgs.Empty);
            }
        }

        public new void SelectTab(int index)
        {
            SelectedIndex = index;
        }

        /// <summary>
        /// Places a page's control below its header text, filling the page to its borders and
        /// stretching with it: the way a wizard page hosts the control that is its content.
        /// </summary>
        /// <param name="pageControl">The page body</param>
        /// <param name="tabPage">The wizard page to add it to</param>
        /// <param name="border">Left, right and bottom inset, in 96-DPI pixels</param>
        /// <param name="header">Height reserved above the body for the page title, in 96-DPI pixels</param>
        /// <param name="footer">Height left free below the body, in 96-DPI pixels</param>
        /// <remarks>
        /// The page bodies are added after InitializeComponent, when the form has already
        /// auto-scaled, so these offsets must be scaled here or the body rides up under the
        /// title header on a high-DPI display.
        /// </remarks>
        public static void AddPageControl(UserControl pageControl, TabPage tabPage, int border, int header, int footer = 0)
        {
            border = DpiUtil.Scale(tabPage, border);
            header = DpiUtil.Scale(tabPage, header);
            footer = DpiUtil.Scale(tabPage, footer);
            pageControl.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            pageControl.Location = new Point(border, header);
            pageControl.Width = tabPage.Width - border * 2;
            pageControl.Height = tabPage.Height - header - border - footer;
            tabPage.Controls.Add(pageControl);
        }

        protected override void WndProc(ref Message m)
        {
            // Hide tabs by trapping the TCM_ADJUSTRECT message
            if (m.Msg == 0x1328 && !DesignMode) m.Result = 1;
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



