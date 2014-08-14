//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
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


#ifndef _SERIALIZER_MZXML_HPP_
#define _SERIALIZER_MZXML_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "MSData.hpp"
#include "BinaryDataEncoder.hpp"
#include "pwiz/utility/misc/IterationListener.hpp"


namespace pwiz {
namespace msdata {


/// MSData <-> mzXML stream serialization
class PWIZ_API_DECL Serializer_mzXML
{
    public:

    /// Serializer_mzXML configuration
    struct PWIZ_API_DECL Config
    {
        /// configuration for binary data encoding in write()
        /// note: byteOrder is ignored (mzXML always big endian) 
        BinaryDataEncoder::Config binaryDataEncoderConfig;

        /// (indexed==true): read/write with <index>
        bool indexed;

        Config() : indexed(true) {}
    };

    /// constructor
    Serializer_mzXML(const Config& config = Config());

    /// write MSData object to ostream as mzXML;
    /// iterationListenerRegistry may be used to receive progress updates
    void write(std::ostream& os, const MSData& msd,
               const pwiz::util::IterationListenerRegistry* iterationListenerRegistry = 0) const;

    /// read in MSData object from an mzXML istream 
    /// note: istream may be managed by MSData's SpectrumList, to allow for 
    /// lazy evaluation of Spectrum data
    void read(boost::shared_ptr<std::istream> is, MSData& msd) const;

    private:
    class Impl;
    boost::shared_ptr<Impl> impl_;
    Serializer_mzXML(Serializer_mzXML&);
    Serializer_mzXML& operator=(Serializer_mzXML&);
};


PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const Serializer_mzXML::Config& config);


} // namespace msdata
} // namespace pwiz


#endif // _SERIALIZER_MZXML_HPP_

