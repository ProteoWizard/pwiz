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


#define PWIZ_SOURCE


#include "ChromatogramList_Bruker.hpp"


#ifdef PWIZ_READER_BRUKER
#include "pwiz/data/msdata/MSData.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/IntegerSet.hpp"
#include "pwiz/utility/misc/SHA1Calculator.hpp"
#include "pwiz/utility/misc/automation_vector.h"

using namespace pwiz::util;
using namespace pwiz::vendor_api::Bruker;

namespace pwiz {
namespace msdata {
namespace detail {

using namespace Bruker;


PWIZ_API_DECL
ChromatogramList_Bruker::ChromatogramList_Bruker(MSData& msd,
                                         const string& rootpath,
                                         Reader_Bruker_Format format,
                                         CompassDataPtr compassDataPtr,
                                         const Reader::Config& config)
:   msd_(msd), rootpath_(rootpath), format_(format),
    compassDataPtr_(compassDataPtr),
    size_(0),
    config_(config)
{
    createIndex();
}


PWIZ_API_DECL size_t ChromatogramList_Bruker::size() const
{
    return size_;
}


PWIZ_API_DECL const ChromatogramIdentity& ChromatogramList_Bruker::chromatogramIdentity(size_t index) const
{
    if (index > size_)
        throw runtime_error(("[ChromatogramList_Bruker::chromatogramIdentity()] Bad index: " 
                            + lexical_cast<string>(index)).c_str());
    return index_[index];
}


PWIZ_API_DECL size_t ChromatogramList_Bruker::find(const string& id) const
{
    map<string, size_t>::const_iterator scanItr = idToIndexMap_.find(id);
    if (scanItr == idToIndexMap_.end())
        return size_;
    return scanItr->second;
}


PWIZ_API_DECL ChromatogramPtr ChromatogramList_Bruker::chromatogram(size_t index, bool getBinaryData) const
{
    return chromatogram(index, getBinaryData ? DetailLevel_FullData : DetailLevel_FullMetadata);
}


PWIZ_API_DECL ChromatogramPtr ChromatogramList_Bruker::chromatogram(size_t index, DetailLevel detailLevel) const
{
    if (index > size_)
        throw runtime_error(("[ChromatogramList_Bruker::chromatogram()] Bad index: " 
                            + lexical_cast<string>(index)).c_str());

    // allocate a new Spectrum
    ChromatogramPtr result(new Chromatogram);
    if (!result.get())
        throw runtime_error("[ChromatogramList_Bruker::chromatogram()] Allocation error.");

    const IndexEntry& ci = index_[index];
    result->index = ci.index;
    result->id = ci.id;
    result->set(ci.chromatogramType);

    if (detailLevel < DetailLevel_FullMetadata)
        return result;
    bool getBinaryData = detailLevel == DetailLevel_FullData;

    vendor_api::Bruker::ChromatogramPtr cd;

    switch (ci.chromatogramType)
    {
        case MS_TIC_chromatogram:
            cd = compassDataPtr_->getTIC(config_.globalChromatogramsAreMs1Only);
            break;

        case MS_basepeak_chromatogram:
            cd = compassDataPtr_->getBPC(config_.globalChromatogramsAreMs1Only);
            break;

        default:
            throw runtime_error("[ChromatogramList_Bruker] unsupported chromatogramType");
    }

    result->setTimeIntensityArrays(cd->times, cd->intensities, UO_second, MS_number_of_detector_counts);

    if (format_ != Reader_Bruker_Format_FID && format_ != Reader_Bruker_Format_U2)
    {
        auto msLevelArray = boost::make_shared<IntegerDataArray>();
        result->integerDataArrayPtrs.emplace_back(msLevelArray);
        msLevelArray->set(MS_non_standard_data_array, "ms level", UO_dimensionless_unit);
        msLevelArray->data.resize(cd->times.size());
        for (size_t i = 1, end = cd->times.size(); i <= end; ++i)
            msLevelArray->data[i-1] = compassDataPtr_->getMSSpectrum(i, vendor_api::Bruker::DetailLevel_InstantMetadata)->getMSMSStage();
    }

    return result;
}


PWIZ_API_DECL void ChromatogramList_Bruker::createIndex()
{
    auto tic = compassDataPtr_->getTIC();
    if (tic)
    {
        index_.push_back(IndexEntry());
        IndexEntry& ie = index_.back();
        ie.index = index_.size() - 1;
        ie.id = "TIC";
        ie.chromatogramType = MS_TIC_chromatogram;
        idToIndexMap_[ie.id] = ie.index;
    }

    auto bpc = compassDataPtr_->getBPC();
    if (bpc)
    {
        index_.push_back(IndexEntry());
        IndexEntry& ie = index_.back();
        ie.index = index_.size() - 1;
        ie.id = "BPC";
        ie.chromatogramType = MS_basepeak_chromatogram;
        idToIndexMap_[ie.id] = ie.index;
    }

    /*if (format_ == Reader_Bruker_Format_U2)
    {
        CompassXtractWrapper::LC_TraceDeclarationList& tdList = compassXtractWrapperPtr_->traceDeclarations_;
        CompassXtractWrapper::LC_AnalysisPtr& analysis = compassXtractWrapperPtr_->lcAnalysis_;
        for (size_t i=0; i < tdList.size(); ++i)
        {
            long tId = tdList[i]->GetTraceId();
            CompassXtractWrapper::LC_TraceDeclarationPtr& td = compassXtractWrapperPtr_->traceDeclarations_[i];

            if (td->GetTraceUnit() == BDal_CXt_Lc_Interfaces::Unit_Intensity)
            {
                index_.push_back(IndexEntry());
                IndexEntry& ci = index_.back();
                ci.declaration = i;
                ci.trace = tId;
                ci.index = index_.size()-1;
                ci.id = "declaration=" + lexical_cast<string>(i) + " trace=" + lexical_cast<string>(tId);
                ci.chromatogramType = MS_TIC_chromatogram;
                idToIndexMap_[ci.id] = ci.index;
            }
            //else
            //    throw runtime_error("[ChromatogramList_Bruker::chromatogram()] unexpected TraceUnit");
        }
    }*/

    size_ = index_.size();
}


} // detail
} // msdata
} // pwiz

#else // PWIZ_READER_BRUKER

//
// non-MSVC implementation
//

namespace pwiz {
namespace msdata {
namespace detail {

namespace {const ChromatogramIdentity emptyIdentity;}

size_t ChromatogramList_Bruker::size() const {return 0;}
const ChromatogramIdentity& ChromatogramList_Bruker::chromatogramIdentity(size_t index) const {return emptyIdentity;}
size_t ChromatogramList_Bruker::find(const string& id) const {return 0;}
ChromatogramPtr ChromatogramList_Bruker::chromatogram(size_t index, bool getBinaryData) const {return ChromatogramPtr();}

} // detail
} // msdata
} // pwiz

#endif // PWIZ_READER_BRUKER
