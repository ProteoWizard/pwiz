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
using System.ComponentModel;
using System.Globalization;
using System.Windows.Forms;
using System.Linq;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Graphs;

namespace pwiz.Skyline.EditUI
{
    public partial class SchedulingGraphPropertyDlg : Form
    {
        public SchedulingGraphPropertyDlg()
        {
            InitializeComponent();

            textTimeWindows.Text = string.Join(", ",
                RTScheduleGraphPane.ScheduleWindows.Select(v => v.ToString(CultureInfo.CurrentCulture)).ToArray());
        }

        private void btnOk_Click(object sender, System.EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            var helper = new MessageBoxHelper(this);
            var e = new CancelEventArgs();

            double[] timeWindows;
            if (!helper.ValidateDecimalListTextBox(e, textTimeWindows, 1, 200, out timeWindows))
                return;

            Array.Sort(timeWindows);

            RTScheduleGraphPane.ScheduleWindows = timeWindows;
            DialogResult = DialogResult.OK;
        }
    }
}
