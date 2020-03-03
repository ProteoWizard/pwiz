/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using Google.Protobuf;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Results.ProtoBuf;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    /// <summary>
    /// A set of chromatograms that get written out with a ChromGroupHeaderInfo.
    /// </summary>
    public abstract class TimeIntensitiesGroup : Immutable
    {
        public static readonly TimeIntensitiesGroup EMPTY = new Empty();

        public static TimeIntensitiesGroup Singleton(TimeIntensities timeIntensities)
        {
            return new SingletonImpl(timeIntensities);
        }

        protected TimeIntensitiesGroup(IEnumerable<TimeIntensities> timeIntensities)
        {
            TransitionTimeIntensities = ImmutableList<TimeIntensities>.ValueOf(timeIntensities);
        }
        public ImmutableList<TimeIntensities> TransitionTimeIntensities { get; private set; }

        public bool HasMassErrors
        {
            get { return TransitionTimeIntensities.Any(timeIntensities => null != timeIntensities.MassErrors); }
        }

        public bool HasAnyPoints
        {
            get { return TransitionTimeIntensities.Any(timeIntensities => 0 < timeIntensities.NumPoints); }
        }

        public float MinTime 
        { 
            get
            {
                return TransitionTimeIntensities.Min(timeIntensities => 0 == timeIntensities.NumPoints ? float.MaxValue : timeIntensities.Times[0]);
            } 
        }

        public float MaxTime
        {
            get
            {
                return TransitionTimeIntensities
                    .Max(timeIntensities => 0 == timeIntensities.NumPoints ? float.MinValue : timeIntensities.Times[timeIntensities.Times.Count - 1]);
            }
        }

        public abstract TimeIntensitiesGroup Truncate(float newStartTime, float newEndTime);
        public abstract TimeIntensitiesGroup RetainTransitionIndexes(ISet<int> transitionIndexes);
        public abstract void WriteToStream(Stream stream);
        public abstract int NumInterpolatedPoints { get; }

        private class SingletonImpl : TimeIntensitiesGroup
        {
            public SingletonImpl(TimeIntensities timeIntensities) : base(ImmutableList.Singleton(timeIntensities))
            {
            }

            public override TimeIntensitiesGroup Truncate(float newStartTime, float newEndTime)
            {
                return Singleton(TransitionTimeIntensities.First().Truncate(newStartTime, newEndTime));
            }

            public override TimeIntensitiesGroup RetainTransitionIndexes(ISet<int> transitionIndexes)
            {
                return transitionIndexes.Contains(0) ? this : EMPTY;
            }

            public override void WriteToStream(Stream stream)
            {
                throw new NotSupportedException();
            }

            public override int NumInterpolatedPoints
            {
                get { return TransitionTimeIntensities.First().NumPoints; }
            }
        }

        private class Empty : TimeIntensitiesGroup
        {
            internal Empty() : base(new TimeIntensities[0])
            {
                
            }

            public override TimeIntensitiesGroup Truncate(float newStartTime, float newEndTime)
            {
                return this;
            }

            public override TimeIntensitiesGroup RetainTransitionIndexes(ISet<int> transitionIndexes)
            {
                return this;
            }

            public override void WriteToStream(Stream stream)
            {
                throw new NotSupportedException();
            }

            public override int NumInterpolatedPoints
            {
                get { return 0; }
            }
        }
    }

    public class InterpolatedTimeIntensities : TimeIntensitiesGroup
    {
        private static readonly ImmutableList<ChromSource> PERSISTED_CHROM_SOURCES
            = ImmutableList<ChromSource>.ValueOf(new[] { ChromSource.fragment, ChromSource.sim, ChromSource.ms1 });

        public InterpolatedTimeIntensities(IEnumerable<TimeIntensities> transitionTimeIntensities,
            IEnumerable<ChromSource> transitionChromSources) : base(transitionTimeIntensities)
        {
            TransitionChromSources = ImmutableList<ChromSource>.ValueOf(transitionChromSources);
            Assume.IsTrue(TransitionTimeIntensities.Count > 0);
            Assume.IsTrue(TransitionChromSources.Count == TransitionTimeIntensities.Count);
        }

        public ImmutableList<ChromSource> TransitionChromSources { get; private set;}

        public ImmutableList<float> InterpolatedTimes
        {
            get
            {
                return TransitionTimeIntensities.First().Times;
            }
        }

        public IDictionary<ChromSource, ImmutableList<int>> ScanIdsByChromSource()
        {
            var result = new Dictionary<ChromSource, ImmutableList<int>>();
            for (int iTransition = 0; iTransition < TransitionTimeIntensities.Count; iTransition++)
            {
                if (null != TransitionTimeIntensities[iTransition].ScanIds)
                {
                    result[TransitionChromSources[iTransition]] = TransitionTimeIntensities[iTransition].ScanIds;
                }
            }
            return result;
        }

        public override void WriteToStream(Stream stream)
        {
            PrimitiveArrays.Write(stream, TransitionTimeIntensities.First().Times.ToArray());
            foreach (var timeIntensities in TransitionTimeIntensities)
            {
                PrimitiveArrays.Write(stream, timeIntensities.Intensities.ToArray());
            }

            foreach (var timeIntensities in TransitionTimeIntensities)
            {
                if (timeIntensities.MassErrors == null)
                {
                    continue;
                }
                WriteMassErrors(stream, timeIntensities.MassErrors);
            }
            var scanIdsByChromSource = ScanIdsByChromSource();
            foreach (var chromSource in PERSISTED_CHROM_SOURCES)
            {
                ImmutableList<int> scanIds;
                if (!scanIdsByChromSource.TryGetValue(chromSource, out scanIds))
                {
                    continue;
                }
                PrimitiveArrays.Write(stream, scanIds.ToArray());
            }
        }

        private static void WriteMassErrors(Stream stream, IList<float> values)
        {
            PrimitiveArrays.Write(stream, values.Select(value => ChromPeak.To10x(value)).ToArray());
        }

        private static float[] ReadMassErrors(Stream stream, int count)
        {
            return PrimitiveArrays.Read<short>(stream, count).Select(value => value / 10f).ToArray();
        }


        public static InterpolatedTimeIntensities ReadFromStream(Stream stream, ChromGroupHeaderInfo chromGroupHeaderInfo, ChromTransition[] chromTransitions)
        {
            Dictionary<ChromSource, int[]> scanIds = new Dictionary<ChromSource, int[]>();
            int numTrans = chromTransitions.Length;
            Assume.IsTrue(numTrans == chromGroupHeaderInfo.NumTransitions);
            int numPoints = chromGroupHeaderInfo.NumPoints;
            var sharedTimes = PrimitiveArrays.Read<float>(stream, numPoints);
            var transitionIntensities = new IList<float>[numTrans];
            for (int i = 0; i < numTrans; i++)
            {
                transitionIntensities[i] = PrimitiveArrays.Read<float>(stream, numPoints);
            }
            IList<float>[] transitionMassErrors = new IList<float>[numTrans];
            if (chromGroupHeaderInfo.HasMassErrors)
            {
                for (int i = 0; i < numTrans; i++)
                {
                    transitionMassErrors[i] = ReadMassErrors(stream, numPoints);
                }
            }
            if (chromGroupHeaderInfo.HasFragmentScanIds)
            {
                scanIds.Add(ChromSource.fragment, PrimitiveArrays.Read<int>(stream, numPoints));
            }
            if (chromGroupHeaderInfo.HasSimScanIds)
            {
                scanIds.Add(ChromSource.sim, PrimitiveArrays.Read<int>(stream, numPoints));
            }
            if (chromGroupHeaderInfo.HasMs1ScanIds)
            {
                scanIds.Add(ChromSource.ms1, PrimitiveArrays.Read<int>(stream, numPoints));
            }
            List<TimeIntensities> listOfTimeIntensities = new List<TimeIntensities>();
            for (int i = 0; i < numTrans; i++)
            {
                var chromSource = chromTransitions[i].Source;
                int[] transitionScanIds;
                scanIds.TryGetValue(chromSource, out transitionScanIds);

                var timeIntensities = new TimeIntensities(sharedTimes, transitionIntensities[i], transitionMassErrors[i], transitionScanIds);
                listOfTimeIntensities.Add(timeIntensities);
            }
            return new InterpolatedTimeIntensities(listOfTimeIntensities, chromTransitions.Select(chromTransition=>chromTransition.Source));
        }

        public override TimeIntensitiesGroup Truncate(float newStartTime, float newEndTime)
        {
            return new InterpolatedTimeIntensities(TransitionTimeIntensities
                .Select(timeIntensities=>timeIntensities.Truncate(newStartTime, newEndTime)), 
                TransitionChromSources);
        }

        public override TimeIntensitiesGroup RetainTransitionIndexes(ISet<int> transitionIndexes)
        {
            var newIndexes = Enumerable.Range(0, TransitionTimeIntensities.Count).Where(transitionIndexes.Contains).ToArray();
            if (newIndexes.Length == 0)
            {
                return EMPTY;
            }
            return new InterpolatedTimeIntensities(newIndexes.Select(i=>TransitionTimeIntensities[i]), newIndexes.Select(i=>TransitionChromSources[i]));
        }

        public override int NumInterpolatedPoints { get { return TransitionTimeIntensities.First().NumPoints; } }
    }

    public class RawTimeIntensities : TimeIntensitiesGroup
    {
        public RawTimeIntensities(IEnumerable<TimeIntensities> transitionTimeIntensities, InterpolationParams interpolationParams)
            : base(transitionTimeIntensities)
        {
            InterpolationParams = interpolationParams;
        }

        public TimeIntervals TimeIntervals { get; private set; }

        public RawTimeIntensities ChangeTimeIntervals(TimeIntervals timeIntervals)
        {
            return ChangeProp(ImClone(this), im => im.TimeIntervals = timeIntervals);
        }
        public InterpolationParams InterpolationParams { get; private set; }
        public bool InferZeroes { get { return InterpolationParams != null && InterpolationParams.InferZeroes; } }
        public InterpolatedTimeIntensities Interpolate(IEnumerable<ChromSource> chromSources)
        {
            var interpolatedTimes = GetInterpolatedTimes();
            return new InterpolatedTimeIntensities(TransitionTimeIntensities.Select(timeIntensities=>timeIntensities.Interpolate(interpolatedTimes, InferZeroes)), 
                chromSources);
        }

        public ImmutableList<float> GetInterpolatedTimes()
        {
            if (InterpolationParams == null)
            {
                return TransitionTimeIntensities.First().Times;
            }
            return ImmutableList.ValueOf(InterpolationParams.GetEvenlySpacedTimesFloat());
        }
        
        public override void WriteToStream(Stream stream)
        {
            var chromatogramGroupData = ToChromatogramGroupData();
            var codedOutputStream = new CodedOutputStream(stream);
            chromatogramGroupData.WriteTo(codedOutputStream);
            codedOutputStream.Flush();
        }

        public ChromatogramGroupData ToChromatogramGroupData()
        {
            var timeLists = new Dictionary<ImmutableList<float>, int>();
            var scanIdLists = new Dictionary<ImmutableList<int>, int>();
            var chromatogramGroupData = new ChromatogramGroupData();
            for (int i = 0; i < TransitionTimeIntensities.Count; i++)
            {
                var timeIntensities = TransitionTimeIntensities[i];
                var chromatogram = new ChromatogramGroupData.Types.Chromatogram();
                int timeListIndex;
                if (!timeLists.TryGetValue(timeIntensities.Times, out timeListIndex))
                {
                    timeListIndex = timeLists.Count + 1;
                    timeLists.Add(timeIntensities.Times, timeListIndex);
                    var timeList = new ChromatogramGroupData.Types.TimeList();
                    timeList.Times.AddRange(timeIntensities.Times);
                    chromatogramGroupData.TimeLists.Add(timeList);
                }
                chromatogram.TimeListIndex = timeListIndex;
                chromatogram.Intensities.AddRange(timeIntensities.Intensities);
                if (null != timeIntensities.MassErrors)
                {
                    chromatogram.MassErrors100X.AddRange(timeIntensities.MassErrors.Select(error=>(int) Math.Round(error * 100)));
                }

                if (null != timeIntensities.ScanIds)
                {
                    int scanIdListIndex;
                    if (!scanIdLists.TryGetValue(timeIntensities.ScanIds, out scanIdListIndex))
                    {
                        scanIdListIndex = scanIdLists.Count + 1;
                        scanIdLists.Add(timeIntensities.ScanIds, scanIdListIndex);
                        var scanIdList = new ChromatogramGroupData.Types.ScanIdList();
                        scanIdList.ScanIds.AddRange(timeIntensities.ScanIds);
                        chromatogramGroupData.ScanIdLists.Add(scanIdList);
                    }
                    chromatogram.ScanIdListIndex = scanIdListIndex;
                }
                chromatogramGroupData.Chromatograms.Add(chromatogram);
            }
            if (InterpolationParams != null)
            {
                chromatogramGroupData.InterpolatedStartTime = InterpolationParams.StartTime;
                chromatogramGroupData.InterpolatedEndTime = InterpolationParams.EndTime;
                chromatogramGroupData.InterpolatedNumPoints = InterpolationParams.NumPoints;
                chromatogramGroupData.InterpolatedDelta = InterpolationParams.IntervalDelta;
                chromatogramGroupData.InferZeroes = InterpolationParams.InferZeroes;
            }

            if (TimeIntervals != null)
            {
                chromatogramGroupData.TimeIntervals = new ChromatogramGroupData.Types.TimeIntervals();
                chromatogramGroupData.TimeIntervals.StartTimes.AddRange(TimeIntervals.Starts);
                chromatogramGroupData.TimeIntervals.EndTimes.AddRange(TimeIntervals.Ends);
            }
            return chromatogramGroupData;
        }

        public static RawTimeIntensities FromChromatogramGroupData(ChromatogramGroupData chromatogramGroupData)
        {
            var timeIntensitiesList = new List<TimeIntensities>();
            var timeLists = chromatogramGroupData.TimeLists.Select(timeList => ImmutableList.ValueOf(timeList.Times)).ToArray();
            var scanIdLists = chromatogramGroupData.ScanIdLists
                .Select(scanIdList => ImmutableList.ValueOf(scanIdList.ScanIds)).ToArray();
            foreach (var chromatogram in chromatogramGroupData.Chromatograms)
            {
                IEnumerable<float> massErrors = null;
                if (chromatogram.MassErrors100X.Count > 0)
                {
                    massErrors = chromatogram.MassErrors100X.Select(error => error/100.0f);
                }
                else if (chromatogram.MassErrorsDeprecated.Count > 0)
                {
                    massErrors = chromatogram.MassErrorsDeprecated;
                }
                var timeIntensities = new TimeIntensities(timeLists[chromatogram.TimeListIndex - 1],
                    chromatogram.Intensities,
                    massErrors,
                    chromatogram.ScanIdListIndex == 0 ? null : scanIdLists[chromatogram.ScanIdListIndex - 1]);
                timeIntensitiesList.Add(timeIntensities);
            }
            InterpolationParams interpolationParams;
            if (chromatogramGroupData.InterpolatedNumPoints == 0)
            {
                interpolationParams = null;
            }
            else
            {
                interpolationParams = new InterpolationParams(chromatogramGroupData.InterpolatedStartTime, chromatogramGroupData.InterpolatedEndTime, chromatogramGroupData.InterpolatedNumPoints, chromatogramGroupData.InterpolatedDelta)
                    .ChangeInferZeroes(chromatogramGroupData.InferZeroes);
            }
            var rawTimeIntensities = new RawTimeIntensities(timeIntensitiesList, interpolationParams);
            if (chromatogramGroupData.TimeIntervals != null)
            {
                var startTimes = chromatogramGroupData.TimeIntervals.StartTimes;
                var endTimes = chromatogramGroupData.TimeIntervals.EndTimes;

                var timeIntervals = TimeIntervals.FromIntervals(Enumerable.Range(0, startTimes.Count)
                    .Select(i => new KeyValuePair<float, float>(startTimes[i], endTimes[i])));
                rawTimeIntensities = rawTimeIntensities.ChangeTimeIntervals(timeIntervals);
            }

            return rawTimeIntensities;
        }

        public static RawTimeIntensities ReadFromStream(Stream stream)
        {
            var chromatogramGroupData = new ChromatogramGroupData();
            chromatogramGroupData.MergeFrom(new CodedInputStream(stream));
            return FromChromatogramGroupData(chromatogramGroupData);
        }

        public override TimeIntensitiesGroup Truncate(float newStartTime, float newEndTime)
        {
            InterpolationParams interpolationParams;
            if (InterpolationParams == null)
            {
                interpolationParams = null;
            }
            else
            {
                var interpolatedTimes = GetInterpolatedTimes();
                int startIndex = CollectionUtil.BinarySearch(interpolatedTimes, newStartTime);
                if (startIndex < 0)
                {
                    startIndex = ~startIndex - 1;
                }
                int endIndex = CollectionUtil.BinarySearch(interpolatedTimes, newEndTime);
                if (endIndex < 0)
                {
                    endIndex = ~endIndex;
                }
                startIndex = Math.Max(startIndex, 0);
                endIndex = Math.Min(Math.Max(startIndex, endIndex), interpolatedTimes.Count - 1);
                interpolationParams = InterpolationParams
                    .ChangeStartTime(interpolatedTimes[startIndex])
                    .ChangeEndTime(interpolatedTimes[endIndex])
                    .ChangeNumPoints(endIndex - startIndex + 1);
            }
            return new RawTimeIntensities(
                TransitionTimeIntensities.Select(timeIntensities=>timeIntensities.Truncate(newStartTime, newEndTime)),
                interpolationParams);
        }

        public override TimeIntensitiesGroup RetainTransitionIndexes(ISet<int> transitionIndexes)
        {
            var newIndexes = Enumerable.Range(0, TransitionTimeIntensities.Count).Where(transitionIndexes.Contains).ToArray();
            if (newIndexes.Length == 0)
            {
                return EMPTY;
            }
            return new RawTimeIntensities(newIndexes.Select(i => TransitionTimeIntensities[i]), InterpolationParams);
        }

        public override int NumInterpolatedPoints
        {
            get
            {
                if (null == InterpolationParams)
                {
                    return 0;
                }
                return InterpolationParams.NumPoints;
            }
        }
    }

    public class InterpolationParams : Immutable
    {
        public InterpolationParams(double startTime, double endTime, int numPoints, double intervalDelta)
        {
            StartTime = startTime;
            EndTime = endTime;
            NumPoints = numPoints;
            if (intervalDelta == 0 && numPoints > 1)
            {
                IntervalDelta = (EndTime - StartTime) / (NumPoints - 1);
            }
            else
            {
                IntervalDelta = intervalDelta;
            }
        }

        public double StartTime { get; private set; }

        public InterpolationParams ChangeStartTime(double startTime)
        {
            return ChangeProp(ImClone(this), im => im.StartTime = startTime);
        }
        public double EndTime { get; private set; }

        public InterpolationParams ChangeEndTime(double endTime)
        {
            return ChangeProp(ImClone(this), im => im.EndTime = endTime);
        }
        public int NumPoints { get; private set; }

        public InterpolationParams ChangeNumPoints(int numPoints)
        {
            return ChangeProp(ImClone(this), im => im.NumPoints = numPoints);
        }
        public double IntervalDelta { get; private set; }

        public InterpolationParams ChangeIntervalDelta(double intervalDelta)
        {
            return ChangeProp(ImClone(this), im => im.IntervalDelta = intervalDelta);
        }

        public bool InferZeroes { get; private set; }

        public InterpolationParams ChangeInferZeroes(bool inferZeroes)
        {
            return ChangeProp(ImClone(this), im => im.InferZeroes = inferZeroes);
        }

        public InterpolationParams ChangeStartEndIndex(int startIndex, int endIndex)
        {
            if (startIndex == 0 && endIndex == NumPoints - 1)
            {
                return this;
            }
            double[] times = GetEvenlySpacedTimes();
            return ChangeProp(ImClone(this), im =>
            {
                im.StartTime = times[startIndex];
                im.EndTime = times[endIndex];
                im.NumPoints = endIndex - startIndex + 1;
            });
        }

        public double[] GetEvenlySpacedTimes()
        {
            double[] result = new double[NumPoints];
            var current = StartTime;
            for (int i = 0; i < NumPoints; i++)
            {
                result[i] = current;
                current += IntervalDelta;
            }
            return result;
        }

        public IEnumerable<float> GetEvenlySpacedTimesFloat()
        {
            return GetEvenlySpacedTimes().Select(t => (float) t);
        }

        public static InterpolationParams WithInterval(double start, double end, double delta)
        {
            int numPoints = 0;
            double lastTime = start;

            for (double nextTime = start; nextTime <= end; nextTime += delta)
            {
                lastTime = nextTime;
                numPoints++;
            }
            return new InterpolationParams(start, lastTime, numPoints, delta);
        }
    }
}
