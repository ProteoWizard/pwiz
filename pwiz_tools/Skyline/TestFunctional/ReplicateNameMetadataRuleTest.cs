/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using pwiz.Common.DataBinding;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings.MetadataExtraction;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ReplicateNameMetadataRuleTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestReplicateNameMetadataRule()
        {
            TestFilesZip = @"TestFunctional\MetadataRuleTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            const string ruleSetName = "Replicate Name";
            // A result file rule which sets the "Replicate Name" to some portion of the Result File name.
            // This test will set the "Pattern" to different values
            var replicateRule = new MetadataRule().ChangeSource(PropertyPath.Root.Property(nameof(ResultFile.FileName)))
                .ChangeTarget(PropertyPath.Root.Property(nameof(ResultFile.Replicate))
                    .Property(nameof(Replicate.Name)));
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("Rat_plasma.sky")));

            ImportResultsFile(TestFilesDir.GetTestPath("D_102_REP1.mzML"));
            ImportResultsFile(TestFilesDir.GetTestPath("H_146_REP1.mzML"));
            Assert.AreEqual(2, SkylineWindow.Document.MeasuredResults.Chromatograms.Count);
            CollectionAssert.AreEqual(new[] { "D_102_REP1", "H_146_REP1" },
                SkylineWindow.Document.MeasuredResults.Chromatograms.Select(c => c.Name).ToList());

            RunLongDlg<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog, documentSettingsDlg =>
            {
                // Add a rule which will set the Replicate Name of both the replicates to "REP1".
                AddRuleSet(documentSettingsDlg, ruleSetName, replicateRule.ChangePattern("REP[0-9]"));
                // Skyline warns that the rule will result in two replicates having the same name
                RunDlg<AlertDlg>(documentSettingsDlg.OkDialog, alertDlg =>
                {
                    alertDlg.ClickCancel();
                });
                // Change the rule so that it sets the replicate names to "D_102" and "H_146"
                EditRuleSet(documentSettingsDlg, ruleSetName, replicateRule.ChangePattern("[DH]_[0-9]+"));
            }, documentSettingsDlg => documentSettingsDlg.OkDialog());
            CollectionAssert.AreEqual(new[] { "D_102", "H_146" }, SkylineWindow.Document.Settings.MeasuredResults.Chromatograms
                .Select(chrom => chrom.Name).ToList());

            // Import a result whose name is going to conflict with an existing replicate
            ImportResultsFile(TestFilesDir.GetTestPath("D_102_REP2.mzML"),
                expectedErrorMessage: string.Format(
                    MetadataExtractionResources.RuleError_ToString_An_error_occurred_applying_the_rule___0___,
                    ruleSetName));
            WaitForDocumentLoaded();

            // The replicate was successfully imported but its name was not changed by the result file rule.
            CollectionAssert.AreEqual(new[] { "D_102", "H_146", "D_102_REP2" },
                SkylineWindow.Document.MeasuredResults.Chromatograms.Select(c => c.Name).ToList());
            // Edit the rule so that every replicate is assigned a unique name
            RunLongDlg<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog, documentSettingsDlg =>
            {
                EditRuleSet(documentSettingsDlg, ruleSetName, replicateRule.ChangePattern("[0-9]+_REP[0-9]"));
            }, documentSettingsDlg => documentSettingsDlg.OkDialog());
            CollectionAssert.AreEqual(new[] { "102_REP1", "146_REP1", "102_REP2" },
                SkylineWindow.Document.MeasuredResults.Chromatograms.Select(c => c.Name).ToList());
        }

        private void AddRuleSet(DocumentSettingsDlg documentSettingsDlg, string name, params MetadataRule[] rules)
        {
            RunUI(()=>documentSettingsDlg.SelectTab(DocumentSettingsDlg.TABS.metadata_rules));
            RunLongDlg<MetadataRuleSetEditor>(documentSettingsDlg.AddMetadataRule, metadataRuleEditor =>
            {
                RunUI(() => { metadataRuleEditor.RuleName = name; });
                SetRules(metadataRuleEditor, rules);
            }, metadataRuleEditor => metadataRuleEditor.OkDialog());
        }

        private void EditRuleSet(DocumentSettingsDlg documentSettingsDlg, string name, params MetadataRule[] rules)
        {
            RunUI(() => documentSettingsDlg.SelectTab(DocumentSettingsDlg.TABS.metadata_rules));
            RunLongDlg<EditListDlg<SettingsListBase<MetadataRuleSet>, MetadataRuleSet>>(
                documentSettingsDlg.EditMetadataRuleList,
                metadataRuleListEditor =>
                {
                    RunLongDlg<MetadataRuleSetEditor>(() =>
                        {
                            metadataRuleListEditor.SelectItem(name);
                            metadataRuleListEditor.EditItem();
                        },
                        metadataRuleSetEditor => SetRules(metadataRuleSetEditor, rules),
                        metadataRuleSetEditor => metadataRuleSetEditor.OkDialog());
                }, metadataRuleListEditor => metadataRuleListEditor.OkDialog());

        }

        private void SetRules(MetadataRuleSetEditor metadataRuleSetEditor, params MetadataRule[] metadataRules)
        {
            for (int i = 0; i < metadataRules.Length; i++)
            {
                RunDlg<MetadataRuleEditor>(() => metadataRuleSetEditor.EditRule(i),
                    metadataRuleStepEditor =>
                    {
                        metadataRuleStepEditor.MetadataRule = metadataRules[i];
                        metadataRuleStepEditor.OkDialog();
                    });
            }
        }
    }

}
