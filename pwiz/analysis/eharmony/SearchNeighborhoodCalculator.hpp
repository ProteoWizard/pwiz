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
/// SearchNeighborhoodCalculator.hpp
///

#ifndef _SEARCHNEIGHBORHOODCALCULATOR_HPP_
#define _SEARCHNEIGHBORHOODCALCULATOR_HPP_

#include "DataFetcherContainer.hpp"
#include "pwiz/data/misc/MinimumPepXML.hpp"

using namespace pwiz::data::pepxml;

namespace pwiz{
namespace eharmony{

typedef boost::shared_ptr<DataFetcherContainer> DfcPtr;

struct SearchNeighborhoodCalculator
{
    SearchNeighborhoodCalculator() : _id("default"), _mzTol(.005), _rtTol(60) {}
    SearchNeighborhoodCalculator(double mzTol, double rtTol) : _mzTol(mzTol), _rtTol(rtTol) { _id = ("naive[" + boost::lexical_cast<string>(_mzTol) + "," + boost::lexical_cast<string>(_rtTol) + "]").c_str();}

    virtual void calculateTolerances(const DataFetcherContainer& dfc) {}
    virtual bool close(const SpectrumQuery& a, const Feature& b) const;
    virtual double score(const SpectrumQuery& a, const Feature& b) const;

    string _id;
    double _mzTol;
    double _rtTol;

    virtual ~SearchNeighborhoodCalculator(){}

    virtual bool operator==(const SearchNeighborhoodCalculator& that) const;
    virtual bool operator!=(const SearchNeighborhoodCalculator& that) const;

};


struct NormalDistributionSearch : public SearchNeighborhoodCalculator
{
    NormalDistributionSearch(double threshold = 0.95) :  _threshold(threshold) 
    { 
        _id = ("normalDistribution[" + boost::lexical_cast<string>(_threshold) + "]").c_str(); 
    }
   
    virtual void calculateTolerances(const DfcPtr dfc);
    virtual double score(const SpectrumQuery& a, const Feature& b) const;
    virtual bool close(const SpectrumQuery& a, const Feature& b) const;

    double _mu_mz;
    double _sigma_mz;
  
    double _mu_rt;
    double _sigma_rt;
   
    double _threshold;

};

} // eharmony
} // pwiz

#endif

