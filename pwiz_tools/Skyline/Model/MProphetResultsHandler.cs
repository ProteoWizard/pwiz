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
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model
{
    // Computes mProphet scores for current document and model
    // and handles exporting them to a file or using them to 
    // adjust peak boundaries.
    public class MProphetResultsHandler
    {
        private readonly IList<List<double>> _mProphetScores;
        private readonly IList<List<double>> _pValues;
        private readonly IList<int> _bestIndices;
        private IList<double> _qValues;
        private readonly IList<IPeakFeatureCalculator> _calcs;
        private IEnumerable<PeakTransitionGroupFeatures> _features;

        private const string Q_VALUE_ANNOTATION = "Q Value"; // Not L10N : for now, we are not localizing column headers

        public static string AnnotationName { get { return AnnotationDef.ANNOTATION_PREFIX + Q_VALUE_ANNOTATION; }}

        public MProphetResultsHandler(SrmDocument document, PeakScoringModelSpec scoringModel)
        {
            Document = document;
            ScoringModel = scoringModel;
            _mProphetScores = new List<List<double>>();
            _pValues = new List<List<double>>();
            _bestIndices = new List<int>();
            _calcs = ScoringModel.PeakFeatureCalculators;
        }

        public SrmDocument Document { get; private set; }

        public PeakScoringModelSpec ScoringModel { get; private set; }

        public void ScoreFeatures(IProgressMonitor progressMonitor = null)
        {
            _features = Document.GetPeakFeatures(_calcs, progressMonitor);
            _features = _features.Where(groupFeatures => groupFeatures.PeakGroupFeatures.Any());
            var bestPvalues = new List<double>();
            foreach (var transitionGroupFeatures in _features)
            {
                var mProphetScoresGroup = new List<double>();
                foreach (var peakGroupFeatures in transitionGroupFeatures.PeakGroupFeatures)
                {
                    var featureValues = peakGroupFeatures.Features.Select(value => (double) value).ToArray();
                    mProphetScoresGroup.Add(ScoringModel.Score(featureValues));
                }
                _mProphetScores.Add(mProphetScoresGroup);
                var pValuesGroup = mProphetScoresGroup.Select(score => 1 - Statistics.PNorm(score)).ToList();
                _pValues.Add(pValuesGroup);
                int bestIndex = mProphetScoresGroup.IndexOf(mProphetScoresGroup.Max());
                double bestPvalue = pValuesGroup[bestIndex];
                _bestIndices.Add(bestIndex);
                bestPvalues.Add(bestPvalue);
            }
            _qValues = new Statistics(bestPvalues).Qvalues().ToList();
        }

        public bool IsMissingScores()
        {
            return _qValues.Any(double.IsNaN);
        }

        public SrmDocument ChangePeaks(double qValueCutoff, bool overrideManual = true, bool addAnnotation = false, IProgressMonitor progressMonitor = null)
        {
            var annotationNames = from def in Document.Settings.DataSettings.AnnotationDefs
                                  where def.AnnotationTargets.Contains(AnnotationDef.AnnotationTarget.precursor_result)
                                  select def.Name;
            var containsQAnnotation = annotationNames.Contains(AnnotationName);
            if (!containsQAnnotation && addAnnotation)
            {
                var annotationTargets = AnnotationDef.AnnotationTargetSet.OfValues(AnnotationDef.AnnotationTarget.precursor_result);
                var newAnnotationDef = new AnnotationDef(AnnotationName, annotationTargets, AnnotationDef.AnnotationType.number, new string[0]);
                Document = Document.ChangeSettings(Document.Settings.ChangeAnnotationDefs(defs =>
                {
                    var defsNew = defs.ToList();
                    defsNew.Add(newAnnotationDef);
                    return defsNew;
                }));
            }
            else if (containsQAnnotation && !addAnnotation)
            {
                Document = Document.ChangeSettings(Document.Settings.ChangeAnnotationDefs(defs =>
                {
                    var defsNew = defs.ToList();
                    defsNew.RemoveAll(def => Equals(def.Name, AnnotationName));
                    return defsNew;
                }));
                var annotationNamesToKeep = Document.Settings.DataSettings.AnnotationDefs.Select(def => def.Name).ToList();
                Document = (SrmDocument) ChooseAnnotationsDlg.StripAnnotationValues(annotationNamesToKeep, Document);
            }
            SrmDocument docNew = (SrmDocument) Document.ChangeIgnoreChangingChildren(true);
            var docReference = docNew;
            int i = 0;
            var status = new ProgressStatus(Resources.MProphetResultsHandler_ChangePeaks_Adjusting_peak_boundaries);
            foreach (var transitionGroupFeatures in _features)
            {
                if (progressMonitor != null && i % 10 == 0)
                    progressMonitor.UpdateProgress(status = status.ChangePercentComplete(100 * i / (_qValues.Count + 1)));
                // Pick the highest-scoring peak
                var bestIndex = _bestIndices[i];
                var bestFeature = transitionGroupFeatures.PeakGroupFeatures[bestIndex];
                double? startTime = bestFeature.StartTime;
                double? endTime = bestFeature.EndTime;
                // Read out node and file paths
                var nodePepGroup = transitionGroupFeatures.Id.NodePepGroup;
                var nodePep = transitionGroupFeatures.Id.NodePep;
                var filePath = transitionGroupFeatures.Id.FilePath;
                var nameSet = transitionGroupFeatures.Id.ChromatogramSet.Name;
                var labelType = transitionGroupFeatures.Id.LabelType;
                // Skyline picks no peak at all if none have a low enough q value
                if (_qValues[i] > qValueCutoff)
                {
                    startTime = null;
                    endTime = null;
                }
                // Change all peaks in this group (defined by same label type)
                foreach (TransitionGroupDocNode groupNode in nodePep.Children)
                {
                    // Transition groups belong to same comparable group if 
                    // label types or equal or the label type is light (which is always matched)
                    if (labelType.IsLight || Equals(labelType, groupNode.TransitionGroup.LabelType))
                    {
                        var pepGroupPath = new IdentityPath(IdentityPath.ROOT, nodePepGroup.Id);
                        var pepPath = new IdentityPath(pepGroupPath, nodePep.Id);
                        var groupPath = new IdentityPath(pepPath, groupNode.Id);
                        var fileId = transitionGroupFeatures.Id.ChromatogramSet.FindFile(transitionGroupFeatures.Id.FilePath);
                        // TODO: HACK To annotate peaks with q values.  This is crappy and should be removed in the long run.
                        if(addAnnotation)
                        {
                            var annotations = new Dictionary<string, string> { { AnnotationName, _qValues[i].ToString(CultureInfo.CurrentCulture) } };
                            docNew = docNew.AddPrecursorResultsAnnotations(groupPath, fileId, annotations);
                        }
                        // end HACK
                        var groupInfo = groupNode.ChromInfos.First(info => Equals(info.FileId.GlobalIndex, fileId.GlobalIndex));
                        // If not overriding manual annotations, skip groups that have been set manually
                        if (groupInfo.IsUserSetManual && !overrideManual)
                            continue;
                        docNew = docNew.ChangePeak(groupPath, nameSet, filePath,
                                                   null, startTime, endTime, UserSet.REINTEGRATED, null, false);
                    }
                }
                ++i;
            }
            if (!ReferenceEquals(docNew, docReference))
                Document = (SrmDocument) Document.ChangeIgnoreChangingChildren(false).ChangeChildrenChecked(docNew.Children);
            return Document;
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
                features = Document.GetPeakFeatures(calcs, progressMonitor);
                features = features.Where(groupFeatures => groupFeatures.PeakGroupFeatures.Any());
            }
            WriteHeaderRow(writer, calcs, cultureInfo);
            int i = 0;
            foreach (var peakTransitionGroupFeatures in features)
            {
                WriteTransitionGroup(writer,
                                     peakTransitionGroupFeatures,
                                     cultureInfo,
                                     i,
                                     bestOnly,
                                     includeDecoys);
                ++i;
            }
        }

        private static void WriteHeaderRow(TextWriter writer, IEnumerable<IPeakFeatureCalculator> calcs, CultureInfo cultureInfo)
        {
            char separator = TextUtil.GetCsvSeparator(cultureInfo);
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
                writer.Write(first ? "main_var_{0}" : "var_{0}", peakFeatureCalculator.Name.Replace(" ", "_"));
                first = false;
            }
            writer.WriteLine();
        }

        private void WriteTransitionGroup(TextWriter writer,
                                          PeakTransitionGroupFeatures features,
                                          CultureInfo cultureInfo,
                                          int groupNumber,
                                          bool bestOnly,
                                          bool includeDecoys)
        {
            var mProphetScores = _mProphetScores[groupNumber];
            var pValues = _pValues[groupNumber];
            double qValue = _qValues[groupNumber];
            int bestScoresIndex = _bestIndices[groupNumber];
            if (features.Id.NodePep.IsDecoy && !includeDecoys)
                return;
            int j = 0;
            foreach (var peakGroupFeatures in features.PeakGroupFeatures)
            {
                if (!bestOnly || j == bestScoresIndex)
                    WriteRow(writer, features, peakGroupFeatures, cultureInfo, mProphetScores[j],
                             pValues[j], qValue);
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
                    features.Id.NodePep.Peptide.Sequence,
                    features.Id.NodePep.ModifiedSequence,
                    features.Id.NodePepGroup.Name,
                    Convert.ToString(features.Id.NodePep.IsDecoy ? 1 : 0, cultureInfo),
                    Convert.ToString((float) mProphetScore, cultureInfo),
                    Convert.ToString((float) pValue, cultureInfo),
                    Convert.ToString((float) qValue, cultureInfo),
                };
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
                writer.WriteDsvField(
                    float.IsNaN(featureColumn) ? TextUtil.EXCEL_NA : Convert.ToString(featureColumn, cultureInfo),
                    separator);
                first = false;
            }
            writer.WriteLine();
        }
    }
}