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
#ifndef _ReferenceRead_triMS5_HPP_
#define _ReferenceRead_triMS5_HPP_



#include "../mz5/ReferenceRead_mz5.hpp"
#include "Connection_triMS5.hpp"

namespace pwiz {
	namespace msdata {
		namespace triMS5 {

			/**
			 * This class is a helper class to read and convert a triMS5 file to a MSData object.
			 */
			class ReferenceRead_triMS5 final
			{
			public:

				/**
				 * Default constructor.
				 * @param msd this MSData object will be filled by calling ReferenceRead_mz5 constructor
				 */
				ReferenceRead_triMS5(pwiz::msdata::MSData& msd);

				/**
				 * Getter.
				 * @param index
				 * @return object with this index. Can throw out_of_range.
				 */
				pwiz::cv::CVID getCVID(const unsigned long index) const { return ref_mz5_.getCVID(index); }

				/**
				 * Getter
				 * @param index
				 * @return object with this index. Can throw out_of_range.
				 */
				pwiz::data::ParamGroupPtr getParamGroupPtr(const unsigned long index) const { return ref_mz5_.getParamGroupPtr(index); }

				
				/**
				* Parsing each single param. Calls mz5 method for parsing
				* @param cv container for cv params
				* @param user user params
				* @param param pointer to param group
				* @param cvstart start in container
				* @param cvend end index in container
				* @param usrstart start index in usr containter
				* @param usrend  end index in usr container
				* @param refstart start index for reference
				* @param refend end index for reference
				*/
				void fillParams(std::vector<pwiz::msdata::CVParam>& cv, std::vector<pwiz::msdata::UserParam>& user,
					std::vector<pwiz::msdata::ParamGroupPtr>& param, const unsigned long& cvstart,
					const unsigned long& cvend, const unsigned long& usrstart, const unsigned long& usrend,
					const unsigned long& refstart, const unsigned long& refend) const
				{
					ref_mz5_.fill(cv, user, param, cvstart, cvend, usrstart, usrend, refstart, refend);
				}

				/**
				 * Getter
				 * @param index
				 * @return object with this index. Can throw out_of_range.
				 */
				pwiz::msdata::SourceFilePtr getSourceFilePtr(const unsigned long index) const { return ref_mz5_.getSourcefilePtr(index); }

				/**
				 * Getter
				 * @param index
				 * @return object with this index. Can throw out_of_range.
				 */
				pwiz::msdata::SamplePtr getSamplePtr(const unsigned long index) const { return ref_mz5_.getSamplePtr(index); }

				/**
				 * Getter
				 * @param index
				 * @return object with this index. Can throw out_of_range.
				 */
				pwiz::msdata::SoftwarePtr getSoftwarePtr(const unsigned long index) const { return ref_mz5_.getSoftwarePtr(index); }

				/**
				 * Getter
				 * @param index
				 * @return object with this index. Can throw out_of_range.
				 */
				pwiz::msdata::ScanSettingsPtr getScanSettingPtr(const unsigned long index) const { return ref_mz5_.getScanSettingPtr(index); }

				/**
				 * Getter
				 * @param index
				 * @return object with this index. Can throw out_of_range.
				 */
				pwiz::msdata::InstrumentConfigurationPtr getInstrumentPtr(const unsigned long index) const { return ref_mz5_.getInstrumentPtr(index); }

				/**
				 * Getter
				 * @param index
				 * @return object with this index. Can throw out_of_range.
				 */
				pwiz::msdata::DataProcessingPtr getDataProcessingPtr(const unsigned long index) const { return ref_mz5_.getDataProcessingPtr(index); }
			
				/**
				 * Sets internal controlled vocabulary map.
				 */
				void setCVRefMZ5(CVRefMZ5*, size_t);

				/**
				 * Fills the internal MSData reference with the data from an triMS5 file.
				 * @param connection open triMS5 connection to a triMS5 file
				 */
				void fill(boost::shared_ptr<Connection_triMS5>& connection);

				/**
				* Gets the default chromatogram data processing pointer by calling mz5's method.
				* @param index
				*/
				pwiz::msdata::DataProcessingPtr getDefaultChromatogramDP(const size_t index) { return ref_mz5_.getDefaultChromatogramDP(index); }


				/**
				* Gets the default spectrum data processing pointer by calling mz5's method.
				* @param index
				*/
				pwiz::msdata::DataProcessingPtr getDefaultSpectrumDP(const size_t index) { return ref_mz5_.getDefaultSpectrumDP(index); }

				/**
				* Getter.
				*/
				int getNumberOfPresetScanConfigurations()const { return numberOfPresetScanConfigurations_; }

				/**
				* Gets the mz5 reference read object required for reading meta data.
				* @return pointer to reference_read mz5 object
				*/
				const mz5::ReferenceRead_mz5* getRefMZ5() const { return &ref_mz5_; }
			private:

				/**
				 * Reference to ReferenceRead_mz5 object.
				 */
				mz5::ReferenceRead_mz5 ref_mz5_;

				/**
				* Number of preset scan configurations (clusters)
				*/
				int numberOfPresetScanConfigurations_;

			};
		}
	}
}
#endif  /* _ReferenceRead_triMS5_HPP_ */
