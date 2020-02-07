﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.EditUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class GroupComparisonRefineTest : AbstractFunctionalTestEx
    {

        [TestMethod]
        public void TestGroupComparisonRefine()
        {
            TestFilesZip = "TestFunctional/AreaCVHistogramTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            OpenDocument(@"Rat_plasma.sky");

            CreateGroupComparison("Test Group Comparison", "Condition", "Healthy", "Diseased");

            var graphStates = new[] { (48, 43, 43, 248), (48, 20, 20, 114), (48, 103, 103, 597), (48, 44, 44, 255) };

            // Verify that bad inputs show error message
            var refineDlg = ShowDialog<RefineDlg>(() => SkylineWindow.ShowRefineDlg());
            RunUI(() =>
            {
                refineDlg.Log = false;
                refineDlg.FoldChangeCutoff = -1;
            });

            var alertDlg = ShowDialog<AlertDlg>(refineDlg.OkDialog);
            OkDialog(alertDlg, alertDlg.OkDialog);

            RunUI(() =>
            {
                refineDlg.FoldChangeCutoff = 2;
                refineDlg.AdjustedPValueCutoff = 0;
            });
            alertDlg = ShowDialog<AlertDlg>(refineDlg.OkDialog);
            OkDialog(alertDlg, alertDlg.OkDialog);

            // Verify remove below cutoff works
            RunUI(() =>
            {
                refineDlg.Log = false;
                refineDlg.AdjustedPValueCutoff = 0.05;
                refineDlg.FoldChangeCutoff = 2;
            });

            OkDialog(refineDlg, refineDlg.OkDialog);
            var doc = SkylineWindow.Document;
            var refineDocState = (doc.PeptideGroupCount, doc.PeptideCount, doc.PeptideTransitionGroupCount,
                doc.PeptideTransitionCount);
            Assert.AreEqual(graphStates[0], refineDocState);
            RunUI(SkylineWindow.Undo);

            // Verify that using only fold change cutoff works
            refineDlg = ShowDialog<RefineDlg>(() => SkylineWindow.ShowRefineDlg());
            RunUI(() =>
            {
                refineDlg.Log = false;
                refineDlg.FoldChangeCutoff = 3;
            });

            OkDialog(refineDlg, refineDlg.OkDialog);
            doc = SkylineWindow.Document;
            refineDocState = (doc.PeptideGroupCount, doc.PeptideCount, doc.PeptideTransitionGroupCount,
                doc.PeptideTransitionCount);
            Assert.AreEqual(graphStates[1], refineDocState);
            RunUI(SkylineWindow.Undo);

            // Verify using only adjusted p value cutoff works
            refineDlg = ShowDialog<RefineDlg>(() => SkylineWindow.ShowRefineDlg());
            RunUI(() =>
            {
                refineDlg.Log = false;
                refineDlg.AdjustedPValueCutoff = 0.08;
            });

            OkDialog(refineDlg, refineDlg.OkDialog);
            doc = SkylineWindow.Document;
            refineDocState = (doc.PeptideGroupCount, doc.PeptideCount, doc.PeptideTransitionGroupCount,
                doc.PeptideTransitionCount);
            Assert.AreEqual(graphStates[2], refineDocState);
            RunUI(SkylineWindow.Undo);

            // Verify the union of 2 group comparisons works
            CreateGroupComparison("Test Group Comparison 2", "Condition", "Healthy", "Diseased", "BioReplicate");

            refineDlg = ShowDialog<RefineDlg>(() => SkylineWindow.ShowRefineDlg());
            RunUI(() =>
            {
                refineDlg.Log = false;
                refineDlg.AdjustedPValueCutoff = 0.05;
                refineDlg.FoldChangeCutoff = 2;
            });

            OkDialog(refineDlg, refineDlg.OkDialog);
            doc = SkylineWindow.Document;
            refineDocState = (doc.PeptideGroupCount, doc.PeptideCount, doc.PeptideTransitionGroupCount,
                doc.PeptideTransitionCount);
            Assert.AreEqual(graphStates[3], refineDocState);
            RunUI(SkylineWindow.Undo);
        }
    }
}
