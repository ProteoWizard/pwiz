//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
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


#ifndef _SPECTRUMLISTBASE_HPP_ 
#define _SPECTRUMLISTBASE_HPP_ 


#include "pwiz/data/msdata/MSData.hpp"
#include "pwiz/utility/misc/IntegerSet.hpp"
#include <boost/make_shared.hpp>
#include <stdexcept>
#include <iostream>


namespace pwiz {
namespace msdata {

/// common functionality for base ChromatogramList and SpectrumList implementations
class PWIZ_API_DECL ListBase
{
    public:

    /// issues a warning once per list instance (based on string hash)
    void warn_once(const char* msg) const;

    size_t hash(const char*) const;

    std::set<size_t>& warn_msg_hashes() const { return warn_msg_hashes_; }

    protected:
    mutable std::set<size_t> warn_msg_hashes_; // for warn_once use
};


/// common functionality for base SpectrumList implementations
class PWIZ_API_DECL SpectrumListBase : public SpectrumList
{
    public:
    SpectrumListBase() : spectrum_id_mismatch_hash_(impl_.hash("spectrum id mismatch")) {}

    /// issues a warning once per list instance (based on string hash)
    void warn_once(const char* msg) const { impl_.warn_once(msg); }

    /// implementation of ChromatogramList/SpectrumList
    const boost::shared_ptr<const DataProcessing> dataProcessingPtr() const { return dp_; }

    /// set DataProcessing
    void setDataProcessingPtr(DataProcessingPtr dp) { dp_ = dp; }

    protected:

    // when find() fails to find a spectrum id, check whether the id fields of the input id and the spectrum list are matching
    size_t checkNativeIdFindResult(size_t result, const std::string& id) const;

    // Useful for avoiding repeated ctor when you just want an empty set
    const pwiz::util::IntegerSet MSLevelsNone;

    DataProcessingPtr dp_;

    private:
    ListBase impl_;
    size_t spectrum_id_mismatch_hash_;
};


class PWIZ_API_DECL SpectrumListIonMobilityBase : public SpectrumListBase
{
    public:
    virtual bool hasIonMobility() const = 0;
    virtual bool hasCombinedIonMobility() const = 0; // Returns true if IM data is returned in 3-array format
    // CONSIDER: should this be in the interface? virtual bool hasPASEF() const = 0;
    virtual bool canConvertIonMobilityAndCCS() const = 0;
    virtual double ionMobilityToCCS(double ionMobility, double mz, int charge) const = 0;
    virtual double ccsToIonMobility(double ccs, double mz, int charge) const = 0;
};


} // namespace msdata 
} // namespace pwiz


#endif // _SPECTRUMLISTBASE_HPP_ 

