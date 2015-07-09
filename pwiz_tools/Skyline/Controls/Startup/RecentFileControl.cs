/*
 * Original author: Yuval Boss <yuval .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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

namespace pwiz.Skyline.Controls.Startup
{
    public partial class RecentFileControl : UserControl
    {
        public RecentFileControl()
        {
            InitializeComponent();
        }

        public string FileName { get { return labelFileName.Text; } set { labelFileName.Text = value; } }
        public string FilePath { get { return labelFilePath.Text; } set { labelFilePath.Text = value; } }
        public Action EventAction { get; set; }

        private void ControlMouseEnter(object sender, EventArgs e)
        {
            Cursor = Cursors.Hand;
            BackColor = StartPage._darkestHoverColor;
            Parent.Focus();
        }

        private void ControlMouseLeave(object sender, EventArgs e)
        {
            BackColor = Color.Transparent;
        }

        private void ControlClick(object sender, EventArgs e)
        {
            Cursor.Current = Cursors.WaitCursor;
            if (null != EventAction)
            {
                EventAction();
            }
        }

        public StartPage StartPage
        {
            get
            {
                Control control = this;
                while (control != null && !(control is StartPage))
                {
                    control = control.Parent;
                }
                return control as StartPage;
            }
        }
    }
}
