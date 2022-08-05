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
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class CrosslinkChromatogramTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestCrosslinkChromatograms()
        {
            TestFilesZip = @"TestFunctional\FullScanIdTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionSettingsUi =>
            {
                transitionSettingsUi.SelectedTab = TransitionSettingsUI.TABS.Filter;
                transitionSettingsUi.FragmentTypes = @"p";
                transitionSettingsUi.SelectedTab = TransitionSettingsUI.TABS.FullScan;
                transitionSettingsUi.PrecursorIsotopesCurrent = FullScanPrecursorIsotopes.Count;
                transitionSettingsUi.PrecursorMassAnalyzer = FullScanMassAnalyzerType.qit;
                transitionSettingsUi.Peaks = 1;
                transitionSettingsUi.PrecursorRes = 0.7;
                transitionSettingsUi.OkDialog();
            });
            var peptideSettingsUi = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() =>
            {
                peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Modifications;
            });
            var editModListDlg = ShowEditStaticModsDlg(peptideSettingsUi);
            // Define a crosslinker which is a water loss. In this way, two peptides can be joined end to end
            // and will have the same chemical formula as a single concatenated peptide
            RunDlg<EditStaticModDlg>(editModListDlg.AddItem, editStaticModDlg => {
                {
                    editStaticModDlg.Modification = new StaticMod(crosslinkerName, null, null, "-H2O");
                    editStaticModDlg.IsCrosslinker = true;
                    editStaticModDlg.OkDialog();
                }
            });
            RunDlg<EditStaticModDlg>(editModListDlg.AddItem, editStaticModDlg =>
            {
                editStaticModDlg.Modification = UniMod.GetModification("Oxidation (M)", true);
                editStaticModDlg.OkDialog();
            });
            OkDialog(editModListDlg, editModListDlg.OkDialog);
            OkDialog(peptideSettingsUi, peptideSettingsUi.OkDialog);
            var peptidesToPaste = TextUtil.LineSeparate(PEPTIDE_SEQUENCES.Select(MakeCrosslinkedSequence));
            RunDlg<PasteDlg>(SkylineWindow.ShowPastePeptidesDlg, pasteDlg =>
            {
                SetClipboardText(peptidesToPaste);
                pasteDlg.PastePeptides();
                pasteDlg.OkDialog();
            });
            RunUI(()=>SkylineWindow.SaveDocument(TestFilesDir.GetTestPath("CrosslinkChromatogramTest.sky")));
            ImportResultsFile(TestFilesDir.GetTestPath("CAexample.mzXML"));
            RunUI(()=>SkylineWindow.ShowRTRegressionGraphScoreToRun());
            RunUI(()=>SkylineWindow.SaveDocument());
        }

        /// <summary>
        /// Transforms an ordinary peptide sequence into a crosslinked peptide composed of three parts attached together
        /// with the Hydrolysis crosslinker.
        /// The crosslinked peptide ends up being the middle peptide sequence which has attachments to the left and right parts of
        /// the peptide sequence.
        ///
        /// For example, AVVQDPALKPLALVYGEATSR becomes:
        /// LKPLALV-AVVQDPA-YGEATSR-[Hydrolysis@1,7,*][Hydrolysis@7,*,1]
        /// </summary>
        private string MakeCrosslinkedSequence(string flatPeptideSequence)
        {
            var aaMods = TokenizeModifiedSequence(flatPeptideSequence).ToList();
            int leftLength = aaMods.Count / 3;
            int middleLength = (2 * aaMods.Count) / 3 - leftLength;

            var left = PeptideLibraryKey.CreateSimple(string.Concat(aaMods.Take(leftLength)));
            var middle = PeptideLibraryKey.CreateSimple(string.Concat(aaMods.Skip(leftLength).Take(middleLength)));
            var right = PeptideLibraryKey.CreateSimple(string.Concat(aaMods.Skip(leftLength + middleLength)));
            var crosslinkLibraryKey = new CrosslinkLibraryKey(new []{middle, left, right}, new []
            {
                new CrosslinkLibraryKey.Crosslink(crosslinkerName, new [] {new[]{1}, new []{leftLength}, Enumerable.Empty<int>()}), 
                new CrosslinkLibraryKey.Crosslink(crosslinkerName, new[]{ new[] { middleLength }, Enumerable.Empty<int>(), new []{1}})
            }, 0);
            return crosslinkLibraryKey.ToString();
        }

        /// <summary>
        /// Returns a sequence of amino acids potentially followed by the modification name in square brackets.
        /// </summary>
        private IEnumerable<string> TokenizeModifiedSequence(string str)
        {
            int ich = 0;
            while (ich < str.Length)
            {
                if (ich == str.Length - 1 || str[ich + 1] != '[')
                {
                    yield return str.Substring(ich, 1);
                    ich++;
                    continue;
                }

                int ichClose = str.IndexOf(']', ich + 1);
                Assert.IsTrue(ichClose > 0);
                yield return str.Substring(ich, ichClose - ich + 1);
                ich = ichClose + 1;
            }
        }

        const string crosslinkerName = "Hydrolysis";
        private static readonly string[] PEPTIDE_SEQUENCES =
        {
            "AVVQDPALKPLALVYGEATSR",
            "DYTQM[Oxidation (M)]NDLQR",
            "EPISVSSQQMLK",
            "EPISVSSQQM[Oxidation (M)]LK",
            "EVLPTPSDDATALMTDPK",
            "FLTIDIEPDIETLLSQGASA",
            "FLVGPDGVPVR",
            "GDGPVQGTIHFEAK",
            "HVGDLGNVTADK",
            "KYAAELHLVHWNTK",
            "LPSEGPQPAHVVVGDVR",
            "LVQFHFHWGSSDDQGSEHTVDR",
            "LYEQLSGK",
            "LYTLVLTDPDAPSR",
            "MVNNGHSFNVEYDDSQDK",
            "M[Oxidation (M)]VNNGHSFNVEYDDSQDK",
            "NDVSWNFEK",
            "NGVAIVDIVDPLISLSGEYSIIGR",
            "NRPTSITWDGLDPGK",
            "QSPVDIDTK",
            "TITLEVEPSDTIENVK",
            "TLSDYNIQK",
            "TMVVHEKPDDLGR",
            "TM[Oxidation (M)]VVHEKPDDLGR",
            "VGDANPALQK",
            "VLDALDSIK",
            "WSGPLSLQEVDERPQHPLQVK",
            "YAAELHLVHWNTK",
            "YGDFGTAAQQPDGLAVVGVFLK",
            "YGGAEVDELGK",
            "YVRPGGGFEPNFMLFEK",
            "YVWLVYEQEGPLK",
        };
    }
}
