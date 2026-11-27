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
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Quasar.Features;
using pwiz.Quasar.Features.Data;
using pwiz.Quasar.IO;

namespace pwiz.Quasar.Test
{
    /// <summary>
    /// A spectrum's peptide-like envelopes, re-found and matched to the run's search, for labeling (ported from
    /// skyline-glimpse's PeptideLikeEnvelopesTests, every assertion and expected value kept).
    /// </summary>
    [TestClass]
    public class PeptideLikeEnvelopesTest
    {
        private static readonly PatternEnvelopeFinder FINDER = new PatternEnvelopeFinder((300.0, 1, 1), (500.0, 2, 3), (700.0, 1, 1));

        /// <summary>Only the envelopes of persistent features, and only when they match the run's search.</summary>
        [TestMethod]
        public void TestPeptideLikeEnvelopesPersistent()
        {
            OnlyPersistentEnvelopesWithTheirPeaks();
            NothingWhenTheScanWasNotSearchedOrTheEnvelopesDiffer();
            ASearchReadFromAHkFileGetsItsPeaksBack();
            AnEnvelopeOfAnotherMassIsAnotherEnvelope();
            TheMs1ChargeRuleLeavesMs2Alone();
        }

        /// <summary>An envelope's label and its base peak.</summary>
        [TestMethod]
        public void TestPeptideLikeEnvelopeLabels()
        {
            AnEnvelopeIsLabeledWithItsMzThenItsCharge();
            TheBasePeakIsTheMatchedPeakAtTheBasePeakMz();
        }

        /// <summary>Hardklor finds exactly the search's envelopes again in every searched scan of a real run.</summary>
        [TestMethod]
        public void TestPeptideLikeEnvelopesOnSmallRaw()
        {
            var finder = new AnalyzerEnvelopeFinder(new PwizEnvelopeModel());
            var spectra = SmallRaw.Spectra;
            var (signal, headers) = Search(spectra, finder);
            var searched = ScanPass.IndicesOfLevel(headers, 1).Where(signal.Covers).ToArray();
            Assert.AreEqual(7, searched.Length);
            var labeled = 0;
            foreach (var i in searched)
            {
                var s = spectra[i];
                var persistent = PeptideLikeEnvelopes.Persistent(signal, i, s.Mz, s.Intensity, s.Header.Analyzer, finder);
                // Matched to the search: as many as the search linked into features in this scan.
                Assert.AreEqual(signal.FeatureOf[i].Count(id => id >= 0), persistent.Count);
                labeled += persistent.Count;
            }
            Assert.IsTrue(labeled > 100, $"{labeled} persistent envelopes");
        }

        private static (PeptideLikeSignal Signal, ScanHeader[] Headers) Search(IReadOnlyList<SpectrumData> spectra, IEnvelopeFinder finder)
        {
            using (var observer = new PeptideLikeObserver(finder, workers: 3))
            {
                var headers = ScanPass.Run(spectra, observer);
                Assert.IsTrue(observer.Completion.Wait(TimeSpan.FromMinutes(2)));
                return (observer.Completion.Result, headers);
            }
        }

        private static FakeRun Run()
        {
            // A 2+ envelope (500.0-501.0) in all three scans; a 1+ at 700 in scan 0 only; a 1+ at 300 in all three.
            var run = new FakeRun();
            for (var s = 0; s < 3; s++)
            {
                var mz = new List<double> { 300.0, 500.0, 500.5, 501.0 };
                var intensity = new List<double> { 30, 100, 50, 25 };
                if (s == 0)
                {
                    mz.Add(700);
                    intensity.Add(40);
                }
                run.Ms1(1 + s * 0.1, mz.ToArray(), intensity.ToArray());
            }
            return run;
        }

        private static void OnlyPersistentEnvelopesWithTheirPeaks()
        {
            var run = Run();
            var (signal, _) = Search(run.Spectra, FINDER);
            var s = run.ReadSpectrum(0);
            var found = PeptideLikeEnvelopes.Persistent(signal, 0, s.Mz, s.Intensity, MassAnalyzer.unknown, FINDER);
            // Not the lone 700.
            FeatureAssert.AreSequenceEqual(new[] { 1, 2 }, found.Select(e => e.Charge));
            FeatureAssert.AreJaggedEqual(new[] { new[] { 0 }, new[] { 1, 2, 3 } }, found.Select(e => e.PeakIndices));
            Assert.AreEqual(1, PeptideLikeEnvelopes.BasePeakIndex(found[1], s.Mz));
            // MS1 2+ only.
            FeatureAssert.AreSequenceEqual(new[] { 2 },
                PeptideLikeEnvelopes.Persistent(signal, 0, s.Mz, s.Intensity, MassAnalyzer.unknown, FINDER, 2).Select(e => e.Charge));
        }

        private static void NothingWhenTheScanWasNotSearchedOrTheEnvelopesDiffer()
        {
            var run = Run();
            var (signal, _) = Search(run.Spectra, FINDER);
            var s = run.ReadSpectrum(1);
            FeatureAssert.IsEmpty(PeptideLikeEnvelopes.Persistent(signal, 7, s.Mz, s.Intensity, MassAnalyzer.unknown, FINDER));      // not searched
            // Another finder (one envelope fewer): the envelopes no longer line up with the search's, so none are used.
            var other = new PatternEnvelopeFinder((500.0, 2, 3));
            FeatureAssert.IsEmpty(PeptideLikeEnvelopes.Persistent(signal, 1, s.Mz, s.Intensity, MassAnalyzer.unknown, other));
            // The same count but another envelope (a 3+ where the search had a 2+).
            var swapped = new PatternEnvelopeFinder((300.0, 1, 1), (500.0, 3, 3));
            FeatureAssert.IsEmpty(PeptideLikeEnvelopes.Persistent(signal, 1, s.Mz, s.Intensity, MassAnalyzer.unknown, swapped));
            // A spectrum read differently (a peak's intensity changed).
            var changed = (double[])s.Intensity.Clone();
            changed[1] = 101;
            FeatureAssert.IsEmpty(PeptideLikeEnvelopes.Persistent(signal, 1, s.Mz, changed, MassAnalyzer.unknown, FINDER));
        }

        private static void ASearchReadFromAHkFileGetsItsPeaksBack()
        {
            var run = Run();
            var (signal, headers) = Search(run.Spectra, FINDER);
            var dir = Directory.CreateTempSubdirectory("quasar-pl");
            try
            {
                var path = Path.Combine(dir.FullName, "run.hk");
                var stamp = new HkFile.Stamp(1, DateTime.UnixEpoch, headers.Length, "s");
                HkFile.Write(path, signal, stamp);
                var read = HkFile.TryRead(path, stamp, headers);
                Assert.IsFalse(read.HasPeaks);
                var s = run.ReadSpectrum(2);
                FeatureAssert.AreJaggedEqual(new[] { new[] { 0 }, new[] { 1, 2, 3 } },
                    PeptideLikeEnvelopes.Persistent(read, 2, s.Mz, s.Intensity, MassAnalyzer.unknown, FINDER).Select(e => e.PeakIndices));
            }
            finally
            {
                dir.Delete(true);
            }
        }

        private static void AnEnvelopeOfAnotherMassIsAnotherEnvelope()
        {
            var run = Run();
            var (signal, _) = Search(run.Spectra, FINDER);
            var s = run.ReadSpectrum(1);
            Assert.AreNotEqual(0, PeptideLikeEnvelopes.Persistent(signal, 1, s.Mz, s.Intensity, MassAnalyzer.unknown, FINDER).Count);
            FeatureAssert.IsEmpty(PeptideLikeEnvelopes.Persistent(signal, 1, s.Mz, s.Intensity, MassAnalyzer.unknown, new ShiftedMass(FINDER)));
        }

        private static void TheMs1ChargeRuleLeavesMs2Alone()
        {
            // A DIA window over three cycles with a 1+ envelope: it counts in MS2 even when MS1 counts 2+ only.
            var run = new FakeRun();
            for (var c = 0; c < 3; c++)
            {
                run.Ms1(1 + c * 0.1, new[] { 450.0 }, new[] { 10.0 });
                run.Ms2(1.05 + c * 0.1, 400, 410, new[] { 300.0, 301.0 }, new[] { 80.0, 20 });
            }
            var finder = new PatternEnvelopeFinder((300.0, 1, 2));
            var (signal, _) = Search(run.Spectra, finder);
            var s = run.ReadSpectrum(1);
            Assert.AreEqual(2, s.Header.MsLevel);
            FeatureAssert.AreSequenceEqual(new[] { 1 },
                PeptideLikeEnvelopes.Persistent(signal, 1, s.Mz, s.Intensity, MassAnalyzer.unknown, finder, 2).Select(e => e.Charge));
        }

        private static void AnEnvelopeIsLabeledWithItsMzThenItsCharge()
        {
            // 1082.5679 Da at 2+: (1082.5679 + 2 x 1.00727646688) / 2 = 542.291226467
            var envelope = new IsotopeEnvelope(1082.5679, 2, 1, 542.29, 0.99, 542, 544, new[] { 0 });
            FeatureAssert.AreEqualRounded(542.29122646688, PeptideLikeEnvelopes.MonoisotopicMz(envelope), 9);
            Assert.AreEqual("542.29 2+", PeptideLikeEnvelopes.Label(envelope, 2));
            Assert.AreEqual("542 2+", PeptideLikeEnvelopes.Label(envelope, 0));
            Assert.AreEqual("542.2912 2+", PeptideLikeEnvelopes.Label(envelope, 4));
            Assert.AreEqual("1083.57518 1+", PeptideLikeEnvelopes.Label(envelope with { Charge = 1 }, 5));   // 1083.575176...
            var culture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                Assert.AreEqual("542.29 2+", PeptideLikeEnvelopes.Label(envelope, 2));    // a decimal point in any language
            }
            finally
            {
                CultureInfo.CurrentCulture = culture;
            }
        }

        private static void TheBasePeakIsTheMatchedPeakAtTheBasePeakMz()
        {
            var mz = new[] { 499.0, 500.0, 500.5, 501.0 };
            var envelope = new IsotopeEnvelope(1000, 2, 1, 500.5, 0.99, 500, 501, new[] { 1, 2, 3 });   // base peak the middle one
            Assert.AreEqual(2, PeptideLikeEnvelopes.BasePeakIndex(envelope, mz));
            Assert.AreEqual(3, PeptideLikeEnvelopes.BasePeakIndex(envelope with { BasePeakMz = 501.0004 }, mz));
        }

        /// <summary>Returns another finder's envelopes with the mass moved a little: only the mass differs.</summary>
        private sealed class ShiftedMass : IEnvelopeFinder
        {
            private readonly IEnvelopeFinder _inner;

            public ShiftedMass(IEnvelopeFinder inner)
            {
                _inner = inner;
            }

            public bool CanSearch(MassAnalyzer analyzer) => _inner.CanSearch(analyzer);

            public IReadOnlyList<IsotopeEnvelope> Find(double[] mz, double[] intensity, MassAnalyzer analyzer) =>
                _inner.Find(mz, intensity, analyzer).Select(e => e with { MonoMass = e.MonoMass + 0.01 }).ToArray();
        }
    }
}
