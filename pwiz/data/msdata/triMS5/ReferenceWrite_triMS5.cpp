//
// $Id$
//
//
// Original author: Jennifer Leclaire <leclaire@uni-mainz.de>
//
// Copyright 2018 Institute of Computer Science, Johannes Gutenberg-Universität Mainz
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

#include "ReferenceWrite_triMS5.hpp"
#include "Configuration_triMS5.hpp"
#include "SpectrumList_Filter_triMS5.hpp"
#include "../mz5/Datastructures_mz5.hpp"
#include "Datastructures_triMS5.hpp"
#include "../../common/cv.hpp"
#include <boost/lexical_cast.hpp>
#include "pwiz/data/msdata/SpectrumWorkerThreads.hpp"

#include<iostream>

namespace pwiz {
	namespace msdata {
		namespace triMS5 {


			using namespace pwiz::msdata::mz5;

			pwiz::util::IterationListener::Status ReferenceWrite_triMS5::readAndWriteSpectra(Connection_triMS5& connection, std::vector<BinaryDataMZ5>& bdl, std::vector<SpectrumMZ5>& spl, int presetScanConfiguration, const pwiz::util::IterationListenerRegistry* iterationListenerRegistry)
			{
				pwiz::util::IterationListener::Status status = pwiz::util::IterationListener::Status_Ok;

				pwiz::msdata::SpectrumListPtr sl_old = ref_mz5_.msd_.run.spectrumListPtr;

				//if there is more than one cluster - we perform the filterting step to achieve scan type specific spectrum lists
				boost::shared_ptr<SpectrumList_Filter_triMS5> sl_filtered = boost::make_shared<SpectrumList_Filter_triMS5>(sl_old, SpectrumList_FilterPredicate_ScanEventSet_triMS5(pwiz::util::IntegerSet(presetScanConfiguration)));

				pwiz::msdata::SpectrumListPtr sl = sl_filtered->empty() ? sl_old : boost::static_pointer_cast<pwiz::msdata::SpectrumList>(sl_filtered);


				SpectrumWorkerThreads spectrumWorkers(*sl, true);

				std::vector<unsigned long> sindex;
				sindex.reserve(sl->size());
				unsigned long accIndex = 0;

				pwiz::msdata::SpectrumPtr sp;
				pwiz::msdata::BinaryDataArrayPtr bdap;
				std::vector<double> mz;

				std::map<double, std::vector<unsigned long>> mz_unique;
				unsigned long datapoints = 0;

				std::set<double> rtAxis;

				///TODO
				std::set<double> dtAxis;


				for (size_t i = 0; i < sl->size(); i++)
				{
					status = pwiz::util::IterationListener::Status_Ok;
					if (iterationListenerRegistry)
						status = iterationListenerRegistry->broadcastUpdateMessage(pwiz::util::IterationListener::UpdateMessage(i, sl->size()));
					if (status == pwiz::util::IterationListener::Status_Cancel)
						break;

					sp = spectrumWorkers.processBatch(i);
					mz.clear();
					if (sp.get())
					{
				//		spl.push_back(SpectrumMZ5(*sp.get(), ref_mz5_));
						if (sp->getMZArray().get() && sp->getIntensityArray().get())
						{
							mz = sp->getMZArray().get()->data;
					//		bdl.push_back(BinaryDataMZ5(*sp->getMZArray().get(), *sp->getIntensityArray().get(), ref_mz5_));

							if (mz.size() > 0)
							{
								accIndex += static_cast<unsigned long>(mz.size());
								connection.extendData(sp->getIntensityArray().get()->data, DataSetType_triMS5::SpectrumIntensity, presetScanConfiguration);

								//add data to axis dictionary 
								for (size_t j = 0; j < mz.size(); j++)
								{
									mz_unique[mz[j]].push_back(datapoints);
									datapoints++;
								}
							}
						}

		/*				else
						{
							bdl.push_back(BinaryDataMZ5());
						}*/
						////get the chromatogram time:
						//for (auto e : sp->scanList.scans)
						//{
						//	//if we found the retention time
						//	if (e.hasCVParam(CVID::MS_scan_start_time))
						//	{
						//		double rt = e.cvParam(CVID::MS_scan_start_time).valueAs<double>();
						//		rtAxis.insert(rt);
						//	}
						//	///TODO: search for different dt bins

						//	if (e.hasCVParam(CVID::MS_ion_mobility_drift_time) || e.hasCVParam(CVID::MS_inverse_reduced_ion_mobility))
						//	{
						//		double dt = e.hasCVParam(CVID::MS_ion_mobility_drift_time) ? e.cvParam(CVID::MS_ion_mobility_drift_time).valueAs<double>() : e.cvParam(CVID::MS_inverse_reduced_ion_mobility).valueAs<double>();
						//		dtAxis.insert(dt);
						//	}
						//}

						sindex.push_back(accIndex);
					}
				}

				//reconstruct mass axis and mass indices data sets
				auto it = mz_unique.begin();
				std::vector<double> massAxis(mz_unique.size());
				std::vector<unsigned long> massIndices(datapoints);
				for (size_t i = 0; i < mz_unique.size(); i++)
				{
					massAxis[i] = (*it).first; //add mass value to axis
					for (auto e : (*it).second)
					{
						massIndices[e] = i;
					}
					it++;
				}


				//write mass indices in one step
				if (massIndices.size() > 0)
					connection.createAndWrite1DDataSet(massIndices.size(), massIndices.data(), DataSetType_triMS5::SpectrumMassIndices, presetScanConfiguration);


				//write massAxis
				if (massAxis.size() > 0)
					connection.createAndWrite1DDataSet(massAxis.size(), massAxis.data(), DataSetType_triMS5::SpectrumMassAxis, presetScanConfiguration);

				//write rtAxis
				if (rtAxis.size() > 0)
				{
					std::vector<double> time(rtAxis.size()); //we need a vector not a set
					std::copy(rtAxis.begin(), rtAxis.end(), time.begin());
					connection.createAndWrite1DDataSet(time.size(), time.data(), DataSetType_triMS5::ChromatogramTime, presetScanConfiguration);
				}

				///TODO write DT Axis

				//write attributes
				connection.writeAttributesToGroup(GroupType_triMS5::Spectrum, presetScanConfiguration, H5::PredType::NATIVE_ULONG, "NumberOfRawDataPoints", datapoints);
				connection.writeAttributesToGroup(GroupType_triMS5::Spectrum, presetScanConfiguration, H5::PredType::NATIVE_ULONG, "MassAxisLength", massAxis.size());
				connection.writeAttributesToGroup(GroupType_triMS5::Chromatogram, presetScanConfiguration, H5::PredType::NATIVE_ULONG, "ChromatogramLength", rtAxis.size());
				connection.writeAttributesToGroup(GroupType_triMS5::Spectrum, presetScanConfiguration, H5::PredType::NATIVE_ULONG, "NumberOfDTbins", dtAxis.size());

				if (sindex.size() > 0)
					connection.createAndWrite1DDataSet(sindex.size(), sindex.data(), DataSetType_triMS5::SpectrumIndex, presetScanConfiguration);

				if (spl.size() > 0)
					connection.createAndWrite1DDataSet(spl.size(), spl.data(), DataSetType_triMS5::SpectrumMetaData, presetScanConfiguration);

				if (bdl.size() > 0)
					connection.createAndWrite1DDataSet(bdl.size(), bdl.data(), DataSetType_triMS5::SpectrumBinaryMetaData, presetScanConfiguration);

				spl.clear();
				bdl.clear();

				//write chromatogram index
				std::vector<unsigned long> cindex;
				cindex.push_back(rtAxis.size());
				connection.createAndWrite1DDataSet(cindex.size(), cindex.data(), DataSetType_triMS5::ChromatogramIndex, presetScanConfiguration);

				return status;
			}




			void ReferenceWrite_triMS5::writeTo(Connection_triMS5& connection, const pwiz::util::IterationListenerRegistry* iterationListenerRegistry)
			{
				pwiz::util::IterationListener::Status status = pwiz::util::IterationListener::Status_Ok;


				std::vector<std::pair<int, unsigned long>> spectrumListIndices; // holding the global spectrum list indices

				//check if the numberOfPresetScanConfigurations has been initialized
				if (numberOfPresetScanConfigurations_ < 0)
				{
					numberOfPresetScanConfigurations_ = init(presetScanConfigurationIndices_, spectrumListIndices); //iterates once over all spectra to get the number of distinct presetScanConfiguration  
				}


				//create the file structure according to the spectra
				connection.writeFileStructure(numberOfPresetScanConfigurations_);


				connection.writeAttributesToGroup(GroupType_triMS5::RawData, -1, H5::PredType::NATIVE_INT, "NumberOfPresetScanConfigurations", numberOfPresetScanConfigurations_);

				//write the spectrumListIndices data set
				if (!spectrumListIndices.empty())
					connection.createAndWrite1DDataSet(spectrumListIndices.size(), spectrumListIndices.data(), DataSetType_triMS5::SpectrumListIndices, 0);


				std::vector<ChromatogramMZ5> cl;
				std::vector<BinaryDataMZ5> cbdl;
				status = readAndWriteChromatograms(connection, cbdl, cl, iterationListenerRegistry);
				
				//write meta data only once
				if (cl.size() > 0)
					connection.createAndWrite1DDataSet(cl.size(), cl.data(), DataSetType_triMS5::ChromatogramMetaData, 1);
				if (cbdl.size() > 0)
					connection.createAndWrite1DDataSet(cbdl.size(), cbdl.data(), DataSetType_triMS5::ChromatogramBinaryMetaData, 1);
				cl.clear();
				cbdl.clear();


				if (status != pwiz::util::IterationListener::Status_Cancel)
				{
					std::vector<SpectrumMZ5> sl;
					std::vector<BinaryDataMZ5> sbdl;

					auto it = presetScanConfigurationIndices_.begin();

					for (int i = 0; i < numberOfPresetScanConfigurations_; i++)
					{
						if (status != pwiz::util::IterationListener::Status_Cancel)
							status = readAndWriteSpectra(connection, sbdl, sl, *it, iterationListenerRegistry); //calls triMS5 methods for writing of spectra
						it++;
					}

					if (status != pwiz::util::IterationListener::Status_Cancel)
					{

						if (ref_mz5_.contvacb_.size() > 0)
							connection.createAndWrite1DDataSet(ref_mz5_.contvacb_.size(), ref_mz5_.contvacb_.data(), DataSetType_triMS5::ControlledVocabulary, 0);

						if (ref_mz5_.fileContent_.size() > 0)
							connection.createAndWrite1DDataSet(ref_mz5_.fileContent_.size(), ref_mz5_.fileContent_.data(), DataSetType_triMS5::FileContent, 0);

						if (ref_mz5_.contacts_.size() > 0)
							connection.createAndWrite1DDataSet(ref_mz5_.contacts_.size(), ref_mz5_.contacts_.data(), DataSetType_triMS5::Contact, 0);


						if (ref_mz5_.rl_.size() > 0)
							connection.createAndWrite1DDataSet(ref_mz5_.rl_.size(), ref_mz5_.rl_.data(), DataSetType_triMS5::Run, 0);

						if (ref_mz5_.sourceFileList_.size() > 0)
							connection.createAndWrite1DDataSet(ref_mz5_.sourceFileList_.size(), ref_mz5_.sourceFileList_.data(), DataSetType_triMS5::SourceFiles, 0);

						if (ref_mz5_.sampleList_.size() > 0)
							connection.createAndWrite1DDataSet(ref_mz5_.sampleList_.size(), ref_mz5_.sampleList_.data(), DataSetType_triMS5::Samples, 0);

						if (ref_mz5_.softwareList_.size() > 0)
							connection.createAndWrite1DDataSet(ref_mz5_.softwareList_.size(), ref_mz5_.softwareList_.data(), DataSetType_triMS5::Software, 0);

						if (ref_mz5_.scanSettingList_.size() > 0)
							connection.createAndWrite1DDataSet(ref_mz5_.scanSettingList_.size(), ref_mz5_.scanSettingList_.data(), DataSetType_triMS5::ScanSetting, 0);

						if (ref_mz5_.instrumentList_.size() > 0)
							connection.createAndWrite1DDataSet(ref_mz5_.instrumentList_.size(), ref_mz5_.instrumentList_.data(), DataSetType_triMS5::InstrumentConfiguration, 0);

						if (ref_mz5_.dataProcessingList_.size() > 0)
							connection.createAndWrite1DDataSet(ref_mz5_.dataProcessingList_.size(), ref_mz5_.dataProcessingList_.data(), DataSetType_triMS5::DataProcessing, 0);

						if (ref_mz5_.cvrefs_.size() > 0)
							connection.createAndWrite1DDataSet(ref_mz5_.cvrefs_.size(), ref_mz5_.cvrefs_.data(), DataSetType_triMS5::CVReference, 0);

						if (ref_mz5_.paramGroupList_.size() > 0)
							connection.createAndWrite1DDataSet(ref_mz5_.paramGroupList_.size(), ref_mz5_.paramGroupList_.data(), DataSetType_triMS5::ParamGroups, 0);

						if (ref_mz5_.cvParams_.size() > 0)
							connection.createAndWrite1DDataSet(ref_mz5_.cvParams_.size(), ref_mz5_.cvParams_.data(), DataSetType_triMS5::CVParam, 0);

						if (ref_mz5_.usrParams_.size() > 0)
							connection.createAndWrite1DDataSet(ref_mz5_.usrParams_.size(), ref_mz5_.usrParams_.data(), DataSetType_triMS5::UserParam, 0);

						if (ref_mz5_.refParms_.size() > 0)
							connection.createAndWrite1DDataSet(ref_mz5_.refParms_.size(), ref_mz5_.refParms_.data(), DataSetType_triMS5::RefParam, 0);

						std::vector<FileInformation_triMS5> fi;
						fi.push_back(FileInformation_triMS5());
						if (fi.size() > 0)
							connection.createAndWrite1DDataSet(fi.size(), fi.data(), DataSetType_triMS5::FileInformation, 0);
					}
				}
			}


			pwiz::util::IterationListener::Status ReferenceWrite_triMS5::readAndWriteChromatograms(Connection_triMS5 & connection, std::vector<BinaryDataMZ5>& bdl, std::vector<ChromatogramMZ5>& cpl, const pwiz::util::IterationListenerRegistry * iterationListenerRegistry)
			{
				pwiz::util::IterationListener::Status status = pwiz::util::IterationListener::Status_Ok;
				pwiz::msdata::ChromatogramListPtr cl = ref_mz5_.msd_.run.chromatogramListPtr;
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
								status = iterationListenerRegistry->broadcastUpdateMessage(pwiz::util::IterationListener::UpdateMessage(i, cl->size()));
							if (status == pwiz::util::IterationListener::Status_Cancel)
								break;
							cp = cl->chromatogram(i, true);
							time.clear();
							inten.clear();
							if (cp.get())
							{
								cpl.push_back(ChromatogramMZ5(*cp.get(), ref_mz5_));
								if (cp->getTimeArray().get() && cp->getIntensityArray().get())
								{
									time = cp->getTimeArray().get()->data;
									inten = cp->getIntensityArray().get()->data;
									bdl.push_back(BinaryDataMZ5(*cp->getTimeArray().get(), *cp->getIntensityArray().get(), ref_mz5_));
									if (inten.size() > 0)
									{
										accIndex += (unsigned long)inten.size();
									}
								}
								else {
									bdl.push_back(BinaryDataMZ5());
								}
							}
						}

					}
					return status;
				}
				return status;
			}


			int ReferenceWrite_triMS5::init(std::set<int>& presetScanConfigurationIndices_, std::vector<std::pair<int, unsigned long>>& global)
			{
				//iterate over the spectrum list to generate the spectrumList indices (and get the number of presetScanConfiguration indices)

				/* Create Spectrum List indices globally
				*/
				pwiz::msdata::SpectrumListPtr sl_ptr = ref_mz5_.msd_.run.spectrumListPtr;
				pwiz::msdata::SpectrumPtr sp;
				int preset;
				bool found = false;

				//counts for each preset the number of spectra --> creation of cluster-specific index
				std::map<int, unsigned long> counter;

				for (size_t i = 0; i < sl_ptr->size(); i++)
				{
					sp = sl_ptr->spectrum(i, false);
					//get the preset scan configuration MS_preset_scan_configuration
					for (auto e : sp->scanList.scans)
					{
						if (e.hasCVParam(CVID::MS_preset_scan_configuration))
						{
							preset = e.cvParam(CVID::MS_preset_scan_configuration).valueAs<int>();
							found = true;
							break;
						}
					}
					if (found)
					{
						//does the preset exists already?
						auto it = counter.find(preset);
						if (it != counter.end())
						{
							it->second++; //increment  cluster-specific index
						}
						else {
							counter[preset] = 0;
							it = counter.find(preset);
						}
						//add the presetScanConfig and cluster-specific index (this is not i)
						global.push_back({ preset, it->second });
						presetScanConfigurationIndices_.insert(preset);
						found = false;
					}
					else
					{
						//missing preset scan config information
						break;
					}
				}

			//if there was no presetScanConfigurationIndex or there were spectra with missing preset scan configuration information --> all spectra are written in one SpectrumList
			if (presetScanConfigurationIndices_.empty() || global.size() != sl_ptr->size())
			{
				preset = 1;
				presetScanConfigurationIndices_.clear();
				presetScanConfigurationIndices_.insert(preset);

				global.clear();
				for (size_t i = 0; i < sl_ptr->size(); i++)
					global.push_back({ preset, i });
			}

			return static_cast<int>(presetScanConfigurationIndices_.size());
		}

	}
}
}
