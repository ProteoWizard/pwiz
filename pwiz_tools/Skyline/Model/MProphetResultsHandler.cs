/*
 * Original author: Dario Amodei <damodei .at. standard.edu>,
 *                  Mallick Lab, Department of Radiology, Stanford
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model
{

    /// <summary>
    /// Computes mProphet scores for current document and model
    /// and handles exporting them to a file or using them to 
    /// adjust peak boundaries.
    /// </summary>
    public class MProphetResultsHandler
    {
        private IList<double> _qValues;
        private readonly IList<IPeakFeatureCalculator> _calcs;
        private IList<PeakTransitionGroupFeatures> _features;

        private readonly Dictionary<PeakTransitionGroupIdKey, PeakFeatureStatistics> _featureDictionary; 

        private static bool IsFilterTestData { get { return false; } }

        private const string Q_VALUE_ANNOTATION = "QValue"; // Not L10N : for now, we are not localizing column headers

        public static string AnnotationName { get { return AnnotationDef.ANNOTATION_PREFIX + Q_VALUE_ANNOTATION; }}

        public static string MAnnotationName { get { return AnnotationDef.ANNOTATION_PREFIX + "Score"; } } // Not L10N

        public MProphetResultsHandler(SrmDocument document, PeakScoringModelSpec scoringModel,
            IList<PeakTransitionGroupFeatures> features = null)
        {
            Document = document;
            ScoringModel = scoringModel;
            _calcs = ScoringModel != null
                ? ScoringModel.PeakFeatureCalculators
                : PeakFeatureCalculator.Calculators.ToArray();
            _features = features;
            _featureDictionary = new Dictionary<PeakTransitionGroupIdKey, PeakFeatureStatistics>();

            // Legacy defaults
            QValueCutoff = double.MaxValue;
            OverrideManual = true;
            IncludeDecoys = true;
        }

        public SrmDocument Document { get; private set; }

        public PeakScoringModelSpec ScoringModel { get; private set; }

        public double QValueCutoff { get; set; }
        public bool OverrideManual { get; set; }
        public bool IncludeDecoys { get; set; }
        public bool AddAnnotation { get; set; }
        public bool AddMAnnotation { get; set; }

        public PeakFeatureStatistics GetPeakFeatureStatistics(int pepIndex, int fileIndex)
        {
            PeakFeatureStatistics peakStatistics;
            if (_featureDictionary.TryGetValue(new PeakTransitionGroupIdKey(pepIndex, fileIndex), out peakStatistics))
                return peakStatistics;
            return null;
        }

        public void ScoreFeatures(IProgressMonitor progressMonitor = null)
        {
            if (_features == null)
            {
                _features = Document.GetPeakFeatures(_calcs, progressMonitor);
            }
            if (ScoringModel == null)
                return;

            var bestTargetPvalues = new List<double>();
            var targetIds = new List<PeakTransitionGroupIdKey>();
            foreach (var transitionGroupFeatures in _features)
            {
                var mProphetScoresGroup = new List<double>();
                foreach (var peakGroupFeatures in transitionGroupFeatures.PeakGroupFeatures)
                {
                    mProphetScoresGroup.Add(ScoringModel.Score(peakGroupFeatures.Features));
                }
                var pValuesGroup = mProphetScoresGroup.Select(score => 1 - Statistics.PNorm(score)).ToList();
                int bestIndex = mProphetScoresGroup.IndexOf(mProphetScoresGroup.Max());
                double bestPvalue = pValuesGroup[bestIndex];
                var featureStats = new PeakFeatureStatistics(transitionGroupFeatures, mProphetScoresGroup, pValuesGroup, bestIndex, null);
                _featureDictionary.Add(transitionGroupFeatures.Id.Key, featureStats);
                if (!transitionGroupFeatures.Id.NodePep.IsDecoy)
                {
                    bestTargetPvalues.Add(bestPvalue);
                    targetIds.Add(transitionGroupFeatures.Id.Key);
                }
            }
            _qValues = new Statistics(bestTargetPvalues).Qvalues(MProphetPeakScoringModel.DEFAULT_R_LAMBDA, MProphetPeakScoringModel.PI_ZERO_MIN).ToList();
            for (int i = 0; i < _qValues.Count; ++i)
            {
                var newFeatureStats = _featureDictionary[targetIds[i]].SetQValue(_qValues[i]);
                _featureDictionary[targetIds[i]] = newFeatureStats;
            }
        }

        public bool IsMissingScores()
        {
            return _qValues.Any(double.IsNaN);
        }

        public SrmDocument ChangePeaks(IProgressMonitor progressMonitor = null)
        {
            var annotationNames = (from def in Document.Settings.DataSettings.AnnotationDefs
                                  where def.AnnotationTargets.Contains(AnnotationDef.AnnotationTarget.precursor_result)
                                  select def.Name).ToArray();
            Document = EnsureAnnotation(Document, AnnotationName, AddAnnotation, annotationNames);
            Document = EnsureAnnotation(Document, MAnnotationName, AddAnnotation, annotationNames);
            var settingsChangeMonitor = progressMonitor != null
                ? new SrmSettingsChangeMonitor(progressMonitor, Resources.MProphetResultsHandler_ChangePeaks_Adjusting_peak_boundaries)
                : null;
            using (settingsChangeMonitor)
            {
                var settingsNew = Document.Settings.ChangePeptideIntegration(integraion =>
                    integraion.ChangeResultsHandler(this));
                // Only update the document if anything has changed
                var docNew = Document.ChangeSettings(settingsNew, settingsChangeMonitor);
                if (!Equals(docNew.Settings.PeptideSettings.Integration, Document.Settings.PeptideSettings.Integration) ||
                    !ArrayUtil.ReferencesEqual(docNew.Children, Document.Children))
                {
                    Document = docNew;
                }
                return Document;
            }
        }

        private SrmDocument EnsureAnnotation(SrmDocument document, string annotationName, bool addAnnotation,
            IEnumerable<string> annotationNames)
        {
            var containsQAnnotation = annotationNames.Contains(annotationName);
            if (!containsQAnnotation && addAnnotation)
            {
                var annotationTargets =
                    AnnotationDef.AnnotationTargetSet.OfValues(AnnotationDef.AnnotationTarget.precursor_result);
                var newAnnotationDef = new AnnotationDef(annotationName, annotationTargets, AnnotationDef.AnnotationType.number,
                    new string[0]);
                AnnotationDef existingAnnotationDef;
                // CONSIDER: Throw error instead of overwriting?
                if (!Settings.Default.AnnotationDefList.TryGetValue(annotationName, out existingAnnotationDef) &&
                    !Equals(existingAnnotationDef, newAnnotationDef))
                {
                    Settings.Default.AnnotationDefList.SetValue(newAnnotationDef);
                }
                else
                {
                    // Use the existing annotation
                    newAnnotationDef = existingAnnotationDef;
                }

                document = document.ChangeSettings(Document.Settings.ChangeAnnotationDefs(defs =>
                {
                    var defsNew = defs.ToList();
                    defsNew.Add(newAnnotationDef);
                    return defsNew;
                }));
            }
            else if (containsQAnnotation && !AddAnnotation)
            {
                document = document.ChangeSettings(Document.Settings.ChangeAnnotationDefs(defs =>
                {
                    var defsNew = defs.ToList();
                    defsNew.RemoveAll(def => Equals(def.Name, annotationName));
                    return defsNew;
                }));
                var annotationNamesToKeep = document.Settings.DataSettings.AnnotationDefs.Select(def => def.Name).ToList();
                document = (SrmDocument) document.StripAnnotationValues(annotationNamesToKeep);
            }
            return document;
        }

        public void WriteScores(TextWriter writer,
                                CultureInfo cultureInfo,
                                IList<IPeakFeatureCalculator> calcs = null,
                                bool bestOnly = true,
                                bool includeDecoys = true,
                                IProgressMonitor progressMonitor = null)
        {
            IEnumerable<PeakTransitionGroupFeatures> features;
            if (calcs == null)
            {
                calcs = _calcs;
                features = _features;
            }
            else
            {
                features = Document.GetPeakFeatures(calcs, progressMonitor, IsFilterTestData);
            }
            WriteHeaderRow(writer, calcs, cultureInfo);
            foreach (var peakTransitionGroupFeatures in features)
            {
                WriteTransitionGroup(writer,
                                     peakTransitionGroupFeatures,
                                     cultureInfo,
                                     bestOnly,
                                     includeDecoys);
            }
        }

        private static void WriteHeaderRow(TextWriter writer, IEnumerable<IPeakFeatureCalculator> calcs, CultureInfo cultureInfo)
        {
            char separator = TextUtil.GetCsvSeparator(cultureInfo);
            // ReSharper disable NonLocalizedString
            var namesArray = new List<string>
                {
                    "transition_group_id",
                    "run_id",
                    "FileName",
                    "RT",
                    "MinStartTime",
                    "MaxEndTime",
                    "Sequence",
                    "PeptideModifiedSequence",
                    "ProteinName",
                    "PrecursorIsDecoy",
                    "mProphetScore",
                    "pValue",
                    "qValue"
                };
            if (IsFilterTestData)
            {
                namesArray.Add("FoundIonFilters");
            }
            // ReSharper restore NonLocalizedString

            foreach (var name in namesArray)
            {
                writer.WriteDsvField(name, separator);
                writer.Write(separator);
            }
            bool first = true;
            foreach (var peakFeatureCalculator in calcs)
            {
                if (!first)
                    writer.Write(separator);
                writer.Write(first ? "main_var_{0}" : "var_{0}", peakFeatureCalculator.HeaderName.Replace(" ", "_")); // Not L10N
                first = false;
            }
            writer.WriteLine();
        }

        private void WriteTransitionGroup(TextWriter writer,
                                          PeakTransitionGroupFeatures features,
                                          CultureInfo cultureInfo,
                                          bool bestOnly,
                                          bool includeDecoys)
        {
            var id = features.Id.Key;
            int bestScoresIndex = -1;
            IList<double> mProphetScores = null;
            IList<double> pValues = null;
            double qValue = double.NaN;
            PeakFeatureStatistics peakFeatureStatistics;
            if (_featureDictionary.TryGetValue(id, out peakFeatureStatistics))
            {
                bestScoresIndex = peakFeatureStatistics.BestScoreIndex;
                mProphetScores = peakFeatureStatistics.MprophetScores;
                pValues = peakFeatureStatistics.PValues;
                qValue = peakFeatureStatistics.QValue ?? double.NaN;
            }
            if (features.Id.NodePep.IsDecoy && !includeDecoys)
                return;
            int j = 0;
            foreach (var peakGroupFeatures in features.PeakGroupFeatures)
            {
                if (!bestOnly || j == bestScoresIndex)
                {
                    double mProphetScore = mProphetScores != null ? mProphetScores[j] : double.NaN;
                    double pValue = pValues != null ? pValues[j] : double.NaN;
                    WriteRow(writer, features, peakGroupFeatures, cultureInfo, mProphetScore,
                             pValue, qValue);
                }
                ++j;
            }
        }

        private static void WriteRow(TextWriter writer,
                                     PeakTransitionGroupFeatures features,
                                     PeakGroupFeatures peakGroupFeatures,
                                     CultureInfo cultureInfo,
                                     double mProphetScore,
                                     double pValue,
                                     double qValue)
        {
            char separator = TextUtil.GetCsvSeparator(cultureInfo);
            string fileName = SampleHelp.GetFileName(features.Id.FilePath);
            var fieldsArray = new List<string>
                {
                    Convert.ToString(features.Id, cultureInfo),
                    Convert.ToString(features.Id.Run, cultureInfo),
                    fileName,
                    Convert.ToString(peakGroupFeatures.RetentionTime, cultureInfo),
                    Convert.ToString(peakGroupFeatures.StartTime, cultureInfo),
                    Convert.ToString(peakGroupFeatures.EndTime, cultureInfo),
                    features.Id.NodePep.RawUnmodifiedTextId, // Unmodified sequence, or custom ion name
                    features.Id.NodePep.RawTextId, // Modified sequence, or custom ion name
                    features.Id.NodePepGroup.Name,
                    Convert.ToString(features.Id.NodePep.IsDecoy ? 1 : 0, cultureInfo),
                    ToFieldString((float) mProphetScore, cultureInfo),
                    ToFieldString((float) pValue, cultureInfo),
                    ToFieldString((float) qValue, cultureInfo)
                };
            if (IsFilterTestData)
            {
                fieldsArray.Add(peakGroupFeatures.GetFilterPairsText(cultureInfo));
            }

            foreach (var name in fieldsArray)
            {
                writer.WriteDsvField(name, separator);
                writer.Write(separator);
            }
            bool first = true;
            foreach (float featureColumn in peakGroupFeatures.Features)
            {
                if (!first)
                    writer.Write(separator);
                writer.WriteDsvField(ToFieldString(featureColumn, cultureInfo), separator);
                first = false;
            }
            writer.WriteLine();
        }

        private static string ToFieldString(float f, IFormatProvider cultureInfo)
        {
            return float.IsNaN(f) ? TextUtil.EXCEL_NA : Convert.ToString(f, cultureInfo);
        }
    }

    public class PeakFeatureStatistics
    {
        public PeakFeatureStatistics(PeakTransitionGroupFeatures features, IList<double> mprophetScores, IList<double> pValues, int bestScoreIndex, double? qValue)
        {
            Features = features;
            MprophetScores = mprophetScores;
            PValues = pValues;
            BestScoreIndex = bestScoreIndex;
            QValue = qValue;
        }

        public PeakTransitionGroupFeatures Features { get; private set; }
        public IList<double> MprophetScores { get; private set; }
        public IList<double> PValues { get; private set; }
        public int BestPeakIndex { get { return Features.PeakGroupFeatures[BestScoreIndex].OriginalPeakIndex; } }
        public int BestScoreIndex { get; private set; }
        public double BestScore { get { return MprophetScores[BestScoreIndex]; } }
        public double? QValue { get; private set; }

        public PeakFeatureStatistics SetQValue(double? qValue)
        {
            QValue = qValue;
            return this;
        }
    }
}