using pwiz.Common.Collections;
using pwiz.Common.PeakFinding;
using pwiz.Skyline.Model.DocSettings;
using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.CommonMsData;

namespace pwiz.Skyline.Model.Results
{
    public class TransitionGroupIntegrator
    {
        private PeakBounds _peakBounds;
        private PeakGroupIntegrator _peakGroupIntegrator;
        private TransitionEntry[] _transitionEntries;

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

        public PeakGroupIntegrator GetPeakGroupIntegrator(PeakBounds peakBounds)
        {
            if (_peakGroupIntegrator == null)
            {
                _peakGroupIntegrator = MakePeakGroupIntegrator(peakBounds);
                _peakBounds = peakBounds;
                return _peakGroupIntegrator;
            }

            if (_peakBounds == null || _peakBounds.Equals(peakBounds))
            {
                return _peakGroupIntegrator;
            }

            _peakGroupIntegrator = MakePeakGroupIntegrator(null);
            _peakBounds = null;
            return _peakGroupIntegrator;
        }

        private PeakGroupIntegrator MakePeakGroupIntegrator(PeakBounds peakBounds)
        {
            _transitionEntries ??= GetTransitionEntries().ToArray();
            var timeIntervals = (ChromatogramGroupInfo.TimeIntensitiesGroup as RawTimeIntensities)?.TimeIntervals;
            var peakGroupIntegrator =
                new PeakGroupIntegrator(Settings.TransitionSettings.FullScan.AcquisitionMethod, timeIntervals);
            ImmutableList<float> interpolatedTimes = GetInterpolatedTimes(peakBounds);
            foreach (var transitionEntry in _transitionEntries)
            {
                var chromatogramInfo = transitionEntry.OptStepChromatograms.GetChromatogramForStep(0);
                if (chromatogramInfo != null)
                {
                    transitionEntry.PeakIntegrator = chromatogramInfo.MakePeakIntegrator(peakGroupIntegrator, interpolatedTimes);
                    peakGroupIntegrator.AddPeakIntegrator(transitionEntry.PeakIntegrator);
                }
            }
            return peakGroupIntegrator;
        }

        private IEnumerable<TransitionEntry> GetTransitionEntries()
        {
            var tolerance = (float)Settings.TransitionSettings.Instrument.MzMatchTolerance;
            foreach (var transition in TransitionGroupDocNode.Transitions)
            {
                var optStepChromatograms = ChromatogramGroupInfo.GetAllTransitionInfo(transition, tolerance,
                    ChromatogramSet.OptimizationFunction, TransformChrom.raw);
                yield return new TransitionEntry(optStepChromatograms);
            }
        }

        private ImmutableList<float> GetInterpolatedTimes(PeakBounds peakBounds)
        {
            ImmutableList<float> interpolatedTimes;
            if (ChromatogramGroupInfo.TimeIntensitiesGroup is RawTimeIntensities rawTimeIntensities)
            {
                interpolatedTimes = rawTimeIntensities.GetInterpolatedTimes();
            }
            else
            {
                return ChromatogramGroupInfo.TimeIntensitiesGroup.TransitionTimeIntensities[0].Times;
            }

            if (peakBounds != null)
            {
                int startIndex = interpolatedTimes.BinarySearch((float)peakBounds.StartTime);
                if (startIndex < 0)
                {
                    startIndex = ~startIndex - 1;
                }

                int endIndex = interpolatedTimes.BinarySearch((float)peakBounds.EndTime);
                if (endIndex < 0)
                {
                    endIndex = ~endIndex;
                }

                startIndex = Math.Max(0, startIndex - 1);
                endIndex = Math.Min(interpolatedTimes.Count, endIndex + 1);
                if (startIndex > 0 || endIndex < interpolatedTimes.Count)
                {
                    interpolatedTimes = interpolatedTimes.Skip(startIndex).Take(endIndex - startIndex).ToImmutable();
                }
            }

            return interpolatedTimes;
        }

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

            var transitionEntry = GetTransitionEntry(transition);
            var chromatogramInfo = transitionEntry.OptStepChromatograms?.GetChromatogramForStep(optStep);
            if (chromatogramInfo == null)
            {
                return ChromPeak.EMPTY;
            }

            var existingPeak = chromatogramInfo.Peaks.FirstOrDefault(peak => peak.StartTime == startTime && peak.EndTime == endTime);
            if (!existingPeak.IsEmpty)
            {
                return existingPeak;
            }

            MakePeakGroupIntegrator(new PeakBounds(startTime, endTime));
            return transitionEntry.PeakIntegrator?.IntegratePeak(startTime, endTime, flags) ?? ChromPeak.EMPTY;
        }

        public ChromPeak GetPeak(Transition transition, int peakIndex)
        {
            return _transitionEntries[TransitionGroupDocNode.FindNodeIndex(transition)].OptStepChromatograms.GetChromatogramForStep(0)
                ?.Peaks.ElementAtOrDefault(peakIndex) ?? ChromPeak.EMPTY;
        }

        public bool IsBestPeak(Transition transition, ChromPeak chromPeak)
        {
            var transitionEntry = GetTransitionEntry(transition);
            var chromatogramInfo = transitionEntry.OptStepChromatograms.GetChromatogramForStep(0);
            if (chromatogramInfo == null || chromatogramInfo.BestPeakIndex == -1) 
            {
                return false;
            }

            var peak = chromatogramInfo!.Peaks.ElementAt(chromatogramInfo.BestPeakIndex);
            return peak.StartTime == chromPeak.StartTime && peak.EndTime == chromPeak.EndTime;
        }

        private TransitionEntry GetTransitionEntry(Transition transition)
        {
            _transitionEntries ??= GetTransitionEntries().ToArray();
            return _transitionEntries[TransitionGroupDocNode.FindNodeIndex(transition)];
        }

        private class TransitionEntry
        {
            public TransitionEntry(OptStepChromatograms chromatogramInfo)
            {
                OptStepChromatograms = chromatogramInfo;
            }
            public OptStepChromatograms OptStepChromatograms { get; }
            public PeakIntegrator PeakIntegrator { get; set;  }
        }
    }
}
