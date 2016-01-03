/*
 * Original author: Alana Killeen <killea .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2010 University of Washington - Seattle, WA
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
    /// Summary description for ModificationMatcherTest
    /// </summary>
    [TestClass]
    public class ModificationMatcherTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestModificationMatcher()
        {
            InitSeqs();

            var carbC = StaticModList.GetDefaultsOn()[0];

            // Test exception thrown if unable to match - mass.
            UpdateMatcherFail(STR_FAIL_MASS);
            UpdateMatcherFail(STR_FAIL_NOT_A_NUMBER);
            // Test exception thrown if unable to match - name.
            UpdateMatcherFail(STR_FAIL_NAME);
            // Can't match empty modifications.
            UpdateMatcherFail(STR_FAIL_EMPTY_MOD); 
            UpdateMatcherFail(STR_FAIL_EMPTY_MOD2);
            // Can't match double modifications.
            UpdateMatcherFail(STR_FAIL_DOUBLE_MOD);
            // Test exception thrown if unimod not specified correctly
            UpdateMatcherFail(STR_FAIL_UNIMOD);
            UpdateMatcherFail(STR_UNKNOWN_UNIMOD);
            // Can't phosphorylate tryptophan
            UpdateMatcherFail(STR_FAIL_WRONG_AA_UNIMOD);
            // Can't put C-terminal modification in middle of peptide
            UpdateMatcherFail(STR_FAIL_UNIMOD_TERMINUS);

            // Test mods in UniMod match correctly. 
            UpdateMatcher(StaticModList.GetDefaultsOn(), HeavyModList.GetDefaultsOn(), null, null);
            // A sequence with no modifications should not be explicitly modified.
            Assert.IsFalse(MATCHER.GetModifiedNode(STR_NO_MODS).HasExplicitMods);
            var nodeCysOxi = MATCHER.GetModifiedNode(STR_CYS_AND_OXI);
            Assert.IsTrue(nodeCysOxi.HasExplicitMods);
            Assert.IsFalse(nodeCysOxi.ExplicitMods.HasHeavyModifications);
            // Modifications should match by name.
            Assert.IsTrue(MATCHER.GetModifiedNode(STR_MOD_BY_NAME).ExplicitMods.StaticModifications.Contains(mod => 
                Equals(mod.Modification.Name,  "Phospho (ST)")));
            // Test can find terminal modification
            Assert.IsTrue(MATCHER.GetModifiedNode(STR_TERM_ONLY).ExplicitMods.HeavyModifications.Contains(mod => 
                mod.Modification.EquivalentAll(UniMod.GetModification("Label:13C(6) (C-term R)", false))));
            // Test can find matches on terminus that are not terminal
            Assert.IsTrue(MATCHER.GetModifiedNode(STR_MOD_BY_NAME).ExplicitMods.StaticModifications.Contains(mod =>
                mod.Modification.Terminus == null));
            // Test matching negative masses
            Assert.IsTrue(MATCHER.GetModifiedNode(STR_AMMONIA_LOSS).ExplicitMods.StaticModifications.Contains(mod =>
                mod.Modification.EquivalentAll(UniMod.GetModification("Ammonia-loss (N-term C)", true))));

            // General and specific
            // If all AAs modified, try for most general modification.
            Assert.IsTrue(MATCHER.GetModifiedNode(STR_HEAVY_15)
                .ExplicitMods.HeavyModifications.Contains(mod => mod.Modification.Equivalent(LABEL15_N)));
            
            // Updating the settings.
            // Peptide settings should change to include new mods.
            var docNew = new SrmDocument(SrmSettingsList.GetDefault());
            IdentityPath firstAdded;
            IdentityPath nextAdded;
            docNew = docNew.AddPeptideGroups(new[] { new PeptideGroupDocNode(new PeptideGroup(), "PepGroup1", "",
                new[] {MATCHER.GetModifiedNode(STR_MOD_BY_NAME)})}, true, null, out firstAdded, out nextAdded);
            var pepSetNew = MATCHER.GetDocModifications(docNew);
            Assert.IsTrue(pepSetNew.StaticModifications.Contains(UniMod.GetModification("Phospho (ST)", true).ChangeExplicit(true)));
            // Update the document to the new settings.
            var pepSetNew1 = pepSetNew;
            var settingsNew2 = docNew.Settings.ChangePeptideModifications(mods => pepSetNew1);
            var lightGlobalMods = new MappedList<string, StaticMod>();
            lightGlobalMods.AddRange(settingsNew2.PeptideSettings.Modifications.StaticModifications);
            var heavyGlobalMods = new MappedList<string, StaticMod>();
            heavyGlobalMods.AddRange(settingsNew2.PeptideSettings.Modifications.HeavyModifications);
            // Match again. Test FoundMatches string should now be empty.
            MATCHER.CreateMatches(docNew.Settings.ChangePeptideModifications(mods => pepSetNew1), 
                new List<string> { STR_MOD_BY_NAME }, lightGlobalMods, heavyGlobalMods);
            Assert.IsTrue(string.IsNullOrEmpty(MATCHER.FoundMatches));
            
            // Adding 15N to the settings.
            UpdateMatcher(new[] { carbC }, new[] { LABEL15_N }, null, null);
            // Test sequences with only explicit heavy mods should not have explicit light mods
            Assert.IsNull(MATCHER.GetModifiedNode(STR_HEAVY_ONLY).ExplicitMods.StaticModifications);

            // Test sequences with only explicit light mods should not have explicit heavy mods
            Assert.IsFalse(MATCHER.GetModifiedNode(STR_LIGHT_ONLY).ExplicitMods.HasHeavyModifications);

            // Test global mods take precendence over UniMod
            UpdateMatcher(new[] { carbC }, null, new[] { OXIDATION_M_GLOBAL }, new[] { LABEL15_N });
            Assert.IsTrue(MATCHER.GetModifiedNode(STR_CYS_AND_OXI).ExplicitMods.StaticModifications
                .Contains(mod => Equals(mod.Modification, OXIDATION_M_GLOBAL)));

            // Test document mods take precendence over UniMod
            UpdateMatcher(new[] { carbC, METHIONINE_OXIDATION }, null, new[] { OXIDATION_M_GLOBAL }, new[] { LABEL15_N });
            Assert.IsFalse(MATCHER.GetModifiedNode(STR_CYS_AND_OXI).HasExplicitMods);

            // Test exception thrown if match doesn't make sense - wrong AA.
            UpdateMatcherFail(STR_FAIL_OX_ON_D);
            // Test exception thrown if match doesn't make sense - wrong terminus.
            _seqs.Add(STR_FAIL_OX_TERM);
            AssertEx.ThrowsException<FormatException>(() => UpdateMatcher(new[] {OXIDATION_M_C_TERM}, null, null, null));
            _seqs.Remove(STR_FAIL_OX_TERM);

            // Heavy 15N - All AAs.
            UpdateMatcher(new[] { carbC, METHIONINE_OXIDATION }, new[] {LABEL15_N}, null, null);
            // Node should be created from document settings if possible.
            Assert.IsNull(MATCHER.GetModifiedNode(STR_HEAVY_15).ExplicitMods);
            // Heavy 15N - specific AA.
            // If only a specific AA is modified, there must be an explicit mod.
            Assert.IsTrue(MATCHER.GetModifiedNode(STR_HEAVY_15_F).HasExplicitMods);

            // Test variable mods match correctly.
            // Put variable mod in global mod and not on doc - make sure don't get variable mod,
            // should get explicit mod in that case.
            var variableMetOx = METHIONINE_OXIDATION.ChangeVariable(true);
            UpdateMatcher(new[] { carbC }, null, new[] { variableMetOx }, null);
            Assert.IsTrue(MATCHER.GetModifiedNode(STR_CYS_AND_OXI).HasExplicitMods);
            Assert.IsFalse(MATCHER.GetModifiedNode(STR_CYS_OXI_PHOS).ExplicitMods.IsVariableStaticMods);
            Assert.IsFalse(MATCHER.GetModifiedNode(STR_CYS_OXI_PHOS_CAP).ExplicitMods.IsVariableStaticMods);
            // Add variable mod to doc
            UpdateMatcher(new[] { carbC, variableMetOx }, null, null, null);
            // Mod can be created by the settings.
            Assert.IsTrue(MATCHER.GetModifiedNode(STR_CYS_AND_OXI).HasExplicitMods);
            Assert.IsTrue(MATCHER.GetModifiedNode(STR_CYS_AND_OXI).ExplicitMods.IsVariableStaticMods);
            // Mod cannot be created by the settings.
            Assert.IsFalse(MATCHER.GetModifiedNode(STR_CYS_OXI_PHOS).ExplicitMods.IsVariableStaticMods);
            Assert.IsFalse(MATCHER.GetModifiedNode(STR_CYS_OXI_PHOS_CAP).ExplicitMods.IsVariableStaticMods);

            // Add Met Ox to global. Test: +16 finds it.
            UpdateMatcher(new[] {carbC}, null, new[] {MET_OX_ROUNDED}, null);
            Assert.IsTrue(MATCHER.GetModifiedNode(STR_CYS_AND_OXI).
                ExplicitMods.StaticModifications.Contains(mod => Equals(mod.Modification, MET_OX_ROUNDED)));
            // Test: +15.99 finds UniMod.
            Assert.IsFalse(MATCHER.GetModifiedNode(STR_HEAVY_15).
                ExplicitMods.StaticModifications.Contains(mod => Equals(mod.Modification, MET_OX_ROUNDED)));
            // Add Methionine Oxidation before Met Ox. Test: +16 finds it.
            UpdateMatcher(new[] { carbC }, null, new[] { METHIONINE_OXIDATION, MET_OX_ROUNDED }, null);
            Assert.IsFalse(MATCHER.GetModifiedNode(STR_CYS_AND_OXI).
                ExplicitMods.StaticModifications.Contains(mod => Equals(mod.Modification, MET_OX_ROUNDED)));
            Assert.IsTrue(MATCHER.GetModifiedNode(STR_CYS_AND_OXI).
                ExplicitMods.StaticModifications.Contains(mod => Equals(mod.Modification, METHIONINE_OXIDATION)));
            // Test long masses rounded.
            Assert.IsTrue(MATCHER.GetModifiedNode(STR_METOX_LONG_MASS).ExplicitMods.StaticModifications.Contains(mod =>
                Equals(mod.Modification, METHIONINE_OXIDATION)));
            // Test UniMod label types
            var node = MATCHER.GetModifiedNode(STR_UNIMOD_LABEL);
            Assert.IsNotNull(node);
            Assert.IsNull(node.ExplicitMods.StaticModifications);
            Assert.IsTrue(node.ExplicitMods.HeavyModifications.Contains(mod =>
                Equals(mod.Modification, N_TERM_LABEL)));
            UpdateMatcherWithNoSequences(new[] { carbC }, new[] { N_TERM_LABEL }, new[] { METHIONINE_OXIDATION, MET_OX_ROUNDED }, null);
            var nodeNew = MATCHER.GetModifiedNode(STR_UNIMOD_LABEL);
            Assert.IsNotNull(nodeNew);
            Assert.IsTrue(nodeNew.TransitionGroups.Any(group => Equals(group.TransitionGroup.LabelType, IsotopeLabelType.heavy)));
            UpdateMatcher(new[] { carbC }, null, new[] { METHIONINE_OXIDATION, MET_OX_ROUNDED }, null);
            
            // Test case where there are lots of unimod labels
            var nodeUniAll = MATCHER.GetModifiedNode(STR_UNIMOD_ALL);
            Assert.AreEqual(nodeUniAll.ExplicitMods.HeavyModifications.Count, 10);
            Assert.IsNull(nodeUniAll.ExplicitMods.StaticModifications);
            foreach (var mod in nodeUniAll.ExplicitMods.HeavyModifications)
            {
                Assert.AreEqual(mod.Modification.ShortName, "+01");
                Assert.AreEqual(mod.Modification.UnimodId, 994);
            }

            // Test unimod terminal label
            var nodeUniTerm = MATCHER.GetModifiedNode(STR_UNIMOD_TERMINUS);
            Assert.AreEqual(nodeUniTerm.ExplicitMods.HeavyModifications.Count, 1);
            Assert.IsNull(nodeUniTerm.ExplicitMods.StaticModifications);
            Assert.AreEqual(nodeUniTerm.ExplicitMods.HeavyModifications[0].Modification.Terminus, ModTerminus.C);
            Assert.AreEqual(nodeUniTerm.ExplicitMods.HeavyModifications[0].Modification.UnimodId, 298);

            // Basic multi-label test
            var heavyLabelType2 = new IsotopeLabelType("Heavy2", 1);
            var typedMod = new TypedModifications(heavyLabelType2, new List<StaticMod> { LABEL15_N });
            var peptideMods = new PeptideModifications(new List<StaticMod>(), new List<TypedModifications> { typedMod });
            var settingsMultiLabel = SrmSettingsList.GetDefault().ChangePeptideModifications(mods => peptideMods);
            var defSetSetLight = new MappedList<string, StaticMod>();
            defSetSetLight.AddRange(StaticModList.GetDefaultsOn());
            var defSetHeavy = new MappedList<string, StaticMod>();
            defSetHeavy.AddRange(HeavyModList.GetDefaultsOn());
            defSetHeavy.Add( LABEL15_N );
            MATCHER.CreateMatches(settingsMultiLabel, new List<string> { STR_HEAVY_15_F }, defSetSetLight, defSetHeavy);
            Assert.IsTrue(MATCHER.GetModifiedNode(STR_HEAVY_15_F).ExplicitMods.GetHeavyModifications().Contains(mod => Equals(mod.LabelType, heavyLabelType2)));
            // Peptide settings should not change.
            var docNew0 = new SrmDocument(settingsMultiLabel).AddPeptideGroups(new[] { new PeptideGroupDocNode(new PeptideGroup(), 
                "PepGroup1", "", new[] {MATCHER.GetModifiedNode(STR_HEAVY_15_F)})}, true, null, out firstAdded, out nextAdded);
            var settingsNew = MATCHER.GetDocModifications(docNew0);
            Assert.AreEqual(settingsMultiLabel.PeptideSettings.Modifications, settingsNew);

            // Finding specific modifications.
            // If only specific AA modified, try for most specific modification.
            UpdateMatcher(null, null, null, null, new[] { STR_HEAVY_15_F});
            Assert.IsTrue(MATCHER.GetModifiedNode(STR_HEAVY_15_F)
                .ExplicitMods.HeavyModifications.Contains(mod =>
                    mod.Modification.AminoAcids.Contains(c => c == 'F')));
            // If only some AAs modified, try for most specific modifications.
            UpdateMatcher(null, null, null, null, new[] { STR_HEAVY_15_NOT_ALL });
            Assert.IsTrue(MATCHER.GetModifiedNode(STR_HEAVY_15_NOT_ALL)
                .ExplicitMods.HeavyModifications.Contains(mod =>
                mod.Modification.AminoAcids.Contains(c => c == 'I')));


            using (var testDir = new TestFilesDir(TestContext, ZIP_FILE))
            {
                var modMatchDocContainer = InitMatchDocContainer(testDir);
                var libkeyModMatcher = new LibKeyModificationMatcher();
                var anlLibSpec = new BiblioSpecLiteSpec("ANL_Combo", testDir.GetTestPath("ANL_Combined.blib"));
                var yeastLibSpec = new BiblioSpecLiteSpec("Yeast", testDir.GetTestPath("Yeast_atlas_small.blib"));
                modMatchDocContainer.ChangeLibSpecs(new[] { anlLibSpec, yeastLibSpec });
                var docLibraries = modMatchDocContainer.Document.Settings.PeptideSettings.Libraries.Libraries;
                int anlLibIndex = docLibraries.IndexOf(library => Equals(library.Name, anlLibSpec.Name));
                int yeastLibIndex = docLibraries.IndexOf(library => Equals(library.Name, yeastLibSpec.Name));

                libkeyModMatcher.CreateMatches(modMatchDocContainer.Document.Settings,
                    docLibraries[anlLibIndex].Keys, defSetSetLight, defSetHeavy);

                // Test can match 15N
                Assert.IsTrue(libkeyModMatcher.Matches.Values.Contains(match =>
                    match.HeavyMod != null && match.HeavyMod.Equivalent(LABEL15_N)));

                var uniModMetOx = UniMod.GetModification("Oxidation (M)", true);

                // Test can match Met Ox
                Assert.IsTrue(libkeyModMatcher.Matches.Values.Contains(match =>
                    match.StructuralMod != null && match.StructuralMod.Equivalent(uniModMetOx)));

                // Test can match 15N and Met ox!
                Assert.IsTrue(libkeyModMatcher.Matches.Contains(match => match.Key.Mass == 17
                    && match.Value.StructuralMod != null && match.Value.StructuralMod.Equivalent(uniModMetOx)
                    && match.Value.HeavyMod != null && match.Value.HeavyMod.Equivalent(LABEL15_N)));

                // Test can match Cysteine (Implicit) and Met Ox (variable)
                libkeyModMatcher.CreateMatches(modMatchDocContainer.Document.Settings,
                    docLibraries[yeastLibIndex].Keys, defSetSetLight, defSetHeavy);
                Assert.IsTrue(libkeyModMatcher.MatcherPepMods.StaticModifications.Contains(mod =>
                    mod.Formula.Equals(UniMod.GetModification(StaticModList.DEFAULT_NAME, true).Formula) && !mod.IsVariable));
                Assert.IsTrue(libkeyModMatcher.MatcherPepMods.StaticModifications.Contains(mod =>
                    mod.Formula.Equals("O") && mod.IsVariable));
            }
        }

        private const string ZIP_FILE = @"TestA\ModMatch.zip";

        private static void UpdateMatcherFail(string seq)
        {
            if (seq != null)
                _seqs.Add(seq);
            AssertEx.ThrowsException<FormatException>(() => 
                UpdateMatcher(null, null, null, null));
            _seqs.Remove(seq);
        }

        private static void UpdateMatcherWithNoSequences(
            StaticMod[] docStatMods, StaticMod[] docHeavyMods,
            StaticMod[] globalStatMods, StaticMod[] globalHeavyMods)
        {
            UpdateMatcher(docStatMods, docHeavyMods, globalStatMods, globalHeavyMods, new List<string>());
        }

        private static void UpdateMatcher(
            StaticMod[] docStatMods, StaticMod[] docHeavyMods, 
            StaticMod[] globalStatMods, StaticMod[] globalHeavyMods)
        {
            UpdateMatcher(docStatMods, docHeavyMods, globalStatMods, globalHeavyMods, _seqs);
        }

        private static void UpdateMatcher(
            StaticMod[] docStatMods, StaticMod[] docHeavyMods, 
            StaticMod[] globalStatMods, StaticMod[] globalHeavyMods, IEnumerable<string> seqs)
        {
            docStatMods = docStatMods ?? new StaticMod[0];
            docHeavyMods = docHeavyMods ?? new StaticMod[0];
            globalStatMods = globalStatMods ?? new StaticMod[0];
            globalHeavyMods = globalHeavyMods ?? new StaticMod[0];
            MappedList<string, StaticMod> mapGlobalStatMods = new MappedList<string, StaticMod>();
            mapGlobalStatMods.AddRange(docStatMods);
            mapGlobalStatMods.AddRange(globalStatMods);
            MappedList<string, StaticMod> mapGlobalHeavyMods = new MappedList<string, StaticMod>();
            mapGlobalHeavyMods.AddRange(docHeavyMods);
            mapGlobalHeavyMods.AddRange(globalHeavyMods);
            var settings = SrmSettingsList.GetDefault()
                .ChangePeptideModifications(mods =>
                    mods.ChangeStaticModifications(docStatMods));
            settings = settings.ChangePeptideModifications(mods =>
                mods.ChangeHeavyModifications(docHeavyMods));
            MATCHER.CreateMatches(settings, seqs, mapGlobalStatMods, mapGlobalHeavyMods);
        }

        private static readonly ModificationMatcher MATCHER = new ModificationMatcher();

        private static ResultsTestDocumentContainer InitMatchDocContainer(TestFilesDir testFilesDir)
        {
            string docPath = testFilesDir.GetTestPath("modmatch.sky");
            SrmDocument doc = ResultsUtil.DeserializeDocument(docPath);
            return new ResultsTestDocumentContainer(doc, docPath);
        }

        private const string STR_NO_MODS = "ALSIGFETCR"; 
        private static string STR_HEAVY_15 { get { return "A{+1}E{+1}I{+1}D{+1}M{+1}[+" + 15.995 + "]L{+1}D{+1}I{+1}R{+4}"; } }
        private const string STR_HEAVY_15_F = "VAILIPF{+1}R";
        private static string STR_HEAVY_15_NOT_ALL { get { return "A{+1}E{+1}I{+1}D{+1}M{+1}[+" + 15.995 + "]L{+1}D{+1}I{+1}R"; } }
        private const string STR_MOD_BY_NAME = "S[PHO]LLYFVYVAPGIVNT(UniMod:21)YLFMMQAQGILIR";
        private const string STR_TERM_ONLY = "VAILIPFR{Label:13C(6) (C-term R)}";
        private const string STR_MOD_BY_NAME_TERMINUS = "SLLAALFFFSLSSSLLYFVYVAPGIVNT[Phospho (ST)]";
        private const string STR_HEAVY_ONLY = "ALSI{+1}GFETC[+57]R"; 
        private const string STR_LIGHT_ONLY = "A{+1}E{+1}I{+1}D{+1}M{+1}[+695]L{+1}D{+1}I{+1}R{+4}";
        private const string STR_CYS_AND_OXI = "AFC(unimod:4)AVPWQGTM[+16]TLSK";
        private const string STR_CYS_OXI_PHOS = "AFC[UniMod:4]AVPWQGTM[oXi]T(UniMod:21)LSK";
        private const string STR_CYS_OXI_PHOS_CAP = "AFC(uNiMoD:4)AVPWQGTM[+16]T(UNIMOD:21)LSK";
        private static string STR_METOX_LONG_MASS { get { return "AFCSFQIYAVPWQGTM[+" + 15.99 + "49151234567890]TLSK"; } }
        private const string STR_AMMONIA_LOSS = "C[-17]AVPWQGTMTLSK";
        private const string STR_UNIMOD_LABEL = "APIPTALDTDSSK(UniMod:259)";
        private const string STR_UNIMOD_ALL = "A(UniMod:994)C(UniMod:994)D(UniMod:994)E(UniMod:994)F(UniMod:994)A(UniMod:994)C(UniMod:994)D(UniMod:994)E(UniMod:994)F(UniMod:994)";
        private const string STR_UNIMOD_TERMINUS = "PEMGFDLER(UniMod:298)";

        // Fails
        private const string STR_FAIL_MASS = "VAI{+42}LIPFR";
        private const string STR_FAIL_NAME = "VAILIP[Gibberish]FR";
        private const string STR_FAIL_EMPTY_MOD = "IHGF[]DLAAI[]NLQR";
        private const string STR_FAIL_EMPTY_MOD2 = "IHGF{}DLAAI{}NLQR";
        private static string STR_FAIL_NOT_A_NUMBER { get { return "IHGF[+" + 5.1 + "" + 1.5 + "]DLAAINLQR"; } }
        private const string STR_FAIL_DOUBLE_MOD = "V{+1}{+5}A{+1}I{+1}L{+1}I{+1}P{+1}F{+1}R{+4}{+1}";
        private const string STR_FAIL_OX_ON_D = "PEMGFD[Oxidation (M)]LER";
        private const string STR_FAIL_OX_TERM = "PEM[Oxidation (M) C-term]GFDLER";
        private const string STR_FAIL_UNIMOD = "AFC[+57]AVPWQGTM[OXI]T(UnimodBad:21)LSK";
        private const string STR_UNKNOWN_UNIMOD = "AFC[+57]AVPWQGTM[+16]T(Unimod:123456)LSK";
        private const string STR_FAIL_WRONG_AA_UNIMOD = "AFC[+57]AVPW(Unimod:21)QGTM[+16]TLSK";
        private const string STR_FAIL_UNIMOD_TERMINUS = "PEM(UniMod:298)GFDLER";

        private static readonly StaticMod METHIONINE_OXIDATION = new StaticMod("Methionine Oxidation", "M", null, "O");
        private static readonly StaticMod OXIDATION_M_GLOBAL = new StaticMod("Oxidation (M)", "M", null, "O");
        private static readonly StaticMod OXIDATION_M_C_TERM = new StaticMod("Oxidation (M) C-term", "M", ModTerminus.C, "O");
        private static readonly StaticMod LABEL15_N = new StaticMod("Label:15N", null, null, LabelAtoms.N15);
        private static readonly StaticMod MET_OX_ROUNDED = new StaticMod("Met Ox Rounded", "M", null, null, LabelAtoms.None, 16.0, 16.0);
        private static readonly StaticMod N_TERM_LABEL = new StaticMod("Label:13C(6)15N(2) (K)", "K", null, false, null, LabelAtoms.C13|LabelAtoms.N15, 
                                                                        RelativeRT.Matching, null,null, null, 259, "+08");

        private static List<string> _seqs;
        private static void InitSeqs()
        {
            _seqs = new List<string>
                {
                    STR_NO_MODS, STR_MOD_BY_NAME, STR_CYS_AND_OXI, STR_HEAVY_15, STR_HEAVY_15_F, STR_HEAVY_15_NOT_ALL,
                    STR_CYS_OXI_PHOS,
                    STR_MOD_BY_NAME_TERMINUS, STR_LIGHT_ONLY, STR_HEAVY_ONLY, STR_TERM_ONLY, STR_METOX_LONG_MASS,
                    STR_AMMONIA_LOSS, STR_CYS_OXI_PHOS_CAP, STR_UNIMOD_LABEL, STR_UNIMOD_ALL, STR_UNIMOD_TERMINUS
                };
        }   
    }
}
