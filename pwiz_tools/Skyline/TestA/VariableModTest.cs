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
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA
{
    /// <summary>
    /// Summary description for VariableModTest
    /// </summary>
    [TestClass]
    public class VariableModTest
    {
        private static readonly StaticMod VAR_MET_OXIDIZED = new StaticMod("Methionine Oxidized", "M", null, true, "O",
            LabelAtoms.None, RelativeRT.Matching, null, null, null);
        private static readonly StaticMod VAR_MET_AMONIA_ADD = new StaticMod("Methionine Amonia Added", "M", null, true, "NH3",
            LabelAtoms.None, RelativeRT.Matching, null, null, null);
        private static readonly StaticMod VAR_ASP_WATER_ADD = new StaticMod("Aspartic Acid Water Added", "D", null, true, "H2O",
            LabelAtoms.None, RelativeRT.Matching, null, null, null);
        private static readonly StaticMod VAR_ASP_WATER_LOSS = new StaticMod("Aspartic Acid Water Loss", "D", null, true, "-H2O",
            LabelAtoms.None, RelativeRT.Matching, null, null, null);
        private static readonly StaticMod VAR_MET_ASP_OXIDIZED = new StaticMod("Oxidized", "D, M", null, true, "O",
            LabelAtoms.None, RelativeRT.Matching, null, null, null);

        private static readonly List<StaticMod> HEAVY_MODS = new List<StaticMod>
            {
                new StaticMod("13C K", "K", ModTerminus.C, null, LabelAtoms.C13, null, null),
                new StaticMod("13C R", "R", ModTerminus.C, null, LabelAtoms.C13, null, null),
            };

        private static readonly List<StaticMod> HEAVY_MODS_MULTI = new List<StaticMod>
            {
                new StaticMod("Aqua", "K, R", ModTerminus.C, null, LabelAtoms.C13, null, null),
            };

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext { get; set; }

        #region Additional test attributes

        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //

        #endregion

        [TestMethod]
        public void VariableModBasicTest()
        {
            SrmDocument document = new SrmDocument(SrmSettingsList.GetDefault0_6());

            // Make sure default document produces no variable modifications
            IdentityPath path = IdentityPath.ROOT;
            var docYeast = document.ImportFasta(new StringReader(ExampleText.TEXT_FASTA_YEAST), false, path, out path);
            Assert.AreEqual(0, GetVariableModCount(docYeast));

            // Add a single variable modification
            var settings = document.Settings;
            var modsDefault = settings.PeptideSettings.Modifications;
            var listStaticMods = new List<StaticMod>(modsDefault.StaticModifications) {VAR_MET_OXIDIZED};
            var docMetOxidized = document.ChangeSettings(settings.ChangePeptideModifications(mods =>
                mods.ChangeStaticModifications(listStaticMods.ToArray())));

            // Make sure variable modifications are added as expected to imported FASTA
            path = IdentityPath.ROOT;
            var docMoYeast = docMetOxidized.ImportFasta(new StringReader(ExampleText.TEXT_FASTA_YEAST), false, path, out path);
            Assert.AreEqual(21, GetVariableModCount(docMoYeast));
            AssertEx.IsDocumentState(docMoYeast, 2, 2, 119, 374);

            // Exclude unmodified peptides
            var docNoMoExclude = docMoYeast.ChangeSettings(docMoYeast.Settings.ChangePeptideFilter(f =>
                f.ChangeExclusions(new[] { new PeptideExcludeRegex("Non-Oxidized Meth", "M\\[", true, true) })));
            Assert.AreEqual(GetVariableModCount(docMoYeast), GetVariableModCount(docNoMoExclude));
            Assert.AreEqual(docNoMoExclude.PeptideCount, GetVariableModCount(docNoMoExclude));
            AssertEx.IsDocumentState(docNoMoExclude, 3, 2, 21, 63);

            // Exclude multiply modified peptides
            var docMultMoExclude = docNoMoExclude.ChangeSettings(docNoMoExclude.Settings.ChangePeptideFilter(f =>
                f.ChangeExclusions(new List<PeptideExcludeRegex>(f.Exclusions) { new PeptideExcludeRegex("Multi-Oxidized Meth", "M\\[.*M\\[", false, true) })));
            Assert.AreEqual(docMultMoExclude.PeptideCount, GetVariableModCount(docMultMoExclude));
            AssertEx.IsDocumentState(docMultMoExclude, 4, 2, 18, 56);

            // And that removing the variable modification removes the variably modifide peptides
            var docYeast2 = docMoYeast.ChangeSettings(docMoYeast.Settings.ChangePeptideModifications(mods => modsDefault));
            Assert.AreEqual(0, GetVariableModCount(docYeast2));
            Assert.AreEqual(docYeast, docYeast2);
            Assert.AreNotSame(docYeast, docYeast2);

            // Even when automanage children is turned off
            var docNoAuto = (SrmDocument)docMoYeast.ChangeChildren((from node in docYeast2.PeptideGroups
                                                                   select node.ChangeAutoManageChildren(false)).ToArray());
            var docYeastNoAuto = docNoAuto.ChangeSettings(docYeast.Settings);
            Assert.AreEqual(0, GetVariableModCount(docYeastNoAuto));
            // Shouldn't come back, if the mods are restored
            var docMoNoAuto = docYeastNoAuto.ChangeSettings(docMetOxidized.Settings);
            Assert.AreEqual(0, GetVariableModCount(docMoNoAuto));
            Assert.AreSame(docYeastNoAuto.Children, docMoNoAuto.Children);

            // Make sure loss modification result in smaller m/z values
            var listModsLoss = new List<StaticMod>(modsDefault.StaticModifications) { VAR_ASP_WATER_LOSS };
            var docAspLoss = docYeast2.ChangeSettings(docYeast2.Settings.ChangePeptideModifications(mods =>
                mods.ChangeStaticModifications(listModsLoss.ToArray())));
            Assert.AreEqual(145, GetVariableModCount(docAspLoss));
            VerifyModificationOrder(docAspLoss, false);

            // Add multiple variable modifications
            listStaticMods.Add(VAR_ASP_WATER_ADD);
            var docVarMulti = docYeast2.ChangeSettings(docYeast2.Settings.ChangePeptideModifications(mods =>
                mods.ChangeStaticModifications(listStaticMods.ToArray())));
            Assert.AreEqual(220, GetVariableModCount(docVarMulti));
            int maxModifiableMulti = GetMaxModifiableCount(docVarMulti);
            Assert.IsTrue(maxModifiableMulti > GetMaxModifiedCount(docVarMulti));
            VerifyModificationOrder(docVarMulti, true);

            // Repeat with a single variable modification on multiple amino acids
            // and verify that this creates the same number of variably modified peptides
            var listModsMultiAA = new List<StaticMod>(modsDefault.StaticModifications) { VAR_MET_ASP_OXIDIZED };
            var docVarMultiAA = docYeast2.ChangeSettings(docYeast2.Settings.ChangePeptideModifications(mods =>
                mods.ChangeStaticModifications(listModsMultiAA.ToArray())));
            Assert.AreEqual(220, GetVariableModCount(docVarMultiAA));
            VerifyModificationOrder(docVarMultiAA, true);

            // And also multiple modifications on the same amino acid residue
            listStaticMods.Add(VAR_MET_AMONIA_ADD);
            var docVarAaMulti = docVarMulti.ChangeSettings(docVarMulti.Settings.ChangePeptideModifications(mods =>
                mods.ChangeStaticModifications(listStaticMods.ToArray())));
            Assert.AreEqual(315, GetVariableModCount(docVarAaMulti));
            int maxModifiableAaMulti = GetMaxModifiableCount(docVarAaMulti);
            Assert.AreEqual(maxModifiableMulti, maxModifiableAaMulti,
                "Unexptected change in the maximum number of modifiable amino acids");
            Assert.IsTrue(maxModifiableAaMulti > GetMaxModifiedCount(docVarAaMulti));
            VerifyModificationOrder(docVarAaMulti, true);

            // Reduce the maximum number of variable modifications allowed
            var docVar2AaMulti = docVarAaMulti.ChangeSettings(docVarAaMulti.Settings.ChangePeptideModifications(mods =>
                mods.ChangeMaxVariableMods(2)));
            Assert.AreEqual(242, GetVariableModCount(docVar2AaMulti));
            Assert.AreEqual(2, GetMaxModifiedCount(docVar2AaMulti));
            VerifyModificationOrder(docVar2AaMulti, true);

            var docVar1AaMulti = docVar2AaMulti.ChangeSettings(docVar2AaMulti.Settings.ChangePeptideModifications(mods =>
                mods.ChangeMaxVariableMods(1)));
            Assert.AreEqual(128, GetVariableModCount(docVar1AaMulti));
            Assert.AreEqual(1, GetMaxModifiedCount(docVar1AaMulti));
            VerifyModificationOrder(docVar1AaMulti, true);

            var docVarAaMultiReset = docVar1AaMulti.ChangeSettings(docVarAaMulti.Settings);
            Assert.AreEqual(315, GetVariableModCount(docVarAaMultiReset));

            // Repeat with auto-manage turned off to make sure it also removes
            // variable modifications which are made invalide by changing the limit
            var docMultiNoAuto = (SrmDocument)docVarAaMulti.ChangeChildren((from node in docVarAaMulti.PeptideGroups
                                                                            select node.ChangeAutoManageChildren(false)).ToArray());
            var docMulti2NoAuto = docMultiNoAuto.ChangeSettings(docVar2AaMulti.Settings);
            Assert.IsTrue(ArrayUtil.ReferencesEqual(docVar2AaMulti.Peptides.ToArray(),
                                                    docMulti2NoAuto.Peptides.ToArray()));
            var docMulti1NoAuto = docMulti2NoAuto.ChangeSettings(docVar1AaMulti.Settings);
            Assert.IsTrue(ArrayUtil.ReferencesEqual(docVar1AaMulti.Peptides.ToArray(),
                                                    docMulti1NoAuto.Peptides.ToArray()));
            var docMultiNoAutoReset = docMulti1NoAuto.ChangeSettings(docVarAaMulti.Settings);
            Assert.AreSame(docMulti1NoAuto.Children, docMultiNoAutoReset.Children);

            // Add heavy modifications to an earlier document to verify
            // that heavy precursors all get greater precursor m/z values than
            // their light versions
            var docVarHeavy = docVarMulti.ChangeSettings(docVarMulti.Settings.ChangePeptideModifications(
                mods => mods.ChangeHeavyModifications(HEAVY_MODS)));
            foreach (var nodePep in docVarHeavy.Peptides)
            {
                if (nodePep.Peptide.NextAA == '-')
                    continue;

                Assert.AreEqual(2, nodePep.Children.Count);
                Assert.AreEqual(GetPrecursorMz(nodePep, 0), GetPrecursorMz(nodePep, 1)-3, 0.02);
            }

            // Repeat with a modification specifying multiple amino acids
            // and make sure the resulting m/z values are the same
            var docVarHeavyMulti = docVarMulti.ChangeSettings(docVarMulti.Settings.ChangePeptideModifications(
                mods => mods.ChangeHeavyModifications(HEAVY_MODS_MULTI)));

            var varHeavyGroups = docVarHeavy.TransitionGroups.ToArray();
            var varHeavyMultiPeptides = docVarHeavyMulti.TransitionGroups.ToArray();
            Assert.AreEqual(varHeavyGroups.Length, varHeavyMultiPeptides.Length);
            for (int i = 0; i < varHeavyGroups.Length; i++)
                Assert.AreEqual(varHeavyGroups[i].PrecursorMz, varHeavyMultiPeptides[i].PrecursorMz);
        }

        /// <summary>
        /// Make sure variable modifications round-trip through serialization correctly
        /// </summary>
        [TestMethod]
        public void VariableModSerializeTest()
        {
            SrmDocument document = new SrmDocument(SrmSettingsList.GetDefault());

            IdentityPath path = IdentityPath.ROOT;
            var settings = document.Settings;
            var modsDefault = settings.PeptideSettings.Modifications;
            var listStaticMods = new List<StaticMod>(modsDefault.StaticModifications)
                                     {
                                         VAR_MET_OXIDIZED,
                                         VAR_MET_AMONIA_ADD,
                                         VAR_ASP_WATER_ADD
                                     };
            var docVarMods = document.ChangeSettings(settings.ChangePeptideModifications(mods =>
                mods.ChangeStaticModifications(listStaticMods.ToArray())));
            var docVmYeast = docVarMods.ImportFasta(new StringReader(ExampleText.TEXT_FASTA_YEAST), false, path, out path);
            Assert.AreEqual(315, GetVariableModCount(docVmYeast));

            AssertEx.Serializable(docVmYeast, 3, AssertEx.DocumentCloned);
        }

        /// <summary>
        /// Make sure variably modified peptides correctly match library spectra
        /// </summary>
        [TestMethod]
        public void VariableModLibraryTest()
        {
            LibraryManager libraryManager;
            TestDocumentContainer docContainer;
            int startRev;
            SrmDocument document = LibrarySettingsTest.CreateNISTLibraryDocument(
                TEXT_FASTA_YEAST_39,
                false,
                TEXT_LIB_YEAST_NIST,
                out libraryManager,
                out docContainer,
                out startRev);
            AssertEx.IsDocumentState(document, startRev, 1, 1, 3);

            var settings = document.Settings;
            var modsDefault = settings.PeptideSettings.Modifications;
            var listStaticMods = new List<StaticMod>(modsDefault.StaticModifications)
                                     {
                                         VAR_MET_OXIDIZED,
                                     };
            var docVarModsLib = document.ChangeSettings(settings.ChangePeptideModifications(mods =>
                mods.ChangeStaticModifications(listStaticMods.ToArray())));
            startRev++;
            Assert.AreEqual(1, GetVariableModCount(docVarModsLib));
            AssertEx.IsDocumentState(docVarModsLib, startRev, 1, 2, 6);
        }

        /// <summary>
        /// Make sure peptide lists behave as expected with variable modifications
        /// </summary>
        [TestMethod]
        public void VariableModPeptideListTest()
        {
            // Create a document with a peptide lists of 3 peptides
            LibraryManager libraryManager;
            TestDocumentContainer docContainer;
            int startRev;
            SrmDocument document = LibrarySettingsTest.CreateNISTLibraryDocument(
                TEXT_PEPTIDES_YEAST,
                true,
                TEXT_LIB_YEAST_NIST,
                out libraryManager,
                out docContainer,
                out startRev);
            AssertEx.IsDocumentState(document, startRev, 1, 3, 1, 3);

            // Add variable modifications
            var settings = document.Settings;
            var modsDefault = settings.PeptideSettings.Modifications;
            var listStaticMods = new List<StaticMod>(modsDefault.StaticModifications)
                                     {
                                         VAR_MET_OXIDIZED,
                                     };
            var docPeptideList = document.ChangeSettings(settings.ChangePeptideModifications(mods =>
                mods.ChangeStaticModifications(listStaticMods.ToArray())));
            startRev++;
            // One new peptide should match a libray spectrum with the variable mods
            Assert.AreEqual(1, GetVariableModCount(docPeptideList));
            AssertEx.IsDocumentState(docPeptideList, startRev, 1, 3, 2, 6);

            // Remove libraries which should yield the full set of variable modifications
            var docNoLib = docPeptideList.ChangeSettings(docPeptideList.Settings.ChangePeptideLibraries(lib =>
                lib.ChangeLibrarySpecs(new LibrarySpec[0])));
            int noLibRev = ++startRev;
            Assert.AreEqual(16, GetVariableModCount(docNoLib));
            AssertEx.IsDocumentState(docNoLib, startRev, 1, 19, 63);

            // Remove the variable modifications and make sure that reduces the peptides to only unmodified
            var docNoMods = docNoLib.ChangeSettings(docNoLib.Settings.ChangePeptideModifications(mods =>
                mods.ChangeStaticModifications(modsDefault.StaticModifications)));
            startRev++;
            Assert.AreEqual(0, GetVariableModCount(docNoMods));
            AssertEx.IsDocumentState(docNoMods, startRev, 1, 3, 12);

            // Repeat the removal with auto-manage children off
            startRev = noLibRev;
            var docNoAutoManage = (SrmDocument) docNoLib.ReplaceChild(
                ((DocNodeParent)docNoLib.Children[0]).ChangeAutoManageChildren(false));
            startRev++;

            var docNoModsNoManage = docNoAutoManage.ChangeSettings(docNoAutoManage.Settings.ChangePeptideModifications(mods =>
                mods.ChangeStaticModifications(modsDefault.StaticModifications)));
            startRev++;
            AssertEx.IsDocumentState(docNoModsNoManage, startRev, 1, 3, 12);
        }

        /// <summary>
        /// Make sure transition lists with variable modifications import as expected
        /// </summary>
        [TestMethod]
        public void VariableModImportListTest()
        {
            // TODO: Deal with multiple variable modifications that produce the same
            //       precursor mass but different product ion masses
            SrmDocument document = new SrmDocument(SrmSettingsList.GetDefault());
            var staticMods = new List<StaticMod>(document.Settings.PeptideSettings.Modifications.StaticModifications);
            staticMods.AddRange(new[]
                                    {
                                        new StaticMod("Met Sulfoxide", "M", null, true, "O", LabelAtoms.None,
                                            RelativeRT.Matching, null, null, new[] { new FragmentLoss("SOCH4") }),
                                        new StaticMod("Met Sulfone", "M", null, true, "O2", LabelAtoms.None,
                                            RelativeRT.Matching, null, null, null), 
                                        new StaticMod("Phospho", "S,T,Y", null, true, "PO3H", LabelAtoms.None,
                                            RelativeRT.Matching, null, null, new[] {new FragmentLoss("PO4H3")}),
                                        new StaticMod("K(GlyGly)", "K", null, true, "N2H6C4O2", LabelAtoms.None,
                                            RelativeRT.Matching, null, null, null), 
                                    });
            document = document.ChangeSettings(document.Settings.ChangePeptideModifications(mods =>
                mods.ChangeStaticModifications(staticMods)));
            IdentityPath pathTo;
            AssertEx.ThrowsException<InvalidDataException>(() => document.ImportMassList(new StringReader(TEXT_PHOSPHO_TRANSITION_LIST),
                CultureInfo.InvariantCulture, ',', null, null, out pathTo));

            var docHighMax = document.ChangeSettings(document.Settings.ChangeTransitionInstrument(inst => inst.ChangeMaxMz(1800)));
            var docList = docHighMax.ImportMassList(new StringReader(TEXT_PHOSPHO_TRANSITION_LIST),
                CultureInfo.InvariantCulture, ',', null, null, out pathTo);

            AssertEx.IsDocumentState(docList, 3, 30, 50, 56, 166);

            AssertEx.ThrowsException<InvalidDataException>(() => docHighMax.ImportMassList(new StringReader(TEXT_PHOSPHO_MULTI_LOSS),
                CultureInfo.InvariantCulture, ',', null, null, out pathTo));
            
            var docMultiLos = docHighMax.ChangeSettings(docHighMax.Settings.ChangePeptideModifications(mods =>
                mods.ChangeMaxNeutralLosses(2)));
            docList = docMultiLos.ImportMassList(new StringReader(TEXT_PHOSPHO_MULTI_LOSS),
                CultureInfo.InvariantCulture, ',', null, null, out pathTo);

            AssertEx.IsDocumentState(docList, 4, 2, 2, 6);
        }

        private static int GetVariableModCount(SrmDocument document)
        {
            int count = 0;
            foreach (var nodePep in document.Peptides)
            {
                if (nodePep.ExplicitMods != null && nodePep.ExplicitMods.IsVariableStaticMods)
                    count++;
            }
            return count;
        }

        private static int GetMaxModifiableCount(SrmDocument document)
        {
            int max = 0;
            var modsVariable = document.Settings.PeptideSettings.Modifications.VariableModifications.ToArray();
            foreach (var nodePep in document.Peptides)
            {
                int modifiable = 0;
                string sequence = nodePep.Peptide.Sequence;
                int len = sequence.Length;
                for (int i = 0; i < len; i++)
                {
                    char aa = sequence[i];
                    foreach (var mod in modsVariable)
                    {
                        if (mod.IsMod(aa, i, len))
                        {
                            modifiable++;
                            break;
                        }
                    }
                }
                max = Math.Max(max, modifiable);
            }
            return max;
        }

        private static int GetMaxModifiedCount(SrmDocument document)
        {
            int max = 0;
            foreach (var nodePep in document.Peptides)
            {
                var mods = nodePep.ExplicitMods;
                if (mods != null && mods.IsVariableStaticMods)
                    max = Math.Max(max, mods.StaticModifications.Count);
            }
            return max;
        }

        private static void VerifyModificationOrder(SrmDocument document, bool addedMass)
        {
            int lastModCount = 0;
            double lastModIndexTotal = 0;
            double lastUnmodMz = double.MaxValue;
            foreach (var nodePep in document.Peptides)
            {
                if (nodePep.ExplicitMods == null)
                {
                    lastModCount = 0;
                    lastModIndexTotal = 0;
                    lastUnmodMz = ((TransitionGroupDocNode) nodePep.Children[0]).PrecursorMz;
                }
                else
                {
                    var mods = nodePep.ExplicitMods.StaticModifications;
                    Assert.IsTrue(lastModCount <= mods.Count);
                    if (lastModCount != mods.Count)
                    {
                        lastModCount = mods.Count;
                        lastModIndexTotal = 0;
                    }
                    else
                    {
                        double modIndexTotal = 0;
                        foreach (var mod in nodePep.ExplicitMods.StaticModifications)
                            modIndexTotal += Math.Pow(10, mod.IndexAA);
                        Assert.IsTrue(lastModIndexTotal <= modIndexTotal);
                    }
                    if (addedMass)
                        Assert.IsTrue(lastUnmodMz < GetPrecursorMz(nodePep, 0));
                    else
                        Assert.IsTrue(lastUnmodMz > GetPrecursorMz(nodePep, 0));
                }
            }
        }

        private static double GetPrecursorMz(PeptideDocNode nodePep, int iChild)
        {
            return ((TransitionGroupDocNode)nodePep.Children[iChild]).PrecursorMz;
        }

        private const string TEXT_FASTA_YEAST_39 =
            ">YAL039C CYC3 SGDID:S000000037, Chr I from 69526-68717, reverse complement, Verified ORF, \"Cytochrome c heme lyase (holocytochrome c synthase), attaches heme to apo-cytochrome c (Cyc1p or Cyc7p) in the mitochondrial intermembrane space; human ortholog may have a role in microphthalmia with linear skin defects (MLS)\"\n" +
            "MGWFWADQKTTGKDIGGAAVSSMSGCPVMHESSSSSPPSSECPVMQGDNDRINPLNNMPE\n" +
            "LAASKQPGQKMDLPVDRTISSIPKSPDSNEFWEYPSPQQMYNAMVRKGKIGGSGEVAEDA\n" +
            "VESMVQVHNFLNEGCWQEVLEWEKPHTDESHVQPKLLKFMGKPGVLSPRARWMHLCGLLF\n" +
            "PSHFSQELPFDRHDWIVLRGERKAEQQPPTFKEVRYVLDFYGGPDDENGMPTFHVDVRPA\n" +
            "LDSLDNAKDRMTRFLDRMISGPSSSSSAP*\n";

        private const string TEXT_PEPTIDES_YEAST =
            ">Peptides1\n" +
            "INPLNNMPELAASK\n" +
            "FMGKPGVLSPR\n" +
            "MOMMOMIER";

        private const string TEXT_LIB_YEAST_NIST =
            "Name: INPLNNMPELAASK/2\n" +
            "MW: 1512.797\n" +
            "Comment: Spec=Consensus Pep=Tryptic Fullname=R.INPLNNMPELAASK.Q/2 Mods=0 Parent=756.398 Inst=it Mz_diff=0.012 Mz_exact=756.3985 Mz_av=756.886 Protein=\"gi|6319278|ref|NP_009361.1| Cyc3p [Saccharomyces cerevisiae]; gi|115944|sp|P06182|CCHL_YEAST Cytochrome c heme lyase (CCHL) (Holocytochrome-C synthase); gi|3616|emb|CAA28470.1| unnamed protein product [Saccharomyces cerevisiae]; gi|595545|gb|AAC04992.1| Cyc3p: cytochrome C heme lyase [Saccharomyces cerevisiae]; gi|82964|pir||A26162 holocytochrome-c synthase (EC 4.4.1.17) CYC3 - yeast  (Saccharomyces cerevisiae)\" Pseq=2 Organism=\"yeast\" Se=3^X2:ex=0.245/0.135,td=83.8/52.2,sd=0/0,hs=20.1/0.5,bs=0.11,b2=0.38,bd=136^O2:ex=6.403e-005/5.697e-005,td=3.7515e+006/3.339e+006,pr=2.6715e-008/2.318e-008,bs=7.06e-006,b2=0.000121,bd=7.09e+006^P1:sc=18.5/0,dc=12.2/0,ps=2.69/0,bs=0 Sample=1/silac_labelled,2,1 Nreps=2/2 Missing=0.3317/0.0737 Parent_med=756.41/0.00 Max2med_orig=237.1/97.1 Dotfull=0.622/0.000 Dot_cons=0.803/0.046 Unassign_all=0.212 Unassigned=0.112 Dotbest=0.76 Flags=0,0,0 Naa=14 DUScorr=10/0.44/1.4 Dottheory=0.95 Pfin=1.9e+009 Probcorr=1 Tfratio=1.9e+006 Pfract=0\n" +
            "Num peaks: 105\n" +
            "305.3	145	\"y3/0.12 2/1 0.7\"\n" +
            "325.1	192	\"b3/-0.08,b6-17^2/-0.07 2/2 0.8\"\n" +
            "438.3	127	\"b4/0.04 2/1 0.8\"\n" +
            "479.3	126	\"? 2/1 1.3\"\n" +
            "486.2	302	\"? 2/2 5.7\"\n" +
            "489.5	183	\"y5/0.20,b9-45^2/-0.24 2/2 2.3\"\n" +
            "496.3	116	\"? 2/1 1.0\"\n" +
            "535.2	116	\"b5-17/-0.11 2/1 1.1\"\n" +
            "549.3	118	\"? 2/1 1.0\"\n" +
            "552.2	150	\"b5/-0.11 2/1 1.1\"\n" +
            "553.3	129	\"b5i/0.99 2/1 0.7\"\n" +
            "559.4	309	\"b10-18^2/-0.38 2/2 5.2\"\n" +
            "568.4	144	\"b10^2/-0.38 2/1 1.0\"\n" +
            "570.3	218	\"? 2/2 3.0\"\n" +
            "571.5	206	\"?i 2/2 3.1\"\n" +
            "581.3	297	\"b11-46^2/-0.00 2/2 4.8\"\n" +
            "583.2	153	\"? 2/2 1.8\"\n" +
            "599.2	484	\"? 2/2 7.7\"\n" +
            "612.7	132	\"? 2/1 1.5\"\n" +
            "615.3	248	\"? 2/2 3.2\"\n" +
            "619.2	140	\"? 2/1 1.6\"\n" +
            "621.7	146	\"b6-45/0.35 2/1 1.5\"\n" +
            "627.2	134	\"? 2/1 0.7\"\n" +
            "632.4	171	\"? 2/2 1.8\"\n" +
            "633.7	234	\"y12-18^2/-0.14 2/2 1.3\"\n" +
            "634.5	650	\"y12-17^2/0.16 2/2 3.6\"\n" +
            "635.6	257	\"y12-17i^2/1.26 2/2 1.6\"\n" +
            "636.4	179	\"y12-17i^2/2.06 2/2 1.4\"\n" +
            "639.9	169	\"b12^2/0.08 2/1 2.4\"\n" +
            "641.2	269	\"b12i^2/1.38 2/2 0.4\"\n" +
            "642.8	10000	\"y12^2/-0.04 2/2 0.0\"\n" +
            "644.1	1189	\"y12i^2/1.26 2/2 7.5\"\n" +
            "645.3	196	\"y12i^2/2.46 2/2 2.8\"\n" +
            "648.0	194	\"b6-18/-0.35 2/1 3.1\"\n" +
            "649.0	350	\"b6-17/-0.35 2/2 0.6\"\n" +
            "650.6	235	\"? 2/2 2.8\"\n" +
            "656.5	131	\"? 2/1 1.6\"\n" +
            "661.1	120	\"b13-44^2/-0.24,b13-45^2/0.26 2/1 0.7\"\n" +
            "662.8	294	\"? 2/1 5.2\"\n" +
            "664.6	220	\"? 2/1 3.7\"\n" +
            "666.1	292	\"b6/-0.25 2/2 3.8\"\n" +
            "667.0	217	\"b6i/0.65 2/2 3.2\"\n" +
            "668.2	195	\"b6i/1.85 2/1 1.8\"\n" +
            "671.1	213	\"y7-44/-0.30 2/2 1.0\"\n" +
            "676.5	264	\"? 2/1 4.2\"\n" +
            "680.8	221	\"? 2/1 2.1\"\n" +
            "681.7	277	\"?i 2/1 3.5\"\n" +
            "683.3	151	\"b13^2/-0.04 2/1 0.3\"\n" +
            "685.6	142	\"? 2/1 0.9\"\n" +
            "686.4	122	\"?i 2/1 0.8\"\n" +
            "687.5	184	\"?i 2/1 2.8\"\n" +
            "688.7	125	\"?i 2/1 1.1\"\n" +
            "690.0	174	\"?i 2/1 2.6\"\n" +
            "692.2	115	\"? 2/1 1.1\"\n" +
            "695.2	124	\"? 2/1 1.1\"\n" +
            "697.5	175	\"y7-18/0.10 2/2 1.2\"\n" +
            "698.7	175	\"y7-17/0.30 2/2 1.8\"\n" +
            "699.9	369	\"y13^2/0.04 2/2 2.3\"\n" +
            "700.9	324	\"y13i^2/1.04 2/2 1.7\"\n" +
            "704.7	110	\"? 2/1 1.1\"\n" +
            "711.5	211	\"? 2/2 0.6\"\n" +
            "712.4	160	\"?i 2/2 1.3\"\n" +
            "713.4	219	\"?i 2/2 2.0\"\n" +
            "714.7	428	\"?i 2/2 3.2\"\n" +
            "715.4	1267	\"y7/0.00 2/2 12.4\"\n" +
            "716.5	764	\"y7i/1.10 2/2 4.8\"\n" +
            "718.3	280	\"? 2/2 2.0\"\n" +
            "719.8	217	\"? 2/1 1.6\"\n" +
            "722.0	238	\"? 2/2 2.0\"\n" +
            "723.2	320	\"?i 2/2 3.1\"\n" +
            "724.3	321	\"?i 2/2 3.4\"\n" +
            "725.4	342	\"?i 2/2 2.5\"\n" +
            "726.1	117	\"?i 2/1 1.1\"\n" +
            "727.4	346	\"?i 2/1 5.4\"\n" +
            "767.2	168	\"? 2/1 2.3\"\n" +
            "780.3	399	\"b7-17/-0.09 2/2 0.8\"\n" +
            "781.1	276	\"b7-17i/0.71 2/2 0.2\"\n" +
            "781.9	278	\"b7-17i/1.51 2/1 3.3\"\n" +
            "783.4	152	\"? 2/2 1.6\"\n" +
            "797.4	240	\"b7/0.01 2/2 1.2\"\n" +
            "801.3	187	\"? 2/2 2.5\"\n" +
            "805.5	250	\"? 2/1 4.0\"\n" +
            "846.2	445	\"y8/-0.24 2/2 1.3\"\n" +
            "847.5	247	\"y8i/1.06 2/2 0.0\"\n" +
            "891.8	153	\"? 2/1 1.2\"\n" +
            "894.2	123	\"b8/-0.24 2/1 1.1\"\n" +
            "910.4	304	\"? 2/2 5.5\"\n" +
            "911.3	146	\"?i 2/1 1.2\"\n" +
            "923.7	176	\"? 2/2 1.1\"\n" +
            "926.6	168	\"? 2/1 1.5\"\n" +
            "951.3	315	\"? 2/2 4.7\"\n" +
            "953.9	249	\"? 2/2 3.4\"\n" +
            "955.5	110	\"? 2/1 1.0\"\n" +
            "960.3	488	\"y9/-0.18 2/2 5.2\"\n" +
            "961.2	205	\"y9i/0.72 2/2 1.3\"\n" +
            "962.9	148	\"? 2/1 0.4\"\n" +
            "1004.3	185	\"? 2/1 2.6\"\n" +
            "1005.5	192	\"b9-18/0.01 2/2 2.9\"\n" +
            "1023.2	246	\"b9/-0.29 2/1 3.9\"\n" +
            "1024.4	197	\"b9i/0.91 2/2 2.7\"\n" +
            "1074.3	683	\"y10/-0.23 2/2 3.3\"\n" +
            "1075.4	371	\"y10i/0.87 2/2 1.2\"\n" +
            "1207.4	178	\"b11/-0.21 2/2 2.0\"\n" +
            "1284.5	440	\"y12/-0.16 2/2 1.1\"\n" +
            "1285.5	231	\"y12i/0.84 2/2 1.1\"\n" +
            "\n" +
            "Name: FM(O)GKPGVLSPR/2\n" +
            "MW: 1205.659\n" +
            "Comment: Spec=Consensus Pep=Tryptic Fullname=K.FM(O)GKPGVLSPR.A/2 Mods=1/1,M,Oxidation Parent=602.830 Inst=it Mz_diff=0.520 Mz_exact=602.8295 Mz_av=603.234 Protein=\"gi|6319278|ref|NP_009361.1| Cyc3p [Saccharomyces cerevisiae]; gi|115944|sp|P06182|CCHL_YEAST Cytochrome c heme lyase (CCHL) (Holocytochrome-C synthase); gi|3616|emb|CAA28470.1| unnamed protein product [Saccharomyces cerevisiae]; gi|595545|gb|AAC04992.1| Cyc3p: cytochrome C heme lyase [Saccharomyces cerevisiae]; gi|82964|pir||A26162 holocytochrome-c synthase (EC 4.4.1.17) CYC3 - yeast  (Saccharomyces cerevisiae)\" Pseq=2 Organism=\"yeast\" Se=3^X2:ex=0.0026075/0.002593,td=29996/2.93e+004,sd=0/0,hs=35.75/5.05,bs=1.5e-005,b2=0.0052,bd=59300^O2:ex=1.1727/1.157,td=1640.75/1619,pr=0.00046954/0.0004665,bs=0.0154,b2=2.33,bd=3260^P1:sc=17.2/0,dc=11.2/0,ps=2.58/0,bs=0 Sample=1/cbs00175_02_01_cam,2,2 Nreps=2/2 Missing=0.1394/0.0130 Parent_med=603.35/0.28 Max2med_orig=142.4/3.8 Dotfull=0.813/0.000 Dot_cons=0.909/0.012 Unassign_all=0.119 Unassigned=0.075 Dotbest=0.92 Flags=0,0,0 Naa=11 DUScorr=0.32/1.1/2.9 Dottheory=0.70 Pfin=1.5e+006 Probcorr=1 Tfratio=1.5e+004 Pfract=0\n" +
            "Num peaks: 103\n" +
            "214.2	1347	\"? 2/2 6.8\"\n" +
            "215.1	162	\"?i 2/2 2.5\"\n" +
            "225.2	39	\"? 2/2 0.0\"\n" +
            "229.4	59	\"? 2/2 0.4\"\n" +
            "231.3	202	\"b2-64/0.20 2/2 1.3\"\n" +
            "253.3	98	\"? 2/2 1.2\"\n" +
            "254.1	58	\"y2-18/-0.07 2/2 0.0\"\n" +
            "255.4	43	\"y2-17/0.23 2/2 0.1\"\n" +
            "261.1	105	\"b2-34/0.00 2/2 0.5\"\n" +
            "269.0	75	\"? 2/2 0.7\"\n" +
            "270.3	209	\"b3-82/0.17 2/2 2.5\"\n" +
            "272.3	109	\"y2/0.13 2/2 0.1\"\n" +
            "278.1	94	\"b2-17/-0.00,y5-17^2/0.42 2/2 0.7\"\n" +
            "284.1	51	\"? 2/2 0.3\"\n" +
            "286.5	82	\"y5^2/0.32 2/2 0.1\"\n" +
            "288.3	134	\"b3-64/0.17 2/2 1.9\"\n" +
            "295.2	743	\"b2/0.10 2/2 2.6\"\n" +
            "296.3	145	\"b2i/1.20 2/2 0.3\"\n" +
            "336.1	51	\"? 2/1 0.4\"\n" +
            "352.2	346	\"b3/0.07 2/2 1.9\"\n" +
            "359.4	497	\"y3/0.20 2/2 2.9\"\n" +
            "381.2	887	\"? 2/2 1.8\"\n" +
            "382.3	245	\"b8-82^2/-0.42 2/2 1.4\"\n" +
            "387.1	463	\"? 2/2 4.2\"\n" +
            "388.2	86	\"?i 2/2 0.1\"\n" +
            "389.2	198	\"?i 2/2 2.0\"\n" +
            "415.2	75	\"b8-17^2/-0.02 2/1 0.9\"\n" +
            "416.3	320	\"b4-64/0.08 2/2 0.8\"\n" +
            "417.4	94	\"b4-64i/1.18 2/2 0.1\"\n" +
            "427.4	86	\"y8^2/0.13 2/2 0.0\"\n" +
            "438.6	43	\"? 2/2 0.1\"\n" +
            "441.5	78	\"? 2/2 0.4\"\n" +
            "446.3	105	\"y9-18^2/-0.48 2/2 0.3\"\n" +
            "456.0	369	\"y9^2/0.22 2/2 2.1\"\n" +
            "457.3	540	\"y9i^2/1.52 2/2 7.4\"\n" +
            "458.5	235	\"b9-17^2/-0.24,b9-18^2/0.26 2/2 0.8\"\n" +
            "462.5	87	\"b4-18/0.28 2/2 1.0\"\n" +
            "469.1	86	\"? 2/2 0.7\"\n" +
            "471.0	31	\"? 2/1 0.0\"\n" +
            "472.6	325	\"y4/0.31 2/2 4.7\"\n" +
            "474.3	3168	\"b10-82*^2/-0.47 2/2 39.9\"\n" +
            "475.4	1576	\"b10-82i^2/0.63 2/2 5.9\"\n" +
            "480.1	432	\"b4/-0.12 2/2 1.1\"\n" +
            "481.3	220	\"b4i/1.08 2/2 3.1\"\n" +
            "482.2	117	\"b4i/1.98 2/2 0.2\"\n" +
            "498.6	125	\"b10-35^2/0.33 2/2 0.2\"\n" +
            "507.7	89	\"b10-17^2/0.43 2/2 0.7\"\n" +
            "516.0	66	\"b10^2/0.23 2/2 0.5\"\n" +
            "519.8	94	\"y10-18^2/-0.50 2/2 0.4\"\n" +
            "529.3	354	\"y10^2/0.00 2/2 3.6\"\n" +
            "530.4	58	\"y10i^2/1.10 2/2 0.5\"\n" +
            "532.5	94	\"? 2/2 0.8\"\n" +
            "536.2	47	\"? 2/2 0.0\"\n" +
            "543.1	58	\"? 2/2 0.4\"\n" +
            "545.5	97	\"? 2/2 0.8\"\n" +
            "548.3	93	\"? 2/2 0.7\"\n" +
            "550.0	132	\"? 2/2 0.7\"\n" +
            "553.3	785	\"y5-18/-0.06 2/2 12.1\"\n" +
            "554.0	710	\"y5-17/-0.36 2/2 0.9\"\n" +
            "562.0	198	\"p-82/0.17 2/2 1.1\"\n" +
            "569.3	141	\"? 2/2 1.5\"\n" +
            "571.1	10000	\"y5/-0.26,p-64/0.27 2/2 0.0\"\n" +
            "571.9	1649	\"y5i/0.54 2/2 2.5\"\n" +
            "572.6	372	\"y5i/1.24 2/2 3.2\"\n" +
            "576.3	156	\"? 2/2 0.1\"\n" +
            "580.9	361	\"p-44/0.07,p-45/0.57 2/2 2.5\"\n" +
            "581.9	234	\"p-44i/1.07 2/2 0.8\"\n" +
            "584.8	723	\"p-36/-0.02 2/2 7.9\"\n" +
            "585.5	1198	\"p-35/0.18 2/2 0.9\"\n" +
            "586.4	454	\"p-35i/1.08 2/2 3.0\"\n" +
            "588.0	453	\"? 2/2 6.6\"\n" +
            "594.0	869	\"p-18/0.18,p-17/-0.33 2/2 2.3\"\n" +
            "595.2	429	\"p-18i/1.38 2/2 0.1\"\n" +
            "628.3	97	\"y6/-0.08 2/2 1.1\"\n" +
            "633.4	164	\"? 2/2 0.2\"\n" +
            "634.5	129	\"b6/0.21 2/2 0.0\"\n" +
            "665.1	47	\"? 2/2 0.2\"\n" +
            "669.3	113	\"b7-64/-0.06 2/2 0.3\"\n" +
            "708.5	50	\"y7-17/0.07 2/2 0.4\"\n" +
            "725.5	6544	\"y7/0.07 2/2 12.7\"\n" +
            "726.5	2490	\"y7i/1.07 2/2 1.7\"\n" +
            "727.6	525	\"y7i/2.17 2/2 3.9\"\n" +
            "728.7	58	\"y7i/3.27 2/2 0.2\"\n" +
            "733.4	113	\"b7/0.04 2/2 0.5\"\n" +
            "746.3	59	\"? 2/2 0.3\"\n" +
            "760.4	82	\"? 2/2 0.3\"\n" +
            "782.3	94	\"b8-64/-0.15 2/2 1.1\"\n" +
            "783.7	105	\"? 2/2 1.0\"\n" +
            "810.5	257	\"? 2/2 0.6\"\n" +
            "846.5	176	\"b8/0.05 2/2 0.5\"\n" +
            "847.6	159	\"b8i/1.15 2/2 1.2\"\n" +
            "848.8	35	\"b8i/2.35 2/2 0.1\"\n" +
            "851.5	51	\"b9-82/0.02 2/2 0.3\"\n" +
            "853.6	249	\"y8/0.07 2/2 0.5\"\n" +
            "869.4	70	\"b9-64/-0.08 2/2 0.0\"\n" +
            "910.7	974	\"y9/0.15 2/2 7.8\"\n" +
            "911.7	404	\"y9i/1.15 2/2 2.6\"\n" +
            "912.6	148	\"y9i/2.05 2/2 0.2\"\n" +
            "933.2	94	\"b9/-0.28 2/2 1.1\"\n" +
            "950.4	58	\"? 2/2 0.4\"\n" +
            "976.4	78	\"? 2/2 0.1\"\n" +
            "1030.5	109	\"b10/-0.03 2/2 0.3\"\n" +
            "1059.3	47	\"? 2/1 0.5\"\n";

        private const string TEXT_PHOSPHO_TRANSITION_LIST =
            "762.033412,850.466895,20,sp|P08697|A2AP_HUMAN.ELKEQQDSPGNKDFLQSLK.1y7.light,86.7,39.6\n" +
            "762.033412,908.447222,20,sp|P08697|A2AP_HUMAN.ELKEQQDSPGNKDFLQSLK.3y16.light,86.7,39.6\n" +
            "762.033412,623.843144,20,sp|P08697|A2AP_HUMAN.ELKEQQDSPGNKDFLQSLK.2y11.light,86.7,39.6\n" +
            "638.626205,764.855796,20,sp|P08697|A2AP_HUMAN.EQQDSPGNKDFLQSLK.y13.light,77.7,31.9\n" +
            "638.626205,715.867348,20,sp|P08697|A2AP_HUMAN.EQQDSPGNKDFLQSLK.y13.light,77.7,31.9\n" +
            "638.626205,623.843144,20,sp|P08697|A2AP_HUMAN.EQQDSPGNKDFLQSLK.y11.light,77.7,31.9\n" +
            "728.316838,1172.569462,20,sp|Q7Z7L8|AG2_HUMAN.GAGGAGEGAVTFPER.1y12.light,84.2,42.5\n" +
            "728.316838,274.644999,20,sp|Q7Z7L8|AG2_HUMAN.GAGGAGEGAVTFPER.y4.light,84.2,42.5\n" +
            "728.316838,500.209952,20,sp|Q7Z7L8|AG2_HUMAN.GAGGAGEGAVTFPER.b7.light,84.2,42.5\n" +
            "706.837072,1129.477275,20,sp|P04075|ALDOA_HUMAN.GILAADESTGSIAK.3y11.light,82.6,41.2\n" +
            "706.837072,1058.440162,20,sp|P04075|ALDOA_HUMAN.GILAADESTGSIAK.2y10.light,82.6,41.2\n" +
            "706.837072,987.403048,20,sp|P04075|ALDOA_HUMAN.GILAADESTGSIAK.1y9.light,82.6,41.2\n" +
            "613.731021,276.101253,20,sp|P35858|ALS_HUMAN.DLSEAHFAPC.y2.light,75.9,35.4\n" +
            "613.731021,951.360789,20,sp|P35858|ALS_HUMAN.DLSEAHFAPC.2b8.light,75.9,35.4\n" +
            "613.731021,853.383893,20,sp|P35858|ALS_HUMAN.DLSEAHFAPC.1b8.light,75.9,35.4\n" +
            "694.982829,800.426093,20,sp|P01008|ANT3_HUMAN.KATEDEGSEQKIPEATNR.2y7.light,81.8,35.4\n" +
            "694.982829,687.342029,20,sp|P01008|ANT3_HUMAN.KATEDEGSEQKIPEATNR.1y6.light,81.8,35.4\n" +
            "694.982829,1298.622286,20,sp|P01008|ANT3_HUMAN.KATEDEGSEQKIPEATNR.3b12.light,81.8,35.4\n" +
            "521.488941,687.342029,20,sp|P01008|ANT3_HUMAN.KATEDEGSEQKIPEATNR.1y6.light,69.1,24.6\n" +
            "521.488941,1283.515118,20,sp|P01008|ANT3_HUMAN.KATEDEGSEQKIPEATNR.3b11.light,69.1,24.6\n" +
            "521.488941,1396.599182,20,sp|P01008|ANT3_HUMAN.KATEDEGSEQKIPEATNR.2b12.light,69.1,24.6\n" +
            "652.284508,687.342029,20,sp|P01008|ANT3_HUMAN.ATEDEGSEQKIPEATNR.1y6.light,78.7,32.8\n" +
            "652.284508,1155.420155,20,sp|P01008|ANT3_HUMAN.ATEDEGSEQKIPEATNR.3b10.light,78.7,32.8\n" +
            "652.284508,1268.504219,20,sp|P01008|ANT3_HUMAN.ATEDEGSEQKIPEATNR.2b11.light,78.7,32.8\n" +
            "753.645659,888.394518,20,sp|Q9H6X2|ANTR1_HUMAN.EVPPPPAEESEEEDDDGLPK.1y8.light,86.1,39.1\n" +
            "753.645659,1015.909347,20,sp|Q9H6X2|ANTR1_HUMAN.EVPPPPAEESEEEDDDGLPK.2y18.light,86.1,39.1\n" +
            "632.610555,808.419945,20,sp|P02647|APOA1_HUMAN.DSGRDYVSQFEGSALGK.3y8.light,77.2,31.5\n" +
            "632.610555,661.351531,20,sp|P02647|APOA1_HUMAN.DSGRDYVSQFEGSALGK.2y7.light,77.2,31.5\n" +
            "632.610555,990.427549,20,sp|P02647|APOA1_HUMAN.DSGRDYVSQFEGSALGK.1b9.light,77.2,31.5\n" +
            "618.833604,941.566609,20,sp|P02652|APOA2_HUMAN.SKEQLTPLIK.3y8.light,76.2,35.7\n" +
            "618.833604,812.524016,20,sp|P02652|APOA2_HUMAN.SKEQLTPLIK.2y7.light,76.2,35.7\n" +
            "618.833604,470.333696,20,sp|P02652|APOA2_HUMAN.SKEQLTPLIK.1y4.light,76.2,35.7\n" +
            "455.589815,598.428659,20,sp|P02652|APOA2_HUMAN.SKEQLTPLIKK.1y5.light,64.3,20.5\n" +
            "455.589815,299.717967,20,sp|P02652|APOA2_HUMAN.SKEQLTPLIKK.3y5.light,64.3,20.5\n" +
            "455.589815,568.308937,20,sp|P02652|APOA2_HUMAN.SKEQLTPLIKK.2b5.light,64.3,20.5\n" +
            "691.991547,1732.897673,20,sp|P06727|APOA4_HUMAN.ENADSLQASLRPHADELK.2y16.light,81.6,35.2\n" +
            "691.991547,1661.860559,20,sp|P06727|APOA4_HUMAN.ENADSLQASLRPHADELK.1y15.light,81.6,35.2\n" +
            "691.991547,1364.728088,20,sp|P06727|APOA4_HUMAN.ENADSLQASLRPHADELK.3y12.light,81.6,35.2\n" +
            "864.387134,1014.521449,20,sp|P02649|APOE_HUMAN.GEVQAMLGQSTEELR.3y9.light,94.1,51\n" +
            "864.387134,999.414281,20,sp|P02649|APOE_HUMAN.GEVQAMLGQSTEELR.1y8.light,94.1,51\n" +
            "864.387134,901.437385,20,sp|P02649|APOE_HUMAN.GEVQAMLGQSTEELR.2y8.light,94.1,51\n" +
            "872.384592,1112.498345,20,sp|P02649|APOE_HUMAN.GEVQAMLGQSTEELR.3y9.light,94.7,51.5\n" +
            "872.384592,999.414281,20,sp|P02649|APOE_HUMAN.GEVQAMLGQSTEELR.2y8.light,94.7,51.5\n" +
            "872.384592,901.437385,20,sp|P02649|APOE_HUMAN.GEVQAMLGQSTEELR.1y8.light,94.7,51.5\n" +
            "581.925486,1112.498345,20,sp|P02649|APOE_HUMAN.GEVQAMLGQSTEELR.1y9.light,73.5,28.4\n" +
            "581.925486,999.414281,20,sp|P02649|APOE_HUMAN.GEVQAMLGQSTEELR.2y8.light,73.5,28.4\n" +
            "581.925486,632.270838,20,sp|P02649|APOE_HUMAN.GEVQAMLGQSTEELR.3b6.light,73.5,28.4\n" +
            "880.382049,1248.588877,20,sp|P02649|APOE_HUMAN.GEVQAMLGQSTEELR.3y11.light,95.3,52\n" +
            "880.382049,999.414281,20,sp|P02649|APOE_HUMAN.GEVQAMLGQSTEELR.2y8.light,95.3,52\n" +
            "880.382049,901.437385,20,sp|P02649|APOE_HUMAN.GEVQAMLGQSTEELR.1y8.light,95.3,52\n" +
            "666.981995,1713.798961,20,sp|P02649|APOE_HUMAN.GEVQAMLGQSTEELRVR.2y14.light,79.7,33.7\n" +
            "666.981995,793.37383,20,sp|P02649|APOE_HUMAN.GEVQAMLGQSTEELRVR.1y13.light,79.7,33.7\n" +
            "666.981995,744.385382,20,sp|P02649|APOE_HUMAN.GEVQAMLGQSTEELRVR.3y13.light,79.7,33.7\n" +
            "744.336904,1073.491469,20,sp|Q15582|BGH3_HUMAN.GDELADSALEIFK.1y9.light,85.4,43.5\n" +
            "744.336904,975.514573,20,sp|Q15582|BGH3_HUMAN.GDELADSALEIFK.2y9.light,85.4,43.5\n" +
            "744.336904,904.477459,20,sp|Q15582|BGH3_HUMAN.GDELADSALEIFK.3y8.light,85.4,43.5\n" +
            "724.649515,757.456664,20,sp|Q9BUN1|CA056_HUMAN.SSAINEEDGSSEEGVVINAGK.2y8.light,83.9,37.3\n" +
            "724.649515,601.366787,20,sp|Q9BUN1|CA056_HUMAN.SSAINEEDGSSEEGVVINAGK.y6.light,83.9,37.3\n" +
            "724.649515,502.298373,20,sp|Q9BUN1|CA056_HUMAN.SSAINEEDGSSEEGVVINAGK.y5.light,83.9,37.3\n" +
            "724.649515,786.29088,20,sp|Q9BUN1|CA056_HUMAN.SSAINEEDGSSEEGVVINAGK.b15.light,83.9,37.3\n" +
            "724.649515,757.456664,20,sp|Q9BUN1|CA056_HUMAN.SSAINEEDGSSEEGVVINAGK.1y8.light,83.9,37.3\n" +
            "724.649515,601.366787,20,sp|Q9BUN1|CA056_HUMAN.SSAINEEDGSSEEGVVINAGK.y6.light,83.9,37.3\n" +
            "724.649515,786.29088,20,sp|Q9BUN1|CA056_HUMAN.SSAINEEDGSSEEGVVINAGK.2b15.light,83.9,37.3\n" +
            "692.318849,1123.477944,20,sp|Q9BUN1|CA056_HUMAN.FIANSQEPEIR.3y9.light,81.6,40.3\n" +
            "692.318849,1025.501048,20,sp|Q9BUN1|CA056_HUMAN.FIANSQEPEIR.2y9.light,81.6,40.3\n" +
            "692.318849,514.298373,20,sp|Q9BUN1|CA056_HUMAN.FIANSQEPEIR.1y4.light,81.6,40.3\n" +
            "556.573553,784.398815,20,sp|P00450|CERU_HUMAN.RQSEDSTFYLGER.2y6.light,71.7,26.8\n" +
            "556.573553,637.330401,20,sp|P00450|CERU_HUMAN.RQSEDSTFYLGER.1y5.light,71.7,26.8\n" +
            "556.573553,654.268825,20,sp|P00450|CERU_HUMAN.RQSEDSTFYLGER.3b10.light,71.7,26.8\n" +
            "556.573553,637.330401,20,sp|P00450|CERU_HUMAN.RQSEDSTFYLGER.1y5.light,71.7,26.8\n" +
            "556.573553,786.337671,20,sp|P00450|CERU_HUMAN.RQSEDSTFYLGER.2b7.light,71.7,26.8\n" +
            "556.573553,933.406085,20,sp|P00450|CERU_HUMAN.RQSEDSTFYLGER.3b8.light,71.7,26.8\n" +
            "758.012065,981.449773,20,sp|P08603|CFAH_HUMAN.IVSSAMEPDREYHFGQAVR.1y17.light,86.4,39.4\n" +
            "758.012065,867.90447,20,sp|P08603|CFAH_HUMAN.IVSSAMEPDREYHFGQAVR.3y14.light,86.4,39.4\n" +
            "758.012065,737.862931,20,sp|P08603|CFAH_HUMAN.IVSSAMEPDREYHFGQAVR.2y12.light,86.4,39.4\n" +
            "568.760868,1474.718586,20,sp|P08603|CFAH_HUMAN.IVSSAMEPDREYHFGQAVR.3y12.light,72.6,27.5\n" +
            "611.267051,850.435217,20,sp|P08603|CFAH_HUMAN.CLHPCVISR.3y7.light,75.7,35.2\n" +
            "611.267051,713.376306,20,sp|P08603|CFAH_HUMAN.CLHPCVISR.1y6.light,75.7,35.2\n" +
            "611.267051,411.1809,20,sp|P08603|CFAH_HUMAN.CLHPCVISR.2b3.light,75.7,35.2\n" +
            "696.304885,937.492398,20,sp|P08603|CFAH_HUMAN.TGESVEFVCKR.3y7.light,81.9,40.5\n" +
            "696.304885,838.423984,20,sp|P08603|CFAH_HUMAN.TGESVEFVCKR.1y6.light,81.9,40.5\n" +
            "696.304885,709.381391,20,sp|P08603|CFAH_HUMAN.TGESVEFVCKR.2y5.light,81.9,40.5\n" +
            "798.719382,1014.582987,20,sp|P10909|CLUS_HUMAN.VTTVASHTSDSDVPSGVTEVVVK.2y10.light,89.3,41.9\n" +
            "798.719382,507.795132,20,sp|P10909|CLUS_HUMAN.VTTVASHTSDSDVPSGVTEVVVK.1y10.light,89.3,41.9\n" +
            "798.719382,1380.567881,20,sp|P10909|CLUS_HUMAN.VTTVASHTSDSDVPSGVTEVVVK.3b13.light,89.3,41.9\n" +
            "775.876728,1129.548392,20,sp|P01024|CO3_HUMAN.IPIEDGSGEVVLSR.1y11.light,87.7,45.5\n" +
            "775.876728,885.478856,20,sp|P01024|CO3_HUMAN.IPIEDGSGEVVLSR.2y9.light,87.7,45.5\n" +
            "775.876728,719.334696,20,sp|P01024|CO3_HUMAN.IPIEDGSGEVVLSR.3y13.light,87.7,45.5\n" +
            "862.881685,1512.603615,20,sp|P01034|CYTC_HUMAN.LVGGPMDASVEEEGVR.2y14.light,94,50.9\n" +
            "862.881685,1072.490543,20,sp|P01034|CYTC_HUMAN.LVGGPMDASVEEEGVR.3y10.light,94,50.9\n" +
            "862.881685,957.4636,20,sp|P01034|CYTC_HUMAN.LVGGPMDASVEEEGVR.1y9.light,94,50.9\n" +
            "627.623919,1668.704726,20,sp|P01034|CYTC_HUMAN.LVGGPMDASVEEEGVRR.1y15.light,76.9,31.2\n" +
            "627.623919,1570.72783,20,sp|P01034|CYTC_HUMAN.LVGGPMDASVEEEGVRR.2y15.light,76.9,31.2\n" +
            "627.623919,1554.661799,20,sp|P01034|CYTC_HUMAN.LVGGPMDASVEEEGVRR.3y13.light,76.9,31.2\n" +
            "834.356949,1162.569856,20,sp|O94769|ECM2_HUMAN.EALQSEEDEEVKEEDTEQKR.3y9.light,91.9,44.1\n" +
            "834.356949,981.432163,20,sp|O94769|ECM2_HUMAN.EALQSEEDEEVKEEDTEQKR.2y16.light,91.9,44.1\n" +
            "600.929342,663.32427,20,sp|P05160|F13B_HUMAN.SGYLLHGSNEITCNR.1y5.light,74.9,29.6\n" +
            "600.929342,550.240206,20,sp|P05160|F13B_HUMAN.SGYLLHGSNEITCNR.y4.light,74.9,29.6\n" +
            "600.929342,1138.45648,20,sp|P05160|F13B_HUMAN.SGYLLHGSNEITCNR.3b10.light,74.9,29.6\n" +
            "971.389044,1104.535385,20,sp|P12259|FA5_HUMAN.CIPDDDEDSYEIFEPPESTVMATR.2y10.light,101.9,52.7\n" +
            "971.389044,552.771331,20,sp|P12259|FA5_HUMAN.CIPDDDEDSYEIFEPPESTVMATR.1y10.light,101.9,52.7\n" +
            "971.389044,1679.581877,20,sp|P12259|FA5_HUMAN.CIPDDDEDSYEIFEPPESTVMATR.3b13.light,101.9,52.7\n" +
            "861.85005,1155.535051,20,sp|P12259|FA5_HUMAN.SSSPELSEMLEYDR.2y9.light,93.9,50.9\n" +
            "861.85005,1042.450987,20,sp|P12259|FA5_HUMAN.SSSPELSEMLEYDR.1y8.light,93.9,50.9\n" +
            "861.85005,691.318842,20,sp|P12259|FA5_HUMAN.SSSPELSEMLEYDR.3y11.light,93.9,50.9\n" +
            "869.847508,1171.529966,20,sp|P12259|FA5_HUMAN.SSSPELSEMLEYDR.3y9.light,94.5,51.4\n" +
            "869.847508,1058.445902,20,sp|P12259|FA5_HUMAN.SSSPELSEMLEYDR.1y8.light,94.5,51.4\n" +
            "869.847508,733.827031,20,sp|P12259|FA5_HUMAN.SSSPELSEMLEYDR.y12.light,94.5,51.4\n" +
            "1102.41048,781.387916,20,sp|P00740|FA9_HUMAN.DDINSYECWCPFGFEGK.1y7.light,111.5,65.9\n" +
            "1102.41048,204.134267,20,sp|P00740|FA9_HUMAN.DDINSYECWCPFGFEGK.y2.light,111.5,65.9\n" +
            "1102.41048,391.197596,20,sp|P00740|FA9_HUMAN.DDINSYECWCPFGFEGK.2y7.light,111.5,65.9\n" +
            "936.39895,1061.510944,20,sp|P23142|FBLN1_HUMAN.SQETGDLDVGGLQETDK.1y10.light,99.4,55.5\n" +
            "936.39895,946.484001,20,sp|P23142|FBLN1_HUMAN.SQETGDLDVGGLQETDK.2y9.light,99.4,55.5\n" +
            "936.39895,847.415587,20,sp|P23142|FBLN1_HUMAN.SQETGDLDVGGLQETDK.y8.light,99.4,55.5\n" +
            "545.00159,595.33107,20,sp|P02765|FETUA_HUMAN.HTFMGVVSLGSPSGEVSHPR.3y5.light,70.8,26.1\n" +
            "545.00159,590.239144,20,sp|P02765|FETUA_HUMAN.HTFMGVVSLGSPSGEVSHPR.1b5.light,70.8,26.1\n" +
            "545.00159,689.307558,20,sp|P02765|FETUA_HUMAN.HTFMGVVSLGSPSGEVSHPR.2b6.light,70.8,26.1\n" +
            "901.382149,1413.615366,20,sp|P36980|FHR2_HUMAN.CLDPCVISQEIMEK.4y11.light,96.8,53.3\n" +
            "901.382149,959.486644,20,sp|P36980|FHR2_HUMAN.CLDPCVISQEIMEK.1y8.light,96.8,53.3\n" +
            "901.382149,389.194196,20,sp|P36980|FHR2_HUMAN.CLDPCVISQEIMEK.y6.light,96.8,53.3\n" +
            "909.379606,1331.633385,20,sp|P36980|FHR2_HUMAN.CLDPCVISQEIMEK.2y11.light,97.4,53.8\n" +
            "909.379606,715.308778,20,sp|P36980|FHR2_HUMAN.CLDPCVISQEIMEK.3y11.light,97.4,53.8\n" +
            "909.379606,389.148932,20,sp|P36980|FHR2_HUMAN.CLDPCVISQEIMEK.1b3.light,97.4,53.8\n" +
            "606.58883,975.481559,20,sp|P36980|FHR2_HUMAN.CLDPCVISQEIMEK.2y8.light,75.3,29.9\n" +
            "606.58883,960.374391,20,sp|P36980|FHR2_HUMAN.CLDPCVISQEIMEK.3y7.light,75.3,29.9\n" +
            "606.58883,858.384822,20,sp|P36980|FHR2_HUMAN.CLDPCVISQEIMEK.1b7.light,75.3,29.9\n" +
            "681.943256,832.39077,20,sp|P02671|FIBA_HUMAN.NPSSAGSWNSGSSGPGSTGNR.2y9.light,80.8,34.6\n" +
            "681.943256,688.337278,20,sp|P02671|FIBA_HUMAN.NPSSAGSWNSGSSGPGSTGNR.1y7.light,80.8,34.6\n" +
            "681.943256,681.223961,20,sp|P02671|FIBA_HUMAN.NPSSAGSWNSGSSGPGSTGNR.b7.light,80.8,34.6\n" +
            "681.943256,832.39077,20,sp|P02671|FIBA_HUMAN.NPSSAGSWNSGSSGPGSTGNR.3y9.light,80.8,34.6\n" +
            "681.943256,745.358741,20,sp|P02671|FIBA_HUMAN.NPSSAGSWNSGSSGPGSTGNR.2y8.light,80.8,34.6\n" +
            "681.943256,688.337278,20,sp|P02671|FIBA_HUMAN.NPSSAGSWNSGSSGPGSTGNR.1y7.light,80.8,34.6\n" +
            "780.669133,517.272886,20,sp|P02671|FIBA_HUMAN.PNNPDWGTFEEVSGNVSPGTR.y5.light,88,40.8\n" +
            "780.669133,430.240858,20,sp|P02671|FIBA_HUMAN.PNNPDWGTFEEVSGNVSPGTR.2y4.light,88,40.8\n" +
            "780.669133,912.367256,20,sp|P02671|FIBA_HUMAN.PNNPDWGTFEEVSGNVSPGTR.1b16.light,88,40.8\n" +
            "1070.128112,1135.610599,20,sp|P02671|FIBA_HUMAN.EVVTSEDGSDCPEAMDLGTLSGIGTLDGFR.2y11.light,109.1,58.9\n" +
            "1070.128112,1022.526535,20,sp|P02671|FIBA_HUMAN.EVVTSEDGSDCPEAMDLGTLSGIGTLDGFR.y10.light,109.1,58.9\n" +
            "1070.128112,765.388979,20,sp|P02671|FIBA_HUMAN.EVVTSEDGSDCPEAMDLGTLSGIGTLDGFR.y7.light,109.1,58.9\n" +
            "1070.128112,1259.413355,20,sp|P02671|FIBA_HUMAN.EVVTSEDGSDCPEAMDLGTLSGIGTLDGFR.1b11.light,109.1,58.9\n" +
            "656.286294,808.383559,20,sp|P02671|FIBA_HUMAN.HRHPDEAAFFDTASTGK.2y8.light,79,33\n" +
            "656.286294,914.422738,20,sp|P02671|FIBA_HUMAN.HRHPDEAAFFDTASTGK.8b8.light,79,33\n" +
            "656.286294,457.715007,20,sp|P02671|FIBA_HUMAN.HRHPDEAAFFDTASTGK.b8.light,79,33\n" +
            "837.345792,1439.572632,20,sp|P02671|FIBA_HUMAN.HPDEAAFFDTASTGK.2y13.light,92.2,49.3\n" +
            "837.345792,719.827888,20,sp|P02671|FIBA_HUMAN.HPDEAAFFDTASTGK.1y14.light,92.2,49.3\n" +
            "837.345792,235.118952,20,sp|P02671|FIBA_HUMAN.HPDEAAFFDTASTGK.b2.light,92.2,49.3\n" +
            "558.566287,906.360455,20,sp|P02671|FIBA_HUMAN.HPDEAAFFDTASTGK.2y8.light,71.8,26.9\n" +
            "558.566287,621.262716,20,sp|P02671|FIBA_HUMAN.HPDEAAFFDTASTGK.3b6.light,71.8,26.9\n" +
            "558.566287,768.33113,20,sp|P02671|FIBA_HUMAN.HPDEAAFFDTASTGK.1b7.light,71.8,26.9\n" +
            "710.57253,973.510157,20,sp|P02671|FIBA_HUMAN.GSESGIFTNTKESSSHHPGIAEFPSR.2y9.light,82.9,36.4\n" +
            "573.249181,706.351865,20,sp|P02671|FIBA_HUMAN.ESSSHHPGIAEFPSR.1y6.light,72.9,27.8\n" +
            "573.249181,359.203744,20,sp|P02671|FIBA_HUMAN.ESSSHHPGIAEFPSR.3y3.light,72.9,27.8\n" +
            "573.249181,487.258716,20,sp|P02671|FIBA_HUMAN.ESSSHHPGIAEFPSR.2y9.light,72.9,27.8\n" +
            "649.275073,559.302078,20,sp|P21333|FLNA_HUMAN.CSGPGLSPGMVR.1y5.light,78.4,37.6\n" +
            "649.275073,287.080852,20,sp|P21333|FLNA_HUMAN.CSGPGLSPGMVR.3b3.light,78.4,37.6\n" +
            "649.275073,641.271172,20,sp|P21333|FLNA_HUMAN.CSGPGLSPGMVR.2b7.light,78.4,37.6\n" +
            "720.350018,899.462144,20,sp|P06396|GELS_HUMAN.VPEARPNSMVVEHPEFLK.6y7.light,83.6,37\n" +
            "720.350018,633.360639,20,sp|P06396|GELS_HUMAN.VPEARPNSMVVEHPEFLK.2y5.light,83.6,37\n" +
            "720.350018,687.327214,20,sp|P06396|GELS_HUMAN.VPEARPNSMVVEHPEFLK.y17.light,83.6,37\n" +
            "650.28616,890.440028,20,sp|P14314|GLU2B_HUMAN.SLEDQVEMLR.1y7.light,78.5,37.6\n" +
            "650.28616,775.413085,20,sp|P14314|GLU2B_HUMAN.SLEDQVEMLR.2y6.light,78.5,37.6\n" +
            "650.28616,647.354507,20,sp|P14314|GLU2B_HUMAN.SLEDQVEMLR.y5.light,78.5,37.6\n" +
            "621.289037,1015.402671,20,sp|Q14520|HABP2_HUMAN.LIANTLCNSR.1y8.light,76.4,35.8\n" +
            "621.289037,917.425775,20,sp|Q14520|HABP2_HUMAN.LIANTLCNSR.2y8.light,76.4,35.8\n" +
            "621.289037,944.365557,20,sp|Q14520|HABP2_HUMAN.LIANTLCNSR.3y7.light,76.4,35.8\n" +
            "1026.936565,1156.574548,20,sp|P05546|HEP2_HUMAN.GGETAQSADPQWEQLNNK.1y9.light,106,61.2\n" +
            "1026.936565,897.298583,20,sp|P05546|HEP2_HUMAN.GGETAQSADPQWEQLNNK.3b9.light,106,61.2\n" +
            "1026.936565,799.321687,20,sp|P05546|HEP2_HUMAN.GGETAQSADPQWEQLNNK.2b9.light,106,61.2\n" +
            "684.960136,931.463206,20,sp|P05546|HEP2_HUMAN.GGETAQSADPQWEQLNNK.3y7.light,81.1,34.8\n" +
            "684.960136,578.790912,20,sp|P05546|HEP2_HUMAN.GGETAQSADPQWEQLNNK.2y9.light,81.1,34.8\n" +
            "684.960136,897.298583,20,sp|P05546|HEP2_HUMAN.GGETAQSADPQWEQLNNK.1b9.light,81.1,34.8\n" +
            "940.386685,833.851954,20,sp|P05546|HEP2_HUMAN.ENTVTNDWIPEGEEDDDYLDLEK.1y14.light,99.7,50.8\n" +
            "940.386685,913.87121,20,sp|P05546|HEP2_HUMAN.ENTVTNDWIPEGEEDDDYLDLEK.b16.light,99.7,50.8\n" +
            "940.386685,833.851954,20,sp|P05546|HEP2_HUMAN.ENTVTNDWIPEGEEDDDYLDLEK.1y14.light,99.7,50.8\n" +
            "940.386685,913.87121,20,sp|P05546|HEP2_HUMAN.ENTVTNDWIPEGEEDDDYLDLEK.b16.light,99.7,50.8";

        private const string TEXT_PHOSPHO_MULTI_LOSS =
            "763.343704,989.44723,20,sp|P08603|CFAH_HUMAN.IVSSAMEPDREYHFGQAVR.1y17.light,86.8,39.7\n" +
            "763.343704,957.448088,20,sp|P08603|CFAH_HUMAN.IVSSAMEPDREYHFGQAVR.2y17.light,86.8,39.7\n" +
            "763.343704,737.862931,20,sp|P08603|CFAH_HUMAN.IVSSAMEPDREYHFGQAVR.3y12.light,86.8,39.7\n" +
            "632.955557,1684.699641,20,sp|P01034|CYTC_HUMAN.LVGGPMDASVEEEGVRR.1y15.light,77.3,31.6\n" +
            "632.955557,793.865011,20,sp|P01034|CYTC_HUMAN.LVGGPMDASVEEEGVRR.2y15.light,77.3,31.6\n" +
            "632.955557,761.865868,20,sp|P01034|CYTC_HUMAN.LVGGPMDASVEEEGVRR.3y15.light,77.3,31.6\n";

    }
}