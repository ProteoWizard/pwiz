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
using System.Linq;
using pwiz.CommonMsData;

namespace pwiz.Skyline.Model.Results
{
    public class TransitionGroupIntegrator
    {
        private PeakGroupIntegrator _peakGroupIntegrator;
        private OptStepChromatograms[] _optStepChromatograms;
        private PeakIntegrator[] _peakIntegrators;
        private ImmutableList<float> _interpolatedTimes;

        public TransitionGroupIntegrator(SrmSettings settings, TransitionGroupDocNode transitionGroupDocNode,
            ChromatogramSet chromatogramSet, ChromatogramGroupInfo chromatogramGroupInfo)
        {
            Settings = settings;
            TransitionGroupDocNode = transitionGroupDocNode;
            ChromatogramSet = chromatogramSet;
            ChromatogramGroupInfo = chromatogramGroupInfo;
            ChromFileInfoId = ChromatogramSet.FindFile(chromatogramGroupInfo.FilePath);
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

        public ChromPeak CalcPeak(Transition transition, int optStep, PeakBounds peakBounds)
        {
            return CalcPeak(transition, optStep, (float)peakBounds.StartTime, (float)peakBounds.EndTime, 0);
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

        public ChromPeak CalcMatchingPeak(Transition transition, int optStep, TransitionGroupChromInfo matchingPeak, out UserSet userSet)
        {
            var peak = CalcPeak(transition, optStep, matchingPeak.StartRetentionTime.Value,
                matchingPeak.EndRetentionTime.Value, 0);
            userSet = IsBestPeak(transition, peak) ? UserSet.FALSE : UserSet.MATCHED;
            return peak;
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
            return EnsureOptStepChromatograms()[index].GetChromatogramForStep(optStep);
        }

        private OptStepChromatograms[] EnsureOptStepChromatograms()
        {
            float tolerance = (float)Settings.TransitionSettings.Instrument.MzMatchTolerance;
            _optStepChromatograms ??= TransitionGroupDocNode.Transitions.Select(transition =>
                ChromatogramGroupInfo.GetAllTransitionInfo(transition, tolerance, ChromatogramSet.OptimizationFunction,
                    TransformChrom.raw)).ToArray();
            return _optStepChromatograms;
        }

        private PeakGroupIntegrator EnsurePeakGroupIntegrator()
        {
            if (_peakGroupIntegrator == null)
            {
                var rawTimeIntensities = ChromatogramGroupInfo.TimeIntensitiesGroup as RawTimeIntensities;
                _interpolatedTimes = rawTimeIntensities?.GetInterpolatedTimes();
                var peakGroupIntegrator =
                    new PeakGroupIntegrator(Settings.TransitionSettings.FullScan.AcquisitionMethod,
                        rawTimeIntensities?.TimeIntervals);
                _peakIntegrators = EnsureOptStepChromatograms().Zip(TransitionGroupDocNode.Transitions,
                    (optStepChromatograms, transition) => optStepChromatograms?.GetChromatogramForStep(0)
                        ?.MakePeakIntegrator(peakGroupIntegrator, _interpolatedTimes)).ToArray();
                _peakGroupIntegrator = peakGroupIntegrator;
            }
            return _peakGroupIntegrator;
        }
        private PeakIntegrator GetPeakIntegrator(Transition transition, int optStep)
        {
            var peakGroupIntegrator = EnsurePeakGroupIntegrator();
            int index = TransitionGroupDocNode.FindNodeIndex(transition);
            if (optStep == 0)
            {
                return _peakIntegrators[index];
            }

            return EnsureOptStepChromatograms()[index]?.GetChromatogramForStep(optStep)
                ?.MakePeakIntegrator(peakGroupIntegrator, _interpolatedTimes);
        }
    }
}
