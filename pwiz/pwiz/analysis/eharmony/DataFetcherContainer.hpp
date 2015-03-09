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
/// DataFetcherContainer.hpp
///

#ifndef _DATAFETCHERCONTAINER_HPP_
#define _DATAFETCHERCONTAINER_HPP_

#include "PeptideID_dataFetcher.hpp"
#include "Feature_dataFetcher.hpp"
#include "WarpFunction.hpp"
#include "boost/shared_ptr.hpp"

namespace pwiz{
namespace eharmony{

typedef boost::shared_ptr<PeptideID_dataFetcher> PidfPtr;
typedef boost::shared_ptr<Feature_dataFetcher> FdfPtr;

struct DataFetcherContainer
{
    DataFetcherContainer(const PidfPtr pidf_a = PidfPtr(new PeptideID_dataFetcher()), const PidfPtr pidf_b = PidfPtr(new PeptideID_dataFetcher()), const FdfPtr fdf_a = FdfPtr(new Feature_dataFetcher()), const FdfPtr fdf_b = FdfPtr(new Feature_dataFetcher()));

    void adjustRT(bool runA=true, bool runB=true); 
    void warpRT(const WarpFunctionEnum& wfe, const int& anchorFrequency = 30, const double& anchorTol = 100);

    PidfPtr _pidf_a;
    PidfPtr _pidf_b;
    
    FdfPtr _fdf_a;
    FdfPtr _fdf_b;

    // accessors
    vector<pair<double,double> > anchors() const { return _anchors;}

private:

    vector<pair<double, double> > _anchors;
    void getAnchors(const int& freq = 30, const double& tol = 100);

    pair<vector<double>, vector<double> > getPeptideRetentionTimes();
    pair<vector<double>, vector<double> > getFeatureRetentionTimes();

    void putPeptideRetentionTimes(const pair<vector<double>, vector<double> >& times);
    void putFeatureRetentionTimes(const pair<vector<double>, vector<double> >& times);

    // no copying
    DataFetcherContainer(DataFetcherContainer&);
    DataFetcherContainer operator=(DataFetcherContainer&);

};

} // namespace eharmony
} // namespace pwiz

#endif
