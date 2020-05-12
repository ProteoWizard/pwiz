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
    public class TutorialActionBoxControl : ActionBoxControl
    {
        public TutorialActionBoxControl() : base(240, 160)
        {
        }
    }

    public partial class ActionBoxControl : UserControl
    {
        private static readonly Color LIGHT_HOVER_COLOR = Color.FromArgb(217, 228, 243); // Hover color for action box items
        public string Caption { get { return labelCaption.Text; } set { labelCaption.Text = value; } }
        public string Description { get { return labelDescription.Text; } set { labelDescription.Text = value; } }
        public Image Icon { get { return iconPictureBox.Image; } set { iconPictureBox.Image = value; } }
        public Action EventAction { get; set; }
        public bool IsProteomicOnly { get; set; } // If true, don't show in small molecule mode

        public ActionBoxControl(int? imageWidth = null, int? imageHeight = null)
        {
            InitializeComponent();

            if (imageWidth.HasValue)
            {
                int deltaWidth = imageWidth.Value - iconPictureBox.Width;
                Width += deltaWidth;
                iconPictureBox.Width = labelDescription.Width = imageWidth.Value;
            }
            if (imageHeight.HasValue)
            {
                int deltaHeight = imageHeight.Value - iconPictureBox.Height;
                Height += deltaHeight;
                labelCaption.Top += deltaHeight;
                iconPictureBox.Height = labelDescription.Height = imageHeight.Value;
            }
        }

        private void labelCaption_MouseEnter(object sender, EventArgs e)
        {
            ControlMouseEnter(sender, e);
            if (!string.IsNullOrEmpty(Description))
            {
                iconPictureBox.Visible = false;
                labelDescription.Visible = true;
            }
        }

        private void labelCaption_MouseLeave(object sender, EventArgs e)
        {
            OnMouseLeave(e);
            if (!string.IsNullOrEmpty(Description))
            {
                iconPictureBox.Visible = true;
                labelDescription.Visible = false;
            }
        }

        private void ControlMouseEnter(object sender, EventArgs e)
        {
            Cursor = Cursors.Hand;
            if (!string.IsNullOrEmpty(Description))
            {               
                BackColor = LIGHT_HOVER_COLOR;
            }
        }

        private void ControlMouseLeave(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(Description))
            {
                BackColor = Color.Transparent;
            }
        }

        private void ControlClick(object sender, EventArgs e)
        {
            Cursor.Current = Cursors.WaitCursor;
            if (EventAction != null)
            {
                EventAction();
            }
        }
    }
}
