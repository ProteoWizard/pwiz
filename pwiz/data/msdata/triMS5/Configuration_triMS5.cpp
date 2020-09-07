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
//

#include "Configuration_triMS5.hpp"
#include "Datastructures_triMS5.hpp"
namespace pwiz {
namespace msdata {
namespace triMS5 {

using namespace H5;

unsigned short Configuration_triMS5::triMS5_FILE_MAJOR_VERSION = 0;
unsigned short Configuration_triMS5::triMS5_FILE_MINOR_VERSION = 1;

Configuration_triMS5::Configuration_triMS5() : config_mz5_(Configuration_mz5())
{
	init();
}

Configuration_triMS5::Configuration_triMS5(const Configuration_triMS5& config) : config_mz5_(config.config_mz5_)
{
	init();
}

Configuration_triMS5::Configuration_triMS5(const pwiz::msdata::MSDataFile::WriteConfig& config) : config_mz5_(Configuration_mz5(config)) 
{
	init();
}

Configuration_triMS5& Configuration_triMS5::operator=(const Configuration_triMS5& rhs)
{
	if (this != &rhs)
	{
		this->config_mz5_ = rhs.config_mz5_;
		init();
	}
	return *this;
}

void Configuration_triMS5::init()
{
	
	config_mz5_.setTranslating(false);
	//meta data (mz5)
	variableNames_.insert({ DataSetType_triMS5::ControlledVocabulary, config_mz5_.getNameFor(Configuration_mz5::ControlledVocabulary) });
	variableNames_.insert({DataSetType_triMS5::FileContent, config_mz5_.getNameFor(Configuration_mz5::FileContent) });
	variableNames_.insert({DataSetType_triMS5::Contact,config_mz5_.getNameFor(Configuration_mz5::Contact) });
	variableNames_.insert({DataSetType_triMS5::CVReference,config_mz5_.getNameFor(Configuration_mz5::CVReference) });
	variableNames_.insert({DataSetType_triMS5::CVParam,config_mz5_.getNameFor(Configuration_mz5::CVParam) });
	variableNames_.insert({DataSetType_triMS5::UserParam, config_mz5_.getNameFor(Configuration_mz5::UserParam) });
	variableNames_.insert({DataSetType_triMS5::RefParam, config_mz5_.getNameFor(Configuration_mz5::RefParam) });
	variableNames_.insert({DataSetType_triMS5::ParamGroups, config_mz5_.getNameFor(Configuration_mz5::ParamGroups) });
	variableNames_.insert({DataSetType_triMS5::SourceFiles, config_mz5_.getNameFor(Configuration_mz5::SourceFiles) });
	variableNames_.insert({DataSetType_triMS5::Samples, config_mz5_.getNameFor(Configuration_mz5::Samples) });
	variableNames_.insert({DataSetType_triMS5::Software, config_mz5_.getNameFor(Configuration_mz5::Software) });
	variableNames_.insert({DataSetType_triMS5::ScanSetting, config_mz5_.getNameFor(Configuration_mz5::ScanSetting) });
	variableNames_.insert({DataSetType_triMS5::InstrumentConfiguration, config_mz5_.getNameFor(Configuration_mz5::InstrumentConfiguration) });
	variableNames_.insert({DataSetType_triMS5::DataProcessing, config_mz5_.getNameFor(Configuration_mz5::DataProcessing) });
	variableNames_.insert({DataSetType_triMS5::Run, config_mz5_.getNameFor(Configuration_mz5::Run) });
	variableNames_.insert({DataSetType_triMS5::FileInformation,config_mz5_.getNameFor(Configuration_mz5::FileInformation) });

	//mz5
	variableNames_.insert({DataSetType_triMS5::ChromatogramTime, "ChromatogramTime" }); //mz5 has a spelling error ChomatogramTime
	
	variableNames_.insert({DataSetType_triMS5::ChromatogramIntensity, config_mz5_.getNameFor(Configuration_mz5::ChromatogramIntensity)});
	variableNames_.insert({DataSetType_triMS5::ChromatogramMetaData, config_mz5_.getNameFor(Configuration_mz5::ChromatogramMetaData)});
	variableNames_.insert({DataSetType_triMS5::ChromatogramBinaryMetaData, config_mz5_.getNameFor(Configuration_mz5::ChromatogramBinaryMetaData)});
	variableNames_.insert({DataSetType_triMS5::ChromatogramIndex, config_mz5_.getNameFor(Configuration_mz5::ChromatogramIndex)});
	
	variableNames_.insert({DataSetType_triMS5::SpectrumMetaData, config_mz5_.getNameFor(Configuration_mz5::SpectrumMetaData) });
	variableNames_.insert({DataSetType_triMS5::SpectrumBinaryMetaData, config_mz5_.getNameFor(Configuration_mz5::SpectrumBinaryMetaData) });
	variableNames_.insert({DataSetType_triMS5::SpectrumIndex, config_mz5_.getNameFor(Configuration_mz5::SpectrumIndex) });
	
	variableNames_.insert({DataSetType_triMS5::SpectrumIntensity, config_mz5_.getNameFor(Configuration_mz5::SpectrumIntensity) });

	//triMS5
	variableNames_.insert({DataSetType_triMS5::SpectrumMassAxis, "SpectrumMassAxis" });
	variableNames_.insert({DataSetType_triMS5::SpectrumMassIndices, "SpectrumMassIndices" });
	variableNames_.insert({ DataSetType_triMS5::SpectrumListIndices, "SpectrumListIndices" });
	

	//set mz5 datatypes
	for (std::map<DataSetType_triMS5, std::string>::iterator it = variableNames_.begin(); it != variableNames_.end(); ++it)
	{
		switch ((*it).first)
		{
		case DataSetType_triMS5::SpectrumMassAxis:
				variableTypes_.insert({DataSetType_triMS5::SpectrumMassAxis, config_mz5_.getDataTypeFor(Configuration_mz5::SpectrumMZ) }); // take precision from mz5
				variableChunkSizes_.insert({DataSetType_triMS5::SpectrumMassAxis, Configuration_mz5::EMPTY_CHUNK_SIZE }); //no chunking necessary mz Axis shouldn't need compression
				variableBufferSizes_.insert({DataSetType_triMS5::SpectrumMassAxis, Configuration_mz5::NO_BUFFER_SIZE }); // no buffer size necesssary
				break;
		case DataSetType_triMS5::SpectrumMassIndices:
				variableTypes_.insert({DataSetType_triMS5::SpectrumMassIndices, PredType::NATIVE_ULONG });
				variableChunkSizes_.insert({DataSetType_triMS5::SpectrumMassIndices, config_mz5_.getChunkSizeFor(Configuration_mz5::SpectrumMZ) });
				variableBufferSizes_.insert({DataSetType_triMS5::SpectrumMassIndices, config_mz5_.getBufferSizeFor(Configuration_mz5::SpectrumMZ) });
			break;
		case DataSetType_triMS5::FileInformation:
				variableTypes_.insert({ DataSetType_triMS5::FileInformation,FileInformation_triMS5::getType()}); // get compound data type
				variableChunkSizes_.insert({DataSetType_triMS5::FileInformation, Configuration_mz5::EMPTY_CHUNK_SIZE }); 
				variableBufferSizes_.insert({DataSetType_triMS5::FileInformation, Configuration_mz5::NO_BUFFER_SIZE }); // no buffer size necesssary
			break;
		case DataSetType_triMS5::SpectrumListIndices:
			variableTypes_.insert({ DataSetType_triMS5::SpectrumListIndices, SpectrumListIndices_triMS5::getType() }); // get compound data type
			variableChunkSizes_.insert({ DataSetType_triMS5::SpectrumListIndices, Configuration_mz5::EMPTY_CHUNK_SIZE });
			variableBufferSizes_.insert({ DataSetType_triMS5::SpectrumListIndices, Configuration_mz5::NO_BUFFER_SIZE }); // no buffer size necesssary
			break;

		case DataSetType_triMS5::ChromatogramTime: //mz5 has a spelling error "ChomatogramTime"
			variableTypes_.insert({ DataSetType_triMS5::ChromatogramTime, config_mz5_.getDataTypeFor(Configuration_mz5::ChomatogramTime)}); // get compound data type
			variableChunkSizes_.insert({ DataSetType_triMS5::ChromatogramTime, config_mz5_.getChunkSizeFor(Configuration_mz5::ChomatogramTime) });
			variableBufferSizes_.insert({ DataSetType_triMS5::ChromatogramTime, config_mz5_.getBufferSizeFor(Configuration_mz5::ChomatogramTime) }); // no buffer size necesssary
			break;
		default:
			////all other data sets are mz5 datasets
			variableTypes_.insert({it->first, config_mz5_.getDataTypeFor(config_mz5_.getVariableFor(it->second)) });
			variableChunkSizes_.insert({it->first, config_mz5_.getChunkSizeFor(config_mz5_.getVariableFor(it->second)) });
			variableBufferSizes_.insert({it->first, config_mz5_.getBufferSizeFor(config_mz5_.getVariableFor(it->second)) });
		}
		
	}

	//map the groups
	groupsNames_.insert({GroupType_triMS5::Root, "/" });
	groupsNames_.insert({GroupType_triMS5::MetaData, "MetaData" });
	groupsNames_.insert({GroupType_triMS5::RawData, "RawData" });
	groupsNames_.insert({GroupType_triMS5::Cluster, "Cluster" });
	groupsNames_.insert({GroupType_triMS5::Spectrum, "Spectrum" });
	groupsNames_.insert({GroupType_triMS5::Chromatogram, "Chromatogram" });

	groupsToGroups_.insert({GroupType_triMS5::Root, GroupType_triMS5::Root });
	groupsToGroups_.insert({GroupType_triMS5::MetaData, GroupType_triMS5::Root });
	groupsToGroups_.insert({GroupType_triMS5::RawData, GroupType_triMS5::Root });
	groupsToGroups_.insert({GroupType_triMS5::Cluster, GroupType_triMS5::RawData });
	groupsToGroups_.insert({GroupType_triMS5::Spectrum, GroupType_triMS5::Cluster });
	groupsToGroups_.insert({GroupType_triMS5::Chromatogram, GroupType_triMS5::Cluster });


	//map the datasets to the groups
	//Root Group
	variableToGroup_.insert({DataSetType_triMS5::FileInformation, GroupType_triMS5::Root });

	variableToGroup_.insert({ DataSetType_triMS5::SpectrumListIndices, GroupType_triMS5::RawData });


	// data sets belonging to metaData group
	variableToGroup_.insert({DataSetType_triMS5::ControlledVocabulary, GroupType_triMS5::MetaData });
	variableToGroup_.insert({DataSetType_triMS5::FileContent, GroupType_triMS5::MetaData });
	variableToGroup_.insert({DataSetType_triMS5::Contact, GroupType_triMS5::MetaData });
	variableToGroup_.insert({DataSetType_triMS5::CVReference, GroupType_triMS5::MetaData });
	variableToGroup_.insert({DataSetType_triMS5::CVParam, GroupType_triMS5::MetaData });
	variableToGroup_.insert({DataSetType_triMS5::UserParam, GroupType_triMS5::MetaData });
	variableToGroup_.insert({DataSetType_triMS5::RefParam, GroupType_triMS5::MetaData });
	variableToGroup_.insert({DataSetType_triMS5::ParamGroups, GroupType_triMS5::MetaData });
	variableToGroup_.insert({DataSetType_triMS5::SourceFiles, GroupType_triMS5::MetaData });
	variableToGroup_.insert({DataSetType_triMS5::Samples, GroupType_triMS5::MetaData });
	variableToGroup_.insert({DataSetType_triMS5::Software, GroupType_triMS5::MetaData });
	variableToGroup_.insert({DataSetType_triMS5::ScanSetting, GroupType_triMS5::MetaData });
	variableToGroup_.insert({DataSetType_triMS5::InstrumentConfiguration, GroupType_triMS5::MetaData });
	variableToGroup_.insert({DataSetType_triMS5::DataProcessing, GroupType_triMS5::MetaData });
	variableToGroup_.insert({DataSetType_triMS5::Run, GroupType_triMS5::MetaData });

	// data sets belonging to cluster group

	variableToGroup_.insert({DataSetType_triMS5::SpectrumIndex, GroupType_triMS5::Spectrum });
	variableToGroup_.insert({DataSetType_triMS5::SpectrumIntensity, GroupType_triMS5::Spectrum });
	variableToGroup_.insert({DataSetType_triMS5::SpectrumMassAxis, GroupType_triMS5::Spectrum });
	variableToGroup_.insert({DataSetType_triMS5::SpectrumMassIndices, GroupType_triMS5::Spectrum });
	variableToGroup_.insert({ DataSetType_triMS5::SpectrumMetaData, GroupType_triMS5::Spectrum });
	variableToGroup_.insert({ DataSetType_triMS5::SpectrumBinaryMetaData, GroupType_triMS5::Spectrum });

	
	variableToGroup_.insert({DataSetType_triMS5::ChromatogramMetaData, GroupType_triMS5::Chromatogram});
	variableToGroup_.insert({DataSetType_triMS5::ChromatogramBinaryMetaData, GroupType_triMS5::Chromatogram});
	variableToGroup_.insert({DataSetType_triMS5::ChromatogramIndex, GroupType_triMS5::Chromatogram});
	variableToGroup_.insert({DataSetType_triMS5::ChromatogramTime, GroupType_triMS5::Chromatogram });
	variableToGroup_.insert({DataSetType_triMS5::ChromatogramIntensity, GroupType_triMS5::Chromatogram});


	//inverse mapping from name to dataset
	for (const auto& it : variableNames_)
	{
		namesVariable_.insert({ it.second, it.first });
	}
	
	//inverse mapping from name to group
	for (const auto& it : groupsNames_)
	{
		namesGroups_.insert({it.second, it.first});
	}
}


const hsize_t Configuration_triMS5::getChunkSizeFor(const DataSetType_triMS5 v)
{
	auto it = variableChunkSizes_.find(v);

	if (it != variableChunkSizes_.end())
	{
		return it->second;
	}
	return Configuration_mz5::EMPTY_CHUNK_SIZE;
}

const hsize_t Configuration_triMS5::getBufferSizeFor(const DataSetType_triMS5 v)
{
	auto it = variableBufferSizes_.find(v);
	if (it != variableBufferSizes_.end())
	{
		return it->second;
	}
	return Configuration_mz5::NO_BUFFER_SIZE;
}

const std::string& Configuration_triMS5::getNameFor(const DataSetType_triMS5 v)
{
	auto it = variableNames_.find(v);
	if (it != variableNames_.end())
	{
		return it->second;
	}
    throw std::out_of_range("[Configurator_triMS5::getNameFor]: out of range");
}

const std::string& Configuration_triMS5::getNameFor(const GroupType_triMS5 v)
{
	auto it = groupsNames_.find(v);
	if (it != groupsNames_.end())
	{
		return it->second;
	}
	throw std::out_of_range("[Configurator_triMS5::getNameFor]: out of range");
}


const DataType& Configuration_triMS5::getDataTypeFor(const DataSetType_triMS5 v)
{
	auto it = variableTypes_.find(v);
	if (it != variableTypes_.end())
	{
		return it->second;
	}
    throw std::out_of_range("[Configurator_triMS5::getDataTypeFor]: out of range");
}


GroupType_triMS5 Configuration_triMS5::getGroupTypeFor(const DataSetType_triMS5 v)
{
	auto it = variableToGroup_.find(v);
	if (it != variableToGroup_.end())
	{
		return it->second;
	}
	throw std::out_of_range("[Configurator_triMS5::getGroupTypeFor]: out of range");
}

GroupType_triMS5 Configuration_triMS5::getGroupTypeFor(const GroupType_triMS5 v)
{
	auto it = groupsToGroups_.find(v);
	if (it != groupsToGroups_.end())
		return it->second;

	throw std::out_of_range("[Configurator_triMS5::getGroupTypeFor]: out of range");
}

DataSetType_triMS5 Configuration_triMS5::getDataSetTypeFor(const std::string& name)
{
	auto it = namesVariable_.find(name);
	if (it != namesVariable_.end())
		return it->second;

	throw std::out_of_range("[Configurator_triMS5::getDataSetTypeFor]: out of range");
}

GroupType_triMS5 Configuration_triMS5::getGroupTypeFor(const std::string & name)
{
	auto it = namesGroups_.find(name);
	if (it != namesGroups_.end())
		return it->second;

	throw std::out_of_range("[Configurator_triMS5::getDataSetTypeFor]: out of range");
}
}
}
}
