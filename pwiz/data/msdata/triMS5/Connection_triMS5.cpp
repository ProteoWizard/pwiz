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

#include "Connection_triMS5.hpp"
#include "../mz5/ReferenceWrite_mz5.hpp"
#include "../mz5/ReferenceRead_mz5.hpp"

#include <algorithm>
#include <string>
#include <iostream>
#include "boost/thread/mutex.hpp"

namespace pwiz {
namespace msdata {
	namespace triMS5 {

		using namespace H5;

		namespace { boost::mutex connectionReadMutex_, connectionWriteMutex_; }

		using namespace pwiz::msdata::mz5;

		Connection_triMS5::Connection_triMS5(const std::string filename, const OpenPolicy op, const Configuration_triMS5 config) : config_(config), file_(nullptr), availDataSets_(), dsets_(), buffers_(), closed_(false)
		{
			boost::mutex::scoped_lock lock(connectionReadMutex_);

			FileCreatPropList fcparm = FileCreatPropList::DEFAULT;
			FileAccPropList faparm = FileAccPropList::DEFAULT;

			if (op == OpenPolicy::ReadWrite || op == OpenPolicy::ReadOnly)
			{
				int mds_nelemts;
				size_t rdcc_nelmts, rdcc_nbytes;
				double rdcc_w0;
				faparm.getCache(mds_nelemts, rdcc_nelmts, rdcc_nbytes, rdcc_w0);
				rdcc_nbytes = config_.getBufferInB();
				// TODO can be set to 1 if chunks that have been fully read/written will never be read/written again
				//  rdcc_w0 = 1.0;
				rdcc_nelmts = config_.getRdccSlots();
				faparm.setCache(mds_nelemts, rdcc_nelmts, rdcc_nbytes, rdcc_w0);
			}

			


			unsigned int openFlag = H5F_ACC_TRUNC;
			switch (op)
			{
			case OpenPolicy::RemoveAndCreate:
				openFlag = H5F_ACC_TRUNC;
				file_ = std::unique_ptr<H5File>{ new H5File(filename, openFlag, fcparm, faparm) };
				writeAttributesToGroup(GroupType_triMS5::Root, -1, H5::PredType::NATIVE_HBOOL, "triMS5", 1);
				break;
			case OpenPolicy::FailIfFileExists:
				openFlag = H5F_ACC_EXCL;
				file_ = std::unique_ptr<H5File>{ new H5File(filename, openFlag, fcparm, faparm) };
				writeAttributesToGroup(GroupType_triMS5::Root, -1, H5::PredType::NATIVE_HBOOL, "triMS5", 1);
				break;
			case OpenPolicy::ReadWrite:
				openFlag = H5F_ACC_RDWR;
				file_ = std::unique_ptr<H5File>{ new H5File(filename, openFlag, fcparm, faparm) };
				readFile();
				break;
			case OpenPolicy::ReadOnly:
				openFlag = H5F_ACC_RDONLY;
				file_ = std::unique_ptr<H5File>{ new H5File(filename, openFlag, fcparm, faparm) };
				readFile();
				break;
			default:
				break;
			}
			closed_ = false;
		}



		void Connection_triMS5::writeFileStructure(int numberOfClusters)
		{
			H5::Group root = file_->openGroup("/");
			
			root.close();

			createAndWriteGroup(GroupType_triMS5::MetaData, config_.getNameFor(GroupType_triMS5::MetaData),0);
			createAndWriteGroup(GroupType_triMS5::RawData, config_.getNameFor(GroupType_triMS5::RawData),0);

			std::string cluster = config_.getNameFor(GroupType_triMS5::Cluster) + "_";
			for (int i = 1; i <= numberOfClusters; i++)
			{
				createAndWriteGroup(GroupType_triMS5::Cluster, cluster + std::to_string(i) + "/",i);
				createAndWriteGroup(GroupType_triMS5::Spectrum, config_.getNameFor(GroupType_triMS5::Spectrum), i);
				createAndWriteGroup(GroupType_triMS5::Chromatogram, config_.getNameFor(GroupType_triMS5::Chromatogram), i);
			}
		}


		void Connection_triMS5::createAndWriteGroup(const GroupType_triMS5& v, const std::string& name, unsigned int presetScanConfiguration)
		{
			boost::mutex::scoped_lock lock(connectionWriteMutex_);
			H5::Group group, father_group;
			father_group = openGroup(config_.getGroupTypeFor(v), presetScanConfiguration);
			group = father_group.createGroup(name);
			group.close();
		}

		// opens the specified group
		H5::Group Connection_triMS5::openGroup(const GroupType_triMS5& v, int presetScanConfiguration)
		{
			std::string name;
			switch (v)
			{
			case GroupType_triMS5::Root:
				name = "/";
				break;

			case GroupType_triMS5::Cluster:
				name = "RawData/Cluster_" + std::to_string(presetScanConfiguration);
				break;

			case GroupType_triMS5::Spectrum:
				name = "RawData/Cluster_" + std::to_string(presetScanConfiguration) + "/Spectrum";
				break;

			case GroupType_triMS5::Chromatogram:
				name = "RawData/Cluster_" + std::to_string(presetScanConfiguration) + "/Chromatogram";
				break;
			default:
				name = config_.getNameFor(v);
				break;
			}
			return file_->openGroup(name);
		}




		Connection_triMS5::~Connection_triMS5()
		{
			close();
		}


		DSetCreatPropList Connection_triMS5::getCParm(int rank, const DataSetType_triMS5& v, const hsize_t& datadim)
		{
			DSetCreatPropList cparm;
			if (config_.getChunkSizeFor(v) != Configuration_mz5::EMPTY_CHUNK_SIZE)
			{
				hsize_t chunks[1] = { std::min(config_.getChunkSizeFor(v), datadim) };
				cparm.setChunk(rank, chunks);
				cparm.setShuffle(); // triMS5 always shuffles
				if (config_.getMZ5Configuration().getDeflateLvl() != 0)
				{
					cparm.setDeflate(config_.getMZ5Configuration().getDeflateLvl());
				}
			}
			return cparm;
		}

		DataSet Connection_triMS5::createDataSet(H5::Group father, int rank, hsize_t* dim, hsize_t* maxdim, const DataSetType_triMS5& v, unsigned int presetScanConfiguration)
		{
			DSetCreatPropList cparms = getCParm(rank, v, maxdim[0]);
			DataSpace dataSpace(rank, dim, maxdim);

			DataSet dataset = father.createDataSet(config_.getNameFor(v), config_.getDataTypeFor(v), dataSpace, cparms);
			availDataSets_[{v, presetScanConfiguration}] =  dim[0]; //initial size of the dataset is zero
			dataSpace.close();
			return dataset;
		}

		void Connection_triMS5::createAndWrite1DDataSet(hsize_t size, void* data, const DataSetType_triMS5& v, unsigned int presetScanConfiguration)
		{
			boost::mutex::scoped_lock lock(connectionWriteMutex_);
			if (size > 0)
			{
				H5::Group father = openGroup(config_.getGroupTypeFor(v), presetScanConfiguration);
				hsize_t dim[1] = { size };
				hsize_t maxdim[1] = { size };
				DataSet dataset = createDataSet(father, 1, dim, maxdim, v, presetScanConfiguration);

				dataset.write(data, config_.getDataTypeFor(v));
				dataset.close();
				father.close();
			}
		}



		void Connection_triMS5::readElementsInGroup(const H5::Group& g, const GroupType_triMS5& group_type, unsigned int level)
		{
			//now get elements in the group
			for (hsize_t i = 0; i < g.getNumObjs(); ++i)
			{
	
				if (g.getObjTypeByIdx(i) == H5G_DATASET)
				{
					H5::DataSet dset = g.openDataSet(g.getObjnameByIdx(i));
					DataSpace dspace = dset.getSpace();
					hsize_t start[1], end[1];
					dspace.getSelectBounds(start, end);
					size_t dsend = (static_cast<size_t> (end[0])) + 1;

					unsigned int presetScanConfig = level;
					availDataSets_[{config_.getDataSetTypeFor(g.getObjnameByIdx(i)), presetScanConfig}] = dsend;
					dset.close();
					dspace.close();
				}

				if (g.getObjTypeByIdx(i) == H5G_GROUP)
				{
					std::string name = g.getObjnameByIdx(i);
					H5::Group sub = g.openGroup(name);
					GroupType_triMS5 gt;  // the group type of the sub group
					unsigned int preset = 0;

					//Check if it is a cluster Group
					if (name.find(config_.getNameFor(GroupType_triMS5::Cluster)) != std::string::npos)
					{
						gt = GroupType_triMS5::Cluster;
						preset = i + 1; 
					}
					else
					{
						gt = config_.getGroupTypeFor(name); // the group type of the sub group
						if (gt == GroupType_triMS5::Spectrum || gt == GroupType_triMS5::Chromatogram)
						{
							preset = level;
						}	
					}
					readElementsInGroup(sub, gt, preset);
					sub.close();
				}
			}
		}
		

#include <iostream>
		void Connection_triMS5::readFile()
		{
			/*find the triMS5Attribut at the root group --> used to distinguish mz5 and triMS5 files
			*/
			H5::Group root = openGroup(GroupType_triMS5::Root);
			if (root.getNumAttrs() > 0)
			{
				//try to open the triMS5 attribute
				H5::Attribute triMS5_att = root.openAttribute("triMS5");
				H5::DataType datatype = triMS5_att.getDataType();
				bool isTriMS5 = false;
				triMS5_att.read(datatype, &isTriMS5);

				//detect all available groups and datasets
				if (isTriMS5)
				{
					readElementsInGroup(root, GroupType_triMS5::Root, -1);
					root.close();
				}
			}
			else
			{
				throw std::runtime_error("Connection_triMS5::constructor(): given file is no triMS5 file.");
			}
		}


		void Connection_triMS5::addToBuffer(std::vector<double>& b, const std::vector<double>& d1, const size_t bs, const DataSet& dataset)
		{
			size_t ci = 0, ni = 0;
			size_t l;
			while (ci < d1.size())
			{
				l = bs - b.size();
				ni = ci + std::min(l, d1.size() - ci);
				//TODO use memcpy?
				for (size_t i = ci; i < ni; ++i)
				{
					b.push_back(d1[i]);
				}
				if (b.size() == bs)
				{
					extendAndWrite1DDataSet(dataset, b);
					b.clear();
					b.reserve(bs);
				}
				ci = ni;
			}
		}


		void Connection_triMS5::extendAndWrite1DDataSet(const H5::DataSet& dataset, const std::vector<double>& d1)
		{
			hsize_t start[1], end[1];
			dataset.getSpace().getSelectBounds(start, end);

			hsize_t currentsize = d1.size();
			hsize_t fullsize = end[0] + 1;

			hsize_t extension_size[1], offset[1], dims1[1];
			extension_size[0] = fullsize + currentsize;
			dataset.extend(extension_size);

			DataSpace fspace = dataset.getSpace();
			offset[0] = fullsize;
			dims1[0] = currentsize;
			fspace.selectHyperslab(H5S_SELECT_SET, dims1, offset);

			DataSpace mspace(1, dims1);
			dataset.write(&d1[0], PredType::NATIVE_DOUBLE, mspace, fspace);
			fspace.close();
			mspace.close();
		}


		void Connection_triMS5::extendData(const std::vector<double>& d1, const DataSetType_triMS5& v, unsigned int presetScanConfigurationIndex)
		{
			boost::mutex::scoped_lock lock(connectionWriteMutex_);
			
			//get buffer size for this data set
			size_t bs = config_.getBufferSizeFor(v);

			//check if the dataset already exists:
			auto it = availDataSets_.find({ v, presetScanConfigurationIndex });

			//if the dataset does not exist or it does not exist with the correct presetScanConfiguration
			if (it == availDataSets_.end())
			{

				//search in dsets - if it is not found in availDataSets_ then it isn't present in dsets_ either but search anyways
				auto it2 = dsets_.find({ v, presetScanConfigurationIndex });


				if (it2 == dsets_.end())
				{
					//first open the corresponding group:
					H5::Group father = openGroup(config_.getGroupTypeFor(v), presetScanConfigurationIndex);

					hsize_t dim[1] = { 0 };
					hsize_t maxdim[1] = { H5S_UNLIMITED };
					H5::DataSet dset = createDataSet(father, 1, dim, maxdim, v, presetScanConfigurationIndex); //the data set is not closed 
					dsets_[{v, presetScanConfigurationIndex}] = dset;
					father.close();

					if (bs != Configuration_mz5::NO_BUFFER_SIZE)
					{
						buffers_[{v, presetScanConfigurationIndex}] = std::vector<double>();
						buffers_.find({ v, presetScanConfigurationIndex })->second.reserve(bs); // if we have No_BUFFER_SIZE zero is reserved
					}
				}

			}

			if (bs != Configuration_mz5::NO_BUFFER_SIZE)
			{
				addToBuffer(buffers_.find({ v, presetScanConfigurationIndex })->second, d1, bs, dsets_.find({ v, presetScanConfigurationIndex })->second);
			}
			else
			{
				extendAndWrite1DDataSet(dsets_.find({ v, presetScanConfigurationIndex })->second, d1);
			}
		}

		void Connection_triMS5::flush(const DataSetType_triMS5& v, unsigned int presetScanConfigurationIndex)
		{
			auto  it2 = buffers_.find({ v, presetScanConfigurationIndex });
			if (it2 == buffers_.end())
			{
				return;
			}
			auto it = dsets_.find({ v, presetScanConfigurationIndex });
			extendAndWrite1DDataSet(it->second, it2->second);
			it2->second.clear();
		}

		void Connection_triMS5::close()
		{
			if (!closed_)
			{
				//writing part
				{
					boost::mutex::scoped_lock lock(connectionWriteMutex_);
					for (auto it = buffers_.begin(); it != buffers_.end(); ++it)
					{
						if (it->second.size() > 0)
						{
							flush(it->first.first, it->first.second);
						}
					}
				}
				//reading part
				{
					boost::mutex::scoped_lock lock(connectionReadMutex_);
					for (auto it = dsets_.begin(); it != dsets_.end(); ++it)
					{
						it->second.close();
					}
					file_->flush(H5F_SCOPE_LOCAL);
					file_->close();
				}
				
				
				closed_ = true;
			}
		}

		void * Connection_triMS5::readDataSet(const DataSetType_triMS5 v, size_t & dsend, unsigned int presetScanConfigurationIndex, void * ptr)
		{
			boost::mutex::scoped_lock lock(connectionReadMutex_);

			H5::Group father = openGroup(config_.getGroupTypeFor(v), presetScanConfigurationIndex);

			H5::DataSet dset = father.openDataSet(config_.getNameFor(v));

			H5::DataSpace dspace(dset.getSpace());
			H5::DataType dtype = config_.getDataTypeFor(v);
			hsize_t start[1], end[1];
			dspace.getSelectBounds(start, end);
			dsend = (static_cast<size_t> (end[0])) + 1;

			if (ptr == 0)
			{
				ptr = calloc(dsend, dtype.getSize());
			}
			dset.read(ptr, dtype);
			dspace.close();
			dset.close();
			return ptr;
		}

		void Connection_triMS5::clean(const DataSetType_triMS5 v, void * buffer, const size_t dsend)
		{
			boost::mutex::scoped_lock lock(connectionReadMutex_);

			hsize_t dim[1] = { static_cast<hsize_t> (dsend) };
			DataSpace dsp(1, dim);
			DataSet::vlenReclaim(buffer, config_.getDataTypeFor(v), dsp);
			free(buffer);
			buffer = 0;
			dsp.close();
		}
	}
}
}
