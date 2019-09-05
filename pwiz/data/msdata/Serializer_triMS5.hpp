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
#ifndef _SERIALIZER_TRIMS5_HPP_
#define _SERIALIZER_TRIMS5_HPP_


#include "MSData.hpp"
#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/utility/misc/IterationListener.hpp"
#include "Configuration_triMS5.hpp"


namespace pwiz {
namespace msdata {


/// MSData <-> triMS5 file serialization
class PWIZ_API_DECL Serializer_triMS5
{
    public:

    /**
     * Default constructor.
     * @param config triMS5 configuration containing dataset names and different parameters for write support.
     */
		Serializer_triMS5(const triMS5::Configuration_triMS5 & config = triMS5::Configuration_triMS5());

    /**
     * Uses config to generate a Configuration_triMS5 instance.
     * @param config pwiz configuration
     */
		Serializer_triMS5(const pwiz::msdata::MSDataFile::WriteConfig& config);

    /**
     * Creates and writes MSData instances to a triMS5 file.
     * @param filename file name
     * @param msd MSData object
     * @param iterationListenerRegistry progress listener
     */
    void write(const std::string& filename, const MSData& msd, const pwiz::util::IterationListenerRegistry* iterationListenerRegistry = 0) const;

    /// This method is not supported by triMS5 since triMS5 can not write to ostreams.
    void write(std::ostream& os, const MSData& msd, const pwiz::util::IterationListenerRegistry* iterationListenerRegistry = 0) const;

    /**
     * Reads the triMS5 file and stores the information in the MSData object.
     * @þaram filename file anme
     * @param msd MSData object
     */
    void read(const std::string& filename, MSData& msd) const;

    /// This method is not supported by triMS5 since triMS5 can not read from an istream.
    void read(boost::shared_ptr<std::istream> is, MSData& msd) const;

    private:
    class Impl;
    boost::shared_ptr<Impl> impl_;
	Serializer_triMS5(Serializer_triMS5&);
	Serializer_triMS5& operator=(Serializer_triMS5&);
};


} // namespace msdata
} // namespace pwiz


#endif // _SERIALIZER_TRIMS5_HPP_
