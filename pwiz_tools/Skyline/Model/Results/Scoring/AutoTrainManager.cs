/*
 * Original author: Kaipo Tamura <kaipot .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Results.Scoring
{
    public class AutoTrainManager : BackgroundLoader, IFeatureScoreProvider
    {
        private SrmDocument _document;
        private FeatureCalculators _cacheCalculators;
        private PeakTransitionGroupFeatureSet _cachedFeatureScores;

        public override void ClearCache()
        {
        }

        protected override bool StateChanged(SrmDocument document, SrmDocument previous)
        {
            return document.IsLoaded != previous?.IsLoaded;
        }

        protected override string IsNotLoadedExplained(SrmDocument document)
        {
            if (document.Settings.PeptideSettings.Integration.IsAutoTrain &&
                document.Settings.HasResults && document.IsLoaded)
            {
                return @"AutoTrainManager: Model not trained";
            }
            return null;
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
            SrmDocument docNew;
            var loadMonitor = new LoadMonitor(this, container, container.Document);

            bool End(string message)
            {
                // Show an error message and set the AutoTrain flag to false.
                IProgressStatus status = new ProgressStatus();
                if (!string.IsNullOrEmpty(message))
                    status = status.ChangeWarningMessage(message);
                UpdateProgress(status);
                do
                {
                    docCurrent = container.Document;
                    docNew = docCurrent.ChangeSettings(docCurrent.Settings.ChangePeptideIntegration(i => i.ChangeAutoTrain(PeptideIntegration.AutoTrainType.none)));
                } while (!CompleteProcessing(container, docNew, docCurrent));
                UpdateProgress(status.Complete());
                return true;
            }

            if (docCurrent.Settings.PeptideSettings.Integration.PeakScoringModel.IsTrained)
                // already have a trained model
                return End(null);
            else if (!docCurrent.Molecules.Any(molecule => molecule.IsDecoy))
                // user removed the decoys
                return End(TextUtil.LineSeparate(
                    Resources.ImportPeptideSearchManager_LoadBackground_The_decoys_have_been_removed_from_the_document__so_the_peak_scoring_model_will_not_be_automatically_trained_,
                    Resources.ImportPeptideSearchManager_LoadBackground_If_you_re_add_decoys_to_the_document_you_can_add_and_train_a_peak_scoring_model_manually_));

            var modelName = Path.GetFileNameWithoutExtension(container.DocumentFilePath);
            var scoringModel = Equals(document.Settings.PeptideSettings.Integration.AutoTrain, PeptideIntegration.AutoTrainType.mprophet_model)
                ? new MProphetPeakScoringModel(modelName, null as LinearModelParams, MProphetPeakScoringModel.GetDefaultCalculators(docCurrent), true)
                : (IPeakScoringModel)new LegacyScoringModel(modelName);

            var targetDecoyGenerator = new TargetDecoyGenerator(docCurrent, scoringModel, this, loadMonitor);

            // Get scores for target and decoy groups.
            targetDecoyGenerator.GetTransitionGroups(out var targetTransitionGroups, out var decoyTransitionGroups);
            if (!targetTransitionGroups.Any())
                return End(string.Format(
                    Resources.ImportPeptideSearchManager_LoadBackground_An_error_occurred_while_training_the_peak_scoring_model___0_,
                    Resources.AutoTrainManager_LoadBackground_None_of_the_targets_in_the_document_have_any_chromatograms_));
            else if (!decoyTransitionGroups.Any())
                return End(string.Format(
                    Resources.ImportPeptideSearchManager_LoadBackground_An_error_occurred_while_training_the_peak_scoring_model___0_,
                    Resources.AutoTrainManager_LoadBackground_None_of_the_decoys_in_the_document_have_any_chromatograms_));

            // Set intial weights based on previous model (with NaN's reset to 0)
            var initialWeights = new double[scoringModel.PeakFeatureCalculators.Count];
            // But then set to NaN the weights that have unknown values for this dataset
            for (var i = 0; i < initialWeights.Length; ++i)
            {
                if (!targetDecoyGenerator.EligibleScores[i])
                    initialWeights[i] = double.NaN;
            }
            var initialParams = new LinearModelParams(initialWeights);

            // Train the model.
            try
            {
                scoringModel = scoringModel.Train(targetTransitionGroups, decoyTransitionGroups, targetDecoyGenerator,
                    initialParams, null, null, scoringModel.UsesSecondBest, true, loadMonitor);
            }
            catch (Exception x)
            {
                return End(string.Format(Resources.ImportPeptideSearchManager_LoadBackground_An_error_occurred_while_training_the_peak_scoring_model___0_, x.Message));
            }

            do
            {
                docCurrent = container.Document;
                docNew = docCurrent.ChangeSettings(docCurrent.Settings.ChangePeptideIntegration(i =>
                    i.ChangeAutoTrain(PeptideIntegration.AutoTrainType.none).ChangePeakScoringModel((PeakScoringModelSpec)scoringModel)));

                // Reintegrate peaks
                var resultsHandler = new MProphetResultsHandler(docNew, (PeakScoringModelSpec)scoringModel, _cachedFeatureScores);
                resultsHandler.ScoreFeatures(loadMonitor);
                if (resultsHandler.IsMissingScores())
                {
                    return End(Resources.ImportPeptideSearchManager_LoadBackground_The_current_peak_scoring_model_is_incompatible_with_one_or_more_peptides_in_the_document_);
                }
                docNew = resultsHandler.ChangePeaks(loadMonitor);
            }
            while (!CompleteProcessing(container, docNew, docCurrent));

            return true;
        }

        public PeakTransitionGroupFeatureSet GetFeatureScores(SrmDocument document, IPeakScoringModel scoringModel,
            IProgressMonitor progressMonitor)
        {
            if (!ReferenceEquals(document, _document) ||
                !Equals(_cacheCalculators, scoringModel.PeakFeatureCalculators))
            {
                _document = document;
                _cacheCalculators = scoringModel.PeakFeatureCalculators;
                _cachedFeatureScores = document.GetPeakFeatures(_cacheCalculators, progressMonitor);
            }
            return _cachedFeatureScores;
        }

        // Return the type of peak scoring model that was automatically trained between the previous and current document, if any.
        public static PeptideIntegration.AutoTrainType CompletedType(SrmDocument current, SrmDocument previous)
        {
            if (current == null || previous == null)
                return PeptideIntegration.AutoTrainType.none;

            var curIntegration = current.Settings.PeptideSettings.Integration;
            var prevIntegration = previous.Settings.PeptideSettings.Integration;

            return prevIntegration.IsAutoTrain && !curIntegration.IsAutoTrain && !Equals(curIntegration.PeakScoringModel, LegacyScoringModel.DEFAULT_UNTRAINED_MODEL)
                ? prevIntegration.AutoTrain
                : PeptideIntegration.AutoTrainType.none;
        }
    }
}
