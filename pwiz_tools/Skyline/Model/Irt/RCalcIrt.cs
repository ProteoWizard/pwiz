/*
 * Original author: John Chilton <jchilton .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using System.Xml;
using System.Xml.Serialization;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Irt
{
    [XmlRoot("irt_calculator")]
    public class RCalcIrt : RetentionScoreCalculatorSpec
    {
        public static readonly RCalcIrt NONE = new RCalcIrt(@"None", string.Empty);

        public const double MIN_IRT_TO_TIME_CORRELATION = 0.99;
        public const double MIN_PEPTIDES_PERCENT = 0.80;
        public const int MIN_PEPTIDES_COUNT = 8;

        public static int MinStandardCount(int expectedCount)
        {
            return expectedCount <= MIN_PEPTIDES_COUNT
                ? Math.Max(2, expectedCount)    // Crazy to even allow 2, but once had no minimum at all
                : Math.Max(MIN_PEPTIDES_COUNT, (int) (expectedCount*MIN_PEPTIDES_PERCENT));
        }

        public static bool IsAcceptableStandardCount(int expectedCount, int actualCount)
        {
            return actualCount >= MinStandardCount(expectedCount);
        }

        private IrtDb _database;

        public RCalcIrt(string name, string databasePath)
            : base(name)
        {
            DatabasePath = databasePath;
        }

        public string DatabasePath { get; private set; }

        public IEnumerable<KeyValuePair<Target, double>> PeptideScores => _database?.PeptideScores ?? Array.Empty<KeyValuePair<Target, double>>();

        public bool IsNone => Name == NONE.Name;

        public override bool IsUsable => _database != null;

        public override RetentionScoreCalculatorSpec Initialize(IProgressMonitor loadMonitor)
        {
            if (_database != null)
                return this;

            var database = IrtDb.GetIrtDb(DatabasePath, loadMonitor);
            // Check for the case where an exception was handled by the progress monitor
            if (database == null)
                return null;
            return ChangeDatabase(database);
        }

        public override string PersistencePath
        {
            get { return DatabasePath; }
        }

        /// <summary>
        /// Saves the database to a new directory with only the standards and peptides used
        /// in a given document.
        /// </summary>
        /// <param name="pathDestDir">The directory to save to</param>
        /// <param name="document">The document for which peptides are to be kept</param>
        /// <returns>The full path to the file saved</returns>
        public override string PersistMinimized(string pathDestDir, SrmDocument document)
        {
            RequireUsable();

            string persistPath = Path.Combine(pathDestDir, Path.GetFileName(PersistencePath) ?? string.Empty);  // ReSharper
            using (var fs = new FileSaver(persistPath))
            {
                var irtDbMinimal = IrtDb.CreateIrtDb(fs.SafeName);

                // Calculate the minimal set of peptides needed for this document
                var dbPeptides = _database.ReadPeptides().ToList();
                var persistPeptides = dbPeptides.Where(pep => pep.Standard).Select(NewPeptide).ToList();
                var dictPeptides = new TargetMap<DbIrtPeptide>(dbPeptides.Where(pep => !pep.Standard)
                    .Select(pep => new KeyValuePair<Target, DbIrtPeptide>(pep.ModifiedTarget, pep)));
                var uniqueTargets = new HashSet<Target>();
                foreach (var nodePep in document.Molecules)
                {
                    var modifiedSeq = document.Settings.GetSourceTarget(nodePep);
                    DbIrtPeptide dbPeptide;
                    if (dictPeptides.TryGetValue(modifiedSeq, out dbPeptide))
                    {
                        if (uniqueTargets.Add(dbPeptide.ModifiedTarget)) // Only add once
                        {
                            persistPeptides.Add(NewPeptide(dbPeptide));
                        }
                    }
                }

                irtDbMinimal.UpdatePeptides(persistPeptides);
                fs.Commit();
            }

            return persistPath;
        }

        private DbIrtPeptide NewPeptide(DbIrtPeptide dbPeptide)
        {
            return new DbIrtPeptide(dbPeptide.ModifiedTarget,
                                    dbPeptide.Irt,
                                    dbPeptide.Standard,
                                    dbPeptide.TimeSource);
        }

        public override IEnumerable<Target> ChooseRegressionPeptides(IEnumerable<Target> peptides, out int minCount)
        {
            RequireUsable();

            var pepArr = peptides.ToArray();
            var returnStandard = pepArr.Where(_database.IsStandard).Distinct().ToArray();
            var returnCount = returnStandard.Length;
            var databaseCount = _database.StandardPeptideCount;

            if (!IsAcceptableStandardCount(databaseCount, returnCount))
            {
                var inStandardButNotTargets = new SortedSet<Target>(_database.StandardPeptides);
                inStandardButNotTargets.ExceptWith(pepArr);
                //Console.Out.WriteLine(@"Database standards: {0}", string.Join(@"; ", _database.StandardPeptides));
                //Console.Out.WriteLine(@"Chosen ({0}): {1}", pepArr.Length, string.Join(@"; ", pepArr.Select(pep => pep.ToString())));
                throw new IncompleteStandardException(this, databaseCount, inStandardButNotTargets);
            }

            minCount = MinStandardCount(databaseCount);
            return returnStandard;
        }

        public override IEnumerable<Target> GetStandardPeptides(IEnumerable<Target> peptides)
        {
            return ChooseRegressionPeptides(peptides, out _);
        }

        public override double? ScoreSequence(Target seq)
        {
            return _database?.ScoreSequence(seq);
        }

        public override double UnknownScore
        {
            get
            {
                RequireUsable();

                return _database.UnknownScore;
            }
        }

        private void RequireUsable()
        {
            if (!IsUsable)
                throw new InvalidOperationException(Resources.RCalcIrt_RequireUsable_Unexpected_use_of_iRT_calculator_before_successful_initialization_);
        }

        public IEnumerable<Target> GetStandardPeptides()
        {
            return _database.StandardPeptides;
        }

        public IEnumerable<DbIrtPeptide> GetDbIrtPeptides()
        {
            return _database.ReadPeptides();
        }

        public string DocumentXml => _database.DocumentXml;
        public IrtRegressionType RegressionType => _database.RegressionType;

        public static ProcessedIrtAverages ProcessRetentionTimes(IProgressMonitor monitor,
            IRetentionTimeProvider[] providers, DbIrtPeptide[] standardPeptideList, DbIrtPeptide[] items, IrtRegressionType regressionType)
        {
            var heavyStandards = new DbIrtPeptide[standardPeptideList.Length];
            var matchedStandard = IrtStandard.WhichStandard(standardPeptideList.Select(pep => pep.ModifiedTarget));
            if (matchedStandard != null && matchedStandard.HasDocument)
            {
                // Check embedded standard document for known standard to determine if the standard peptides should be heavy
                // Import iRT standard document into an empty document (rather than just getting the document), because importing also imports the modifications
                var standardDoc = matchedStandard.ImportTo(new SrmDocument(SrmSettingsList.GetDefault()));
                standardPeptideList = standardPeptideList.Select(pep => new DbIrtPeptide(pep)).ToArray();
                foreach (var dummyPep in standardDoc.Molecules.Where(pep => pep.HasExplicitMods))
                {
                    var standardPepIdx = standardPeptideList.IndexOf(pep => dummyPep.ModifiedTarget.Equals(pep.ModifiedTarget));
                    if (standardPepIdx < 0)
                        continue;
                    var heavyTarget = standardDoc.Settings.GetModifiedSequence(dummyPep.ModifiedTarget, IsotopeLabelType.heavy, dummyPep.ExplicitMods);
                    if (!standardPeptideList[standardPepIdx].ModifiedTarget.Equals(heavyTarget))
                        heavyStandards[standardPepIdx] = new DbIrtPeptide(standardPeptideList[standardPepIdx]) {ModifiedTarget = heavyTarget};
                }
            }

            IProgressStatus status = new ProgressStatus(Resources.LibraryGridViewDriver_ProcessRetentionTimes_Adding_retention_times);
            var dictPeptideAverages = new Dictionary<Target, IrtPeptideAverages>();
            var providerData = new List<RetentionTimeProviderData>();
            var runCount = 0;
            foreach (var retentionTimeProvider in providers)
            {
                if (monitor.IsCanceled)
                    return null;
                monitor.UpdateProgress(status = status.ChangeMessage(string.Format(
                    Resources.LibraryGridViewDriver_ProcessRetentionTimes_Converting_retention_times_from__0__,
                    retentionTimeProvider.Name)));

                runCount++;

                var data = new RetentionTimeProviderData(regressionType, retentionTimeProvider, standardPeptideList, heavyStandards);
                if (data.RegressionSuccess ||
                    (ReferenceEquals(regressionType, IrtRegressionType.LINEAR) && data.CalcRegressionWith(retentionTimeProvider, standardPeptideList, items)))
                {
                    AddRetentionTimesToDict(retentionTimeProvider, data.RegressionRefined, dictPeptideAverages, standardPeptideList);
                }
                providerData.Add(data);

                monitor.UpdateProgress(status = status.ChangePercentComplete(runCount * 100 / providers.Length));
            }

            monitor.UpdateProgress(status.Complete());
            return new ProcessedIrtAverages(dictPeptideAverages, providerData);
        }

        public static ProcessedIrtAverages ProcessRetentionTimesCirt(IProgressMonitor monitor,
            IRetentionTimeProvider[] providers, DbIrtPeptide[] cirtPeptides, int numCirt, IrtRegressionType regressionType, out DbIrtPeptide[] chosenCirtPeptides)
        {
            chosenCirtPeptides = new DbIrtPeptide[0];

            var irts = new TargetMap<DbIrtPeptide>(IrtStandard.CIRT.Peptides.Select(pep => new KeyValuePair<Target, DbIrtPeptide>(pep.ModifiedTarget, pep)));
            var targetRts = new Dictionary<Target, List<double>>();
            var targetCounts = new Dictionary<Target, int>(); // count how many successful regressions each peptide participated in
            foreach (var provider in providers)
            {
                if (monitor.IsCanceled)
                    return null;

                var times = (
                    from pep in cirtPeptides
                    let rt = provider.GetRetentionTime(pep.ModifiedTarget)
                    where rt.HasValue
                    select Tuple.Create(pep.ModifiedTarget, rt.Value, irts[pep.ModifiedTarget].Irt)).ToList();

                foreach (var (target, rt, _) in times)
                {
                    if (targetRts.TryGetValue(target, out var list))
                        list.Add(rt);
                    else
                        targetRts[target] = new List<double> {rt};
                }

                var removed = new List<Tuple<double, double>>();
                if (!IrtRegression.TryGet<RegressionLine>(times.Select(t => t.Item2).ToList(), times.Select(t => t.Item3).ToList(),
                    MIN_PEPTIDES_COUNT, out _, removed))
                    continue;
                foreach (var (removeRt, removeIrt) in removed)
                    times.Remove(times.First(time => time.Item2.Equals(removeRt) && time.Item3.Equals(removeIrt)));
                foreach (var (target, _, _) in times)
                    targetCounts[target] = targetCounts.TryGetValue(target, out var existing) ? existing + 1 : 1;
            }

            // for each target, only keep median retention time
            var dupTargets = targetRts.Where(kvp => kvp.Value.Count > 1).Select(kvp => kvp.Key).ToArray();
            foreach (var target in dupTargets)
                targetRts[target] = new List<double> {new Statistics(targetRts[target]).Median()};

            // separate targets into equal retention time bins
            var candidateBins = new List<Tuple<Target, double>>[numCirt];
            for (var i = 0; i < candidateBins.Length; i++)
                candidateBins[i] = new List<Tuple<Target, double>>();

            var minRt = double.MaxValue;
            var maxRt = -1d;
            foreach (var rt in targetRts.Values.Select(list => list.First()))
            {
                if (rt < minRt)
                    minRt = rt;
                if (rt > maxRt)
                    maxRt = rt;
            }

            var binSize = (maxRt - minRt) / numCirt;
            var lastBinIdx = candidateBins.Length - 1;
            foreach (var target in targetRts)
            foreach (var rt in target.Value)
                candidateBins[Math.Min((int) ((rt - minRt) / binSize), lastBinIdx)].Add(Tuple.Create(target.Key, rt));

            Tuple<Target, double> GetBest(List<Tuple<Target, double>> bin, int binIdx, out int bestCount, out double bestRtDelta)
            {
                if (bin.Count == 0)
                {
                    bestCount = 0;
                    bestRtDelta = 0;
                    return null;
                }

                bestCount = 0;
                var filtered = new List<Tuple<Target, double>>();
                foreach (var t in bin)
                {
                    if (!targetCounts.TryGetValue(t.Item1, out var count))
                        continue;
                    if (count > bestCount)
                    {
                        bestCount = count;
                        filtered.Clear();
                        filtered.Add(t);
                    }
                    else if (count == bestCount)
                    {
                        filtered.Add(t);
                    }
                }
                if (filtered.Count == 0)
                    filtered = bin;
                var targetRt = ((minRt + binSize * binIdx) + (minRt + binSize * (binIdx + 1))) / 2;
                var closest = filtered.Aggregate((x, y) => Math.Abs(x.Item2 - targetRt) < Math.Abs(y.Item2 - targetRt) ? x : y);
                bestRtDelta = Math.Abs(closest.Item2 - targetRt);
                return closest;
            }

            var chosenList = new List<DbIrtPeptide>();
            var emptyBins = new HashSet<int>();
            for (var i = 0; i < candidateBins.Length; i++)
            {
                var bin = candidateBins[i];
                if (bin.Count > 0)
                {
                    // choose the best from this bin
                    var best = GetBest(bin, i, out _, out _);
                    chosenList.Add(irts[best.Item1]);
                    bin.Remove(best);
                }
                else
                {
                    emptyBins.Add(i);
                }
            }
            foreach (var emptyIdx in emptyBins)
            {
                var bins = new List<int>();
                var left = emptyIdx - 1;
                var right = emptyIdx + 1;
                while (bins.Count == 0)
                {
                    if (left >= 0)
                        bins.Add(left);
                    if (right < candidateBins.Length)
                        bins.Add(right);
                    left--;
                    right++;
                }
                Tuple<Target, double> best = null;
                var bestBinIdx = -1;
                var bestCount = 0;
                var bestRtDelta = 0d;
                foreach (var i in bins)
                {
                    var current = GetBest(candidateBins[i], i, out var count, out var rtDelta);
                    if (count > bestCount || (count == bestCount && rtDelta < bestRtDelta))
                    {
                        best = current;
                        bestBinIdx = i;
                        bestCount = count;
                        bestRtDelta = rtDelta;
                    }
                }
                if (best != null)
                {
                    chosenList.Add(irts[best.Item1]);
                    candidateBins[bestBinIdx].Remove(best);
                }
            }

            // Process retention times using the chosen peptides
            chosenCirtPeptides = chosenList.ToArray();
            return ProcessRetentionTimes(monitor, providers, chosenCirtPeptides, new DbIrtPeptide[0], regressionType);
        }

        private static void AddRetentionTimesToDict(IRetentionTimeProvider retentionTimes,
            IRegressionFunction regression,
            IDictionary<Target, IrtPeptideAverages> dictPeptideAverages,
            IEnumerable<DbIrtPeptide> standardPeptideList)
        {
            var setStandards = new TargetMap<bool>(standardPeptideList.Select(peptide => new KeyValuePair<Target, bool>(peptide.Target, true)));
            foreach (var pepTime in retentionTimes.PeptideRetentionTimes.Where(p => !setStandards.ContainsKey(p.PeptideSequence)))
            {
                var peptideModSeq = pepTime.PeptideSequence;
                var timeSource = retentionTimes.GetTimeSource(peptideModSeq);
                var irt = regression.GetY(pepTime.RetentionTime);
                if (!dictPeptideAverages.TryGetValue(peptideModSeq, out var pepAverage))
                    dictPeptideAverages.Add(peptideModSeq, new IrtPeptideAverages(peptideModSeq, irt, timeSource));
                else
                    pepAverage.AddIrt(irt);
            }
        }

        public static RCalcIrt Calculator(SrmDocument document)
        {
            if (!document.Settings.HasRTPrediction)
                return null;
            return document.Settings.PeptideSettings.Prediction.RetentionTime.Calculator as RCalcIrt;
        }

        public static IEnumerable<Target> IrtPeptides(SrmDocument document)
        {
            var calc = Calculator(document);
            if (calc == null)
                yield break;

            try
            {
                calc = calc.Initialize(null) as RCalcIrt;
            }
            catch
            {
                yield break;
            }

            if (calc != null)
                foreach (var peptide in calc.GetStandardPeptides())
                    yield return peptide;
        }

        #region Property change methods

        public RCalcIrt ChangeDatabasePath(string path)
        {
            return ChangeProp(ImClone(this), im => im.DatabasePath = path);
        }

        public RCalcIrt ChangeDatabase(IrtDb database)
        {
            return ChangeProp(ImClone(this), im => im._database = database);
        }

        #endregion

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private RCalcIrt()
        {
        }

        enum ATTR
        {
            database_path
        }

        public static RCalcIrt Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new RCalcIrt());
        }

        public override void ReadXml(XmlReader reader)
        {
            base.ReadXml(reader);
            DatabasePath = reader.GetAttribute(ATTR.database_path);
            // Consume tag
            reader.Read();
        }

        public override void WriteXml(XmlWriter writer)
        {
            base.WriteXml(writer);
            writer.WriteAttribute(ATTR.database_path, DatabasePath);
        }

        #endregion

        #region object overrrides

        public bool Equals(RCalcIrt other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return base.Equals(other) && Equals(other._database, _database) && Equals(other.DatabasePath, DatabasePath);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as RCalcIrt);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = base.GetHashCode();
                result = (result*397) ^ DatabasePath.GetHashCode();
                result = (result*397) ^ (_database != null ? _database.GetHashCode() : 0);
                return result;
            }
        }

        #endregion
    }

    public sealed class ProcessedIrtAverages
    {
        public ProcessedIrtAverages(Dictionary<Target, IrtPeptideAverages> dictPeptideIrtAverages, IList<RetentionTimeProviderData> providerData)
        {
            DictPeptideIrtAverages = dictPeptideIrtAverages;
            ProviderData = providerData;
        }

        private Dictionary<Target, IrtPeptideAverages> DictPeptideIrtAverages { get; }
        public IList<RetentionTimeProviderData> ProviderData { get; }

        public IEnumerable<DbIrtPeptide> DbIrtPeptides
        {
            get
            {
                // TODO: Something better than making unknown times source equal to peak
                return from pepAverage in DictPeptideIrtAverages.Values
                    orderby pepAverage.IrtAverage
                    select new DbIrtPeptide(pepAverage.PeptideModSeq, pepAverage.IrtAverage, false, pepAverage.TimeSource ?? TimeSource.peak);
            }
        }

        public bool CanRecalibrateStandards(IList<DbIrtPeptide> standardPeptideList)
        {
            if (!standardPeptideList.Any())
                return false;

            var standards = new HashSet<Target>(standardPeptideList.Select(standard => standard.ModifiedTarget));
            foreach (var data in ProviderData)
            {
                foreach (var peptide in data.FilteredPeptides)
                {
                    if (standards.Remove(peptide.Target) && standards.Count == 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public List<DbIrtPeptide> RecalibrateStandards(DbIrtPeptide[] standardPeptideList)
        {
            var peptideAllIrtTimes = new Dictionary<Target, List<Tuple<double, double>>>(); // peptide -> list of (irt, time)
            foreach (var data in ProviderData)
            {
                foreach (var peptide in data.FilteredPeptides)
                {
                    if (!peptideAllIrtTimes.TryGetValue(peptide.Target, out var pepTimes))
                    {
                        pepTimes = new List<Tuple<double, double>>();
                        peptideAllIrtTimes[peptide.Target] = pepTimes;
                    }
                    pepTimes.Add(Tuple.Create(peptide.Irt, peptide.RetentionTime.Value));
                }
            }
            var peptideBestIrtTimes = new Dictionary<Target, Tuple<double, double>>(); // peptide -> (percentile irt, percentile time)
            foreach (var peptide in peptideAllIrtTimes)
            {
                var statIrts = new Statistics(peptide.Value.Select(p => p.Item1));
                var statTimes = new Statistics(peptide.Value.Select(p => p.Item2));
                var percentile = IrtStandard.GetSpectrumTimePercentile(peptide.Key);
                peptideBestIrtTimes[peptide.Key] = Tuple.Create(statIrts.Percentile(percentile), statTimes.Percentile(percentile));
            }
            DbIrtPeptide min = null, max = null;
            foreach (var standard in standardPeptideList) // loop over list of standard peptides to find min/max that we have values for
            {
                if ((min == null || standard.Irt < min.Irt) && peptideBestIrtTimes.ContainsKey(standard.ModifiedTarget))
                    min = standard;
                if ((max == null || standard.Irt > max.Irt) && peptideBestIrtTimes.ContainsKey(standard.ModifiedTarget))
                    max = standard;
            }
            if (min == null || max == null)
                throw new Exception(Resources.EditIrtCalcDlg_RecalibrateStandards_Could_not_get_a_minimum_or_maximum_standard_peptide_);

            var statX = new Statistics(peptideBestIrtTimes[min.ModifiedTarget].Item2, peptideBestIrtTimes[max.ModifiedTarget].Item2);
            var statY = new Statistics(peptideBestIrtTimes[min.ModifiedTarget].Item1, peptideBestIrtTimes[max.ModifiedTarget].Item1);
            var line = new RegressionLine(statY.Slope(statX), statY.Intercept(statX));
            var newStandardPeptideList = new List<DbIrtPeptide>();
            foreach (var peptide in standardPeptideList)
            {
                if (!peptideBestIrtTimes.TryGetValue(peptide.ModifiedTarget, out var times))
                    throw new Exception(Resources.ProcessedIrtAverages_RecalibrateStandards_A_standard_peptide_was_missing_when_trying_to_recalibrate_);
                newStandardPeptideList.Add(new DbIrtPeptide(peptide) { Irt = line.GetY(times.Item2) });
            }
            return newStandardPeptideList;
        }
    }

    public class IrtPeptideAverages
    {
        private readonly List<double> _irtValues;

        public IrtPeptideAverages(Target peptideModSeq, double irtAverage, TimeSource? timeSource)
        {
            PeptideModSeq = peptideModSeq;
            _irtValues = new List<double> { irtAverage };
            TimeSource = timeSource;
        }

        public Target PeptideModSeq { get; }

        public double IrtAverage => new Statistics(_irtValues).Median();

        public TimeSource? TimeSource { get; }

        public void AddIrt(double irt)
        {
            _irtValues.Add(irt);
        }
    }

    public sealed class RetentionTimeProviderData
    {
        public RetentionTimeProviderData(IrtRegressionType regressionType, IRetentionTimeProvider retentionTimes,
            IReadOnlyList<DbIrtPeptide> standardPeptides, IReadOnlyList<DbIrtPeptide> heavyStandardPeptides)
        {
            RetentionTimeProvider = retentionTimes;

            Peptides = new List<Peptide>(standardPeptides.Count);
            for (var i = 0; i < standardPeptides.Count; i++)
            {
                var heavy = heavyStandardPeptides[i] != null;
                var standard = heavy ? heavyStandardPeptides[i] : standardPeptides[i];
                var rt = retentionTimes.GetRetentionTime(standard.ModifiedTarget);
                if (!rt.HasValue && heavy)
                {
                    standard = standardPeptides[i];
                    rt = retentionTimes.GetRetentionTime(standard.ModifiedTarget);
                }
                Peptides.Add(new Peptide(standard.ModifiedTarget, rt, standard.Irt));
            }
            Peptides.Sort((x, y) => x.Irt.CompareTo(y.Irt));

            if (!FilteredPeptides.Any())
            {
                Regression = null;
                RegressionRefined = null;
                RegressionSuccess = false;
            }

            var filteredRt = FilteredPeptides.Select(pep => pep.RetentionTime.Value).ToList();
            var filteredIrt = FilteredPeptides.Select(pep => pep.Irt).ToList();
            var removed = new List<Tuple<double, double>>();
            if (ReferenceEquals(regressionType, IrtRegressionType.LINEAR))
            {
                Regression = new RegressionLine(filteredRt.ToArray(), filteredIrt.ToArray());
            }
            else if (ReferenceEquals(regressionType, IrtRegressionType.LOGARITHMIC))
            {
                Regression = new LogRegression(filteredRt, filteredIrt);
            }
            else if (ReferenceEquals(regressionType, IrtRegressionType.LOWESS))
            {
                Regression = new LoessRegression(filteredRt.ToArray(), filteredIrt.ToArray());
            }
            else
            {
                throw new ArgumentException();
            }

            IIrtRegression regressionRefined;
            if (IrtRegression.Accept(Regression, MinPoints))
            {
                regressionRefined = Regression;
                Regression = null;
                RegressionSuccess = true;
            }
            else
            {
                RegressionSuccess = IrtRegression.TryGet(Regression, filteredRt, filteredIrt, MinPoints, out regressionRefined, removed);
            }

            RegressionRefined = regressionRefined;
            foreach (var remove in removed)
            {
                for (var i = 0; i < Peptides.Count; i++)
                {
                    var peptide = Peptides[i];
                    if (peptide.RetentionTime.Equals(remove.Item1) && peptide.Irt.Equals(remove.Item2))
                        Peptides[i] = new Peptide(peptide, true);
                }
            }
        }

        public bool CalcRegressionWith(IRetentionTimeProvider retentionTimes, IEnumerable<DbIrtPeptide> standardPeptideList, DbIrtPeptide[] items)
        {
            if (items.Any())
            {
                // Attempt to get a regression based on shared peptides
                var calculator = new CurrentCalculator(standardPeptideList, items);
                var peptidesTimes = retentionTimes.PeptideRetentionTimes.ToArray();
                var regression = RetentionTimeRegression.FindThreshold(
                    RCalcIrt.MIN_IRT_TO_TIME_CORRELATION,
                    RetentionTimeRegression.ThresholdPrecision,
                    peptidesTimes,
                    new MeasuredRetentionTime[0],
                    peptidesTimes,null,
                    calculator,
                    RegressionMethodRT.linear);

                var startingCount = peptidesTimes.Length;
                var regressionCount = regression?.PeptideTimes.Count ?? 0;
                if (regression != null && RCalcIrt.IsAcceptableStandardCount(startingCount, regressionCount))
                {
                    // Finally must recalculate the regression, because it is transposed from what
                    // we want.
                    var statTimes = new Statistics(regression.PeptideTimes.Select(pt => pt.RetentionTime));
                    var statIrts = new Statistics(regression.PeptideTimes.Select(pt =>
                        calculator.ScoreSequence(pt.PeptideSequence) ?? calculator.UnknownScore));

                    RegressionRefined = new RegressionLine(statIrts.Slope(statTimes), statIrts.Intercept(statTimes));
                    RegressionSuccess = true;
                    return true;
                }
            }
            return false;
        }

        public IRetentionTimeProvider RetentionTimeProvider { get; }
        public List<Peptide> Peptides { get; private set; }
        public IEnumerable<Peptide> FilteredPeptides => Peptides.Where(peptide => !peptide.Missing);

        public int MinPoints => RCalcIrt.MinStandardCount(FilteredPeptides.Count());

        public IIrtRegression RegressionRefined { get; private set; }
        public IIrtRegression Regression { get; }
        public bool RegressionSuccess { get; private set; }

        public class Peptide
        {
            public Target Target { get; }
            public double? RetentionTime { get; }
            public double Irt { get; }
            public bool Outlier { get; }

            public Peptide(Target target, double? rt, double irt)
            {
                Target = target;
                RetentionTime = rt;
                Irt = irt;
                Outlier = false;
            }

            public Peptide(Peptide other, bool outlier)
            {
                Target = other.Target;
                RetentionTime = other.RetentionTime;
                Irt = other.Irt;
                Outlier = outlier;
            }

            public bool Missing => !RetentionTime.HasValue;
        }
    }

    public sealed class CurrentCalculator : RetentionScoreCalculatorSpec
    {
        private readonly TargetMap<double> _dictStandards;
        private readonly TargetMap<double> _dictLibrary;

        private readonly double _unknownScore;

        public CurrentCalculator(IEnumerable<DbIrtPeptide> standardPeptides, IEnumerable<DbIrtPeptide> libraryPeptides)
            : base(NAME_INTERNAL)
        {
            _dictStandards = new TargetMap<double>(standardPeptides.Select(pep => new KeyValuePair<Target, double>(pep.ModifiedTarget, pep.Irt)));
            _dictLibrary = new TargetMap<double>(libraryPeptides.Select(pep => new KeyValuePair<Target, double>(pep.ModifiedTarget, pep.Irt)));
            var minStandard = _dictStandards.Values.Min();
            var minLibrary = _dictLibrary.Values.Min();

            // Come up with a value lower than the lowest value, but still within the scale
            // of the measurements.
            _unknownScore = Math.Min(minStandard, minLibrary) - Math.Abs(minStandard - minLibrary);
        }

        public override double UnknownScore { get { return _unknownScore; } }

        public override double? ScoreSequence(Target sequence)
        {
            double irt;
            if (_dictStandards.TryGetValue(sequence, out irt) || _dictLibrary.TryGetValue(sequence, out irt))
                return irt;
            return null;
        }

        public override IEnumerable<Target> ChooseRegressionPeptides(IEnumerable<Target> peptides, out int minCount)
        {
            var peptideArray = peptides.ToArray();
            var returnStandard = peptideArray.Where(_dictStandards.ContainsKey).ToArray();
            var returnCount = returnStandard.Length;
            var standardsCount = _dictStandards.Count;

            if (!RCalcIrt.IsAcceptableStandardCount(standardsCount, returnCount))
            {
                var inStandardButNotTargets = new SortedSet<Target>(_dictStandards.Keys);
                inStandardButNotTargets.ExceptWith(peptideArray);

                throw new IncompleteStandardException(this, standardsCount, inStandardButNotTargets);
            }

            minCount = RCalcIrt.MinStandardCount(standardsCount);
            return returnStandard;
        }

        public override IEnumerable<Target> GetStandardPeptides(IEnumerable<Target> peptides)
        {
            return _dictStandards.Keys;
        }
    }

    public class IncompleteStandardException : CalculatorException
    {
        //This will only be thrown by ChooseRegressionPeptides so it is OK to have an error specific to regressions.
        private static string ERROR => Resources
            .IncompleteStandardException_The_calculator__0__requires_all__1__of_its_standard_peptides_to_be_in_the_targets_list_in_order_to_determine_a_regression_The_following__2__peptides_are_missing___3__;

        public RetentionScoreCalculatorSpec Calculator { get; private set; }

        public IncompleteStandardException(RetentionScoreCalculatorSpec calc, int standardPeptideCount, ICollection<Target> missingPeptides)
            : base(String.Format(ERROR, calc.Name, standardPeptideCount, missingPeptides.Count,
                string.Join(Environment.NewLine, missingPeptides.Select(o => o.Sequence))))
        {
            Calculator = calc;
        }
    }
}
