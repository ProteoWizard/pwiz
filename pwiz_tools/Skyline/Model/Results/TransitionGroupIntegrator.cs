/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using pwiz.Common.Collections;
using pwiz.Common.PeakFinding;
using pwiz.Skyline.Model.DocSettings;
using System.Collections.Generic;
using System.Linq;
using pwiz.CommonMsData;

namespace pwiz.Skyline.Model.Results
{
    public class TransitionGroupIntegrator
    {
        private readonly PeakGroupIntegrator _peakGroupIntegrator;
        private readonly TransitionEntry[] _transitionEntries;
        private readonly ImmutableList<float> _interpolatedTimes;

        public TransitionGroupIntegrator(SrmSettings settings, TransitionGroupDocNode transitionGroupDocNode,
            ChromatogramSet chromatogramSet, ChromatogramGroupInfo chromatogramGroupInfo)
        {
            Settings = settings;
            TransitionGroupDocNode = transitionGroupDocNode;
            ChromatogramSet = chromatogramSet;
            ChromatogramGroupInfo = chromatogramGroupInfo;
            ChromFileInfoId = ChromatogramSet.FindFile(chromatogramGroupInfo.FilePath);
            var rawTimeIntensities = ChromatogramGroupInfo.TimeIntensitiesGroup as RawTimeIntensities;
            _peakGroupIntegrator =
                new PeakGroupIntegrator(Settings.TransitionSettings.FullScan.AcquisitionMethod, rawTimeIntensities?.TimeIntervals);
            _interpolatedTimes = rawTimeIntensities?.GetInterpolatedTimes();

            float tolerance = (float) Settings.TransitionSettings.Instrument.MzMatchTolerance;
            var transitionEntries = new List<TransitionEntry>();
            foreach (var transition in transitionGroupDocNode.Transitions)
            {
                var optStepChromatograms = ChromatogramGroupInfo.GetAllTransitionInfo(transition, tolerance,
                    ChromatogramSet.OptimizationFunction, TransformChrom.raw);
                var peakIntegrator = optStepChromatograms?.GetChromatogramForStep(0)?.MakePeakIntegrator(_peakGroupIntegrator, _interpolatedTimes);
                transitionEntries.Add(new TransitionEntry(optStepChromatograms, peakIntegrator));
            }

            _transitionEntries = transitionEntries.ToArray();
        }

        public SrmSettings Settings { get; }
        public TransitionGroupDocNode TransitionGroupDocNode { get; }
        public ChromatogramSet ChromatogramSet { get; }
        public ChromatogramGroupInfo ChromatogramGroupInfo { get; }
        public MsDataFileUri FilePath
        {
            get { return ChromatogramGroupInfo.FilePath; }
        }
        public ChromFileInfoId ChromFileInfoId { get; }

        public ChromPeak CalcPeak(Transition transition, PeakBounds peakBounds)
        {
            return CalcPeak(transition, 0, (float)peakBounds.StartTime, (float)peakBounds.EndTime, 0);
        }

        public ChromPeak CalcPeak(Transition transition, int optStep, float startTime, float endTime, ChromPeak.FlagValues flags)
        {
            if (Settings.MeasuredResults.IsTimeNormalArea)
                flags |= ChromPeak.FlagValues.time_normalized;

            if (startTime == endTime)
            {
                return ChromPeak.EMPTY;
            }

            var chromatogramInfo = GetChromatogram(transition, optStep);
            if (chromatogramInfo == null)
            {
                return ChromPeak.EMPTY;
            }

            var existingPeak = chromatogramInfo.Peaks.FirstOrDefault(peak => peak.StartTime == startTime && peak.EndTime == endTime);
            if (!existingPeak.IsEmpty)
            {
                return existingPeak;
            }

            return GetPeakIntegrator(transition, optStep)?.IntegratePeak(startTime, endTime, flags) ?? ChromPeak.EMPTY;
        }

        public ChromPeak GetPeak(Transition transition, int peakIndex)
        {
            return GetChromatogram(transition, 0)?.Peaks.ElementAtOrDefault(peakIndex) ?? ChromPeak.EMPTY;
        }

        public bool IsBestPeak(Transition transition, ChromPeak chromPeak)
        {
            var chromatogramInfo = GetChromatogram(transition, 0);
            if (chromatogramInfo == null || chromatogramInfo.BestPeakIndex == -1) 
            {
                return false;
            }

            var peak = chromatogramInfo!.Peaks.ElementAt(chromatogramInfo.BestPeakIndex);
            return peak.StartTime == chromPeak.StartTime && peak.EndTime == chromPeak.EndTime;
        }

        private ChromatogramInfo GetChromatogram(Transition transition, int optStep)
        {
            int index = TransitionGroupDocNode.FindNodeIndex(transition);
            return _transitionEntries[index]?.OptStepChromatograms.GetChromatogramForStep(optStep);
        }

        private PeakIntegrator GetPeakIntegrator(Transition transition, int optStep)
        {
            var transitionEntry = _transitionEntries[TransitionGroupDocNode.FindNodeIndex(transition)];
            if (optStep == 0)
            {
                return transitionEntry.PeakIntegrator;
            }

            return transitionEntry.OptStepChromatograms?.GetChromatogramForStep(optStep)
                ?.MakePeakIntegrator(_peakGroupIntegrator, _interpolatedTimes);
        }

        private class TransitionEntry
        {
            public TransitionEntry(OptStepChromatograms optStepChromatograms, PeakIntegrator peakIntegrator)
            {
                OptStepChromatograms = optStepChromatograms;
                PeakIntegrator = peakIntegrator;
            }

            public OptStepChromatograms OptStepChromatograms { get; }
            public PeakIntegrator PeakIntegrator { get; }
        }
    }
}
