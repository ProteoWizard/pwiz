//
// Original author: Matt Chambers <matt.chambers42 .@. gmail.com>
//
// Copyright 2023 Matt Chambers
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

using System;
using System.Drawing;
using System.Windows.Forms;

namespace MSConvertGUI
{
    public class BaseForm : Form
    {
        protected override void OnLoad(EventArgs e)
        {
            Font = new Font("Microsoft Sans Serif", 8.25f, GraphicsUnit.Point);

            foreach (Control c in Controls)
            {
                c.Font = Font;

                if (!c.HasChildren)
                    continue;

                foreach (Control child in c.Controls)
                    child.Font = Font;
            }

            base.OnLoad(e);
        }
    }
}
