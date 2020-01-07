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
#define PWIZ_SOURCE

#include "pwiz/utility/misc/Std.hpp"
#include "References.hpp"
#include "SpectrumList_triMS5.hpp"
#include "triMS5/Datastructures_triMS5.hpp"
#include <boost/thread.hpp>

namespace pwiz {
	namespace msdata {

		namespace {

			using namespace triMS5;

			/**
			 * Implementation of a spectrum list, using a triMS5 file.
			 */
			class SpectrumList_triMS5Impl : public SpectrumList_triMS5
			{
			public:
				/**
				 * Default constructor.
				 * @param readPtr helper object to read triMS5 files
				 * @param connectionPtr connection to triMS5 file
				 * @param msd MSData object
				 */
				SpectrumList_triMS5Impl(boost::shared_ptr<ReferenceRead_triMS5> readPtr, boost::shared_ptr<Connection_triMS5> connectionPtr, const MSData& msd);

				/**
				 * Getter.
				 * @return number of spectra
				 */
				virtual size_t size() const;

				/**
				 * Return minimal information about a spectrum.
				 * @return index index
				 */
				virtual const SpectrumIdentity& spectrumIdentity(size_t index) const;

				/**
				 * Returns the spectrum with a specific id, if not found an out_of_range exception.
				 * @param id id
				 */
				virtual size_t find(const std::string& id) const;

				/**
				 * Getter.
				 * @param spotID a spot id
				 * @return an index list with all spectra with this spotID
				 */
				virtual IndexList findSpotID(const std::string& spotID) const;

				/**
				*  Find all spectrum indexes with specified name/value pair
				* @param name a string for the name
				* @param value the value
				* @return an index list with all spectra with this name/value pair
				*/
				virtual IndexList findNameValue(const std::string& name, const std::string& value) const;

				/**
				 * Getter.
				 * @param index spectrum index
				 * @param getBinaryData If true this method will also get all binary data(raw data)
				 * @return smart pointer to spectrum
				 */
				virtual SpectrumPtr spectrum(size_t index, bool getBinaryData) const;

				/**
				* Getter.
				* @param index spectrum index
				* @param detailLevel If set to DetailLevel_FullData this method will also get all binary data(raw data)
				* @return smart pointer to spectrum
				*/
				virtual SpectrumPtr spectrum(size_t index, DetailLevel detailLevel) const;

				/**
				 * Destructor.
				 */
				virtual ~SpectrumList_triMS5Impl();

			private:

				/**
				 * MSData object.
				 */
				const MSData& msd_;
				/**
				 * Helper class to read triMS5 files. This reference is used to resolve for example CVParams.
				 */
				boost::shared_ptr<ReferenceRead_triMS5> rref_;
				/**
				 * Connection to the triMS5 file used to get raw data.
				 */
				boost::shared_ptr<Connection_triMS5> conn_;
				/**
				 * List of meta information.
				 */
				mutable std::vector<SpectrumMZ5*> spectrumData_;
				/**
				 * List of binary data meta information.
				 */
				mutable std::vector<BinaryDataMZ5*> binaryParamsData_;

				/**
				*  For each cluster a list of spectrumIdentities
				*/
				mutable std::map<unsigned int, std::vector<SpectrumIdentity>> spectrumIdentityList_;

				/**
				* contains for each spectrum a k,v tuple with k being the index of the spectrum and v another tuple of the start and end points in the hdf5 datasets
				*/
				mutable std::vector<std::map<size_t, std::pair<hsize_t, hsize_t>>> spectrumRanges_;

				/**
				* internal containers used for mapping
				*/
				mutable std::map<std::string, std::pair<unsigned int, size_t>> idMap_;
				mutable std::map<std::string, IndexList> spotMap_;
				size_t numberOfSpectra_;
				std::vector<size_t> spectraPerCluster_;
				mutable bool initSpectra_;
				mutable std::vector<SpectrumListIndices_triMS5> spectrumListIndices_;
				mutable std::map<unsigned int, std::vector<double>> massAxis_;
				
				mutable boost::mutex readMutex;
				/**
				* internal methods used for initializing spectra data and required mapping
				*/
				void initSpectra() const;
			};

			SpectrumList_triMS5Impl::~SpectrumList_triMS5Impl()
			{
				if (!spectrumData_.empty())
				{
					for (size_t i = 0; i < spectrumData_.size(); ++i)
					{
						if (spectrumData_[i])
						{
							conn_->clean(DataSetType_triMS5::SpectrumMetaData, spectrumData_[i], spectraPerCluster_[i]);
							spectrumData_[i] = nullptr;
						}
					}
				}
				if (!binaryParamsData_.empty())
				{
					for (size_t i = 0; i < binaryParamsData_.size(); ++i)
					{
						if (binaryParamsData_[i])
						{
							conn_->clean(DataSetType_triMS5::SpectrumBinaryMetaData, binaryParamsData_[i], spectraPerCluster_[i]);
							binaryParamsData_[i] = nullptr;
						}
					}
				}

			}

			SpectrumList_triMS5Impl::SpectrumList_triMS5Impl(boost::shared_ptr<ReferenceRead_triMS5> readPtr, boost::shared_ptr<Connection_triMS5> connectionPtr, const MSData& msd) : msd_(msd), rref_(readPtr), conn_(connectionPtr), spectrumData_(), binaryParamsData_(), spectrumIdentityList_(), spectrumRanges_(), idMap_(), spotMap_(), numberOfSpectra_(0), spectraPerCluster_(), initSpectra_(false), spectrumListIndices_(), massAxis_()
			{
				//get number of spectra in each cluster
				for (int i = 0; i < readPtr->getNumberOfPresetScanConfigurations(); ++i)
				{
					auto it = conn_->getAvailableDataSets().find({ DataSetType_triMS5::SpectrumMetaData, i + 1 });
					if (it != conn_->getAvailableDataSets().end())
					{
						size_t tmp = it->second;
						spectraPerCluster_.push_back(tmp);
						numberOfSpectra_ += tmp;
					}
				}
				setDataProcessingPtr(readPtr->getDefaultSpectrumDP(0)); //calls ref_mz5 default spectrum dp method

				Configuration_triMS5 c_triMS5 = conn_->getConfiguration();
				Configuration_mz5 c_mz5 = c_triMS5.getMZ5Configuration();
				if (c_mz5.getSpectrumLoadPolicy() == Configuration_mz5::SLP_InitializeAllOnCreation)
				{
					initSpectra();
				}
			}

			void SpectrumList_triMS5Impl::initSpectra() const
			{
				if (!initSpectra_)
				{
					if (numberOfSpectra_ > 0)
					{
						size_t numberOfClusters = spectraPerCluster_.size();
						//read out the spectrumListIndices used for mapping of global and cluster specific spectrum indices
						spectrumListIndices_.resize(numberOfSpectra_);
						size_t dsend;
						conn_->readDataSet(DataSetType_triMS5::SpectrumListIndices, dsend, -1, spectrumListIndices_.data());

						spectrumRanges_.resize(numberOfClusters);

						//for each cluster
						for (size_t i = 0; i < numberOfClusters; ++i)
						{
							std::vector<unsigned long> index;
							index.resize(spectraPerCluster_[i]);

							conn_->readDataSet(DataSetType_triMS5::SpectrumIndex, dsend, (i + 1), index.data());
							hsize_t last = 0, current = 0;
							hsize_t overflow_correction = 0; // triMS5 writes these as 32 bit values, so deal with overflow
							for (size_t j = 0; j < index.size(); ++j)
							{
								current = static_cast<hsize_t> (index[j]) + overflow_correction;
								if (last > current)
								{
									overflow_correction += 0x0100000000; // This assumes no scan has more than 4GB of peak data
									current = static_cast<hsize_t> (index[j]) + overflow_correction;
								}
								spectrumRanges_[i].insert({ j,{last, current} });
								last = current;
							}
							spectrumData_.push_back((SpectrumMZ5*)calloc(spectraPerCluster_[i], sizeof(SpectrumMZ5)));
							conn_->readDataSet(DataSetType_triMS5::SpectrumMetaData, dsend, (i + 1), spectrumData_[i]);

							binaryParamsData_.push_back((BinaryDataMZ5*)calloc(dsend, sizeof(BinaryDataMZ5)));
							conn_->readDataSet(DataSetType_triMS5::SpectrumBinaryMetaData, dsend, (i + 1), binaryParamsData_[i]);

							spectrumIdentityList_[i+1].resize(dsend);
							for (hsize_t j = 0; j < dsend; ++j)
							{
								spectrumIdentityList_[i+1][j] = spectrumData_[i][j].getSpectrumIdentity();
								idMap_.insert({ spectrumIdentityList_[i + 1][j].id, {i + 1, j} });
								if (!spectrumIdentityList_[i + 1][j].spotID.empty())
								{
									//get the global index of (presetScanConfigurationIndex, cluster-specific index) - it should match the requested one instead the one specific to the cluster
									auto it2 = std::find(spectrumListIndices_.begin(), spectrumListIndices_.end(), SpectrumListIndices_triMS5(i + 1, j));
									if (it2 != spectrumListIndices_.end())
									{
										spotMap_[spectrumIdentityList_[i + 1][j].spotID].push_back(std::distance(spectrumListIndices_.begin(), it2));
									}
										
								}
									
							}

							//finally read mass axis
							auto ma_it = conn_->getAvailableDataSets().find({ DataSetType_triMS5::SpectrumMassAxis, i + 1 });
							if (ma_it != conn_->getAvailableDataSets().end())
							{
								//read out the spectrumListIndices used for mapping of global and cluster specific spectrum indices
								massAxis_[i+1].resize(ma_it->second);
								conn_->readDataSet(DataSetType_triMS5::SpectrumMassAxis, dsend, i + 1, massAxis_[i + 1].data());

							}
						}
					}
					initSpectra_ = true;
				}
			}

			size_t SpectrumList_triMS5Impl::size() const
			{
				return numberOfSpectra_;
			}

			const SpectrumIdentity& SpectrumList_triMS5Impl::spectrumIdentity(size_t index) const
			{
				initSpectra();
				if (index >= 0 && index < numberOfSpectra_)
				{
					SpectrumListIndices_triMS5 idx = spectrumListIndices_[index];
					//change the index of the spectrum identity since it should match the requested one instead the one specific to the cluster
					spectrumIdentityList_[idx.presetScanConfigurationIndex][idx.localSpectrumIndex].index = index;
					return spectrumIdentityList_[idx.presetScanConfigurationIndex][idx.localSpectrumIndex];
				}
				throw std::out_of_range("[SpectrumList_triMS5Impl::spectrumIdentity()] out of range");
			}

			size_t SpectrumList_triMS5Impl::find(const std::string& id) const
			{
				initSpectra();
				auto it = idMap_.find(id);
				
				if (it != idMap_.end())
				{
					//get the global index of (presetScanConfigurationIndex, cluster-specific index)
					auto it2 = std::find(spectrumListIndices_.begin(), spectrumListIndices_.end(), SpectrumListIndices_triMS5(it->second.first, it->second.second));
					
					//adopt the index of the spectrum identity since it should match the requested one instead the one specific to the cluster
					if (it2 != spectrumListIndices_.end())
						return std::distance(spectrumListIndices_.begin(), it2);
						
				}
				return size();
				
			}

			IndexList SpectrumList_triMS5Impl::findSpotID(const std::string& spotID) const
			{
				initSpectra();
				auto it = spotMap_.find(spotID);
				return it != spotMap_.end() ? it->second : IndexList();
			}

			IndexList SpectrumList_triMS5Impl::findNameValue(const std::string& name, const std::string& value) const
			{
				initSpectra();
				IndexList result;
				for (size_t index = 0; index < size(); ++index)
				{
					if (id::value(spectrumIdentity(index).id, name) == value)
					{
						result.push_back(index);
					}
				}
				return result;
			}


			SpectrumPtr SpectrumList_triMS5Impl::spectrum(size_t index, DetailLevel detailLevel) const
			{
				return spectrum(index, detailLevel == DetailLevel_FullData);
			}

			SpectrumPtr SpectrumList_triMS5Impl::spectrum(size_t index, bool getBinaryData) const
			{
				boost::lock_guard<boost::mutex> lock(readMutex); 
				initSpectra();

				if (index >= 0 && index < numberOfSpectra_)
				{
					SpectrumListIndices_triMS5 idx = spectrumListIndices_[index];
					const ReferenceRead_mz5* ref = rref_->getRefMZ5();
					SpectrumPtr ptr(spectrumData_[idx.presetScanConfigurationIndex - 1][idx.localSpectrumIndex].getSpectrum(*ref, *conn_));
					std::pair<hsize_t, hsize_t> bounds = spectrumRanges_[idx.presetScanConfigurationIndex - 1].find(idx.localSpectrumIndex)->second;
					hsize_t start = bounds.first;
					hsize_t end = bounds.second;
					ptr->defaultArrayLength = end - start;
					if (getBinaryData)
					{
						if (!binaryParamsData_[idx.presetScanConfigurationIndex - 1][idx.localSpectrumIndex].empty())
						{
							std::vector<double> mz, inten;
							std::vector<unsigned int> mz_indices;
				
							conn_->getData(mz_indices, DataSetType_triMS5::SpectrumMassIndices, idx.presetScanConfigurationIndex,  start, end);
							//translate indices back
							for (auto e : mz_indices)
								mz.push_back(massAxis_[idx.presetScanConfigurationIndex][e]);
							conn_->getData(inten, DataSetType_triMS5::SpectrumIntensity, idx.presetScanConfigurationIndex, start, end);
							ptr->setMZIntensityArrays(mz, inten, CVID_Unknown);
								
							// intensity unit will be set by the following command
							binaryParamsData_[idx.presetScanConfigurationIndex - 1][idx.localSpectrumIndex].fill(*ptr->getMZArray(), *ptr->getIntensityArray(), *ref);
						}
					}
					ptr->index = index;
					References::resolve(*ptr, msd_);
					return ptr;
				}
				throw std::out_of_range("[SpectrumList_triMS5Impl::spectrum()] out of range");
			}

		} // namespace


		PWIZ_API_DECL
			SpectrumListPtr SpectrumList_triMS5::create(boost::shared_ptr<ReferenceRead_triMS5> readPtr, boost::shared_ptr<Connection_triMS5> connectionPtr, const MSData& msd)
		{
			return SpectrumListPtr(new SpectrumList_triMS5Impl(readPtr, connectionPtr, msd));
		}

		PWIZ_API_DECL SpectrumList_triMS5::~SpectrumList_triMS5()
		{
		}


	} // namespace msdata
} // namespace pwiz
