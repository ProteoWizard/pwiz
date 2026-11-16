/*
 * Original author: Rita Chupalov <rita .at. uw .edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;
using ZedGraph;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Verifies the spectrum sequence-ruler behavior in all three spectrum hosts
    /// (the measured full-scan viewer, the library-match viewer, and the Library
    /// Explorer dialog): hovering an annotated peak shows the ruler for that ion
    /// series, and the Pin / Unpin / Unpin-All commands add and remove rulers.
    ///
    /// Ruler activation is mouse-over and context-menu driven, which a functional
    /// test cannot synthesize, so this drives the public test seams on each host
    /// (HoverRulerPeak / PinHoveredRuler / UnpinRuler / UnpinAllRulers) — the same
    /// code paths the mouse and menu invoke. The document is loaded once and the
    /// three hosts are exercised in succession.
    /// </summary>
    [TestClass]
    public class SpectrumSequenceRulerTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestSpectrumSequenceRuler()
        {
            Run(@"TestFunctional\EADZIonsTest.zip");
        }

        protected override void DoTest()
        {
            // Load the EAD document once. Its c/z ion series give both N- and C-terminal
            // rulers at multiple charges across all three hosts.
            OpenDocument("EADZIonsTest.sky");
            var transitionSettingsUI = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() =>
            {
                transitionSettingsUI.FragmentTypes = "c, z., z'";
                transitionSettingsUI.IonCount = 5;
            });
            OkDialog(transitionSettingsUI, transitionSettingsUI.OkDialog);
            RunUI(() =>
            {
                SkylineWindow.ShowZHIons(true);
                SkylineWindow.ShowZHHIons(true);
            });
            WaitForGraphs();
            FindNode((505.5810).ToString("F4", LocalizationHelper.CurrentCulture) + "+++");
            WaitForGraphs();

            TestLibraryMatchRuler();
            TestFullScanRuler();
            TestViewLibraryRuler();
        }

        // The library-match spectrum is shown for the selected precursor as soon as the
        // document is open, so no results import is required for this host.
        private void TestLibraryMatchRuler()
        {
            var graph = SkylineWindow.GraphSpectrum;
            WaitForConditionUI(() => RulerGraphReady(graph.RulerGraphItem));
            RunUI(() => VerifyRulerBehavior(graph.RulerGraphItem,
                peak => graph.HoverRulerPeak(peak),
                () => graph.PinHoveredRuler(),
                key => graph.UnpinRuler(key),
                () => graph.UnpinAllRulers(),
                () => graph.ToggleRulersEnabled()));
        }

        // Import results and open a measured full scan so the GraphFullScan host has a
        // stick spectrum with annotated peaks to hover. The ruler only applies in annotated
        // mode — in target-only mode the graph shows just the document transitions, leaving
        // no broader matched-peak set to align against.
        private void TestFullScanRuler()
        {
            ImportResults("FilteredScans\\LITV56_EAD" + ExtensionTestContext.ExtMzml);
            WaitForGraphs();
            ClickChromatogram(6.46, 2600.0);
            WaitForGraphs();

            var graph = SkylineWindow.GraphFullScan;
            RunUI(() => graph.ShowAnnotations(true));
            WaitForGraphs();
            WaitForConditionUI(() => graph.IsAnnotated && RulerGraphReady(graph.RulerGraphItem));
            RunUI(() => VerifyRulerBehavior(graph.RulerGraphItem,
                peak => graph.HoverRulerPeak(peak),
                () => graph.PinHoveredRuler(),
                key => graph.UnpinRuler(key),
                () => graph.UnpinAllRulers(),
                () => graph.ToggleRulersEnabled()));
        }

        private void TestViewLibraryRuler()
        {
            var dlg = ShowDialog<ViewLibraryDlg>(SkylineWindow.ViewSpectralLibraries);
            WaitForConditionUI(() => RulerGraphReady(dlg.RulerGraphItem));
            RunUI(() => VerifyRulerBehavior(dlg.RulerGraphItem,
                peak => dlg.HoverRulerPeak(peak),
                () => dlg.PinHoveredRuler(),
                key => dlg.UnpinRuler(key),
                () => dlg.UnpinAllRulers(),
                () => dlg.ToggleRulersEnabled()));
            OkDialog(dlg, dlg.CancelDialog);
        }

        private static bool RulerGraphReady(SpectrumGraphItem item)
        {
            return item != null && item.RulersApplicable && item.SpectrumInfo.PeaksMatched.Any();
        }

        /// <summary>
        /// Runs the host-independent ruler assertions against a populated spectrum graph
        /// item. Must be called on the UI thread. The delegates invoke the host's own
        /// hover / pin / unpin code paths.
        /// </summary>
        private static void VerifyRulerBehavior(
            SpectrumGraphItem item,
            Action<LibraryRankedSpectrumInfo.RankedMI> hover,
            Action pinHovered,
            Action<IonSeriesKey> unpin,
            Action unpinAll,
            Action toggleRulers)
        {
            Assert.IsNotNull(item);
            Assert.IsTrue(item.RulersApplicable);

            // Use peaks with a single matched ion so the expected ruler series is
            // unambiguous (no reliance on mass-error tie-breaking among co-matched ions).
            var peak1 = FindSingleIonPeak(item, _ => true);
            Assert.IsNotNull(peak1, "No singly-matched peak available to drive the ruler");
            var key1 = SingleIonKey(peak1);

            // No ruler before any hover.
            Assert.IsFalse(item.HoveredSeriesKey.HasValue);
            Assert.AreEqual(0, LadderCount(item));

            // 1. Hovering the peak shows exactly the ruler for that ion series.
            hover(peak1);
            Assert.AreEqual(key1, item.HoveredSeriesKey);
            Assert.AreEqual(1, LadderCount(item));

            // 2. Pinning keeps the ruler after the mouse moves away (hover cleared).
            pinHovered();
            Assert.IsTrue(item.PinnedSeriesKeys.Contains(key1));
            hover(null);
            Assert.IsFalse(item.HoveredSeriesKey.HasValue);
            Assert.AreEqual(1, LadderCount(item));

            // 3. A peak from a different ruler group, when the spectrum has one, adds a
            //    second independent ruler that can be pinned alongside the first.
            var peak2 = FindBestKeyPeak(item, k => !Equals(k.GroupKey, key1.GroupKey));
            if (peak2 != null)
            {
                var key2 = SpectrumGraphItem.GetBestSeriesKey(peak2).Value;
                hover(peak2);
                Assert.AreEqual(key2, item.HoveredSeriesKey);
                Assert.AreEqual(2, LadderCount(item)); // one pinned + one hovered

                pinHovered();
                hover(null);
                Assert.AreEqual(2, item.PinnedSeriesKeys.Count);
                Assert.AreEqual(2, LadderCount(item));

                // 4. Unpinning one ruler leaves the other in place.
                unpin(key1);
                Assert.IsFalse(item.PinnedSeriesKeys.Contains(key1));
                Assert.IsTrue(item.PinnedSeriesKeys.Contains(key2));
                Assert.AreEqual(1, LadderCount(item));
            }
            else
            {
                // 4. With only one ruler group present, unpinning the single ruler clears it.
                unpin(key1);
                Assert.IsFalse(item.PinnedSeriesKeys.Contains(key1));
                Assert.AreEqual(0, LadderCount(item));
            }

            // 5. Unpin All removes every remaining ruler.
            unpinAll();
            Assert.AreEqual(0, item.PinnedSeriesKeys.Count);
            Assert.AreEqual(0, LadderCount(item));

            // 6. Master Enable/Disable toggle. Pin one ruler, then disabling the feature
            //    clears the pinned state and renders nothing, and hovering is inert.
            //    Re-enabling does NOT bring the pinned ruler back (disable clears pins).
            hover(peak1);
            pinHovered();
            hover(null);
            Assert.AreEqual(1, item.PinnedSeriesKeys.Count);
            Assert.AreEqual(1, LadderCount(item));

            // The toggle's menu label reflects the current state.
            Assert.IsTrue(SpectrumGraphItem.RulersEnabled);
            Assert.AreEqual(GraphsResources.SequenceRulerMenu_DisableRulers, SpectrumGraphItem.RulerToggleMenuText);
            toggleRulers();
            Assert.IsFalse(SpectrumGraphItem.RulersEnabled);
            Assert.AreEqual(GraphsResources.SequenceRulerMenu_EnableRulers, SpectrumGraphItem.RulerToggleMenuText);
            Assert.AreEqual(0, item.PinnedSeriesKeys.Count);
            Assert.AreEqual(0, LadderCount(item));
            hover(peak1);
            Assert.IsFalse(item.HoveredSeriesKey.HasValue);
            Assert.AreEqual(0, LadderCount(item));

            toggleRulers();
            Assert.IsTrue(SpectrumGraphItem.RulersEnabled);
            Assert.AreEqual(GraphsResources.SequenceRulerMenu_DisableRulers, SpectrumGraphItem.RulerToggleMenuText);
            Assert.AreEqual(0, item.PinnedSeriesKeys.Count);
            Assert.AreEqual(0, LadderCount(item));
        }

        // Finds a matched peak that resolves to exactly one ion and whose series key
        // satisfies the predicate, or null when none qualifies.
        private static LibraryRankedSpectrumInfo.RankedMI FindSingleIonPeak(
            SpectrumGraphItem item, Func<IonSeriesKey, bool> match)
        {
            foreach (var rmi in item.SpectrumInfo.PeaksMatched)
            {
                if (rmi.MatchedIons == null || rmi.MatchedIons.Count != 1)
                    continue;
                if (match(SingleIonKey(rmi)))
                    return rmi;
            }
            return null;
        }

        private static IonSeriesKey SingleIonKey(LibraryRankedSpectrumInfo.RankedMI rmi)
        {
            var mfi = rmi.MatchedIons[0];
            return new IonSeriesKey(mfi.IonType, mfi.Charge.AdductCharge, mfi.Losses);
        }

        // Finds a matched peak whose best (lowest mass error) ion series satisfies the
        // predicate — the same resolution the hovered ruler uses — or null when none match.
        private static LibraryRankedSpectrumInfo.RankedMI FindBestKeyPeak(
            SpectrumGraphItem item, Func<IonSeriesKey, bool> match)
        {
            foreach (var rmi in item.SpectrumInfo.PeaksMatched)
            {
                var key = SpectrumGraphItem.GetBestSeriesKey(rmi);
                if (key.HasValue && match(key.Value))
                    return rmi;
            }
            return null;
        }

        // Number of ruler ladders the graph item renders for its current hovered/pinned state.
        private static int LadderCount(SpectrumGraphItem item)
        {
            var annotations = new GraphObjList();
            item.AddPreCurveAnnotations(null, null, null, annotations);
            return annotations.OfType<AminoAcidLadderObj>().Count();
        }
    }
}
