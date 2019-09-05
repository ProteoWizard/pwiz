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
#ifndef _ReferenceWrite_triMS5_HPP_
#define _ReferenceWrite_triMS5_HPP_

#include "../../common/cv.hpp"
#include "../../common/ParamTypes.hpp"
#include "../MSData.hpp"
#include "pwiz/utility/misc/IterationListener.hpp"

#include "Connection_triMS5.hpp"

#include "../mz5/ReferenceWrite_mz5.hpp"

#include <string>
#include <vector>
#include <map>


namespace pwiz {
namespace msdata {
namespace triMS5 {

/**
 * This class is a helper class for converting and writing mz5 files in mz5 format.
 */
class ReferenceWrite_triMS5 final
{
public:
    /**
     * Default constructor. Calls constructor of ReferenceWrite_mz5
     * @param msd MSData input object
     */
	ReferenceWrite_triMS5(const pwiz::msdata::MSData& msd) : ref_mz5_(mz5::ReferenceWrite_mz5(msd)), numberOfPresetScanConfigurations_(-1), presetScanConfigurationIndices_(){}

  
     /**
     * Main method to write a triMS5 file.
     */
    void writeTo(Connection_triMS5 & connection, const pwiz::util::IterationListenerRegistry* iterationListenerRegistry);


	/**
	* Reads and writes all chromtograms using an existing mz5 connection.
	* @param connection mz5 connection object
	* @param bdl binary data list of a MSData object
	* @param cpl chromatogram list of a MSData object
	*/
	pwiz::util::IterationListener::Status
		readAndWriteChromatograms(
			Connection_triMS5& connection,
			std::vector<mz5::BinaryDataMZ5>& bdl,
			std::vector<mz5::ChromatogramMZ5>& cpl,
			const pwiz::util::IterationListenerRegistry* iterationListenerRegistry);

private:
	
	mz5::ReferenceWrite_mz5 ref_mz5_;

	/**
	* Number of preset scan configurations (clusters)
	*/
	int numberOfPresetScanConfigurations_;

	/**
	* Set of used preset scan configurations (clusters) indizes
	*/
	std::set<unsigned int> presetScanConfigurationIndices_;

	/** 
	* Detects  and sets the number of used presetScanConfigurationIndices
	* if no such terms is found attached to the spectrum, by default there will only be one presetScanConfiguration
	*/
	int init(std::set<unsigned int> & presetScanConfigurationIndizes_, std::vector<std::pair<unsigned int, unsigned int>>& spectrumListIndices);
	
	/**
	* Reads and writes all raw spectra using an existing triMS5 connection.
	* @param connection mz5 connection object
	* @param bdl binary data list of a MSData object
	* @param spl spetrum data list of a MSData object
	*/
	pwiz::util::IterationListener::Status
		readAndWriteSpectra(
			Connection_triMS5& connection,
			std::vector<mz5::BinaryDataMZ5>& bdl,
			std::vector<mz5::SpectrumMZ5>& spl, unsigned int presetScanConfiguration,
			const pwiz::util::IterationListenerRegistry* iterationListenerRegistry);

};

}
}
}

#endif /* _ReferenceWrite_triMS5_HPP_ */
