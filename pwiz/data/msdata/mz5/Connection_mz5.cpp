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

#include "Connection_mz5.hpp"
#include "ReferenceWrite_mz5.hpp"
#include "ReferenceRead_mz5.hpp"
#include "Translator_mz5.hpp"
#include <algorithm>
#include "boost/thread/mutex.hpp"

namespace pwiz {
namespace msdata {
namespace mz5 {

using namespace H5;

namespace {boost::mutex connectionReadMutex_, connectionWriteMutex_;}

Connection_mz5::Connection_mz5(const std::string filename, const OpenPolicy op,
        const Configuration_mz5 config) :
    config_(config)
{
    boost::mutex::scoped_lock lock(connectionReadMutex_);

    FileCreatPropList fcparm = FileCreatPropList::DEFAULT;
    FileAccPropList faparm = FileAccPropList::DEFAULT;
    if (op == ReadWrite || op == ReadOnly)
    {
        int mds_nelemts;
        size_t rdcc_nelmts, rdcc_nbytes;
        double rdcc_w0;
        faparm.getCache(mds_nelemts, rdcc_nelmts, rdcc_nbytes, rdcc_w0);
        //TODO do not set global buffer size, instead set dataset specific buffer size
        rdcc_nbytes = config_.getBufferInB();
        // TODO can be set to 1 if chunks that have been fully read/written will never be read/written again
        //  rdcc_w0 = 1.0;
        rdcc_nelmts = config_.getRdccSlots();
        faparm.setCache(mds_nelemts, rdcc_nelmts, rdcc_nbytes, rdcc_w0);
    }

    unsigned int openFlag = H5F_ACC_TRUNC;
    switch (op)
    {
    case RemoveAndCreate:
        openFlag = H5F_ACC_TRUNC;
        file_ = new H5File(filename, openFlag, fcparm, faparm);
        break;
    case FailIfFileExists:
        openFlag = H5F_ACC_EXCL;
        file_ = new H5File(filename, openFlag, fcparm, faparm);
        break;
    case ReadWrite:
        openFlag = H5F_ACC_RDWR;
        file_ = new H5File(filename, openFlag, fcparm, faparm);
        readFile();
        break;
    case ReadOnly:
        openFlag = H5F_ACC_RDONLY;
        file_ = new H5File(filename, openFlag, fcparm, faparm);
        readFile();
        break;
    default:
        break;
    }
    closed_ = false;
}

Connection_mz5::~Connection_mz5()
{
    close();
}

DSetCreatPropList Connection_mz5::getCParm(int rank,
        const Configuration_mz5::MZ5DataSets& v, const hsize_t& datadim)
{
    DSetCreatPropList cparm;
    if (config_.getChunkSizeFor(v) != Configuration_mz5::EMPTY_CHUNK_SIZE)
    {
        hsize_t chunks[1] =
        { std::min(config_.getChunkSizeFor(v), datadim) };
        cparm.setChunk(rank, chunks);
        if (config_.doShuffel())
        {
            cparm.setShuffle();
        }
        if (config_.getDeflateLvl() != 0)
        {
            cparm.setDeflate(config_.getDeflateLvl());
        }
    }
    return cparm;
}

DataSet Connection_mz5::getDataSet(int rank, hsize_t* dim, hsize_t* maxdim,
        const Configuration_mz5::MZ5DataSets v)
{
    DSetCreatPropList cparms = getCParm(rank, v, maxdim[0]);
    DataSpace dataSpace(rank, dim, maxdim);
    DataSet dataset = file_->createDataSet(config_.getNameFor(v),
            config_.getDataTypeFor(v), dataSpace, cparms);
    dataSpace.close();
    return dataset;
}

void Connection_mz5::createAndWrite1DDataSet(hsize_t size, void* data,
        const Configuration_mz5::MZ5DataSets v)
{
    boost::mutex::scoped_lock lock(connectionWriteMutex_);
    if (size > 0)
    {
        hsize_t dim[1] =
        { size };
        hsize_t maxdim[1] =
        { size };
        DataSet dataset = getDataSet(1, dim, maxdim, v);
        dataset.write(data, config_.getDataTypeFor(v));
        dataset.close();
    }
}

void Connection_mz5::readFile()
{
    hsize_t start[1], end[1];
    size_t dsend = 0;
    DataSet dataset;
    DataSpace dataspace;
    std::string oname;
    Configuration_mz5::MZ5DataSets v;
    for (hsize_t i = 0; i < file_->getNumObjs(); ++i)
    {
        oname = file_->getObjnameByIdx(i);
        dataset = file_->openDataSet(oname);
        dataspace = dataset.getSpace();
        dataspace.getSelectBounds(start, end);
        dsend = (static_cast<size_t> (end[0])) + 1;
        try
        {
            v = config_.getVariableFor(oname);
            fields_.insert(std::pair<Configuration_mz5::MZ5DataSets, size_t>(v,
                    dsend));
        } catch (std::out_of_range&)
        {
        }
        dataspace.close();
        dataset.close();
    }

    std::map<Configuration_mz5::MZ5DataSets, size_t>::const_iterator it;
    it = fields_.find(Configuration_mz5::FileInformation);
    if (it != fields_.end())
    {
        DataSet ds = file_->openDataSet(config_.getNameFor(
                Configuration_mz5::FileInformation));
        DataSpace dsp = ds.getSpace();
        hsize_t start[1], end[1];
        dsp.getSelectBounds(start, end);
        dsend = (static_cast<size_t> (end[0])) + 1;
        DataType dt =
                config_.getDataTypeFor(Configuration_mz5::FileInformation);
        FileInformationMZ5* fi = (FileInformationMZ5*) (calloc(dsend,
                dt.getSize()));
        ds.read(fi, dt);
        dsp.close();
        ds.close();

        if (dsend == 1)
        {
            if (fi[0].majorVersion == Configuration_mz5::MZ5_FILE_MAJOR_VERSION
                    && fi[0].minorVersion
                            == Configuration_mz5::MZ5_FILE_MINOR_VERSION)
            {
                config_.setTranslating(fi[0].deltaMZ && fi[0].translateInten);
            }
        }
        hsize_t dim[1] =
        { static_cast<hsize_t> (dsend) };
        DataSpace dspr(1, dim);
        DataSet::vlenReclaim(fi, config_.getDataTypeFor(
                Configuration_mz5::FileInformation), dspr);
        free(fi);
        fi = 0;
        dspr.close();
    }
    else
    {
        it = fields_.find(Configuration_mz5::Run);
        if (it == fields_.end())
        {
            throw std::runtime_error(
                    "Connection_mz5::constructor(): given file is no mz5 file.");
        }
    }
}

void Connection_mz5::addToBuffer(std::vector<double>& b, const std::vector<
        double>& d1, const size_t bs, const DataSet& dataset)
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

void Connection_mz5::extendData(const std::vector<double>& d1,
        const Configuration_mz5::MZ5DataSets v)
{
    boost::mutex::scoped_lock lock(connectionWriteMutex_);
    size_t bs = config_.getBufferSizeFor(v);
    std::map<Configuration_mz5::MZ5DataSets, DataSet>::iterator it =
            bufferMap_.find(v);
    if (it == bufferMap_.end())
    {
        hsize_t dim[1] =
        { 0 };
        hsize_t maxdim[1] =
        { H5S_UNLIMITED };
        it = bufferMap_.insert(std::pair<Configuration_mz5::MZ5DataSets, DataSet>(v,
                getDataSet(1, dim, maxdim, v))).first;
        if (bs != Configuration_mz5::NO_BUFFER_SIZE) {
            buffers_.insert(std::pair<Configuration_mz5::MZ5DataSets,
                std::vector<double> >(v, std::vector<double>()));
            buffers_.find(v)->second.reserve(bs);
        }
    }
    if (bs != Configuration_mz5::NO_BUFFER_SIZE)
    {
        addToBuffer(buffers_.find(v)->second, d1, bs, it->second);
    }
    else
    {
        extendAndWrite1DDataSet(it->second, d1);
    }
}

void Connection_mz5::extendAndWrite1DDataSet(const DataSet& dataset,
        const std::vector<double>& d1)
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

const std::map<Configuration_mz5::MZ5DataSets, size_t>& Connection_mz5::getFields()
{
    return fields_;
}

void *Connection_mz5::readDataSet(const Configuration_mz5::MZ5DataSets v,
        size_t& dsend, void* ptr)
{
    boost::mutex::scoped_lock lock(connectionReadMutex_);

    DataSet ds = file_->openDataSet(config_.getNameFor(v));
    DataSpace dsp = ds.getSpace();
    hsize_t start[1], end[1];
    dsp.getSelectBounds(start, end);
    dsend = (static_cast<size_t> (end[0])) + 1;
    DataType dt = config_.getDataTypeFor(v);
    if (ptr == 0)
    {
        ptr = calloc(dsend, dt.getSize());
    }
    ds.read(ptr, dt);
    dsp.close();
    ds.close();
    return ptr;
}

void Connection_mz5::clean(const Configuration_mz5::MZ5DataSets v, void* data,
        const size_t dsend)
{
    boost::mutex::scoped_lock lock(connectionReadMutex_);

    hsize_t dim[1] =
    { static_cast<hsize_t> (dsend) };
    DataSpace dsp(1, dim);
    DataSet::vlenReclaim(data, config_.getDataTypeFor(v), dsp);
    free(data);
    data = 0;
    dsp.close();
}

void Connection_mz5::getData(std::vector<double>& data,
        const Configuration_mz5::MZ5DataSets v, const hsize_t start,
        const hsize_t end)
{
    boost::mutex::scoped_lock lock(connectionReadMutex_);

    hsize_t scount = end - start;
    data.resize(scount);
    if (scount > 0)
    {
        std::map<Configuration_mz5::MZ5DataSets, DataSet>::iterator it =
                bufferMap_.find(v);
        if (it == bufferMap_.end())
        {
            DataSet ds = file_->openDataSet(config_.getNameFor(v));
            bufferMap_.insert(
                    std::pair<Configuration_mz5::MZ5DataSets, DataSet>(v, ds));
            it = bufferMap_.find(v);
        }
        DataSet dataset = it->second;
        DataSpace dataspace = dataset.getSpace();
        hsize_t offset[1];
        offset[0] = start;
        hsize_t count[1];
        count[0] = scount;
        dataspace.selectHyperslab(H5S_SELECT_SET, count, offset);

        hsize_t dimsm[1];
        dimsm[0] = scount;
        DataSpace memspace(1, dimsm);

        dataset.read(&data[0], PredType::NATIVE_DOUBLE, memspace, dataspace);
        if (v == Configuration_mz5::SpectrumMZ && config_.doTranslating())
        {
            Translator_mz5::reverseTranslateMZ(data);
        }
        if (v == Configuration_mz5::SpectrumIntensity
                && config_.doTranslating())
        {
            Translator_mz5::reverseTranslateIntensity(data);
        }
        memspace.close();
        dataspace.close();
    }
}

void Connection_mz5::flush(const Configuration_mz5::MZ5DataSets v)
{
    size_t bs = config_.getBufferSizeFor(v);
    if (bs != Configuration_mz5::NO_BUFFER_SIZE)
    {
        std::map<Configuration_mz5::MZ5DataSets, std::vector<double> >::iterator
                it2 = buffers_.find(v);
        if (it2 == buffers_.end())
        {
            return;
        }
        std::map<Configuration_mz5::MZ5DataSets, DataSet>::iterator it =
                bufferMap_.find(v);
        extendAndWrite1DDataSet(it->second, it2->second);
        it2->second.clear();
    }
}

const Configuration_mz5& Connection_mz5::getConfiguration()
{
    return config_;
}

void Connection_mz5::close()
{
    if (!closed_)
    {
        {
            boost::mutex::scoped_lock lock(connectionWriteMutex_);
            for (std::map<Configuration_mz5::MZ5DataSets, std::vector<double> >::iterator
                    it = buffers_.begin(); it != buffers_.end(); ++it)
            {
                if (it->second.size() > 0)
                {
                    flush(it->first);
                }
            }
        }
        {
            boost::mutex::scoped_lock lock(connectionReadMutex_);
            for (std::map<Configuration_mz5::MZ5DataSets, DataSet>::iterator
                    it = bufferMap_.begin(); it != bufferMap_.end(); ++it)
            {
                it->second.close();
            }
            file_->flush(H5F_SCOPE_LOCAL);
            file_->close();
        }
        delete file_;
        file_ = 0;
        closed_ = true;
    }
}

}
}
}
