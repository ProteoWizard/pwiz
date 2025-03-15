/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using MathNet.Numerics.Statistics;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.Results.Imputation
{
    public abstract class RtValueType
    {
        public static readonly RtValueType PEAK_APEXES = new PeakApexes();
        public static readonly RtValueType PSM_TIMES = new PsmTimes();

        private static ImmutableList<RtValueType> ALL = ImmutableList.ValueOf(new[] { PEAK_APEXES, PSM_TIMES });
        public static IEnumerable<RtValueType> GetChoices(SrmDocument document)
        {
            return ALL.Where(item => item.IsValidFor(document));
        }

        public static RetentionScoreCalculatorSpec GetRetentionScoreCalculatorSpec(SrmDocument document, string name)
        {
            var calculatorSpec = Settings.Default.GetCalculatorByName(name);
            if (calculatorSpec != null)
            {
                return calculatorSpec;
            }

            var rtValueType = ALL.FirstOrDefault(rtValueType => rtValueType.Name == name);
            if (rtValueType != null)
            {
                return rtValueType.ToRetentionScoreCalculator(document);
            }

            return null;
        }

        public static IEnumerable<RtValueType> GetChoicesWithCalculators(SrmDocument document)
        {
            return GetChoices(document).Concat(Settings.Default.RTScoreCalculatorList
                .Select(calc => new Calculator(calc)));
        }

        public virtual bool IsValidFor(SrmDocument document)
        {
            return true;
        }

        public static RtValueType ForName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            foreach (var rtValueType in new[] { PEAK_APEXES, PSM_TIMES })
            {
                if (rtValueType.Name == name)
                {
                    return rtValueType;
                }
            }

            var calc = Settings.Default.RTScoreCalculatorList[name];
            if (calc != null)
            {
                return new Calculator(calc);
            }

            return null;
        }

        public abstract string Name { get; }

        public virtual RetentionScoreCalculatorSpec ToRetentionScoreCalculator(SrmDocument document)
        {
            var measuredRetentionTimeDictionary = new Dictionary<Target, MeasuredRetentionTime>();
            var replicateDictionaries = ReplicateFileId.List(document.MeasuredResults)
                .Select(replicateFileId => GetRetentionTimes(document, replicateFileId)).ToList();
            foreach (var target in replicateDictionaries.SelectMany(dict => dict.Keys).Distinct())
            {
                var times = new List<double>();
                foreach (var dict in replicateDictionaries)
                {
                    if (dict.TryGetValue(target, out var time))
                    {
                        times.Add(time);
                    }
                }

                if (times.Count > 0)
                {
                    measuredRetentionTimeDictionary.Add(target, new MeasuredRetentionTime(target, times.Mean(), true));
                }
            }

            return new DictionaryRetentionScoreCalculator(Name, ToString(), measuredRetentionTimeDictionary);
        }

        public Dictionary<Target, double> GetRetentionTimes(SrmDocument document,
            ReplicateFileId replicateFileId)
        {
            var dictionary = new Dictionary<Target, double>();
            var fileInfo = replicateFileId.FindInfo(document.MeasuredResults);
            if (fileInfo == null)
            {
                return dictionary;
            }

            foreach (var entry in GetAllRetentionTimes(document, fileInfo))
            {
                dictionary.Add(entry.Key, entry.Value.Median());
            }

            return dictionary;
        }

        protected abstract IEnumerable<KeyValuePair<Target, IEnumerable<double>>> GetAllRetentionTimes(
            SrmDocument document, ReplicateFileInfo fileInfo);

        class PeakApexes : RtValueType
        {
            protected override IEnumerable<KeyValuePair<Target, IEnumerable<double>>> GetAllRetentionTimes(SrmDocument document, ReplicateFileInfo fileInfo)
            {
                foreach (var peptideGroup in document.Molecules.GroupBy(peptideDocNode => peptideDocNode.ModifiedTarget))
                {
                    var times = new List<double>();
                    foreach (var peptideDocNode in peptideGroup)
                    {
                        foreach (var peptideChromInfo in peptideDocNode.GetSafeChromInfo(fileInfo.ReplicateIndex))
                        {
                            if (ReferenceEquals(peptideChromInfo.FileId, fileInfo.ReplicateFileId.FileId))
                            {
                                if (peptideChromInfo.RetentionTime.HasValue)
                                {
                                    times.Add(peptideChromInfo.RetentionTime.Value);
                                }
                            }
                        }
                    }

                    if (times.Count > 0)
                    {
                        yield return new KeyValuePair<Target, IEnumerable<double>>(peptideGroup.Key, times);
                    }
                }
            }

            public override string ToString()
            {
                return ImputationResources.PeakApexes_ToString_Average_Peak_Apex;
            }

            public override string Name
            {
                get { return @"peak_apexes"; }
            }
        }

        private class PsmTimes : RtValueType
        {
            protected override IEnumerable<KeyValuePair<Target, IEnumerable<double>>> GetAllRetentionTimes(SrmDocument document, ReplicateFileInfo fileInfo)
            {
                if (!document.Settings.PeptideSettings.Libraries.IsLoaded)
                {
                    return Array.Empty<KeyValuePair<Target, IEnumerable<double>>>();
                }
                var retentionTimes = document.Settings.GetRetentionTimes(fileInfo.MsDataFileUri);
                if (retentionTimes == null)
                {
                    return Array.Empty<KeyValuePair<Target, IEnumerable<double>>>();
                }

                return retentionTimes.GetFirstRetentionTimes().Select(kvp =>
                    new KeyValuePair<Target, IEnumerable<double>>(kvp.Key, new[] { kvp.Value }));
            }

            public override string ToString()
            {
                return ImputationResources.PsmTimes_ToString_Average_PSM_Time;
            }

            public override bool IsValidFor(SrmDocument document)
            {
                var measuredResults = document.MeasuredResults;
                if (measuredResults == null)
                {
                    return true;
                }

                if (!document.Settings.PeptideSettings.Libraries.IsLoaded)
                {
                    return true;
                }

                foreach (var chromatogramSet in measuredResults.Chromatograms)
                {
                    foreach (var chromFileInfo in chromatogramSet.MSDataFileInfos)
                    {
                        if (document.Settings.PeptideSettings.Libraries.TryGetRetentionTimes(chromFileInfo.FilePath,
                                out var libraryRetentionTimes))
                        {
                            if (libraryRetentionTimes.PeptideRetentionTimes.Any())
                            {
                                return true;
                            }
                        }
                    }
                }

                return false;
            }

            public override string Name
            {
                get { return @"psm_times"; }
            }
        }

        public class Calculator : RtValueType
        {
            public Calculator(RetentionScoreCalculatorSpec calculator)
            {
                RetentionScoreCalculator = calculator;
            }

            protected override IEnumerable<KeyValuePair<Target, IEnumerable<double>>> GetAllRetentionTimes(SrmDocument document, ReplicateFileInfo fileInfo)
            {
                foreach (var target in document.Molecules.Select(m => m.ModifiedTarget).Distinct())
                {
                    var score = RetentionScoreCalculator.ScoreSequence(target);
                    if (score.HasValue)
                    {
                        yield return new KeyValuePair<Target, IEnumerable<double>>(target, new[] { score.Value });
                    }
                }
            }

            public RetentionScoreCalculatorSpec RetentionScoreCalculator
            {
                get;
            }

            public override RetentionScoreCalculatorSpec ToRetentionScoreCalculator(SrmDocument document)
            {
                return RetentionScoreCalculator;
            }

            public override string ToString()
            {
                return RetentionScoreCalculator.Name;
            }

            protected bool Equals(Calculator other)
            {
                return RetentionScoreCalculator.Name.Equals(other.RetentionScoreCalculator.Name);
            }

            public override bool Equals(object obj)
            {
                if (obj is null) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((Calculator)obj);
            }

            public override int GetHashCode()
            {
                return RetentionScoreCalculator.Name.GetHashCode();
            }

            public override string Name
            {
                get { return RetentionScoreCalculator.Name; }
            }
        }
    }
}
