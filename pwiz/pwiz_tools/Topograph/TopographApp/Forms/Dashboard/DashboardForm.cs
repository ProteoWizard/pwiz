/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;

namespace pwiz.Topograph.ui.Forms.Dashboard
{
    public partial class DashboardForm : DockableForm
    {
        public DashboardForm(TopographForm topographForm)
        {
            InitializeComponent();
            TopographForm = topographForm;
            Icon = Properties.Resources.TopographIcon;
            var frames = new List<Control>();
            foreach (var step in new DashboardStep[]
                                     {
                                        new CreateWorkspaceStep(),
                                        new AddSearchResultsStep(),
                                        new SettingsStep(),
                                        new RawFilesStep(),
                                        new AnalyzePeptidesStep(),
                                        new WaitForResultsStep(),
                                        new ViewResultsStep(), 
                                     })
            {
                var frame = new DashboardStepFrame
                                {
                                    DashboardStep = step,
                                    StepOrdinal = frames.Count,
                                    Dock = DockStyle.Top,
                                };
                frames.Add(frame);
            }
            frames.Reverse();
            panelSteps.Controls.AddRange(frames.ToArray());
        }

        public TopographForm TopographForm { get; private set; }
    }
}
