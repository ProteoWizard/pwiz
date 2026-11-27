/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on skyline-glimpse (https://github.com/maccoss/skyline-glimpse)
 *   by Michael J. MacCoss, MacCoss Lab, Department of Genome Sciences, UW
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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Quasar.Features;
using pwiz.Quasar.Features.Data;

namespace pwiz.Quasar.Test
{
    /// <summary>
    /// The peptide-like signal: envelopes found on worker threads during the pass, linked after it (ported from
    /// skyline-glimpse's PeptideLikeTests, every assertion and expected value kept).
    /// </summary>
    [TestClass]
    public class PeptideLikeTest
    {
        /// <summary>What is peptide-like: persistent envelopes' peaks, each peak once, by the linking settings.</summary>
        [TestMethod]
        public void TestPeptideLikeSignal()
        {
            PersistentMs1EnvelopesArePeptideLike();
            TheLinkingSettingsDecideWhatPersists();
            APeakInTwoEnvelopesCountsOnce();
            ScanEnvelopesKeepEachEnvelopesPeaks();
        }

        /// <summary>Which scans are searched: centroided MS1, DIA windows that repeat, analyzers the finder takes.</summary>
        [TestMethod]
        public void TestPeptideLikeScanSelection()
        {
            ProfileSpectraAreNotSearched();
            DiaWindowsPersistOnTheirOwnAndMs2CountsSinglyCharged();
            AWindowSeenOnceIsNotSearched();
            ADdaRunHasNoMs2Signal();
            ScansTheFinderCannotSearchAreLeftOut();
        }

        /// <summary>A failing spectrum stops neither the search nor the read; an abandoned pass cancels the search.</summary>
        [TestMethod]
        public void TestPeptideLikeFailureAndCancellation()
        {
            AFailedSpectrumNeitherStopsTheSearchNorTheRead();
            AnAbandonedPassCancelsTheSearch();
        }

        private static PeptideLikeSignal Run(FakeRun run, IEnvelopeFinder finder, KronikSettings linking = null)
        {
            return Run(run.Spectra, finder, linking);
        }

        private static PeptideLikeSignal Run(IReadOnlyList<SpectrumData> spectra, IEnvelopeFinder finder, KronikSettings linking = null)
        {
            using (var observer = new PeptideLikeObserver(finder, linking, 3))
            {
                ScanPass.Run(spectra, observer);
                return observer.Completion.Wait(TimeSpan.FromSeconds(30)) ? observer.Completion.Result : throw new TimeoutException();
            }
        }

        private static void PersistentMs1EnvelopesArePeptideLike()
        {
            // A 2+ envelope (500.0, 500.5, 501.0) in four scans; a 1+ envelope at 700 in three; a 3+ one at 800 in one.
            var run = new FakeRun();
            for (var s = 0; s < 4; s++)
            {
                var mz = new List<double> { 500.0, 500.5, 501.0 };
                var intensity = new List<double> { 100, 50, 25 };
                if (s < 3)
                {
                    mz.Add(700);
                    intensity.Add(40);
                }
                if (s == 0)
                {
                    mz.Add(800);
                    intensity.Add(1000);
                }
                run.Ms1(1 + s * 0.1, mz.ToArray(), intensity.ToArray());
            }
            var signal = Run(run, new PatternEnvelopeFinder((500.0, 2, 3), (700, 1, 1), (800, 3, 1)));

            FeatureAssert.AreSequenceEqual(new[] { 215.0, 215, 215, 175 }, signal.IntensityByScan());   // 175 + the 1+ at 700
            FeatureAssert.AreSequenceEqual(new[] { 175.0, 175, 175, 175 }, signal.IntensityByScan(2));
            FeatureAssert.AreSequenceEqual(new[] { 0.0, 0, 0, 0 }, signal.IntensityByScan(3));         // the 3+ is in one scan only
            Assert.AreSame(signal.IntensityByScan(2), signal.IntensityByScan(2));                        // computed once
            FeatureAssert.AreSequenceEqual(new[] { 0, 1, 2, 3 }, signal.PeptideLikePeaks(0));           // not the 800 peak (index 4)
            FeatureAssert.AreSequenceEqual(new[] { 0, 1, 2 }, signal.PeptideLikePeaks(0, 2));
            Assert.AreEqual(2, signal.Features.Count);
            foreach (var f in signal.Features)
                Assert.AreEqual(0, f.Series);
            FeatureAssert.AreSequenceEqual(new[] { 0, 1, 2, 3 }, FeatureAssert.Single(signal.Series));
            Assert.IsFalse(signal.IsDda);
        }

        private static void TheLinkingSettingsDecideWhatPersists()
        {
            // An envelope in two scans: not peptide-like with Kronik's 3, peptide-like when 2 scans are enough.
            var run = new FakeRun()
                .Ms1(1.0, new[] { 500.0, 500.5 }, new[] { 100.0, 50 })
                .Ms1(1.1, new[] { 500.0, 500.5 }, new[] { 100.0, 50 });
            var finder = new PatternEnvelopeFinder((500.0, 2, 2));
            FeatureAssert.IsEmpty(Run(run, finder).Features);
            FeatureAssert.Single(Run(run, finder, KronikSettings.Default with { MinScans = 2 }).Features);
        }

        private static void AFailedSpectrumNeitherStopsTheSearchNorTheRead()
        {
            // 600 spectra through one worker (more than its 256-spectrum queue); every other one makes the finder throw.
            var run = new FakeRun();
            for (var i = 0; i < 600; i++)
                run.Ms1(1 + i * 0.01, new[] { i % 2 == 0 ? 500.0 : 600.0 }, new[] { 10.0 });
            using (var observer = new PeptideLikeObserver(new FailingFinder(500.0), workers: 1))
            {
                var read = Task.Run(() => ScanPass.Run(run.Spectra, observer));
                Assert.IsTrue(read.Wait(TimeSpan.FromSeconds(30)), "the read finished");
                Assert.IsTrue(observer.Completion.Wait(TimeSpan.FromSeconds(30)), "the search finished");
                var signal = observer.Completion.Result;
                Assert.AreEqual(300, signal.FailedScans);
                Assert.AreEqual(600, signal.IntensityByScan().Count(v => v == 0));      // failed scans: searched, no envelopes
            }
        }

        private static void ProfileSpectraAreNotSearched()
        {
            var run = new FakeRun();
            for (var s = 0; s < 3; s++)
                run.Ms1(1 + s * 0.1, new[] { 500.0, 500.5, 501.0 }, new[] { 100.0, 50, 25 });
            // The same spectra, marked as profile data.
            var profile = run.Spectra.Select(s => new SpectrumData(s.Header with { Centroided = false }, s.Mz, s.Intensity, s.Details)).ToArray();
            var finder = new PatternEnvelopeFinder((500.0, 2, 3));
            using (var observer = new PeptideLikeObserver(finder, workers: 2))
            {
                ScanPass.Run(profile, observer);
                Assert.IsTrue(observer.Completion.Wait(TimeSpan.FromSeconds(30)));
                Assert.AreEqual(0, finder.Calls);
                Assert.IsFalse(observer.Completion.Result.CoversAny);
            }
        }

        private static void APeakInTwoEnvelopesCountsOnce()
        {
            // Envelopes 500.0 (2+, three peaks) and 501.0 (1+, two peaks) share the 501.0 peak.
            var run = new FakeRun();
            for (var s = 0; s < 3; s++)
                run.Ms1(1 + s * 0.1, new[] { 500.0, 500.5, 501.0, 502.0 }, new[] { 100.0, 50, 25, 5 });
            var signal = Run(run, new PatternEnvelopeFinder((500.0, 2, 3), (501.0, 1, 2)));
            Assert.AreEqual(180.0, signal.IntensityByScan()[0]);   // 100 + 50 + 25 + 5, the shared 25 once
        }

        private static void DiaWindowsPersistOnTheirOwnAndMs2CountsSinglyCharged()
        {
            // Three cycles of MS1 + windows A (400-410) and B (410-420). A has a 1+ envelope every cycle; B only once.
            var run = new FakeRun();
            for (var c = 0; c < 3; c++)
            {
                var rt = 1 + c * 0.1;
                run.Ms1(rt, new[] { 450.0 }, new[] { 10.0 });
                run.Ms2(rt + 0.01, 400, 410, new[] { 300.0, 301.0 }, new[] { 80.0, 20 });
                run.Ms2(rt + 0.02, 410, 420, c == 1 ? new[] { 600.0 } : new[] { 650.0 }, new[] { 9.0 });
            }
            var finder = new PatternEnvelopeFinder((300.0, 1, 2), (600.0, 1, 1));
            var signal = Run(run, finder);

            var tic = signal.IntensityByScan(2);
            FeatureAssert.AreSequenceEqual(new[] { 0.0, 100, 0, 0, 100, 0, 0, 100, 0 }, tic);   // 1+ counts in MS2
            Assert.AreEqual(3, signal.Series.Count);                                               // MS1, A, B
            FeatureAssert.AreSequenceEqual(new[] { 1, 4, 7 }, signal.Series[1]);
            Assert.AreEqual(1, FeatureAssert.Single(signal.Features).Series);
            Assert.AreEqual(9, finder.Calls);                                                      // every scan searched once
        }

        private static void AWindowSeenOnceIsNotSearched()
        {
            var run = new FakeRun()
                .Ms1(1.0, new[] { 450.0 }, new[] { 10.0 })
                .Ms2(1.01, 400, 410, new[] { 300.0 }, new[] { 5.0 })
                .Ms2(1.02, 500, 510, new[] { 300.0 }, new[] { 5.0 })
                .Ms2(1.03, 400, 410, new[] { 300.0 }, new[] { 5.0 });
            var finder = new PatternEnvelopeFinder();
            var signal = Run(run, finder);
            Assert.AreEqual(3, finder.Calls);                       // the MS1 and both 400-410 scans
            Assert.IsTrue(signal.Covers(1));
            Assert.IsFalse(signal.Covers(2));
            Assert.IsTrue(double.IsNaN(signal.IntensityByScan()[2]));
            Assert.AreEqual(0.0, signal.IntensityByScan()[1]);
            FeatureAssert.IsEmpty(signal.PeptideLikePeaks(2));
        }

        private static void ADdaRunHasNoMs2Signal()
        {
            // A new isolation window for every MS2 scan: more than the DIA limit means DDA.
            var run = new FakeRun();
            for (var i = 0; i < PeptideLikeObserver.MAX_DIA_WINDOWS + 5; i++)
            {
                if (i % 50 == 0)
                    run.Ms1(1 + i * 0.001, new[] { 450.0 }, new[] { 10.0 });
                var lo = 400 + i * 0.37;
                run.Ms2(1 + i * 0.001 + 0.0005, lo, lo + 2, new[] { 300.0 }, new[] { 5.0 });
                if (i % 7 == 0)
                    run.Ms2(1 + i * 0.001 + 0.0007, lo, lo + 2, new[] { 300.0 }, new[] { 5.0 });   // some come back
            }
            var signal = Run(run, new PatternEnvelopeFinder());
            Assert.IsTrue(signal.IsDda);
            FeatureAssert.Single(signal.Series);                                                     // MS1 only
            foreach (var i in signal.Series[0])
                Assert.IsTrue(signal.Covers(i));
            Assert.AreEqual(signal.Series[0].Length, signal.IntensityByScan().Count(v => !double.IsNaN(v)));
        }

        private static void ScansTheFinderCannotSearchAreLeftOut()
        {
            // Orbitrap MS1 scans and ion trap MS1 scans in turn (a Tribrid), and ion trap DIA MS2.
            var run = new FakeRun();
            for (var c = 0; c < 3; c++)
            {
                run.Ms1(1 + c * 0.1, new[] { 500.0 }, new[] { 10.0 }, 10f, MassAnalyzer.orbitrap);
                run.Ms1(1.05 + c * 0.1, new[] { 500.0 }, new[] { 10.0 }, 10f, MassAnalyzer.ion_trap);
                run.Ms2(1.07 + c * 0.1, 400, 410, new[] { 300.0 }, new[] { 5.0 }, 10f, MassAnalyzer.ion_trap);
            }
            var finder = new PatternEnvelopeFinder((500.0, 2, 1)) { Refused = new[] { MassAnalyzer.ion_trap } };
            var signal = Run(run, finder);
            Assert.AreEqual(3, finder.Calls);                                        // the Orbitrap scans only
            FeatureAssert.AreSequenceEqual(new[] { 0, 3, 6 }, FeatureAssert.Single(signal.Series));
            Assert.IsTrue(signal.CoversAny);
            Assert.IsFalse(signal.Covers(1));
            FeatureAssert.AreSequenceEqual(new[] { 10.0, double.NaN, double.NaN, 10, double.NaN, double.NaN, 10, double.NaN, double.NaN },
                signal.IntensityByScan());

            // All ion trap (a Stellar): nothing searched.
            var stellar = new FakeRun()
                .Ms1(1.0, new[] { 500.0 }, new[] { 10.0 }, 10f, MassAnalyzer.ion_trap)
                .Ms1(1.1, new[] { 500.0 }, new[] { 10.0 }, 10f, MassAnalyzer.ion_trap);
            Assert.IsFalse(Run(stellar, finder).CoversAny);
        }

        private static void AnAbandonedPassCancelsTheSearch()
        {
            var observer = new PeptideLikeObserver(new PatternEnvelopeFinder(), workers: 2);
            observer.Begin(10, 0, 1);
            observer.Dispose();
            Assert.IsTrue(observer.Completion.IsCanceled);
        }

        private static void ScanEnvelopesKeepEachEnvelopesPeaks()
        {
            var envelopes = new[]
            {
                new IsotopeEnvelope(998, 2, 175, 500, 0.9, 500, 501, new[] { 2, 3, 4 }),
                new IsotopeEnvelope(500, 1, 30, 501, 0.9, 501, 502, new[] { 4, 7 }),
            };
            var compact = ScanEnvelopes.From(envelopes, new[] { 0.0, 1, 2, 3, 4, 5, 6, 7 });
            FeatureAssert.AreSequenceEqual(new[] { 2, 3, 4, 7 }, compact.PeakIndex);
            FeatureAssert.AreSequenceEqual(new[] { 2f, 3, 4, 7 }, compact.PeakIntensity);
            FeatureAssert.AreSequenceEqual(new ushort[] { 0, 1, 2 }, compact.PeaksOf(0).ToArray());
            FeatureAssert.AreSequenceEqual(new ushort[] { 2, 3 }, compact.PeaksOf(1).ToArray());
            FeatureAssert.AreSequenceEqual(new[] { new EnvelopeKey(998, 2, 175), new EnvelopeKey(500, 1, 30) }, compact.Keys);
            Assert.AreSame(ScanEnvelopes.Empty, ScanEnvelopes.From(Array.Empty<IsotopeEnvelope>(), Array.Empty<double>()));
        }

        /// <summary>Throws on spectra whose first peak is at the given m/z, finds nothing otherwise.</summary>
        private sealed class FailingFinder : IEnvelopeFinder
        {
            private readonly double _failAt;

            public FailingFinder(double failAt)
            {
                _failAt = failAt;
            }

            public bool CanSearch(MassAnalyzer analyzer) => true;

            public IReadOnlyList<IsotopeEnvelope> Find(double[] mz, double[] intensity, MassAnalyzer analyzer) =>
                Math.Abs(mz[0] - _failAt) < 1e-6 ? throw new InvalidOperationException("bad spectrum") : Array.Empty<IsotopeEnvelope>();
        }
    }
}
