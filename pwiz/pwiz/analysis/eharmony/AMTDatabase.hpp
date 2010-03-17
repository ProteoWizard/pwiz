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
/// AMTDatabase.hpp
///

#ifndef _AMTDATABASE_HPP_
#define _AMTDATABASE_HPP_

#include "AMTContainer.hpp"

namespace pwiz{
namespace eharmony{

using namespace pwiz::data::peakdata;


struct AMTDatabase
{
    AMTDatabase(const AMTContainer& amtContainer);

    virtual std::vector<SpectrumQuery> query(DfcPtr dfc, const WarpFunctionEnum& wfe, NormalDistributionSearch& nds, MSMSPipelineAnalysis& mspa_in, string outputDir="./amtdb_query",const int& roc=0, const double& threshold=0.75);
    
    PidfPtr _peptides;
    
};

struct IslandizedDatabase : public AMTDatabase
{
    IslandizedDatabase(boost::shared_ptr<AMTContainer> amtContainer);

    virtual std::vector<SpectrumQuery> query(DfcPtr dfc, const WarpFunctionEnum& wfe, NormalDistributionSearch& nds, MSMSPipelineAnalysis& mspa_in, string outputDir="./amtdb_query");   

    struct Gaussian 
    {
        Gaussian() : mu(make_pair(0,0)), sigma(make_pair(0,0)){}
        Gaussian(const pair<pair<double,double>, pair<double,double> >& params) : mu(params.first), sigma(params.second){}
        pair<double,double> mu;
        pair<double,double> sigma;      

        bool operator==(const Gaussian& that) const { return mu == that.mu && sigma == that.sigma;}
        bool operator!=(const Gaussian& that) const { return !(*this == that);}

    };

    struct Island
    {

        Island() : massMin(0), massMax(0), rtMin(0), rtMax(0) {}
        double calculatePVal(const double& mz, const double& rt) const;

        double massMin;
        double massMax;
        double massMedian;
        double massMean;

        double rtMin;
        double rtMax;
        double rtMedian;
        double rtMean;

        double relativeArea;

        vector<boost::shared_ptr<SpectrumQuery> > spectrumQueries;
        vector<Gaussian> gaussians;
        
        string id;

    };

    vector<Island> islands;
    vector<string> uniquePeptides;

};

} // namespace eharmony
} // namespace pwiz

#endif // _AMTDATABASE_HPP_
