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


#ifndef _SERIALIZER_MZ5_HPP_
#define _SERIALIZER_MZ5_HPP_


#include "MSData.hpp"
#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/utility/misc/IterationListener.hpp"
#include "mz5/Configuration_mz5.hpp"


namespace pwiz {
namespace msdata {


/// MSData <-> MZ5 file serialization
class PWIZ_API_DECL Serializer_mz5
{
    public:

    /**
     * Default constructor.
     * @param config mz5 configuration containing dataset names and different parameters for write support.
     */
    Serializer_mz5(const mz5::Configuration_mz5& config = mz5::Configuration_mz5());

    /**
     * Uses config to generate a Configuration_mz5 instance.
     * @param config pwiz configuration
     */
    Serializer_mz5(const pwiz::msdata::MSDataFile::WriteConfig& config);

    /**
     * Creates and writes MSData instances to a mz5 file.
     * @param filename file name
     * @param msd MSData object
     * @param iterationListenerRegistry progress listener
     */
    void write(const std::string& filename, const MSData& msd,
               const pwiz::util::IterationListenerRegistry* iterationListenerRegistry = 0) const;

    /// This method is not supported by mz5 since mz5 can not write to ostreams.
    void write(std::ostream& os, const MSData& msd,
               const pwiz::util::IterationListenerRegistry* iterationListenerRegistry = 0) const;

    /**
     * Reads the mz5 file and stores the information in the MSData object.
     * @þaram filename file anme
     * @param msd MSData object
     */
    void read(const std::string& filename, MSData& msd) const;

    /// This method is not supported by mz5 since mz5 can not read from an istream.
    void read(boost::shared_ptr<std::istream> is, MSData& msd) const;

    private:
    class Impl;
    boost::shared_ptr<Impl> impl_;
    Serializer_mz5(Serializer_mz5&);
    Serializer_mz5& operator=(Serializer_mz5&);
};


} // namespace msdata
} // namespace pwiz


#endif // _SERIALIZER_MZ5_HPP_
