/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class SimpleRatiosTest : AbstractFunctionalTest
    {
        private PropertyPath propertyPathNormalizedArea = PropertyPath.Root
            .Property(nameof(PeptideResult.Quantification))
            .Property(nameof(QuantificationResult.NormalizedArea));

        [TestMethod]
        public void TestSimpleRatios()
        {
            TestFilesZip = @"TestFunctional\SimpleRatiosTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("SimpleRatiosTest.sky"));
                SkylineWindow.ExpandPrecursors();
            });
            WaitForGraphs();
            Assert.IsFalse(SkylineWindow.Document.Settings.PeptideSettings.Quantification.SimpleRatios);
            Assert.AreEqual(new NormalizationMethod.RatioToLabel(IsotopeLabelType.heavy),
                SkylineWindow.Document.Settings.PeptideSettings.Quantification.NormalizationMethod);
            PeptideTreeNode peptideTreeNode = (PeptideTreeNode) SkylineWindow.SequenceTree.Nodes[0].Nodes[0];
            TransitionGroupTreeNode lightTreeNode =
                (TransitionGroupTreeNode) peptideTreeNode.Nodes[0];
            TransitionGroupTreeNode heavyTreeNode = (TransitionGroupTreeNode) peptideTreeNode.Nodes[1];
            var lightTransitionAreas =
                lightTreeNode.DocNode.Transitions.Select(tran => tran.Results[0][0].Area).ToList();
            Assert.AreEqual(4, lightTransitionAreas.Count);
            var heavyTransitionAreas =
                heavyTreeNode.DocNode.Transitions.Select(tran => tran.Results[0][0].Area).ToList();
            Assert.AreEqual(2, heavyTransitionAreas.Count);
            // Verify that only the first two transition areas in both precursors contribute to the displayed ratio
            var expectedRatio = lightTransitionAreas.Take(2).Sum() / heavyTransitionAreas.Sum();
            StringAssert.Contains(lightTreeNode.Text, GetTransitionGroupRatioText(expectedRatio));

            RunUI(()=>
            {
                SkylineWindow.ShowResultsGrid(true);
                SkylineWindow.SelectedPath = peptideTreeNode.Path;
            });
            var resultsGridForm = FindOpenForm<LiveResultsGrid>();
            WaitForConditionUI(()=>resultsGridForm.IsComplete);
            RunDlg<ViewEditor>(resultsGridForm.DataboundGridControl.NavBar.CustomizeView, viewEditor =>
            {
                viewEditor.ChooseColumnsTab.AddColumn(propertyPathNormalizedArea);
                viewEditor.ViewName = "PeptideResultsWithNormalizedArea";
                viewEditor.OkDialog();
            });
            VerifyExpectedRatio(resultsGridForm, expectedRatio);

            RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
            {
                peptideSettingsUi.SimpleRatios = true;
                peptideSettingsUi.OkDialog();
            });
            Assert.IsTrue(SkylineWindow.Document.Settings.PeptideSettings.Quantification.SimpleRatios);


            // When SimpleRatios is true, verify that all transitions contribute to the ratio
            expectedRatio = lightTransitionAreas.Sum() / heavyTransitionAreas.Sum();
            StringAssert.Contains(lightTreeNode.Text, GetTransitionGroupRatioText(expectedRatio));

            VerifyExpectedRatio(resultsGridForm, expectedRatio);

            // Verify that deleting a transition from the light precursor does not affect the heavy precursor
            Assert.AreEqual(4, lightTreeNode.DocNode.TransitionCount);
            Assert.AreEqual(2, heavyTreeNode.DocNode.TransitionCount);
            RunUI(() =>
            {
                SkylineWindow.SequenceTree.SelectedPaths = new[]
                    {SkylineWindow.Document.GetPathTo((int) SrmDocument.Level.Transitions, 0)};
                SkylineWindow.EditDelete();
            });

            Assert.AreEqual(3, lightTreeNode.DocNode.TransitionCount);
            Assert.AreEqual(2, heavyTreeNode.DocNode.TransitionCount);

            expectedRatio = lightTransitionAreas.Skip(1).Sum() / heavyTransitionAreas.Sum();
            StringAssert.Contains(lightTreeNode.Text, GetTransitionGroupRatioText(expectedRatio));

            RunUI(()=>SkylineWindow.SelectedPath = peptideTreeNode.Path);
            VerifyExpectedRatio(resultsGridForm, expectedRatio);
        }

        private string GetTransitionGroupRatioText(float ratio)
        {
            return string.Format(Resources.TransitionGroupTreeNode_GetResultsText_total_ratio__0__,
                MathEx.RoundAboveZero(ratio, 2, 4));
        }

        private void VerifyExpectedRatio(LiveResultsGrid resultsGridForm, float expectedRatio)
        {
            WaitForConditionUI(() => resultsGridForm.IsComplete);
            RunUI(() =>
            {
                Assert.AreEqual(1, resultsGridForm.RowCount);
                var colRatio =
                    resultsGridForm.FindColumn(PropertyPath.Root.Property(nameof(PeptideResult.RatioToStandard)));
                var colNormalizedArea = resultsGridForm.FindColumn(propertyPathNormalizedArea);
                var row = resultsGridForm.DataGridView.Rows[0];
                AssertEx.AreEqual(expectedRatio, (double)row.Cells[colRatio.Index].Value, 1e-5);
                AssertEx.AreEqual(expectedRatio, Convert.ToDouble(row.Cells[colNormalizedArea.Index].Value), 1e-5);
            });

        }
    }
}
