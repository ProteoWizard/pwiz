//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
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

#ifndef _SERIALIZER_MSN_HPP_
#define _SERIALIZER_MSN_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "MSData.hpp"
#include "BinaryDataEncoder.hpp"
#include "pwiz/utility/misc/IterationListener.hpp"


namespace pwiz {
namespace msdata {


/// MSData <-> MSn stream serialization
class PWIZ_API_DECL Serializer_MSn
{
    public:

    /// constructor
    Serializer_MSn();

    /// write MSData object to ostream as MS1 or MS2, depending on what kind of spectrum types
    /// are in fileContent: if only MS1 or if both MS1 and MSn spectra, the output is MS1
    /// (MSn spectra are skipped), otherwise the output is MS2 (MS1 spectra are skipped);
    /// iterationListenerRegistry may be used to receive progress updates
    void write(std::ostream& os, const MSData& msd,
               const pwiz::util::IterationListenerRegistry* iterationListenerRegistry = 0) const;

    /// read in MSData object from an MS1 or MS2 istream
    /// note: istream may be managed by MSData's SpectrumList, to allow for 
    /// lazy evaluation of Spectrum data
    void read(boost::shared_ptr<std::istream> is, MSData& msd) const;

    private:
    class Impl;
    boost::shared_ptr<Impl> impl_;
    Serializer_MSn(Serializer_MSn&);
    Serializer_MSn& operator=(Serializer_MSn&);
};


} // namespace msdata
} // namespace pwiz


#endif // _SERIALIZER_MSN_HPP_
