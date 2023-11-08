/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
using pwiz.Common.SystemUtil;
using pwiz.ProteomeDatabase.API;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Proteome;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class CopyProteinGroupTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestProteinGroupCopy()
        {
            TestFilesZip = @"TestFunctional\CopyProteinGroupTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            SetClipboardText("ELVIS");
            RunUI(()=>SkylineWindow.Paste());
            var associateProteinsDlg = ShowDialog<AssociateProteinsDlg>(SkylineWindow.ShowAssociateProteinsDlg);
            RunUI(()=>
            {
                associateProteinsDlg.FastaFileName = TestFilesDir.GetTestPath("elvis.fasta");
                associateProteinsDlg.GroupProteins = true;
            });
            WaitForConditionUI(() => associateProteinsDlg.IsOkEnabled);
            OkDialog(associateProteinsDlg, associateProteinsDlg.OkDialog);
            RunUI(() =>
            {
                SkylineWindow.SelectAll();
                SkylineWindow.Copy();
                SkylineWindow.EditDelete();
                SkylineWindow.Paste();
            });

            // Verify that the "ProteinMetadata_Name" entry in the audit log has the correct
            // name of the protein group
            // This is to make sure that the ProteinGroupMetadata override of the Name property
            // was the one whose value got recorded in the audit log
            var document = SkylineWindow.Document;
            var firstProteinGroup = document.MoleculeGroups.First();
            Assert.IsInstanceOfType(firstProteinGroup.PeptideGroup, typeof(FastaSequenceGroup));
            Assert.IsInstanceOfType(firstProteinGroup.ProteinMetadata, typeof(ProteinGroupMetadata));
            var proteinGroupName = firstProteinGroup.ProteinMetadata.Name;
            Assert.IsNotNull(document.AuditLog);
            var lastAuditLogEntry = document.AuditLog.AuditLogEntries;
            Assert.AreEqual(MessageType.pasted_targets, lastAuditLogEntry.UndoRedo.MessageInfo.Type);

            // Construct a PropertyName corresponding to "{0:Targets}{2:PropertySeparator}Protein1 / Protein2{2:PropertySeparator}{0:ProteinMetadata_Name}" which is the first "Name" entry of the detail line
            var propertyName =
                new PropertyName(
                    new PropertyName(
                        new PropertyName(
                            AuditLogParseHelper.GetParseString(ParseStringType.property_names, nameof(Targets))),
                        proteinGroupName),
                    AuditLogParseHelper.GetParseString(ParseStringType.property_names, nameof(ProteinMetadata) + "_" + nameof(ProteinMetadata.Name)));
            
            var proteinMetadataNameDetails = lastAuditLogEntry.AllInfo.Where(detailLogMessage => detailLogMessage.Names.FirstOrDefault() == propertyName.ToString()).ToList();
            Assert.AreEqual(1, proteinMetadataNameDetails.Count);

            var proteinMetadataNameDetail = proteinMetadataNameDetails[0];
            Assert.AreEqual(MessageType.is_, proteinMetadataNameDetail.Type);
            Assert.AreEqual(2, proteinMetadataNameDetail.Names.Count);

            // Verify that the second "Name" value of the detail item is the quoted string "Protein1 / Protein2", so
            Assert.AreEqual(LogMessage.Quote(proteinGroupName), proteinMetadataNameDetail.Names[1]);
        }
    }
}
