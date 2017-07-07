/*
 * Original author: Tobias Rohde <tobiasr .at. uw.edu>,
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NHibernate.Util;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
	[TestClass]
	public class SynchronizeSummaryZoomingTest : AbstractFunctionalTest
	{
		[TestMethod]
		public void TestSynchronizeSummaryZooming()
		{
			TestFilesZip = "TestFunctional/SynchronizeSummaryZoomingTest.zip";
			RunFunctionalTest();
		}

		protected override void DoTest()
		{
			RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath(@"Ms1FilterTutorial.sky")));
			WaitForDocumentLoaded();

			Settings.Default.SynchronizeSummaryZooming = false;

			GraphSummary[] summaries = { SkylineWindow.GraphPeakArea, SkylineWindow.GraphRetentionTime, SkylineWindow.GraphMassError };
			
			RunUI(() =>
			{
				// Set all graph types to replicate
				Enumerable.Range(0, 3).ForEach(i => SetGraphType(i, GraphTypeRT.replicate.ToString()));

				SkylineWindow.ShowGraphRetentionTime(true);
				SkylineWindow.ShowGraphPeakArea(true);
				SkylineWindow.ShowGraphMassError(true);
			});

			WaitForGraphs();
			Settings.Default.SynchronizeSummaryZooming = true;

			// Test if graphs sync correctly with ShowLibraryPeakArea disabled/enabled
			Settings.Default.ShowLibraryPeakArea = false;
			TestGraphSummary(summaries[0], new [] { summaries[1], summaries[2] }, true);

			Settings.Default.ShowLibraryPeakArea = true;
			TestGraphSummary(summaries[0], new[] { summaries[1], summaries[2] }, true);
			TestGraphSummary(summaries[1], new[] { summaries[0], summaries[2] }, true);

			// Test that graphs don't sync when the graph types are different
			Settings.Default.RTGraphType = GraphTypeRT.peptide.ToString();
			TestGraphSummary(summaries[1], new[] {summaries[0], summaries[2]}, false);
			Settings.Default.RTGraphType = GraphTypeRT.replicate.ToString();

			//Test that graphs don't sync when they are hidden
			RunUI(() => { summaries[0].Hide(); summaries[1].Hide(); });
			WaitForGraphs();
			TestGraphSummary(summaries[2], new[] { summaries[0], summaries[1] }, false);
		}

		void SetGraphType(int index, string type)
		{
			switch (index)
			{
				case 0: Settings.Default.RTGraphType = type; break;
				case 1: Settings.Default.AreaGraphType = type; break;
				case 2: Settings.Default.MassErrorGraphType = type; break;
			}
		}

		private void TestGraphSummary(GraphSummary active, GraphSummary[] others, bool shouldSync)
		{
			double min = 0.0;
			double max = SkylineWindow.GraphChromatograms.Count();

			double otherMin = -1.0;
			double otherMax = 1.0;
			
			RunUI(() =>
			{
				active.GraphControl.GraphPane.XAxis.Scale.Min = min;
				active.GraphControl.GraphPane.XAxis.Scale.Max = max;

				foreach (var graphSummary in others)
				{
					graphSummary.GraphControl.GraphPane.XAxis.Scale.Min = otherMin;
					graphSummary.GraphControl.GraphPane.XAxis.Scale.Max = otherMax;
				}

				active.Activate();
				SkylineWindow.SynchronizeSummaryZooming();
			});

			WaitForGraphs();

			RunUI(() =>
			{
				double add = GetExpectedVisible(active) ? -1.0 : 0.0;

				foreach (var graphSummary in others)
				{
					bool expectedVisible = GetExpectedVisible(graphSummary);
					add += expectedVisible ? 1.0 : 0.0;
					Assert.AreEqual(shouldSync ? min + add : otherMin, graphSummary.GraphControl.GraphPane.XAxis.Scale.Min);
					Assert.AreEqual(shouldSync ? max + add : otherMax, graphSummary.GraphControl.GraphPane.XAxis.Scale.Max);
					add += expectedVisible ? -1.0 : 0.0;
				}
			});
		}

		private bool GetExpectedVisible(GraphSummary g)
		{
			var pane = g.GraphControl.GraphPane as AreaReplicateGraphPane;
			return pane != null && pane.IsExpectedVisible;
		}
	}
}