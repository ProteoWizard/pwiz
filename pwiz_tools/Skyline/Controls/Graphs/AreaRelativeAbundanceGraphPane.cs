/*
 * Original author: Henry Sanford <henrytsanford .at. u.washington.edu>,
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

using System.Collections.Generic;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Controls.Graphs
{
    internal class AreaRelativeAbundanceGraphPane : SummaryRelativeAbundanceGraphPane
    {
        public AreaRelativeAbundanceGraphPane(GraphSummary graphSummary, IList<MatchRgbHexColor> colorRows)
            : base(graphSummary, colorRows)
        {
        }
        protected override GraphData CreateGraphData(SkylineDataSchema dataSchema)
        {
            return new AreaGraphData(Document, dataSchema, AnyMolecules);
        }

        protected override void UpdateAxes()
        {
            YAxis.Title.Text = GraphsResources.AreaPeptideGraphPane_UpdateAxes_Peak_Area;

            base.UpdateAxes();

        }

        internal class AreaGraphData : GraphData
        {
            public AreaGraphData(SrmDocument document, SkylineDataSchema schema,
                bool anyMolecules)
                : base(document, schema, anyMolecules)
            {
            }

            public override double MaxValueSetting { get { return Settings.Default.PeakAreaMaxArea; } }
            public override double MaxCvSetting { get { return Settings.Default.PeakAreaMaxCv; } }
        }
    }

}
