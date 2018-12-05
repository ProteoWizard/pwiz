/*
 * Original author: Dario Amodei <damodei .at. stanford.edu>,
 *                  Mallick Lab, Department of Radiology, Stanford University
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
    public class ComparePeakBoundaries : XmlNamedElement
    {
        public const string APEX_ANNOTATION = "annotation_Apex";
        public const string START_TIME_ANNOTATION = "annotation_StartTime";
        public const string END_TIME_ANNOTATION = "annotation_EndTime";

        public bool IsModel { get; private set; }
        public PeakScoringModelSpec PeakScoringModel { get; private set; }
        public string FileName { get; private set; }
        public string FilePath { get; private set; }
        public SrmDocument DocOriginal { get; private set; }
        public List<PeakBoundsMatch> Matches { get; private set; }
        public PeakBoundaryImporter Importer { get; private set; }
        public bool ApexPresent { get; private set; }

        /// <summary>
        /// For use in lists
        /// </summary>
        public bool IsActive { get; set; }

        public int CountMissing { get { return Importer == null ? 0 : Importer.CountMissing; } }

        public bool HasNoQValues
        {
            get
            {
                return Importer != null && !Importer.AnnotationsAdded.Contains(MProphetResultsHandler.AnnotationName);
            }
        }

        public bool HasNoScores
        {
            get
            {
                return Importer != null && !Importer.AnnotationsAdded.Contains(MProphetResultsHandler.MAnnotationName);
            }
        }

        private SrmDocument _docCompare;

        public ComparePeakBoundaries(PeakScoringModelSpec peakScoringModel) : 
            this(peakScoringModel.Name)
        {
            IsModel = true;
            PeakScoringModel = peakScoringModel;
        }

        public ComparePeakBoundaries(string fileName, string filePath) :
            this(fileName)
        {
            FileName = fileName;
            FilePath = filePath;
        }

        public ComparePeakBoundaries(string name) : base(name)
        {
            IsActive = true;
        }

        public void GenerateComparison(SrmDocument docOriginal, IProgressMonitor progressMonitor)
        {
            DocOriginal = docOriginal;
            _docCompare = docOriginal;
            if (IsModel)
            {
                var handler = new MProphetResultsHandler(DocOriginal, PeakScoringModel);
                handler.ScoreFeatures(progressMonitor, true);
                _docCompare = handler.ChangePeaks(progressMonitor);
            }
            else
            {
                // Add in the necessary annotation definitions so that ImportPeakBoundaries can read them
                var annotationNamesToStrip = new List<string>
                {
                    MProphetResultsHandler.AnnotationName,
                    MProphetResultsHandler.MAnnotationName,
                    APEX_ANNOTATION,
                    START_TIME_ANNOTATION,
                    END_TIME_ANNOTATION
                };
                foreach (var annotationName in annotationNamesToStrip)
                    _docCompare = AddAnnotationIfMissing(_docCompare, annotationName);
                // But strip the values of these annotations if there were any
                var annotationNamesToKeep = _docCompare.Settings.DataSettings.AnnotationDefs
                    .Where(def => !annotationNamesToStrip.Contains(def.Name))
                    .Select(def => def.Name).ToList();
                _docCompare = (SrmDocument)_docCompare.StripAnnotationValues(annotationNamesToKeep);
                Importer = new PeakBoundaryImporter(_docCompare);
                long lineCount = Helpers.CountLinesInFile(FilePath);
                // Peek at the file to see if it has a column called "Apex"
                using (var reader = new StreamReader(FilePath))
                {
                    var line = reader.ReadLine();
                    var fieldNames = PeakBoundaryImporter.FIELD_NAMES.ToList();
                    int fieldsTotal;
                    int[] fieldIndices;
                    var separator = PeakBoundaryImporter.DetermineCorrectSeparator(line, fieldNames,
                        PeakBoundaryImporter.REQUIRED_NO_CHROM, out fieldIndices, out fieldsTotal);
                    ApexPresent = separator != null && fieldIndices[(int) PeakBoundaryImporter.Field.apex_time] != -1;
                }
                _docCompare = Importer.Import(FilePath, progressMonitor, lineCount, true, !ApexPresent);
                if (progressMonitor.IsCanceled)
                    return;
            }
            var matches = new List<PeakBoundsMatch>();
            var truePeptides = DocOriginal.Molecules.ToList();
            var pickedPeptides = _docCompare.Molecules.ToList();
            int nTruePeptides = truePeptides.Count;
            var chromatogramSets = DocOriginal.Settings.MeasuredResults.Chromatograms;
            for (int i = 0; i < chromatogramSets.Count; i++)
            {
                var chromSet = chromatogramSets[i];
                var set = chromatogramSets[i];
                for (int j = 0; j < nTruePeptides; j++)
                {
                    var truePeptide = truePeptides[j];
                    var pickedPeptide = pickedPeptides[j];
                    // We don't care about peak picking performance for decoys
                    if (truePeptide.IsDecoy)
                        continue;
                    // We don't care about peak picking performance for standard peptides
                    if (truePeptide.GlobalStandardType != null)
                        continue;
                    var trueGroups = truePeptide.TransitionGroups.ToList();
                    var pickedGroups = pickedPeptide.TransitionGroups.ToList();
                    for (int k = 0; k < trueGroups.Count; k++)
                    {
                        var trueGroup = trueGroups[k];
                        var pickedGroup = pickedGroups[k];
                        var trueResults = trueGroup.Results[i];
                        var pickedResults = pickedGroup.Results[i];
                        if (trueResults.IsEmpty)
                            continue;
                        int nChromInfos = trueResults.Count;
                        for (int m = 0; m < nChromInfos; m++)
                        {
                            var trueInfo = trueResults[m];
                            var pickedInfo = pickedResults[m];
                            // For TRIC testing at the moment
//                            double? qvaluePicked = PeakBoundsMatch.GetScoreValue(pickedInfo,
//                                MProphetResultsHandler.AnnotationName, ci => pickedInfo.QValue);
//                            if (qvaluePicked.HasValue && qvaluePicked.Value > 0.01)
//                                continue;
                            var fileInfo = set.GetFileInfo(trueInfo.FileId);
                            var filePath = fileInfo.FilePath;
                            var key = new MatchKey(trueGroup.Id.GlobalIndex, trueInfo.FileId.GlobalIndex);
                            matches.Add(new PeakBoundsMatch(trueInfo, pickedInfo, key, chromSet, filePath,
                                trueGroup, truePeptide,HasNoQValues, HasNoScores, ApexPresent));
                        }
                    }
                }
            }
            Matches = matches;
            // Detect if the apex time is in seconds, and if so adjust it to minutes
            if (ApexPresent)
            {
                // ReSharper disable PossibleMultipleEnumeration
                var matchesWithApex = matches.Where(match => match.PickedApex.HasValue).ToArray();
                double maxTime = 0;
                if (matchesWithApex.Length > 0)
                    maxTime = matchesWithApex.Select(match => match.PickedApex.Value).Max();
                if (maxTime > PeakBoundaryImporter.GetMaxRt(DocOriginal))
                {
                    foreach (var match in matchesWithApex)
                    {
                        // If the end is greater than the Apex, then it must also be in seconds (otherwise minutes)
                        if (match.PickedEndBoundary.HasValue && match.PickedEndBoundary > match.PickedApex)
                            match.PickedEndBoundary = match.PickedEndBoundary / 60;
                        match.PickedApex = match.PickedApex / 60;
                        // If the start is now greater than the apex, then it must also be in seconds (otherwise minutes)
                        if (match.PickedStartBoundary.HasValue && match.PickedStartBoundary > match.PickedApex)
                            match.PickedStartBoundary = match.PickedStartBoundary / 60;
                    }
                }
                // ReSharper restore PossibleMultipleEnumeration
            }
        }

        public void Clear()
        {
            DocOriginal = null;
            _docCompare = null;
            Matches.Clear();
        }

        public static SrmDocument AddAnnotationIfMissing(SrmDocument docOriginal, string annotationName)
        {
            SrmDocument docNew = docOriginal;
            var annotationNames = from def in docOriginal.Settings.DataSettings.AnnotationDefs
                                  where def.AnnotationTargets.Contains(AnnotationDef.AnnotationTarget.precursor_result)
                                  select def.Name;
            var containsAnnotation = annotationNames.Contains(annotationName);
            if (!containsAnnotation)
            {
                var annotationTargets = AnnotationDef.AnnotationTargetSet.OfValues(AnnotationDef.AnnotationTarget.precursor_result);
                var newAnnotationDef = new AnnotationDef(annotationName, annotationTargets, AnnotationDef.AnnotationType.number, new string[0]);
                docNew = docOriginal.ChangeSettings(docOriginal.Settings.ChangeAnnotationDefs(defs =>
                {
                    var defsNew = defs.ToList();
                    defsNew.Add(newAnnotationDef);
                    return defsNew;
                }));
            }
            return docNew;
        }
    }

    public class PeakBoundsMatch
    {
        public const double DELTA = 1e-4;
        public const double PERCENT_HIGH_OVERLAP = 0.5;
        public const double MAX_APEX_DELTA = 20.0 / 60.0;

        private readonly ChromatogramSet _chromatogramSet;

        public TransitionGroupChromInfo ChromInfoTrue { get; private set; }
        public TransitionGroupChromInfo ChromInfoPicked { get; private set; }
        public MatchKey Key { get; private set; }
        public MsDataFileUri FilePath { get; private set; }
        public TransitionGroupDocNode NodeGroup { get; private set; }

        public PeptideDocNode NodePep { get; private set; }

        public double? QValue { get; private set; }
        public double? Score { get; private set; }
        public double? PickedApex { get; set; }
        public double? PickedStartBoundary { get; set; }
        public double? PickedEndBoundary { get; set; }

        public string FileName { get { return FilePath.GetFileNameWithoutExtension(); } }
        public string ReplicateName { get { return _chromatogramSet.Name; } }
        public Target ModifiedSequence { get { return NodePep.ModifiedTarget; }}
        public Target Sequence { get { return NodeGroup.Peptide.Target; } }
        public Adduct Charge { get { return NodeGroup.TransitionGroup.PrecursorAdduct; } }
        public int TargetIndex { get { return NodeGroup.TransitionGroup.GlobalIndex; } }
        public double? TrueApex { get { return ChromInfoTrue.RetentionTime; } }
        public double? TrueStartBoundary { get { return ChromInfoTrue.StartRetentionTime; } }
        public double? TrueEndBoundary { get { return ChromInfoTrue.EndRetentionTime; } }
        public bool IsMissingTruePeak { get { return TrueEndBoundary == null; } }
        public bool IsMissingPickedPeak { get { return PickedApex == null; } }

        public bool IsPickedApexBetweenCuratedBoundaries 
        { 
            get
            {
                return TrueStartBoundary != null &&
                        TrueEndBoundary != null &&
                        PickedApex != null &&
                        TrueStartBoundary - DELTA <= PickedApex &&
                        PickedApex <= TrueEndBoundary + DELTA;
            } 
        }

        public bool IsCuratedApexBetweenPickedBoundaries
        {
            get
            {
                return PickedStartBoundary != null &&
                    PickedEndBoundary != null &&
                    TrueApex != null &&
                    PickedStartBoundary - DELTA <= TrueApex &&
                    TrueApex <= PickedEndBoundary + DELTA;
            }
        }

        public bool IsClosePeakApex
        {
            get
            {
                return PickedApex != null && TrueApex != null &&
                       Math.Abs(PickedApex.Value - TrueApex.Value) < MAX_APEX_DELTA;
            }
        }

        public bool IsHighlyOverlapping
        {
            get
            {
                if (!PickedStartBoundary.HasValue || !TrueStartBoundary.HasValue)
                    return false;

                double rangeTotal = (TrueEndBoundary.Value - TrueStartBoundary.Value) +
                    (PickedEndBoundary.Value - PickedStartBoundary.Value);
                double rangeIntersect = Math.Min(TrueEndBoundary.Value, PickedEndBoundary.Value) -
                                        Math.Max(TrueStartBoundary.Value, PickedStartBoundary.Value);
                if (rangeIntersect <= 0)
                    return false;
                double rangeHalf = rangeTotal / 2;
                double percentOverlap = rangeIntersect / rangeHalf;
                if (percentOverlap < PERCENT_HIGH_OVERLAP)
                    return false;
                return true;
            }
        }

        public bool IsMatch { get { return PickedStartBoundary.HasValue ? IsMatchOverlap : IsMatchProximity; } }

        private bool IsMatchOverlap { get { return IsPickedApexBetweenCuratedBoundaries || IsCuratedApexBetweenPickedBoundaries || IsHighlyOverlapping; } }
        private bool IsMatchProximity { get { return IsPickedApexBetweenCuratedBoundaries || IsCuratedApexBetweenPickedBoundaries || IsClosePeakApex; } }

        public bool IsFalsePositive { get { return !IsMatch && !IsMissingPickedPeak; } }

        public PeakBoundsMatch(TransitionGroupChromInfo chromInfoTrue, 
                               TransitionGroupChromInfo chromInfoPicked, 
                               MatchKey key, 
                               ChromatogramSet chromSet,
                               MsDataFileUri filePath, 
                               TransitionGroupDocNode nodeGroup, 
                               PeptideDocNode nodePep,
                               bool hasNoQValues, 
                               bool hasNoScores,
                               bool apexPresent)
        {
            _chromatogramSet = chromSet;

            ChromInfoTrue = chromInfoTrue;
            ChromInfoPicked = chromInfoPicked;
            Key = key;
            FilePath = filePath;
            NodeGroup = nodeGroup;
            NodePep = nodePep;
            // Read apex
            if (apexPresent)
            {
                PickedApex = ParsePickedTimeAnnotation(ComparePeakBoundaries.APEX_ANNOTATION);
                PickedStartBoundary = ParsePickedTimeAnnotation(ComparePeakBoundaries.START_TIME_ANNOTATION);
                PickedEndBoundary = ParsePickedTimeAnnotation(ComparePeakBoundaries.END_TIME_ANNOTATION);
            }
            else
            {
                PickedApex = ChromInfoPicked.RetentionTime;
                PickedStartBoundary = ChromInfoPicked.StartRetentionTime;
                PickedEndBoundary = ChromInfoPicked.EndRetentionTime;
            }
            QValue = GetScoreValue(ChromInfoPicked, MProphetResultsHandler.AnnotationName, ci => ci.QValue);
            if (QValue == null && !IsMissingPickedPeak && !hasNoQValues)
            {
                throw new IOException(string.Format(
                    Resources.PeakBoundsMatch_QValue_Unable_to_read_q_value_annotation_for_peptide__0__of_file__1_,
                    ModifiedSequence, FileName));
            }
            Score = GetScoreValue(ChromInfoPicked, MProphetResultsHandler.MAnnotationName, ci => ci.ZScore);
            if (Score == null && !IsMissingPickedPeak && !hasNoScores)
            {
                throw new IOException(string.Format(
                    Resources.PeakBoundsMatch_PeakBoundsMatch_Unable_to_read_a_score_annotation_for_peptide__0__of_file__1_, 
                    ModifiedSequence, FileName));
            }
        }

        private double? ParsePickedTimeAnnotation(string annotationName)
        {
            string timeText = ChromInfoPicked.Annotations.GetAnnotation(annotationName);
            double time;
            if (double.TryParse(timeText, out time))
                return time;
            else if (double.TryParse(timeText, NumberStyles.Float, CultureInfo.InvariantCulture, out time))
                return time;
            if (timeText == null || timeText.Equals(TextUtil.EXCEL_NA))
                return null;

            throw new IOException(string.Format(Resources.PeakBoundsMatch_PeakBoundsMatch_Unable_to_read_apex_retention_time_value_for_peptide__0__of_file__1__, ModifiedSequence, FileName));
        }

        public static double? GetScoreValue(TransitionGroupChromInfo groupChromInfo, string annotationName, Func<TransitionGroupChromInfo, double?> getNativeValue)
        {
            if (groupChromInfo != null)
            {
                string scoreValueString = groupChromInfo.Annotations.GetAnnotation(annotationName);
                if (scoreValueString == null)
                    return getNativeValue(groupChromInfo);
                double scoreValueDouble;
                if (double.TryParse(scoreValueString, out scoreValueDouble))
                    return scoreValueDouble;
                else if (double.TryParse(scoreValueString, NumberStyles.Float, CultureInfo.InvariantCulture, out scoreValueDouble))
                    return scoreValueDouble;
                if (scoreValueString == @"Infinity") // : Support some of our PeakView tests
                    return double.MaxValue;
                if (scoreValueString == @"-Infinity") // : Support some of our PeakView tests
                    return double.MinValue;
            }
            return null;
        }

        /// <summary>
        /// Sorts descending by score, nulls at the end
        /// </summary>
        /// <param name="match1">First peak bounds match</param>
        /// <param name="match2">Second peak bounds match</param>
        /// <returns></returns>
        public static int CompareScore(PeakBoundsMatch match1, PeakBoundsMatch match2)
        {
            if (match1.Score == null && match2.Score == null)
                return 0;
            if (match1.Score == null)
                return 1;
            if (match2.Score == null)
                return -1;
            return Comparer<double>.Default.Compare(match2.Score.Value, match1.Score.Value);
        }

        /// <summary>
        /// Sorts ascending by q value, nulls at the end
        /// </summary>
        /// <param name="match1">First peak bounds match</param>
        /// <param name="match2">Second peak bounds match</param>
        /// <returns></returns>
        public static int CompareQValue(PeakBoundsMatch match1, PeakBoundsMatch match2)
        {
            if (match1.QValue == null && match2.QValue == null)
                return 0;
            if (match1.QValue == null)
                return 1;
            if (match2.QValue == null)
                return -1;
            return Comparer<double>.Default.Compare(match1.QValue.Value, match2.QValue.Value);
        }
    }

    public class MatchKey
    {
        public int TransitionGroupId { get; private set; }
        public int FileId { get; private set; }

        public MatchKey(int transitionGroupId, int fileId)
        {
            TransitionGroupId = transitionGroupId;
            FileId = fileId;
        }

        protected bool Equals(MatchKey other)
        {
            return FileId == other.FileId && TransitionGroupId == other.TransitionGroupId;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((MatchKey) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (FileId*397) ^ TransitionGroupId;
            }
        }
    }
}
