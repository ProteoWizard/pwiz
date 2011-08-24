//
// $Id$
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s): Surendra Dasari
//

#include "pwiz/utility/misc/Std.hpp"
#include "StaticWeightQonverter.hpp"
#include "Qonverter.hpp"


using namespace IDPICKER_NAMESPACE;


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


BEGIN_IDPICKER_NAMESPACE


void StaticWeightQonverter::Qonvert(PSMList& psmRows,
                                    const Qonverter::Settings& settings,
                                    const vector<double>& scoreWeights)
{
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
            else
                itr->totalScore = 0;
        }
    }
    else
        for (PSMIterator itr = psmRows.begin(); itr != psmRows.end(); ++itr)
            itr->totalScore = 0;

    if (fullRange.empty())
        fullRange = PSMIteratorRange(psmRows.begin(), psmRows.end());

    // partition the data by charge and/or terminal specificity (depending on qonverter settings)
    vector<PSMIteratorRange> psmPartitionedRows = partition(settings, fullRange);

    BOOST_FOREACH(const PSMIteratorRange& range, psmPartitionedRows)
    {
        StaticWeightedTotalScoreBetterThan totalScoreBetterThan(scoreWeights);

        // calculate and sort the PSMs by total score
        sort(range.begin(), range.end(), totalScoreBetterThan);

        discriminate(range);
    }
}


} // namespace IDPicker
