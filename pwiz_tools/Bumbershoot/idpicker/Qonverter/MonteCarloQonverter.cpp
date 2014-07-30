//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Surendra Dasari.
//
// Copyright 2011 Vanderbilt University
//
// Contributor(s): Matt Chambers
//

#include "pwiz/utility/misc/Std.hpp"
#include "MonteCarloQonverter.hpp"
#include "waffles/GVec.h"


using namespace GClasses;
using namespace IDPICKER_NAMESPACE;


BEGIN_IDPICKER_NAMESPACE


void MonteCarloQonverter::Qonvert(PSMList& psmRows,
                                  const Qonverter::Settings& settings,
                                  const vector<double>& scoreWeights,
                                  WeightsByChargeAndBestSpecificity* monteCarloWeightsByChargeAndBestSpecificity)
{
    vector< vector<double> > validScorePermutations;

    if (scoreWeights.size() > 1)
    {
        // create a coordinate vector with scoreWeights.size() dimensions and iterate through all the coordinates;
        // on the iterations that sum to coordinateRange, use it as a scoreWeight permutation
        const size_t coordinateRange = 10;
        vector<size_t> scoreWeightRanges(scoreWeights.size(), coordinateRange+1);
        GCoordVectorIterator scorePermutations(scoreWeightRanges);
        scorePermutations.reset();
        vector<double> currentPermutNormalized(scoreWeights.size());
        while (scorePermutations.advanceSampling())
        {
            vector<size_t> currentPermut(scorePermutations.current(), scorePermutations.current() + scoreWeights.size());
            size_t totalWeight = std::accumulate(currentPermut.begin(), currentPermut.end(), 0);

            // only keep permutations that add up to the total axis length
            if (totalWeight == coordinateRange)
            {
                // compute the total score weight and also change the direction of the scores
                for(size_t scoreIndex = 0; scoreIndex < scoreWeights.size(); ++scoreIndex)
                    currentPermutNormalized[scoreIndex] = currentPermut[scoreIndex] * scoreWeights[scoreIndex] / totalWeight;
                validScorePermutations.push_back(currentPermutNormalized);
                if (validScorePermutations.size() > settings.maxPermutations)
                    break;
            }
        }
        if(validScorePermutations.empty())
            throw runtime_error("insufficient number of score permutations in Monte Carlo optimization");
    }
    else
        validScorePermutations.push_back(vector<double>(1, scoreWeights[0] > 0 ? 1 : -1));

    PSMIteratorRange fullRange(psmRows.end(), psmRows.end());

    if (!settings.rerankMatches)
    {
        sort(psmRows.begin(), psmRows.end(), OriginalRankLessThan());

        for (PSMIterator itr = psmRows.begin(); itr != psmRows.end(); ++itr)
        {
            if (itr->originalRank > 1)
            {
                if (fullRange.empty())
                    fullRange = PSMIteratorRange(psmRows.begin(), itr);
                itr->newRank = itr->originalRank;
                itr->fdrScore = itr->qValue = 2;
            }
        }
    }

    if (fullRange.empty())
        fullRange = PSMIteratorRange(psmRows.begin(), psmRows.end());

    // partition the data by charge and/or terminal specificity (depending on qonverter settings)
    vector<PSMIteratorRange> psmPartitionedRows = partition(settings, fullRange);
    BOOST_FOREACH(const PSMIteratorRange& range, psmPartitionedRows)
    {
        int passingPSMs = -1;
        const vector<double>* bestSolution;
        BOOST_FOREACH(const vector<double>& scorePermutation, validScorePermutations)
        {
            // reset the scores and q-values with the current permutation
            calculateWeightedTotalScore(range, scorePermutation);

            if (settings.rerankMatches)
                boost::sort(range, TotalScoreBetterThanIgnoringRank());
            else
                boost::sort(range, TotalScoreBetterThanWithRank());

            discriminate(range);

            int passedPSMs = 0;
            BOOST_FOREACH(const PeptideSpectrumMatch& psm, range)
            {
                if (psm.fdrScore <= settings.maxFDR)
                    ++passedPSMs;
                else
                    break;
            }

            // save the solution if this is best we have seen so far
            if(passingPSMs <= passedPSMs)
            {
                passingPSMs = passedPSMs;
                bestSolution = &scorePermutation;
            }
        }

        if (monteCarloWeightsByChargeAndBestSpecificity)
            BOOST_FOREACH(const PeptideSpectrumMatch& psm, range)
                (*monteCarloWeightsByChargeAndBestSpecificity)[psm.chargeState][psm.bestSpecificity] = *bestSolution;

        // early exit if the last solution was the best solution
        if (bestSolution == &validScorePermutations.back())
            continue;

        // calculate the scores and q-values with the best permutation
        calculateWeightedTotalScore(range, *bestSolution);

        if (settings.rerankMatches)
            boost::sort(range, TotalScoreBetterThanIgnoringRank());
        else
            boost::sort(range, TotalScoreBetterThanWithRank());

        discriminate(range);
    }
}


} // namespace IDPicker

