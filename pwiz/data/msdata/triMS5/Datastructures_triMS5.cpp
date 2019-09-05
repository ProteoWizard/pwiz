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
#include "Datastructures_triMS5.hpp"

namespace pwiz {
namespace msdata {
namespace triMS5 {

using namespace H5;

FileInformation_triMS5::FileInformation_triMS5(const FileInformation_triMS5& fi)
{
	this->majorVersion = fi.majorVersion;
	this->minorVersion = fi.minorVersion;

}
FileInformation_triMS5::FileInformation_triMS5(const Configuration_triMS5&)
{
	this->majorVersion = Configuration_triMS5::triMS5_FILE_MAJOR_VERSION;
	this->minorVersion = Configuration_triMS5::triMS5_FILE_MINOR_VERSION;
}
FileInformation_triMS5& FileInformation_triMS5::operator=(const FileInformation_triMS5& fi)
{
	if (this != &fi)
	{
		this->majorVersion = fi.majorVersion;
		this->minorVersion = fi.minorVersion;
	}
	return *this;
}
H5::CompType FileInformation_triMS5::getType()
{
	CompType ret(sizeof(FileInformation_triMS5_Data));
	ret.insertMember("majorVersion", HOFFSET(FileInformationMZ5Data, majorVersion), PredType::NATIVE_USHORT);
	ret.insertMember("minorVersion", HOFFSET(FileInformationMZ5Data, minorVersion), PredType::NATIVE_USHORT);
	return ret;
}
H5::CompType pwiz::msdata::triMS5::SpectrumListIndices_triMS5::getType()
{
	CompType ret(sizeof(SpectrumListIndices_triMS5_Data));
	ret.insertMember("presetScanConfigurationIndex", HOFFSET(SpectrumListIndices_triMS5_Data, presetScanConfigurationIndex), PredType::NATIVE_UINT);
	ret.insertMember("spectrumIndex", HOFFSET(SpectrumListIndices_triMS5_Data, localSpectrumIndex), PredType::NATIVE_ULONG);
	return ret;
}
}
}
}
