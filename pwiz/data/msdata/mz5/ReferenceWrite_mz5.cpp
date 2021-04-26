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

#include "ReferenceWrite_mz5.hpp"
#include "Translator_mz5.hpp"
#include "Datastructures_mz5.hpp"
#include "../../common/cv.hpp"
#include <boost/lexical_cast.hpp>
#include "pwiz/data/msdata/SpectrumWorkerThreads.hpp"

namespace pwiz {
namespace msdata {
namespace mz5 {

ReferenceWrite_mz5::ReferenceWrite_mz5(const pwiz::msdata::MSData& msd) :
    msd_(msd)
{
    SourceFileMZ5::read(msd_.fileDescription.sourceFilePtrs, *this);
    //TODO add source file when creating mz5
    SampleMZ5::read(msd_.samplePtrs, *this);
    SoftwareMZ5::read(msd_.softwarePtrs, *this);
    DataProcessingMZ5::read(msd_.dataProcessingPtrs, *this);
    //TODO look for "conversion to mzML" which is added to dp and default do for chromatograms and spectra
    ScanSettingMZ5::read(msd_.scanSettingsPtrs, *this);
    InstrumentConfigurationMZ5::read(msd_.instrumentConfigurationPtrs, *this);

    if (msd_.cvs.size() > 0)
    {
        ContVocabMZ5::convert(contvacb_, msd_.cvs);
    }

    if (msd_.fileDescription.fileContent.cvParams.size() > 0
            || msd_.fileDescription.fileContent.userParams.size() > 0
            || msd_.fileDescription.fileContent.paramGroupPtrs.size() > 0)
    {
        std::vector<pwiz::msdata::FileContent> fcl;
        fcl.push_back(msd_.fileDescription.fileContent);
        ParamListMZ5::convert(fileContent_, fcl, *this);
    }

    if (msd_.fileDescription.contacts.size() > 0)
    {
        ParamListMZ5::convert(contacts_, msd_.fileDescription.contacts, *this);
    }

    rl_.push_back(RunMZ5(msd_.run, msd_.id, msd_.accession, *this));
}

unsigned long ReferenceWrite_mz5::getCVRefId(const pwiz::cv::CVID cvid) const
{
    std::map<pwiz::cv::CVID, unsigned long>::iterator it =
            cvToIndexMapping_.find(cvid);
    if (it != cvToIndexMapping_.end())
    {
        return it->second;
    }
    else
    {
        unsigned long ret = cvrefs_.size();
        cvrefs_.push_back(CVRefMZ5(cvid));
        cvToIndexMapping_.insert(std::pair<pwiz::cv::CVID, unsigned long>(cvid,
                ret));
        return ret;
    }
    return ULONG_MAX;
}

unsigned long ReferenceWrite_mz5::getParamGroupId(
        const pwiz::data::ParamGroup& pg, const ParamGroupMZ5* pg5) const
{
    std::string id(pg.id);
    if (paramGroupMapping_.find(id) != paramGroupMapping_.end())
    {
        unsigned long ret = paramGroupMapping_.find(id)->second;
        return ret;
    }
    else
    {
        unsigned long ret = paramGroupList_.size();
        paramGroupMapping_.insert(
                std::pair<std::string, unsigned long>(id, ret));
        if (!pg5)
        {
            paramGroupList_.push_back(ParamGroupMZ5(pg, *this));
        }
        else
        {
            paramGroupList_.push_back(*pg5);
        }
        return ret;
    }
    return ULONG_MAX;
}

void ReferenceWrite_mz5::getIndizes(unsigned long& cvstart,
        unsigned long& cvend, unsigned long& usrstart, unsigned long& usrend,
        unsigned long& refstart, unsigned long& refend, const std::vector<
                pwiz::msdata::CVParam>& cvs, const std::vector<
                pwiz::msdata::UserParam>& usrs, const std::vector<
                pwiz::msdata::ParamGroupPtr>& groups) const
{
    if (cvs.size() > 0)
    {
        cvstart = cvParams_.size();
        for (size_t i = 0; i < cvs.size(); ++i)
        {
            cvParams_.push_back(CVParamMZ5(cvs[i], *this));
        }
        cvend = cvParams_.size();
    }
    else
    {
        cvstart = 0;
        cvend = 0;
    }

    if (usrs.size() > 0)
    {
        usrstart = usrParams_.size();
        for (size_t i = 0; i < usrs.size(); ++i)
        {
            usrParams_.push_back(UserParamMZ5(usrs[i], *this));
        }
        usrend = usrParams_.size();
    }
    else
    {
        usrstart = 0;
        usrend = 0;
    }

    if (groups.size() > 0)
    {
        refstart = refParms_.size();
        for (size_t i = 0; i < groups.size(); ++i)
        {
            if (groups[i].get())
            {
                refParms_.push_back(RefMZ5(*groups[i].get(), *this));
            }
        }
        refend = refParms_.size();
    }
    else
    {
        refstart = 0;
        refend = 0;
    }
}

unsigned long ReferenceWrite_mz5::getSourceFileId(
        const pwiz::msdata::SourceFile& sourceFile, const SourceFileMZ5* sf5) const
{
    std::string id = sourceFile.id;
    if (sourceFileMapping_.find(id) != sourceFileMapping_.end())
    {
        unsigned long ret = sourceFileMapping_.find(id)->second;
        return ret;
    }
    else
    {
        unsigned long ret = sourceFileList_.size();
        sourceFileMapping_.insert(
                std::pair<std::string, unsigned long>(id, ret));
        if (!sf5)
        {
            sourceFileList_.push_back(SourceFileMZ5(sourceFile, *this));
        }
        else
        {
            sourceFileList_.push_back(*sf5);
        }
        return ret;
    }
    return ULONG_MAX;
}

unsigned long ReferenceWrite_mz5::getSampleId(
        const pwiz::msdata::Sample& sample, const SampleMZ5* s5) const
{
    std::string id = sample.id;
    if (sampleMapping_.find(id) != sampleMapping_.end())
    {
        unsigned long ret = sampleMapping_.find(id)->second;
        return ret;
    }
    else
    {
        unsigned long ret = sampleList_.size();
        sampleMapping_.insert(std::pair<std::string, unsigned long>(id, ret));
        if (!s5)
        {
            sampleList_.push_back(SampleMZ5(sample, *this));
        }
        else
        {
            sampleList_.push_back(*s5);
        }
        return ret;
    }
    return ULONG_MAX;
}

unsigned long ReferenceWrite_mz5::getSoftwareId(
        const pwiz::msdata::Software& software, const SoftwareMZ5* s5) const
{
    std::string id = software.id;
    if (softwareMapping_.find(id) != softwareMapping_.end())
    {
        unsigned long ret = softwareMapping_.find(id)->second;
        return ret;
    }
    else
    {
        unsigned long ret = softwareList_.size();
        softwareMapping_.insert(std::pair<std::string, unsigned long>(id, ret));
        if (!s5)
        {
            softwareList_.push_back(SoftwareMZ5(software, *this));
        }
        else
        {
            softwareList_.push_back(*s5);
        }
        return ret;
    }
    return ULONG_MAX;
}

unsigned long ReferenceWrite_mz5::getScanSettingId(
        const pwiz::msdata::ScanSettings& scanSetting,
        const ScanSettingMZ5* ss5) const
{
    std::string id = scanSetting.id;
    if (scanSettingMapping_.find(id) != scanSettingMapping_.end())
    {
        unsigned long ret = scanSettingMapping_.find(id)->second;
        return ret;
    }
    else
    {
        unsigned long ret = scanSettingList_.size();
        scanSettingMapping_.insert(std::pair<std::string, unsigned long>(id,
                ret));
        if (!ss5)
        {
            scanSettingList_.push_back(ScanSettingMZ5(scanSetting, *this));
        }
        else
        {
            scanSettingList_.push_back(*ss5);
        }
        return ret;
    }
    return ULONG_MAX;
}

unsigned long ReferenceWrite_mz5::getInstrumentId(
        const pwiz::msdata::InstrumentConfiguration& ic,
        const InstrumentConfigurationMZ5* ic5) const
{
    std::string id = ic.id;
    if (instrumentMapping_.find(id) != instrumentMapping_.end())
    {
        unsigned long ret = instrumentMapping_.find(id)->second;
        return ret;
    }
    else
    {
        unsigned long ret = instrumentList_.size();
        instrumentMapping_.insert(
                std::pair<std::string, unsigned long>(id, ret));
        if (!ic5)
        {
            instrumentList_.push_back(InstrumentConfigurationMZ5(ic, *this));
        }
        else
        {
            instrumentList_.push_back(*ic5);
        }
        return ret;
    }
    return ULONG_MAX;
}

unsigned long ReferenceWrite_mz5::getDataProcessingId(
        const pwiz::msdata::DataProcessing& dp, const DataProcessingMZ5* dp5) const
{
    std::string id = dp.id;
    if (dataProcessingMapping_.find(id) != dataProcessingMapping_.end())
    {
        unsigned long ret = dataProcessingMapping_.find(id)->second;
        return ret;
    }
    else
    {
        unsigned long ret = dataProcessingList_.size();
        dataProcessingMapping_.insert(std::pair<std::string, unsigned long>(id,
                ret));
        if (!dp5)
        {
            dataProcessingList_.push_back(DataProcessingMZ5(dp, *this));
        }
        else
        {
            dataProcessingList_.push_back(*dp5);
        }
        return ret;
    }
    return ULONG_MAX;
}

void ReferenceWrite_mz5::addSpectrumIndexPair(const std::string& id,
        const unsigned long index) const
{
    spectrumMapping_.insert(std::pair<std::string, unsigned long>(id, index));
}

unsigned long ReferenceWrite_mz5::getSpectrumIndex(const std::string& id) const
{
    //TODO save it?
    if (spectrumMapping_.find(id) != spectrumMapping_.end())
    {
        return spectrumMapping_.find(id)->second;
    }
    return ULONG_MAX;
}

pwiz::util::IterationListener::Status ReferenceWrite_mz5::readAndWriteSpectra(
        Connection_mz5& connection, std::vector<BinaryDataMZ5>& bdl,
        std::vector<SpectrumMZ5>& spl,
        const pwiz::util::IterationListenerRegistry* iterationListenerRegistry,
        bool useWorkerThreads)
{
    pwiz::util::IterationListener::Status status =
            pwiz::util::IterationListener::Status_Ok;
    pwiz::msdata::SpectrumListPtr sl = msd_.run.spectrumListPtr;
    if (sl.get())
    {
        std::vector<unsigned long> sindex;
        sindex.reserve(sl->size());
        unsigned long accIndex = 0;

        pwiz::msdata::SpectrumPtr sp;
        pwiz::msdata::BinaryDataArrayPtr bdap;
        std::vector<double> mz;
        SpectrumWorkerThreads spectrumWorkers(*sl, useWorkerThreads);
        for (size_t i = 0; i < sl->size(); i++)
        {
            status = pwiz::util::IterationListener::Status_Ok;
            if (iterationListenerRegistry)
                status = iterationListenerRegistry->broadcastUpdateMessage(
                        pwiz::util::IterationListener::UpdateMessage(i, sl->size(), "writing spectra"));
            if (status == pwiz::util::IterationListener::Status_Cancel)
                break;

            //sp = sl->spectrum(i, true);
            sp = spectrumWorkers.processBatch(i);
            mz.clear();
            if (sp.get())
            {
                spl.push_back(SpectrumMZ5(*sp.get(), *this));
                if (sp->getMZArray().get() && sp->getIntensityArray().get())
                {
                    mz = sp->getMZArray().get()->data;
                    bdl.push_back(BinaryDataMZ5(*sp->getMZArray().get(),
                        *sp->getIntensityArray().get(), *this));
                    if (mz.size() > 0)
                    {
                        if (connection.getConfiguration().doTranslating())
                        {
                            Translator_mz5::translateMZ(mz);
                        }
                        accIndex += (unsigned long)mz.size();
                        connection.extendData(mz, Configuration_mz5::SpectrumMZ);
                        connection.extendData(sp->getIntensityArray().get()->data,
                                Configuration_mz5::SpectrumIntensity);
                    }
                } else {
                    bdl.push_back(BinaryDataMZ5());
                }
            }
            sindex.push_back(accIndex);
        }

        if (sindex.size() > 0)
            connection.createAndWrite1DDataSet(sindex.size(), &sindex[0],
                    Configuration_mz5::SpectrumIndex);
    }
    return status;
}

pwiz::util::IterationListener::Status ReferenceWrite_mz5::readAndWriteChromatograms(
        Connection_mz5& connection, std::vector<BinaryDataMZ5>& bdl,
        std::vector<ChromatogramMZ5>& cpl,
        const pwiz::util::IterationListenerRegistry* iterationListenerRegistry)
{
    pwiz::util::IterationListener::Status status =
            pwiz::util::IterationListener::Status_Ok;
    pwiz::msdata::ChromatogramListPtr cl = msd_.run.chromatogramListPtr;
    if (cl.get() && cl->size() > 0)
    {
        if (cl.get())
        {
            std::vector<unsigned long> cindex;
            cindex.reserve(cl->size());
            unsigned long accIndex = 0;

            pwiz::msdata::ChromatogramPtr cp;
            pwiz::msdata::BinaryDataArrayPtr bdap;
            std::vector<double> time, inten;
            for (size_t i = 0; i < cl->size(); i++)
            {
                status = pwiz::util::IterationListener::Status_Ok;
                if (iterationListenerRegistry)
                    status = iterationListenerRegistry->broadcastUpdateMessage(
                            pwiz::util::IterationListener::UpdateMessage(i, cl->size(), "writing chromatograms"));
                if (status == pwiz::util::IterationListener::Status_Cancel)
                    break;
                cp = cl->chromatogram(i, true);
                time.clear();
                inten.clear();
                if (cp.get())
                {
                    cpl.push_back(ChromatogramMZ5(*cp.get(), *this));
                    if (cp->getTimeArray().get() && cp->getIntensityArray().get())
                    {
                        time = cp->getTimeArray().get()->data;
                        inten = cp->getIntensityArray().get()->data;
                        bdl.push_back(BinaryDataMZ5(*cp->getTimeArray().get(),
                            *cp->getIntensityArray().get(), *this));
                        if (inten.size() > 0)
                        {
                            accIndex += (unsigned long)inten.size();
                            connection.extendData(time,
                                    Configuration_mz5::ChomatogramTime);
                            connection.extendData(inten,
                                    Configuration_mz5::ChromatogramIntensity);
                        }
                    } else {
                        bdl.push_back(BinaryDataMZ5());
                    }
                }
                cindex.push_back(accIndex);
            }

            if (cindex.size() > 0)
                connection.createAndWrite1DDataSet(cindex.size(), &cindex[0],
                        Configuration_mz5::ChromatogramIndex);
        }
    }
    return status;
}

void ReferenceWrite_mz5::writeTo(Connection_mz5& connection,
        const pwiz::util::IterationListenerRegistry* iterationListenerRegistry,
        bool useWorkerThreads)
{
    pwiz::util::IterationListener::Status status;

    std::vector<ChromatogramMZ5> cl;
    std::vector<BinaryDataMZ5> cbdl;
    status = readAndWriteChromatograms(connection, cbdl, cl,
            iterationListenerRegistry);
    if (cl.size() > 0)
        connection.createAndWrite1DDataSet(cl.size(), &cl[0],
                Configuration_mz5::ChromatogramMetaData);
    if (cbdl.size() > 0)
        connection.createAndWrite1DDataSet(cbdl.size(), &cbdl[0],
                Configuration_mz5::ChromatogramBinaryMetaData);
    cl.clear();
    cbdl.clear();

    if (status != pwiz::util::IterationListener::Status_Cancel)
    {
        std::vector<SpectrumMZ5> sl;
        std::vector<BinaryDataMZ5> sbdl;
        status = readAndWriteSpectra(connection, sbdl, sl,
                iterationListenerRegistry, useWorkerThreads);
        if (sl.size() > 0)
            connection.createAndWrite1DDataSet(sl.size(), &sl[0],
                    Configuration_mz5::SpectrumMetaData);
        if (sbdl.size() > 0)
            connection.createAndWrite1DDataSet(sbdl.size(), &sbdl[0],
                    Configuration_mz5::SpectrumBinaryMetaData);
        sl.clear();
        sbdl.clear();

        if (status != pwiz::util::IterationListener::Status_Cancel)
        {
            if (contvacb_.size() > 0)
                connection.createAndWrite1DDataSet(contvacb_.size(),
                        &contvacb_[0], Configuration_mz5::ControlledVocabulary);
            if (fileContent_.size() > 0)
                connection.createAndWrite1DDataSet(fileContent_.size(),
                        &fileContent_[0], Configuration_mz5::FileContent);
            if (contacts_.size() > 0)
                connection.createAndWrite1DDataSet(contacts_.size(),
                        &contacts_[0], Configuration_mz5::Contact);
            if (rl_.size() > 0)
                connection.createAndWrite1DDataSet(rl_.size(), &rl_[0],
                        Configuration_mz5::Run);
            if (sourceFileList_.size() > 0)
                connection.createAndWrite1DDataSet(sourceFileList_.size(),
                        &sourceFileList_[0], Configuration_mz5::SourceFiles);
            if (sampleList_.size() > 0)
                connection.createAndWrite1DDataSet(sampleList_.size(),
                        &sampleList_[0], Configuration_mz5::Samples);
            if (softwareList_.size() > 0)
                connection.createAndWrite1DDataSet(softwareList_.size(),
                        &softwareList_[0], Configuration_mz5::Software);
            if (scanSettingList_.size() > 0)
                connection.createAndWrite1DDataSet(scanSettingList_.size(),
                        &scanSettingList_[0], Configuration_mz5::ScanSetting);
            if (instrumentList_.size() > 0)
                connection.createAndWrite1DDataSet(instrumentList_.size(),
                        &instrumentList_[0],
                        Configuration_mz5::InstrumentConfiguration);
            if (dataProcessingList_.size() > 0)
                connection.createAndWrite1DDataSet(dataProcessingList_.size(),
                        &dataProcessingList_[0],
                        Configuration_mz5::DataProcessing);
            if (cvrefs_.size() > 0)
                connection.createAndWrite1DDataSet(cvrefs_.size(), &cvrefs_[0],
                        Configuration_mz5::CVReference);
            if (paramGroupList_.size() > 0)
                connection.createAndWrite1DDataSet(paramGroupList_.size(),
                        &paramGroupList_[0], Configuration_mz5::ParamGroups);
            if (cvParams_.size() > 0)
                connection.createAndWrite1DDataSet(cvParams_.size(),
                        &cvParams_[0], Configuration_mz5::CVParam);
            if (usrParams_.size() > 0)
                connection.createAndWrite1DDataSet(usrParams_.size(),
                        &usrParams_[0], Configuration_mz5::UserParam);
            if (refParms_.size() > 0)
                connection.createAndWrite1DDataSet(refParms_.size(),
                        &refParms_[0], Configuration_mz5::RefParam);

            std::vector<FileInformationMZ5> fi;
            fi.push_back(FileInformationMZ5(connection.getConfiguration()));
            if (fi.size() > 0)
                connection.createAndWrite1DDataSet(fi.size(), &fi[0],
                        Configuration_mz5::FileInformation);
        }
    }
}

}
}
}
