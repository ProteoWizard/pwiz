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


void StaticWeightQonverter::Qonvert(vector<PeptideSpectrumMatch>& psmRows,
                                    const Qonverter::Settings& settings,
                                    const vector<double>& scoreWeights)
{
    PSMIteratorRange fullRange(psmRows.begin(), psmRows.end());

    sort(fullRange.begin(), fullRange.end(), OriginalRankLessThan());

    for (PSMIterator itr = fullRange.begin(); itr != fullRange.end(); ++itr)
        if (itr->originalRank > 1)
        {
            fullRange = PSMIteratorRange(fullRange.begin(), itr);
            break;
        }

    // partition the data by charge and/or terminal specificity (depending on qonverter settings)
    vector<PSMIteratorRange> psmPartitionedRows = partition(settings, fullRange);

    double targetToDecoyRatio = 1;

    BOOST_FOREACH(const PSMIteratorRange& range, psmPartitionedRows)
    {
        StaticWeightedTotalScoreBetterThan totalScoreBetterThan(scoreWeights);

        // calculate and sort the PSMs by total score
        sort(range.begin(), range.end(), totalScoreBetterThan);

        int numTargets = 0;
        int numDecoys = 0;

        sqlite3_int64 currentSpectrumId = psmRows.front().spectrum;
        DecoyState::Type currentDecoyState = psmRows.front().decoyState;
        vector<PeptideSpectrumMatch*> currentPSMs(1, &psmRows.front());

        // calculate Q values with the current sort
        BOOST_FOREACH(PeptideSpectrumMatch& psm, range)
        {
            /*if (maxRank > 0 && psm.originalRank > maxRank)
            {
                psm.qValue = 2;
                continue;
            }*/
            if (psm.originalRank > 1)
            {
                psm.qValue = 2;
                continue;
            }

            if (currentSpectrumId != psm.spectrum)
            {
                switch (currentDecoyState)
                {
                    case DecoyState::Target: ++numTargets; break;
                    case DecoyState::Decoy: ++numDecoys; break;
                    default: break;
                }

                BOOST_FOREACH(PeptideSpectrumMatch* psm, currentPSMs)
                    psm->qValue = (numTargets + numDecoys > 0) ? min(1.0, max(0.0, (numDecoys * 2 * targetToDecoyRatio) / (numTargets + numDecoys))) : 0;

                // reset the current spectrum
                currentSpectrumId = psm.spectrum;
                currentDecoyState = psm.decoyState;
                currentPSMs.assign(1, &psm);
            }
            else
            {
                currentDecoyState = static_cast<DecoyState::Type>(currentDecoyState | psm.decoyState);
                currentPSMs.push_back(&psm);
            }
        }

        if (!currentPSMs.empty())
        {
            switch (currentDecoyState)
            {
                case DecoyState::Target: ++numTargets; break;
                case DecoyState::Decoy: ++numDecoys; break;
                default: break;
            }

            BOOST_FOREACH(PeptideSpectrumMatch* psm, currentPSMs)
                psm->qValue = (numTargets + numDecoys > 0) ? min(1.0, max(0.0, (numDecoys * 2 * targetToDecoyRatio) / (numTargets + numDecoys))) : 0;
        }

        // with high scoring decoys, Q values can spike and gradually go down again;
        // we squash these spikes such that Q value is monotonically increasing
        for (int i = int(range.size())-2; i >= 0; --i)
            if (range[i].qValue > range[i+1].qValue)
            {
                int j = i - 1;
                while (j >= 0 && range[j].qValue == range[i].qValue)
                {
                    range[j].qValue = range[i+1].qValue;
                    --j;
                }
                range[i].qValue = range[i+1].qValue;
            }
    }
}


} // namespace IDPicker
