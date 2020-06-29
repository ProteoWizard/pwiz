//
// $Id$
//
//
// Original authors: Mathias Wilhelm <mw@wilhelmonline.com>
//                   Marc Kirchner <mail@marc-kirchner.de>
//
// Copyright 2011 Proteomics Center
//                Children's Hospital Boston, Boston, MA 02135
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

#include "pwiz/utility/misc/Std.hpp"
#include "References.hpp"
#include "SpectrumList_mz5.hpp"
#include <boost/thread.hpp>

namespace pwiz {
namespace msdata {

namespace {

using namespace mz5;

/**
 * Implementation of a spectrum list, using a mz5 file.
 */
class SpectrumList_mz5Impl: public SpectrumList_mz5
{
public:
    /**
     * Default constructor.
     * @param readPtr helper object to read mz5 files
     * @param connectionPtr connection to mz5 file
     * @param msd MSData object
     */
    SpectrumList_mz5Impl(boost::shared_ptr<ReferenceRead_mz5> readPtr,
                         boost::shared_ptr<Connection_mz5> connectionPtr,
                         const MSData& msd);

    /**
     * Getter.
     * @return number of spectra
     */
    virtual size_t size() const;

    /**
     * Return minimal information about a spectrum.
     * @return index index
     */
    virtual const SpectrumIdentity& spectrumIdentity(size_t index) const;

    /**
     * Returns the spectrum with a specific id, if not found an out_of_range exception.
     * @param id id
     */
    virtual size_t find(const std::string& id) const;

    /**
     * Getter.
     * @param spotID a spot id
     * @return an index list with all spectra with this spotID
     */
    virtual IndexList findSpotID(const std::string& spotID) const;

    /**
     * Getter.
     * @param index spectrum index
     * @param getBinaryData If true this method will also get all binary data(raw data)
     * @return smart pointer to spectrum
     */
    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData) const;

    /**
    * Getter.
    * @param index spectrum index
    * @param detailLevel If set to DetailLevel_FullData this method will also get all binary data(raw data)
    * @return smart pointer to spectrum
    */
    virtual SpectrumPtr spectrum(size_t index, DetailLevel detailLevel) const;

    /**
     * Destructor.
     */
    virtual ~SpectrumList_mz5Impl();

private:

    /**
     * MSData object.
     */
    const MSData& msd_;
    /**
     * Helper class to read mz5 files. This reference is used to resolve for example CVParams.
     */
    boost::shared_ptr<ReferenceRead_mz5> rref_;
    /**
     * Connection to the mz5 file used to get raw data.
     */
    boost::shared_ptr<Connection_mz5> conn_;
    /**
     * List of meta information.
     */
    mutable SpectrumMZ5* spectrumData_;
    /**
     * List of binary data meta information.
     */
    mutable BinaryDataMZ5* binaryParamsData_;
    mutable std::vector<SpectrumIdentity> spectrumIdentityList_;
    mutable std::map<size_t, std::pair<hsize_t, hsize_t> > spectrumRanges_;
    mutable std::map<std::string, size_t> idMap_;
    mutable std::map<std::string, IndexList> spotMap_;
    size_t numberOfSpectra_;
    mutable bool initSpectra_;
    mutable boost::mutex readMutex;

    void initSpectra() const;
};

SpectrumList_mz5Impl::~SpectrumList_mz5Impl()
{
    if (spectrumData_)
    {
        conn_->clean(Configuration_mz5::SpectrumMetaData, spectrumData_,
                numberOfSpectra_);
        //free(spectrumData_);
        spectrumData_ = 0;
    }
    if (binaryParamsData_)
    {
        conn_->clean(Configuration_mz5::SpectrumBinaryMetaData,
                binaryParamsData_, numberOfSpectra_);
        //free(binaryParamsData_);
        binaryParamsData_ = 0;
    }
}

SpectrumList_mz5Impl::SpectrumList_mz5Impl(boost::shared_ptr<ReferenceRead_mz5> readPtr,
                                           boost::shared_ptr<Connection_mz5> connectionPtr,
                                           const MSData& msd)
    : msd_(msd), rref_(readPtr), conn_(connectionPtr)
{
    initSpectra_ = false;

    setDataProcessingPtr(readPtr->getDefaultSpectrumDP(0));

    numberOfSpectra_ = conn_->getFields().find(Configuration_mz5::SpectrumMetaData)->second;
    spectrumData_ = 0;
    binaryParamsData_ = 0;

    if (conn_->getConfiguration().getSpectrumLoadPolicy()
            == Configuration_mz5::SLP_InitializeAllOnCreation)
    {
        initSpectra();
    }
}

void SpectrumList_mz5Impl::initSpectra() const
{
    if (!initSpectra_)
    {
        if (numberOfSpectra_ > 0)
        {
            std::vector<unsigned long> index;
            index.resize(numberOfSpectra_);
            size_t dsend;
            conn_->readDataSet(Configuration_mz5::SpectrumIndex, dsend, &index[0]);
            hsize_t last = 0, current = 0;
            hsize_t overflow_correction = 0; // mz5 writes these as 32 bit values, so deal with overflow
            for (size_t i = 0; i < index.size(); ++i)
            {
                current = static_cast<hsize_t> (index[i]) + overflow_correction;
                if (last > current)
                {
                    overflow_correction += 0x0100000000; // This assumes no scan has more than 4GB of peak data
                    current = static_cast<hsize_t> (index[i]) + overflow_correction;
                }
                spectrumRanges_.insert(make_pair(i, make_pair(last, current)));
                last = current;
            }

            spectrumData_ = (SpectrumMZ5*) calloc(numberOfSpectra_, sizeof(SpectrumMZ5));
            conn_->readDataSet(Configuration_mz5::SpectrumMetaData, dsend, spectrumData_);

            binaryParamsData_ = (BinaryDataMZ5*) calloc(dsend, sizeof(BinaryDataMZ5));
            conn_->readDataSet(Configuration_mz5::SpectrumBinaryMetaData, dsend, binaryParamsData_);

            spectrumIdentityList_.resize(dsend);
            for (hsize_t i = 0; i < dsend; ++i)
            {
                spectrumIdentityList_[i] = spectrumData_[i].getSpectrumIdentity();
                idMap_.insert(make_pair(spectrumIdentityList_[i].id, i));
                if (!spectrumIdentityList_[i].spotID.empty())
                    spotMap_[spectrumIdentityList_[i].spotID].push_back(i);
            }
        }
        initSpectra_ = true;
    }
}

size_t SpectrumList_mz5Impl::size() const
{
    return numberOfSpectra_;
}

const SpectrumIdentity& SpectrumList_mz5Impl::spectrumIdentity(size_t index) const
{
    initSpectra();
    if (index >= 0 && index < numberOfSpectra_)
    {
        return spectrumIdentityList_[index];
    }
    throw std::out_of_range("[SpectrumList_mz5Impl::spectrumIdentity()] out of range");
}

size_t SpectrumList_mz5Impl::find(const std::string& id) const
{
    initSpectra();
    std::map<std::string, size_t>::const_iterator it = idMap_.find(id);
    return it != idMap_.end() ? it->second : checkNativeIdFindResult(size(), id);
}

IndexList SpectrumList_mz5Impl::findSpotID(const std::string& spotID) const
{
    initSpectra();
    std::map<std::string, IndexList>::const_iterator it = spotMap_.find(spotID);
    return it != spotMap_.end() ? it->second : IndexList();
}

SpectrumPtr SpectrumList_mz5Impl::spectrum(size_t index, DetailLevel detailLevel) const
{
    return spectrum(index, detailLevel == DetailLevel_FullData);
}

SpectrumPtr SpectrumList_mz5Impl::spectrum(size_t index, bool getBinaryData) const
{
    boost::lock_guard<boost::mutex> lock(readMutex);  // lock_guard will unlock mutex when out of scope or when exception thrown (during destruction)
    initSpectra();
    if (index >= 0 && index < numberOfSpectra_)
    {
        SpectrumPtr ptr(spectrumData_[index].getSpectrum(*rref_, *conn_));
        std::pair<hsize_t, hsize_t> bounds = spectrumRanges_.find(index)->second;
        hsize_t start = bounds.first;
        hsize_t end = bounds.second;
        ptr->defaultArrayLength = end - start;
        if (getBinaryData)
        {
            if (!binaryParamsData_[index].empty()) {
                std::vector<double> mz, inten;
                conn_->getData(mz, Configuration_mz5::SpectrumMZ, start, end);
                conn_->getData(inten, Configuration_mz5::SpectrumIntensity, start, end);
                ptr->setMZIntensityArrays(mz, inten, CVID_Unknown);
                // intensity unit will be set by the following command
                binaryParamsData_[index].fill(*ptr->getMZArray(), *ptr->getIntensityArray(), *rref_);
            }
        }
        References::resolve(*ptr, msd_);
        return ptr;
    }
    throw std::out_of_range("[SpectrumList_mz5Impl::spectrum()] out of range");
}

} // namespace


PWIZ_API_DECL
SpectrumListPtr SpectrumList_mz5::create(boost::shared_ptr<ReferenceRead_mz5> readPtr,
                                         boost::shared_ptr<Connection_mz5> connectionPtr,
                                         const MSData& msd)
{
    return SpectrumListPtr(new SpectrumList_mz5Impl(readPtr, connectionPtr, msd));
}

PWIZ_API_DECL SpectrumList_mz5::~SpectrumList_mz5()
{
}


} // namespace msdata
} // namespace pwiz
