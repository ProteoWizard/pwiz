/*
 * Original author: Brian Pratt <bspratt .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using pwiz.Common.SystemUtil.PInvoke;

namespace pwiz.Skyline.Controls.Graphs
{
    /// <summary>
    /// ToolStrip subclass that replies MA_ACTIVATE (not MA_ACTIVATEANDEAT) to
    /// WM_MOUSEACTIVATE so the first click on a toolbar button in an inactive
    /// window activates the window *and* delivers the click to the button,
    /// instead of eating the click.
    ///
    /// Without this the user experiences an apparently dead button on the first
    /// click, which is confusing.
    /// </summary>
    public class ClickThroughToolStrip : ToolStrip
    {
        private const int MA_ACTIVATE = 1;
        private const int MA_ACTIVATEANDEAT = 2;

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == (int)User32.WinMessageType.WM_MOUSEACTIVATE &&
                m.Result == (IntPtr)MA_ACTIVATEANDEAT)
            {
                m.Result = (IntPtr)MA_ACTIVATE;
            }
        }
    }
}
