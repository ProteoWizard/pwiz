//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2012 Vanderbilt University
//
// Contributor(s): Brendan MacLean <brendanx .at. u.washington.edu>,
//

using System;
using System.Windows.Forms;

namespace IDPicker.Controls
{
    /// <summary>
    /// TextBox subclass that can be customized to not play a beep sound for special keys like enter.
    /// </summary>
    public class SilentTextBox : TextBox
    {
        public event EventHandler EnterPressed;

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && EnterPressed != null)
                e.Handled = true;
            else
                base.OnKeyDown(e);
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            if (e.KeyChar == '\r' && EnterPressed != null)
            {
                EnterPressed(this, e);
                e.Handled = true;
            }
            else
                base.OnKeyPress(e);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && EnterPressed != null)
                e.Handled = true;
            else
                base.OnKeyUp(e);
        }
    }

    /// <summary>
    /// ToolStripTextBox subclass that can be customized to not play a beep sound for special keys like Enter.
    /// </summary>
    public class SilentToolStripTextBox : ToolStripTextBox
    {
        public event EventHandler EnterPressed;

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && EnterPressed != null)
                e.Handled = true;
            else
                base.OnKeyDown(e);
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            if (e.KeyChar == '\r' && EnterPressed != null)
            {
                EnterPressed(this, e);
                e.Handled = true;
            }
            else
                base.OnKeyPress(e);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && EnterPressed != null)
                e.Handled = true;
            else
                base.OnKeyUp(e);
        }
    }
}