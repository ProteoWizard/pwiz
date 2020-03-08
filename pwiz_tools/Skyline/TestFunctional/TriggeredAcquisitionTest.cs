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
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class TriggeredAcquisitionTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestTriggeredAcquisition()
        {
            TestFilesZip = @"TestFunctional\TriggeredAcquisitionTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("TriggeredAcquisitionTestWIthMs1.sky")));
            // First import the results as not triggered:
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, dlg =>
            {
                dlg.TriggeredAcquisition = false;
                dlg.OkDialog();
            });
            Assert.IsFalse(SkylineWindow.Document.Settings.TransitionSettings.Instrument.TriggeredAcquisition);
            ImportResultsFile(TestFilesDir.GetTestPath("TriggeredAcquisition.mzML"));
            var idPathPep1 = FindPeptide(SkylineWindow.Document, "VTSIQDWVQK");
            var idPathPep2 = FindPeptide(SkylineWindow.Document, "LGPHAGDVEGHLSFLEK");
            var untriggeredDocument = SkylineWindow.Document;
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, dlg =>
            {
                dlg.TriggeredAcquisition = true;
                dlg.OkDialog();
            });
            Assert.IsTrue(SkylineWindow.Document.Settings.TransitionSettings.Instrument.TriggeredAcquisition);

            // TODO (nicksh): Update the timestamp on the .mzML file so that ChromatogramSet.CalcCacheFlags notices
            // that the file is different.
            // This should be removed once we have a robust way of noticing that a file has been reimported
            File.SetLastWriteTimeUtc(TestFilesDir.GetTestPath("TriggeredAcquisition.mzML"), DateTime.UtcNow);

            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, dlg =>
            {
                dlg.ReimportResults();
                dlg.OkDialog();
            });
            WaitForDocumentChange(untriggeredDocument);
            WaitForConditionUI(() => SkylineWindow.DocumentUI.Settings.MeasuredResults.IsLoaded);
            var triggeredDocument = SkylineWindow.Document;
            CompareUntriggeredTriggered(untriggeredDocument, triggeredDocument, idPathPep1);
            CompareUntriggeredTriggered(untriggeredDocument, triggeredDocument, idPathPep2);
        }

        private void CompareUntriggeredTriggered(SrmDocument untriggeredDocument, SrmDocument triggeredDocument, IdentityPath identityPath)
        {
            Assert.IsFalse(untriggeredDocument.Settings.TransitionSettings.Instrument.TriggeredAcquisition);
            Assert.IsTrue(triggeredDocument.Settings.TransitionSettings.Instrument.TriggeredAcquisition);
            PeptideDocNode untriggeredPeptide = (PeptideDocNode) untriggeredDocument.FindNode(identityPath);
            PeptideDocNode triggeredPeptide = (PeptideDocNode) triggeredDocument.FindNode(identityPath);

            Assert.AreEqual(untriggeredPeptide.TransitionGroupCount, triggeredPeptide.TransitionGroupCount);
            for (int iTransitionGroup = 0;
                iTransitionGroup < untriggeredPeptide.TransitionGroupCount;
                iTransitionGroup++)
            {
                var untriggeredTransitionGroup = (TransitionGroupDocNode) untriggeredPeptide.Children[iTransitionGroup];
                var triggeredTransitionGroup = (TransitionGroupDocNode) triggeredPeptide.Children[iTransitionGroup];
                Assert.IsNotNull(untriggeredTransitionGroup.Results);
                Assert.IsNotNull(triggeredTransitionGroup.Results);
                Assert.AreEqual(untriggeredTransitionGroup.Results.Count, triggeredTransitionGroup.Results.Count);
                Assert.AreNotEqual(0, untriggeredTransitionGroup.Results.Count);
                for (int iReplicate = 0; iReplicate < untriggeredTransitionGroup.Results.Count; iReplicate++)
                {
                    Assert.AreEqual(1, untriggeredTransitionGroup.Results[iReplicate].Count);
                    Assert.AreEqual(1, triggeredTransitionGroup.Results[iReplicate].Count);
                    var untriggeredChromInfo = untriggeredTransitionGroup.Results[iReplicate].First();
                    var triggeredChromInfo = triggeredTransitionGroup.Results[iReplicate].First();
                    Assert.AreNotEqual(0, untriggeredChromInfo.BackgroundArea);
                    Assert.AreEqual(0, triggeredChromInfo.BackgroundArea);
                }
            }
        }

        private IdentityPath FindPeptide(SrmDocument doc, string peptideSequence)
        {
            foreach (var moleculeGroup in doc.MoleculeGroups)
            {
                foreach (var molecule in moleculeGroup.Molecules)
                {
                    if (peptideSequence == molecule.Target.Sequence)
                    {
                        return new IdentityPath(moleculeGroup.Id, molecule.Id);
                    }
                }
            }

            return null;
        }
    }
}
