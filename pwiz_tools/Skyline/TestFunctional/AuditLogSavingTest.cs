using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.AuditLog;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.AuditLog.Databinding;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;
using LogEntryMessages = pwiz.SkylineTestUtil.AuditLogUtil.LogEntryMessages;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class AuditLogSavingTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestAuditLogSaving()
        {
            TestFilesZip = "TestFunctional/AuditLogSavingTest.zip";
            AuditLogList.IgnoreTestChecks = true;
            RunFunctionalTest();
            AuditLogList.IgnoreTestChecks = false;

            Assert.IsFalse(IsRecordMode, "Successfully recorded data");
        }

        public bool IsRecordMode
        {
            get { return false; }
        }

        protected override void DoTest()
        {
            OpenDocument("Rat_plasma.sky");

            RunUI(SkylineWindow.ShowAuditLog);
            var auditLogForm = WaitForOpenForm<AuditLogForm>();
            Assert.IsFalse(SkylineWindow.Document.Settings.DataSettings.AuditLogging);
            RunUI(() => auditLogForm.EnableAuditLogging(true));

            // Test that initial hash is correct
            var expectedHash = BlockHash.SafeToBase64(new byte[]
            {
                0xFE, 0x0F, 0x0C, 0x54,
                0xA0, 0x77, 0xE5, 0x8F,
                0x77, 0xDC, 0x8B, 0xEE,
                0x44, 0xB6, 0x65, 0x6D,
                0x98, 0x31, 0xAA, 0x35
            });
            var actualHash = GetDocumentHash();
            Assert.AreEqual(expectedHash, actualHash);

            // Test that the hash is the same as if the document was simply read and hashed
            // The document is really small (<20KB) so it's fine to read it all into memory
            var bytes = File.ReadAllBytes(SkylineWindow.DocumentFilePath);
            var hash = Hash(bytes);
            Assert.AreEqual(expectedHash, hash);

            // Make sure that there's an entry describing 1) number of nodes and 2) settings changes from default settings
            Assert.AreEqual(1, SkylineWindow.Document.AuditLog.AuditLogEntries.Count);

            if(!RecordNewestEntry())
                RunUI(() => { LOG_ENTRY_MESSAGES[0].AssertEquals(SkylineWindow.Document.AuditLog.AuditLogEntries); });

            // Modify and save the document so that the audit log gets saved
            ChangeSettings(settings => settings.ChangePeptideFilter(filter => filter.ChangeExcludeNTermAAs(3))); // Change from 2 to 3

            RunUI(() => SkylineWindow.SaveDocument());

            RecordNewestEntry();

            Assert.IsTrue(File.Exists(SrmDocument.GetAuditLogPath(SkylineWindow.DocumentFilePath)), "Audit log does not exist after saving document");
            AssertEx.ValidateAuditLogAgainstSchema(File.ReadAllText(SrmDocument.GetAuditLogPath(SkylineWindow.DocumentFilePath)));

            // Also validate an old document
            AssertEx.ValidateAuditLogAgainstSchema(File.ReadAllText(TestFilesDir.GetTestPath("old_rat_plasma.skyl")));

            // Modify document outside of skyline
            var docPath = SkylineWindow.DocumentFilePath;
            RunUI(() => SkylineWindow.NewDocument(true));

            // Audit logging should be enabled since the previous settings will be used
            Assert.IsTrue(SkylineWindow.Document.Settings.DataSettings.AuditLogging);

            // Perform the following action manually:
            // Settings > Peptide Settings -- Filter > Exclude N-Terminal AA's changed from "3" to "4"

            var text = File.ReadAllText(docPath);

            var match = Regex.Match(text, "<peptide_filter .*start=\"3\"");
            Assert.IsTrue(match.Success);
            Assert.IsFalse(match.NextMatch().Success);
            var sb = new StringBuilder(text);
            sb[match.Index + match.Length - 2] = '4';

            File.WriteAllText(docPath, sb.ToString());

            var oldRef = SkylineWindow.Document;
            // Add an entry describing this change
            OpenDocumentNoWait("Rat_plasma.sky");
            var alert = WaitForOpenForm<AlertDlg>();
            OkDialog(alert, alert.ClickYes);
            var logForm = WaitForOpenForm<DocumentChangeLogEntryDlg>();
            RunUI(() => { logForm.LogMessage = "Changed Exlude N-Terminal AA's from 3 to 4 manually"; });
            OkDialog(logForm, logForm.OkDialog);
            // Wait until document gets switched, otherwise WaitForDocumentLoaded will think that the document is already loaded
            // since we're not waiting for the document to open (OpenDocumentNoWait)
            WaitForCondition(() => !ReferenceEquals(oldRef, SkylineWindow.Document));
            Assert.AreNotEqual(expectedHash, SkylineWindow.Document.DocumentHash); // If this wasn't the case the dialogs would never show up, but check anyways
            WaitForDocumentLoaded();
            RunUI(() => SkylineWindow.SaveDocument()); // Save so that hash in audit log matches document now

            // Now audit logging should be enabled
            Assert.IsTrue(SkylineWindow.Document.Settings.DataSettings.AuditLogging);

            // Make sure the entry was actually added
            Assert.AreEqual(3, SkylineWindow.Document.AuditLog.AuditLogEntries.Count);

            // Check that this entry got saved and read correctly, we don't record here
            if (!IsRecordMode)
                RunUI(() => { LOG_ENTRY_MESSAGES[1].AssertEquals(SkylineWindow.Document.AuditLog.AuditLogEntries.Parent); });

            if (!RecordNewestEntry())
                RunUI(() => { LOG_ENTRY_MESSAGES[2].AssertEquals(SkylineWindow.Document.AuditLog.AuditLogEntries); });

            var fasta = FastaImporter.ToFasta(PROTEINLIST_CLIPBOARD_TEXT, TextUtil.SEPARATOR_TSV);
            RunDlg<EmptyProteinsDlg>(() => SkylineWindow.Paste(fasta), dlg => dlg.KeepEmptyProteins());

            if(!RecordNewestEntry())
                RunUI(() => { LOG_ENTRY_MESSAGES[3].AssertEquals(SkylineWindow.Document.AuditLog.AuditLogEntries); });

            RunUI(SkylineWindow.ShowAuditLog);
            var auditLogForm1 = WaitForOpenForm<AuditLogForm>();
            RunUI(() => auditLogForm1.ChooseView(AuditLogStrings.AuditLogForm_MakeAuditLogForm_Undo_Redo));
            AuditLogUtil.WaitForAuditLogForm(auditLogForm1);

            // Show extra info for this entry
            RunDlg<AuditLogExtraInfoForm>(() =>
            {
                Assert.AreEqual(4, auditLogForm1.DataGridView.RowCount);
                var fastaRow = auditLogForm1.DataGridView.Rows[0];
                var undoRedoCol = auditLogForm1.FindColumn(nameof(AuditLogRow.UndoRedoMessage));
                Assert.IsNotNull(undoRedoCol);
                var auditLogRowText = fastaRow.Cells[undoRedoCol.Index].Value as AuditLogRow.AuditLogRowText;
                Assert.IsNotNull(auditLogRowText);
                var col = auditLogForm1.DataGridView.Columns[undoRedoCol.Index] as AuditLogColumn;
                Assert.IsNotNull(col);
                Assert.IsTrue(col.ShouldDisplay(auditLogRowText, (int)AuditLogColumn.ImageIndex.extra_info));
                col.Click(auditLogRowText, (int)AuditLogColumn.ImageIndex.extra_info);
            }, form =>
            {
                var entry = SkylineWindow.DocumentUI.AuditLog.AuditLogEntries;
                Assert.AreEqual(form.Message, entry.UndoRedo.ToString());
                Assert.AreEqual(form.ExtraInfo, entry.ExtraInfo);
                form.OkDialog();
            });

            // Disable audit logging, this should warn the user
            RunDlg<AlertDlg>(() => { auditLogForm1.EnableAuditLogging(false); }, alertDlg => { alertDlg.ClickYes(); });
            AuditLogUtil.WaitForAuditLogForm(auditLogForm1);
            Assert.AreEqual(0, SkylineWindow.Document.AuditLog.AuditLogEntries.Count);
            Assert.IsFalse(SkylineWindow.Document.Settings.DataSettings.AuditLogging);

            // Re-open document without saving
            RunUI(() => SkylineWindow.NewDocument(true));

            OpenDocument("Rat_plasma.sky");
            RunUI(SkylineWindow.ShowAuditLog);
            var auditLogForm2 = WaitForOpenForm<AuditLogForm>();
            RunUI(() => auditLogForm2.ChooseView(AuditLogStrings.AuditLogForm_MakeAuditLogForm_Undo_Redo));
            AuditLogUtil.WaitForAuditLogForm(auditLogForm2);
            RunUI(() =>
            {
                // Audit logging shold be back on and the entries should still be there
                Assert.IsTrue(SkylineWindow.DocumentUI.Settings.DataSettings.AuditLogging);
                Assert.AreEqual(3, SkylineWindow.DocumentUI.AuditLog.AuditLogEntries.Count);
            });

            // Disable again, this time save
            RunDlg<AlertDlg>(() => { auditLogForm2.EnableAuditLogging(false); }, alertDlg => { alertDlg.ClickYes(); });
            RunUI(() => SkylineWindow.SaveDocument());
            // audit log should be gone
            Assert.IsFalse(File.Exists(SrmDocument.GetAuditLogPath(SkylineWindow.DocumentFilePath)));
        }

        private static string Hash(byte[] bytes)
        {
            using (var sha1 = new SHA1CryptoServiceProvider())
            {
                return BlockHash.SafeToBase64(sha1.ComputeHash(bytes));
            }
        }

        private bool RecordNewestEntry()
        {
            if (IsRecordMode)
                Console.WriteLine(AuditLogUtil.AuditLogEntryToCode(SkylineWindow.Document.AuditLog.AuditLogEntries));

            return IsRecordMode;
        }

        private static void ChangeSettings(Func<SrmSettings, SrmSettings> changeSettings)
        {
            RunUI(() => { SkylineWindow.ChangeSettings(changeSettings(SkylineWindow.DocumentUI.Settings), true); });
            
        }

        private static string GetDocumentHash()
        {
            string documentHash = null;
            RunUI(() => documentHash = SkylineWindow.DocumentUI.DocumentHash);
            return documentHash;
        }

        // From FastaImporterTest
        private const string PROTEINLIST_CLIPBOARD_TEXT =
            @"IPI:IPI00187591.3|SWISS-PROT:Q4V8C5-1|ENSEMBL:ENSRNOP00000023455	MDALEEESFALSFSSASDAEFDAVVGCLEDIIMDAEFQLLQRSFMDKYYQEFEDTEENKLTYTPIFNEYISLVEKYIEEQLLERIPGFNMAAFTTTLQHHKDEVAGDIFDMLLTFTDFLAFKEMFLDYRAEKEGRGLDLSSGLVVTSLCKSSSTPASQNNLRH
IPI:IPI00187593.1|SWISS-PROT:P23977|ENSEMBL:ENSRNOP00000024015;ENSRNOP00000047272|REFSEQ:NP_036826	MSKSKCSVGPMSSVVAPAKESNAVGPREVELILVKEQNGVQLTNSTLINPPQTPVEAQERETWSKKIDFLLSVIGFAVDLANVWRFPYLCYKNGGGAFLVPYLLFMVIAGMPLFYMELALGQFNREGAAGVWKICPVLKGVGFTVILISFYVGFFYNVIIAWALHYFFSSFTMDLPWIHCNNTWNSPNCSDAHASNSSDGLGLNDTFGTTPAAEYFERGVLHLHQSRGIDDLGPPRWQLTACLVLVIVLLYFSLWKGVKTSGKVVWITATMPYVVLTALLLRGVTLPGAMDGIRAYLSVDFYRLCEASVWIDAATQVCFSLGVGFGVLIAFSSYNKFTNNCYRDAIITTSINSLTSFSSGFVVFSFLGYMAQKHNVPIRDVATDGPGLIFIIYPEAIATLPLSSAWAAVFFLMLLTLGIDSAMGGMESVITGLVDEFQLLHRHRELFTLGIVLATFLLSLFCVTNGGIYVFTLLDHFAAGTSILFGVLIEAIGVAWFYGVQQFSDDIKQMTGQRPNLYWRLCWKLVSPCFLLYVVVVSIVTFRPPHYGAYIFPDWANALGWIIATSSMAMVPIYATYKFCSLPGSFREKLAYAITPEKDHQLVDRGEVRQFTLRHWLLL
IPI:IPI00187596.1|SWISS-PROT:P23978|ENSEMBL:ENSRNOP00000009705|REFSEQ:NP_077347	MATDNSKVADGQISTEVSEAPVASDKPKTLVVKVQKKAGDLPDRDTWKGRFDFLMSCVGYAIGLGNVWRFPYLCGKNGGGAFLIPYFLTLIFAGVPLFLLECSLGQYTSIGGLGVWKLAPMFKGVGLAAAVLSFWLNIYYIVIISWAIYYLYNSFTTTLPWKQCDNPWNTDRCFSNYSLVNTTNMTSAVVEFWERNMHQMTDGLDKPGQIRWPLAITLAIAWVLVYFCIWKGVGWTGKVVYFSATYPYIMLIILFFRGVTLPGAKEGILFYITPNFRKLSDSEVWLDAATQIFFSYGLGLGSLIALGSYNSFHNNVYRDSIIVCCINSCTSMFAGFVIFSIVGFMAHVTKRSIADVAASGPGLAFLAYPEAVTQLPISPLWAILFFSMLLMLGIDSQFCTVEGFITALVDEYPRLLRNRRELFIAAVCIVSYLIGLSNITQGGIYVFKLFDYYSASGMSLLFLVFFECVSISWFYGVNRFYDNIQEMVGSRPCIWWKLCWSFFTPIIVAGVFLFSAVQMTPLTMGSYVFPKWGQGVGWLMALSSMVLIPGYMAYMFLTLKGSLKQRLQVMIQPSEDIVRPENGPEQPQAGSSASKEAYI";


        private static LogEntryMessages[] LOG_ENTRY_MESSAGES = {
            new LogEntryMessages(
                new LogMessage(LogLevel.undo_redo, MessageType.start_log_existing_doc, SrmDocument.DOCUMENT_TYPE.proteomic, false),
                new LogMessage(LogLevel.summary, MessageType.start_log_existing_doc, SrmDocument.DOCUMENT_TYPE.proteomic, false),
                new[]
                {
                    new DetailLogMessage(LogLevel.undo_redo, MessageType.start_log_existing_doc, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, false),
                    new DetailLogMessage(LogLevel.all_info, MessageType.is_, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:DocumentNodeCounts}{2:PropertySeparator}{0:DocumentNodeCounts_MoleculeGroupCount}",
                        "{3:1}"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.is_, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:DocumentNodeCounts}{2:PropertySeparator}{0:DocumentNodeCounts_MoleculeCount}",
                        "{3:1}"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.is_, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:DocumentNodeCounts}{2:PropertySeparator}{0:DocumentNodeCounts_PrecursorCount}",
                        "{3:1}"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.is_, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:DocumentNodeCounts}{2:PropertySeparator}{0:DocumentNodeCounts_TransitionCount}",
                        "{3:6}"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.changed_from_to, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, false,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_PeptideSettings}{2:TabSeparator}{0:PeptideSettings_Prediction}{2:PropertySeparator}{0:PeptidePrediction_MeasuredRTWindow}",
                        "{3:2}",
                        "{3:5}"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.changed_from_to, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, false,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_PeptideSettings}{2:TabSeparator}{0:PeptideSettings_Filter}{2:PropertySeparator}{0:PeptideFilter_ExcludeNTermAAs}",
                        "{3:25}",
                        "{3:2}"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.changed_from_to, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, false,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_PeptideSettings}{2:TabSeparator}{0:PeptideSettings_Filter}{2:PropertySeparator}{0:PeptideFilter_MinPeptideLength}",
                        "{3:8}",
                        "{3:6}"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.changed_from_to, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, false,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_PeptideSettings}{2:TabSeparator}{0:PeptideSettings_Filter}{2:PropertySeparator}{0:PeptideFilter_MaxPeptideLength}",
                        "{3:25}",
                        "{3:30}"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.changed_from_to, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, false,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_PeptideSettings}{2:TabSeparator}{0:PeptideSettings_Filter}{2:PropertySeparator}{0:PeptideFilter_AutoSelect}",
                        "{3:True}",
                        "{3:False}"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.removed_from, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, false,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_PeptideSettings}{2:TabSeparator}{0:PeptideSettings_Modifications}{2:PropertySeparator}{0:PeptideModifications_StaticModifications}",
                        "\"Carbamidomethyl (C)\""),
                    new DetailLogMessage(LogLevel.all_info, MessageType.added_to, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, false,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_PeptideSettings}{2:TabSeparator}{0:PeptideSettings_Modifications}{2:PropertySeparator}{0:PeptideModifications_StaticModifications}",
                        "\"Carbamidomethyl Cysteine\""),
                    new DetailLogMessage(LogLevel.all_info, MessageType.is_, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_PeptideSettings}{2:TabSeparator}{0:PeptideSettings_Modifications}{2:PropertySeparator}{0:PeptideModifications_StaticModifications}{2:PropertySeparator}\"Carbamidomethyl Cysteine\"{2:PropertySeparator}{0:StaticMod_AAs}",
                        "\"C\""),
                    new DetailLogMessage(LogLevel.all_info, MessageType.is_, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_PeptideSettings}{2:TabSeparator}{0:PeptideSettings_Modifications}{2:PropertySeparator}{0:PeptideModifications_StaticModifications}{2:PropertySeparator}\"Carbamidomethyl Cysteine\"{2:PropertySeparator}{0:StaticMod_Formula}",
                        "\"C2H3ON\""),
                    new DetailLogMessage(LogLevel.all_info, MessageType.is_, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_PeptideSettings}{2:TabSeparator}{0:PeptideSettings_Modifications}{2:PropertySeparator}{0:PeptideModifications_StaticModifications}{2:PropertySeparator}\"Carbamidomethyl Cysteine\"{2:PropertySeparator}{0:StaticMod_MonoisotopicMass}",
                        "{3:57.021464}"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.is_, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_PeptideSettings}{2:TabSeparator}{0:PeptideSettings_Modifications}{2:PropertySeparator}{0:PeptideModifications_StaticModifications}{2:PropertySeparator}\"Carbamidomethyl Cysteine\"{2:PropertySeparator}{0:StaticMod_AverageMass}",
                        "{3:57.05162}"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.changed_from_to, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, false,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Prediction}{2:PropertySeparator}{0:TransitionPrediction_NonNullCollisionEnergy}",
                        "\"{2:None}\"",
                        "\"Thermo\""),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Prediction}{2:PropertySeparator}{0:TransitionPrediction_NonNullCollisionEnergy}{2:PropertySeparator}{0:CollisionEnergyRegression_Conversions}",
                        "{ {0:ChargeRegressionLine_Charge} = {3:2}, {0:ChargeRegressionLine_Slope} = {3:0.034}, {0:ChargeRegressionLine_Intercept} = {3:3.314} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Prediction}{2:PropertySeparator}{0:TransitionPrediction_NonNullCollisionEnergy}{2:PropertySeparator}{0:CollisionEnergyRegression_Conversions}",
                        "{ {0:ChargeRegressionLine_Charge} = {3:3}, {0:ChargeRegressionLine_Slope} = {3:0.044}, {0:ChargeRegressionLine_Intercept} = {3:3.314} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.changed_from_to, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, false,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Filter}{2:PropertySeparator}{0:TransitionFilter_SmallMoleculePrecursorAdductsString}",
                        "\"[M+H]\"",
                        "\"[M-3H], [M-2H], [M-H], [M-], [M+H], [M+], [M+2H], [M+3H]\""),
                    new DetailLogMessage(LogLevel.all_info, MessageType.changed_from_to, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, false,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Filter}{2:PropertySeparator}{0:TransitionFilter_SmallMoleculeFragmentAdductsString}",
                        "\"[M+]\"",
                        "\"[M-3], [M-2], [M-], [M+], [M+2], [M+3]\""),
                    new DetailLogMessage(LogLevel.all_info, MessageType.changed_from_to, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, false,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Filter}{2:PropertySeparator}{0:TransitionFilter_StartFragmentFinderLabel}",
                        "\"{6:MzFragmentFinder_m_z__gt__precursor}\"",
                        "\"{6:OrdinalFragmentFinder_ion_3}\""),
                    new DetailLogMessage(LogLevel.all_info, MessageType.changed_from_to, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, false,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Filter}{2:PropertySeparator}{0:TransitionFilter_EndFragmentFinderLabel}",
                        "\"{6:DeltaFragmentFinder_3_ions}\"",
                        "\"{6:LastFragmentFinder_last_ion__minus__1}\""),
                    new DetailLogMessage(LogLevel.all_info, MessageType.removed_all, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, false,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Filter}{2:PropertySeparator}{0:TransitionFilter_MeasuredIons}"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.changed_from_to, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, false,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Filter}{2:PropertySeparator}{0:TransitionFilter_AutoSelect}",
                        "{3:True}",
                        "{3:False}"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.changed_from_to, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, false,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Libraries}{2:PropertySeparator}{0:TransitionLibraries_IonCount}",
                        "{3:3}",
                        "{3:4}"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.changed_from_to, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, false,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Libraries}{2:PropertySeparator}{0:TransitionLibraries_Pick}",
                        "\"{6:TransitionLibraryPick_all}\"",
                        "\"{6:TransitionLibraryPick_none}\""),
                    new DetailLogMessage(LogLevel.all_info, MessageType.changed_from_to, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, false,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Libraries}{2:PropertySeparator}{0:TransitionLibraries_PickMostIntenseIons}",
                        "{3:True}",
                        "{3:False}"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.changed_from_to, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, false,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Integration}{2:PropertySeparator}{0:TransitionIntegration_IsIntegrateAll}",
                        "{3:False}",
                        "{3:True}"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.added_to, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, false,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_DataSettings}{2:TabSeparator}{0:DataSettings_AnnotationDefs}",
                        "\"SubjectId\""),
                    new DetailLogMessage(LogLevel.all_info, MessageType.is_, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_DataSettings}{2:TabSeparator}{0:DataSettings_AnnotationDefs}{2:PropertySeparator}\"SubjectId\"{2:PropertySeparator}{0:AnnotationDef_AnnotationTargets}",
                        "\"replicate\""),
                    new DetailLogMessage(LogLevel.all_info, MessageType.is_, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_DataSettings}{2:TabSeparator}{0:DataSettings_AnnotationDefs}{2:PropertySeparator}\"SubjectId\"{2:PropertySeparator}{0:AnnotationDef_Type}",
                        "\"{6:AnnotationType_text}\""),
                    new DetailLogMessage(LogLevel.all_info, MessageType.added_to, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, false,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_DataSettings}{2:TabSeparator}{0:DataSettings_AnnotationDefs}",
                        "\"BioReplicate\""),
                    new DetailLogMessage(LogLevel.all_info, MessageType.is_, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_DataSettings}{2:TabSeparator}{0:DataSettings_AnnotationDefs}{2:PropertySeparator}\"BioReplicate\"{2:PropertySeparator}{0:AnnotationDef_AnnotationTargets}",
                        "\"replicate\""),
                    new DetailLogMessage(LogLevel.all_info, MessageType.is_, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_DataSettings}{2:TabSeparator}{0:DataSettings_AnnotationDefs}{2:PropertySeparator}\"BioReplicate\"{2:PropertySeparator}{0:AnnotationDef_Type}",
                        "\"{6:AnnotationType_text}\""),
                    new DetailLogMessage(LogLevel.all_info, MessageType.added_to, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, false,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_DataSettings}{2:TabSeparator}{0:DataSettings_AnnotationDefs}",
                        "\"Condition\""),
                    new DetailLogMessage(LogLevel.all_info, MessageType.is_, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_DataSettings}{2:TabSeparator}{0:DataSettings_AnnotationDefs}{2:PropertySeparator}\"Condition\"{2:PropertySeparator}{0:AnnotationDef_AnnotationTargets}",
                        "\"replicate\""),
                    new DetailLogMessage(LogLevel.all_info, MessageType.is_, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_DataSettings}{2:TabSeparator}{0:DataSettings_AnnotationDefs}{2:PropertySeparator}\"Condition\"{2:PropertySeparator}{0:AnnotationDef_Type}",
                        "\"{6:AnnotationType_value_list}\""),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_DataSettings}{2:TabSeparator}{0:DataSettings_AnnotationDefs}{2:PropertySeparator}\"Condition\"{2:PropertySeparator}{0:AnnotationDef_Items}",
                        "\"Healthy\""),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_DataSettings}{2:TabSeparator}{0:DataSettings_AnnotationDefs}{2:PropertySeparator}\"Condition\"{2:PropertySeparator}{0:AnnotationDef_Items}",
                        "\"Diseased\""),
                    new DetailLogMessage(LogLevel.all_info, MessageType.added_to, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, false,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_DataSettings}{2:TabSeparator}{0:DataSettings_AnnotationDefs}",
                        "\"MissingData\""),
                    new DetailLogMessage(LogLevel.all_info, MessageType.is_, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_DataSettings}{2:TabSeparator}{0:DataSettings_AnnotationDefs}{2:PropertySeparator}\"MissingData\"{2:PropertySeparator}{0:AnnotationDef_AnnotationTargets}",
                        "\"peptide\""),
                    new DetailLogMessage(LogLevel.all_info, MessageType.is_, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_DataSettings}{2:TabSeparator}{0:DataSettings_AnnotationDefs}{2:PropertySeparator}\"MissingData\"{2:PropertySeparator}{0:AnnotationDef_Type}",
                        "\"{6:AnnotationType_true_false}\""),
                }, @"{0:DocumentNodeCounts_MoleculeGroupCount} = {3:1},
{0:DocumentNodeCounts_MoleculeCount} = {3:1},
{0:DocumentNodeCounts_PrecursorCount} = {3:1},
{0:DocumentNodeCounts_TransitionCount} = {3:6}"),
            new LogEntryMessages(
                new LogMessage(LogLevel.undo_redo, MessageType.changed_to, SrmDocument.DOCUMENT_TYPE.proteomic, false,
                    "{0:SrmSettings_PeptideSettings}{2:TabSeparator}{0:PeptideSettings_Filter}{2:PropertySeparator}{0:PeptideFilter_ExcludeNTermAAs}",
                    "{3:3}"),
                new LogMessage(LogLevel.summary, MessageType.changed_to, SrmDocument.DOCUMENT_TYPE.proteomic, false,
                    "{0:Settings}{2:PropertySeparator}{0:SrmSettings_PeptideSettings}{2:TabSeparator}{0:PeptideSettings_Filter}{2:PropertySeparator}{0:PeptideFilter_ExcludeNTermAAs}",
                    "{3:3}"),
                new[]
                {
                    new DetailLogMessage(LogLevel.undo_redo, MessageType.changed_to, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, false,
                        "{0:SrmSettings_PeptideSettings}{2:TabSeparator}{0:PeptideSettings_Filter}{2:PropertySeparator}{0:PeptideFilter_ExcludeNTermAAs}",
                        "{3:3}"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.changed_from_to, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, false,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_PeptideSettings}{2:TabSeparator}{0:PeptideSettings_Filter}{2:PropertySeparator}{0:PeptideFilter_ExcludeNTermAAs}",
                        "{3:2}",
                        "{3:3}"),
                }),
            new LogEntryMessages(
                new LogMessage(LogLevel.undo_redo, MessageType.modified_outside_of_skyline, SrmDocument.DOCUMENT_TYPE.none, false,
                    "Changed Exlude N-Terminal AA's from 3 to 4 manually"),
                new LogMessage(LogLevel.summary, MessageType.modified_outside_of_skyline, SrmDocument.DOCUMENT_TYPE.none, false,
                    "Changed Exlude N-Terminal AA's from 3 to 4 manually"),
                new[]
                {
                    new DetailLogMessage(LogLevel.undo_redo, MessageType.modified_outside_of_skyline, SrmDocument.DOCUMENT_TYPE.none, string.Empty, false,
                        "Changed Exlude N-Terminal AA's from 3 to 4 manually"),
                }),
            new LogEntryMessages(
                new LogMessage(LogLevel.undo_redo, MessageType.imported_fasta_paste, SrmDocument.DOCUMENT_TYPE.none, false),
                new LogMessage(LogLevel.summary, MessageType.imported_fasta_paste, SrmDocument.DOCUMENT_TYPE.none, false),
                new[]
                {
                    new DetailLogMessage(LogLevel.undo_redo, MessageType.imported_fasta_paste, SrmDocument.DOCUMENT_TYPE.none, string.Empty, false),
                    new DetailLogMessage(LogLevel.all_info, MessageType.kept_empty_proteins, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, false,
                        "3"),
                }, @">IPI:IPI00187591.3|SWISS-PROT:Q4V8C5-1|ENSEMBL:ENSRNOP00000023455
MDALEEESFALSFSSASDAEFDAVVGCLEDIIMDAEFQLLQRSFMDKYYQEFEDTEENKLTYTPIFNEYISLVEKYIEEQLLERIPGFNMAAFTTTLQHHKDEVAGDIFDMLLTFTDFLAFKEMFLDYRAEKEGRGLDLSSGLVVTSLCKSSSTPASQNNLRH
>IPI:IPI00187593.1|SWISS-PROT:P23977|ENSEMBL:ENSRNOP00000024015;ENSRNOP00000047272|REFSEQ:NP_036826
MSKSKCSVGPMSSVVAPAKESNAVGPREVELILVKEQNGVQLTNSTLINPPQTPVEAQERETWSKKIDFLLSVIGFAVDLANVWRFPYLCYKNGGGAFLVPYLLFMVIAGMPLFYMELALGQFNREGAAGVWKICPVLKGVGFTVILISFYVGFFYNVIIAWALHYFFSSFTMDLPWIHCNNTWNSPNCSDAHASNSSDGLGLNDTFGTTPAAEYFERGVLHLHQSRGIDDLGPPRWQLTACLVLVIVLLYFSLWKGVKTSGKVVWITATMPYVVLTALLLRGVTLPGAMDGIRAYLSVDFYRLCEASVWIDAATQVCFSLGVGFGVLIAFSSYNKFTNNCYRDAIITTSINSLTSFSSGFVVFSFLGYMAQKHNVPIRDVATDGPGLIFIIYPEAIATLPLSSAWAAVFFLMLLTLGIDSAMGGMESVITGLVDEFQLLHRHRELFTLGIVLATFLLSLFCVTNGGIYVFTLLDHFAAGTSILFGVLIEAIGVAWFYGVQQFSDDIKQMTGQRPNLYWRLCWKLVSPCFLLYVVVVSIVTFRPPHYGAYIFPDWANALGWIIATSSMAMVPIYATYKFCSLPGSFREKLAYAITPEKDHQLVDRGEVRQFTLRHWLLL
>IPI:IPI00187596.1|SWISS-PROT:P23978|ENSEMBL:ENSRNOP00000009705|REFSEQ:NP_077347
MATDNSKVADGQISTEVSEAPVASDKPKTLVVKVQKKAGDLPDRDTWKGRFDFLMSCVGYAIGLGNVWRFPYLCGKNGGGAFLIPYFLTLIFAGVPLFLLECSLGQYTSIGGLGVWKLAPMFKGVGLAAAVLSFWLNIYYIVIISWAIYYLYNSFTTTLPWKQCDNPWNTDRCFSNYSLVNTTNMTSAVVEFWERNMHQMTDGLDKPGQIRWPLAITLAIAWVLVYFCIWKGVGWTGKVVYFSATYPYIMLIILFFRGVTLPGAKEGILFYITPNFRKLSDSEVWLDAATQIFFSYGLGLGSLIALGSYNSFHNNVYRDSIIVCCINSCTSMFAGFVIFSIVGFMAHVTKRSIADVAASGPGLAFLAYPEAVTQLPISPLWAILFFSMLLMLGIDSQFCTVEGFITALVDEYPRLLRNRRELFIAAVCIVSYLIGLSNITQGGIYVFKLFDYYSASGMSLLFLVFFECVSISWFYGVNRFYDNIQEMVGSRPCIWWKLCWSFFTPIIVAGVFLFSAVQMTPLTMGSYVFPKWGQGVGWLMALSSMVLIPGYMAYMFLTLKGSLKQRLQVMIQPSEDIVRPENGPEQPQAGSSASKEAYI
"),


        };
    }
}