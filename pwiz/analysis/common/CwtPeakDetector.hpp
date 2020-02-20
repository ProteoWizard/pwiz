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


#ifndef _CWTPEAKDETECTOR_HPP_ 
#define _CWTPEAKDETECTOR_HPP_

// custom type for storing ridge line info
typedef struct {
    int Col;
    int Row;
} ridgeLine;


#include "PeakDetector.hpp"

namespace pwiz {
namespace analysis {


struct PWIZ_API_DECL CwtPeakDetector : public PeakDetector
{
    CwtPeakDetector(double minSnr, int fixedPeaksKeep, double mzTol, bool centroid = false);

    virtual void detect(const std::vector<double>& x, const std::vector<double>& y,
                        std::vector<double>& xPeakValues, std::vector<double>& yPeakValues,
                        std::vector<Peak>* peaks = NULL);
    virtual const char* name() const { return "CantWaiT (continuous wavelet transform) peak picker"; }
    void getScales( const std::vector <double> &, const std::vector <double> &, std::vector <std::vector< std::vector<int> > > &, std::vector <double> &) const;
    void calcCorrelation( const std::vector <double> &, const std::vector <double> &, const std::vector <std::vector<std::vector<int> > > &, const std::vector <double> &, std::vector < std::vector <double> > &) const;
    void getPeakLines(const std::vector < std::vector <double> > &, const std::vector <double> &, std::vector <ridgeLine> &, std::vector <double> &) const;
    void refinePeaks( const std::vector <double> &, const std::vector <double> &, const std::vector <ridgeLine> &, const std::vector <double> &, std::vector <double> &, std::vector <double> &, std::vector <double> &) const;
    
    private:
    // parameters
    double minSnr_;
    int fixedPeaksKeep_;
    double mzTol_;
    bool centroid_;
    int nScales;
    std::vector<double> scalings; // how to scale the wavelet widths, unchanged once it's initialized

};


} // namespace analysis
} // namespace pwiz

#endif // _CWTPEAKDETECTOR_HPP_
