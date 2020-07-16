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

#define PWIZ_SOURCE

#include "pwiz/utility/misc/Std.hpp"
#include "References.hpp"
#include "ChromatogramList_triMS5.hpp"
#include "pwiz/data/msdata/SpectrumWorkerThreads.hpp"


namespace pwiz {
	namespace msdata {


		namespace {

			using namespace triMS5;

			/**
			 * Implementation of a ChromatogramList.
			 */
			class ChromatogramList_triMS5Impl : public ChromatogramList_triMS5
			{
			public:

				/**
				 * Default constructor.
				 * @param readPtr helper class to read triMS5 files
				 * @param connectenPtr connection to triMS5 file
				 * @param msd MSData object
				 */
				ChromatogramList_triMS5Impl(boost::shared_ptr<ReferenceRead_triMS5> readPtr, boost::shared_ptr<Connection_triMS5> connectionptr, const MSData& msd);

				/**
				 * Getter.
				 * @return number of data points in this chromatogram.
				 */
				virtual size_t size() const;

				/**
				 * Getter.
				 * @return minimal information about a chromatogram.
				 */
				virtual const ChromatogramIdentity& chromatogramIdentity(size_t index) const;

				/**
				 * Returns the index the chromatogram with a specific id, otherwise an out_of_range exception.
				 * @param id chromatogram id
				 * @return index
				 */
				virtual size_t find(const std::string& id) const;

				/**
				 * Returns a specific chromatogram.
				 * @param index index
				 * @param getBinaryData
				 * @return smart pointer to chromatogram
				 */
				virtual ChromatogramPtr chromatogram(size_t index, bool getBinaryData) const;

				/**
				 * Destructor.
				 */
				virtual ~ChromatogramList_triMS5Impl();

			private:
				/**
				 * Initializes chromatogram list.
				 */
				void initialize() const;


				void createAndWriteTIC() const;

				/**
				* Helper class to read triMS5 files. This reference is used to resolve for example CVParams.
				*/
				boost::shared_ptr<ReferenceRead_triMS5> rref_;


				/**
				* Connection to the triMS5 file used to get raw data.
				*/
				boost::shared_ptr<Connection_triMS5> conn_;

				/**
				 * MSData object.
				 */
				const MSData& msd_;
	
				/**
				 * List of binary data meta information.
				 */
				mutable BinaryDataMZ5* binaryParamList_;
				/**
				 * List of all chromatogram identities.
				 */
				mutable std::vector<ChromatogramIdentity> identities_;
				/**
				 * List of chromatogram meta data.
				 */
				mutable ChromatogramMZ5* chromatogramData_;
				/**
				 * Mapping of a chromatogram ID to an index.
				 */
				mutable std::map<std::string, size_t> chromatogramIndex_;
				/**
				 * Map of chromatogram indices to start and stop indices in time and intensity datasets.
				 */
				mutable std::map<hsize_t, std::pair<hsize_t, hsize_t> > chromatogramRanges_;
				mutable bool initialized_;
				mutable size_t numberOfChromatograms_;
			};



			ChromatogramList_triMS5Impl::ChromatogramList_triMS5Impl(boost::shared_ptr<ReferenceRead_triMS5> readPtr, boost::shared_ptr<Connection_triMS5> connectionPtr, const MSData& msd) : rref_(readPtr), conn_(connectionPtr), msd_(msd), binaryParamList_(0), identities_(), chromatogramData_(0), chromatogramIndex_(), chromatogramRanges_(), initialized_(false), numberOfChromatograms_(0)
			{
				setDataProcessingPtr(readPtr->getDefaultChromatogramDP(0));
				Configuration_triMS5 c_triMS5 = conn_->getConfiguration();
				Configuration_mz5 c_mz5 = c_triMS5.getMZ5Configuration();
				if (c_mz5.getChromatogramLoadPolicy() == Configuration_mz5::CLP_InitializeAllOnCreation)
				{
					initialize();
				}

			}

			ChromatogramList_triMS5Impl::~ChromatogramList_triMS5Impl()
			{
			    if (chromatogramData_)
			    {
			        conn_->clean(DataSetType_triMS5::ChromatogramMetaData, chromatogramData_, numberOfChromatograms_);
			        chromatogramData_ = 0;
			    }
			    if (binaryParamList_)
			    {
					conn_->clean(DataSetType_triMS5::ChromatogramBinaryMetaData, binaryParamList_, numberOfChromatograms_);
			        binaryParamList_ = 0;
			    }
			}





			void ChromatogramList_triMS5Impl::initialize() const
			{
				if (!initialized_)
				{
					const DataSetIdPairToSizeMap& fields = conn_->getAvailableDataSets();
					size_t dsend = fields.find({ DataSetType_triMS5::ChromatogramMetaData, 1 })->second;
					numberOfChromatograms_ = dsend;
					if (dsend > 0)
					{
						binaryParamList_ = (BinaryDataMZ5*)calloc(dsend, sizeof(BinaryDataMZ5));
						chromatogramData_ = (ChromatogramMZ5*)calloc(dsend, sizeof(ChromatogramMZ5));
						conn_->readDataSet(DataSetType_triMS5::ChromatogramMetaData, dsend, 1, chromatogramData_);
						conn_->readDataSet(DataSetType_triMS5::ChromatogramBinaryMetaData, dsend, 1, binaryParamList_);
						for (size_t i = 0; i < dsend; ++i)
						{

							identities_.push_back(chromatogramData_[i].getChromatogramIdentity());
							std::string cid(chromatogramData_[i].id);
							chromatogramIndex_.insert(std::make_pair(cid, i));

						}

						std::vector<unsigned long> index(dsend);

						conn_->readDataSet(DataSetType_triMS5::ChromatogramIndex, dsend, 1, index.data());
						hsize_t last = 0, current = 0;
						hsize_t overflow_correction = 0; // mz5 writes these as 32 bit values, so deal with overflow
						for (size_t i = 0; i < index.size(); ++i)
						{
							current = static_cast<hsize_t> (index[i]) + overflow_correction;
							if (last > current)
							{
								overflow_correction += 0x0100000000; // This assumes no chromatogram has more than 4GB of data
								current = static_cast<hsize_t> (index[i]) + overflow_correction;
							}
							chromatogramRanges_.insert(std::make_pair(i, std::make_pair(last, current)));
							last = current;
						}
					}
					else
					{
						binaryParamList_ = 0;
						chromatogramData_ = 0;
					}

					//check if ChromatogramIntensity data set is already created
					auto it = fields.find({ DataSetType_triMS5::ChromatogramIntensity, 1 });
					if (it == fields.end())
					{
						createAndWriteTIC();
					}

					initialized_ = true;
				}
			}

			size_t ChromatogramList_triMS5Impl::size() const
			{
				initialize();
				return numberOfChromatograms_;
			}

			const ChromatogramIdentity& ChromatogramList_triMS5Impl::chromatogramIdentity(size_t index) const
			{
				initialize();
				if (numberOfChromatograms_ > index && index >= 0)
				{
					return identities_[index];
				}
				throw std::out_of_range("ChromatogramList_triMS5Impl::chromatogramIdentity() out of range");
			}


			size_t ChromatogramList_triMS5Impl::find(const std::string& id) const
			{
				initialize();
				std::map<std::string, size_t>::const_iterator it = chromatogramIndex_.find(id);
				return it != chromatogramIndex_.end() ? it->second : size();
			}


			ChromatogramPtr ChromatogramList_triMS5Impl::chromatogram(size_t index, bool getBinaryData) const
			{
				initialize();
				if (numberOfChromatograms_ > index)
				{
					ChromatogramPtr ptr(chromatogramData_[index].getChromatogram(*rref_->getRefMZ5(), *conn_));
					std::pair<hsize_t, hsize_t> bounds = chromatogramRanges_.find(index)->second;
					hsize_t start = bounds.first;
					hsize_t end = bounds.second;
					ptr->defaultArrayLength = end - start;
					if (getBinaryData)
					{
						if (!binaryParamList_[index].empty()) {

							//triMS5 has time and inten after initialization, eventually written in different clusters
							std::vector<double> time;

							//read in preset scan configuration specific chromatogram time 
							const DataSetIdPairToSizeMap& fields = conn_->getAvailableDataSets();

							//find first ChromatogramTime data set
							int presetIndex = 1;
							auto it = fields.find({ DataSetType_triMS5::ChromatogramTime, presetIndex });
							while (it != conn_->getAvailableDataSets().end())
							{
								//chromatogram time
								size_t dsend;
								std::vector<double> rt_tmp(it->second);
								conn_->readDataSet(DataSetType_triMS5::ChromatogramTime, dsend, presetIndex, rt_tmp.data());

								//update
								presetIndex++;
								it = fields.find({ DataSetType_triMS5::ChromatogramTime, presetIndex });

								time.insert(time.end(), rt_tmp.begin(), rt_tmp.end());
							}
							std::sort(time.begin(), time.end());

							//get intensities
							it = fields.find({ DataSetType_triMS5::ChromatogramIntensity, 1 });
							size_t dsend;
							std::vector<double> inten(it->second);
							conn_->readDataSet(DataSetType_triMS5::ChromatogramIntensity, dsend, 1, inten.data());


							ptr->setTimeIntensityArrays(time, inten, CVID_Unknown, CVID_Unknown);
							// time and intensity unit will be set by the following command
							binaryParamList_[index].fill(*ptr->getTimeArray(), *ptr->getIntensityArray(), *rref_->getRefMZ5());
						}
					}
					References::resolve(*ptr, msd_);
					return ptr;
				}
				throw std::out_of_range("ChromatogramList_triMS5Impl::chromatogram() out of range");
			}


			void ChromatogramList_triMS5Impl::createAndWriteTIC() const
			{
				const DataSetIdPairToSizeMap& fields = conn_->getAvailableDataSets();

				if (fields.find({ DataSetType_triMS5::ChromatogramIntensity, 1 }) == fields.end())
				{
					//triMS5 has time array only, eventually written in different clusters
					std::map<double, double> cmap;
					std::vector<double> time, inten;

				
					//read in chromatogram time and preset scan configuration specific intensities
					//find get all chromatogram times 
					unsigned int preset = 1;
					auto it = fields.find({ DataSetType_triMS5::ChromatogramTime, preset });

					while (it != conn_->getAvailableDataSets().end())
					{
						//chromatogram time
						size_t dsend;
						std::vector<double> rt_tmp(it->second);
						conn_->readDataSet(DataSetType_triMS5::ChromatogramTime, dsend, 1, rt_tmp.data());
						time.insert(time.end(), rt_tmp.begin(), rt_tmp.end());

						//update
						preset++;
						it = fields.find({ DataSetType_triMS5::ChromatogramTime, preset });
					}

					//sort chromatogram time
					std::sort(time.begin(), time.end());


					auto rt_it = time.begin();
					double curr_rt = *rt_it;


					//intensities from spectrum data
					//get the spectrum list
					pwiz::msdata::SpectrumListPtr sl = msd_.run.spectrumListPtr;

					SpectrumWorkerThreads spectrumWorkers(*sl, true);
					SpectrumPtr sp;
					BinaryDataArrayPtr inten_tmp;
					for (size_t i = 0; i < sl->size(); i++)
					{
						sp = spectrumWorkers.processBatch(i);
						if (sp.get())
						{
							inten_tmp = sp->getIntensityArray();
							if (inten_tmp.get())
							{

								//generate tic
								double sum = std::accumulate(inten_tmp->data.begin(), inten_tmp->data.end(), 0);

								//check rt
								for (auto e : sp->scanList.scans)
								{
									//if we found the retention time
									if (e.hasCVParam(CVID::MS_scan_start_time))
									{
										curr_rt = e.cvParam(CVID::MS_scan_start_time).valueAs<double>();

										//go to next retention time point
										if (curr_rt != *rt_it)
											rt_it++;
									}
								}
								//add intensities to rt bin
								cmap[curr_rt] += sum;
							}
						}
					}
					//get write buffer for intensities
					for (auto e : cmap)
						inten.push_back(e.second);
					//once we generated  intensities write them to the file
					conn_->createAndWrite1DDataSet(inten.size(), inten.data(), DataSetType_triMS5::ChromatogramIntensity, 1);

				}
			}


		} // namespace


		PWIZ_API_DECL
			ChromatogramListPtr ChromatogramList_triMS5::create(boost::shared_ptr<ReferenceRead_triMS5> readPtr, boost::shared_ptr<Connection_triMS5> connectionPtr, const MSData& msd)
		{
			return ChromatogramListPtr(new ChromatogramList_triMS5Impl(readPtr, connectionPtr, msd));
		}

		PWIZ_API_DECL ChromatogramList_triMS5::~ChromatogramList_triMS5()
		{
		}
	} // namespace msdata
} // namespace pwiz
