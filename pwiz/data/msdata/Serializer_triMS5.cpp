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
#include "Serializer_triMS5.hpp"
#include "SpectrumList_triMS5.hpp"
#include "ChromatogramList_triMS5.hpp"
#include "triMS5/ReferenceWrite_triMS5.hpp"
#include "triMS5/ReferenceRead_triMS5.hpp"
#include "References.hpp"

namespace pwiz {
namespace msdata {

	using namespace triMS5;

/**
 * This class allows reading and writing of MSData objects to triMS5 files.
 */
class Serializer_triMS5::Impl
{
    public:

    /**
     * Default constructor.
     * @param config triMS5 configuration
     */
    Impl(const Configuration_triMS5& config) : config_(config) {}


    /**
     * Writes MSData objects to a file in triMS5 format.
     * @param filename file name
     * @param msd MSData object
     * @param iterationListenerRegistry progress listener
     */
    void write(const std::string& filename, const MSData& msd, const pwiz::util::IterationListenerRegistry* iterationListenerRegistry) const;


    /**
     * Not supported.
     */
    void write(std::ostream& os, const MSData& msd, const pwiz::util::IterationListenerRegistry* iterationListenerRegistry) const;


    /**
     * Read a triMS5 file.
     * @param filename file name
     * @param msd MSData object
     */
    void read(const std::string& filename, MSData& msd) const;


    /**
     * Not supported.
     */
    void read(boost::shared_ptr<std::istream> is, MSData& msd) const;


private:
    /**
     * Internal configuration instance.
     */
    mutable Configuration_triMS5 config_;

};

void Serializer_triMS5::Impl::write(const std::string& filename, const MSData& msd, const pwiz::util::IterationListenerRegistry* iterationListenerRegistry) const
{
	ReferenceWrite_triMS5 wref(msd);
    Connection_triMS5 con(filename, Connection_triMS5::OpenPolicy::RemoveAndCreate, config_);
    wref.writeTo(con, iterationListenerRegistry);
}

void Serializer_triMS5::Impl::write(std::ostream& os, const MSData& msd, const pwiz::util::IterationListenerRegistry* iterationListenerRegistry) const
{
    throw std::runtime_error("[Serializer_triMS5::write()] triMS55 does not support writing with stream.");
}


void Serializer_triMS5::Impl::read(const std::string& filename, MSData& msd) const
{
    boost::shared_ptr<Connection_triMS5> connectionPtr(new Connection_triMS5(filename, Connection_triMS5::OpenPolicy::ReadWrite, config_));

    boost::shared_ptr<ReferenceRead_triMS5> rrefptr(new ReferenceRead_triMS5(msd));
    rrefptr->fill(connectionPtr);

	if (connectionPtr->getAvailableDataSets().find({ DataSetType_triMS5::SpectrumMetaData, 1 }) != connectionPtr->getAvailableDataSets().end())
    {
        SpectrumListPtr ptr = SpectrumList_triMS5::create(rrefptr, connectionPtr, msd);
        msd.run.spectrumListPtr = ptr;
    }

    if (connectionPtr->getAvailableDataSets().find({ DataSetType_triMS5::ChromatogramMetaData, 1 }) != connectionPtr->getAvailableDataSets().end())
    {
        ChromatogramListPtr ptr = ChromatogramList_triMS5::create(rrefptr, connectionPtr, msd);
        msd.run.chromatogramListPtr = ptr;
    }
    References::resolve(msd);
}


void Serializer_triMS5::Impl::read(boost::shared_ptr<std::istream> is, MSData& msd) const
{
    throw std::runtime_error("[Serializer_triMS5::read()] triMS5 does not support reading with stream.");
}


//
// Serializer_triMS5
//

PWIZ_API_DECL Serializer_triMS5::Serializer_triMS5(const Configuration_triMS5& config) : impl_(new Impl(config)) { }


PWIZ_API_DECL Serializer_triMS5::Serializer_triMS5(const pwiz::msdata::MSDataFile::WriteConfig& config) : impl_(new Impl(Configuration_triMS5(config))) { }


PWIZ_API_DECL
void Serializer_triMS5::write(const std::string& filename, const MSData& msd, const pwiz::util::IterationListenerRegistry* iterationListenerRegistry) const
{
    return impl_->write(filename, msd, iterationListenerRegistry);
}


PWIZ_API_DECL
void Serializer_triMS5::write(std::ostream& os, const MSData& msd, const pwiz::util::IterationListenerRegistry* iterationListenerRegistry) const
{
    return impl_->write(os, msd, iterationListenerRegistry);
}


PWIZ_API_DECL void Serializer_triMS5::read(const std::string& filename, MSData& msd) const
{
    return impl_->read(filename, msd);
}


PWIZ_API_DECL void Serializer_triMS5::read(boost::shared_ptr<std::istream> is, MSData& msd) const
{
    return impl_->read(is, msd);
}


} // namespace msdata
} // namespace pwiz
