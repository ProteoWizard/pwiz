/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Summary description for LibraryExplorerTest
    /// </summary>
    [TestClass]
    public class NeutralLossLibraryTest : AbstractFunctionalTest
    {
        private const string TEXT_FASTA_SPROT =
            ">sp|P04075|ALDOA_HUMAN Fructose-bisphosphate aldolase A OS=Homo sapiens GN=ALDOA PE=1 SV=2\n" +
            "MPYQYPALTPEQKKELSDIAHRIVAPGKGILAADESTGSIAKRLQSIGTENTEENRRFYR\n" +
            "QLLLTADDRVNPCIGGVILFHETLYQKADDGRPFPQVIKSKGGVVGIKVDKGVVPLAGTN\n" +
            "GETTTQGLDGLSERCAQYKKDGADFAKWRCVLKIGEHTPSALAIMENANVLARYASICQQ\n" +
            "NGIVPIVEPEILPDGDHDLKRCQYVTEKVLAAVYKALSDHHIYLEGTLLKPNMVTPGHAC\n" +
            "TQKFSHEEIAMATVTALRRTVPPAVTGITFLSGGQSEEEASINLNAINKCPLLKPWALTF\n" +
            "SYGRALQASALKAWGGKKENLKAAQEEYVKRALANSLACQGKYTPSGQAGAAASESLFVS\n" +
            "NHAY*\n" +
            ">sp|Q9Y5C1|ANGL3_HUMAN Angiopoietin-related protein 3 OS=Homo sapiens GN=ANGPTL3 PE=1 SV=1\n" +
            "MFTIKLLLFIVPLVISSRIDQDNSSFDSLSPEPKSRFAMLDDVKILANGLLQLGHGLKDF\n" +
            "VHKTKGQINDIFQKLNIFDQSFYDLSLQTSEIKEEEKELRRTTYKLQVKNEEVKNMSLEL\n" +
            "NSKLESLLEEKILLQQKVKYLEEQLTNLIQNQPETPEHPEVTSLKTFVEKQDNSIKDLLQ\n" +
            "TVEDQYKQLNQQHSQIKEIENQLRRTSIQEPTEISLSSKPRAPRTTPFLQLNEIRNVKHD\n" +
            "GIPAECTTIYNRGEHTSGMYAIRPSNSQVFHVYCDVISGSPWTLIQHRIDGSQNFNETWE\n" +
            "NYKYGFGRLDGEFWLGLEKIYSIVKQSNYVLRIELEDWKDNKHYIEYSFYLGNHETNYTL\n" +
            "HLVAITGNVPNAIPENKDLVFSTWDHKAKGHFNCPEGYSGGWWWHDECGENNLNGKYNKP\n" +
            "RAKSKPERRRGLSWKSQNGRLYSIKSTKMLIHPTDSESFE*\n" +
            ">sp|Q13790|APOF_HUMAN Apolipoprotein F OS=Homo sapiens GN=APOF PE=1 SV=1\n" +
            "MIPVELLLCYLLLHPVDATSYGKQTNVLMHFPLSLESQTPSSDPLSCQFLHPKSLPGFSH\n" +
            "MAPLPKFLVSLALRNALEEAGCQADVWALQLQLYRQGGVNATQVLIQHLRGLQKGRSTER\n" +
            "NVSVEALASALQLLAREQQSTGRVGRSLPTEDCENEKEQAVHNVVQLLPGVGTFYNLGTA\n" +
            "LYYATQNCLGKARERGRDGAIDLGYDLLMTMAGMSGGPMGLAISAALKPALRSGVQQLIQ\n" +
            "YYQDQKDANISQPETTKEGLRAISDVSDLEETTTLASFISEVVSSAPYWGWAIIKSYDLD\n" +
            "PGAGSLEI*";

        [TestMethod]
        public void TestNeutralLossLibrary()
        {
            TestFilesZip = @"TestFunctional\NeutralLossLibraryTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Create the modification and library spec used in this test
            var phosphLossMod = new StaticMod("Phospho Loss", "S, T", null, true, "HPO3",
                                              LabelAtoms.None, RelativeRT.Matching, null, null,  new[] { new FragmentLoss("H3PO4"), });
            var librarySpec = new BiblioSpecLiteSpec("Phospho Library",
                                                     TestFilesDir.GetTestPath("phospho_30882_v2.blib"));

            // Prepare settings for this test
            Settings.Default.StaticModList.Clear();
            Settings.Default.StaticModList.AddRange(StaticModList.GetDefaultsOn());
            Settings.Default.StaticModList.Add(phosphLossMod);
            Settings.Default.SpectralLibraryList.Clear();
            Settings.Default.SpectralLibraryList.Add(librarySpec);

            // Prepare document settings for this test
            var settings = SrmSettingsList.GetDefault()
                .ChangePeptideModifications(mods => mods.ChangeStaticModifications(new List<StaticMod>(mods.StaticModifications) { phosphLossMod }))
                .ChangePeptideLibraries(lib => lib.ChangeLibrarySpecs(new[] { librarySpec }))
                .ChangeTransitionLibraries(tlib => tlib.ChangeIonCount(6));

            RunUI(() => SkylineWindow.ModifyDocument("Set test settings",
                                                     doc => doc.ChangeSettings(settings)));

            WaitForCondition(() => SkylineWindow.Document.Settings.IsLoaded);

            // Add FASTA sequence
            RunUI(() => SkylineWindow.Paste(TEXT_FASTA_SPROT));

            var docLibLoss = SkylineWindow.Document;
            AssertEx.IsDocumentState(docLibLoss, null, 3, 4, 24);
            Assert.AreEqual(7, GetLossCount(docLibLoss, 1));

            string lossLabel = "-" + Math.Round(phosphLossMod.Losses[0].MonoisotopicMass, 1);
            int checkedCount = 0;
            for (int i = 0; i < docLibLoss.TransitionGroupCount; i++)
            {
                var pathTranGroup = docLibLoss.GetPathTo((int) SrmDocument.Level.TransitionGroups, i);
                var nodeGroup = (TransitionGroupDocNode) docLibLoss.FindNode(pathTranGroup);
                if (!nodeGroup.Children.Contains(child => ((TransitionDocNode) child).HasLoss))
                    continue;

                // Select the transition groups that contain loss ions
                SelectNode(SrmDocument.Level.TransitionGroups, i);
                WaitForGraphs();

                // Make sure the spectrum graph contains some -98 ions
                RunUI(() => Assert.IsTrue(SkylineWindow.GraphSpectrum.IonLabels.Contains(label => label.Contains(lossLabel)),
                    string.Format("Missing loss labels in spectrum graph for {0}", nodeGroup.TransitionGroup.Peptide.Sequence)));

                // Make sure the transition tree nodes contain -98 ions
                RunUI(() => Assert.IsTrue(GetChildLabels(SkylineWindow.SelectedNode).Contains(label => label.Contains(lossLabel)),
                    string.Format("Missing loss labels in transition tree nodes for {0}", nodeGroup.TransitionGroup.Peptide.Sequence)));

                checkedCount++;
            }
        }

        private static string[] GetChildLabels(TreeNode nodeTree)
        {
            nodeTree.Expand();
            var listLabels = new List<string>();
            foreach (TreeNode nodeChild in nodeTree.Nodes)
                listLabels.Add(nodeChild.Text);
            return listLabels.ToArray();
        }

        private static int GetLossCount(SrmDocument document, int minLosses)
        {
            int count = 0;
            foreach (var nodeTran in document.Transitions)
            {
                if (nodeTran.HasLoss && nodeTran.Losses.Losses.Count >= minLosses)
                    count++;
            }
            return count;
        }
    }
}