/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Verifies that charge indicators (e.g. "+++") can be appended to peptide sequences
    /// in the Edit > Insert > Peptides dialog.
    /// </summary>
    [TestClass]
    public class PastePeptidesWithChargesTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestPastePeptidesWithCharges()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // List of peptide sequences, charges, and protein names to paste in the Paste Peptides grid
            var peptideChargeProteins = new[]
            {
                Tuple.Create("VVEATNSMTAVR++", "sp|Q61687|ATRX_MOUSE", new[]{2}, false),
                Tuple.Create("TSQSGLNTLSQR,+6", "sp|O70126|AURKB_MOUSE", new[]{6}, false),
                Tuple.Create("QLEEEQSVRPK+++", "sp|P24860|CCNB1_MOUSE", new[]{3}, false),
                Tuple.Create("LSGKPQNAPEGYQNR", "sp|Q9JJ66|CDC20_MOUSE", new[]{2, 3}, true),
                Tuple.Create("GSISENEAQGASTQDTAK++++", "sp|Q6RT24|CENPE_MOUSE", new[]{4}, false),
            };
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionSettingsUi =>
            {
                transitionSettingsUi.PrecursorCharges = string.Join(TextUtil.CsvSeparator.ToString(), new[] {2, 3});
                transitionSettingsUi.OkDialog();
            });
            var pasteDlg = ShowDialog<PasteDlg>(SkylineWindow.ShowPastePeptidesDlg);
            var clipboardText = TextUtil.LineSeparate(peptideChargeProteins.Select(tuple =>
                tuple.Item1 + "\t" + tuple.Item2
            ));
            RunUI(() =>
            {
                SetClipboardText(clipboardText);
                pasteDlg.PastePeptides();
            });
            OkDialog(pasteDlg, pasteDlg.OkDialog);
            Assert.AreEqual(SkylineWindow.Document.MoleculeCount, peptideChargeProteins.Length);

            // Make sure that the peptides were all inserted with the specified charges
            foreach (var tuple in peptideChargeProteins)
            {
                var unmodifiedSequence = FastaSequence.StripModifications(Transition.StripChargeIndicators(tuple.Item1, TransitionGroup.MIN_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE));
                var peptideDocNodes =
                    SkylineWindow.Document.Molecules.Where(pep => pep.Peptide.Sequence == unmodifiedSequence).ToList();
                Assert.AreEqual(1, peptideDocNodes.Count, unmodifiedSequence);
                var peptideDocNode = peptideDocNodes[0];
                var expectedCharges = tuple.Item3;
                Assert.AreEqual(expectedCharges.Length, peptideDocNode.TransitionGroupCount);
                var actualCharges = peptideDocNode.TransitionGroups.Select(tg => tg.PrecursorCharge).ToList();
                CollectionAssert.AreEqual(expectedCharges, actualCharges);
                Assert.AreEqual(tuple.Item4, peptideDocNode.AutoManageChildren);
            }
        }
    }
}
