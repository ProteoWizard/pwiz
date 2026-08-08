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
#include <atomic>


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
    SpectrumListBase()
    :   spectrum_id_mismatch_hash_(impl_.hash("spectrum id mismatch")),
        mzOrderVerdict_(MzOrderVerdict::unsettled)
    {}

    /// issues a warning once per list instance (based on string hash)
    void warn_once(const char* msg) const { impl_.warn_once(msg); }

    /// implementation of ChromatogramList/SpectrumList
    const boost::shared_ptr<const DataProcessing> dataProcessingPtr() const { return dp_; }

    /// set DataProcessing
    void setDataProcessingPtr(DataProcessingPtr dp) { dp_ = dp; }

    protected:

    // when find() fails to find a spectrum id, check whether the id fields of the input id and the spectrum list are matching
    size_t checkNativeIdFindResult(size_t result, const std::string& id) const;

    /// Put a spectrum's peaks in ascending m/z order if its writer did not, carrying every array
    /// that holds one value per peak along with them.
    ///
    /// Call this on the way out of spectrum(), from a list that reads a format some other tool
    /// wrote. Ascending m/z is nowhere required by any of those specifications, but it is what
    /// every consumer assumes: extraction binary searches the m/z axis, so a spectrum presented in
    /// another order makes the search land nowhere useful and the chromatogram comes out empty with
    /// no error at all. Writers that use another order do exist - one shipped peaks in ascending
    /// intensity - so the order is checked rather than trusted.
    ///
    /// The vendor lists do not call this, on the grounds that peaks arriving through a vendor API
    /// are already ascending. That reasoning is weakest for the readers whose input is a file some
    /// other desktop tool wrote rather than an instrument stream - ABI T2D, UIMF, Mobilion,
    /// waters_connect - and nothing enforces the ordering for them; none has been observed to
    /// produce anything else. A format added later that forgets simply goes uncorrected, which is
    /// the same as today and the safe direction to fail in.
    ///
    /// The question is settled from the first few spectra of a file rather than re-asked for every
    /// one, since walking every m/z array of every file to catch a rare writer is a cost the whole
    /// world would pay for the few. The first spectrum alone will not do: early scans can precede
    /// the sample and carry almost no peaks, and a spectrum with two of them ascends half the time
    /// by chance. So the two verdicts are not symmetric - one spectrum out of order proves the
    /// writer does not sort however few peaks it holds, while peaks found in order are only
    /// believed from a spectrum with enough of them to mean it.
    void ensureMzAscending(const SpectrumPtr& spectrum) const;

    // Useful for avoiding repeated ctor when you just want an empty set
    const pwiz::util::IntegerSet MSLevelsNone;

    DataProcessingPtr dp_;

    private:
    ListBase impl_;
    size_t spectrum_id_mismatch_hash_;

    /// what this file has shown so far about the way its writer orders peaks
    enum class MzOrderVerdict { unsettled, writerSortsByMz, writerDoesNotSortByMz };

    // mutable and atomic because spectrum() is const and is called from worker threads;
    // condemnation is an unconditional store while the good verdict is a compare-exchange from
    // unsettled, so no late-arriving good spectrum can un-condemn a file
    mutable std::atomic<MzOrderVerdict> mzOrderVerdict_;
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

