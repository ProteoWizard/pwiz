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
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s): Surendra Dasari
//

#include "pwiz/utility/misc/Std.hpp"
#include "StaticWeightQonverter.hpp"


using namespace IDPICKER_NAMESPACE;


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
        }
    }

    if (fullRange.empty())
        fullRange = PSMIteratorRange(psmRows.begin(), psmRows.end());

    // partition the data by charge and/or terminal specificity (depending on qonverter settings)
    vector<PSMIteratorRange> psmPartitionedRows = partition(settings, fullRange);

    BOOST_FOREACH(const PSMIteratorRange& range, psmPartitionedRows)
    {
        calculateWeightedTotalScore(range, scoreWeights);

        if (settings.rerankMatches)
            boost::sort(range, TotalScoreBetterThanIgnoringRank());
        else
            boost::sort(range, TotalScoreBetterThanWithRank());

        discriminate(range);
    }
}


} // namespace IDPicker
