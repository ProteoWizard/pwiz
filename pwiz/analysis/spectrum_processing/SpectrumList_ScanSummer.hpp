//
// $Id$
//
//
// Original author: William French <william.r.french .@. vanderbilt.edu>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
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


#ifndef _SPECTRUMLIST_SCANSUMMER_HPP_ 
#define _SPECTRUMLIST_SCANSUMMER_HPP_ 


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/utility/misc/IterationListener.hpp"
#include "pwiz/data/msdata/SpectrumListWrapper.hpp"

struct parentIon
{
    double mz;
    double intensity;
};

struct precursorGroup 
{
    std::vector<double> precursorMZs;
    std::vector<double> scanTimes;
    std::vector<double> ionMobilities;
    std::vector<int> indexList;
};

typedef boost::shared_ptr<precursorGroup> precursorGroupPtr;


namespace pwiz {
namespace analysis {


/// Provides a custom-sorted spectrum list
class PWIZ_API_DECL SpectrumList_ScanSummer : public msdata::SpectrumListWrapper
{
    public:

    SpectrumList_ScanSummer(const msdata::SpectrumListPtr& inner, double precursorTol, double rTimeTol, double mobilityTol = 0, pwiz::util::IterationListenerRegistry* ilr = 0);
    void pushSpectrum(const msdata::SpectrumIdentity&);
    double getPrecursorMz(const msdata::Spectrum&) const;
    //void sumSubScansResample( std::vector<double> &, std::vector<double> &, size_t, msdata::DetailLevel) const;
    void sumSubScansNaive( pwiz::util::BinaryData<double> &, pwiz::util::BinaryData<double> &, const precursorGroupPtr&, msdata::DetailLevel) const;
    virtual size_t size() const;
    virtual const msdata::SpectrumIdentity& spectrumIdentity(size_t index) const;
    virtual msdata::SpectrumPtr spectrum(size_t index, bool getBinaryData = false) const;
    virtual msdata::SpectrumPtr spectrum(size_t index, msdata::DetailLevel) const;

    private:

    double lowerMZlimit;
    double upperMZlimit;
    double TotalDaltons;
    double precursorTol_;
    double rTimeTol_;
    double mobilityTol_;

    std::vector<msdata::SpectrumIdentity> spectrumIdentities; // local cache, with fixed up index fields
    std::vector<size_t> indexMap; // maps index -> original index
    std::vector<precursorGroupPtr> precursorMap; // maps new index -> precursor group
    std::map<double, precursorGroupPtr> precursorList;
    std::vector<precursorGroupPtr> ms2RetentionTimes;
    SpectrumList_ScanSummer(SpectrumList_ScanSummer&); //copy constructor
    SpectrumList_ScanSummer& operator=(SpectrumList_ScanSummer&); //assignment operator
};


} // namespace analysis 
} // namespace pwiz


#endif // _SPECTRUMLIST_SCANSUMMER_HPP_ 

