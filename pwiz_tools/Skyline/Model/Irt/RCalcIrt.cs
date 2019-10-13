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

        public IEnumerable<KeyValuePair<Target, double>> PeptideScores
        {
            get { return _database != null ? _database.PeptideScores : new KeyValuePair<Target, double>[0]; }
        }

        public bool IsNone
        {
            get { return Name == NONE.Name; }
        }

        public override bool IsUsable
        {
            get { return _database != null; }
        }

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
                var dbPeptides = _database.GetPeptides().ToList();
                var persistPeptides = dbPeptides.Where(pep => pep.Standard).Select(NewPeptide).ToList();
                var dictPeptides = dbPeptides.Where(pep => !pep.Standard).ToDictionary(pep => pep.ModifiedTarget);
                foreach (var nodePep in document.Molecules)
                {
                    var modifiedSeq = document.Settings.GetSourceTarget(nodePep);
                    DbIrtPeptide dbPeptide;
                    if (dictPeptides.TryGetValue(modifiedSeq, out dbPeptide))
                    {
                        persistPeptides.Add(NewPeptide(dbPeptide));
                        // Only add once
                        dictPeptides.Remove(modifiedSeq);
                    }
                }

                irtDbMinimal.AddPeptides(null, persistPeptides);
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

        public static bool TryGetRegressionLine(IList<double> listIndependent, IList<double> listDependent, int minPoints, out RegressionLine line, IList<Tuple<double, double>> removedValues = null)
        {
            line = null;
            if (removedValues != null)
                removedValues.Clear();
            if (listIndependent.Count != listDependent.Count || listIndependent.Count < minPoints)
                return false;

            var listX = new List<double>(listIndependent);
            var listY = new List<double>(listDependent);

            double correlation;
            while (true)
            {
                var statIndependent = new Statistics(listX);
                var statDependent = new Statistics(listY);
                line = new RegressionLine(statDependent.Slope(statIndependent), statDependent.Intercept(statIndependent));
                correlation = statDependent.R(statIndependent);

                if (correlation >= MIN_IRT_TO_TIME_CORRELATION || listX.Count <= minPoints)
                    break;

                var furthest = 0;
                var maxDistance = 0.0;
                for (var i = 0; i < listY.Count; i++)
                {
                    var distance = Math.Abs(line.GetY(listX[i]) - listY[i]);
                    if (distance > maxDistance)
                    {
                        furthest = i;
                        maxDistance = distance;
                    }
                }

                if (removedValues != null)
                    removedValues.Add(new Tuple<double, double>(listX[furthest], listY[furthest]));
                listX.RemoveAt(furthest);
                listY.RemoveAt(furthest);
            }

            return correlation >= MIN_IRT_TO_TIME_CORRELATION;
        }

        public override IEnumerable<Target> ChooseRegressionPeptides(IEnumerable<Target> peptides, out int minCount)
        {
            RequireUsable();

            var returnStandard = peptides.Where(_database.IsStandard).Distinct().ToArray();
            var returnCount = returnStandard.Length;
            var databaseCount = _database.StandardPeptideCount;

            if (!IsAcceptableStandardCount(databaseCount, returnCount))
                throw new IncompleteStandardException(this);

            minCount = MinStandardCount(databaseCount);
            return returnStandard;
        }

        public override IEnumerable<Target> GetStandardPeptides(IEnumerable<Target> peptides)
        {
            int minCount;
            return ChooseRegressionPeptides(peptides, out minCount);
        }

        public override double? ScoreSequence(Target seq)
        {
            if (_database != null)
                return _database.ScoreSequence(seq);
            return null;
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
            return _database.GetPeptides();
        }

        public string DocumentXml => _database.DocumentXml;

        public static ProcessedIrtAverages ProcessRetentionTimes(IProgressMonitor monitor,
            IEnumerable<IRetentionTimeProvider> providers, int countProviders,
            DbIrtPeptide[] standardPeptideList, DbIrtPeptide[] items)
        {
            IProgressStatus status = new ProgressStatus(Resources.LibraryGridViewDriver_ProcessRetentionTimes_Adding_retention_times);
            var dictProviderData = new List<KeyValuePair<string, RetentionTimeProviderData>>();
            var dictPeptideAverages = new Dictionary<Target, IrtPeptideAverages>();
            var runCount = 0;
            foreach (var retentionTimeProvider in providers)
            {
                if (monitor.IsCanceled)
                    return null;

                var message = string.Format(Resources.LibraryGridViewDriver_ProcessRetentionTimes_Converting_retention_times_from__0__, retentionTimeProvider.Name);
                monitor.UpdateProgress(status = status.ChangeMessage(message));

                runCount++;
                var data = new RetentionTimeProviderData(retentionTimeProvider, standardPeptideList.OrderBy(peptide => peptide.Irt));
                if (data.RegressionSuccess || data.CalcRegressionWith(retentionTimeProvider, standardPeptideList, items))
                {
                    // Trace.WriteLine(string.Format("slope = {0}, intercept = {1}", data.RegressionRefined.Slope, data.RegressionRefined.Intercept));

                    AddRetentionTimesToDict(retentionTimeProvider, data.RegressionRefined, dictPeptideAverages, standardPeptideList);
                }
                dictProviderData.Add(new KeyValuePair<string, RetentionTimeProviderData>(retentionTimeProvider.Name, data));

                monitor.UpdateProgress(status = status.ChangePercentComplete(runCount * 100 / countProviders));
            }

            monitor.UpdateProgress(status.Complete());

            return new ProcessedIrtAverages(dictPeptideAverages, dictProviderData);
        }

        private static void AddRetentionTimesToDict(IRetentionTimeProvider retentionTimes,
                                                    IRegressionFunction regressionLine,
                                                    IDictionary<Target, IrtPeptideAverages> dictPeptideAverages,
                                                    IEnumerable<DbIrtPeptide> standardPeptideList)
        {
            var setStandards = new TargetMap<bool>(standardPeptideList.Select(peptide => new KeyValuePair<Target, bool>(peptide.Target, true)));
            foreach (var pepTime in retentionTimes.PeptideRetentionTimes.Where(p => !setStandards.ContainsKey(p.PeptideSequence)))
            {
                var peptideModSeq = pepTime.PeptideSequence;
                var timeSource = retentionTimes.GetTimeSource(peptideModSeq);
                var irt = regressionLine.GetY(pepTime.RetentionTime);
                IrtPeptideAverages pepAverage;
                if (!dictPeptideAverages.TryGetValue(peptideModSeq, out pepAverage))
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
        public ProcessedIrtAverages(Dictionary<Target, IrtPeptideAverages> dictPeptideIrtAverages,
                                    IList<KeyValuePair<string, RetentionTimeProviderData>> providerData)
        {
            DictPeptideIrtAverages = dictPeptideIrtAverages;
            ProviderData = providerData;

            RegressionLineCount = 0;
            foreach (var data in ProviderData.Select(p => p.Value))
            {
                var statsX = new Statistics(data.Times);
                var statsY = new Statistics(data.Irts);
                if (statsY.R(statsX) >= RCalcIrt.MIN_IRT_TO_TIME_CORRELATION)
                    RegressionLineCount++;
            }
        }

        private Dictionary<Target, IrtPeptideAverages> DictPeptideIrtAverages { get; set; }
        public IList<KeyValuePair<string, RetentionTimeProviderData>> ProviderData { get; private set; }
        public int RunCount { get { return ProviderData.Count; } }
        public int RegressionLineCount { get; private set; }

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
                var dataCurrent = data;
                foreach (var peptide in data.Value.Peptides.Where((peptide, i) => !dataCurrent.Value.MissingIndices.Contains(i)))
                {
                    if (standards.Remove(peptide) && standards.Count == 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public List<DbIrtPeptide> RecalibrateStandards(IList<DbIrtPeptide> standardPeptideList)
        {
            var peptideAllIrtTimes = new Dictionary<Target, List<Tuple<double, double>>>(); // peptide -> list of (irt, time)
            foreach (var data in ProviderData)
            {
                for (var i = 0; i < data.Value.Peptides.Length; i++)
                {
                    if (data.Value.MissingIndices.Contains(i))
                        continue;

                    var peptide = data.Value.Peptides[i];
                    List<Tuple<double, double>> pepTimes;
                    if (!peptideAllIrtTimes.TryGetValue(peptide, out pepTimes))
                    {
                        pepTimes = new List<Tuple<double, double>>();
                        peptideAllIrtTimes[peptide] = pepTimes;
                    }
                    pepTimes.Add(Tuple.Create(data.Value.Irts[i], data.Value.Times[i]));
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
                Tuple<double, double> times;
                if (!peptideBestIrtTimes.TryGetValue(peptide.ModifiedTarget, out times))
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

        public Target PeptideModSeq { get; private set; }

        public double IrtAverage
        {
            get
            {
                var statIrts = new Statistics(_irtValues);
                return statIrts.Median();
            }
        }

        public TimeSource? TimeSource { get; private set; }

        public void AddIrt(double irt)
        {
            _irtValues.Add(irt);
        }
    }

    public sealed class RetentionTimeProviderData
    {
        public RetentionTimeProviderData(IRetentionTimeProvider retentionTimes, IEnumerable<DbIrtPeptide> standardPeptides)
        {
            RetentionTimeProvider = retentionTimes;

            // Attempt to get regression based on standards
            var listPeptides = new List<Target>();
            var listTimes = new List<double>();
            var listIrts = new List<double>();
            foreach (var standardPeptide in standardPeptides)
            {
                listPeptides.Add(standardPeptide.ModifiedTarget);
                listTimes.Add(retentionTimes.GetRetentionTime(standardPeptide.ModifiedTarget) ?? double.MaxValue);
                listIrts.Add(standardPeptide.Irt);
            }

            var arrayTimes = listTimes.ToArray();
            // var libraryTimes = retentionTimes as LibraryRetentionTimes;
            // if (libraryTimes != null)
            //     Trace.WriteLine(libraryTimes.Name);
            // Trace.WriteLine(string.Format("times = {0}", string.Join(", ", arrayTimes.Select(t => string.Format("{0:F02}", t)))));

            Peptides = listPeptides.ToArray();
            Times = arrayTimes;
            Irts = listIrts.ToArray();
            MissingIndices = new HashSet<int>();
            for (var i = 0; i < Times.Length; i++)
            {
                if (Times[i] == double.MaxValue)
                    MissingIndices.Add(i);
            }
            TimesFiltered = Times.Where((v, i) => !MissingIndices.Contains(i)).ToArray();
            IrtsFiltered = Irts.Where((v, i) => !MissingIndices.Contains(i)).ToArray();

            OutlierIndices = new HashSet<int>();
            if (TimesFiltered.Any())
            {
                var statTimes = new Statistics(TimesFiltered);
                var statIrts = new Statistics(IrtsFiltered);
                Regression = new RegressionLine(statIrts.Slope(statTimes), statIrts.Intercept(statTimes));

                var removed = new List<Tuple<double, double>>();
                RegressionSuccess = RCalcIrt.TryGetRegressionLine(TimesFiltered.ToList(), IrtsFiltered.ToList(), MinPoints, out _regressionRefined, removed);
                foreach (var remove in removed)
                {
                    for (var i = 0; i < Times.Length && i < Irts.Length; i++)
                    {
                        if (Times[i] == remove.Item1 && Irts[i] == remove.Item2)
                            OutlierIndices.Add(i);
                    }
                }
            }
            else
            {
                Regression = null;
                RegressionRefined = null;
                RegressionSuccess = false;
            }
        }

        public bool CalcRegressionWith(IRetentionTimeProvider retentionTimes, IEnumerable<DbIrtPeptide> standardPeptideList, DbIrtPeptide[] items)
        {
            if (items.Any())
            {
                // Attempt to get a regression based on shared peptides
                var calculator = new CurrentCalculator(standardPeptideList, items);
                var peptidesTimes = retentionTimes.PeptideRetentionTimes.ToArray();
                var regression = RetentionTimeRegression.FindThreshold(RCalcIrt.MIN_IRT_TO_TIME_CORRELATION,
                                                                       RetentionTimeRegression.ThresholdPrecision,
                                                                       peptidesTimes,
                                                                       new MeasuredRetentionTime[0],
                                                                       peptidesTimes,null,
                                                                       calculator,
                                                                       RegressionMethodRT.linear, 
                                                                       () => false);

                var startingCount = peptidesTimes.Length;
                var regressionCount = regression != null ? regression.PeptideTimes.Count : 0;
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

        public IRetentionTimeProvider RetentionTimeProvider { get; private set; }

        public Target[] Peptides { get; private set; }
        public double[] Times { get; private set; }
        public double[] Irts { get; private set; }
        private double[] TimesFiltered { get; set; }
        private double[] IrtsFiltered { get; set; }

        public HashSet<int> MissingIndices { get; private set; }
        public HashSet<int> OutlierIndices { get; private set; }

        public int MinPoints
        {
            get
            {
                return RCalcIrt.MinStandardCount(TimesFiltered.Length);
            }
        }

        private RegressionLine _regressionRefined;
        public RegressionLine RegressionRefined { get { return _regressionRefined; } private set { _regressionRefined = value; } }
        public RegressionLine Regression { get; private set; }
        public bool RegressionSuccess { get; private set; }
    }

    public sealed class CurrentCalculator : RetentionScoreCalculatorSpec
    {
        private readonly Dictionary<Target, double> _dictStandards;
        private readonly Dictionary<Target, double> _dictLibrary;

        private readonly double _unknownScore;

        public CurrentCalculator(IEnumerable<DbIrtPeptide> standardPeptides, IEnumerable<DbIrtPeptide> libraryPeptides)
            : base(NAME_INTERNAL)
        {
            _dictStandards = standardPeptides.ToDictionary(p => p.ModifiedTarget, p => p.Irt);
            _dictLibrary = libraryPeptides.ToDictionary(p => p.ModifiedTarget, p => p.Irt);
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
            var returnStandard = peptides.Where(_dictStandards.ContainsKey).ToArray();
            var returnCount = returnStandard.Length;
            var standardsCount = _dictStandards.Count;

            if (!RCalcIrt.IsAcceptableStandardCount(standardsCount, returnCount))
                throw new IncompleteStandardException(this);

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
        private static readonly string ERROR =
            Resources.IncompleteStandardException_ERROR_The_calculator__0__requires_all_of_its_standard_peptides_in_order_to_determine_a_regression_;

        public RetentionScoreCalculatorSpec Calculator { get; private set; }

        public IncompleteStandardException(RetentionScoreCalculatorSpec calc)
            : base(String.Format(ERROR, calc.Name))
        {
            Calculator = calc;
        }
    }

    public class DatabaseNotConnectedException : CalculatorException
    {
        private static readonly string DBERROR =
            Resources.DatabaseNotConnectedException_DBERROR_The_database_for_the_calculator__0__could_not_be_opened__Check_that_the_file__1__was_not_moved_or_deleted_;

        private readonly RetentionScoreCalculatorSpec _calculator;
        public RetentionScoreCalculatorSpec Calculator { get { return _calculator; } }

        public DatabaseNotConnectedException(RCalcIrt calc)
            : base(string.Format(DBERROR, calc.Name, calc.DatabasePath))
        {
            _calculator = calc;
        }
    }

}
