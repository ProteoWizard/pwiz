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

#include "ReferenceRead_mz5.hpp"
#include "../References.hpp"
#include "../ChromatogramList_mz5.hpp"
#include "../SpectrumList_mz5.hpp"

#include <iostream>

namespace pwiz {
namespace msdata {
namespace mz5 {

ReferenceRead_mz5::ReferenceRead_mz5(pwiz::msdata::MSData& msd) :
    msd_(msd)
{
}

pwiz::cv::CVID ReferenceRead_mz5::getCVID(const unsigned long index) const
{
    if (cvrefs_.size() > index)
    {
        std::map<unsigned long, pwiz::cv::CVID>::iterator it = bbmapping_.find(
                index);
        if (it != bbmapping_.end())
        {
            return it->second;
        }
        char id[16];
        size_t n = sprintf(id, "%s:%07lu", cvrefs_[index].prefix,
                cvrefs_[index].accession);
        id[n] = '\0';
        pwiz::cv::CVID c = pwiz::cv::cvTermInfo(id).cvid;
        //caching of previous results speeds up the requests
        bbmapping_.insert(std::pair<unsigned long, pwiz::cv::CVID>(index, c));
        return c;
    }
    return pwiz::cv::CVID_Unknown;
}

pwiz::data::ParamGroupPtr ReferenceRead_mz5::getParamGroupPtr(
        const unsigned long index) const
{
    if (msd_.paramGroupPtrs.size() > index)
    {
        return msd_.paramGroupPtrs[index];
    }
    else
    {
        throw std::out_of_range(
                "ReferenceRead_mz5::getParamGroupPtr: out of range");
    }
}

void ReferenceRead_mz5::fill(std::vector<pwiz::msdata::CVParam>& cv,
        std::vector<pwiz::msdata::UserParam>& user, std::vector<
                pwiz::msdata::ParamGroupPtr>& param,
        const unsigned long& cvstart, const unsigned long& cvend,
        const unsigned long& usrstart, const unsigned long& usrend,
        const unsigned long& refstart, const unsigned long& refend) const
{
    if (cvend - cvstart > 0)
    {
        if (cvParams_.size() >= cvend)
        {
            cv.clear();
            cv.resize(cvend - cvstart);
            for (unsigned long i = cvstart; i < cvend; ++i)
            {
                cvParams_[i].fill(cv[i - cvstart], *this);
            }
        }
        else
        {
            throw std::out_of_range("ParamListHelper: cvParam out of range");
        }
    }
    if (usrend - usrstart > 0)
    {
        if (usrParams_.size() >= usrend)
        {
            user.clear();
            user.reserve(usrend - usrstart);
            for (unsigned long i = usrstart; i < usrend; ++i)
            {
                user.push_back(usrParams_[i].getUserParam(*this));
            }
        }
        else
        {
            throw std::out_of_range("ParamListHelper: userParam out of range");
        }
    }
    if (refend - refstart > 0)
    {
        if (refParms_.size() >= refend)
        {
            param.clear();
            param.reserve(refend - refstart);
            for (unsigned long i = refstart; i < refend; ++i)
            {
                param.push_back(refParms_[i].getParamGroupPtr(*this));
            }
        }
        else
        {
            throw std::out_of_range("ParamListHelper: refParam out of range");
        }
    }
}

pwiz::msdata::SourceFilePtr ReferenceRead_mz5::getSourcefilePtr(
        const unsigned long index) const
{
    if (msd_.fileDescription.sourceFilePtrs.size() > index)
    {
        return msd_.fileDescription.sourceFilePtrs[index];
    }
    else
    {
        throw std::out_of_range(
                "ReferenceRead_mz5::getSourceFilePtr: out of range");
    }
}

pwiz::msdata::SamplePtr ReferenceRead_mz5::getSamplePtr(
        const unsigned long index) const
{
    if (msd_.samplePtrs.size() > index)
    {
        return msd_.samplePtrs[index];
    }
    else
    {
        throw std::out_of_range("ReferenceRead_mz5::getSamplePtr: out of range");
    }
}

pwiz::msdata::SoftwarePtr ReferenceRead_mz5::getSoftwarePtr(
        const unsigned long index) const
{
    if (msd_.softwarePtrs.size() > index)
    {
        return msd_.softwarePtrs[index];
    }
    else
    {
        throw std::out_of_range(
                "ReferenceRead_mz5::getSoftwarePtr: out of range");
    }
}

pwiz::msdata::ScanSettingsPtr ReferenceRead_mz5::getScanSettingPtr(
        const unsigned long index) const
{
    if (msd_.scanSettingsPtrs.size() > index)
    {
        return msd_.scanSettingsPtrs[index];
    }
    else
    {
        throw std::out_of_range(
                "ReferenceRead_mz5::getScanSettingPtr: out of range");
    }
}

pwiz::msdata::InstrumentConfigurationPtr ReferenceRead_mz5::getInstrumentPtr(
        const unsigned long index) const
{
    if (msd_.instrumentConfigurationPtrs.size() > index)
    {
        return msd_.instrumentConfigurationPtrs[index];
    }
    else
    {
        throw std::out_of_range(
                "ReferenceRead_mz5::getInstrumentPtr: out of range");
    }

}

pwiz::msdata::DataProcessingPtr ReferenceRead_mz5::getDataProcessingPtr(
        const unsigned long index) const
{
    if (msd_.dataProcessingPtrs.size() > index)
    {
        return msd_.dataProcessingPtrs[index];
    }
    else
    {
		return pwiz::msdata::DataProcessingPtr();
    }
}

std::string ReferenceRead_mz5::getSpectrumId(const unsigned long index) const
{
    std::map<unsigned long, std::string>::iterator it = spectrumIndex_.find(
            index);
    if (it != spectrumIndex_.end())
    {
        return it->second;
    }
    throw std::out_of_range("ReferenceRead_mz5::getSpectrumId(): out of range");
}

void ReferenceRead_mz5::addSpectrumIndexPair(const std::string& id,
        const unsigned long index) const
{
    spectrumIndex_.insert(std::pair<unsigned long, std::string>(index, id));
}

void ReferenceRead_mz5::setCVRefMZ5(CVRefMZ5* cvs, size_t s)
{
    for (size_t i = 0; i < s; ++i)
    {
        cvrefs_.push_back(cvs[i]);
    }
}

void ReferenceRead_mz5::fill(boost::shared_ptr<Connection_mz5>& connectionPtr)
{
    const std::map<Configuration_mz5::MZ5DataSets, size_t>& fields =
            connectionPtr.get()->getFields();
    std::map<Configuration_mz5::MZ5DataSets, size_t>::const_iterator it;

    size_t dsend;
    it = fields.find(Configuration_mz5::ControlledVocabulary);
    if (it != fields.end())
    {
        ContVocabMZ5* cvl = (ContVocabMZ5*) connectionPtr.get()->readDataSet(
                Configuration_mz5::ControlledVocabulary, dsend);
        msd_.cvs.reserve(dsend);
        for (size_t i = 0; i < dsend; ++i)
        {
            msd_.cvs.push_back(cvl[i].getCV());
        }
        connectionPtr.get()->clean(Configuration_mz5::ControlledVocabulary,
                cvl, dsend);
    }

    it = fields.find(Configuration_mz5::CVReference);
    if (it != fields.end())
    {
        CVRefMZ5* cvrl = (CVRefMZ5*) connectionPtr.get()->readDataSet(
                Configuration_mz5::CVReference, dsend);
        setCVRefMZ5(cvrl, dsend);
        connectionPtr.get()->clean(Configuration_mz5::CVReference, cvrl, dsend);
    }

    it = fields.find(Configuration_mz5::CVParam);
    if (it != fields.end())
    {
        cvParams_.resize(it->second);
        connectionPtr.get()->readDataSet(Configuration_mz5::CVParam, dsend,
                &cvParams_[0]);
    }

    it = fields.find(Configuration_mz5::UserParam);
    if (it != fields.end())
    {
        usrParams_.resize(it->second);
        connectionPtr.get()->readDataSet(Configuration_mz5::UserParam, dsend,
                &usrParams_[0]);
    }

    it = fields.find(Configuration_mz5::RefParam);
    if (it != fields.end())
    {
        refParms_.resize(it->second);
        connectionPtr.get()->readDataSet(Configuration_mz5::RefParam, dsend,
                &refParms_[0]);
    }

    if (fields.find(Configuration_mz5::ParamGroups) != fields.end())
    {
        ParamGroupMZ5* pgrl =
                (ParamGroupMZ5*) connectionPtr.get()->readDataSet(
                        Configuration_mz5::ParamGroups, dsend);
        for (size_t i = 0; i < dsend; ++i)
        {
            pwiz::msdata::ParamGroupPtr ptr(pgrl[i].getParamGroup(*this));
            msd_.paramGroupPtrs.push_back(ptr);
            pwiz::msdata::References::resolve(*ptr, msd_);
        }
        connectionPtr.get()->clean(Configuration_mz5::ParamGroups, pgrl, dsend);
    }

    if (fields.find(Configuration_mz5::Contact) != fields.end())
    {
        ParamListMZ5* cl = (ParamListMZ5*) connectionPtr.get()->readDataSet(
                Configuration_mz5::Contact, dsend);
        for (size_t i = 0; i < dsend; ++i)
        {
            pwiz::msdata::Contact c;
            cl[i].fillParamContainer(
                    dynamic_cast<pwiz::msdata::ParamContainer&> (c), *this);
            msd_.fileDescription.contacts.push_back(c);
        }
        connectionPtr.get()->clean(Configuration_mz5::Contact, cl, dsend);
    }

    if (fields.find(Configuration_mz5::Contact) != fields.end())
    {
        ParamListMZ5* cl = (ParamListMZ5*) connectionPtr.get()->readDataSet(
                Configuration_mz5::Contact, dsend);
        for (size_t i = 0; i < dsend; ++i)
        {
            pwiz::msdata::Contact c;
            cl[i].fillParamContainer(
                    dynamic_cast<pwiz::msdata::ParamContainer&> (c), *this);
            msd_.fileDescription.contacts.push_back(c);
        }
        connectionPtr.get()->clean(Configuration_mz5::Contact, cl, dsend);
    }

    if (fields.find(Configuration_mz5::FileContent) != fields.end())
    {
        ParamListMZ5* cl = (ParamListMZ5*) connectionPtr.get()->readDataSet(
                Configuration_mz5::FileContent, dsend);
        cl[0].fillParamContainer(
                dynamic_cast<pwiz::msdata::ParamContainer&> (msd_.fileDescription.fileContent),
                *this);
        connectionPtr.get()->clean(Configuration_mz5::FileContent, cl, dsend);
    }

    if (fields.find(Configuration_mz5::SourceFiles) != fields.end())
    {
        SourceFileMZ5* sl = (SourceFileMZ5*) connectionPtr.get()->readDataSet(
                Configuration_mz5::SourceFiles, dsend);
        for (size_t i = 0; i < dsend; ++i)
        {
            pwiz::msdata::SourceFilePtr ptr(sl[i].getSourceFile(*this));
            msd_.fileDescription.sourceFilePtrs.push_back(ptr);
            pwiz::msdata::References::resolve(*ptr, msd_);
        }
        connectionPtr.get()->clean(Configuration_mz5::SourceFiles, sl, dsend);
    }

    if (fields.find(Configuration_mz5::Software) != fields.end())
    {
        SoftwareMZ5* sl = (SoftwareMZ5*) connectionPtr.get()->readDataSet(
                Configuration_mz5::Software, dsend);
        for (size_t i = 0; i < dsend; ++i)
        {
            pwiz::msdata::SoftwarePtr ptr(sl[i].getSoftware(*this));
            msd_.softwarePtrs.push_back(ptr);
            pwiz::msdata::References::resolve(*ptr, msd_);
        }
        connectionPtr.get()->clean(Configuration_mz5::Software, sl, dsend);
    }

    if (fields.find(Configuration_mz5::DataProcessing) != fields.end())
    {
        DataProcessingMZ5* dpl =
                (DataProcessingMZ5*) connectionPtr.get()->readDataSet(
                        Configuration_mz5::DataProcessing, dsend);
        for (size_t i = 0; i < dsend; ++i)
        {
            pwiz::msdata::DataProcessingPtr
                    ptr(dpl[i].getDataProcessing(*this));
            msd_.dataProcessingPtrs.push_back(ptr);
            pwiz::msdata::References::resolve(*ptr, msd_);
        }
        connectionPtr.get()->clean(Configuration_mz5::DataProcessing, dpl,
                dsend);
    }

    if (fields.find(Configuration_mz5::Samples) != fields.end())
    {
        SampleMZ5* sl = (SampleMZ5*) connectionPtr.get()->readDataSet(
                Configuration_mz5::Samples, dsend);
        for (size_t i = 0; i < dsend; ++i)
        {
            pwiz::msdata::SamplePtr ptr(sl[i].getSample(*this));
            msd_.samplePtrs.push_back(ptr);
            pwiz::msdata::References::resolve(*ptr, msd_);
        }
        connectionPtr.get()->clean(Configuration_mz5::Samples, sl, dsend);
    }

    if (fields.find(Configuration_mz5::ScanSetting) != fields.end())
    {
        ScanSettingMZ5* sl =
                (ScanSettingMZ5*) connectionPtr.get()->readDataSet(
                        Configuration_mz5::ScanSetting, dsend);
        for (size_t i = 0; i < dsend; ++i)
        {
            pwiz::msdata::ScanSettingsPtr ptr(sl[i].getScanSetting(*this));
            msd_.scanSettingsPtrs.push_back(ptr);
            pwiz::msdata::References::resolve(*ptr, msd_);
        }
        connectionPtr.get()->clean(Configuration_mz5::ScanSetting, sl, dsend);
    }

    if (fields.find(Configuration_mz5::InstrumentConfiguration) != fields.end())
    {
        InstrumentConfigurationMZ5* il =
                (InstrumentConfigurationMZ5*) connectionPtr.get()->readDataSet(
                        Configuration_mz5::InstrumentConfiguration, dsend);
        for (size_t i = 0; i < dsend; ++i)
        {
            pwiz::msdata::InstrumentConfigurationPtr ptr(
                    il[i].getInstrumentConfiguration(*this));
            msd_.instrumentConfigurationPtrs.push_back(ptr);
            pwiz::msdata::References::resolve(*ptr, msd_);
        }
        connectionPtr.get()->clean(Configuration_mz5::InstrumentConfiguration,
                il, dsend);
    }

    if (fields.find(Configuration_mz5::Run) != fields.end())
    {
        RunMZ5* rl = (RunMZ5*) connectionPtr.get()->readDataSet(
                Configuration_mz5::Run, dsend);
        if (dsend > 0)
        {
            rl[0].addInformation(msd_.run, *this);
            std::string fid(rl[0].fid);
            msd_.id = fid;
            std::string faccession(rl[0].facc);
            msd_.accession = faccession;

            defaultChromatogramDataProcessingRefID_
                    = rl[0].defaultChromatogramDataProcessingRefID.refID;
            defaultSpectrumDataProcessingRefID_
                    = rl[0].defaultSpectrumDataProcessingRefID.refID;
        }
        connectionPtr.get()->clean(Configuration_mz5::Run, rl, dsend);
    }
}

pwiz::msdata::DataProcessingPtr ReferenceRead_mz5::getDefaultChromatogramDP(
        const size_t index)
{
    if (index == 0)
    {
        return getDataProcessingPtr(defaultChromatogramDataProcessingRefID_);
    }
    else
    {
        throw std::out_of_range(
                "ReferenceRead_mz5::getChromatogramSpectrumDP(): does not support multiple runs yet");
    }
}

pwiz::msdata::DataProcessingPtr ReferenceRead_mz5::getDefaultSpectrumDP(
        const size_t index)
{
    if (index == 0)
    {
        return getDataProcessingPtr(defaultSpectrumDataProcessingRefID_);
    }
    else
    {
        throw std::out_of_range(
                "ReferenceRead_mz5::getChromatogramSpectrumDP(): does not support multiple runs yet");
    }
}

}
}
}
