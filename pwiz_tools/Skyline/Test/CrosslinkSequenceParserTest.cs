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

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
    public class CrosslinkSequenceParserTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestCrosslinkSequenceParser()
        {
            CrosslinkLibraryKey libKey =
                CrosslinkSequenceParser.ParseCrosslinkLibraryKey("VTIAQGGVLPNIQAVLLPKK-TESHHKACGK-[+138.0681@19,6]", 1);
            Assert.AreEqual(2, libKey.PeptideLibraryKeys.Count);
            Assert.AreEqual("VTIAQGGVLPNIQAVLLPKK", libKey.PeptideLibraryKeys[0].ModifiedSequence);
            Assert.AreEqual("TESHHKACGK", libKey.PeptideLibraryKeys[1].ModifiedSequence);
            Assert.AreEqual(1, libKey.Crosslinks.Count);
            Assert.AreEqual("+138.0681", libKey.Crosslinks[0].Name);
            Assert.AreEqual(2, libKey.Crosslinks[0].Positions.Count);
            Assert.AreEqual(ImmutableList.Singleton(19), libKey.Crosslinks[0].Positions[0]);
            Assert.AreEqual(ImmutableList.Singleton(6), libKey.Crosslinks[0].Positions[1]);
        }

        [TestMethod]
        public void TestCrosslinkWithModifications()
        {
            CrosslinkLibraryKey libKey = CrosslinkSequenceParser.ParseCrosslinkLibraryKey(
                "C[+57.02146]C[+57.02146]TKPESER-EKVLTSSAR-[+138.0681@4,2]", 1);
            Assert.AreEqual(2, libKey.PeptideLibraryKeys.Count);
            Assert.AreEqual("C[+57.02146]C[+57.02146]TKPESER", libKey.PeptideLibraryKeys[0].ModifiedSequence);
            Assert.AreEqual("EKVLTSSAR", libKey.PeptideLibraryKeys[1].ModifiedSequence);
            Assert.AreEqual("+138.0681", libKey.Crosslinks[0].Name);
            Assert.AreEqual(2, libKey.Crosslinks[0].Positions.Count);
            Assert.AreEqual(ImmutableList.Singleton(4), libKey.Crosslinks[0].Positions[0]);
            Assert.AreEqual(ImmutableList.Singleton(2), libKey.Crosslinks[0].Positions[1]);
        }

        [TestMethod]
        public void TestCrosslinkIsSupportedBySkyline()
        {
            VerifySupported("YGPPCPPCPAPEFLGGPSVFLFPPKPK-YGPPCPPCPAPEFLGGPSVFLFPPKPK-[-2.01565@5,5]", true);
            // Two crosslinks between the same pair of peptides
            VerifySupported("YGPPCPPCPAPEFLGGPSVFLFPPKPK-YGPPCPPCPAPEFLGGPSVFLFPPKPK-[-2.01565@5,5][-2.01565@8,8]", true);
            VerifySupported("YGPPCPPCPAPEFLGGPSVFLFPPKPK-YGPPCPPCPAPEFLGGPSVFLFPPKPK-[-2.01565@8-5,*][-2.01565@5,5]", true);
            VerifySupported("PEPTIDEA-PEPTIDEB-PEPTIDEC-[-2@2,2,*][-2@*,2,3]", true);
            // Crosslinks forming a ring that cannot be represented as a tree
            VerifySupported("PEPTIDEA-PEPTIDEB-PEPTIDEC-[-2@2,2,*][-2@2,*,3][-2@*,2,3]", true);
            VerifySupported("PEPTIDEA-PEPTIDEB-PEPTIDEC-[-2@2,2,*][-2@2,*,3]", true);
            // One peptide that is not connected to the rest of the peptides.
            VerifySupported("PEPTIDEA-PEPTIDEB-PEPTIDEC-[-2@2,*,3]", false);
            // Zero is not a valid amino acid position
            VerifySupported("VTKIFVDEGPSM[+16]K-VTKIFVDEGPSM[+16]K-[DSBU@1,0]", false);
            VerifySupported("MKKDIHPKYEEITASC[+57]SC[+57]GNVMK-[DSBU@0-2]", false);
            // Amino acid position greater than peptide sequence length
            VerifySupported("VTKIFVDEGPSM[+16]K-VTKIFVDEGPSM[+16]K-[DSBU@14,1]", false);
        }

        private static void VerifySupported(string libKeyString, bool expectedSupported)
        {
            var libKey = CrosslinkSequenceParser.ParseCrosslinkLibraryKey(libKeyString, 1);
            Assert.AreEqual(expectedSupported, libKey.IsSupportedBySkyline());
        }

        /// <summary>
        /// Verifies that when a crosslinked peptide's precursor m/z is close the the MaxMz instrument settings,
        /// the user only gets the MS1 precursor transitions which are less than the MaxMz.
        /// </summary>
        [TestMethod]
        public void TestCrosslinkedPeptideCloseToMaxMz()
        {
            var crosslinkSequence = "DAIATVNKQEDANFSNNAMAEAFK-KGEDVEK-[DSBU@5,1]";
            var srmSettings = SrmSettingsList.GetDefault();
            var crosslinkMod =
                new StaticMod("DSBU", "K", null, "C9O3N2H12").ChangeCrosslinkerSettings(CrosslinkerSettings.EMPTY);
            var peptideSettings = srmSettings.PeptideSettings;
            peptideSettings = peptideSettings.ChangeModifications(
                peptideSettings.Modifications.ChangeStaticModifications(
                    ImmutableList.ValueOf(peptideSettings.Modifications.StaticModifications.Append(crosslinkMod))));
            srmSettings = srmSettings.ChangePeptideSettings(peptideSettings);
            var transitionSettings = srmSettings.TransitionSettings;
            var transitionFilter = transitionSettings.Filter;
            transitionFilter = transitionFilter.ChangePeptideIonTypes(new[] {IonType.precursor});
            transitionSettings = transitionSettings
                .ChangeFilter(transitionFilter)
                .ChangeFullScan(transitionSettings.FullScan
                    .ChangePrecursorIsotopes(FullScanPrecursorIsotopes.Count, 3, IsotopeEnrichmentsList.GetDefault())
                    .ChangePrecursorResolution(FullScanMassAnalyzerType.tof, 70000, null));
            foreach (var maxMz in new[] {1800, 1801, 1802})
            {
                srmSettings = srmSettings.ChangeTransitionSettings(transitionSettings.ChangeInstrument(transitionSettings.Instrument.ChangeMaxMz(maxMz)));
                var modificationMatcher = new ModificationMatcher();
                var defStaticMods = new MappedList<string, StaticMod>();
                defStaticMods.AddRange(StaticModList.GetDefaultsOn());
                defStaticMods.Add(crosslinkMod);
                modificationMatcher.CreateMatches(srmSettings, new[] { crosslinkSequence }, defStaticMods, new MappedList<string, StaticMod>());
                var peptideDocNode = modificationMatcher.GetModifiedNode(crosslinkSequence);
                Assert.AreEqual(1, peptideDocNode.TransitionGroupCount);
                var transitionGroupDocNode = peptideDocNode.TransitionGroups.First();
                Assert.AreEqual(1799.85, transitionGroupDocNode.Transitions.First().Mz, .01);
                foreach (var transition in transitionGroupDocNode.Transitions)
                {
                    Assert.IsTrue(transition.Mz <= maxMz, "{0} cannot be greater than {1}", transition.Mz, maxMz);
                }
            }
        }
    }
}
