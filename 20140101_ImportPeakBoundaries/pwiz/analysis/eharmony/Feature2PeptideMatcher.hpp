//
// $Id$
//
//
// Original author: Kate Hoff <katherine.hoff@proteowizard.org>
//
// Copyright 2009 Center for Applied Molecular Medicine
//   University of Southern California, Los Angeles, CA
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

///
/// Feature2PeptideMatcher.hpp
///

#ifndef _FEATURE2PEPTIDEMATCHER_HPP_
#define _FEATURE2PEPTIDEMATCHER_HPP_

#include "DataFetcherContainer.hpp"
#include "SearchNeighborhoodCalculator.hpp"
#include "PeptideMatcher.hpp"
#include "pwiz/data/misc/MinimumPepXML.hpp"
#include "pwiz/utility/minimxml/SAXParser.hpp"

namespace pwiz{
namespace eharmony{

class Feature2PeptideMatcher
{

public:

    Feature2PeptideMatcher(){}
    Feature2PeptideMatcher(FdfPtr a, PidfPtr b, const NormalDistributionSearch& nds, const int& rocStats=0, const double& threshold=0.75); 

    // accessors
    std::vector<MatchPtr> getMatches() const { return _matches;}
    std::vector<MatchPtr> getMismatches() const { return _mismatches;}
    std::vector<MatchPtr> getTruePositives() const { return _truePositives;}
    std::vector<MatchPtr> getFalsePositives() const { return _falsePositives;}
    std::vector<MatchPtr> getTrueNegatives() const { return _trueNegatives;}
    std::vector<MatchPtr> getFalseNegatives() const { return _falseNegatives;}
    std::vector<MatchPtr> getUnknownPositives() const { return _unknownPositives;}
    std::vector<MatchPtr> getUnknownNegatives() const { return _unknownNegatives;}

    bool operator==(const Feature2PeptideMatcher& that);
    bool operator!=(const Feature2PeptideMatcher& that);


    std::vector<MatchPtr> _matches;
    std::vector<MatchPtr> _mismatches; // un-apt type name Match, but want to store all the info in the Match struct so we can look at why there was a missed match

    // ROC info
    std::vector<MatchPtr> _truePositives;
    std::vector<MatchPtr> _falsePositives;
    std::vector<MatchPtr> _trueNegatives;
    std::vector<MatchPtr> _falseNegatives;
    std::vector<MatchPtr> _unknownPositives; // featureSequenced.ms2.size() == 0 && featureSequenced.ms1_5.size() > 0
    std::vector<MatchPtr> _unknownNegatives; // featureSequenced.ms2.size() == 0 && featureSequenced.ms1_5.size() == 0

};

} // namespace eharmony
} // namespace pwiz

#endif // _FEATURE2PEPTIDEMATCHER_HPP_
