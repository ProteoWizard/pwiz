/*
 * Original author: Dario Amodei <damodei .at. stanford.edu>,
 *                  Mallick Lab, Department of Radiology, Stanford University
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

using System.Collections.Generic;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using System.Linq;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.Find
{
    /// <summary>
    /// Finds peptides 
    /// </summary>
    public class MissingScoresFinder : AbstractFinder
    {
        private readonly string _calculatorName;
        private readonly int _selectedCalculator;
        private bool _isLastNodeMatch;
        private readonly Dictionary<PeakTransitionGroupIdKey, List<PeakTransitionGroupFeatures>> _featureDictionary;

        public MissingScoresFinder(string calculatorName, int selectedCalculator, 
                                   Dictionary<PeakTransitionGroupIdKey, List<PeakTransitionGroupFeatures>> featureDictionary)
        {
            _calculatorName = calculatorName;
            _selectedCalculator = selectedCalculator;
            _featureDictionary = featureDictionary;
        }

        public override string Name
        {
            get { return @"missing_scores_finder"; }
        }

        public override string DisplayName
        {
            get { return Resources.MissingScoresFinder_DisplayName_missing_scores; }
        }

        public override FindMatch Match(BookmarkEnumerator bookmarkEnumerator)
        {
            var nodePep = bookmarkEnumerator.CurrentDocNode as PeptideDocNode;
            if (nodePep == null)
                return null;
            if (bookmarkEnumerator.ResultsIndex < 0)
            {
                _isLastNodeMatch = IsMatch(nodePep);
                if (_isLastNodeMatch)
                    return new FindMatch(string.Format(Resources.MissingScoresFinder_Match__0__missing_from_peptide, _calculatorName));
            }
            else if (IsMatch(bookmarkEnumerator.CurrentChromInfo, nodePep) && !_isLastNodeMatch)
            {
                return new FindMatch(string.Format(Resources.MissingScoresFinder_Match__0__missing_from_chromatogram_peak, _calculatorName));
            }
            return null;
        }

        private bool IsMatch(PeptideDocNode nodePep)
        {
            // The peptide node matches if its results are missing for all files
            return nodePep.HasResults &&
                   nodePep.Results.All(chromInfoList => chromInfoList.All(chromInfo => IsMatch(chromInfo, nodePep)));
        }

        private bool IsMatch(ChromInfo chromInfo, PeptideDocNode nodePep)
        {
            var key = new PeakTransitionGroupIdKey(nodePep.Id.GlobalIndex, chromInfo.FileId.GlobalIndex);
            if (!_featureDictionary.ContainsKey(key))
                return false;
            var listFeatures = _featureDictionary[key];
            foreach (var features in listFeatures)
            {
                if (IsUnknownScore(features, _selectedCalculator))
                    return true;
            }
            return false;
        }

        private static bool IsUnknownScore(PeakTransitionGroupFeatures features, int selectedCalculator)
        {
            return features.PeakGroupFeatures.Any(groupFeatures => double.IsNaN(groupFeatures.Features[selectedCalculator]));
        }
    }
}
