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

#define PWIZ_SOURCE

#include "pwiz/utility/misc/Std.hpp"
#include "References.hpp"
#include "Serializer_mz5.hpp"
#include "SpectrumList_mz5.hpp"
#include "ChromatogramList_mz5.hpp"
#include "mz5/ReferenceWrite_mz5.hpp"
#include "mz5/ReferenceRead_mz5.hpp"


namespace pwiz {
namespace msdata {


using namespace mz5;


/**
 * This class allows reading and writing of MSData objects to mz5 files.
 */
class Serializer_mz5::Impl
{
    public:

    /**
     * Default constructor.
     * @param config mz5 configuration
     */
    Impl(const Configuration_mz5& config)
        : config_(config)
    {
    }

    /**
     * Writes MSData objects to a file in mz5 format.
     * @param filename file name
     * @param msd MSData object
     * @param iterationListenerRegistry progress listener
     */
    void write(const std::string& filename, const MSData& msd,
               const pwiz::util::IterationListenerRegistry* iterationListenerRegistry,
               bool useWorkerThreads) const;

    /**
     * Not supported.
     */
    void write(std::ostream& os, const MSData& msd,
               const pwiz::util::IterationListenerRegistry* iterationListenerRegistry) const;

    /**
     * Read a mz5 file.
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
    mutable Configuration_mz5 config_;

};

void Serializer_mz5::Impl::write(const std::string& filename,
                                 const MSData& msd,
                                 const pwiz::util::IterationListenerRegistry* iterationListenerRegistry,
                                 bool useWorkerThreads) const
{
    ReferenceWrite_mz5 wref(msd);
    Connection_mz5 con(filename, Connection_mz5::RemoveAndCreate, config_);
    wref.writeTo(con, iterationListenerRegistry, useWorkerThreads);
}

void Serializer_mz5::Impl::write(std::ostream& os, const MSData& msd,
                                 const pwiz::util::IterationListenerRegistry* iterationListenerRegistry) const
{
    throw std::runtime_error(
            "[Serializer_mz5::write()] MZ5 does not support writing with stream.");
}

void Serializer_mz5::Impl::read(const std::string& filename, MSData& msd) const
{
    boost::shared_ptr<Connection_mz5> connectionPtr(new Connection_mz5(
            filename, Connection_mz5::ReadOnly, config_));
    boost::shared_ptr<ReferenceRead_mz5> rrefptr(new ReferenceRead_mz5(msd));
    rrefptr->fill(connectionPtr);

    if (connectionPtr->getFields().find(Configuration_mz5::SpectrumMetaData)
            != connectionPtr->getFields().end())
    {
        SpectrumListPtr ptr = SpectrumList_mz5::create(rrefptr, connectionPtr, msd);
        msd.run.spectrumListPtr = ptr;
    }

    if (connectionPtr->getFields().find(Configuration_mz5::ChromatogramMetaData)
            != connectionPtr->getFields().end())
    {
        ChromatogramListPtr ptr = ChromatogramList_mz5::create(rrefptr, connectionPtr, msd);
        msd.run.chromatogramListPtr = ptr;
    }
    References::resolve(msd);
}

void Serializer_mz5::Impl::read(boost::shared_ptr<std::istream> is, MSData& msd) const
{
    throw std::runtime_error("[Serializer_mz5::read()] MZ5 does not support reading with stream.");
}


//
// Serializer_mz5
//

PWIZ_API_DECL Serializer_mz5::Serializer_mz5(const Configuration_mz5& config)
    : impl_(new Impl(config))
{
}

PWIZ_API_DECL Serializer_mz5::Serializer_mz5(const pwiz::msdata::MSDataFile::WriteConfig& config)
    : impl_(new Impl(Configuration_mz5(config)))
{
}

PWIZ_API_DECL
void Serializer_mz5::write(const std::string& filename, const MSData& msd,
                           const pwiz::util::IterationListenerRegistry* iterationListenerRegistry,
                           bool useWorkerThreads) const
{
    return impl_->write(filename, msd, iterationListenerRegistry, useWorkerThreads);
}

PWIZ_API_DECL
void Serializer_mz5::write(std::ostream& os, const MSData& msd,
                           const pwiz::util::IterationListenerRegistry* iterationListenerRegistry) const
{
    return impl_->write(os, msd, iterationListenerRegistry);
}

PWIZ_API_DECL void Serializer_mz5::read(const std::string& filename, MSData& msd) const
{
    return impl_->read(filename, msd);
}

PWIZ_API_DECL void Serializer_mz5::read(boost::shared_ptr<std::istream> is, MSData& msd) const
{
    return impl_->read(is, msd);
}


} // namespace msdata
} // namespace pwiz
