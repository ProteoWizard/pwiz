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
/// PeptideMatcher.hpp
///

#ifndef _PEPTIDEMATCHER_HPP_
#define _PEPTIDEMATCHER_HPP_

#include "DataFetcherContainer.hpp"

using namespace pwiz::data::pepxml;

namespace pwiz{
namespace eharmony{

typedef std::vector<std::pair<boost::shared_ptr<SpectrumQuery> , boost::shared_ptr<SpectrumQuery> > > PeptideMatchContainer;
typedef boost::shared_ptr<PeptideID_dataFetcher> PidfPtr;
typedef boost::shared_ptr<Feature_dataFetcher> FdfPtr;

class PeptideMatcher
{

public:

    PeptideMatcher(const PidfPtr a = PidfPtr(new PeptideID_dataFetcher()), const PidfPtr b = PidfPtr(new PeptideID_dataFetcher()));

    PeptideMatchContainer getMatches() const { return _matches;}

    void calculateDeltaRTDistribution(); // find mean and stdev deltaRT 
    void calculateDeltaMZDistribution();

    pair<double,double> getDeltaRTParams() const { return make_pair(_meanDeltaRT, _stdevDeltaRT); }
    pair<double,double> getDeltaMZParams() const { return make_pair(_meanDeltaMZ, _stdevDeltaMZ); }

    bool operator==(const PeptideMatcher& that);
    bool operator!=(const PeptideMatcher& that);

private:

    PeptideMatchContainer _matches;

    double _meanDeltaRT;
    double _stdevDeltaRT;

    double _meanDeltaMZ;
    double _stdevDeltaMZ;

};

} // namespace eharmony
} // namespace pwiz

#endif //_PEPTIDEMATCHER_HPP_
