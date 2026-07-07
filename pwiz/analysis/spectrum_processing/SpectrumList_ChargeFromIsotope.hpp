//
// $Id$
//
//
// Original author: William French <william.r.french <a.t> vanderbilt.edu>
//
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
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


#ifndef _SPECTRUMLIST_CHARGEFROMISOTOPE_HPP_ 
#define _SPECTRUMLIST_CHARGEFROMISOTOPE_HPP_ 

#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/utility/misc/IntegerSet.hpp"
#include "pwiz/data/msdata/SpectrumListWrapper.hpp"

typedef struct {
    int charge;
    std::vector <int> indexList;
    int parentIndex;
} isotopeChain;

typedef struct {
    // three score types
    double mzPvalue;
    double intensityPvalue;
    double intensitySumPvalue;
    double overallPvalue;
    // ranks of chains based on different score types
    int mzRank;
    int intensityRank;
    int intensitySumRank;
    int sumRanks;
    // need index to the chain
    int chainIndex;
} scoreChain;

typedef struct {
    double rtime;
    int indexMap;
} rtimeMap;

typedef struct {
    double mz;
    double intensity;
} pairData;


namespace pwiz {
namespace analysis {

using namespace msdata;

/// SpectrumList implementation that assigns (probable) charge states to tandem mass spectra
class PWIZ_API_DECL SpectrumList_ChargeFromIsotope : public msdata::SpectrumListWrapper
{
    public:
    SpectrumList_ChargeFromIsotope(const msdata::MSData& msd,
                                       int maxCharge = 3,
                                       int minCharge = 1,
                                       int parentsBefore = 2,
                                       int parentsAfter = 0,
                                       double isolationWindow = 1.25,
                                       int defaultChargeMax = 0,
                                       int defaultChargeMin = 0);

    virtual msdata::SpectrumPtr spectrum(size_t index, bool getBinaryData = true) const;

    virtual bool benefitsFromWorkerThreads() const { return true; }

    void getMS1RetentionTimes();
    void simulateSSE(const int,const int);
    void simulateKL(const double,const int,const int);
    void simulateTotIntensity(const int);
    void getParentIndices(const SpectrumPtr,std::vector <int> &) const;
    void getParentPeaks(const SpectrumPtr,const std::vector <int> &,const double,const double,const double,std::vector< std::vector <double> > &,std::vector< std::vector <double> > &) const;

    SpectrumListPtr instantiatePeakPicker( const std::vector <int> & ) const;
    SpectrumListPtr instantiatePeakPicker( const std::vector <int> &, const double, const double, const double ) const;

    private:
    bool override_;
    int maxCharge_;
    int minCharge_;
    double sigVal_;
    int nChainsCheck_;
    int parentsBefore_;
    int parentsAfter_;
    int defaultChargeMax_;
    int defaultChargeMin_;
    double defaultIsolationWidth_;

    int nSamples; // number of samples per simulation
    int maxIsotopePeaks;
    int minIsotopePeaks;
    int maxNumberPeaks;
    int minNumberPeaks;
    double mzTol;
    double massNeutron;
    double upperLimitPadding;
    
    std::vector <double> simulatedSSEs;
    std::vector <double> simulatedKLs;
    std::vector <int> simulatedIntensityRankSum;
    std::vector <rtimeMap> MS1retentionTimes; 
    
};


} // namespace analysis
} // namespace pwiz

int nChoosek(int,int);
double getKLscore( const isotopeChain, const std::vector <double> &, const std::vector <double> & );

#endif // _SPECTRUMLIST_CHARGEFROMISOTOPE_HPP_
