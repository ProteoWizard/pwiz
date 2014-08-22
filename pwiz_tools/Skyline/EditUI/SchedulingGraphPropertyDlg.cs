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
using System.Collections.Generic;
using System.Windows.Forms;
using System.Linq;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.EditUI
{
    public partial class SchedulingGraphPropertyDlg : FormEx
    {
        public SchedulingGraphPropertyDlg()
        {
            InitializeComponent();

            TimeWindows = RTScheduleGraphPane.ScheduleWindows;
            PrimaryTransitionCount = RTScheduleGraphPane.PrimaryTransitionCount;
        }

        public double[] TimeWindows
        {
            get { return textTimeWindows.Text.Split(',').Select(t => double.Parse(t.Trim())).ToArray(); } // Not L10N
            set { textTimeWindows.Text = WindowsToString(value); }
        }

        public int PrimaryTransitionCount
        {
            get
            {
                return string.IsNullOrEmpty(textPrimaryTransitionCount.Text)
                           ? 0
                           : int.Parse(textPrimaryTransitionCount.Text);
            }
            set
            {
                textPrimaryTransitionCount.Text = value == 0
                    ? string.Empty
                    : value.ToString(LocalizationHelper.CurrentCulture);
            }
        }

        private static string WindowsToString(IEnumerable<double> windows)
        {
            return string.Join(", ", windows.Select(v => v.ToString(LocalizationHelper.CurrentCulture)).ToArray()); // Not L10N
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            var helper = new MessageBoxHelper(this);
            double[] timeWindows;
            if (!helper.ValidateDecimalListTextBox(textTimeWindows,
                                                   PeptidePrediction.MIN_MEASURED_RT_WINDOW,
                                                   PeptidePrediction.MAX_MEASURED_RT_WINDOW,
                                                   out timeWindows)) 
                return;

            int primaryTransitionCount = 0;
            if (!string.IsNullOrEmpty(textPrimaryTransitionCount.Text))
            {
                if (!helper.ValidateNumberTextBox(textPrimaryTransitionCount,
                                                  AbstractMassListExporter.PRIMARY_COUNT_MIN,
                                                  AbstractMassListExporter.PRIMARY_COUNT_MAX,
                                                  out primaryTransitionCount))
                    return;
            }

            Array.Sort(timeWindows);

            RTScheduleGraphPane.ScheduleWindows = timeWindows;
            RTScheduleGraphPane.PrimaryTransitionCount = primaryTransitionCount;

            DialogResult = DialogResult.OK;
        }
    }
}
