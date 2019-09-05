//
// $Id$
//
//
// Original author: Jennifer Leclaire <leclaire@uni-mainz.de>
//
// Copyright 2019 Institute of Computer Science, Johannes Gutenberg-Universität Mainz
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
//limitations under the License.
//
#include "ReferenceRead_triMS5.hpp"
#include "../References.hpp"


#include <iostream>

namespace pwiz {
namespace msdata {
namespace triMS5 {

	ReferenceRead_triMS5::ReferenceRead_triMS5(pwiz::msdata::MSData& msd) : ref_mz5_(mz5::ReferenceRead_mz5(msd)), numberOfPresetScanConfigurations_(-1) {};



void ReferenceRead_triMS5::setCVRefMZ5(CVRefMZ5* cvs, size_t s)
{
    for (size_t i = 0; i < s; ++i)
    {
		ref_mz5_.cvrefs_.push_back(cvs[i]);
    }
}


void ReferenceRead_triMS5::fill(boost::shared_ptr<Connection_triMS5>& connectionPtr)
{
	const DataSetIdPairToSizeMap& fields = connectionPtr.get()->getAvailableDataSets();
	numberOfPresetScanConfigurations_ = 0;
	/// TODO maybe read SpectrumList_Indices instead --> create set for presetindices 
	auto it = fields.begin();
	for (it; it != fields.end(); it++)
	{
		if (it->first.first == DataSetType_triMS5::SpectrumMetaData)
			numberOfPresetScanConfigurations_++;
	}

	size_t dsend;

	unsigned int preSetConfigIdx = 0; //all meta data sets here are not specific to a presetScanConfigurationIndex
	it = fields.find({ DataSetType_triMS5::ControlledVocabulary, preSetConfigIdx });


    if (it != fields.end())
    {
		ContVocabMZ5* cvl = (ContVocabMZ5*)connectionPtr.get()->readDataSet(DataSetType_triMS5::ControlledVocabulary, dsend, preSetConfigIdx);
		ref_mz5_.msd_.cvs.reserve(dsend);
		for (size_t i = 0; i < dsend; ++i)
		{
			ref_mz5_.msd_.cvs.push_back(cvl[i].getCV());
		}
		connectionPtr.get()->clean(DataSetType_triMS5::ControlledVocabulary, cvl, dsend);
    }
	it = fields.find({ DataSetType_triMS5::CVReference, preSetConfigIdx });
    if ( it != fields.end())
    {
		CVRefMZ5* cvrl = (CVRefMZ5*)connectionPtr.get()->readDataSet(DataSetType_triMS5::CVReference, dsend, preSetConfigIdx);
		setCVRefMZ5(cvrl, dsend);
		connectionPtr.get()->clean(DataSetType_triMS5::CVReference, cvrl, dsend);
    }

	it = fields.find({ DataSetType_triMS5::CVParam, preSetConfigIdx });
    if ( it != fields.end())
    {
		ref_mz5_.cvParams_.resize(it->second);
		Configuration_triMS5 c = connectionPtr->getConfiguration();
		connectionPtr.get()->readDataSet(DataSetType_triMS5::CVParam, dsend, preSetConfigIdx, ref_mz5_.cvParams_.data());
    }

	it = fields.find({ DataSetType_triMS5::UserParam, preSetConfigIdx });
    if ( it != fields.end())
    {
		ref_mz5_.usrParams_.resize(it->second);
		connectionPtr.get()->readDataSet(DataSetType_triMS5::UserParam, dsend, preSetConfigIdx, ref_mz5_.usrParams_.data());
    }

	it = fields.find({ DataSetType_triMS5::RefParam, preSetConfigIdx });
	if ( it != fields.end())
    {
		ref_mz5_.refParms_.resize(it->second);
		connectionPtr.get()->readDataSet(DataSetType_triMS5::RefParam, dsend, preSetConfigIdx, ref_mz5_.refParms_.data());
    }

    if (fields.find({DataSetType_triMS5::ParamGroups, preSetConfigIdx }) != fields.end())
    {
		ParamGroupMZ5* pgrl = (ParamGroupMZ5*)connectionPtr.get()->readDataSet(DataSetType_triMS5::ParamGroups,dsend, preSetConfigIdx);
		for (size_t i = 0; i < dsend; ++i)
		{
			pwiz::msdata::ParamGroupPtr ptr(pgrl[i].getParamGroup(this->ref_mz5_));
			ref_mz5_.msd_.paramGroupPtrs.push_back(ptr);
			pwiz::msdata::References::resolve(*ptr, ref_mz5_.msd_);
		}
		connectionPtr.get()->clean(DataSetType_triMS5::ParamGroups, pgrl, dsend);
    }

    if (fields.find({DataSetType_triMS5::Contact, preSetConfigIdx }) != fields.end())
    {
		ParamListMZ5* cl = (ParamListMZ5*)connectionPtr.get()->readDataSet(DataSetType_triMS5::Contact, dsend, preSetConfigIdx);
		for (size_t i = 0; i < dsend; ++i)
		{
			pwiz::msdata::Contact c;
			cl[i].fillParamContainer(dynamic_cast<pwiz::msdata::ParamContainer&> (c), this->ref_mz5_);
			ref_mz5_.msd_.fileDescription.contacts.push_back(c);
		}
		connectionPtr.get()->clean(DataSetType_triMS5::Contact, cl, dsend);
    }

    if (fields.find({DataSetType_triMS5::FileContent, preSetConfigIdx}) != fields.end())
    {
		ParamListMZ5* cl = (ParamListMZ5*)connectionPtr.get()->readDataSet(DataSetType_triMS5::FileContent, dsend, preSetConfigIdx);
		cl[0].fillParamContainer(dynamic_cast<pwiz::msdata::ParamContainer&> (ref_mz5_.msd_.fileDescription.fileContent),this->ref_mz5_);
		connectionPtr.get()->clean(DataSetType_triMS5::FileContent, cl, dsend);
    }

	if (fields.find({ DataSetType_triMS5::SourceFiles, preSetConfigIdx }) != fields.end())
    {
		SourceFileMZ5* sl = (SourceFileMZ5*)connectionPtr.get()->readDataSet(DataSetType_triMS5::SourceFiles, dsend, preSetConfigIdx);
		for (size_t i = 0; i < dsend; ++i)
		{
			pwiz::msdata::SourceFilePtr ptr(sl[i].getSourceFile(this->ref_mz5_));
			ref_mz5_.msd_.fileDescription.sourceFilePtrs.push_back(ptr);
			pwiz::msdata::References::resolve(*ptr, ref_mz5_.msd_);
		}
		connectionPtr.get()->clean(DataSetType_triMS5::SourceFiles, sl, dsend);
    }

    if (fields.find({DataSetType_triMS5::Software, preSetConfigIdx }) != fields.end())
    {
		SoftwareMZ5* sl = (SoftwareMZ5*)connectionPtr.get()->readDataSet(DataSetType_triMS5::Software, dsend, preSetConfigIdx);
		for (size_t i = 0; i < dsend; ++i)
		{
			pwiz::msdata::SoftwarePtr ptr(sl[i].getSoftware(this->ref_mz5_));
			ref_mz5_.msd_.softwarePtrs.push_back(ptr);
			pwiz::msdata::References::resolve(*ptr, ref_mz5_.msd_);
		}
		connectionPtr.get()->clean(DataSetType_triMS5::Software, sl, dsend);
    }

    if (fields.find({DataSetType_triMS5::DataProcessing, preSetConfigIdx }) != fields.end())
    {
		DataProcessingMZ5* dpl = (DataProcessingMZ5*)connectionPtr.get()->readDataSet(DataSetType_triMS5::DataProcessing, dsend, preSetConfigIdx);
		for (size_t i = 0; i < dsend; ++i)
		{
			pwiz::msdata::DataProcessingPtr ptr(dpl[i].getDataProcessing(this->ref_mz5_));
			ref_mz5_.msd_.dataProcessingPtrs.push_back(ptr);
			pwiz::msdata::References::resolve(*ptr, ref_mz5_.msd_);
		}
		connectionPtr.get()->clean(DataSetType_triMS5::DataProcessing, dpl,	dsend);
    }

    if (fields.find({DataSetType_triMS5::Samples, preSetConfigIdx }) != fields.end())
    {
		SampleMZ5* sl = (SampleMZ5*)connectionPtr.get()->readDataSet(DataSetType_triMS5::Samples, dsend, preSetConfigIdx);
		for (size_t i = 0; i < dsend; ++i)
		{
			pwiz::msdata::SamplePtr ptr(sl[i].getSample(this->ref_mz5_));
			ref_mz5_.msd_.samplePtrs.push_back(ptr);
			pwiz::msdata::References::resolve(*ptr, ref_mz5_.msd_);
		}
		connectionPtr.get()->clean(DataSetType_triMS5::Samples, sl, dsend);
    }

    if (fields.find({DataSetType_triMS5::ScanSetting, preSetConfigIdx }) != fields.end())
    {
		ScanSettingMZ5* sl = (ScanSettingMZ5*)connectionPtr.get()->readDataSet(DataSetType_triMS5::ScanSetting, dsend, preSetConfigIdx);
		for (size_t i = 0; i < dsend; ++i)
		{
			pwiz::msdata::ScanSettingsPtr ptr(sl[i].getScanSetting(this->ref_mz5_));
			ref_mz5_.msd_.scanSettingsPtrs.push_back(ptr);
			pwiz::msdata::References::resolve(*ptr, ref_mz5_.msd_);
		}
		connectionPtr.get()->clean(DataSetType_triMS5::ScanSetting, sl, dsend);
    }

    if (fields.find({DataSetType_triMS5::InstrumentConfiguration, preSetConfigIdx }) != fields.end())
    {
		InstrumentConfigurationMZ5* il = (InstrumentConfigurationMZ5*)connectionPtr.get()->readDataSet(DataSetType_triMS5::InstrumentConfiguration, dsend, preSetConfigIdx);
		for (size_t i = 0; i < dsend; ++i)
		{
			pwiz::msdata::InstrumentConfigurationPtr ptr( il[i].getInstrumentConfiguration(this->ref_mz5_));
			ref_mz5_.msd_.instrumentConfigurationPtrs.push_back(ptr);
			pwiz::msdata::References::resolve(*ptr, ref_mz5_.msd_);
		}
		connectionPtr.get()->clean(DataSetType_triMS5::InstrumentConfiguration, il, dsend);
    }
	
    if (fields.find({DataSetType_triMS5::Run, preSetConfigIdx }) != fields.end())
    {
		RunMZ5* rl = (RunMZ5*)connectionPtr.get()->readDataSet(DataSetType_triMS5::Run, dsend, preSetConfigIdx);
		if (dsend > 0)
		{
			rl[0].addInformation(ref_mz5_.msd_.run, this->ref_mz5_);
			std::string fid(rl[0].fid);
			ref_mz5_.msd_.id = fid;
			std::string faccession(rl[0].facc);
			ref_mz5_.msd_.accession = faccession;

			ref_mz5_.defaultChromatogramDataProcessingRefID_	= rl[0].defaultChromatogramDataProcessingRefID.refID;
			ref_mz5_.defaultSpectrumDataProcessingRefID_ = rl[0].defaultSpectrumDataProcessingRefID.refID;
		}
		connectionPtr.get()->clean(DataSetType_triMS5::Run, rl, dsend);
    }
}

}
}
}
