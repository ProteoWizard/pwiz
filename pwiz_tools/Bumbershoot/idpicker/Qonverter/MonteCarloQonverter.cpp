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
#include "Qonverter.hpp"

using namespace GClasses;
using namespace IDPICKER_NAMESPACE;

BEGIN_IDPICKER_NAMESPACE

namespace {

struct StaticWeightedTotalScoreBetterThan
{
    StaticWeightedTotalScoreBetterThan(const vector<double>& scoreWeights) : scoreWeights(scoreWeights) {}

    double getTotalScore(const PeptideSpectrumMatch& psm) const
    {
        BOOST_ASSERT(psm.scores.size() == scoreWeights.size());

        if (psm.totalScore == 0)
            for (size_t i=0, end=psm.scores.size(); i < end; ++i)
                const_cast<PeptideSpectrumMatch&>(psm).totalScore += psm.scores[i] * scoreWeights[i];
        return psm.totalScore;
    }

    bool operator() (const PeptideSpectrumMatch& lhs, const PeptideSpectrumMatch& rhs) const
    {
        double lhsTotalScore = getTotalScore(lhs);
        double rhsTotalScore = getTotalScore(rhs);

        if (lhsTotalScore != rhsTotalScore)
            return lhsTotalScore > rhsTotalScore;

        // arbitrary tie-breaker when scores are equal
        //return lhs.massError < rhs.massError;
        return lhs.spectrum < rhs.spectrum;
    }

    private:
    vector<double> scoreWeights;
};

} // namespace

void MonteCarloQonverter::Qonvert(PSMList& psmRows,
                                  const Qonverter::Settings& settings,
                                  const vector<double>& scoreWeights)
{
    vector< vector<double> > validScorePermutations;

    if (scoreWeights.size() > 1)
    {
        // figure out all the permutations of the score weights.
        vector<size_t> scoreWeightRanges(scoreWeights.size(), 10);
        GCoordVectorIterator scorePermutations(scoreWeightRanges);
        scorePermutations.reset();
        while(scorePermutations.advance())
        {
            vector<double> currentPermut(scoreWeights.size(), 0);
            scorePermutations.currentNormalized(&currentPermut[0]);
            double totalWeight = 0.0;

            // compute the total score weight and also change the direction of the scores
            for(size_t scoreIndex = 0; scoreIndex < scoreWeights.size(); ++scoreIndex)
            {
                totalWeight += currentPermut[scoreIndex];
                currentPermut[scoreIndex] *= scoreWeights[scoreIndex];
            }

            // only take permuations that add to utmost 1.0
            if(totalWeight == 1.0)
                validScorePermutations.push_back(currentPermut);
        }

        if(validScorePermutations.empty())
            throw runtime_error("insufficient number of score permutations in Monte Carlo optimization");
    }
    else
        validScorePermutations.push_back(vector<double>(1, 1));

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
            // reset the scores and q-values with the best solution.
            BOOST_FOREACH(PeptideSpectrumMatch& psm, range)
                psm.totalScore = 0;

            // recompute the scores and determine the number of ids passing the FDR threshold
            StaticWeightedTotalScoreBetterThan scoreComparator(scorePermutation);
            sort(range.begin(), range.end(), scoreComparator);
            discriminate(range);
            int passedPSMs = 0;
            BOOST_FOREACH(const PeptideSpectrumMatch& psm, range)
                if(psm.fdrScore <= settings.maxFDR)
                    ++passedPSMs;
                else
                    break;

            // save the solution if this is best we have seen so far
            if(passingPSMs <= passedPSMs)
            {
                passingPSMs = passedPSMs;
                bestSolution = &scorePermutation;
            }
        }

        /*size_t specificity = range.front().bestSpecificity;
        size_t charge = range.front().chargeState;
        stringstream ss;
        ss << "Z:" << charge << " NET:" << specificity;
        ss << " Num Ids passed: " << passingPSMs << " ScoreWeights: (";
        for(size_t i = 0; i < bestSolution.size(); ++i)
            ss << (*bestSolution)[i] << ",";
        ss << ")" << endl;
        cout << ss.str();*/

        // early exit if the last solution was the best solution
        if (bestSolution == &validScorePermutations.back())
            continue;

        // reset the scores and q-values with the best solution.
        BOOST_FOREACH(PeptideSpectrumMatch& psm, range)
            psm.totalScore = 0;
        StaticWeightedTotalScoreBetterThan scoreComparator(*bestSolution);
        // calculate and sort the PSMs by total score
        sort(range.begin(), range.end(), scoreComparator);
        discriminate(range);
    }
}


} // namespace IDPicker

