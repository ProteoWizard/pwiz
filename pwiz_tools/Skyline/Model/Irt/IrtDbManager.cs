/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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
using System.Linq;
using System.Collections.Generic;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Irt
{
    public sealed class IrtDbManager : BackgroundLoader
    {
        public static string IsNotLoadedDocumentExplained(SrmDocument document)
        {
            // Not loaded if the calculator is not usable
            var calc = GetIrtCalculator(document);
            if (calc != null && !calc.IsUsable)
                return @"IrtDbManager: GetIrtCalculator(document) not usable";
            // Auto-calc of all replicates can wait until the bulk load completes
            if (document.Settings.IsResultsJoiningDisabled)
                return null;
            // Not loaded if the regression requires re-calculating of auto-calc regressions
            var rtRegression = document.Settings.PeptideSettings.Prediction.RetentionTime;
            if (rtRegression == null)
                return null;
            if (rtRegression.IsAutoCalcRequired(document, null))
                return @"IrtDbManager: rtRegression IsAutoCalcRequired";
            return null;
        }

        private readonly Dictionary<string, RCalcIrt> _loadedCalculators =
            new Dictionary<string, RCalcIrt>();

        // For use on container shutdown, clear caches to restore minimal memory footprint
        public override void ClearCache()
        {
            lock (_loadedCalculators)
            {
                _loadedCalculators.Clear();
            }
        }

        protected override bool StateChanged(SrmDocument document, SrmDocument previous)
        {
            if (previous == null)
            {
                return true;
            }
            if (!ReferenceEquals(GetIrtCalculator(document), GetIrtCalculator(previous)))
            {
                return true;
            }
            var rtRegression = document.Settings.PeptideSettings.Prediction.RetentionTime;
            if (rtRegression != null && rtRegression.IsAutoCalcRequired(document, previous))
            {
                return true;
            }
            return false;
        }

        protected override string IsNotLoadedExplained(SrmDocument document)
        {
            return IsNotLoadedDocumentExplained(document);
        }

        protected override IEnumerable<IPooledStream> GetOpenStreams(SrmDocument document)
        {
            yield break;
        }

        protected override bool IsCanceled(IDocumentContainer container, object tag)
        {
            return false;
        }

        protected override bool LoadBackground(IDocumentContainer container, SrmDocument document, SrmDocument docCurrent)
        {
            var calc = GetIrtCalculator(docCurrent);
            if (calc != null && !calc.IsUsable)
                calc = LoadCalculator(container, calc);
            if (calc == null || !ReferenceEquals(document.Id, container.Document.Id))
            {
                // Loading was cancelled or document changed
                EndProcessing(document);
                return false;
            }

            var standards = new List<DbIrtPeptide>();
            var library = new List<DbIrtPeptide>();
            foreach (var pep in calc.GetDbIrtPeptides())
            {
                if (pep.Standard)
                    standards.Add(pep);
                else
                    library.Add(pep);
            }

            // Watch out for stale db read
            if (calc.IsUsable && 
                (standards.Any(s => !calc.GetStandardPeptides().Contains(s.ModifiedTarget)) || 
                 library.Any(l => !calc.GetLibraryPeptides().Contains(l.ModifiedTarget))))
            {
                calc = calc.ChangeDatabase(IrtDb.GetIrtDb(calc.DatabasePath, null));
            }

            var duplicates = IrtDb.CheckForDuplicates(standards, library);
            if (duplicates != null && duplicates.Any())
            {
                calc = calc.ChangeDatabase(IrtDb.GetIrtDb(calc.DatabasePath, null).RemoveDuplicateLibraryPeptides());
            }

            var rtRegression = docCurrent.Settings.PeptideSettings.Prediction.RetentionTime;
            var rtRegressionNew = !ReferenceEquals(calc, rtRegression.Calculator)
                ? rtRegression.ChangeCalculator(calc)
                : rtRegression;
            if (rtRegressionNew.IsAutoCalcRequired(docCurrent, null))
                rtRegressionNew = AutoCalcRegressions(container, rtRegressionNew);

            if (rtRegressionNew == null ||
                !ReferenceEquals(document.Id, container.Document.Id) ||
                // No change in the regression, including reference equal standard peptides
                (Equals(rtRegression, rtRegressionNew) && rtRegression.SamePeptides(rtRegressionNew)))
            {
                // Loading was cancelled or document changed
                EndProcessing(document);
                return false;
            }
            SrmDocument docNew;
            do
            {
                // Change the document to use the new calculator and regression information.
                docCurrent = container.Document;
                if (!ReferenceEquals(rtRegression, docCurrent.Settings.PeptideSettings.Prediction.RetentionTime))
                {
                    EndProcessing(document);
                    return false;
                }
                docNew = docCurrent.ChangeSettings(docCurrent.Settings.ChangePeptidePrediction(predict =>
                    predict.ChangeRetentionTime(rtRegressionNew)));
            }
            while (!CompleteProcessing(container, docNew, docCurrent));
            return true;
        }

        private RCalcIrt LoadCalculator(IDocumentContainer container, RCalcIrt calc)
        {
            // TODO: Something better than locking for the entire load
            lock (_loadedCalculators)
            {
                RCalcIrt calcResult;
                if (!_loadedCalculators.TryGetValue(calc.Name, out calcResult))
                {
                    calcResult = (RCalcIrt) calc.Initialize(new LoadMonitor(this, container, calc));
                    if (calcResult != null)
                        _loadedCalculators.Add(calcResult.Name, calcResult);
                }
                return calcResult;
            }
        }

        private static RCalcIrt GetIrtCalculator(SrmDocument document)
        {
            var regressionRT = document.Settings.PeptideSettings.Prediction.RetentionTime;
            if (regressionRT == null)
                return null;
            return regressionRT.Calculator as RCalcIrt;
        }

        private static RetentionTimeRegression AutoCalcRegressions(IDocumentContainer container,
                                                                   RetentionTimeRegression rtRegression)
        {
            var document = container.Document;
            var dictSeqToPeptide = new Dictionary<Target, PeptideDocNode>();
            foreach (var nodePep in document.Molecules)
            {
                if (nodePep.IsDecoy)
                    continue;

                var seqMod = document.Settings.GetSourceTarget(nodePep);
                if (!dictSeqToPeptide.ContainsKey(seqMod))
                    dictSeqToPeptide.Add(seqMod, nodePep);
            }
            int minCount = 0;
            try
            {
                var regressionPeps = rtRegression.Calculator.ChooseRegressionPeptides(dictSeqToPeptide.Keys, out minCount);
                var setRegression = new HashSet<Target>(regressionPeps);
                dictSeqToPeptide = dictSeqToPeptide.Where(p => setRegression.Contains(p.Key))
                                                   .ToDictionary(p => p.Key, p => p.Value);
            }
            catch (IncompleteStandardException)
            {
                // Without a full set of regression peptides, no auto-calculation is possible
                dictSeqToPeptide.Clear();
            }

            var dictStandardPeptides = dictSeqToPeptide.ToDictionary(p => p.Value.Peptide.GlobalIndex, p => p.Value);

            // Must have standard peptides, all with results
            if (dictSeqToPeptide.Count == 0)
                return rtRegression.ClearEquations();
            else if (dictSeqToPeptide.Values.Count(nodePep => nodePep.HasResults) < minCount)
                return rtRegression.ClearEquations(dictStandardPeptides);

            var calculator = rtRegression.Calculator;
            var dictSeqToScore = dictSeqToPeptide.ToDictionary(p => p.Key,
                p => calculator.ScoreSequence(p.Key) ?? calculator.UnknownScore);
            var dictFileIdToCorr = new Dictionary<int, IList<TimeScorePair>>();
            var listPepCorr = new List<TimeScorePair>();
            foreach (var seqToPeptide in dictSeqToPeptide)
            {
                var nodePep = seqToPeptide.Value;
                double? time = nodePep.SchedulingTime;
                if (!time.HasValue)
                    continue;
                double score = dictSeqToScore[seqToPeptide.Key];
                listPepCorr.Add(new TimeScorePair(time.Value, score));

                foreach (var fileId in nodePep.Results.SelectMany(r => r)
                                                      .Select(chromInfo => chromInfo.FileId))
                {
                    IList<TimeScorePair> listTimeScores;
                    if (!dictFileIdToCorr.TryGetValue(fileId.GlobalIndex, out listTimeScores))
                        listTimeScores = dictFileIdToCorr[fileId.GlobalIndex] = new List<TimeScorePair>();
                    time = nodePep.GetSchedulingTime(fileId);
                    if (!time.HasValue)
                        continue;
                    listTimeScores.Add(new TimeScorePair(time.Value, score));
                }
            }

            // If not all standard peptides have at least some retention time value, fail prediction
            if (listPepCorr.Count < minCount)
                return rtRegression.ClearEquations(dictStandardPeptides);

            // Only calculate regressions for files with retention times for all of the standards
            var fileIdToConversions = from p in dictFileIdToCorr
                                       where p.Value.Count == dictSeqToPeptide.Count
                                       select new KeyValuePair<int, RegressionLine>(p.Key, CalcConversion(p.Value, minCount));

            var line = CalcConversion(listPepCorr, minCount);
            return line != null
                ? rtRegression.ChangeEquations(new RegressionLineElement(line), fileIdToConversions, dictStandardPeptides)
                : rtRegression.ChangeEquations(null, fileIdToConversions, dictStandardPeptides).ChangeInsufficientCorrelation(true);
        }

        private static RegressionLine CalcConversion(IList<TimeScorePair> listPepCorr, int minCount)
        {
            var listTime = listPepCorr.Select(p => p.Time).ToList();
            var listScore = listPepCorr.Select(p => p.Score).ToList();

            return IrtRegression.TryGet<RegressionLine>(listScore, listTime, minCount, out var line)
                ? (RegressionLine) line
                : null;
        }

        private struct TimeScorePair
        {
            public TimeScorePair(double time, double score)
                : this()
            {
                Time = time;
                Score = score;
            }

            public double Time { get; private set; }
            public double Score { get; private set; }
        }
    }
}
