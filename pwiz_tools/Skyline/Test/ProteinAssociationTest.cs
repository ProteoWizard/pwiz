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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class ProteinAssociationTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestTrypticProteinAssociation()
        {
            var fastaFileText = @">Protein1
MRALWVLGLCCVLLTFGSVRADDEVDVDGTVEEDLGKSREGSRTDDEVVQREEEAIQLDG
LNASQIRELREKSEKFAFQAEVNRMMKLIINSLYKNKEIFLRELISNASDALDKIRLISL
TDENALSGNEELTVKIKCDKEKNLLHVTDTGVGMTREELVKNLGTIAKSGTSEFLNKMTE
>Protein2
MPEEVHHGEEEVETFAFQAEIAQLMSLIINTFYSNKEIFLRELISNASDALDKIRYESLT
DPSKLDSGKELKIDIIPNPQERTLTLVDTGIGMTKADLINNLGTIAKSGTKAFMEALQAG
ADISMIGQFGVGFYSAYLVAEKVVVITKHNDDEQYAWESSAGGSFTVRADHGEPIGRGTK";
            var peptides = new[]
            {
                "ADLINNLGTIAK", "ELISNASDALDKIR", "FAFQAEVNR", "IDIIPNPQER", "LIINSLYK",
                "LISLTDENALSGNEELTVK", "NKEIFLR", "NLLHVTDTGVGMTR", "SGTSEFLNK", "TDDEVVQREEEAIQLDGLNASQIR",
                "KYSQFINFPIYVWSSK"
            };

            var document = CreateDocumentWithPeptides(peptides);

            // Associate proteins using Trypsin. The peptide "NKEIFLR" is only tryptic for the first protein
            var trypsin = EnzymeList.GetDefault();
            var trypsinAssociatedProteins = AssociateProteins(document, fastaFileText, trypsin);
            CollectionAssert.Contains(trypsinAssociatedProteins["Protein1"], "NKEIFLR");
            CollectionAssert.DoesNotContain(trypsinAssociatedProteins["Protein2"], "NKEIFLR");
            CollectionAssert.AreEquivalent(new[] { "ADLINNLGTIAK", "ELISNASDALDKIR", "IDIIPNPQER" },
                trypsinAssociatedProteins["Protein2"]);

            // Now associate proteins using Chymotrypsin. The peptide "NKEIFLR" is not chymotryptic for either protein
            var chymotrypsin = new Enzyme("Chymotrypsin", "FWYL", "P");
            var chymotrypsinAssociatedProteins = AssociateProteins(document, fastaFileText, chymotrypsin);
            CollectionAssert.Contains(chymotrypsinAssociatedProteins["Protein1"], "NKEIFLR");
            CollectionAssert.Contains(chymotrypsinAssociatedProteins["Protein2"], "NKEIFLR");
            CollectionAssert.AreEquivalent(new[] { "ADLINNLGTIAK", "ELISNASDALDKIR", "IDIIPNPQER", "NKEIFLR" },
                chymotrypsinAssociatedProteins["Protein2"]);
        }

        private static Dictionary<string, List<string>> AssociateProteins(SrmDocument document, string fastaFileText, Enzyme enzyme)
        {
            var fastaProteinSource =
                new ProteinAssociation.FastaSource(new MemoryStream(Encoding.UTF8.GetBytes(fastaFileText)));

            var proteinAssociation = new ProteinAssociation(document, new LongWaitBrokerImpl());
            proteinAssociation.UseProteinSource(fastaProteinSource, enzyme, new LongWaitBrokerImpl());
            return proteinAssociation.AssociatedProteins.ToDictionary(
                kvp => kvp.Key.Sequence.Name, kvp => kvp.Value.Peptides.Select(p => p.Peptide.Sequence).ToList());
        }

        private static SrmDocument CreateDocumentWithPeptides(IEnumerable<string> peptides)
        {
            var settings = SrmSettingsList.GetDefault();
            var peptideDocNodes = new List<PeptideDocNode>();
            foreach (var peptideSequence in peptides)
            {
                var peptideDocNode =
                    new PeptideDocNode(new Peptide(peptideSequence)).ChangeSettings(settings, SrmSettingsDiff.ALL);
                peptideDocNodes.Add(peptideDocNode);
            }

            var peptideGroupDocNode = new PeptideGroupDocNode(new PeptideGroup(), Annotations.EMPTY, "Peptide List",
                null, peptideDocNodes.ToArray());
            return (SrmDocument)new SrmDocument(settings).ChangeChildren(new DocNode[] { peptideGroupDocNode });
        }

        private class LongWaitBrokerImpl : ILongWaitBroker
        {
            public bool IsCanceled
            {
                get { return false; }
            }
            public int ProgressValue { get; set; }
            public string Message { get; set; }
            public bool IsDocumentChanged(SrmDocument docOrig)
            {
                return false;
            }

            public DialogResult ShowDialog(Func<IWin32Window, DialogResult> show)
            {
                throw new InvalidOperationException();
            }

            public void SetProgressCheckCancel(int step, int totalSteps)
            {
            }

            public CancellationToken CancellationToken => CancellationToken.None;
        }
}
}
