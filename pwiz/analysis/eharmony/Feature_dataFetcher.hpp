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
/// Feature_dataFetcher.hpp
///

#ifndef _FEATURE_DATAFETCHER_HPP_
#define _FEATURE_DATAFETCHER_HPP_

#include "Bin.hpp"
#include "FeatureSequenced.hpp"
#include "pwiz/data/misc/PeakData.hpp"
#include "boost/shared_ptr.hpp"

#include<iostream>
#include<fstream>

using namespace pwiz::data::peakdata;

namespace pwiz{
namespace eharmony{

typedef boost::shared_ptr<FeatureSequenced> FeatureSequencedPtr;

class Feature_dataFetcher
{

public:

    Feature_dataFetcher(){}
    Feature_dataFetcher(std::istream& is);
    Feature_dataFetcher(const std::vector<FeaturePtr>& features);
    
    void update(const FeatureSequenced& fs);
    void erase(const FeatureSequenced& fs);
    void merge(const Feature_dataFetcher& that);

    std::vector<FeatureSequencedPtr> getFeatures(double mz, double rt) ;
    std::vector<FeatureSequencedPtr> getAllContents() const;
    Bin<FeatureSequenced> getBin() const { return _bin; } 
    
    void setMS2LabeledFlag(const bool& flag) { _ms2Labeled = flag; }
    const bool& getMS2LabeledFlag() const { return _ms2Labeled; }
  
    bool operator==(const Feature_dataFetcher& that);
    bool operator!=(const Feature_dataFetcher& that);

private:

    bool _ms2Labeled;
    Bin<FeatureSequenced> _bin;
    
    // no copying
    Feature_dataFetcher(Feature_dataFetcher&);
    Feature_dataFetcher operator=(Feature_dataFetcher&);

};

} // namespace eharmony
} // namespace pwiz


#endif //_FEATURE_DATAFETCHER_HPP_
