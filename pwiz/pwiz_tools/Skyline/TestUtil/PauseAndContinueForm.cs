/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using pwiz.Skyline.Util;

namespace pwiz.SkylineTestUtil
{
    public partial class PauseAndContinueForm : FormEx
    {
        public PauseAndContinueForm(string description = null)
        {
            InitializeComponent();
            if (description == null)
                Height -= 15;
            else
            {
                // Adjust dialog width to accommodate description.
                lblDescription.Text = description;
                if (lblDescription.Width > btnContinue.Width)
                    Width = lblDescription.Width + 30;
            }
        }

        private void btnContinue_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
