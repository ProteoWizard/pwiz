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

using System;
using System.Collections.Generic;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Find
{
    /// <summary>
    /// Finds peptides 
    /// </summary>
    public class MissingScoresFinder : AbstractFinder
    {
        private readonly FeatureCalculators _featureCalculators;
        private readonly ImmutableList<int> _calculatorIndices;
        private bool _isLastNodeMatch;
        private readonly Dictionary<PeakTransitionGroupIdKey, List<PeakTransitionGroupFeatures>> _featureDictionary;

        public MissingScoresFinder(FeatureCalculators featureCalculators, IEnumerable<int> calculatorIndices, 
                                   Dictionary<PeakTransitionGroupIdKey, List<PeakTransitionGroupFeatures>> featureDictionary)
        {
            _featureCalculators = featureCalculators;
            _calculatorIndices = ImmutableList.ValueOf(calculatorIndices);
            _featureDictionary = featureDictionary;
        }

        public override string Name
        {
            get { return @"missing_scores_finder"; }
        }

        public override string DisplayName
        {
            get { return FindResources.MissingScoresFinder_DisplayName_missing_scores; }
        }

        public override FindMatch Match(BookmarkEnumerator bookmarkEnumerator)
        {
            var nodePep = bookmarkEnumerator.CurrentDocNode as PeptideDocNode;
            if (nodePep == null)
                return null;
            if (bookmarkEnumerator.ResultsIndex < 0 || bookmarkEnumerator.CurrentChromInfo == null)
            {
                var missingScoreIndices = GetMissingScoreIndices(nodePep);
                _isLastNodeMatch = missingScoreIndices.Any();
                if (_isLastNodeMatch)
                {
                    var missingCalculators = GetCalculatorNameList(missingScoreIndices);
                    return new FindMatch(bookmarkEnumerator.Current, string.Format(FindResources.MissingScoresFinder_Match__0__missing_from_peptide, missingCalculators));
                }
            }
            else if (!_isLastNodeMatch)
            {
                var missingScoreIndices = GetMissingScoreIndices(bookmarkEnumerator.CurrentChromInfo, nodePep).ToList();
                if (missingScoreIndices.Count != 0)
                {
                    var missingCalculators = GetCalculatorNameList(missingScoreIndices);
                    if (!string.IsNullOrEmpty(missingCalculators))
                    {
                        return new FindMatch(bookmarkEnumerator.Current, string.Format(FindResources.MissingScoresFinder_Match__0__missing_from_chromatogram_peak, missingCalculators));
                    }
                }
            }
            return null;
        }

        private ICollection<int> GetMissingScoreIndices(PeptideDocNode nodePep)
        {
            if (!nodePep.HasResults)
            {
                return Array.Empty<int>();
            }

            // The peptide node matches if its results are missing for all files
            var missingSet = new HashSet<int>();
            foreach (var chromInfo in nodePep.Results.SelectMany(chromInfoList => chromInfoList))
            {
                bool any = false;
                foreach (var missing in GetMissingScoreIndices(chromInfo, nodePep))
                {
                    missingSet.Add(missing);
                    any = true;
                }

                if (!any)
                {
                    return Array.Empty<int>();
                }
            }

            return missingSet;
        }

        private string GetCalculatorNameList(IEnumerable<int> calculatorIndices)
        {
            return TextUtil.CommaSeparateListItems(calculatorIndices.Distinct().OrderBy(i => i)
                .Select(i => _featureCalculators[i].Name));
        }

        private IEnumerable<int> GetMissingScoreIndices(ChromInfo chromInfo, PeptideDocNode nodePep)
        {
            var key = new PeakTransitionGroupIdKey(nodePep.Peptide, chromInfo.FileId);
            if (!_featureDictionary.TryGetValue(key, out var listFeatures))
                return Array.Empty<int>();
            return listFeatures.SelectMany(features => _calculatorIndices.Where(i => IsUnknownScore(features, i))).Distinct();
        }

        private static bool IsUnknownScore(PeakTransitionGroupFeatures features, int selectedCalculator)
        {
            return features.PeakGroupFeatures.Any(groupFeatures => double.IsNaN(groupFeatures.Features[selectedCalculator]));
        }
    }
}
