/*
 * Original author: Tobias Rohde <tobiasr .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model;

namespace pwiz.Skyline.Controls.Graphs
{
    public abstract class GraphSummaryToolbar : UserControl
    {
        protected GraphSummary _graphSummary;

        protected GraphSummaryToolbar(GraphSummary graphSummary)
        {
            _graphSummary = graphSummary;
            // ReSharper disable once VirtualMemberCallInConstructor
            Dock = DockStyle.Top;
        }

        public new abstract bool Visible { get; }
        public abstract void OnDocumentChanged(SrmDocument oldDocument, SrmDocument newDocument);
        public abstract void UpdateUI();
    }
}
