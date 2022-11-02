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
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Crosslinking;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class CrosslinkModTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestTransitionGroupDocNodeGetNeutralFormula()
        {
            var peptide = new Peptide("PEPTIDE");
            var transitionGroup = new TransitionGroup(peptide, Adduct.SINGLY_PROTONATED, IsotopeLabelType.light);
            var srmSettings = SrmSettingsList.GetDefault();
            var transitionGroupDocNode = new TransitionGroupDocNode(transitionGroup, Annotations.EMPTY, srmSettings, null, null, ExplicitTransitionGroupValues.EMPTY, null, null, false);
            var moleculeOffset = transitionGroupDocNode.GetNeutralFormula(srmSettings, null);
            Assert.AreEqual("C34H53N7O15", moleculeOffset.Molecule.ToString());
        }

        [TestMethod]
        public void TestCrosslinkGetNeutralFormula()
        {
            var mainPeptide = new Peptide("MERCURY");
            var srmSettings = SrmSettingsList.GetDefault();
            srmSettings = srmSettings.ChangePeptideSettings(
                srmSettings.PeptideSettings.ChangeModifications(srmSettings.PeptideSettings.Modifications
                    .ChangeStaticModifications(new StaticMod[0])));
            var transitionGroup = new TransitionGroup(mainPeptide, Adduct.SINGLY_PROTONATED, IsotopeLabelType.light);
            var mainTransitionGroupDocNode = new TransitionGroupDocNode(transitionGroup, Annotations.EMPTY, srmSettings,
                null, null, ExplicitTransitionGroupValues.EMPTY, null, null, false);
            var unlinkedFormula = mainTransitionGroupDocNode.GetNeutralFormula(srmSettings, null);
            Assert.AreEqual("C37H61N13O11S2Se", unlinkedFormula.ToString());

            var linkedPeptide = new Peptide("ARSENIC");

            var linkedPeptideFormula = new TransitionGroupDocNode(new TransitionGroup(linkedPeptide, Adduct.SINGLY_PROTONATED, IsotopeLabelType.light), Annotations.EMPTY, srmSettings,
                null, null, ExplicitTransitionGroupValues.EMPTY, null, null, false).GetNeutralFormula(srmSettings, null);
            Assert.AreEqual("C30H53N11O12S", linkedPeptideFormula.ToString());
            var crosslinkMod = new StaticMod("disulfide", null, null, "-H2");
            var crosslink = new Crosslink(crosslinkMod, new []{new CrosslinkSite(0, 3), new CrosslinkSite(1, 2)});

            var explicitModsWithCrosslink = new ExplicitMods(mainPeptide,
                null,
                new TypedExplicitModifications[0]).ChangeCrosslinkStructure(new CrosslinkStructure(new []{linkedPeptide}, new[]{crosslink}));
            var crosslinkedFormula =
                mainTransitionGroupDocNode.GetNeutralFormula(srmSettings, explicitModsWithCrosslink);
            
            Assert.AreEqual("C67H112N24O23S3Se", crosslinkedFormula.Molecule.ToString());
        }

        [TestMethod]
        public void TestSingleAminoAcidLinkedPeptide()
        {
            var srmSettings = SrmSettingsList.GetDefault();
            var mainPeptide = new Peptide("A");
            var staticMod = new StaticMod("crosslinker", null, null, "-C2");
            var linkedPeptide = new Peptide("D");
            var mainTransitionGroup = new TransitionGroup(mainPeptide, Adduct.SINGLY_PROTONATED, IsotopeLabelType.light);
            var mainTransitionGroupDocNode = new TransitionGroupDocNode(mainTransitionGroup, 
                Annotations.EMPTY, srmSettings, null, null,
                ExplicitTransitionGroupValues.EMPTY, null, new TransitionDocNode[0], false);
            var modsWithoutLinkedPeptide = new ExplicitMods(mainPeptide, new[]{new ExplicitMod(0, staticMod), }, new TypedExplicitModifications[0]);
            Assert.AreEqual("C3H7NO2", AminoAcidFormulas.Default.GetFormula("A").ToString());
            Assert.AreEqual("C3H7NO2", mainTransitionGroupDocNode.GetNeutralFormula(srmSettings, null).Molecule.ToString());
            Assert.AreEqual("CH7NO2", mainTransitionGroupDocNode.GetNeutralFormula(srmSettings, modsWithoutLinkedPeptide).Molecule.ToString());
            Assert.AreEqual("C4H7NO4", AminoAcidFormulas.Default.GetFormula("D").ToString());
            var modsWithLinkedPeptide = new ExplicitMods(mainPeptide,
                null,
                null).ChangeCrosslinkStructure(CrosslinkStructure.ToPeptide(linkedPeptide, null, staticMod, 0, 0));
            Assert.AreEqual("C5H14N2O6", mainTransitionGroupDocNode.GetNeutralFormula(srmSettings, modsWithLinkedPeptide).Molecule.ToString());
            var mainComplexFragmentIon = NeutralFragmentIon.Simple(
                new Transition(mainTransitionGroup, IonType.precursor, mainPeptide.Length - 1, 0,
                    Adduct.SINGLY_PROTONATED), null);
            var linkedComplexFragmentIon = new SingleFragmentIon(new IonOrdinal(IonType.precursor, 0), null);
            var complexFragmentIon = linkedComplexFragmentIon.Prepend(mainComplexFragmentIon);
            var chargedIon =
                complexFragmentIon.MakeChargedIon(mainTransitionGroup, Adduct.SINGLY_PROTONATED, modsWithLinkedPeptide);
            var transition = chargedIon.MakeTransitionDocNode(srmSettings, modsWithLinkedPeptide, null);
            var sequenceMassCalc = new SequenceMassCalc(MassType.Monoisotopic);
            var expectedMz = sequenceMassCalc.GetPrecursorMass("A") + sequenceMassCalc.GetPrecursorMass("D") - 24 - BioMassCalc.MassProton;
            Assert.AreEqual(expectedMz, transition.Mz, .00001);
        }

        [TestMethod]
        public void TestTwoAminoAcidLinkedPeptide()
        {
            const string modName = "crosslinker";
            var srmSettings = SrmSettingsList.GetDefault();
            srmSettings = srmSettings.ChangeTransitionSettings(srmSettings.TransitionSettings.ChangeFilter(
                srmSettings.TransitionSettings.Filter
                    .ChangeFragmentRangeAll()
                    .ChangePeptideIonTypes(new[] {IonType.precursor, IonType.y, IonType.b})
            ));


            var mainPeptide = new Peptide("AD");
            var staticMod = new StaticMod(modName, null, null, "-C2");
            var linkedPeptide = new Peptide("EF");
            var mainTransitionGroup = new TransitionGroup(mainPeptide, Adduct.DOUBLY_PROTONATED, IsotopeLabelType.light);
            var mainTransitionGroupDocNode = new TransitionGroupDocNode(mainTransitionGroup,
                Annotations.EMPTY, srmSettings, null, null, ExplicitTransitionGroupValues.EMPTY, null,
                new TransitionDocNode[0], false);
            var modsWithLinkedPeptide = new ExplicitMods(mainPeptide, null, null)
                .ChangeCrosslinkStructure(CrosslinkStructure.ToPeptide(linkedPeptide, null, staticMod, 0, 1));
            Assert.AreEqual(1, srmSettings.PeptideSettings.Modifications.MaxNeutralLosses);
            var oneNeutralLossChoices = mainTransitionGroupDocNode.GetTransitions(
                srmSettings,
                modsWithLinkedPeptide,
                mainTransitionGroupDocNode.PrecursorMz,
                mainTransitionGroupDocNode.IsotopeDist,
                null,
                null,
                true).Select(transition => transition.ComplexFragmentIon.GetName()).ToList();
            var expectedFragmentIons = new[]
            {
                IonChain.FromIons(IonOrdinal.Precursor, IonOrdinal.Precursor),
                IonChain.FromIons(IonOrdinal.Precursor, new IonOrdinal(IonType.y, 1)),
                IonChain.FromIons(IonOrdinal.Y(1), IonOrdinal.Empty),
                IonChain.FromIons(IonOrdinal.B(1), IonOrdinal.Precursor),
                IonChain.FromIons(IonOrdinal.Empty, IonOrdinal.B(1)),
            };
            CollectionAssert.AreEquivalent(expectedFragmentIons, oneNeutralLossChoices);

            var legacySiteMap = new Dictionary<int, ImmutableList<ModificationSite>>();
            var legacyExplicitMods =
                new LegacyCrosslinkConverter(srmSettings, modsWithLinkedPeptide).ConvertToLegacyFormat(
                    legacySiteMap);
            Assert.IsNotNull(legacyExplicitMods);
            Assert.AreEqual(2, legacySiteMap.Count);
            Assert.AreEqual(ImmutableList.Empty<ModificationSite>(), legacySiteMap[0]);
            Assert.AreEqual(ImmutableList.Singleton(new ModificationSite(0, modName)), legacySiteMap[1]);
        }

        [TestMethod]
        public void TestComplexIonGetNeutralFormula()
        {
            var srmSettings = SrmSettingsList.GetDefault();
            var fullTransitionGroup = new TransitionGroupDocNode(
                new TransitionGroup(new Peptide("ELVIS"), Adduct.SINGLY_PROTONATED, IsotopeLabelType.light), 
                Annotations.EMPTY, srmSettings, null, null, ExplicitTransitionGroupValues.EMPTY, 
                null, null, false);
            var fullFormula = fullTransitionGroup.GetNeutralFormula(srmSettings, null);
            Assert.AreEqual("C25H45N5O9", fullFormula.Molecule.ToString());

            var hydrolysisDef = new StaticMod("hydrolysis", null, ModTerminus.C, "-H2O")
                .ChangeCrosslinkerSettings(CrosslinkerSettings.EMPTY);
            var mainPeptide = new Peptide("EL");
            var explicitMods = new ExplicitMods(mainPeptide, null, null).ChangeCrosslinkStructure(CrosslinkStructure.ToPeptide(new Peptide("VIS"), null, hydrolysisDef, 1, 0));
            var mainTransitionGroup = new TransitionGroupDocNode(
                new TransitionGroup(mainPeptide, Adduct.SINGLY_PROTONATED, IsotopeLabelType.light),
                Annotations.EMPTY, srmSettings, 
                explicitMods, null,
                ExplicitTransitionGroupValues.EMPTY,
                null, null, false);
            var mainFullFormula = mainTransitionGroup.GetNeutralFormula(srmSettings, explicitMods);
            Assert.AreEqual(fullFormula, mainFullFormula);
        }
        [TestMethod]
        public void TestPermuteComplexIons()
        {
            var mainPeptide = new Peptide("MERCURY");
            var srmSettings = SrmSettingsList.GetDefault();
            var transitionFilter = srmSettings.TransitionSettings.Filter;
            transitionFilter = transitionFilter
                .ChangeFragmentRangeAll()
                .ChangePeptideIonTypes(new[]{IonType.precursor,IonType.y, IonType.b});
            srmSettings =  srmSettings.ChangeTransitionSettings(
                srmSettings.TransitionSettings.ChangeFilter(transitionFilter));

            var transitionGroup = new TransitionGroup(mainPeptide, Adduct.SINGLY_PROTONATED, IsotopeLabelType.light);
            var crosslinkerDef = new StaticMod("disulfide", "C", null, "-H2");
            var linkedPeptide = new LegacyLinkedPeptide(new Peptide("ARSENIC"), 6, null);
            var crosslinkMod = new ExplicitMod(3, crosslinkerDef).ChangeLinkedPeptide(linkedPeptide);
            var explicitModsWithCrosslink = new ExplicitMods(mainPeptide, new[]{crosslinkMod}, new TypedExplicitModifications[0]);
            var transitionGroupDocNode = new TransitionGroupDocNode(transitionGroup, Annotations.EMPTY, srmSettings,
                explicitModsWithCrosslink, null, ExplicitTransitionGroupValues.EMPTY, null, null, false);
            var choices = transitionGroupDocNode.GetPrecursorChoices(srmSettings, explicitModsWithCrosslink, true)
                .Cast<TransitionDocNode>().ToArray();
            var complexFragmentIons = choices.Select(transition => transition.ComplexFragmentIon.GetName()).ToArray();

            Assert.AreNotEqual(0, complexFragmentIons.Length);
        }
        [TestMethod]
        public void TestCrosslinkModSerialization()
        {
            var settings = SrmSettingsList.GetDefault();
            var crosslinkerDef = new StaticMod("disulfide", null, null, "-H2")
                .ChangeCrosslinkerSettings(CrosslinkerSettings.EMPTY);
            settings = settings.ChangePeptideSettings(settings.PeptideSettings.ChangeModifications(
                settings.PeptideSettings.Modifications.ChangeStaticModifications(new[] {crosslinkerDef})));
            settings = settings.ChangeTransitionSettings(settings.TransitionSettings.ChangeFilter(
                settings.TransitionSettings.Filter
                    .ChangeFragmentRangeAll()
                    .ChangePeptideIonTypes(new[] { IonType.precursor, IonType.y, IonType.b })
            )); var mainPeptide = new Peptide("MERCURY");
            var transitionGroup = new TransitionGroup(mainPeptide, Adduct.DOUBLY_PROTONATED, IsotopeLabelType.light);
            var linkedPeptide = new Peptide("ARSENIC");//, 2, null);
            var explicitModsWithCrosslink =
                new ExplicitMods(mainPeptide, null, null).ChangeCrosslinkStructure(
                    CrosslinkStructure.ToPeptide(linkedPeptide, null, crosslinkerDef, 3, 2));
            var transitionGroupDocNode = new TransitionGroupDocNode(transitionGroup, Annotations.EMPTY, settings,
                explicitModsWithCrosslink, null, ExplicitTransitionGroupValues.EMPTY, null, null, true);
            
            var peptideDocNode = new PeptideDocNode(mainPeptide, settings, explicitModsWithCrosslink, null, ExplicitRetentionTimeInfo.EMPTY, new []{transitionGroupDocNode}, false);
            peptideDocNode = peptideDocNode.ChangeSettings(settings, SrmSettingsDiff.ALL);
            Assert.AreNotEqual(0, peptideDocNode.TransitionCount);
            var peptideGroupDocNode = new PeptideGroupDocNode(new PeptideGroup(), Annotations.EMPTY, "Peptides", null, new []{peptideDocNode});
            var srmDocument = (SrmDocument) new SrmDocument(settings).ChangeChildren(new[] {peptideGroupDocNode});
            VerifyLegacyFormatSupported(srmDocument);
            AssertEx.Serializable(srmDocument);
            AssertEx.Serializable(srmDocument, string.Empty, SkylineVersion.V20_2);
            string docXML = null;
            AssertEx.RoundTrip(srmDocument, ref docXML);
            Assert.IsNotNull(docXML);
        }

        [TestMethod]
        public void TestIncludesAaIndex()
        {
            var peptide = new Peptide("AD");
            var precursor = IonOrdinal.Precursor;
            Assert.IsTrue(precursor.IncludesAaIndex(peptide, 0));
            Assert.IsTrue(precursor.IncludesAaIndex(peptide, 1));
            var y1 = IonOrdinal.Y(1);
            Assert.AreEqual(1, y1.Ordinal);
            Assert.IsFalse(y1.IncludesAaIndex(peptide, 0));
            Assert.IsTrue(y1.IncludesAaIndex(peptide, 1));
            var b1 = IonOrdinal.B(1);
            Assert.AreEqual(1, b1.Ordinal);
            Assert.IsTrue(b1.IncludesAaIndex(peptide, 0));
            Assert.IsFalse(b1.IncludesAaIndex(peptide, 1));
        }

        [TestMethod]
        public void TestComplexIonMz()
        {
            var srmSettings = SrmSettingsList.GetDefault();
            var peptide = new Peptide("DLGEEHFKGLVLIAFSQYLQQCPFDEHVK");
            var linkedPeptide = new Peptide("LVNELTEFAKTCVADESHAGCEK");
            var transitionGroup = new TransitionGroup(peptide, Adduct.QUADRUPLY_PROTONATED, IsotopeLabelType.light);
            var crosslinker = new StaticMod("linker", "K", null, "C8H10O2");
            var explicitMods = new ExplicitMods(peptide, GetImplicitMods(srmSettings, peptide).StaticModifications, null)
                .ChangeCrosslinkStructure(CrosslinkStructure.ToPeptide(linkedPeptide, GetImplicitMods(srmSettings, linkedPeptide), crosslinker, 7, 9));
            var linkedTransition =
                new Transition(new TransitionGroup(linkedPeptide, Adduct.SINGLY_PROTONATED, IsotopeLabelType.light),
                    IonType.precursor, linkedPeptide.Length - 1, 0, Adduct.SINGLY_PROTONATED);
            var expectedMzs = new[]
            {
                Tuple.Create(IonType.b, 2, 1, 229.1183),
                Tuple.Create(IonType.b, 10, 3, 1291.2766)
            };
            foreach (var tuple in expectedMzs)
            {
                int offset = Transition.OrdinalToOffset(tuple.Item1, tuple.Item2, peptide.Sequence.Length);
                var transition = new Transition(transitionGroup, tuple.Item1, offset, 0, Adduct.FromChargeProtonated(tuple.Item3));
                var complexFragmentIon = NeutralFragmentIon.Simple(transition, null);
                if (transition.IncludesAaIndex(7))
                {
                    complexFragmentIon = SingleFragmentIon.FromTransition(linkedTransition).Prepend(complexFragmentIon);
                }
                else
                {
                    complexFragmentIon = SingleFragmentIon.EMPTY.Prepend(complexFragmentIon);
                }

                var chargedIon =
                    complexFragmentIon.MakeChargedIon(transitionGroup, Adduct.FromChargeProtonated(tuple.Item3), explicitMods);

                var complexTransitionDocNode = chargedIon.MakeTransitionDocNode(srmSettings, explicitMods, null);
                Assert.AreEqual(tuple.Item4, complexTransitionDocNode.Mz, .0001, "{0}{1}{2}", tuple.Item1, tuple.Item2,
                    Transition.GetChargeIndicator(Adduct.FromChargeProtonated(tuple.Item3)));
            }
        }

        private ExplicitMods GetImplicitMods(SrmSettings settings, Peptide peptide)
        {
            var peptideModifications = settings.PeptideSettings.Modifications;
            return new ExplicitMods(new PeptideDocNode(peptide), peptideModifications.StaticModifications, Settings.Default.StaticModList, peptideModifications.GetHeavyModifications(), Settings.Default.HeavyModList);
        }

        [TestMethod]
        public void TestCrosslinkPrecursorMz()
        {
            var srmSettings = SrmSettingsList.GetDefault();
            srmSettings = srmSettings.ChangePeptideSettings(
                srmSettings.PeptideSettings.ChangeModifications(
                    srmSettings.PeptideSettings.Modifications.ChangeStaticModifications(new StaticMod[0])));
            var mainPeptide = new Peptide("WQEGNVFSCSVMHEALHNHYTQK");
            var otherPeptide = new Peptide("NQVSLTCLVK");
            var crosslinkMod = new StaticMod("disulfide", "C", null, "-H2").ChangeCrosslinkerSettings(CrosslinkerSettings.EMPTY);

            var transitionGroup = new TransitionGroup(mainPeptide, Adduct.DOUBLY_PROTONATED, IsotopeLabelType.light);
            var explicitMods = new ExplicitMods(mainPeptide, null, null).ChangeCrosslinkStructure(CrosslinkStructure.ToPeptide(otherPeptide, null, crosslinkMod, 8, 6));
            
            var transitionGroupDocNode = new TransitionGroupDocNode(transitionGroup, Annotations.EMPTY, srmSettings, explicitMods, null, ExplicitTransitionGroupValues.EMPTY, null, null, false);
            Assert.AreEqual(1923.9111, transitionGroupDocNode.PrecursorMz, .001);
        }

        [TestMethod]
        public void TestLooplink()
        {
            var peptide = new Peptide("PEPTIDE");
            var srmSettings = SrmSettingsList.GetDefault();
            var transitionFilter = srmSettings.TransitionSettings.Filter;
            transitionFilter = transitionFilter
                .ChangeFragmentRangeAll()
                .ChangePeptideIonTypes(new[] { IonType.precursor, IonType.y, IonType.b });
            srmSettings = srmSettings.ChangeTransitionSettings(
                srmSettings.TransitionSettings.ChangeFilter(transitionFilter));

            var transitionGroup = new TransitionGroup(peptide, Adduct.SINGLY_PROTONATED, IsotopeLabelType.light);
            var crosslinkerDef = new StaticMod("dss", null, null, "C8H10O2");
            var explicitModsWithCrosslink = new ExplicitMods(peptide, null, null).ChangeCrosslinkStructure(
                new CrosslinkStructure(new Peptide[0],
                    new[]
                    {
                        new Crosslink(crosslinkerDef,
                            new[] {new CrosslinkSite(0, 2), new CrosslinkSite(0, 5),}),
                    }));
            var transitionGroupDocNode = new TransitionGroupDocNode(transitionGroup, Annotations.EMPTY, srmSettings,
                explicitModsWithCrosslink, null, ExplicitTransitionGroupValues.EMPTY, null, null, false);
            var modifiedSequence = CrosslinkedSequence.GetCrosslinkedSequence(srmSettings,
                new PeptideStructure(peptide, explicitModsWithCrosslink), IsotopeLabelType.light);
            Assert.AreEqual("PEPTIDE-[dss@3-6]", modifiedSequence.FullNames);

            var choices = transitionGroupDocNode.GetPrecursorChoices(srmSettings, explicitModsWithCrosslink, true)
                .Cast<TransitionDocNode>().ToArray();
            var complexFragmentIons = choices.Select(transition => transition.ComplexFragmentIon.GetName()).ToArray();
            // Make sure none of the transitions involve a cleavage in between the two ends of the looplink
            // PEpTIdE
            var yOrdinals = complexFragmentIons.Where(ion => ion.Ions[0].Type == IonType.y).Select(ion => ion.Ions[0].Ordinal)
                .Distinct().ToList();
            var bOrdinals = complexFragmentIons.Where(ion => ion.Ions[0].Type == IonType.b).Select(ion => ion.Ions[0].Ordinal)
                .Distinct().ToList();
            CollectionAssert.AreEquivalent(new[]{6,5,1}, yOrdinals);
            CollectionAssert.AreEquivalent(new[]{1,2,6}, bOrdinals);
        }

        [TestMethod]
        public void TestLooplinkCrosslinkLibraryKey()
        {
            var crosslinkLibraryKey = new CrosslinkLibraryKey(new []{new PeptideLibraryKey("AKIQDKEGIPPDQQR", 0)}, new[]{new CrosslinkLibraryKey.Crosslink("+138.0681", new[]{new []{2,6}})}, 3);
            var srmSettings = SrmSettingsList.GetDefault();
            srmSettings = srmSettings.ChangePeptideSettings(
                srmSettings.PeptideSettings.ChangeModifications(srmSettings.PeptideSettings.Modifications
                    .ChangeStaticModifications(
                        srmSettings.PeptideSettings.Modifications.StaticModifications
                            .Append(new StaticMod("DSS", "K", null, "C8H10O2").ChangeCrosslinkerSettings(
                                CrosslinkerSettings.EMPTY)).ToList()
                    )));
            var libKeyModificationMatcher = new LibKeyModificationMatcher();
            libKeyModificationMatcher.CreateMatches(srmSettings, new[] {new LibKey(crosslinkLibraryKey)},
                new MappedList<string, StaticMod>(), new MappedList<string, StaticMod>());

            var peptideDocNode = libKeyModificationMatcher.CreateDocNodeFromSettings(new LibKey(crosslinkLibraryKey),
                new Peptide("AKIQDKEGIPPDQQR"), SrmSettingsDiff.ALL, out _);
            Assert.IsNotNull(peptideDocNode);
            Assert.IsNotNull(peptideDocNode.ExplicitMods);
            Assert.AreEqual(0, peptideDocNode.ExplicitMods.CrosslinkStructure.LinkedPeptides.Count);
        }

        public static void VerifyLegacyFormatSupported(SrmDocument document)
        {
            foreach (var peptide in document.Peptides)
            {
                if (peptide.ExplicitMods == null || !peptide.ExplicitMods.HasCrosslinks)
                {
                    continue;
                }

                var legacyExplicitMods =
                    new LegacyCrosslinkConverter(document.Settings, peptide.ExplicitMods).ConvertToLegacyFormat(
                        new Dictionary<int, ImmutableList<ModificationSite>>());
                    
                var roundTripExplicitMods = legacyExplicitMods.ConvertFromLegacyCrosslinkStructure();
                AssertEx.AreEqual(peptide.ExplicitMods.CrosslinkStructure, roundTripExplicitMods.CrosslinkStructure);
            }
        }

        [TestMethod]
        public void TestIsConnectedFragmentIon()
        {
            var mainPeptide = new Peptide("MERCURY");
            var linkedPeptide1 = new Peptide("ARSENIC");
            var linkedPeptide2 = new Peptide("CALCIUM");


            var crosslinkMod = new StaticMod("disulfide", null, null, "-H2");
            var explicitMods = new ExplicitMods(mainPeptide, null, Array.Empty<TypedExplicitModifications>())
                .ChangeCrosslinkStructure(new CrosslinkStructure(new[] { linkedPeptide1, linkedPeptide2 },
                    new[]
                    {
                        new Crosslink(crosslinkMod, new[] { new CrosslinkSite(0, 3), new CrosslinkSite(2, 0) }),
                        new Crosslink(crosslinkMod, new[] { new CrosslinkSite(1, 6), new CrosslinkSite(2, 3) })
                    }));


            var peptideStructure = new PeptideStructure(mainPeptide, explicitMods);
            var b4_b2 = new NeutralFragmentIon(new[] { new IonOrdinal(IonType.b, 4), IonOrdinal.Empty, new IonOrdinal(IonType.b, 2) }, null);
            AssertEx.IsTrue(b4_b2.IsAllowed(peptideStructure));
            AssertEx.IsTrue(b4_b2.IsConnected(peptideStructure));
            var b2b2_= new NeutralFragmentIon(new[] { new IonOrdinal(IonType.b, 2), new IonOrdinal(IonType.b, 2), IonOrdinal.Empty},
                null);
            AssertEx.IsTrue(b2b2_.IsAllowed(peptideStructure));
            AssertEx.IsFalse(b2b2_.IsConnected(peptideStructure));
            var _y2y4 = new NeutralFragmentIon(
                new[] { IonOrdinal.Empty, new IonOrdinal(IonType.y, 2), new IonOrdinal(IonType.y, 4) }, null);
            AssertEx.IsTrue(_y2y4.IsAllowed(peptideStructure));
            AssertEx.IsTrue(_y2y4.IsConnected(peptideStructure));
            var b2y2y4 = new NeutralFragmentIon(new[] { new IonOrdinal(IonType.b, 2), new IonOrdinal(IonType.y, 2), 
                new IonOrdinal(IonType.y, 4) }, null);
            AssertEx.IsTrue(b2y2y4.IsAllowed(peptideStructure));
            AssertEx.IsFalse(b2y2y4.IsConnected(peptideStructure));
            var b4y2p = new NeutralFragmentIon(
                new[] { new IonOrdinal(IonType.b, 4), new IonOrdinal(IonType.y, 2), IonOrdinal.Precursor }, null);
            AssertEx.IsTrue(b4y2p.IsAllowed(peptideStructure));
            AssertEx.IsTrue(b4y2p.IsConnected(peptideStructure));
        }
    }
}
