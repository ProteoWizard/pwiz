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
using pwiz.Common.SystemUtil.Caching;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Controls.Graphs
{
    internal class AreaRelativeAbundanceGraphPane : SummaryRelativeAbundanceGraphPane
    {
        public AreaRelativeAbundanceGraphPane(GraphSummary graphSummary)
            : base(graphSummary)
        {
        }

        protected override Producer<GraphDataParameters, GraphData> GraphDataProducer => _graphDataProducer;

        protected override void UpdateAxes()
        {
            YAxis.Title.Text = GraphsResources.AreaPeptideGraphPane_UpdateAxes_Peak_Area;

            base.UpdateAxes();

        }

        internal class AreaGraphData : GraphData
        {
            public AreaGraphData(GraphDataParameters parameters, ProductionMonitor productionMonitor)
                : base(parameters, productionMonitor)
            {
            }

            public override double MaxValueSetting { get { return Settings.Default.PeakAreaMaxArea; } }
            public override double MaxCvSetting { get { return Settings.Default.PeakAreaMaxCv; } }
        }

        private static readonly GraphDataProducerImpl _graphDataProducer = new GraphDataProducerImpl();
        private class GraphDataProducerImpl : Producer<GraphDataParameters, GraphData>
        {
            public override GraphData ProduceResult(ProductionMonitor productionMonitor, GraphDataParameters parameter, IDictionary<WorkOrder, object> inputs)
            {
                return new AreaGraphData(parameter, productionMonitor);
            }
        }
    }

}
