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
#include "ChromatogramList_mz5.hpp"


namespace pwiz {
namespace msdata {


namespace {

using namespace mz5;

/**
 * Implementation of a ChromatogramList.
 */
class ChromatogramList_mz5Impl: public ChromatogramList_mz5
{
    public:

    /**
     * Default constructor.
     * @param readPtr helper class to read mz5 files
     * @param connectenPtr connection to mz5 file
     * @param msd MSData object
     */
    ChromatogramList_mz5Impl(boost::shared_ptr<ReferenceRead_mz5> readPtr,
                             boost::shared_ptr<Connection_mz5> connectionptr,
                             const MSData& msd);

    /**
     * Getter.
     * @return number of data points in this chromatogram.
     */
    virtual size_t size() const;

    /**
     * Getter.
     * @return minimal information about a chromatogram.
     */
    virtual const ChromatogramIdentity& chromatogramIdentity(size_t index) const;

    /**
     * Returns the index the chromatogram with a specific id, otherwise an out_of_range exception.
     * @param id chromatogram id
     * @return index
     */
    virtual size_t find(const std::string& id) const;

    /**
     * Returns a specific chromatogram.
     * @param index index
     * @param getBinaryData
     * @return smart pointer to chromatogram
     */
    virtual ChromatogramPtr chromatogram(size_t index, bool getBinaryData) const;

    /**
     * Destructor.
     */
    virtual ~ChromatogramList_mz5Impl();

private:
    /**
     * Initializes chromatogram list.
     */
    void initialize() const;

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
     * List of binary data meta information.
     */
    mutable BinaryDataMZ5* binaryParamList_;
    /**
     * List of all chromatogram identities.
     */
    mutable std::vector<ChromatogramIdentity> identities_;
    /**
     * List of chromatogram meta data.
     */
    mutable ChromatogramMZ5* chromatogramData_;
    /**
     * Mapping of a chromatogram ID to an index.
     */
    mutable std::map<std::string, size_t> chromatogramIndex_;
    /**
     * Map of chromatogram indices to start and stop indices in time and intensity datasets.
     */
    mutable std::map<hsize_t, std::pair<hsize_t, hsize_t> > chromatogramRanges_;
    mutable bool initialized_;
    mutable size_t numberOfChromatograms_;
};

//TODO initialize chomatogramlist the same way as spectrumlist?

ChromatogramList_mz5Impl::ChromatogramList_mz5Impl(boost::shared_ptr<ReferenceRead_mz5> readPtr,
                                                   boost::shared_ptr<Connection_mz5> connectionPtr,
                                                   const MSData& msd) :
    msd_(msd)
{
    setDataProcessingPtr(readPtr->getDefaultChromatogramDP(0));
    rref_ = readPtr;
    conn_ = connectionPtr;
    chromatogramData_ = 0;
    binaryParamList_ = 0;
    initialized_ = false;
    if (conn_->getConfiguration().getChromatogramLoadPolicy()
            == Configuration_mz5::CLP_InitializeAllOnCreation)
    {
        initialize();
    }
}

ChromatogramList_mz5Impl::~ChromatogramList_mz5Impl()
{
    if (chromatogramData_)
    {
        conn_->clean(Configuration_mz5::ChromatogramMetaData,
                chromatogramData_, numberOfChromatograms_);
        //free(chromatogramData_);
        chromatogramData_ = 0;
    }
    if (binaryParamList_)
    {
        conn_->clean(Configuration_mz5::ChromatogramBinaryMetaData,
                binaryParamList_, numberOfChromatograms_);
        //free(binaryParamList_);
        binaryParamList_ = 0;
    }
}

void ChromatogramList_mz5Impl::initialize() const
{
    if (!initialized_)
    {
        size_t dsend = conn_->getFields().find(
                Configuration_mz5::ChromatogramMetaData)->second;
        numberOfChromatograms_ = dsend;
        if (dsend > 0)
        {
            binaryParamList_ = (BinaryDataMZ5*) calloc(dsend,
                    sizeof(BinaryDataMZ5));
            chromatogramData_ = (ChromatogramMZ5*) calloc(dsend,
                    sizeof(ChromatogramMZ5));
            conn_->readDataSet(Configuration_mz5::ChromatogramMetaData, dsend,
                    chromatogramData_);
            conn_->readDataSet(Configuration_mz5::ChromatogramBinaryMetaData,
                    dsend, binaryParamList_);
            for (size_t i = 0; i < dsend; ++i)
            {
                //ChromatogramPtr ptr(cl[i].getChromatogram(*rref_));
                //chromatogramPtrList_.push_back(ptr);
                identities_.push_back(
                        chromatogramData_[i].getChromatogramIdentity());
                std::string cid(chromatogramData_[i].id);
                chromatogramIndex_.insert(make_pair(cid, i));
                //References::resolve(*ptr, msd_);
            }
            //conn_->clean(Configuration_mz5::ChromatogramMetaData, cl, dsend);

            std::vector<unsigned long> index;
            index.resize(dsend);
            conn_->readDataSet(Configuration_mz5::ChromatogramIndex, dsend,
                    &index[0]);
            hsize_t last = 0, current = 0;
            hsize_t overflow_correction = 0; // mz5 writes these as 32 bit values, so deal with overflow
            for (size_t i = 0; i < index.size(); ++i)
            {
                current = static_cast<hsize_t> (index[i]) + overflow_correction;
                if (last > current)
                {
                    overflow_correction += 0x0100000000; // This assumes no chromatogram has more than 4GB of data
                    current = static_cast<hsize_t> (index[i]) + overflow_correction;
                }
                chromatogramRanges_.insert(make_pair(i, make_pair(last, current)));
                last = current;
            }
        }
        else
        {
            binaryParamList_ = 0;
            chromatogramData_ = 0;
        }
        initialized_ = true;
    }
}

size_t ChromatogramList_mz5Impl::size() const
{
    initialize();
    return numberOfChromatograms_;
}

const ChromatogramIdentity& ChromatogramList_mz5Impl::chromatogramIdentity(size_t index) const
{
    initialize();
    if (numberOfChromatograms_ > index && index >= 0)
    {
        return identities_[index];
    }
    throw std::out_of_range("ChromatogramList_mz5Impl::chromatogramIdentity() out of range");
}

size_t ChromatogramList_mz5Impl::find(const std::string& id) const
{
    initialize();
    std::map<std::string, size_t>::const_iterator it = chromatogramIndex_.find(id);
    return it != chromatogramIndex_.end() ? it->second : size();
}

ChromatogramPtr ChromatogramList_mz5Impl::chromatogram(size_t index, bool getBinaryData) const
{
    initialize();
    if (numberOfChromatograms_ > index)
    {
        ChromatogramPtr ptr(chromatogramData_[index].getChromatogram(*rref_));
        std::pair<hsize_t, hsize_t> bounds = chromatogramRanges_.find(index)->second;
        hsize_t start = bounds.first;
        hsize_t end = bounds.second;
        ptr->defaultArrayLength = end - start;
        if (getBinaryData)
        {
            if (!binaryParamList_[index].empty()) {
                std::vector<double> time, inten;
                conn_->getData(time, Configuration_mz5::ChomatogramTime, start, end);
                conn_->getData(inten, Configuration_mz5::ChromatogramIntensity, start, end);
                ptr->setTimeIntensityArrays(time, inten, CVID_Unknown, CVID_Unknown);
                // time and intensity unit will be set by the following command
                binaryParamList_[index].fill(*ptr->getTimeArray(), *ptr->getIntensityArray(), *rref_);
            }
        }
        References::resolve(*ptr, msd_);
        return ptr;
    }
    throw std::out_of_range("ChromatogramList_mz5Impl::chromatogram() out of range");
}

} // namespace


PWIZ_API_DECL
ChromatogramListPtr ChromatogramList_mz5::create(boost::shared_ptr<ReferenceRead_mz5> readPtr,
                                                 boost::shared_ptr<Connection_mz5> connectionPtr,
                                                 const MSData& msd)
{
    return ChromatogramListPtr(new ChromatogramList_mz5Impl(readPtr, connectionPtr, msd));
}

PWIZ_API_DECL ChromatogramList_mz5::~ChromatogramList_mz5()
{
}


} // namespace msdata
} // namespace pwiz
