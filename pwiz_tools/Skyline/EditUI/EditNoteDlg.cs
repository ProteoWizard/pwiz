/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.ComponentModel;
using System.Windows.Forms;

namespace pwiz.Skyline.EditUI
{
    public partial class EditNoteDlg : Form
    {
        private string _note;
        private bool _clickedOk;

        public EditNoteDlg()
        {
            InitializeComponent();
        }

        public string Note
        {
            get { return _note; }
            set
            {
                _note = value;

                textNote.Text = _note ?? "";
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_clickedOk)
            {
                _note = textNote.Text;
                if (_note == "")
                    _note = null;
            }

            base.OnClosing(e);
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            _clickedOk = true;
        }        
    }
}
