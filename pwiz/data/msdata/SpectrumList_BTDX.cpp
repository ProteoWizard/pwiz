//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars Sinai Medical Center, Los Angeles, California  90048
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

#define PWIZ_SOURCE

#include "SpectrumList_BTDX.hpp"
#include "References.hpp"
#include "pwiz/utility/minimxml/SAXParser.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <boost/thread.hpp>


namespace pwiz {
namespace msdata {


using namespace pwiz::minimxml;
using namespace pwiz::util;
using boost::iostreams::stream_offset;
using boost::iostreams::offset_to_position;


namespace {

class SpectrumList_BTDXImpl : public SpectrumList_BTDX
{
    public:

    SpectrumList_BTDXImpl(shared_ptr<istream> is, const MSData& msd);

    // SpectrumList implementation
    virtual size_t size() const {return index_.size();}
    virtual const SpectrumIdentity& spectrumIdentity(size_t index) const;
    virtual size_t find(const string& id) const;
    virtual size_t findNative(const string& nativeID) const;
    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData) const;

    private:
    shared_ptr<istream> is_;
    const MSData& msd_;
    vector<SpectrumIdentity> index_;
    map<string,size_t> idToIndex_;
    mutable vector<SpectrumPtr> spectrumCache_;
    mutable boost::mutex readMutex;

    void createIndex();
    void createMaps();
};


SpectrumList_BTDXImpl::SpectrumList_BTDXImpl(shared_ptr<istream> is, const MSData& msd)
:   is_(is), msd_(msd)
{
    createIndex();
    createMaps();
    spectrumCache_.resize(index_.size());
}


const SpectrumIdentity& SpectrumList_BTDXImpl::spectrumIdentity(size_t index) const
{
    if (index > index_.size())
        throw runtime_error("[SpectrumList_BTDX::spectrumIdentity()] Index out of bounds.");

    return index_[index];
}


size_t SpectrumList_BTDXImpl::find(const string& id) const
{
    map<string,size_t>::const_iterator it=idToIndex_.find(id);
    return it!=idToIndex_.end() ? it->second : checkNativeIdFindResult(size(), id);
}


size_t SpectrumList_BTDXImpl::findNative(const string& nativeID) const
{
    return find(nativeID); 
}


class HandlerPeaks : public SAXParser::Handler
{
    public:

    HandlerPeaks(Spectrum& spectrum,
                 bool getBinaryData,
                 BinaryData<double>& mzArray,
                 BinaryData<double>& iArray)
    :   spectrum_(spectrum),
        mzArray_(mzArray), iArray_(iArray),
        totalIntensity_(0),
        basePeakIntensity_(0),
        getBinaryData_(getBinaryData)
    {
        spectrum_.defaultArrayLength = 0;
    }

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "pk")
        {
            string mz, i, sn, z;
            getAttribute(attributes, "mz", mz);
            getAttribute(attributes, "i", i);
            //getAttribute(attributes, "sn", sn);
            //getAttribute(attributes, "z", z);

            double mzValue = lexical_cast<double>(mz);
            double iValue = lexical_cast<double>(i);
            //double snValue = lexical_cast<double>(sn);
            //int zValue = lexical_cast<int>(z);

            if (getBinaryData_)
            {
                mzArray_.push_back(mzValue);
                iArray_.push_back(iValue);
            }

            ++spectrum_.defaultArrayLength;
            totalIntensity_ += iValue;

            if (iValue > basePeakIntensity_)
            {
                basePeakMz_ = mzValue;
                basePeakIntensity_ = iValue;
            }

            return Status::Ok;
        }
        else if (name == "ms_peaks")
        {
            return Status::Ok;
        }

        throw runtime_error(("[SpectrumList_BTDX::HandlerPeaks] Unexpected element name: " + name).c_str());
    }

    virtual Status endElement(const string& name,
                              stream_offset position)
    {
        if (name == "ms_peaks")
        {
            spectrum_.set(MS_TIC, totalIntensity_);
            spectrum_.set(MS_base_peak_m_z, basePeakMz_);
            spectrum_.set(MS_base_peak_intensity, basePeakIntensity_);
            return Status::Done;
        }
        return Status::Ok;
    }
 
    private:
    Spectrum& spectrum_;
    BinaryData<double>& mzArray_;
    BinaryData<double>& iArray_;
    double totalIntensity_;
    double basePeakMz_;
    double basePeakIntensity_;
    bool getBinaryData_;
};


class HandlerCompound : public SAXParser::Handler
{
    public:

    HandlerCompound(const MSData& msd, Spectrum& spectrum, bool getBinaryData)
    :   msd_(msd),
        spectrum_(spectrum), 
        getBinaryData_(getBinaryData)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "cmpd")
        {
            string cmpdnr, rt, rt_unit;

            getAttribute(attributes, "cmpdnr", cmpdnr);
            getAttribute(attributes, "rt", rt);
            getAttribute(attributes, "rt_unit", rt_unit);

            spectrum_.id = cmpdnr;
            spectrum_.sourceFilePosition = position;

            if (!rt.empty())
            {
                CVID rtUnits = CVID_Unknown;
                if (rt_unit == "s")
                    rtUnits = UO_second;
                else if (rt_unit == "m")
                    rtUnits = UO_minute;
                else if (rt_unit == "h")
                    rtUnits = UO_hour;
                spectrum_.scanList.scans.push_back(Scan());
                spectrum_.scanList.scans.back().set(MS_scan_start_time, rt, rtUnits);
            }

            return Status::Ok;
        }
        else if (name == "title")
        {
            // WTF is this?
            return Status::Ok;
        }
        else if (name == "precursor")
        {
            string mz, i, sn, z, targetPosition, chipPosition;

            getAttribute(attributes, "mz", mz);
            getAttribute(attributes, "i", i);
            //getAttribute(attributes, "sn", sn);
            getAttribute(attributes, "z", z);
            getAttribute(attributes, "TargetPosition", targetPosition);
            getAttribute(attributes, "ChipPosition", chipPosition);

            if (!targetPosition.empty() && !chipPosition.empty())
                spectrum_.spotID = targetPosition + "," + chipPosition;

            double mzValue = lexical_cast<double>(mz);
            double iValue = lexical_cast<double>(i);
            //double snValue = lexical_cast<double>(sn);
            int zValue = lexical_cast<int>(z);

            spectrum_.precursors.push_back(
                Precursor(mzValue, iValue, zValue, MS_number_of_detector_counts));
            // TODO: support sn: spectrum_.precursors.back().set(MS_signal_to_noise, sn);
            return Status::Ok;
        }
        else if (name == "ms_spectrum")
        {
            string msms_stage;
            getAttribute(attributes, "msms_stage", msms_stage);
            if (msms_stage.empty())
                spectrum_.set(MS_ms_level, "1");
            else
                spectrum_.set(MS_ms_level, msms_stage);
            return Status::Ok;
        }
        else if (name == "ms_peaks")
        {
            spectrum_.setMZIntensityArrays(vector<double>(), vector<double>(), MS_number_of_detector_counts);

            handlerPeaks_ = shared_ptr<HandlerPeaks>(
                               new HandlerPeaks(spectrum_, getBinaryData_,
                                                spectrum_.getMZArray()->data,
                                                spectrum_.getIntensityArray()->data));
            return Status(Status::Delegate, &*handlerPeaks_);
        }

        throw runtime_error(("[SpectrumList_BTDX::HandlerCompound] Unexpected element name: " + name).c_str());
    }

    private:
    const MSData& msd_;
    Spectrum& spectrum_;
    bool getBinaryData_;
    shared_ptr<HandlerPeaks> handlerPeaks_;
};


SpectrumPtr SpectrumList_BTDXImpl::spectrum(size_t index, bool getBinaryData) const
{
    boost::lock_guard<boost::mutex> lock(readMutex);  // lock_guard will unlock mutex when out of scope or when exception thrown (during destruction)
    if (index > index_.size())
        throw runtime_error("[SpectrumList_BTDX::spectrum()] Index out of bounds.");

    // returned cached Spectrum if possible
    if (!getBinaryData && spectrumCache_[index].get())
        return spectrumCache_[index];

    // allocate Spectrum object and read it in
    SpectrumPtr result(new Spectrum);
    if (!result.get())
        throw runtime_error("[SpectrumList_BTDX::spectrum()] Out of memory.");

    result->index = index;
    result->set(MS_MSn_spectrum);
    result->set(MS_centroid_spectrum);

    result->scanList.set(MS_no_combination);
    result->scanList.scans.push_back(Scan());
    //Scan& scan = result->scanList.scans[0];

    is_->seekg(offset_to_position(index_[index].sourceFilePosition));
    if (!*is_)
        throw runtime_error("[SpectrumList_BTDX::spectrum()] Error seeking to <cmpd>.");

    HandlerCompound handler(msd_, *result, getBinaryData);
    SAXParser::parse(*is_, handler);

    // save to cache if no binary data
    if (!getBinaryData && !spectrumCache_[index].get())
        spectrumCache_[index] = result; 

    // resolve any references into the MSData object
    References::resolve(*result, msd_);

    return result;
}


class HandlerIndexCreator : public SAXParser::Handler
{
    public:

    HandlerIndexCreator(vector<SpectrumIdentity>& index)
    :   index_(index)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "cmpd")
        {
            string cmpdNumber;
            getAttribute(attributes, "cmpdnr", cmpdNumber);

            SpectrumIdentity si;
            si.index = index_.size();
            si.id = cmpdNumber;
            si.sourceFilePosition = position;

            index_.push_back(si);
        }

        return Status::Ok;
    }

    virtual Status endElement(const string& name, 
                              stream_offset position)
    {
        if (name == "compounds")
            return Status::Done;

        return Status::Ok;
    }

    private:
    vector<SpectrumIdentity>& index_;
};


void SpectrumList_BTDXImpl::createIndex()
{
    is_->seekg(0);
    HandlerIndexCreator handler(index_);
    SAXParser::parse(*is_, handler);
}


void SpectrumList_BTDXImpl::createMaps()
{
    vector<SpectrumIdentity>::const_iterator it=index_.begin();
    for (unsigned int i=0; i!=index_.size(); ++i, ++it)
        idToIndex_[it->id] = i;
}


} // namespace


SpectrumListPtr SpectrumList_BTDX::create(boost::shared_ptr<std::istream> is,
                                          const MSData& msd)
{
    return SpectrumListPtr(new SpectrumList_BTDXImpl(is, msd));
}


} // namespace msdata
} // namespace pwiz
