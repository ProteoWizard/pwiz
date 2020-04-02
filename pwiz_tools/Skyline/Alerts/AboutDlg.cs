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

using System.Windows.Forms;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.CLI.Bruker.PrmScheduling;

namespace pwiz.Skyline.Alerts
{
    public partial class AboutDlg : FormEx
    {
        public AboutDlg()
        {
            InitializeComponent();

            labelSoftwareVersion.Text = Install.ProgramNameAndVersion;

            // Designer has problems getting images from resources
            pictureSkylineIcon.Image = Resources.SkylineImg;
            pictureProteoWizardIcon.Image = Resources.ProteoWizard;

            InputTarget makeTarget(int id, double rtStart, double rtEnd, double isoMz, double imStart, double imEnd)
            {
                return new InputTarget()
                {
                    external_id = id.ToString(),
                    time_in_seconds_begin = rtStart,
                    time_in_seconds_end = rtEnd,
                    isolation_mz = isoMz,
                    one_over_k0_lower_limit = imStart,
                    one_over_k0_upper_limit = imEnd,
                    charge = 2, collision_energy = -1,
                    isolation_width = 3,
                    one_over_k0 = (imStart+imEnd)/2,
                    time_in_seconds = (rtStart+rtEnd)/2,
                    monoisotopic_mz = isoMz
                };
            };

            using (var s = new Scheduler(@"C:\pwiz.git\pwiz\pwiz\utility\bindings\CLI\timstof_prm_scheduler"))
            {
                var targetList = new TargetList
                {
                    makeTarget(1, -53.2000, 146.8000, 784.8662, 1.0216, 1.0497),
                    makeTarget(2, -15.4000, 184.6000, 523.7778, 0.8433, 0.8716),
                    makeTarget(3, 5.0000, 205.0000, 576.2796, 0.8592, 0.8874),
                    makeTarget(4, 10.4000, 210.4000, 569.2863, 0.9010, 0.9292)
                };
                s.SetAdditionalMeasurementParameters(new AdditionalMeasurementParameters {ms1_repetition_time = 10});
                s.SchedulePrmTargets(targetList, null);

                var timeSegmentList = new TimeSegmentList();
                var schedulingEntryList = new SchedulingEntryList();
                s.GetScheduling(timeSegmentList, schedulingEntryList);
                foreach (var time in timeSegmentList)
                    textBox1.AppendText(string.Format("{0}-{1}\r\n", time.time_in_seconds_begin, time.time_in_seconds_end));
            }
        }

        private void linkProteome_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            WebHelpers.OpenLink(this, @"http://skyline.ms");
        }

        private void linkProteoWizard_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            WebHelpers.OpenLink(this, @"https://github.com/ProteoWizard");
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            WebHelpers.OpenLink(this, @"http://proteome.gs.washington.edu/software/Skyline/funding.html");
        }
    }
}
