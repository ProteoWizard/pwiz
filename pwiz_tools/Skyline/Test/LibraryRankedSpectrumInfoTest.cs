/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class LibraryRankedSpectrumInfoTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestLibraryRankedNeutralLosses()
        {
            var sequence = "ELVISLIVES";
            var srmSettings = SrmSettingsList.GetDefault();
            var peptideModifications = srmSettings.PeptideSettings.Modifications;
            var lossModification =
                new StaticMod("Some Neutral Losses", null, null, false, "", LabelAtoms.None, RelativeRT.Matching, 0, 0,
                    new[]
                    {
                        new FragmentLoss(null, 17, 17),
                        new FragmentLoss(null, 19, 19),
                        new FragmentLoss(null, 23, 23)
                    });
            
            
            peptideModifications = peptideModifications.ChangeStaticModifications(peptideModifications
                    .StaticModifications.Append(lossModification).ToList())
                .ChangeMaxNeutralLosses(3);
            srmSettings = srmSettings.ChangePeptideSettings(
                    srmSettings.PeptideSettings.ChangeModifications(peptideModifications))
                .ChangeTransitionSettings(srmSettings.TransitionSettings.ChangeFilter(srmSettings.TransitionSettings
                    .Filter.ChangePeptideIonTypes(new[]
                    {
                        IonType.precursor, IonType.y, IonType.b
                    }).ChangePeptidePrecursorCharges(
                        new[]{Adduct.DOUBLY_PROTONATED, Adduct.TRIPLY_PROTONATED })
                    .ChangePeptideProductCharges(new[]{Adduct.SINGLY_PROTONATED})));
            
            var peptide = new Peptide(sequence);
            var explicitMods = new ExplicitMods(peptide,
                Enumerable.Range(0, sequence.Length).Select(indexAa => new ExplicitMod(indexAa, lossModification))
                    .ToList(), Array.Empty<TypedExplicitModifications>());
            var transitionGroup = new TransitionGroup(peptide, Adduct.DOUBLY_PROTONATED, IsotopeLabelType.light);
            var firstLossOnly = new TransitionLosses(lossModification.Losses.Take(1)
                    .Select(fragmentLoss => new TransitionLoss(lossModification, fragmentLoss, MassType.Monoisotopic))
                    .ToList(),
                MassType.Monoisotopic);
            var firstTwoLosses = new TransitionLosses(lossModification.Losses.Take(2)
                    .Select(fragmentLoss => new TransitionLoss(lossModification, fragmentLoss, MassType.Monoisotopic))
                    .ToList(),
                MassType.Monoisotopic);
            var allThreeLosses = new TransitionLosses(
                lossModification.Losses
                    .Select(fragmentLoss => new TransitionLoss(lossModification, fragmentLoss, MassType.Monoisotopic))
                    .ToList(),
                MassType.Monoisotopic);
            // Make a fake spectrum with a bunch of m/z's between 400 and 1400 with intensities equal to 1
            var mzIntensities = new List<SpectrumPeaksInfo.MI>();
            mzIntensities.AddRange(Enumerable.Range(400, 1000).Select(i=>new SpectrumPeaksInfo.MI()
            {
                Mz = i,
                Intensity = 1
            }));

            // Calculate the m/z's for some transitions
            var expectedIons = new[]
            {
                MakeMatchedFragmentIon(srmSettings, transitionGroup, explicitMods, IonType.y, 6, null, Adduct.SINGLY_PROTONATED),
                MakeMatchedFragmentIon(srmSettings, transitionGroup, explicitMods, IonType.b, 3, null, Adduct.SINGLY_PROTONATED),
                MakeMatchedFragmentIon(srmSettings, transitionGroup, explicitMods, IonType.precursor, sequence.Length, allThreeLosses, Adduct.DOUBLY_PROTONATED),
                MakeMatchedFragmentIon(srmSettings, transitionGroup, explicitMods, IonType.y, 4, firstTwoLosses, Adduct.SINGLY_PROTONATED),
                MakeMatchedFragmentIon(srmSettings, transitionGroup, explicitMods, IonType.b, 5, firstLossOnly, Adduct.SINGLY_PROTONATED)
            };

            // Add each of those calculated m/z's to the spectrum in reverse order with increasing intensity value
            foreach (var expectedIon in expectedIons.Reverse())
            {
                mzIntensities.Add(new SpectrumPeaksInfo.MI
                {
                    Mz = expectedIon.PredictedMz,
                    Intensity = mzIntensities.Count
                });
            }
            var spectrumPeaksInfo = new SpectrumPeaksInfo(mzIntensities.OrderBy(mi => mi.Mz).ToArray());

            var transitionGroupDocNode = new TransitionGroupDocNode(transitionGroup, Annotations.EMPTY, srmSettings,
                explicitMods, null, ExplicitTransitionGroupValues.EMPTY, null, Array.Empty<TransitionDocNode>(), false);
            var libraryRankedSpectrumInfo = LibraryRankedSpectrumInfo.NewLibraryRankedSpectrumInfo(spectrumPeaksInfo,
                IsotopeLabelType.light, transitionGroupDocNode, srmSettings, null, false, -1);
            
            // The highest ranked peaks that we get back should correspond to the expectedIon's that we added
            var rankedPeaks = libraryRankedSpectrumInfo.PeaksRanked.OrderBy(rankedMI=>rankedMI.Rank).ToList();
            if (rankedPeaks.Count < expectedIons.Length)
            {
                Assert.Fail("Ranked peaks has only {0} items", rankedPeaks.Count);
            }
            for (int i = 0; i < expectedIons.Length; i++)
            {
                var expectedIon = expectedIons[i];
                var rankedPeak = rankedPeaks[i];
                var matchedIon = rankedPeak.MatchedIons.FirstOrDefault(ion=>Equals(ion.PredictedMz, expectedIon.PredictedMz));
                if (matchedIon == null)
                {
                    Assert.Fail("Ranked peak #{0} has no matched ion with m/z {1}", i, expectedIon.PredictedMz);
                }
                AssertEx.AreEqual(expectedIon.IonType, matchedIon.IonType);
                AssertEx.AreEqual(expectedIon.Ordinal, matchedIon.Ordinal);
                AssertEx.AreEqual(expectedIon.Losses, matchedIon.Losses);
            }
        }

        private MatchedFragmentIon MakeMatchedFragmentIon(SrmSettings settings, TransitionGroup transitionGroup, ExplicitMods explicitMods, IonType ionType, int ordinal, TransitionLosses losses, Adduct charge)
        {
            var fragmentCalc = settings.GetFragmentCalc(IsotopeLabelType.light, explicitMods);
            int offset = Transition.OrdinalToOffset(ionType, ordinal, transitionGroup.Peptide.Length);
            var transition = new Transition(transitionGroup, ionType,
                offset, 0, charge);
            var mass = fragmentCalc.GetFragmentMass(transition, null);
            var mz = charge.MzFromNeutralMass(mass - (losses?.Mass ?? 0));
            return new MatchedFragmentIon(ionType, ordinal, charge, null, losses, mz);
        }
    }
}
