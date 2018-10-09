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
#include <boost/functional/hash.hpp>
#include <stdexcept>
#include <iostream>


namespace pwiz {
namespace msdata {


/// common functionality for base SpectrumList implementations
class PWIZ_API_DECL SpectrumListBase : public SpectrumList
{
    public:
    SpectrumListBase() : MSLevelsNone() {};

    /// implementation of SpectrumList
    virtual const boost::shared_ptr<const DataProcessing> dataProcessingPtr() const {return dp_;}

    /// set DataProcessing
    virtual void setDataProcessingPtr(DataProcessingPtr dp) { dp_ = dp; }

    /// issues a warning once per SpectrumList instance (based on string hash)
    virtual void warn_once(const char* msg) const
    {
        boost::hash<const char*> H;
        if (warn_msg_hashes.insert(H(msg)).second) // .second is true iff value is new
        {
            std::cerr << msg << std::endl;
        }
    }

    protected:

    DataProcessingPtr dp_;

    // Useful for avoiding repeated ctor when you just want an empty set
    const pwiz::util::IntegerSet MSLevelsNone;

    private:

    mutable std::set<size_t> warn_msg_hashes; // for warn_once use
};


class PWIZ_API_DECL SpectrumListIonMobilityBase : public SpectrumListBase
{
    public:
    virtual bool hasIonMobility() const = 0;
    // CONSIDER: should this be in the interface? virtual bool hasPASEF() const = 0;
    virtual bool canConvertIonMobilityAndCCS() const = 0;
    virtual double ionMobilityToCCS(double ionMobility, double mz, int charge) const = 0;
    virtual double ccsToIonMobility(double ccs, double mz, int charge) const = 0;
};


} // namespace msdata 
} // namespace pwiz


#endif // _SPECTRUMLISTBASE_HPP_ 

