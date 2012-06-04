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
// Copyright 2011 Vanderbilt University
//
// Contributor(s):
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace IDPicker.Controls
{
    public partial class ForeBackColorControl : UserControl
    {
        public new Color? ForeColor
        {
            get { Color color = foregroundColorBox.BackColor; return color.ToArgb() == SystemColors.WindowText.ToArgb() ? default(Color?) : color; }
            set { foregroundColorBox.BackColor = value ?? SystemColors.WindowText; Refresh(); }
        }

        public new Color? BackColor
        {
            get { Color color = backgroundColorBox.BackColor; return color.ToArgb() == SystemColors.Window.ToArgb() ? default(Color?) : color; }
            set { backgroundColorBox.BackColor = value ?? SystemColors.Window; Refresh(); }
        }

        public ForeBackColorControl ()
        {
            InitializeComponent();
        }

        protected override void OnLoad (EventArgs e)
        {
            foregroundCheckBox.Checked = ForeColor.HasValue;
            backgroundCheckBox.Checked = BackColor.HasValue;
            foregroundCheckBox.CheckedChanged += foregroundCheckBox_CheckedChanged;
            backgroundCheckBox.CheckedChanged += backgroundCheckBox_CheckedChanged;
            base.OnLoad(e);
            Refresh();
        }

        public override void Refresh ()
        {
            previewBox.ForeColor = ForeColor ?? SystemColors.WindowText;
            previewBox.BackColor = BackColor ?? SystemColors.Window;
            base.Refresh();
        }

        private void foregroundCheckBox_CheckedChanged (object sender, EventArgs e)
        {
            if (!foregroundCheckBox.Checked)
            {
                ForeColor = null;
                return;
            }

            colorDialog.Color = ForeColor ?? SystemColors.WindowText;
            if (colorDialog.ShowDialog() == DialogResult.OK)
                ForeColor = colorDialog.Color;
            else
                foregroundCheckBox.Checked = ForeColor.HasValue;
        }

        private void backgroundCheckBox_CheckedChanged (object sender, EventArgs e)
        {
            if (!backgroundCheckBox.Checked)
            {
                BackColor = null;
                return;
            }

            colorDialog.Color = BackColor ?? SystemColors.Window;
            if (colorDialog.ShowDialog() == DialogResult.OK)
                BackColor = colorDialog.Color;
            else
                backgroundCheckBox.Checked = BackColor.HasValue;
        }

        private void previewBox_Enter (object sender, EventArgs e) { foregroundCheckBox.Select(); }
    }
}
