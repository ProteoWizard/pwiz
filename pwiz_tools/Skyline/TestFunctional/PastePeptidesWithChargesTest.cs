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
                Tuple.Create("GVDC[+57]QEVSQEK", 2, "sp|Q61687|ATRX_MOUSE"),
                Tuple.Create("VVEATNSMTAVR", 2, "sp|Q61687|ATRX_MOUSE"),
                Tuple.Create("TSQSGLNTLSQR", 2, "sp|O70126|AURKB_MOUSE"),
                Tuple.Create("IADFGWSVHAPSLR", 2, "sp|O70126|AURKB_MOUSE"),
                Tuple.Create("AMQAVQQEGAGGQQEEK", 2, "sp|Q9Z1S0|BUB1B_MOUSE"),
                Tuple.Create("SPATGGPQVLNAQR", 2, "sp|Q9Z1S0|BUB1B_MOUSE"),
                Tuple.Create("DAGSALLSLHQEDQENVNPEK", 2, "sp|P51943|CCNA2_MOUSE"),
                Tuple.Create("QLEEEQSVRPK", 3, "sp|P24860|CCNB1_MOUSE"),
                Tuple.Create("LSGKPQNAPEGYQNR", 3, "sp|Q9JJ66|CDC20_MOUSE"),
                Tuple.Create("GSISENEAQGASTQDTAK", 2, "sp|Q6RT24|CENPE_MOUSE"),
                Tuple.Create("FIQPNIGELPTALK", 2, "sp|A2AGT5|CKAP5_MOUSE"),
                Tuple.Create("LDDIFEPVLIPEPK", 2, "sp|A2AGT5|CKAP5_MOUSE"),
                Tuple.Create("FYTQQWEDYR", 2, "sp|Q9WTX6|CUL1_MOUSE"),
                Tuple.Create("ESFESQFLADTER", 2, "sp|Q9WTX6|CUL1_MOUSE"),
                Tuple.Create("SPLAELGVLK", 2, "sp|Q8BHK9|ERC6L_MOUSE"),
                Tuple.Create("ASLGPNLDLQDSVVLYHR", 3, "sp|Q8BHK9|ERC6L_MOUSE"),
                Tuple.Create("AQGLDLLQAVLTR", 2, "sp|P60330|ESPL1_MOUSE"),
                Tuple.Create("EVAQAATPQNPVNQGK", 2, "sp|Q8R080|GTSE1_MOUSE"),
                Tuple.Create("GSQSDVLQDKPSTAPDAASR", 3, "sp|Q8R080|GTSE1_MOUSE"),
                Tuple.Create("LAASASTQQFQEVK", 2, "sp|P97329|KI20A_MOUSE"),
                Tuple.Create("TPTC[+57]QSSTDSSPYAR", 2, "sp|P97329|KI20A_MOUSE"),
                Tuple.Create("LNLVDLAGSENIGR", 2, "sp|Q6P9P6|KIF11_MOUSE"),
                Tuple.Create("EAGNINQSLLTLGR", 2, "sp|Q6P9P6|KIF11_MOUSE"),
                Tuple.Create("LTDDHVQFLIYQILR", 3, "sp|P47811|MK14_MOUSE"),
                Tuple.Create("SLAPVELLLR", 2, "sp|P70268|PKN1_MOUSE"),
                Tuple.Create("KLEEEEEGSAPATSR", 2, "sp|A2APB8|TPX2_MOUSE"),
                Tuple.Create("NVTQAEPFSLETDK", 2, "sp|A2APB8|TPX2_MOUSE"),
                Tuple.Create("EVTTLTADPPDGIK", 2, "sp|Q921J4|UBE2S_MOUSE"),
                Tuple.Create("LLTEIHGGAC[+57]STSSGR", 3, "sp|Q921J4|UBE2S_MOUSE"),
                Tuple.Create("LAEQFAAGEVLTDMSR", 2, "sp|Q80X41|VRK1_MOUSE"),
                Tuple.Create("SVESQGAIHGSMSQPAAGC[+57]SSSDSSR", 3, "sp|Q80X41|VRK1_MOUSE"),
            };
            var pasteDlg = ShowDialog<PasteDlg>(SkylineWindow.ShowPastePeptidesDlg);
            var clipboardText = TextUtil.LineSeparate(peptideChargeProteins.Select(tuple =>
                tuple.Item1 + Transition.GetChargeIndicator(tuple.Item2) + "\t" + tuple.Item3
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
                var unmodifiedSequence = FastaSequence.StripModifications(tuple.Item1);
                var peptideDocNodes =
                    SkylineWindow.Document.Molecules.Where(pep => pep.Peptide.Sequence == unmodifiedSequence).ToList();
                Assert.AreEqual(1, peptideDocNodes.Count);
                var peptideDocNode = peptideDocNodes[0];
                Assert.AreEqual(1, peptideDocNode.TransitionGroupCount);
                var precursor = peptideDocNode.TransitionGroups.First();
                Assert.AreEqual(tuple.Item2, precursor.PrecursorCharge);
            }
        }
    }
}
