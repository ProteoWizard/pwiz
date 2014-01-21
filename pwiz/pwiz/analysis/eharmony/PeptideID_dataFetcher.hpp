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
/// PeptideID_dataFetcher.hpp
///

#ifndef _PEPTIDEID_DATAFETCHER_HPP_
#define _PEPTIDEID_DATAFETCHER_HPP_

#include "Feature_dataFetcher.hpp"
#include "pwiz/data/misc/MinimumPepXML.hpp"
#include "boost/shared_ptr.hpp"

#include <iostream>
#include <fstream>


using namespace pwiz;
using namespace pwiz::data::pepxml;

namespace pwiz{
namespace eharmony{

class PeptideID_dataFetcher
{

public:


    PeptideID_dataFetcher() : _rtAdjusted(false) {}
    PeptideID_dataFetcher(std::istream& is, const double& threshold = .9);
    PeptideID_dataFetcher(const std::vector<boost::shared_ptr<SpectrumQuery> >& sqs);
    PeptideID_dataFetcher(const MSMSPipelineAnalysis& mspa);

    void update(const SpectrumQuery& sq);
    void erase(const SpectrumQuery& sq);
    void merge(const PeptideID_dataFetcher& that);
    size_t size(){ return this->getAllContents().size();}

    // accessors
    std::vector<boost::shared_ptr<SpectrumQuery> > getAllContents() const;
    std::vector<boost::shared_ptr<SpectrumQuery> > getSpectrumQueries(double mz, double rt) ;
    Bin<SpectrumQuery> getBin() const { return _bin;}
    void setRtAdjustedFlag(const bool& flag) { _rtAdjusted = flag; }
    const bool& getRtAdjustedFlag() const { return _rtAdjusted; }

    bool operator==(const PeptideID_dataFetcher& that);
    bool operator!=(const PeptideID_dataFetcher& that);

    std::string id;

private:
    
    bool _rtAdjusted;
    Bin<SpectrumQuery> _bin;
    
    // no copying
    PeptideID_dataFetcher(PeptideID_dataFetcher&);
    PeptideID_dataFetcher operator=(PeptideID_dataFetcher&);

};

} // namespace eharmony
} // namespace pwiz

#endif //_PEPTIDEID_DATAFETCHER_HPP_
