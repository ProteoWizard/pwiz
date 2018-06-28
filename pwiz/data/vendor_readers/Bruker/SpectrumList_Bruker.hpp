//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
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


#ifndef _SPECTRUMLIST_BRUKER_HPP_
#define _SPECTRUMLIST_BRUKER_HPP_

#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/msdata/Reader.hpp"
#include "pwiz/data/msdata/SpectrumListBase.hpp"
#include "pwiz/utility/misc/IntegerSet.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Container.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/Stream.hpp"
#include "Reader_Bruker_Detail.hpp"
#include <boost/container/flat_map.hpp>


namespace pwiz {
namespace msdata {
namespace detail {

using boost::shared_ptr;

//
// SpectrumList_Bruker
//
class PWIZ_API_DECL SpectrumList_Bruker : public SpectrumListBase
{
    public:

    virtual size_t size() const;
    virtual const SpectrumIdentity& spectrumIdentity(size_t index) const;
    virtual size_t find(const string& id) const;
    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData) const;
    virtual SpectrumPtr spectrum(size_t index, DetailLevel detailLevel) const;
    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData, const pwiz::util::IntegerSet& msLevelsToCentroid) const;
    virtual SpectrumPtr spectrum(size_t index, DetailLevel detailLevel, const pwiz::util::IntegerSet& msLevelsToCentroid) const;
    virtual bool hasIonMobility() const;
    virtual bool hasPASEF() const;
    virtual bool canConvertInverseK0AndCCS() const;
    virtual double inverseK0ToCCS(double inverseK0, double mz, int charge) const;
    virtual double ccsToInverseK0(double ccs, double mz, int charge) const;

#ifdef PWIZ_READER_BRUKER
    SpectrumList_Bruker(MSData& msd,
                        const string& rootpath,
                        Bruker::Reader_Bruker_Format format,
                        CompassDataPtr compassDataPtr,
                        const Reader::Config& config);

    MSSpectrumPtr getMSSpectrumPtr(size_t index, vendor_api::Bruker::DetailLevel detailLevel) const;

    private:

    MSData& msd_;
    bfs::path rootpath_;
    Bruker::Reader_Bruker_Format format_;
    mutable CompassDataPtr compassDataPtr_;
    const Reader::Config config_;
    size_t size_;
    vector<bfs::path> sourcePaths_;

    struct IndexEntry : public SpectrumIdentity
    {
        int source;
        int collection; // -1 for an MS spectrum
        int scan;
    };

    vector<IndexEntry> index_;

    // idToIndexMap_["scan=<#>" or "file=<sourceFile::id>"] == index
    boost::container::flat_map<string, size_t> idToIndexMap_;

    void fillSourceList();
    void createIndex();
    //string findPrecursorID(int precursorMsLevel, size_t index) const;
#endif // PWIZ_READER_BRUKER
};

} // detail
} // msdata
} // pwiz

#endif // _SPECTRUMLIST_BRUKER_HPP_
