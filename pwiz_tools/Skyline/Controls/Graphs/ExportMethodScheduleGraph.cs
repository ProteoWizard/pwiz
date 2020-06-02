/*
 * Original author: Kaipo Tamura <kaipot .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.Graphs
{
    public partial class ExportMethodScheduleGraph : FormEx
    {
        public readonly Exception Exception;

        public ExportMethodScheduleGraph(SrmDocument document, string brukerTemplate)
        {
            InitializeComponent();

            Icon = Resources.Skyline;

            var masterPane = graphControl.MasterPane;
            masterPane.PaneList.Clear();
            masterPane.Border.IsVisible = false;

            var pane = new RTScheduleGraphPane(null, true);

            // Save existing settings
            var oldWindows = RTScheduleGraphPane.ScheduleWindows;
            RTScheduleGraphPane.ScheduleWindows = new double[] { };
            string oldBrukerTemplate = null;
            if (!string.IsNullOrEmpty(brukerTemplate) && !Equals(brukerTemplate, RTScheduleGraphPane.BrukerTemplateFile))
            {
                oldBrukerTemplate = RTScheduleGraphPane.BrukerTemplateFile;
                RTScheduleGraphPane.BrukerTemplateFile = brukerTemplate;
            }

            masterPane.PaneList.Add(pane);
            try
            {
                pane.UpdateGraph(false);
            }
            catch (Exception x)
            {
                Exception = x;
            }
            finally
            { 
                // Restore settings
                if (!Equals(oldBrukerTemplate, RTScheduleGraphPane.BrukerTemplateFile))
                {
                    RTScheduleGraphPane.BrukerTemplateFile = oldBrukerTemplate;
                }
                RTScheduleGraphPane.ScheduleWindows = oldWindows;
            }
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
        }
    }
}
