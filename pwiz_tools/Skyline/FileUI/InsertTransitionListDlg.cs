/*
 * Original author: Henry Estberg <henrye1 .at. outlook.com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using System.Runtime.InteropServices;
using System.Windows.Forms;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.FileUI
{
    public partial class InsertTransitionListDlg : FormEx
    {

        public InsertTransitionListDlg()
        {
            InitializeComponent();

            Icon = Resources.Skyline;

            // Move the prompt text from the label to the text box, hopefully a more obvious cue
            label1.Visible = false;
            textBox1.TextAlign = HorizontalAlignment.Center;
            textBox1.Text = Environment.NewLine + Environment.NewLine + Environment.NewLine + label1.Text;
            textBox1.SelectionStart = textBox1.Text.Length;
            textBox1.SelectionLength = 0;

            // Bigger prompt
            textBox1.Font = new Font(textBox1.Font.Name, textBox1.Font.Size * 2, textBox1.Font.Style);

            // Don't show the blinking cursor
            textBox1.GotFocus += textBox1_HideCaret;  
        }

        public string TransitionListText
        {
            get => textBox1.Text;
            set { textBox1.Text = value; DialogResult = DialogResult.OK; }
        } 

        private void textBox1_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.Handled = true; // We're not interested in any keyboard input other than ctrl-V, and we handle that in keydown event
        }

        private void textBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && (e.KeyCode == Keys.V)) // Ignore any keyboard activity other than paste
            {
                textBox1.Text = string.Empty;
                textBox1.TextAlign = HorizontalAlignment.Left; // So the pasted text, which appears briefly, doesn't look weird
                textBox1.Font = new Font(textBox1.Font.Name, textBox1.Font.Size / 2, textBox1.Font.Style); // Back to standard font
                textBox1.WordWrap = false; // Makes brief appearance of pasted text look a little tidier
                textBox1.Paste(); // Copy the clipboard contents to the textbox
                DialogResult = DialogResult.OK;
            }
        }

        [DllImport("user32.dll")]
        static extern bool HideCaret(IntPtr hWnd);
        private void textBox1_HideCaret(object sender, EventArgs e)
        {
            HideCaret(textBox1.Handle);// Don't show the blinking cursor
        }
    }
}
