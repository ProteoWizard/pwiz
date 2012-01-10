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
/// DatabaseQuery.hpp
///

#ifndef _DATABASEQUERY_HPP_
#define _DATABASEQUERY_HPP_

#include "FeatureSequenced.hpp"
#include "pwiz/data/misc/MinimumPepXML.hpp"
#include "SearchNeighborhoodCalculator.hpp"

namespace pwiz{
namespace eharmony{

using namespace pwiz::data::pepxml;

typedef boost::shared_ptr<SpectrumQuery> SpectrumQueryPtr;
//    Independent of retention time calibration, this struct allows querying of a feature to the database as a whole and is constructed from the database itself

struct DatabaseQuery
{
    std::vector<MatchPtr> query(FeatureSequencedPtr fs, NormalDistributionSearch nds, double threshold);

    pair<double,double> calculateSearchRegion(const double& mu1, const double& mu2, const double& sigma1, const double& sigma2, const double& threshold);
    pair<double,double> calculateNormalSearchRegion(const double& mu1, const double& mu2, double& sigma1, double& sigma2, const double& threshold);

    PidfPtr _database;
    DatabaseQuery(const PidfPtr database) : _database(database){}    

};

}
}


#endif
