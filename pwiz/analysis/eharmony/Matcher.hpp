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
/// Matcher.hpp
///

#ifndef _MATCHER_HPP_
#define _MATCHER_HPP_


#include "DataFetcherContainer.hpp"
#include "Match_dataFetcher.hpp"
#include "SearchNeighborhoodCalculator.hpp"
#include "NeighborJoiner.hpp"
//#include "Matrix.hpp"
#include "boost/shared_ptr.hpp"
#include "boost/filesystem/path.hpp"
#include "boost/filesystem/convenience.hpp"

namespace pwiz{
namespace eharmony{

class Matcher
{
public:

    Matcher(){}
    Matcher(Config& config);

    void checkSourceFiles();
    void readSourceFiles();
    void processFiles();
    //    void msmatchmake(DataFetcherContainer& dfc, SearchNeighborhoodCalculator& snc, MSMSPipelineAnalysis& mspa, string& outputDir);


private:

    Config _config;

    std::map<std::string, PeptideID_dataFetcher> _peptideData;
    std::map<std::string, Feature_dataFetcher> _featureData;

};

} // namespace match
} // namespace pwiz

#endif
